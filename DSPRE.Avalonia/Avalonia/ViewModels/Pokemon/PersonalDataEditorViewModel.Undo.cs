using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using DSPRE.Resources;
using DSPRE.Avalonia.Views.Shell;
using DSPRE.Csv;
using IEditorWithUnsavedChanges = global::DSPRE.Editors.IEditorWithUnsavedChanges;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    public partial class PersonalDataEditorViewModel
    {
        private sealed class PersonalSnapshot { public byte[] Data; public int Hatch; public HgStaged Hg; public FollowerStaged[] Followers; public byte[][] Athlon; public int[] ExtraTms; public int[] SourceMachineMoves; }

        // PlatPatches' TM121+ compatibility is in the synthetic overlay, staged until Save.
        private HashSet<int> _extraMaskTms = new HashSet<int>(), _extraMaskTmsSaved = new HashSet<int>();
        private static int FirstMaskMachine => TMEditor.VanillaMachineCount + PlatPatches.PersonalMaskRows;

        // hg-engine lists machine moves in learnsets.json, not as bits. Staged until Save, in file order.
        private List<int> _sourceMachineMoves, _sourceMachineMovesSaved;
        private HashSet<int> _sourceLevelUpMoves = new HashSet<int>();
        private int[] _machineMoveIds = Array.Empty<int>();
        private readonly UndoHistory<PersonalSnapshot> _history = new();
        private PersonalSnapshot _savedSnapshot;   // UndoHistory doesn't expose it, and Discard restores it
        private DateTime _lastCaptureUtc = DateTime.MinValue;
        private const int CoalesceMs = 500;

        public bool CanUndo => _history.CanUndo;
        public bool CanRedo => _history.CanRedo;
        public void Undo() { if (_history.CanUndo) ApplyState(_history.Undo()); }
        public void Redo() { if (_history.CanRedo) ApplyState(_history.Redo()); }
        private void RaiseUndoState() { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); }

        private PersonalSnapshot Snapshot() =>
            new PersonalSnapshot { Data = _current.ToByteArray(), Hatch = _hatchResultIndex, Hg = CaptureHg(), Followers = (FollowerStaged[])_followers?.Clone(), Athlon = _athlon?.Select(r => (byte[])r.Clone()).ToArray(), ExtraTms = _extraMaskTms.OrderBy(i => i).ToArray(), SourceMachineMoves = _sourceMachineMoves?.ToArray() };

        private void ApplyState(PersonalSnapshot snap)
        {
            if (snap == null || _current == null) return;
            _loading = true;
            _current = new PokemonPersonalData(new MemoryStream(snap.Data));
            if (snap.ExtraTms != null) _extraMaskTms = new HashSet<int>(snap.ExtraTms);
            if (snap.SourceMachineMoves != null) _sourceMachineMoves = snap.SourceMachineMoves.ToList();
            PopulateFromCurrent();
            _hatchResultIndex = snap.Hatch; OnPropertyChanged(nameof(HatchResultIndex));
            ApplyHg(snap.Hg);
            if (snap.Followers != null) { _followers = (FollowerStaged[])snap.Followers.Clone(); ShowFollower(); }
            if (snap.Athlon != null) { _athlon = snap.Athlon.Select(r => (byte[])r.Clone()).ToArray(); ShowPokeathlonForm(); }
            _loading = false;

            _dirty = _history.IsDirty;
            Title = _dirty ? "● Personal data editor" : "Personal data editor";
            OnPropertyChanged(nameof(HasUnsavedChanges));
            RaiseUndoState();
        }

        private void RecordUndoSnapshot()
        {
            if (_current == null) return;
            bool coalesce = (DateTime.UtcNow - _lastCaptureUtc).TotalMilliseconds < CoalesceMs;
            _history.Capture(Snapshot(), coalesce);
            _lastCaptureUtc = DateTime.UtcNow;
            RaiseUndoState();
        }
        private string[] _allFileNames;
        private string[] _machineMoveNames;
        private int[] _machineMoves = System.Array.Empty<int>();
        private string[] _typeNamesArr;
        private string[] _abilityNamesArr;
        private string[] _itemNamesArr;
    }
}
