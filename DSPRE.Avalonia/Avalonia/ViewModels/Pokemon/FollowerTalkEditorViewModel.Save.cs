using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;
using Slot = DSPRE.ROMFiles.FollowerTalk.Slot;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    public partial class FollowerTalkEditorViewModel
    {
        // Reached by undo, redo, discard or any save. Own edits are already in memory.
        private void OnPendingChanged()
        {
            if (_own) return;
            Load();
            foreach (SlotRow r in Slots) r.Refresh();
            SlotEditor?.Refresh();
            foreach (ReactionRow r in _allReactions) r.Refresh();
            ReactionEditor?.Refresh();
            if (MoveRows.Count != MoveCount) BuildMoveRows();
            else foreach (MoveRow r in MoveRows) r.Refresh();
            MoveEditor?.Refresh();
            foreach (SpeciesRow r in SpeciesRows) r.Refresh();
            RaiseSlotState();
            AfterEdit();
        }

        // The Text editor saved this bank: show its lines unless this editor has its own edits to them.
        private void OnTextSaved(object sender, int id)
        {
            if (id != FollowerTalkTextArchive || ReferenceEquals(sender, this)) return;
            global::Avalonia.Threading.Dispatcher.UIThread.Post(OnPendingChanged);
        }

        public void Detach() => TextArchive.Saved -= OnTextSaved;

        /// <summary>Raised after any change, undo or redo, so the preview can redraw.</summary>
        public event Action Edited;

        public bool CanUndo => _pending?.CanUndo == true;
        public bool CanRedo => _pending?.CanRedo == true;
        public void Undo() => _pending?.Undo();
        public void Redo() => _pending?.Redo();

        public bool HasUnsavedChanges => _pending?.IsDirty == true;
        public string UnsavedChangesDescription => "Follower talk";

        public void SaveChanges() => DialogHelper.Watch(SaveChangesAsync());

        public async Task<bool> SaveChangesAsync()
        {
            if (_pending == null) return true;
            try { _pending.Save(); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException)
            {
                await DialogHelper.ShowErrorAsync("Follower talk wasn't saved:\n" + e.Message, "Follower talk");
                return false;
            }
            AfterEdit();
            SaveNotice.Saved(UnsavedChangesDescription);
            return true;
        }

        public void DiscardChanges() => _pending?.Discard();
    }
}
