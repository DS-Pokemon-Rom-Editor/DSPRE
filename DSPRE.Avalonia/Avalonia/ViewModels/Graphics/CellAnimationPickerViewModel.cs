using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using DSPRE.Avalonia.Data;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    /// <summary>One animation file that was found, and the files that go with it.</summary>
    public sealed class CellAnimationFound
    {
        public DirNames Archive { get; init; }
        public string ArchiveName { get; init; }
        public int Animation { get; init; }
        public int Cells { get; init; }
        public int Sprites { get; init; }
        public int Palette { get; init; }
        public int Sequences { get; init; }
        public int Frames { get; init; }
        public bool Extended { get; init; }

        public string Title => $"{ArchiveName}  #{Animation}";

        public string Detail
        {
            get
            {
                var bits = new List<string>
                {
                    $"{Sequences} sequence{(Sequences == 1 ? "" : "s")}",
                    $"{Frames} frame{(Frames == 1 ? "" : "s")}",
                };
                bits.Add(Cells >= 0 ? $"layout {Cells}" : "no layout found");
                if (Extended) bits.Add("extended block");
                return string.Join(", ", bits);
            }
        }

        /// <summary>Whether there is enough here to draw anything.</summary>
        public bool Drawable => Cells >= 0 && Sprites >= 0;
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

        private int _selected = -1;
        public int SelectedIndex { get => _selected; set => Set(ref _selected, value); }

        public CellAnimationFound Selected =>
            _selected >= 0 && _selected < Found.Count ? Found[_selected] : null;

        private string _status = "";
        public string StatusText { get => _status; private set => Set(ref _status, value); }

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
                                    || r.Animation.ToString() == q);
            }
            foreach (var r in rows) Found.Add(r);
            _selected = Found.Count > 0 ? 0 : -1;
            OnPropertyChanged(nameof(SelectedIndex));
            OnPropertyChanged(nameof(Selected));
            StatusText = $"{Found.Count} shown";
        }

        // Walks every archive the game maps and reads whatever turns out to be an animation.
        private void Look()
        {
            _all.Clear();
            var dirs = gameDirs?.Keys.ToList() ?? new List<DirNames>();
            foreach (var dir in dirs)
                _all.AddRange(InArchive(dir));
            OnPropertyChanged(nameof(Summary));
        }

        /// <summary>
        /// Every animation in one archive, with the files each one draws from. Separate and static so the
        /// pairing can be checked against what the games are known to do.
        /// </summary>
        public static List<CellAnimationFound> InArchive(DirNames dir)
        {
            var found = new List<CellAnimationFound>();
            ScriptNarc narc;
            try { narc = new ScriptNarc(dir); } catch { return found; }
            if (!narc.Available) return found;

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

            for (int i = 0; i < count; i++)
            {
                if (kinds[i] != GraphicAssets.Kind.CellAnimation) continue;
                var file = NanrFile.Read(bytes[i]);
                if (file == null) continue;

                found.Add(new CellAnimationFound
                {
                    Archive = dir,
                    ArchiveName = dir.ToString(),
                    Animation = i,
                    Cells = Nearest(kinds, i, GraphicAssets.Kind.CellLayout),
                    Sprites = Nearest(kinds, i, GraphicAssets.Kind.TileGraphic),
                    Palette = Nearest(kinds, i, GraphicAssets.Kind.Palette),
                    Sequences = file.Sequences.Count,
                    Frames = file.Sequences.Sum(s => s.Frames.Count),
                    Extended = file.HasExtendedData,
                });
            }
            return found;
        }

        // These archives keep a thing's files together, so the nearest one of a kind is the right one, and
        // the one before is preferred because colours and layouts are written ahead of what uses them.
        private static int Nearest(GraphicAssets.Kind[] kinds, int from, GraphicAssets.Kind want)
        {
            for (int step = 1; step < kinds.Length; step++)
            {
                int before = from - step;
                if (before >= 0 && kinds[before] == want) return before;
                int after = from + step;
                if (after < kinds.Length && kinds[after] == want) return after;
            }
            return -1;
        }
    }
}
