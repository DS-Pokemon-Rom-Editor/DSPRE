using System.Collections.Generic;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Threading;
using DSPRE.Avalonia.ViewModels;
using DSPRE.Avalonia.Views.Controls;

namespace DSPRE.Avalonia.Views.Battle
{
    public partial class BattleScriptEditorView : UserControl
    {
        private readonly SquiggleRenderer _squiggles = new();
        private bool _editorReady;
        private bool _syncing;   // guards the editor⇄VM text loop
        private BattleScriptEditorViewModel _hookedVm;

        public BattleScriptEditorView()
        {
            InitializeComponent();
            Loaded += (_, _) => { SetupEditor(); FitPreviewScale(); };
            Timeline.CommandPicked += cmd => VM?.PickCommand(cmd);
            Timeline.SeekRequested += frame => VM?.SeekTo(frame);
            Timeline.FindKnob = (item, movesBar) => VM?.FindKnob(item, movesBar);
            Timeline.KnobDropped += (knob, value) => VM?.SetKnob(knob, value);
            AddHandler(KeyDownEvent, OnTransportKey, RoutingStrategies.Tunnel);
            EditorArea.SizeChanged += (_, e) => { if (e.HeightChanged) FitTimelineHeight(); };
            TimelineZoom.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty) Timeline.FrameWidth = TimelineZoom.Value; };
            Unloaded += (_, _) => { if (_scaleSource != null) _scaleSource.ScalingChanged -= OnScalingChanged; _scaleSource = null; };
            DataContextChanged += (_, _) => HookVm();
            NameRefresh.Bind(this, () => VM?.RefreshNames());
        }

        private BattleScriptEditorViewModel VM => DataContext as BattleScriptEditorViewModel;

        private TopLevel _scaleSource;

        // Twice the game's size in screen pixels, rounded to whole device pixels, so every game pixel is the same size.
        private void FitPreviewScale()
        {
            TopLevel top = TopLevel.GetTopLevel(this);
            if (top == null) return;
            if (!ReferenceEquals(_scaleSource, top))
            {
                if (_scaleSource != null) _scaleSource.ScalingChanged -= OnScalingChanged;
                _scaleSource = top;
                top.ScalingChanged += OnScalingChanged;
            }
            double scaling = top.RenderScaling > 0 ? top.RenderScaling : 1;
            int devicePixels = System.Math.Max(1, (int)System.Math.Round(2 * scaling, System.MidpointRounding.AwayFromZero));
            double scale = devicePixels / scaling;
            PreviewTransform.LayoutTransform = new global::Avalonia.Media.ScaleTransform(scale, scale);
        }

        private void OnScalingChanged(object sender, System.EventArgs e) => FitPreviewScale();

        private void EditParticles_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null || sender is not Button button) return;
            IReadOnlyList<(int File, bool Orthographic)> files = VM.ParticleFilesOfMove();
            string move = $"Move {VM.FileIndex}";
            void Open((int File, bool Orthographic) f) => AvaloniaEditorLauncher.OpenParticleEditor(
                RomInfo.DirNames.WazaParticle, f.File, $"{move} particle file {f.File}", null, f.Orthographic);
            if (files.Count == 0)
            {
                _ = DialogHelper.ShowInfoAsync("This script loads no particle files.", "Edit particles");
                return;
            }
            if (files.Count == 1) { Open(files[0]); return; }
            MenuFlyout menu = new MenuFlyout();
            foreach ((int File, bool Orthographic) f in files)
            {
                MenuItem item = new MenuItem { Header = $"Particle file {f.File}" };
                item.Click += (_, _) => Open(f);
                menu.Items.Add(item);
            }
            menu.ShowAt(button);
        }
        private static ScriptCmdRow Row(object sender) => (sender as Control)?.DataContext as ScriptCmdRow;

        private void SetupEditor()
        {
            if (_editorReady || CommandsTextEditor == null) return;
            _editorReady = true;
            CommandsTextEditor.TextArea.TextView.BackgroundRenderers.Add(_squiggles);
            CommandsTextEditor.TextChanged += (_, _) =>
            {
                if (_syncing || VM == null) return;
                VM.CommandsText = CommandsTextEditor.Text;
            };
            HookVm();
            PushTextToEditor();
            RefreshSquiggles();
        }

        private void HookVm()
        {
            BattleScriptEditorViewModel vm = VM;
            if (ReferenceEquals(vm, _hookedVm)) return;
            if (_hookedVm != null) _hookedVm.PropertyChanged -= OnVmChanged;
            _hookedVm = vm;
            if (vm != null) vm.PropertyChanged += OnVmChanged;
            if (_editorReady) { PushTextToEditor(); RefreshSquiggles(); }
            PushTimeline();
            FitColumns();
        }

        private void OnVmChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(BattleScriptEditorViewModel.CommandsText): PushTextToEditor(); break;
                case nameof(BattleScriptEditorViewModel.TextErrors): RefreshSquiggles(); break;
                case nameof(BattleScriptEditorViewModel.Timeline): PushTimeline(); break;
                case nameof(BattleScriptEditorViewModel.HasPreview): FitColumns(); break;
                case nameof(BattleScriptEditorViewModel.PreviewFrame): Timeline.Playhead = VM.PreviewFrame; FollowPlayhead(); break;
                case nameof(BattleScriptEditorViewModel.PickedCommand):
                    Timeline.SelectedCommand = VM.PickedCommand;
                    // The card opens on the next layout pass, so bring it into sight after that.
                    Dispatcher.UIThread.Post(() => CardList.ContainerFromIndex(VM.PickedCommand)?.BringIntoView(), DispatcherPriority.Background);
                    break;
            }
        }

        // Keeps the playhead in sight when the timeline is zoomed wider than its box.
        private void FollowPlayhead()
        {
            if (VM == null || VM.PreviewFrame < 0) return;
            double x = Timeline.XOf(VM.PreviewFrame);
            double left = TimelineScroll.Offset.X, width = TimelineScroll.Viewport.Width;
            if (width <= 0 || (x >= left + MoveTimelineView.Gutter && x <= left + width - 20)) return;
            TimelineScroll.Offset = new Vector(System.Math.Max(0, x - MoveTimelineView.Gutter - 20), TimelineScroll.Offset.Y);
        }

        private void PushTimeline()
        {
            if (VM == null) return;
            Timeline.SetBars(VM.Timeline?.Items, VM.Timeline?.TotalPasses ?? 1);
            Timeline.Playhead = VM.PreviewFrame;
            FitTimelineHeight();
        }

        // The timeline gets the height of all its lanes, as far as the preview and its controls leave room; the cards
        // take the rest.
        private void FitTimelineHeight()
        {
            if (VM?.Timeline == null) return;
            const double HeaderAndScrollBar = 44, Splitter = 6;
            PreviewPanel.Measure(Size.Infinity);
            double room = EditorArea.Bounds.Height > 0 ? EditorArea.Bounds.Height - PreviewPanel.DesiredSize.Height - Splitter : 400;
            double h = System.Math.Min(Timeline.ContentHeight + HeaderAndScrollBar, room);
            EditorArea.RowDefinitions[2].Height = new GridLength(System.Math.Max(120, h));
        }

        private void FitColumns()
        {
            bool preview = VM?.HasPreview == true;
            TopArea.ColumnDefinitions[0].Width = new GridLength(preview ? 3 : 1, GridUnitType.Star);
            TopArea.ColumnDefinitions[2].Width = preview ? new GridLength(2, GridUnitType.Star) : GridLength.Auto;
        }

        private void PushTextToEditor()
        {
            if (!_editorReady || VM == null) return;
            string text = VM.CommandsText ?? "";
            if (CommandsTextEditor.Text == text) return;   // no echo → keeps the user's caret while they type
            _syncing = true;
            CommandsTextEditor.Text = text;
            _syncing = false;
        }

        private void RefreshSquiggles()
        {
            if (!_editorReady || VM == null) return;
            List<(int, int)> errs = new List<(int, int)>();
            foreach (BattleScriptEditorViewModel.TextError er in VM.TextErrors) errs.Add((er.Offset, er.Length));
            _squiggles.SetErrors(errs);
            CommandsTextEditor.TextArea.TextView.InvalidateVisual();
        }

        // Block viewing the cards while the text has parse errors: the cards can't represent an invalid script.
        private async void EditorTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!ReferenceEquals(e.Source, EditorTabs) || EditorTabs == null || VM == null) return;   // ignore child selectors
            VM.OpenTab = EditorTabs.SelectedIndex;
            // Cards, Script, Read: Cards can't be built while the script has errors, so it bounces back to Script.
            if (EditorTabs.SelectedIndex == 0 && VM.HasTextErrors)
            {
                Dispatcher.UIThread.Post(() => EditorTabs.SelectedIndex = 1);
                await DSPRE.Avalonia.DialogHelper.ShowInfoAsync(
                    "Cards can't open while the script has errors. Fix the underlined lines first.",
                    "Fix errors first");
            }
        }

        private void AddCommand_Click(object sender, RoutedEventArgs e) => VM?.AddCommand();
        private void Save_Click(object sender, RoutedEventArgs e) => VM?.Save();
        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) VM?.DiscardChanges();
        }
        private void CommandGuide_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            new ScriptCommandGuideView(VM.BuildCommandGuideViewModel()).ShowManaged();
        }
        private async void PreviewSound_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Control)?.DataContext is not ParamVM p) return;
            string error = p.PreviewSound();
            if (error != null) await DSPRE.Avalonia.DialogHelper.ShowErrorAsync(error, "Couldn't play sound");
        }
        private void Up_Click(object sender, RoutedEventArgs e) { ScriptCmdRow r = Row(sender); if (r != null) VM?.MoveCommand(r, -1); }
        private void Down_Click(object sender, RoutedEventArgs e) { ScriptCmdRow r = Row(sender); if (r != null) VM?.MoveCommand(r, 1); }
        private void Remove_Click(object sender, RoutedEventArgs e) { ScriptCmdRow r = Row(sender); if (r != null) VM?.RemoveCommand(r); }
        private void PlayCell_Click(object sender, RoutedEventArgs e) => VM?.ToggleCellPlay();
        private void StopCell_Click(object sender, RoutedEventArgs e) => VM?.StopPreview();
        private void FrameBack_Click(object sender, RoutedEventArgs e) => VM?.StepFrame(-1);
        private void FrameOn_Click(object sender, RoutedEventArgs e) => VM?.StepFrame(1);

        // Transport keys work from the preview and the timeline, not while typing or in a list.
        private void OnTransportKey(object sender, KeyEventArgs e)
        {
            if (VM == null || e.KeyModifiers != KeyModifiers.None || e.Source is not Visual source) return;
            if (!(PreviewPanel.IsVisualAncestorOf(source) || ReferenceEquals(source, Timeline) || Timeline.IsVisualAncestorOf(source))) return;
            if (source is ComboBox || source.FindAncestorOfType<ComboBox>() != null) return;
            if (e.Key == Key.Space && (source is Button || source is CheckBox)) return;
            switch (e.Key)
            {
                case Key.Space: VM.ToggleCellPlay(); break;
                case Key.Left: VM.StepFrame(-1); break;
                case Key.Right: VM.StepFrame(1); break;
                case Key.Home: VM.SeekTo(0); break;
                case Key.End: VM.SeekToEnd(); break;
                default: return;
            }
            e.Handled = true;
        }
    }
}
