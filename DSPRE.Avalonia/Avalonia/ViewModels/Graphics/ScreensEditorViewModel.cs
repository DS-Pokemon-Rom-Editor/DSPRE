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
    /// <summary>The game's screens in groups, each with the views it has: Preview, Edit and Layout.</summary>
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

        // ── the embedded editors' unsaved changes and undo, passed on to the window ──

        // Summary's Edit and Layout share one model, so each model counts once.
        private IEnumerable<DSPRE.Editors.IEditorWithUnsavedChanges> Editors =>
            Entries.SelectMany(e => e.Views).OfType<ScreenEditorView>().Select(v => v.Model)
                   .OfType<DSPRE.Editors.IEditorWithUnsavedChanges>().Distinct();

        /// <summary>Its changes now count as this window's.</summary>
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
            // Preview draws the Layout view's unsaved edits while it has one.
            if (Page != null)
                foreach (ScreenEditorView editor in Views.OfType<ScreenEditorView>())
                    editor.PropertyChanged += (_, e) =>
                    {
                        if (e.PropertyName == nameof(ScreenEditorView.Control)) Page.Follow(editor.Model as ScreenWindowLayoutViewModel);
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

    public abstract class ScreenView
    {
        protected ScreenView(string name) { Name = name; }
        public string Name { get; }
    }

    public sealed class ScreenPreviewView : ScreenView
    {
        public ScreenPreviewView(ScreenTabViewModel page) : base("Preview") { Page = page; }
        public ScreenTabViewModel Page { get; }
    }

    /// <summary>A screen's own editor, made the first time its view shows.</summary>
    public sealed class ScreenEditorView : ScreenView, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private readonly Func<Control> _make;
        private readonly Action _openApart, _prepare;
        private readonly string _busy;
        private readonly List<Action<object>> _then = new List<Action<object>>();
        private bool _making;

        /// <param name="make">The editor's view; null while it still opens in its own window.</param>
        /// <param name="openApart">Opens it in its own window, for editors not moved in yet.</param>
        /// <param name="prepare">File work run first, off the UI thread, behind the busy card.</param>
        /// <param name="forgotten">Run when the editor is dropped, for state its views share.</param>
        public ScreenEditorView(string name, Func<Control> make, Action openApart = null, Action prepare = null, string busy = null,
                                Action forgotten = null) : base(name)
        {
            _make = make;
            _openApart = openApart;
            _prepare = prepare;
            _busy = busy;
            _forgotten = forgotten;
        }

        private readonly Action _forgotten;

        public Control Control { get; private set; }
        public object Model => Control?.DataContext;
        public bool OpensApart => _make == null;

        public void OpenApart() => _openApart?.Invoke();

        /// <summary>Runs on the editor's model now, or once it's made.</summary>
        internal void WhenMade(Action<object> action)
        {
            if (Control != null) action(Model);
            else _then.Add(action);
        }

        /// <summary>Drops an editor with nothing unsaved, so it's made fresh next time.</summary>
        internal void ForgetIfClean()
        {
            if (Control == null || (Model as DSPRE.Editors.IEditorWithUnsavedChanges)?.HasUnsavedChanges == true) return;
            (Model as IDetachable)?.Detach();
            Control = null;
            _forgotten?.Invoke();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Control)));
        }

        internal async void Make(ScreensEditorViewModel owner)
        {
            if (Control != null || _make == null || _making) return;
            _making = true;
            try
            {
                if (_prepare != null) await BusyOverlay.RunAsync(_busy ?? $"Opening {Name}…", "", _prepare, () => Control = _make());
                else Control = _make();
            }
            catch (Exception ex) { AppLogger.Error("Screens editor: " + Name + ": " + ex.Message); }
            finally { _making = false; }
            if (Control == null) return;
            // Offers the editor's own tour the first time it shows here.
            EditorTours.Attach(Control, Control.GetType().Name);
            owner.Watch(Model);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Control)));
            foreach (Action<object> action in _then) action(Model);
            _then.Clear();
        }
    }

    internal static class ScreenCatalog
    {
        private static GameTextScreens.Screen Text(string key) => GameTextScreens.Screens.FirstOrDefault(s => s.Key == key);

        /// <summary>The Pokémon and page the next Pokédex and summary editors open on.</summary>
        public static (int Species, string Page) PokedexStart, SummaryStart;

        private static ScreenEditorView Editor(string gate, string name, Func<Control> make, Action openApart = null,
                                               Action prepare = null, string busy = null, Action forgotten = null) =>
            EditorAvailability.Allows(gate) ? new ScreenEditorView(name, make, openApart, prepare, busy, forgotten) : null;

        private static ScreenEditorView LayoutOf(string screen, string title) =>
            ScreenWindowLayout.Read(screen) == null ? null
                : new ScreenEditorView("Layout", () => new Views.Graphics.ScreenWindowLayoutView(new ScreenWindowLayoutViewModel(screen, title)));

        private static readonly DirNames[] SummaryArchives =
        {
            DirNames.SummaryGraphics, DirNames.SummarySprites, DirNames.PokemonBattleSprites, DirNames.MonIcons,
            DirNames.BattleObj, DirNames.PersonalPokeData, DirNames.MoveData, DirNames.Fonts,
        };

        private static Action Unpack(params DirNames[] dirs) => () => DSUtils.TryUnpackNarcs(dirs.ToList());

        // Summary's Edit and Layout are two views of one model.
        private static ScreenEntry SummaryEntry(ScreensEditorViewModel o)
        {
            ScreenPagesEditorViewModel shared = null;
            ScreenPagesEditorViewModel Model() =>
                shared ??= new ScreenPagesEditorViewModel(new SummaryBook(SummaryStart.Species), SummaryStart.Page);
            void Forget() => shared = null;
            return new ScreenEntry(o, "summary", "Summary", null,
                Editor("SummaryScreenEditorView", "Edit", () => new Views.Graphics.SummaryScreenEditorView(Model(), "Pieces"), null,
                       Unpack(SummaryArchives), "Opening Summary screen…", Forget),
                Editor("SummaryScreenEditorView", "Layout", () => new Views.Graphics.SummaryScreenEditorView(Model(), "Layout"), null,
                       Unpack(SummaryArchives), "Opening Summary screen…", Forget));
        }

        public static IEnumerable<(string, IEnumerable<ScreenEntry>)> Build(ScreensEditorViewModel o)
        {
            yield return ("Battle", new[]
            {
                new ScreenEntry(o, "battle", "Battle screen", null,
                    Editor("BattleScreenEditorView", "Edit", () => new Views.Battle.BattleScreenEditorView(), null,
                           Unpack(DirNames.BattleObj, DirNames.BattleBg, DirNames.WindowFrames, DirNames.Fonts), "Opening Battle screen…")),
                new ScreenEntry(o, "hpbar", "HP bars", null,
                    Editor("HpBarLayoutEditorView", "Layout", () => new Views.Graphics.HpBarLayoutEditorView(), null,
                           Unpack(DirNames.BattleObj, DirNames.BattleBg, DirNames.WindowFrames, DirNames.Fonts), "Opening HP bars…")),
            });
            yield return ("Menus", new[]
            {
                new ScreenEntry(o, "party", "Party", Text("party"), LayoutOf("party", "Party")),
                new ScreenEntry(o, "bag", "Bag", Text("bag")),
                new ScreenEntry(o, "box", "PC boxes", Text("box"), LayoutOf("box", "PC boxes")),
                new ScreenEntry(o, "options", "Options", Text("options")),
                new ScreenEntry(o, "mainmenu", "Main menu", Text("mainmenu")),
                new ScreenEntry(o, "startmenu", "Start menu", Text("startmenu")),
                new ScreenEntry(o, "trainercard", "Trainer card", Text("trainercard"),
                    Editor("TrainerCardEditorView", "Edit", () => new Views.Graphics.TrainerCardEditorView())),
                new ScreenEntry(o, "naming", "Naming screen", Text("naming"),
                    Editor("NamingScreenEditor", "Edit", () => new Views.Trainers.TrainerSpriteEditorView(
                            new ViewModels.Trainers.TrainerSpriteEditorViewModel(48, TrainerSpriteSet.NamingIcons)), null,
                           Unpack(DirNames.NameInputGraphics), "Opening Naming screen…")),
            });
            yield return ("Pokédex and summary", new[]
            {
                new ScreenEntry(o, "pokedex", "Pokédex", null,
                    Editor("PokedexGraphicsEditorView", "Edit", () => new Views.Graphics.PokedexGraphicsEditorView(
                        new ScreenPagesEditorViewModel(new PokedexBook(PokedexStart.Species), PokedexStart.Page)))),
                SummaryEntry(o),
            });
            yield return ("Field", new[]
            {
                new ScreenEntry(o, "banner", "Place-name banner", Text("banner")),
                new ScreenEntry(o, "bottomscreen", "Bottom screen", null,
                    Editor("BottomScreenEditorView", "Edit", () => new Views.Graphics.BottomScreenEditorView())),
                new ScreenEntry(o, "pokegear", "Pokégear", Text("pokegear")),
            });
            yield return ("Title", new[]
            {
                new ScreenEntry(o, "title", "Title screen", null,
                    Editor("TitleScreenEditorView", "Edit", () => new Views.Graphics.TitleScreenEditorView())),
            });
        }
    }

    public sealed class ScreenTabViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private readonly GameTextScreens.Screen _screen;
        private bool _filled;

        public ScreenTabViewModel(GameTextScreens.Screen screen)
        {
            _screen = screen;
            int[] banks = screen.Banks();
            foreach (int bank in banks) Archives.Add(new ScreenArchiveChoice(bank, banks.Length > 1 ? FirstLineOf(bank) : null));
            States = screen.States ?? Array.Empty<string>();
            if (HasSample) PartySample.Changed += OnSampleChanged;
        }

        // ── the sample party, for the party screen ──

        public bool HasSample => _screen.Key == "party";

        private ScreenWindowLayoutViewModel _layout;

        /// <summary>Draws the layout's unsaved edits from now on; null draws the ROM's.</summary>
        public void Follow(ScreenWindowLayoutViewModel layout)
        {
            if (ReferenceEquals(_layout, layout)) return;
            if (_layout != null) _layout.Changed -= OnLayoutChanged;
            _layout = layout;
            if (_layout != null) _layout.Changed += OnLayoutChanged;
            OnLayoutChanged();
        }

        private void OnLayoutChanged()
        {
            if (_filled) _ = RedrawAsync();
        }

        private IDisposable DrawScope() => _layout?.DrawScope();

        private void OnSampleChanged()
        {
            RaiseSample();
            if (_filled) _ = RedrawAsync();
        }

        public void Detach() { if (HasSample) PartySample.Changed -= OnSampleChanged; }

        private List<string> _species;
        /// <summary>Species names by number; 0 leaves the slot empty.</summary>
        public List<string> SpeciesNames
        {
            get
            {
                if (_species != null) return _species;
                try { _species = new TextArchive(PokemonNamesTextNumbers[0]).Messages.Take(494).ToList(); }
                catch (Exception ex) { AppLogger.WarnOnce("ScreensEditor.Species", ex); _species = new List<string>(); }
                if (_species.Count > 0) _species[0] = "Empty";
                return _species;
            }
        }

        // A fixed list: rebuilding it resets the box's selection, which sets the Pokémon again and loops.
        public List<string> SampleSlots { get; } = Enumerable.Range(1, 6).Select(i => $"Pokémon {i}").ToList();

        private int _slot;
        public int SampleIndex
        {
            get => _slot;
            set
            {
                if (value < 0 || value > 5) return;
                _slot = value;
                RaiseSample();
                _litBySlot = true;
                _scrollToLit = true;
                LightSlot();
            }
        }

        private PartySample.Pokemon Who => PartySample.Party[_slot];

        private void Change(Action<PartySample.Pokemon> change)
        {
            change(Who);
            Who.Hp = Math.Clamp(Who.Hp, 0, Math.Max(1, Who.MaxHp));
            PartySample.Raise();
        }

        public int SampleSpecies { get => Who.Species; set { if (value >= 0 && value != Who.Species) Change(w => w.Species = value); } }
        public decimal SampleLevel { get => Who.Level; set { if ((int)value != Who.Level) Change(w => w.Level = (int)value); } }
        public decimal SampleHp { get => Who.Hp; set { if ((int)value != Who.Hp) Change(w => w.Hp = (int)value); } }
        public decimal SampleMaxHp { get => Who.MaxHp; set { if ((int)value != Who.MaxHp) Change(w => w.MaxHp = Math.Max(1, (int)value)); } }
        public int SampleGender { get => Who.Gender; set { if (value >= 0 && value != Who.Gender) Change(w => w.Gender = value); } }
        public int SampleCondition { get => Who.Condition; set { if (value >= 0 && value != Who.Condition) Change(w => w.Condition = value); } }
        public int SampleItem { get => Who.Item; set { if (value >= 0 && value != Who.Item) Change(w => w.Item = value); } }
        public bool SampleEgg { get => Who.Egg; set { if (value != Who.Egg) Change(w => w.Egg = value); } }

        private void RaiseSample()
        {
            foreach (string n in new[] { nameof(SampleIndex),nameof(SampleSpecies), nameof(SampleLevel), nameof(SampleHp),
                                         nameof(SampleMaxHp), nameof(SampleGender), nameof(SampleCondition), nameof(SampleItem), nameof(SampleEgg) })
                Raise(n);
        }

        /// <summary>The states the screen can be shown in; empty when it only has one.</summary>
        public string[] States { get; }
        public bool HasStates => States.Length > 0;

        private int _state;
        public int StateIndex
        {
            get => _state;
            set
            {
                if (value < 0 || value == _state) return;
                _state = value;
                Raise();
                _stop = 0;
                _ = RedrawAsync();
            }
        }

        // Picking a line printed in one state shows that state.
        private void FollowLine()
        {
            int wanted = _screen.StateOf == null || _line == null || _archive == null ? -1 : _screen.StateOf(_archive.Bank, _line.Index);
            if (wanted < 0 || wanted == _state) return;
            _state = wanted;
            Raise(nameof(StateIndex));
        }

        // An archive's first line names a caller or radio show, which tells the archives apart.
        private static string FirstLineOf(int bank)
        {
            try
            {
                List<string> lines = new TextArchive(bank).Messages;
                string first = lines.Count > 0 ? lines[0] ?? "" : "";
                first = ScreenLineRow.Readable(first);
                return first.Length > 32 ? first.Substring(0, 32) + "…" : first;
            }
            catch (Exception ex) { AppLogger.WarnOnce("ScreensEditor.FirstLine", ex); return null; }
        }

        public string Key => _screen.Key;
        public string Title => _screen.Title;

        public ObservableCollection<ScreenArchiveChoice> Archives { get; } = new ObservableCollection<ScreenArchiveChoice>();
        public bool HasSeveralArchives => Archives.Count > 1;

        private ScreenArchiveChoice _archive;
        public ScreenArchiveChoice SelectedArchive
        {
            get => _archive;
            set
            {
                if (ReferenceEquals(_archive, value) || value == null) return;
                _archive = value;
                Raise();
                FillLines(_screen.FirstLine);
            }
        }

        public ObservableCollection<ScreenLineRow> Lines { get; } = new ObservableCollection<ScreenLineRow>();
        public string StatusText => Lines.Count == 1 ? "1 line" : $"{Lines.Count} lines";

        private ScreenLineRow _line;
        public ScreenLineRow SelectedLine
        {
            get => _line;
            set
            {
                if (ReferenceEquals(_line, value)) return;
                _line = value;
                _stop = 0;
                Raise();
                FollowLine();
                _ = RedrawAsync();
            }
        }

        private Bitmap _picture;
        public Bitmap Picture { get => _picture; private set { _picture = value; Raise(); } }

        private string _pictureName = "";
        public string PictureName { get => _pictureName; private set { _pictureName = value; Raise(); } }

        private string _note = "";
        /// <summary>Shown when the chosen line isn't printed on this screen.</summary>
        public string Note { get => _note; private set { _note = value; Raise(); Raise(nameof(HasNote)); } }
        public bool HasNote => !string.IsNullOrEmpty(_note);

        public ObservableCollection<ScreenPieceRow> Pieces { get; } = new ObservableCollection<ScreenPieceRow>();

        // ── which pieces draw what ──

        private GameTextScreens.Shot _shot;
        private bool[][] _areas;
        private HashSet<(DirNames, int)> _lit = new HashSet<(DirNames, int)>();
        private bool _litBySlot, _scrollToLit;

        /// <summary>Raised when a piece lights up, so the pieces list can scroll to it.</summary>
        public event Action<ScreenPieceRow> Lit;

        private Bitmap _outline;
        public Bitmap Outline { get => _outline; private set { _outline = value; Raise(); } }

        /// <summary>Lights the pieces that draw the screen pixel at x, y.</summary>
        public void PickAt(int x, int y)
        {
            if (_areas == null || x < 0 || y < 0 || x >= 256 || y >= 192) return;
            int at = y * 256 + x;
            _lit = new HashSet<(DirNames, int)>();
            for (int i = 0; i < Pieces.Count && i < _areas.Length; i++)
                if (_areas[i] != null && _areas[i][at]) _lit.Add((Pieces[i].Dir, Pieces[i].Member));
            _litBySlot = false;
            _scrollToLit = true;
            ApplyLit();
        }

        public void PointAt(ScreenPieceRow row)
        {
            int i = row == null ? -1 : Pieces.IndexOf(row);
            Outline = _areas == null || i < 0 || i >= _areas.Length ? null : OutlineOf(_areas[i]);
        }

        private void LightSlot()
        {
            if (!_litBySlot || _shot == null) return;
            _lit = _shot.PokemonPieces.Where(p => p.Who == _slot).Select(p => (p.Dir, p.Member)).ToHashSet();
            ApplyLit();
        }

        private void ApplyLit()
        {
            ScreenPieceRow first = null;
            foreach (ScreenPieceRow row in Pieces)
            {
                row.IsLit = _lit.Contains((row.Dir, row.Member));
                if (row.IsLit) first ??= row;
            }
            if (_scrollToLit && first != null) Lit?.Invoke(first);
            _scrollToLit = false;
        }

        // Tinted, with a solid edge so thin pieces still show.
        private static Bitmap OutlineOf(bool[] area)
        {
            if (area == null || !area.Contains(true)) return null;
            const int w = 256, h = 192;
            byte[] rgba = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (!area[y * w + x]) continue;
                    bool edge = x == 0 || y == 0 || x == w - 1 || y == h - 1
                                || !area[y * w + x - 1] || !area[y * w + x + 1] || !area[(y - 1) * w + x] || !area[(y + 1) * w + x];
                    int at = (y * w + x) * 4;
                    rgba[at] = 255; rgba[at + 1] = 40; rgba[at + 2] = 160; rgba[at + 3] = (byte)(edge ? 255 : 90);
                }
            return ImageConverter.FromRgba(rgba, w, h);
        }

        private async Task FindAreasAsync(int asked, GameTextScreens.Shot shot, int bank, int line, string text, int stop, int state)
        {
            bool[][] areas = null;
            try
            {
                areas = await Task.Run(() =>
                {
                    using (DrawScope()) return GameTextScreens.Areas(_screen, bank, line, text, ArchiveLine, stop, state, shot, () => asked != _asked);
                });
            }
            catch (Exception ex) { AppLogger.WarnOnce("ScreensEditor.Areas", ex); }
            if (asked == _asked) _areas = areas;
        }

        private int _stop, _stops = 1;
        private bool _byItself;
        public bool HasStops => _stops > 1;
        public bool WaitsForPress => _stops > 1 && !_byItself;
        public string StopText => _stops > 1 ? $"Box {_stop % _stops + 1} of {_stops}" : "";

        public void NextStop()
        {
            if (_stops < 2) return;
            _stop = (_stop + 1) % _stops;
            _ = RedrawAsync();
        }

        private global::Avalonia.Threading.DispatcherTimer _play;

        /// <summary>Reads the lines the first time the tab shows, and starts screens that move on by themselves.</summary>
        public void Enter()
        {
            if (!_filled)
            {
                _filled = true;
                _archive = Archives.FirstOrDefault();
                Raise(nameof(SelectedArchive));
                FillLines(_screen.FirstLine);
            }
            UpdatePlay();
        }

        public void Leave()
        {
            _play?.Stop();
            _play = null;
        }

        public void Show(int bank, int line)
        {
            Enter();
            ScreenArchiveChoice archive = Archives.FirstOrDefault(a => a.Bank == bank);
            if (archive != null && !ReferenceEquals(archive, _archive))
            {
                _archive = archive;
                Raise(nameof(SelectedArchive));
                FillLines(line >= 0 ? line : _screen.FirstLine);
                return;
            }
            ScreenLineRow row = Lines.FirstOrDefault(l => l.Index == line);
            if (row != null) SelectedLine = row;
        }

        private readonly Dictionary<int, List<string>> _archiveLines = new Dictionary<int, List<string>>();

        private List<string> Read(int bank)
        {
            if (_archiveLines.TryGetValue(bank, out List<string> lines)) return lines;
            try { lines = new TextArchive(bank).Messages; } catch (Exception ex) { AppLogger.WarnOnce("ScreensEditor.Read", ex); lines = new List<string>(); }
            _archiveLines[bank] = lines;
            return lines;
        }

        private string ArchiveLine(int bank, int line)
        {
            List<string> lines = Read(bank);
            return line >= 0 && line < lines.Count ? lines[line] : "";
        }

        private void FillLines(int pick)
        {
            Lines.Clear();
            if (_archive == null) return;
            _archiveLines.Remove(_archive.Bank);
            List<string> lines = Read(_archive.Bank);
            // Empty lines print nothing, so they are left out.
            for (int i = 0; i < lines.Count; i++)
                if (!string.IsNullOrWhiteSpace(lines[i])) Lines.Add(new ScreenLineRow(i, lines[i]));
            _line = null;
            SelectedLine = Lines.FirstOrDefault(l => l.Index >= pick) ?? Lines.LastOrDefault();
            Raise(nameof(StatusText));
        }

        private int _asked;

        private async Task RedrawAsync()
        {
            int asked = ++_asked;
            ScreenLineRow row = _line;
            ScreenArchiveChoice archive = _archive;
            if (row == null || archive == null) { Picture = null; return; }
            int stop = _stop, state = HasStates ? _state : -1;
            GameTextScreens.Shot shot = null;
            try
            {
                shot = await Task.Run(() =>
                {
                    using (DrawScope()) return GameTextScreens.Draw(_screen, archive.Bank, row.Index, row.Text, ArchiveLine, stop, state);
                });
            }
            catch (Exception ex) { AppLogger.WarnOnce("ScreensEditor.Draw", ex); }
            if (asked != _asked) return;

            Pieces.Clear();
            _shot = shot;
            _areas = null;
            Outline = null;
            if (shot == null)
            {
                Picture = null;
                PictureName = "";
                Note = "Not on this screen";
                _stops = 1;
            }
            else
            {
                Picture = ImageConverter.FromRgba(shot.Rgba, 256, 192);
                PictureName = GameTextScreens.Caption(shot.Name);
                Note = "";
                _stops = Math.Max(1, shot.Stops);
                _byItself = shot.MovesOnByItself;
                // The archive is only named when pieces come from more than one.
                bool several = shot.Pieces.Select(p => p.Dir).Distinct().Count() > 1;
                foreach ((DirNames dir, int member, string kind) in shot.Pieces) Pieces.Add(new ScreenPieceRow(dir, member, kind, several));
                if (_litBySlot) LightSlot(); else ApplyLit();
                _ = FindAreasAsync(asked, shot, archive.Bank, row.Index, row.Text, stop, state);
            }
            Raise(nameof(HasStops));
            Raise(nameof(WaitsForPress));
            Raise(nameof(StopText));
            UpdatePlay();
        }

        // A screen that moves on by itself shows each box for about as long as the game does.
        private void UpdatePlay()
        {
            bool play = _stops > 1 && _byItself;
            if (play && _play == null)
            {
                _play = new global::Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                _play.Tick += (_, _) => NextStop();
                _play.Start();
            }
            else if (!play) Leave();
        }

        public void EditText()
        {
            if (_archive != null && _line != null) AvaloniaEditorLauncher.OpenTextEditorAt(_archive.Bank, _line.Index);
        }
    }

    public sealed class ScreenArchiveChoice
    {
        public ScreenArchiveChoice(int bank, string first) { Bank = bank; _first = first; }
        private readonly string _first;
        public int Bank { get; }
        public string Label => string.IsNullOrEmpty(_first) ? $"Text archive {Bank}" : $"{Bank}  {_first}";
    }

    public sealed class ScreenLineRow
    {
        public ScreenLineRow(int index, string text) { Index = index; Text = text ?? ""; }
        public int Index { get; }
        public string Text { get; }
        /// <summary>The line as the archive holds it, placeholders included, line breaks as spaces.</summary>
        public string Label => $"{Index}  {Text.Replace("\\n", " ").Replace("\\r", " ").Replace("\\f", " ").Trim()}";
        public string Full => Text.Replace("\\n", "\n").Replace("\\r", "\n").Replace("\\f", "\n");

        private static string Words(string text) =>
            global::System.Text.RegularExpressions.Regex.Replace(FieldStringVars.Expand(text ?? "", GameTextScreens.SampleWord), @"\{[^}]*\}", "");

        /// <summary>Sample words for placeholders, line breaks as spaces.</summary>
        internal static string Readable(string text) => Words(text).Replace("\\n", " ").Replace("\\r", " ").Replace("\\f", " ").Trim();
    }

    /// <summary>An archive file the screen is drawn from, opened in All graphics.</summary>
    public sealed class ScreenPieceRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private bool _lit;
        /// <summary>True when it draws the picked spot or the picked sample Pokémon.</summary>
        public bool IsLit
        {
            get => _lit;
            set
            {
                if (_lit == value) return;
                _lit = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLit)));
            }
        }

        public ScreenPieceRow(DirNames dir, int member, string kind, bool nameArchive)
        {
            Dir = dir;
            Member = member;
            GraphicAssets.Archive archive = GraphicAssets.All.FirstOrDefault(a => a.Dir == dir);
            string name = null;
            try { name = archive?.NameOf?.Invoke(member); } catch (Exception ex) { AppLogger.WarnOnce("ScreensEditor.PieceName", ex); }
            Name = string.IsNullOrEmpty(name) ? kind ?? "File" : name;
            Where = nameArchive ? $"{archive?.Title ?? dir.ToString()}, file {member}" : $"File {member}";
        }

        public DirNames Dir { get; }
        public int Member { get; }
        public string Name { get; }
        public string Where { get; }

        public void Open() => AvaloniaEditorLauncher.OpenGraphicAt(Dir, Member);
    }
}
