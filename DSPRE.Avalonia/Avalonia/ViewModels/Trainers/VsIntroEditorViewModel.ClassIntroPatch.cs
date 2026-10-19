using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DSPRE.Avalonia.Data;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;
using Row = DSPRE.ROMFiles.ClassIntroPatch.Row;
using Style = DSPRE.ROMFiles.ClassIntroPatch.Style;

namespace DSPRE.Avalonia.ViewModels.Trainers
{
    /// <summary>
    /// Diamond, Pearl or Platinum with the class intros patch: each class owns a row (style, battle music, a gym leader and
    /// an Elite Four record) and the game no longer picks intros with its switch. This mode edits those rows.
    /// </summary>
    public partial class VsIntroEditorViewModel
    {
        public const int ClassIntroPatchTab = 4;

        public bool IsClassIntroPatch { get; private set; }

        private readonly List<Row> _ciRows = new();
        private readonly List<byte[]> _ciSaved = new();
        private string[] _ciClassNames = Array.Empty<string>();

        public ObservableCollection<string> CiClassNames { get; } = new();
        public ObservableCollection<string> CiStyleChoices { get; } = new(ClassIntroPatch.StyleNames);

        private void LoadCiRows()
        {
            _ciRows.Clear();
            _ciSaved.Clear();
            List<Row> rows = ClassIntroPatch.ReadRows() ?? throw new InvalidOperationException("The class intros patch could not be read.");
            foreach (Row r in rows) { _ciRows.Add(r); _ciSaved.Add((byte[])r.Bytes.Clone()); }
            try { _ciClassNames = GetTrainerClassNames(); } catch (Exception ex) { AppLogger.WarnOnce("VsIntroEditorViewModel.LoadCiRows", ex); _ciClassNames = Array.Empty<string>(); }
        }

        private int CiClassCount => Math.Clamp(_ciClassNames.Length, 1, ClassIntroPatch.Classes);

        private void ReadyCiRows()
        {
            // Rows can hold themes the intro table never used, so the music list takes those too.
            foreach (int music in _ciRows.Take(CiClassCount).Select(r => (int)r.Music).Distinct())
                if (!_sequenceIds.Contains(music)) { _sequenceIds.Add(music); SequenceChoices.Add(_names.Sequence(music)); }
            ListSync.Apply(CiClassNames, Enumerable.Range(0, CiClassCount)
                .Select(i => $"[{i:D3}] {(i < _ciClassNames.Length ? _ciClassNames[i] : "")}").ToList());
            StartUndo();
            _ciIndex = -1;
            CiSelectedIndex = StartClass >= 0 && StartClass < CiClassCount ? StartClass : 0;
            SelectedTab = ClassIntroPatchTab;
            StatusText = "";
        }

        private int _ciIndex = -1;
        public int CiSelectedIndex
        {
            get => _ciIndex;
            set
            {
                if (value == _ciIndex || value < 0 || value >= CiClassCount) return;
                StopAnimation();
                _ciIndex = value;
                Raise(nameof(CiSelectedIndex));
                RaiseCiRow();
            }
        }

        private Row CiRow => _ciIndex >= 0 && _ciIndex < _ciRows.Count ? _ciRows[_ciIndex] : null;
        public bool CiHasRow => CiRow != null;
        public bool CiIsGym => CiRow?.Style == Style.Gym;
        public bool CiIsLeague => CiRow?.Style == Style.League;
        public bool CiHasRecord => CiIsGym || CiIsLeague;
        public bool CiPtRecord => CiHasRecord && !IsDp;
        public bool CiDpGym => CiIsGym && IsDp;
        public bool CiDpLeague => CiIsLeague && IsDp;
        public string CiClassLabel => IsDp ? "Face from class" : "Colours from class";
        public string CiClassTip => IsDp ? "The face is cut from this class's battle sprite" : "The portrait takes this class's battle sprite colours";

        public int CiStyleIndex
        {
            get => CiRow == null ? -1 : (int)CiRow.Style;
            set { if (!_suppress && CiRow != null && value >= 0 && value < ClassIntroPatch.StyleCount && value != (int)CiRow.Style) { CiRow.Style = (Style)value; CiEdited(); } }
        }

