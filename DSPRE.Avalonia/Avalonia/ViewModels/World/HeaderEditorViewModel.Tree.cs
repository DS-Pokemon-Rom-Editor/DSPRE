using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using global::Avalonia.Collections;
using global::Avalonia.Controls;
using global::Avalonia.Threading;
using global::Avalonia.Media;
using global::Avalonia.Media.Imaging;
using DSPRE.Avalonia;
using DSPRE.Avalonia.Models;
using DSPRE.Editors;
using DSPRE.Resources;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;
using DSPRE.Avalonia.Data;
using Avalonia.Platform.Storage;

namespace DSPRE.Avalonia.ViewModels.World
{
    public partial class HeaderEditorViewModel
    {
        // Typed HeaderTreeNode: TreeView casts a clicked leaf to T and throws if T is HeaderTreeFolder.
        private readonly AvaloniaList<HeaderTreeNode> _treeFolders = new AvaloniaList<HeaderTreeNode>();
        public AvaloniaList<HeaderTreeNode> TreeFolders => _treeFolders;
        private List<HeaderTreeFolder> _allTreeFolders = new List<HeaderTreeFolder>();
        private IReadOnlyList<HeaderSearchEntry> _headerSearchIndex = Array.Empty<HeaderSearchEntry>();
        private List<int> _locationIndexByHeader = new List<int>();
        private Dictionary<HeaderTreeFolder, bool> _folderExpansionBeforeFilter;
        private bool _filterTreeActive;

        private CancellationTokenSource _treeFilterCancellation;
        private int _treeFilterGeneration;

        private HeaderTreeNode _selectedTreeNode;
        public HeaderTreeNode SelectedTreeNode
        {
            get => _selectedTreeNode;
            set
            {
                // Hiding the TreeView under a filter makes its binding report null briefly. Ignore it.
                if (RecordSwitchGuard.IsSnappingBack) return;
                if (value == null && !_suppress && _selectedTreeNode != null) return;
                if (!_suppress && value is HeaderTreeLeaf pick && MustAskBeforeLeaving(pick.HeaderId))
                {
                    // Leave the tree on the loaded header until the user answers.
                    RecordSwitchGuard.SnapBack(() => _selectedTreeNode, v => _selectedTreeNode = v, () => OnPropertyChanged(nameof(SelectedTreeNode)), _ => null);
                    if (!_switchPending) _ = SwitchHeaderAsync(pick.HeaderId);
                    return;
                }
                if (!Set(ref _selectedTreeNode, value) || _suppress) return;
                if (value is HeaderTreeLeaf leaf) { SelectedHeaderId = leaf.HeaderId; LoadHeader(leaf.HeaderId); }
            }
        }

        private bool _switchPending;

        /// <summary>From the Maps workspace: true if a tab with unsaved edits would change file.</summary>
        public Func<ushort, bool> LinkedEditsWouldMove { get; set; }
        public Func<ushort, Task<bool>> ConfirmLinkedTabsAsync { get; set; }

        private bool MustAskBeforeLeaving(ushort id)
            => _header != null && id != _header.ID && (_dirty || (LinkedEditsWouldMove?.Invoke(id) ?? false));

        private async Task SwitchHeaderAsync(ushort id)
        {
            _switchPending = true;
            try
            {
                if (!await RecordSwitchGuard.ConfirmLeaveAsync(this, _owner, "header")) return;
                if (ConfirmLinkedTabsAsync != null && !await ConfirmLinkedTabsAsync(id)) return;
                SetClean();
                if (!string.IsNullOrWhiteSpace(TreeFilterText) && FindLeaf(id) is { IsVisible: false }) TreeFilterText = "";
                SelectHeader(id);
            }
            finally { _switchPending = false; }
        }

        private ushort _selectedHeaderId;
        public ushort SelectedHeaderId { get => _selectedHeaderId; private set => Set(ref _selectedHeaderId, value); }

        public bool IsFiltering => !string.IsNullOrWhiteSpace(_treeFilterText);

        private string _treeFilterText = "";
        public string TreeFilterText
        {
            get => _treeFilterText;
            set
            {
                if (!Set(ref _treeFilterText, value ?? string.Empty)) return;
                OnPropertyChanged(nameof(IsFiltering));
                ScheduleTreeRebuild();
            }
        }

        private bool _fuzzySearch;
        public bool FuzzySearch
        {
            get => _fuzzySearch;
            set { if (Set(ref _fuzzySearch, value)) ScheduleTreeRebuild(); }
        }

