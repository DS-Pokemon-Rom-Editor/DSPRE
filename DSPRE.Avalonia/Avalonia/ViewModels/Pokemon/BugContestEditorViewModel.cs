using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using DSPRE.Editors;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    /// <summary>The Bug-Catching Contest: its opponents and prizes. Its encounters stay in the Special encounters editor.</summary>
    public class BugContestEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo, IDetachable
    {
        public const string Title = "Bug Contest editor";

        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public BugContestTrainersViewModel OpponentsVM { get; } = new BugContestTrainersViewModel();
        public BugContestPrizesViewModel PrizesVM { get; } = new BugContestPrizesViewModel();

        private IEditorWithUnsavedChanges[] Children => new IEditorWithUnsavedChanges[] { OpponentsVM, PrizesVM };

        public BugContestEditorViewModel() { }

        public BugContestEditorViewModel(bool load)
        {
            foreach (IEditorWithUnsavedChanges c in Children)
                if (c is INotifyPropertyChanged n) n.PropertyChanged += OnChildChanged;
        }

        private void OnChildChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(HasUnsavedChanges)) Raise(nameof(HasUnsavedChanges));
            if (e.PropertyName == nameof(CanUndo) || e.PropertyName == nameof(CanRedo)) { Raise(nameof(CanUndo)); Raise(nameof(CanRedo)); }
        }

        public void RefreshNames()
        {
            OpponentsVM.RefreshNames();
            PrizesVM.RefreshNames();
        }

        public void Detach() => PrizesVM.Detach();

        // ── Unsaved changes across both tabs ────────────────────────────────────────
        public bool HasUnsavedChanges
        {
            get { foreach (IEditorWithUnsavedChanges c in Children) if (c.HasUnsavedChanges) return true; return false; }
        }

        public string UnsavedChangesDescription
        {
            get
            {
                List<string> parts = new List<string>();
                foreach (IEditorWithUnsavedChanges c in Children) if (c.HasUnsavedChanges) parts.Add(c.UnsavedChangesDescription);
                return parts.Count > 0 ? string.Join(", ", parts) : Title;
            }
        }

        public void SaveChanges() => DialogHelper.Watch(SaveChangesAsync());

        public async Task<bool> SaveChangesAsync()
        {
            foreach (IEditorWithUnsavedChanges c in Children)
                if (c.HasUnsavedChanges) await c.SaveChangesAsync();
            return !HasUnsavedChanges;
        }

        public void DiscardChanges()
        {
            foreach (IEditorWithUnsavedChanges c in Children) c.DiscardChanges();
        }

        // ── Undo acts on the tab that is showing. The view sets it. ─────────────────
        private ISupportsUndo _activeTab;
        public object ActiveTab { set { _activeTab = value as ISupportsUndo; Raise(nameof(CanUndo)); Raise(nameof(CanRedo)); } }
        public bool CanUndo => _activeTab?.CanUndo == true;
        public bool CanRedo => _activeTab?.CanRedo == true;
        public void Undo() => _activeTab?.Undo();
        public void Redo() => _activeTab?.Redo();
    }
}
