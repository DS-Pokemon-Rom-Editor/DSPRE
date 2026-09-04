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
    /// <summary>The game's models and the pictures painted on them, in their own list.</summary>
    public sealed partial class ModelBrowserViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        {
            if (EqualityComparer<T>.Default.Equals(f, v)) return false;
            f = v; OnPropertyChanged(n); return true;
        }

        /// <summary>Raised when a model has been read and is ready to be handed to the 3D view.</summary>
        public event EventHandler ModelReady;

        public sealed class Item
        {
            public ModelAssets.Archive Archive { get; init; }
            public ModelAssets.Group In { get; init; }
            public int Index { get; init; }
            public ModelAssets.Unit Unit { get; init; }
            /// <summary>What the file calls itself, which is the name GameFreak gave it.</summary>
            public string Name { get; init; }
            public string Label => Name == null
                ? $"{Index,5}  {Archive.Title}"
                : $"{Index,5}  {Name}";
            public string Search { get; init; }

            /// <summary>An animation, and no model in the game is named for it.</summary>
            public bool Unclaimed { get; set; }
        }

        /// <summary>One file of the thing picked.</summary>
        public sealed class Part
        {
            public int Index { get; init; }
            public string Name { get; init; }
            public string Label => Name;
        }

        /// <summary>One tab: a kind of 3D thing, and how many of them this game has.</summary>
        public sealed class CategoryTab
        {
            public string Title { get; init; }
            public ModelAssets.Group? Only { get; init; }   // null means everything
            /// <summary>A tab for the animations nothing is named for, rather than a kind of thing.</summary>
            public bool OnlyUnclaimed { get; init; }
            public int Count { get; init; }
            public string What { get; init; }
            public string Header => $"{Title} ({Count})";
        }

        private readonly List<Item> _everything = new();
        private IReadOnlyList<BuildingModelTextureSet> _buildingTextureSets = Array.Empty<BuildingModelTextureSet>();

        /// <summary>
        /// Reading every archive to see what is in it is real file work, so it does not happen here.
        /// The caller runs <see cref="Scan"/> off the UI thread and then <see cref="Publish"/> on it.
        /// </summary>
        public ModelBrowserViewModel() { }

        /// <summary>Reads and lists in one go, for callers that are already off the UI thread.</summary>
        public void Reload() { Scan(); Publish(); }

        private List<CategoryTab> _scanned;

        /// <summary>
        /// Walks every archive and works out what it holds. Touches files and nothing else, so it is
        /// safe to run away from the UI thread; it fills plain lists rather than the bound collections.
        /// </summary>
        public void Scan()
        {
            var found = new List<Item>();
            var tabs = new List<CategoryTab>();
            try { _buildingTextureSets = BuildingModelTextureSets.ReadCurrentRom(); }
            catch (Exception ex)
            {
                _buildingTextureSets = Array.Empty<BuildingModelTextureSet>();
                AppLogger.Error("ModelBrowser could not read building texture associations: " + ex.Message);
            }
            foreach (var g in Enum.GetValues<ModelAssets.Group>())
            {
                int inGroup = 0;
                foreach (var a in ModelAssets.All.Where(x => x.In == g))
                {
                    int n;
                    try { n = ModelAssets.Count(a); } catch { n = 0; }
                    if (n == 0) continue;
                    inGroup += ModelAssets.Units(a, n).Count;
                    foreach (var u in ModelAssets.Units(a, n))
                        found.Add(new Item
                        {
                            Archive = a, In = g, Index = u.First, Unit = u,
                            Name = u.Name == a.Title ? null : u.Name,
                            Search = (a.Title + " " + u.First + " " + a.What + " " + u.Name).ToLowerInvariant(),
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
                    Title = "Nothing claims these", OnlyUnclaimed = true, Count = orphans,
                    What = "Animations no model in this game is named for. They still belong to something, "
                         + "but the names do not say what.",
                });

            if (tabs.Count > 0)
                tabs.Insert(0, new CategoryTab { Title = "Everything", Only = null, Count = found.Count,
                                                 What = "Every model, picture set and animation this game has." });

            _everything.Clear();
            _everything.AddRange(found);
            _scanned = tabs;
        }

        /// <summary>Puts what <see cref="Scan"/> found into the bound collections. UI thread only.</summary>
        public void Publish()
        {
            Tabs.Clear();
            foreach (var t in _scanned ?? new List<CategoryTab>()) Tabs.Add(t);
            _selectedTab = Tabs.FirstOrDefault();
            OnPropertyChanged(nameof(SelectedTab));
            ApplyFilter();
        }

        /// <summary>
        /// Works out which animations no model is named for. Every model name in the game is collected
        /// first, then each animation's own names are matched against them, so an animation only lands in
        /// the unclaimed group when nothing anywhere fits it.
        /// </summary>
        private static void MarkTheUnclaimed(List<Item> found)
        {
            var modelNames = found
                .Where(f => !string.IsNullOrWhiteSpace(f.Name))
                .Select(f => f.Name)
                .Distinct()
                .ToList();

            foreach (var item in found)
            {
                var kind = KindOf(item);
                if (kind == null) continue;

                IReadOnlyList<string> names;
                try { names = AnimationNames(item, kind.Value); } catch { continue; }
                item.Unclaimed = names.Count == 0
                                 || !modelNames.Any(m => ModelAssets.BelongsTo(names, m));
            }
        }

        private static ModelAssets.Kind? KindOf(Item item)
        {
            try
            {
                var narc = new ScriptNarc(item.Archive.Dir);
                if (!narc.Available) return null;
                var b = narc.Get(item.Index);
                if (b == null) return null;
                var k = ModelAssets.Identify(b);
                return IsAnAnimation(k) ? k : (ModelAssets.Kind?)null;
            }
            catch { return null; }
        }

        private static IReadOnlyList<string> AnimationNames(Item item, ModelAssets.Kind kind)
        {
            var narc = new ScriptNarc(item.Archive.Dir);
            if (!narc.Available) return Array.Empty<string>();
            var b = narc.Get(item.Index);
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
        public CategoryTab SelectedTab
        {
            get => _selectedTab;
            set { if (Set(ref _selectedTab, value)) ApplyFilter(); }
        }

        private string _search = "";
        public string Search { get => _search; set { if (Set(ref _search, value)) ApplyFilter(); } }

        private const int ShowAtMost = 3000;

        private void ApplyFilter()
        {
            Shown.Clear();
            string q = (_search ?? "").Trim().ToLowerInvariant();
            IEnumerable<Item> hits = _everything;
            if (_selectedTab?.OnlyUnclaimed == true) hits = hits.Where(i => i.Unclaimed);
            else if (_selectedTab?.Only != null) hits = hits.Where(i => i.In == _selectedTab.Only.Value);
            if (!string.IsNullOrEmpty(q)) hits = hits.Where(i => i.Search.Contains(q));
            foreach (var i in hits.Take(ShowAtMost)) Shown.Add(i);
            if (_selected == null || !Shown.Contains(_selected))
                Selected = Shown.FirstOrDefault();
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
                        ? $"{here} models, picture sets and animations {what}. Showing the first {Shown.Count}; type to narrow it down."
                        : $"{here} models, picture sets and animations {what}.";
                return Shown.Count == 0
                    ? $"Nothing {what} matches \"{q}\". Try part of a name, like building or map."
                    : $"{Shown.Count} of {here} {what} match \"{q}\".";
            }
        }

        /// <summary>The files that make up whatever is picked, shown only when there is more than one.</summary>
        public ObservableCollection<Part> Parts { get; } = new();
        public bool HasParts => Parts.Count > 1;

        private int _partIndex = -1;
        public int PartIndex
        {
            get => _partIndex;
            set { if (Set(ref _partIndex, value) && _selected != null) Look(); }
        }

        /// <summary>The file being shown: the piece picked, or the thing itself when it is one file.</summary>
        public int ShowingIndex => _partIndex >= 0 && _partIndex < Parts.Count
            ? Parts[_partIndex].Index : _selected?.Index ?? 0;

        private void BuildParts()
        {
            Parts.Clear();
            if (_selected?.Unit != null && _selected.Unit.Parts.Count > 1)
                foreach (var up in _selected.Unit.Parts)
                    Parts.Add(new Part { Index = up.Index, Name = up.Name });

            _partIndex = -1;
            OnPropertyChanged(nameof(PartIndex));
            _partIndex = Parts.Count > 0 ? 0 : -1;
            OnPropertyChanged(nameof(PartIndex));
            OnPropertyChanged(nameof(HasParts));
        }

        private Item _selected;
        public Item Selected
        {
            get => _selected;
            set
            {
                if (!Set(ref _selected, value)) return;
                BuildParts();
                Look();
            }
        }

        /// <summary>The model the 3D view should draw, or null when there is not one.</summary>
        public NsbmdRenderModel Model3D { get; private set; }

        private Bitmap _texturePreview;
        public Bitmap TexturePreview
        {
            get => _texturePreview;
            private set
            {
                if (!Set(ref _texturePreview, value)) return;
                OnPropertyChanged(nameof(HasTexturePreview));
                OnPropertyChanged(nameof(HasNoModel));
            }
        }
        public bool HasTexturePreview => TexturePreview != null;

        private string _details = "Pick something on the left to see it.";
        public string Details { get => _details; private set => Set(ref _details, value); }

        private string _whynot = "";
        public string Whynot { get => _whynot; private set => Set(ref _whynot, value); }
        public bool HasModel => Model3D != null;
        public bool HasNoModel => Model3D == null && TexturePreview == null && !string.IsNullOrEmpty(_whynot);

        private ModelAssets.Options _options;

        private void Look()
        {
            Model3D = null;
            ClearTexturePreview();
            ClearCompanions();
            TextureChoices.Clear();
            _textureSetEntries.Clear();
            AnimationChoices.Clear();
            _animationEntries.Clear();
            Playing = false;
            AnimationDetails = "";
            AnimationOwner = "";
            MovementNote = "";
            Whynot = "";
            if (_selected == null)
            {
                _options = null;
                BaseDetails = "";
                Details = "Pick something on the left to see it.";
                OnPropertyChanged(nameof(HasTextureChoice));
                OnPropertyChanged(nameof(HasAnimationChoice));
                OnPropertyChanged(nameof(HasAnimationDetails));
                OnPropertyChanged(nameof(HasMovementNote));
                RaiseAll();
                return;
            }

            var a = _selected.Archive;
            _options = ModelAssets.WhatCanBeDone(a, ShowingIndex);
            BaseDetails = $"{Named}, number {ShowingIndex}. {ModelAssets.ShortName(_options.Kind)}.";
            Details = BaseDetails;

            // An animation picked on its own has no shape to draw, so say what it does instead of
            // leaving an empty box and the word "animation".
            if (IsAnAnimation(_options.Kind))
            {
                AnimationDetails = DescribeAnimation(a, ShowingIndex, _options.Kind);
                AnimationOwner = WhoClaimsIt(a, ShowingIndex, _options.Kind);
            }
            OnPropertyChanged(nameof(AnimationDetails));
            OnPropertyChanged(nameof(AnimationOwner));
            OnPropertyChanged(nameof(HasAnimationDetails));
            OnPropertyChanged(nameof(HasCompanionSummary));
            OnPropertyChanged(nameof(CompanionSummary));

            if (_options.Kind == ModelAssets.Kind.TextureBundle)
            {
                LoadTexturePreview(a, ShowingIndex);
                OnPropertyChanged(nameof(HasTextureChoice));
                OnPropertyChanged(nameof(HasAnimationChoice));
            }
            else if (_options.CanShow)
            {
                FindCompanions(a, ShowingIndex, Named);
                BuildTextureChoices(a);
                BuildAnimationChoices(a);
                Draw();
            }
            else
            {
                Whynot = _options.ShowNote ?? "There is nothing here to show.";
                OnPropertyChanged(nameof(HasTextureChoice));
                OnPropertyChanged(nameof(HasAnimationChoice));
            }

            RaiseAll();
            if (Model3D != null) ModelReady?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>What this model can be dressed in. </summary>
        public ObservableCollection<string> TextureChoices { get; } = new();
        public bool HasTextureChoice => TextureChoices.Count > 1;
        private readonly List<int> _textureSetEntries = new();

        private int _textureChoice;
        public int TextureChoice
        {
            get => _textureChoice;
            set
            {
                if (!Set(ref _textureChoice, value) || _selected == null) return;
                Draw();
                RaiseAll();
                if (Model3D != null) ModelReady?.Invoke(this, EventArgs.Empty);
            }
        }

        private void BuildTextureChoices(ModelAssets.Archive a)
        {
            TextureChoices.Clear();
            _textureSetEntries.Clear();
            bool embedded = ModelAssets.EmbeddedTextures(a, ShowingIndex) != null
                         || ModelAssets.NeighbouringTextures(a, ShowingIndex) != null;
            TextureChoices.Add(embedded ? "Its own pictures" : "No embedded pictures");
            _textureSetEntries.Add(-1);
            int sets = ModelAssets.TextureSetCount(a);
            var uses = _buildingTextureSets
                .Where(x => x.Indoor == a.Indoor && x.ModelIds.Contains(ShowingIndex))
                .OrderBy(x => x.TextureSetId).ToList();
            var authoritative = new HashSet<int>(uses.Select(x => x.TextureSetId));
            var model = ModelAssets.LoadModel(a, ShowingIndex);

            foreach (var use in uses)
            {
                if (use.TextureSetId < 0 || use.TextureSetId >= sets) continue;
                var coverage = ModelAssets.Coverage(model, ModelAssets.TextureSet(a, use.TextureSetId));
                string areas = string.Join(", ", use.AreaIds.Take(4));
                if (use.AreaIds.Count > 4) areas += ", …";
                AddTextureChoice(use.TextureSetId,
                    $"ROM uses set {use.TextureSetId} · {CoverageText(coverage)} · area{(use.AreaIds.Count == 1 ? "" : "s")} {areas}");
            }

            var compatible = new List<(int id, ModelAssets.TextureCoverage coverage)>();
            for (int i = 0; uses.Count == 0 && i < sets; i++)
            {
                if (authoritative.Contains(i)) continue;
                var coverage = ModelAssets.Coverage(model, ModelAssets.TextureSet(a, i));
                if (coverage.HasMatches) compatible.Add((i, coverage));
            }
            foreach (var candidate in compatible
                .OrderByDescending(x => x.coverage.Complete)
                .ThenByDescending(x => x.coverage.MatchedTextures)
                .ThenBy(x => x.id))
                AddTextureChoice(candidate.id,
                    $"Likely match: set {candidate.id} · {CoverageText(candidate.coverage)}");

            // Clear the choice first, then set it. The list was just refilled, so the box has dropped back
            // to nothing selected, and going straight to the same number as before would say nothing.
            _textureChoice = -1;
            OnPropertyChanged(nameof(TextureChoice));
            _textureChoice = uses.Count > 0 && TextureChoices.Count > 1 ? 1 : 0;
            OnPropertyChanged(nameof(TextureChoice));
            OnPropertyChanged(nameof(HasTextureChoice));
        }

        private void AddTextureChoice(int setId, string label)
        {
            _textureSetEntries.Add(setId);
            TextureChoices.Add(label);
        }

        private static string CoverageText(ModelAssets.TextureCoverage coverage)
        {
            if (coverage.RequiredTextures == 0) return "model requests no textures";
            string textures = coverage.MatchedTextures == coverage.RequiredTextures
                ? $"all {coverage.RequiredTextures} texture{(coverage.RequiredTextures == 1 ? "" : "s")} found"
                : $"{coverage.MatchedTextures} of {coverage.RequiredTextures} textures found";
            if (coverage.RequiredPalettes == 0) return textures;
            string palettes = coverage.MatchedPalettes == coverage.RequiredPalettes
                ? $"all {coverage.RequiredPalettes} palette{(coverage.RequiredPalettes == 1 ? "" : "s")} found"
                : $"{coverage.MatchedPalettes} of {coverage.RequiredPalettes} palettes found";
            return textures + "; " + palettes;
        }

        // ── texture-bundle preview ───────────────────────────────────────────────────────────────

        public ObservableCollection<string> PreviewTextureNames { get; } = new();
        public ObservableCollection<string> PreviewPaletteNames { get; } = new();
        public bool HasPreviewTextureChoice => PreviewTextureNames.Count > 1;
        public bool HasPreviewPaletteChoice => PreviewPaletteNames.Count > 1;
        private List<NSBMDTexture> _previewTextures = new();
        private List<NSBMDPalette> _previewPalettes = new();
        private int _previewTextureIndex = -1, _previewPaletteIndex = -1;
        private bool _fillingTexturePreview;

        public int PreviewTextureIndex
        {
            get => _previewTextureIndex;
            set
            {
                int picked = PreviewTextureNames.Count == 0 ? -1 : Math.Max(0, value);
                if (!Set(ref _previewTextureIndex, picked) || _fillingTexturePreview) return;
                _fillingTexturePreview = true;
                _previewPaletteIndex = BestPaletteFor(picked);
                OnPropertyChanged(nameof(PreviewPaletteIndex));
                _fillingTexturePreview = false;
                RenderTexturePreview();
            }
        }

        public int PreviewPaletteIndex
        {
            get => _previewPaletteIndex;
            set
            {
                int picked = PreviewPaletteNames.Count == 0 ? -1 : Math.Max(0, value);
                if (Set(ref _previewPaletteIndex, picked) && !_fillingTexturePreview) RenderTexturePreview();
            }
        }

        private void LoadTexturePreview(ModelAssets.Archive archive, int index)
        {
            if (!ModelAssets.LoadTextureBundle(archive, index, out _previewTextures, out _previewPalettes))
            {
                Whynot = "This texture set would not open.";
                return;
            }

            _fillingTexturePreview = true;
            PreviewTextureNames.Clear();
            PreviewPaletteNames.Clear();
            foreach (var texture in _previewTextures)
                PreviewTextureNames.Add(string.IsNullOrWhiteSpace(texture.texname)
                    ? $"Texture {PreviewTextureNames.Count}" : texture.texname);
            foreach (var palette in _previewPalettes)
                PreviewPaletteNames.Add(string.IsNullOrWhiteSpace(palette.palname)
                    ? $"Palette {PreviewPaletteNames.Count}" : palette.palname);
            _previewTextureIndex = _previewTextures.Count > 0 ? 0 : -1;
            _previewPaletteIndex = BestPaletteFor(_previewTextureIndex);
            _fillingTexturePreview = false;
            OnPropertyChanged(nameof(PreviewTextureIndex));
            OnPropertyChanged(nameof(PreviewPaletteIndex));
            OnPropertyChanged(nameof(HasPreviewTextureChoice));
            OnPropertyChanged(nameof(HasPreviewPaletteChoice));
            RenderTexturePreview();
        }

        private int BestPaletteFor(int textureIndex)
        {
            string texture = textureIndex >= 0 && textureIndex < _previewTextures.Count
                ? _previewTextures[textureIndex].texname : "";
            return ModelTexturePairing.BestPaletteIndex(_previewPalettes, texture);
        }

        private void RenderTexturePreview()
        {
            TexturePreview = null;
            if (_previewTextureIndex < 0 || _previewTextureIndex >= _previewTextures.Count)
            {
                Whynot = "This texture set contains no picture to show.";
                OnPropertyChanged(nameof(HasNoModel));
                return;
            }
            var texture = _previewTextures[_previewTextureIndex];
            RGBA[] palette = _previewPaletteIndex >= 0 && _previewPaletteIndex < _previewPalettes.Count
                ? _previewPalettes[_previewPaletteIndex].paldata : null;
            var decoded = NsbmdTextureDecoder.Decode(new NSBMDMaterial
            {
                format = texture.format,
                width = texture.width,
                height = texture.height,
                texdata = texture.texdata,
                spdata = texture.spdata,
                color0 = texture.color0,
                paldata = palette,
            });
            if (decoded == null)
            {
                Whynot = texture.format == 7 || palette != null
                    ? "This texture is malformed or uses data the preview cannot decode."
                    : "This texture needs a palette, but this set does not contain one.";
                Details = BaseDetails;
                OnPropertyChanged(nameof(HasNoModel));
                return;
            }
            Whynot = "";
            TexturePreview = RgbaToBitmap(decoded.Rgba, decoded.Width, decoded.Height);
            Details = BaseDetails + $"  {PreviewTextureNames[_previewTextureIndex]}, {decoded.Width} × {decoded.Height} pixels. "
                + $"This set contains {_previewTextures.Count} texture{(_previewTextures.Count == 1 ? "" : "s")} "
                + $"and {_previewPalettes.Count} palette{(_previewPalettes.Count == 1 ? "" : "s")}.";
        }

        private static Bitmap RgbaToBitmap(byte[] rgba, int width, int height)
        {
            var bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96),
                PixelFormat.Rgba8888, AlphaFormat.Unpremul);
            using var buffer = bitmap.Lock();
            int sourceStride = width * 4;
            if (buffer.RowBytes == sourceStride)
                Marshal.Copy(rgba, 0, buffer.Address, Math.Min(rgba.Length, buffer.RowBytes * height));
            else
                for (int y = 0; y < height; y++)
                    Marshal.Copy(rgba, y * sourceStride, IntPtr.Add(buffer.Address, y * buffer.RowBytes), sourceStride);
            return bitmap;
        }

        private void ClearTexturePreview()
        {
            TexturePreview = null;
            _previewTextures.Clear();
            _previewPalettes.Clear();
            PreviewTextureNames.Clear();
            PreviewPaletteNames.Clear();
            _previewTextureIndex = _previewPaletteIndex = -1;
            OnPropertyChanged(nameof(PreviewTextureIndex));
            OnPropertyChanged(nameof(PreviewPaletteIndex));
            OnPropertyChanged(nameof(HasTexturePreview));
            OnPropertyChanged(nameof(HasPreviewTextureChoice));
            OnPropertyChanged(nameof(HasPreviewPaletteChoice));
        }

        // ── movement ──────────────────────────────────────────────────────────────────────────────

        public ObservableCollection<string> AnimationChoices { get; } = new();
        public bool HasAnimationChoice => AnimationChoices.Count > 1;

        private readonly List<int> _animationEntries = new();   // what each row above points at

        /// <summary>How many rows got there by name rather than by the game's own table.</summary>
        private int _namedForThisModel;

        /// <summary>Where the movement now showing came from.</summary>
        private enum Belonging { Table, Name, Elsewhere }

        private Belonging WhoseMovement()
        {
            if (_selected == null || _animationIndex <= 0 || _animationIndex >= _animationEntries.Count)
                return Belonging.Elsewhere;
            if (ModelAssets.OwnAnimations(_selected.Archive, ShowingIndex)
                           .Contains(_animationEntries[_animationIndex])) return Belonging.Table;
            return AnimationChoices[_animationIndex].StartsWith(NamedPrefix)
                ? Belonging.Name : Belonging.Elsewhere;
        }

        private const string NamedPrefix = "Named for this model";

        /// <summary>What a movement is called, from the name inside the file.</summary>
        private static string Called(DSPRE.ROMFiles.JointAnimation anim, int number)
        {
            string name = anim?.Name;
            if (string.IsNullOrWhiteSpace(name)) return $"Movement {number}";
            return $"{name} (movement {number})";
        }

        private void BuildAnimationChoices(ModelAssets.Archive a)
        {
            AnimationChoices.Clear();
            _animationEntries.Clear();
            Playing = false;
            MovementNote = "";
            OnPropertyChanged(nameof(HasMovementNote));

            AnimationChoices.Add("Still");
            _animationEntries.Add(int.MinValue);

            if (ModelAssets.AnimationFor(a, ShowingIndex, -1) != null)
            {
                AnimationChoices.Add("Its own movement");
                _animationEntries.Add(-1);
            }

            // The game's own table says which movements belong to this model, so those go first and one of
            // them is what the box starts on.
            var own = ModelAssets.OwnAnimations(a, ShowingIndex);
            int startOn = 0;
            foreach (int code in own)
            {
                var anim = ModelAssets.AnimationFor(a, ShowingIndex, code);
                if (anim == null) continue;
                if (startOn == 0) startOn = AnimationChoices.Count;
                AnimationChoices.Add($"The one it uses: {Called(anim, code)}, {anim.FrameCount} frames");
                _animationEntries.Add(code);
            }

            // Where the table says nothing, a movement's own name often still says which model it is for.
            // Those come next, best match first, and one of them is where the box starts when the table
            // gave nothing. Everything else follows, so there is always the whole list to pick from.
            string modelName = ModelAssets.NameOf(a, ShowingIndex);
            var named = new List<(int code, int howWell, JointAnimation anim)>();
            var rest = new List<(int code, JointAnimation anim)>();

            int n = ModelAssets.AnimationCount(a);
            for (int i = 0; i < n; i++)
            {
                if (own.Contains(i)) continue;
                var anim = ModelAssets.AnimationFor(a, ShowingIndex, i);
                if (anim == null) continue;
                int howWell = ModelAssets.NameMatch(modelName, anim.Name);
                if (howWell > 0) named.Add((i, howWell, anim));
                else rest.Add((i, anim));
            }

            _namedForThisModel = named.Count;
            foreach (var (code, howWell, anim) in named.OrderByDescending(x => x.howWell).ThenBy(x => x.code))
            {
                if (startOn == 0) startOn = AnimationChoices.Count;
                AnimationChoices.Add($"{NamedPrefix}: {Called(anim, code)}, {anim.FrameCount} frames");
                _animationEntries.Add(code);
            }
            foreach (var (code, anim) in rest)
            {
                AnimationChoices.Add($"{Called(anim, code)}, {anim.FrameCount} frames");
                _animationEntries.Add(code);
            }

            _animationIndex = -1;
            OnPropertyChanged(nameof(AnimationIndex));
            _movementJustPicked = true;
            _animationIndex = startOn;
            OnPropertyChanged(nameof(AnimationIndex));
            OnPropertyChanged(nameof(HasAnimationChoice));
            OnPropertyChanged(nameof(CanPlay));
            OnPropertyChanged(nameof(PlayHelp));
            OnPropertyChanged(nameof(MovementSource));
            OnPropertyChanged(nameof(HasMovementSource));
        }

        private int _animationIndex;
        public int AnimationIndex
        {
            get => _animationIndex;
            set
            {
                if (!Set(ref _animationIndex, value) || _selected == null) return;
                _frame = 0;
                _movementJustPicked = true;
                if (!CanPlay) Playing = false;
                Draw();
                RaiseAll();
                if (Model3D != null) ModelReady?.Invoke(this, EventArgs.Empty);
            }
        }

        private bool _movementJustPicked;

        /// <summary>The still model, kept so every frame of a movement is placed the same way it was.</summary>
        private NsbmdRenderModel _placeLike;

        private JointAnimation Chosen()
        {
            if (_selected == null) return null;
            if (_animationIndex <= 0 || _animationIndex >= _animationEntries.Count) return null;
            return ModelAssets.AnimationFor(_selected.Archive, _selected.Index, _animationEntries[_animationIndex]);
        }

        /// <summary>A movement to run, or any of the other animations, which run on the same clock.</summary>
        public bool CanPlay => (_animationIndex > 0 && _animationIndex < _animationEntries.Count)
                               || CompanionFrames > 1;

        public string PlayHelp => CanPlay
            ? "Run it. Buildings mostly move on a clock rather than on their own, so this is a "
            + "way to see what an animation does, not what the game does with it."
            : "Pick a movement or one of the other animations first. Still means nothing is moving.";

        /// <summary>Says whether the movement showing is the one the game gives this model or one that
        /// belongs to something else and is only being tried on it.</summary>
        public string MovementSource
        {
            get
            {
                if (_selected == null || _animationIndex <= 0
                    || _animationIndex >= _animationEntries.Count) return "";
                switch (WhoseMovement())
                {
                    case Belonging.Table: return "The movement the game gives this model.";
                    case Belonging.Name:
                        return "Named after this model, so very likely its own.";
                    default:
                        var own = ModelAssets.OwnAnimations(_selected.Archive, ShowingIndex);
                        if (own.Count == 0 && _namedForThisModel == 0) return "";
                        return "Borrowed from something else.";
                }
            }
        }

        public bool HasMovementSource => !string.IsNullOrEmpty(MovementSource);

        private bool _playing;
        public bool Playing
        {
            get => _playing;
            set
            {
                if (!Set(ref _playing, value)) return;
                OnPropertyChanged(nameof(PlayLabel));
                PlayingChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>Raised when the view should start or stop stepping the frames.</summary>
        public event EventHandler PlayingChanged;

        public string PlayLabel => _playing ? "Stop" : "Play";

        private int _frame;

        private string _movementNote = "";
        /// <summary>Says when a movement leaves this model exactly where it was. </summary>
        public string MovementNote { get => _movementNote; private set => Set(ref _movementNote, value); }
        public bool HasMovementNote => !string.IsNullOrEmpty(_movementNote);

        /// <summary>Whether a movement changes this model at all, checked once when it is picked rather
        /// than on every frame.</summary>
        private void CheckTheMovementDoesSomething(NSBMD nsbmd, JointAnimation anim)
        {
            MovementNote = "";
            OnPropertyChanged(nameof(HasMovementNote));
            if (anim == null || nsbmd?.models == null || nsbmd.models.Length == 0) return;

            try
            {
                var model = nsbmd.models[0];
                float scale = model.modelScale;
                var still = NsbmdGeometry.BuildModel(model);
                bool differs = false;
                for (int f = 1; f < anim.FrameCount && !differs; f++)
                {
                    var at = NsbmdGeometry.BuildModel(model,
                        (objectId, part) => anim.MatrixFor(objectId, f, part, scale), still);
                    differs = Differs(still, at);
                }
                // Whose movement it is decides why nothing moved, and saying the wrong one of these is
                // worse than saying neither.
                if (!differs)
                    MovementNote = WhoseMovement() switch
                    {
                        Belonging.Table => "The game gives this model this movement, but it moves nothing "
                                         + "on it.",
                        Belonging.Name => "Named after this model, but it moves nothing on it.",
                        _ => "Moves nothing on this model. It was written for something else.",
                    };
            }
            catch (Exception ex) { AppLogger.Error("ModelBrowser.CheckTheMovement failed: " + ex.Message); }
            OnPropertyChanged(nameof(HasMovementNote));
        }

        private static bool Differs(NsbmdRenderModel a, NsbmdRenderModel b)
        {
            if (a?.Parts == null || b?.Parts == null || a.Parts.Count != b.Parts.Count) return true;
            for (int m = 0; m < a.Parts.Count; m++)
            {
                var va = a.Parts[m].Vertices; var vb = b.Parts[m].Vertices;
                if (va == null || vb == null || va.Length != vb.Length) return true;
                for (int i = 0; i < va.Length; i++)
                    if (Math.Abs(va[i] - vb[i]) > 1e-6f) return true;
            }
            return false;
        }

        /// <summary>Moves on one frame and redraws. Called by the view on a timer.</summary>
        public void Step()
        {
            var anim = Chosen();
            int length = Math.Max(anim?.FrameCount ?? 0, CompanionFrames);
            if (length <= 0) { Playing = false; return; }
            _frame = (_frame + 1) % length;
            OnPropertyChanged(nameof(Frame));
            DrawNow();
        }

        /// <summary>Draws at the frame it is already on, and tells the view.</summary>
        private void DrawNow()
        {
            Draw();
            OnPropertyChanged(nameof(FrameSummary));
            OnPropertyChanged(nameof(FrameCount));
            if (Model3D != null) ModelReady?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>How many frames the thing showing runs for, so the slider has an end.</summary>
        public int FrameCount
        {
            get
            {
                var anim = Chosen();
                return Math.Max(1, Math.Max(anim?.FrameCount ?? 0, CompanionFrames));
            }
        }

        /// <summary>The frame showing, which can be dragged to rather than only played through.</summary>
        public int Frame
        {
            get => _frame;
            set
            {
                int at = Math.Clamp(value, 0, Math.Max(0, FrameCount - 1));
                if (_frame == at) return;
                _frame = at;
                OnPropertyChanged(nameof(Frame));
                DrawNow();
            }
        }

        /// <summary>Which frame is showing, for the panel beside the view.</summary>
        public string FrameSummary
        {
            get
            {
                var anim = Chosen();
                int length = Math.Max(anim?.FrameCount ?? 0, CompanionFrames);
                return length <= 1 ? "" : $"Frame {_frame + 1} of {length}";
            }
        }

        private void Draw()
        {
            var a = _selected.Archive;
            Model3D = null;
            Whynot = "";
            try
            {
                var nsbmd = ModelAssets.LoadModel(a, ShowingIndex);
                if (nsbmd == null || nsbmd.models == null || nsbmd.models.Length == 0)
                {
                    Whynot = "This model would not open.";
                    return;
                }

                int textureSet = _textureChoice >= 0 && _textureChoice < _textureSetEntries.Count
                    ? _textureSetEntries[_textureChoice] : -1;
                var textures = ModelAssets.TexturesFor(a, ShowingIndex, textureSet);
                bool dressed = ModelAssets.Dress(nsbmd, textures);

                var anim = Chosen();
                if (_movementJustPicked)
                {
                    _movementJustPicked = false;
                    _placeLike = NsbmdGeometry.BuildModel(nsbmd.models[0]);
                    CheckTheMovementDoesSomething(nsbmd, anim);
                }
                float scale = nsbmd.models[0].modelScale;
                Model3D = anim == null
                    ? NsbmdGeometry.BuildModel(nsbmd.models[0])
                    : NsbmdGeometry.BuildModel(nsbmd.models[0],
                        (objectId, part) => anim.MatrixFor(objectId, _frame % Math.Max(1, anim.FrameCount), part, scale),
                        _placeLike);
                if (Model3D == null) { Whynot = "This model would not open."; return; }

                if (!dressed && TextureChoices.Count > 1)
                    Details = BaseDetails + "  Shown bare. Pick a picture set above to dress it.";
                else if (!dressed)
                    Details = BaseDetails + "  This model has no pictures of its own, and this game keeps "
                            + "none for it to borrow.";
                else Details = BaseDetails;
            }
            catch (Exception ex)
            {
                AppLogger.Error("ModelBrowser.Draw failed: " + ex.Message);
                Whynot = "This model would not open.";
            }

            ApplyCompanions(_frame);
        }


        private string BaseDetails = "";

        /// <summary>What an animation picked on its own does, and which model it was written for.</summary>
        public string AnimationDetails { get; private set; } = "";
        public string AnimationOwner { get; private set; } = "";
        public bool HasAnimationDetails => !string.IsNullOrEmpty(AnimationDetails);

        private static bool IsAnAnimation(ModelAssets.Kind k) =>
            k is ModelAssets.Kind.JointAnimation or ModelAssets.Kind.TextureAnimation
              or ModelAssets.Kind.TextureSwap or ModelAssets.Kind.VisibilityAnimation
              or ModelAssets.Kind.MaterialAnimation;

        /// <summary>What to call the thing on screen: the name in the file when it has one.</summary>
        private string Named => _selected?.Name ?? _selected?.Archive.Title ?? "";

        private void RaiseAll()
        {
            OnPropertyChanged(nameof(HasModel));
            OnPropertyChanged(nameof(HasNoModel));
            OnPropertyChanged(nameof(HasTexturePreview));
            _cannotImport = _selected == null
                ? "Pick something first."
                : ModelAssets.CannotImportBecause(_selected.Archive, ShowingIndex);

            OnPropertyChanged(nameof(CanSaveModel));
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(SaveModelHelp));
            OnPropertyChanged(nameof(CanPutFileIn));
            OnPropertyChanged(nameof(PutFileInHelp));
            OnPropertyChanged(nameof(DeepEditorHint));
            OnPropertyChanged(nameof(HasDeepEditor));
            OnPropertyChanged(nameof(CanPlay));
            OnPropertyChanged(nameof(PlayHelp));
            OnPropertyChanged(nameof(MovementSource));
            OnPropertyChanged(nameof(HasMovementSource));
        }

        /// <summary>Whether anything is picked. Saving a file as it is works on whatever is
        /// selected, so with nothing selected the button has nothing to act on.</summary>
        public bool HasSelection => _selected != null;

        public bool CanSaveModel => _options?.CanSaveModel == true;

        public string SaveModelHelp => _selected == null
            ? "Pick something first."
            : _options?.SaveNote ?? "There is nothing here to save as a 3D file.";

        public string DeepEditorName => _selected?.Archive.DeepEditor;
        public bool HasDeepEditor => !string.IsNullOrEmpty(DeepEditorName);
        public string DeepEditorHint => HasDeepEditor ? $"The {DeepEditorName} does more with these." : "";

        private string _status = "";
        public string Status { get => _status; set => Set(ref _status, value); }

        public string SuggestedFileName(string extension)
        {
            if (_selected == null) return "model" + extension;
            string name = _selected.Archive.Title.ToLowerInvariant().Replace(' ', '_');
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return $"{name}_{ShowingIndex:D4}{extension}";
        }

        /// <summary>Whether a file can be put in over what is picked, and why not when it cannot.</summary>
        public bool CanPutFileIn => _selected != null && _cannotImport == null;

        public string PutFileInHelp => _selected == null
            ? "Pick something first."
            : _cannotImport
              ?? ("Put a file in place of this one: a finished NSBMD, NSBTX or animation file, or an OBJ "
                  + "mesh, which is turned into a model as it goes in. Anything already in a Nitro format "
                  + "has to be the same kind as what is here now.\n\n" + ModelAssets.CanConvertAMesh);

        private string _cannotImport = "Pick something first.";

        /// <summary>Puts a file in over the entry showing. Comes back with a reason when it could not.</summary>
        public string PutFileIn(string path) => PutFileIn(path, out _);

        /// <param name="note">What a mesh came to once it was turned into a model, when it was one.</param>
        public string PutFileIn(string path, out string note)
        {
            note = null;
            if (_selected == null) return "Pick something first.";
            if (_cannotImport != null) return _cannotImport;
            if (string.Equals(Path.GetExtension(path), ".obj", StringComparison.OrdinalIgnoreCase))
                return ModelAssets.ImportMesh(_selected.Archive, ShowingIndex, path, out note);
            return ModelAssets.ImportRaw(_selected.Archive, ShowingIndex, path);
        }

        /// <summary>Saves the entry exactly as it sits in the ROM.</summary>
        public string SaveFileAsItIs(string path)
        {
            if (_selected == null) return "Pick something first.";
            return ModelAssets.SaveRaw(_selected.Archive, ShowingIndex, path);
        }

        /// <summary>Saves the model as a file other 3D programs open, with its pictures.</summary>
        public string SaveAsThreeD(string path, bool glb)
        {
            if (_selected == null) return "Pick something first.";
            if (!CanSaveModel) return _options?.SaveNote ?? "This is not a model.";

            var narc = new ScriptNarc(_selected.Archive.Dir);
            var model = narc.Get(ShowingIndex);
            int textureSet = _textureChoice >= 0 && _textureChoice < _textureSetEntries.Count
                ? _textureSetEntries[_textureChoice] : -1;
            var textures = ModelAssets.TexturesFor(_selected.Archive, ShowingIndex, textureSet);

            try
            {
                string name = Path.GetFileNameWithoutExtension(path);
                string dir = Path.GetDirectoryName(path);
                string was = Directory.GetCurrentDirectory();
                try
                {
                    if (!string.IsNullOrEmpty(dir)) Directory.SetCurrentDirectory(dir);
                    if (glb) DSPRE.ModelUtils.ModelToGLB(name, model, textures);
                    else DSPRE.ModelUtils.ModelToDAE(name, model, textures);
                }
                finally { Directory.SetCurrentDirectory(was); }
                return null;
            }
            catch (Exception ex)
            {
                AppLogger.Error("ModelBrowser.SaveAsThreeD failed: " + ex.Message);
                return "This model could not be turned into a 3D file. The whole file can still be saved "
                     + "as it is.";
            }
        }
    }
}
