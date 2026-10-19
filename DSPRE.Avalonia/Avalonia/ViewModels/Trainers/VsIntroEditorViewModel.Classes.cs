using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using DSPRE.Avalonia.Data;
using DSPRE.ROMFiles;
using static DSPRE.ROMFiles.VsIntroTables;
using Kind = DSPRE.Avalonia.Data.GraphicAssets.Kind;

namespace DSPRE.Avalonia.ViewModels.Trainers
{
    /// <summary>A Classes tab line: a group heading or a class, with its tick for setting several at once.</summary>
    public sealed class ClassRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public bool IsHeader { get; init; }
        public bool IsItem => !IsHeader;
        public int Class { get; init; } = -1;
        public Action TickChanged { get; init; }

        private string _title = "", _detail = "";
        public string Title { get => _title; set { if (_title == value) return; _title = value; Raise(nameof(Title)); } }
        public string Detail { get => _detail; set { if (_detail == value) return; _detail = value; Raise(nameof(Detail)); } }

        private bool _tick;
        public bool Tick { get => _tick; set { if (_tick == value) return; _tick = value; Raise(nameof(Tick)); TickChanged?.Invoke(); } }
    }

    /// <summary>An intro a class can have: what it looks like and who it's for, with its music under it.</summary>
    public sealed class IntroChoice
    {
        public int Combo { get; init; }
        public string Name { get; init; }
        public string Music { get; init; }
        public override string ToString() => $"{Name} ({Music})";
    }

    public partial class VsIntroEditorViewModel
    {
        public const int MugshotsTab = 0, IntrosTab = 1, ClassesTab = 2, ClassRecordsTab = 3;

        // The HGSS intro archive members the stock ball and door intros load (palette, tiles, cells, animation;
        // the doors are palette, tiles, screen). Checked against the archive before use.
        private static readonly int[] HgssLargeBall = { 0, 7, 9, 8 }, HgssSmallBall = { 0, 4, 6, 5 }, HgssDoors = { 166, 167, 168 };

        private static readonly string[] ClassGroups =
            { "Gym Leaders", "Elite Four and Champion", "Rival", "Team Rocket", "Team Galactic", "Other", "Ordinary trainers" };

        public ObservableCollection<ClassRow> ClassRows { get; } = new();
        private readonly Dictionary<int, ClassRow> _classRowOf = new();
        private bool _rebuildingClasses;

        private void BuildClassRows()
        {
            _classRowOf.Clear();
            for (int c = 0; c < _names.Classes.Length; c++)
                _classRowOf[c] = new ClassRow { Class = c, TickChanged = RaiseTicks };
            BuildBulkChoices();
            RenameClasses();
        }

        private string ClassGroup(int trainerClass)
        {
            int combo = _t.ComboForClass(trainerClass);
            if (combo < 0) return "Other";
            return _t.RoleOf(combo) == ComboRole.Ordinary ? "Ordinary trainers" : GroupOf(combo);
        }

        /// <summary>What the intro is: how it looks, then whose it is where that tells two apart.</summary>
        private string IntroName(int combo)
        {
            if (combo < 0) return "Unknown";
            string kind = VsIntroNames.Kind(_t, _t.EffectOf(combo));
            bool named = _t.RecordFor(_t.EffectOf(combo)) != null || _t.RoleOf(combo) is not (ComboRole.None or ComboRole.Ordinary);
            string who = _names.Combo(_t, combo);
            return named && !kind.Contains(who, StringComparison.OrdinalIgnoreCase) ? $"{kind}: {who}" : kind;
        }

        private IntroChoice Choice(int combo) => new IntroChoice { Combo = combo, Name = IntroName(combo), Music = _names.Sequence(_t.SequenceOf(combo)) };

        // HGSS class rows hold the combo in 6 bits; DP and Platinum can only jump to combos some stub already picks.
        private bool Offerable(int combo) => IsHgss ? combo < 64 : _t.CanSwitchTo(combo);

        private bool CanChange(int trainerClass) => IsHgss || _t.InSwitch(trainerClass);

        private void RenameClasses()
        {
            foreach (ClassRow row in _classRowOf.Values)
            {
                row.Title = _names.Class(row.Class);
                row.Detail = IntroName(_t.ComboForClass(row.Class));
            }
            BuildClassList();
        }

        private string _classSearch = "";
        public string ClassSearch
        {
            get => _classSearch;
            set { value ??= ""; if (_classSearch == value) return; _classSearch = value; Raise(); BuildClassList(); }
        }

        /// <summary>The classes the search keeps, under the group of the intro each has now.</summary>
        private void BuildClassList()
        {
            List<ClassRow> rows = _classRowOf.Values.OrderBy(r => r.Class).ToList();
            string q = _classSearch.Trim();
            if (q.Length > 0)
            {
                List<ClassRow> hits = rows.Where(r => SearchMatch.Contains(r.Title, q) || SearchMatch.Contains(r.Detail, q)).ToList();
                if (hits.Count == 0) hits = rows.Where(r => SearchMatch.NearMiss(r.Title, q) || SearchMatch.NearMiss(r.Detail, q)).ToList();
                rows = hits;
            }
            _rebuildingClasses = true;
            try
            {
                ClassRows.Clear();
                foreach (string group in ClassGroups)
                {
                    List<ClassRow> members = rows.Where(r => ClassGroup(r.Class) == group).ToList();
                    if (members.Count == 0) continue;
                    ClassRows.Add(new ClassRow { IsHeader = true, Title = $"{group} ({members.Count})" });
                    foreach (ClassRow r in members) ClassRows.Add(r);
                }
            }
            finally { _rebuildingClasses = false; }
            // The list dropped its selection when it was cleared; the class stays selected even when filtered out.
            Raise(nameof(SelectedClass));
        }

        private ClassRow _class;
        public ClassRow SelectedClass
        {
            get => _class;
            set
            {
                if (_rebuildingClasses) return;
                if (value == null || value.IsHeader) { Raise(); return; }
                if (_class == value) return;
                _class = value;
                Raise();
                StopClassPreview();
                RaiseClass();
            }
        }

        private int ClassId => _class?.Class ?? -1;
        private int ClassCombo => HasClass ? _t.ComboForClass(ClassId) : -1;
        public bool HasClass => ClassId >= 0;
        public string ClassTitle => HasClass ? _names.Class(ClassId) : "";
        public bool ClassEditable => HasClass && CanChange(ClassId);
        public string ClassNote
        {
            get
            {
                if (!HasClass) return "";
                if (IsHgss) return $"{_t.FreeClassRows} of {_t.ClassRowCount} class rows free";
                if (!_t.InSwitch(ClassId))
                    return $"Only classes {_t.FirstSwitchClass} to {_t.FirstSwitchClass + _t.SwitchCount - 1} can change their intro.";
                return "";
            }
        }
        public bool CanMakeClassRoom => IsHgss && !_t.ClassTableMoved && !_t.FromSource;

        /// <summary>Making room changes the game's code, so the toolbox does it.</summary>
        public void OpenClassRoomPatch() => AvaloniaEditorLauncher.OpenPatchToolboxAt("vsClassTableExpanded");

        public ObservableCollection<IntroChoice> ClassIntroChoices { get; } = new();

        public int ClassIntroIndex
        {
            get
            {
                int now = ClassCombo;
                for (int i = 0; i < ClassIntroChoices.Count; i++) if (ClassIntroChoices[i].Combo == now) return i;
                return -1;
            }
            set
            {
                if (_suppress || !ClassEditable || value < 0 || value >= ClassIntroChoices.Count || value == ClassIntroIndex) return;
                if (!Assign(ClassId, ClassIntroChoices[value].Combo))
                {
                    StatusText = RowsFull;
                    RaiseClass();
                    return;
                }
                Edited();
            }
        }

        private string RowsFull => _t.ClassTableMoved
            ? $"All {_t.ClassRowCount} class rows are in use. Set another class back to ordinary trainers first."
            : $"All {_t.ClassRowCount} class rows are in use. Make room for more, or set another class back to ordinary trainers.";

        private bool Assign(int trainerClass, int combo) =>
            _t.RoleOf(combo) == ComboRole.Ordinary ? (_t.UnassignClass(trainerClass) || true) : _t.AssignClass(trainerClass, combo);

        public bool ClassMusicEditable => ClassCombo >= 0 && _t.OwnsMusic(ClassCombo);
        public int ClassMusicIndex
        {
            get => ClassCombo >= 0 ? _sequenceIds.IndexOf(_t.SequenceOf(ClassCombo)) : -1;
            set
            {
                if (_suppress || !ClassMusicEditable || value < 0 || value >= _sequenceIds.Count || value == ClassMusicIndex) return;
                _t.SetSequence(ClassCombo, _sequenceIds[value]);
                Edited();
            }
        }

        /// <summary>The music belongs to the intro, so every class on that intro hears the change.</summary>
        public string ClassMusicNote
        {
            get
            {
                int combo = ClassCombo;
                if (combo < 0) return "";
                if (_t.RoleOf(combo) == ComboRole.Ordinary) return "Shared by every class on this intro";
                int others = _t.ClassesUsing(combo).Count(c => c != ClassId);
                return others == 0 ? "" : $"Shared with {others} other class{(others == 1 ? "" : "es")}";
            }
        }

        public bool HasClassMugshot => ClassCombo >= 0 && MugshotRows.Any(r => r.Record != null && r.Record == _t.RecordFor(_t.EffectOf(ClassCombo)));

        public void ShowClassMugshot()
        {
            if (ClassCombo < 0) return;
            Record record = _t.RecordFor(_t.EffectOf(ClassCombo));
            IntroRow row = MugshotRows.FirstOrDefault(r => r.Record != null && r.Record == record);
            if (row == null) return;
            SelectedMugshot = row;
            SelectedTab = MugshotsTab;
        }

        private void RaiseClass()
        {
            _suppress = true;
            try
            {
                ClassIntroChoices.Clear();
                if (HasClass)
                {
                    int now = ClassCombo;
                    foreach (int c in _trainerCombos)
                        if (c == now || Offerable(c)) ClassIntroChoices.Add(Choice(c));
                    if (now >= 0 && !_trainerCombos.Contains(now)) ClassIntroChoices.Add(Choice(now));
                }
                Raise(nameof(HasClass), nameof(ClassTitle), nameof(ClassEditable), nameof(ClassNote), nameof(CanMakeClassRoom),
                      nameof(ClassIntroIndex), nameof(ClassMusicEditable), nameof(ClassMusicIndex), nameof(ClassMusicNote),
                      nameof(HasClassMugshot));
            }
            finally { _suppress = false; }
            RenderClassPreview();
        }

        /// <summary>Selects a class on the Classes tab, clearing a search that hides it.</summary>
        public void GoToClass(int trainerClass)
        {
            if (!_classRowOf.TryGetValue(trainerClass, out ClassRow row)) return;
            if (!ClassRows.Contains(row)) ClassSearch = "";
            SelectedClass = row;
            SelectedTab = ClassesTab;
        }

        // ── Setting several classes at once ──────────────────────────────────────────────────────

        public ObservableCollection<IntroChoice> BulkChoices { get; } = new();

        // Rebuilt after edits, since the entries carry the music; the pick stays.
        private void BuildBulkChoices()
        {
            int keep = _bulkIndex;
            BulkChoices.Clear();
            foreach (int c in _trainerCombos) if (Offerable(c)) BulkChoices.Add(Choice(c));
            _bulkIndex = keep < BulkChoices.Count ? keep : -1;
            Raise(nameof(BulkIndex), nameof(CanApplyBulk));
        }

        private int _bulkIndex = -1;
        public int BulkIndex { get => _bulkIndex; set { if (_bulkIndex == value) return; _bulkIndex = value; Raise(); Raise(nameof(CanApplyBulk)); } }

        private int TickCount => _classRowOf.Values.Count(r => r.Tick);
        public bool HasTicks => TickCount > 0;
        public string BulkLabel => TickCount == 1 ? "Set intro for 1 class" : $"Set intro for {TickCount} classes";
        public bool CanApplyBulk => HasTicks && _bulkIndex >= 0 && _bulkIndex < BulkChoices.Count;

        private void RaiseTicks() => Raise(nameof(HasTicks), nameof(BulkLabel), nameof(CanApplyBulk));

        public void ClearTicks()
        {
            foreach (ClassRow r in _classRowOf.Values) r.Tick = false;
        }

        /// <summary>Gives every ticked class the chosen intro as one undo step, stopping when the class rows run out.</summary>
        public void ApplyBulk()
        {
            if (!CanApplyBulk) return;
            int combo = BulkChoices[_bulkIndex].Combo;
            List<int> ticked = _classRowOf.Values.Where(r => r.Tick).Select(r => r.Class).OrderBy(c => c).ToList();
            List<string> locked = new List<string>();
            int changed = 0, left = 0;
            foreach (int c in ticked)
            {
                if (!CanChange(c)) { locked.Add(_names.Class(c)); continue; }
                if (_t.ComboForClass(c) == combo) continue;
                if (!Assign(c, combo)) { left = ticked.Count(x => x >= c && CanChange(x)); break; }
                changed++;
            }
            if (changed > 0) Edited();
            List<string> notes = new List<string>();
            if (changed > 0) notes.Add(changed == 1 ? "1 class changed." : $"{changed} classes changed.");
            if (left > 0) notes.Add((left == 1 ? "1 class not changed. " : $"{left} classes not changed. ") + RowsFull);
            if (locked.Count > 0) notes.Add($"Can't change their intro: {string.Join(", ", locked)}.");
            StatusText = string.Join(" ", notes);
            if (left == 0) ClearTicks();
        }

        // ── Its own preview ──────────────────────────────────────────────────────────────────────

        private int[] Members(int[] m, params Kind[] want)
        {
            for (int i = 0; i < m.Length; i++)
                if (m[i] >= _kinds.Length || _kinds[m[i]] != want[i]) return null;
            return m;
        }

        /// <summary>The intro a combo plays: its mugshot record, or on HGSS the stock ball, Rocket, door and Red intros.</summary>
        private VsIntroPreview.Scene ComboScene(int combo)
        {
            if (combo < 0 || IsDp || _preview == null) return null;
            int effect = _t.EffectOf(combo);
            Record record = _t.RecordFor(effect);
            if (record != null) return SceneFor(record);
            if (!IsHgss) return null;
            switch (_t.KindOfEffect(effect))
            {
                case IntroKind.Terrain:
                case IntroKind.Generic:
                    return new VsIntroPreview.Scene
                    {
                        Kind = VsIntroPreview.Layout.Balls, Variant = (VsIntroPreview.BallVariant)_classTerrain,
                        Large = Members(HgssLargeBall, Kind.Palette, Kind.TileGraphic, Kind.CellLayout, Kind.CellAnimation),
                        Small = Members(HgssSmallBall, Kind.Palette, Kind.TileGraphic, Kind.CellLayout, Kind.CellAnimation),
                        BlockOrder = _motion?.BlockOrder,
                    };
                case IntroKind.TeamGrunt:
                    return new VsIntroPreview.Scene
                    {
                        Kind = VsIntroPreview.Layout.Special, Special = VsIntroPreview.SpecialVariant.Rocket, Emblem = Emblem(), Flights = _motion?.Flights,
                    };
                case IntroKind.Kimono:
                    return new VsIntroPreview.Scene
                    {
                        Kind = VsIntroPreview.Layout.Special, Special = VsIntroPreview.SpecialVariant.Shoji,
                        Screen = Members(HgssDoors, Kind.Palette, Kind.TileGraphic, Kind.TileMap),
                    };
                case IntroKind.OldBall:
                    return new VsIntroPreview.Scene { Kind = VsIntroPreview.Layout.Special, Special = VsIntroPreview.SpecialVariant.Red };
                default:
                    return null;
            }
        }

        private VsIntroPreview.Scene ClassPreviewScene() => ComboScene(ClassCombo);

        public bool HasClassPreview => ClassPreviewScene() != null;
        public string ClassPreviewNote => !HasClass || HasClassPreview ? "" : "No preview";

        private global::Avalonia.Media.Imaging.Bitmap _classImage;
        public global::Avalonia.Media.Imaging.Bitmap ClassPreviewImage { get => _classImage; private set { _classImage = value; Raise(); } }

        // The ordinary intros pick their Poké Balls from the terrain and the trainer's level, so the preview asks which.
        private int _classTerrain;
        public int ClassTerrainIndex
        {
            get => _classTerrain;
            set { if (value < 0 || value == _classTerrain) return; _classTerrain = value; Raise(); StopClassPreview(); RenderClassPreview(); }
        }
        public bool ShowClassTerrain => ClassPreviewScene()?.Kind == VsIntroPreview.Layout.Balls;
        public bool ShowClassPlayerChoice => ClassPreviewScene()?.Kind == VsIntroPreview.Layout.League;

        private int _classFrame = -1;
        private global::Avalonia.Threading.DispatcherTimer _classTimer;
        public bool ClassAnimating => _classTimer != null;
        public string ClassAnimateLabel => ClassAnimating ? "Stop" : "Animate";

        private void RenderClassPreview()
        {
            Raise(nameof(HasClassPreview), nameof(ClassPreviewNote), nameof(ShowClassTerrain), nameof(ShowClassPlayerChoice));
            VsIntroPreview.Scene scene = ClassPreviewScene();
            if (scene == null) { ClassPreviewImage = null; return; }
            try { ClassPreviewImage = ImageConverter.FromRgba(_preview.Draw(scene, _classFrame), DsBgScreen.Width, DsBgScreen.Height); }
            catch (Exception e) { AppLogger.Error("VS intro class preview failed: " + e.Message); ClassPreviewImage = null; }
        }

        public void ToggleClassPreview()
        {
            if (ClassAnimating) { StopClassPreview(); RenderClassPreview(); return; }
            VsIntroPreview.Scene scene = ClassPreviewScene();
            if (scene == null) return;
            int length = VsIntroPreview.Length(scene);
            _classFrame = 0;
            _classTimer = new global::Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000.0 / 30) };
            _classTimer.Tick += (_, _) =>
            {
                if (++_classFrame >= length) StopClassPreview();
                RenderClassPreview();
            };
            _classTimer.Start();
            Raise(nameof(ClassAnimating), nameof(ClassAnimateLabel));
            RenderClassPreview();
        }

        public void StopClassPreview()
        {
            _classTimer?.Stop();
            _classTimer = null;
            _classFrame = -1;
            Raise(nameof(ClassAnimating), nameof(ClassAnimateLabel));
        }
    }
}
