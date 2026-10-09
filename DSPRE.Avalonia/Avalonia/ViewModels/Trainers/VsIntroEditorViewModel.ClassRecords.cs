using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using DSPRE.Avalonia.Data;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;
using Field = DSPRE.ROMFiles.TrainerClassMetadataAssetField;
using Kind = DSPRE.Avalonia.Data.GraphicAssets.Kind;

namespace DSPRE.Avalonia.ViewModels.Trainers
{
    /// <summary>One picture a class intro draws, picked as a whole set of files from the intro art archive.</summary>
    public sealed class IntroArtSlot : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        internal void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public string Label { get; internal set; }
        internal Func<ushort, string> LabelFor { get; init; }

        /// <summary>The set's files one by one, for art that isn't laid out like the stock sets.</summary>
        public ObservableCollection<IntroArtFile> Parts { get; } = new();

        /// <summary>Sets with cells and an animation open in the cell animation editor.</summary>
        public bool CanAnimate => Roles.Contains(Kind.CellAnimation);
        internal Field[] Fields { get; init; }
        internal Kind[] Roles { get; init; }
        internal Func<ushort, bool> UsedBy { get; init; }
        internal List<int[]> Sets { get; } = new();
        public ObservableCollection<string> Choices { get; } = new();
        internal Action<IntroArtSlot, int> Picked;

        private int _index = -1;
        public int Index
        {
            get => _index;
            set { if (_index == value) return; _index = value; Raise(nameof(Index)); if (value >= 0) Picked?.Invoke(this, value); }
        }
        internal void Show(int index) { _index = index; Raise(nameof(Index)); }

