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

        /// <summary>Whether an animation archive is open: move animations or their subroutines.</summary>
        public bool IsAnimation => (Archive)_archiveIndex is Archive.MoveAnimation or Archive.SubAnimation;
        private ScriptNarc CurrentNarc => _narcs[_archiveIndex];
        private readonly DirNames[] _dirs = new DirNames[5];
        private Dictionary<int, HgEngineOwnedFile> _sourceById;
        private List<int> _sourceOrder = new List<int>();

        public ScriptCommandGuideViewModel BuildCommandGuideViewModel() => new ScriptCommandGuideViewModel(IsAnimation);


        /// <summary>The lines the chosen view shows. All three read the same commands.</summary>
        public ObservableCollection<BattleAnimLine> ViewLines { get; } = new ObservableCollection<BattleAnimLine>();

        private BattleAnimLine _pickedLine;
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
        public int OpenTab
        {
            get => _openTab;
            set { if (Set(ref _openTab, value)) OnPropertyChanged(nameof(ShowTextHelp)); }
        }

        /// <summary>The writing note shows on Script for animations, always for other scripts.</summary>
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
            foreach (string n in OpcodeNames) OpcodeDisplayNames.Add(BattleAnimSchema.OpcodeDisplay(n, !IsAnimation));
            _nameToOp = null;   // the opcode table changed, so the name map is rebuilt on next use

            BuildFileList();
            OnPropertyChanged(nameof(IsAnimation));
            OnPropertyChanged(nameof(IsHgEngineSource));
            OnPropertyChanged(nameof(SourceNote));
            OnPropertyChanged(nameof(ShowTextHelp));
            OnPropertyChanged(nameof(ArchiveNotAvailable));
            OnPropertyChanged(nameof(ArchiveUnavailableText));
            _fileIndex = -1;
            SelectedFileIndex = FileItems.Count > 0 ? 0 : -1;
        }

        public bool ArchiveNotAvailable => IsAvailable && _archiveIndex >= 0 && !CurrentNarc.Available;
        public string ArchiveUnavailableText =>
            $"Not available for {GameFamily} yet.";

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
            ListSync.Apply(FileItems, labels);
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
    }
}