        // Reads each header's location-name index once, for grouping and the first selection.
        private void LoadLocationIndices()
        {
            int mystery = FindMysteryZoneIndex();
            _locationIndexByHeader = new List<int>(_headerListNames.Count);
            for (ushort id = 0; id < _headerListNames.Count; id++)
                _locationIndexByHeader.Add(
                    MapHeader.TryReadLocationNameIndex(id, _dynamicHeaders, out int idx) ? idx : mystery);
        }

        private int FindMysteryZoneIndex()
        {
            for (int i = 0; i < LocationNames.Count; i++)
                if (LocationNames[i] != null && LocationNames[i].Trim().EndsWith("Mystery Zone", StringComparison.OrdinalIgnoreCase))
                    return i;
            return 0;
        }

        /// <summary>Header to open on load, set before SetupAsync. -1 picks one.</summary>
        public int InitialHeaderId { get; set; } = -1;

        private ushort FindInitialHeaderId()
        {
            if (InitialHeaderId >= 0 && InitialHeaderId < _locationIndexByHeader.Count)
                return (ushort)InitialHeaderId;

            // Open where a new game starts, not at whichever header sorts first.
            int start = FieldStartLocation.HeaderFor(GameFamily, _headerListNames);
            if (start >= 0 && start < _locationIndexByHeader.Count) return (ushort)start;

            int mystery = FindMysteryZoneIndex();
            for (ushort id = 0; id < _locationIndexByHeader.Count; id++)
                if (_locationIndexByHeader[id] != mystery)
                    return id;
            return 0;
        }

        // Groups by location, routes together.
        private void RebuildTree()
        {
            CancelPendingTreeRebuild();
            _filterTreeActive = false;
            _folderExpansionBeforeFilter = null;
            TreeBuildInput input = CaptureTreeBuildInput();
            ApplyTreeBuild(BuildTreeStructure(input), input.Query, input.Fuzzy);
        }

        private void ScheduleTreeRebuild()
        {
            CancelPendingTreeRebuild();
            if (_headerSearchIndex.Count == 0 || _allTreeFolders.Count == 0) return;

            string query = (TreeFilterText ?? "").Trim();
            TreeFilterInput input = new TreeFilterInput(_headerSearchIndex, query, _fuzzySearch);
            if (!input.Filtering)
            {
                ApplyTreeFilter(input, new HashSet<ushort>());
                return;
            }

            int generation = unchecked(++_treeFilterGeneration);
            CancellationTokenSource cancellation = new CancellationTokenSource();
            _treeFilterCancellation = cancellation;
            _ = RebuildTreeAsync(cancellation, generation, input);
        }

        private TreeBuildInput CaptureTreeBuildInput()
        {
            return new TreeBuildInput
            {
                HeaderNames = _headerListNames.ToList(),
                LocationIndices = _locationIndexByHeader.ToList(),
                LocationNames = LocationNames.ToList(),
                Query = (TreeFilterText ?? "").Trim(),
                Fuzzy = _fuzzySearch
            };
        }

