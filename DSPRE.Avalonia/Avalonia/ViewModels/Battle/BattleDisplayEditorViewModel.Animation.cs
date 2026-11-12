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
        // DP pokeanm record: front number and wait, 3 back steps, 10 pattern steps from byte 8.
        private const int AnimRecLen = 28, AnimPatOffset = 8, AnimPatMax = 10;
        private OffsetNarc _animNarc;
        private bool _animNarcTried;

        private int _animFrontProg; public int AnimFrontProgNum { get => _animFrontProg; set { if (Set(ref _animFrontProg, value)) { if (!_loading) SetDirty(); if (_scriptTarget == 0) RefreshProgramScript(); else OnPropertyChanged(nameof(ProgramScriptHeader)); } } }
        private int _animFrontWait; public int AnimFrontWait { get => _animFrontWait; set { if (Set(ref _animFrontWait, value) && !_loading) SetDirty(); } }

        // Platinum, HGSS and hg-engine only: front and back each have a cry delay.
        private int _animFrontCryDelay; public int AnimFrontCryDelay { get => _animFrontCryDelay; set { if (Set(ref _animFrontCryDelay, value) && !_loading) SetDirty(); } }
        private int _animBackCryDelay; public int AnimBackCryDelay { get => _animBackCryDelay; set { if (Set(ref _animBackCryDelay, value) && !_loading) SetDirty(); } }

        /// <summary>The back animation: number and start delay. One entry, three on DP.</summary>
        public ObservableCollection<AnimProgStep> AnimBack { get; } = new ObservableCollection<AnimProgStep>();
        public string AnimBackLabel => RecordFamily ? "Back animation (# and start delay)" : "Back program steps";
        /// <summary>DP's pattern steps. Platinum, HGSS and hg-engine use the frame runs.</summary>
        public ObservableCollection<AnimPatternStep> AnimSteps { get; } = new ObservableCollection<AnimPatternStep>();

        public bool CanAddAnimStep => AnimSteps.Count < AnimPatMax;

        private void EnsureAnimNarc()
        {
            if (_animNarcTried) return;
            _animNarcTried = true;
            if (IsAvailable) _animNarc = new OffsetNarc(DirNames.PokeAnim, AnimRecLen);
        }

        private void LoadAnim(int id)
        {
            HasAnimData = false;
            foreach (AnimPatternStep s in AnimSteps) s.PropertyChanged -= OnAnimStepChanged;
            foreach (AnimProgStep s in AnimBack) s.PropertyChanged -= OnAnimStepChanged;
            AnimSteps.Clear(); AnimBack.Clear();
            _animFrontCryDelay = _animBackCryDelay = 0;
            OnPropertyChanged(nameof(AnimBackLabel));
            if (!IsAvailable || id < 0) { OnPropertyChanged(nameof(CanAddAnimStep)); return; }

            if (HgEngineProject.IsActive) { LoadAnimFromHgeSource(_currentHgeSpeciesId); return; }
            if (RecordFamily) { LoadAnimFromRecord(); return; }

            EnsureAnimNarc();
            byte[] r = _animNarc?.GetRecord(BaseSpeciesIdFor(id));
            if (r == null || r.Length < AnimRecLen) { OnPropertyChanged(nameof(CanAddAnimStep)); return; }
            _animFrontProg = r[0]; _animFrontWait = r[1];
            for (int i = 0; i < 3; i++) AddBackStep(r[2 + i * 2], r[3 + i * 2]);
            for (int i = 0; i < AnimPatMax; i++)
            {
                sbyte patno = (sbyte)r[AnimPatOffset + i * 2];
                if (patno == -1) break;   // -1 ends; lower values are loops back, which the player follows
                AddPatternStep(patno, r[AnimPatOffset + i * 2 + 1]);
            }
            RecomputePatternSlots();
            OnPropertyChanged(nameof(AnimFrontProgNum)); OnPropertyChanged(nameof(AnimFrontWait)); OnPropertyChanged(nameof(CanAddAnimStep));
            HasAnimData = true;
            RefreshProgramScript();
        }

        private void LoadAnimFromRecord()
        {
            SpeciesSpriteData rec = ReadRecord();
            if (rec == null) { OnPropertyChanged(nameof(CanAddAnimStep)); return; }
            _animFrontProg = rec.Front.Animation; _animFrontWait = rec.Front.StartDelay; _animFrontCryDelay = rec.Front.CryDelay;
            AddBackStep(rec.Back.Animation, rec.Back.StartDelay);
            _animBackCryDelay = rec.Back.CryDelay;
            RaiseAnimFields();
        }

        private void LoadAnimFromHgeSource(int id)
        {
            if (!HgEngineSpriteOffsets.TryLoad(id, out HgEngineSourceBlock block, out _)) { OnPropertyChanged(nameof(CanAddAnimStep)); return; }
            if (!block.TryGetInt(new[] { FieldPathSegment.Field("frontHeader"), FieldPathSegment.Field("animation") }, out _animFrontProg)) { OnPropertyChanged(nameof(CanAddAnimStep)); return; }
            block.TryGetInt(new[] { FieldPathSegment.Field("frontHeader"), FieldPathSegment.Field("animationDelay") }, out _animFrontWait);
            block.TryGetInt(new[] { FieldPathSegment.Field("frontHeader"), FieldPathSegment.Field("cryDelay") }, out _animFrontCryDelay);
            block.TryGetInt(new[] { FieldPathSegment.Field("backHeader"), FieldPathSegment.Field("animation") }, out int backProg);
            block.TryGetInt(new[] { FieldPathSegment.Field("backHeader"), FieldPathSegment.Field("animationDelay") }, out int backWait);
            block.TryGetInt(new[] { FieldPathSegment.Field("backHeader"), FieldPathSegment.Field("cryDelay") }, out _animBackCryDelay);
            foreach (AnimProgStep s in AnimBack) s.PropertyChanged -= OnAnimStepChanged;
            AnimBack.Clear();
            AddBackStep(backProg, backWait);
            RaiseAnimFields();
        }

        private void RaiseAnimFields()
        {
            OnPropertyChanged(nameof(AnimFrontProgNum)); OnPropertyChanged(nameof(AnimFrontWait));
            OnPropertyChanged(nameof(AnimFrontCryDelay)); OnPropertyChanged(nameof(AnimBackCryDelay));
            OnPropertyChanged(nameof(CanAddAnimStep));
            HasAnimData = true;
            RefreshProgramScript();
        }

        private void SaveAnim()
        {
            if (!_hasAnimData) return;
            int backProg = AnimBack.Count > 0 ? AnimBack[0].Number : 0;
            int backWait = AnimBack.Count > 0 ? AnimBack[0].Wait : 0;

            if (HgEngineProject.IsActive)
            {
                HgEngineFieldWrite[] fields = new[]
                {
                    new HgEngineFieldWrite(new[] { FieldPathSegment.Field("frontHeader"), FieldPathSegment.Field("animation") }, _animFrontProg.ToString()),
                    new HgEngineFieldWrite(new[] { FieldPathSegment.Field("frontHeader"), FieldPathSegment.Field("animationDelay") }, _animFrontWait.ToString()),
                    new HgEngineFieldWrite(new[] { FieldPathSegment.Field("frontHeader"), FieldPathSegment.Field("cryDelay") }, _animFrontCryDelay.ToString()),
                    new HgEngineFieldWrite(new[] { FieldPathSegment.Field("backHeader"), FieldPathSegment.Field("animation") }, backProg.ToString()),
                    new HgEngineFieldWrite(new[] { FieldPathSegment.Field("backHeader"), FieldPathSegment.Field("animationDelay") }, backWait.ToString()),
                    new HgEngineFieldWrite(new[] { FieldPathSegment.Field("backHeader"), FieldPathSegment.Field("cryDelay") }, _animBackCryDelay.ToString()),
                };
                WriteSpriteOffsets(fields);
                return;
            }

            if (RecordFamily)
            {
                EditRecord(rec =>
                {
                    rec.Front.Animation = _animFrontProg; rec.Front.StartDelay = _animFrontWait; rec.Front.CryDelay = _animFrontCryDelay;
                    rec.Back.Animation = backProg; rec.Back.StartDelay = backWait; rec.Back.CryDelay = _animBackCryDelay;
                });
                return;
            }

            if (_animNarc == null) return;
            int animId = BaseSpeciesIdFor(_currentId);
            byte[] r = _animNarc.GetRecord(animId);
            if (r == null || r.Length < AnimRecLen) return;
            r[0] = (byte)_animFrontProg; r[1] = (byte)_animFrontWait;
            for (int i = 0; i < 3 && i < AnimBack.Count; i++) { r[2 + i * 2] = (byte)AnimBack[i].Number; r[3 + i * 2] = (byte)AnimBack[i].Wait; }
            for (int i = 0; i < AnimPatMax; i++)
            {
                if (i < AnimSteps.Count) { r[AnimPatOffset + i * 2] = (byte)(sbyte)AnimSteps[i].Frame; r[AnimPatOffset + i * 2 + 1] = (byte)AnimSteps[i].Wait; }
                else
                {
                    // The game stops at the first -1, so a terminator and what follows it stay as read.
                    if (r[AnimPatOffset + i * 2] != 0xFF) { r[AnimPatOffset + i * 2] = 0xFF; r[AnimPatOffset + i * 2 + 1] = 0; }
                    break;
                }
            }
            _animNarc.PutRecord(animId, r);
        }

        private void AddBackStep(int num, int wait) { AnimProgStep s = new AnimProgStep { Number = num, Wait = wait }; s.PropertyChanged += OnAnimStepChanged; AnimBack.Add(s); }
        private void AddPatternStep(int frame, int wait) { AnimPatternStep s = new AnimPatternStep { Frame = frame, Wait = wait }; s.PropertyChanged += OnAnimStepChanged; AnimSteps.Add(s); }
        private void OnAnimStepChanged(object _, PropertyChangedEventArgs __) { if (!_loading) { SetDirty(); RecomputePatternSlots(); } }

        public void AddAnimStep()
        {
            if (AnimSteps.Count >= AnimPatMax) return;
            AddPatternStep(0, 4);
            OnPropertyChanged(nameof(CanAddAnimStep));
            if (!_loading) { SetDirty(); RecomputePatternSlots(); }
        }
        public void RemoveAnimStep(AnimPatternStep step)
        {
            if (step == null || !AnimSteps.Contains(step)) return;
            step.PropertyChanged -= OnAnimStepChanged;
            AnimSteps.Remove(step);
            OnPropertyChanged(nameof(CanAddAnimStep));
            if (!_loading) { SetDirty(); RecomputePatternSlots(); }
        }

        // DP drives both sprites from its one pattern list; the other families have their own frame runs.
        private void RecomputePatternSlots()
        {
            if (RecordFamily) return;
            List<SpriteFrameSlot> slots = AnimSteps.Select(s => new SpriteFrameSlot(s.Frame, s.Wait, 0, 0)).ToList();
            SetFrameSlots(slots, slots);
        }

        // Fixed size, no add/remove. FrameNo -1 ends the run.
        public ObservableCollection<SpriteFrameEntry> FrontFrameEntries { get; } = new ObservableCollection<SpriteFrameEntry>();
        public ObservableCollection<SpriteFrameEntry> BackFrameEntries { get; } = new ObservableCollection<SpriteFrameEntry>();
        public bool HasFrameRuns => RecordFamily && _hasSpriteData;

        private void LoadFrameEntries(IEnumerable<SpriteFrameSlot> front, IEnumerable<SpriteFrameSlot> back)
        {
            foreach (SpriteFrameEntry e in FrontFrameEntries) e.PropertyChanged -= OnFrameEntryChanged;
            foreach (SpriteFrameEntry e in BackFrameEntries) e.PropertyChanged -= OnFrameEntryChanged;
            FrontFrameEntries.Clear(); BackFrameEntries.Clear();

            foreach (SpriteFrameSlot slot in front) AddFrameEntry(FrontFrameEntries, slot);
            foreach (SpriteFrameSlot slot in back) AddFrameEntry(BackFrameEntries, slot);

            SetFrameSlots(ToSlotData(FrontFrameEntries), ToSlotData(BackFrameEntries));
            OnPropertyChanged(nameof(HasFrameRuns));
        }

        private void AddFrameEntry(ObservableCollection<SpriteFrameEntry> list, SpriteFrameSlot slot)
        {
            SpriteFrameEntry e = new SpriteFrameEntry
            {
                FrameNo = slot.FrameNo, Duration = slot.Duration,
                HorizontalShift = slot.HorizontalShift, VerticalShift = slot.VerticalShift,
            };
            e.PropertyChanged += OnFrameEntryChanged;
            list.Add(e);
        }

        private void OnFrameEntryChanged(object _, PropertyChangedEventArgs __)
        {
            if (_loading) return;
            SetDirty();
            SetFrameSlots(ToSlotData(FrontFrameEntries), ToSlotData(BackFrameEntries));
        }

        // Every slot is needed: a frameNo below -1 is a counted jump, and -1 ends the run on frame 0.
        private void SetFrameSlots(List<SpriteFrameSlot> front, List<SpriteFrameSlot> back)
        {
            _frontSlots = front;
            _backSlots = back;
            StopPlayback();
            RaiseFrameWarnings();
        }

        private void SaveFrames()
        {
            if (!RecordFamily || !_hasSpriteData) return;
            List<SpriteFrameSlot> front = ToSlotData(FrontFrameEntries);
            List<SpriteFrameSlot> back = ToSlotData(BackFrameEntries);

            if (HgEngineProject.IsActive)
            {
                List<HgEngineFieldWrite> fields = new List<HgEngineFieldWrite>();
                fields.AddRange(HgEngineSpriteOffsets.BuildFrameWrites("frontFrames", front));
                fields.AddRange(HgEngineSpriteOffsets.BuildFrameWrites("backFrames", back));
                WriteSpriteOffsets(fields);
                return;
            }

            EditRecord(rec =>
            {
                for (int i = 0; i < SpeciesSpriteData.FrameCount; i++)
                {
                    if (i < front.Count) rec.Front.Frames[i] = front[i];
                    if (i < back.Count) rec.Back.Frames[i] = back[i];
                }
            });
        }

        private static List<SpriteFrameSlot> ToSlotData(ObservableCollection<SpriteFrameEntry> entries)
        {
            List<SpriteFrameSlot> slots = new List<SpriteFrameSlot>(entries.Count);
            foreach (SpriteFrameEntry e in entries)
                slots.Add(new SpriteFrameSlot(e.FrameNo, e.Duration, e.HorizontalShift, e.VerticalShift));
            return slots;
        }
    }
}
