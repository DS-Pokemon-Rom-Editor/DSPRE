using Avalonia.Media.Imaging;
using DSPRE.Avalonia.Data;
using DSPRE.Editors;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    /// <summary>
    /// The Pokéwalker tab (HGSS): the picture the Pokéwalker shows for this Pokémon (by sex, or each form) and its
    /// small icon. Pictures are edited in memory and written to a/2/5/6 and a/2/4/8 on Save; on hg-engine the
    /// write goes on to data/graphics/pokewalker's PNGs.
    /// </summary>
    public class PokewalkerViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        /// <summary>One picture on the tab.</summary>
        public sealed class Picture : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            public string Title { get; init; }
            internal GraphicAssets.Archive Archive { get; init; }
            internal int Member { get; init; }
            public int Width { get; init; }
            public int Height { get; init; }
            private Bitmap _image;
            public Bitmap Image { get => _image; internal set { _image = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Image))); } }
        }

        public ObservableCollection<Picture> Pictures { get; } = new();

        private string _unavailable;
        public string Unavailable { get => _unavailable; private set { _unavailable = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsAvailable)); OnPropertyChanged(nameof(IsUnavailable)); } }
        public bool IsAvailable => _unavailable == null;
        public bool IsUnavailable => _unavailable != null;

        /// <summary>Only HeartGold and SoulSilver have a Pokéwalker.</summary>
        public static bool GameHasPokewalker => gameFamily == GameFamilies.HGSS && gameDirs != null && gameDirs.ContainsKey(DirNames.pokewalkerSprites);

        // Edited members, keyed by archive and member; the loaded bytes are kept to tell what changed.
        private Dictionary<(DirNames, int), byte[]> _state = new(), _loaded = new();
        private readonly UndoHistory<Dictionary<(DirNames, int), byte[]>> _history = new();
        private int _currentId = -1;

        public bool CanUndo => _history.CanUndo;
        public bool CanRedo => _history.CanRedo;
        public void Undo() { if (_history.CanUndo) Apply(_history.Undo()); }
        public void Redo() { if (_history.CanRedo) Apply(_history.Redo()); }

        public bool HasUnsavedChanges => _state.Any(kv => !_loaded.TryGetValue(kv.Key, out var was) || !kv.Value.AsSpan().SequenceEqual(was));
        public string UnsavedChangesDescription => $"Pokéwalker (Mon {_currentId})";

        public void LoadMon(int id)
        {
            _currentId = id;
            Pictures.Clear();
            _state = new(); _loaded = new();
            Unavailable = Load(id);
            _history.Reset(_state);
            RaiseState();
        }

        private string Load(int species)
        {
            if (!GameHasPokewalker) return "The Pokéwalker is in HeartGold and SoulSilver.";
            var index = GraphicAssets.PokewalkerIndex();
            if (index == null) return "This ROM's Pokéwalker pictures couldn't be matched to Pokémon.";
            var art = GraphicAssets.All.First(x => x.Dir == DirNames.pokewalkerSprites);
            var icons = GraphicAssets.All.First(x => x.Dir == DirNames.pokewalkerIcons);

            int forms = index.FormCount(species);
            if (forms > 0)
                for (int f = 0; f < forms; f++) Add(art, index.PictureFor(species, false, f), $"Form {f + 1}");
            else
            {
                int male = index.PictureFor(species, false, 0), female = index.PictureFor(species, true, 0);
                Add(art, male, female != male ? "Male" : "Picture");
                if (female != male) Add(art, female, "Female");
            }

            // Pokéwalker icons follow the party icons, whose own entry for a species is that species plus the lead-in.
            int partyIcon = species + PokemonIconFiles.SharedFiles;
            var icon = PokemonIconFiles.Describe(partyIcon);
            if (icon != null && icon.Species == species && icon.Form == null && PokewalkerIconLeadIn >= 0)
                Add(icons, partyIcon - PokewalkerIconLeadIn, "Icon");

            return Pictures.Count == 0 ? "This Pokémon has no Pokéwalker picture." : null;
        }

        private void Add(GraphicAssets.Archive archive, int member, string title)
        {
            if (member < 0 || archive.Pokewalker is not { } size) return;
            byte[] bytes = new ScriptNarc(archive.Dir).Get(member);
            if (bytes == null) return;
            _loaded[(archive.Dir, member)] = bytes;
            _state[(archive.Dir, member)] = bytes;
            var p = new Picture { Title = title, Archive = archive, Member = member, Width = size.Width * 2, Height = size.Height * 2 };
            Pictures.Add(p);
            Show(p);
        }

        // Reads of the tab's members see its in-memory bytes; writes go to write instead of the project.
        private ScriptNarc.Staging Staging(Action<DirNames, int, byte[]> write = null) => new()
        {
            Read = (dir, id) => _state.TryGetValue((dir, id), out var b) ? b : null,
            Write = write ?? ((_, _, _) => { }),
        };

        private void Show(Picture p)
        {
            Bitmap image = null;
            using (ScriptNarc.Use(Staging()))
            {
                var shown = GraphicAssets.Render(p.Archive, p.Member);
                if (shown.Rgba != null) image = ImageConverter.FromRgba(shown.Rgba, shown.Width, shown.Height);
            }
            p.Image = image;
        }

        public string Export(Picture p, string path)
        {
            using (ScriptNarc.Use(Staging())) return GraphicAssets.ExportPng(p.Archive, p.Member, path);
        }

        public string Import(Picture p, string path)
        {
            byte[] drawn = null;
            string error;
            using (ScriptNarc.Use(Staging((dir, id, bytes) => { if (dir == p.Archive.Dir && id == p.Member) drawn = bytes; })))
                error = GraphicAssets.ImportPng(p.Archive, p.Member, path, out _);
            if (error != null) return error;
            if (drawn == null || drawn.AsSpan().SequenceEqual(_state[(p.Archive.Dir, p.Member)])) return null;
            var next = new Dictionary<(DirNames, int), byte[]>(_state) { [(p.Archive.Dir, p.Member)] = drawn };
            _state = next;
            _history.Capture(next);
            Show(p);
            RaiseState();
            return null;
        }

        private void Apply(Dictionary<(DirNames, int), byte[]> state)
        {
            if (state == null) return;
            _state = state;
            foreach (var p in Pictures) Show(p);
            RaiseState();
        }

        private void RaiseState()
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
        }

        // ─── Save ─────────────────────────────────────────────────────────────────
        public void SaveChanges() => _ = SaveAsync();
        async Task<bool> IEditorWithUnsavedChanges.SaveChangesAsync() { await SaveAsync(); return !HasUnsavedChanges; }
        public void DiscardChanges() => LoadMon(_currentId);

        private string WriteChanged()
        {
            foreach (var (key, bytes) in _state)
                if (!_loaded.TryGetValue(key, out var was) || !bytes.AsSpan().SequenceEqual(was))
                    new ScriptNarc(key.Item1).Put(key.Item2, bytes);
            return null;
        }

        private async Task SaveAsync()
        {
            if (!HasUnsavedChanges) return;
            if (HgEngineProject.IsActive)
            {
                var (saved, error) = await HgEngineSave.RunAsync(WriteChanged);
                if (!saved)
                {
                    if (error != null) await DialogHelper.ShowError($"The Pokéwalker pictures were not saved:\n{error}", "Pokéwalker");
                    return;
                }
            }
            else WriteChanged();
            _loaded = new Dictionary<(DirNames, int), byte[]>(_state);
            _history.MarkSaved();
            SaveNotice.Saved(UnsavedChangesDescription);
            RaiseState();
        }
    }
}
