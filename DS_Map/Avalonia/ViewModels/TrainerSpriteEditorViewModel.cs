using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using DSPRE.Editors;
using DSPRE.HgEngine;
using Ekona.Images;
using Images;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels
{
    public enum SpriteEditTool { Pencil, Eyedropper }

    /// <summary>One swatch in the palette strip, a fixed existing palette color, not editable.</summary>
    public class PaletteSwatchViewModel
    {
        public int Index { get; }
        public IBrush Brush { get; }
        public PaletteSwatchViewModel(int index, System.Drawing.Color color)
        {
            Index = index;
            Brush = new SolidColorBrush(global::Avalonia.Media.Color.FromRgb(color.R, color.G, color.B));
        }
    }

    /// <summary>One clickable frame thumbnail in the strip.</summary>
    public class FrameThumbnailViewModel
    {
        public int Index { get; }
        public Bitmap Image { get; }
        public FrameThumbnailViewModel(int index, Bitmap image) { Index = index; Image = image; }
    }

    /// <summary>One selectable "pose" in a frame's pose picker: an NCER cell, labeled and thumbnailed the
    /// same way the Sprite tab's own frame strip is.</summary>
    public class AnimCellChoiceViewModel
    {
        public int Index { get; }
        public string Label { get; }
        public Bitmap Thumbnail { get; }
        public AnimCellChoiceViewModel(int index, string label, Bitmap thumbnail) { Index = index; Label = label; Thumbnail = thumbnail; }
    }

    /// <summary>One row in the frame list: a played pose + how long it holds. Edits write straight into
    /// the underlying <see cref="AnimFrameDataJson"/> and notify the owner so it can re-serialize
    /// AnimJsonText and refresh the thumbnail.</summary>
    public class AnimFrameRowViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private readonly AnimFrameDataJson _model;
        private readonly Action _onChanged;
        private readonly Func<int, Bitmap> _renderThumbnail;

        public AnimFrameRowViewModel(AnimFrameDataJson model, Action onChanged, Func<int, Bitmap> renderThumbnail)
        {
            _model = model;
            _onChanged = onChanged;
            _renderThumbnail = renderThumbnail;
            _thumbnail = renderThumbnail(model.CellIndex);
        }

        public AnimFrameDataJson Model => _model;

        /// <summary>60fps game ticks, hg-engine's own unit; DelayMs is the friendlier readout.</summary>
        public int Delay
        {
            get => _model.FrameDelay;
            set
            {
                int clamped = Math.Max(1, value);
                if (_model.FrameDelay == clamped) return;
                _model.FrameDelay = clamped;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DelayMs));
                _onChanged();
            }
        }

        public double DelayMs => Math.Round(Delay * 1000.0 / 60.0);

        public int CellIndex
        {
            get => _model.CellIndex;
            set
            {
                if (_model.CellIndex == value) return;
                _model.CellIndex = value;
                OnPropertyChanged();
                Thumbnail = _renderThumbnail(value);
                _onChanged();
            }
        }

        private Bitmap _thumbnail;
        public Bitmap Thumbnail { get => _thumbnail; private set { _thumbnail = value; OnPropertyChanged(); } }
    }

    /// <summary>One entry in the sequence picker (hg-engine calls a sequence an "animation": idle pose,
    /// walk cycle, battle dance, etc). A class usually has 1-2.</summary>
    public class AnimSequenceChoiceViewModel
    {
        public AnimSequenceJson Model { get; }
        public int Index { get; }
        public string DisplayName { get; }
        public AnimSequenceChoiceViewModel(AnimSequenceJson model, int index)
        {
            Model = model;
            Index = index;
            int n = model.FrameData.Count;
            DisplayName = $"Sequence {index} ({n} frame{(n == 1 ? "" : "s")})";
        }
    }

    /// <summary>
    /// Pixel-level editor for a trainer class's sprite.
    ///
    /// Plat/HGSS trainer classes composite per-frame OAM cells (NCER) from a shared NCGR tile sheet;
    /// the flat sheet itself is a jumbled atlas, not a coherent picture. Editing happens on the
    /// composited "as it looks" preview instead: each paint stroke is hit-tested against the current
    /// frame's OAM cells (same geometry
    /// <see cref="Ekona.Images.Actions.Get_RawImage(Bank, uint, ImageBase, PaletteBase, int, int, bool, int, int, int[])"/>
    /// uses) to find which cell, tile bytes and palette bank own that pixel, then edits just those
    /// bytes in place. Cells shared across frames mean an edit naturally propagates to every frame
    /// that reuses them; frames that don't share tiles edit independently.
    ///
    /// DP trainer classes have no NCER (no per-class animation), so editing falls back to the flat
    /// NCGR tile sheet directly (<see cref="_flatIndices"/> path).
    /// </summary>
    public class TrainerSpriteEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        {
            if (EqualityComparer<T>.Default.Equals(f, v)) return false;
            f = v; OnPropertyChanged(n); return true;
        }

        // Composited-canvas fixed logical size (OAM offsets are relative to its center), generous
        // enough to fit any trainer-class sprite without clipping, matching the size convention
        // TrainerEditorViewModel already renders class sprites at (96) with a little headroom.
        private const int CanvasSize = 128;

        private PaletteBase _pal;
        private ImageBase _tile;
        private SpriteBase _sprite; // null on DP (no NCER), flat-sheet fallback mode; also null when _jsonBanks is used
        private string _tilesPath;

        // hg-engine path: cell geometry read from *_cell.json instead of the compiled narc, same nitrogfx
        // bug as TrainerClassSpriteRenderer works around (see HgEngineTrainerGraphicsSource). Only the
        // geometry source changes; painted pixels still go into _tile/_pal as before.
        private Bank[] _jsonBanks;
        private uint _jsonBlockSize;

        private int BankCount => _jsonBanks?.Length ?? _sprite?.Banks.Length ?? 0;
        private Bank GetBank(int i) => _jsonBanks != null ? _jsonBanks[i] : _sprite.Banks[i];
        private uint BlockSize => _jsonBanks != null ? _jsonBlockSize : (_sprite?.BlockSize ?? 0);
        private DSPRE.RawImage GetCompositedRawImage(int bankIndex, int width, int height, int[] drawIndex) =>
            _jsonBanks != null
                ? Actions.Get_RawImage(_jsonBanks[bankIndex], _jsonBlockSize, _tile, _pal, width, height, true, -1, 1, drawIndex)
                : _sprite.Get_RawImage(_tile, _pal, bankIndex, width, height, trans: true, currOAM: -1, draw_index: drawIndex);

        // ── Mode A: composited cell editing (Plat/HGSS) ─────────────────────────
        private sealed class EditCell
        {
            public int Width, Height;
            public int DstX, DstY;
            public bool FlipX, FlipY;
            public int PaletteBank;
            public int ByteStart, ByteLen;
        }
        private readonly List<EditCell> _cells = new();
        private int _selectedFrameIndex = -1;
        private int _activePaletteBank = -1;

        // ── Mode B: flat tile-sheet editing (DP fallback) ───────────────────────
        private int[] _flatIndices;
        private int _flatWidth, _flatHeight;

        public bool IsFlatSheetMode => _sprite == null && _jsonBanks == null;

        public int ZoomFactor { get; private set; } = 4;

        public int FrameCount => BankCount;
        public int SelectedFrameIndex
        {
            get => _selectedFrameIndex;
            set { if (Set(ref _selectedFrameIndex, value)) LoadFrame(value); }
        }

        public ObservableCollection<FrameThumbnailViewModel> FrameThumbnails { get; } = new();
        public bool HasFrames => FrameThumbnails.Count > 0;

        public ObservableCollection<string> ClassNames { get; } = new();
        public int SelectedClassIndex
        {
            get => _trClassID;
            set { if (Set(ref _trClassID, value)) Load(value); }
        }

        public ObservableCollection<PaletteSwatchViewModel> PaletteSwatches { get; } = new();

        private int _selectedSwatchIndex;
        public int SelectedSwatchIndex { get => _selectedSwatchIndex; set => Set(ref _selectedSwatchIndex, value); }

        private SpriteEditTool _selectedTool = SpriteEditTool.Pencil;
        public SpriteEditTool SelectedTool { get => _selectedTool; set => Set(ref _selectedTool, value); }

        private Bitmap _canvasBitmap;
        public Bitmap CanvasBitmap { get => _canvasBitmap; private set => Set(ref _canvasBitmap, value); }

        private string _statusText = "";
        public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }

        private bool _dirty;
        public bool HasUnsavedChanges
        {
            get => _dirty || AnimJsonDirty;
            private set => Set(ref _dirty, value);
        }
        public string UnsavedChangesDescription => $"Trainer Class Sprite Editor (class {_trClassID})";
        public void SaveChanges()
        {
            Save();
            if (AnimJsonDirty) SaveAnimJson();
        }

        /// <summary>Edits are applied straight into the in-memory <see cref="_tile"/>/<see cref="_flatIndices"/>
        /// buffers as you paint (there's no separate undo buffer), so discarding just means throwing all of
        /// that away and re-reading the class fresh from disk.</summary>
        public void DiscardChanges() => Load(_trClassID);

        // ── Animations tab: NNN_anim.json read/written straight from the linked hg-engine checkout's
        // source (this is the raw-text half; the structured sequence/frame editor further down keeps
        // this text in sync both ways).
        private string _animJsonPath;

        private string _animJsonText = "";
        public string AnimJsonText
        {
            get => _animJsonText;
            set
            {
                if (!Set(ref _animJsonText, value)) return;
                AnimJsonDirty = true;
                if (!_syncingAnimText) TryRebuildAnimModelFromText();
            }
        }

        private bool _animJsonDirty;
        public bool AnimJsonDirty
        {
            get => _animJsonDirty;
            private set { if (Set(ref _animJsonDirty, value)) OnPropertyChanged(nameof(HasUnsavedChanges)); }
        }

        private string _animJsonStatusText = "";
        public string AnimJsonStatusText { get => _animJsonStatusText; private set => Set(ref _animJsonStatusText, value); }

        public bool CanEditAnimJson => HgEngineProject.IsActive;
        public bool HasAnimJsonFile => _animJsonPath != null && File.Exists(_animJsonPath);

        private void SetAnimJsonTextSilent(string text)
        {
            _animJsonText = text;
            OnPropertyChanged(nameof(AnimJsonText));
            TryRebuildAnimModelFromText();
        }

        private void LoadAnimJson(int trClassID)
        {
            _animJsonPath = HgEngineProject.IsActive
                ? Path.Combine(HgEngineProject.RepoPathUnc, "data", "graphics", "trainer_gfx", $"{trClassID:D3}_anim.json")
                : null;
            OnPropertyChanged(nameof(CanEditAnimJson));
            OnPropertyChanged(nameof(HasAnimJsonFile));

            if (_animJsonPath == null)
            {
                SetAnimJsonTextSilent("");
                AnimJsonStatusText = "Link an hg-engine checkout to edit this class's animation JSON.";
            }
            else if (File.Exists(_animJsonPath))
            {
                try
                {
                    SetAnimJsonTextSilent(File.ReadAllText(_animJsonPath));
                    AnimJsonStatusText = _animJsonPath;
                }
                catch (Exception ex)
                {
                    SetAnimJsonTextSilent("");
                    AnimJsonStatusText = "Failed to read: " + ex.Message;
                }
            }
            else
            {
                SetAnimJsonTextSilent("");
                AnimJsonStatusText = $"No {trClassID:D3}_anim.json yet for this class.";
            }
            AnimJsonDirty = false;
        }

        /// <summary>Returns null on success, error message on failure. Validates the text parses as JSON
        /// before writing.</summary>
        public string SaveAnimJson()
        {
            if (_animJsonPath == null) return "No hg-engine checkout linked.";
            try
            {
                using (JsonDocument.Parse(AnimJsonText)) { }
            }
            catch (JsonException ex)
            {
                return "Invalid JSON: " + ex.Message;
            }
            try
            {
                File.WriteAllText(_animJsonPath, AnimJsonText);
                AnimJsonDirty = false;
                AnimJsonStatusText = "Saved: " + _animJsonPath;
                OnPropertyChanged(nameof(HasAnimJsonFile));
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        /// <summary>Seeds a minimal, valid single-pose anim.json (matching the shape hg-engine's own
        /// single-frame classes use) for a class that doesn't have one yet.</summary>
        public string CreateAnimJson()
        {
            if (_animJsonPath == null) return "No hg-engine checkout linked.";
            const string template = """
                {
                	"labelEnabled":	true,
                	"uaatEnabled":	false,
                	"sequenceCount":	1,
                	"frameCount":	1,
                	"sequences":	[{
                			"frameCount":	1,
                			"loopStartFrame":	0,
                			"animationElement":	0,
                			"animationType":	1,
                			"playbackMode":	2,
                			"frameData":	[{
                					"frameDelay":	4,
                					"resultId":	0
                				}]
                		}],
                	"animationResults":	[{
                			"resultType":	0,
                			"index":	0
                		}],
                	"resultCount":	1,
                	"labels":	["CellAnime0"],
                	"labelCount":	1
                }
                """;
            try
            {
                File.WriteAllText(_animJsonPath, template);
                SetAnimJsonTextSilent(template);
                AnimJsonDirty = false;
                AnimJsonStatusText = "Created: " + _animJsonPath;
                OnPropertyChanged(nameof(HasAnimJsonFile));
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ── Structured animation editor: sequences/frames built from AnimJsonText, kept in sync with it
        // both ways, like the Battle Script editor's Cards/Text tabs.
        private AnimJsonRoot _animRoot;
        private bool _syncingAnimText;   // guards the structured-model <-> AnimJsonText echo loop

        public ObservableCollection<AnimSequenceChoiceViewModel> AnimSequenceChoices { get; } = new();

        private AnimSequenceChoiceViewModel _selectedAnimSequence;
        public AnimSequenceChoiceViewModel SelectedAnimSequence
        {
            get => _selectedAnimSequence;
            set
            {
                if (!Set(ref _selectedAnimSequence, value)) return;
                OnPropertyChanged(nameof(HasSelectedAnimSequence));
                StopAnimPreview();
                RebuildAnimFrameRows();
            }
        }
        public bool HasSelectedAnimSequence => SelectedAnimSequence != null;

        public ObservableCollection<AnimFrameRowViewModel> AnimFrameRows { get; } = new();
        public bool HasAnimFrameRows => AnimFrameRows.Count > 0;

        public ObservableCollection<AnimCellChoiceViewModel> AnimCellChoices { get; } = new();

        private string _animModelStatusText = "";
        public string AnimModelStatusText { get => _animModelStatusText; private set => Set(ref _animModelStatusText, value); }

        private bool _animPreviewPlaying;
        public bool AnimPreviewPlaying { get => _animPreviewPlaying; private set => Set(ref _animPreviewPlaying, value); }

        private Bitmap _animPreviewBitmap;
        public Bitmap AnimPreviewBitmap { get => _animPreviewBitmap; private set => Set(ref _animPreviewBitmap, value); }

        private int _animPreviewFrameNumber;
        public int AnimPreviewFrameNumber { get => _animPreviewFrameNumber; private set => Set(ref _animPreviewFrameNumber, value); }

        private CancellationTokenSource _animPreviewCts;

        private void RebuildAnimCellChoices()
        {
            AnimCellChoices.Clear();
            for (int i = 0; i < BankCount; i++)
            {
                string name = GetBank(i).name;
                string label = string.IsNullOrWhiteSpace(name) ? $"Cell {i}" : $"{name} ({i})";
                AnimCellChoices.Add(new AnimCellChoiceViewModel(i, label, RenderAnimCellThumbnail(i)));
            }
        }

        private Bitmap RenderAnimCellThumbnail(int cellIndex, int size = 72)
        {
            if (BankCount == 0 || cellIndex < 0 || cellIndex >= BankCount || _tile == null || _pal == null) return null;
            try
            {
                var raw = GetCompositedRawImage(cellIndex, size, size, null);
                return ImageConverter.ToAvaloniaBitmap(raw);
            }
            catch { return null; }
        }

        /// <summary>Re-parses AnimJsonText into the structured model. On a parse error, leaves whatever
        /// structure is already showing untouched (so a mid-typo keystroke in the JSON tab doesn't blank
        /// the Editor tab) and surfaces the error in <see cref="AnimModelStatusText"/> instead.</summary>
        private void TryRebuildAnimModelFromText()
        {
            if (string.IsNullOrWhiteSpace(AnimJsonText))
            {
                _animRoot = null;
                AnimSequenceChoices.Clear();
                AnimFrameRows.Clear();
                OnPropertyChanged(nameof(HasAnimFrameRows));
                AnimModelStatusText = "";
                return;
            }
            try
            {
                _animRoot = AnimJsonRoot.Parse(AnimJsonText);
                AnimModelStatusText = "";
            }
            catch (Exception ex)
            {
                AnimModelStatusText = "JSON has an error, showing the last valid structure: " + ex.Message;
                return; // keep whatever AnimSequenceChoices/AnimFrameRows already have
            }

            int keepIndex = SelectedAnimSequence?.Index ?? 0;
            AnimSequenceChoices.Clear();
            for (int i = 0; i < (_animRoot?.Sequences.Count ?? 0); i++)
                AnimSequenceChoices.Add(new AnimSequenceChoiceViewModel(_animRoot.Sequences[i], i));

            var restore = AnimSequenceChoices.FirstOrDefault(s => s.Index == keepIndex) ?? AnimSequenceChoices.FirstOrDefault();
            if (!ReferenceEquals(restore, _selectedAnimSequence))
                SelectedAnimSequence = restore;   // triggers RebuildAnimFrameRows via the setter
            else
                RebuildAnimFrameRows();            // same selection, but its frames may have changed
        }

        private void RebuildAnimFrameRows()
        {
            AnimFrameRows.Clear();
            if (SelectedAnimSequence != null)
            {
                foreach (var frame in SelectedAnimSequence.Model.FrameData)
                    AnimFrameRows.Add(new AnimFrameRowViewModel(frame, SyncAnimModelToText, i => RenderAnimCellThumbnail(i)));
            }
            OnPropertyChanged(nameof(HasAnimFrameRows));
        }

        /// <summary>Called whenever a frame/sequence edit mutates <see cref="_animRoot"/>; re-serializes
        /// it back into AnimJsonText without re-triggering a model rebuild.</summary>
        private void SyncAnimModelToText()
        {
            if (_animRoot == null) return;
            _syncingAnimText = true;
            AnimJsonText = _animRoot.Serialize();
            _syncingAnimText = false;

            // Sequence picker labels show frame counts, so rebuild them after any add/remove.
            int keepIndex = SelectedAnimSequence?.Index ?? 0;
            AnimSequenceChoices.Clear();
            for (int i = 0; i < _animRoot.Sequences.Count; i++)
                AnimSequenceChoices.Add(new AnimSequenceChoiceViewModel(_animRoot.Sequences[i], i));
            var restore = AnimSequenceChoices.FirstOrDefault(s => s.Index == keepIndex) ?? AnimSequenceChoices.FirstOrDefault();
            if (!ReferenceEquals(restore, _selectedAnimSequence)) _selectedAnimSequence = restore;
            OnPropertyChanged(nameof(SelectedAnimSequence));
            OnPropertyChanged(nameof(HasSelectedAnimSequence));
        }

        public void AddAnimFrame()
        {
            if (SelectedAnimSequence == null) return;
            StopAnimPreview();
            int copyFrom = AnimFrameRows.Count > 0 ? SelectedAnimSequence.Model.FrameData[^1].CellIndex : 0;
            SelectedAnimSequence.Model.FrameData.Add(new AnimFrameDataJson { FrameDelay = 4, CellIndex = copyFrom });
            RebuildAnimFrameRows();
            SyncAnimModelToText();
        }

        /// <summary>Returns an error message if the removal was refused (every sequence needs at least
        /// one frame), null on success.</summary>
        public string RemoveAnimFrame(AnimFrameRowViewModel row)
        {
            if (SelectedAnimSequence == null || row == null) return null;
            if (SelectedAnimSequence.Model.FrameData.Count <= 1)
                return "A sequence needs at least one frame. Remove the whole sequence instead if you don't want it.";
            StopAnimPreview();
            SelectedAnimSequence.Model.FrameData.Remove(row.Model);
            RebuildAnimFrameRows();
            SyncAnimModelToText();
            return null;
        }

        public void MoveAnimFrame(AnimFrameRowViewModel row, int direction)
        {
            if (SelectedAnimSequence == null || row == null) return;
            var list = SelectedAnimSequence.Model.FrameData;
            int i = list.IndexOf(row.Model);
            int j = i + direction;
            if (i < 0 || j < 0 || j >= list.Count) return;
            StopAnimPreview();
            (list[i], list[j]) = (list[j], list[i]);
            RebuildAnimFrameRows();
            SyncAnimModelToText();
        }

        public void AddAnimSequence()
        {
            if (_animRoot == null) return;
            StopAnimPreview();
            var seq = new AnimSequenceJson
            {
                AnimationType = 1,
                PlaybackMode = 2,
                FrameData = { new AnimFrameDataJson { FrameDelay = 4, CellIndex = 0 } },
            };
            _animRoot.Sequences.Add(seq);
            SyncAnimModelToText();
            SelectedAnimSequence = AnimSequenceChoices.LastOrDefault();
        }

        /// <summary>Returns an error message if the removal was refused (the file needs at least one
        /// sequence), null on success.</summary>
        public string RemoveAnimSequence()
        {
            if (_animRoot == null || SelectedAnimSequence == null) return null;
            if (_animRoot.Sequences.Count <= 1)
                return "This file needs at least one sequence.";
            StopAnimPreview();
            _animRoot.Sequences.Remove(SelectedAnimSequence.Model);
            SyncAnimModelToText();
            return null;
        }

        public void StopAnimPreview()
        {
            _animPreviewCts?.Cancel();
            _animPreviewCts = null;
            AnimPreviewPlaying = false;
        }

        /// <summary>Plays the selected sequence's frames once, in real per-frame timing, and stops on the
        /// last frame. Deliberately not a loop.</summary>
        public async Task PlayAnimPreviewOnceAsync()
        {
            if (SelectedAnimSequence == null || AnimFrameRows.Count == 0) return;
            StopAnimPreview();
            var cts = new CancellationTokenSource();
            _animPreviewCts = cts;
            AnimPreviewPlaying = true;
            try
            {
                for (int i = 0; i < AnimFrameRows.Count; i++)
                {
                    var row = AnimFrameRows[i];
                    AnimPreviewBitmap = row.Thumbnail;
                    AnimPreviewFrameNumber = i + 1;
                    int ms = Math.Max(16, (int)(row.Delay * 1000.0 / 60.0));
                    await Task.Delay(ms, cts.Token);
                }
            }
            catch (OperationCanceledException) { /* user stopped or switched away, not an error */ }
            finally
            {
                if (ReferenceEquals(_animPreviewCts, cts)) { _animPreviewCts = null; AnimPreviewPlaying = false; }
            }
        }

        private int _trClassID;
        public bool Loaded => _tile != null;

        // ── Design-time constructor ────────────────────────────────────────────
        public TrainerSpriteEditorViewModel()
        {
            if (!Design.IsDesignMode) return;
            StatusText = "Design preview";
        }

        public TrainerSpriteEditorViewModel(int trClassID)
        {
            string[] names = GetTrainerClassNames();
            for (int i = 0; i < names.Length; i++) ClassNames.Add($"[{i:D3}] {names[i]}");
            Load(trClassID);
        }

        // ── Load ───────────────────────────────────────────────────────────────
        /// Returns null on success, error message on failure.
        public string Load(int trClassID)
        {
            _trClassID = trClassID;
            try
            {
                string dir = RomInfo.gameDirs[DirNames.trainerGraphics].unpackedDir;

                int paletteFileID = trClassID * 5 + 1;
                string paletteFilename = paletteFileID.ToString("D4");
                _pal = new NCLR(Path.Combine(dir, paletteFilename), paletteFileID, paletteFilename);

                int tilesFileID = trClassID * 5;
                string tilesFilename = tilesFileID.ToString("D4");
                _tilesPath = Path.Combine(dir, tilesFilename);
                _tile = new NCGR(_tilesPath, tilesFileID, tilesFilename);

                _sprite = null; _jsonBanks = null;
                if (RomInfo.gameFamily != GameFamilies.DP)
                {
                    if (HgEngineProject.IsActive)
                    {
                        string trainerGfxDir = Path.Combine(HgEngineProject.RepoPathUnc, "data", "graphics", "trainer_gfx");
                        string cellPath = Path.Combine(trainerGfxDir, $"{trClassID:D3}_cell.json");
                        if (File.Exists(cellPath))
                        {
                            if (HgEngineTrainerGraphicsSource.TryReadCellBanks(cellPath, out var banks, out var blockSize, out string cellError))
                            {
                                _jsonBanks = banks;
                                _jsonBlockSize = blockSize;
                            }
                            else
                            {
                                AppLogger.Error("TrainerSpriteEditorViewModel: " + cellError);
                            }
                        }
                    }

                    if (_jsonBanks == null)
                    {
                        int spriteFileID = trClassID * 5 + 2;
                        string spriteFilename = spriteFileID.ToString("D4");
                        _sprite = new NCER(Path.Combine(dir, spriteFilename), spriteFileID, spriteFilename);
                    }
                }

                if (BankCount > 0)
                {
                    ZoomFactor = 4;
                    BuildFrameThumbnails();
                    _activePaletteBank = -1;
                    // Force the property setter below to detect a change (and so actually rebuild
                    // cells/canvas/swatches) even on a reload where the frame index doesn't move,
                    // e.g. a discard while already on frame 0.
                    _selectedFrameIndex = -1;
                    SelectedFrameIndex = 0; // triggers LoadFrame -> cells + canvas + swatches
                    StatusText = $"Class {trClassID}: {FrameCount} frame(s), {_tile.BPP}bpp";
                }
                else
                {
                    // DP (or an NCER with no banks): flat tile-sheet fallback.
                    _sprite = null;
                    ZoomFactor = 12;
                    FrameThumbnails.Clear();
                    OnPropertyChanged(nameof(HasFrames));
                    LoadFlatSheet();
                    StatusText = $"Class {trClassID}: {_flatWidth}×{_flatHeight} tile sheet (no per-class animation on this game), {_tile.BPP}bpp";
                }

                OnPropertyChanged(nameof(IsFlatSheetMode));
                OnPropertyChanged(nameof(FrameCount));
                HasUnsavedChanges = false;
                StopAnimPreview();
                RebuildAnimCellChoices();
                LoadAnimJson(trClassID);
                return null;
            }
            catch (Exception ex)
            {
                _tile = null; _pal = null; _sprite = null;
                StatusText = "Load failed: " + ex.Message;
                AppLogger.Error("TrainerSpriteEditorViewModel.Load failed: " + ex.Message);
                return ex.Message;
            }
        }

        private void BuildFrameThumbnails()
        {
            FrameThumbnails.Clear();
            for (int i = 0; i < BankCount; i++)
            {
                var raw = GetCompositedRawImage(i, 64, 64, null);
                var bmp = ImageConverter.ToAvaloniaBitmap(raw);
                if (bmp != null) FrameThumbnails.Add(new FrameThumbnailViewModel(i, bmp));
            }
            OnPropertyChanged(nameof(HasFrames));
        }

        // ── Mode A: per-frame cell geometry + composited canvas ────────────────
        private void LoadFrame(int frameIndex)
        {
            if (BankCount == 0 || frameIndex < 0 || frameIndex >= BankCount) return;

            _cells.Clear();
            var bank = GetBank(frameIndex);
            int bpp = _tile.BPP;
            foreach (var oam in bank.oams)
            {
                if (oam.width == 0 || oam.height == 0) continue;

                uint tileOffset = oam.obj2.tileOffset;
                tileOffset <<= (byte)BlockSize;
                int byteStart = (int)(tileOffset * 0x20) + (int)bank.data_offset;
                int byteLen = oam.width * oam.height * bpp / 8;
                if (byteStart < 0 || byteLen <= 0 || byteStart + byteLen > _tile.Tiles.Length)
                    continue; // malformed/out-of-range cell, skip rather than risk corrupting unrelated bytes

                int bank_ = oam.obj2.index_palette;
                if (bank_ >= _pal.Palette.Length) bank_ = 0; // matches Actions.Get_RawImage(Bank...)'s own clamp

                _cells.Add(new EditCell
                {
                    Width = oam.width,
                    Height = oam.height,
                    DstX = CanvasSize / 2 + (int)oam.obj1.xOffset,
                    DstY = CanvasSize / 2 + (int)oam.obj0.yOffset,
                    FlipX = oam.obj1.flipX == 1,
                    FlipY = oam.obj1.flipY == 1,
                    PaletteBank = bank_,
                    ByteStart = byteStart,
                    ByteLen = byteLen,
                });
            }

            RebuildCompositedCanvas();

            // Default the palette strip to the first cell's bank so it's never empty, even before
            // the user has hovered/clicked anywhere.
            int firstBank = _cells.Count > 0 ? _cells[0].PaletteBank : 0;
            if (firstBank != _activePaletteBank)
                BuildPaletteSwatches(firstBank);
        }

        private void RebuildCompositedCanvas()
        {
            if (BankCount == 0 || _tile == null || _pal == null) return;
            var raw = GetCompositedRawImage(_selectedFrameIndex, CanvasSize, CanvasSize, null);
            CanvasBitmap = ImageConverter.ToAvaloniaBitmap(ZoomRaw(raw, ZoomFactor));
        }

        private static DSPRE.RawImage ZoomRaw(DSPRE.RawImage src, int zoom)
        {
            if (zoom <= 1) return src;
            var dst = new DSPRE.RawImage(src.Width * zoom, src.Height * zoom);
            for (int y = 0; y < src.Height; y++)
            {
                for (int x = 0; x < src.Width; x++)
                {
                    int si = (y * src.Width + x) * 4;
                    byte b = src.Bgra[si], g = src.Bgra[si + 1], r = src.Bgra[si + 2], a = src.Bgra[si + 3];
                    for (int dy = 0; dy < zoom; dy++)
                    {
                        int drow = (y * zoom + dy) * dst.Width;
                        for (int dx = 0; dx < zoom; dx++)
                        {
                            int di = (drow + x * zoom + dx) * 4;
                            dst.Bgra[di] = b; dst.Bgra[di + 1] = g; dst.Bgra[di + 2] = r; dst.Bgra[di + 3] = a;
                        }
                    }
                }
            }
            return dst;
        }

        /// Finds which cell owns composited-canvas pixel (x,y). Topmost drawn (last in draw order)
        /// non-transparent hit wins, matching what's visually on top; falls back to any cell whose
        /// bounds contain the point (even if transparent there) so painting into empty regions works.
        private EditCell HitTest(int x, int y)
        {
            for (int i = _cells.Count - 1; i >= 0; i--)
            {
                var c = _cells[i];
                if (x < c.DstX || x >= c.DstX + c.Width || y < c.DstY || y >= c.DstY + c.Height) continue;
                CellLocal(c, x, y, out int lx, out int ly);
                if (ReadCellIndex(c, lx, ly) != 0) return c;
            }
            for (int i = _cells.Count - 1; i >= 0; i--)
            {
                var c = _cells[i];
                if (x >= c.DstX && x < c.DstX + c.Width && y >= c.DstY && y < c.DstY + c.Height) return c;
            }
            return null;
        }

        private static void CellLocal(EditCell c, int x, int y, out int lx, out int ly)
        {
            int rawX = x - c.DstX, rawY = y - c.DstY;
            lx = c.FlipX ? c.Width - 1 - rawX : rawX;
            ly = c.FlipY ? c.Height - 1 - rawY : rawY;
        }

        private int[] DecodeCell(EditCell c)
        {
            byte[] slice = new byte[c.ByteLen];
            Array.Copy(_tile.Tiles, c.ByteStart, slice, 0, c.ByteLen);
            byte[] raster = _tile.FormTile == TileForm.Horizontal
                ? Actions.LinealToHorizontal(slice, c.Width, c.Height, _tile.BPP, _tile.TileSize)
                : slice;
            return UnpackIndices(raster, c.Width, c.Height, _tile.BPP);
        }

        private void EncodeCell(EditCell c, int[] indices)
        {
            byte[] raster = PackIndices(indices, c.Width, c.Height, _tile.BPP);
            byte[] native = _tile.FormTile == TileForm.Horizontal
                ? Actions.HorizontalToLineal(raster, c.Width, c.Height, _tile.BPP, _tile.TileSize)
                : raster;
            Array.Copy(native, 0, _tile.Tiles, c.ByteStart, c.ByteLen);
        }

        private int ReadCellIndex(EditCell c, int lx, int ly)
        {
            if (lx < 0 || lx >= c.Width || ly < 0 || ly >= c.Height) return 0;
            return DecodeCell(c)[ly * c.Width + lx];
        }

        private void BuildPaletteSwatches(int bankIndex)
        {
            _activePaletteBank = bankIndex;
            PaletteSwatches.Clear();
            var pal = _pal.Palette[bankIndex];
            int keep = SelectedSwatchIndex;
            for (int i = 0; i < pal.Length; i++)
                PaletteSwatches.Add(new PaletteSwatchViewModel(i, pal[i]));
            SelectedSwatchIndex = keep >= 0 && keep < pal.Length ? keep : 0;
        }

        // ── Mode B: flat tile-sheet fallback (DP, no NCER) ─────────────────────
        private void LoadFlatSheet()
        {
            _flatWidth = _tile.Width;
            _flatHeight = _tile.Height;

            byte[] rasterBytes = _tile.FormTile == TileForm.Horizontal
                ? Actions.LinealToHorizontal(_tile.Tiles, _flatWidth, _flatHeight, _tile.BPP, _tile.TileSize)
                : _tile.Tiles;
            _flatIndices = UnpackIndices(rasterBytes, _flatWidth, _flatHeight, _tile.BPP);

            BuildPaletteSwatches(0);
            RebuildFlatCanvas();
        }

        private void RebuildFlatCanvas()
        {
            var raw = new DSPRE.RawImage(_flatWidth, _flatHeight);
            var pal = _pal.Palette[0];
            for (int y = 0; y < _flatHeight; y++)
                for (int x = 0; x < _flatWidth; x++)
                {
                    var c = ColorAt(pal, _flatIndices[y * _flatWidth + x]);
                    raw.SetPixel(x, y, c.R, c.G, c.B, 255);
                }
            CanvasBitmap = ImageConverter.ToAvaloniaBitmap(ZoomRaw(raw, ZoomFactor));
        }

        private static System.Drawing.Color ColorAt(System.Drawing.Color[] pal, int index) =>
            index >= 0 && index < pal.Length ? pal[index] : System.Drawing.Color.Black;

        // ── Pointer interaction (canvas coordinates, already un-zoomed by the view) ────────────────
        public void HandlePointer(int x, int y)
        {
            if (BankCount > 0) HandlePointerComposited(x, y);
            else HandlePointerFlat(x, y);
        }

        private void HandlePointerComposited(int x, int y)
        {
            if (x < 0 || x >= CanvasSize || y < 0 || y >= CanvasSize) return;
            var cell = HitTest(x, y);
            if (cell == null) return;

            if (cell.PaletteBank != _activePaletteBank)
                BuildPaletteSwatches(cell.PaletteBank);

            CellLocal(cell, x, y, out int lx, out int ly);
            if (lx < 0 || lx >= cell.Width || ly < 0 || ly >= cell.Height) return;

            if (SelectedTool == SpriteEditTool.Eyedropper)
            {
                SelectedSwatchIndex = DecodeCell(cell)[ly * cell.Width + lx];
                SelectedTool = SpriteEditTool.Pencil;
                return;
            }

            var indices = DecodeCell(cell);
            int pos = ly * cell.Width + lx;
            if (indices[pos] == SelectedSwatchIndex) return;
            indices[pos] = SelectedSwatchIndex;
            EncodeCell(cell, indices);

            RebuildCompositedCanvas();
            HasUnsavedChanges = true;
        }

        private void HandlePointerFlat(int x, int y)
        {
            if (_flatIndices == null || x < 0 || x >= _flatWidth || y < 0 || y >= _flatHeight) return;

            if (SelectedTool == SpriteEditTool.Eyedropper)
            {
                SelectedSwatchIndex = _flatIndices[y * _flatWidth + x];
                SelectedTool = SpriteEditTool.Pencil;
                return;
            }

            int pos = y * _flatWidth + x;
            if (_flatIndices[pos] == SelectedSwatchIndex) return;
            _flatIndices[pos] = SelectedSwatchIndex;
            RebuildFlatCanvas();
            HasUnsavedChanges = true;
        }

        // ── Import / Export PNG ────────────────────────────────────────────────
        /// Returns null on success, error message on failure. In composited mode, the PNG must match
        /// the fixed canvas size (export first to get a correctly-sized/aligned template). Each pixel
        /// is re-hit-tested the same way a click would be, and validated against whichever cell (and
        /// therefore palette bank) owns it.
        public string ImportPng(string filePath)
        {
            if (_tile == null) return "No sprite loaded.";
            try
            {
                DSPRE.RawImage import;
                using (var fs = File.OpenRead(filePath))
                    import = ImageConverter.DecodeRawImage(fs);
                if (import == null) return "Image could not be decoded.";

                return BankCount > 0 ? ImportPngComposited(import) : ImportPngFlat(import);
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private string ImportPngComposited(DSPRE.RawImage import)
        {
            if (import.Width != CanvasSize || import.Height != CanvasSize)
                return $"Size mismatch. This editor's canvas is {CanvasSize}×{CanvasSize} (fixed), PNG: {import.Width}×{import.Height}. Export first to get a correctly-sized template.";

            var lookups = new Dictionary<int, Dictionary<int, int>>();
            Dictionary<int, int> LookupFor(int bank)
            {
                if (lookups.TryGetValue(bank, out var d)) return d;
                d = new Dictionary<int, int>();
                var pal = _pal.Palette[bank];
                for (int i = 0; i < pal.Length; i++)
                {
                    int key = (pal[i].R << 16) | (pal[i].G << 8) | pal[i].B;
                    if (!d.ContainsKey(key)) d[key] = i;
                }
                lookups[bank] = d;
                return d;
            }

            var perCell = new Dictionary<EditCell, int[]>();
            for (int y = 0; y < CanvasSize; y++)
            {
                for (int x = 0; x < CanvasSize; x++)
                {
                    var cell = HitTest(x, y);
                    if (cell == null) continue; // background area, no cell to write into, ignore

                    if (!perCell.TryGetValue(cell, out int[] idxArr))
                        idxArr = perCell[cell] = DecodeCell(cell);

                    int i = (y * CanvasSize + x) * 4;
                    int key = (import.Bgra[i + 2] << 16) | (import.Bgra[i + 1] << 8) | import.Bgra[i];
                    if (!LookupFor(cell.PaletteBank).TryGetValue(key, out int idx))
                        return $"Pixel ({x},{y}) isn't one of that area's {_pal.Palette[cell.PaletteBank].Length} palette colors (bank {cell.PaletteBank}). Recolor to match exactly, or use the pencil tool instead.";

                    CellLocal(cell, x, y, out int lx, out int ly);
                    idxArr[ly * cell.Width + lx] = idx;
                }
            }

            foreach (var kv in perCell) EncodeCell(kv.Key, kv.Value);
            RebuildCompositedCanvas();
            HasUnsavedChanges = true;
            return null;
        }

        private string ImportPngFlat(DSPRE.RawImage import)
        {
            if (import.Width != _flatWidth || import.Height != _flatHeight)
                return $"Size mismatch. Sprite sheet: {_flatWidth}×{_flatHeight}, PNG: {import.Width}×{import.Height}";

            var pal = _pal.Palette[0];
            var lookup = new Dictionary<int, int>();
            for (int i = 0; i < pal.Length; i++)
            {
                int key = (pal[i].R << 16) | (pal[i].G << 8) | pal[i].B;
                if (!lookup.ContainsKey(key)) lookup[key] = i;
            }

            int[] newIndices = new int[_flatWidth * _flatHeight];
            for (int y = 0; y < _flatHeight; y++)
            {
                for (int x = 0; x < _flatWidth; x++)
                {
                    int i = (y * _flatWidth + x) * 4;
                    int key = (import.Bgra[i + 2] << 16) | (import.Bgra[i + 1] << 8) | import.Bgra[i];
                    if (!lookup.TryGetValue(key, out int idx))
                        return $"Pixel ({x},{y}) isn't one of this sprite's {pal.Length} palette colors. " +
                               "Recolor the PNG to match the current palette exactly, or use the pencil tool instead.";
                    newIndices[y * _flatWidth + x] = idx;
                }
            }

            _flatIndices = newIndices;
            RebuildFlatCanvas();
            HasUnsavedChanges = true;
            return null;
        }

        public bool ExportPng(string filePath)
        {
            try
            {
                DSPRE.RawImage raw;
                if (BankCount > 0)
                {
                    raw = GetCompositedRawImage(_selectedFrameIndex, CanvasSize, CanvasSize, null);
                }
                else
                {
                    if (_flatIndices == null) return false;
                    raw = new DSPRE.RawImage(_flatWidth, _flatHeight);
                    var pal = _pal.Palette[0];
                    for (int y = 0; y < _flatHeight; y++)
                        for (int x = 0; x < _flatWidth; x++)
                        {
                            var c = ColorAt(pal, _flatIndices[y * _flatWidth + x]);
                            raw.SetPixel(x, y, c.R, c.G, c.B, 255);
                        }
                }
                ImageConverter.ToAvaloniaBitmap(raw).Save(filePath, PngBitmapEncoderOptions.Default);
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Error("TrainerSpriteEditorViewModel.ExportPng failed: " + ex.Message);
                return false;
            }
        }

        // ── Save (write back to the unpacked trainerGraphics NARC member) ──────
        /// Returns null on success, error message on failure.
        public string Save()
        {
            if (_tile == null) return "No sprite loaded.";
            try
            {
                if (BankCount == 0)
                {
                    // Composited-mode edits are already written in place into _tile.Tiles by
                    // EncodeCell as they happen; the flat-sheet fallback packs on save instead.
                    int bpp = _tile.BPP;
                    byte[] rasterBytes = PackIndices(_flatIndices, _flatWidth, _flatHeight, bpp);
                    byte[] nativeBytes = _tile.FormTile == TileForm.Horizontal
                        ? Actions.HorizontalToLineal(rasterBytes, _flatWidth, _flatHeight, bpp, _tile.TileSize)
                        : rasterBytes;
                    _tile.Set_Tiles(nativeBytes);
                }

                _tile.Write(_tilesPath, _pal);

                if (BankCount > 0) BuildFrameThumbnails();

                HasUnsavedChanges = false;
                StatusText = "Saved.";
                return null;
            }
            catch (Exception ex)
            {
                StatusText = "Save failed: " + ex.Message;
                AppLogger.Error("TrainerSpriteEditorViewModel.Save failed: " + ex.Message);
                return ex.Message;
            }
        }

        // ── Palette-index <-> packed byte helpers ──────────────────────────────
        // Mirror Ekona.Images.Actions.Get_Color's bit layout exactly (see Ekona/Images/Actions.cs and
        // Ekona/Helper/BitsConverter.cs) so packing is the true inverse of how the format is read.
        private static int[] UnpackIndices(byte[] data, int width, int height, int bpp)
        {
            int count = width * height;
            int[] indices = new int[count];
            switch (bpp)
            {
                case 4:
                    for (int i = 0; i < count && i / 2 < data.Length; i++)
                        indices[i] = Ekona.Helper.BitsConverter.ByteToBit4(data[i / 2])[i % 2];
                    break;
                case 8:
                    for (int i = 0; i < count && i < data.Length; i++)
                        indices[i] = data[i];
                    break;
                case 2:
                    for (int i = 0; i < count && i / 4 < data.Length; i++)
                        indices[i] = Ekona.Helper.BitsConverter.ByteToBit2(data[i / 4])[i % 4];
                    break;
                case 1:
                    for (int i = 0; i < count && i / 8 < data.Length; i++)
                        indices[i] = Ekona.Helper.BitsConverter.ByteToBits(data[i / 8])[i % 8];
                    break;
                default:
                    throw new NotSupportedException($"Unsupported color depth ({bpp} bpp) for sprite editing.");
            }
            return indices;
        }

        private static byte[] PackIndices(int[] indices, int width, int height, int bpp)
        {
            int count = width * height;
            switch (bpp)
            {
                case 4:
                {
                    byte[] result = new byte[(count + 1) / 2];
                    for (int i = 0; i < count; i += 2)
                    {
                        byte lo = (byte)(indices[i] & 0xF);
                        byte hi = (byte)((i + 1 < count ? indices[i + 1] : 0) & 0xF);
                        result[i / 2] = Ekona.Helper.BitsConverter.Bit4ToByte(lo, hi);
                    }
                    return result;
                }
                case 8:
                {
                    byte[] result = new byte[count];
                    for (int i = 0; i < count; i++) result[i] = (byte)(indices[i] & 0xFF);
                    return result;
                }
                case 2:
                {
                    byte[] result = new byte[(count + 3) / 4];
                    for (int i = 0; i < count; i += 4)
                    {
                        int b = 0;
                        for (int j = 0; j < 4; j++)
                        {
                            int idx = i + j < count ? indices[i + j] : 0;
                            b |= (idx & 0x3) << (j * 2);
                        }
                        result[i / 4] = (byte)b;
                    }
                    return result;
                }
                case 1:
                {
                    int padded = count % 8 == 0 ? count : count + (8 - count % 8);
                    byte[] bits = new byte[padded];
                    for (int i = 0; i < count; i++) bits[i] = (byte)(indices[i] & 0x1);
                    return Ekona.Helper.BitsConverter.BitsToBytes(bits);
                }
                default:
                    throw new NotSupportedException($"Unsupported color depth ({bpp} bpp) for sprite editing.");
            }
        }
    }
}
