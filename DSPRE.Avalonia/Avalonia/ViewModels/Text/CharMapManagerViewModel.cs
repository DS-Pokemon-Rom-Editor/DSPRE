using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using DSPRE.CharMaps;

namespace DSPRE.Avalonia.ViewModels.Text
{
    public class CharMapManagerViewModel : INotifyPropertyChanged, DSPRE.Editors.IEditorWithUnsavedChanges
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        // ── Observable state ─────────────────────────────────────────────────
        public ObservableCollection<string> CharMapItems { get; } = new();
        public ObservableCollection<string> AliasItems   { get; } = new();
        public ObservableCollection<string> CodeItems    { get; } = new();

        private int _selectedCodeIndex = -1;
        public int SelectedCodeIndex
        {
            get => _selectedCodeIndex;
            set => Set(ref _selectedCodeIndex, value);
        }

        private int _selectedCharMapIndex = -1;
        public int SelectedCharMapIndex
        {
            get => _selectedCharMapIndex;
            set => Set(ref _selectedCharMapIndex, value);
        }

        private int _selectedAliasIndex = -1;
        public int SelectedAliasIndex
        {
            get => _selectedAliasIndex;
            set => Set(ref _selectedAliasIndex, value);
        }

        private string _newAliasText = string.Empty;
        public string NewAliasText { get => _newAliasText; set => Set(ref _newAliasText, value); }

        private string _searchText = string.Empty;
        public string SearchText { get => _searchText; set => Set(ref _searchText, value); }

        private bool _hasMap;
        public bool HasMap { get => _hasMap; private set { if (Set(ref _hasMap, value)) OnPropertyChanged(nameof(CanSave)); } }
        public bool CanSave => _hasMap && _dirty;

        private bool _dirty;

        // ── Private data ─────────────────────────────────────────────────────
        private CharMap _currentMap;

        // ── Constructor ───────────────────────────────────────────────────────
        public CharMapManagerViewModel()
        {
            if (Design.IsDesignMode)
            {
                HasMap = true;
                CharMapItems.Add("0x00 = \\x00 (NULL)");
                CharMapItems.Add("0x41 = A");
                CharMapItems.Add("0x42 = B");
                AliasItems.Add("[PK] = 0xE1");
                AliasItems.Add("[MN] = 0xE2");
                CodeItems.Add("0x41 = A");
                CodeItems.Add("0x42 = B");
                return;
            }
            LoadCharMap();
            PopulateListsFromMap();
        }

        // ── Commands ──────────────────────────────────────────────────────────

        public async Task AddAliasCommandAsync()
        {
            if (_currentMap == null) return;

            string alias = NewAliasText.Trim();
            if (string.IsNullOrEmpty(alias))
            {
                await DialogHelper.ShowErrorAsync("Alias name can't be empty.", "Invalid alias");
                return;
            }

            // Warn about un-bracketed single-character aliases
            if (alias.Length == 1)
            {
                DialogHelper.MsgResult result = await DialogHelper.AskYesNoCancelAsync(
                    "Unbracketed single character aliases may cause encoding issues.\nEnclose in brackets?",
                    "Single character alias");
                if (result == DialogHelper.MsgResult.Yes)
                    alias = "[" + alias + "]";
                else if (result == DialogHelper.MsgResult.Cancel)
                    return;
            }
            else if (alias.Length > 1 && !(alias.StartsWith("[") && alias.EndsWith("]")))
                alias = "[" + alias + "]";

            if (_currentMap.FindCode(alias) != null)
            {
                await DialogHelper.ShowErrorAsync("This alias or character already exists in the character map.", "Duplicate alias");
                return;
            }

            if (SelectedCodeIndex < 0)
            {
                await DialogHelper.ShowErrorAsync("Please select a character code to alias.", "No code selected");
                return;
            }

            string codeItem = CodeItems[SelectedCodeIndex];
            string codeStr  = codeItem.Split(' ')[0];
            ushort code     = ushort.Parse(codeStr.Substring(2), System.Globalization.NumberStyles.HexNumber);

            CharMapEntry entry = _currentMap.GetEntry(code);
            if (entry != null)
            {
                entry.AddAlias(alias);
                SetDirty(true);
                PopulateListsFromMap();
                NewAliasText = string.Empty;

                for (int i = 0; i < AliasItems.Count; i++)
                {
                    if (AliasItems[i].StartsWith(alias + " "))
                    {
                        SelectedAliasIndex = i;
                        break;
                    }
                }
            }
        }

