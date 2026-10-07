using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using DSPRE.Avalonia.Data;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    /// <summary>One layer of the page in the list: a background, a sprite, a label or the sample Pokémon.</summary>
    public sealed class DexLayerRow
    {
        public string Name { get; init; }
        public string Where { get; init; }
        public DexSide Side { get; init; }
        public DexLayerKind Kind { get; init; }
        /// <summary>The background's number, or the item's place in its list (negative for a label).</summary>
        public int Index { get; init; }
    }

    /// <summary>One file a layer is made from, which can be painted, exported or replaced.</summary>
    public sealed class DexPartRow
    {
        public string Name { get; init; }
        public string Where { get; init; }
        public DirNames Dir { get; init; }
        public int Member { get; init; }
    }

    /// <summary>
    /// Every Pokédex page of the loaded game, put together from its own files with a sample Pokémon, so each
    /// background, sprite and label can be picked and its drawing, arrangement or colours edited in place.
    /// </summary>
    public sealed class PokedexGraphicsEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private readonly IReadOnlyList<DexPage> _pages;
        private readonly DexSample _sample = new();
        private readonly Dictionary<int, FieldFont> _fonts = new();
        private DexPageComposer _composer;
        private DexComposed _top, _bottom;

        public ObservableCollection<string> PageNames { get; } = new();
        public ObservableCollection<string> VariantNames { get; } = new();
        public ObservableCollection<DexLayerRow> Layers { get; } = new();
        public ObservableCollection<DexPartRow> Parts { get; } = new();

        public PokedexGraphicsEditorViewModel()
        {
            _pages = DexPageRecipes.For(gameFamily);
            foreach (DexPage p in _pages) PageNames.Add(p.Name);
            BuildComposer();
            if (_pages.Count > 0) PageIndex = 0;
            else StatusText = "This game has no Pokédex pages to show.";
        }

        private void BuildComposer()
        {
            ScriptNarc dex = null;
            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.pokedexGraphics, DirNames.footprintGraphics,
                                                            DirNames.pokemonBattleSprites, DirNames.monIcons,
                                                            DirNames.pokedexAreas, DirNames.pokedexSearchSteps });
                dex = new ScriptNarc(DirNames.pokedexGraphics);
            }
            catch (Exception ex) { AppLogger.Error("Pokédex graphics: " + ex.Message); }
            ScriptNarc archive = dex;
            _composer = new DexPageComposer(i => archive?.Get(i), Font, _sample.Text, _sample.Picture, _sample.TypeSlot,
                                            i => ScreenGraphicsLayouts.BankFor(DirNames.pokedexGraphics, i))
            {
                Habitat = _sample.Habitat,
                Size = _sample.SizeInfo,
                Digit = _sample.SearchDigit,
            };
        }

        private FieldFont Font(int entry)
        {
            if (_fonts.TryGetValue(entry, out FieldFont known)) return known;
            FieldFont font = null;
            try { font = FieldFont.LoadFromArchive(entry); } catch { }
            _fonts[entry] = font;
            return font;
        }

        // ── page and state ─────────────────────────────────────────────────────────────────────────

        private int _pageIndex = -1;
        public int PageIndex
        {
            get => _pageIndex;
            set
            {
                if (value < 0 || value >= _pages.Count || value == _pageIndex) return;
                _pageIndex = value;
                OnPropertyChanged();
                VariantNames.Clear();
                foreach (string v in Page.Variants) VariantNames.Add(v);
                OnPropertyChanged(nameof(HasVariants));
                _variantIndex = -1;
                VariantIndex = 0;
                if (VariantNames.Count == 0) Compose();
                // The State box clears its own selection when its items refill; say the state again once it has.
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (_variantIndex < 0) return;
                    int keep = _variantIndex;
                    _variantIndex = -1;
                    OnPropertyChanged(nameof(VariantIndex));
                    _variantIndex = keep;
                    OnPropertyChanged(nameof(VariantIndex));
                }, global::Avalonia.Threading.DispatcherPriority.Background);
            }
        }

        private DexPage Page => _pageIndex >= 0 && _pageIndex < _pages.Count ? _pages[_pageIndex] : null;

        private int _variantIndex = -1;
        public int VariantIndex
        {
            get => _variantIndex;
            set
            {
                if (value < 0 || value >= VariantNames.Count || value == _variantIndex) return;
                _variantIndex = value;
                OnPropertyChanged();
                Compose();
            }
        }

        public bool HasVariants => VariantNames.Count > 1;
        private string Variant => _variantIndex >= 0 && _variantIndex < VariantNames.Count ? VariantNames[_variantIndex] : "";

        // ── the two screens ────────────────────────────────────────────────────────────────────────

        private Bitmap _topImage, _bottomImage;
        public Bitmap TopImage { get => _topImage; private set => Set(ref _topImage, value); }
        public Bitmap BottomImage { get => _bottomImage; private set => Set(ref _bottomImage, value); }

        private void Compose()
        {
            DexPage page = Page;
            if (page == null || _composer == null) return;
            try
            {
                DexSide topSide = page.MainOnTop ? DexSide.Main : DexSide.Sub;
                DexSide bottomSide = page.MainOnTop ? DexSide.Sub : DexSide.Main;
                _top = _composer.Compose(page, Variant, topSide);
                _bottom = _composer.Compose(page, Variant, bottomSide);
                int keep = _layerIndex;
                Layers.Clear();
                AddLayers(_top, topSide, "Top");
                AddLayers(_bottom, bottomSide, "Bottom");
                _layerIndex = -1;
                LayerIndex = keep >= 0 && keep < Layers.Count ? keep : -1;
                Redraw();
            }
            catch (Exception ex)
            {
                AppLogger.Error("Pokédex page compose: " + ex.Message);
                StatusText = "This page could not be put together. " + ex.Message;
            }
        }

        private void AddLayers(DexComposed composed, DexSide side, string where)
        {
            // Front first, the way a person looks at a screen.
            for (int i = composed.Layers.Count - 1; i >= 0; i--)
            {
                DexLayerImage l = composed.Layers[i];
                Layers.Add(new DexLayerRow { Name = l.Name, Where = where, Side = side, Kind = l.Kind, Index = l.Index });
            }
        }

        private void Redraw()
        {
            DexLayerRow picked = SelectedLayer;
            TopImage = Show(_top, picked, "Top");
            BottomImage = Show(_bottom, picked, "Bottom");
        }

        // The picked layer at full strength over everything else dimmed, the other screen included, so it is plain
        // which pixels it owns.
        private Bitmap Show(DexComposed composed, DexLayerRow picked, string where)
        {
            if (composed == null) return null;
            byte[] rgba = (byte[])composed.Rgba.Clone();
            // Both screens number their backgrounds alike, so the picked layer belongs to one screen only.
            DexLayerImage own = picked == null || picked.Where != where ? null : composed.Layers.FirstOrDefault(
                l => l.Kind == picked.Kind && l.Index == picked.Index && l.Name == picked.Name);
            if (picked != null)
            {
                for (int at = 0; at < rgba.Length; at += 4)
                {
                    if (own != null && own.Rgba[at + 3] != 0)
                    {
                        rgba[at] = own.Rgba[at];
                        rgba[at + 1] = own.Rgba[at + 1];
                        rgba[at + 2] = own.Rgba[at + 2];
                    }
                    else
                    {
                        rgba[at] = (byte)(rgba[at] / 3);
                        rgba[at + 1] = (byte)(rgba[at + 1] / 3);
                        rgba[at + 2] = (byte)(rgba[at + 2] / 3);
                    }
                }
            }
            return ToBitmap(rgba, DexPageComposer.Width, DexPageComposer.Height);
        }

        /// <summary>Picks the front layer that draws the pixel x, y of the top or bottom screen.</summary>
        public void PickAt(bool top, int x, int y)
        {
            DexComposed composed = top ? _top : _bottom;
            if (composed == null || x < 0 || y < 0 || x >= DexPageComposer.Width || y >= DexPageComposer.Height) return;
            int at = (y * DexPageComposer.Width + x) * 4 + 3;
            for (int i = composed.Layers.Count - 1; i >= 0; i--)
            {
                DexLayerImage l = composed.Layers[i];
                if (l.Rgba[at] == 0) continue;
                string where = top ? "Top" : "Bottom";
                int row = -1;
                for (int k = 0; k < Layers.Count; k++)
                    if (Layers[k].Where == where && Layers[k].Kind == l.Kind && Layers[k].Index == l.Index && Layers[k].Name == l.Name)
                    {
                        row = k;
                        break;
                    }
                LayerIndex = row;
                return;
            }
        }

        // ── the picked layer and its files ─────────────────────────────────────────────────────────

        private int _layerIndex = -1;
        public int LayerIndex
        {
            get => _layerIndex;
            set
            {
                if (value == _layerIndex) return;
                _layerIndex = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedLayer));
                OnPropertyChanged(nameof(LayerName));
                FillParts();
                Redraw();
            }
        }

        public DexLayerRow SelectedLayer => _layerIndex >= 0 && _layerIndex < Layers.Count ? Layers[_layerIndex] : null;
        public string LayerName => SelectedLayer == null ? "" : SelectedLayer.Where + ": " + SelectedLayer.Name;

        private int _partIndex = -1;
        public int PartIndex
        {
            get => _partIndex;
            set
            {
                if (value == _partIndex) return;
                _partIndex = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedPart));
                OnPropertyChanged(nameof(CanEditPart));
            }
        }

        public DexPartRow SelectedPart => _partIndex >= 0 && _partIndex < Parts.Count ? Parts[_partIndex] : null;
        public bool CanEditPart => SelectedPart != null;

        private void FillParts()
        {
            Parts.Clear();
            DexLayerRow layer = SelectedLayer;
            DexPage page = Page;
            if (layer != null && page != null)
                foreach (DexPartRow part in PartsOf(page.Side(layer.Side), layer)) Parts.Add(part);
            _partIndex = -1;
            PartIndex = Parts.Count > 0 ? 0 : -1;
        }

        private IEnumerable<DexPartRow> PartsOf(DexScreen screen, DexLayerRow layer)
        {
            const DirNames dex = DirNames.pokedexGraphics;
            HashSet<int> seen = new();
            DexPartRow Dex(string name, int member) =>
                member >= 0 && seen.Add(member) ? new DexPartRow { Name = name, Where = "File " + member, Dir = dex, Member = member } : null;

            List<DexPartRow> parts = new();
            void Add(DexPartRow row) { if (row != null) parts.Add(row); }
            switch (layer.Kind)
            {
                case DexLayerKind.Background:
                    DexBg bg = screen.Bgs.FirstOrDefault(b => b.Bg == layer.Index && DexPage.Shows(b.When, Variant));
                    if (bg == null) break;
                    Add(Dex("Drawing", bg.Drawing));
                    foreach (DexPiece piece in bg.Pieces.Where(p => p.Screen >= 0 && DexPage.Shows(p.When, Variant)))
                        Add(Dex("Arrangement", piece.Screen));
                    foreach (DexPalette load in screen.Palettes.Where(p => DexPage.Shows(p.When, Variant)))
                        Add(Dex("Colours", load.File));
                    break;
                case DexLayerKind.Sprite when layer.Index >= 0 && layer.Index < screen.Sprites.Count:
                    DexSprite s = screen.Sprites[layer.Index];
                    int drawing = s.TypeSlot > 0 && s.TypeDrawing >= 0 ? s.TypeDrawing + Math.Max(0, _sample.TypeSlot(s.TypeSlot)) : s.Drawing;
                    Add(Dex("Drawing", drawing));
                    Add(Dex("Cell layout", s.Cells));
                    Add(Dex("Animation", s.Anim));
                    Add(Dex("Colours", s.Colours));
                    break;
                case DexLayerKind.Sprite:
                    int t = -1 - layer.Index;
                    if (t >= 0 && t < screen.Texts.Count) Add(Dex("Colours", screen.Texts[t].Colours));
                    break;
                case DexLayerKind.Pokemon when layer.Index >= 0 && layer.Index < screen.Mons.Count:
                    DexMon m = screen.Mons[layer.Index];
                    int species = _sample.Species + m.Offset;
                    switch (m.Kind)
                    {
                        case DexMonKind.Front:
                        case DexMonKind.Back:
                            int first = species * 6;
                            int face = m.Kind == DexMonKind.Front ? first + 3 : first + 1;
                            parts.Add(new DexPartRow { Name = m.Kind == DexMonKind.Front ? "Front" : "Back", Where = "Battle sprites " + face,
                                                       Dir = DirNames.pokemonBattleSprites, Member = face });
                            parts.Add(new DexPartRow { Name = "Colours", Where = "Battle sprites " + (first + 4),
                                                       Dir = DirNames.pokemonBattleSprites, Member = first + 4 });
                            break;
                        case DexMonKind.Footprint:
                            int foot = GraphicAssets.FootprintEntry(species);
                            if (foot >= 0)
                                parts.Add(new DexPartRow { Name = "Footprint", Where = "Footprints " + foot, Dir = DirNames.footprintGraphics, Member = foot });
                            break;
                        case DexMonKind.Icon:
                            parts.Add(new DexPartRow { Name = "Icon", Where = "Party icons " + (species + 7), Dir = DirNames.monIcons, Member = species + 7 });
                            break;
                    }
                    break;
            }
            return parts;
        }

        /// <summary>The browser's entry for a file's archive, which the painter, export and import work through.</summary>
        public static GraphicAssets.Archive ArchiveOf(DexPartRow part) =>
            part == null ? null : GraphicAssets.All.FirstOrDefault(a => a.Dir == part.Dir);

        /// <summary>Reads every file again after one has been painted or replaced.</summary>
        public void Reload()
        {
            DropLastStepIfUnchanged();
            _composer?.Forget();
            _sample.Forget();
            try { GraphicAssets.Forget(); } catch { }
            Compose();
        }

        // ── changes ────────────────────────────────────────────────────────────────────────────────

        private sealed record Step(DirNames Dir, int Member, byte[] Bytes, string What);

        private readonly Stack<Step> _undo = new(), _redo = new();
        // Edits hit the unpacked files at once, so each file's prior bytes are kept for Discard to write back.
        private readonly Dictionary<(DirNames Dir, int Member), byte[]> _originals = new();

        public bool HasUnsavedChanges => _originals.Count > 0;
        public string UnsavedChangesDescription => "Pokédex graphics";

        public void SaveChanges()
        {
            if (_originals.Count == 0) return;
            _originals.Clear();
            RaiseSteps();
            SaveNotice.Saved(UnsavedChangesDescription);
        }

        public void DiscardChanges()
        {
            foreach (KeyValuePair<(DirNames Dir, int Member), byte[]> kv in _originals)
            {
                try { new ScriptNarc(kv.Key.Dir).Put(kv.Key.Member, kv.Value); }
                catch (Exception ex) { AppLogger.Error("Pokédex graphics discard: " + ex.Message); }
            }
            _originals.Clear();
            _undo.Clear();
            _redo.Clear();
            Reload();
            RaiseSteps();
        }

        /// <summary>Keeps a copy of a file before it is written to, including by the painter.</summary>
        public void Remember(DexPartRow part, string what)
        {
            if (part == null || part.Member < 0) return;
            try
            {
                byte[] before = new ScriptNarc(part.Dir).Get(part.Member);
                if (before == null) return;
                _originals.TryAdd((part.Dir, part.Member), (byte[])before.Clone());
                _undo.Push(new Step(part.Dir, part.Member, before, what));
                _redo.Clear();
                RaiseSteps();
            }
            catch (Exception ex) { AppLogger.Error("Pokédex graphics remember: " + ex.Message); }
        }

        /// <summary>Drops the last remembered step when its file was never written, so nothing shows as changed.</summary>
        public void DropLastStepIfUnchanged()
        {
            if (_undo.Count == 0) return;
            Step step = _undo.Peek();
            byte[] now;
            try { now = new ScriptNarc(step.Dir).Get(step.Member); }
            catch (Exception ex) { AppLogger.Error("Pokédex graphics drop: " + ex.Message); return; }
            if (now == null || !now.AsSpan().SequenceEqual(step.Bytes)) return;
            _undo.Pop();
            if (!_undo.Any(s => s.Dir == step.Dir && s.Member == step.Member))
                _originals.Remove((step.Dir, step.Member));
            RaiseSteps();
        }

        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;
        public string UndoWhat => _undo.Count == 0 ? "Nothing to undo" : "Undo " + _undo.Peek().What;
        public string RedoWhat => _redo.Count == 0 ? "Nothing to redo" : "Redo " + _redo.Peek().What;

        public void Undo() => StepAcross(_undo, _redo, "Put back");
        public void Redo() => StepAcross(_redo, _undo, "Done again");

        private void StepAcross(Stack<Step> from, Stack<Step> to, string said)
        {
            if (from.Count == 0) return;
            Step step = from.Pop();
            try
            {
                ScriptNarc narc = new ScriptNarc(step.Dir);
                byte[] now = narc.Get(step.Member);
                // With nothing kept for this file, the bytes on disk are the saved ones. A file stepped back
                // to its saved bytes no longer counts as changed.
                (DirNames, int) key = (step.Dir, step.Member);
                if (now != null) _originals.TryAdd(key, (byte[])now.Clone());
                narc.Put(step.Member, step.Bytes);
                if (_originals.TryGetValue(key, out byte[] saved) && saved.AsSpan().SequenceEqual(step.Bytes))
                    _originals.Remove(key);
                if (now != null) to.Push(new Step(step.Dir, step.Member, now, step.What));
                Reload();
                StatusText = said + ": " + step.What;
            }
            catch (Exception ex)
            {
                AppLogger.Error("Pokédex graphics step: " + ex.Message);
                StatusText = "That change could not be moved. " + ex.Message;
            }
            RaiseSteps();
        }

        private void RaiseSteps()
        {
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
            OnPropertyChanged(nameof(UndoWhat));
            OnPropertyChanged(nameof(RedoWhat));
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        // ── status ─────────────────────────────────────────────────────────────────────────────────

        private string _status = "";
        public string StatusText { get => _status; private set => Set(ref _status, value); }
        public void Say(string what) => StatusText = what;

        private static Bitmap ToBitmap(byte[] rgba, int width, int height)
        {
            WriteableBitmap wb = new(new global::Avalonia.PixelSize(width, height), new global::Avalonia.Vector(96, 96),
                                     PixelFormat.Rgba8888, AlphaFormat.Unpremul);
            using (global::Avalonia.Platform.ILockedFramebuffer fb = wb.Lock())
                System.Runtime.InteropServices.Marshal.Copy(rgba, 0, fb.Address, rgba.Length);
            return wb;
        }

        private void Set<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value;
            OnPropertyChanged(name);
        }

        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
