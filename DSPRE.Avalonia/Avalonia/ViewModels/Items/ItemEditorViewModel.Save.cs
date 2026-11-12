using Avalonia.Controls;
using Avalonia.Platform.Storage;
using DSPRE;
using DSPRE.Avalonia;
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
using System.Text;
using System.Threading.Tasks;
using static DSPRE.ROMFiles.ItemData;
using static DSPRE.RomInfo;
using AvaBitmap = Avalonia.Media.Imaging.Bitmap;

namespace DSPRE.Avalonia.ViewModels.Items
{
    public partial class ItemEditorViewModel
    {
        private bool _dataDirty;
        private bool _entryDirty;
        public bool HasUnsavedChanges       => _dataDirty || _entryDirty || _pendingItem != null;
        public string UnsavedChangesDescription =>
            _pendingItem != null ? $"New item {_pendingItem.DisplayName}"
            : _selectedItemIndex >= 0 && _selectedItemIndex < ItemNames.Count ? ItemNames[_selectedItemIndex] : $"Item {_selectedItemIndex}";

        // A new item exists only here until Save; Discard drops it.
        private HgEngineItemExpansion.PendingItem _pendingItem;
        private int _returnIndex;
        // Set while the list is rebuilt, so index changes aren't treated as picks.
        private bool _syncingList;

        public void SaveChanges() => DialogHelper.Watch(SaveAsync());

        async System.Threading.Tasks.Task<bool> IEditorWithUnsavedChanges.SaveChangesAsync()
        {
            await SaveAsync();
            return !HasUnsavedChanges;
        }

        public async System.Threading.Tasks.Task SaveAsync()
        {
            if (_pendingItem != null)
            {
                await SavePendingItemAsync();
                RaiseUndoState();
                return;
            }
            if (_entryDirty) SaveTableEntry();
            if (_dataDirty && !await SaveItemDataAsync()) { RaiseUndoState(); return; }
            _history.MarkSaved();
            RaiseUndoState();
        }

        public void DiscardChanges()
        {
            _dataDirty = _entryDirty = false;
            if (_pendingItem != null)
            {
                int back = _returnIndex;
                DropPendingItem();
                _selectedItemIndex = back;
                OnPropertyChanged(nameof(SelectedItemIndex));
                LoadFile(back);
            }
            else if (_selectedItemIndex >= 0 && _selectedItemIndex < ItemNames.Count)
                LoadFile(_selectedItemIndex);
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private void SyncNames()
        {
            List<string> names = RomInfo.GetItemNames().ToList();
            if (_pendingItem != null)
            {
                while (names.Count <= _pendingItem.Id) names.Add("");
                names[_pendingItem.Id] = _pendingItem.DisplayName + " (not saved)";
            }
            // Update in place. Clear would reset the bound SelectedIndex.
            _syncingList = true;
            try { ListSync.Apply(ItemNames, names); }
            finally { _syncingList = false; }
            MaxItemIndex = ItemNames.Count - 1;
            OnPropertyChanged(nameof(MaxItemIndex));
            OnPropertyChanged(nameof(SelectedItemIndex));
        }

        private void DropPendingItem()
        {
            if (_pendingItem == null) return;
            _pendingItem = null;
            SyncNames();
        }

        private async Task ConfirmDropPendingAsync(int newIndex)
        {
            if (!await DialogHelper.AskYesNoAsync("The new item isn't saved. Discard it?", "Unsaved changes"))
            {
                RecordSwitchGuard.SnapBack(() => _selectedItemIndex, v => _selectedItemIndex = v, () => OnPropertyChanged(nameof(SelectedItemIndex)));
                return;
            }
            _dataDirty = _entryDirty = false;
            DropPendingItem();
            newIndex = Math.Min(newIndex, ItemNames.Count - 1);
            _selectedItemIndex = newIndex;
            OnPropertyChanged(nameof(SelectedItemIndex));
            LoadFile(newIndex);
        }

        private async Task<bool> SavePendingItemAsync()
        {
            HgEngineItemExpansion.PendingItem pending = _pendingItem;
            ItemData data = _currentData;
            (bool saved, string error) = await HgEngineSave.RunAsync(() => HgEngineItemExpansion.TryCommitItem(pending, data, out string commitError) ? null : commitError);
            if (!saved)
            {
                if (error != null) await DialogHelper.ShowErrorAsync($"{pending.DisplayName} wasn't saved.\n{error}", "Item editor");
                return false;
            }

            HgEngineItemExpansion.CompleteAdd(pending);
            _pendingItem = null;
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.ItemData });
            data.SaveToFileDefaultDir(pending.Id, false);
            MaxItemDataId = GetItemDataFileCount() - 1;
            OnPropertyChanged(nameof(MaxItemDataId));
            SyncNames();
            AppEvents.RaiseNamesChanged();
            _selectedItemIndex = pending.Id;
            OnPropertyChanged(nameof(SelectedItemIndex));
            LoadFile(pending.Id);
            SaveNotice.Saved(UnsavedChangesDescription);
            return true;
        }

        public async Task ExportToFileAsync(Window owner)
        {
            if (_currentData == null) return;
            string path = await DialogHelper.SaveFileAsync(owner, "Export item data",
                new[] { new FilePickerFileType("Item data") { Patterns = new[] { "*.bin" } } }, $"itemdata_{_itemDataId:D4}.bin");
            if (path == null) return;
            try { PlainFiles.WriteBytes(path, _currentData.ToByteArray()); SaveNotice.Exported(path); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                await DialogHelper.ShowErrorAsync($"Couldn't export:\n{e.Message}", "Item editor", owner);
            }
        }

