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
using DSPRE.Avalonia.Data;
using static DSPRE.RomInfo;
using Avalonia.Platform;
using System.Runtime.InteropServices;

namespace DSPRE.Avalonia.ViewModels.Battle
{
    public sealed partial class BattleScriptEditorViewModel
    {
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
            SyncTextFromRows();
            RefreshViewLines();
            RefreshStoryboard();
            SetupCellPreview();
            OnPropertyChanged(nameof(HasRows));
            OnPropertyChanged(nameof(EntryHeader));
        }

        // Bytes after the entry's last whole word, carried through a save.
        private byte[] _tail;

        // Steps over the entry's bytes, so card and text edits undo alike. hg-engine text undoes itself.
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

        // The dropdown index is the opcode id.
        private string OpNameOf(int opId) => opId >= 0 && opId < OpcodeNames.Count ? OpcodeNames[opId] : "op" + opId;

        // Words the game reads after the opcode. An animation command's variable list adds its count.
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
            if (_holding) { int frame = PreviewFrame; _holding = false; SeekTo(frame); }
        }

        public void AddCommand()
        {
            AddRow(0, Array.Empty<int>());
            Dirty = DiffersFromLoaded();
            RefreshViewLines();
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
            RefreshViewLines();
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
            RefreshViewLines();
            RefreshStoryboard();
            SyncTextFromRows();
            Stepped();
        }

        public void Save()
        {
            if (IsHgEngineSource) { DialogHelper.Watch(SaveSourceAsync()); return; }
            if (!IsAvailable || _fileIndex < 0 || !CurrentNarc.Available) return;
            if (HasTextErrors)   // the cards are stale while the text is invalid
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
            LoadEntry();
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

        /// <summary>The particle files this script loads, in load order.</summary>
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

        public int FileIndex => _fileIndex;

        private List<WazaSeqCommand> BuildCommands()
        {
            List<WazaSeqCommand> list = new List<WazaSeqCommand>();
            foreach (ScriptCmdRow row in Rows)
            {
                int[] args;
                if (IsAnimation || row.Raw)
                {
                    // Animation commands are variable-length and a raw row is undecoded words: args stay exactly.
                    args = row.Args.ToArray();
                }
                else
                {
                    // waza, be and sub opcodes are fixed-length or sized by a message tag, so pad or cut to it.
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
    }
}
