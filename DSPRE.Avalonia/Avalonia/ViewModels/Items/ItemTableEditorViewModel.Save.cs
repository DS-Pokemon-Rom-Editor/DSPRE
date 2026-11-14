using Avalonia.Controls;
using DSPRE.Editors;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Items
{
    public partial class ItemTableEditorViewModel
    {
        public void SaveChanges()
        {
            if (_pickupDirty && ShowPickupTab) SavePickupTable();
            if (_hiddenDirty && ShowHiddenItemsTab) SaveHiddenItems();
            if (_rockSmashDirty && ShowRockSmashTab) SaveRockSmash();
        }

        async System.Threading.Tasks.Task<bool> IEditorWithUnsavedChanges.SaveChangesAsync()
        {
            if (_pickupDirty && ShowPickupTab) SavePickupTable();
            if (_hiddenDirty && ShowHiddenItemsTab)
            {
                if (HgEngineProject.IsActive) await SaveHiddenItemsToSourceAsync();
                else SaveHiddenItems();
            }
            if (_rockSmashDirty && ShowRockSmashTab) SaveRockSmash();
            return !HasUnsavedChanges;
        }

        private async System.Threading.Tasks.Task SaveHiddenItemsToSourceAsync()
        {
            List<HgEngineHiddenItems.Entry> entries = new List<HgEngineHiddenItems.Entry>(HiddenItems.Count);
            foreach (HiddenItemRowVM e in HiddenItems)
                entries.Add(new HgEngineHiddenItems.Entry { ItemId = e.ItemID, Quantity = e.Amount, Index = e.ScriptID });
            (bool saved, string error) = await DSPRE.Avalonia.HgEngineSave.RunAsync(() => HgEngineHiddenItems.TrySave(entries, out string err) ? null : err);
            if (!saved)
            {
                if (error != null) await DialogHelper.ShowErrorAsync($"Hidden items weren't saved.\n{error}", "Item tables");
                return;
            }

            _hiddenDirty = false;
            SavedTab();
            SaveNotice.Saved(UnsavedChangesDescription);
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        // A tab just written becomes its new saved state. Undo's saved mark follows once nothing is left.
        private void SavedTab()
        {
            if (_saved == null) return;
            _saved = new TablesState(
                _pickupDirty ? _saved.Pickup : CurrentPickup(),
                _hiddenDirty ? _saved.Hidden : CurrentHidden(),
                _rockSmashDirty ? _saved.RockSmash : CurrentRockSmash(),
                _rockSmashDirty ? _saved.Slots : CurrentSlots());
            if (!HasUnsavedChanges) _undo?.MarkSaved();
        }

        private void SavePickupTable()
        {
            _pickup.Write();

            _pickupDirty = false;
            PickupLinkNote = "";
            SavedTab();
            SaveNotice.Saved(UnsavedChangesDescription);
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private void SaveHiddenItems()
        {
            if (HgEngineProject.IsActive)
            {
                DialogHelper.Watch(SaveHiddenItemsToSourceAsync());
                return;
            }

            HiddenItemRowVM empty = HiddenItems.FirstOrDefault(h => h.ItemID == 0);
            string refusal = !_hiddenSitesValid ? "The ARM9 count checks aren't where expected."
                : HiddenItems.Count > _hiddenMaxCapacity ? $"There is room for {_hiddenMaxCapacity} entries."
                : empty != null ? $"Script {empty.ScriptID} has no item. Pick one or remove the entry."
                : null;
            if (refusal != null)
            {
                _ = DialogHelper.ShowErrorAsync($"Hidden items weren't saved.\n{refusal}", "Item tables");
                return;
            }

            HiddenItemTable.Write(HiddenItems.Select(e => new HiddenItemTable.Entry
            {
                Item = e.ItemID, Quantity = (byte)e.Amount, Range = e.Range, Padding = e.Padding, Script = e.ScriptID,
            }).ToList(), _hiddenMaxCapacity);

            _hiddenDirty = false;
            SavedTab();
            SaveNotice.Saved(UnsavedChangesDescription);
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private void SaveRockSmash()
        {
            // Writing the whole file also creates one for a header that had none in a/2/5/3.
            foreach (RockSmashHeaderRow row in RockSmashRows)
            {
                row.Data.SaveToFile();
                row.RefreshMissing();
            }

            if (ShowRockSmashItemTables)
            {
                try
                {
                    RockSmashItemSlots.Write(RockSmashItemSlots.DefaultOffset, RockSmashDefaultTable.ItemIDs);
                    RockSmashItemSlots.Write(RockSmashItemSlots.RuinsOfAlphOffset, RockSmashRuinsOfAlphTable.ItemIDs);
                    RockSmashItemSlots.Write(RockSmashItemSlots.CliffCaveOffset, RockSmashCliffCaveTable.ItemIDs);
                }
                catch (Exception e) when (e is InvalidOperationException || e is System.IO.IOException || e is UnauthorizedAccessException)
                {
                    AppLogger.Error("Item Tables, Rock Smash items: " + e.Message);
                    _ = DialogHelper.ShowErrorAsync("The Rock Smash items weren't saved:\n" + e.Message, "Item tables");
                    return;
                }
            }

            _rockSmashDirty = false;
            SavedTab();
            SaveNotice.Saved(UnsavedChangesDescription);
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        // Re-reads each edited table. The flags clear even if a read fails, so closing never gets stuck.
        public void DiscardChanges()
        {
            try
            {
                if (_pickupDirty && ShowPickupTab) { LoadPickupTable(); OnPropertyChanged(nameof(ActivationDivisorEdit)); }
                if (_hiddenDirty && ShowHiddenItemsTab) { SelectedHiddenItem = null; LoadHiddenItems(); }
                if (_rockSmashDirty && ShowRockSmashTab) LoadRockSmash();
            }
            finally
            {
                _pickupDirty = false;
                _hiddenDirty = false;
                _rockSmashDirty = false;
                ResetUndo();
                OnPropertyChanged(nameof(HasUnsavedChanges));
            }
        }
    }
}