        public async Task RemoveAliasCommandAsync()
        {
            if (_currentMap == null) return;
            if (SelectedAliasIndex < 0)
            {
                await DialogHelper.ShowErrorAsync("Please select an alias to remove.", "No alias selected");
                return;
            }

            string selectedAliasStr = AliasItems[SelectedAliasIndex];
            string aliasName        = selectedAliasStr.Split(new[] { " -> " }, StringSplitOptions.None)[0];
            ushort? code            = _currentMap.FindCode(aliasName);

            if (code == null)
            {
                await DialogHelper.ShowErrorAsync("Couldn't find the code for this alias.", "Alias not found");
                return;
            }

            CharMapEntry entry = _currentMap.GetEntry(code.Value);
            if (entry != null && entry.RemoveAlias(aliasName))
            {
                SetDirty(true);
                PopulateListsFromMap();
            }
            else
            {
                await DialogHelper.ShowErrorAsync("Couldn't remove the alias.", "Character map manager");
            }
        }

        public async Task SaveCommandAsync()
        {
            if (_currentMap == null) return;
            try
            {
                CharMapManager.SaveCharMap(_currentMap, saveToCustomPath: true);
                SetDirty(false);
                SaveNotice.Show("Character map saved.");
            }
            catch (Exception ex)
            {
                AppLogger.Error("Couldn't save the custom character map: " + ex);
                await DialogHelper.ShowErrorAsync("Couldn't save the custom character map: " + ex.Message, "Character map manager");
            }
        }

        public async Task CreateMapCommandAsync()
        {
            if (File.Exists(CharMapManager.CustomCharmapFilePath))
            {
                bool overwrite = await DialogHelper.AskYesNoAsync(
                    "A custom character map already exists. Overwrite it?", "Custom character map exists");
                if (!overwrite) return;
            }

            if (CharMapManager.CreateCustomCharMapFile())
            {
                LoadCharMap();
                PopulateListsFromMap();
                SaveNotice.Show("Created the custom character map.");
            }
            else
            {
                await DialogHelper.ShowErrorAsync("Couldn't create the custom character map.", "Character map manager");
            }
        }

        public async Task DeleteMapCommandAsync()
        {
            bool confirm = await DialogHelper.AskYesNoAsync(
                "Delete the custom character map? This can't be undone.",
                "Delete custom character map?");
            if (!confirm) return;

            if (CharMapManager.DeleteCustomCharMapFile())
            {
                _currentMap = null;
                HasMap = false;
                SetDirty(false);
                PopulateListsFromMap();
                SaveNotice.Show("Deleted the custom character map.");
            }
            else
            {
                await DialogHelper.ShowErrorAsync("Couldn't delete the custom character map.", "Character map manager");
            }
        }

        public async Task ReloadCommandAsync()
        {
            if (_dirty)
            {
                if (!await DialogHelper.AskTwoWayAsync("Discard the unsaved changes and reload the character map?",
                                                  "Unsaved changes", "Discard", "Cancel"))
                    return;
            }
            LoadCharMap();
            PopulateListsFromMap();
        }

        public async Task OpenFileCommandAsync()
        {
            if (!File.Exists(CharMapManager.CustomCharmapFilePath))
            {
                await DialogHelper.ShowInfoAsync("No custom character map file exists to open.", "File not found");
                return;
            }
            SystemShell.OpenWithDefaultApp(CharMapManager.CustomCharmapFilePath);
        }

