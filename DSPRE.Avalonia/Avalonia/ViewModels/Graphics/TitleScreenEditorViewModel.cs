using DSPRE.Avalonia;
using DSPRE.Avalonia.Data;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using AvaBitmap = Avalonia.Media.Imaging.Bitmap;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    public class TitleScreenEditorViewModel : INotifyPropertyChanged
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
        private int _selectedTabIndex = RomInfo.gameVersion == RomInfo.GameVersions.HeartGold ? 0 : 1;
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
        public AvaBitmap LogoPreview { get => _logoPreview; private set => Set(ref _logoPreview, value); }
        public AvaBitmap BackgroundPreview { get => _backgroundPreview; private set => Set(ref _backgroundPreview, value); }
        public AvaBitmap CopyrightPreview { get => _copyrightPreview; private set => Set(ref _copyrightPreview, value); }

        private string _statusText = string.Empty;
        public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }

        /// <summary>True once any import this session has changed a title-screen archive member (for
        /// either version, or the shared copyright), so <see cref="RevertChanges"/> has something to undo.</summary>
        public bool HasChanges => _graphics.HasChanges;

        public TitleScreenEditorViewModel() => RefreshPreviews();

        private void RefreshPreviews()
        {
            if (!GraphicsAvailable)
            {
                LogoPreview = BackgroundPreview = CopyrightPreview = null;
                StatusText = "Title screen graphics are not available for this ROM.";
                OnPropertyChanged(nameof(HasChanges));
                return;
            }
            var logo = _graphics.ComposeLogo();
            var background = _graphics.ComposeBackground();
            var copyright = _graphics.ComposeCopyright();
            LogoPreview = ImageConverter.ToAvaloniaBitmap(logo);
            BackgroundPreview = ImageConverter.ToAvaloniaBitmap(background);
            CopyrightPreview = ImageConverter.ToAvaloniaBitmap(copyright);
            StatusText = (logo == null || background == null || copyright == null)
                ? "Could not decode the current title screen graphics." : string.Empty;
            OnPropertyChanged(nameof(HasChanges));
        }

        /// <summary>Undoes every import made this session (either version's logo/background/palette, and
        /// the shared copyright), restoring each touched archive member to what it was when this editor
        /// was opened. Does not touch anything not edited this session.</summary>
        public void RevertChanges()
        {
            _graphics.RevertAll();
            RefreshPreviews();
        }

        public string ImportLogo(string pngPath)
        {
            var raw = DecodePng(pngPath, out string err);
            if (raw == null) return err;
            string error = _graphics.ImportLogo(raw);
            if (error == null) RefreshPreviews();
            return error;
        }

        public string ImportBackground(string pngPath)
        {
            var raw = DecodePng(pngPath, out string err);
            if (raw == null) return err;
            string error = _graphics.ImportBackground(raw);
            if (error == null) RefreshPreviews();
            return error;
        }

        public string ImportCopyright(string pngPath)
        {
            var raw = DecodePng(pngPath, out string err);
            if (raw == null) return err;
            string error = _graphics.ImportCopyright(raw);
            if (error == null) RefreshPreviews();
            return error;
        }

        public string ExportLogo(string pngPath) => SavePng(_graphics.ComposeLogo(), pngPath);
        public string ExportBackground(string pngPath) => SavePng(_graphics.ComposeBackground(), pngPath);
        public string ExportCopyright(string pngPath) => SavePng(_graphics.ComposeCopyright(), pngPath);

        public string ImportPalette(string nclrPath)
        {
            byte[] bytes;
            try { bytes = System.IO.File.ReadAllBytes(nclrPath); }
            catch (Exception ex) { return ex.Message; }
            string error = _graphics.ImportPaletteRaw(bytes);
            if (error == null) RefreshPreviews();
            return error;
        }

        public string ExportPalette(string nclrPath)
        {
            byte[] bytes = _graphics.ExportPaletteRaw();
            if (bytes == null) return "Could not read the current palette.";
            try { System.IO.File.WriteAllBytes(nclrPath, bytes); return null; }
            catch (Exception ex) { return ex.Message; }
        }

        private static DSPRE.RawImage DecodePng(string path, out string error)
        {
            error = null;
            try
            {
                using var stream = System.IO.File.OpenRead(path);
                var raw = ImageConverter.DecodeRawImage(stream);
                if (raw == null) error = "Could not read this PNG.";
                return raw;
            }
            catch (Exception ex) { error = ex.Message; return null; }
        }

        private static string SavePng(DSPRE.RawImage raw, string path)
        {
            if (raw == null) return "Could not decode this image.";
            try { ImageConverter.ToAvaloniaBitmap(raw).Save(path); return null; }
            catch (Exception ex) { return ex.Message; }
        }
    }
}
