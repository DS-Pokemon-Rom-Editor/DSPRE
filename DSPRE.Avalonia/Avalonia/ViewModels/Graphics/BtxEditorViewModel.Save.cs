using Avalonia.Controls;
using Avalonia.Media.Imaging;
using DSPRE.Editors;
using DSPRE.LibNDSFormats;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using static DSPRE.RomInfo;
using Avalonia.Threading;
using DSPRE.Avalonia.Data;
using DSPRE.ROMFiles;
using DSPRE.HgEngine;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    public partial class BtxEditorViewModel
    {
        private readonly List<string> _saveErrors = new List<string>();

        private bool Fail(string error)
        {
            _saveErrors.Add(error);
            return false;
        }

        // One dialog per Save, however many overworlds failed.
        private void ReportSaveErrors()
        {
            if (_saveErrors.Count == 0) return;
            string message = string.Join("\n\n", _saveErrors.Distinct());
            _saveErrors.Clear();
            _ = DialogHelper.ShowErrorAsync(message, "Overworld editor");
        }

        public int SaveSelected()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _owKeys.Count) return 0;
            _saveErrors.Clear();
            uint key = _owKeys[_selectedIndex];
            bool hadRender = _pendingRender.ContainsKey(key);
            bool hadFile = _modifiedFiles.TryGetValue(key, out byte[] data);
            bool ok = SaveRenderState(key);
            if (ok && hadFile) ok = SaveEntry(key, data);
            else if (ok && hadRender) StatusText = "Saved.";
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(ModifiedCount));
            ReportSaveErrors();
            return ok && (hadRender || hadFile) ? 1 : 0;
        }

        public int SaveAll()
        {
            int saved = 0;
            _saveErrors.Clear();
            foreach (uint key in _pendingRender.Keys.ToList())
                if (SaveRenderState(key) && !_modifiedFiles.ContainsKey(key)) saved++;
            foreach (KeyValuePair<uint, byte[]> kvp in _modifiedFiles.ToList())
            {
                if (SaveEntry(kvp.Key, kvp.Value)) saved++;
            }
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(ModifiedCount));
            if (saved > 0) SaveNotice.Show($"Saved {saved} overworld{(saved == 1 ? "" : "s")}.");
            ReportSaveErrors();
            return saved;
        }

        private bool SaveEntry(uint key, byte[] data)
        {
            uint sprite = RomInfo.OverworldTable[key].spriteID;
            byte[] original = ArchiveMembers.ReadOrNull(DirNames.OWSprites, (int)sprite);
            bool metadataApplied = false;

            if (_metadataPatches.TryGetValue(key, out OverworldSpriteProfileMetadataPatch patch))
            {
                if (!patch.TryApply(out string metadataError))
                {
                    return Fail("The overworld wasn't saved:\n" + metadataError);
                }
                metadataApplied = true;
            }

            try
            {
                // hg-engine rebuilds every texture from the checkout, so the sources are what has to change.
                if (OverworldSourceFiles.Write((int)sprite, data) is string sourceError)
                {
                    if (metadataApplied) patch.TryRollback(out _);
                    return Fail("The overworld wasn't saved:\n" + sourceError);
                }
                ArchiveMembers.Put(DirNames.OWSprites, (int)sprite, data);
            }
            catch (Exception ex)
            {
                if (original != null)
                {
                    try { ArchiveMembers.Put(DirNames.OWSprites, (int)sprite, original); } catch (Exception restoreError) { AppLogger.Warn("BTX editor: restoring the original sprite: " + restoreError.Message); }
                }
                if (metadataApplied && !patch.TryRollback(out string rollbackError))
                    return Fail("The overworld wasn't saved:\n" + ex.Message + "\n\nIts table entry couldn't be put back either:\n" + rollbackError);
                return Fail("The overworld wasn't saved:\n" + ex.Message);
            }

            _modifiedFiles.Remove(key);
            _metadataPatches.Remove(key);
            StatusText = "Saved.";
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(ModifiedCount));
            return true;
        }
    }
}
