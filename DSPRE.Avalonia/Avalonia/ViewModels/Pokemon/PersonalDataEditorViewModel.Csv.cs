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
        private PersonalDataCsv.Lists CsvLists() => new PersonalDataCsv.Lists
        {
            Pokemon = _allFileNames, Types = _typeNamesArr, Abilities = _abilityNamesArr, Items = _itemNamesArr,
            GrowthCurves = GrowthCurveNames.ToArray(), EggGroups = EggGroupNames.ToArray(), DexColors = DexColorNames.ToArray(),
        };

        /// <summary>Every Pokémon's personal data as held here, unsaved edits included.</summary>
        public async Task ExportCommandAsync(Window owner)
        {
            string path = await DialogHelper.SaveFileAsync(owner, "Export personal data to CSV",
                new[] { DialogHelper.CsvFilter, DialogHelper.AllFilter }, "PersonalData.csv");
            if (path == null) return;
            try
            {
                List<int> unread = new List<int>();
                List<(int, PokemonPersonalData)> rows = new List<(int, PokemonPersonalData)>();
                for (int i = 0; i < GetPersonalFilesCount(); i++)
                {
                    (PokemonPersonalData d, string error) = HeldData(i);
                    if (error != null) unread.Add(i); else rows.Add((i, d));
                }
                using (StreamWriter writer = new StreamWriter(PlainFiles.Create(path)))
                    PersonalDataCsv.Write(writer, CsvLists(), rows);
                SaveNotice.Exported(path);
                if (unread.Count > 0)
                    await DialogHelper.ShowInfoAsync($"{unread.Count} Pokémon couldn't be read and were left out: {string.Join(", ", unread.Take(10))}{(unread.Count > 10 ? ", ..." : "")}",
                        "Pokémon left out");
            }
            catch (Exception ex) when (AppLogger.Noted("PersonalDataEditorViewModel.ExportCommandAsync", ex)) { await DialogHelper.ShowErrorAsync($"Couldn't export:\n{ex.Message}", "Pokémon editor"); }
        }

        /// <summary>Imports the Pokémon in the file. Changes stay unsaved until Save.</summary>
        public async Task ImportCommandAsync(Window owner)
        {
            if (_current == null) return;
            PersonalDataCsv importer = new PersonalDataCsv(CsvLists(), GetPersonalFilesCount(), HeldData);
            CsvImportSession session = await CsvImportReviewView.ReviewAsync(owner, importer);
            if (session == null) return;

            List<CsvRecord> accepted = session.Accepted;
            foreach (CsvRecord r in accepted)
            {
                PersonalDataCsv.Entry e = (PersonalDataCsv.Entry)r.Value;
                if (e.Id != _currentId) { _pending[e.Id] = e.Data.ToByteArray(); continue; }
                TakeImported(e.Data.ToByteArray());
                _heldRecord = true;
            }
            OnPropertyChanged(nameof(HasUnsavedChanges));
            SaveNotice.Imported($"{accepted.Count} Pokémon", session.FileName);
        }
    }
}