        public int CiMusicIndex
        {
            get => CiRow == null ? -1 : _sequenceIds.IndexOf(CiRow.Music);
            set { if (!_suppress && CiRow != null && value >= 0 && value < _sequenceIds.Count && _sequenceIds[value] != CiRow.Music) { CiRow.Music = (ushort)_sequenceIds[value]; CiEdited(); } }
        }

        // The name, colours and portrait belong to whichever record the style shows.
        public int CiNameIndex
        {
            get => CiIsGym ? CiRow.GymTrainer : CiIsLeague ? CiRow.LeagueTrainer : -1;
            set
            {
                if (_suppress || !CiHasRecord || value < 0 || value >= TrainerChoices.Count || value == CiNameIndex) return;
                if (CiIsGym) CiRow.GymTrainer = value; else CiRow.LeagueTrainer = value;
                CiEdited();
            }
        }

        // In Diamond and Pearl this is the class the face is cut from.
        public int CiColoursIndex
        {
            get => IsDp ? (CiIsGym ? CiRow.DpGymClass : CiIsLeague ? CiRow.DpLeagueClass : -1)
                        : CiIsGym ? CiRow.GymClass : CiIsLeague ? CiRow.LeagueClass : -1;
            set
            {
                if (_suppress || !CiHasRecord || value < 0 || value >= ClassChoices.Count || value == CiColoursIndex) return;
                if (IsDp && CiIsLeague && value > byte.MaxValue) { StatusText = "An Elite Four record keeps a class up to 255."; Raise(nameof(CiColoursIndex)); return; }
                if (IsDp) { if (CiIsGym) CiRow.DpGymClass = value; else CiRow.DpLeagueClass = value; }
                else if (CiIsGym) CiRow.GymClass = value; else CiRow.LeagueClass = value;
                CiEdited();
            }
        }

        public decimal CiFaceColumn
        {
            get => CiDpGym ? CiRow.DpFaceColumn : 0;
            set { if (!_suppress && CiDpGym && (int)value != CiRow.DpFaceColumn) { CiRow.DpFaceColumn = (int)value; CiEdited(); } }
        }

        public decimal CiCameraTurn
        {
            get => CiDpLeague ? CiRow.DpCameraTurn : 0;
            set { if (!_suppress && CiDpLeague && (int)value != CiRow.DpCameraTurn) { CiRow.DpCameraTurn = (int)value; CiEdited(); } }
        }

        public decimal CiPanFrames
        {
            get => CiDpLeague ? CiRow.DpPanFrames : 0;
            set { if (!_suppress && CiDpLeague && (int)value != CiRow.DpPanFrames) { CiRow.DpPanFrames = (int)value; CiEdited(); } }
        }

        private static int[] LeagueFace(int palette) => new[] { palette, palette + 1, palette + 2, palette + 3 };

        private VsIntroTables.Record StockRecordWithFace(int[] face) =>
            _t?.Records.FirstOrDefault(r => r.FaceMembers() is int[] m && m.SequenceEqual(face));

