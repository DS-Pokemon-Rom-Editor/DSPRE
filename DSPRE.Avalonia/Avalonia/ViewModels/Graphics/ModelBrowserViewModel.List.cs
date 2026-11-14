using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using global::Avalonia;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Platform;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.Gl;
using DSPRE.ROMFiles;
using LibNDSFormats.NSBMD;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    public sealed partial class ModelBrowserViewModel
    {
        /// <summary>Reads and lists in one go.</summary>
        public void Reload() { Scan(); Publish(); }

        private List<CategoryTab> _scanned;
        private List<Item> _scannedItems;

        /// <summary>Reads every archive. The view runs it off the UI thread.</summary>
        public void Scan()
        {
            List<Item> found = new List<Item>();
            List<CategoryTab> tabs = new List<CategoryTab>();
            try { _buildingTextureSets = BuildingModelTextureSets.ReadCurrentRom(); }
            catch (Exception ex)
            {
                _buildingTextureSets = Array.Empty<BuildingModelTextureSet>();
                AppLogger.Error("ModelBrowser couldn't read building texture associations: " + ex.Message);
            }
            foreach (ModelAssets.Group g in Enum.GetValues<ModelAssets.Group>())
            {
                int inGroup = 0;
                foreach (ModelAssets.Archive a in ModelAssets.All.Where(x => x.In == g))
                {
                    int n;
                    try { n = ModelAssets.Count(a); } catch (Exception ex) { AppLogger.WarnOnce("ModelBrowserViewModel.Scan", ex); n = 0; }
                    if (n == 0) continue;
                    inGroup += ModelAssets.Units(a, n).Count;
                    foreach (ModelAssets.Unit u in ModelAssets.Units(a, n))
                        found.Add(new Item
                        {
                            Archive = a, In = g, Index = u.First, Unit = u,
                            Name = u.Name == a.Title ? null : u.Name,
                            Search = SearchMatch.Fold(a.Title + " " + u.First + " " + a.What + " " + u.Name),
                        });
                }
                if (inGroup > 0)
                    tabs.Add(new CategoryTab { Title = FriendlyGroup(g), Only = g, Count = inGroup,
                                               What = string.Join("  ", ModelAssets.All
                                                   .Where(x => x.In == g).Select(x => x.Title)) });
            }

            MarkTheUnclaimed(found);
            int orphans = found.Count(f => f.Unclaimed);
            if (orphans > 0)
                tabs.Add(new CategoryTab
                {
                    Title = "Unclaimed animations", OnlyUnclaimed = true, Count = orphans,
                    What = "Animations no model in this game is named for. They belong to something, but the "
                         + "names don't say what.",
                });

            if (tabs.Count > 0)
                tabs.Insert(0, new CategoryTab { Title = "Everything", Only = null, Count = found.Count,
                                                 What = "Every model, texture set and animation this game has." });

            _scannedItems = found;
            _scanned = tabs;
        }

        private bool _rescanning, _rescanAgain;

        /// <summary>A call during a scan queues one more scan instead of running alongside it.</summary>
        public async System.Threading.Tasks.Task RescanAsync()
        {
            if (_rescanning) { _rescanAgain = true; return; }
            _rescanning = true;
            try
            {
                do { _rescanAgain = false; await System.Threading.Tasks.Task.Run(Scan); } while (_rescanAgain);
            }
            finally { _rescanning = false; }
            Publish();
        }

        /// <summary>Puts what Scan found into the bound collections. UI thread only.</summary>
        public void Publish()
        {
            CategoryTab oldTab = _selectedTab;
            Item old = _selected;
            int oldPart = _partIndex;

            _everything.Clear();
            _everything.AddRange(_scannedItems ?? new List<Item>());
            _publishing = true;
            Tabs.Clear();
            foreach (CategoryTab t in _scanned ?? new List<CategoryTab>()) Tabs.Add(t);
            _selectedTab = Tabs.FirstOrDefault(t => oldTab != null && t.Only == oldTab.Only && t.OnlyUnclaimed == oldTab.OnlyUnclaimed)
                           ?? Tabs.FirstOrDefault();
            OnPropertyChanged(nameof(SelectedTab));
            _publishing = false;

            Item again = old == null ? null : _everything.FirstOrDefault(i => i.Archive.Dir == old.Archive.Dir && i.Index == old.Index);
            ApplyFilter(again, reread: true);
            if (again != null && ReferenceEquals(_selected, again) && oldPart > 0 && oldPart < Parts.Count) PartIndex = oldPart;
        }

        // An animation is unclaimed only when no model or texture set name in the game fits its names.
        private static void MarkTheUnclaimed(List<Item> found)
        {
            List<string> modelNames = found
                .Where(f => !string.IsNullOrWhiteSpace(f.Name))
                .Select(f => f.Name)
                .Distinct()
                .ToList();

            foreach (Item item in found)
            {
                ModelAssets.Kind? kind = KindOf(item);
                if (kind == null) continue;

                IReadOnlyList<string> names;
                try { names = AnimationNames(item, kind.Value); } catch (Exception ex) { AppLogger.WarnOnce("ModelBrowserViewModel.MarkTheUnclaimed", ex); continue; }
                item.Unclaimed = names.Count == 0
                                 || !modelNames.Any(m => ModelAssets.BelongsTo(names, m));
            }
        }

        private static ModelAssets.Kind? KindOf(Item item)
        {
            try
            {
                ScriptNarc narc = new ScriptNarc(item.Archive.Dir);
                if (!narc.Available) return null;
                byte[] b = narc.Get(item.Index);
                if (b == null) return null;
                ModelAssets.Kind k = ModelAssets.Identify(b);
                return IsAnAnimation(k) ? k : (ModelAssets.Kind?)null;
            }
            catch (Exception ex) { AppLogger.WarnOnce("ModelBrowserViewModel.KindOf", ex); return null; }
        }

        private static IReadOnlyList<string> AnimationNames(Item item, ModelAssets.Kind kind)
        {
            ScriptNarc narc = new ScriptNarc(item.Archive.Dir);
            if (!narc.Available) return Array.Empty<string>();
            byte[] b = narc.Get(item.Index);
            if (b == null) return Array.Empty<string>();
            return kind switch
            {
                ModelAssets.Kind.JointAnimation => JointAnimation.NamesIn(b),
                ModelAssets.Kind.TextureAnimation => TextureSrtAnimation.Load(b)?.MaterialNames ?? Array.Empty<string>(),
                ModelAssets.Kind.TextureSwap => TexturePatternAnimation.Load(b)?.MaterialNames ?? Array.Empty<string>(),
                ModelAssets.Kind.MaterialAnimation => MaterialColourAnimation.Load(b)?.MaterialNames ?? Array.Empty<string>(),
                ModelAssets.Kind.VisibilityAnimation => VisibilityAnimation.Load(b)?.AnimationNames ?? Array.Empty<string>(),
                _ => Array.Empty<string>(),
            };
        }

        private static string FriendlyGroup(ModelAssets.Group g) => g switch
        {
            ModelAssets.Group.Overworld => "People and objects",
            ModelAssets.Group.Buildings => "Buildings",
            ModelAssets.Group.Maps => "Maps",
            _ => "Other",
        };

        public ObservableCollection<CategoryTab> Tabs { get; } = new();
        public ObservableCollection<Item> Shown { get; } = new();

        private CategoryTab _selectedTab;
        // Set while Publish swaps tabs, so the list box doesn't filter on every step.
        private bool _publishing;
        public CategoryTab SelectedTab
        {
            get => _selectedTab;
            set { if (Set(ref _selectedTab, value) && !_publishing) ApplyFilter(); }
        }

        private string _search = "";
        public string Search { get => _search; set { if (Set(ref _search, value)) ApplyFilter(); } }

        private const int ShowAtMost = 3000;

        private void ApplyFilter(Item prefer = null, bool reread = false)
        {
            // Clearing the list sets Selected to null, so the selection is saved first.
            Item keep = prefer ?? _selected;
            Shown.Clear();
            string q = SearchMatch.Fold((_search ?? "").Trim());
            IEnumerable<Item> hits = _everything;
            if (_selectedTab?.OnlyUnclaimed == true) hits = hits.Where(i => i.Unclaimed);
            else if (_selectedTab?.Only != null) hits = hits.Where(i => i.In == _selectedTab.Only.Value);
            if (!string.IsNullOrEmpty(q)) hits = hits.Where(i => i.Search.Contains(q));
            foreach (Item i in hits.Take(ShowAtMost)) Shown.Add(i);
            Item next = keep != null && Shown.Contains(keep) ? keep : Shown.FirstOrDefault();
            if (!ReferenceEquals(_selected, next)) Selected = next;
            else if (reread && next != null) { BuildParts(); Look(); }
            OnPropertyChanged(nameof(FoundSummary));
        }

        public string FoundSummary
        {
            get
            {
                int total = _everything.Count;
                if (total == 0) return "This game has no 3D data DSPRE can list. Open a ROM first.";
                string q = (_search ?? "").Trim();
                int here = _selectedTab?.Count ?? total;
                string what = _selectedTab?.Only == null ? "in this game" : "under " + _selectedTab.Title;
                if (string.IsNullOrEmpty(q))
                    return Shown.Count < here
                        ? $"{here} models, texture sets and animations {what}; first {Shown.Count} shown"
                        : $"{here} models, texture sets and animations {what}.";
                return Shown.Count == 0
                    ? $"Nothing {what} matches \"{q}\". Try part of a name, like building or map."
                    : $"{Shown.Count} of {here} {what} match \"{q}\".";
            }
        }

        /// <summary>The files that make up what is picked, shown when there is more than one.</summary>
        public ObservableCollection<Part> Parts { get; } = new();
        public bool HasParts => Parts.Count > 1;

        private int _partIndex = -1;
        public int PartIndex
        {
            get => _partIndex;
            set { if (Set(ref _partIndex, value) && _selected != null) Look(); }
        }

        public int ShowingIndex => _partIndex >= 0 && _partIndex < Parts.Count
            ? Parts[_partIndex].Index : _selected?.Index ?? 0;

        private void BuildParts()
        {
            Parts.Clear();
            if (_selected?.Unit != null && _selected.Unit.Parts.Count > 1)
                foreach (ModelAssets.UnitPart up in _selected.Unit.Parts)
                    Parts.Add(new Part { Index = up.Index, Name = up.Name });

            _partIndex = -1;
            OnPropertyChanged(nameof(PartIndex));
            _partIndex = Parts.Count > 0 ? 0 : -1;
            OnPropertyChanged(nameof(PartIndex));
            OnPropertyChanged(nameof(HasParts));
        }
    }
}
