using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using DSPRE.Avalonia.Data;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    /// <summary>One animation file that was found, and the files that go with it.</summary>
    public sealed class CellAnimationFound
    {
        public DirNames Archive { get; init; }
        public ArchiveFiles Source { get; init; }
        public string ArchiveName { get; init; }

        /// <summary>What the drawing shows, such as the Pokemon an icon belongs to.</summary>
        public string Label { get; init; }

        /// <summary>Which bank of the palette file the drawing is painted with.</summary>
        public int PaletteRow { get; init; }
        public int Animation { get; init; }
        public int Cells { get; init; }
        public int Sprites { get; init; }
        public int Palette { get; init; }
        public int Sequences { get; init; }
        public int Frames { get; init; }
        public bool Extended { get; init; }

        /// <summary>How many drawings the paired layout holds, and how many tiles the paired sheet has.</summary>
        public int Banks { get; init; }
        public int SheetTiles { get; init; }

        /// <summary>
        /// The editor that already specialises in this archive, if any. Trainer sprites and Pokemon sprites
        /// have their own editors that know about poses, genders and the hg-engine sources; this window edits
        /// the animation file itself, which is a different job, so it points at them rather than competing.
        /// </summary>
        public string DeepEditor { get; init; }

        public string Title => Label == null ? $"{ArchiveName}  #{Animation}" : $"{ArchiveName}  #{Animation}  {Label}";

        public string Detail
        {
            get
            {
                var bits = new List<string>
                {
                    $"{Sequences} sequence{(Sequences == 1 ? "" : "s")}",
                    $"{Frames} frame{(Frames == 1 ? "" : "s")}",
                };
                bits.Add(Cells >= 0 ? $"layout {Cells}" : "no layout in this archive");
                if (Cells >= 0 && Banks == 0) bits.Add("that layout is empty");
                if (Sprites >= 0 && SheetTiles == 0) bits.Add("its sheet is empty");
                if (Extended) bits.Add("extended block");
                return string.Join(", ", bits);
            }
        }

        /// <summary>
        /// Whether there is enough here to draw anything. Finding a layout and a sheet is not enough: a few
        /// archives pair an animation with files that turn out to be empty, and offering those opens an
        /// editor with a blank preview.
        /// </summary>
        public bool Drawable => Cells >= 0 && Sprites >= 0 && Banks > 0 && SheetTiles > 0;
    }

    /// <summary>
    /// Finding a cell animation to edit. An animation on its own draws nothing: it names cells in a
    /// layout, which names tiles in a sheet, which are painted with a set of colours. Those four files sit
    /// together in an archive, so they are found by looking either side of the animation.
    /// </summary>
    public sealed class CellAnimationPickerViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        {
            if (EqualityComparer<T>.Default.Equals(f, v)) return false;
            f = v; OnPropertyChanged(n); return true;
        }

        private readonly List<CellAnimationFound> _all = new();

        public CellAnimationPickerViewModel()
        {
        }

        /// <summary>
        /// Reads every archive looking for animations. This is the slow part, so it is a separate step the
        /// caller runs off the window's thread rather than something the constructor does.
        /// </summary>
        public void Gather()
        {
            if (Design.IsDesignMode) return;
            Look();
        }

        /// <summary>Shows what was found. Call this on the window's own thread once Gather has finished.</summary>
        public void Ready() => Show();

        public ObservableCollection<CellAnimationFound> Found { get; } = new();

        private string _search = "";
        public string Search { get => _search; set { if (Set(ref _search, value)) Show(); } }

        private bool _drawableOnly = true;
        /// <summary>Hides the ones with nothing to draw them from, which cannot usefully be opened.</summary>
        public bool DrawableOnly { get => _drawableOnly; set { if (Set(ref _drawableOnly, value)) Show(); } }

        private bool _hideSpecialised = true;
        /// <summary>
        /// Hides the archives whose own editor does this same job better. Trainer and Pokemon sprites are
        /// handled there, and listing their hundreds of animations as equals buries the archives this
        /// window is the only way to reach.
        /// </summary>
        public bool HideSpecialised
        {
            get => _hideSpecialised;
            set { if (Set(ref _hideSpecialised, value)) Show(); }
        }

        // These own poses, colours and source images. Frame order and timing are this window's job.
        private static readonly string[] OwnsTheArtwork =
        {
            "Trainer Sprite Editor", "Trainer Back Sprite Editor", "Pokemon Sprite Editor",
        };

        private static bool DoneBetterElsewhere(CellAnimationFound row) =>
            !string.IsNullOrEmpty(row.DeepEditor)
            && Array.IndexOf(OwnsTheArtwork, row.DeepEditor) >= 0;

        private int _selected = -1;
        public int SelectedIndex
        {
            get => _selected;
            set { if (Set(ref _selected, value)) RaiseSelection(); }
        }

        // The hint and the link button follow the picked row, so they have to be told when it changes.
        private void RaiseSelection()
        {
            OnPropertyChanged(nameof(Selected));
            OnPropertyChanged(nameof(DeepEditorName));
            OnPropertyChanged(nameof(HasDeepEditor));
            OnPropertyChanged(nameof(DeepEditorHint));
        }

        public CellAnimationFound Selected =>
            _selected >= 0 && _selected < Found.Count ? Found[_selected] : null;

        private string _status = "";
        public string StatusText { get => _status; private set => Set(ref _status, value); }

        /// <summary>
        /// The editor that specialises in the picked file's archive, if any. Trainer and Pokemon sprites
        /// have their own editors that know about poses, genders and hg-engine sources; this window edits
        /// the animation file itself. Saying so beats quietly doing a worse version of their job.
        /// </summary>
        public string DeepEditorName => Selected?.DeepEditor;
        public bool HasDeepEditor => !string.IsNullOrEmpty(DeepEditorName);
        public string DeepEditorHint => HasDeepEditor
            ? $"The {DeepEditorName} does more with these: poses, colours and the source files. This window "
              + "edits the animation itself, which it does not."
            : "";

        public string Summary => $"{_all.Count} animation files in this game"
                               + (_all.Count == 0 ? "" : $", {_all.Count(a => a.Drawable)} with art to draw");

        private void Show()
        {
            Found.Clear();
            IEnumerable<CellAnimationFound> rows = _all;
            if (_drawableOnly) rows = rows.Where(r => r.Drawable);
            if (!string.IsNullOrWhiteSpace(_search))
            {
                string q = _search.Trim();
                rows = rows.Where(r => r.ArchiveName.Contains(q, StringComparison.OrdinalIgnoreCase)
                                    || (r.Label?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                                    || r.Animation.ToString() == q);
            }

            // After the search, so the count describes what was held back from what is on screen.
            var matched = rows.ToList();
            int specialised = matched.Count(DoneBetterElsewhere);
            if (_hideSpecialised) matched = matched.Where(r => !DoneBetterElsewhere(r)).ToList();

            foreach (var r in matched) Found.Add(r);
            _selected = Found.Count > 0 ? 0 : -1;
            OnPropertyChanged(nameof(SelectedIndex));
            RaiseSelection();
            StatusText = $"{Found.Count} shown"
                       + (_hideSpecialised && specialised > 0
                            ? $", {specialised} hidden that another editor does better"
                            : "");
        }

        private void Look()
        {
            _all.Clear();
            _all.AddRange(Everywhere());
            OnPropertyChanged(nameof(Summary));
        }

        /// <summary>Every animation in the ROM, mapped archives first, then unmapped NARCs so none is missed.</summary>
        public static List<CellAnimationFound> Everywhere()
        {
            var found = new List<CellAnimationFound>();
            var mappedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var dir in gameDirs?.Keys.ToList() ?? new List<DirNames>())
            {
                try { mappedFiles.Add(Path.GetFullPath(Path.Combine(workDir ?? "", gameDirs[dir].packedDir))); } catch { }
                // That slot holds map headers once the dynamic headers patch is applied, not graphics.
                if (dir == DirNames.dynamicHeaders) continue;
                found.AddRange(InArchive(dir));
            }

            if (string.IsNullOrEmpty(dataPath) || !Directory.Exists(dataPath)) return found;
            foreach (string path in Directory.EnumerateFiles(dataPath, "*", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
            {
                string full;
                try { full = Path.GetFullPath(path); } catch { continue; }
                if (mappedFiles.Contains(full) || !StartsLikeANarc(full)) continue;
                string name = Path.GetRelativePath(dataPath, full).Replace('\\', '/');
                found.AddRange(InArchive(ArchiveFiles.Loose(full, name)));
            }
            return found;
        }

        private static bool StartsLikeANarc(string path)
        {
            try
            {
                using var s = File.OpenRead(path);
                var head = new byte[4];
                return s.Read(head, 0, 4) == 4 && head[0] == 'N' && head[1] == 'A' && head[2] == 'R' && head[3] == 'C';
            }
            catch { return false; }
        }

        /// <summary>Every animation in one archive, with the files each one draws from.</summary>
        public static List<CellAnimationFound> InArchive(DirNames dir) => InArchive(ArchiveFiles.Mapped(dir));

        public static List<CellAnimationFound> InArchive(ArchiveFiles narc)
        {
            var found = new List<CellAnimationFound>();
            try { if (!narc.Available) return found; } catch { return found; }
            DirNames dir = narc.Dir ?? default;

            // What each file in this archive is, so neighbours can be recognised.
            int count = narc.Count;
            var kinds = new GraphicAssets.Kind[count];
            var bytes = new byte[count][];
            for (int i = 0; i < count; i++)
            {
                try
                {
                    bytes[i] = NitroBgCodec.Inflate(narc.Get(i));
                    kinds[i] = GraphicAssets.Identify(bytes[i]);
                }
                catch { kinds[i] = GraphicAssets.Kind.Unknown; }
            }

            // This archive's own entry hooks, where it has them, beat a guess from neighbouring files.
            var described = narc.Dir == null ? null : GraphicAssets.All.FirstOrDefault(a => a.Dir == dir);

            // Reading a layout or a sheet is not free and the same one is asked about repeatedly, so each is
            // read once per archive.
            var banksAt = new Dictionary<int, List<DsBgScreen.Oam[]>>();
            var tilesAt = new Dictionary<int, int>();

            List<DsBgScreen.Oam[]> BanksOf(int at)
            {
                if (at < 0 || at >= count) return new List<DsBgScreen.Oam[]>();
                if (!banksAt.TryGetValue(at, out var banks))
                {
                    try { banks = DsBgScreen.ReadCells(bytes[at]); }
                    catch { banks = new List<DsBgScreen.Oam[]>(); }
                    banksAt[at] = banks;
                }
                return banks;
            }

            int TilesOf(int at)
            {
                if (at < 0 || at >= count) return 0;
                if (!tilesAt.TryGetValue(at, out int tiles))
                {
                    try { tiles = DsBgScreen.ReadCharacters(bytes[at]).Length / 32; }
                    catch { tiles = 0; }
                    tilesAt[at] = tiles;
                }
                return tiles;
            }

            // Every party icon shares one animation, so it is offered once per Pokemon with its own drawing and colour bank.
            if (narc.Dir == DirNames.monIcons)
            {
                const int IconAnimation = 1, IconLayout = 2, IconPalette = 0;
                var iconFile = IconAnimation < count && kinds[IconAnimation] == GraphicAssets.Kind.CellAnimation
                    ? NanrFile.Read(bytes[IconAnimation]) : null;
                if (iconFile != null)
                {
                    string[] names;
                    try { names = GetPokemonNames(); } catch { names = Array.Empty<string>(); }
                    int banks = BanksOf(IconLayout).Count;
                    for (int f = PokemonIconFiles.SharedFiles + 1; f < count; f++)
                    {
                        var icon = PokemonIconFiles.Describe(f);
                        if (icon == null) continue;
                        int row;
                        try { row = DSUtils.GetMonIconPaletteId(f - PokemonIconFiles.SharedFiles); } catch { row = 0; }
                        found.Add(new CellAnimationFound
                        {
                            Archive = dir, Source = narc, ArchiveName = narc.Name,
                            Label = PokemonIconFiles.Label(icon, names),
                            Animation = IconAnimation, Cells = IconLayout, Sprites = f, Palette = IconPalette, PaletteRow = row,
                            Sequences = iconFile.Sequences.Count,
                            Frames = iconFile.Sequences.Sum(s => s.Frames.Count),
                            Extended = iconFile.HasExtendedData,
                            Banks = banks, SheetTiles = TilesOf(f),
                            DeepEditor = described?.DeepEditor,
                        });
                    }
                }
            }

            for (int i = 0; i < count; i++)
            {
                if (narc.Dir == DirNames.monIcons) break;
                if (kinds[i] != GraphicAssets.Kind.CellAnimation) continue;
                var file = NanrFile.Read(bytes[i]);
                if (file == null) continue;

                // Which drawings this animation actually names. A layout too short to hold them is the wrong
                // layout however close it sits.
                var wanted = new SortedSet<int>();
                for (int s = 0; s < file.Sequences.Count; s++)
                    for (int f = 0; f < file.Sequences[s].Frames.Count; f++)
                    {
                        int cell = file.CellOf(s, f);
                        if (cell >= 0) wanted.Add(cell);
                    }
                int topCell = wanted.Count == 0 ? -1 : wanted.Max;

                int cells = BestFit(kinds, i, GraphicAssets.Kind.CellLayout,
                                    at => BanksOf(at).Count > topCell);
                var chosen = BanksOf(cells);
                int topTile = HighestTile(chosen, wanted);

                // A file the archive names outright beats one picked by distance, and still has to reach
                // the tiles those drawings read.
                int sprites = Declared(described?.DrawingEntry, i, kinds, GraphicAssets.Kind.TileGraphic);
                if (sprites >= 0 && topTile >= 0 && TilesOf(sprites) <= topTile) sprites = -1;
                if (sprites < 0)
                    sprites = BestFit(kinds, i, GraphicAssets.Kind.TileGraphic,
                                      at => TilesOf(at) > topTile);

                int colours = Declared(described?.ColourEntry, i, kinds, GraphicAssets.Kind.Palette);
                if (colours < 0) colours = Nearest(kinds, i, GraphicAssets.Kind.Palette);

                int paletteRow = 0;
                try { if (sprites >= 0 && described?.ColourBank != null) paletteRow = Math.Max(0, described.ColourBank(sprites)); } catch { }

                found.Add(new CellAnimationFound
                {
                    Archive = dir,
                    Source = narc,
                    ArchiveName = narc.Name,
                    PaletteRow = paletteRow,
                    Animation = i,
                    Cells = cells,
                    Sprites = sprites,
                    Palette = colours,
                    Sequences = file.Sequences.Count,
                    Frames = file.Sequences.Sum(s => s.Frames.Count),
                    Extended = file.HasExtendedData,
                    Banks = chosen.Count,
                    SheetTiles = TilesOf(sprites),
                    DeepEditor = described?.DeepEditor,
                });
            }
            return found;
        }

        /// <summary>
        /// The file an archive's own description points at, or -1 when it names none, points outside the
        /// archive, or points at something that is not the kind of file wanted.
        /// </summary>
        private static int Declared(Func<int, int> entry, int from, GraphicAssets.Kind[] kinds,
                                    GraphicAssets.Kind want)
        {
            if (entry == null) return -1;
            int at;
            try { at = entry(from); } catch { return -1; }
            if (at < 0 || at >= kinds.Length || at == from) return -1;
            return kinds[at] == want ? at : -1;
        }

        /// <summary>The highest tile the named drawings read, so a sheet that cannot reach it is ruled out.</summary>
        private static int HighestTile(List<DsBgScreen.Oam[]> banks, SortedSet<int> used)
        {
            int top = -1;
            foreach (int b in used)
            {
                if (b < 0 || b >= banks.Count) continue;
                foreach (var piece in banks[b])
                {
                    if (piece == null) continue;
                    int wide = Math.Max(1, piece.Width / 8), tall = Math.Max(1, piece.Height / 8);
                    top = Math.Max(top, piece.Tile + wide * tall - 1);
                }
            }
            return top;
        }

        // These archives keep a thing's files together, so the nearest one of a kind is usually right, and
        // the one before is preferred because colours and layouts are written ahead of what uses them.
        private static IEnumerable<int> Candidates(GraphicAssets.Kind[] kinds, int from, GraphicAssets.Kind want)
        {
            for (int step = 1; step < kinds.Length; step++)
            {
                int before = from - step;
                if (before >= 0 && kinds[before] == want) yield return before;
                int after = from + step;
                if (after < kinds.Length && kinds[after] == want) yield return after;
            }
        }

        private static int Nearest(GraphicAssets.Kind[] kinds, int from, GraphicAssets.Kind want)
        {
            foreach (int at in Candidates(kinds, from, want)) return at;
            return -1;
        }

        /// <summary>
        /// How many neighbours of a kind are worth considering. Enough to get past a file sitting between an
        /// animation and the sheet it belongs to, and not enough to wander off into another object's files.
        /// </summary>
        private const int Neighbourhood = 4;

        /// <summary>
        /// The nearest file of a kind that suits the animation, falling back to the nearest of that kind.
        /// The search stops after a few neighbours: a screen loading a shared sheet ahead of its own counts
        /// tiles across both, so its own sheet is legitimately too small to serve the animation.
        /// </summary>
        private static int BestFit(GraphicAssets.Kind[] kinds, int from, GraphicAssets.Kind want,
                                   Func<int, bool> fits)
        {
            int first = -1, looked = 0;
            foreach (int at in Candidates(kinds, from, want))
            {
                if (first < 0) first = at;
                if (fits(at)) return at;
                if (++looked >= Neighbourhood) break;
            }
            return first;
        }
    }
}
