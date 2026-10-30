using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using DSPRE.Avalonia.BattleAnim;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using global::Avalonia.Media;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Threading;
using DSPRE.Avalonia.BattleAnim;
using DSPRE.Avalonia.Data;
using static DSPRE.RomInfo;
using Avalonia.Platform;
using System.Runtime.InteropServices;

namespace DSPRE.Avalonia.ViewModels.Battle
{
    /// <summary>Battle scripts (waza_seq, be_seq, sub_seq) and move animations (we). Platinum and HGSS only.</summary>
    public sealed class BattleScriptEditorViewModel : INotifyPropertyChanged, DSPRE.Editors.IEditorWithUnsavedChanges, ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T field, T value, [CallerMemberName] string n = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value; OnPropertyChanged(n); return true;
        }

        public enum Archive { MoveScripts = 0, EffectScripts = 1, Subroutines = 2, MoveAnimation = 3, SubAnimation = 4 }

        private readonly WazaSeqVersion _version;
        private string[] _moveNames;
        private readonly ScriptNarc[] _narcs = new ScriptNarc[5];

        public bool IsAvailable { get; }
        public string UnavailableText => "Not in this game";

        public BattleScriptEditorViewModel()
        {
            IsAvailable = GameFamily == GameFamilies.Plat || GameFamily == GameFamilies.HGSS;
            _version = GameFamily switch
            {
                GameFamilies.DP => WazaSeqVersion.DP,
                GameFamilies.Plat => WazaSeqVersion.Plat,
                _ => WazaSeqVersion.HGSS,
            };
            _moveNames = SafeMoveNames();

            _narcs[(int)Archive.MoveScripts]  = new ScriptNarc(DirNames.WazaSeq);
            _narcs[(int)Archive.EffectScripts] = new ScriptNarc(DirNames.BeSeq);
            _narcs[(int)Archive.Subroutines]  = new ScriptNarc(DirNames.SubSeq);
            _narcs[(int)Archive.MoveAnimation] = new ScriptNarc(DirNames.WazaEffectScripts);
            _narcs[(int)Archive.SubAnimation] = new ScriptNarc(DirNames.WazaEffectSub);

            _dirs[(int)Archive.MoveScripts] = DirNames.WazaSeq;
            _dirs[(int)Archive.EffectScripts] = DirNames.BeSeq;
            _dirs[(int)Archive.Subroutines] = DirNames.SubSeq;
            _dirs[(int)Archive.MoveAnimation] = DirNames.WazaEffectScripts;
            _dirs[(int)Archive.SubAnimation] = DirNames.WazaEffectSub;

            if (IsAvailable) SelectArchive(0);
        }

        private static string[] SafeMoveNames()
        {
            try { return GetAttackNames(); } catch (Exception ex) { AppLogger.WarnOnce("BattleScriptEditorViewModel.SafeMoveNames", ex); return Array.Empty<string>(); }
        }

        // ── Archive selection ────────────────────────────────────────────────────
        public string[] ArchiveOptions { get; } =
        {
            "Move scripts", "Move effect scripts", "Subroutines", "Move animations", "Animation subroutines",
        };

        private static readonly string[] AnimationParts = { "Animations", "Subroutines" };
        private static readonly string[] ScriptParts = { "Move scripts", "Effect scripts", "Subroutines" };
        private int _lastAnimationArchive = (int)Archive.MoveAnimation, _lastScriptArchive = (int)Archive.MoveScripts;

        /// <summary>0 for move animations, 1 for battle scripts.</summary>
        public int GroupIndex
        {
            get => _archiveIndex < 0 ? 0 : IsAnimation ? 0 : 1;
            set { if (value != GroupIndex) ArchiveIndex = value == 0 ? _lastAnimationArchive : _lastScriptArchive; }
        }

        public string[] PartNames => IsAnimation ? AnimationParts : ScriptParts;

        public int PartIndex
        {
            get => _archiveIndex < 0 ? 0 : IsAnimation ? _archiveIndex - (int)Archive.MoveAnimation : _archiveIndex;
            set { if (value >= 0 && value < PartNames.Length) ArchiveIndex = (IsAnimation ? (int)Archive.MoveAnimation : 0) + value; }
        }

        private void RaiseArchive()
        {
            OnPropertyChanged(nameof(ArchiveIndex));
            OnPropertyChanged(nameof(GroupIndex));
            OnPropertyChanged(nameof(PartNames));
            OnPropertyChanged(nameof(PartIndex));
        }

        private int _archiveIndex = -1;
        public int ArchiveIndex
        {
            get => _archiveIndex;
            set
            {
                if (RecordSwitchGuard.IsSnappingBack) return;
                if (value == _archiveIndex) return;
                if (Dirty && _fileIndex >= 0)
                {
                    int requested = value;
                    RecordSwitchGuard.SnapBack(() => _archiveIndex, v => _archiveIndex = v, RaiseArchive);
                    _ = SwitchEntryAsync(() => SelectArchive(requested));
                    return;
                }
                SelectArchive(value);
            }
        }

        /// <summary>Whether an animation archive is open: the move animations, or the subroutines they call.</summary>
        public bool IsAnimation => (Archive)_archiveIndex is Archive.MoveAnimation or Archive.SubAnimation;
        private ScriptNarc CurrentNarc => _narcs[_archiveIndex];
        private readonly DirNames[] _dirs = new DirNames[5];
        private Dictionary<int, HgEngineOwnedFile> _sourceById;
        private List<int> _sourceOrder = new List<int>();

        public ScriptCommandGuideViewModel BuildCommandGuideViewModel() => new ScriptCommandGuideViewModel(IsAnimation);

        // ── Sound preview (PlaySoundEffect and friends' "Sound" argument) ───────────────────────
        // Lazily loads and caches the ROM's own sound archive so scrubbing through several sound IDs in a
        // session only pays the parse cost once.
        private SdatArchive _sdat;
        private bool _sdatLoadTried;
        private string _sdatLoadError;
        private SdatArchive LoadSdat()
        {
            if (_sdat != null || _sdatLoadTried) return _sdat;
            _sdatLoadTried = true;
            try
            {
                // Each version's sound archive has its own filename; DP does not carry Platinum's "pl_" file.
                string fileName = GameFamily switch
                {
                    GameFamilies.HGSS => "gs_sound_data.sdat",
                    GameFamilies.Plat => "pl_sound_data.sdat",
                    GameFamilies.DP => "sound_data.sdat",
                    _ => "sound_data.sdat",
                };
                // workDir is the project's own root (where arm9/banner/etc. live); the ROM's own internal
                // filesystem (where "data/sound/..." actually lives) is nested one level deeper, under "files"
                // for a ds-rom project or "data" for a legacy one.
                string romRoot = System.IO.Path.Combine(WorkDir, IsDsRomProject ? "files" : "data");
                string path = System.IO.Path.Combine(romRoot, "data", "sound", fileName);
                if (!System.IO.File.Exists(path)) { _sdatLoadError = $"Sound archive not found at:\n{path}"; return null; }
                _sdat = SdatArchive.Parse(System.IO.File.ReadAllBytes(path));
                if (_sdat.Sequences.Count == 0) _sdatLoadError = $"The sound archive at {path} has no sequences DSPRE can read.";
            }
            catch (System.Exception ex) { _sdat = null; _sdatLoadError = ex.ToString(); }
            return _sdat;
        }

        /// <summary>The sound's real name from the ROM's own sound archive (e.g. "SEQ_SE_PL_KEZURI"), or null
        /// if it can't be resolved. Shown next to a "Sound" argument so the ID isn't just a bare number.</summary>
        private string SoundNameOf(int soundId) => LoadSdat()?.SeqNames.TryGetValue(soundId, out string n) == true ? n : null;

        /// <summary>
        /// The cry of whichever Pokemon the preview is showing as the attacker. PlayPokemonCry hands the
        /// games a pan and a volume as well; neither is applied here.
        /// </summary>
        private void PreviewCry()
        {
            short[] pcm = AtVolume(CryPcm());
            if (pcm == null || pcm.Length == 0) return;
            try { _previewVoices.Add((-1, AudioOutput.Current.Start(pcm, SoundRate))); }
            catch { /* a preview should not put a dialog up because a sound would not play */ }
        }

        // Rendered once per sound, so playback starts on time and the player knows how long each one lasts.
        private const int SoundRate = AudioOutput.RenderRate;
        // Each sound renders once, on whichever thread asks first; the others wait for that render.
        private readonly System.Collections.Concurrent.ConcurrentDictionary<int, Lazy<short[]>> _soundPcm =
            new System.Collections.Concurrent.ConcurrentDictionary<int, Lazy<short[]>>();
        private int _cryPcmSpecies = -1;
        private short[] _cryPcm;

        private short[] SoundPcm(int soundId)
        {
            SdatArchive sdat = LoadSdat();
            if (sdat == null) return null;
            return _soundPcm.GetOrAdd(soundId, id => new Lazy<short[]>(() =>
            {
                try { return SseqPlayer.Render(sdat, id); }
                catch (Exception ex) { AppLogger.WarnOnce("BattleScriptEditorViewModel.SoundPcm", ex); return null; }
            })).Value;
        }

        private short[] CryPcm()
        {
            int species = _gaugeSpeciesId;
            if (species <= 0) return null;
            if (_cryPcmSpecies == species) return _cryPcm;
            try { _cryPcm = SoundArchive.RenderCry(species); }
            catch (Exception ex) { AppLogger.WarnOnce("BattleScriptEditorViewModel.CryPcm", ex); _cryPcm = null; }
            _cryPcmSpecies = species;
            return _cryPcm;
        }

        // A scaled copy, so the rendered sound stays as it is for the next play.
        private short[] AtVolume(short[] pcm)
        {
            if (pcm == null || _volume >= 100) return pcm;
            short[] scaled = new short[pcm.Length];
            for (int i = 0; i < pcm.Length; i++) scaled[i] = (short)(pcm[i] * _volume / 100);
            return scaled;
        }

        private int PassesOf(short[] pcm) =>
            pcm == null || pcm.Length == 0 ? 0 : (int)Math.Ceiling(pcm.Length / 2.0 / SoundRate * PreviewFps);

        // Every sound a script can play, so they are all rendered before the clock starts.
        private static readonly HashSet<string> SoundCommands = new HashSet<string>
        {
            "PlaySoundEffect", "PlayPannedSoundEffect", "PlayMovingSoundEffectAtkDef", "PlayMovingSoundEffectAtkDef2",
            "PlayMovingSoundEffectNoCorrection", "PlayLoopedSoundEffect", "PlayDelayedSoundEffect",
        };

        private void RenderScriptSounds(List<WazaSeqCommand> cmds)
        {
            foreach (WazaSeqCommand c in cmds)
                if (c.Args.Length >= 1 && SoundCommands.Contains(BattleAnimCommands.Name(_version, c.OpId) ?? "")) SoundPcm(c.Args[0]);
        }

        // The sounds this preview started, so stopping one sound or the preview leaves other editors' sound alone.
        private readonly List<(int Sound, object Handle)> _previewVoices = new List<(int Sound, object Handle)>();

        private void PreviewStopSound(int soundId)
        {
            foreach ((int sound, object handle) in _previewVoices)
                if (sound == soundId) try { AudioOutput.Current.Stop(handle); } catch { }   // stopping a sound that already ended is harmless
            _previewVoices.RemoveAll(v => v.Sound == soundId);
        }

        private void StopPreviewSounds()
        {
            foreach ((int _, object handle) in _previewVoices) try { AudioOutput.Current.Stop(handle); } catch { }   // stopping a sound that already ended is harmless
            _previewVoices.Clear();
        }

        private void PreviewSound(int soundId)
        {
            short[] pcm = AtVolume(SoundPcm(soundId));
            if (pcm == null || pcm.Length == 0) return;
            try { _previewVoices.Add((soundId, AudioOutput.Current.Start(pcm, SoundRate))); }
            catch { /* best-effort preview; animation playback must never surface an error dialog */ }
        }

        /// <summary>Same as <see cref="PreviewSound"/> but returns a human-readable reason on failure (null on
        /// success), for the card's "Preview sound" button to surface via a dialog instead of failing silently.</summary>
        internal string TryPreviewSound(int soundId)
        {
            SdatArchive sdat = LoadSdat();
            if (sdat == null) return _sdatLoadError ?? "The sound archive couldn't be loaded.";
            try
            {
                if (!sdat.SeqNames.ContainsKey(soundId) && (soundId < 0 || soundId >= sdat.Sequences.Count || sdat.Sequences[soundId] == null))
                    return $"Sound {soundId} isn't in this ROM's sound archive.";
                short[] pcm = SseqPlayer.Render(sdat, soundId);
                if (pcm == null || pcm.Length == 0) return $"Sound {soundId} plays nothing here.";
                AudioOutput.Current.Play(pcm, SoundRate);
                if (AudioOutput.Current is NullAudioOutput) return "No sound output.";
                return null;
            }
            catch (System.Exception ex) { AppLogger.WarnOnce("BattleScriptEditorViewModel.TryPreviewSound", ex); return ex.ToString(); }
        }

        /// <summary>Internal opcode names for the current archive (index = opcode id), used for schema lookups.</summary>
        public ObservableCollection<string> OpcodeNames { get; } = new ObservableCollection<string>();
        /// <summary>Friendly opcode titles (index = opcode id); drives the per-row opcode dropdown.</summary>
        public ObservableCollection<string> OpcodeDisplayNames { get; } = new ObservableCollection<string>();

        // ── the three ways of reading a script ──────────────────────────────────────

        /// <summary>The lines the chosen view shows. All three read the same commands.</summary>
        public ObservableCollection<BattleAnimLine> ViewLines { get; } = new ObservableCollection<BattleAnimLine>();

        private BattleAnimLine _pickedLine;
        /// <summary>The clicked line, which the panel beside it explains.</summary>
        public BattleAnimLine PickedLine
        {
            get => _pickedLine;
            set
            {
                if (!Set(ref _pickedLine, value)) return;
                OnPropertyChanged(nameof(PickedDetail));
                OnPropertyChanged(nameof(PickedSource));
                OnPropertyChanged(nameof(HasPickedDetail));
            }
        }

        public string PickedDetail => _pickedLine?.Detail ?? "";
        public string PickedSource => string.IsNullOrEmpty(_pickedLine?.Source) ? "" : "From " + _pickedLine.Source;
        public bool HasPickedDetail => !string.IsNullOrEmpty(PickedDetail);

        /// <summary>Which view is showing. Remembered between sessions.</summary>
        public int ViewMode
        {
            get => DSPRE.SettingsManager.Settings?.MoveAnimationViewMode ?? 0;
            set
            {
                int v = Math.Clamp(value, 0, 2);
                if (DSPRE.SettingsManager.Settings == null || DSPRE.SettingsManager.Settings.MoveAnimationViewMode == v) return;
                DSPRE.SettingsManager.Settings.MoveAnimationViewMode = v;
                try { DSPRE.SettingsManager.Save(); } catch (Exception ex) { AppLogger.Warn("Saving the move animation view mode: " + ex.Message); }
                OnPropertyChanged(nameof(ViewMode));
                RefreshViewLines();
            }
        }

        /// <summary>Builds the lines for whichever view is showing, from the commands as they stand now.</summary>
        public void RefreshViewLines()
        {
            ViewLines.Clear();
            if (!IsAnimation) { OnPropertyChanged(nameof(HasViewLines)); return; }

            List<WazaSeqCommand> cmds = Rows.Select(r => new WazaSeqCommand(r.OpId, r.Args.ToArray())).ToList();
            int pos = 0;
            foreach (WazaSeqCommand c in cmds) { c.WordPos = pos; pos += 1 + c.Args.Length; }

            foreach (BattleAnimLine l in BattleAnimScriptDisplay.Build(cmds, _version, (BattleAnimViewMode)ViewMode, SoundNameOf))
                ViewLines.Add(l);
            OnPropertyChanged(nameof(HasViewLines));
            OnPropertyChanged(nameof(ViewSummary));
        }

        public bool HasViewLines => ViewLines.Count > 0;

        private int _openTab;
        /// <summary>Which of Cards, Script and Read is open.</summary>
        public int OpenTab
        {
            get => _openTab;
            set { if (Set(ref _openTab, value)) OnPropertyChanged(nameof(ShowTextHelp)); }
        }

        /// <summary>The note on writing a command: on the Script tab for animations, always for other scripts.</summary>
        public bool ShowTextHelp => _openTab == 1 || !IsAnimation;

        public string ViewSummary
        {
            get
            {
                int shown = ViewLines.Count(l => !l.IsHeading);
                int commands = Rows.Count;
                if (commands == 0) return "";
                return shown == commands
                    ? $"{commands} commands"
                    : $"{shown} lines for {commands} commands";
            }
        }


        private void SelectArchive(int index)
        {
            _archiveIndex = Math.Clamp(index, 0, ArchiveOptions.Length - 1);
            if (IsAnimation) _lastAnimationArchive = _archiveIndex; else _lastScriptArchive = _archiveIndex;
            RaiseArchive();

            OpcodeNames.Clear();
            if (IsAnimation)
                foreach (BattleAnimCommand o in BattleAnimCommands.Table(_version)) OpcodeNames.Add(o.Name);
            else
                foreach (WazaSeqOp o in WazaSeqOpcodes.Table(_version)) OpcodeNames.Add(o.Name);
            OpcodeDisplayNames.Clear();
            foreach (string n in OpcodeNames) OpcodeDisplayNames.Add(DSPRE.Avalonia.Data.BattleAnimSchema.OpcodeDisplay(n, !IsAnimation));
            _nameToOp = null;   // opcode table changed → rebuild the text-parser's name→id map lazily

            BuildFileList();
            OnPropertyChanged(nameof(IsAnimation));
            OnPropertyChanged(nameof(IsHgEngineSource));
            OnPropertyChanged(nameof(SourceNote));
            OnPropertyChanged(nameof(ShowTextHelp));
            OnPropertyChanged(nameof(ArchiveNotAvailable));
            OnPropertyChanged(nameof(ArchiveUnavailableText));
            // Reset to the first entry of the new archive.
            _fileIndex = -1;
            SelectedFileIndex = FileItems.Count > 0 ? 0 : -1;
        }

        public bool ArchiveNotAvailable => IsAvailable && _archiveIndex >= 0 && !CurrentNarc.Available;
        public string ArchiveUnavailableText =>
            $"Not available for {GameFamily} yet.";

        // ── Entry (file) selection ────────────────────────────────────────────────
        public ObservableCollection<string> FileItems { get; } = new ObservableCollection<string>();

        public void RefreshNames()
        {
            _moveNames = SafeMoveNames();
            _speciesNames = null;
            OnPropertyChanged(nameof(SpeciesNames));
            if (!IsAvailable) return;
            List<string> labels = _sourceOrder != null && _sourceById != null
                ? _sourceOrder.Select(LabelFor).ToList()
                : Enumerable.Range(0, FileItems.Count).Select(LabelFor).ToList();
            DSPRE.Avalonia.Data.ListSync.Apply(FileItems, labels);
        }

        private void BuildFileList()
        {
            FileItems.Clear();
            _sourceById = null;
            if (!IsAvailable) return;

            IReadOnlyDictionary<int, HgEngineOwnedFile> owned = HgEngineOwnedFiles.FilesIn(HgEngineOwnedFiles.ArchiveOf(_dirs[_archiveIndex]));
            if (owned.Count > 0)
            {
                _sourceById = owned.Values
                    .Where(f => f.Ownership == HgEngineOwnership.EditableSource)
                    .ToDictionary(f => f.Id);
                _sourceOrder = _sourceById.Keys.OrderBy(id => id).ToList();
                foreach (int id in _sourceOrder) FileItems.Add(LabelFor(id));
                return;
            }

            if (!CurrentNarc.Available) return;
            int count = CurrentNarc.Count;
            for (int i = 0; i < count; i++) FileItems.Add(LabelFor(i));
        }

        // In source mode the list is the checkout's files, which need not be a full run from zero.
        private int EntryIdAt(int listIndex) =>
            _sourceById == null ? listIndex
            : listIndex >= 0 && listIndex < _sourceOrder.Count ? _sourceOrder[listIndex] : -1;

        public bool IsHgEngineSource => _sourceById != null;

        private HgEngineOwnedFile CurrentSource =>
            _sourceById != null && _sourceById.TryGetValue(EntryIdAt(_fileIndex), out HgEngineOwnedFile f) ? f : null;

        public string SourceNote => CurrentSource == null
            ? ""
            : $"From hg-engine's {CurrentSource.RelPath}. Edits go to that file.";

        private string _sourceText = "";
        public string SourceText
        {
            get => _sourceText;
            set { if (Set(ref _sourceText, value)) Dirty = value != _loadedSourceText; }
        }

        private string LabelFor(int i)
        {
            switch ((Archive)_archiveIndex)
            {
                case Archive.MoveScripts:
                case Archive.MoveAnimation:
                    return i < _moveNames.Length ? $"{i:D3} - {_moveNames[i]}" : $"{i:D3} - Move {i}";
                case Archive.EffectScripts:
                    return $"{i:D3} - Effect {i}";
                case Archive.SubAnimation:
                    return $"{i:D3} - Animation subroutine {i}";
                default:
                    return $"{i:D3} - Subroutine {i}";
            }
        }

        private int _fileIndex = -1;
        public int SelectedFileIndex
        {
            get => _fileIndex;
            set
            {
                if (RecordSwitchGuard.IsSnappingBack) return;
                if (value == _fileIndex) return;
                if (Dirty && _fileIndex >= 0)
                {
                    // Snap the list back to the entry still loaded until the user has answered.
                    int requested = value;
                    RecordSwitchGuard.SnapBack(() => _fileIndex, v => _fileIndex = v, () => OnPropertyChanged(nameof(SelectedFileIndex)));
                    _ = SwitchEntryAsync(() => { if (Set(ref _fileIndex, requested, nameof(SelectedFileIndex))) LoadEntry(); });
                    return;
                }
                if (Set(ref _fileIndex, value)) LoadEntry();
            }
        }

        private async System.Threading.Tasks.Task SwitchEntryAsync(Action go)
        {
            if (!await RecordSwitchGuard.ConfirmLeaveAsync(this, null, "script")) return;
            Dirty = false;
            go();
        }

        public bool HasUnsavedChanges => Dirty;
        public string UnsavedChangesDescription => _fileIndex < 0 ? "Battle script" : $"{ArchiveOptions[_archiveIndex]} {_fileIndex}";
        public void SaveChanges() => Save();

        public async System.Threading.Tasks.Task<bool> SaveChangesAsync()
        {
            if (!IsHgEngineSource && HasTextErrors)
            {
                await DSPRE.Avalonia.DialogHelper.ShowInfoAsync("The script has errors. Fix the underlined lines before saving.", "Fix errors first");
                return false;
            }
            Save();
            return !HasUnsavedChanges;
        }
        public void DiscardChanges() { Dirty = false; if (_fileIndex >= 0) LoadEntry(); }

        public string EntryHeader =>
            _fileIndex < 0 ? "(no entry selected)"
                           : $"{ArchiveOptions[_archiveIndex]} {_fileIndex}, {Plural.Of(Rows.Count, "command")}";

        // ── Command list ───────────────────────────────────────────────────────────
        public ObservableCollection<ScriptCmdRow> Rows { get; } = new ObservableCollection<ScriptCmdRow>();
        public bool HasRows => Rows.Count > 0;

        private bool _dirty;
        public bool Dirty
        {
            get => _dirty;
            private set { if (Set(ref _dirty, value)) OnPropertyChanged(nameof(HasUnsavedChanges)); }
        }

        private void LoadEntry()
        {
            Rows.Clear();
            if (IsHgEngineSource)
            {
                HgEngineOwnedFile source = CurrentSource;
                _sourceText = source == null ? ""
                    : HgEngineOwnedFiles.TryReadText(source, out string text, out string readError)
                        ? text : readError;
                _loadedSourceText = _sourceText;
                Dirty = false;
                OnPropertyChanged(nameof(SourceText));
                OnPropertyChanged(nameof(SourceNote));
                OnPropertyChanged(nameof(IsHgEngineSource));
                OnPropertyChanged(nameof(EntryHeader));
                return;
            }

            if (IsAvailable && _fileIndex >= 0 && CurrentNarc.Available)
            {
                byte[] bytes = CurrentNarc.Get(_fileIndex);
                List<WazaSeqCommand> cmds = bytes == null ? null
                         : IsAnimation ? BattleAnimScript.Parse(bytes, _version)
                                  : WazaSeqScript.Parse(bytes, _version);
                _tail = cmds?.LastOrDefault(c => c.Tail != null)?.Tail;
                if (cmds != null) foreach (WazaSeqCommand c in cmds.Where(c => !c.OnlyTail)) AddRow(c.OpId, c.Args, c.Raw);
            }
            _loadedBytes = _fileIndex >= 0 ? CurrentBytes() : null;
            _history.Reset(_loadedBytes);
            _shownBytes = _loadedBytes;
            RaiseUndo();
            Dirty = false;
            TextErrors = Array.Empty<TextError>();
            SyncTextFromRows();   // seed the text view from the freshly-loaded cards
            RefreshViewLines();
            RefreshStoryboard();
            SetupCellPreview();
            OnPropertyChanged(nameof(HasRows));
            OnPropertyChanged(nameof(EntryHeader));
        }

        // Bytes after the entry's last whole word, carried through a save.
        private byte[] _tail;

        // Steps over the entry's bytes, so card and text edits undo alike. hg-engine source text keeps the text box's own undo.
        private readonly UndoHistory<byte[]> _history = new UndoHistory<byte[]>();
        private byte[] _shownBytes;
        private DateTime _lastStep;

        public bool CanUndo => !IsHgEngineSource && _history.CanUndo;
        public bool CanRedo => !IsHgEngineSource && _history.CanRedo;
        public void Undo() { if (CanUndo) ShowBytes(_history.Undo()); }
        public void Redo() { if (CanRedo) ShowBytes(_history.Redo()); }

        private void RaiseUndo() { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); }

        private void Stepped()
        {
            if (IsHgEngineSource || _loadedBytes == null) return;
            byte[] now = CurrentBytes();
            if (_shownBytes != null && now.AsSpan().SequenceEqual(_shownBytes)) return;
            // Typing a value or a line is many edits; those within half a second are one step.
            DateTime at = DateTime.UtcNow;
            _history.Capture(now, coalesce: (at - _lastStep).TotalMilliseconds < 500);
            _lastStep = at;
            _shownBytes = now;
            RaiseUndo();
        }

        private void ShowBytes(byte[] bytes)
        {
            List<WazaSeqCommand> cmds = IsAnimation ? BattleAnimScript.Parse(bytes, _version) : WazaSeqScript.Parse(bytes, _version);
            Rows.Clear();
            foreach (WazaSeqCommand cmd in cmds.Where(cmd => !cmd.OnlyTail)) AddRow(cmd.OpId, cmd.Args, cmd.Raw);
            _shownBytes = bytes;
            _lastStep = default;
            TextErrors = Array.Empty<TextError>();
            SyncTextFromRows();
            Dirty = DiffersFromLoaded();
            RefreshViewLines();
            RefreshStoryboard();
            OnPropertyChanged(nameof(HasRows));
            OnPropertyChanged(nameof(EntryHeader));
            RaiseUndo();
        }

        // What the entry held when loaded, so undoing an edit back to it leaves nothing to save.
        private byte[] _loadedBytes;
        private string _loadedSourceText = "";

        private byte[] CurrentBytes()
        {
            List<WazaSeqCommand> cmds = BuildCommands();
            return IsAnimation ? BattleAnimScript.Serialize(cmds) : WazaSeqScript.Serialize(cmds);
        }

        private bool DiffersFromLoaded() => _loadedBytes == null || !CurrentBytes().AsSpan().SequenceEqual(_loadedBytes);

        private void AddRow(int opId, int[] args, bool raw = false)
        {
            ScriptCmdRow row = new ScriptCmdRow { Script = !IsAnimation, Raw = raw, OpNameOf = OpNameOf, ArgCountOf = ArgCountOf, OnEdited = OnRowEdited, PreviewSound = TryPreviewSound, SoundNameOf = SoundNameOf };
            row.Args.AddRange(args ?? System.Array.Empty<int>());
            row._opIdSilent(opId);   // set without firing OnEdited during load
            row.Rebuild();
            Rows.Add(row);
        }

        // opId → opcode name for the row labels (the dropdown index IS the opcode id).
        private string OpNameOf(int opId) => opId >= 0 && opId < OpcodeNames.Count ? OpcodeNames[opId] : "op" + opId;

        // The opcode's parameter count for these args (animation opcodes report only their fixed leading args; a
        // message command's count follows its tag).
        // Words the game reads after the opcode: an animation command's variable list adds the count it carries.
        private int ExpectedArgCount(int opId, IReadOnlyList<int> args)
        {
            if (!IsAnimation) return ArgCountOf(opId, args);
            if (!BattleAnimCommands.TryGet(_version, opId, out BattleAnimCommand op)) return -1;
            if (!op.IsVariable) return op.ArgCount;
            int count = op.CountIndex >= 0 && op.CountIndex < args.Count ? args[op.CountIndex] : 0;
            return op.ArgCount + Math.Max(0, count);
        }

        private int ArgCountOf(int opId, IReadOnlyList<int> args)
        {
            if (IsAnimation) return BattleAnimCommands.TryGet(_version, opId, out BattleAnimCommand op) ? op.ArgCount : 0;
            return Math.Max(0, WazaSeqOpcodes.ArgCount(_version, opId, i => i < args.Count ? args[i] : 0));
        }

        private void OnRowEdited(ScriptCmdRow row)
        {
            RefreshViewLines();
            Dirty = DiffersFromLoaded();
            RefreshStoryboard();
            SyncTextFromRows();
            Stepped();
            // A paused preview shows the same frame of the edited move.
            if (_holding) { int frame = PreviewFrame; _holding = false; SeekTo(frame); }
        }

        public void AddCommand()
        {
            AddRow(0, Array.Empty<int>());
            Dirty = DiffersFromLoaded();
            RefreshStoryboard();
            SyncTextFromRows();
            OnPropertyChanged(nameof(HasRows));
            OnPropertyChanged(nameof(EntryHeader));
            Stepped();
        }

        public void RemoveCommand(ScriptCmdRow row)
        {
            if (row == null || !Rows.Contains(row)) return;
            Rows.Remove(row);
            Dirty = DiffersFromLoaded();
            RefreshStoryboard();
            SyncTextFromRows();
            OnPropertyChanged(nameof(HasRows));
            OnPropertyChanged(nameof(EntryHeader));
            Stepped();
        }

        public void MoveCommand(ScriptCmdRow row, int dir)
        {
            int i = Rows.IndexOf(row), j = i + dir;
            if (i < 0 || j < 0 || j >= Rows.Count) return;
            Rows.Move(i, j);
            Dirty = DiffersFromLoaded();
            RefreshStoryboard();
            SyncTextFromRows();
            Stepped();
        }

        public void Save()
        {
            if (IsHgEngineSource) { DialogHelper.Watch(SaveSourceAsync()); return; }
            if (!IsAvailable || _fileIndex < 0 || !CurrentNarc.Available) return;
            if (HasTextErrors)   // the cards are stale while the text is invalid, don't persist the wrong thing
            {
                _ = DSPRE.Avalonia.DialogHelper.ShowInfoAsync("The script has errors. Fix the underlined lines before saving.", "Fix errors first");
                return;
            }
            List<WazaSeqCommand> cmds = BuildCommands();
            byte[] bytes = IsAnimation ? BattleAnimScript.Serialize(cmds) : WazaSeqScript.Serialize(cmds);
            if (!ReadsBackTheSame(cmds, bytes))
            {
                _ = DSPRE.Avalonia.DialogHelper.ShowErrorAsync("A command has the wrong number of values.", "Not saved");
                return;
            }
            CurrentNarc.Put(_fileIndex, bytes);
            LoadEntry();   // reflect the canonical form
        }

        private bool ReadsBackTheSame(List<WazaSeqCommand> written, byte[] bytes)
        {
            List<WazaSeqCommand> back = IsAnimation ? BattleAnimScript.Parse(bytes, _version) : WazaSeqScript.Parse(bytes, _version);
            List<WazaSeqCommand> a = written.Where(c => !c.OnlyTail).ToList();
            List<WazaSeqCommand> b = back.Where(c => !c.OnlyTail).ToList();
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (a[i].OpId != b[i].OpId || !(a[i].Args ?? Array.Empty<int>()).SequenceEqual(b[i].Args ?? Array.Empty<int>())) return false;
            return true;
        }

        private async System.Threading.Tasks.Task SaveSourceAsync()
        {
            HgEngineOwnedFile source = CurrentSource;
            if (source == null) return;

            if (!HgEngineOwnedFiles.TryWriteText(source, SourceText ?? "", out string error))
            {
                await DSPRE.Avalonia.DialogHelper.ShowErrorAsync(error, "Move animations & battle scripts");
                return;
            }
            _loadedSourceText = SourceText ?? "";
            Dirty = false;

            if (HgEngineProject.SuppressManagedFileSaveNotice) return;
            bool never = await DSPRE.Avalonia.DialogHelper.ShowNoticeWithOptOutAsync(
                $"{source.RelPath} saved. Build the ROM in hg-engine to see it in game.",
                "Managed by hg-engine");
            if (never) HgEngineProject.SuppressManagedFileSaveNoticeForProject();
        }

        /// <summary>The particle files this script loads, in load order, each with whether its slot draws orthographic.</summary>
        public IReadOnlyList<(int File, bool Orthographic)> ParticleFilesOfMove()
        {
            List<(int File, bool Orthographic)> found = new List<(int File, bool Orthographic)>();
            if (!IsAnimation) return found;
            Dictionary<int, int> projection = new Dictionary<int, int>();
            foreach (WazaSeqCommand c in BuildCommands())
            {
                string op = BattleAnimCommands.Name(_version, c.OpId);
                if (op == "SetCameraProjection" && c.Args.Length >= 2) projection[c.Args[0]] = c.Args[1];
                if (op is not ("LoadParticleSystem" or "LoadDebugParticleSystem") || c.Args.Length < 2) continue;
                bool ortho = !projection.TryGetValue(c.Args[0], out int p) || p != 0;
                if (!found.Exists(f => f.File == c.Args[1])) found.Add((c.Args[1], ortho));
            }
            return found;
        }

        /// <summary>The move being edited.</summary>
        public int FileIndex => _fileIndex;

        private List<WazaSeqCommand> BuildCommands()
        {
            List<WazaSeqCommand> list = new List<WazaSeqCommand>();
            foreach (ScriptCmdRow row in Rows)
            {
                int[] args;
                if (IsAnimation || row.Raw)
                {
                    // Animation commands are variable-length, and a raw row is words the reader didn't understand;
                    // either way the row's args are kept exactly.
                    args = row.Args.ToArray();
                }
                else
                {
                    // waza/be/sub opcodes are fixed-length or sized by a message tag; pad/truncate to that count.
                    int n = ArgCountOf(row.OpId, row.Args);
                    args = new int[n];
                    for (int i = 0; i < n; i++) args[i] = i < row.Args.Count ? row.Args[i] : 0;
                }
                list.Add(new WazaSeqCommand(row.OpId, args) { Raw = row.Raw });
            }
            if (_tail != null)
            {
                if (list.Count > 0) list[^1].Tail = _tail;
                else list.Add(new WazaSeqCommand(0, null) { Tail = _tail, OnlyTail = true });
            }
            return list;
        }

        private static List<int> ParseIntList(string s)
        {
            List<int> list = new List<int>();
            if (string.IsNullOrWhiteSpace(s)) return list;
            foreach (string p in s.Split(new[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string t = p.Trim();
                bool ok = t.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    ? int.TryParse(t.Substring(2), NumberStyles.HexNumber, null, out int v)
                    : int.TryParse(t, out v);
                if (ok) list.Add(v);
            }
            return list;
        }

        // ── Text ⇄ container two-way sync ───────────────────────────────────────────
        // Cards (Rows) and text (CommandsText) stay in sync; text only rebuilds the cards when it has no errors.
        private bool _pushingText;     // Rows→text: a programmatic CommandsText push (don't re-parse it)
        private bool _rebuildingRows;  // text→Rows: rebuilding the cards from text (don't regenerate the text)

        private string _commandsText = "";
        public string CommandsText
        {
            get => _commandsText;
            set { if (Set(ref _commandsText, value) && !_pushingText) ParseTextIntoRows(); }
        }

        public readonly struct TextError
        {
            public TextError(int offset, int length, string message) { Offset = offset; Length = length; Message = message; }
            public int Offset { get; } public int Length { get; } public string Message { get; }
        }
        private IReadOnlyList<TextError> _textErrors = Array.Empty<TextError>();
        public IReadOnlyList<TextError> TextErrors { get => _textErrors; private set { _textErrors = value; OnPropertyChanged(nameof(TextErrors)); OnPropertyChanged(nameof(HasTextErrors)); OnPropertyChanged(nameof(TextErrorSummary)); } }
        public bool HasTextErrors => _textErrors.Count > 0;
        public string TextErrorSummary => _textErrors.Count == 0 ? "" : Plural.Of(_textErrors.Count, "error") + ": " + _textErrors[0].Message;

        // Rows → text (called after any card edit). Suppressed while we are rebuilding the rows FROM text.
        private void SyncTextFromRows()
        {
            if (_rebuildingRows) return;
            _pushingText = true;
            CommandsText = RowsToText();
            _pushingText = false;
        }

        // Each line is "CommandName label=value ...", e.g. "AddParticles slot=0 data=482 behavior=3". Named args
        // take their slot; bare ones fill the next free slot. Raw opcode names and plain numbers parse too.
        private string RowsToText()
        {
            StringBuilder sb = new StringBuilder();
            foreach (ScriptCmdRow row in Rows)
            {
                string raw = OpNameOf(row.OpId);
                sb.Append(DSPRE.Avalonia.Data.BattleAnimSchema.CommandName(raw, !IsAnimation));
                for (int i = 0; i < row.Args.Count; i++)
                {
                    sb.Append(' ');
                    int v = row.Args[i];
                    string label = DSPRE.Avalonia.Data.BattleAnimSchema.ParamName(raw, i, !IsAnimation);
                    // An enum parameter shows its friendly value token; a generic "Param N" label is dropped
                    // (bare number) since a made-up name would add no meaning.
                    BattleAnimSchema.EnumOption[] opts = DSPRE.Avalonia.Data.BattleAnimSchema.EnumFor(raw, i);
                    string valText = v.ToString(CultureInfo.InvariantCulture);
                    if (opts != null)
                        foreach (BattleAnimSchema.EnumOption o in opts) if (o.Value == v) { valText = DSPRE.Avalonia.Data.BattleAnimSchema.Token(o.Label, true); break; }
                    if (label.StartsWith("Param ", StringComparison.Ordinal)) sb.Append(valText);
                    else sb.Append(DSPRE.Avalonia.Data.BattleAnimSchema.ArgToken(raw, i, !IsAnimation)).Append('=').Append(valText);
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }

        private Dictionary<string, int> _nameToOp;
        // Maps BOTH the raw opcode identifier and its single-word command name → opId (== table index), so a
        // text line can start with either. Built once per version.
        private Dictionary<string, int> NameToOp()
        {
            Dictionary<string, int> d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < OpcodeNames.Count; i++)
            {
                d[OpcodeNames[i]] = i;   // index == opId
                string cmd = DSPRE.Avalonia.Data.BattleAnimSchema.CommandName(OpcodeNames[i], !IsAnimation);
                if (!string.IsNullOrEmpty(cmd) && !d.ContainsKey(cmd)) d[cmd] = i;   // single-word name (first wins on collision)
            }
            return d;
        }

        private static bool TryParseWord(string t, out int v)
        {
            t = t.Trim();
            return t.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? int.TryParse(t.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v)
                : int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out v);
        }

        // Resolve a friendly enum value token (e.g. "AttackerSide", "ConvergeToPoint") back to its engine value
        // for an enum-typed parameter; the old spaced label is still accepted too.
        private static bool TryResolveEnum(string rawOpName, int argIndex, string token, out int value)
        {
            value = 0;
            BattleAnimSchema.EnumOption[] opts = DSPRE.Avalonia.Data.BattleAnimSchema.EnumFor(rawOpName, argIndex);
            if (opts == null) return false;
            foreach (BattleAnimSchema.EnumOption o in opts)
            {
                if (string.Equals(o.Label, token, StringComparison.OrdinalIgnoreCase)) { value = o.Value; return true; }
                if (string.Equals(DSPRE.Avalonia.Data.BattleAnimSchema.Token(o.Label, true), token, StringComparison.OrdinalIgnoreCase)) { value = o.Value; return true; }
            }
            return false;
        }

        private static bool TryParseArgValue(string rawOpName, int argIndex, string token, out int v)
            => TryParseWord(token, out v) || TryResolveEnum(rawOpName, argIndex, token, out v);

        // Finds the argument index whose single-word label (or, for a payload slot with no known name, "paramN"/
        // "argN") matches the given token by scanning only the opcode's KNOWN fixed labels (stops at the first
        // generic "Param N" fallback, since anything past that is unnamed variable payload).
        private static int ResolveArgIndex(string rawOpName, string label, bool script)
        {
            string digits = label.Length > 5 && label.StartsWith("param", StringComparison.OrdinalIgnoreCase) ? label.Substring(5)
                           : label.Length > 3 && label.StartsWith("arg", StringComparison.OrdinalIgnoreCase) ? label.Substring(3)
                           : null;
            if (digits != null && int.TryParse(digits, out int n) && n >= 1) return n - 1;
            for (int i = 0; i < 32; i++)
            {
                string pn = DSPRE.Avalonia.Data.BattleAnimSchema.ParamName(rawOpName, i, script);
                if (pn.StartsWith("Param ", StringComparison.Ordinal)) break;
                if (string.Equals(DSPRE.Avalonia.Data.BattleAnimSchema.ArgToken(rawOpName, i, script), label, StringComparison.OrdinalIgnoreCase)) return i;
            }
            return -1;
        }

        // Text → Rows. Collects per-token errors (offset/length for squiggles). Rebuilds the cards ONLY when clean.
        private void ParseTextIntoRows()
        {
            List<TextError> errors = new List<TextError>();
            List<(int opId, int[] args)> parsed = new List<(int opId, int[] args)>();
            _nameToOp ??= NameToOp();

            int offset = 0;
            foreach (string rawLine in _commandsText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                int lineStart = offset;
                offset += rawLine.Length + 1;   // + the newline we split on
                string line = rawLine.Trim();
                if (line.Length == 0) continue;                                   // blank line = no command
                if (line.StartsWith("//") || line.StartsWith("#")) continue;      // allow comment lines

                // Command = the first whitespace/comma-separated token; the rest are its arguments.
                string[] toks = line.Split(new[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);
                string cmdName = toks[0];

                if (!_nameToOp.TryGetValue(cmdName, out int opId))
                {
                    int col = rawLine.IndexOf(cmdName, StringComparison.Ordinal);
                    errors.Add(new TextError(lineStart + Math.Max(0, col), cmdName.Length, $"Unknown command '{cmdName}'"));
                    continue;
                }
                string raw = OpNameOf(opId);

                bool argOk = true;
                int searchFrom = Math.Max(0, rawLine.IndexOf(cmdName, StringComparison.Ordinal) + cmdName.Length);
                Dictionary<int, int> slotValue = new Dictionary<int, int>();
                List<(string tok, int col)> bareToks = new List<(string tok, int col)>();

                // Pass 1: named "label=value" tokens claim their specific slot, wherever they appear on the line.
                for (int i = 1; i < toks.Length; i++)
                {
                    string tok = toks[i];
                    int col = rawLine.IndexOf(tok, searchFrom, StringComparison.Ordinal);
                    if (col >= 0) searchFrom = col + tok.Length;

                    int eq = tok.IndexOf('=');
                    if (eq <= 0) { bareToks.Add((tok, col)); continue; }

                    string label = tok.Substring(0, eq);
                    string valTok = tok.Substring(eq + 1);
                    int idx = ResolveArgIndex(raw, label, !IsAnimation);
                    if (idx < 0)
                    {
                        errors.Add(new TextError(lineStart + Math.Max(0, col), tok.Length, $"Unknown argument '{label}'"));
                        argOk = false; continue;
                    }
                    if (!TryParseArgValue(raw, idx, valTok, out int v))
                    {
                        errors.Add(new TextError(lineStart + Math.Max(0, col), tok.Length, $"'{valTok}' isn't a number or known value"));
                        argOk = false; continue;
                    }
                    slotValue[idx] = v;
                }

                // Pass 2: bare tokens fill whichever slots are left, in order.
                int cursor = 0;
                foreach ((string tok, int col) in bareToks)
                {
                    while (slotValue.ContainsKey(cursor)) cursor++;
                    if (!TryParseArgValue(raw, cursor, tok, out int v))
                    {
                        errors.Add(new TextError(lineStart + Math.Max(0, col < 0 ? 0 : col), tok.Length, $"'{tok}' isn't a number or known value"));
                        argOk = false; cursor++; continue;
                    }
                    slotValue[cursor] = v;
                    cursor++;
                }

                if (argOk)
                {
                    int maxIdx = -1;
                    foreach (int k in slotValue.Keys) if (k > maxIdx) maxIdx = k;
                    int[] args = new int[maxIdx + 1];
                    foreach (KeyValuePair<int, int> kv in slotValue) args[kv.Key] = kv.Value;
                    // A wrong count shifts every word after it, so the game and the next load read other commands.
                    int expected = ExpectedArgCount(opId, args);
                    if (expected >= 0 && args.Length != expected)
                    {
                        int col = Math.Max(0, rawLine.IndexOf(cmdName, StringComparison.Ordinal));
                        errors.Add(new TextError(lineStart + col, rawLine.TrimEnd().Length - col,
                            $"{cmdName} takes {Plural.Of(expected, "value")} but this line has {args.Length}"));
                        continue;
                    }
                    parsed.Add((opId, args));
                }
            }

            TextErrors = errors;
            // Keep the cards at the last good state, but count the typed text as an edit so leaving asks first.
            if (errors.Count > 0) { Dirty = true; return; }

            _rebuildingRows = true;
            Rows.Clear();
            foreach ((int opId, int[] args) in parsed) AddRow(opId, args);
            _rebuildingRows = false;
            Dirty = DiffersFromLoaded();
            RefreshStoryboard();
            OnPropertyChanged(nameof(HasRows));
            OnPropertyChanged(nameof(EntryHeader));
            Stepped();
        }

        private string _storyboard = "";
        public string Storyboard { get => _storyboard; private set => Set(ref _storyboard, value); }
        private IReadOnlyList<BattleAnimStoryboard.Line> _storyboardLines = Array.Empty<BattleAnimStoryboard.Line>();
        public IReadOnlyList<BattleAnimStoryboard.Line> StoryboardLines { get => _storyboardLines; private set => Set(ref _storyboardLines, value); }
        public bool ShowStoryboard => IsAvailable && HasRows;
        public string StoryboardTitle => IsAnimation ? "Animation storyboard" : "Effect summary";

        // The storyboard is a column of numbers and stays unwrapped; the effect summary is prose, so it wraps.
        public global::Avalonia.Media.TextWrapping StoryboardWrap =>
            IsAnimation ? global::Avalonia.Media.TextWrapping.NoWrap : global::Avalonia.Media.TextWrapping.Wrap;

        private int _previewFrame = -1;
        /// <summary>The frame the preview shows, or -1 when nothing has played.</summary>
        public int PreviewFrame { get => _previewFrame; private set { if (_previewFrame != value) { _previewFrame = value; OnPropertyChanged(); OnPropertyChanged(nameof(FrameText)); } } }
        public string FrameText => _timeline == null ? "" : $"Frame {Math.Max(0, _previewFrame)} of {_timeline.TotalPasses}, {_timeline.TotalPasses / 30.0:0.00} s";

        /// <summary>Shows one frame of the move, paused there. Runs on silently from the current frame, or from the start
        /// for an earlier frame. The fixed seed gives the same picture either way.</summary>
        public void SeekTo(int frame)
        {
            if (!HasPreview) return;
            if (IsCellPlaying) Pause();
            frame = Math.Max(0, frame);
            if (!_holding || _animPlayer == null || _animPlayer.Pass > frame)
            {
                List<WazaSeqCommand> cmds = BuildCommands();
                LoadCellResourcesForCommands(cmds, _fileIndex);
                _animPlayer = NewPlayer(cmds, _fileIndex);
                _cellFrameIdx = 0; _cellTick = 0; _cellLoops = 0; _previewFrames = 0;
                CellPreview = HasCellAnimation && _cellFrames.Count > 0 ? _cellFrames[0].Bitmap : null;
                _holding = true;
            }
            SetSoundsOn(_animPlayer, false);
            while (!_animPlayer.Finished && _animPlayer.Pass < frame && _animPlayer.Pass < MaxPreviewFrames) StepPass();
            ShowPass();
            RaiseNotes();
            RaisePreviewProps();
        }

        public void StepFrame(int delta) => SeekTo(Math.Max(0, PreviewFrame) + delta);
        public void SeekToEnd() => SeekTo(_timeline?.TotalPasses ?? MaxPreviewFrames);

        /// <summary>Stops on the frame shown, so Play carries on from it.</summary>
        public void Pause()
        {
            if (!IsCellPlaying) return;
            _previewTimer.Stop();
            StopPreviewSounds();
            _holding = true;
            RaisePreviewProps();
        }

        private bool _loop;
        public bool Loop { get => _loop; set => Set(ref _loop, value); }

        private static readonly double[] Speeds = { 1, 0.5, 0.25 };
        public string[] SpeedNames { get; } = { "Normal speed", "Half speed", "Quarter speed" };
        private int _speedIndex;
        /// <summary>Slower playback is silent, since the sounds can't slow down with it.</summary>
        public int SpeedIndex
        {
            get => _speedIndex;
            set
            {
                if (value < 0 || value >= Speeds.Length || !Set(ref _speedIndex, value)) return;
                if (!IsCellPlaying) return;
                _passesRun = 0; _clock.Restart();
                if (Speeds[value] != 1) StopPreviewSounds();
                SetSoundsOn(_animPlayer, Speeds[value] == 1);
            }
        }

        private void SetSoundsOn(BattleAnimPlayer player, bool on)
        {
            if (player == null) return;
            on &= _soundsOn;
            player.PlaySound = on ? PreviewSound : null;
            player.PlayCry = on ? PreviewCry : null;
            player.StopSound = on ? PreviewStopSound : null;
        }

        /// <summary>Opens the card of the command a timeline bar came from.</summary>
        public void PickCommand(int command)
        {
            if (command < 0 || command >= Rows.Count) return;
            for (int i = 0; i < Rows.Count; i++) Rows[i].IsExpanded = i == command;
            PickedCommand = command;
        }

        private int _pickedCommand = -1;
        /// <summary>The command last picked on the timeline, for the view to bring into sight.</summary>
        public int PickedCommand { get => _pickedCommand; private set { _pickedCommand = value; OnPropertyChanged(); } }

        private BattleAnimTrace _timeline;
        /// <summary>The move's run recorded silently, with its bars labelled, for the timeline; null for other scripts.</summary>
        public BattleAnimTrace Timeline { get => _timeline; private set { _timeline = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasTimeline)); OnPropertyChanged(nameof(FrameText)); OnPropertyChanged(nameof(ShowStoryboardText)); } }
        /// <summary>The written storyboard shows for scripts with no timeline.</summary>
        public bool ShowStoryboardText => ShowStoryboard && _timeline == null;
        public bool HasTimeline => _timeline != null;

        private short[] RenderedSoundPcm(int soundId) =>
            _soundPcm.TryGetValue(soundId, out Lazy<short[]> pcm) && pcm.IsValueCreated ? pcm.Value : null;

        private void RecordTimeline(List<WazaSeqCommand> cmds)
        {
            if (!IsAnimation || cmds.Count == 0 || _fileIndex < 0) { Timeline = null; return; }
            BattleAnimTrace trace = RecordTrace(cmds, null);
            if (trace == null) { Timeline = null; return; }
            foreach (BattleAnimTrace.Item item in trace.Items) LabelTimelineItem(item, cmds);
            Timeline = trace;
        }

        // A silent run of the commands. With stopAfter, it ends once that bar has finished.
        private BattleAnimTrace RecordTrace(List<WazaSeqCommand> cmds, Func<BattleAnimTrace, bool> stopAfter)
        {
            try
            {
                BattleAnimPlayer player = NewPlayer(cmds, _fileIndex, live: false);
                while (!player.Finished && player.Pass < MaxPreviewFrames && (stopAfter == null || !stopAfter(player.Trace))) player.Step();
                return player.Trace;
            }
            catch (Exception ex) { AppLogger.WarnOnce("BattleScriptEditorViewModel.RecordTrace", ex); return null; }
        }

        /// <summary>Finds the value of a bar's command that moves its end (or the whole bar) in step, by running the
        /// move again with each value nudged. Null when no single value does.</summary>
        public BattleAnimTrace.Knob FindKnob(BattleAnimTrace.Item item, bool movesBar)
        {
            if (_timeline == null || IsHgEngineSource || item == null || item.Command < 0 || item.Command >= Rows.Count) return null;
            ScriptCmdRow row = Rows[item.Command];
            if (row.Raw) return null;
            List<WazaSeqCommand> cmds = BuildCommands();
            WazaSeqCommand c = cmds[item.Command];
            string op = BattleAnimCommands.Name(_version, c.OpId) ?? "";
            int ordinal = OrdinalOf(_timeline, item);
            // A routine's first two values pick the routine and count its values.
            int first = op == "CallFunc" ? 2 : 0;
            BattleAnimTrace.Knob best = null;
            // The finest value wins: a fade's hold moves the end a pass at a time, its step length many.
            for (int a = first; a < c.Args.Length; a++)
            {
                ParamVM param = row.ParamAt(a);
                if (param != null && (param.IsEnum || param.IsSound)) continue;
                int v = c.Args[a];
                if (v < 0) continue;
                int d1 = Nudged(cmds, item, ordinal, a, v + 1, movesBar);
                if (d1 <= 0 || (best != null && d1 >= best.FramesPerStep)) continue;
                if (Nudged(cmds, item, ordinal, a, v + 2, movesBar) != 2 * d1) continue;
                if (Nudged(cmds, item, ordinal, a, v + 3, movesBar) != 3 * d1) continue;
                bool byteValue = op != "CallFunc";
                best = new BattleAnimTrace.Knob
                {
                    Command = item.Command, Arg = a, Value = v, Min = 0, Max = byteValue ? 255 : Math.Max(v, 999),
                    FramesPerStep = d1, MovesBar = movesBar,
                    Name = DSPRE.Avalonia.Data.BattleAnimSchema.ParamLabel(row.OpName, a, row.Args),
                };
                if (d1 == 1) break;
            }
            return best;
        }

        // How far the matching bar's end moved, or its start if the whole bar moved, with one value changed. 0 when the
        // bar changed length some other way or is gone.
        private int Nudged(List<WazaSeqCommand> cmds, BattleAnimTrace.Item item, int ordinal, int arg, int value, bool movesBar)
        {
            WazaSeqCommand c = cmds[item.Command];
            int[] args = (int[])c.Args.Clone();
            args[arg] = value;
            List<WazaSeqCommand> nudged = new List<WazaSeqCommand>(cmds);
            nudged[item.Command] = new WazaSeqCommand(c.OpId, args) { WordPos = c.WordPos, Raw = c.Raw, Tail = c.Tail };
            BattleAnimTrace.Item found = null;
            BattleAnimTrace trace = RecordTrace(nudged, t =>
            {
                found ??= Match(t, item, ordinal);
                return found != null && found.End < t.TotalPasses;
            });
            found ??= trace == null ? null : Match(trace, item, ordinal);
            if (found == null) return 0;
            int dStart = found.Start - item.Start, dEnd = found.End - item.End;
            if (movesBar) return dStart == dEnd ? dStart : 0;
            return dStart == 0 ? dEnd : 0;
        }

        private static int OrdinalOf(BattleAnimTrace trace, BattleAnimTrace.Item item)
        {
            int n = 0;
            foreach (BattleAnimTrace.Item i in trace.Items)
            {
                if (ReferenceEquals(i, item)) return n;
                if (i.Lane == item.Lane && i.Command == item.Command) n++;
            }
            return -1;
        }

        private static BattleAnimTrace.Item Match(BattleAnimTrace trace, BattleAnimTrace.Item like, int ordinal)
        {
            int n = 0;
            foreach (BattleAnimTrace.Item i in trace.Items)
                if (i.Lane == like.Lane && i.Command == like.Command && n++ == ordinal) return i;
            return null;
        }

        /// <summary>Writes a dragged value into its card, as one undo step.</summary>
        public void SetKnob(BattleAnimTrace.Knob knob, int value)
        {
            if (knob == null || knob.Command < 0 || knob.Command >= Rows.Count) return;
            value = Math.Clamp(value, knob.Min, knob.Max);
            if (value == knob.Value) return;
            _lastStep = default;
            Rows[knob.Command].SetArg(knob.Arg, value);
        }

        private void LabelTimelineItem(BattleAnimTrace.Item item, List<WazaSeqCommand> cmds)
        {
            WazaSeqCommand c = item.Command >= 0 && item.Command < cmds.Count ? cmds[item.Command] : null;
            string op = c == null ? "" : BattleAnimCommands.Name(_version, c.OpId) ?? "";
            string command = item.Command >= 0 && item.Command < Rows.Count ? Rows[item.Command].OpDisplay : op;
            string routine = op == "CallFunc" && c.Args.Length > 0 ? BattleAnimScriptDisplay.RoutineName(c.Args[0]) : command;
            item.Label = item.Lane switch
            {
                BattleAnimTrace.Lane.Script => op == "Delay" && c.Args.Length > 0 ? $"{command} {c.Args[0]}" : command,
                BattleAnimTrace.Lane.Particles => $"Slot {item.A}, emitter {item.B}",
                BattleAnimTrace.Lane.Sound => ShortSoundName(item.A),
                _ => routine,
            };
            int frames = item.End - item.Start;
            item.Tip = $"{item.Label}\nFrame {item.Start}, {Plural.Of(frames, "frame")}" + (command != item.Label ? $"\n{command}" : "");
        }

        private string ShortSoundName(int soundId)
        {
            string name = SoundNameOf(soundId);
            if (string.IsNullOrEmpty(name)) return $"Sound {soundId}";
            foreach (string prefix in new[] { "SEQ_SE_DP_", "SEQ_SE_PL_", "SEQ_SE_", "SEQ_" })
                if (name.StartsWith(prefix, StringComparison.Ordinal)) return name.Substring(prefix.Length);
            return name;
        }

        private void RefreshStoryboard()
        {
            OnPropertyChanged(nameof(ShowStoryboard));
            OnPropertyChanged(nameof(StoryboardTitle));
            OnPropertyChanged(nameof(StoryboardWrap));
            if (!HasRows) { Storyboard = ""; StoryboardLines = Array.Empty<BattleAnimStoryboard.Line>(); return; }
            List<WazaSeqCommand> cmds = BuildCommands();
            RecordTimeline(cmds);
            if (IsAnimation)
            {
                List<BattleAnimStoryboard.Line> lines = BattleAnimStoryboard.Build(cmds, _version);
                StoryboardLines = lines;
                Storyboard = lines.Count == 0 ? "(empty script)" : "";
            }
            else
            {
                StoryboardLines = Array.Empty<BattleAnimStoryboard.Line>();
                Storyboard = WazaSeqStoryboard.Build(cmds, _version);
            }
        }

        private readonly WeCellAnimRenderer _cellRenderer = new WeCellAnimRenderer();
        private IReadOnlyList<WeCellAnimRenderer.Frame> _cellFrames = Array.Empty<WeCellAnimRenderer.Frame>();
        private readonly ScriptNarc _particleNarc = new ScriptNarc(DirNames.WazaParticle);
        private BattleAnimPlayer _animPlayer;
        private DispatcherTimer _previewTimer;
        private int _cellFrameIdx, _cellTick, _cellLoops, _previewFrames;
        private const int MaxPreviewFrames = 1200;   // safety cap in script passes

        // The UI timer fires irregularly, so passes are counted from a stopwatch, not one per callback.
        private readonly System.Diagnostics.Stopwatch _clock = new System.Diagnostics.Stopwatch();
        private long _passesRun;

        // A move's script runs once per pass of the game's main loop, which waits two screen frames per pass.
        // 60 is kept only to preview a hack that runs it faster; the setting is shared with the Battle display.
        private static int PreviewFps => DSPRE.SettingsManager.Settings?.BattlePreviewFps == 60 ? 60 : 30;

        public bool HasCellAnimation { get; private set; }
        public bool HasParticleAnimation { get; private set; }
        public bool HasPreview => IsAnimation && _fileIndex >= 0;
        public string CellAnimNote { get; private set; } = "";

        /// <summary>
        /// What this move does that the preview does not show, gathered from the player while it runs.
        /// </summary>
        public string PreviewNotes => _animPlayer == null || _animPlayer.Notes.Count == 0
            ? "" : "Not drawn: " + string.Join(", ", _animPlayer.Notes);

        public bool HasPreviewNotes => PreviewNotes.Length > 0;

        private int _notesShown;

        private Bitmap _cellPreview;
        public Bitmap CellPreview { get => _cellPreview; private set => Set(ref _cellPreview, value); }
        private Bitmap _backgroundFrame;
        public Bitmap BackgroundFrame { get => _backgroundFrame; private set { if (Set(ref _backgroundFrame, value)) { OnPropertyChanged(nameof(BackgroundBehind)); OnPropertyChanged(nameof(BackgroundOver)); } } }
        private bool _bgOverlay;
        public bool BackgroundIsOverlay { get => _bgOverlay; private set { if (Set(ref _bgOverlay, value)) { OnPropertyChanged(nameof(BackgroundBehind)); OnPropertyChanged(nameof(BackgroundOver)); } } }
        public Bitmap BackgroundBehind => _bgOverlay ? null : _backgroundFrame;   // backdrop-replace (Fly/Dig/Cosmic)
        public Bitmap BackgroundOver => _bgOverlay ? _backgroundFrame : null;     // effect overlay (Surf water sweep)
        private double _cellSX = 1, _cellSY = 1, _cellOpacity = 1, _cellOX, _cellOY;
        public double CellScaleX { get => _cellSX; private set => Set(ref _cellSX, value); }
        public double CellScaleY { get => _cellSY; private set => Set(ref _cellSY, value); }
        public double CellOpacity { get => _cellOpacity; private set => Set(ref _cellOpacity, value); }
        public double CellOffsetX { get => _cellOX; private set => Set(ref _cellOX, value); }
        public double CellOffsetY { get => _cellOY; private set => Set(ref _cellOY, value); }
        // The sprite's content centre, so Surf's wave scales in place.
        private global::Avalonia.RelativePoint _cellOrigin = global::Avalonia.RelativePoint.Center;
        public global::Avalonia.RelativePoint CellOrigin { get => _cellOrigin; private set => Set(ref _cellOrigin, value); }
        public bool IsCellPlaying => _previewTimer != null && _previewTimer.IsEnabled;
        // Paused or stepped to a frame: the move's state stays on screen.
        private bool _holding;
        private bool ShowsMove => IsCellPlaying || _holding;
        public bool CanStopPreview => ShowsMove;
        public string CellPlayButtonText => IsCellPlaying ? "Pause" : "Play";
        public string CellPlayButtonIcon => IsCellPlaying ? null : "play";
        private double _bgDarken; public double BackgroundDarken { get => _bgDarken; private set => Set(ref _bgDarken, value); }
        private IBrush _fadeBrush = Brushes.Black; public IBrush FadeBrush { get => _fadeBrush; private set => Set(ref _fadeBrush, value); }
        private double _shakeX; public double ShakeX { get => _shakeX; private set => Set(ref _shakeX, value); }
        private double _shakeY; public double ShakeY { get => _shakeY; private set => Set(ref _shakeY, value); }
        private double _pRot, _pScaleX = 1, _pScaleY = 1, _pTintA, _eRot, _eScaleX = 1, _eScaleY = 1, _eTintA, _pDX, _pDY, _eDX, _eDY;
        private bool _pVis = true, _eVis = true;
        private IBrush _tintBrush = Brushes.Transparent;
        public double PlayerOffsetX { get => _pDX; private set => Set(ref _pDX, value); }
        public double PlayerOffsetY { get => _pDY; private set => Set(ref _pDY, value); }
        public double EnemyOffsetX { get => _eDX; private set => Set(ref _eDX, value); }
        public double EnemyOffsetY { get => _eDY; private set => Set(ref _eDY, value); }
        public double PlayerRotation { get => _pRot; private set => Set(ref _pRot, value); }
        public double PlayerScaleX { get => _pScaleX; private set => Set(ref _pScaleX, value); }
        public double PlayerScaleY { get => _pScaleY; private set => Set(ref _pScaleY, value); }
        public bool PlayerVisible { get => _pVis; private set => Set(ref _pVis, value); }
        public double PlayerTintOpacity { get => _pTintA; private set => Set(ref _pTintA, value); }
        public double EnemyRotation { get => _eRot; private set => Set(ref _eRot, value); }
        public double EnemyScaleX { get => _eScaleX; private set => Set(ref _eScaleX, value); }
        public double EnemyScaleY { get => _eScaleY; private set => Set(ref _eScaleY, value); }
        public bool EnemyVisible { get => _eVis; private set => Set(ref _eVis, value); }
        public double EnemyTintOpacity { get => _eTintA; private set => Set(ref _eTintA, value); }
        public IBrush TintBrush { get => _tintBrush; private set => Set(ref _tintBrush, value); }

        // Backdrop + mon sprites are positioned like the Battle display tab (sprite-Y offset + heights),
        // reusing its VM, then composited with the real NDS blend so the GX blend is exact. Particles/cell/chrome
        // stay as overlays on top.
        private readonly BattleSceneCompositor _compositor = new BattleSceneCompositor();
        private Bitmap _sceneComposite;
        public Bitmap SceneComposite { get => _sceneComposite; private set => Set(ref _sceneComposite, value); }

        private bool _sceneLoaded;
        private Bitmap _enemySprite, _playerSprite;
        public Bitmap EnemySprite { get => _enemySprite; private set => Set(ref _enemySprite, value); }
        public Bitmap PlayerSprite { get => _playerSprite; private set => Set(ref _playerSprite, value); }
        public double EnemyLeft { get; private set; } = 152;
        public double PlayerLeft { get; private set; } = 23;
        public double EnemyTop { get; private set; } = 24;
        public double PlayerTop { get; private set; } = 84;

        // Emitter screen anchors = sprite centres (80×80 cell → +40), derived from the loaded positions.
        private double _atX = 63, _atY = 124, _dfX = 192, _dfY = 64;
        private int _backYOffset, _frontYOffset;

        private bool _attackerIsEnemy;
        public bool AttackerIsEnemy
        {
            get => _attackerIsEnemy;
            set { if (Set(ref _attackerIsEnemy, value)) { bool was = IsCellPlaying; SetupCellPreview(); if (was) ToggleCellPlay(); } }
        }

        /// <summary>The weather the move's weather check sees, in the game's order.</summary>
        public List<string> WeatherNames { get; } = new List<string> { "None", "Rain", "Sandstorm", "Sun", "Hail" };
        private int _weatherIndex;
        public int WeatherIndex
        {
            get => _weatherIndex;
            set { if (Set(ref _weatherIndex, Math.Max(0, value))) { bool was = IsCellPlaying; SetupCellPreview(); if (was) ToggleCellPlay(); } }
        }

        /// <summary>Whether the open move checks the weather, so the choice is only offered when it means something.</summary>
        public bool HasWeatherCheck => UsesCommand("JumpIfWeather");

        /// <summary>Only Diamond, Pearl and Platinum have contests, and only some moves branch for them.</summary>
        public bool HasContestCheck => GameFamily != GameFamilies.HGSS
            && (UsesCommand("JumpIfContest") || UsesCommand("JumpIfContestCondition"));

        private bool _contest;
        /// <summary>Plays the move as a contest appeal: its contest branch and the contestants' positions.</summary>
        public bool Contest
        {
            get => _contest;
            set { if (Set(ref _contest, value)) { bool was = IsCellPlaying; SetupCellPreview(); if (was) ToggleCellPlay(); } }
        }

        private bool _soundsOn = true;
        public bool SoundsOn
        {
            get => _soundsOn;
            set
            {
                if (!Set(ref _soundsOn, value)) return;
                if (!value) StopPreviewSounds();
                SetSoundsOn(_animPlayer, Speeds[_speedIndex] == 1);
            }
        }

        private int _volume = 100;
        /// <summary>Sound volume in percent, applied as each sound starts.</summary>
        public int Volume
        {
            get => _volume;
            set => Set(ref _volume, Math.Clamp(value, 0, 100));
        }

        private bool UsesCommand(string name)
        {
            List<WazaSeqCommand> cmds = BuildCommands();
            if (cmds == null) return false;
            foreach (WazaSeqCommand c in cmds)
                if (BattleAnimCommands.Name(_version, c.OpId) == name) return true;
            return false;
        }

        // Some moves have two animations; the game alternates them by turn count.
        private bool _secondTurnVariant;
        public bool SecondTurnVariant
        {
            get => _secondTurnVariant;
            set { if (Set(ref _secondTurnVariant, value)) { bool was = IsCellPlaying; SetupCellPreview(); if (was) ToggleCellPlay(); } }
        }

        /// <summary>Whether the open move has a second animation at all, so the choice is only offered when it means something.</summary>
        public bool HasTurnCheck => UsesCommand("JumpByTurn");

        private void EnsureScene()
        {
            if (_sceneLoaded) return;
            _sceneLoaded = true;
            ApplyBackdrop();   // backdrop, platforms + gauges don't depend on the displayed species
            ApplyGround();
            RenderGauges();
            LoadDisplayMon(_gaugeSpeciesId);
        }

        // Loads the front/back battle sprites for species `id` into the preview (+ their positions) and feeds them to the
        // compositor. Re-callable so the gauge-config species dropdown can swap the displayed Pokémon live.
        private void LoadDisplayMon(int id)
        {
            try
            {
                PokemonSpriteEditorViewModel sv = new PokemonSpriteEditorViewModel(true);
                sv.LoadMon(id);
                BattleDisplayEditorViewModel bd = new BattleDisplayEditorViewModel(sv);
                bd.LoadMon(id);
                bd.Detach();       // we only want its computed positions, stop its preview timer

                EnemySprite = bd.EnemySprite ?? First(sv.BattleFrontM) ?? First(sv.BattleFrontF);
                PlayerSprite = bd.PlayerSprite ?? First(sv.BattleBackM) ?? First(sv.BattleBackF);
                EnemyLeft = bd.EnemyLeft; EnemyTop = bd.EnemyTop;
                PlayerLeft = bd.PlayerLeft; PlayerTop = bd.PlayerTop;
                _dfX = EnemyLeft + 40; _dfY = EnemyTop + 40;
                _atX = PlayerLeft + 40; _atY = PlayerTop + 40;
                _backYOffset = bd.HasHeights ? bd.BackHeightM : 0;
                _frontYOffset = bd.HasHeights ? bd.FrontHeightM : 0;

                OnPropertyChanged(nameof(EnemySprite)); OnPropertyChanged(nameof(PlayerSprite));
                OnPropertyChanged(nameof(EnemyLeft)); OnPropertyChanged(nameof(EnemyTop));
                OnPropertyChanged(nameof(PlayerLeft)); OnPropertyChanged(nameof(PlayerTop));

                if (PlayerSprite != null) { (byte[] px, int pw, int ph) = ToRgba(PlayerSprite); _compositor.SetPlayer(px, pw, ph, (int)PlayerLeft, (int)PlayerTop); }
                if (EnemySprite != null) { (byte[] ex, int ew, int eh) = ToRgba(EnemySprite); _compositor.SetEnemy(ex, ew, eh, (int)EnemyLeft, (int)EnemyTop); }
                if (IsAnimation && _sceneLoaded && !IsCellPlaying) SceneComposite = _compositor.Render(null);
            }
            catch { /* no ROM / sprite, backdrop just shows the scene without the mons */ }

            static Bitmap First(System.Collections.Generic.IReadOnlyList<Bitmap> l) => l != null && l.Count > 0 ? l[0] : null;
        }

        private void AddStatic(string asset, int left, int top)
        {
            Bitmap bmp = LoadAsset(asset);
            if (bmp == null) return;
            (byte[] rgba, int w, int h) = ToRgba(bmp);
            _compositor.AddStatic(rgba, w, h, left, top);
        }

        // ── Configurable battle background (real ROM data) ──────────────────────────────────────────────────────
        private DSPRE.Avalonia.Data.BattleBgRenderer _bgRenderer;
        private System.Collections.Generic.List<string> _backgroundOptions;
        public System.Collections.Generic.List<string> BackgroundOptions => _backgroundOptions ??= BuildBackgroundOptions();
        private static System.Collections.Generic.List<string> BuildBackgroundOptions()
        {
            List<string> l = new System.Collections.Generic.List<string> { "No background" };
            for (int i = 0; i < DSPRE.Avalonia.Data.BattleBgRenderer.BackdropCount; i++) l.Add($"Backdrop {i}");
            return l;
        }
        // Start on grass, not black: on black every move that swaps the backdrop looks brighter than in game.
        private int _backgroundIndex = 1;
        public int BackgroundIndex
        {
            get => _backgroundIndex;
            set
            {
                if (!Set(ref _backgroundIndex, value)) return;
                ApplyBackdrop();
                if (IsAnimation && _sceneLoaded && !IsCellPlaying) SceneComposite = _compositor.Render(null);
            }
        }

        // ── Configurable terrain ground platforms (real ROM data, battle/graphic/pl_batt_obj.narc) ──────────────
        private DSPRE.Avalonia.Data.BattleGroundRenderer _groundRenderer;
        private System.Collections.Generic.List<string> _terrainOptions;
        /// <summary>Dropdown: "Placeholder" (the bundled platform PNGs) + each terrain (Gravel, Sand, Lawn,
        /// Pool, Rock, Cave, Snow, Water, Ice, Floor). Picking one renders the real in-game ground "tray" the Pokémon
        /// stand on (battle/), which move animations interact with, from pl_batt_obj.narc.</summary>
        public System.Collections.Generic.List<string> TerrainOptions => _terrainOptions ??= BuildTerrainOptions();
        private static System.Collections.Generic.List<string> BuildTerrainOptions()
        {
            List<string> l = new System.Collections.Generic.List<string> { "Placeholder platforms" };
            l.AddRange(DSPRE.Avalonia.Data.BattleGroundRenderer.TerrainNames);
            return l;
        }
        private int _terrainIndex = 3;   // Lawn, the ordinary grass battle
        public int TerrainIndex
        {
            get => _terrainIndex;
            set
            {
                if (!Set(ref _terrainIndex, value)) return;
                ApplyGround();
                // Pick the matching backdrop for the terrain; the user can still change it. Index 0 leaves it alone.
                if (_terrainIndex > 0)
                {
                    int bg = DSPRE.Avalonia.Data.BattleGroundRenderer.BackdropForTerrain(_terrainIndex - 1);
                    if (bg >= 0) BackgroundIndex = bg + 1;   // +1: option 0 is the bundled image
                }
                if (IsAnimation && _sceneLoaded && !IsCellPlaying) SceneComposite = _compositor.Render(null);
            }
        }

        /// <summary>Rebuilds the ground platforms: the bundled placeholder PNGs for index 0, otherwise the real
        /// pl_batt_obj terrain "tray" (mine + enemy, at the game's platform positions). Falls back to the
        /// placeholders if the ROM/NARC is unavailable so the scene always has a floor.</summary>
        private void ApplyGround()
        {
            _compositor.ClearStatics();
            bool placed = false;
            if (_terrainIndex > 0)
            {
                try
                {
                    (BattleGroundRenderer.GroundImage mine, BattleGroundRenderer.GroundImage enemy) = (_groundRenderer ??= new DSPRE.Avalonia.Data.BattleGroundRenderer()).Build(_terrainIndex - 1);
                    if (enemy?.Rgba != null) { _compositor.AddStatic(enemy.Rgba, enemy.Width, enemy.Height, enemy.Left, enemy.Top); placed = true; }
                    if (mine?.Rgba != null) { _compositor.AddStatic(mine.Rgba, mine.Width, mine.Height, mine.Left, mine.Top); placed = true; }
                }
                catch (Exception ex) { AppLogger.WarnOnce("BattleScriptEditorViewModel.ApplyGround", ex); placed = false; }
            }
            if (!placed) { AddStatic("platform_opponent.png", 129, 72); AddStatic("platform_you.png", -42, 122); }
        }

        // ── Real HP gauges (pl_batt_obj frames) + configurable readout + per-move UI-hide ──────────────────────────
        private Bitmap _gaugePlayerImage, _gaugeEnemyImage;
        public Bitmap GaugePlayerImage { get => _gaugePlayerImage; private set => Set(ref _gaugePlayerImage, value); }
        public Bitmap GaugeEnemyImage { get => _gaugeEnemyImage; private set => Set(ref _gaugeEnemyImage, value); }
        public bool HasRealGauges => _gaugePlayerImage != null || _gaugeEnemyImage != null;
        public bool ShowPlaceholderGauges => !HasRealGauges;

        // Move flag 0x40 keeps the gauges up during the animation; 0x80 hides the shadow.
        private bool _hideGaugesThisMove, _hideShadowThisMove;
        public bool GaugesVisible => !(ShowsMove && _hideGaugesThisMove);
        public bool ShadowHidden => ShowsMove && _hideShadowThisMove;
        public bool RealGaugesVisible => HasRealGauges && GaugesVisible;
        public bool PlaceholderGaugesVisible => !HasRealGauges && GaugesVisible;
        public double PlayerGaugeImageLeft => Data.BattleGaugeComposer.CentreOf(Data.BattleGaugeComposer.Kind.PlayerSingle).X - 128;
        public double PlayerHealthFillLeft => Data.BattleGaugeComposer.HealthFillLeft(Data.BattleGaugeComposer.Kind.PlayerSingle);

        private int GetMoveFlagField(int moveId = -1)
        {
            if (moveId < 0) moveId = _fileIndex;
            if ((Archive)_archiveIndex != Archive.MoveAnimation || moveId < 0) return -1;
            try { return new MoveData(moveId).FlagField; }
            catch (Exception ex) { AppLogger.WarnOnce("BattleScriptEditorViewModel.GetMoveFlagField", ex); return -1; }
        }

        // Configurable gauge readout (the preview mons are placeholders, so these are user-set, default Shuckle/Lv42/100%).
        private int _gaugeHpPercent = 100, _gaugeLevel = 42, _gaugeMaxHp = 250;
        private int _gaugeSpeciesId = 213;   // Shuckle
        public int GaugeHpPercent { get => _gaugeHpPercent; set { if (Set(ref _gaugeHpPercent, Math.Clamp(value, 0, 100))) RaiseHpProps(); } }
        public int GaugeMaxHp { get => _gaugeMaxHp; set { if (Set(ref _gaugeMaxHp, Math.Max(1, value))) RaiseHpProps(); } }
        public int GaugeCurHp => (int)Math.Round(_gaugeMaxHp * _gaugeHpPercent / 100.0);
        public string GaugeHpText => GaugeCurHp + "/" + _gaugeMaxHp;
        private void RaiseHpProps() { OnPropertyChanged(nameof(GaugeHpBarWidth)); OnPropertyChanged(nameof(GaugeHpBrush)); OnPropertyChanged(nameof(GaugeCurHp)); OnPropertyChanged(nameof(GaugeHpText)); }
        public int GaugeLevel { get => _gaugeLevel; set { if (Set(ref _gaugeLevel, Math.Clamp(value, 1, 100))) { OnPropertyChanged(nameof(GaugeLevelText)); OnPropertyChanged(nameof(GaugeLevelImage)); RenderGauges(); } } }

        // The Pokémon shown in the preview (and named on the gauge); pick it from a dropdown. Changing it
        // live-loads that species' front/back battle sprites into the scene.
        private string[] _speciesNames;
        public string[] SpeciesNames => _speciesNames ??= SafeSpeciesNames();
        private static string[] SafeSpeciesNames() { try { return GetPokemonNames(); } catch (Exception ex) { AppLogger.WarnOnce("BattleScriptEditorViewModel.SafeSpeciesNames", ex); return Array.Empty<string>(); } }
        public int GaugeSpeciesIndex
        {
            get => _gaugeSpeciesId;
            set
            {
                if (!Set(ref _gaugeSpeciesId, value)) return;
                OnPropertyChanged(nameof(GaugeNameText));
                OnPropertyChanged(nameof(GaugeNameImage));
                RenderGauges();
                if (_sceneLoaded) LoadDisplayMon(value);
            }
        }
        public string GaugeNameText
        {
            get
            {
                string[] n = SpeciesNames;
                string s = (_gaugeSpeciesId >= 0 && _gaugeSpeciesId < n.Length) ? n[_gaugeSpeciesId] : "SHUCKLE";
                return (s ?? "").ToUpperInvariant();
            }
        }
        public string GaugeLevelText => "Lv" + _gaugeLevel;

        // The name and level as the game itself draws them, shared with the other two battle previews.
        public bool GaugeTextIsReal => Data.GaugeTextImages.Available;
        public global::Avalonia.Media.Imaging.Bitmap GaugeNameImage => Data.GaugeTextImages.Name(GaugeNameText);
        public global::Avalonia.Media.Imaging.Bitmap GaugeLevelImage =>
            Data.GaugeTextImages.Level(_gaugeLevel, ROMFiles.BattleGaugeText.Gender.Genderless);
        public global::Avalonia.Media.Imaging.Bitmap GaugeHealthImage =>
            Data.GaugeTextImages.Health((int)Math.Round(_gaugeMaxHp * _gaugeHpPercent / 100.0), _gaugeMaxHp);
        public double GaugeHpBarWidth => 48.0 * _gaugeHpPercent / 100.0;        // groove is 48px, measured off real battles
        // All three read off real Platinum battles, by watching the player's bar take damage across fourteen
        // recordings. Gold and OrangeRed, which stood in for the last two, were both too bright.
        private static IBrush Bar(byte r, byte g, byte b) =>
            new SolidColorBrush(global::Avalonia.Media.Color.FromRgb(r, g, b));
        private static readonly IBrush GaugeGreen = Bar(0x18, 0xC3, 0x20);
        private static readonly IBrush GaugeAmber = Bar(0xEB, 0xAA, 0x00);
        private static readonly IBrush GaugeRed = Bar(0xFB, 0x41, 0x10);
        public IBrush GaugeHpBrush => _gaugeHpPercent > 50 ? GaugeGreen : _gaugeHpPercent > 20 ? GaugeAmber : GaugeRed;
        /// <summary>HGSS gauges are cream frames with dark text; DPPt frames are dark with white text.</summary>
        public IBrush GaugeTextBrush => GameFamily == GameFamilies.HGSS
            ? new SolidColorBrush(global::Avalonia.Media.Color.FromRgb(0x28, 0x28, 0x28))
            : new SolidColorBrush(global::Avalonia.Media.Color.FromRgb(0xF8, 0xF8, 0xF8));

        /// <summary>Off, then the twenty frames the player can pick in Options.</summary>
        public List<string> MessageBoxNames { get; } = MessageBoxNameList();
        private static List<string> MessageBoxNameList()
        {
            List<string> names = new List<string> { "Off" };
            for (int i = 0; i < ROMFiles.FieldWindowFrame.FrameCount; i++) names.Add($"Frame {i + 1}");
            return names;
        }

        private int _messageBoxIndex = 1;
        public int MessageBoxIndex
        {
            get => _messageBoxIndex;
            set { if (Set(ref _messageBoxIndex, Math.Max(0, value))) RenderMessage(); }
        }

        private Bitmap _messageImage;
        /// <summary>The move's "X used Y!" line in the battle's message box; null shows the plain box.</summary>
        public Bitmap MessageImage
        {
            get => _messageImage;
            private set { if (Set(ref _messageImage, value)) OnPropertyChanged(nameof(PlainMessageBoxVisible)); }
        }
        public bool PlainMessageBoxVisible => _messageImage == null && _messageBoxIndex > 0;

        private void RenderMessage()
        {
            OnPropertyChanged(nameof(PlainMessageBoxVisible));
            if (_messageBoxIndex <= 0 || (Archive)_archiveIndex != Archive.MoveAnimation || _fileIndex <= 0) { MessageImage = null; return; }
            try
            {
                string[] names = SpeciesNames;
                string name = _gaugeSpeciesId >= 0 && _gaugeSpeciesId < names.Length ? names[_gaugeSpeciesId] : "";
                byte[] rgba = BattleMessageText.MoveUsed(_fileIndex, _attackerIsEnemy, name, _messageBoxIndex - 1);
                MessageImage = rgba == null ? null : ImageConverter.FromRgba(rgba, 256, 192);
            }
            catch (Exception ex) { AppLogger.WarnOnce("BattleScriptEditorViewModel.RenderMessage", ex); MessageImage = null; }
        }

        private void RenderGauges()
        {
            try
            {
                // The name and level go into each bar's own picture, the way a battle writes them.
                // Games whose letters cannot be read fall back to the plain bar.
                BattleGroundRenderer r = _groundRenderer ??= new DSPRE.Avalonia.Data.BattleGroundRenderer();
                GaugePlayerImage = Data.GaugeTextImages.Bar(true, GaugeNameText, _gaugeLevel)
                                ?? Gauge(r.BuildGauge(true));
                GaugeEnemyImage = Data.GaugeTextImages.Bar(false, GaugeNameText, _gaugeLevel)
                               ?? Gauge(r.BuildGauge(false));
                RenderMessage();
                OnPropertyChanged(nameof(HasRealGauges));
                OnPropertyChanged(nameof(ShowPlaceholderGauges));
                OnPropertyChanged(nameof(RealGaugesVisible));
                OnPropertyChanged(nameof(PlaceholderGaugesVisible));
                OnPropertyChanged(nameof(PlayerGaugeImageLeft));
                OnPropertyChanged(nameof(PlayerHealthFillLeft));
            }
            catch (Exception ex) { AppLogger.Warn("Battle script preview gauges: " + ex.Message); }
        }

        private static Bitmap Gauge(DSPRE.Avalonia.Data.BattleGroundRenderer.GroundImage g) =>
            g?.Rgba == null ? null : ImageConverter.FromRgba(g.Rgba, g.Width, g.Height);

        /// <summary>Loads the selected backdrop into the compositor: the bundled PNG for index 0, otherwise the
        /// real pl_batt_bg background (cropped to the 256×192 scene). Falls back to the PNG if the ROM/NARC is
        /// unavailable.</summary>
        private void ApplyBackdrop()
        {
            byte[] rgb = null;
            if (_backgroundIndex > 0)
            {
                try
                {
                    BattleBgRenderer.BgImage img = (_bgRenderer ??= new DSPRE.Avalonia.Data.BattleBgRenderer()).BuildBackdrop(_backgroundIndex - 1);
                    if (img?.Rgba != null) rgb = BgToBackdrop(img.Rgba, img.Width, img.Height);
                }
                catch (Exception ex) { AppLogger.WarnOnce("BattleScriptEditorViewModel.ApplyBackdrop", ex); rgb = null; }
            }
            // Index 0 is deliberately "No background" (plain black), NOT the bundled placeholder art:
            // effects are easiest to judge against black, and it's honest about not being ROM data.
            _compositor.SetBackdrop(rgb ?? new byte[256 * 192 * 3]);
        }

        // RGBA w×h (battle BG, usually 256×256) → opaque RGB 256×192 backdrop (top-left crop, black-padded).
        private static byte[] BgToBackdrop(byte[] rgba, int w, int h)
        {
            byte[] outp = new byte[256 * 192 * 3];
            for (int y = 0; y < 192; y++)
                for (int x = 0; x < 256; x++)
                {
                    int di = (y * 256 + x) * 3;
                    if (x < w && y < h)
                    {
                        int si = (y * w + x) * 4;
                        outp[di] = rgba[si]; outp[di + 1] = rgba[si + 1]; outp[di + 2] = rgba[si + 2];
                    }
                }
            return outp;
        }

        private static Bitmap LoadAsset(string name)
        {
            try { return new Bitmap(global::Avalonia.Platform.AssetLoader.Open(new System.Uri($"avares://DSPRE.Avalonia/Avalonia/Assets/Battle/{name}"))); }
            catch (Exception ex) { AppLogger.WarnOnce("BattleScriptEditorViewModel.LoadAsset", ex); return null; }
        }

        // Avalonia Bitmap → straight RGBA (un-premultiplied). CopyPixels gives BGRA premultiplied.
        private static (byte[] rgba, int w, int h) ToRgba(Bitmap bmp)
        {
            int w = bmp.PixelSize.Width, h = bmp.PixelSize.Height;
            byte[] buf = new byte[w * h * 4];
            GCHandle handle = System.Runtime.InteropServices.GCHandle.Alloc(buf, System.Runtime.InteropServices.GCHandleType.Pinned);
            try { bmp.CopyPixels(new global::Avalonia.PixelRect(0, 0, w, h), handle.AddrOfPinnedObject(), buf.Length, w * 4); }
            finally { handle.Free(); }
            byte[] rgba = new byte[w * h * 4];
            for (int i = 0; i < w * h; i++)
            {
                byte b = buf[i * 4], g = buf[i * 4 + 1], r = buf[i * 4 + 2], a = buf[i * 4 + 3];
                if (a > 0 && a < 255) { r = (byte)System.Math.Min(255, r * 255 / a); g = (byte)System.Math.Min(255, g * 255 / a); b = (byte)System.Math.Min(255, b * 255 / a); }
                rgba[i * 4] = r; rgba[i * 4 + 1] = g; rgba[i * 4 + 2] = b; rgba[i * 4 + 3] = a;
            }
            return (rgba, w, h);
        }

        private static byte[] LoadRgb(string name, int w, int h)
        {
            Bitmap bmp = LoadAsset(name);
            if (bmp == null) return null;
            (byte[] rgba, int bw, int bh) = ToRgba(bmp);
            byte[] rgb = new byte[w * h * 3];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int sx = bw == w ? x : x * bw / w, sy = bh == h ? y : y * bh / h;   // nearest-fit if sizes differ
                    int si = (sy * bw + sx) * 4, di = (y * w + x) * 3;
                    rgb[di] = rgba[si]; rgb[di + 1] = rgba[si + 1]; rgb[di + 2] = rgba[si + 2];
                }
            return rgb;
        }

        private void SetupCellPreview()
        {
            StopCell();
            PreviewFrame = -1;
            if (IsAnimation) { EnsureScene(); SceneComposite = _compositor.Render(null); }   // static scene (backdrop + mons)
            HasCellAnimation = false; HasParticleAnimation = false;
            CellPreview = null; CellAnimNote = "";
            RenderMessage();
            _animPlayer = null; ShakeX = ShakeY = 0; BackgroundDarken = 0;
            if (IsAnimation && _fileIndex >= 0)
            {
                List<WazaSeqCommand> cmds = BuildCommands();
                LoadCellResourcesForCommands(cmds, _fileIndex);
                // Render the move's sounds as soon as it opens, so Play starts at once.
                RecordTimeline(cmds);
                // Once they're rendered, the timeline is recorded again, since their lengths set how long the move waits.
                if (LoadSdat() != null)
                    System.Threading.Tasks.Task.Run(() => RenderScriptSounds(cmds))
                        .ContinueWith(_ => Dispatcher.UIThread.Post(() => RecordTimeline(BuildCommands())));

                int emitters = BattleAnimParticles.Extract(cmds, _version, _attackerIsEnemy).Count;
                HasParticleAnimation = emitters > 0 && _particleNarc.Available;

                CellAnimNote = "";
            }
            RaisePreviewProps();
        }

        private void LoadCellResourcesForCommands(List<WazaSeqCommand> cmds, int moveIdForLogging)
        {
            HasCellAnimation = false;
            BattleAnimSpriteResources res = BattleAnimSprites.Extract(cmds, _version);
            if (res.HasCellAnimation)
            {
                bool loaded = _cellRenderer.Load(res.Char, res.Pltt, res.Cell, res.CellAnm);
                if (loaded)
                {
                    // Surf picks the sequence by side; 1 is the enemy's flipped wave.
                    int bank = _attackerIsEnemy && _cellRenderer.AnimationCount > 1 ? 1 : 0;
                    _cellFrames = _cellRenderer.RenderAnimation(bank);
                    if (_cellFrames.Count > 0)
                    {
                        HasCellAnimation = true;   // shown only on Play, not as a static poster
                        CellOrigin = new global::Avalonia.RelativePoint(
                            _cellRenderer.ContentCx / 256.0, _cellRenderer.ContentCy / 192.0,
                            global::Avalonia.RelativeUnit.Relative);
                    }
                }
                AppLogger.Info($"Move animation {moveIdForLogging} sprites: char={res.Char} pltt={res.Pltt} cell={res.Cell} " +
                    $"anm={res.CellAnm} load={loaded} banks={_cellRenderer.AnimationCount} frames={_cellFrames.Count}");
            }
            else
            {
                // The renderer is shared between moves; without this a move would draw the previous move's sprite.
                _cellRenderer.Unload();
                _cellFrames = Array.Empty<WeCellAnimRenderer.Frame>();
                AppLogger.Info($"Move animation {moveIdForLogging}: no sprites (char={res.Char} pltt={res.Pltt} " +
                    $"cell={res.Cell} anm={res.CellAnm})");
            }
        }

        // The game picks Metronome's move in battle code, so the preview picks one itself after the finger wag.
        private const int MetronomeMoveId = 118;
        private bool IsMetronomePreview => IsAnimation && _fileIndex == MetronomeMoveId;
        private static readonly Random MetronomeRandom = new Random();
        private int _metronomeCalledMoveId = -1;

        /// <summary>Picks a move for Metronome's preview to call. Skips only Metronome, move 0 and scripts that
        /// don't parse, not the game's full exclusion table.</summary>
        private int PickRandomMetronomeTarget()
        {
            int count = CurrentNarc.Count;
            if (count <= 1) return -1;
            for (int attempt = 0; attempt < 20; attempt++)
            {
                int id = MetronomeRandom.Next(1, count);
                if (id == MetronomeMoveId) continue;
                byte[] bytes = CurrentNarc.Get(id);
                if (bytes != null && bytes.Length >= 4) return id;
            }
            return -1;
        }

        public void ToggleCellPlay()
        {
            if (!HasPreview) return;
            if (IsCellPlaying) { Pause(); return; }
            if (_holding && _animPlayer != null && !_animPlayer.Finished) { StartClock(); return; }
            PlayFromStart();
        }

        public void StopPreview() { if (IsCellPlaying || _holding) StopCell(); }

        private void StartClock()
        {
            SetSoundsOn(_animPlayer, Speeds[_speedIndex] == 1);
            _holding = false;
            _previewTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(5) };
            _previewTimer.Tick -= PreviewTick; _previewTimer.Tick += PreviewTick;
            _passesRun = 0;
            _clock.Restart();
            _previewTimer.Start();
            RaisePreviewProps();
        }

        private void PlayFromStart(bool cutSounds = true)
        {
            _holding = false;
            _cellFrameIdx = 0; _cellTick = 0; _cellLoops = 0; _previewFrames = 0;
            // A previous play may have chained into Metronome's move and left its sprites loaded.
            List<WazaSeqCommand> cmds = BuildCommands();
            LoadCellResourcesForCommands(cmds, _fileIndex);
            if (HasCellAnimation && _cellFrames.Count > 0) CellPreview = _cellFrames[0].Bitmap;
            _notesShown = 0;
            if (cutSounds) StopPreviewSounds();
            _animPlayer = NewPlayer(cmds, _fileIndex);
            _metronomeCalledMoveId = IsMetronomePreview ? PickRandomMetronomeTarget() : -1;
            ShowPass();
            StartClock();
        }

        private BattleAnimPlayer NewPlayer(List<WazaSeqCommand> cmds, int moveId) => NewPlayer(cmds, moveId, live: true);

        // A live player plays its sounds and waits for them to render; a recording one stays silent and counts only the
        // sounds already rendered, and is recorded again when the rest are ready.
        private BattleAnimPlayer NewPlayer(List<WazaSeqCommand> cmds, int moveId, bool live)
        {
            if (live) RenderScriptSounds(cmds);
            double aX = _attackerIsEnemy ? _dfX : _atX, aY = _attackerIsEnemy ? _dfY : _atY;
            double dX = _attackerIsEnemy ? _atX : _dfX, dY = _attackerIsEnemy ? _atY : _dfY;
            BattleAnimPlayer player = new BattleAnimPlayer(cmds, _version, _particleNarc, aX, aY, dX, dY,
                                   attackerIsEnemy: _attackerIsEnemy, selfTarget: IsSelfTargetMove(moveId))
            { SecondTurnVariant = _secondTurnVariant, Weather = _weatherIndex, Contest = _contest && HasContestCheck };
            player.Cells = _cellRenderer;
            if (live && _soundsOn)
            {
                player.PlaySound = PreviewSound;
                player.PlayCry = PreviewCry;
                player.StopSound = PreviewStopSound;
            }
            if (live)
            {
                player.SoundPasses = id => PassesOf(SoundPcm(id));
                player.CryPasses = () => PassesOf(CryPcm());
            }
            else
            {
                player.SoundPasses = id => PassesOf(RenderedSoundPcm(id));
                player.CryPasses = () => _cryPcmSpecies == _gaugeSpeciesId ? PassesOf(_cryPcm) : 0;
            }
            player.MovePower = GetMovePower(moveId);
            player.SpriteYOffsets[0] = _backYOffset;
            player.SpriteYOffsets[1] = _frontYOffset;
            int moveFlag = GetMoveFlagField(moveId);
            _hideGaugesThisMove = moveFlag >= 0 && (moveFlag & 0x40) == 0;
            _hideShadowThisMove = moveFlag >= 0 && (moveFlag & 0x80) != 0;
            return player;
        }

        private void StartChainedAnimation(int moveId)
        {
            byte[] bytes = CurrentNarc.Get(moveId);
            List<WazaSeqCommand> cmds = bytes != null ? BattleAnimScript.Parse(bytes, _version) : new List<WazaSeqCommand>();
            LoadCellResourcesForCommands(cmds, moveId);
            _animPlayer = NewPlayer(cmds, moveId);
            _cellFrameIdx = 0; _cellTick = 0; _cellLoops = 0;
            if (HasCellAnimation && _cellFrames.Count > 0) CellPreview = _cellFrames[0].Bitmap;

            string calledName = moveId >= 0 && moveId < _moveNames.Length ? _moveNames[moveId] : $"Move {moveId}";
            CellAnimNote = $"Metronome called {calledName}!";
            RaisePreviewProps();
        }

        // A move whose range targets the user (User / User-side / User-or-ally bits) plays its effect on the caster:
        // in-game df_client == at_client. Only meaningful for the move-animation archive (file index = move id).
        // The move's base power (MoveData.damage) when previewing the move-animation archive, else −1 (unknown).
        private int GetMovePower(int moveId = -1)
        {
            if (moveId < 0) moveId = _fileIndex;
            if ((Archive)_archiveIndex != Archive.MoveAnimation || moveId < 0) return -1;
            try { return new MoveData(moveId).Damage; }
            catch (Exception ex) { AppLogger.WarnOnce("BattleScriptEditorViewModel.GetMovePower", ex); return -1; }
        }

        private bool IsSelfTargetMove(int moveId = -1)
        {
            if (moveId < 0) moveId = _fileIndex;
            if ((Archive)_archiveIndex != Archive.MoveAnimation || moveId < 0) return false;
            try
            {
                MoveData move = new MoveData(moveId);
                return BattleAnimTarget.DefenderIsUser(move.Target, move.Battleeffect);
            }
            catch (Exception ex) { AppLogger.WarnOnce("BattleScriptEditorViewModel.IsSelfTargetMove", ex); return false; }
        }

        private void PreviewTick(object sender, EventArgs e)
        {
            if (!IsCellPlaying) return;
            long due = (long)(_clock.Elapsed.TotalSeconds * PreviewFps * Speeds[_speedIndex]);
            if (due - _passesRun > 4) _passesRun = due - 1;   // after a stall, resume instead of fast-forwarding
            if (_passesRun >= due) return;
            while (IsCellPlaying && _passesRun < due)
            {
                _passesRun++;
                if (!RunPass()) return;
            }
            if (IsCellPlaying) ShowPass();
        }

        private void StepPass()
        {
            // Actors are drawn into SceneComposite, so the standalone cell overlay would draw them twice.
            bool cellsViaActors = _animPlayer != null && _animPlayer.SpriteActors.Count > 0;
            if (!cellsViaActors && HasCellAnimation && _cellFrames.Count > 0 && ++_cellTick >= _cellFrames[_cellFrameIdx].Duration)
            {
                _cellTick = 0;
                if (_cellFrameIdx + 1 >= _cellFrames.Count) _cellLoops++;
                _cellFrameIdx = (_cellFrameIdx + 1) % _cellFrames.Count;
                CellPreview = _cellFrames[_cellFrameIdx].Bitmap;
            }
            if (_animPlayer != null)
            {
                if (cellsViaActors && CellOpacity != 0) CellOpacity = 0;
                _animPlayer.Step();
            }
        }

        // Notes appear only when their command runs, so the panel is updated as the move goes.
        private void RaiseNotes()
        {
            if (_animPlayer == null || _animPlayer.Notes.Count == _notesShown) return;
            _notesShown = _animPlayer.Notes.Count;
            OnPropertyChanged(nameof(PreviewNotes));
            OnPropertyChanged(nameof(HasPreviewNotes));
        }

        // One script pass. False when playback stopped or moved on to a chained move.
        private bool RunPass()
        {
            StepPass();
            RaiseNotes();
            bool done = _animPlayer != null ? _animPlayer.Finished : (_cellLoops >= 1);
            if (done && _metronomeCalledMoveId >= 0)
            {
                int calledMoveId = _metronomeCalledMoveId;
                _metronomeCalledMoveId = -1;   // only chain once; the called move doesn't itself call another
                _previewFrames = 0;
                StartChainedAnimation(calledMoveId);
                return false;   // keep the timer running for the chained move instead of stopping
            }
            // At the end the move's last sounds play out, as they do in the game.
            if (done && _loop) { PlayFromStart(cutSounds: false); return false; }
            if (done || ++_previewFrames >= MaxPreviewFrames) { StopCell(stopSounds: false); return false; }
            return true;
        }

        private WriteableBitmap[] _frameBuffers;
        private int _frameBuffer;
        private static WriteableBitmap NewFrameBuffer() =>
            new WriteableBitmap(new global::Avalonia.PixelSize(256, 192), new global::Avalonia.Vector(96, 96),
                global::Avalonia.Platform.PixelFormat.Bgra8888, global::Avalonia.Platform.AlphaFormat.Premul);

        // Draws the latest pass once, however many passes the clock had to catch up.
        private void ShowPass()
        {
            if (_animPlayer != null)
            {
                // One picture a frame: the scene with the particles on top, alternating between two bitmaps so the
                // image gets a new source each frame without allocating.
                _frameBuffers ??= new[] { NewFrameBuffer(), NewFrameBuffer() };
                _frameBuffer ^= 1;
                _compositor.RenderInto(_frameBuffers[_frameBuffer], _animPlayer, _animPlayer.RenderParticlesBgra());
                SceneComposite = _frameBuffers[_frameBuffer];
                PreviewFrame = _animPlayer.Pass;
                ShakeX = _animPlayer.ShakeX; ShakeY = _animPlayer.ShakeY;
                BackgroundDarken = _animPlayer.FadeOpacity;
                if (_animPlayer.FadeOpacity > 0) FadeBrush = new SolidColorBrush(Color.FromRgb(_animPlayer.FadeR, _animPlayer.FadeG, _animPlayer.FadeB));
                PlayerOffsetX = _animPlayer.MonDX[0] + _animPlayer.MonShakeX[0]; PlayerOffsetY = _animPlayer.MonDY[0] + _animPlayer.MonShakeY[0];
                PlayerRotation = _animPlayer.MonRot[0]; PlayerScaleX = _animPlayer.MonScaleX[0]; PlayerScaleY = _animPlayer.MonScaleY[0];
                PlayerVisible = _animPlayer.MonVisible[0]; PlayerTintOpacity = _animPlayer.MonTintA[0];
                EnemyOffsetX = _animPlayer.MonDX[1] + _animPlayer.MonShakeX[1]; EnemyOffsetY = _animPlayer.MonDY[1] + _animPlayer.MonShakeY[1];
                EnemyRotation = _animPlayer.MonRot[1]; EnemyScaleX = _animPlayer.MonScaleX[1]; EnemyScaleY = _animPlayer.MonScaleY[1];
                EnemyVisible = _animPlayer.MonVisible[1]; EnemyTintOpacity = _animPlayer.MonTintA[1];
                if (_animPlayer.MonTintA[0] > 0 || _animPlayer.MonTintA[1] > 0)
                    TintBrush = new SolidColorBrush(Color.FromRgb(_animPlayer.TintR, _animPlayer.TintG, _animPlayer.TintB));
            }
        }

        private void StopCell(bool stopSounds = true)
        {
            _previewTimer?.Stop();
            _holding = false;
            if (stopSounds) StopPreviewSounds();
            _metronomeCalledMoveId = -1;   // manually stopped mid-Metronome; don't chain into it on a later, unrelated play
            BackgroundFrame = null;
            CellPreview = null;   // don't leave the last wave frame statically on screen
            // Re-render the static scene: the last played frame may have mons hidden / dragged (Dark Void
            // vanishes the defender mid-effect). Without this the frozen composite keeps them invisible
            // after playback ends.
            if (IsAnimation && _sceneLoaded) SceneComposite = _compositor.Render(null);
            CellScaleX = CellScaleY = CellOpacity = 1;
            ShakeX = ShakeY = 0; BackgroundDarken = 0;
            PlayerOffsetX = PlayerOffsetY = EnemyOffsetX = EnemyOffsetY = 0;
            PlayerRotation = EnemyRotation = 0; PlayerScaleX = PlayerScaleY = EnemyScaleX = EnemyScaleY = 1;
            PlayerTintOpacity = EnemyTintOpacity = 0; PlayerVisible = EnemyVisible = true;
            OnPropertyChanged(nameof(IsCellPlaying));
            OnPropertyChanged(nameof(CellPlayButtonText)); OnPropertyChanged(nameof(CellPlayButtonIcon));
            OnPropertyChanged(nameof(CanStopPreview));
            OnPropertyChanged(nameof(GaugesVisible));
            OnPropertyChanged(nameof(ShadowHidden));
            OnPropertyChanged(nameof(RealGaugesVisible));
            OnPropertyChanged(nameof(PlaceholderGaugesVisible));
        }

        private void RaisePreviewProps()
        {
            OnPropertyChanged(nameof(HasCellAnimation));
            OnPropertyChanged(nameof(HasParticleAnimation));
            OnPropertyChanged(nameof(HasPreview));
            OnPropertyChanged(nameof(HasTurnCheck));
            OnPropertyChanged(nameof(HasWeatherCheck));
            OnPropertyChanged(nameof(HasContestCheck));
            OnPropertyChanged(nameof(CellAnimNote));
            OnPropertyChanged(nameof(PreviewNotes));
            OnPropertyChanged(nameof(HasPreviewNotes));
            OnPropertyChanged(nameof(IsCellPlaying));
            OnPropertyChanged(nameof(CellPlayButtonText)); OnPropertyChanged(nameof(CellPlayButtonIcon));
            OnPropertyChanged(nameof(CanStopPreview));
            OnPropertyChanged(nameof(GaugesVisible));
            OnPropertyChanged(nameof(ShadowHidden));
            OnPropertyChanged(nameof(RealGaugesVisible));
            OnPropertyChanged(nameof(PlaceholderGaugesVisible));
        }
    }

    /// <summary>One command in the structured editor: a collapsible card. Collapsed it shows <see cref="Summary"/>
    /// (read-only); expanded it shows the opcode dropdown + one typed editor per argument (<see cref="Params"/>) and
    /// a raw-args escape hatch for variable-length opcodes. <see cref="Args"/> is the canonical value store.</summary>
    public sealed class ScriptCmdRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        /// <summary>Words the reader could not decode, shown and saved as they are.</summary>
        public bool Raw { get; init; }

        // Context wired by the view-model so the row can name its opcode/params and report edits.
        internal System.Func<int, string> OpNameOf;
        internal bool Script;
        internal System.Func<int, System.Collections.Generic.IReadOnlyList<int>, int> ArgCountOf;
        internal System.Action<ScriptCmdRow> OnEdited;
        internal System.Func<int, string> PreviewSound;   // returns an error message, or null on success
        internal System.Func<int, string> SoundNameOf;

        public System.Collections.Generic.List<int> Args { get; } = new();
        public ObservableCollection<ParamVM> Params { get; } = new();

        private int _opId;
        public int OpId
        {
            get => _opId;
            set
            {
                // A card's command list sends -1 while it has no items yet; it reads the real value again once it has.
                if (value < 0) { Dispatcher.UIThread.Post(() => Raise(nameof(OpId))); return; }
                if (_opId == value) return;
                _opId = value; Raise(nameof(OpId)); Raise(nameof(OpName)); Raise(nameof(OpDisplay)); Raise(nameof(ValuesSummary)); PadArgs(); Rebuild(); OnEdited?.Invoke(this);
            }
        }
        // Ensure the args list has at least the new opcode's fixed parameter count (pad with 0). Extra args are kept
        // (variable-length opcodes); the user can trim them via the raw-args field.
        private void PadArgs() { int need = ArgCountOf?.Invoke(_opId, Args) ?? 0; while (Args.Count < need) Args.Add(0); }
        public string OpName => OpNameOf?.Invoke(_opId) ?? ("op" + _opId);
        public string OpDisplay => DSPRE.Avalonia.Data.BattleAnimSchema.OpcodeDisplay(OpName, Script);
        public string OpDoc => DSPRE.Avalonia.Data.BattleAnimSchema.OpcodeDoc(OpName, Script);
        public bool HasDoc => !string.IsNullOrEmpty(OpDoc);
        // Set the opcode during load without rebuilding/raising an edit (the caller rebuilds + keeps the loaded args).
        internal void _opIdSilent(int id) { _opId = id; Raise(nameof(OpId)); Raise(nameof(OpName)); Raise(nameof(OpDisplay)); Raise(nameof(ValuesSummary)); Raise(nameof(OpDoc)); Raise(nameof(HasDoc)); }

        private bool _expanded;
        public bool IsExpanded { get => _expanded; set { if (_expanded != value) { _expanded = value; Raise(nameof(IsExpanded)); } } }

        // Collapsed one-liner: "OPCODE  name=val  name=val …"
        public string Summary
        {
            get
            {
                if (Args.Count == 0) return OpDisplay;
                StringBuilder sb = new System.Text.StringBuilder(OpDisplay).Append("  ");
                for (int i = 0; i < Args.Count; i++)
                {
                    if (i > 0) sb.Append("  ");
                    sb.Append(DSPRE.Avalonia.Data.BattleAnimSchema.ParamLabel(OpName, i, Args, Script)).Append('=').Append(Args[i]);
                }
                return sb.ToString();
            }
        }

        /// <summary>The values alone, for the line under a collapsed card's title.</summary>
        public string ValuesSummary
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < Args.Count; i++)
                {
                    if (i > 0) sb.Append("  ");
                    sb.Append(DSPRE.Avalonia.Data.BattleAnimSchema.ParamLabel(OpName, i, Args, Script)).Append('=').Append(Args[i]);
                }
                return sb.ToString();
            }
        }
        public bool HasValues => Args.Count > 0;

        // Raw comma/space-separated args; lets the user add/remove arguments (needed for variable-length opcodes).
        public string RawArgs
        {
            get => string.Join(", ", Args);
            set
            {
                Args.Clear();
                foreach (string t in (value ?? "").Split(new[] { ',', ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries))
                {
                    string s = t.Trim();
                    int v = s.StartsWith("0x", System.StringComparison.OrdinalIgnoreCase)
                        ? System.Convert.ToInt32(s.Substring(2), 16)
                        : (int.TryParse(s, out int n) ? n : 0);
                    Args.Add(v);
                }
                Rebuild(); OnEdited?.Invoke(this);
            }
        }

        // (Re)build the typed param editors from the current args + schema.
        internal void Rebuild()
        {
            Params.Clear();
            for (int i = 0; i < Args.Count; i++)
                Params.Add(new ParamVM(DSPRE.Avalonia.Data.BattleAnimSchema.ParamLabel(OpName, i, Args, Script), Args[i], i, this,
                                       DSPRE.Avalonia.Data.BattleAnimSchema.EnumFor(OpName, i)));
            Raise(nameof(Summary)); Raise(nameof(ValuesSummary)); Raise(nameof(HasValues)); Raise(nameof(RawArgs)); Raise(nameof(HasParams));
        }
        public bool HasParams => Args.Count > 0;

        internal ParamVM ParamAt(int index)
        {
            foreach (ParamVM p in Params) if (p.Index == index) return p;
            return null;
        }

        /// <summary>Sets a value as its editor would, so the card shows it.</summary>
        internal void SetArg(int index, int v)
        {
            ParamVM p = ParamAt(index);
            if (p != null) p.Value = v; else SetParam(index, v);
        }

        internal void SetParam(int index, int v)
        {
            if (index < 0 || index >= Args.Count || Args[index] == v) return;
            Args[index] = v;
            // A message command's tag decides how many values follow it.
            if (Script && !Raw && ArgCountOf != null)
            {
                int need = ArgCountOf(_opId, Args);
                if (need != Args.Count)
                {
                    while (Args.Count < need) Args.Add(0);
                    if (Args.Count > need) Args.RemoveRange(need, Args.Count - need);
                    Rebuild();
                }
            }
            // A new routine number names its values differently.
            if (!Script && index == 0 && OpName == "CallFunc")
                foreach (ParamVM p in Params) p.Name = DSPRE.Avalonia.Data.BattleAnimSchema.ParamLabel(OpName, p.Index, Args);
            Raise(nameof(Summary)); Raise(nameof(ValuesSummary)); Raise(nameof(HasValues)); Raise(nameof(RawArgs));
            OnEdited?.Invoke(this);
        }
    }

    /// <summary>One argument editor inside a <see cref="ScriptCmdRow"/> card. Writes straight back into the
    /// row's args.</summary>
    public sealed class ParamVM : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private readonly ScriptCmdRow _row;
        private readonly int _index;
        internal int Index => _index;
        private string _name;
        public string Name { get => _name; set { if (_name != value) { _name = value; Raise(nameof(Name)); } } }

        // Enum params (operator target/pos/axis/…) show a dropdown; everything else a numeric entry.
        private readonly System.Collections.Generic.List<int> _enumValues = new();
        public ObservableCollection<string> EnumItems { get; } = new();
        public bool IsEnum { get; }
        public bool IsInt => !IsEnum;

        private int _value;
        public int Value
        {
            get => _value;
            set { if (_value != value) { _value = value; Raise(nameof(Value)); Raise(nameof(ValueDec)); Raise(nameof(SelectedEnumIndex)); Raise(nameof(SoundName)); _row.SetParam(_index, value); } }
        }
        // NumericUpDown.Value is decimal?; bridge it to the int store.
        public decimal ValueDec { get => _value; set { Value = (int)value; } }

        // A "Sound" argument gets a name lookup + a preview-playback button next to its numeric entry.
        public bool IsSound => Name == "Sound";
        public string SoundName => IsSound ? _row.SoundNameOf?.Invoke(_value) : null;
        /// <summary>Plays this sound; returns an error message to show the user, or null on success.</summary>
        public string PreviewSound() => IsSound ? _row.PreviewSound?.Invoke(_value) : null;

        public int SelectedEnumIndex
        {
            get => _enumValues.IndexOf(_value);
            set { if (value >= 0 && value < _enumValues.Count) Value = _enumValues[value]; }
        }

        private void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public ParamVM(string name, int value, int index, ScriptCmdRow row, DSPRE.Avalonia.Data.BattleAnimSchema.EnumOption[] options)
        {
            Name = name; _value = value; _index = index; _row = row;
            if (options != null)
            {
                IsEnum = true;
                foreach (BattleAnimSchema.EnumOption o in options) { EnumItems.Add($"{o.Label}  ({o.Value})"); _enumValues.Add(o.Value); }
                if (!_enumValues.Contains(value)) { EnumItems.Add($"(raw {value})"); _enumValues.Add(value); }   // keep an out-of-table value selectable
            }
        }
    }
}
