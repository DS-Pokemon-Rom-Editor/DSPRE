using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DSPRE.Avalonia.ViewModels;
using System.Collections.Generic;

namespace DSPRE.Avalonia.Views.Trainers
{
    public partial class TrainerSpriteEditorView : UserControl
    {
        private Window Owner => TopLevel.GetTopLevel(this) as Window;

        private TrainerSpriteEditorViewModel VM => DataContext as TrainerSpriteEditorViewModel;
        private string ErrorTitle => (VM?.Title is string t ? (t.EndsWith(" editor") ? t : t + " editor") : "Trainer sprite editor") + " error";

        private bool _animEditorReady;
        private bool _animSyncing;   // guards the AnimJsonEditor⇄VM text loop
        private TrainerSpriteEditorViewModel _hookedVm;

        public TrainerSpriteEditorView()
        {
            InitializeComponent();
            Loaded += (_, _) => SetupAnimEditor();
            DataContextChanged += (_, _) => HookVm();
            DetachedFromVisualTree += (_, _) => { VM?.StopAnimPreview(); VM?.StopTopBar(); };
            NameRefresh.Bind(this, () => VM?.RefreshNames());
        }

        public TrainerSpriteEditorView(TrainerSpriteEditorViewModel vm) : this()
        {
            DataContext = vm;
        }


        private void Swatch_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (sender is Control c && c.Tag is int index)
                VM.SelectedSwatchIndex = index;
        }

        private void Swatch_DoubleTapped(object sender, TappedEventArgs e)
        {
            if (sender is Control c && c.Tag is int index) OpenColourEditor(index);
        }

        private void EditColour_Click(object sender, RoutedEventArgs e) => OpenColourEditor(VM?.SelectedSwatchIndex ?? -1);

        // Takes the palette now so painting another part can't redirect the edit.
        private void OpenColourEditor(int index)
        {
            TrainerSpriteEditorViewModel vm = VM;
            if (vm == null || !vm.Loaded || index < 0) return;
            vm.SelectedSwatchIndex = index;
            int bank = vm.ActivePaletteBank;
            PaletteColorEditorViewModel editor = new DSPRE.Avalonia.ViewModels.Graphics.PaletteColorEditorViewModel(
                vm.PaletteTitle(bank, index), vm.SwatchColor(bank, index), (argb, group) => vm.SetSwatchColor(bank, index, argb, group));
            PaletteColorEditorView window = new DSPRE.Avalonia.Views.Graphics.PaletteColorEditorView(editor);
            // Outside the editor, so the palette and the sprite both stay in view while the colour changes.
            Window owner = Owner;
            if (owner == null) return;
            PixelRect? area = owner.Screens.ScreenFromWindow(owner)?.WorkingArea;
            double scale = owner.RenderScaling;
            PixelPoint right = this.PointToScreen(new Point(Bounds.Width + 8, 0));
            int width = (int)(window.Width * scale);
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Position = area == null || right.X + width <= area.Value.Right
                ? right
                : this.PointToScreen(new Point(-8 - window.Width, 0));
            window.Show(owner);
        }

