using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using DSPRE.Avalonia.Data;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    /// <summary>The game's screens in groups, each with its views.</summary>
    public sealed class ScreensEditorViewModel : INotifyPropertyChanged, DSPRE.Editors.IEditorWithUnsavedChanges, ISupportsUndo, IDetachable
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public ObservableCollection<ScreenGroup> Groups { get; } = new ObservableCollection<ScreenGroup>();

        private IEnumerable<ScreenEntry> Entries => Groups.SelectMany(g => g.Entries);

        private object _selectedNode;
        /// <summary>A screen or a group heading; a heading keeps the current screen on show.</summary>
        public object SelectedNode
        {
            get => _selectedNode;
            set
            {
                _selectedNode = value;
                Raise();
                if (value is ScreenEntry entry) SelectedEntry = entry;
            }
        }

        private ScreenEntry _selectedEntry;
        public ScreenEntry SelectedEntry
        {
            get => _selectedEntry;
            set
            {
                if (ReferenceEquals(_selectedEntry, value) || value == null) return;
                _selectedEntry?.Leave();
                _selectedEntry = value;
                Raise();
                _selectedEntry.Enter();
                if (!ReferenceEquals(_selectedNode, value)) { _selectedNode = value; Raise(nameof(SelectedNode)); }
                RaiseUndo();
            }
        }

        public ScreensEditorViewModel() { if (!Design.IsDesignMode) return; }

        public ScreensEditorViewModel(bool load)
        {
            if (!load) return;
            foreach ((string title, IEnumerable<ScreenEntry> entries) in ScreenCatalog.Build(this))
            {
                List<ScreenEntry> shown = entries.Where(e => e.Views.Count > 0).ToList();
                if (shown.Count > 0) Groups.Add(new ScreenGroup(title, shown));
            }
            SelectedEntry = Entries.FirstOrDefault();
        }

        /// <summary>Shows a screen, and a line on it when given.</summary>
        /// <param name="then">Run on the editor once it exists, to point it at a page or piece.</param>
        /// <param name="fresh">Remake the editor, when nothing is unsaved, so it picks up a new sample.</param>
        public void GoTo(string key, int bank = -1, int line = -1, string view = null, Action<object> then = null, bool fresh = false)
        {
            ScreenEntry entry = Entries.FirstOrDefault(e => e.Key == key || e.Page?.Key == key);
            if (entry == null) return;
            ScreenView target = view == null ? null : entry.Views.FirstOrDefault(v => v.Name == view);
            if (fresh)
                foreach (ScreenEditorView stale in entry.Views.OfType<ScreenEditorView>()) stale.ForgetIfClean();
            if (target != null && ReferenceEquals(_selectedEntry, entry)) entry.SelectedView = target;
            else if (target != null) entry.PickWithoutMaking(target);
            SelectedEntry = entry;
            if (target is ScreenEditorView editor && then != null) editor.WhenMade(then);
            entry.Page?.Show(bank, line);
        }

        public void Detach()
        {
            foreach (ScreenEntry entry in Entries) { entry.Leave(); entry.Page?.Detach(); }
            foreach (object model in Entries.SelectMany(e => e.Views).OfType<ScreenEditorView>().Select(v => v.Model))
            {
                (model as IDetachable)?.Detach();
                if (model is BottomScreenEditorViewModel bottom) bottom.Stop();
            }
        }

        // Summary's Edit and Layout share one model, so each model counts once.
        private IEnumerable<DSPRE.Editors.IEditorWithUnsavedChanges> Editors =>
            Entries.SelectMany(e => e.Views).OfType<ScreenEditorView>().Select(v => v.Model)
                   .OfType<DSPRE.Editors.IEditorWithUnsavedChanges>().Distinct();

        // Models whose change notices are already passed on to the window.
        private readonly HashSet<object> _watched = new HashSet<object>();

        internal void Watch(object model)
        {
            if (model == null || !_watched.Add(model)) return;
            if (model is INotifyPropertyChanged inpc)
                inpc.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(HasUnsavedChanges)) Raise(nameof(HasUnsavedChanges));
                    if (e.PropertyName == nameof(CanUndo) || e.PropertyName == nameof(CanRedo)) RaiseUndo();
                };
        }

        public bool HasUnsavedChanges => Editors.Any(e => e.HasUnsavedChanges);

        public string UnsavedChangesDescription =>
            string.Join(", ", Editors.Where(e => e.HasUnsavedChanges).Select(e => e.UnsavedChangesDescription));

        public void SaveChanges() => _ = SaveChangesAsync();

        public async Task<bool> SaveChangesAsync()
        {
            bool all = true;
            foreach (DSPRE.Editors.IEditorWithUnsavedChanges editor in Editors.Where(e => e.HasUnsavedChanges).ToList())
                all &= await editor.SaveChangesAsync();
            Raise(nameof(HasUnsavedChanges));
            return all;
        }

        public void DiscardChanges()
        {
            foreach (DSPRE.Editors.IEditorWithUnsavedChanges editor in Editors.Where(e => e.HasUnsavedChanges).ToList()) editor.DiscardChanges();
            Raise(nameof(HasUnsavedChanges));
        }

        // Undo belongs to the editor on show.
        private ISupportsUndo ShownUndo => (_selectedEntry?.SelectedView as ScreenEditorView)?.Model as ISupportsUndo;
        public bool CanUndo => ShownUndo?.CanUndo == true;
        public bool CanRedo => ShownUndo?.CanRedo == true;
        public void Undo() => ShownUndo?.Undo();
        public void Redo() => ShownUndo?.Redo();
        internal void RaiseUndo() { Raise(nameof(CanUndo)); Raise(nameof(CanRedo)); }
    }

    public sealed class ScreenGroup
    {
        public ScreenGroup(string title, IEnumerable<ScreenEntry> entries)
        {
            Title = title;
            Entries = new ObservableCollection<ScreenEntry>(entries);
        }

        public string Title { get; }
        public ObservableCollection<ScreenEntry> Entries { get; }
    }

    public sealed class ScreenEntry : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private readonly ScreensEditorViewModel _owner;

        public ScreenEntry(ScreensEditorViewModel owner, string key, string title, GameTextScreens.Screen text, params ScreenView[] more)
        {
            _owner = owner;
            Key = key;
            Title = title;
            if (text != null && text.Available)
            {
                Page = new ScreenTabViewModel(text);
                Views.Add(new ScreenPreviewView(Page));
            }
            foreach (ScreenView view in more) if (view != null) Views.Add(view);
            _view = Views.FirstOrDefault();
            // Preview draws the other views' unsaved edits.
            if (Page != null)
                foreach (ScreenEditorView editor in Views.OfType<ScreenEditorView>())
                    editor.PropertyChanged += (_, e) =>
                    {
                        if (e.PropertyName == nameof(ScreenEditorView.Control)) Page.Follow(editor, editor.Model as IScreenPreviewSource);
                    };
        }

        public string Key { get; }
        public string Title { get; }
        public ScreenTabViewModel Page { get; }
        public ObservableCollection<ScreenView> Views { get; } = new ObservableCollection<ScreenView>();
        public bool HasSeveralViews => Views.Count > 1;

        private ScreenView _view;
        public ScreenView SelectedView
        {
            get => _view;
            set
            {
                if (ReferenceEquals(_view, value) || value == null) return;
                _view = value;
                Raise();
                if (value is ScreenEditorView editor) editor.Make(_owner);
                _owner.RaiseUndo();
            }
        }

        public void Enter()
        {
            Page?.Enter();
            if (_view is ScreenEditorView editor) editor.Make(_owner);
        }

        /// <summary>Picks a view before showing the screen, so only that view's editor is made.</summary>
        internal void PickWithoutMaking(ScreenView view)
        {
            _view = view;
            Raise(nameof(SelectedView));
        }

        public void Leave() => Page?.Leave();
    }
}