        private bool _visible;
        public bool Visible { get => _visible; internal set { if (_visible == value) return; _visible = value; Raise(nameof(Visible)); } }
    }

    /// <summary>One file of an intro picture, by what it is (palette, tiles, cells, animation or screen).</summary>
    public sealed class IntroArtFile : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public string Label { get; init; }
        public bool CanShow { get; init; } = true;
        internal Field Field { get; init; }
        internal Field? PaletteField { get; init; }
        internal Action<IntroArtFile, int> Changed;
        private int _value;
        public int Value
        {
            get => _value;
            set { if (_value == value) return; _value = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value))); Changed?.Invoke(this, value); }
        }
        internal void Show(int value) { _value = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value))); }
    }

    /// <summary>
    /// With the trainer class metadata patch each class carries its own intro (VS style, name and art) in a/1/5/5,
    /// and the game no longer reads the stock intro tables. This mode edits those records instead.
    /// </summary>
    public partial class VsIntroEditorViewModel
    {
        /// <summary>The patch is installed: classes own their intros and the stock tables are retired.</summary>
        public bool IsClassRecords { get; private set; }
        public bool ShowStockTabs => !IsClassRecords;

        private readonly List<TrainerClassMetadataRecord> _records = new();
        private readonly List<byte[]> _savedRecords = new();
        private VsIntroSites _sites;
        private string[] _classNames = Array.Empty<string>();

        public ObservableCollection<string> ClassRecordNames { get; } = new();
        public ObservableCollection<string> StyleChoices { get; } = new(TrainerClassMetadataSchema.StyleNames);
        public ObservableCollection<string> StaticNameChoices { get; } = new();
        public ObservableCollection<IntroArtSlot> ArtSlots { get; } = new();

        /// <summary>The class to show first, when the editor is opened for one.</summary>
        public int StartClass { get; set; } = -1;

        private bool DetectClassRecords()
        {
            IsClassRecords = gameFamily == GameFamilies.HGSS &&
                TrainerClassMetadataStore.DetectCurrentRom(out _) == TrainerClassMetadataDetectionState.SchemaV1;
            return IsClassRecords;
        }

        private void LoadClassRecords()
        {
            if (!TrainerClassMetadataStore.EnsureUnpacked(out string error)) throw new InvalidOperationException(error);
            _sites = VsIntroCodeSites;
            _records.Clear();
            _savedRecords.Clear();
            for (int i = 0; i < TrainerClassMetadataStore.RecordCount; i++)
            {
                if (!TrainerClassMetadataStore.TryReadRecord(i, out TrainerClassMetadataRecord record, out error))
                    throw new InvalidOperationException(error);
                _records.Add(record);
                _savedRecords.Add(record.ToByteArray());
            }
            try { _classNames = GetTrainerClassNames(); } catch { _classNames = Array.Empty<string>(); }
        }

        private void ReadyClassRecords()
        {
            ClassRecordNames.Clear();
            for (int i = 0; i < _records.Count; i++)
                ClassRecordNames.Add($"[{i:D3}] {(i < _classNames.Length ? _classNames[i] : "")}");
            StaticNameChoices.Clear();
            for (int i = 0; i < _names.Trainers.Length; i++) StaticNameChoices.Add($"[{i}] {_names.Trainers[i]}");
            BuildSlots();
            ReadyTimings();
            StartUndo();
            SelectedRecordIndex = StartClass >= 0 && StartClass < _records.Count ? StartClass : 0;
            SelectedTab = 2;
            StatusText = "";
        }

        // What each group draws, from the sets the patch's builder copies out of the stock intros: group 2 is the
        // portrait (the large Poké Ball for ordinary trainers), group 3 the VS mark, and group 1 the banner, frame,
        // backdrop, background, small Poké Ball or emblem, depending on the style.
        private static readonly Kind[] FourParts = { Kind.Palette, Kind.TileGraphic, Kind.CellLayout, Kind.CellAnimation };

        private void BuildSlots()
        {
            ArtSlots.Clear();
            Add(s => s == 0 ? "Large Poké Ball" : "Portrait", new[] { Field.Group2Rlcn, Field.Group2Rgcn, Field.Group2Recn, Field.Group2Rnan },
                FourParts, s => TrainerClassMetadataSchema.IsAssetConsumed(s, Field.Group2Rlcn));
            Add(s => s == 4 ? "Background" : "Banner", new[] { Field.Group1Rlcn, Field.Group1Rgcn, Field.Group1Rcsn1 },
                new[] { Kind.Palette, Kind.TileGraphic, Kind.TileMap }, s => s == 1 || s == 4 || s == 13);
            Add(_ => "Backdrop", new[] { Field.Group1Rlcn, Field.Group1Rgcn, Field.Group1Rcsn1, Field.Group1Rcsn2, Field.Group1Rcsn3 },
                new[] { Kind.Palette, Kind.TileGraphic, Kind.TileMap, Kind.TileMap, Kind.TileMap }, s => s == 3);
            Add(s => s == 2 ? "Frame" : s is 5 or 6 ? "Emblem" : "Small Poké Ball",
                new[] { Field.Group1Rlcn, Field.Group1Rgcn, Field.Group1Recn, Field.Group1Rnan }, FourParts,
                s => TrainerClassMetadataSchema.IsAssetConsumed(s, Field.Group1Recn));
            Add(_ => "VS mark", new[] { Field.Group3Rlcn, Field.Group3Rgcn, Field.Group3Recn, Field.Group3Rnan }, FourParts,
                s => TrainerClassMetadataSchema.IsAssetConsumed(s, Field.Group3Rlcn));
            foreach (IntroArtSlot slot in ArtSlots) FillSlotChoices(slot);

            void Add(Func<ushort, string> label, Field[] fields, Kind[] roles, Func<ushort, bool> usedBy)
            {
                IntroArtSlot slot = new() { LabelFor = label, Fields = fields, Roles = roles, UsedBy = usedBy };
                slot.Picked = OnSlotPicked;
                for (int i = 0; i < fields.Length; i++)
                {
                    int pal = Array.IndexOf(roles, Kind.Palette);
                    IntroArtFile part = new()
                    {
                        Label = PartName(roles[i], roles.Take(i).Count(k => k == roles[i]), roles.Count(k => k == roles[i])), Field = fields[i],
                        // Timing has no picture to show; the Animation button opens it where it is edited.
                        CanShow = roles[i] != Kind.CellAnimation,
                        PaletteField = pal >= 0 ? fields[pal] : null,
                    };
                    part.Changed = OnPartChanged;
                    slot.Parts.Add(part);
                }
                ArtSlots.Add(slot);
            }
        }

        private static string PartName(Kind kind, int nth, int count)
        {
            string name = kind switch
            {
                Kind.Palette => "Palette",
                Kind.TileGraphic => "Tiles",
                Kind.CellLayout => "Cells",
                Kind.CellAnimation => "Animation",
                _ => "Screen",
            };
            return count > 1 ? $"{name} {nth + 1}" : name;
        }

        private void OnPartChanged(IntroArtFile part, int value)
        {
            if (_suppress || Record == null || value < 0 || value > ushort.MaxValue || Record.GetAsset(part.Field) == value) return;
            Record.SetAsset(part.Field, (ushort)value);
            RecordEdited();
        }

        private int[] SetOf(TrainerClassMetadataRecord record, IntroArtSlot slot) =>
            slot.Fields.Select(f => (int)record.GetAsset(f)).ToArray();

        // Every run of files laid out like a set some class already uses, plus each class's own set, named after
        // the classes that show it.
        private void FillSlotChoices(IntroArtSlot slot)
        {
            List<int[]> used = _records.Where(r => slot.UsedBy(r.VsStyle)).Select(r => SetOf(r, slot)).ToList();
            FindSets(slot.Sets, used, slot.Roles);
            slot.Choices.Clear();
            foreach (int[] set in slot.Sets)
            {
                List<string> who = Enumerable.Range(0, _records.Count)
                    .Where(i => slot.UsedBy(_records[i].VsStyle) && SetOf(_records[i], slot).SequenceEqual(set))
                    .Select(ShownName).Distinct().Take(3).ToList();
                slot.Choices.Add((who.Count == 0 ? "Unused" : string.Join(", ", who)) + $" ({set[0]})");
            }
        }

        // A class is known by the name its intro shows where it has one (several are all "Leader").
        private string ShownName(int classId)
        {
            TrainerClassMetadataRecord r = _records[classId];
            if (r.VsStyle == 1 && r.UseSavedRivalName == 1) return "Rival";
            if (TrainerClassMetadataSchema.UsesStaticName(r) && r.TrainerNameId < _names.Trainers.Length) return _names.Trainers[r.TrainerNameId];
            return classId < _classNames.Length ? _classNames[classId] : classId.ToString();
        }

        // ── The selected class ───────────────────────────────────────────────────────────────────

        private int _recordIndex = -1;
        private TrainerClassMetadataRecord Record => _recordIndex >= 0 && _recordIndex < _records.Count ? _records[_recordIndex] : null;

        public int SelectedRecordIndex
        {
            get => _recordIndex;
            set
            {
                if (value == _recordIndex || value < 0 || value >= _records.Count) return;
                StopAnimation();
                _recordIndex = value;
                Raise(nameof(SelectedRecordIndex));
                RaiseRecord();
            }
        }

        public bool HasRecord => Record != null;

        /// <summary>For style 0, the terrain and level the preview plays.</summary>
        public ObservableCollection<string> TerrainChoices { get; } = new(VsIntroPreview.BallVariantNames);
        private int _terrain;
        public int TerrainIndex
        {
            get => _terrain;
            set { if (value < 0 || value == _terrain) return; _terrain = value; Raise(nameof(TerrainIndex)); StopAnimation(); RenderPreview(); }
        }
        public bool ShowTerrain => Record?.VsStyle == 0;

        /// <summary>The still art previews have nothing to play.</summary>
        public bool CanAnimate => !IsClassRecords || ClassScene()?.Kind != VsIntroPreview.Layout.Pieces;

        /// <summary>Shows one class's intro, for the editor opened again from the Trainer Classes window.</summary>
        public void ShowClassRecord(int trainerClass)
        {
            if (!IsClassRecords) return;
            SelectedTab = 2;
            SelectedRecordIndex = trainerClass;
        }

        public int StyleIndex
        {
            get => Record?.VsStyle ?? -1;
            set
            {
                if (_suppress || Record == null || value < 0 || value > TrainerClassMetadataSchema.MaximumStyle || value == Record.VsStyle) return;
                Record.VsStyle = (ushort)value;
                RecordEdited();
            }
        }

        public bool ShowSavedRival => Record?.VsStyle == 1;
        public bool SavedRivalName
        {
            get => Record?.UseSavedRivalName == 1;
            set
            {
                if (_suppress || Record == null || SavedRivalName == value) return;
                Record.UseSavedRivalName = (byte)(value ? 1 : 0);
                RecordEdited();
            }
        }

        public bool ShowStaticName => TrainerClassMetadataSchema.UsesStaticName(Record);
        public int StaticNameIndex
        {
            get => Record?.TrainerNameId ?? -1;
            set
            {
                if (_suppress || Record == null || value < 0 || value >= StaticNameChoices.Count || value == Record.TrainerNameId) return;
                Record.TrainerNameId = (ushort)value;
                RecordEdited();
            }
        }

        // Style 1's motion is where the portrait stops sliding in, in 1/4096ths of a pixel like the stock gym records.
        public bool ShowMotion => Record?.VsStyle == 1;
        public decimal Motion
        {
            get => (Record?.Style1Motion ?? 0) >> 12;
            set
            {
                if (_suppress || Record == null || value < 0 || value > 255 || (uint)value == Record.Style1Motion >> 12) return;
                Record.Style1Motion = (uint)value << 12;
                RecordEdited();
            }
        }

        // Style 2's timing is the stock League records' clash shake, in frames.
        public bool ShowTiming => Record?.VsStyle == 2;
        public decimal Timing
        {
            get => Record?.Style2Timing ?? 0;
            set
            {
                if (_suppress || Record == null || value < 0 || value > byte.MaxValue || (ushort)value == Record.Style2Timing) return;
                Record.Style2Timing = (ushort)value;
                RecordEdited();
            }
        }

        private void OnSlotPicked(IntroArtSlot slot, int index)
        {
            if (_suppress || Record == null || index >= slot.Sets.Count) return;
            int[] set = slot.Sets[index];
            if (SetOf(Record, slot).SequenceEqual(set)) return;
            for (int i = 0; i < slot.Fields.Length; i++) Record.SetAsset(slot.Fields[i], (ushort)set[i]);
            RecordEdited();
        }

        private void RaiseRecord()
        {
            _suppress = true;
            try
            {
                foreach (IntroArtSlot slot in ArtSlots)
                {
                    slot.Visible = Record != null && slot.UsedBy(Record.VsStyle);
                    if (Record == null) continue;
                    slot.Label = slot.LabelFor(Record.VsStyle);
                    slot.Raise(nameof(IntroArtSlot.Label));
                    foreach (IntroArtFile part in slot.Parts) part.Show(Record.GetAsset(part.Field));
                    int[] now = SetOf(Record, slot);
                    int at = slot.Sets.FindIndex(s => s.SequenceEqual(now));
                    if (at < 0 && slot.Visible)
                    {
                        slot.Sets.Add(now);
                        slot.Choices.Add($"This class ({now[0]})");
                        at = slot.Sets.Count - 1;
                    }
                    slot.Show(at);
                }
                Raise(nameof(HasRecord), nameof(StyleIndex), nameof(ShowSavedRival), nameof(SavedRivalName),
                      nameof(ShowStaticName), nameof(StaticNameIndex), nameof(ShowMotion), nameof(Motion),
                      nameof(ShowTiming), nameof(Timing), nameof(ShowPlayerChoice), nameof(CanAnimate), nameof(ShowTerrain));
                ShowTimings();
            }
            finally { _suppress = false; }
            RenderPreview();
        }

        private void RecordEdited()
        {
            _undo?.Record();
            RaiseRecord();
            Raise(nameof(HasUnsavedChanges));
        }

        // ── Preview ──────────────────────────────────────────────────────────────────────────────

        private VsIntroPreview.Scene ClassScene()
        {
            VsIntroPreview.Scene scene = ClassSceneCore();
            if (scene != null && _timing != null)
                scene.Timings = Enumerable.Range(0, VsIntroTimingAddon.FieldsPerClass).Select(f => TimingOf((VsIntroTimingAddon.Field)f)).ToArray();
            return scene;
        }

        private VsIntroPreview.Scene ClassSceneCore()
        {
            TrainerClassMetadataRecord r = Record;
            if (r == null || _preview == null || _sites == null) return null;
            int[] Get(params Field[] fields) => fields.Select(f => (int)r.GetAsset(f)).ToArray();
            int[] portrait = Get(Field.Group2Rlcn, Field.Group2Rgcn, Field.Group2Recn, Field.Group2Rnan);
            int[] mark = Get(Field.Group3Rlcn, Field.Group3Rgcn, Field.Group3Recn, Field.Group3Rnan);
            string name = r.VsStyle == 1 && r.UseSavedRivalName == 1 ? "Rival"
                : TrainerClassMetadataSchema.UsesStaticName(r) && r.TrainerNameId < _names.Trainers.Length ? _names.Trainers[r.TrainerNameId] : "";
            int[] Four(int first) => first < 0 ? null : Order(first, 4);
            switch (r.VsStyle)
            {
                case 1:
                case 13:
                    return new VsIntroPreview.Scene
                    {
                        Kind = r.VsStyle == 1 ? VsIntroPreview.Layout.Gym : VsIntroPreview.Layout.Frontier,
                        Face = portrait, Vs = mark, NamePalette = _sites.NamePalette, Name = name,
                        Banner = Get(Field.Group1Rlcn, Field.Group1Rgcn, Field.Group1Rcsn1),
                        EndX = r.VsStyle == 1 ? (int)(r.Style1Motion >> 12) : 214,
                    };
                case 2:
                    // Group 1 is the League frame: its palette, then the tiles and cells the frame draws with.
                    return new VsIntroPreview.Scene
                    {
                        Kind = VsIntroPreview.Layout.League, Face = portrait, Vs = mark,
                        PlayerFace = Four(PlayerGirl ? _sites.PlayerFaceGirl : _sites.PlayerFaceBoy),
                        Frame = Get(Field.Group1Rgcn, Field.Group1Recn), FramePalette = r.GetAsset(Field.Group1Rlcn),
                        ClashFrames = r.Style2Timing, NamePalette = _sites.NamePalette, Name = name,
                    };
                case 3:
                    return new VsIntroPreview.Scene
                    {
                        Kind = VsIntroPreview.Layout.Executive, Face = portrait, NamePalette = _sites.NamePalette, Name = name,
                        Backdrop = Get(Field.Group1Rlcn, Field.Group1Rgcn, Field.Group1Rcsn1, Field.Group1Rcsn2, Field.Group1Rcsn3),
                    };
                case 4:
                    return new VsIntroPreview.Scene
                    {
                        Kind = VsIntroPreview.Layout.Special, Special = VsIntroPreview.SpecialVariant.Shoji,
                        Screen = Get(Field.Group1Rlcn, Field.Group1Rgcn, Field.Group1Rcsn1),
                    };
                case 5:
                case 6:
                    return new VsIntroPreview.Scene
                    {
                        Kind = VsIntroPreview.Layout.Special,
                        Special = r.VsStyle == 5 ? VsIntroPreview.SpecialVariant.Red : VsIntroPreview.SpecialVariant.Rocket,
                        Emblem = Get(Field.Group1Rlcn, Field.Group1Rgcn, Field.Group1Recn, Field.Group1Rnan),
                        Flights = _motion?.Flights,
                    };
                case 0:
                case >= 7 and <= 12:
                    // Style 0 picks its variant from the terrain and the trainer's level; 7 to 12 fix one.
                    return new VsIntroPreview.Scene
                    {
                        Kind = VsIntroPreview.Layout.Balls,
                        Variant = (VsIntroPreview.BallVariant)(r.VsStyle == 0 ? Math.Max(0, _terrain) : r.VsStyle - 7),
                        Large = portrait, Small = Get(Field.Group1Rlcn, Field.Group1Rgcn, Field.Group1Recn, Field.Group1Rnan),
                        BlockOrder = _motion?.BlockOrder,
                    };
                default:
                    // The terrain, Red and Rocket intros move their art over the field; the preview shows the art itself.
                    List<int[]> pieces = new();
                    if (TrainerClassMetadataSchema.IsAssetConsumed(r.VsStyle, Field.Group1Recn))
                        pieces.Add(Get(Field.Group1Rlcn, Field.Group1Rgcn, Field.Group1Recn, Field.Group1Rnan));
                    if (TrainerClassMetadataSchema.IsAssetConsumed(r.VsStyle, Field.Group2Rlcn)) pieces.Add(portrait);
                    return pieces.Count == 0 ? null : new VsIntroPreview.Scene { Kind = VsIntroPreview.Layout.Pieces, Pieces = pieces };
            }
        }

        // ── Opening the art where it's edited ────────────────────────────────────────────────────

        public void PaintSlot(IntroArtSlot slot)
        {
            if (Record == null || slot == null) return;
            int at = Array.IndexOf(slot.Roles, Kind.TileGraphic);
            int pal = Array.IndexOf(slot.Roles, Kind.Palette);
            if (at >= 0) Paint(Record.GetAsset(slot.Fields[at]), pal >= 0 ? Record.GetAsset(slot.Fields[pal]) : -1);
        }

        public void AnimateSlot(IntroArtSlot slot)
        {
            if (Record == null || slot == null || !slot.CanAnimate) return;
            int[] m = SetOf(Record, slot);
            OpenCells(m, -1, $"{ClassRecordNames[_recordIndex]}: {slot.Label}");
        }

        /// <summary>Shows one file in the graphics browser, which edits every kind.</summary>
        public void ShowPart(IntroArtFile part)
        {
            if (Record == null || part == null || !part.CanShow) return;
            int palette = part.PaletteField is Field f ? Record.GetAsset(f) : -1;
            AvaloniaEditorLauncher.OpenGraphicAt(Archive, Record.GetAsset(part.Field), true, palette);
        }

        // ── Undo and saving ──────────────────────────────────────────────────────────────────────

        private byte[] RecordsSnapshot() => _records.SelectMany(r => r.ToByteArray()).ToArray();

        private void RestoreRecords(byte[] all)
        {
            int size = TrainerClassMetadataStore.RecordLength;
            for (int i = 0; i < _records.Count && (i + 1) * size <= all.Length; i++)
                if (TrainerClassMetadataRecord.TryParse(all.Skip(i * size).Take(size).ToArray(), out TrainerClassMetadataRecord r, out _))
                    _records[i] = r;
            RaiseRecord();
            Raise(nameof(HasUnsavedChanges));
        }

        private bool RecordsChanged => Enumerable.Range(0, _records.Count).Any(i => !_records[i].ToByteArray().SequenceEqual(_savedRecords[i]));

        private async Task<bool> SaveRecordsAsync()
        {
            List<string> failures = new();
            for (int i = 0; i < _records.Count; i++)
            {
                if (_records[i].ToByteArray().SequenceEqual(_savedRecords[i])) continue;
                TrainerClassMetadataValidationResult check = TrainerClassMetadataStore.ValidatePresentation(_records[i], _names.Trainers.Length);
                if (!check.IsValid) { failures.Add($"Class {i}: " + string.Join(" ", check.Errors)); continue; }
                if (!TrainerClassMetadataStore.TryWritePresentationFields(i, _records[i], out string error)) { failures.Add($"Class {i}: {error}"); continue; }
                _savedRecords[i] = _records[i].ToByteArray();
            }
            Raise(nameof(HasUnsavedChanges));
            if (failures.Count > 0)
            {
                await DialogHelper.ShowError("Some class intros were not saved:\n" + string.Join("\n", failures), Title);
                return false;
            }
            SaveNotice.Saved(UnsavedChangesDescription);
            AppEvents.RaiseClassIntrosSaved();
            StatusText = "Saved. Save the ROM to keep the changes.";
            return true;
        }

        private void DiscardRecords()
        {
            for (int i = 0; i < _records.Count; i++)
                if (TrainerClassMetadataRecord.TryParse(_savedRecords[i], out TrainerClassMetadataRecord r, out _)) _records[i] = r;
            StartUndo();
            RaiseRecord();
            Raise(nameof(HasUnsavedChanges), nameof(CanUndo), nameof(CanRedo));
        }
    }
}