        private void Frame_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (sender is Control c && c.Tag is int index)
                VM.SelectedFrameIndex = index;
        }

        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) VM?.DiscardChanges();
        }
        private void ZoomIn_Click(object sender, RoutedEventArgs e) => VM?.ZoomIn();
        private void ZoomOut_Click(object sender, RoutedEventArgs e) => VM?.ZoomOut();

        // Without Ctrl the wheel still scrolls a drawing bigger than the window.
        private void Canvas_PointerWheelChanged(object sender, PointerWheelEventArgs e)
        {
            TrainerSpriteEditorViewModel vm = VM;
            if (vm == null || !e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
            if (e.Delta.Y > 0) vm.ZoomIn(); else if (e.Delta.Y < 0) vm.ZoomOut();
            e.Handled = true;
        }

        private bool _painting;

        private void Canvas_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            _painting = true;
            VM?.BeginStroke();
            PaintAtPointer(e);
        }

        // A whole drag is one undo step.
        private void Canvas_PointerReleased(object sender, PointerReleasedEventArgs e)
        {
            _painting = false;
            VM?.EndStroke();
        }

        private void Canvas_PointerMoved(object sender, PointerEventArgs e)
        {
            if (!_painting || !e.GetCurrentPoint(CanvasImage).Properties.IsLeftButtonPressed)
            {
                _painting = false;
                VM?.EndStroke();
                return;
            }
            PaintAtPointer(e);
        }

        private void PaintAtPointer(PointerEventArgs e)
        {
            TrainerSpriteEditorViewModel vm = VM;
            if (vm == null) return;
            Point pos = e.GetPosition(CanvasImage);
            int x = (int)(pos.X / vm.ZoomFactor);
            int y = (int)(pos.Y / vm.ZoomFactor);
            vm.HandlePointer(x, y);
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            string error = VM?.SaveAll();
            if (error != null)
                await DialogHelper.ShowErrorAsync($"Couldn't save: {error}", ErrorTitle, Owner);
        }

        /// <summary>Opens All graphics at this sprite's drawing, the first of its five files.</summary>
        private void OpenInGraphics_Click(object sender, RoutedEventArgs e)
        {
            TrainerSpriteEditorViewModel vm = DataContext as DSPRE.Avalonia.ViewModels.Trainers.TrainerSpriteEditorViewModel;
            if (vm == null) return;
            DSPRE.Avalonia.AvaloniaEditorLauncher.OpenGraphicAt(vm.Archive, vm.SelectedClassIndex * 5);
        }

        private async void Import_Click(object sender, RoutedEventArgs e)
        {
            IReadOnlyList<IStorageFile> files = await TopLevel.GetTopLevel(this).StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import PNG",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new FilePickerFileType("PNG image") { Patterns = new[] { "*.png" } }
                }
            });
            if (files.Count == 0) return;
            string path = files[0].TryGetLocalPath();
            if (path == null) return;

            string error = VM?.ImportPng(path);
            if (error != null)
                await DialogHelper.ShowErrorAsync($"Couldn't import: {error}", ErrorTitle, Owner);
            else SaveNotice.Imported(null, path);
        }

        private async void CopyFrameToOtherSet_Click(object sender, RoutedEventArgs e)
        {
            string error = VM?.CopyFrameToOtherSet();
            if (error != null) await DialogHelper.ShowErrorAsync(error, ErrorTitle, Owner);
        }

        private async void Export_Click(object sender, RoutedEventArgs e)
        {
            IStorageFile file = await TopLevel.GetTopLevel(this).StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export PNG",
                DefaultExtension = "png",
                FileTypeChoices = new List<FilePickerFileType>
                {
                    new FilePickerFileType("PNG image") { Patterns = new[] { "*.png" } }
                }
            });
            if (file == null) return;
            string path = file.TryGetLocalPath();
            if (path == null) return;

            if (VM?.ExportPng(path) == false)
                await DialogHelper.ShowErrorAsync("Couldn't export.", ErrorTitle, Owner);
            else SaveNotice.Exported(path);
        }

        private void ExportSheet_Click(object sender, RoutedEventArgs e) => ShowSheetMenu((Control)sender, export: true);
        private void ImportSheet_Click(object sender, RoutedEventArgs e) => ShowSheetMenu((Control)sender, export: false);

        private void ShowSheetMenu(Control anchor, bool export)
        {
            if (VM == null) return;
            MenuFlyout flyout = new MenuFlyout();
            MenuItem frames = new MenuItem { Header = "Frames…" };
            frames.Click += async (_, _) => { if (export) await ExportSheetAsync(null); else await ImportSheetAsync(false); };
            flyout.Items.Add(frames);
            if (VM.CanUseAnimationSheets)
            {
                if (export)
                {
                    MenuItem animations = new MenuItem { Header = "Animation" };
                    foreach (TrainerSheetImportViewModel.AnimationChoice choice in VM.AnimationSheetChoices(false))
                    {
                        MenuItem item = new MenuItem { Header = choice.Label + "…" };
                        item.Click += async (_, _) => await ExportSheetAsync(choice.Index);
                        animations.Items.Add(item);
                    }
                    flyout.Items.Add(animations);
                }
                else
                {
                    MenuItem animation = new MenuItem { Header = "Animation…" };
                    animation.Click += async (_, _) => await ImportSheetAsync(true);
                    flyout.Items.Add(animation);
                }
            }
            flyout.ShowAt(anchor);
        }

        private async System.Threading.Tasks.Task ExportSheetAsync(int? animation)
        {
            IStorageFile file = await TopLevel.GetTopLevel(this).StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = animation == null ? "Export frames sheet" : "Export animation sheet",
                DefaultExtension = "png",
                SuggestedFileName = VM.SheetFileName(animation),
                FileTypeChoices = new List<FilePickerFileType> { new FilePickerFileType("PNG image") { Patterns = new[] { "*.png" } } }
            });
            string path = file?.TryGetLocalPath();
            if (path == null) return;
            string error = animation == null ? VM.ExportFramesSheet(path) : VM.ExportAnimationSheet(animation.Value, path);
            if (error != null) await DialogHelper.ShowErrorAsync($"Couldn't export: {error}", ErrorTitle, Owner);
        }

        private async System.Threading.Tasks.Task ImportSheetAsync(bool animation)
        {
            IReadOnlyList<IStorageFile> files = await TopLevel.GetTopLevel(this).StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = animation ? "Open animation sheet" : "Open frames sheet",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType> { new FilePickerFileType("PNG image") { Patterns = new[] { "*.png" } } }
            });
            string path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
            if (path == null) return;

            TrainerSheetImportViewModel wizard = VM.OpenSheetImport(animation, path, out string why);
            if (wizard == null)
            {
                await DialogHelper.ShowErrorAsync($"Couldn't import: {why}", ErrorTitle, Owner);
                return;
            }
            if (await new TrainerSheetImportView(wizard).ShowDialog<bool>(Owner)) VM.ApplySheetImport(wizard);
        }

        // The JSON editor exists only once its tab is shown, so setup retries on tab changes.
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
            TrainerSpriteEditorViewModel vm = VM;
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
                await DialogHelper.ShowErrorAsync($"Couldn't save: {error}", ErrorTitle, Owner);
        }

        private async void CreateAnimJson_Click(object sender, RoutedEventArgs e)
        {
            string error = VM?.CreateAnimJson();
            if (error != null)
                await DialogHelper.ShowErrorAsync($"Create failed: {error}", ErrorTitle, Owner);
        }

        private void AddFrame_Click(object sender, RoutedEventArgs e) => VM?.AddAnimFrame();

        private async void RemoveFrame_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Control)?.DataContext is not AnimFrameRowViewModel row) return;
            string error = VM?.RemoveAnimFrame(row);
            if (error != null)
                await DialogHelper.ShowErrorAsync(error, ErrorTitle, Owner);
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
                await DialogHelper.ShowErrorAsync(error, ErrorTitle, Owner);
        }

        private async void PlayAnimPreview_Click(object sender, RoutedEventArgs e)
        {
            if (VM != null) await VM.PlayAnimPreviewOnceAsync();
        }
    }
}
