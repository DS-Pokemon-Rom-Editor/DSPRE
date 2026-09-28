using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using DSPRE.Avalonia.Data;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;
using static DSPRE.ROMFiles.VsIntroTables;
using Kind = DSPRE.Avalonia.Data.GraphicAssets.Kind;

namespace DSPRE.Avalonia.ViewModels.Trainers
{
    /// <summary>One line of an intro list: a group heading, a record, a combo or a class.</summary>
    public sealed class IntroRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public bool IsHeader { get; init; }
        public bool IsItem => !IsHeader;
        public Record Record { get; init; }
        public int Combo { get; init; } = -1;
        public int Class { get; init; } = -1;

        private string _title = "", _detail = "";
        public string Title { get => _title; set { if (_title == value) return; _title = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title))); } }
        public string Detail { get => _detail; set { if (_detail == value) return; _detail = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Detail))); } }
    }

    /// <summary>A file the game fixes in code, with a way to open it where there is one.</summary>
    public sealed class FixedArt
    {
        public string Label { get; init; }
        public string Value { get; init; }
        public Action Open { get; init; }
        public bool CanOpen => Open != null;
    }

    public sealed class ParticleArt
    {
        public string Label { get; init; }
        public int File { get; init; }
        public string Value => File.ToString();
    }

    /// <summary>
    /// The intros special trainer battles open with: the mugshot records and their art, and which trainer
    /// classes get which intro and music.
    /// </summary>
    public class VsIntroEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private void Raise(params string[] names) { foreach (var n in names) Raise(n); }

        private VsIntroTables _t;
        private VsIntroNames _names = new();
        private ScriptNarc _art;
        private Kind[] _kinds = Array.Empty<Kind>();
        private FieldFont _font;
        private VsIntroPreview _preview;
        private readonly Dictionary<int, ushort[]> _classColours = new();
        private bool _suppress;

        public const string Title = "VS Intro Editor";
        private const DirNames Archive = DirNames.encounterEffectGraphics;

        public VsIntroEditorViewModel() { }

        /// <summary>Reads everything the editor shows. File work only, for the busy overlay's thread.</summary>
        public void Load()
        {
            _t = VsIntroTables.Load(Part.Trainers);
            var dirs = new List<DirNames> { Archive, DirNames.textArchives };
            if (gameFamily == GameFamilies.Plat) dirs.Add(DirNames.trainerGraphics);
            DSUtils.TryUnpackNarcs(dirs);
            _names = VsIntroNames.Read();
            _art = new ScriptNarc(Archive);
            _kinds = Enumerable.Range(0, _art.Available ? _art.Count : 0)
                               .Select(i => { try { return GraphicAssets.Identify(_art.Get(i)); } catch { return Kind.Unknown; } }).ToArray();
            try { _font = FieldFont.LoadSystemFont(); } catch { _font = null; }
            _preview = new VsIntroPreview(i => _art.Get(i), _font);
        }

        /// <summary>Fills the lists once loaded, on the UI thread.</summary>
        public void Ready()
        {
            BuildChoices();
            BuildMugshotRows();
            BuildIntroRows();
            BuildClassRows();
            StartUndo();
            SelectedMugshot = MugshotRows.FirstOrDefault(r => r.IsItem);
            SelectedIntro = IntroRows.FirstOrDefault(r => r.IsItem);
            SelectedClass = ClassRows.FirstOrDefault(r => r.Class == (_t.FirstSwitchClass > 0 ? _t.FirstSwitchClass : 1)) ?? ClassRows.FirstOrDefault();
            StatusText = _t.Family == GameFamilies.DP
                ? "Diamond and Pearl cut the face out of the class's battle sprite, so there is no mugshot art to pick."
                : "";
        }

        private int _tab;
        public int SelectedTab { get => _tab; set { if (_tab == value) return; _tab = value; Raise(); } }

        private string _status = "";
        public string StatusText { get => _status; set { if (_status == value) return; _status = value; Raise(); } }

        public bool IsDp => _t?.Family == GameFamilies.DP;
        public bool IsHgss => _t?.Family == GameFamilies.HGSS;
        public bool IsPlatinum => _t?.Family == GameFamilies.Plat;

        // ── Choices ──────────────────────────────────────────────────────────────────────────────

        private readonly List<int[]> _faceSets = new(), _bannerSets = new();
        private readonly List<int> _palettes = new(), _sequenceIds = new();
        public ObservableCollection<string> TrainerChoices { get; } = new();
        public ObservableCollection<string> ClassChoices { get; } = new();
        public ObservableCollection<string> SequenceChoices { get; } = new();
        public ObservableCollection<string> FaceSetChoices { get; } = new();
        public ObservableCollection<string> BannerSetChoices { get; } = new();
        public ObservableCollection<string> FramePaletteChoices { get; } = new();

        private void BuildChoices()
        {
            TrainerChoices.Clear();
            for (int i = 0; i < _names.Trainers.Length; i++) TrainerChoices.Add(_names.Trainer(i));
            ClassChoices.Clear();
            for (int i = 0; i < _names.Classes.Length; i++) ClassChoices.Add(_names.Class(i));

            _sequenceIds.Clear();
            _sequenceIds.AddRange(_names.SequenceChoices(Enumerable.Range(0, _t.ComboCount).Select(_t.SequenceOf)));
            SequenceChoices.Clear();
            foreach (int id in _sequenceIds) SequenceChoices.Add(_names.Sequence(id));

            _palettes.Clear();
            for (int i = 0; i < _kinds.Length; i++) if (_kinds[i] == Kind.Palette) _palettes.Add(i);
            FindSets(_faceSets, _t.Records.Select(r => r.FaceMembers()), Kind.Palette, Kind.TileGraphic, Kind.CellLayout, Kind.CellAnimation);
            FindSets(_bannerSets, _t.Records.Select(r => r.BannerMembers()), Kind.Palette, Kind.TileGraphic, Kind.TileMap);
            FaceSetChoices.Clear();
            foreach (var set in _faceSets) FaceSetChoices.Add(ArtLabel(set[0], r => r.FaceMembers()?[0]));
            BannerSetChoices.Clear();
            foreach (var set in _bannerSets) BannerSetChoices.Add(ArtLabel(set[0], r => r.BannerMembers()?[0]));
            FramePaletteChoices.Clear();
            foreach (int p in _palettes) FramePaletteChoices.Add(ArtLabel(p, r => r.Has(RecordField.FramePalette) ? r.Get(RecordField.FramePalette) : null));
        }

        /// <summary>
        /// Every run of files laid out the way the records' own sets are, so a set is only ever offered whole.
        /// HGSS keeps a banner's tiles before its palette, so the layout is learnt rather than assumed.
        /// </summary>
        private void FindSets(List<int[]> sets, IEnumerable<int[]> used, params Kind[] roles)
        {
            sets.Clear();
            var layouts = used.Where(m => m != null && m.Length == roles.Length)
                              .Select(m => m.Select(x => x - m.Min()).ToArray())
                              .Where(o => o.Distinct().Count() == o.Length && o.Max() == o.Length - 1)
                              .GroupBy(o => string.Join(",", o)).Select(g => g.First()).ToList();
            for (int i = 0; i < _kinds.Length; i++)
                foreach (var offsets in layouts)
                    if (offsets.Select((o, k) => i + o < _kinds.Length && _kinds[i + o] == roles[k]).All(x => x))
                    {
                        sets.Add(offsets.Select(o => i + o).ToArray());
                        break;
                    }
            foreach (var m in used)
                if (m != null && m.Length == roles.Length && !sets.Any(x => x.SequenceEqual(m))) sets.Add(m);
        }

        // A file is named after the records that use it, as the retail rows set them.
        private string ArtLabel(int member, Func<Record, int?> uses)
        {
            var who = _t.Records.Where(r => uses(r) == member).Select(r => _names.Record(_t, r)).Distinct().ToList();
            var s = _t.Sites;
            if (who.Count == 0 && member == s.PlayerFaceBoy) who.Add("Player, boy");
            if (who.Count == 0 && member == s.PlayerFaceGirl) who.Add("Player, girl");
            return (who.Count == 0 ? "Unused" : string.Join(", ", who)) + $" ({member})";
        }

        // ── Mugshots tab ─────────────────────────────────────────────────────────────────────────

        public ObservableCollection<IntroRow> MugshotRows { get; } = new();

        private void BuildMugshotRows()
        {
            MugshotRows.Clear();
            void Group(string heading, IEnumerable<Record> records)
            {
                var list = records.ToList();
                if (list.Count == 0) return;
                MugshotRows.Add(new IntroRow { IsHeader = true, Title = heading });
                foreach (var r in list) MugshotRows.Add(new IntroRow { Record = r });
            }
            Group("Gym Leaders", _t.Records.Where(r => r.Kind == RecordKind.Gym));
            Group("Rival", _t.Records.Where(r => r.Kind == RecordKind.Rival));
            Group("Elite Four and Champion", _t.Records.Where(r => r.Kind == RecordKind.League));
            Group("Team Rocket", _t.Records.Where(r => r.Kind == RecordKind.Executive));
            RenameMugshots();
        }

        private void RenameMugshots()
        {
            foreach (var row in MugshotRows.Where(r => r.IsItem))
            {
                row.Title = _names.Record(_t, row.Record);
                row.Detail = string.Join(", ", ClassesFor(row.Record).Select(_names.Class).Distinct());
            }
        }

        private IEnumerable<int> CombosFor(Record r)
        {
            var effects = new HashSet<int>(_t.EffectsUsing(r));
            return Enumerable.Range(0, _t.ComboCount).Where(c => effects.Contains(_t.EffectOf(c)));
        }

        private IEnumerable<int> ClassesFor(Record r) => CombosFor(r).SelectMany(_t.ClassesUsing);

        private IntroRow _mugshot;
        public IntroRow SelectedMugshot
        {
            get => _mugshot;
            set
            {
                if (value != null && value.IsHeader) { Raise(); return; }
                if (_mugshot == value) return;
                _mugshot = value;
                StopAnimation();
                RaiseMugshot();
            }
        }

        private Record Rec => _mugshot?.Record;
        private bool Has(RecordField f) => Rec != null && Rec.Has(f);

        public bool HasMugshot => Rec != null;
        public string MugshotTitle => Rec == null ? "" : _names.Record(_t, Rec);
        public string MugshotUsedBy
        {
            get
            {
                if (Rec == null) return "";
                var classes = ClassesFor(Rec).Select(_names.Class).Distinct().ToList();
                return classes.Count == 0 ? "No trainer class uses this intro." : "Used by " + string.Join(", ", classes) + ".";
            }
        }

        public bool ShowTrainer => Has(RecordField.TrainerId);
        public int TrainerIndex
        {
            get => ShowTrainer ? Rec.Get(RecordField.TrainerId) : -1;
            set { if (!_suppress && ShowTrainer && value >= 0 && value != TrainerIndex) { Rec.Set(RecordField.TrainerId, value); Edited(); } }
        }

        // HGSS only compares the gym record's class with the rival class, which prints the save's rival name.
        public bool ShowRivalName => IsHgss && Has(RecordField.Class) && Rec.Kind is RecordKind.Gym or RecordKind.Rival;
        public bool RivalName
        {
            get => ShowRivalName && Rec.Get(RecordField.Class) == HgssRivalClass;
            set
            {
                if (_suppress || !ShowRivalName || value == RivalName) return;
                int fallback = ClassesFor(Rec).FirstOrDefault(c => c != HgssRivalClass);
                Rec.Set(RecordField.Class, value ? HgssRivalClass : fallback);
                Edited();
            }
        }

        public bool ShowClass => !IsHgss && Has(RecordField.Class);
        public string ClassLabel => IsDp ? "Face from class" : "Face colours";
        public string ClassTip => IsDp
            ? "The face is cut out of this class's battle sprite"
            : "The face takes this class's battle sprite colours";
        public int ClassIndex
        {
            get => ShowClass ? Rec.Get(RecordField.Class) : -1;
            set { if (!_suppress && ShowClass && value >= 0 && value != ClassIndex) { Rec.Set(RecordField.Class, value); Edited(); } }
        }
        public bool CanEditClassColours => IsPlatinum && ShowClass;

        public bool ShowEndX => Has(RecordField.EndX);
        public decimal EndX
        {
            get => ShowEndX ? Rec.Get(RecordField.EndX) : 0;
            set { if (!_suppress && ShowEndX) { Rec.Set(RecordField.EndX, (int)value); Edited(); } }
        }

        public bool ShowFaceColumn => Has(RecordField.FaceColumn) && IsDp;
        public decimal FaceColumn
        {
            get => ShowFaceColumn ? Rec.Get(RecordField.FaceColumn) : 0;
            set { if (!_suppress && ShowFaceColumn) { Rec.Set(RecordField.FaceColumn, (int)value); Edited(); } }
        }

        public bool ShowFace => Rec?.FaceMembers() != null;
        public int FaceSetIndex
        {
            get
            {
                var m = Rec?.FaceMembers();
                return m == null ? -1 : _faceSets.FindIndex(s => s.SequenceEqual(m));
            }
            set
            {
                if (_suppress || Rec == null || value < 0 || value >= _faceSets.Count || value == FaceSetIndex) return;
                if (Rec.SetFaceMembers(_faceSets[value])) Edited();
            }
        }
        public string FaceFiles => Rec?.FaceMembers() is int[] m ? string.Join(", ", m) : "";

        public bool ShowBanner => Rec?.BannerMembers() != null;
        public int BannerSetIndex
        {
            get
            {
                var m = Rec?.BannerMembers();
                return m == null ? -1 : _bannerSets.FindIndex(s => s.SequenceEqual(m));
            }
            set
            {
                if (_suppress || Rec == null || value < 0 || value >= _bannerSets.Count || value == BannerSetIndex) return;
                if (Rec.SetBannerMembers(_bannerSets[value])) Edited();
            }
        }
        public string BannerFiles => Rec?.BannerMembers() is int[] m ? string.Join(", ", m) : "";

        public bool ShowFramePalette => Has(RecordField.FramePalette);
        public string FramePaletteLabel => IsDp ? "Banner colours" : "Frame colours";
        public int FramePaletteIndex
        {
            get => ShowFramePalette ? _palettes.IndexOf(Rec.Get(RecordField.FramePalette)) : -1;
            set
            {
                if (_suppress || !ShowFramePalette || value < 0 || value >= _palettes.Count || value == FramePaletteIndex) return;
                Rec.Set(RecordField.FramePalette, _palettes[value]);
                Edited();
            }
        }

        public bool ShowClash => Has(RecordField.ClashFrames);
        public decimal ClashFrames
        {
            get => ShowClash ? Rec.Get(RecordField.ClashFrames) : 0;
            set { if (!_suppress && ShowClash) { Rec.Set(RecordField.ClashFrames, (int)value); Edited(); } }
        }

        public bool ShowCamera => Has(RecordField.CameraTurn);
        public decimal CameraTurn
        {
            get => ShowCamera ? Rec.Get(RecordField.CameraTurn) : 0;
            set { if (!_suppress && ShowCamera) { Rec.Set(RecordField.CameraTurn, (int)value); Edited(); } }
        }
        public decimal PanFrames
        {
            get => ShowCamera ? Rec.Get(RecordField.PanFrames) : 0;
            set { if (!_suppress && ShowCamera) { Rec.Set(RecordField.PanFrames, (int)value); Edited(); } }
        }

        public ObservableCollection<ParticleArt> Particles { get; } = new();
        public bool ShowParticles => Rec?.Kind == RecordKind.League && Particles.Count > 0;
        public ObservableCollection<FixedArt> Fixed { get; } = new();

        private void RaiseMugshot()
        {
            _suppress = true;
            try
            {
                Particles.Clear();
                if (Rec?.Kind == RecordKind.League)
                    for (int i = 0; i < _t.ParticleFiles.Length; i++)
                        Particles.Add(new ParticleArt { Label = i == 0 ? "First burst" : "Second burst", File = _t.ParticleFiles[i] });
                BuildFixed();
                Raise(nameof(HasMugshot), nameof(MugshotTitle), nameof(MugshotUsedBy), nameof(ShowTrainer), nameof(TrainerIndex),
                      nameof(ShowRivalName), nameof(RivalName), nameof(ShowClass), nameof(ClassLabel), nameof(ClassTip), nameof(ClassIndex),
                      nameof(CanEditClassColours), nameof(ShowEndX), nameof(EndX), nameof(ShowFaceColumn), nameof(FaceColumn),
                      nameof(ShowFace), nameof(FaceSetIndex), nameof(FaceFiles), nameof(ShowBanner), nameof(BannerSetIndex), nameof(BannerFiles),
                      nameof(ShowFramePalette), nameof(FramePaletteLabel), nameof(FramePaletteIndex), nameof(ShowClash), nameof(ClashFrames),
                      nameof(ShowCamera), nameof(CameraTurn), nameof(PanFrames), nameof(ShowParticles), nameof(HasPreview));
            }
            finally { _suppress = false; }
            RenderPreview();
        }

        private void BuildFixed()
        {
            Fixed.Clear();
            if (Rec == null) return;
            var s = _t.Sites;
            void Add(string label, string value, Action open = null) => Fixed.Add(new FixedArt { Label = label, Value = value, Open = open });
            string Files(int first, int n) => string.Join(", ", Enumerable.Range(first, n));

            if (IsDp)
            {
                if (Rec.Kind == RecordKind.Gym && s.GymBanner >= 0)
                    Add("Banner", Files(s.GymBanner, 3), () => AvaloniaEditorLauncher.OpenGraphicAt(Archive, s.GymBanner + 2, true));
                if (Rec.Kind == RecordKind.League && s.LeagueFrame >= 0)
                    Add("Banner frame", Files(s.LeagueFrame, 3), () => OpenCells(Order(s.LeagueFrame, 3), Rec.Get(RecordField.FramePalette), "League banner"));
                return;
            }
            if (Rec.Kind != RecordKind.Executive && s.VsMark >= 0)
                Add("VS mark", Files(s.VsMark, 4), () => OpenCells(Order(s.VsMark, 4), -1, "VS mark"));
            if (Rec.Kind == RecordKind.League)
            {
                Add("Frame", Files(s.LeagueFrame, 3), () => OpenCells(Order(s.LeagueFrame, 3), Rec.Get(RecordField.FramePalette), "League frame"));
                Add("Player, boy", Files(s.PlayerFaceBoy, 4), () => OpenCells(Order(s.PlayerFaceBoy, 4), -1, "Player face"));
                Add("Player, girl", Files(s.PlayerFaceGirl, 4), () => OpenCells(Order(s.PlayerFaceGirl, 4), -1, "Player face"));
            }
            if (Rec.Kind == RecordKind.Executive && s.ExecutiveBackdrop >= 0)
                Add("Backdrop", Files(s.ExecutiveBackdrop, 4), () => AvaloniaEditorLauncher.OpenGraphicAt(Archive, s.ExecutiveBackdrop + 3, true));
            if (s.NamePalette >= 0) Add("Name colours", s.NamePalette.ToString());
            if (Rec.Kind is RecordKind.Gym or RecordKind.Rival)
                Add("Timing", "Face slides in over 4 frames, VS over 6, banner scrolls 30 pixels a frame");
            if (Rec.Kind == RecordKind.League)
                Add("Timing", "Faces slide in over 6 frames, then the clash, then an 8 frame fade");
        }

        /// <summary>A run of files sorted into palette, tiles, cells, animation, whatever order the archive keeps them in.</summary>
        private int[] Order(int first, int count)
        {
            int Find(Kind k) => Enumerable.Range(first, count).FirstOrDefault(i => i < _kinds.Length && _kinds[i] == k, -1);
            return new[] { Find(Kind.Palette), Find(Kind.TileGraphic), Find(Kind.CellLayout), Find(Kind.CellAnimation) };
        }

        // ── Opening the art ──────────────────────────────────────────────────────────────────────

        private static GraphicAssets.Archive ArtArchive => GraphicAssets.All.FirstOrDefault(a => a.Dir == Archive);

        private static void Paint(int ncgr)
        {
            var archive = ArtArchive;
            if (archive == null || ncgr < 0) return;
            new Views.Graphics.GraphicPainterView(new GraphicPainterViewModel(archive, ncgr)).ShowManaged();
        }

        private static void OpenCells(int[] m, int palette, string what)
        {
            if (m == null || m[3] < 0) return;
            AvaloniaEditorLauncher.OpenCellAnimationEditor(Archive, m[3], m[2], m[1], palette >= 0 ? palette : m[0], 0, what);
        }

        public void PaintFace() { if (Rec?.FaceMembers() is int[] m) Paint(m[1]); }
        public void AnimateFace() { if (Rec?.FaceMembers() is int[] m) OpenCells(m, -1, MugshotTitle + " face"); }
        public void PaintBanner() { if (Rec?.BannerMembers() is int[] m) Paint(m[1]); }
        public void ShowBannerInGraphics() { if (Rec?.BannerMembers() is int[] m) AvaloniaEditorLauncher.OpenGraphicAt(Archive, m[2], true); }
        public void EditClassColours() { if (CanEditClassColours) AvaloniaEditorLauncher.OpenTrainerSpriteEditor(Rec.Get(RecordField.Class)); }
        public void OpenParticles(ParticleArt p) { if (p != null) AvaloniaEditorLauncher.OpenParticleEditor(Archive, p.File, $"{MugshotTitle}: {p.Label}", null); }

        /// <summary>Goes to the combo that uses this mugshot on the other tab.</summary>
        public void ShowClassesAndMusic()
        {
            if (Rec == null) return;
            int combo = CombosFor(Rec).DefaultIfEmpty(-1).First();
            var row = IntroRows.FirstOrDefault(r => r.Combo == combo);
            if (row == null) return;
            SelectedIntro = row;
            SelectedTab = 1;
        }

        // ── Preview ──────────────────────────────────────────────────────────────────────────────

        private global::Avalonia.Media.Imaging.Bitmap _image;
        public global::Avalonia.Media.Imaging.Bitmap PreviewImage { get => _image; private set { _image = value; Raise(); } }
        public bool HasPreview => !IsDp && Rec != null && Scene() != null;

        private bool _girl;
        public bool PlayerGirl { get => _girl; set { if (_girl == value) return; _girl = value; Raise(); RenderPreview(); } }
        public bool ShowPlayerChoice => Rec?.Kind == RecordKind.League && !IsDp;

        private ushort[] ClassColours(int trainerClass)
        {
            if (!IsPlatinum || trainerClass < 0) return null;
            if (_classColours.TryGetValue(trainerClass, out var c)) return c;
            try { c = DsBgScreen.ReadColours(new ScriptNarc(DirNames.trainerGraphics).Get(trainerClass * 5 + 1)); }
            catch { c = null; }
            if (c != null && c.Length == 0) c = null;
            return _classColours[trainerClass] = c;
        }

        private VsIntroPreview.Scene Scene()
        {
            if (Rec == null || IsDp || _preview == null) return null;
            var s = _t.Sites;
            int[] Four(int first) => first < 0 ? null : Order(first, 4);
            string name = Rec.Kind == RecordKind.Rival || (IsHgss && Rec.Has(RecordField.Class) && Rec.Get(RecordField.Class) == HgssRivalClass)
                ? "Rival" : Rec.Has(RecordField.TrainerId) ? _names.Trainer(Rec.Get(RecordField.TrainerId)) : "";
            switch (Rec.Kind)
            {
                case RecordKind.Gym:
                case RecordKind.Rival:
                    return new VsIntroPreview.Scene
                    {
                        Kind = VsIntroPreview.Layout.Gym, Face = Rec.FaceMembers(), Banner = Rec.BannerMembers(), Vs = Four(s.VsMark),
                        FaceColours = ClassColours(IsPlatinum ? Rec.Get(RecordField.Class) : -1),
                        EndX = Rec.Get(RecordField.EndX), NamePalette = s.NamePalette, Name = name,
                    };
                case RecordKind.League:
                    var frame = Order(s.LeagueFrame, 3);
                    return new VsIntroPreview.Scene
                    {
                        Kind = VsIntroPreview.Layout.League, Face = Rec.FaceMembers(), Vs = Four(s.VsMark),
                        FaceColours = ClassColours(IsPlatinum ? Rec.Get(RecordField.Class) : -1),
                        PlayerFace = Four(_girl ? s.PlayerFaceGirl : s.PlayerFaceBoy), PlayerColours = ClassColours(IsPlatinum ? (_girl ? 1 : 0) : -1),
                        Frame = new[] { frame[1], frame[2] }, FramePalette = Rec.Get(RecordField.FramePalette),
                        ClashFrames = Rec.Get(RecordField.ClashFrames), NamePalette = s.NamePalette, Name = name,
                    };
                default:
                    return new VsIntroPreview.Scene
                    {
                        Kind = VsIntroPreview.Layout.Executive, Face = Rec.FaceMembers(), NamePalette = s.NamePalette, Name = name,
                        Backdrop = s.ExecutiveBackdrop >= 0 ? Enumerable.Range(s.ExecutiveBackdrop, 4).ToArray() : null,
                    };
            }
        }

        private int _frame = -1;
        private global::Avalonia.Threading.DispatcherTimer _timer;
        public bool Animating => _timer != null;
        public string AnimateLabel => Animating ? "Stop" : "Animate";

        private void RenderPreview()
        {
            Raise(nameof(HasPreview), nameof(ShowPlayerChoice));
            var scene = Scene();
            if (scene == null) { PreviewImage = null; return; }
            try { PreviewImage = ImageConverter.FromRgba(_preview.Draw(scene, _frame), DsBgScreen.Width, DsBgScreen.Height); }
            catch (Exception e) { AppLogger.Error("VS intro preview failed: " + e.Message); PreviewImage = null; }
        }

        public void ToggleAnimation()
        {
            if (Animating) { StopAnimation(); RenderPreview(); return; }
            var scene = Scene();
            if (scene == null) return;
            int length = VsIntroPreview.Length(scene);
            _frame = 0;
            _timer = new global::Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000.0 / 30) };
            _timer.Tick += (_, _) =>
            {
                if (++_frame >= length) { StopAnimation(); }
                RenderPreview();
            };
            _timer.Start();
            Raise(nameof(Animating), nameof(AnimateLabel));
            RenderPreview();
        }

        public void StopAnimation()
        {
            _timer?.Stop();
            _timer = null;
            _frame = -1;
            Raise(nameof(Animating), nameof(AnimateLabel));
        }

        // ── Trainer intros tab ───────────────────────────────────────────────────────────────────

        public ObservableCollection<IntroRow> IntroRows { get; } = new();
        private List<int> _trainerCombos = new();

        private string GroupOf(int combo)
        {
            var kind = _t.KindOfEffect(_t.EffectOf(combo));
            if (kind == IntroKind.Gym) return "Gym Leaders";
            if (kind == IntroKind.League) return "Elite Four and Champion";
            if (kind == IntroKind.Rival || _t.RoleOf(combo) == ComboRole.Rival) return "Rival";
            if (kind is IntroKind.TeamGrunt or IntroKind.TeamLeader) return IsHgss ? "Team Rocket" : "Team Galactic";
            return "Other";
        }

        private void BuildIntroRows()
        {
            IntroRows.Clear();
            _trainerCombos = Enumerable.Range(0, _t.ComboCount).Where(_t.OwnsMusic).ToList();
            foreach (string group in new[] { "Gym Leaders", "Elite Four and Champion", "Rival", "Team Rocket", "Team Galactic", "Other" })
            {
                var combos = _trainerCombos.Where(c => GroupOf(c) == group).ToList();
                if (combos.Count == 0) continue;
                IntroRows.Add(new IntroRow { IsHeader = true, Title = group });
                foreach (int c in combos) IntroRows.Add(new IntroRow { Combo = c });
            }
            RenameIntros();
        }

        private void RenameIntros()
        {
            foreach (var row in IntroRows.Where(r => r.IsItem))
            {
                row.Title = _names.Combo(_t, row.Combo);
                row.Detail = _names.Sequence(_t.SequenceOf(row.Combo));
            }
        }

        private IntroRow _intro;
        public IntroRow SelectedIntro
        {
            get => _intro;
            set
            {
                if (value != null && value.IsHeader) { Raise(); return; }
                if (_intro == value) return;
                _intro = value;
                RaiseIntro();
            }
        }

        private int Combo => _intro?.Combo ?? -1;
        public bool HasIntro => Combo >= 0;
        public string IntroTitle => HasIntro ? _names.Combo(_t, Combo) : "";
        public string IntroKindText => HasIntro ? VsIntroNames.Kind(_t, _t.EffectOf(Combo)) : "";
        public int SequenceIndex
        {
            get => HasIntro ? _sequenceIds.IndexOf(_t.SequenceOf(Combo)) : -1;
            set
            {
                if (_suppress || !HasIntro || value < 0 || value >= _sequenceIds.Count || value == SequenceIndex) return;
                _t.SetSequence(Combo, _sequenceIds[value]);
                Edited();
            }
        }
        public bool HasIntroMugshot => HasIntro && MugshotRows.Any(r => r.Record != null && r.Record == _t.RecordFor(_t.EffectOf(Combo)));
        public ObservableCollection<string> IntroClasses { get; } = new();
        public string IntroClassesNote => !HasIntro ? "" : _t.RoleOf(Combo) switch
        {
            ComboRole.Ordinary => "Every class not given another intro.",
            ComboRole.Frontier or ComboRole.Link or ComboRole.Double or ComboRole.FrontierBrain or ComboRole.DoubleLeader
                => "Picked by the kind of battle, whatever the class.",
            _ => IntroClasses.Count == 0 ? "No class uses this intro." : "",
        };

        private void RaiseIntro()
        {
            _suppress = true;
            try
            {
                IntroClasses.Clear();
                if (HasIntro) foreach (int c in _t.ClassesUsing(Combo)) IntroClasses.Add(_names.Class(c));
                Raise(nameof(HasIntro), nameof(IntroTitle), nameof(IntroKindText), nameof(SequenceIndex), nameof(HasIntroMugshot), nameof(IntroClassesNote));
            }
            finally { _suppress = false; }
        }

        public void ShowMugshot()
        {
            if (!HasIntro) return;
            var record = _t.RecordFor(_t.EffectOf(Combo));
            var row = MugshotRows.FirstOrDefault(r => r.Record != null && r.Record == record);
            if (row == null) return;
            SelectedMugshot = row;
            SelectedTab = 0;
        }

        /// <summary>Selects the class list's row for one of the classes the selected intro lists.</summary>
        public void GoToClass(int index)
        {
            var classes = HasIntro ? _t.ClassesUsing(Combo) : new List<int>();
            if (index < 0 || index >= classes.Count) return;
            SelectedClass = ClassRows.FirstOrDefault(r => r.Class == classes[index]);
        }

        // ── Classes ──────────────────────────────────────────────────────────────────────────────

        public ObservableCollection<IntroRow> ClassRows { get; } = new();
        public ObservableCollection<string> ClassIntroChoices { get; } = new();
        private readonly List<int> _classIntroCombos = new();

        private void BuildClassRows()
        {
            ClassRows.Clear();
            for (int c = 0; c < _names.Classes.Length; c++) ClassRows.Add(new IntroRow { Class = c });
            RenameClasses();
        }

        private void RenameClasses()
        {
            foreach (var row in ClassRows)
            {
                row.Title = _names.Class(row.Class);
                int combo = _t.ComboForClass(row.Class);
                row.Detail = combo < 0 ? "Unknown" : _names.Combo(_t, combo);
            }
        }

        private IntroRow _class;
        public IntroRow SelectedClass
        {
            get => _class;
            set
            {
                if (_class == value) return;
                _class = value;
                RaiseClass();
            }
        }

        private int ClassId => _class?.Class ?? -1;
        public bool HasClass => ClassId >= 0;
        public string ClassTitle => HasClass ? _names.Class(ClassId) : "";
        public bool ClassEditable => HasClass && (IsHgss || _t.InSwitch(ClassId));
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
        public bool CanMakeClassRoom => IsHgss && !_t.ClassTableMoved;

        /// <summary>Gives the class table room for more rows. It writes at once, so it asks first.</summary>
        public async System.Threading.Tasks.Task MakeClassRoomAsync()
        {
            string why = _t.WhyNoClassRoom();
            if (why != null) { StatusText = why; return; }
            if (!await DialogHelper.AskYesNo($"Move the class table to the expanded ARM9 area? It will hold up to {VsIntroTables.MostClassRows} classes instead of {_t.ClassRowCount}. This is written right away.", "Make room")) return;
            try { _t.MakeClassRoom(); }
            catch (Exception ex) { StatusText = ex.Message; return; }
            StatusText = $"The class table now has room for {_t.ClassRowCount} classes.";
            Raise(nameof(CanMakeClassRoom), nameof(ClassNote));
        }

        public int ClassIntroIndex
        {
            get => HasClass ? _classIntroCombos.IndexOf(_t.ComboForClass(ClassId)) : -1;
            set
            {
                if (_suppress || !ClassEditable || value < 0 || value >= _classIntroCombos.Count || value == ClassIntroIndex) return;
                int combo = _classIntroCombos[value];
                bool ok = _t.RoleOf(combo) == ComboRole.Ordinary ? (_t.UnassignClass(ClassId) || true) : _t.AssignClass(ClassId, combo);
                if (!ok)
                {
                    StatusText = _t.ClassTableMoved
                        ? $"All {_t.ClassRowCount} class rows are in use. Set another class back to ordinary trainers first."
                        : $"All {_t.ClassRowCount} class rows are in use. Make room for more, or set another class back to ordinary trainers.";
                    RaiseClass();
                    return;
                }
                Edited();
            }
        }

        private void RaiseClass()
        {
            _suppress = true;
            try
            {
                _classIntroCombos.Clear();
                ClassIntroChoices.Clear();
                if (HasClass)
                {
                    int now = _t.ComboForClass(ClassId);
                    foreach (int c in _trainerCombos)
                        if (c == now || (IsHgss ? c < 64 : _t.CanSwitchTo(c)))
                        {
                            _classIntroCombos.Add(c);
                            ClassIntroChoices.Add($"{_names.Combo(_t, c)} ({VsIntroNames.Kind(_t, _t.EffectOf(c))})");
                        }
                }
                Raise(nameof(HasClass), nameof(ClassTitle), nameof(ClassEditable), nameof(ClassNote), nameof(ClassIntroIndex));
            }
            finally { _suppress = false; }
        }

        // ── Editing, undo and saving ─────────────────────────────────────────────────────────────

        private ByteStateUndo _undo;
        private void StartUndo() => _undo = new ByteStateUndo(_t.Snapshot, s => { _t.Restore(s); RefreshAll(); }, () => Raise(nameof(CanUndo), nameof(CanRedo)));
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() => _undo?.Undo();
        public void Redo() => _undo?.Redo();

        private void Edited()
        {
            _undo?.Record();
            RefreshAll();
        }

        private void RefreshAll()
        {
            RenameMugshots();
            RenameIntros();
            RenameClasses();
            RaiseMugshot();
            RaiseIntro();
            RaiseClass();
            Raise(nameof(HasUnsavedChanges));
        }

        public bool HasUnsavedChanges => _t?.HasChanges == true;
        public string UnsavedChangesDescription => "VS intros";

        public void SaveChanges() => _ = SaveChangesAsync();

        public async Task<bool> SaveChangesAsync()
        {
            if (_t == null || !_t.HasChanges) return true;
            try { _t.Save(); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                await DialogHelper.ShowError("The VS intros were not saved:\n" + e.Message, Title);
                return false;
            }
            RefreshAll();
            SaveNotice.Saved(UnsavedChangesDescription);
            StatusText = "Saved. Save the ROM to keep the changes.";
            return true;
        }

        public void DiscardChanges()
        {
            if (_t == null) return;
            try { _t = VsIntroTables.Load(Part.Trainers); }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is InvalidOperationException)
            {
                _ = DialogHelper.ShowError("The saved VS intros couldn't be read back:\n" + e.Message, Title);
                return;
            }
            int mug = MugshotRows.IndexOf(_mugshot), intro = IntroRows.IndexOf(_intro), cls = ClassRows.IndexOf(_class);
            _mugshot = null; _intro = null; _class = null;
            BuildChoices();
            BuildMugshotRows();
            BuildIntroRows();
            BuildClassRows();
            StartUndo();
            SelectedMugshot = mug >= 0 && mug < MugshotRows.Count ? MugshotRows[mug] : MugshotRows.FirstOrDefault(r => r.IsItem);
            SelectedIntro = intro >= 0 && intro < IntroRows.Count ? IntroRows[intro] : IntroRows.FirstOrDefault(r => r.IsItem);
            SelectedClass = cls >= 0 && cls < ClassRows.Count ? ClassRows[cls] : ClassRows.FirstOrDefault();
            Raise(nameof(HasUnsavedChanges), nameof(CanUndo), nameof(CanRedo));
        }
    }
}
