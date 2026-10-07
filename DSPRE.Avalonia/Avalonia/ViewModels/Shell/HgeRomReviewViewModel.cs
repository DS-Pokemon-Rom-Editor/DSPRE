using DSPRE.Avalonia.Data;
using DSPRE.Editors;
using DSPRE.HgEngine;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using AvaloniaBitmap = Avalonia.Media.Imaging.Bitmap;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Shell
{
    /// <summary>
    /// Pokemon graphics for an hg-engine project and a check of the a/0/2/8 table archive.
    /// Graphics come from the linked checkout when there is one, since that is what the next build packs.
    /// </summary>
    public class HgeRomReviewViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo
    {
        /// <summary>One species in the list.</summary>
        public class SpeciesRow
        {
            public int Id { get; set; }
            public string Label { get; set; }
            public string Search { get; set; }
        }

        /// <summary>One member of a/0/2/8, named both ways when the archive is shifted.</summary>
        public class MemberRow
        {
            public int Index { get; set; }
            public string Size { get; set; }
            public string ReadAs { get; set; }
            public string Holds { get; set; }
            public string State { get; set; }
        }

        /// <summary>One drawing of a species, with the colours it was read with.</summary>
        public class SpriteTile
        {
            public string Caption { get; set; }
            public AvaloniaBitmap Picture { get; set; }
            public string Whynot { get; set; }
            public bool HasPicture => Picture != null;
        }

        private readonly List<SpeciesRow> _allSpecies = new List<SpeciesRow>();
        private HgEngineCodeAddons.Layout _layout;
        private int _loadedSpeciesId = -1;
        private bool _repairStaged;

        /// <summary>hg-engine owns neither archive, so the unpacked copies are the ROM's own bytes.</summary>
        public static void Unpack() => DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.monIcons, DirNames.synthOverlay });

        public HgeRomReviewViewModel()
        {
            Unpack();

            LoadSpecies();
            LoadLayout();
            if (Species.Count > 0) SelectedSpeciesIndex = 0;
        }

        // ── Species graphics ────────────────────────────────────────────────────────

        public ObservableCollection<SpeciesRow> Species { get; } = new ObservableCollection<SpeciesRow>();
        public ObservableCollection<SpriteTile> Icons { get; } = new ObservableCollection<SpriteTile>();
        public ObservableCollection<SpriteTile> Sprites { get; } = new ObservableCollection<SpriteTile>();
        public ObservableCollection<SpriteTile> Followers { get; } = new ObservableCollection<SpriteTile>();

        private string _followerNote = "";
        public string FollowerNote { get => _followerNote; private set => Set(ref _followerNote, value); }

        private string _spriteNote = "";
        public string SpriteNote { get => _spriteNote; private set => Set(ref _spriteNote, value); }

        private string _search = "";
        public string Search
        {
            get => _search;
            set { if (Set(ref _search, value)) ApplySearch(); }
        }

        private int _selectedSpeciesIndex = -1;
        public int SelectedSpeciesIndex
        {
            get => _selectedSpeciesIndex;
            set { if (Set(ref _selectedSpeciesIndex, value)) QueueLoad(); }
        }

        // Debounced: drawing a species is too slow to repeat on every search keystroke.
        private global::Avalonia.Threading.DispatcherTimer _loadTimer;

        private void QueueLoad()
        {
            if (_loadTimer == null)
            {
                _loadTimer = new global::Avalonia.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(220),
                };
                _loadTimer.Tick += (_, _) => { _loadTimer.Stop(); LoadSelectedSpecies(); };
            }
            _loadTimer.Stop();
            _loadTimer.Start();
        }

        private string _paletteNote = "";
        public string PaletteNote { get => _paletteNote; private set => Set(ref _paletteNote, value); }

        private bool _paletteIsWrong;
        public bool PaletteIsWrong { get => _paletteIsWrong; private set => Set(ref _paletteIsWrong, value); }

        private void LoadSpecies()
        {
            string[] names;
            try { names = GetPokemonNames(); } catch { names = Array.Empty<string>(); }

            // A linked checkout's count is the one its editors use.
            int count;
            try
            {
                count = HgEngineProject.IsActive ? GetPersonalFilesCount() : HgEngineCodeAddons.SpeciesCountFromRom();
            }
            catch { count = 0; }
            if (count <= 0) count = names.Length;

            for (int id = 0; id < count; id++)
            {
                string name = id < names.Length ? names[id] : "?";
                _allSpecies.Add(new SpeciesRow { Id = id, Label = $"[{id:D4}] {name}", Search = $"{id} {name}".ToLowerInvariant() });
            }
            ApplySearch();
        }

        private void ApplySearch()
        {
            string needle = (_search ?? "").Trim().ToLowerInvariant();
            List<SpeciesRow> shown = needle.Length == 0 ? _allSpecies : _allSpecies.Where(s => s.Search.Contains(needle)).ToList();

            Species.Clear();
            foreach (SpeciesRow row in shown) Species.Add(row);

            // Keep the open species if still listed, so typing does not redraw on each letter.
            int keep = _loadedSpeciesId >= 0 ? Species.ToList().FindIndex(r => r.Id == _loadedSpeciesId) : -1;
            SelectedSpeciesIndex = keep >= 0 ? keep : (Species.Count > 0 ? 0 : -1);
        }

        /// <summary>Everything drawn for one species, built away from the UI thread.</summary>
        private sealed class SpeciesView
        {
            public readonly List<SpriteTile> Icons = new List<SpriteTile>();
            public readonly List<SpriteTile> Sprites = new List<SpriteTile>();
            public readonly List<SpriteTile> Followers = new List<SpriteTile>();
            public string PaletteNote = "", SpriteNote = "", FollowerNote = "";
            public bool PaletteIsWrong;
        }

        private int _loadToken;

        /// <summary>Builds the species pictures off the UI thread and drops results a newer selection overtook.</summary>
        private void LoadSelectedSpecies()
        {
            if (_selectedSpeciesIndex < 0 || _selectedSpeciesIndex >= Species.Count)
            {
                Icons.Clear(); Sprites.Clear(); Followers.Clear();
                _loadedSpeciesId = -1;
                return;
            }

            int species = Species[_selectedSpeciesIndex].Id;
            if (species == _loadedSpeciesId) return;   // the list moved under the same species
            _loadedSpeciesId = species;

            Icons.Clear();
            Sprites.Clear();
            Followers.Clear();

            int token = ++_loadToken;
            Task.Run(() =>
            {
                SpeciesView built;
                try { built = BuildSpeciesView(species); }
                catch (Exception ex) { AppLogger.Error("HgeRomReview.LoadSelectedSpecies: " + ex.Message); return; }

                global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (token != _loadToken) return;
                    PaletteNote = built.PaletteNote;
                    PaletteIsWrong = built.PaletteIsWrong;
                    SpriteNote = built.SpriteNote;
                    FollowerNote = built.FollowerNote;
                    foreach (SpriteTile tile in built.Icons) Icons.Add(tile);
                    foreach (SpriteTile tile in built.Followers) Followers.Add(tile);
                    foreach (SpriteTile tile in built.Sprites) Sprites.Add(tile);
                });
            });
        }

        private SpeciesView BuildSpeciesView(int species)
        {
            SpeciesView view = new SpeciesView();
            LoadIcons(species, view);
            LoadSprites(species, view);
            LoadFollower(species, view);
            return view;
        }

        /// <summary>Follower sprites sit in one run whose start moves when a project adds objects, so it is searched for.</summary>
        private void LoadFollower(int species, SpeciesView view)
        {
            int start = FollowerRunStart();
            if (start < 0)
            {
                view.FollowerNote = "The ROM's a/0/8/1 holds no Pokémon follower sprites.";
                return;
            }

            view.FollowerNote = $"In the ROM's a/0/8/1, follower sprites start at file {start}, so this one is file {start + species}.";
            byte[] model = OverworldArchive().Get(start + species);
            if (model == null) { view.Followers.Add(new SpriteTile { Caption = "Follower", Whynot = "Nothing is stored here." }); return; }

            AddFollowerPictures(model, 0, "normal", view);
            AddFollowerPictures(model, 1, "shiny", view);
        }

        private void AddFollowerPictures(byte[] model, int palette, string which, SpeciesView view)
        {
            try
            {
                List<OverworldSprites.SpritePixels> pictures = OverworldSprites.Pictures(model, palette);
                if (pictures == null || pictures.Count == 0)
                {
                    view.Followers.Add(new SpriteTile { Caption = $"Follower, {which}", Whynot = "No picture in this entry." });
                    return;
                }
                OverworldSprites.SpritePixels first = pictures[0];
                view.Followers.Add(new SpriteTile
                {
                    Caption = $"Follower, {which}",
                    Picture = ImageConverter.FromRgba(first.Rgba, first.Width, first.Height),
                });
            }
            catch (Exception ex)
            {
                view.Followers.Add(new SpriteTile { Caption = $"Follower, {which}", Whynot = ex.Message });
            }
        }

        // Graphics prefer the checkout's build; the archive check always reads the ROM.
        private ScriptNarc _spriteArchive, _overworldArchive;
        private string _spriteSourceNote = "";

        private ScriptNarc SpriteArchive()
        {
            if (_spriteArchive != null) return _spriteArchive;

            if (HgEngineProject.IsActive)
            {
                ScriptNarc fromCheckout = new ScriptNarc(DirNames.pokemonBattleSprites);
                if (fromCheckout.Available && fromCheckout.Count > 0)
                {
                    _spriteSourceNote = "From your checkout's build of pokegra.narc.";
                    return _spriteArchive = fromCheckout;
                }
                _spriteSourceNote = "Your checkout's pokegra.narc could not be read, so these come from the ROM's a/0/0/4.";
            }

            return _spriteArchive = new ScriptNarc(DirNames.pokemonBattleSprites, fromPacked: true);
        }

        // Read packed so one follower does not unpack an archive of nearly two thousand files.
        private ScriptNarc OverworldArchive()
            => _overworldArchive ??= new ScriptNarc(DirNames.OWSprites, fromPacked: true);

        // Per window, not static, so another ROM starts fresh.
        private int _followerRunStart = -2;

        /// <summary>Where the species run of follower entries begins, or -1 when the ROM has none.</summary>
        private int FollowerRunStart()
        {
            if (_followerRunStart != -2) return _followerRunStart;

            _followerRunStart = -1;
            try
            {
                // Vanilla follower entries remain too, so the species run is the last unbroken one.
                ScriptNarc narc = OverworldArchive();
                for (int i = narc.Count - 1; i >= 0; i--)
                {
                    byte[] member = narc.Get(i);
                    bool isFollower = member != null && member.Length > 4
                        && System.Text.Encoding.ASCII.GetString(member, 0, 4) == "BTX0"
                        && Contains(member, "tsure_poke");
                    if (isFollower) _followerRunStart = i;
                    else if (_followerRunStart >= 0) break;
                }
            }
            catch (Exception ex) { AppLogger.Error("HgeRomReview.FollowerRunStart: " + ex.Message); }
            return _followerRunStart;
        }

        private static bool Contains(byte[] haystack, string needle)
        {
            byte[] bytes = System.Text.Encoding.ASCII.GetBytes(needle);
            for (int i = 0; i + bytes.Length <= haystack.Length; i++)
            {
                int k = 0;
                while (k < bytes.Length && haystack[i + k] == bytes[k]) k++;
                if (k == bytes.Length) return true;
            }
            return false;
        }

        /// <summary>Compares the ROM's icon palette bank with the checkout's; without a checkout shows every bank.</summary>
        private void LoadIcons(int species, SpeciesView view)
        {
            HgEngineCodeAddons.PaletteStatus status = HgEngineCodeAddons.ReadIconPaletteId(species, out int inRom);
            bool readable = status == HgEngineCodeAddons.PaletteStatus.Ok;

            string romSays = status switch
            {
                HgEngineCodeAddons.PaletteStatus.Ok => $"bank {inRom}",
                HgEngineCodeAddons.PaletteStatus.NotABank => $"{inRom}, which is not one of the {HgEngineCodeAddons.IconPaletteBanks} banks",
                HgEngineCodeAddons.PaletteStatus.PastEndOfTable => "nothing: this species is past the end of the table",
                _ => "nothing: the ROM has no table there",
            };

            view.Icons.Add(IconTile(species, readable ? inRom : 0,
                readable ? $"In the ROM: bank {inRom}" : "In the ROM"));

            int inSource = -1;
            bool haveSource = HgEngineProject.IsActive
                && HgEngineIconPalette.TryGetPaletteId(species, out inSource);

            if (haveSource)
            {
                string caption = $"In {System.IO.Path.GetFileName(HgEngineIconPalette.SourceFile)}: bank {inSource}";
                view.Icons.Add(HgEnginePokemonIcons.TryGetIconPath(species, out string png)
                    ? SourceIconTile(png, caption)
                    : IconTile(species, inSource, caption));

                view.PaletteIsWrong = !readable || inSource != inRom;
                view.PaletteNote = view.PaletteIsWrong
                    ? $"{HgEngineIconPalette.SourceFile} says bank {inSource}. The ROM's {ArchiveAndMember} says {romSays}."
                    : $"The ROM's {ArchiveAndMember} and {HgEngineIconPalette.SourceFile} agree: bank {inRom}.";
                return;
            }

            view.PaletteIsWrong = !readable;
            view.PaletteNote = $"The ROM's {ArchiveAndMember} says {romSays}.";
            for (int b = 0; b < HgEngineCodeAddons.IconPaletteBanks; b++)
                view.Icons.Add(IconTile(species, b, $"Bank {b}"));
        }

        private static string ArchiveAndMember => $"a/0/2/8 file {HgEngineCodeAddons.IconPalettes}";

        private static SpriteTile SourceIconTile(string iconPath, string caption)
        {
            try
            {
                return new SpriteTile { Caption = caption, Picture = ImageConverter.LoadHgeIconFirstFrame(iconPath) };
            }
            catch (Exception ex)
            {
                return new SpriteTile { Caption = caption, Whynot = ex.Message };
            }
        }

        private static SpriteTile IconTile(int species, int bank, string caption)
        {
            try
            {
                RawImage raw = DSUtils.GetPokePicRaw(species, 32, 32, bank);
                return new SpriteTile { Caption = caption, Picture = ImageConverter.ToAvaloniaBitmap(raw) };
            }
            catch (Exception ex)
            {
                return new SpriteTile { Caption = caption, Whynot = ex.Message };
            }
        }

        /// <summary>Single-gender species leave the female members empty.</summary>
        private void LoadSprites(int species, SpeciesView view)
        {
            GraphicAssets.Archive archive = GraphicAssets.All.FirstOrDefault(a => a.Dir == DirNames.pokemonBattleSprites);
            if (archive == null) return;

            ScriptNarc source = SpriteArchive();
            view.SpriteNote = _spriteSourceNote;

            string[] drawings = { "Back, female", "Back, male", "Front, female", "Front, male" };
            for (int slot = 0; slot < drawings.Length; slot++)
            {
                int index = species * SpriteMembersPerSpecies + slot;
                view.Sprites.Add(SpriteTileFor(archive, source, index, $"{drawings[slot]}, normal", shiny: false));
                view.Sprites.Add(SpriteTileFor(archive, source, index, $"{drawings[slot]}, shiny", shiny: true));
            }
        }

        // Back female, back male, front female, front male, normal palette, shiny palette.
        private const int SpriteMembersPerSpecies = 6;

        private static SpriteTile SpriteTileFor(GraphicAssets.Archive archive, ScriptNarc source, int index,
                                                string caption, bool shiny)
        {
            try
            {
                GraphicAssets.Preview preview = GraphicAssets.Render(archive, index, shiny, source);
                return preview?.Rgba == null
                    ? new SpriteTile { Caption = caption, Whynot = preview?.Whynot ?? "No picture here." }
                    : new SpriteTile
                    {
                        Caption = caption,
                        Picture = ImageConverter.FromRgba(preview.Rgba, preview.Width, preview.Height),
                    };
            }
            catch (Exception ex)
            {
                return new SpriteTile { Caption = caption, Whynot = ex.Message };
            }
        }

        // ── The archive the tables live in ──────────────────────────────────────────

        public ObservableCollection<MemberRow> Members { get; } = new ObservableCollection<MemberRow>();

        private string _archiveSummary = "";
        public string ArchiveSummary { get => _archiveSummary; private set => Set(ref _archiveSummary, value); }

        private bool _archiveIsHealthy;
        public bool ArchiveIsHealthy { get => _archiveIsHealthy; private set => Set(ref _archiveIsHealthy, value); }

        public bool CanRepair => !_repairStaged && _layout != null && _layout.StaleMembers.Count > 0;

        private string _repairNote = "";
        public string RepairNote { get => _repairNote; private set => Set(ref _repairNote, value); }

        private void LoadLayout()
        {
            _layout = HgEngineCodeAddons.Describe();
            ArchiveSummary = _layout.Summary;
            ArchiveIsHealthy = _layout.IsHealthy;
            RepairNote = _layout.StaleMembers.Count > 0 ? HgEngineCodeAddonRepair.Describe(_layout) : "";

            Members.Clear();
            foreach (HgEngineCodeAddons.MemberInfo m in _layout.Members)
            {
                Members.Add(new MemberRow
                {
                    Index = m.Index,
                    Size = m.Length == 0 ? "empty" : $"{m.Length:N0} bytes",
                    ReadAs = m.ReadAs ?? "",
                    Holds = m.Holds ?? (m.Index < HgEngineCodeAddons.VanillaMembers ? "(vanilla data)" : ""),
                    State = m.IsStale ? "left over" : "",
                });
            }
            Raise(nameof(CanRepair));
        }

        // The staged repair is the only edit: undo unstages it, redo stages it again.
        private bool _repairUnstaged;
        public bool CanUndo => _repairStaged;
        public bool CanRedo => _repairUnstaged;
        public void Undo() { if (!_repairStaged) return; DiscardChanges(); _repairUnstaged = true; RaiseSteps(); }
        public void Redo() { if (!_repairUnstaged) return; StageRepair(); }
        private void RaiseSteps() { Raise(nameof(CanUndo)); Raise(nameof(CanRedo)); }

        public void StageRepair()
        {
            if (!CanRepair) return;
            _repairUnstaged = false;
            _repairStaged = true;
            RepairNote = HgEngineCodeAddonRepair.Describe(_layout) + " Not saved yet.";
            Raise(nameof(CanRepair));
            Raise(nameof(HasUnsavedChanges));
            RaiseSteps();
        }

        public bool HasUnsavedChanges => _repairStaged;

        public string UnsavedChangesDescription =>
            _repairStaged ? "a/0/2/8 member order repair" : null;

        public void SaveChanges()
        {
            if (!_repairStaged || _layout == null) return;

            if (!HgEngineCodeAddonRepair.TryRepair(Filesystem.synthOverlay, _layout.StaleMembers, out string error))
            {
                AppMessages.Error("The archive could not be repaired. " + error, "hg-engine ROM review");
                return;
            }

            _repairStaged = false;
            LoadLayout();
            _loadedSpeciesId = -1;
            LoadSelectedSpecies();
            RepairNote = "Repaired. Save the ROM to write it back.";
            Raise(nameof(HasUnsavedChanges));
            RaiseSteps();
        }

        public Task<bool> SaveChangesAsync()
        {
            SaveChanges();
            return Task.FromResult(!_repairStaged);
        }

        public void DiscardChanges()
        {
            if (!_repairStaged) return;
            _repairStaged = false;
            _repairUnstaged = false;
            RepairNote = HgEngineCodeAddonRepair.Describe(_layout);
            Raise(nameof(CanRepair));
            Raise(nameof(HasUnsavedChanges));
            RaiseSteps();
        }

        // ── Notification ────────────────────────────────────────────────────────────

        public event PropertyChangedEventHandler PropertyChanged;

        private void Raise([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private bool Set<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            Raise(name);
            return true;
        }
    }
}