        private async Task RebuildTreeAsync(CancellationTokenSource cancellation, int generation, TreeFilterInput input)
        {
            CancellationToken token = cancellation.Token;
            try
            {
                HashSet<ushort> matches = await Task.Run(() => BuildTreeFilter(input, token), token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();

                Dispatcher.UIThread.Post(() =>
                {
                    if (token.IsCancellationRequested || generation != _treeFilterGeneration) return;
                    ApplyTreeFilter(input, matches);
                });
            }
            catch (OperationCanceledException)
            {
                // A newer filter value superseded this rebuild.
            }
            catch (Exception ex)
            {
                AppLogger.Error("Header tree filter failed: " + ex);
            }
            finally
            {
                if (ReferenceEquals(_treeFilterCancellation, cancellation))
                    _treeFilterCancellation = null;
                cancellation.Dispose();
            }
        }

        private static TreeBuildResult BuildTreeStructure(TreeBuildInput input)
        {
            Dictionary<string, HeaderTreeFolder> byName = new Dictionary<string, HeaderTreeFolder>(StringComparer.OrdinalIgnoreCase);
            List<HeaderTreeFolder> order = new List<HeaderTreeFolder>();
            List<HeaderSearchEntry> searchIndex = new List<HeaderSearchEntry>(input.HeaderNames.Count);

            for (int id = 0; id < input.HeaderNames.Count; id++)
            {
                ushort headerId = (ushort)id;
                string label = input.HeaderNames[id];
                string locationName = LocationNameFor(input, headerId);
                string folderName = FolderNameFor(locationName);

                if (!byName.TryGetValue(folderName, out HeaderTreeFolder folder))
                {
                    folder = new HeaderTreeFolder { DisplayName = folderName };
                    byName[folderName] = folder;
                    order.Add(folder);
                }
                HeaderTreeLeaf leaf = new HeaderTreeLeaf { HeaderId = headerId, DisplayName = label };
                folder.Children.Add(leaf);
                searchIndex.Add(new HeaderSearchEntry(headerId, label, locationName, folderName));
            }

            return new TreeBuildResult(
                order.OrderBy(f => f.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList(),
                searchIndex);
        }

        private static HashSet<ushort> BuildTreeFilter(TreeFilterInput input, CancellationToken cancellationToken)
        {
            HashSet<ushort> matches = new HashSet<ushort>();
            if (!input.Filtering) return matches;

            foreach (HeaderSearchEntry entry in input.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (HeaderMatchesFilter(input.Query, entry, input.Fuzzy))
                    matches.Add(entry.HeaderId);
            }
            return matches;
        }

        private void ApplyTreeBuild(TreeBuildResult result, string query, bool fuzzy)
        {
            _allTreeFolders = result.Folders;
            _headerSearchIndex = result.SearchIndex;
            _treeFolders.Clear();
            _treeFolders.AddRange(result.Folders);
            TreeFilterInput input = new TreeFilterInput(_headerSearchIndex, query, fuzzy);
            ApplyTreeFilter(input, BuildTreeFilter(input, CancellationToken.None));
        }

        private void ApplyTreeFilter(TreeFilterInput input, HashSet<ushort> matches)
        {
            _suppress = true;
            try
            {
                ushort keepHeaderId = SelectedHeaderId;
                if (input.Filtering)
                {
                    if (!_filterTreeActive)
                    {
                        _folderExpansionBeforeFilter = _allTreeFolders.ToDictionary(f => f, f => f.IsExpanded);
                        _filterTreeActive = true;
                    }

                    foreach (HeaderTreeFolder folder in _allTreeFolders)
                    {
                        bool hasMatch = false;
                        foreach (HeaderTreeLeaf leaf in folder.Children.OfType<HeaderTreeLeaf>())
                        {
                            bool visible = matches.Contains(leaf.HeaderId);
                            leaf.IsVisible = visible;
                            hasMatch |= visible;
                        }
                        folder.IsVisible = hasMatch;
                        folder.IsExpanded = hasMatch;
                    }

                    HeaderTreeNode logicalSelection = FindLeaf(keepHeaderId) ?? _selectedTreeNode;
                    if (logicalSelection != null) SelectedTreeNode = logicalSelection;
                }
                else
                {
                    foreach (HeaderTreeFolder folder in _allTreeFolders)
                    {
                        folder.IsVisible = true;
                        foreach (HeaderTreeLeaf leaf in folder.Children.OfType<HeaderTreeLeaf>())
                            leaf.IsVisible = true;

                        if (_filterTreeActive && _folderExpansionBeforeFilter != null
                            && _folderExpansionBeforeFilter.TryGetValue(folder, out bool wasExpanded))
                            folder.IsExpanded = wasExpanded;
                        else
                            folder.IsExpanded = folder.Children.OfType<HeaderTreeLeaf>().Any(l => l.HeaderId == keepHeaderId);
                    }
                    _filterTreeActive = false;
                    _folderExpansionBeforeFilter = null;
                    SelectedTreeNode = FindLeaf(keepHeaderId);
                }
            }
            finally
            {
                _suppress = false;
            }
        }

        private void CancelPendingTreeRebuild()
        {
            CancellationTokenSource cancellation = _treeFilterCancellation;
            _treeFilterCancellation = null;
            try { cancellation?.Cancel(); }
            catch (ObjectDisposedException) { /* the search already finished and was disposed */ }
            unchecked { _treeFilterGeneration++; }
        }

        private sealed class TreeBuildInput
        {
            public List<string> HeaderNames { get; set; }
            public List<int> LocationIndices { get; set; }
            public List<string> LocationNames { get; set; }
            public string Query { get; set; }
            public bool Fuzzy { get; set; }
        }

        private sealed class TreeBuildResult
        {
            public List<HeaderTreeFolder> Folders { get; }
            public IReadOnlyList<HeaderSearchEntry> SearchIndex { get; }

            public TreeBuildResult(List<HeaderTreeFolder> folders, IReadOnlyList<HeaderSearchEntry> searchIndex)
            {
                Folders = folders;
                SearchIndex = searchIndex;
            }
        }

        private sealed class TreeFilterInput
        {
            public IReadOnlyList<HeaderSearchEntry> Entries { get; }
            public string Query { get; }
            public bool Fuzzy { get; }
            public bool Filtering => Query.Length > 0;

            public TreeFilterInput(IReadOnlyList<HeaderSearchEntry> entries, string query, bool fuzzy)
            {
                Entries = entries;
                Query = query;
                Fuzzy = fuzzy;
            }
        }

        private sealed class HeaderSearchEntry
        {
            public ushort HeaderId { get; }
            public string Label { get; }
            public string LocationName { get; }
            public string FolderName { get; }
            public string IdText { get; }

            public HeaderSearchEntry(ushort headerId, string label, string locationName, string folderName)
            {
                HeaderId = headerId;
                Label = label ?? "";
                LocationName = locationName ?? "";
                FolderName = folderName ?? "";
                IdText = headerId.ToString();
            }
        }

        private static string LocationNameFor(TreeBuildInput input, ushort id)
        {
            int index = id < input.LocationIndices.Count ? input.LocationIndices[id] : -1;
            return index >= 0 && index < input.LocationNames.Count
                ? (input.LocationNames[index] ?? "").Trim()
                : "";
        }

        private static string FolderNameFor(string locationName)
        {
            if (locationName.StartsWith("Route ", StringComparison.OrdinalIgnoreCase)) return "Routes";
            return string.IsNullOrEmpty(locationName) ? "Unknown" : locationName;
        }

        private static bool HeaderMatchesFilter(string q, HeaderSearchEntry entry, bool fuzzy)
        {
            if (SearchMatch.Contains(entry.Label, q)
                || SearchMatch.Contains(entry.FolderName, q)
                || SearchMatch.Contains(entry.LocationName, q)
                || SearchMatch.Contains(entry.IdText, q))
                return true;
            return fuzzy && (SearchMatch.NearMiss(entry.LocationName, q) || SearchMatch.NearMiss(entry.Label, q));
        }

        public void ExpandAllFolders() { foreach (HeaderTreeNode f in TreeFolders) f.IsExpanded = true; }
        public void CollapseAllFolders() { foreach (HeaderTreeNode f in TreeFolders) f.IsExpanded = false; }

        public void SelectHeader(ushort headerId)
        {
            ExpandFolderContaining(headerId);
            _suppress = true;
            SelectedTreeNode = FindLeaf(headerId);
            _suppress = false;
            SelectedHeaderId = headerId;
            LoadHeader(headerId);
        }

        private void ExpandFolderContaining(ushort headerId)
        {
            foreach (HeaderTreeFolder folder in _allTreeFolders)
                if (folder.Children.OfType<HeaderTreeLeaf>().Any(l => l.HeaderId == headerId))
                {
                    folder.IsExpanded = true;
                    return;
                }
        }

        private HeaderTreeLeaf FindLeaf(ushort headerId)
        {
            foreach (HeaderTreeFolder folder in _allTreeFolders)
            {
                HeaderTreeLeaf leaf = folder.Children.OfType<HeaderTreeLeaf>().FirstOrDefault(l => l.HeaderId == headerId);
                if (leaf != null) return leaf;
            }
            return null;
        }

        private void UpdateHeaderLocationInTree(ushort id, int locIndex)
        {
            if (id >= _locationIndexByHeader.Count || _locationIndexByHeader[id] == locIndex) return;
            _locationIndexByHeader[id] = locIndex;
            RebuildTree();
        }

        private int CurrentHeaderLocationIndex()
        {
            switch (GameFamily)
            {
                case GameFamilies.DP: return ((HeaderDP)_header).LocationName;
                case GameFamilies.Plat: return ((HeaderPt)_header).LocationName;
                default: return ((HeaderHGSS)_header).LocationName;
            }
        }

        public void ReloadLocationNames()
        {
            if (_headerListNames.Count == 0 || !AvaloniaEditorLauncher.IsRomLoaded) return;   // not set up yet
            if (AvaloniaRomLoader.Loading) return;   // the folders are being swapped
            List<string> fresh = ReadLocationNames();
            if (fresh.SequenceEqual(LocationNames)) return;   // archive untouched: leave the combo alone

            // Clearing the ItemsSource makes the ComboBox write -1 back and wipe _locationNameIndex.
            int keepLoc = _locationNameIndex;
            _suppress = true;
            LocationNames.Clear();
            foreach (string m in fresh) LocationNames.Add(m);
            _locationNameIndex = -1;
            LocationNameIndex = keepLoc < LocationNames.Count ? keepLoc : -1;   // suppressed: restores the combo without re-applying
            _suppress = false;
            RebuildTree();
        }
    }
}
