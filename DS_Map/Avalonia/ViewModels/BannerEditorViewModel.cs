using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using global::Avalonia.Platform.Storage;
using AvaloniaBitmap = Avalonia.Media.Imaging.Bitmap;

namespace DSPRE.Avalonia.ViewModels
{
    /// <summary>
    /// Editor for the DS system-menu banner of a ds-rom project: the 32×32 game icon
    /// (import/export PNG, palette slot 0 transparent + up to 15 opaque colors) and the
    /// per-language titles. Everything lands in the project's <c>banner/</c> folder, which
    /// <c>dsrom build</c> re-encodes into the ROM on Save ROM. Legacy ndstool projects are
    /// display-only (the main window still shows their icon; this editor refuses to open).
    /// </summary>
    public class BannerEditorViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        private GameBanner.BannerYaml _yaml;

        private AvaloniaBitmap _iconPreview;
        public AvaloniaBitmap IconPreview { get => _iconPreview; private set => Set(ref _iconPreview, value); }

        private string _statusText = "";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        // One row per language ds-rom knows about; missing languages simply stay empty and are
        // only written back if they existed in the original yaml (we never invent new keys).
        public class TitleEntry : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            public string Key { get; init; }          // yaml key, e.g. "english"
            public string Label { get; init; }        // display, e.g. "English"
            private string _text;
            public string Text
            {
                get => _text;
                set { if (_text == value) return; _text = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text))); }
            }
        }
        public List<TitleEntry> Titles { get; } = new();

        public BannerEditorViewModel() { if (Design.IsDesignMode) return; Load(); }

        private void Load()
        {
            _yaml = GameBanner.ReadDsRomYaml();
            if (_yaml?.title != null)
            {
                foreach (var kv in _yaml.title)
                    Titles.Add(new TitleEntry
                    {
                        Key = kv.Key,
                        Label = char.ToUpperInvariant(kv.Key[0]) + kv.Key.Substring(1),
                        Text = kv.Value,
                    });
            }
            RefreshIconPreview();
            StatusText = _yaml == null ? "banner.yaml not found — titles unavailable." : $"{Titles.Count} title languages.";
        }

        private void RefreshIconPreview()
        {
            try
            {
                IconPreview = File.Exists(GameBanner.DsRomBitmapPath) ? new AvaloniaBitmap(GameBanner.DsRomBitmapPath) : null;
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Banner icon preview failed: " + ex.Message);
                IconPreview = null;
            }
        }

        public async Task ExportIconAsync(Window owner)
        {
            if (!File.Exists(GameBanner.DsRomBitmapPath))
            {
                await DialogHelper.ShowError("This project has no banner/bitmap.png to export.", "Export icon");
                return;
            }
            string dest = await DialogHelper.SaveFile(owner, "Export game icon as PNG",
                new[] { new FilePickerFileType("PNG image") { Patterns = new[] { "*.png" } } },
                (RomInfo.projectName ?? "icon") + "_icon.png");
            if (string.IsNullOrEmpty(dest)) return;
            File.Copy(GameBanner.DsRomBitmapPath, dest, overwrite: true);
            StatusText = "Icon exported.";
        }

        public async Task ImportIconAsync(Window owner)
        {
            string src = await DialogHelper.OpenFile(owner, "Import a 32×32 game icon (max 15 colors + transparency)",
                new[] { new FilePickerFileType("PNG image") { Patterns = new[] { "*.png" } }, DialogHelper.AllFilter });
            if (string.IsNullOrEmpty(src)) return;

            RawImage raw;
            using (var fs = File.OpenRead(src))
                raw = ImageConverter.DecodeRawImage(fs);

            string error = GameBanner.ValidateAndWriteDsRomIcon(raw);
            if (error != null)
            {
                await DialogHelper.ShowError(error, "Cannot import icon");
                return;
            }
            RefreshIconPreview();
            AppEvents.RaiseBannerChanged();
            StatusText = "Icon imported.";
        }

        public void SaveTitles()
        {
            if (_yaml?.title == null) return;
            foreach (var entry in Titles)
                _yaml.title[entry.Key] = entry.Text ?? "";
            GameBanner.WriteDsRomYaml(_yaml);
            AppEvents.RaiseBannerChanged();
            StatusText = "Titles saved. They are written into the ROM on the next Save ROM.";
        }
    }

    /// <summary>Loads the game icon + English title for the main window, from whichever banner
    /// format the loaded project uses.</summary>
    public static class GameBannerUi
    {
        public static (AvaloniaBitmap icon, string title) TryLoad()
        {
            try
            {
                if (RomInfo.IsDsRomProject)
                {
                    AvaloniaBitmap icon = File.Exists(GameBanner.DsRomBitmapPath)
                        ? new AvaloniaBitmap(GameBanner.DsRomBitmapPath)
                        : null;
                    string title = null;
                    var yaml = GameBanner.ReadDsRomYaml();
                    yaml?.title?.TryGetValue("english", out title);
                    return (icon, title);
                }
                else
                {
                    var raw = GameBanner.ReadNdstoolIcon(RomInfo.bannerPath);
                    return (raw == null ? null : ImageConverter.ToAvaloniaBitmap(raw),
                            GameBanner.ReadNdstoolTitle(RomInfo.bannerPath));
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Game icon load failed: " + ex.Message);
                return (null, null);
            }
        }
    }
}
