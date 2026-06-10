using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using AvaloniaEdit;
using DSPRE.Avalonia.ViewModels;

namespace DSPRE.Avalonia.Views
{
    public partial class ScriptEditorView : Window
    {
        private ScriptEditorViewModel VM => DataContext as ScriptEditorViewModel;
        private bool _setupDone;
        private bool _closeConfirmed;
        private bool _syncing;   // guards the editor⇄VM text loop

        public ScriptEditorView()
        {
            InitializeComponent();

            foreach (var ed in new[] { ScriptsEditor, FunctionsEditor, ActionsEditor })
            {
                ed.SyntaxHighlighting = ScriptSyntax.Definition;
                ed.Options.ConvertTabsToSpaces = false;
                ed.Options.IndentationSize = 4;
            }

            ScriptsEditor.TextChanged += (_, _) => { if (!_syncing && VM != null) VM.ScriptsText = ScriptsEditor.Text; };
            FunctionsEditor.TextChanged += (_, _) => { if (!_syncing && VM != null) VM.FunctionsText = FunctionsEditor.Text; };
            ActionsEditor.TextChanged += (_, _) => { if (!_syncing && VM != null) VM.ActionsText = ActionsEditor.Text; };

            Loaded += OnLoadedSetup;
        }

        public ScriptEditorView(ScriptEditorViewModel vm) : this() { DataContext = vm; }

        private async void OnLoadedSetup(object sender, RoutedEventArgs e)
        {
            if (_setupDone || Design.IsDesignMode) return;
            var vm = VM;
            if (vm == null) return;
            _setupDone = true;
            vm.PropertyChanged += OnVmPropertyChanged;
            await vm.SetupAsync(this);
        }

        private void OnVmPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ScriptEditorViewModel.ScriptsText): PushToEditor(ScriptsEditor, VM.ScriptsText); break;
                case nameof(ScriptEditorViewModel.FunctionsText): PushToEditor(FunctionsEditor, VM.FunctionsText); break;
                case nameof(ScriptEditorViewModel.ActionsText): PushToEditor(ActionsEditor, VM.ActionsText); break;
                case nameof(ScriptEditorViewModel.IsReadOnly):
                    ScriptsEditor.IsReadOnly = FunctionsEditor.IsReadOnly = ActionsEditor.IsReadOnly = VM.IsReadOnly;
                    break;
            }
        }

        private void PushToEditor(TextEditor editor, string text)
        {
            if (editor.Text == (text ?? "")) return;
            _syncing = true;
            editor.Text = text ?? "";
            _syncing = false;
        }

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.Save();
        private async void Import_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ImportAsync());
        private async void Export_Click(object sender, RoutedEventArgs e) => await Safe(VM?.ExportAsync());

        protected override async void OnClosing(WindowClosingEventArgs e)
        {
            if (VM != null && VM.HasUnsavedChanges && !_closeConfirmed)
            {
                e.Cancel = true;
                bool discard = await DialogHelper.AskYesNo($"Discard unsaved changes to {VM.UnsavedChangesDescription}?", "Unsaved Changes");
                if (discard) { _closeConfirmed = true; VM.DiscardChanges(); Close(); }
                return;
            }
            base.OnClosing(e);
        }

        private static async Task Safe(Task task)
        {
            if (task == null) return;
            try { await task; } catch { }
        }
    }
}
