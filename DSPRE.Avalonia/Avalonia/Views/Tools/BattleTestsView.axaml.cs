using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaEdit;
using DSPRE.Avalonia.ViewModels.Tools;
using DSPRE.HgEngine;

namespace DSPRE.Avalonia.Views.Tools
{
    public partial class BattleTestsView : Window
    {
        private BattleTestsViewModel VM => DataContext as BattleTestsViewModel;
        private readonly SquiggleRenderer _squiggles = new();
        private bool _syncing;

        public BattleTestsView() : this(new BattleTestsViewModel()) { }

        public BattleTestsView(BattleTestsViewModel vm)
        {
            DataContext = vm;
            InitializeComponent();
            DSPRE.Avalonia.EditorWindowChrome.Attach(this, vm);

            Code.SyntaxHighlighting = CSyntax.Definition;
            Code.TextArea.TextView.BackgroundRenderers.Add(_squiggles);
            Code.TextChanged += (_, _) => { if (!_syncing && VM != null) VM.Text = Code.Text; };
            Code.TextArea.TextView.PointerMoved += Code_PointerMoved;
            // The editor's own undo would miss form edits, so Ctrl+Z / Ctrl+Y go to the shared history instead.
            Code.Document.UndoStack.SizeLimit = 0;
            Code.AddHandler(KeyDownEvent, Code_KeyDown, RoutingStrategies.Tunnel);
            vm.PropertyChanged += Vm_PropertyChanged;
            PushText();
            RefreshSquiggles();
        }

        private void Vm_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(BattleTestsViewModel.Text)) PushText();
            else if (e.PropertyName == nameof(BattleTestsViewModel.Problems)) RefreshSquiggles();
        }

        private void PushText()
        {
            string text = VM?.Text ?? "";
            if (Code.Text == text) return;   // no echo, so the caret stays put while typing
            _syncing = true;
            Code.Text = text;
            _syncing = false;
        }

        private void RefreshSquiggles()
        {
            _squiggles.SetErrors(VM?.Problems.Select(p => (p.Offset, p.Length)));
            Code.TextArea.TextView.InvalidateVisual();
        }

        // The message for whatever underlined spot the pointer is over.
        private void Code_PointerMoved(object sender, PointerEventArgs e)
        {
            TextViewPosition? at = Code.GetPositionFromPoint(e.GetPosition(Code));
            string tip = null;
            if (at.HasValue && VM != null)
            {
                int offset = Code.Document.GetOffset(at.Value.Location);
                HgEngineBattleTests.Problem hit = VM.Problems.FirstOrDefault(p => offset >= p.Offset && offset <= p.Offset + p.Length);
                tip = hit?.Message;
            }
            ToolTip.SetTip(Code, tip);
            ToolTip.SetIsOpen(Code, tip != null);
        }

        private void Code_KeyDown(object sender, KeyEventArgs e)
        {
            if (VM == null || e.KeyModifiers != KeyModifiers.Control) return;
            if (e.Key == Key.Z) { if (VM.CanUndo) VM.Undo(); e.Handled = true; }
            else if (e.Key == Key.Y) { if (VM.CanRedo) VM.Redo(); e.Handled = true; }
        }

        private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!ReferenceEquals(e.Source, Tabs)) return;   // pickers inside the tabs raise this too
            VM?.ShowForm(Tabs.SelectedIndex == 1);
        }

        private void AddExpectation_Click(object sender, RoutedEventArgs e) => VM?.Form?.AddExpectation();
        private void RemoveExpectation_Click(object sender, RoutedEventArgs e) =>
            VM?.Form?.RemoveExpectation((sender as Control)?.DataContext as FormExpectation);

        private void Save_Click(object sender, RoutedEventArgs e) => VM?.SaveChanges();
        private async void Discard_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ConfirmDiscardAsync(this)) VM?.DiscardChanges();
        }
        private void Add_Click(object sender, RoutedEventArgs e) => VM?.AddTest();

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (VM?.Selected == null) return;
            if (!await DialogHelper.AskYesNoAsync($"Delete {VM.Selected} from the checkout?", "Delete battle test?", this)) return;
            string error = VM.DeleteSelected();
            if (error != null) await DialogHelper.ShowErrorAsync(error, "Battle tests error", this);
        }
    }
}
