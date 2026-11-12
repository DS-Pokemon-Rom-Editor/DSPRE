using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using DSPRE.Avalonia.BattleAnim.Particles;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.Gl;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using static DSPRE.RomInfo;
using static MKDS_Course_Editor.NSBTP.NSBTP.NSBTP_File;
using IEditorWithUnsavedChanges = global::DSPRE.Editors.IEditorWithUnsavedChanges;

namespace DSPRE.Avalonia.ViewModels.Battle
{
    public partial class BattleDisplayEditorViewModel
    {
        // Pt and HGSS keep offsets and animation in one 89-byte record. DP and Pt add height.narc.
        private IBattleOffsetSource _src;
        private bool _srcTried;

        // Every edit goes through this one cache: each write saves the whole archive from its copy.
        private OffsetNarc _recordNarc;
        private static bool RecordFamily => GameFamily == GameFamilies.Plat || GameFamily == GameFamilies.HGSS;

        private void EnsureSource()
        {
            if (_srcTried) return;
            _srcTried = true;
            try
            {
                if (RecordFamily) _recordNarc = new OffsetNarc(DirNames.PokemonSpriteOffsets, SpeciesSpriteData.Size);
                _src = GameFamily switch
                {
                    GameFamilies.HGSS or GameFamilies.Plat => new CombinedTailSource(_recordNarc, withHeights: true),
                    GameFamilies.DP => new SeparateByteSource(DirNames.PokeYofs, DirNames.PokeShadowOfx, DirNames.PokeShadow),
                    _ => null,
                };
            }
            catch (Exception ex) { AppLogger.WarnOnce("BattleDisplayEditorViewModel.EnsureSource", ex); _src = null; }
        }

        private SpeciesSpriteData ReadRecord()
        {
            EnsureSource();
            return SpeciesSpriteData.Parse(_recordNarc?.GetRecord(BaseSpeciesIdFor(_currentId)));
        }

        private void EditRecord(Action<SpeciesSpriteData> edit)
        {
            SpeciesSpriteData rec = ReadRecord();
            if (rec == null) return;
            edit(rec);
            _recordNarc.PutRecord(BaseSpeciesIdFor(_currentId), rec.ToBytes());
        }

        private bool _hasSpriteData;
        public bool HasSpriteData
        {
            get => _hasSpriteData;
            private set { if (Set(ref _hasSpriteData, value)) OnPropertyChanged(nameof(HasFrameRuns)); }
        }

        private bool _hasHeights;
        public bool HasHeights { get => _hasHeights; private set { if (Set(ref _hasHeights, value)) OnPropertyChanged(nameof(ShowBaseHeights)); } }

        private bool _hasAnimData;
        public bool HasAnimData { get => _hasAnimData; private set => Set(ref _hasAnimData, value); }

        // hg-engine has no _src, so it must not gate dirty tracking.
        private bool CanEditSprite => (_src != null || HgEngineProject.IsActive) && _hasSpriteData && !_loading;

        private int _spriteY;   // signed −128..127, additive (positive = up, negative = down)
        public int SpriteY { get => _spriteY; set { if (Set(ref _spriteY, value)) { if (CanEditSprite) SetDirty(); RaiseLayout(); } } }

        private int _shadowX;   // signed −128..127 (negative = left, positive = right)
        public int ShadowX { get => _shadowX; set { if (Set(ref _shadowX, value)) { if (CanEditSprite) SetDirty(); RaiseLayout(); } } }

        private int _shadowSize;   // 0 none / 1 small / 2 medium / 3 large
        public int ShadowSize { get => _shadowSize; set { if (Set(ref _shadowSize, value)) { if (CanEditSprite) SetDirty(); RaiseLayout(); } } }

        public ObservableCollection<string> ShadowSizes { get; } =
            new ObservableCollection<string> { "None", "Small", "Medium", "Large" };

        private int _frontHeightM; public int FrontHeightM { get => _frontHeightM; set { if (Set(ref _frontHeightM, value)) { if (CanEditSprite) SetDirty(); OnPropertyChanged(nameof(FrontHeightUnified)); RaiseLayout(); } } }
        private int _frontHeightF; public int FrontHeightF { get => _frontHeightF; set { if (Set(ref _frontHeightF, value)) { if (CanEditSprite) SetDirty(); RaiseLayout(); } } }
        private int _backHeightM; public int BackHeightM { get => _backHeightM; set { if (Set(ref _backHeightM, value)) { if (CanEditSprite) SetDirty(); OnPropertyChanged(nameof(BackHeightUnified)); RaiseLayout(); } } }
        private int _backHeightF; public int BackHeightF { get => _backHeightF; set { if (Set(ref _backHeightF, value)) { if (CanEditSprite) SetDirty(); RaiseLayout(); } } }

