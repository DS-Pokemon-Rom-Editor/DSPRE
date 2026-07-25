using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DSPRE.Avalonia.ViewModels;
using System.Collections.Generic;

namespace DSPRE.Avalonia.Views
{
    public partial class TrainerSpriteEditorView : Window
    {
        private TrainerSpriteEditorViewModel VM => DataContext as TrainerSpriteEditorViewModel;

        public TrainerSpriteEditorView() => InitializeComponent();

        public TrainerSpriteEditorView(TrainerSpriteEditorViewModel vm) : this()
        {
            DataContext = vm;
            EditorWindowChrome.Attach(this, vm);
        }

        private void Pencil_Click(object sender, RoutedEventArgs e) => VM.SelectedTool = SpriteEditTool.Pencil;
        private void Eyedropper_Click(object sender, RoutedEventArgs e) => VM.SelectedTool = SpriteEditTool.Eyedropper;

        private void Swatch_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (sender is Control c && c.Tag is int index)
                VM.SelectedSwatchIndex = index;
        }

        private void Frame_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (sender is Control c && c.Tag is int index)
                VM.SelectedFrameIndex = index;
        }

        private bool _painting;

        private void Canvas_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            _painting = true;
            PaintAtPointer(e);
        }

        private void Canvas_PointerMoved(object sender, PointerEventArgs e)
        {
            if (!_painting || !e.GetCurrentPoint(CanvasImage).Properties.IsLeftButtonPressed)
            {
                _painting = false;
                return;
            }
            PaintAtPointer(e);
        }

        private void PaintAtPointer(PointerEventArgs e)
        {
            var vm = VM;
            if (vm == null) return;
            var pos = e.GetPosition(CanvasImage);
            int x = (int)(pos.X / vm.ZoomFactor);
            int y = (int)(pos.Y / vm.ZoomFactor);
            vm.HandlePointer(x, y);
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            string error = VM?.Save();
            if (error != null)
                await DialogHelper.ShowError($"Save failed: {error}", owner: this);
        }

        private async void Import_Click(object sender, RoutedEventArgs e)
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import PNG",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new FilePickerFileType("PNG Image") { Patterns = new[] { "*.png" } }
                }
            });
            if (files.Count == 0) return;
            string path = files[0].TryGetLocalPath();
            if (path == null) return;

            string error = VM?.ImportPng(path);
            if (error != null)
                await DialogHelper.ShowError($"Import failed: {error}", owner: this);
        }

        private async void Export_Click(object sender, RoutedEventArgs e)
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export PNG",
                DefaultExtension = "png",
                FileTypeChoices = new List<FilePickerFileType>
                {
                    new FilePickerFileType("PNG Image") { Patterns = new[] { "*.png" } }
                }
            });
            if (file == null) return;
            string path = file.TryGetLocalPath();
            if (path == null) return;

            if (VM?.ExportPng(path) == false)
                await DialogHelper.ShowError("Export failed.", owner: this);
        }
    }
}
