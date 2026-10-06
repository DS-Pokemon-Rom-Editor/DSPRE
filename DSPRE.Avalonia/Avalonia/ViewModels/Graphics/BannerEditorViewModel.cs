using System;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using global::Avalonia.Platform.Storage;
using AvaloniaBitmap = Avalonia.Media.Imaging.Bitmap;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    /// <summary>
    /// Editor for the DS system-menu banner of a ds-rom project: the 32×32 game icon
    /// (import/export PNG, palette slot 0 transparent + up to 15 opaque colors) and the
    /// per-language titles. Everything lands in the project's <c>banner/</c> folder, which
    /// <c>dsrom build</c> re-encodes into the ROM on Save ROM. Legacy ndstool projects are
    /// display-only (the main window still shows their icon; this editor refuses to open).
    /// </summary>
    public class BannerEditorViewModel : INotifyPropertyChanged, DSPRE.Editors.IEditorWithUnsavedChanges, ISupportsUndo
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
            internal Action Edited;
            private string _text;
            public string Text
            {
                get => _text;
                set { if (_text == value) return; _text = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text))); Edited?.Invoke(); }
            }
        }
        public System.Collections.ObjectModel.ObservableCollection<TitleEntry> Titles { get; } = new();

        public BannerEditorViewModel() { if (Design.IsDesignMode) return; Load(); }

        // A picked icon and edited titles wait here until Save.
        private RawImage _pendingIcon;
        private string _savedTitles = "";
        private string TitleText() => string.Join("", Titles.Select(t => t.Text ?? ""));
        private bool TitlesEdited => TitleText() != _savedTitles;
        public bool HasUnsavedChanges => _pendingIcon != null || TitlesEdited;

        // Icons picked this session, so an undo step can name one by its place here.
        private readonly List<RawImage> _picked = new();
        private ByteStateUndo _undo;
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() => _undo?.Undo();
        public void Redo() => _undo?.Redo();

        private byte[] TakeState() => ByteStateUndo.Pack(w =>
        {
            w.Write(_pendingIcon == null ? -1 : _picked.IndexOf(_pendingIcon));
            foreach (TitleEntry t in Titles) w.Write(t.Text ?? "");
        });

        private void ApplyState(byte[] state) => ByteStateUndo.Unpack(state, r =>
        {
            int icon = r.ReadInt32();
            _pendingIcon = icon >= 0 ? _picked[icon] : null;
            foreach (TitleEntry t in Titles) t.Text = r.ReadString();
            if (_pendingIcon != null) IconPreview = ImageConverter.ToAvaloniaBitmap(_pendingIcon); else RefreshIconPreview();
            RaiseUnsaved();
        });

        private void StartUndo()
        {
            _savedTitles = TitleText();
            _undo = new ByteStateUndo(TakeState, ApplyState, () => { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); });
            OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo));
        }

        private void Edited()
        {
            RaiseUnsaved();
            _undo?.Record();
        }
        public string UnsavedChangesDescription => "Game icon and titles";
        private void RaiseUnsaved() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasUnsavedChanges)));

        public void SaveChanges()
        {
            if (_pendingIcon != null)
            {
                string error = GameBanner.ValidateAndWriteDsRomIcon(_pendingIcon);
                if (error != null) { _ = DialogHelper.ShowError(error, "Game Icon & Banner"); return; }
                _pendingIcon = null;
            }
            if (TitlesEdited && _yaml?.title != null)
            {
                foreach (TitleEntry entry in Titles) _yaml.title[entry.Key] = entry.Text ?? "";
                GameBanner.WriteDsRomYaml(_yaml);
                _savedTitles = TitleText();
            }
            // The written icon is the file now; a step back to an older pick would need it as a pick again.
            StartUndo();
            AppEvents.RaiseBannerChanged();
            RefreshIconPreview();
            RaiseUnsaved();
            StatusText = "Saved. The ROM gets it on the next Save ROM.";
        }

        public void DiscardChanges()
        {
            _pendingIcon = null;
            Titles.Clear();
            Load();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Titles)));
            RaiseUnsaved();
        }

        private void Load()
        {
            _yaml = GameBanner.ReadDsRomYaml();
            if (_yaml?.title != null)
            {
                foreach (KeyValuePair<string, string> kv in _yaml.title)
                    Titles.Add(new TitleEntry
                    {
                        Key = kv.Key,
                        Label = char.ToUpperInvariant(kv.Key[0]) + kv.Key.Substring(1),
                        Text = kv.Value,
                        Edited = Edited,
                    });
            }
            RefreshIconPreview();
            StatusText = _yaml == null ? "banner.yaml not found, titles unavailable." : $"{Titles.Count} title languages.";
            StartUndo();
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
            using (FileStream fs = File.OpenRead(src))
                raw = ImageConverter.DecodeRawImage(fs);

            string error = GameBanner.IconProblem(raw);
            if (error != null)
            {
                await DialogHelper.ShowError(error, "Cannot import icon");
                return;
            }
            _pendingIcon = raw;
            _picked.Add(raw);
            IconPreview = ImageConverter.ToAvaloniaBitmap(raw);
            Edited();
            StatusText = "Icon picked. Save to keep it.";
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
                    GameBanner.BannerYaml yaml = GameBanner.ReadDsRomYaml();
                    yaml?.title?.TryGetValue("english", out title);
                    return (icon, title);
                }
                else
                {
                    RawImage raw = GameBanner.ReadNdstoolIcon(RomInfo.bannerPath);
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