        private bool _unifiedEdit = true;
        public bool UnifiedEdit { get => _unifiedEdit; set => Set(ref _unifiedEdit, value); }
        public int FrontHeightUnified { get => _frontHeightM; set { FrontHeightM = value; FrontHeightF = value; } }
        public int BackHeightUnified { get => _backHeightM; set { BackHeightM = value; BackHeightF = value; } }

        // HeightTable.c stores s8 and uses -1; the vanilla height narc is read as unsigned bytes.
        public decimal HeightMinimum => HgEngineProject.IsActive ? -128 : 0;
        public decimal HeightMaximum => HgEngineProject.IsActive ? 127 : 255;

        // height_o.narc, indexed by the form's otherpoke sprite index.
        private OffsetNarc _formHeightNarc;
        private bool _formNarcTried;
        private bool _formMode;
        private int _formIndex = -1;
        private int _formFrontH, _formBackH;

        private bool _hasFormHeights;
        public bool HasFormHeights { get => _hasFormHeights; private set { if (Set(ref _hasFormHeights, value)) OnPropertyChanged(nameof(ShowBaseHeights)); } }
        public bool FormMode => _formMode;
        public bool ShowBaseHeights => _hasHeights && !_formMode;

        public int FormFrontHeight { get => _formFrontH; set { if (Set(ref _formFrontH, value)) { if (!_loading) SetDirty(); RaiseLayout(); } } }
        public int FormBackHeight { get => _formBackH; set { if (Set(ref _formBackH, value)) { if (!_loading) SetDirty(); RaiseLayout(); } } }

        private void EnsureFormNarc()
        {
            if (_formNarcTried) return;
            _formNarcTried = true;
            _formHeightNarc = new OffsetNarc(DirNames.PokeHeightForms, 1);
        }

        private void LoadFormHeights()
        {
            HasFormHeights = false;
            if (!IsAvailable || !_formMode || _formIndex < 0) return;
            EnsureFormNarc();
            if (_formHeightNarc == null) return;
            if (_sprites == null || !_sprites.TryGetCurrentFormHeightIndices(out int backIdx, out int frontIdx)) return;
            try
            {
                byte[] b = _formHeightNarc.GetRecord(backIdx);
                byte[] f = _formHeightNarc.GetRecord(frontIdx);
                if (b == null || f == null || b.Length < 1 || f.Length < 1) return;
                _formBackH = b[0]; _formFrontH = f[0];
                OnPropertyChanged(nameof(FormFrontHeight)); OnPropertyChanged(nameof(FormBackHeight));
                HasFormHeights = true;
            }
            catch (Exception ex) { AppLogger.WarnOnce("BattleDisplayEditorViewModel.LoadFormHeights", ex); HasFormHeights = false; }
        }

        private void SaveFormHeights()
        {
            if (_formHeightNarc == null || !_hasFormHeights || _formIndex < 0) return;
            if (_sprites == null || !_sprites.TryGetCurrentFormHeightIndices(out int backIdx, out int frontIdx)) return;
            WriteForm(backIdx, _formBackH);
            WriteForm(frontIdx, _formFrontH);
        }
        // An unused slot is a real but empty file, so it is grown, not skipped.
        private void WriteForm(int idx, int v) => BattleOffsetBytes.Put(_formHeightNarc, idx, (byte)v);

        private void OnSpriteFormChanged()
        {
            _formMode = _sprites != null && _sprites.IsAlternateForms;
            _formIndex = _sprites != null ? _sprites.SelectedFormIndex : -1;
            LoadFormHeights();
            OnPropertyChanged(nameof(FormMode));
            OnPropertyChanged(nameof(ShowBaseHeights));
            RaiseLayout();
        }