        // An Elite Four record names only its palette and reads the next three files, so only such sets fit it.
        public int CiFaceIndex
        {
            get
            {
                if (_hidePicks) return -1;
                if (CiIsGym) return _faceSets.FindIndex(s => s.SequenceEqual(CiRow.GymPortrait));
                if (CiIsLeague) return _faceSets.FindIndex(s => s.SequenceEqual(LeagueFace(CiRow.LeaguePortrait)));
                return -1;
            }
            set
            {
                if (_suppress || !CiHasRecord || value < 0 || value >= _faceSets.Count || value == CiFaceIndex) return;
                int[] set = _faceSets[value];
                if (CiIsGym && set.Any(m => m > byte.MaxValue)) { StatusText = "A gym leader record keeps file numbers up to 255."; Raise(nameof(CiFaceIndex)); return; }
                if (CiIsLeague && !set.SequenceEqual(LeagueFace(set[0]))) { StatusText = "An Elite Four portrait has to be four files in a row: palette, tiles, cells, animation."; Raise(nameof(CiFaceIndex)); return; }
                // The face is tinted with a class's sprite colours and named after a trainer. When those still match the
                // record the old face came from, they follow the new face's record; anything set by hand stays.
                VsIntroTables.Record before = StockRecordWithFace(CiIsGym ? CiRow.GymPortrait : LeagueFace(CiRow.LeaguePortrait));
                VsIntroTables.Record after = StockRecordWithFace(set);
                if (CiIsGym) CiRow.GymPortrait = set; else CiRow.LeaguePortrait = set[0];
                if (after != null)
                {
                    if (after.Has(VsIntroTables.RecordField.Class) && (before == null || CiColoursIndex == before.Get(VsIntroTables.RecordField.Class)))
                    {
                        if (CiIsGym) CiRow.GymClass = after.Get(VsIntroTables.RecordField.Class); else CiRow.LeagueClass = after.Get(VsIntroTables.RecordField.Class);
                    }
                    if (after.Has(VsIntroTables.RecordField.TrainerId) && (before == null || CiNameIndex == before.Get(VsIntroTables.RecordField.TrainerId)))
                    {
                        if (CiIsGym) CiRow.GymTrainer = after.Get(VsIntroTables.RecordField.TrainerId); else CiRow.LeagueTrainer = after.Get(VsIntroTables.RecordField.TrainerId);
                    }
                }
                CiEdited();
            }
        }

        public int CiBannerIndex
        {
            get => CiIsGym && !_hidePicks ? _bannerSets.FindIndex(s => s.SequenceEqual(CiRow.GymBanner)) : -1;
            set
            {
                if (_suppress || !CiIsGym || value < 0 || value >= _bannerSets.Count || value == CiBannerIndex) return;
                if (_bannerSets[value].Any(m => m > byte.MaxValue)) { StatusText = "A gym leader record keeps file numbers up to 255."; Raise(nameof(CiBannerIndex)); return; }
                CiRow.GymBanner = _bannerSets[value];
                CiEdited();
            }
        }

        public int CiFrameIndex
        {
            get => CiIsLeague && !_hidePicks ? _palettes.IndexOf(IsDp ? CiRow.DpBannerPalette : CiRow.LeagueFrame) : -1;
            set
            {
                if (_suppress || !CiIsLeague || value < 0 || value >= _palettes.Count || value == CiFrameIndex) return;
                if (_palettes[value] > byte.MaxValue) { StatusText = "An Elite Four record keeps a frame palette up to 255."; Raise(nameof(CiFrameIndex)); return; }
                if (IsDp) CiRow.DpBannerPalette = _palettes[value]; else CiRow.LeagueFrame = _palettes[value];
                CiEdited();
            }
        }

        public decimal CiEndX
        {
            get => CiIsGym ? CiRow.GymEndX : 0;
            set { if (!_suppress && CiIsGym && (int)value != CiRow.GymEndX) { CiRow.GymEndX = (int)value; CiEdited(); } }
        }

        public decimal CiClash
        {
            get => CiIsLeague ? CiRow.LeagueClash : 0;
            set { if (!_suppress && CiIsLeague && (int)value != CiRow.LeagueClash) { CiRow.LeagueClash = (int)value; CiEdited(); } }
        }

        private void RaiseCiRow()
        {
            _suppress = true;
            try
            {
                Raise(nameof(CiHasRow), nameof(CiIsGym), nameof(CiIsLeague), nameof(CiHasRecord), nameof(CiPtRecord), nameof(CiDpGym), nameof(CiDpLeague),
                      nameof(CiClassLabel), nameof(CiClassTip), nameof(CiFaceColumn), nameof(CiCameraTurn), nameof(CiPanFrames),
                      nameof(CiStyleIndex), nameof(CiMusicIndex),
                      nameof(CiNameIndex), nameof(CiColoursIndex), nameof(CiFaceIndex), nameof(CiBannerIndex), nameof(CiFrameIndex),
                      nameof(CiEndX), nameof(CiClash), nameof(ShowPlayerChoice), nameof(CiCanAnimate));
            }
            finally { _suppress = false; }
            RenderPreview();
        }

