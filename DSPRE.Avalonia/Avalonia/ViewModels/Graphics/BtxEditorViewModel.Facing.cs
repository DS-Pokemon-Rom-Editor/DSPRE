using Avalonia.Controls;
using Avalonia.Media.Imaging;
using DSPRE.Editors;
using DSPRE.LibNDSFormats;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using static DSPRE.RomInfo;
using Avalonia.Threading;
using DSPRE.Avalonia.Data;
using DSPRE.ROMFiles;
using DSPRE.HgEngine;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    public partial class BtxEditorViewModel
    {
        public ObservableCollection<FacingPreview> Facings { get; } = new();
        public bool HasFacings => Facings.Count > 0;

        private readonly List<Bitmap> _pictures = new();
        private readonly FieldWalkCycle _walk = new(), _run = new();
        private DispatcherTimer _previewTimer;

        private void BuildFacings()
        {
            Facings.Clear();
            _pictures.Clear();
            if (_btxData != null && HasSelectedEntry)
            {
                try
                {
                    foreach (OverworldSprites.SpritePixels p in OverworldSprites.Pictures(_btxData, _isShiny && HasShinyPalette ? 1 : 0))
                        _pictures.Add(p != null ? ImageConverter.FromRgba(p.Rgba, p.Width, p.Height) : null);
                }
                catch (Exception ex)
                {
                    AppLogger.Error("Overworld pictures couldn't be read: " + ex.Message);
                    _pictures.Clear();
                }
            }

            int count = _pictures.Count;
            if (count >= 4)
            {
                int per = FieldSpriteAnimation.PerFacing(count);
                Bitmap first = _pictures.FirstOrDefault(b => b != null);
                double width = (first?.PixelSize.Width ?? 32) * 3, height = (first?.PixelSize.Height ?? 32) * 3;
                foreach ((int facing, string label) in new[] { (1, "Down"), (0, "Up"), (2, "Left"), (3, "Right") })
                    Facings.Add(new FacingPreview
                    {
                        Label = label, Facing = facing, Width = width, Height = height,
                        HasWalking = per == 4,
                        HasRunning = per == 4 && count >= FieldSpriteAnimation.WalkingPictures * 2,
                    });
            }
            OnPropertyChanged(nameof(HasFacings));
            ShowFacingFrames();

            if (Facings.Count == 0) { _previewTimer?.Stop(); return; }
            if (_previewTimer == null)
            {
                // The field draws its people on a 30 Hz clock, and a normal step is 8 ticks.
                _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.0 / 30) };
                _previewTimer.Tick += (_, _) =>
                {
                    _walk.Tick();
                    _walk.Walk(8);
                    _run.Tick();
                    _run.Dash();
                    ShowFacingFrames();
                };
            }
            _previewTimer.Start();
        }

        private void ShowFacingFrames()
        {
            int count = _pictures.Count;
            bool pair = FieldSpriteAnimation.PerFacing(count) == 2;
            foreach (FacingPreview f in Facings)
            {
                f.Looking = PictureAt(FieldSpriteAnimation.PictureFor(count, f.Facing, pair ? _walk : null));
                if (f.HasWalking) f.Walking = PictureAt(FieldSpriteAnimation.PictureFor(count, f.Facing, _walk));
                if (f.HasRunning) f.Running = PictureAt(FieldSpriteAnimation.PictureFor(count, f.Facing, _run));
            }
        }

        private Bitmap PictureAt(int index) => index >= 0 && index < _pictures.Count ? _pictures[index] : null;

        public void StopPreview() => _previewTimer?.Stop();

        private bool _isShiny;
        public bool IsShiny
        {
            get => _isShiny;
            set { if (Set(ref _isShiny, value) && _btxData != null) RefreshImage(); }
        }

        private bool _hasShinyPalette;
        public bool HasShinyPalette { get => _hasShinyPalette; private set => Set(ref _hasShinyPalette, value); }
        public string ShinyPaletteNote => HasShinyPalette
            ? "This entry stores normal and shiny palettes."
            : "This entry stores only its normal palette.";

        private byte[] _btxData;
        private Dictionary<uint, byte[]> _modifiedFiles = new();
        private readonly Dictionary<uint, OverworldSpriteProfileMetadataPatch> _metadataPatches = new();
    }
}
