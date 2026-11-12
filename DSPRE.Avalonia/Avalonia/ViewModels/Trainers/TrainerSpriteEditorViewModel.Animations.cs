using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using DSPRE.Avalonia.Data;
using DSPRE.Editors;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
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

namespace DSPRE.Avalonia.ViewModels.Trainers
{
    public partial class TrainerSpriteEditorViewModel
    {
        // hg-engine's NNN_anim.json. Null outside hg-engine, where the text comes from the ROM.
        private string _animJsonPath;

        // Created in the editor and not written yet.
        private bool _animJsonCreated;

        private string _animJsonText = "";
        public string AnimJsonText
        {
            get => _animJsonText;
            set
            {
                if (!Set(ref _animJsonText, value)) return;
                if (CanEditAnimJson) AnimJsonDirty = true;
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

        // Outside hg-engine only each frame's cell and hold change in the ROM's animation file.
        private NanrFile _romAnim;
        public bool CanEditAnimFrames => CanEditAnimJson || _romAnim != null;

        private NanrFile OpenRomAnimations()
        {
            if (_set.NamingScreen || _parts.Count == 0 || IsFlatSheetMode) return null;
            try
            {
                SpritePart p = _parts[0];
                return NanrFile.Read(p.Nanr ?? Member(TrainerGraphicsLayout.AnimationEntry(p.Entry)));
            }
            catch (Exception ex)
            {
                AppLogger.Error("TrainerSpriteEditorViewModel: animation file couldn't be read: " + ex.Message);
                return null;
            }
        }

        // A changed frame gets a new result entry, so frames sharing the old one keep it.
        private void ApplyRomAnimEdits()
        {
            if (_romAnim == null || _animRoot == null || _parts.Count == 0) return;
            bool changed = false;
            for (int s = 0; s < Math.Min(_romAnim.Sequences.Count, _animRoot.Sequences.Count); s++)
            {
                List<NanrFile.Frame> frames = _romAnim.Sequences[s].Frames;
                List<AnimFrameDataJson> model = _animRoot.Sequences[s].FrameData;
                for (int f = 0; f < Math.Min(frames.Count, model.Count); f++)
                {
                    if (_romAnim.CellOf(s, f) != model[f].CellIndex) { _romAnim.SetCell(s, f, model[f].CellIndex); changed = true; }
                    if (Math.Max(1, (int)frames[f].Delay) != model[f].FrameDelay) { _romAnim.SetDelay(s, f, model[f].FrameDelay); changed = true; }
                }
            }
            if (!changed) return;
            _romAnimDirty = true;
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private string SaveRomAnimations()
        {
            try
            {
                SpritePart p = _parts[0];
                ArchiveMembers.Put(_set.Archive, TrainerGraphicsLayout.AnimationEntry(p.Entry), _romAnim.Write());
                p.Nanr = null;
                _romAnimDirty = false;
                OnPropertyChanged(nameof(HasUnsavedChanges));
                StatusText = "Saved.";
                return null;
            }
            catch (Exception ex) when (AppLogger.Noted("TrainerSpriteEditorViewModel.SaveRomAnimations", ex))
            {
                StatusText = "Couldn't save: " + ex.Message;
                return ex.Message;
            }
        }

        public bool HasAnimJsonFile => HgEngineTrainerGraphicsSource.Has(_animJsonPath);
        /// <summary>True when there is an animation to show, from hg-engine's JSON or the ROM.</summary>
        public bool HasAnimation => HasAnimJsonFile || _animJsonCreated || (!CanEditAnimJson && !string.IsNullOrEmpty(AnimJsonText));

        private void SetAnimJsonTextSilent(string text)
        {
            _animJsonText = text;
            OnPropertyChanged(nameof(AnimJsonText));
            TryRebuildAnimModelFromText();
        }

        private void LoadAnimJson(int trClassID)
        {
            _animJsonCreated = false;
            _animJsonPath = HgEngineProject.IsActive
                ? HgEngineTrainerGraphicsSource.Stem(_set.IsBack, trClassID) + "_anim.json"
                : null;
            OnPropertyChanged(nameof(CanEditAnimJson));
            OnPropertyChanged(nameof(HasAnimJsonFile));

            _romAnim = _animJsonPath == null ? OpenRomAnimations() : null;
            _romAnimDirty = false;
            OnPropertyChanged(nameof(CanEditAnimFrames));
            if (_animJsonPath == null)
            {
                SetAnimJsonTextSilent((_romAnim != null ? AnimationJsonOf(_romAnim) : RomAnimationJson(trClassID)) ?? "");
                AnimJsonStatusText = "";
            }
            else if (HgEngineTrainerGraphicsSource.Has(_animJsonPath))
            {
                try
                {
                    SetAnimJsonTextSilent(HgEngineTrainerGraphicsSource.ReadText(_animJsonPath));
                    AnimJsonStatusText = "";
                }
                catch (Exception ex) when (AppLogger.Noted("TrainerSpriteEditorViewModel.LoadAnimJson", ex))
                {
                    SetAnimJsonTextSilent("");
                    AnimJsonStatusText = "Couldn't read: " + ex.Message;
                }
            }
            else
            {
                SetAnimJsonTextSilent("");
                AnimJsonStatusText = "";
            }
            AnimJsonDirty = false;
            OnPropertyChanged(nameof(HasAnimation));
            OnPropertyChanged(nameof(CanCreateAnimJson));
        }

        // The ROM's compiled animation as the JSON model, for showing and playing without a checkout.
        private string RomAnimationJson(int id)
        {
            try
            {
                TrainerClassSpriteRenderer renderer = new TrainerClassSpriteRenderer();
                renderer.Load(id, _set.Archive);
                if (renderer.SequenceCount == 0) return null;
                AnimJsonRoot root = new AnimJsonRoot();
                for (int seq = 0; seq < renderer.SequenceCount; seq++)
                {
                    AnimSequenceJson model = new AnimSequenceJson { AnimationType = 1, PlaybackMode = 2 };
                    foreach ((int bank, int duration) in renderer.Sequence(seq))
                        model.FrameData.Add(new AnimFrameDataJson { CellIndex = bank, FrameDelay = Math.Max(1, duration) });
                    root.Sequences.Add(model);
                }
                return root.Serialize();
            }
            catch (Exception ex)
            {
                AppLogger.Error("TrainerSpriteEditorViewModel: animation couldn't be read: " + ex.Message);
                return null;
            }
        }

        /// <summary>Writes the text if it parses as JSON. Null on success, else the error.</summary>
        public string SaveAnimJson()
        {
            if (_animJsonPath == null) return "No hg-engine checkout is linked.";
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
                // The shared writer replaces the file atomically and keeps its line endings and cached text.
                HgEngineFileCache.WriteText(_animJsonPath, AnimJsonText);
                _animJsonCreated = false;
                AnimJsonDirty = false;
                AnimJsonStatusText = "Saved.";
                OnPropertyChanged(nameof(HasAnimJsonFile));
                OnPropertyChanged(nameof(HasAnimation));
                OnPropertyChanged(nameof(CanCreateAnimJson));
                return null;
            }
            catch (Exception ex) when (AppLogger.Noted("TrainerSpriteEditorViewModel.SaveAnimJson", ex))
            {
                return ex.Message;
            }
        }

        /// <summary>Seeds a one-frame anim.json shaped like hg-engine's own.</summary>
        public string CreateAnimJson()
        {
            if (_animJsonPath == null) return "No hg-engine checkout is linked.";
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
            // Held in the editor like any other edit, so Discard can still take it back.
            SetAnimJsonTextSilent(template);
            _animJsonCreated = true;
            AnimJsonDirty = true;
            AnimJsonStatusText = "Not saved yet.";
            OnPropertyChanged(nameof(HasAnimation));
            OnPropertyChanged(nameof(CanCreateAnimJson));
            return null;
        }

        private AnimJsonRoot _animRoot;
        private bool _syncingAnimText;   // guards the structured-model <-> AnimJsonText echo loop

        public ObservableCollection<AnimSequenceChoiceViewModel> AnimSequenceChoices { get; } = new();
        private bool _refillingSequences;

        private void RefillSequenceChoices()
        {
            _refillingSequences = true;
            try
            {
                AnimSequenceChoices.Clear();
                for (int i = 0; i < (_animRoot?.Sequences.Count ?? 0); i++)
                    AnimSequenceChoices.Add(new AnimSequenceChoiceViewModel(_animRoot.Sequences[i], i));
            }
            finally { _refillingSequences = false; }
        }

        private AnimSequenceChoiceViewModel _selectedAnimSequence;
        public AnimSequenceChoiceViewModel SelectedAnimSequence
        {
            get => _selectedAnimSequence;
            set
            {
                // Refilling the choices makes the picker push null back; that is not the user's choice.
                if (_refillingSequences) return;
                if (!Set(ref _selectedAnimSequence, value)) return;
                OnPropertyChanged(nameof(HasSelectedAnimSequence));
                StopAnimPreview();
                RebuildAnimFrameRows();
            }
        }
        public bool HasSelectedAnimSequence => SelectedAnimSequence != null;
        public bool CanCreateAnimJson => CanEditAnimJson && !HasAnimation;

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
                string name = GetBank(i).Name;
                string label = string.IsNullOrWhiteSpace(name) ? $"Cell {i}" : $"{name} ({i})";
                AnimCellChoices.Add(new AnimCellChoiceViewModel(i, label, RenderAnimCellThumbnail(i)));
            }
        }