        private void CiEdited()
        {
            _undo?.Record();
            RaiseCiRow();
            Raise(nameof(HasUnsavedChanges));
        }

        // ── Preview ──────────────────────────────────────────────────────────────────────────────

        private VsIntroPreview.Scene CiScene()
        {
            Row r = CiRow;
            if (r == null || _preview == null || _t == null) return null;
            VsIntroSites s = _t.Sites;
            int[] Four(int first) => first < 0 ? null : Order(first, 4);
            if (IsDp && r.Style == Style.Gym)
                return new VsIntroPreview.Scene { Kind = VsIntroPreview.Layout.DpGym, Banner = new[] { s.GymBanner, s.GymBanner + 1, s.GymBanner + 2 }, Cut = DpCut(r.DpGymClass, r.DpFaceColumn), EndX = r.GymEndX };
            if (IsDp && r.Style == Style.League)
            {
                int[] dpFrame = Order(s.LeagueFrame, 3);
                return new VsIntroPreview.Scene
                {
                    Kind = VsIntroPreview.Layout.DpLeague, Cut = DpCut(r.DpLeagueClass, DpLeagueColumn), PlayerCut = DpCut(_girl ? 1 : 0, DpLeagueColumn),
                    Frame = new[] { dpFrame[1], dpFrame[2] }, FrameAnim = dpFrame[3], FramePalette = r.DpBannerPalette,
                };
            }
            switch (r.Style)
            {
                case Style.Gym:
                    return new VsIntroPreview.Scene
                    {
                        Kind = VsIntroPreview.Layout.Gym, Face = r.GymPortrait, Banner = r.GymBanner, Vs = Four(s.VsMark),
                        FaceColours = ClassColours(r.GymClass), EndX = r.GymEndX, NamePalette = s.NamePalette, Name = _names.Trainer(r.GymTrainer),
                    };
                case Style.League:
                    int[] frame = Order(s.LeagueFrame, 3);
                    return new VsIntroPreview.Scene
                    {
                        Kind = VsIntroPreview.Layout.League, Face = LeagueFace(r.LeaguePortrait), Vs = Four(s.VsMark),
                        FaceColours = ClassColours(r.LeagueClass),
                        PlayerFace = Four(_girl ? s.PlayerFaceGirl : s.PlayerFaceBoy), PlayerColours = ClassColours(_girl ? 1 : 0),
                        Frame = new[] { frame[1], frame[2] }, FrameAnim = frame[3], FramePalette = r.LeagueFrame,
                        ClashFrames = r.LeagueClash, NamePalette = s.NamePalette, Name = _names.Trainer(r.LeagueTrainer),
                        Particles = RomLocator.VsIntroMotionSites?.LeagueParticles,
                    };
                case Style.GalacticGrunt:
                    return Emblem() is int[] emblem
                        ? new VsIntroPreview.Scene { Kind = VsIntroPreview.Layout.Special, Special = VsIntroPreview.SpecialVariant.Rocket, Emblem = emblem, Flights = _motion?.Flights }
                        : null;
                default:
                    // The other styles move art the game fixes; the preview shows that art still.
                    List<int[]> pieces = (r.Style switch
                    {
                        Style.Ordinary => new[] { LowBall, HighBall },
                        Style.GalacticBoss => new[] { GalacticSprite },
                        Style.FrontierBrain => new[] { HighBall },
                        _ => new[] { LowBall },
                    }).Select(StillSprite).Where(m => m != null).ToList();
                    return pieces.Count == 0 ? null : new VsIntroPreview.Scene { Kind = VsIntroPreview.Layout.Pieces, Pieces = pieces };
            }
        }

        // The encounter archive's low and high level Poke Balls the ordinary, Frontier and double intros draw, and the
        // Galactic boss sprite (palette, tiles, cells, animation; pokeplatinum field_encounteffect.order).
        private static readonly int[] LowBall = { 0, 2, 4, 3 }, HighBall = { 0, 5, 7, 6 }, GalacticSprite = { 1, 8, 10, 9 };

