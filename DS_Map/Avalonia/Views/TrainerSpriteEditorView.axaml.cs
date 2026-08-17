using System.ComponentModel;
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

        private bool _animEditorReady;
        private bool _animSyncing;   // guards the AnimJsonEditor⇄VM text loop
        private TrainerSpriteEditorViewModel _hookedVm;

        public TrainerSpriteEditorView()
        {
            InitializeComponent();
            Loaded += (_, _) => SetupAnimEditor();
            DataContextChanged += (_, _) => HookVm();
            Closing += (_, _) => VM?.StopAnimPreview();
        }

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

        // The outer "Animations" tab and its inner "JSON" sub-tab lazily realize content only once
        // selected, so AnimJsonEditor can still be null at window load. Retry on either tab strip's
        // selection change; SetupAnimEditor is a no-op once already wired.
        private void OuterTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!ReferenceEquals(e.Source, OuterTabs)) return;
            SetupAnimEditor();
        }

        private void AnimInnerTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!ReferenceEquals(e.Source, AnimInnerTabs)) return;
            SetupAnimEditor();
        }

        // ── Animations tab (AvaloniaEdit) wiring: live two-way text sync with the VM ──────────────
        private void SetupAnimEditor()
        {
            if (_animEditorReady || AnimJsonEditor == null) return;
            _animEditorReady = true;
            AnimJsonEditor.TextChanged += (_, _) =>
            {
                if (_animSyncing || VM == null) return;
                VM.AnimJsonText = AnimJsonEditor.Text;
            };
            HookVm();
            PushAnimTextToEditor();
        }

        private void HookVm()
        {
            var vm = VM;
            if (ReferenceEquals(vm, _hookedVm)) return;
            if (_hookedVm != null) _hookedVm.PropertyChanged -= OnVmChanged;
            _hookedVm = vm;
            if (vm != null) vm.PropertyChanged += OnVmChanged;
            if (_animEditorReady) PushAnimTextToEditor();
        }

        private void OnVmChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TrainerSpriteEditorViewModel.AnimJsonText))
                PushAnimTextToEditor();
        }

        private void PushAnimTextToEditor()
        {
            if (!_animEditorReady || VM == null) return;
            string text = VM.AnimJsonText ?? "";
            if (AnimJsonEditor.Text == text) return;   // no echo → keeps the user's caret while they type
            _animSyncing = true;
            AnimJsonEditor.Text = text;
            _animSyncing = false;
        }

        private async void SaveAnimJson_Click(object sender, RoutedEventArgs e)
        {
            string error = VM?.SaveAnimJson();
            if (error != null)
                await DialogHelper.ShowError($"Save failed: {error}", owner: this);
        }

        private async void CreateAnimJson_Click(object sender, RoutedEventArgs e)
        {
            string error = VM?.CreateAnimJson();
            if (error != null)
                await DialogHelper.ShowError($"Create failed: {error}", owner: this);
        }

        // ── Structured animation editor: sequence + frame list + play-once preview ───────────────
        private void AddFrame_Click(object sender, RoutedEventArgs e) => VM?.AddAnimFrame();

        private async void RemoveFrame_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Control)?.DataContext is not AnimFrameRowViewModel row) return;
            string error = VM?.RemoveAnimFrame(row);
            if (error != null)
                await DialogHelper.ShowError(error, owner: this);
        }

        private void MoveFrameUp_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Control)?.DataContext is AnimFrameRowViewModel row)
                VM?.MoveAnimFrame(row, -1);
        }

        private void MoveFrameDown_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Control)?.DataContext is AnimFrameRowViewModel row)
                VM?.MoveAnimFrame(row, +1);
        }

        private void AddSequence_Click(object sender, RoutedEventArgs e) => VM?.AddAnimSequence();

        private async void RemoveSequence_Click(object sender, RoutedEventArgs e)
        {
            string error = VM?.RemoveAnimSequence();
            if (error != null)
                await DialogHelper.ShowError(error, owner: this);
        }

        private async void PlayAnimPreview_Click(object sender, RoutedEventArgs e)
        {
            if (VM != null) await VM.PlayAnimPreviewOnceAsync();
        }
    }
}