        private Bitmap RenderAnimCellThumbnail(int cellIndex, int size = 72)
        {
            if (BankCount == 0 || cellIndex < 0 || cellIndex >= BankCount || _tile == null || _pal == null) return null;
            try
            {
                RawImage raw = GetCompositedRawImage(cellIndex, size, size, null);
                return ImageConverter.ToAvaloniaBitmap(raw);
            }
            catch (Exception ex) { AppLogger.WarnOnce("TrainerSpriteEditorViewModel.RenderAnimCellThumbnail", ex); return null; }
        }

        // A parse error leaves the shown structure alone, so a typo in the JSON tab doesn't blank it.
        private void TryRebuildAnimModelFromText()
        {
            if (string.IsNullOrWhiteSpace(AnimJsonText))
            {
                _animRoot = null;
                _selectedAnimSequence = null;
                RefillSequenceChoices();
                OnPropertyChanged(nameof(SelectedAnimSequence));
                OnPropertyChanged(nameof(HasSelectedAnimSequence));
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
            catch (Exception ex) when (AppLogger.Noted("TrainerSpriteEditorViewModel.TryRebuildAnimModelFromText", ex))
            {
                AnimModelStatusText = "JSON has an error, showing the last valid structure: " + ex.Message;
                return;
            }

            int keepIndex = SelectedAnimSequence?.Index ?? 0;
            RefillSequenceChoices();

            AnimSequenceChoiceViewModel restore = AnimSequenceChoices.FirstOrDefault(s => s.Index == keepIndex) ?? AnimSequenceChoices.FirstOrDefault();
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
                foreach (AnimFrameDataJson frame in SelectedAnimSequence.Model.FrameData)
                    AnimFrameRows.Add(new AnimFrameRowViewModel(frame, SyncAnimModelToText, i => RenderAnimCellThumbnail(i)));
            }
            if (!AnimPreviewPlaying) AnimPreviewBitmap = AnimFrameRows.Count > 0 ? AnimFrameRows[0].Thumbnail : null;
            OnPropertyChanged(nameof(HasAnimFrameRows));
        }

