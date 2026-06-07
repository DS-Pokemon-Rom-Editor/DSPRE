using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using DSPRE.ROMFiles;
using IEditorWithUnsavedChanges = global::DSPRE.Editors.IEditorWithUnsavedChanges;
using static DSPRE.DSUtils;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels
{
    /// <summary>
    /// ViewModel for the TM/HM Editor Avalonia window.
    /// Implements IEditorWithUnsavedChanges so it participates in ROM-switch prompts.
    /// </summary>
    public class TMEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        // ----------------------------------------------------------------
        // INotifyPropertyChanged
        // ----------------------------------------------------------------

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private bool Set<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }

        // ----------------------------------------------------------------
        // Private state
        // ----------------------------------------------------------------

        private int[] _curMachineMoves;
        private int[] _curMachinePalettes;
        private bool _loading;
        private bool _dirty;

        // ----------------------------------------------------------------
        // Observable collections (bound to ListBox / ComboBoxes)
        // ----------------------------------------------------------------

        public ObservableCollection<string> MachineItems { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> MoveNames { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> TypeNames { get; } = new ObservableCollection<string>();

        // ----------------------------------------------------------------
        // Selected indices
        // ----------------------------------------------------------------

        private int _selectedMachineIndex = -1;
        public int SelectedMachineIndex
        {
            get => _selectedMachineIndex;
            set
            {
                if (!Set(ref _selectedMachineIndex, value)) return;
                if (_loading || value < 0) return;
                OnMachineSelected(value);
            }
        }

        private int _selectedMoveIndex = -1;
        public int SelectedMoveIndex
        {
            get => _selectedMoveIndex;
            set
            {
                if (!Set(ref _selectedMoveIndex, value)) return;
                if (_loading || _selectedMachineIndex < 0 || _selectedMachineIndex >= _curMachineMoves.Length) return;

                _curMachineMoves[_selectedMachineIndex] = value;
                string label = TMEditor.MachineLabelFromIndex(_selectedMachineIndex);
                MachineItems[_selectedMachineIndex] = $"{label} - {GetMoveNameFromID(value)}";
                SetDirty(true);
            }
        }

        private int _selectedTypeIndex = -1;
        public int SelectedTypeIndex
        {
            get => _selectedTypeIndex;
            set
            {
                if (!Set(ref _selectedTypeIndex, value)) return;
                if (_loading || _selectedMachineIndex < 0 || _selectedMachineIndex >= _curMachineMoves.Length) return;

                _curMachinePalettes[_selectedMachineIndex] = TypeIndexToPalette(value);
                SetDirty(true);
            }
        }

        // ----------------------------------------------------------------
        // Window title (reflects dirty state)
        // ----------------------------------------------------------------

        private string _title = "TM/HM Editor";
        public string Title
        {
            get => _title;
            private set => Set(ref _title, value);
        }

        // ----------------------------------------------------------------
        // IEditorWithUnsavedChanges
        // ----------------------------------------------------------------

        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => "TM/HM Editor";

        void IEditorWithUnsavedChanges.SaveChanges() => SaveChangesCore();
        public void DiscardChanges() => SetDirty(false);

        // ----------------------------------------------------------------
        // Constructor
        // ----------------------------------------------------------------

        public TMEditorViewModel()
        {
            TryUnpackNarcs(new List<DirNames> { DirNames.moveData });

            PopulateMoveNames();
            PopulateTypeNames();

            _curMachineMoves = TMEditor.ReadMachineMoves();
            _curMachinePalettes = TMEditor.ReadMachinePalettes();
            RefreshMachineMoveList();
        }

        // ----------------------------------------------------------------
        // Commands (async, called from View code-behind)
        // ----------------------------------------------------------------

        public void SaveCommand() => SaveChangesCore();

        public void AutoPaletteCommand()
        {
            if (_selectedMachineIndex < 0 || _selectedMachineIndex >= _curMachineMoves.Length)
                return;

            int moveId = _curMachineMoves[_selectedMachineIndex];
            int typeIndex = GetMoveType(moveId);

            _loading = true;
            SelectedTypeIndex = typeIndex;
            _loading = false;

            _curMachinePalettes[_selectedMachineIndex] = TypeIndexToPalette(typeIndex);
            SetDirty(true);
        }

        public async Task AutoPaletteAllCommand(Window owner)
        {
            bool confirmed = await DialogHelper.AskYesNo(
                "This will set the palette of all TMs and HMs based on their move types.\n" +
                "If any of the moves have custom types (e.g. Fairy) they will receive the Normal type palette instead and " +
                "may need to be manually corrected.\nDo you want to continue?",
                "Auto-Set All Palettes");

            if (!confirmed) return;

            for (int i = 0; i < _curMachineMoves.Length; i++)
            {
                int typeIndex = GetMoveType(_curMachineMoves[i]);
                _curMachinePalettes[i] = TypeIndexToPalette(typeIndex);
            }

            if (_selectedMachineIndex >= 0 && _selectedMachineIndex < _curMachinePalettes.Length)
            {
                _loading = true;
                SelectedTypeIndex = PaletteToTypeIndex(_curMachinePalettes[_selectedMachineIndex]);
                _loading = false;
            }

            SetDirty(true);
        }

        public async Task ExportCommand(Window owner)
        {
            string path = await DialogHelper.SaveFile(
                owner,
                "Export Machine Data",
                new[] { DialogHelper.CsvFilter, DialogHelper.AllFilter },
                "machine_data.csv");

            if (path == null) return;

            try
            {
                using var writer = new StreamWriter(path);
                writer.WriteLine("Machine,Move ID,Move Name,Palette ID");
                for (int i = 0; i < _curMachineMoves.Length; i++)
                {
                    string label = TMEditor.MachineLabelFromIndex(i);
                    int moveId = _curMachineMoves[i];
                    string moveName = GetMoveNameFromID(moveId);
                    int paletteId = _curMachinePalettes[i];
                    writer.WriteLine($"{label},{moveId},{moveName},{paletteId}");
                }

                await DialogHelper.ShowInfo("Machine data exported successfully.", "Export Complete");
            }
            catch (Exception ex)
            {
                AppLogger.Error($"TM Editor: Failed to export machine data. Exception: {ex.Message}");
                await DialogHelper.ShowError("An error occurred while exporting the machine data. Please try again.", "Export Error");
            }
        }

        public async Task ImportCommand(Window owner)
        {
            string path = await DialogHelper.OpenFile(
                owner,
                "Import Machine Data",
                new[] { DialogHelper.CsvFilter, DialogHelper.AllFilter });

            if (path == null) return;

            try
            {
                var lines = File.ReadAllLines(path);
                for (int i = 1; i < lines.Length; i++) // skip header
                {
                    var parts = lines[i].Split(',');
                    if (parts.Length < 4) continue;

                    string machineLabel = parts[0].Trim();
                    int moveId = int.Parse(parts[1].Trim());
                    int paletteId = int.Parse(parts[3].Trim());
                    int machineIndex;

                    if (machineLabel.StartsWith("TM"))
                        machineIndex = int.Parse(machineLabel.Substring(2)) - 1;
                    else if (machineLabel.StartsWith("HM"))
                        machineIndex = int.Parse(machineLabel.Substring(2)) + PokemonPersonalData.tmsCount - 1;
                    else
                        continue;

                    if (machineIndex >= 0 && machineIndex < _curMachineMoves.Length)
                    {
                        _curMachineMoves[machineIndex] = moveId;
                        _curMachinePalettes[machineIndex] = paletteId;
                    }
                }

                RefreshMachineMoveList();
                SetDirty(true);
                await DialogHelper.ShowInfo("Machine data imported successfully.", "Import Complete");
            }
            catch (Exception ex)
            {
                AppLogger.Error($"TM Editor: Failed to import machine data. Exception: {ex.Message}");
                await DialogHelper.ShowError(
                    "An error occurred while importing the machine data. Please ensure the file format is correct.",
                    "Import Error");
            }
        }

        /// <summary>
        /// Called when the window is closing. Returns false if close should be cancelled.
        /// </summary>
        public async Task<bool> ConfirmCloseAsync()
        {
            if (!_dirty) return true;

            var result = await DialogHelper.AskYesNoCancel(
                "You have unsaved changes. Do you want to save them?",
                "Unsaved Changes");

            if (result == DialogHelper.MsgResult.Yes)
            {
                SaveChangesCore();
                return true;
            }

            return result == DialogHelper.MsgResult.No;
        }

        // ----------------------------------------------------------------
        // Private helpers
        // ----------------------------------------------------------------

        private void OnMachineSelected(int index)
        {
            if (index < 0 || index >= _curMachineMoves.Length || index >= _curMachinePalettes.Length)
                return;

            _loading = true;
            SelectedMoveIndex = _curMachineMoves[index];
            SelectedTypeIndex = PaletteToTypeIndex(_curMachinePalettes[index]);
            _loading = false;
        }

        private void RefreshMachineMoveList()
        {
            MachineItems.Clear();
            string[] names = TMEditor.GetMachineMoveNames(_curMachineMoves);
            for (int i = 0; i < names.Length; i++)
                MachineItems.Add($"{TMEditor.MachineLabelFromIndex(i)} - {names[i]}");
        }

        private void PopulateMoveNames()
        {
            MoveNames.Clear();
            foreach (var name in RomInfo.GetAttackNames())
                MoveNames.Add(name);
        }

        private void PopulateTypeNames()
        {
            TypeNames.Clear();
            foreach (var name in RomInfo.GetTypeNames())
                TypeNames.Add(name);
        }

        private string GetMoveNameFromID(int moveId)
        {
            var names = RomInfo.GetAttackNames();
            return (moveId >= 0 && moveId < names.Length) ? names[moveId] : $"UNK_{moveId}";
        }

        private void SaveChangesCore()
        {
            try
            {
                var writer = new ARM9.Writer(RomInfo.GetMachineMoveOffset());
                foreach (int move in _curMachineMoves)
                    writer.Write((ushort)move);
                writer.Close();

                for (int i = 0; i < _curMachinePalettes.Length; i++)
                    WritePaletteID(i, _curMachinePalettes[i]);

                SetDirty(false);
            }
            catch (Exception ex)
            {
                AppLogger.Error($"TM Editor: Failed to save machine moves or palettes. Exception: {ex.Message}");
                // Fire-and-forget – we're in a sync context here; errors are logged
            }
        }

        private void WritePaletteID(int machineIndex, int paletteID)
        {
            uint itemTableOffset = RomInfo.GetItemTableOffset();
            int adjustedIndex = machineIndex + 328;
            try
            {
                ARM9.WriteBytes(
                    System.BitConverter.GetBytes((ushort)paletteID),
                    (uint)(itemTableOffset + adjustedIndex * 8 + 4));
            }
            catch (Exception ex)
            {
                AppLogger.Error($"TM Editor: Failed to write palette ID for machine index {machineIndex}. Exception: {ex.Message}");
            }
        }

        private void SetDirty(bool isDirty)
        {
            _dirty = isDirty;
            Title = isDirty ? "TM/HM Editor*" : "TM/HM Editor";
            OnPropertyChanged(nameof(HasUnsavedChanges));

            // Avalonia ViewModels are tracked by AvaloniaEditorsRegistry, not the WinForms OpenEditorsRegistry
        }

        private int GetMoveType(int moveId)
        {
            var moveData = new MoveData(moveId);
            return (int)moveData.movetype;
        }

        private static int PaletteToTypeIndex(int paletteID) => paletteID switch
        {
            398 => 1,  // Fighting
            399 => 16, // Dragon
            400 => 11, // Water
            401 => 14, // Psychic
            402 => 0,  // Normal
            403 => 3,  // Poison
            404 => 15, // Ice
            405 => 12, // Grass
            406 => 10, // Fire
            407 => 17, // Dark
            408 => 8,  // Steel
            409 => 13, // Electric
            410 => 4,  // Ground
            411 => 7,  // Ghost
            412 => 5,  // Rock
            413 => 2,  // Flying
            610 => 6,  // Bug
            _   => 0,  // Fallback Normal
        };

        private static int TypeIndexToPalette(int typeIndex) => typeIndex switch
        {
            0  => 402, // Normal
            1  => 398, // Fighting
            2  => 413, // Flying
            3  => 403, // Poison
            4  => 410, // Ground
            5  => 412, // Rock
            6  => 610, // Bug
            7  => 411, // Ghost
            8  => 408, // Steel
            10 => 406, // Fire
            11 => 400, // Water
            12 => 405, // Grass
            13 => 409, // Electric
            14 => 401, // Psychic
            15 => 404, // Ice
            16 => 399, // Dragon
            17 => 407, // Dark
            _  => 402, // Fallback Normal
        };
    }
}