        private int[] StillSprite(int[] m)
        {
            GraphicAssets.Kind[] want = { GraphicAssets.Kind.Palette, GraphicAssets.Kind.TileGraphic, GraphicAssets.Kind.CellLayout, GraphicAssets.Kind.CellAnimation };
            for (int i = 0; i < m.Length; i++)
                if (m[i] >= _kinds.Length || _kinds[m[i]] != want[i]) return null;
            return m;
        }

        // Diamond and Pearl's Elite Four intro cuts both faces one column in (pokediamond ov05_021EBCD8).
        private const int DpLeagueColumn = 1;
        private readonly Dictionary<(int, int), byte[]> _dpCuts = new();

        private byte[] DpCut(int trainerClass, int column)
        {
            if (_dpCuts.TryGetValue((trainerClass, column), out byte[] cut)) return cut;
            TrainerClassSpriteRenderer sprite = new TrainerClassSpriteRenderer();
            sprite.Load(trainerClass);
            return _dpCuts[(trainerClass, column)] = sprite.FlatCut(column * 8, VsIntroPreview.DpCutSize);
        }

        /// <summary>The still previews have nothing to play.</summary>
        public bool CiCanAnimate => CiScene() is VsIntroPreview.Scene scene && VsIntroPreview.Length(scene) > 1;

        // ── Undo, saving, discarding ─────────────────────────────────────────────────────────────

        private byte[] CiSnapshot() => _ciRows.SelectMany(r => r.Bytes).ToArray();

        private void CiRestore(byte[] all)
        {
            for (int i = 0; i < _ciRows.Count && (i + 1) * ClassIntroPatch.RowSize <= all.Length; i++)
                Array.Copy(all, i * ClassIntroPatch.RowSize, _ciRows[i].Bytes, 0, ClassIntroPatch.RowSize);
            RaiseCiRow();
            Raise(nameof(HasUnsavedChanges));
        }

        private bool CiRowsChanged => Enumerable.Range(0, _ciRows.Count).Any(i => !_ciRows[i].Bytes.SequenceEqual(_ciSaved[i]));

        private async Task<bool> SaveCiRowsAsync()
        {
            try
            {
                for (int i = 0; i < _ciRows.Count; i++)
                {
                    if (_ciRows[i].Bytes.SequenceEqual(_ciSaved[i])) continue;
                    ClassIntroPatch.WriteRow(i, _ciRows[i]);
                    _ciSaved[i] = (byte[])_ciRows[i].Bytes.Clone();
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException)
            {
                await DialogHelper.ShowErrorAsync("The class intros were not saved:\n" + e.Message, Title);
                Raise(nameof(HasUnsavedChanges));
                return false;
            }
            _undo?.MarkSaved();
            Raise(nameof(HasUnsavedChanges));
            AppEvents.RaiseClassIntrosSaved();
            SaveNotice.Saved(UnsavedChangesDescription);
            StatusText = "Saved. Save the ROM to keep the changes.";
            return true;
        }

        private void CiDiscard()
        {
            for (int i = 0; i < _ciRows.Count; i++) Array.Copy(_ciSaved[i], _ciRows[i].Bytes, ClassIntroPatch.RowSize);
            StartUndo();
            RaiseCiRow();
            Raise(nameof(HasUnsavedChanges), nameof(CanUndo), nameof(CanRedo));
        }

        private void ReloadCiNames()
        {
            _names = VsIntroNames.Read();
            try { _ciClassNames = GetTrainerClassNames(); } catch (Exception ex) { AppLogger.Warn("VS intro editor: class names: " + ex.Message); }
            ListSync.Apply(CiClassNames, Enumerable.Range(0, CiClassCount)
                .Select(i => $"[{i:D3}] {(i < _ciClassNames.Length ? _ciClassNames[i] : "")}").ToList());
            TrainerChoices.Clear();
            for (int i = 0; i < _names.Trainers.Length; i++) TrainerChoices.Add(_names.Trainer(i));
            ClassChoices.Clear();
            for (int i = 0; i < _names.Classes.Length; i++) ClassChoices.Add(_names.Class(i));
            RaiseCiRow();
        }
    }
}
