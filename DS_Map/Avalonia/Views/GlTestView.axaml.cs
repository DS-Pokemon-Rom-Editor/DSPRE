using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DSPRE.Avalonia;
using DSPRE.Avalonia.Gl;
using LibNDSFormats.NSBMD;
using LibNDSFormats.NSBTX;

namespace DSPRE.Avalonia.Views
{
    public partial class GlTestView : Window
    {
        private Point? _lastPointer;
        private NSBMD _currentNsbmd;

        public GlTestView()
        {
            InitializeComponent();

            GlView.ErrorChanged += (_, _) => UpdateStatus();
            // Pointer events are handled on the transparent host Border (hit-testable),
            // not on the bare GL control.
            GlHost.PointerPressed += OnPointerPressed;
            GlHost.PointerMoved += OnPointerMoved;
            GlHost.PointerReleased += OnPointerReleased;
            GlHost.PointerWheelChanged += OnWheel;
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            StatusLabel.Text = string.IsNullOrEmpty(GlView.LastError)
                ? "GL ready, rendering."
                : "GL error: " + GlView.LastError;
        }

        private void OnPointerPressed(object sender, PointerPressedEventArgs e)
        {
            _lastPointer = e.GetPosition(GlHost);
            e.Pointer.Capture(GlHost);
        }

        private void OnPointerReleased(object sender, PointerReleasedEventArgs e)
        {
            _lastPointer = null;
            e.Pointer.Capture(null);
        }

        private void OnPointerMoved(object sender, PointerEventArgs e)
        {
            if (_lastPointer is not Point last) return;
            var p = e.GetPosition(GlHost);
            GlView.OrbitByDrag((float)(p.X - last.X), (float)(p.Y - last.Y));
            _lastPointer = p;
        }

        private void OnWheel(object sender, PointerWheelEventArgs e)
            => GlView.ZoomByWheel((float)e.Delta.Y);

        private async void OpenNsbmd_Click(object sender, RoutedEventArgs e)
        {
            var filter = new FilePickerFileType("NSBMD model") { Patterns = new[] { "*.nsbmd", "*.bmd0", "*.*" } };
            string path = await DialogHelper.OpenFile(this, "Open NSBMD model", new[] { filter });
            if (path == null) return;

            try
            {
                NSBMD nsbmd;
                using (var fs = File.OpenRead(path)) nsbmd = NSBMDLoader.LoadNSBMD(fs);

                if (nsbmd?.models == null || nsbmd.models.Length == 0)
                {
                    StatusLabel.Text = "No models found in file.";
                    return;
                }

                _currentNsbmd = nsbmd;
                RebuildAndShow($"Loaded '{Path.GetFileName(path)}'");
            }
            catch (Exception ex)
            {
                StatusLabel.Text = "Load failed: " + ex.Message;
                AppLogger.Error("NSBMD load/build failed: " + ex);
            }
        }

        private async void OpenNsbtx_Click(object sender, RoutedEventArgs e)
        {
            if (_currentNsbmd == null) { StatusLabel.Text = "Open an NSBMD model first."; return; }

            var filter = new FilePickerFileType("NSBTX textures") { Patterns = new[] { "*.nsbtx", "*.btx0", "*.*" } };
            string path = await DialogHelper.OpenFile(this, "Bind NSBTX texture file", new[] { filter });
            if (path == null) return;

            try
            {
                using (var ms = new MemoryStream(File.ReadAllBytes(path)))
                    _currentNsbmd.materials = NSBTXLoader.LoadNsbtx(ms, out _currentNsbmd.Textures, out _currentNsbmd.Palettes);
                _currentNsbmd.MatchTextures();
                RebuildAndShow($"Bound textures '{Path.GetFileName(path)}'");
            }
            catch (Exception ex)
            {
                StatusLabel.Text = "NSBTX bind failed: " + ex.Message;
                AppLogger.Error("NSBTX bind failed: " + ex);
            }
        }

        private void RebuildAndShow(string prefix)
        {
            var renderModel = NsbmdGeometry.BuildModel(_currentNsbmd.models[0]);
            GlView.SetModel(renderModel);
            int vc = renderModel.TotalVertices;
            StatusLabel.Text = vc > 0
                ? $"{prefix}: {vc} verts ({vc / 3} tris), {renderModel.Parts.Count} parts, {renderModel.Textures.Count} textures."
                : $"{prefix}: parsed but produced no geometry.";
        }

        private void ResetCube_Click(object sender, RoutedEventArgs e)
        {
            GlView.ShowTestCube();
            StatusLabel.Text = "Showing self-test cube.";
        }
    }
}