        public async Task ImportFromFileAsync(Window owner)
        {
            if (_currentData == null) return;
            string path = await DialogHelper.OpenFileAsync(owner, "Import item data",
                new[] { new FilePickerFileType("Item data") { Patterns = new[] { "*.bin" } } });
            if (path == null) return;

            byte[] bytes;
            try { bytes = PlainFiles.ReadBytes(path); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                await DialogHelper.ShowErrorAsync($"The file couldn't be read.\n{e.Message}", "Import item data", owner);
                return;
            }
            // Retail is 34 bytes, hg-engine 36.
            if (bytes.Length != ItemDataSize && bytes.Length != ItemData.RetailSize)
            {
                await DialogHelper.ShowErrorAsync($"Item data files are {ItemData.RetailSize} or {ItemDataSize} bytes, not {bytes.Length}.",
                                             "Import item data", owner);
                return;
            }
            Array.Resize(ref bytes, _currentData.ToByteArray().Length);

            _isLoading = true;
            try
            {
                _currentData = new ItemData(new MemoryStream(bytes), _itemDataId);
                PopulateFromCurrentData();
            }
            finally { _isLoading = false; }
            _lastCaptureUtc = DateTime.MinValue;   // separate undo step
            SetDataDirty();
            SaveNotice.Imported($"item data {_itemDataId}", path);
        }

        public async Task AddNewItemAsync(Window owner)
        {
            if (!HgEngineProject.IsActive) return;
            if (_pendingItem != null)
            {
                await DialogHelper.ShowErrorAsync("Save or discard the new item first.", "Add new item", owner);
                return;
            }
            if (!await RecordSwitchGuard.ConfirmLeaveAsync(this, owner, "item", "Save them before adding a new item?")) return;

            string name = await DialogHelper.PromptTextAsync("New item's display name:", "Add new item", owner: owner);
            if (name == null) return;

            ItemData data = new ItemData(new MemoryStream(new byte[ItemDataSize]), 0);
            if (!HgEngineItemExpansion.TryPrepareItem(name, out HgEngineItemExpansion.PendingItem pending, out string error)
                || !HgEngineItemExpansion.TryReadTemplate(pending, data, out error))
            {
                await DialogHelper.ShowErrorAsync($"Couldn't add the item:\n{error}", "Add new item", owner);
                return;
            }
            data.ID = pending.Id;

            _returnIndex = _selectedItemIndex;
            _pendingItem = pending;
            SyncNames();

            _isLoading = true;
            try
            {
                _selectedItemIndex = pending.Id;
                OnPropertyChanged(nameof(SelectedItemIndex));
                // hg-engine's table entry follows from the id, so the add sets none of its fields.
                _currentEntry = ReadTableEntry(pending.Id);
                RefreshEntryBoundProps();
                _currentData = data;
                SourceLoadError = null;
                PopulateFromCurrentData();
                UpdateIcon();
                _dataDirty = _entryDirty = false;
                _history.Reset(Snapshot());
                _lastCaptureUtc = DateTime.MinValue;
            }
            finally { _isLoading = false; }
            OnPropertyChanged(nameof(HasUnsavedChanges));
            RaiseUndoState();
        }

        // Snapshot before the dirty check, so every edit is undoable, not just the first.
        private void SetDataDirty()  { RecordUndoSnapshot(); if (_dataDirty)  return; _dataDirty  = true; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        private void SetEntryDirty() { RecordUndoSnapshot(); if (_entryDirty) return; _entryDirty = true; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        private static ItemNarcTableEntry ReadTableEntry(int index) => DSPRE.ROMFiles.ItemTable.Read(index);

        private void SaveTableEntry()
        {
            if (RomInfo.IsHGE)
            {
                // No item table on hg-engine.
                _entryDirty = false;
                OnPropertyChanged(nameof(HasUnsavedChanges));
                return;
            }

            DSPRE.ROMFiles.ItemTable.Write(_selectedItemIndex, _currentEntry);
            _entryDirty = false;
            SaveNotice.Saved(UnsavedChangesDescription);
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        // False when hg-engine's source couldn't take the edit.
        private async System.Threading.Tasks.Task<bool> SaveItemDataAsync()
        {
            if (_currentData == null) return true;
            int item = (int)_currentEntry.ItemData;
            ItemData data = _currentData;
            if (HgEngineProject.IsActive)
            {
                if (SourceLoadError != null)
                {
                    await DialogHelper.ShowErrorAsync($"Item {item} wasn't saved.\n{SourceLoadError}", "Item editor");
                    return false;
                }
                // The next sync rebuilds from the source, so a failed write fails the save.
                (bool saved, string error) = await HgEngineSave.RunAsync(() => HgEngineItemSource.TryWrite(item, data, out string writeError) ? null : writeError);
                if (!saved)
                {
                    if (error != null) await DialogHelper.ShowErrorAsync($"Item {item} wasn't saved.\n{error}", "Item editor");
                    return false;
                }
            }
            data.SaveToFileDefaultDir(item, false);
            _dataDirty = false;
            SaveNotice.Saved(UnsavedChangesDescription);
            OnPropertyChanged(nameof(HasUnsavedChanges));
            return true;
        }
    }
}