        public async Task RebaseCommandAsync()
        {
            if (!File.Exists(CharMapManager.CustomCharmapFilePath))
            {
                await DialogHelper.ShowInfoAsync("No custom character map exists to merge. Please create one first.",
                    "No custom character map");
                return;
            }

            if (!CharMapManager.IsCustomMapOutdated())
            {
                bool proceed = await DialogHelper.AskYesNoAsync(
                    "The custom character map is already up to date. Update it anyway?", "Update from the built-in map");
                if (!proceed) return;
            }

            MergeStrategy? strategy = await AskMergeStrategyAsync();
            if (strategy == null) return;

            try
            {
                MergeResult result = CharMapManager.MergeCustomWithDefault(strategy.Value);
                _currentMap = result.MergedMap;
                PopulateListsFromMap();
                SetDirty(true);

                string summary = result.GetSummary();
                if (result.Conflicts.Count > 0)
                {
                    bool viewConflicts = await DialogHelper.AskYesNoAsync(
                        "Merge summary:\n" + summary +
                        "\n\nThere were conflicts. View them?", "Merge result");
                    if (viewConflicts)
                        await DialogHelper.ShowInfoAsync(result.GetConflictDetails(), "Merge conflicts");
                }
                else
                {
                    await DialogHelper.ShowInfoAsync("Updated the character map from the built-in one.\n\n" + summary, "Update from the built-in map");
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("Character map update from the built-in map failed: " + ex);
                await DialogHelper.ShowErrorAsync("Couldn't update the character map: " + ex.Message, "Character map manager");
            }
        }

        public void SearchCommand()
        {
            string term = SearchText.Trim();
            if (string.IsNullOrEmpty(term)) { PopulateListsFromMap(); return; }
            if (_currentMap == null) return;

            CharMapItems.Clear();
            bool byCode = term.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
            string termLower = term.ToLower();

            foreach (ushort code in _currentMap.GetAllCodes().OrderBy(c => c))
            {
                CharMapEntry entry = _currentMap.GetEntry(code);
                if (entry == null) continue;
                string codeStr = $"0x{code:X4}";
                bool match = byCode ? codeStr.ToLower().Contains(termLower)
                                    : entry.Character.Contains(term);
                if (match)
                    CharMapItems.Add($"{codeStr} <-> {entry.Character}");

                if (entry.Aliases != null)
                    foreach (string alias in entry.Aliases)
                    {
                        bool aliasMatch = byCode ? codeStr.ToLower().Contains(termLower)
                                                 : alias.Contains(term) || entry.Character.Contains(term);
                        if (aliasMatch)
                            CharMapItems.Add($"{codeStr} <- {alias} (alias)");
                    }
            }
        }

        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => "Character map";
        public void SaveChanges() => DialogHelper.Watch(SaveChangesAsync());

        public async Task<bool> SaveChangesAsync()
        {
            await SaveCommandAsync();
            return !_dirty;
        }

        public void DiscardChanges()
        {
            LoadCharMap();
            PopulateListsFromMap();
        }

        // ── Private helpers ───────────────────────────────────────────────────
        private void SetDirty(bool d)
        {
            _dirty = d;
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(CanSave));
        }

        private void LoadCharMap()
        {
            if (!File.Exists(CharMapManager.CustomCharmapFilePath))
            {
                _currentMap = null;
                HasMap = false;
                return;
            }
            try
            {
                _currentMap = CharMapManager.DeserializeCharMap(CharMapManager.CustomCharmapFilePath);
                HasMap = true;
                SetDirty(false);
            }
            catch (Exception ex)
            {
                _currentMap = null;
                HasMap = false;
                AppLogger.Error("Couldn't load custom character map: " + ex);
            }
        }

        private void PopulateListsFromMap()
        {
            CharMapItems.Clear();
            AliasItems.Clear();
            CodeItems.Clear();
            SelectedCodeIndex = -1;

            if (_currentMap == null) return;

            foreach (ushort code in _currentMap.GetAllCodes().OrderBy(c => c))
            {
                CharMapEntry entry = _currentMap.GetEntry(code);
                if (entry == null) continue;

                string codeStr = $"0x{code:X4}";
                CharMapItems.Add($"{codeStr} <-> {entry.Character}");
                CodeItems.Add($"{codeStr} <-> {entry.Character}");

                if (entry.Aliases != null)
                    foreach (string alias in entry.Aliases)
                    {
                        CharMapItems.Add($"{codeStr} <- {alias} (alias)");
                        AliasItems.Add($"{alias} -> {entry.Character} <-> {codeStr}");
                    }
            }
        }

        // Null when the user cancels. Four answers do not fit one dialog, so replacing is asked first.
        private static async Task<MergeStrategy?> AskMergeStrategyAsync()
        {
            DialogHelper.MsgResult mode = await DialogHelper.AskThreeWayAsync(
                "Merge your custom map with the built-in one, or replace the built-in map with only your characters and aliases?",
                "Update from the built-in map", "Merge", "Replace");
            if (mode == DialogHelper.MsgResult.Cancel) return null;
            if (mode == DialogHelper.MsgResult.No) return MergeStrategy.ReplaceBase;

            DialogHelper.MsgResult keep = await DialogHelper.AskThreeWayAsync(
                "When both maps define a code, keep which character? Aliases from both are kept.",
                "Update from the built-in map", "Custom", "Built-in");
            return keep == DialogHelper.MsgResult.Yes ? MergeStrategy.PreferCustom
                 : keep == DialogHelper.MsgResult.No  ? MergeStrategy.PreferBase
                 : (MergeStrategy?)null;
        }
    }
}
