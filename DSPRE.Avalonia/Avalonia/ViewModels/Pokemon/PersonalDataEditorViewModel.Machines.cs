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
        public void AddMachineCommand()
        {
            if (_current == null || _selectedAddableMachineIndex < 0 || _selectedAddableMachineIndex >= AddableMachines.Count) return;
            int idx = AddableMachines[_selectedAddableMachineIndex].Index;
            if (idx < 0) return;
            SetMachine(idx, true);
            RebuildMachineLists();
            SetDirty();
        }

        public void RemoveMachineCommand()
        {
            if (_current == null || _selectedAddedMachineIndex < 0 || _selectedAddedMachineIndex >= AddedMachines.Count) return;
            int idx = AddedMachines[_selectedAddedMachineIndex].Index;
            if (LearnedByLevelUp(idx)) return;   // the build adds level-up moves back anyway
            if (idx < 0) return;
            SetMachine(idx, false);
            RebuildMachineLists();
            SetDirty();
        }

        // Both only touch the machines listed, so compatibility bits this ROM doesn't list survive.
        public void AddAllMachinesCommand()
        {
            if (_current == null) return;
            for (int i = 0; i < TMEditor.MachineCount; i++) SetMachine(i, true);
            RebuildMachineLists();
            SetDirty();
        }

        public void RemoveAllMachinesCommand()
        {
            if (_current == null) return;
            for (int i = 0; i < TMEditor.MachineCount; i++) SetMachine(i, false);
            RebuildMachineLists();
            SetDirty();
        }

        private bool HasMachine(int index)
        {
            if (_sourceMachineMoves != null)
                return index < _machineMoveIds.Length && (_sourceMachineMoves.Contains(_machineMoveIds[index]) || _sourceLevelUpMoves.Contains(_machineMoveIds[index]));
            return index >= FirstMaskMachine ? _extraMaskTms.Contains(index) : _current.Machines.Contains((byte)index);
        }

        // On hg-engine a machine whose move is also learned by level-up is always compatible.
        private bool LearnedByLevelUp(int index) =>
            _sourceMachineMoves != null && index < _machineMoveIds.Length && _sourceLevelUpMoves.Contains(_machineMoveIds[index]);

        private void SetMachine(int index, bool on)
        {
            if (_sourceMachineMoves != null)
            {
                if (index >= _machineMoveIds.Length) return;
                int move = _machineMoveIds[index];
                if (on && !_sourceMachineMoves.Contains(move)) _sourceMachineMoves.Add(move);
                else if (!on) _sourceMachineMoves.Remove(move);
                return;
            }
            if (index >= FirstMaskMachine) { if (on) _extraMaskTms.Add(index); else _extraMaskTms.Remove(index); return; }
            if (on) _current.Machines.Add((byte)index); else _current.Machines.Remove((byte)index);
        }

        // From learnsets.json on an hg-engine folder.
        private void LoadSourceMachineMoves()
        {
            _sourceMachineMoves = _sourceMachineMovesSaved = null;
            _sourceLevelUpMoves = new HashSet<int>();
            if (!HgEngineProject.IsActive || _currentId < 0) return;
            _machineMoveIds = TMEditor.ReadMachineMoves();
            // Inherited lists are the build's fallback for forms; saving gives the form its own list.
            if (!HgEngineLearnsets.TryGetMoveNames(_currentId, HgEngineLearnsets.MachineMovesField, out List<int> moves, out _, out string error)
                || !HgEngineLearnsets.TryGetLevelMoves(_currentId, out List<(int level, int move)> levelUp, out error))
            {
                AppLogger.Error($"learnsets.json read failed for species {_currentId}: {error}");
                return;
            }
            _sourceMachineMoves = moves;
            _sourceMachineMovesSaved = new List<int>(moves);
            _sourceLevelUpMoves = levelUp.Select(m => m.move).ToHashSet();
        }

        private string SaveSourceMachineMoves()
        {
            if (_sourceMachineMoves == null || _sourceMachineMoves.SequenceEqual(_sourceMachineMovesSaved)) return null;
            if (!HgEngineLearnsets.TrySaveMoveNames(_currentId, HgEngineLearnsets.MachineMovesField, _sourceMachineMoves, out string error))
                return error;
            _sourceMachineMovesSaved = new List<int>(_sourceMachineMoves);
            return null;
        }

        // TM121 and up are kept in the synthetic overlay.
        private void LoadExtraMaskTms()
        {
            _extraMaskTms = new HashSet<int>();
            PlatPatches.ExtraTms t = PlatPatches.Tms();
            if (t != null && _currentId >= 0)
                foreach ((int row, int _) in PlatPatches.Compatibility(t, new[] { _currentId }, PlatPatches.PersonalMaskRows))
                    _extraMaskTms.Add(TMEditor.VanillaMachineCount + row);
            _extraMaskTmsSaved = new HashSet<int>(_extraMaskTms);
        }

        private string SaveExtraMaskTms()
        {
            PlatPatches.ExtraTms t = PlatPatches.Tms();
            if (t == null || _extraMaskTms.SetEquals(_extraMaskTmsSaved)) return null;
            IEnumerable<(int, int _currentId, bool)> changes = _extraMaskTms.Union(_extraMaskTmsSaved)
                .Where(i => _extraMaskTms.Contains(i) != _extraMaskTmsSaved.Contains(i))
                .Select(i => (i - TMEditor.VanillaMachineCount, _currentId, _extraMaskTms.Contains(i)));
            try { PlatPatches.SetCanLearn(t, changes); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException || e is ArgumentException)
            { return e.Message; }
            _extraMaskTmsSaved = new HashSet<int>(_extraMaskTms);
            return null;
        }
    }
}
