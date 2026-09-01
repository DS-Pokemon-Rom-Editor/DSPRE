using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Media.Imaging;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.ViewModels
{
    /// <summary>
    /// Every 2D graphic in the game in one list, with a picture of whatever is picked.
    ///
    /// Grouped by what a thing is for rather than by which archive it sits in, because nobody looks for
    /// "the ninth entry of pl_batt_obj". Anything that cannot be shown, saved or replaced says why in
    /// words, which is the part worth copying from the audio editor: no control is ever just greyed out.
    /// </summary>
    public sealed class GraphicsBrowserViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T field, T value, [CallerMemberName] string n = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value; OnPropertyChanged(n); return true;
        }

        /// <summary>One entry of one archive, as it appears in the list.</summary>
        /// <summary>Properties, not fields: the compiled bindings this project uses cannot see fields.</summary>
        /// <summary>One row of the list: a whole thing, which is usually made of several files.</summary>
        public sealed class Item
        {
            public GraphicAssets.Archive Archive { get; init; }
            public GraphicAssets.Group In { get; init; }
            /// <summary>The first of this thing's files, and what is shown when nothing else is picked.</summary>
            public int Index { get; init; }
            public GraphicAssets.Unit Unit { get; init; }
            /// <summary>What this thing is called. The games name most of them, so say the name rather
            /// than repeating the archive down every row of the list.</summary>
            public string Name { get; init; }
            public string Label => Name == null
                ? $"{Index,5}  {Archive.Title}"
                : $"{Index,5}  {Name}";
            public string Search { get; init; }   // lowercased, for filtering
        }

        /// <summary>One file of the thing picked, offered underneath it.</summary>
        public sealed class Part
        {
            public int Index { get; init; }
            /// <summary>Usually the row's own archive, but a model's pictures and movement live elsewhere.</summary>
            public GraphicAssets.Archive Archive { get; init; }
            public string Name { get; init; }
            public string Label => Name;
        }

        /// <summary>One tab: a kind of graphic, and how many of them this game has.</summary>
        public sealed class CategoryTab
        {
            public string Title { get; init; }
            public GraphicAssets.Group? Only { get; init; }   // null means everything
            public int Count { get; init; }
            public string What { get; init; }
            public string Header => $"{Title} ({Count})";
        }

        private readonly List<Item> _everything = new();

        public GraphicsBrowserViewModel()
        {
            Reload();
        }

        /// <summary>
        /// Opens the window already looking at one file, for an editor handing a graphic over.
        ///
        /// The editors that already know a graphic should be able to pass it here rather than making
        /// somebody find it again in a list of six thousand. Which row a file belongs to is worked out
        /// from the grouping, so the Pokemon Editor can say "this Pokemon's icon" and the right row and
        /// the right piece of it both light up.
        /// </summary>
        public bool JumpTo(GraphicAssets.Archive archive, int fileIndex)
        {
            if (archive == null) return false;

            // Land on the tab that holds it, so the row is not filtered out of view.
            var tab = Tabs.FirstOrDefault(t => t.Only == archive.In) ?? Tabs.FirstOrDefault();
            if (tab != null && !ReferenceEquals(tab, _selectedTab))
            {
                _selectedTab = tab;
                OnPropertyChanged(nameof(SelectedTab));
            }
            Search = "";

            var row = _everything.FirstOrDefault(i => i.Archive.Dir == archive.Dir
                                                   && i.Unit != null
                                                   && i.Unit.Parts.Any(pt => pt.Index == fileIndex
                                                        && (pt.Archive == null || pt.Archive.Dir == archive.Dir)))
                   ?? _everything.FirstOrDefault(i => i.Archive.Dir == archive.Dir && i.Index == fileIndex);
            if (row == null) return false;

            if (!Shown.Contains(row)) Shown.Insert(0, row);
            Selected = row;

            int at = Parts.ToList().FindIndex(pt => pt.Index == fileIndex);
            if (at >= 0) PartIndex = at;
            return true;
        }

        /// <summary>Builds the list from whatever the open game actually has.</summary>
        public void Reload()
        {
            _everything.Clear();
            Tabs.Clear();
            GraphicAssets.Forget();

            foreach (var g in Enum.GetValues<GraphicAssets.Group>())
            {
                int inGroup = 0;
                // A row can name its own tab, so an archive holding several different things is read for
                // every tab rather than only its own.
                foreach (var a in GraphicAssets.All)
                {
                    int n;
                    try { n = GraphicAssets.Count(a); } catch { n = 0; }
                    if (n == 0) continue;      // this game does not have it

                    var units = GraphicAssets.Units(a, n);
                    if (a.In != g && !units.Any(u => u.In == g)) continue;

                    foreach (var u in units)
                    {
                        if ((u.In ?? a.In) != g) continue;
                        // Count the things, not the files: a row is a whole Pokemon or a whole trainer now,
                        // so counting files made the tab claim six times as many as it lists.
                        inGroup++;
                        _everything.Add(new Item
                        {
                            Archive = a, In = g, Index = u.First, Name = u.Name, Unit = u,
                            Search = (a.Title + " " + u.First + " " + a.What + " " + u.Name).ToLowerInvariant(),
                        });
                    }
                }
                if (inGroup > 0)
                    Tabs.Add(new CategoryTab { Title = FriendlyGroup(g), Only = g, Count = inGroup,
                                               What = WhatIsOn(g) });
            }

            if (Tabs.Count > 0)
                Tabs.Insert(0, new CategoryTab { Title = "Everything", Only = null, Count = _everything.Count,
                                                 What = "Every flat graphic this game has." });
            _selectedTab = Tabs.FirstOrDefault();
            OnPropertyChanged(nameof(SelectedTab));

            ApplyFilter();
            OnPropertyChanged(nameof(FoundSummary));
        }

        /// <summary>Which archives feed a tab. A tab can be fed by part of an archive, so this asks the
        /// rows rather than assuming an archive belongs to one tab.</summary>
        private string WhatIsOn(GraphicAssets.Group g)
        {
            var titles = _everything.Where(i => i.In == g).Select(i => i.Archive.Title).Distinct();
            return string.Join("  ", titles);
        }

        private static string FriendlyGroup(GraphicAssets.Group g) => g switch
        {
            GraphicAssets.Group.PokemonSprites => "Pokemon sprites",
            GraphicAssets.Group.PokemonIcons => "Pokemon icons",
            GraphicAssets.Group.Trainers => "Trainers",
            GraphicAssets.Group.BattleScenery => "Battle scenery",
            GraphicAssets.Group.BattleGauges => "Battle HP bars",
            GraphicAssets.Group.BattleIcons => "Battle icons",
            GraphicAssets.Group.BattleChrome => "Battle screen",
            GraphicAssets.Group.MoveEffects => "Move effects",
            GraphicAssets.Group.Items => "Items",
            GraphicAssets.Group.TextAndFonts => "Fonts",
            GraphicAssets.Group.Windows => "Text boxes",
            GraphicAssets.Group.Places => "Places",
            _ => g.ToString(),
        };

        public ObservableCollection<CategoryTab> Tabs { get; } = new();
        public ObservableCollection<Item> Shown { get; } = new();

        private CategoryTab _selectedTab;
        public CategoryTab SelectedTab
        {
            get => _selectedTab;
            set { if (Set(ref _selectedTab, value)) ApplyFilter(); }
        }

        private string _search = "";
        public string Search
        {
            get => _search;
            set { if (Set(ref _search, value)) ApplyFilter(); }
        }

        private void ApplyFilter()
        {
            Shown.Clear();
            string q = (_search ?? "").Trim().ToLowerInvariant();
            IEnumerable<Item> hits = _everything;
            if (_selectedTab?.Only != null) hits = hits.Where(i => i.In == _selectedTab.Only.Value);
            if (!string.IsNullOrEmpty(q)) hits = hits.Where(i => i.Search.Contains(q));
            // Thousands of entries would make the list crawl, so show the first slice and say so.
            foreach (var i in hits.Take(ShowAtMost)) Shown.Add(i);
            OnPropertyChanged(nameof(FoundSummary));
        }

        private const int ShowAtMost = 3000;

        public string FoundSummary
        {
            get
            {
                string q = (_search ?? "").Trim();
                int total = _everything.Count;
                if (total == 0) return "This game has no graphics DSPRE can list. Open a ROM first.";
                int here = _selectedTab?.Count ?? total;
                string what = _selectedTab?.Only == null ? "in this game" : "under " + _selectedTab.Title;
                if (string.IsNullOrEmpty(q))
                    return Shown.Count < here
                        ? $"{here} graphics {what}. Showing the first {Shown.Count}; type to narrow it down."
                        : $"{here} graphics {what}.";
                return Shown.Count == 0
                    ? $"Nothing {what} matches \"{q}\". Try part of a name, like trainer or icon."
                    : $"{Shown.Count} of {here} {what} match \"{q}\".";
            }
        }

        /// <summary>The files that make up whatever is picked. Only shown when there is more than one,
        /// because a single file needs no choosing between.</summary>
        public ObservableCollection<Part> Parts { get; } = new();
        public bool HasParts => Parts.Count > 1;

        private void BuildParts()
        {
            Parts.Clear();
            if (_selected?.Unit != null && _selected.Unit.Parts.Count > 1)
                foreach (var up in _selected.Unit.Parts)
                    Parts.Add(new Part { Index = up.Index, Archive = up.Archive, Name = up.Name });

            _partIndex = -1;
            OnPropertyChanged(nameof(PartIndex));
            _partIndex = Parts.Count > 0 ? 0 : -1;
            OnPropertyChanged(nameof(PartIndex));
            OnPropertyChanged(nameof(HasParts));
        }

        private int _partIndex = -1;
        public int PartIndex
        {
            get => _partIndex;
            set
            {
                if (!Set(ref _partIndex, value) || _selected == null) return;
                OnPropertyChanged(nameof(CanShowShiny));
                OnPropertyChanged(nameof(ShinyHelp));
                OnPropertyChanged(nameof(HasOwningEditor));
                OnPropertyChanged(nameof(OwningEditorLabel));
                OnPropertyChanged(nameof(OwningEditorHelp));
                Look();
            }
        }

        /// <summary>What the file being shown is called, falling back to the archive's own name where
        /// the game does not name its files.</summary>
        private string Showing(GraphicAssets.Archive a)
        {
            var arc = ShowingArchive ?? a;
            try
            {
                string name = arc.NameOf?.Invoke(ShowingIndex);
                if (!string.IsNullOrWhiteSpace(name)) return name;
            }
            catch { }
            return arc.Title;
        }

        /// <summary>The file actually being shown: the piece picked, or the thing itself when it is one
        /// file on its own.</summary>
        public int ShowingIndex => _partIndex >= 0 && _partIndex < Parts.Count
            ? Parts[_partIndex].Index : _selected?.Index ?? 0;

        /// <summary>The archive the piece being shown lives in.</summary>
        public GraphicAssets.Archive ShowingArchive =>
            (_partIndex >= 0 && _partIndex < Parts.Count ? Parts[_partIndex].Archive : null)
            ?? _selected?.Archive;

        /// <summary>The editor that decides this graphic's numbers, when one does. The hand-off goes both
        /// ways: an editor can send a graphic to the brush, and the brush can send you back to what owns
        /// it.</summary>
        public string OwningEditorName
        {
            get
            {
                var a = ShowingArchive;
                if (a == null) return null;
                try { return AvaloniaEditorLauncher.EditorForGraphic(a.Dir, ShowingIndex)?.Name; }
                catch { return null; }
            }
        }

        public bool HasOwningEditor => OwningEditorName != null;

        public string OwningEditorLabel => HasOwningEditor
            ? $"Edit in the {OwningEditorName}…" : "Edit its properties…";

        public string OwningEditorHelp => HasOwningEditor
            ? $"Open the {OwningEditorName}, which decides everything about this other than how it looks."
            : "Nothing in DSPRE owns this graphic's numbers, so there is nowhere else to send you.";

        /// <summary>Opens whatever owns this graphic. Does nothing when nothing does.</summary>
        public void OpenOwningEditor()
        {
            var a = ShowingArchive;
            if (a == null) return;
            try { AvaloniaEditorLauncher.EditorForGraphic(a.Dir, ShowingIndex)?.Open?.Invoke(); }
            catch (Exception ex) { AppLogger.Error("OpenOwningEditor failed: " + ex.Message); }
        }

        private bool _showShiny;
        /// <summary>Show a Pokemon in its shiny colours. The drawing is the same either way; only which
        /// of its two sets of colours is used changes, which is all shiny means in these games.</summary>
        public bool ShowShiny
        {
            get => _showShiny;
            set { if (Set(ref _showShiny, value) && _selected != null) Look(); }
        }

        public bool CanShowShiny => ShowingArchive?.ShinyColourEntry != null;

        public string ShinyHelp => CanShowShiny
            ? "Show this in its shiny colours. It is the same drawing; only the colours change."
            : "This kind of graphic has no second set of colours, so there is no shiny to show.";

        private Item _selected;
        public Item Selected
        {
            get => _selected;
            set
            {
                if (!Set(ref _selected, value)) return;
                BuildParts();
                OnPropertyChanged(nameof(CanShowShiny));
                OnPropertyChanged(nameof(ShinyHelp));
                OnPropertyChanged(nameof(HasOwningEditor));
                OnPropertyChanged(nameof(OwningEditorLabel));
                OnPropertyChanged(nameof(OwningEditorHelp));
                Look();
            }
        }

        private Bitmap _picture;
        public Bitmap Picture { get => _picture; private set => Set(ref _picture, value); }

        private string _whynot = "";
        /// <summary>Why there is no picture, when there is not one. Empty when there is.</summary>
        public string Whynot { get => _whynot; private set => Set(ref _whynot, value); }
        public bool HasPicture => _picture != null;
        public bool HasNoPicture => _picture == null && !string.IsNullOrEmpty(_whynot);

        private string _details = "Pick something on the left to see it.";
        public string Details { get => _details; private set => Set(ref _details, value); }

        private void Look()
        {
            Picture = null;
            Whynot = "";
            if (_selected == null)
            {
                Details = "Pick something on the left to see it.";
                RaiseAll();
                return;
            }

            var a = _selected.Archive;
            try
            {
                var p = GraphicAssets.Render(ShowingArchive ?? a, ShowingIndex, _showShiny);
                if (p.Rgba != null && p.Width > 0)
                {
                    Picture = ImageConverter.FromRgba(p.Rgba, p.Width, p.Height);
                    SizeToShow(p.Width, p.Height);
                    Details = $"{Showing(a)}, number {ShowingIndex}. {p.Width} by {p.Height}. {a.What}";
                }
                else
                {
                    Whynot = p.Whynot ?? "There is no picture in this entry.";
                    Details = $"{Showing(a)}, number {ShowingIndex}. {a.What}";
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("GraphicsBrowser.Look failed: " + ex.Message);
                Whynot = "This entry could not be opened.";
            }
            RaiseAll();
        }

        private void RaiseAll()
        {
            OnPropertyChanged(nameof(HasPicture));
            OnPropertyChanged(nameof(HasNoPicture));
            OnPropertyChanged(nameof(CanSavePicture));
            OnPropertyChanged(nameof(CanReplace));
            OnPropertyChanged(nameof(ReplaceHelp));
            OnPropertyChanged(nameof(SavePictureHelp));
            OnPropertyChanged(nameof(DeepEditorName));
            OnPropertyChanged(nameof(HasDeepEditor));
            OnPropertyChanged(nameof(DeepEditorHint));
        }

        // ── what can be done with what is picked ───────────────────────────────────────────────────

        public bool CanSavePicture => _selected != null && _picture != null;

        public string SavePictureHelp => _selected == null
            ? "Pick something first."
            : _picture != null
                ? "Save this as a PNG that keeps its numbered colours, so it can be put back in unchanged."
                : "There is no picture in this entry to save. The whole file can still be saved as it is.";

        public bool CanReplace => _selected != null && _selected.Archive.CannotImportBecause == null
                                  && _picture != null;

        public string ReplaceHelp
        {
            get
            {
                if (_selected == null) return "Pick something first.";
                if (_selected.Archive.CannotImportBecause != null) return _selected.Archive.CannotImportBecause;
                if (_picture == null)
                    return "This entry has no picture in it, so a PNG cannot take its place. "
                         + (Whynot ?? "");
                return "Put a PNG in place of this. It has to be the same size and use the same numbered "
                     + "colours, so save this one first and paint over what comes out.";
            }
        }

        // Blown up to something readable, in whole steps so the pixels stay square, and never past a size
        // that would need scrolling for no reason.
        private int _shownW, _shownH;
        public int ShownWidth => _shownW;
        public int ShownHeight => _shownH;

        private void SizeToShow(int w, int h)
        {
            if (w <= 0 || h <= 0) { _shownW = _shownH = 0; return; }
            int step = 1;
            while ((w * (step + 1)) <= 384 && (h * (step + 1)) <= 384 && step < 8) step++;
            _shownW = w * step; _shownH = h * step;
            OnPropertyChanged(nameof(ShownWidth));
            OnPropertyChanged(nameof(ShownHeight));
        }

        public string DeepEditorHint => HasDeepEditor
            ? $"The {DeepEditorName} knows this kind of graphic properly, including how the pieces fit "
              + "together. Use it when you want more than the raw drawing."
            : "";

        public string DeepEditorName => _selected?.Archive.DeepEditor;
        public bool HasDeepEditor => !string.IsNullOrEmpty(DeepEditorName);

        private string _status = "";
        public string Status { get => _status; set => Set(ref _status, value); }

        /// <summary>A sensible file name for saving what is picked.</summary>
        public string SuggestedFileName(string extension)
        {
            if (_selected == null) return "graphic" + extension;
            string name = _selected.Archive.Title.ToLowerInvariant().Replace(' ', '_');
            foreach (char c in System.IO.Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return $"{name}_{ShowingIndex:D4}{extension}";
        }

        public string SavePicture(string path)
        {
            if (_selected == null) return "Pick something first.";
            return GraphicAssets.ExportPng(ShowingArchive ?? _selected.Archive, ShowingIndex, path);
        }

        public string SaveFileAsItIs(string path)
        {
            if (_selected == null) return "Pick something first.";
            return GraphicAssets.ExportRaw(ShowingArchive ?? _selected.Archive, ShowingIndex, path);
        }

        public string Replace(string path) => Replace(path, out _);

        /// <param name="note">Something worth telling somebody afterwards, or null. Painting a background
        /// changes every place drawn from the same piece, which is worth saying rather than leaving to be
        /// noticed.</param>
        public string Replace(string path, out string note)
        {
            note = null;
            if (_selected == null) return "Pick something first.";
            string err = GraphicAssets.ImportPng(ShowingArchive ?? _selected.Archive, ShowingIndex,
                                                 path, out note);
            if (err == null) Look();   // show what went in
            return err;
        }
    }
}
