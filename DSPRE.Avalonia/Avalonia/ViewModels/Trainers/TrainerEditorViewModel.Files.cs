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
        public async Task ExportTrainerAsync()
        {
            if (_trainer == null) return;
            SyncToTrainer();
            FilePickerFileType filter = new FilePickerFileType("Trainer file") { Patterns = new[] { "*.trf" } };
            string path = await DialogHelper.SaveFileAsync(_owner, "Export trainer", new[] { filter }, $"trainer_{_selectedTrainerIndex:D4}.trf");
            if (path == null) return;
            try
            {
                PlainFiles.WriteBytes(path, _trainer.ToByteArray());
                SaveNotice.Exported(path);
            }
            catch (Exception ex) when (AppLogger.Noted("TrainerEditorViewModel.ExportTrainerAsync", ex)) { await DialogHelper.ShowErrorAsync($"Couldn't export:\n{ex.Message}", "Trainer editor"); }
        }

        /// <summary>Loads a .trf (name, properties, party) into the trainer. It waits for Save.</summary>
        public async Task ImportTrainerAsync()
        {
            if (_trainer == null || _selectedTrainerIndex < 0) return;
            FilePickerFileType filter = new FilePickerFileType("Trainer file") { Patterns = new[] { "*.trf" } };
            string path = await DialogHelper.OpenFileAsync(_owner, "Import trainer", new[] { filter });
            if (path == null) return;
            try
            {
                _trainer = TrainerFile.FromExport(PlainFiles.ReadBytes(path), (ushort)_selectedTrainerIndex);

                _suppress = true;
                try { PopulateFromTrainer(); } finally { _suppress = false; }
                SetDirty();
                SaveNotice.Imported($"trainer {_selectedTrainerIndex}", path);
            }
            catch (Exception ex) when (AppLogger.Noted("TrainerEditorViewModel.ImportTrainerAsync", ex)) { await DialogHelper.ShowErrorAsync($"Couldn't import:\n{ex.Message}", "Trainer editor"); }
        }

        public async Task ExportPropertiesAsync()
        {
            if (_trainer == null) return;
            SyncToTrainer();
            FilePickerFileType filter = new FilePickerFileType("Trainer properties") { Patterns = new[] { "*.trp" } };
            string path = await DialogHelper.SaveFileAsync(_owner, "Export trainer properties", new[] { filter }, $"trainer_{_selectedTrainerIndex:D4}.trp");
            if (path == null) return;
            try
            {
                PlainFiles.WriteBytes(path, _trainer.Trp.ToByteArray());
                SaveNotice.Exported(path);
            }
            catch (Exception ex) when (AppLogger.Noted("TrainerEditorViewModel.ExportPropertiesAsync", ex)) { await DialogHelper.ShowErrorAsync($"Couldn't export:\n{ex.Message}", "Trainer editor"); }
        }

        public async Task ReplacePropertiesAsync()
        {
            if (_trainer == null || _selectedTrainerIndex < 0) return;
            FilePickerFileType filter = new FilePickerFileType("Trainer properties") { Patterns = new[] { "*.trp" } };
            string path = await DialogHelper.OpenFileAsync(_owner, "Import trainer properties", new[] { filter });
            if (path == null) return;
            try
            {
                using FileStream fs = PlainFiles.OpenRead(path);
                _trainer.Trp = new TrainerProperties((ushort)_selectedTrainerIndex, fs);
                _suppress = true;
                try { PopulateFromTrainer(); } finally { _suppress = false; }
                SetDirty();
                SaveNotice.Imported($"the properties of trainer {_selectedTrainerIndex}", path);
            }
            catch (Exception ex) when (AppLogger.Noted("TrainerEditorViewModel.ReplacePropertiesAsync", ex)) { await DialogHelper.ShowErrorAsync($"Couldn't import:\n{ex.Message}", "Trainer editor"); }
        }

        public async Task ExportPartyAsync()
        {
            if (_trainer == null) return;
            SyncToTrainer();
            FilePickerFileType filter = new FilePickerFileType("Party data") { Patterns = new[] { "*.pdat" } };
            string path = await DialogHelper.SaveFileAsync(_owner, "Export trainer party", new[] { filter }, $"party_{_selectedTrainerIndex:D4}.pdat");
            if (path == null) return;
            try
            {
                byte[] condensed;
                _trainer.Party.ExportCondensedData = true;
                try { condensed = _trainer.Party.ToByteArray(); }
                finally { _trainer.Party.ExportCondensedData = false; }   // Save must write the ROM layout
                PlainFiles.WriteBytes(path, condensed);
                SaveNotice.Exported(path);
            }
            catch (Exception ex) when (AppLogger.Noted("TrainerEditorViewModel.ExportPartyAsync", ex)) { await DialogHelper.ShowErrorAsync($"Couldn't export:\n{ex.Message}", "Trainer editor"); }
        }

        public async Task ImportPartyAsync()
        {
            if (_trainer == null) return;
            FilePickerFileType filter = new FilePickerFileType("Party data") { Patterns = new[] { "*.pdat" } };
            string path = await DialogHelper.OpenFileAsync(_owner, "Import trainer party", new[] { filter });
            if (path == null) return;
            try
            {
                using FileStream fs = PlainFiles.OpenRead(path);
                _trainer.Party = new Party(readFirstByte: true, TrainerFile.PokeInParty, fs, _trainer.Trp);
                _suppress = true;
                try { PopulateFromTrainer(); } finally { _suppress = false; }
                SetDirty();
                SaveNotice.Imported($"the party of trainer {_selectedTrainerIndex}", path);
            }
            catch (Exception ex) when (AppLogger.Noted("TrainerEditorViewModel.ImportPartyAsync", ex)) { await DialogHelper.ShowErrorAsync($"Couldn't import:\n{ex.Message}", "Trainer editor"); }
        }
    }
}
