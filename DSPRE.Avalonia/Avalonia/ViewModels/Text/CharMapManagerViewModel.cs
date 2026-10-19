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
        public bool HasMap { get => _hasMap; private set => Set(ref _hasMap, value); }

        private bool _dirty;

        // ── Private data ─────────────────────────────────────────────────────
        private CharMap _currentMap;

        // ── Constructor ───────────────────────────────────────────────────────
        public CharMapManagerViewModel()
        {
            if (Design.IsDesignMode)
            {
                HasMap = true;
                CharMapItems.Add("0x00 → \\x00 (NULL)");
                CharMapItems.Add("0x41 → A");
                CharMapItems.Add("0x42 → B");
                AliasItems.Add("[PK] → 0xE1");
                AliasItems.Add("[MN] → 0xE2");
                CodeItems.Add("0x41 → A");
                CodeItems.Add("0x42 → B");
                return;
            }
            LoadCharMap();
            PopulateListsFromMap();
        }

        // ── Commands ──────────────────────────────────────────────────────────

        public async Task AddAliasCommand()
        {
            if (_currentMap == null) return;

            string alias = NewAliasText.Trim();
            if (string.IsNullOrEmpty(alias))
            {
                await DialogHelper.ShowError("Alias name cannot be empty.", "Invalid alias");
                return;
            }

            // Warn about un-bracketed single-character aliases
            if (alias.Length == 1)
            {
                DialogHelper.MsgResult result = await DialogHelper.AskYesNoCancel(
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
                await DialogHelper.ShowError("This alias or character already exists in the charmap.", "Duplicate alias");
                return;
            }

            if (SelectedCodeIndex < 0)
            {
                await DialogHelper.ShowError("Please select a character code to alias.", "No code selected");
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

                // Select newly added alias
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

        public async Task RemoveAliasCommand()
        {
            if (_currentMap == null) return;
            if (SelectedAliasIndex < 0)
            {
                await DialogHelper.ShowError("Please select an alias to remove.", "No alias selected");
                return;
            }

            string selectedAliasStr = AliasItems[SelectedAliasIndex];
            string aliasName        = selectedAliasStr.Split(new[] { " -> " }, StringSplitOptions.None)[0];
            ushort? code            = _currentMap.FindCode(aliasName);

            if (code == null)
            {
                await DialogHelper.ShowError("Could not find the code for this alias.", "Alias not found");
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
                await DialogHelper.ShowError("Failed to remove alias.", "Char map manager error");
            }
        }

        public async Task SaveCommand()
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
                AppLogger.Error("Failed to save custom charmap: " + ex);
                await DialogHelper.ShowError("Failed to save custom charmap: " + ex.Message, "Char map manager error");
            }
        }

        public async Task CreateMapCommand()
        {
            if (File.Exists(CharMapManager.customCharmapFilePath))
            {
                bool overwrite = await DialogHelper.AskYesNo(
                    "A custom charmap already exists. Overwrite it?", "Custom charmap exists");
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
                await DialogHelper.ShowError("Failed to create custom charmap.", "Char map manager error");
            }
        }

        public async Task DeleteMapCommand()
        {
            bool confirm = await DialogHelper.AskYesNo(
                "Are you sure you want to delete the custom charmap? This cannot be undone.",
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
                await DialogHelper.ShowError("Failed to delete custom charmap.", "Char map manager error");
            }
        }

        public async Task ReloadCommand()
        {
            if (_dirty)
            {
                if (!await DialogHelper.AskTwoWay("Discard the unsaved changes and reload the character map?",
                                                  "Unsaved changes", "Discard", "Cancel"))
                    return;
            }
            LoadCharMap();
            PopulateListsFromMap();
        }

        public async Task OpenFileCommand()
        {
            if (!File.Exists(CharMapManager.customCharmapFilePath))
            {
                await DialogHelper.ShowInfo("No custom charmap file exists to open.", "File not found");
                return;
            }
            SystemShell.OpenWithDefaultApp(CharMapManager.customCharmapFilePath);
        }

        public async Task RebaseCommand()
        {
            if (!File.Exists(CharMapManager.customCharmapFilePath))
            {
                await DialogHelper.ShowInfo("No custom charmap exists to merge. Please create one first.",
                    "No custom charmap");
                return;
            }

            if (!CharMapManager.IsCustomMapOutdated())
            {
                bool proceed = await DialogHelper.AskYesNo(
                    "The custom charmap is already up to date. Rebase anyway?", "Rebase charmap");
                if (!proceed) return;
            }

            // Ask for merge strategy via a simple picker dialog
            MergeStrategy strategy = await AskMergeStrategyAsync();

            try
            {
                MergeResult result = CharMapManager.MergeCustomWithDefault(strategy);
                _currentMap = result.MergedMap;
                PopulateListsFromMap();
                SetDirty(true);

                string summary = result.GetSummary();
                if (result.Conflicts.Count > 0)
                {
                    bool viewConflicts = await DialogHelper.AskYesNo(
                        "Charmap Merge Summary:\n" + summary +
                        "\n\nThere were conflicts. View them?", "Merge result");
                    if (viewConflicts)
                        await DialogHelper.ShowInfo(result.GetConflictDetails(), "Merge conflicts");
                }
                else
                {
                    await DialogHelper.ShowInfo("Rebased the character map.\n\n" + summary, "Rebase");
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("Failed to rebase charmap: " + ex);
                await DialogHelper.ShowError("Failed to rebase charmap: " + ex.Message, "Char map manager error");
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
        public void SaveChanges() => _ = SaveChangesAsync();

        public async Task<bool> SaveChangesAsync()
        {
            await SaveCommand();
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
        }

        private void LoadCharMap()
        {
            if (!File.Exists(CharMapManager.customCharmapFilePath))
            {
                _currentMap = null;
                HasMap = false;
                return;
            }
            try
            {
                _currentMap = CharMapManager.DeserializeCharMap(CharMapManager.customCharmapFilePath);
                HasMap = true;
                SetDirty(false);
            }
            catch (Exception ex)
            {
                _currentMap = null;
                HasMap = false;
                AppLogger.Error("Failed to load custom charmap: " + ex);
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

        private async Task<MergeStrategy> AskMergeStrategyAsync()
        {
            // Simple info+choice dialog: three options presented as a message
            string message =
                "Select merge strategy for conflicting entries:\n\n" +
                "1 = Prefer Custom  (keep your custom chars, merge aliases)\n" +
                "2 = Prefer Base    (keep default chars, add your custom aliases)\n" +
                "3 = Replace Base   (use only your custom chars and aliases)\n\n" +
                "Reply Yes for option 1 (Prefer Custom), No for option 2 (Prefer Base).\n" +
                "(Cancel to use Replace Base)";

            DialogHelper.MsgResult r = await DialogHelper.AskYesNoCancel(message, "Merge strategy");
            return r == DialogHelper.MsgResult.Yes   ? MergeStrategy.PreferCustom
                 : r == DialogHelper.MsgResult.No    ? MergeStrategy.PreferBase
                 : MergeStrategy.ReplaceBase;
        }
    }
}
