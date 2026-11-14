using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using global::Avalonia.Platform.Storage;
using DSPRE.Avalonia;
using DSPRE.CharMaps;
using DSPRE.Editors;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Text
{
    public partial class TextEditorViewModel
    {
        private string _searchText = "";
        public string SearchText { get => _searchText; set => Set(ref _searchText, value); }

        private string _replaceText = "";
        public string ReplaceText { get => _replaceText; set => Set(ref _replaceText, value); }

        private bool _searchAllArchives = true;
        public bool SearchAllArchives { get => _searchAllArchives; set => Set(ref _searchAllArchives, value); }

        private bool _caseSensitive;
        public bool CaseSensitive { get => _caseSensitive; set => Set(ref _caseSensitive, value); }

        private string _statusText = "Not loaded";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        public void Search()
        {
            if (string.IsNullOrEmpty(SearchText)) return;

            int first, last;
            if (SearchAllArchives) { first = 0; last = Filesystem.GetTextArchivesCount(); }
            else { first = SelectedArchiveIndex; last = first + 1; }
            if (first < 0) return;

            SearchResults.Clear();
            Func<string, bool> match = CaseSensitive
                ? (x => x.Contains(SearchText))
                : (x => x.IndexOf(SearchText, StringComparison.InvariantCultureIgnoreCase) >= 0);

            for (int i = first; i < last; i++)
            {
                TextArchive file = new TextArchive(i);
                for (int j = 0; j < file.Messages.Count; j++)
                {
                    if (match(file.Messages[j]))
                    {
                        string preview = file.Messages[j].Substring(0, Math.Min(file.Messages[j].Length, 40));
                        SearchResults.Add(new TextSearchResultVM(i, j,
                            $"({i:D3}) - #{j:D2} --- {preview}"));
                    }
                }
            }
            StatusText = $"Found {Plural.Of(SearchResults.Count, "match", "matches")}.";
        }

        public async Task ReplaceAsync()
        {
            if (string.IsNullOrEmpty(SearchText)) return;

            int first, last;
            string specify;
            if (SearchAllArchives)
            {
                first = 0; last = Filesystem.GetTextArchivesCount();
                specify = $" in every text archive ({first} to {last - 1})";
            }
            else
            {
                if (_current == null) return;
                if (await RefusedForManagedArchiveAsync()) return;
                first = SelectedArchiveIndex; last = first + 1;
                specify = $" in text archive {first} only";
            }
            if (first < 0) return;

            string message = $"Replace every \"{SearchText}\" with \"{ReplaceText}\"{specify}? This saves at once and can't be undone.";
            if (!await DialogHelper.AskYesNoAsync(message, "Replace text?")) return;
            if (!await SaveOrDiscardBeforeReloadAsync("Replacing")) return;

            SearchResults.Clear();

            // hg-engine builds these from its own files, which a replace here would not reach.
            int skipped = 0;
            bool OwnedByHgEngine(int id)
            {
                if (HgEngineOwnedFiles.Get(TextArchivePath, id) == null) return false;
                skipped++;
                return true;
            }

            List<int> edited;
            try
            {
                edited = DSUtils.ReplaceTextInArchives(
                    new[] { (SearchText, ReplaceText ?? "", CaseSensitive) }, first, last, OwnedByHgEngine, this);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // Earlier archives may already be rewritten, so the shown one is read again either way.
                if (SelectedArchiveIndex >= 0) LoadArchive(SelectedArchiveIndex);
                AppEvents.RaiseNamesChanged();
                bool one = last - first == 1;
                StatusText = "";
                await DialogHelper.ShowErrorAsync(one
                    ? $"Text archive {first} wasn't changed:\n{ex.Message}"
                    : $"Replace stopped partway. Archives before the failure were already changed:\n{ex.Message}", "Text editor");
                return;
            }
            foreach (int cur in edited)
                SearchResults.Add(new TextSearchResultVM(cur, 0, $"Text archive {cur} edited"));

            if (SelectedArchiveIndex >= 0) LoadArchive(SelectedArchiveIndex);
            if (edited.Count > 0) AppEvents.RaiseNamesChanged();
            StatusText = skipped == 0 ? ""
                : skipped == 1 ? "1 hg-engine text archive was skipped." : $"{skipped} hg-engine text archives were skipped.";
        }

        /// <summary>Shows a search result's line, loading its archive first if needed.</summary>
        public async Task GoToResultAsync(TextSearchResultVM result)
        {
            if (result == null) return;
            if (SelectedArchiveIndex != result.Archive)
            {
                if (OpenResultElsewhere?.Invoke(result.Archive, result.Line) == true) return;
                SelectedArchiveIndex = result.Archive;
                await _archiveLoad;
                // Cancelled at the unsaved changes prompt.
                if (SelectedArchiveIndex != result.Archive) return;
            }
            RevealLine(result.Line);
        }
    }
}
