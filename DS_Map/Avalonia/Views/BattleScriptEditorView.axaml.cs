using System.Collections.Generic;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
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
            Loaded += (_, _) => SetupEditor();
            DataContextChanged += (_, _) => HookVm();
        }

        private BattleScriptEditorViewModel VM => DataContext as BattleScriptEditorViewModel;
        private static ScriptCmdRow Row(object sender) => (sender as Control)?.DataContext as ScriptCmdRow;

        // ── Text tab (AvaloniaEdit) wiring: live two-way sync with the VM + red-squiggle error markers ──
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
            var vm = VM;
            if (ReferenceEquals(vm, _hookedVm)) return;
            if (_hookedVm != null) _hookedVm.PropertyChanged -= OnVmChanged;
            _hookedVm = vm;
            if (vm != null) vm.PropertyChanged += OnVmChanged;
            if (_editorReady) { PushTextToEditor(); RefreshSquiggles(); }
        }

        private void OnVmChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(BattleScriptEditorViewModel.CommandsText): PushTextToEditor(); break;
                case nameof(BattleScriptEditorViewModel.TextErrors): RefreshSquiggles(); break;
            }
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
            var errs = new List<(int, int)>();
            foreach (var er in VM.TextErrors) errs.Add((er.Offset, er.Length));
            _squiggles.SetErrors(errs);
            CommandsTextEditor.TextArea.TextView.InvalidateVisual();
        }

        // Block viewing the cards while the text has parse errors — the cards can't represent an invalid script.
        private async void EditorTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!ReferenceEquals(e.Source, EditorTabs) || EditorTabs == null || VM == null) return;   // ignore child selectors
            if (EditorTabs.SelectedIndex == 0 && VM.HasTextErrors)
            {
                Dispatcher.UIThread.Post(() => EditorTabs.SelectedIndex = 1);
                await DSPRE.Avalonia.DialogHelper.ShowInfo(
                    "Can't view the commands as cards while the text has errors. Fix the red-underlined line(s) first.",
                    "Fix errors first");
            }
        }

        private void AddCommand_Click(object sender, RoutedEventArgs e) => VM?.AddCommand();
        private void Save_Click(object sender, RoutedEventArgs e) => VM?.Save();
        private void CommandGuide_Click(object sender, RoutedEventArgs e)
        {
            if (VM == null) return;
            new ScriptCommandGuideView(VM.BuildCommandGuideViewModel()).ShowManaged();
        }
        private async void PreviewSound_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Control)?.DataContext is not ParamVM p) return;
            string error = p.PreviewSound();
            if (error != null) await DSPRE.Avalonia.DialogHelper.ShowError(error, "Couldn't play sound");
        }
        private void Up_Click(object sender, RoutedEventArgs e) { var r = Row(sender); if (r != null) VM?.MoveCommand(r, -1); }
        private void Down_Click(object sender, RoutedEventArgs e) { var r = Row(sender); if (r != null) VM?.MoveCommand(r, 1); }
        private void Remove_Click(object sender, RoutedEventArgs e) { var r = Row(sender); if (r != null) VM?.RemoveCommand(r); }
        private void PlayCell_Click(object sender, RoutedEventArgs e) => VM?.ToggleCellPlay();
    }
}
