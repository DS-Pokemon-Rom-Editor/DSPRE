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
    public sealed class ScreenLayerRow
    {
        public string Name { get; init; }
        public string Where { get; init; }
        public ScreenSide Side { get; init; }
        public ScreenLayerKind Kind { get; init; }
        /// <summary>The background's number, or the item's place in its list (negative for a label).</summary>
        public int Index { get; init; }
    }

    /// <summary>One file a layer is made from, which can be painted, exported or replaced.</summary>
    public sealed class ScreenPartRow
    {
        public string Name { get; init; }
        public string Where { get; init; }
        public DirNames Dir { get; init; }
        public int Member { get; init; }
    }

    /// <summary>
    /// Every page of a book (the Pokédex, the summary screen) of the loaded game, put together from its own files
    /// with a sample, so each background, sprite and label can be picked and its drawing, arrangement or colours
    /// edited in place.
    /// </summary>
    public sealed class ScreenPagesEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo, IDetachable
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private readonly IReadOnlyList<ScreenPage> _pages;
        private readonly ScreenBook _book;
        private readonly Dictionary<int, FieldFont> _fonts = new();
        private ScreenPageComposer _composer;
        private ComposedScreen _top, _bottom;

        public ObservableCollection<string> PageNames { get; } = new();
        public ObservableCollection<string> VariantNames { get; } = new();
        public ObservableCollection<ScreenLayerRow> Layers { get; } = new();
        public ObservableCollection<ScreenPartRow> Parts { get; } = new();

        public ScreenPagesEditorViewModel() : this(new PokedexBook(), null) { }

        /// <param name="pageId">The page to open on, or null for the first.</param>
        public ScreenPagesEditorViewModel(ScreenBook book, string pageId)
        {
            _book = book;
            Pending.Changed += RaiseSteps;
            _pages = book.Pages;
            foreach (ScreenPage p in _pages) PageNames.Add(p.Name);
            _composer = book.MakeComposer(Font);
            int start = pageId == null ? 0 : Math.Max(0, _pages.ToList().FindIndex(p => p.Id == pageId));
            if (_pages.Count > 0) PageIndex = start;
            else StatusText = "This game has no pages to show here.";
            _picturesSaved = AppEvents.WhenSaved(Compose, book.PictureArchives);
        }

        private readonly IDisposable _picturesSaved;
        public void Detach() => _picturesSaved?.Dispose();

        public string Title => _book.Title;

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

        private ScreenPage Page => _pageIndex >= 0 && _pageIndex < _pages.Count ? _pages[_pageIndex] : null;

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
            ScreenPage page = Page;
            if (page == null || _composer == null) return;
            try
            {
                using IDisposable pending = Pending.Reading();
                if (_book.Layout != null) _book.Layout.Use(Pending.Read(_book.Layout.Target));
                ScreenSide topSide = page.MainOnTop ? ScreenSide.Main : ScreenSide.Sub;
                ScreenSide bottomSide = page.MainOnTop ? ScreenSide.Sub : ScreenSide.Main;
                _top = _composer.Compose(page, Variant, topSide);
                _bottom = _composer.Compose(page, Variant, bottomSide);
                int keep = _layerIndex;
                Layers.Clear();
                AddLayers(_top, topSide, "Top");
                AddLayers(_bottom, bottomSide, "Bottom");
                _layerIndex = -1;
                LayerIndex = keep >= 0 && keep < Layers.Count ? keep : -1;
                FillBoxes();
                Redraw();
            }
            catch (Exception ex)
            {
                AppLogger.Error(_book.Title + " page compose: " + ex.Message);
                StatusText = "This page could not be put together. " + ex.Message;
            }
        }

        private void AddLayers(ComposedScreen composed, ScreenSide side, string where)
        {
            // Front first, the way a person looks at a screen.
            for (int i = composed.Layers.Count - 1; i >= 0; i--)
            {
                ScreenLayerImage l = composed.Layers[i];
                Layers.Add(new ScreenLayerRow { Name = l.Name, Where = where, Side = side, Kind = l.Kind, Index = l.Index });
            }
        }

        private void Redraw()
        {
            ScreenLayerRow picked = SelectedLayer;
            TopImage = Show(_top, picked, "Top");
            BottomImage = Show(_bottom, picked, "Bottom");
        }

        // The picked text box or bar, outlined on its screen.
        private void Outline(byte[] rgba, string where)
        {
            ScreenLayoutItem box = SelectedBox;
            ScreenPage page = Page;
            if (box == null || page == null) return;
            ScreenSide side = where == "Top" == page.MainOnTop ? ScreenSide.Main : ScreenSide.Sub;
            if (box.Side != side) return;
            int left = box.X * 8, top = box.Y * 8, right = (box.X + box.Width) * 8 - 1, bottom = (box.Y + box.Height) * 8 - 1;
            for (int x = left; x <= right; x++) { Mark(rgba, x, top); Mark(rgba, x, bottom); }
            for (int y = top; y <= bottom; y++) { Mark(rgba, left, y); Mark(rgba, right, y); }
        }

        private static void Mark(byte[] rgba, int x, int y)
        {
            if (x < 0 || y < 0 || x >= ScreenPageComposer.Width || y >= ScreenPageComposer.Height) return;
            int at = (y * ScreenPageComposer.Width + x) * 4;
            // Every other pixel light and dark, so the outline shows on any colour.
            bool light = ((x + y) & 1) == 0;
            rgba[at] = light ? (byte)255 : (byte)0;
            rgba[at + 1] = light ? (byte)64 : (byte)0;
            rgba[at + 2] = light ? (byte)160 : (byte)0;
        }

        // The picked layer at full strength over everything else dimmed, the other screen included, so it is plain
        // which pixels it owns.
        private Bitmap Show(ComposedScreen composed, ScreenLayerRow picked, string where)
        {
            if (composed == null) return null;
            byte[] rgba = (byte[])composed.Rgba.Clone();
            // Both screens number their backgrounds alike, so the picked layer belongs to one screen only.
            ScreenLayerImage own = picked == null || picked.Where != where ? null : composed.Layers.FirstOrDefault(
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
            Outline(rgba, where);
            return ToBitmap(rgba, ScreenPageComposer.Width, ScreenPageComposer.Height);
        }

        /// <summary>Picks the front layer that draws the pixel x, y of the top or bottom screen.</summary>
        public void PickAt(bool top, int x, int y)
        {
            ComposedScreen composed = top ? _top : _bottom;
            if (composed == null || x < 0 || y < 0 || x >= ScreenPageComposer.Width || y >= ScreenPageComposer.Height) return;
            int at = (y * ScreenPageComposer.Width + x) * 4 + 3;
            for (int i = composed.Layers.Count - 1; i >= 0; i--)
            {
                ScreenLayerImage l = composed.Layers[i];
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

        public ScreenLayerRow SelectedLayer => _layerIndex >= 0 && _layerIndex < Layers.Count ? Layers[_layerIndex] : null;
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

        public ScreenPartRow SelectedPart => _partIndex >= 0 && _partIndex < Parts.Count ? Parts[_partIndex] : null;
        public bool CanEditPart => SelectedPart != null;

        private void FillParts()
        {
            Parts.Clear();
            ScreenLayerRow layer = SelectedLayer;
            ScreenPage page = Page;
            if (layer != null && page != null)
                foreach (ScreenPartRow part in PartsOf(page.Side(layer.Side), layer)) Parts.Add(part);
            _partIndex = -1;
            PartIndex = Parts.Count > 0 ? 0 : -1;
        }

        private IEnumerable<ScreenPartRow> PartsOf(ScreenLayout screen, ScreenLayerRow layer)
        {
            DirNames dex = _book.Archive;
            HashSet<(DirNames, int)> seen = new();
            ScreenPartRow Dex(string name, int member, DirNames? from = null) =>
                member >= 0 && seen.Add((from ?? dex, member))
                    ? new ScreenPartRow { Name = name, Where = (from == null ? "File " : NamedArchive(from.Value) + " ") + member, Dir = from ?? dex, Member = member }
                    : null;

            List<ScreenPartRow> parts = new();
            void Add(ScreenPartRow row) { if (row != null) parts.Add(row); }
            switch (layer.Kind)
            {
                case ScreenLayerKind.Background:
                    ScreenBg bg = screen.Bgs.FirstOrDefault(b => b.Bg == layer.Index && ScreenPage.Shows(b.When, Variant));
                    if (bg == null) break;
                    Add(Dex("Drawing", bg.Drawing));
                    foreach (ScreenPaste piece in bg.Pieces.Where(p => p.Screen >= 0 && ScreenPage.Shows(p.When, Variant)))
                        Add(Dex("Arrangement", piece.Screen));
                    foreach (ScreenPaletteLoad load in screen.Palettes.Where(p => ScreenPage.Shows(p.When, Variant)))
                        Add(Dex("Colours", load.File));
                    break;
                case ScreenLayerKind.Sprite when layer.Index >= 0 && layer.Index < screen.Sprites.Count:
                    ScreenSprite s = screen.Sprites[layer.Index];
                    Add(Dex("Drawing", _book.SpriteDrawing(s), s.DrawingArchive));
                    Add(Dex("Cell layout", s.Cells, s.Archive));
                    Add(Dex("Animation", s.Anim, s.Archive));
                    Add(Dex("Colours", s.Colours));
                    break;
                case ScreenLayerKind.Sprite:
                    int t = -1 - layer.Index;
                    if (t >= 0 && t < screen.Texts.Count) Add(Dex("Colours", screen.Texts[t].Colours));
                    break;
                case ScreenLayerKind.Pokemon when layer.Index >= 0 && layer.Index < screen.Mons.Count:
                    foreach ((string name, string where, DirNames dir, int member) in _book.MonParts(screen.Mons[layer.Index]))
                        parts.Add(new ScreenPartRow { Name = name, Where = where, Dir = dir, Member = member });
                    break;
            }
            return parts;
        }

        private static string NamedArchive(DirNames dir) => GraphicAssets.All.FirstOrDefault(a => a.Dir == dir)?.Title ?? dir.ToString();

        /// <summary>The browser's entry for a file's archive, which the painter, export and import work through.</summary>
        public static GraphicAssets.Archive ArchiveOf(ScreenPartRow part) =>
            part == null ? null : GraphicAssets.All.FirstOrDefault(a => a.Dir == part.Dir);

        /// <summary>Reads every file again after one has been painted or replaced.</summary>
        public void Reload()
        {
            _composer?.Forget();
            _book.Forget();
            try { GraphicAssets.Forget(); } catch { }
            Compose();
        }

        // ── text boxes and bars ────────────────────────────────────────────────────────────────────

        /// <summary>Whether the book places text boxes and bars that can be moved.</summary>
        public bool HasLayout => _book.Layout != null;

        public ObservableCollection<ScreenLayoutItem> Boxes { get; } = new();

        private void FillBoxes()
        {
            if (_book.Layout == null) return;
            string keep = SelectedBox?.Name;
            _filling = true;
            Boxes.Clear();
            foreach (ScreenLayoutItem item in _book.Layout.Items(_pageIndex, Variant)) Boxes.Add(item);
            _boxIndex = -1;
            _filling = false;
            int again = keep == null ? -1 : Boxes.ToList().FindIndex(b => b.Name == keep);
            BoxIndex = again;
            OnPropertyChanged(nameof(BoxIndex));
        }

        private bool _filling;
        private int _boxIndex = -1;
        public int BoxIndex
        {
            get => _boxIndex;
            set
            {
                if (_filling || value == _boxIndex) return;
                _boxIndex = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedBox));
                OnPropertyChanged(nameof(HasBox));
                OnPropertyChanged(nameof(CanResizeBox));
                OnPropertyChanged(nameof(BoxX));
                OnPropertyChanged(nameof(BoxY));
                OnPropertyChanged(nameof(BoxWidth));
                OnPropertyChanged(nameof(BoxHeight));
                Redraw();
            }
        }

        public ScreenLayoutItem SelectedBox => _boxIndex >= 0 && _boxIndex < Boxes.Count ? Boxes[_boxIndex] : null;
        public bool HasBox => SelectedBox != null;
        public bool CanResizeBox => SelectedBox?.CanResize == true;

        public decimal? BoxX { get => SelectedBox?.X; set => PlaceBox(value, null, null, null); }
        public decimal? BoxY { get => SelectedBox?.Y; set => PlaceBox(null, value, null, null); }
        public decimal? BoxWidth { get => SelectedBox?.Width; set => PlaceBox(null, null, value, null); }
        public decimal? BoxHeight { get => SelectedBox?.Height; set => PlaceBox(null, null, null, value); }

        private void PlaceBox(decimal? x, decimal? y, decimal? width, decimal? height)
        {
            ScreenLayoutItem box = SelectedBox;
            if (box == null || _book.Layout == null) return;
            int nx = (int)(x ?? box.X), ny = (int)(y ?? box.Y), nw = (int)(width ?? box.Width), nh = (int)(height ?? box.Height);
            if (nx == box.X && ny == box.Y && nw == box.Width && nh == box.Height) return;
            (byte[] bytes, string error) = _book.Layout.Place(box, nx, ny, nw, nh);
            if (error != null)
            {
                StatusText = error;
                OnPropertyChanged(nameof(BoxX));
                OnPropertyChanged(nameof(BoxY));
                OnPropertyChanged(nameof(BoxWidth));
                OnPropertyChanged(nameof(BoxHeight));
                return;
            }
            PendingArchiveWrites.Target target = _book.Layout.Target;
            Pending.Import(() => { Pending.Stage(target, bytes); return null; });
            StatusText = "";
            Compose();
        }

        // ── changes ────────────────────────────────────────────────────────────────────────────────

        // Imports wait here until Save; the pages are drawn from them meanwhile.
        public PendingArchiveWrites Pending { get; } = new PendingArchiveWrites();

        public bool HasUnsavedChanges => Pending.IsDirty;
        public string UnsavedChangesDescription => _book.UnsavedChangesDescription;
        public bool CanUndo => Pending.CanUndo;
        public bool CanRedo => Pending.CanRedo;

        public void Undo() { if (!Pending.CanUndo) return; Pending.Undo(); Reload(); }
        public void Redo() { if (!Pending.CanRedo) return; Pending.Redo(); Reload(); }

        public void SaveChanges()
        {
            if (!HasUnsavedChanges) return;
            Pending.Save();
            Reload();
            SaveNotice.Saved(UnsavedChangesDescription);
        }

        public void DiscardChanges()
        {
            if (!HasUnsavedChanges) return;
            Pending.Discard();
            Reload();
        }

        /// <summary>Reads on this thread see the pending files until disposed.</summary>
        public IDisposable Reading() => Pending.Reading();

        /// <summary>Runs an import whose writes become one pending step. Returns its error or null.</summary>
        public string Import(Func<string> import)
        {
            string error = Pending.Import(import);
            Reload();
            return error;
        }

        private void RaiseSteps()
        {
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
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
