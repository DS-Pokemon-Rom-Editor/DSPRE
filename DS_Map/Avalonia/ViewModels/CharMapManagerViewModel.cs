using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using DSPRE.CharMaps;

namespace DSPRE.Avalonia.ViewModels
{
    public class CharMapManagerViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        // ── Observable state ─────────────────────────────────────────────────
        private string _title = "Character Map Manager";
        public string Title { get => _title; private set => Set(ref _title, value); }

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
                await DialogHelper.ShowError("Alias name cannot be empty.", "Invalid Alias");
                return;
            }

            // Warn about un-bracketed single-character aliases
            if (alias.Length == 1)
            {
                var result = await DialogHelper.AskYesNoCancel(
                    "Unbracketed single character aliases may cause encoding issues.\nEnclose in brackets?",
                    "Single Character Alias");
                if (result == DialogHelper.MsgResult.Yes)
                    alias = "[" + alias + "]";
                else if (result == DialogHelper.MsgResult.Cancel)
                    return;
            }
            else if (alias.Length > 1 && !(alias.StartsWith("[") && alias.EndsWith("]")))
                alias = "[" + alias + "]";

            if (_currentMap.FindCode(alias) != null)
            {
                await DialogHelper.ShowError("This alias or character already exists in the charmap.", "Duplicate Alias");
                return;
            }

            if (SelectedCodeIndex < 0)
            {
                await DialogHelper.ShowError("Please select a character code to alias.", "No Code Selected");
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
                await DialogHelper.ShowError("Please select an alias to remove.", "No Alias Selected");
                return;
            }

            string selectedAliasStr = AliasItems[SelectedAliasIndex];
            string aliasName        = selectedAliasStr.Split(new[] { " -> " }, StringSplitOptions.None)[0];
            ushort? code            = _currentMap.FindCode(aliasName);

            if (code == null)
            {
                await DialogHelper.ShowError("Could not find the code for this alias.", "Alias Not Found");
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
                await DialogHelper.ShowError("Failed to remove alias.", "Error");
            }
        }

        public async Task SaveCommand()
        {
            if (_currentMap == null) return;
            try
            {
                CharMapManager.SaveCharMap(_currentMap, saveToCustomPath: true);
                SetDirty(false);
                await DialogHelper.ShowInfo("Charmap saved successfully!", "Success");
            }
            catch (Exception ex)
            {
                AppLogger.Error("Failed to save custom charmap: " + ex);
                await DialogHelper.ShowError("Failed to save custom charmap: " + ex.Message, "Error");
            }
        }

        public async Task CreateMapCommand()
        {
            if (File.Exists(CharMapManager.customCharmapFilePath))
            {
                bool overwrite = await DialogHelper.AskYesNo(
                    "A custom charmap already exists. Overwrite it?", "Custom Charmap Exists");
                if (!overwrite) return;
            }

            if (CharMapManager.CreateCustomCharMapFile())
            {
                LoadCharMap();
                PopulateListsFromMap();
                await DialogHelper.ShowInfo("Custom charmap created successfully!", "Success");
            }
            else
            {
                await DialogHelper.ShowError("Failed to create custom charmap.", "Error");
            }
        }

        public async Task DeleteMapCommand()
        {
            bool confirm = await DialogHelper.AskYesNo(
                "Are you sure you want to delete the custom charmap? This cannot be undone.",
                "Delete Custom Charmap");
            if (!confirm) return;

            if (CharMapManager.DeleteCustomCharMapFile())
            {
                _currentMap = null;
                HasMap = false;
                SetDirty(false);
                PopulateListsFromMap();
                await DialogHelper.ShowInfo("Custom charmap deleted successfully!", "Success");
            }
            else
            {
                await DialogHelper.ShowError("Failed to delete custom charmap.", "Error");
            }
        }

        public async Task ReloadCommand()
        {
            if (_dirty)
            {
                var r = await DialogHelper.AskYesNoCancel(
                    "You have unsaved changes. Discard and reload?", "Unsaved Changes");
                if (r == DialogHelper.MsgResult.Cancel || r == DialogHelper.MsgResult.No) return;
            }
            LoadCharMap();
            PopulateListsFromMap();
        }

        public async Task OpenFileCommand()
        {
            if (!File.Exists(CharMapManager.customCharmapFilePath))
            {
                await DialogHelper.ShowInfo("No custom charmap file exists to open.", "File Not Found");
                return;
            }
            Helpers.OpenFileWithDefaultApp(CharMapManager.customCharmapFilePath);
        }

        public async Task RebaseCommand()
        {
            if (!File.Exists(CharMapManager.customCharmapFilePath))
            {
                await DialogHelper.ShowInfo("No custom charmap exists to merge. Please create one first.",
                    "No Custom Charmap");
                return;
            }

            if (!CharMapManager.IsCustomMapOutdated())
            {
                bool proceed = await DialogHelper.AskYesNo(
                    "The custom charmap is already up to date. Rebase anyway?", "Rebase Charmap");
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
                        "\n\nThere were conflicts. View them?", "Merge Result");
                    if (viewConflicts)
                        await DialogHelper.ShowInfo(result.GetConflictDetails(), "Merge Conflicts");
                }
                else
                {
                    await DialogHelper.ShowInfo("Charmap rebased successfully!\nRemember to save.\n\n" + summary,
                        "Rebase Complete");
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("Failed to rebase charmap: " + ex);
                await DialogHelper.ShowError("Failed to rebase charmap: " + ex.Message, "Error");
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

        public async Task<bool> ConfirmCloseAsync()
        {
            if (!_dirty) return true;
            var r = await DialogHelper.AskYesNoCancel(
                "You have unsaved changes. Save before closing?", "Unsaved Changes");
            if (r == DialogHelper.MsgResult.Yes) { await SaveCommand(); return true; }
            return r == DialogHelper.MsgResult.No;
        }

        // ── Private helpers ───────────────────────────────────────────────────
        private void SetDirty(bool d)
        {
            _dirty = d;
            Title  = d ? "Character Map Manager*" : "Character Map Manager";
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

            var r = await DialogHelper.AskYesNoCancel(message, "Merge Strategy");
            return r == DialogHelper.MsgResult.Yes   ? MergeStrategy.PreferCustom
                 : r == DialogHelper.MsgResult.No    ? MergeStrategy.PreferBase
                 : MergeStrategy.ReplaceBase;
        }
    }
}