        private void LoadSpriteData(int id)
        {
            HasSpriteData = false; HasHeights = false;
            SetFrameSlots(null, null);
            if (!IsAvailable || id < 0) return;
            try
            {
                if (HgEngineProject.IsActive) { LoadSpriteDataFromHgeSource(_currentHgeSpeciesId); return; }

                EnsureSource();
                if (_src == null) return;
                if (!_src.TryLoad(BaseSpeciesIdFor(id), out BattleOffsetRecord rec)) return;
                if (RecordFamily)
                {
                    SpeciesSpriteData record = ReadRecord();
                    if (record == null) return;
                    LoadFrameEntries(record.Front.Frames, record.Back.Frames);
                }
                _spriteY = rec.FrontY; _shadowX = rec.ShadowX; _shadowSize = rec.ShadowSize;
                _backHeightF = rec.BackF; _backHeightM = rec.BackM; _frontHeightF = rec.FrontF; _frontHeightM = rec.FrontM;
                OnPropertyChanged(nameof(SpriteY)); OnPropertyChanged(nameof(ShadowX)); OnPropertyChanged(nameof(ShadowSize));
                OnPropertyChanged(nameof(FrontHeightM)); OnPropertyChanged(nameof(FrontHeightF)); OnPropertyChanged(nameof(BackHeightM)); OnPropertyChanged(nameof(BackHeightF));
                OnPropertyChanged(nameof(FrontHeightUnified)); OnPropertyChanged(nameof(BackHeightUnified));
                HasHeights = rec.HasHeights;
                HasSpriteData = true;
            }
            catch (Exception ex) { AppLogger.WarnOnce("BattleDisplayEditorViewModel.LoadSpriteData", ex); HasSpriteData = false; }
        }

        private void LoadSpriteDataFromHgeSource(int id)
        {
            if (!HgEngineSpriteOffsets.TryLoad(id, out HgEngineSourceBlock block, out _)) return;
            if (!block.TryGetInt(new[] { FieldPathSegment.Field("spriteYOffset") }, out int spriteY)) return;
            if (!block.TryGetInt(new[] { FieldPathSegment.Field("shadowXOffset") }, out int shadowX)) return;
            if (!block.TryGetInt(new[] { FieldPathSegment.Field("shadowSize") }, out int shadowSize)) return;

            LoadFrameEntries(HgEngineSpriteOffsets.ReadFrameSlots(block, "frontFrames"), HgEngineSpriteOffsets.ReadFrameSlots(block, "backFrames"));

            _spriteY = spriteY; _shadowX = shadowX; _shadowSize = shadowSize;

            bool hasHeights = HgEngineHeightTable.TryGet(id, out int femaleBack, out int maleBack, out int femaleFront, out int maleFront);
            _backHeightF = hasHeights ? femaleBack : 0; _backHeightM = hasHeights ? maleBack : 0;
            _frontHeightF = hasHeights ? femaleFront : 0; _frontHeightM = hasHeights ? maleFront : 0;

            OnPropertyChanged(nameof(SpriteY)); OnPropertyChanged(nameof(ShadowX)); OnPropertyChanged(nameof(ShadowSize));
            OnPropertyChanged(nameof(FrontHeightM)); OnPropertyChanged(nameof(FrontHeightF)); OnPropertyChanged(nameof(BackHeightM)); OnPropertyChanged(nameof(BackHeightF));
            OnPropertyChanged(nameof(FrontHeightUnified)); OnPropertyChanged(nameof(BackHeightUnified));
            HasHeights = hasHeights;
            HasSpriteData = true;
        }

        // WriteAll turns the throw into the save's error, so the tab stays unsaved.
        private void WriteSpriteOffsets(IEnumerable<HgEngineFieldWrite> fields)
        {
            if (!HgEngineWriter.TryWriteFields(HgEngineDomain.SpriteOffsets, _currentHgeSpeciesId, fields, out _, out string error, allOrNothing: true))
                throw new InvalidOperationException(error);
        }

        private void SaveSpriteData()
        {
            if (!IsAvailable || !_hasSpriteData) return;

            if (HgEngineProject.IsActive)
            {
                // frontHeader.animation is SaveAnim's alone. Writing it here too overwrote its value.
                HgEngineFieldWrite[] fields = new[]
                {
                    new HgEngineFieldWrite(new[] { FieldPathSegment.Field("spriteYOffset") }, _spriteY.ToString()),
                    new HgEngineFieldWrite(new[] { FieldPathSegment.Field("shadowXOffset") }, _shadowX.ToString()),
                    new HgEngineFieldWrite(new[] { FieldPathSegment.Field("shadowSize") }, _shadowSize.ToString()),
                };
                WriteSpriteOffsets(fields);
                if (_hasHeights && !HgEngineHeightTable.TrySet(_currentHgeSpeciesId, _backHeightF, _backHeightM, _frontHeightF, _frontHeightM, out string heightError))
                    throw new InvalidOperationException(heightError);
                return;
            }

            if (_src == null) return;
            BattleOffsetRecord rec = new BattleOffsetRecord
            {
                FrontY = _spriteY,
                ShadowX = _shadowX,
                ShadowSize = _shadowSize,
                BackF = _backHeightF,
                BackM = _backHeightM,
                FrontF = _frontHeightF,
                FrontM = _frontHeightM,
                HasHeights = _hasHeights,
            };
            _src.Save(BaseSpeciesIdFor(_currentId), in rec);
        }
    }
}
