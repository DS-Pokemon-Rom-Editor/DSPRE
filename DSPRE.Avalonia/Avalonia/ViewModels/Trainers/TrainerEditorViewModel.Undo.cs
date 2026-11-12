using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Platform.Storage;
using DSPRE.Avalonia;
using DSPRE.Editors;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;
using Avalonia.Threading;

namespace DSPRE.Avalonia.ViewModels.Trainers
{
    public partial class TrainerEditorViewModel
    {
        private bool _dirty;
        public bool HasUnsavedChanges => _dirty || _classes?.HasUnsavedChanges == true;
        public string UnsavedChangesDescription =>
            _dirty && _classes?.HasUnsavedChanges == true ? $"{TrainerLabel}, {_classes.UnsavedChangesDescription}"
            : _dirty || _classes == null ? TrainerLabel
            : _classes.UnsavedChangesDescription;

        private string TrainerLabel => _loadedTrainerId >= 0 && _loadedTrainerId < TrainerNames.Count ? TrainerNames[_loadedTrainerId] : $"Trainer {_loadedTrainerId}";
        public void SaveChanges()
        {
            if (_dirty) Save();
            if (_classes?.HasUnsavedChanges == true) _classes.SaveChanges();
        }
        public void DiscardChanges()
        {
            _dirty = false;
            OnPropertyChanged(nameof(HasUnsavedChanges));
            if (_selectedTrainerIndex >= 0) LoadTrainer(_selectedTrainerIndex);
            _classes?.DiscardChanges();
        }

        /// <summary>Reloads the shown trainer from disk; the Classes tab keeps its own edits.</summary>
        public void DiscardTrainer()
        {
            if (_selectedTrainerIndex >= 0) LoadTrainer(_selectedTrainerIndex);
        }

        /// <summary>The Classes tab shares this window, so its unsaved edits count here too.</summary>
        public TrainerClassesViewModel Classes
        {
            get => _classes;
            set
            {
                if (_classes != null) _classes.PropertyChanged -= OnClassesChanged;
                _classes = value;
                if (_classes != null) _classes.PropertyChanged += OnClassesChanged;
                OnPropertyChanged(nameof(HasUnsavedChanges));
            }
        }
        private TrainerClassesViewModel _classes;
        private void OnClassesChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(HasUnsavedChanges)) OnPropertyChanged(nameof(HasUnsavedChanges));
        }
        // The snapshot is recorded before the _dirty check, so every edit is captured, not only the first.
        private void SetDirty() { RaisePrize(); QueueDerived(); if (_suppress) return; RecordUndoSnapshot(); if (_dirty) return; _dirty = true; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        private void SetClean() { if (!_dirty) return; _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        // Snapshot: trp, party and name bytes, or packed fields for hg-engine. Bursts merge into one.
        private sealed class TrainerSnapshot { public byte[] Trp; public byte[] Party; public string Name; public byte[] Source; }
        private readonly UndoHistory<TrainerSnapshot> _history = new();
        private DateTime _lastCaptureUtc = DateTime.MinValue;
        private const int CoalesceMs = 500;

        public bool CanUndo => _history.CanUndo;
        public bool CanRedo => _history.CanRedo;
        public void Undo() { if (_history.CanUndo) ApplyState(_history.Undo()); }
        public void Redo() { if (_history.CanRedo) ApplyState(_history.Redo()); }
        private void RaiseUndoState() { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); }

        private byte[] PackSourceFields() => ByteStateUndo.Pack(w =>
        {
            w.Write(_trainerName ?? ""); w.Write(_trainerClassIndex); w.Write(BattleTypeIndex); w.Write((int)_partyCount);
            foreach (AiFlagViewModel f in TrainerDataTypeFlags) w.Write(f.Checked);
            foreach (AiFlagViewModel f in AiFlags) w.Write(f.Checked);
            foreach (TrainerItemSlotViewModel item in TrainerItems) w.Write(item.ItemIndex);
            foreach (TrainerPartyMonViewModel mon in Party)
            {
                w.Write(mon.SpeciesIndex); w.Write((int)mon.FormId); w.Write((int)mon.Level); w.Write(mon.ItemIndex); w.Write(mon.AbilityIndex);
                w.Write((int)mon.Difficulty); w.Write((int)mon.BallSeals);
                foreach (int move in new[] { mon.Move1, mon.Move2, mon.Move3, mon.Move4 }) w.Write(move);
                mon.HgeExtras.WriteTo(w);
            }
        });

        private void UnpackSourceFields(byte[] state) => ByteStateUndo.Unpack(state, r =>
        {
            TrainerName = r.ReadString(); TrainerClassIndex = r.ReadInt32(); BattleTypeIndex = r.ReadInt32(); PartyCount = r.ReadInt32();
            foreach (AiFlagViewModel f in TrainerDataTypeFlags) f.Checked = r.ReadBoolean();
            RaiseHgeFlagCheckedChanged();
            ChooseMoves = HgeMovesFlagChecked;
            ChooseItems = HgeItemsFlagChecked;
            foreach (AiFlagViewModel f in AiFlags) f.Checked = r.ReadBoolean();
            foreach (TrainerItemSlotViewModel item in TrainerItems) item.Load(r.ReadInt32());
            foreach (TrainerPartyMonViewModel mon in Party)
            {
                int species = r.ReadInt32(), form = r.ReadInt32(), level = r.ReadInt32(), held = r.ReadInt32(), ability = r.ReadInt32();
                int ivs = r.ReadInt32(), seals = r.ReadInt32();
                int[] moves = { r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32() };
                mon.Load(species, form, level, moves, held, 0, ability, ivs, seals);
                mon.HgeExtras.ReadFrom(r);
            }
            ApplyMovesEnabled();
            ApplyItemsEnabled();
            ApplyPartyVisibility();
            ApplyHgeGatingToParty();
        });

        private TrainerSnapshot Snapshot()
        {
            if (IsHgeActive) return new TrainerSnapshot { Source = PackSourceFields() };
            if (_trainer == null) return null;
            SyncToTrainer();
            return new TrainerSnapshot
            {
                Trp   = _trainer.Trp.ToByteArray(),
                // Every slot, including those past the party count, so undo brings hidden ones back.
                Party = _trainer.Party.ToByteArray(TrainerFile.PokeInParty),
                Name  = _trainerName,
            };
        }

        private void ApplyState(TrainerSnapshot snap)
        {
            if (snap == null || (_trainer == null && snap.Source == null) || _loadedTrainerId < 0) return;
            _suppress = true;
            if (snap.Source != null) UnpackSourceFields(snap.Source);
            else
            {
                TrainerProperties trp = new TrainerProperties((ushort)_loadedTrainerId, new MemoryStream(snap.Trp));
                byte partyCount = trp.PartyCount;
                trp.PartyCount = TrainerFile.PokeInParty;
                _trainer = new TrainerFile(trp, new MemoryStream(snap.Party), snap.Name);
                trp.PartyCount = partyCount;
                PopulateFromTrainer();
            }
            _suppress = false;
            QueueDerived();

            _dirty = _history.IsDirty;
            OnPropertyChanged(nameof(HasUnsavedChanges));
            RaiseUndoState();
        }

        private void RecordUndoSnapshot()
        {
            if (_suppress || (_trainer == null && !IsHgeActive)) return;
            bool coalesce = (DateTime.UtcNow - _lastCaptureUtc).TotalMilliseconds < CoalesceMs;
            _history.Capture(Snapshot(), coalesce);
            _lastCaptureUtc = DateTime.UtcNow;
            RaiseUndoState();
        }
    }
}
