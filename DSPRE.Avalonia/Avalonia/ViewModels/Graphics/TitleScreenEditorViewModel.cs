using DSPRE.Avalonia;
using DSPRE.Avalonia.Data;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using AvaBitmap = Avalonia.Media.Imaging.Bitmap;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    public class TitleScreenEditorViewModel : INotifyPropertyChanged, DSPRE.Editors.IEditorWithUnsavedChanges, ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        private readonly TitleScreenGraphics _graphics = new();
        public bool GraphicsAvailable => _graphics.Available;

        /// <summary>0 = HeartGold, 1 = SoulSilver. Both sets live in the same archive regardless of which
        /// one is actually loaded, so either is always editable; defaults to the loaded ROM's own version.</summary>
        private int _selectedTabIndex = RomInfo.GameVersion == RomInfo.GameVersions.HeartGold ? 0 : 1;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set
            {
                if (!Set(ref _selectedTabIndex, value)) return;
                _graphics.Version = value == 0 ? RomInfo.GameVersions.HeartGold : RomInfo.GameVersions.SoulSilver;
                RefreshPreviews();
            }
        }

        private AvaBitmap _logoPreview, _backgroundPreview, _copyrightPreview;
        public AvaBitmap LogoPreview { get => _logoPreview; private set { if (Set(ref _logoPreview, value)) OnPropertyChanged(nameof(PiecePreview)); } }
        public AvaBitmap BackgroundPreview { get => _backgroundPreview; private set { if (Set(ref _backgroundPreview, value)) OnPropertyChanged(nameof(PiecePreview)); } }
        public AvaBitmap CopyrightPreview { get => _copyrightPreview; private set { if (Set(ref _copyrightPreview, value)) OnPropertyChanged(nameof(PiecePreview)); } }

        /// <summary>The title screen's pieces, listed beside it. Platinum's background is the top screen's border.</summary>
        public string[] Pieces { get; } = IsPlatinum ? new[] { "Top screen border", "Logo", "Credit line", "Logo colours" }
                                                     : new[] { "Background", "Logo", "Copyright text", "Colours" };

        public static bool IsPlatinum => RomInfo.GameFamily == RomInfo.GameFamilies.Plat;
        public bool HasVersions => !IsPlatinum;
        /// <summary>HGSS shows the copyright on the top screen; Platinum shows its credit line on the bottom one.</summary>
        public bool CopyrightOnTop => !IsPlatinum;

        private AvaBitmap _bottomPreview;
        /// <summary>Platinum's bottom screen: its border and the credit line.</summary>
        public AvaBitmap BottomPreview { get => _bottomPreview; private set => Set(ref _bottomPreview, value); }
        public string ColoursTip => IsPlatinum ? "Rebuilt on a logo import" : "Rebuilt on a logo or background import";

        private int _pieceIndex = 1;
        public int PieceIndex
        {
            get => _pieceIndex;
            set
            {
                if (value < 0 || !Set(ref _pieceIndex, value)) return;
                OnPropertyChanged(nameof(PieceName)); OnPropertyChanged(nameof(PiecePreview));
                OnPropertyChanged(nameof(PieceIsImage)); OnPropertyChanged(nameof(PieceIsColours));
            }
        }

        public string PieceName => Pieces[_pieceIndex];
        public bool PieceIsImage => _pieceIndex < 3;
        public bool PieceIsColours => _pieceIndex == 3;
        public AvaBitmap PiecePreview => _pieceIndex switch { 0 => BackgroundPreview, 1 => LogoPreview, 2 => CopyrightPreview, _ => null };

        private string _statusText = string.Empty;
        public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }

        /// <summary>True while imports for either version, or the shared copyright, wait to be saved.</summary>
        public bool HasChanges => _graphics.HasChanges;

        public TitleScreenEditorViewModel()
        {
            RefreshPreviews();
            _graphics.Pending.Changed += () => { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); OnPropertyChanged(nameof(HasChanges)); OnPropertyChanged(nameof(HasUnsavedChanges)); };
        }

        public bool CanUndo => _graphics.Pending.CanUndo;
        public bool CanRedo => _graphics.Pending.CanRedo;
        public void Undo() { _graphics.Pending.Undo(); RefreshPreviews(); }
        public void Redo() { _graphics.Pending.Redo(); RefreshPreviews(); }

        // Each import is one pending step until Save.
        private string Imported(Func<string> import)
        {
            string error = _graphics.Pending.Import(import);
            if (error == null) RefreshPreviews();
            return error;
        }

        private void RefreshPreviews()
        {
            if (!GraphicsAvailable)
            {
                LogoPreview = BackgroundPreview = CopyrightPreview = null;
                StatusText = "Title screen graphics aren't available for this ROM.";
                OnPropertyChanged(nameof(HasChanges)); OnPropertyChanged(nameof(HasUnsavedChanges));
                return;
            }
            RawImage logo = _graphics.ComposeLogo();
            RawImage background = _graphics.ComposeBackground();
            RawImage copyright = _graphics.ComposeCopyright();
            LogoPreview = ImageConverter.ToAvaloniaBitmap(logo);
            BackgroundPreview = ImageConverter.ToAvaloniaBitmap(background);
            CopyrightPreview = ImageConverter.ToAvaloniaBitmap(copyright);
            BottomPreview = IsPlatinum ? ImageConverter.ToAvaloniaBitmap(Over(_graphics.ComposeBottomBorder(), copyright)) : null;
            StatusText = (logo == null || background == null || copyright == null)
                ? "Couldn't decode the current title screen graphics." : string.Empty;
            OnPropertyChanged(nameof(HasChanges)); OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        /// <summary>Undoes every import made this session (either version's logo/background/palette, and
        /// the shared copyright), restoring each touched archive member to what it was when this editor
        /// was opened. Does not touch anything not edited this session.</summary>
        // Imports land in the unpacked archive at once; Save keeps them, Discard or closing without saving puts the old bytes back.
        public bool HasUnsavedChanges => HasChanges;
        public string UnsavedChangesDescription => "Title screen";
        public void SaveChanges() { _graphics.AcceptAll(); OnPropertyChanged(nameof(HasChanges)); OnPropertyChanged(nameof(HasUnsavedChanges)); SaveNotice.Saved(UnsavedChangesDescription); }
        public void DiscardChanges() => RevertChanges();

        public void RevertChanges()
        {
            _graphics.RevertAll();
            RefreshPreviews();
        }

        public string ImportLogo(string pngPath)
        {
            RawImage raw = DecodePng(pngPath, out string err);
            if (raw == null) return err;
            return Imported(() => _graphics.ImportLogo(raw));
        }

        public string ImportBackground(string pngPath)
        {
            RawImage raw = DecodePng(pngPath, out string err);
            if (raw == null) return err;
            return Imported(() => _graphics.ImportBackground(raw));
        }

        public string ImportCopyright(string pngPath)
        {
            RawImage raw = DecodePng(pngPath, out string err);
            if (raw == null) return err;
            return Imported(() => _graphics.ImportCopyright(raw));
        }

        public string ExportLogo(string pngPath) => SavePng(_graphics.ComposeLogo(), pngPath);
        public string ExportBackground(string pngPath) => SavePng(_graphics.ComposeBackground(), pngPath);
        public string ExportCopyright(string pngPath) => SavePng(_graphics.ComposeCopyright(), pngPath);

        public string ImportPalette(string nclrPath)
        {
            byte[] bytes;
            try { bytes = PlainFiles.ReadBytes(nclrPath); }
            catch (Exception ex) when (AppLogger.Noted("TitleScreenEditorViewModel.ImportPalette", ex)) { return ex.Message; }
            return Imported(() => _graphics.ImportPaletteRaw(bytes));
        }

        public string ExportPalette(string nclrPath)
        {
            byte[] bytes = _graphics.ExportPaletteRaw();
            if (bytes == null) return "Couldn't read the current palette.";
            try { PlainFiles.WriteBytes(nclrPath, bytes); return null; }
            catch (Exception ex) when (AppLogger.Noted("TitleScreenEditorViewModel.ExportPalette", ex)) { return ex.Message; }
        }

        // The copyright strip over the bottom border, both 256 by 192.
        private static RawImage Over(RawImage under, RawImage over)
        {
            if (under == null || over == null || under.Width != over.Width || under.Height != over.Height) return under ?? over;
            RawImage both = new RawImage(under.Width, under.Height);
            Array.Copy(under.Bgra, both.Bgra, under.Bgra.Length);
            for (int i = 0; i + 3 < over.Bgra.Length; i += 4)
                if (over.Bgra[i + 3] != 0) Array.Copy(over.Bgra, i, both.Bgra, i, 4);
            return both;
        }

        private static DSPRE.RawImage DecodePng(string path, out string error)
        {
            error = null;
            try
            {
                using FileStream stream = PlainFiles.OpenRead(path);
                RawImage raw = ImageConverter.DecodeRawImage(stream);
                if (raw == null) error = "Couldn't read this PNG.";
                return raw;
            }
            catch (Exception ex) when (AppLogger.Noted("TitleScreenEditorViewModel.DecodePng", ex)) { error = ex.Message; return null; }
        }

        private static string SavePng(DSPRE.RawImage raw, string path)
        {
            if (raw == null) return "Couldn't decode this image.";
            try { ImageConverter.ToAvaloniaBitmap(raw).Save(path, global::Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default); return null; }
            catch (Exception ex) when (AppLogger.Noted("TitleScreenEditorViewModel.SavePng", ex)) { return ex.Message; }
        }
    }
}
