using System.Collections.Generic;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using DSPRE.Avalonia.ViewModels;

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
            Loaded += (_, _) => SetupEditor();
            DataContextChanged += (_, _) => HookVm();
            NameRefresh.Bind(this, () => VM?.RefreshNames());
        }

        private BattleScriptEditorViewModel VM => DataContext as BattleScriptEditorViewModel;

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
    }
}
