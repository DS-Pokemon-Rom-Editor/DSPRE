using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using global::Avalonia.Media;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Threading;
using DSPRE.Avalonia.Data;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels
{
    /// <summary>
    /// Standalone editor for the battle move-sequence scripts (waza_seq / be_seq / sub_seq) and the move
    /// visual-effect scripts (WEST / we). Picks an archive + entry, decodes it into an editable opcode/args command
    /// list (via <see cref="WazaSeqScript"/> / <see cref="WestScript"/> against the version's opcode table), and
    /// writes it back to the unpacked NARC (repacked on the normal ROM save). HGSS + Platinum only.
    /// </summary>
    public sealed class BattleScriptEditorViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T field, T value, [CallerMemberName] string n = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value; OnPropertyChanged(n); return true;
        }

        public enum Archive { MoveScripts = 0, EffectScripts = 1, Subroutines = 2, MoveAnimation = 3 }

        private readonly WazaSeqVersion _version;
        private readonly string[] _moveNames;
        private readonly ScriptNarc[] _narcs = new ScriptNarc[4];

        public bool IsAvailable { get; }
        public string UnavailableText => "The battle-script editor currently supports Platinum and HeartGold/SoulSilver only.";

        public BattleScriptEditorViewModel()
        {
            IsAvailable = gameFamily == GameFamilies.Plat || gameFamily == GameFamilies.HGSS;
            _version = gameFamily switch
            {
                GameFamilies.DP => WazaSeqVersion.DP,
                GameFamilies.Plat => WazaSeqVersion.Plat,
                _ => WazaSeqVersion.HGSS,
            };
            _moveNames = SafeMoveNames();

            _narcs[(int)Archive.MoveScripts]  = new ScriptNarc(DirNames.wazaSeq);
            _narcs[(int)Archive.EffectScripts] = new ScriptNarc(DirNames.beSeq);
            _narcs[(int)Archive.Subroutines]  = new ScriptNarc(DirNames.subSeq);
            _narcs[(int)Archive.MoveAnimation] = new ScriptNarc(DirNames.wazaEffectScripts);

            if (IsAvailable) SelectArchive(0);
        }

        private static string[] SafeMoveNames()
        {
            try { return GetAttackNames(); } catch { return Array.Empty<string>(); }
        }

        // ── Archive selection ────────────────────────────────────────────────────
        public string[] ArchiveOptions { get; } =
        {
            "Move scripts (waza_seq)", "Move-effect scripts (be_seq)",
            "Subroutines (sub_seq)", "Move animation (WEST)",
        };

        private int _archiveIndex = -1;
        public int ArchiveIndex
        {
            get => _archiveIndex;
            set { if (value != _archiveIndex) SelectArchive(value); }
        }

        /// <summary>Whether the move-animation archive is the one open. The three views are for it.</summary>
        public bool IsWest => (Archive)_archiveIndex == Archive.MoveAnimation;
        private ScriptNarc CurrentNarc => _narcs[_archiveIndex];

        /// <summary>Builds the reference data for the command guide window, for whichever command set this
        /// archive uses (WEST move-animation opcodes, or the waza/be/sub effect-sequence opcodes).</summary>
        public ScriptCommandGuideViewModel BuildCommandGuideViewModel() => new ScriptCommandGuideViewModel(IsWest);

        // ── Sound preview (WEST_SE and friends' "Sound" argument) ───────────────────────
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
                string fileName = gameFamily switch
                {
                    GameFamilies.HGSS => "gs_sound_data.sdat",
                    GameFamilies.Plat => "pl_sound_data.sdat",
                    GameFamilies.DP => "sound_data.sdat",
                    _ => "sound_data.sdat",
                };
                // workDir is the project's own root (where arm9/banner/etc. live); the ROM's own internal
                // filesystem (where "data/sound/..." actually lives) is nested one level deeper, under "files"
                // for a ds-rom project or "data" for a legacy one.
                string romRoot = System.IO.Path.Combine(workDir, IsDsRomProject ? "files" : "data");
                string path = System.IO.Path.Combine(romRoot, "data", "sound", fileName);
                if (!System.IO.File.Exists(path)) { _sdatLoadError = $"Sound archive not found at:\n{path}"; return null; }
                _sdat = SdatArchive.Parse(System.IO.File.ReadAllBytes(path));
                if (_sdat.Sequences.Count == 0) _sdatLoadError = $"Sound archive at {path} parsed to 0 sequences (unexpected format?).";
            }
            catch (System.Exception ex) { _sdat = null; _sdatLoadError = ex.ToString(); }
            return _sdat;
        }

        /// <summary>The sound's real name from the ROM's own sound archive (e.g. "SEQ_SE_PL_KEZURI"), or null
        /// if it can't be resolved. Shown next to a "Sound" argument so the ID isn't just a bare number.</summary>
        private string SoundNameOf(int soundId) => LoadSdat()?.SeqNames.TryGetValue(soundId, out var n) == true ? n : null;

        /// <summary>Renders and plays the given sound ID through the active <see cref="AudioOutput"/> backend.
        /// Best-effort: an out-of-range ID or an unresolved instrument just stays silent rather than erroring.
        /// Used during animation playback, where a popup per failed note would be disruptive.
        ///
        /// Rendering (SSEQ interpretation + PCM mixdown) runs on a background thread, not inline on the caller:
        /// each render allocates several MB on the Large Object Heap, and doing that on the UI dispatcher thread
        /// (which drives <see cref="WestPlayer"/>'s 60Hz preview timer) causes LOH-churn GC pauses that stall the
        /// animation loop.</summary>
        /// <summary>
        /// The cry of whichever Pokemon the preview is showing as the attacker. WEST_VOICE_PLAY hands the
        /// games a pan and a volume as well; neither is applied here.
        /// </summary>
        private void PreviewCry()
        {
            int species = _gaugeSpeciesId;
            if (species <= 0) return;
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var pcm = SoundArchive.RenderCry(species);
                    if (pcm != null && pcm.Length > 0) AudioOutput.Current.Play(pcm, 32000);
                }
                catch { /* a preview should not put a dialog up because a sound would not play */ }
            });
        }

        /// <summary>Stops what is playing. The output mixes everything together, so this stops all of it
        /// rather than the one sound the script named.</summary>
        private void PreviewStopSound(int soundId)
        {
            try { AudioOutput.Current.Stop(); } catch { }
        }

        private void PreviewSound(int soundId)
        {
            var sdat = LoadSdat();
            if (sdat == null) return;
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var pcm = SseqPlayer.Render(sdat, soundId);
                    if (pcm != null && pcm.Length > 0) AudioOutput.Current.Play(pcm, 32000);
                }
                catch { /* best-effort preview; animation playback must never surface an error dialog */ }
            });
        }

        /// <summary>Same as <see cref="PreviewSound"/> but returns a human-readable reason on failure (null on
        /// success), for the card's "Preview sound" button to surface via a dialog instead of failing silently.</summary>
        internal string TryPreviewSound(int soundId)
        {
            var sdat = LoadSdat();
            if (sdat == null) return _sdatLoadError ?? "Sound archive could not be loaded.";
            try
            {
                if (!sdat.SeqNames.ContainsKey(soundId) && (soundId < 0 || soundId >= sdat.Sequences.Count || sdat.Sequences[soundId] == null))
                    return $"Sound ID {soundId} doesn't resolve to a sequence in this ROM's sound archive.";
                var pcm = SseqPlayer.Render(sdat, soundId);
                if (pcm == null || pcm.Length == 0) return $"Sound {soundId} rendered to no audio (an unsupported instrument type, most likely).";
                AudioOutput.Current.Play(pcm, 32000);
                if (AudioOutput.Current is NullAudioOutput) return "No audio backend is wired up in this shell (the pure cross-platform preview build has no sound output yet).";
                return null;
            }
            catch (System.Exception ex) { return ex.ToString(); }
        }

        /// <summary>Internal opcode names for the current archive (index = opcode id), used for schema lookups.</summary>
        public ObservableCollection<string> OpcodeNames { get; } = new ObservableCollection<string>();
        /// <summary>Friendly opcode titles (index = opcode id); drives the per-row opcode dropdown.</summary>
        public ObservableCollection<string> OpcodeDisplayNames { get; } = new ObservableCollection<string>();

        // ── the three ways of reading a script ──────────────────────────────────────

        /// <summary>The lines the chosen view shows. All three read the same commands.</summary>
        public ObservableCollection<WestLine> ViewLines { get; } = new ObservableCollection<WestLine>();

        private WestLine _pickedLine;
        /// <summary>The line somebody clicked, which is what the panel beside it explains.</summary>
        public WestLine PickedLine
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
            get => DSPRE.SettingsManager.Settings?.moveAnimationViewMode ?? 0;
            set
            {
                int v = Math.Clamp(value, 0, 2);
                if (DSPRE.SettingsManager.Settings == null || DSPRE.SettingsManager.Settings.moveAnimationViewMode == v) return;
                DSPRE.SettingsManager.Settings.moveAnimationViewMode = v;
                try { DSPRE.SettingsManager.Save(); } catch { }
                OnPropertyChanged(nameof(ViewMode));
                OnPropertyChanged(nameof(ViewNote));
                RefreshViewLines();
            }
        }

        public string ViewNote => ViewMode switch
        {
            0 => "Grouped by what part of the move each command belongs to, with the shorthands the scripts "
               + "were written in folded back to one line.",
            1 => "One command a line, lined up in columns, with loops and subroutine bodies indented.",
            _ => "Every word as it sits in the ROM: where it is, its number, its name, its values and their hex. "
               + "Nothing folded and nothing hidden.",
        };

        /// <summary>Builds the lines for whichever view is showing, from the commands as they stand now.</summary>
        public void RefreshViewLines()
        {
            ViewLines.Clear();
            if (!IsWest) { OnPropertyChanged(nameof(HasViewLines)); return; }

            var cmds = Rows.Select(r => new WazaSeqCommand(r.OpId, r.Args.ToArray())).ToList();
            int pos = 0;
            foreach (var c in cmds) { c.WordPos = pos; pos += 1 + c.Args.Length; }

            foreach (var l in WestScriptDisplay.Build(cmds, _version, (WestViewMode)ViewMode, SoundNameOf))
                ViewLines.Add(l);
            OnPropertyChanged(nameof(HasViewLines));
            OnPropertyChanged(nameof(ViewSummary));
        }

        public bool HasViewLines => ViewLines.Count > 0;

        private int _openTab;
        /// <summary>Which of Read, Cards and Text is open, so help meant for one does not sit above them all.</summary>
        public int OpenTab
        {
            get => _openTab;
            set { if (Set(ref _openTab, value)) OnPropertyChanged(nameof(ShowTextHelp)); }
        }

        /// <summary>The note about how to write a command belongs to the text view, not to the others.</summary>
        public bool ShowTextHelp => _openTab == 2 || !IsWest;

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
            _archiveIndex = Math.Clamp(index, 0, 3);
            OnPropertyChanged(nameof(ArchiveIndex));

            OpcodeNames.Clear();
            if (IsWest)
                foreach (var o in WestOpcodes.Table(_version)) OpcodeNames.Add(o.Name);
            else
                foreach (var o in WazaSeqOpcodes.Table(_version)) OpcodeNames.Add(o.Name);
            OpcodeDisplayNames.Clear();
            foreach (var n in OpcodeNames) OpcodeDisplayNames.Add(DSPRE.Avalonia.Data.WestParamSchema.OpcodeDisplay(n));
            _nameToOp = null;   // opcode table changed → rebuild the text-parser's name→id map lazily

            BuildFileList();
            OnPropertyChanged(nameof(IsWest));
            OnPropertyChanged(nameof(ShowTextHelp));
            OnPropertyChanged(nameof(ArchiveNotAvailable));
            OnPropertyChanged(nameof(ArchiveUnavailableText));
            // Reset to the first entry of the new archive.
            _fileIndex = -1;
            SelectedFileIndex = FileItems.Count > 0 ? 0 : -1;
        }

        public bool ArchiveNotAvailable => IsAvailable && _archiveIndex >= 0 && !CurrentNarc.Available;
        public string ArchiveUnavailableText =>
            $"This archive isn't mapped for {gameFamily} yet (no path wired). Provide the NARC path to enable it.";

        // ── Entry (file) selection ────────────────────────────────────────────────
        public ObservableCollection<string> FileItems { get; } = new ObservableCollection<string>();

        private void BuildFileList()
        {
            FileItems.Clear();
            if (!IsAvailable || !CurrentNarc.Available) return;
            int count = CurrentNarc.Count;
            for (int i = 0; i < count; i++) FileItems.Add(LabelFor(i));
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
                default:
                    return $"{i:D3} - Subroutine {i}";
            }
        }

        private int _fileIndex = -1;
        public int SelectedFileIndex
        {
            get => _fileIndex;
            set { if (Set(ref _fileIndex, value)) LoadEntry(); }
        }

        public string EntryHeader =>
            _fileIndex < 0 ? "(no entry selected)"
                           : $"{ArchiveOptions[_archiveIndex]}  -  #{_fileIndex}  ({Rows.Count} commands)";

        // ── Command list ───────────────────────────────────────────────────────────
        public ObservableCollection<ScriptCmdRow> Rows { get; } = new ObservableCollection<ScriptCmdRow>();
        public bool HasRows => Rows.Count > 0;

        private bool _dirty;
        public bool Dirty
        {
            get => _dirty;
            private set { if (Set(ref _dirty, value)) OnPropertyChanged(nameof(SaveHint)); }
        }

        private void LoadEntry()
        {
            Rows.Clear();
            if (IsAvailable && _fileIndex >= 0 && CurrentNarc.Available)
            {
                var bytes = CurrentNarc.Get(_fileIndex);
                var cmds = bytes == null ? null
                         : IsWest ? WestScript.Parse(bytes, _version)
                                  : WazaSeqScript.Parse(bytes, _version);
                if (cmds != null) foreach (var c in cmds) AddRow(c.OpId, c.Args);
            }
            Dirty = false;
            TextErrors = Array.Empty<TextError>();
            SyncTextFromRows();   // seed the text view from the freshly-loaded cards
            RefreshViewLines();
            RefreshStoryboard();
            SetupCellPreview();
            OnPropertyChanged(nameof(HasRows));
            OnPropertyChanged(nameof(EntryHeader));
        }

        private void AddRow(int opId, int[] args)
        {
            var row = new ScriptCmdRow { OpNameOf = OpNameOf, FixedArgCountOf = FixedArgCountOf, OnEdited = OnRowEdited, PreviewSound = TryPreviewSound, SoundNameOf = SoundNameOf };
            row.Args.AddRange(args ?? System.Array.Empty<int>());
            row._opIdSilent(opId);   // set without firing OnEdited during load
            row.Rebuild();
            Rows.Add(row);
        }

        // opId → opcode name for the row labels (the dropdown index IS the opcode id).
        private string OpNameOf(int opId) => opId >= 0 && opId < OpcodeNames.Count ? OpcodeNames[opId] : "op" + opId;

        // The opcode's fixed parameter count (variable-length opcodes report only their fixed leading args).
        private int FixedArgCountOf(int opId)
        {
            if (IsWest) return WestOpcodes.TryGet(_version, opId, out var op) ? op.ArgCount : 0;
            return Math.Max(0, WazaSeqOpcodes.ArgCount(_version, opId));
        }

        private void OnRowEdited(ScriptCmdRow row)
        {
            RefreshViewLines();
            Dirty = true;
            RefreshStoryboard();
            SyncTextFromRows();
        }

        public void AddCommand()
        {
            AddRow(0, Array.Empty<int>());
            Dirty = true;
            RefreshStoryboard();
            SyncTextFromRows();
            OnPropertyChanged(nameof(HasRows));
            OnPropertyChanged(nameof(EntryHeader));
        }

        public void RemoveCommand(ScriptCmdRow row)
        {
            if (row == null || !Rows.Contains(row)) return;
            Rows.Remove(row);
            Dirty = true;
            RefreshStoryboard();
            SyncTextFromRows();
            OnPropertyChanged(nameof(HasRows));
            OnPropertyChanged(nameof(EntryHeader));
        }

        public void MoveCommand(ScriptCmdRow row, int dir)
        {
            int i = Rows.IndexOf(row), j = i + dir;
            if (i < 0 || j < 0 || j >= Rows.Count) return;
            Rows.Move(i, j);
            Dirty = true;
            RefreshStoryboard();
            SyncTextFromRows();
        }

        public void Save()
        {
            if (!IsAvailable || _fileIndex < 0 || !CurrentNarc.Available) return;
            if (HasTextErrors)   // the cards are stale while the text is invalid, don't persist the wrong thing
            {
                _ = DSPRE.Avalonia.DialogHelper.ShowInfo("The command text has errors. Fix the red-underlined line(s) before saving.", "Fix errors first");
                return;
            }
            var cmds = BuildCommands();
            byte[] bytes = IsWest ? WestScript.Serialize(cmds) : WazaSeqScript.Serialize(cmds);
            CurrentNarc.Put(_fileIndex, bytes);
            LoadEntry();   // reflect the canonical form
        }

        private List<WazaSeqCommand> BuildCommands()
        {
            var list = new List<WazaSeqCommand>();
            foreach (var row in Rows)
            {
                int[] args;
                if (IsWest)
                {
                    // WEST opcodes are variable-length; keep exactly the row's args.
                    args = row.Args.ToArray();
                }
                else
                {
                    // waza/be/sub opcodes are fixed-length, pad/truncate to the opcode's arg count.
                    int n = Math.Max(0, WazaSeqOpcodes.ArgCount(_version, row.OpId));
                    args = new int[n];
                    for (int i = 0; i < n; i++) args[i] = i < row.Args.Count ? row.Args[i] : 0;
                }
                list.Add(new WazaSeqCommand(row.OpId, args));
            }
            return list;
        }

        private static List<int> ParseIntList(string s)
        {
            var list = new List<int>();
            if (string.IsNullOrWhiteSpace(s)) return list;
            foreach (var p in s.Split(new[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
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
        // The command list can be edited either as collapsible cards (Rows) or as plain text (CommandsText); the two are
        // kept live-synced. Each text line is "OPCODE_NAME arg0 arg1 …" (decimal or 0xHEX). Editing the cards regenerates
        // the text; editing the text re-parses into the cards, but ONLY when the text is error-free (else the cards keep
        // the last good state and the errors are surfaced as red squiggles + block switching to card view).
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
        public string TextErrorSummary => _textErrors.Count == 0 ? "" : _textErrors.Count + " error(s): " + _textErrors[0].Message;

        // Rows → text (called after any card edit). Suppressed while we are rebuilding the rows FROM text.
        private void SyncTextFromRows()
        {
            if (_rebuildingRows) return;
            _pushingText = true;
            CommandsText = RowsToText();
            _pushingText = false;
        }

        // The text format is a single-word command line: "CommandName label=value label=value ...", e.g.
        // "AddParticles slot=0 data=482 behavior=3". Every command/argument/enum-value token is a single
        // camel/Pascal-case word (WestParamSchema.CommandName/ArgToken/Token) so it types and greps like a real
        // command line rather than a sentence. Args may be named (label=value, in any order; the label pins it
        // to that parameter's slot) or bare (a plain number/enum name; fills the next slot not already claimed
        // by a named arg, left to right). Raw internal opcode names and plain numbers still parse too.
        private string RowsToText()
        {
            var sb = new StringBuilder();
            foreach (var row in Rows)
            {
                string raw = OpNameOf(row.OpId);
                sb.Append(DSPRE.Avalonia.Data.WestParamSchema.CommandName(raw));
                for (int i = 0; i < row.Args.Count; i++)
                {
                    sb.Append(' ');
                    int v = row.Args[i];
                    string label = DSPRE.Avalonia.Data.WestParamSchema.ParamName(raw, i);
                    // An enum parameter shows its friendly value token; a generic "Param N" label is dropped
                    // (bare number) since a made-up name would add no meaning.
                    var opts = DSPRE.Avalonia.Data.WestParamSchema.EnumFor(raw, i);
                    string valText = v.ToString(CultureInfo.InvariantCulture);
                    if (opts != null)
                        foreach (var o in opts) if (o.Value == v) { valText = DSPRE.Avalonia.Data.WestParamSchema.Token(o.Label, true); break; }
                    if (label.StartsWith("Param ", StringComparison.Ordinal)) sb.Append(valText);
                    else sb.Append(DSPRE.Avalonia.Data.WestParamSchema.ArgToken(raw, i)).Append('=').Append(valText);
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
            var d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < OpcodeNames.Count; i++)
            {
                d[OpcodeNames[i]] = i;   // index == opId
                string cmd = DSPRE.Avalonia.Data.WestParamSchema.CommandName(OpcodeNames[i]);
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
            var opts = DSPRE.Avalonia.Data.WestParamSchema.EnumFor(rawOpName, argIndex);
            if (opts == null) return false;
            foreach (var o in opts)
            {
                if (string.Equals(o.Label, token, StringComparison.OrdinalIgnoreCase)) { value = o.Value; return true; }
                if (string.Equals(DSPRE.Avalonia.Data.WestParamSchema.Token(o.Label, true), token, StringComparison.OrdinalIgnoreCase)) { value = o.Value; return true; }
            }
            return false;
        }

        private static bool TryParseArgValue(string rawOpName, int argIndex, string token, out int v)
            => TryParseWord(token, out v) || TryResolveEnum(rawOpName, argIndex, token, out v);

        // Finds the argument index whose single-word label (or, for a payload slot with no known name, "paramN"/
        // "argN") matches the given token by scanning only the opcode's KNOWN fixed labels (stops at the first
        // generic "Param N" fallback, since anything past that is unnamed variable payload).
        private static int ResolveArgIndex(string rawOpName, string label)
        {
            string digits = label.Length > 5 && label.StartsWith("param", StringComparison.OrdinalIgnoreCase) ? label.Substring(5)
                           : label.Length > 3 && label.StartsWith("arg", StringComparison.OrdinalIgnoreCase) ? label.Substring(3)
                           : null;
            if (digits != null && int.TryParse(digits, out int n) && n >= 1) return n - 1;
            for (int i = 0; i < 32; i++)
            {
                string pn = DSPRE.Avalonia.Data.WestParamSchema.ParamName(rawOpName, i);
                if (pn.StartsWith("Param ", StringComparison.Ordinal)) break;
                if (string.Equals(DSPRE.Avalonia.Data.WestParamSchema.ArgToken(rawOpName, i), label, StringComparison.OrdinalIgnoreCase)) return i;
            }
            return -1;
        }

        // Text → Rows. Collects per-token errors (offset/length for squiggles). Rebuilds the cards ONLY when clean.
        private void ParseTextIntoRows()
        {
            var errors = new List<TextError>();
            var parsed = new List<(int opId, int[] args)>();
            _nameToOp ??= NameToOp();

            int offset = 0;
            foreach (var rawLine in _commandsText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                int lineStart = offset;
                offset += rawLine.Length + 1;   // + the newline we split on
                string line = rawLine.Trim();
                if (line.Length == 0) continue;                                   // blank line = no command
                if (line.StartsWith("//") || line.StartsWith("#")) continue;      // allow comment lines

                // Command = the first whitespace/comma-separated token; the rest are its arguments.
                var toks = line.Split(new[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);
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
                var slotValue = new Dictionary<int, int>();
                var bareToks = new List<(string tok, int col)>();

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
                    int idx = ResolveArgIndex(raw, label);
                    if (idx < 0)
                    {
                        errors.Add(new TextError(lineStart + Math.Max(0, col), tok.Length, $"Unknown argument '{label}'"));
                        argOk = false; continue;
                    }
                    if (!TryParseArgValue(raw, idx, valTok, out int v))
                    {
                        errors.Add(new TextError(lineStart + Math.Max(0, col), tok.Length, $"'{valTok}' is not a number or known value"));
                        argOk = false; continue;
                    }
                    slotValue[idx] = v;
                }

                // Pass 2: bare tokens fill whichever slots are left, in order.
                int cursor = 0;
                foreach (var (tok, col) in bareToks)
                {
                    while (slotValue.ContainsKey(cursor)) cursor++;
                    if (!TryParseArgValue(raw, cursor, tok, out int v))
                    {
                        errors.Add(new TextError(lineStart + Math.Max(0, col < 0 ? 0 : col), tok.Length, $"'{tok}' is not a number or known value"));
                        argOk = false; cursor++; continue;
                    }
                    slotValue[cursor] = v;
                    cursor++;
                }

                if (argOk)
                {
                    int maxIdx = -1;
                    foreach (var k in slotValue.Keys) if (k > maxIdx) maxIdx = k;
                    var args = new int[maxIdx + 1];
                    foreach (var kv in slotValue) args[kv.Key] = kv.Value;
                    parsed.Add((opId, args));
                }
            }

            TextErrors = errors;
            if (errors.Count > 0) return;   // keep the cards at the last good state; the squiggles + guard handle it

            _rebuildingRows = true;
            Rows.Clear();
            foreach (var (opId, args) in parsed) AddRow(opId, args);
            _rebuildingRows = false;
            Dirty = true;
            RefreshStoryboard();
            OnPropertyChanged(nameof(HasRows));
            OnPropertyChanged(nameof(EntryHeader));
        }

        // ── WEST storyboard (readable timeline) ─────────────────────────────────────
        private string _storyboard = "";
        public string Storyboard { get => _storyboard; private set => Set(ref _storyboard, value); }
        public bool ShowStoryboard => IsAvailable && HasRows;
        public string StoryboardTitle => IsWest ? "Animation storyboard" : "Effect summary";

        // The animation storyboard is a column of frame numbers and reads best left alone; the effect
        // summary is prose and was running off the right-hand edge behind a scrollbar.
        public global::Avalonia.Media.TextWrapping StoryboardWrap =>
            IsWest ? global::Avalonia.Media.TextWrapping.NoWrap : global::Avalonia.Media.TextWrapping.Wrap;

        private void RefreshStoryboard()
        {
            OnPropertyChanged(nameof(ShowStoryboard));
            OnPropertyChanged(nameof(StoryboardTitle));
            OnPropertyChanged(nameof(StoryboardWrap));
            if (!HasRows) { Storyboard = ""; return; }
            var cmds = BuildCommands();
            Storyboard = IsWest ? WestStoryboard.Build(cmds, _version) : WazaSeqStoryboard.Build(cmds, _version);
        }

        // ── Animation preview: cell-anim (CATS, ~32 moves) + particles (SPA, ~425 moves) ──
        private readonly WeCellAnimRenderer _cellRenderer = new WeCellAnimRenderer();
        private IReadOnlyList<WeCellAnimRenderer.Frame> _cellFrames = Array.Empty<WeCellAnimRenderer.Frame>();
        private readonly ScriptNarc _particleNarc = new ScriptNarc(DirNames.wazaParticle);
        private WestPlayer _west;                    // faithful timeline interpreter (built on Play)
        private DispatcherTimer _previewTimer;
        private int _cellFrameIdx, _cellTick, _cellLoops, _previewFrames;
        private const int MaxPreviewFrames = 1200;   // safety cap (~20 s)

        public bool HasCellAnimation { get; private set; }
        public bool HasParticleAnimation { get; private set; }
        // Any WEST entry shows the battle scene, even moves with no particles still animate (lunge/shake/fade).
        public bool HasPreview => IsWest && _fileIndex >= 0;
        public string CellAnimNote { get; private set; } = "";

        /// <summary>
        /// What this move does that the preview does not show, gathered from the player while it runs.
        /// Better said out loud than left for somebody to spot a difference and wonder whose fault it is.
        /// </summary>
        public string PreviewNotes => _west == null || _west.Notes.Count == 0
            ? "" : "Not shown here: " + string.Join(" ", _west.Notes);

        public bool HasPreviewNotes => PreviewNotes.Length > 0;

        private int _notesShown;

        /// <summary>Why Save is greyed out, so a disabled button is not left unexplained.</summary>
        public string SaveHint => Dirty
            ? "Write these commands back into the ROM."
            : "Nothing has been changed yet, so there is nothing to write back.";
        private Bitmap _cellPreview;
        public Bitmap CellPreview { get => _cellPreview; private set => Set(ref _cellPreview, value); }
        private Bitmap _particlePreview;
        public Bitmap ParticlePreview { get => _particlePreview; private set => Set(ref _particlePreview, value); }
        // HAIKEI scrolling background: drawn behind the mons (backdrop replace) or over them (effect overlay).
        private Bitmap _backgroundFrame;
        public Bitmap BackgroundFrame { get => _backgroundFrame; private set { if (Set(ref _backgroundFrame, value)) { OnPropertyChanged(nameof(BackgroundBehind)); OnPropertyChanged(nameof(BackgroundOver)); } } }
        private bool _bgOverlay;
        public bool BackgroundIsOverlay { get => _bgOverlay; private set { if (Set(ref _bgOverlay, value)) { OnPropertyChanged(nameof(BackgroundBehind)); OnPropertyChanged(nameof(BackgroundOver)); } } }
        public Bitmap BackgroundBehind => _bgOverlay ? null : _backgroundFrame;   // backdrop-replace (Fly/Dig/Cosmic)
        public Bitmap BackgroundOver => _bgOverlay ? _backgroundFrame : null;     // effect overlay (Surf water sweep)
        // WE_057 wave transform (rise/wash + fade) applied to the cell-anim layer.
        private double _cellSX = 1, _cellSY = 1, _cellOpacity = 1, _cellOX, _cellOY;
        public double CellScaleX { get => _cellSX; private set => Set(ref _cellSX, value); }
        public double CellScaleY { get => _cellSY; private set => Set(ref _cellSY, value); }
        public double CellOpacity { get => _cellOpacity; private set => Set(ref _cellOpacity, value); }
        public double CellOffsetX { get => _cellOX; private set => Set(ref _cellOX, value); }
        public double CellOffsetY { get => _cellOY; private set => Set(ref _cellOY, value); }
        // Scale pivot for the cell layer = the sprite's measured content centre (so WE_057 scales it in place).
        private global::Avalonia.RelativePoint _cellOrigin = global::Avalonia.RelativePoint.Center;
        public global::Avalonia.RelativePoint CellOrigin { get => _cellOrigin; private set => Set(ref _cellOrigin, value); }
        public bool IsCellPlaying => _previewTimer != null && _previewTimer.IsEnabled;
        public string CellPlayButtonText => IsCellPlaying ? "⏹ Stop" : "▶ Play animation";
        // Scene-wide effects driven live by the timeline (WT_SHAKE, HAIKEI_PAL_FADE).
        private double _bgDarken; public double BackgroundDarken { get => _bgDarken; private set => Set(ref _bgDarken, value); }
        private IBrush _fadeBrush = Brushes.Black; public IBrush FadeBrush { get => _fadeBrush; private set => Set(ref _fadeBrush, value); }
        private double _shakeX; public double ShakeX { get => _shakeX; private set => Set(ref _shakeX, value); }
        private double _shakeY; public double ShakeY { get => _shakeY; private set => Set(ref _shakeY, value); }
        // Per-Pokémon sprite transforms driven by the WEST_SP routines (rotate / scale / vanish / colour flash).
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

        // Backdrop + mon sprites are positioned exactly like the Battle Display editor (sprite-Y offset + heights),
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

        // Who casts the move in the preview. In-game a move flips for an enemy caster (SIDE_JP, attacker-anchored
        // emitters, lunges). Default: the player (bottom) attacks the enemy (top). Toggling re-runs from the top.
        private bool _attackerIsEnemy;
        public bool AttackerIsEnemy
        {
            get => _attackerIsEnemy;
            set { if (Set(ref _attackerIsEnemy, value)) { bool was = IsCellPlaying; SetupCellPreview(); if (was) ToggleCellPlay(); } }
        }

        // Some moves hold two animations and the game alternates them by the battle's turn count
        // (we_sys.c:3018). Only the first one plays here unless this is ticked, so the second was
        // previously impossible to see or check.
        private bool _secondTurnVariant;
        public bool SecondTurnVariant
        {
            get => _secondTurnVariant;
            set { if (Set(ref _secondTurnVariant, value)) { bool was = IsCellPlaying; SetupCellPreview(); if (was) ToggleCellPlay(); } }
        }

        /// <summary>Whether the open move has a second animation at all, so the choice is only offered when it means something.</summary>
        public bool HasTurnCheck
        {
            get
            {
                var cmds = BuildCommands();
                if (cmds == null) return false;
                foreach (var c in cmds)
                    if (WestOpcodes.Name(_version, c.OpId) == "WEST_TURN_CHK") return true;
                return false;
            }
        }

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
                var sv = new PokemonSpriteEditorViewModel(true);
                sv.LoadMon(id);
                var bd = new BattleDisplayEditorViewModel(sv);
                bd.LoadMon(id);
                bd.Detach();       // we only want its computed positions, stop its preview timer

                EnemySprite = bd.EnemySprite ?? First(sv.BattleFrontM) ?? First(sv.BattleFrontF);
                PlayerSprite = bd.PlayerSprite ?? First(sv.BattleBackM) ?? First(sv.BattleBackF);
                EnemyLeft = bd.EnemyLeft; EnemyTop = bd.EnemyTop;
                PlayerLeft = bd.PlayerLeft; PlayerTop = bd.PlayerTop;
                _dfX = EnemyLeft + 40; _dfY = EnemyTop + 40;
                _atX = PlayerLeft + 40; _atY = PlayerTop + 40;

                OnPropertyChanged(nameof(EnemySprite)); OnPropertyChanged(nameof(PlayerSprite));
                OnPropertyChanged(nameof(EnemyLeft)); OnPropertyChanged(nameof(EnemyTop));
                OnPropertyChanged(nameof(PlayerLeft)); OnPropertyChanged(nameof(PlayerTop));

                if (PlayerSprite != null) { var (px, pw, ph) = ToRgba(PlayerSprite); _compositor.SetPlayer(px, pw, ph, (int)PlayerLeft, (int)PlayerTop); }
                if (EnemySprite != null) { var (ex, ew, eh) = ToRgba(EnemySprite); _compositor.SetEnemy(ex, ew, eh, (int)EnemyLeft, (int)EnemyTop); }
                if (IsWest && _sceneLoaded && !IsCellPlaying) SceneComposite = _compositor.Render(null);
            }
            catch { /* no ROM / sprite, backdrop just shows the scene without the mons */ }

            static Bitmap First(System.Collections.Generic.IReadOnlyList<Bitmap> l) => l != null && l.Count > 0 ? l[0] : null;
        }

        private void AddStatic(string asset, int left, int top)
        {
            var bmp = LoadAsset(asset);
            if (bmp == null) return;
            var (rgba, w, h) = ToRgba(bmp);
            _compositor.AddStatic(rgba, w, h, left, top);
        }

        // ── Configurable battle background (real ROM data) ──────────────────────────────────────────────────────
        private DSPRE.Avalonia.Data.BattleBgRenderer _bgRenderer;
        private System.Collections.Generic.List<string> _backgroundOptions;
        /// <summary>Dropdown: "Provided image" (the bundled PNG) + every REAL battle-scene backdrop decoded from
        /// pl_batt_bg.narc (BATTLE_BG00 + bg_id, the scenery behind the platforms, NOT the move-effect backgrounds).
        /// Picking one swaps the scene backdrop for the real ROM graphics.</summary>
        public System.Collections.Generic.List<string> BackgroundOptions => _backgroundOptions ??= BuildBackgroundOptions();
        private static System.Collections.Generic.List<string> BuildBackgroundOptions()
        {
            var l = new System.Collections.Generic.List<string> { "No background" };
            for (int i = 0; i < DSPRE.Avalonia.Data.BattleBgRenderer.BackdropCount; i++) l.Add($"Backdrop #{i}");
            return l;
        }
        // A real battle always has a backdrop and a real ground, so start on grass rather than on black with
        // placeholder platforms: on black, every move that swaps the backdrop looks brighter than it does in game.
        private int _backgroundIndex = 1;
        public int BackgroundIndex
        {
            get => _backgroundIndex;
            set
            {
                if (!Set(ref _backgroundIndex, value)) return;
                ApplyBackdrop();
                if (IsWest && _sceneLoaded && !IsCellPlaying) SceneComposite = _compositor.Render(null);
            }
        }

        // ── Configurable terrain ground platforms (real ROM data, battle/graphic/pl_batt_obj.narc) ──────────────
        private DSPRE.Avalonia.Data.BattleGroundRenderer _groundRenderer;
        private System.Collections.Generic.List<string> _terrainOptions;
        /// <summary>Dropdown: "Placeholder" (the bundled platform PNGs) + each GROUND_ID terrain (Gravel, Sand, Lawn,
        /// Pool, Rock, Cave, Snow, Water, Ice, Floor). Picking one renders the real in-game ground "tray" the Pokémon
        /// stand on (battle/), which move animations interact with, from pl_batt_obj.narc.</summary>
        public System.Collections.Generic.List<string> TerrainOptions => _terrainOptions ??= BuildTerrainOptions();
        private static System.Collections.Generic.List<string> BuildTerrainOptions()
        {
            var l = new System.Collections.Generic.List<string> { "Placeholder platforms" };
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
                // Auto-populate a matching scene backdrop for the terrain (GROUND_ID and bg_id are independent per-zone
                // in the data, so this is an editor convenience using the GROUND##↔BG## scene numbering; the Backdrop
                // selector can still override). Index 0 (placeholder) leaves the backdrop untouched.
                if (_terrainIndex > 0)
                {
                    int bg = DSPRE.Avalonia.Data.BattleGroundRenderer.BackdropForTerrain(_terrainIndex - 1);
                    if (bg >= 0) BackgroundIndex = bg + 1;   // +1: option 0 is the bundled image
                }
                if (IsWest && _sceneLoaded && !IsCellPlaying) SceneComposite = _compositor.Render(null);
            }
        }

        /// <summary>Rebuilds the ground platforms: the bundled placeholder PNGs for index 0, otherwise the real
        /// pl_batt_obj terrain "tray" (mine + enemy, at the game's GROUND_MINE/ENEMY positions). Falls back to the
        /// placeholders if the ROM/NARC is unavailable so the scene always has a floor.</summary>
        private void ApplyGround()
        {
            _compositor.ClearStatics();
            bool placed = false;
            if (_terrainIndex > 0)
            {
                try
                {
                    var (mine, enemy) = (_groundRenderer ??= new DSPRE.Avalonia.Data.BattleGroundRenderer()).Build(_terrainIndex - 1);
                    if (enemy?.Rgba != null) { _compositor.AddStatic(enemy.Rgba, enemy.Width, enemy.Height, enemy.Left, enemy.Top); placed = true; }
                    if (mine?.Rgba != null) { _compositor.AddStatic(mine.Rgba, mine.Width, mine.Height, mine.Left, mine.Top); placed = true; }
                }
                catch { placed = false; }
            }
            if (!placed) { AddStatic("platform_opponent.png", 129, 72); AddStatic("platform_you.png", -42, 122); }
        }

        // ── Real HP gauges (pl_batt_obj frames) + configurable readout + per-move UI-hide ──────────────────────────
        private Bitmap _gaugePlayerImage, _gaugeEnemyImage;
        public Bitmap GaugePlayerImage { get => _gaugePlayerImage; private set => Set(ref _gaugePlayerImage, value); }
        public Bitmap GaugeEnemyImage { get => _gaugeEnemyImage; private set => Set(ref _gaugeEnemyImage, value); }
        public bool HasRealGauges => _gaugePlayerImage != null || _gaugeEnemyImage != null;
        public bool ShowPlaceholderGauges => !HasRealGauges;

        // CT_WazaEffectGaugeShadowOnOffCheck: during a move the gauges are hidden UNLESS the move's
        // WazaData flag (byte 11) has FLAG_PUT_GAUGE(0x40); the soft-sprite shadow is hidden if FLAG_DEL_SHADOW(0x80).
        // Re-shown when the effect ends. So most moves drop the HUD for their animation, exactly per the code.
        private bool _hideGaugesThisMove, _hideShadowThisMove;
        public bool GaugesVisible => !(IsCellPlaying && _hideGaugesThisMove);
        public bool ShadowHidden => IsCellPlaying && _hideShadowThisMove;
        public bool RealGaugesVisible => HasRealGauges && GaugesVisible;
        public bool PlaceholderGaugesVisible => !HasRealGauges && GaugesVisible;

        private int GetMoveFlagField(int moveId = -1)
        {
            if (moveId < 0) moveId = _fileIndex;
            if ((Archive)_archiveIndex != Archive.MoveAnimation || moveId < 0) return -1;
            try { return new MoveData(moveId).flagField; }
            catch { return -1; }
        }

        // Configurable gauge readout (the preview mons are placeholders, so these are user-set, default Shuckle/Lv42/100%).
        private int _gaugeHpPercent = 100, _gaugeLevel = 42, _gaugeMaxHp = 250;
        private int _gaugeSpeciesId = 213;   // Shuckle
        public int GaugeHpPercent { get => _gaugeHpPercent; set { if (Set(ref _gaugeHpPercent, Math.Clamp(value, 0, 100))) RaiseHpProps(); } }
        public int GaugeMaxHp { get => _gaugeMaxHp; set { if (Set(ref _gaugeMaxHp, Math.Max(1, value))) RaiseHpProps(); } }
        public int GaugeCurHp => (int)Math.Round(_gaugeMaxHp * _gaugeHpPercent / 100.0);
        public string GaugeHpText => GaugeCurHp + "/" + _gaugeMaxHp;
        private void RaiseHpProps() { OnPropertyChanged(nameof(GaugeHpBarWidth)); OnPropertyChanged(nameof(GaugeHpBrush)); OnPropertyChanged(nameof(GaugeCurHp)); OnPropertyChanged(nameof(GaugeHpText)); }
        public int GaugeLevel { get => _gaugeLevel; set { if (Set(ref _gaugeLevel, Math.Clamp(value, 1, 100))) OnPropertyChanged(nameof(GaugeLevelText)); } }

        // The Pokémon shown in the preview (and named on the gauge); pick it from a dropdown. Changing it
        // live-loads that species' front/back battle sprites into the scene.
        private string[] _speciesNames;
        public string[] SpeciesNames => _speciesNames ??= SafeSpeciesNames();
        private static string[] SafeSpeciesNames() { try { return GetPokemonNames(); } catch { return Array.Empty<string>(); } }
        public int GaugeSpeciesIndex
        {
            get => _gaugeSpeciesId;
            set
            {
                if (!Set(ref _gaugeSpeciesId, value)) return;
                OnPropertyChanged(nameof(GaugeNameText));
                if (_sceneLoaded) LoadDisplayMon(value);
            }
        }
        public string GaugeNameText
        {
            get
            {
                var n = SpeciesNames;
                string s = (_gaugeSpeciesId >= 0 && _gaugeSpeciesId < n.Length) ? n[_gaugeSpeciesId] : "SHUCKLE";
                return (s ?? "").ToUpperInvariant();
            }
        }
        public string GaugeLevelText => "Lv" + _gaugeLevel;
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
        public IBrush GaugeTextBrush => gameFamily == GameFamilies.HGSS
            ? new SolidColorBrush(global::Avalonia.Media.Color.FromRgb(0x28, 0x28, 0x28))
            : new SolidColorBrush(global::Avalonia.Media.Color.FromRgb(0xF8, 0xF8, 0xF8));

        private void RenderGauges()
        {
            try
            {
                var r = _groundRenderer ??= new DSPRE.Avalonia.Data.BattleGroundRenderer();
                GaugePlayerImage = GaugeToBitmap(r.BuildGauge(true));
                GaugeEnemyImage = GaugeToBitmap(r.BuildGauge(false));
                OnPropertyChanged(nameof(HasRealGauges));
                OnPropertyChanged(nameof(ShowPlaceholderGauges));
                OnPropertyChanged(nameof(RealGaugesVisible));
                OnPropertyChanged(nameof(PlaceholderGaugesVisible));
            }
            catch { }
        }

        // GroundImage (256² straight RGBA) → an unpremultiplied BGRA Avalonia bitmap (the frame has transparency).
        private static Bitmap GaugeToBitmap(DSPRE.Avalonia.Data.BattleGroundRenderer.GroundImage g)
        {
            if (g?.Rgba == null) return null;
            int w = g.Width, h = g.Height;
            var wb = new WriteableBitmap(new global::Avalonia.PixelSize(w, h), new global::Avalonia.Vector(96, 96),
                                         global::Avalonia.Platform.PixelFormat.Bgra8888, global::Avalonia.Platform.AlphaFormat.Unpremul);
            var bgra = new byte[w * h * 4];
            for (int i = 0; i < w * h * 4; i += 4) { bgra[i] = g.Rgba[i + 2]; bgra[i + 1] = g.Rgba[i + 1]; bgra[i + 2] = g.Rgba[i]; bgra[i + 3] = g.Rgba[i + 3]; }
            using (var fb = wb.Lock())
            {
                int rb = fb.RowBytes;
                if (rb == w * 4) System.Runtime.InteropServices.Marshal.Copy(bgra, 0, fb.Address, bgra.Length);
                else for (int y = 0; y < h; y++) System.Runtime.InteropServices.Marshal.Copy(bgra, y * w * 4, fb.Address + y * rb, w * 4);
            }
            return wb;
        }

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
                    var img = (_bgRenderer ??= new DSPRE.Avalonia.Data.BattleBgRenderer()).BuildBackdrop(_backgroundIndex - 1);
                    if (img?.Rgba != null) rgb = BgToBackdrop(img.Rgba, img.Width, img.Height);
                }
                catch { rgb = null; }
            }
            // Index 0 is deliberately "No background" (plain black), NOT the bundled placeholder art:
            // effects are easiest to judge against black, and it's honest about not being ROM data.
            _compositor.SetBackdrop(rgb ?? new byte[256 * 192 * 3]);
        }

        // RGBA w×h (battle BG, usually 256×256) → opaque RGB 256×192 backdrop (top-left crop, black-padded).
        private static byte[] BgToBackdrop(byte[] rgba, int w, int h)
        {
            var outp = new byte[256 * 192 * 3];
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
            catch { return null; }
        }

        // Avalonia Bitmap → straight RGBA (un-premultiplied). CopyPixels gives BGRA premultiplied.
        private static (byte[] rgba, int w, int h) ToRgba(Bitmap bmp)
        {
            int w = bmp.PixelSize.Width, h = bmp.PixelSize.Height;
            var buf = new byte[w * h * 4];
            var handle = System.Runtime.InteropServices.GCHandle.Alloc(buf, System.Runtime.InteropServices.GCHandleType.Pinned);
            try { bmp.CopyPixels(new global::Avalonia.PixelRect(0, 0, w, h), handle.AddrOfPinnedObject(), buf.Length, w * 4); }
            finally { handle.Free(); }
            var rgba = new byte[w * h * 4];
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
            var bmp = LoadAsset(name);
            if (bmp == null) return null;
            var (rgba, bw, bh) = ToRgba(bmp);
            var rgb = new byte[w * h * 3];
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
            if (IsWest) { EnsureScene(); SceneComposite = _compositor.Render(null); }   // static scene (backdrop + mons)
            HasCellAnimation = false; HasParticleAnimation = false;
            CellPreview = null; ParticlePreview = null; CellAnimNote = "";
            _west = null; ShakeX = ShakeY = 0; BackgroundDarken = 0;
            if (IsWest && _fileIndex >= 0)
            {
                var cmds = BuildCommands();
                LoadCellResourcesForCommands(cmds, _fileIndex);

                int emitters = WestParticles.Extract(cmds, _version, _attackerIsEnemy).Count;
                HasParticleAnimation = emitters > 0 && _particleNarc.Available;

                CellAnimNote =
                    HasCellAnimation && HasParticleAnimation ? "Cell + particle effect. ▶ to play."
                  : HasCellAnimation ? $"Cell animation: {_cellFrames.Count} frame(s). ▶ to play."
                  : HasParticleAnimation ? $"Particle effect: {emitters} emitter(s). ▶ to play."
                  : "Pokémon-motion effect (no particles). Press ▶ to play the lunge / shake.";
            }
            RaisePreviewProps();
        }

        /// <summary>Loads (or explicitly unloads) the shared <see cref="_cellRenderer"/> for one move's parsed
        /// commands, updating <see cref="HasCellAnimation"/>/<see cref="_cellFrames"/>/<see cref="CellOrigin"/>.
        /// Factored out of <see cref="SetupCellPreview"/> so the Metronome "plays a randomly called move's real
        /// animation" preview (see <see cref="StartChainedWest"/>) can load the CALLED move's own cell resource
        /// the same correct way. Without this, the called move would render with whatever Metronome's own hand
        /// graphics left loaded (the exact stale-cache bug already fixed once for ordinary move-to-move switches).</summary>
        private void LoadCellResourcesForCommands(List<WazaSeqCommand> cmds, int moveIdForLogging)
        {
            HasCellAnimation = false;
            var res = WestCats.Extract(cmds, _version);
            if (res.HasCellAnimation)
            {
                bool loaded = _cellRenderer.Load(res.Char, res.Pltt, res.Cell, res.CellAnm);
                if (loaded)
                {
                    // WE_057 picks the cell animation SEQUENCE by side (0=player /
                    // 1=enemy), sequence 1 is the enemy-facing (flipped) wave.
                    int bank = _attackerIsEnemy && _cellRenderer.AnimationCount > 1 ? 1 : 0;
                    _cellFrames = _cellRenderer.RenderAnimation(bank);
                    if (_cellFrames.Count > 0)
                    {
                        HasCellAnimation = true;   // shown only on ▶ play, not as a static poster
                        CellOrigin = new global::Avalonia.RelativePoint(
                            _cellRenderer.ContentCx / 256.0, _cellRenderer.ContentCy / 192.0,
                            global::Avalonia.RelativeUnit.Relative);
                    }
                }
                AppLogger.Info($"WEST cell-anim file {moveIdForLogging}: char={res.Char} pltt={res.Pltt} cell={res.Cell} " +
                    $"anm={res.CellAnm} → load={loaded} banks={_cellRenderer.AnimationCount} frames={_cellFrames.Count}");
            }
            else
            {
                // _cellRenderer is shared across every move preview in this session (not recreated per
                // move), without explicitly unloading here, a move with no CATS resource of its own
                // would keep whatever the PREVIOUSLY previewed move loaded (e.g. Surf's wave sprite),
                // and any later move whose script still fires a generic ACT_ADD-family opcode would
                // render using that stale graphic instead of nothing.
                _cellRenderer.Unload();
                _cellFrames = Array.Empty<WeCellAnimRenderer.Frame>();
                AppLogger.Info($"WEST file {moveIdForLogging}: no CATS cell-anim (char={res.Char} pltt={res.Pltt} " +
                    $"cell={res.Cell} anm={res.CellAnm})");
            }
        }

        // Metronome's move id (118), consistent across DP/Platinum/HGSS. Its own WEST script is just the
        // self-contained finger-wag flourish; the actual "call a random other move" behaviour lives in the
        // battle engine's move-selection logic, not the animation script, so this preview picks one itself
        // as a plausible-looking bonus once the finger-wag finishes.
        private const int MetronomeMoveId = 118;
        private bool IsMetronomePreview => IsWest && _fileIndex == MetronomeMoveId;
        private static readonly Random _metronomeRandom = new Random();
        private int _metronomeCalledMoveId = -1;

        /// <summary>Picks a move id for Metronome's preview flourish to "call". Real Metronome excludes a long,
        /// specific table of moves (other move-calling moves, signature/exclusive moves, Struggle, etc.), not
        /// ported here, since this is a discretionary visual bonus, not an accuracy requirement; only excludes
        /// Metronome itself, move id 0 (the no-move placeholder) and any id whose script fails to parse.</summary>
        private int PickRandomMetronomeTarget()
        {
            int count = CurrentNarc.Count;
            if (count <= 1) return -1;
            for (int attempt = 0; attempt < 20; attempt++)
            {
                int id = _metronomeRandom.Next(1, count);
                if (id == MetronomeMoveId) continue;
                var bytes = CurrentNarc.Get(id);
                if (bytes != null && bytes.Length >= 4) return id;
            }
            return -1;
        }

        public void ToggleCellPlay()
        {
            if (!HasPreview) return;
            if (IsCellPlaying) { StopCell(); return; }
            _cellFrameIdx = 0; _cellTick = 0; _cellLoops = 0; _previewFrames = 0;
            // Re-establish the CURRENTLY selected move's own cell resource on every fresh play, not just on
            // selection: a previous play may have chained into a Metronome-called move (StartChainedWest)
            // and left ITS graphics loaded, since SetupCellPreview only runs on selection, not on repeated
            // Play clicks for the same move.
            var cmds = BuildCommands();
            LoadCellResourcesForCommands(cmds, _fileIndex);
            if (HasCellAnimation && _cellFrames.Count > 0) CellPreview = _cellFrames[0].Bitmap;
            // Fresh timeline interpreter each play: runs the WEST script, spawning emitters / firing shake+fade
            // at the right frames.
            // Anchor the attacker on the chosen side: player (bottom) by default, enemy (top) when toggled.
            double aX = _attackerIsEnemy ? _dfX : _atX, aY = _attackerIsEnemy ? _dfY : _atY;
            double dX = _attackerIsEnemy ? _atX : _dfX, dY = _attackerIsEnemy ? _atY : _dfY;
            _notesShown = 0;
            _west = new WestPlayer(cmds, _version, _particleNarc, aX, aY, dX, dY,
                                   attackerIsEnemy: _attackerIsEnemy, selfTarget: IsSelfTargetMove())
            { SecondTurnVariant = _secondTurnVariant };
            _west.Cells = _cellRenderer;   // general CATS engine: ACT_ADD opcodes spawn live cell actors from these
            _west.PlaySound = PreviewSound;   // WEST_SE-family opcodes audibly play their sound during preview
            _west.PlayCry = PreviewCry;       // WEST_VOICE_PLAY plays the attacking Pokemon's own cry
            _west.StopSound = PreviewStopSound;
            _west.MovePower = GetMovePower();   // real base power (MoveData.damage) for WE_222's power-scaled shake
            int moveFlag = GetMoveFlagField();  // WazaData byte 11: hide gauges unless FLAG_PUT_GAUGE, shadow if DEL_SHADOW
            _hideGaugesThisMove = moveFlag >= 0 && (moveFlag & 0x40) == 0;
            _hideShadowThisMove = moveFlag >= 0 && (moveFlag & 0x80) != 0;
            _metronomeCalledMoveId = IsMetronomePreview ? PickRandomMetronomeTarget() : -1;

            ParticlePreview = _west.RenderFrame();
            _previewTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000.0 / 60) };
            _previewTimer.Tick -= PreviewTick; _previewTimer.Tick += PreviewTick;
            _previewTimer.Start();
            RaisePreviewProps();
        }

        /// <summary>Swaps the live preview to a SECOND, independent WestPlayer for the move Metronome "called":
        /// its own real WEST script (not the currently-edited grid), so an in-progress edit to Metronome's own
        /// script can't affect it. Reloads the shared cell renderer for the called move's own CATS resource
        /// (same fix as ordinary move-to-move switching, otherwise it would render with Metronome's hand).</summary>
        private void StartChainedWest(int moveId)
        {
            var bytes = CurrentNarc.Get(moveId);
            var cmds = bytes != null ? WestScript.Parse(bytes, _version) : new List<WazaSeqCommand>();
            LoadCellResourcesForCommands(cmds, moveId);

            double aX = _attackerIsEnemy ? _dfX : _atX, aY = _attackerIsEnemy ? _dfY : _atY;
            double dX = _attackerIsEnemy ? _atX : _dfX, dY = _attackerIsEnemy ? _atY : _dfY;
            _west = new WestPlayer(cmds, _version, _particleNarc, aX, aY, dX, dY,
                                   attackerIsEnemy: _attackerIsEnemy, selfTarget: IsSelfTargetMove(moveId))
            { SecondTurnVariant = _secondTurnVariant };
            _west.Cells = _cellRenderer;
            _west.PlaySound = PreviewSound;
            _west.PlayCry = PreviewCry;
            _west.StopSound = PreviewStopSound;
            _west.MovePower = GetMovePower(moveId);
            int moveFlag = GetMoveFlagField(moveId);
            _hideGaugesThisMove = moveFlag >= 0 && (moveFlag & 0x40) == 0;
            _hideShadowThisMove = moveFlag >= 0 && (moveFlag & 0x80) != 0;
            _cellFrameIdx = 0; _cellTick = 0; _cellLoops = 0;
            if (HasCellAnimation && _cellFrames.Count > 0) CellPreview = _cellFrames[0].Bitmap;

            string calledName = moveId >= 0 && moveId < _moveNames.Length ? _moveNames[moveId] : $"Move {moveId}";
            CellAnimNote = $"🎲 Metronome called {calledName}!";
            RaisePreviewProps();
        }

        // A move whose range targets the user (User / User-side / User-or-ally bits) plays its effect on the caster:
        // in-game df_client == at_client. Only meaningful for the move-animation archive (file index = move id).
        // The move's base power (MoveData.damage) when previewing the move-animation archive, else −1 (unknown).
        private int GetMovePower(int moveId = -1)
        {
            if (moveId < 0) moveId = _fileIndex;
            if ((Archive)_archiveIndex != Archive.MoveAnimation || moveId < 0) return -1;
            try { return new MoveData(moveId).damage; }
            catch { return -1; }
        }

        private bool IsSelfTargetMove(int moveId = -1)
        {
            if (moveId < 0) moveId = _fileIndex;
            if ((Archive)_archiveIndex != Archive.MoveAnimation || moveId < 0) return false;
            try
            {
                ushort range = new MoveData(moveId).target;
                const ushort USER = 1 << 4, USER_SIDE = 1 << 5, USER_OR_ALLY = 1 << 9;
                return (range & (USER | USER_SIDE | USER_OR_ALLY)) != 0;
            }
            catch { return false; }
        }

        private void PreviewTick(object sender, EventArgs e)
        {
            // When the WEST script drives CATS cell actors (incl. the Surf WE_057 wave) the cells are composited
            // straight into SceneComposite; hide the legacy CellPreview overlay so it can't double-draw. Only a
            // STANDALONE cell archive (no CATS actors) still previews through the overlay by cycling its frames.
            bool cellsViaCats = _west != null && _west.CatsActors.Count > 0;
            if (!cellsViaCats && HasCellAnimation && _cellFrames.Count > 0 && ++_cellTick >= _cellFrames[_cellFrameIdx].Duration)
            {
                _cellTick = 0;
                if (_cellFrameIdx + 1 >= _cellFrames.Count) _cellLoops++;
                _cellFrameIdx = (_cellFrameIdx + 1) % _cellFrames.Count;
                CellPreview = _cellFrames[_cellFrameIdx].Bitmap;
            }
            if (_west != null)
            {
                if (cellsViaCats && CellOpacity != 0) CellOpacity = 0;
                _west.Step();
                // A note is only found when the command that earns it runs, which is partway through,
                // so the panel has to be told again rather than only when the preview was built.
                if (_west.Notes.Count != _notesShown)
                {
                    _notesShown = _west.Notes.Count;
                    OnPropertyChanged(nameof(PreviewNotes));
                    OnPropertyChanged(nameof(HasPreviewNotes));
                }
                ParticlePreview = _west.RenderFrame();
                SceneComposite = _compositor.Render(_west);   // backdrop + mons + cell actors + effect-BG, blended exactly
                ShakeX = _west.ShakeX; ShakeY = _west.ShakeY;
                BackgroundDarken = _west.FadeOpacity;
                if (_west.FadeOpacity > 0) FadeBrush = new SolidColorBrush(Color.FromRgb(_west.FadeR, _west.FadeG, _west.FadeB));
                PlayerOffsetX = _west.MonDX[0] + _west.MonShakeX[0]; PlayerOffsetY = _west.MonDY[0] + _west.MonShakeY[0];
                PlayerRotation = _west.MonRot[0]; PlayerScaleX = _west.MonScaleX[0]; PlayerScaleY = _west.MonScaleY[0];
                PlayerVisible = _west.MonVisible[0]; PlayerTintOpacity = _west.MonTintA[0];
                EnemyOffsetX = _west.MonDX[1] + _west.MonShakeX[1]; EnemyOffsetY = _west.MonDY[1] + _west.MonShakeY[1];
                EnemyRotation = _west.MonRot[1]; EnemyScaleX = _west.MonScaleX[1]; EnemyScaleY = _west.MonScaleY[1];
                EnemyVisible = _west.MonVisible[1]; EnemyTintOpacity = _west.MonTintA[1];
                if (_west.MonTintA[0] > 0 || _west.MonTintA[1] > 0)
                    TintBrush = new SolidColorBrush(Color.FromRgb(_west.TintR, _west.TintG, _west.TintB));
            }
            bool done = _west != null ? _west.Finished : (_cellLoops >= 1);
            if (done && _metronomeCalledMoveId >= 0)
            {
                int calledMoveId = _metronomeCalledMoveId;
                _metronomeCalledMoveId = -1;   // only chain once; the called move doesn't itself call another
                _previewFrames = 0;
                StartChainedWest(calledMoveId);
                return;   // keep the timer running for the chained move instead of stopping
            }
            if (done || ++_previewFrames >= MaxPreviewFrames) StopCell();
        }

        private void StopCell()
        {
            _previewTimer?.Stop();
            _metronomeCalledMoveId = -1;   // manually stopped mid-Metronome; don't chain into it on a later, unrelated play
            BackgroundFrame = null;
            CellPreview = null; ParticlePreview = null;   // don't leave the last wave/particle frame statically on screen
            // Re-render the static scene: the last played frame may have mons hidden / dragged (Dark Void
            // vanishes the defender mid-effect). Without this the frozen composite keeps them invisible
            // after playback ends.
            if (IsWest && _sceneLoaded) SceneComposite = _compositor.Render(null);
            CellScaleX = CellScaleY = CellOpacity = 1;
            ShakeX = ShakeY = 0; BackgroundDarken = 0;
            PlayerOffsetX = PlayerOffsetY = EnemyOffsetX = EnemyOffsetY = 0;
            PlayerRotation = EnemyRotation = 0; PlayerScaleX = PlayerScaleY = EnemyScaleX = EnemyScaleY = 1;
            PlayerTintOpacity = EnemyTintOpacity = 0; PlayerVisible = EnemyVisible = true;
            OnPropertyChanged(nameof(IsCellPlaying));
            OnPropertyChanged(nameof(CellPlayButtonText));
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
            OnPropertyChanged(nameof(CellAnimNote));
            OnPropertyChanged(nameof(PreviewNotes));
            OnPropertyChanged(nameof(HasPreviewNotes));
            OnPropertyChanged(nameof(IsCellPlaying));
            OnPropertyChanged(nameof(CellPlayButtonText));
            OnPropertyChanged(nameof(GaugesVisible));
            OnPropertyChanged(nameof(ShadowHidden));
            OnPropertyChanged(nameof(RealGaugesVisible));
            OnPropertyChanged(nameof(PlaceholderGaugesVisible));
        }
    }

    /// <summary>One editable command row: an opcode id (bound to the archive's opcode dropdown by index) plus its
    /// argument words as an editable comma/space/0x list. <see cref="Hint"/> is set by the VM.</summary>
    /// <summary>One command in the structured editor: a collapsible card. Collapsed it shows <see cref="Summary"/>
    /// (read-only); expanded it shows the opcode dropdown + one typed editor per argument (<see cref="Params"/>) and
    /// a raw-args escape hatch for variable-length opcodes. <see cref="Args"/> is the canonical value store.</summary>
    public sealed class ScriptCmdRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        // Context wired by the view-model so the row can name its opcode/params and report edits.
        internal System.Func<int, string> OpNameOf;
        internal System.Func<int, int> FixedArgCountOf;
        internal System.Action<ScriptCmdRow> OnEdited;
        internal System.Func<int, string> PreviewSound;   // returns an error message, or null on success
        internal System.Func<int, string> SoundNameOf;

        public System.Collections.Generic.List<int> Args { get; } = new();
        public ObservableCollection<ParamVM> Params { get; } = new();

        private int _opId;
        public int OpId
        {
            get => _opId;
            set { if (_opId != value) { _opId = value; Raise(nameof(OpId)); Raise(nameof(OpName)); Raise(nameof(OpDisplay)); PadArgs(); Rebuild(); OnEdited?.Invoke(this); } }
        }
        // Ensure the args list has at least the new opcode's fixed parameter count (pad with 0). Extra args are kept
        // (variable-length opcodes); the user can trim them via the raw-args field.
        private void PadArgs() { int need = FixedArgCountOf?.Invoke(_opId) ?? 0; while (Args.Count < need) Args.Add(0); }
        public string OpName => OpNameOf?.Invoke(_opId) ?? ("op" + _opId);
        public string OpDisplay => DSPRE.Avalonia.Data.WestParamSchema.OpcodeDisplay(OpName);
        public string OpDoc => DSPRE.Avalonia.Data.WestParamSchema.OpcodeDoc(OpName);
        public bool HasDoc => !string.IsNullOrEmpty(OpDoc);
        // Set the opcode during load without rebuilding/raising an edit (the caller rebuilds + keeps the loaded args).
        internal void _opIdSilent(int id) { _opId = id; Raise(nameof(OpId)); Raise(nameof(OpName)); Raise(nameof(OpDisplay)); Raise(nameof(OpDoc)); Raise(nameof(HasDoc)); }

        private bool _expanded;
        public bool IsExpanded { get => _expanded; set { if (_expanded != value) { _expanded = value; Raise(nameof(IsExpanded)); } } }

        // Collapsed one-liner: "OPCODE  name=val  name=val …"
        public string Summary
        {
            get
            {
                if (Args.Count == 0) return OpDisplay;
                var sb = new System.Text.StringBuilder(OpDisplay).Append("  ");
                for (int i = 0; i < Args.Count; i++)
                {
                    if (i > 0) sb.Append("  ");
                    sb.Append(DSPRE.Avalonia.Data.WestParamSchema.ParamName(OpName, i)).Append('=').Append(Args[i]);
                }
                return sb.ToString();
            }
        }

        // Raw comma/space-separated args; lets the user add/remove arguments (needed for variable-length opcodes).
        public string RawArgs
        {
            get => string.Join(", ", Args);
            set
            {
                Args.Clear();
                foreach (var t in (value ?? "").Split(new[] { ',', ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries))
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
                Params.Add(new ParamVM(DSPRE.Avalonia.Data.WestParamSchema.ParamName(OpName, i), Args[i], i, this,
                                       DSPRE.Avalonia.Data.WestParamSchema.EnumFor(OpName, i)));
            Raise(nameof(Summary)); Raise(nameof(RawArgs)); Raise(nameof(HasParams));
        }
        public bool HasParams => Args.Count > 0;

        internal void SetParam(int index, int v)
        {
            if (index < 0 || index >= Args.Count || Args[index] == v) return;
            Args[index] = v;
            Raise(nameof(Summary)); Raise(nameof(RawArgs));
            OnEdited?.Invoke(this);
        }
    }

    /// <summary>One argument editor inside a <see cref="ScriptCmdRow"/> card (currently an integer entry; enum
    /// dropdowns can be added per-parameter later). Writes straight back into the row's canonical args.</summary>
    public sealed class ParamVM : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private readonly ScriptCmdRow _row;
        private readonly int _index;
        public string Name { get; }

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

        public ParamVM(string name, int value, int index, ScriptCmdRow row, DSPRE.Avalonia.Data.WestParamSchema.EnumOption[] options)
        {
            Name = name; _value = value; _index = index; _row = row;
            if (options != null)
            {
                IsEnum = true;
                foreach (var o in options) { EnumItems.Add($"{o.Label}  ({o.Value})"); _enumValues.Add(o.Value); }
                if (!_enumValues.Contains(value)) { EnumItems.Add($"(raw {value})"); _enumValues.Add(value); }   // keep an out-of-table value selectable
            }
        }
    }
}
