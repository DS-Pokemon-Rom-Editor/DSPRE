using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DSPRE.Avalonia.ViewModels;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Views.Text
{
    public partial class TextEditorView : UserControl
    {
        private TextEditorViewModel VM => DataContext as TextEditorViewModel;
        private bool _setupDone;

        public TextEditorView()
        {
            InitializeComponent();
            // The list takes Enter for itself, so it is caught on the way in.
            SearchResultsList.AddHandler(KeyDownEvent, SearchResult_KeyDown, RoutingStrategies.Tunnel);
            Loaded += OnLoadedSetup;
            // Tunnel, so Enter reaches the search before the text box can take it.
            SearchBox.AddHandler(KeyDownEvent, Search_KeyDown, global::Avalonia.Interactivity.RoutingStrategies.Tunnel);
            DataContextChanged += (_, _) =>
            {
                if (_watched != null) _watched.LineRevealed -= ScrollToLine;
                _watched = VM;
                if (_watched != null) _watched.LineRevealed += ScrollToLine;
            };
        }

        private TextEditorViewModel _watched;

        // The archive's rows may still be filling, so scroll once layout has caught up.
        private void ScrollToLine(object sender, System.EventArgs e)
            => global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (LinesGrid.SelectedItem != null) LinesGrid.ScrollIntoView(LinesGrid.SelectedItem, null);
            }, global::Avalonia.Threading.DispatcherPriority.Background);

        /// <summary>
        /// Points the preview at the ROM's own font, so a line is shown with the letters the game would
        /// really use, and says so plainly when that font could not be read.
        /// </summary>
        private void AttachPreviewFont()
        {
            if (VM == null) return;
            FieldMessageBoxView.Font = FieldFont.LoadTalkFont();
            // Screens with their own window use the system font inside the standard border.
            FieldMenuWindowView.Font = FieldFont.LoadSystemFont();
            FieldMenuWindowView.Frame = DSPRE.Avalonia.Data.WindowFrames.LoadStandard();
            FieldMenuWindowView.Colours = DSPRE.Avalonia.Data.WindowFrames.LoadSystemFontColours();

            if (VM.BorderNames.Count == 0)
            {
                for (int i = 0; i < FieldWindowFrame.FrameCount; i++) VM.BorderNames.Add($"Frame {i + 1}");
                // The list filled after the box was bound, which leaves it showing nothing picked.
                int picked = System.Math.Max(0, VM.BorderIndex);
                VM.BorderIndex = -1;
                VM.BorderIndex = picked;
                VM.BorderChanged += (_, _) =>
                {
                    FieldMessageBoxView.Frame = DSPRE.Avalonia.Data.WindowFrames.Load(VM.BorderIndex);
                    PreviewBox.InvalidateVisual();
                };
            }
            FieldMessageBoxView.Frame = DSPRE.Avalonia.Data.WindowFrames.Load(VM.BorderIndex);
            VM.MeasureText = FieldMessageBoxView.Measure;
            VM.PreviewFontNote = FieldMessageBoxView.Font == null
                ? "Showing stand-in letters: the game's own font couldn't be read from this ROM."
                : null;
            VM.RefreshPreview();
        }

        private void PreviewNext_Click(object sender, RoutedEventArgs e) => VM?.NextPreviewStep();

        private void ShotOpen_Click(object sender, RoutedEventArgs e) =>
            ((sender as Control)?.DataContext as DSPRE.Avalonia.Data.TextScreenShot)?.Open?.Invoke();

        // Three times DS size, without smoothing.
        private async void ShotLarger_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Control)?.DataContext is not DSPRE.Avalonia.Data.TextScreenShot shot || shot.Picture == null) return;
            if (TopLevel.GetTopLevel(this) is not Window owner) return;
            Image image = new Image { Source = shot.Picture, Stretch = global::Avalonia.Media.Stretch.Uniform };
            global::Avalonia.Media.RenderOptions.SetBitmapInterpolationMode(image, global::Avalonia.Media.Imaging.BitmapInterpolationMode.None);
            Window larger = new Window
            {
                Title = shot.Name,
                Width = 256 * 3 + 24,
                Height = 192 * 3 + 24,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new Border { Padding = new global::Avalonia.Thickness(12), Child = image },
            };
            await larger.ShowDialog(owner);
        }
        private void StrVarHelp_Click(object sender, RoutedEventArgs e) => AvaloniaEditorLauncher.OpenStrVarHelp();

        public TextEditorView(TextEditorViewModel vm) : this()
        {
            DataContext = vm;
        }

        private async void OnLoadedSetup(object sender, RoutedEventArgs e)
        {
            // Loaded fires on every tab revisit; ROM loads call EnsureSetupAsync themselves.
            if (!_setupDone) await EnsureSetupAsync();
        }

        /// <summary>Sets up the view model; see <see cref="MapsWorkspaceView"/>.</summary>
        /// <param name="ownerOverride">The owning window, for a tab not yet in the visual tree.</param>
        public async Task EnsureSetupAsync(Window ownerOverride = null)
        {
            if (Design.IsDesignMode) return;
            TextEditorViewModel vm = VM;
            if (vm == null || !AvaloniaEditorLauncher.IsRomLoaded) return;
            Window owner = ownerOverride ?? TopLevel.GetTopLevel(this) as Window;
            if (owner == null) return;
            if (!_setupDone) owner.Closed += (_, _) => vm.Detach();
            _setupDone = true;
            await vm.SetupAsync(owner);
            AttachPreviewFont();
        }

        private void AddArchive_Click(object sender, RoutedEventArgs e) => VM?.AddArchive();
        private async void RemoveArchive_Click(object sender, RoutedEventArgs e)
            => await DialogHelper.GuardAsync(() => VM?.RemoveArchiveAsync() ?? Task.CompletedTask, "The text archive wasn't removed.", ErrorTitle);
        private void Save_Click(object sender, RoutedEventArgs e) => VM?.Save();
        private async void Import_Click(object sender, RoutedEventArgs e)
            => await DialogHelper.GuardAsync(() => VM?.ImportAsync() ?? Task.CompletedTask, "The text archive wasn't imported.", ErrorTitle);
        private async void Export_Click(object sender, RoutedEventArgs e)
            => await DialogHelper.GuardAsync(() => VM?.ExportAsync() ?? Task.CompletedTask, "The text archive wasn't exported.", ErrorTitle);

        private void AddString_Click(object sender, RoutedEventArgs e) => VM?.AddString();
        private void RemoveString_Click(object sender, RoutedEventArgs e) => VM?.RemoveString();
        private void MoveUp_Click(object sender, RoutedEventArgs e) => VM?.MoveSelectedUp();
        private void MoveDown_Click(object sender, RoutedEventArgs e) => VM?.MoveSelectedDown();

        private void Search_Click(object sender, RoutedEventArgs e) => VM?.Search();
        private async void Replace_Click(object sender, RoutedEventArgs e)
            => await DialogHelper.GuardAsync(() => VM?.ReplaceAsync() ?? Task.CompletedTask, "The replace didn't finish.", ErrorTitle);

        private void Search_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            VM?.Search();
            e.Handled = true;
        }

        private void ShowArchiveFile_Click(object sender, RoutedEventArgs e) => VM?.ShowArchiveFile();
        private void OpenArchiveFile_Click(object sender, RoutedEventArgs e) => VM?.OpenArchiveFile();

        private void SearchResult_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            SearchResult_DoubleTapped(sender, null);
            e.Handled = true;
        }

        private void SearchResult_DoubleTapped(object sender, TappedEventArgs e)
        {
            if (SearchResultsList.SelectedItem is not TextSearchResultVM r || VM == null) return;
            _ = DialogHelper.GuardAsync(() => VM.GoToResultAsync(r) ?? Task.CompletedTask, "The search result couldn't be opened.", ErrorTitle);
        }

        private const string ErrorTitle = "Text editor";

        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) (DataContext as DSPRE.Editors.IEditorWithUnsavedChanges)?.DiscardChanges();
        }
    }
}