        private void SyncAnimModelToText()
        {
            if (_animRoot == null) return;
            _syncingAnimText = true;
            AnimJsonText = _animRoot.Serialize();
            _syncingAnimText = false;
            ApplyRomAnimEdits();
            if (!AnimPreviewPlaying && AnimFrameRows.Count > 0) AnimPreviewBitmap = AnimFrameRows[0].Thumbnail;

            // Sequence picker labels show frame counts, so rebuild them after any add/remove.
            int keepIndex = SelectedAnimSequence?.Index ?? 0;
            RefillSequenceChoices();
            AnimSequenceChoiceViewModel restore = AnimSequenceChoices.FirstOrDefault(s => s.Index == keepIndex) ?? AnimSequenceChoices.FirstOrDefault();
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

        /// <summary>Null on success. Refused when it is the sequence's only frame.</summary>
        public string RemoveAnimFrame(AnimFrameRowViewModel row)
        {
            if (SelectedAnimSequence == null || row == null) return null;
            if (SelectedAnimSequence.Model.FrameData.Count <= 1)
                return "A sequence needs at least one frame. Remove the whole sequence instead.";
            StopAnimPreview();
            SelectedAnimSequence.Model.FrameData.Remove(row.Model);
            RebuildAnimFrameRows();
            SyncAnimModelToText();
            return null;
        }

        public void MoveAnimFrame(AnimFrameRowViewModel row, int direction)
        {
            if (SelectedAnimSequence == null || row == null) return;
            List<AnimFrameDataJson> list = SelectedAnimSequence.Model.FrameData;
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
            AnimSequenceJson seq = new AnimSequenceJson
            {
                AnimationType = 1,
                PlaybackMode = 2,
                FrameData = { new AnimFrameDataJson { FrameDelay = 4, CellIndex = 0 } },
            };
            _animRoot.Sequences.Add(seq);
            SyncAnimModelToText();
            SelectedAnimSequence = AnimSequenceChoices.LastOrDefault();
        }

        /// <summary>Null on success. Refused when it is the file's only sequence.</summary>
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

        public async Task PlayAnimPreviewOnceAsync()
        {
            if (SelectedAnimSequence == null || AnimFrameRows.Count == 0) return;
            StopAnimPreview();
            CancellationTokenSource cts = new CancellationTokenSource();
            _animPreviewCts = cts;
            AnimPreviewPlaying = true;
            try
            {
                for (int i = 0; i < AnimFrameRows.Count; i++)
                {
                    AnimFrameRowViewModel row = AnimFrameRows[i];
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
    }
}
