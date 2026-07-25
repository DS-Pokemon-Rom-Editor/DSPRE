using Avalonia.Controls;
using Avalonia.Media.Imaging;
using DSPRE.LibNDSFormats;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels
{
    /// <summary>An entry in one of the dialog's ID pickers: a NARC/appearance ID plus a
    /// human-readable label, so the user never has to guess or type an internal number.</summary>
    public class OwIdOption
    {
        public uint Id;
        public string Label;
        public override string ToString() => Label;
    }

    /// <summary>Backing model for the "Add Custom Entry…" dialog opened from the Overworld
    /// Editor. Both the texture slot and the clone-source are picked from real, existing data
    /// (never typed as a raw internal number) — <see cref="BtxEditorViewModel.AddEntryWithImage"/>
    /// does the actual ROM write once the dialog closes with <see cref="Confirmed"/>.
    ///
    /// A texture slot can only be reused at its own exact size/colour-count — DSPRE can't create a
    /// brand-new slot from scratch (see MaxUnusedSlotOptions notes). So once the user picks an
    /// image (PNG or a raw already-BTX0-formatted texture), the slot list is re-sorted with any
    /// slot whose existing dimensions/colour limit actually fit that image pushed to the top and
    /// marked, instead of leaving the user to guess by trial and error.</summary>
    public class AddOverworldEntryViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        {
            if (EqualityComparer<T>.Default.Equals(f, v)) return false;
            f = v; OnPropertyChanged(n); return true;
        }

        private string _appearanceIdText = "";
        public string AppearanceIdText { get => _appearanceIdText; set => Set(ref _appearanceIdText, value); }

        public ObservableCollection<OwIdOption> SlotOptions { get; } = new();
        private OwIdOption _selectedSlot;
        public OwIdOption SelectedSlot { get => _selectedSlot; set => Set(ref _selectedSlot, value); }

        public ObservableCollection<OwIdOption> CloneOptions { get; } = new();
        private OwIdOption _selectedCloneSource;
        public OwIdOption SelectedCloneSource { get => _selectedCloneSource; set => Set(ref _selectedCloneSource, value); }

        private string _pngPath;
        public string PngPath { get => _pngPath; private set => Set(ref _pngPath, value); }
        public bool HasPng => _pngPath != null;

        private string _rawBtxPath;
        public string RawBtxPath { get => _rawBtxPath; private set => Set(ref _rawBtxPath, value); }
        public bool HasRawBtx => _rawBtxPath != null;

        public bool HasImage => HasPng || HasRawBtx;

        private Bitmap _imagePreview;
        public Bitmap ImagePreview { get => _imagePreview; private set => Set(ref _imagePreview, value); }

        /// <summary>Dimensions/colors of whichever image is currently picked — shown right under
        /// the preview so the user can tell which texture slot will actually fit it, before they
        /// even look at the slot dropdown.</summary>
        private string _imageInfoText = "";
        public string ImageInfoText { get => _imageInfoText; private set => Set(ref _imageInfoText, value); }

        private string _statusText = "";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        public bool Confirmed { get; private set; }

        // Info about whichever image (PNG or raw BTX) is currently picked, used to sort/label
        // SlotOptions by fit. 0 width = nothing picked yet.
        private int _targetWidth, _targetHeight;
        private uint _targetColors;

        private class SlotInfo
        {
            public uint Id;
            public int Width, Height;
            public uint ColorLimit;
            public string BaseLabel;
        }
        private readonly List<SlotInfo> _allSlots = new();

        public AddOverworldEntryViewModel()
        {
            if (Design.IsDesignMode) return;
            LoadOptions();
        }

        // mmodel.narc holds a mix of file types (flat billboard textures AND full 3D models for
        // "3D model" draw-type overworlds) — an "unused" member number is not necessarily a
        // texture at all. Only offer/measure ones BTX0 can actually read.
        private const int MaxUnusedSlotCandidatesToScan = 400;
        private const int MaxUnusedSlotOptions = 60;

        private void LoadOptions()
        {
            var used = new HashSet<uint>(RomInfo.OverworldTable.Values.Select(v => v.spriteID));
            string dir = RomInfo.gameDirs[DirNames.OWSprites].unpackedDir;
            var unusedCandidates = Directory.Exists(dir)
                ? Directory.GetFiles(dir)
                    .Select(Path.GetFileName)
                    .Select(n => uint.TryParse(n, out uint id) ? (uint?)id : null)
                    .Where(id => id.HasValue).Select(id => id.Value)
                    .Where(id => !used.Contains(id))
                    .OrderBy(id => id).ToList()
                : new List<uint>();

            int scanned = 0, added = 0;
            foreach (uint id in unusedCandidates)
            {
                if (added >= MaxUnusedSlotOptions || scanned >= MaxUnusedSlotCandidatesToScan) break;
                scanned++;
                if (!TryReadTextureInfo(Path.Combine(dir, id.ToString("D4")), out int w, out int h, out uint colorLimit)) continue;
                _allSlots.Add(new SlotInfo { Id = id, Width = w, Height = h, ColorLimit = colorLimit, BaseLabel = $"Unused slot #{id}" });
                added++;
            }
            foreach (var kv in RomInfo.OverworldTable)
            {
                string path = Path.Combine(dir, kv.Value.spriteID.ToString("D4"));
                if (!TryReadTextureInfo(path, out int w, out int h, out uint colorLimit)) continue;
                _allSlots.Add(new SlotInfo { Id = kv.Value.spriteID, Width = w, Height = h, ColorLimit = colorLimit, BaseLabel = $"Reuse art from OW Entry {kv.Key} (slot #{kv.Value.spriteID}, shared)" });
            }
            RebuildSlotOptions();

            foreach (var key in RomInfo.OverworldTable.Keys)
                CloneOptions.Add(new OwIdOption { Id = key, Label = $"OW Entry {key}" });
            SelectedCloneSource = CloneOptions.FirstOrDefault(o => o.Id == 0x78) ?? CloneOptions.FirstOrDefault();

            uint? suggested = OverworldSpriteTableExpansion.SuggestNewAppearanceId();
            if (suggested.HasValue) AppearanceIdText = $"0x{suggested.Value:X}";
        }

        private void RebuildSlotOptions()
        {
            var previouslySelectedId = SelectedSlot?.Id;
            SlotOptions.Clear();
            bool haveTarget = _targetWidth > 0;

            IEnumerable<SlotInfo> ordered = haveTarget
                ? _allSlots.OrderByDescending(s => Fits(s))
                : _allSlots;

            foreach (var s in ordered)
            {
                bool fits = haveTarget && Fits(s);
                string label = haveTarget
                    ? $"{(fits ? "✓ " : "")}{s.BaseLabel} — {s.Width}×{s.Height}, up to {s.ColorLimit} colors{(fits ? " (fits your image)" : " (different size/palette)")}"
                    : $"{s.BaseLabel} — {s.Width}×{s.Height}, up to {s.ColorLimit} colors";
                SlotOptions.Add(new OwIdOption { Id = s.Id, Label = label });
            }

            SelectedSlot = (previouslySelectedId.HasValue ? SlotOptions.FirstOrDefault(o => o.Id == previouslySelectedId.Value) : null)
                ?? SlotOptions.FirstOrDefault();
        }

        private bool Fits(SlotInfo s) => s.Width == _targetWidth && s.Height == _targetHeight && s.ColorLimit >= _targetColors;

        public void SetPng(string path)
        {
            RawBtxPath = null;
            OnPropertyChanged(nameof(HasRawBtx));
            PngPath = path;
            try
            {
                RawImage raw;
                using (var fs = File.OpenRead(path))
                    raw = ImageConverter.DecodeRawImage(fs);
                if (raw == null)
                {
                    PngPath = null;
                    ImagePreview = null;
                    ImageInfoText = "";
                    StatusText = "Could not decode that image.";
                }
                else
                {
                    _targetWidth = raw.Width; _targetHeight = raw.Height; _targetColors = CountColors(raw);
                    ImagePreview = ImageConverter.ToAvaloniaBitmap(raw);
                    ImageInfoText = $"Your image: {_targetWidth}×{_targetHeight}, {_targetColors} unique colors — pick a slot below marked ✓ (fits your image).";
                    StatusText = "";
                }
            }
            catch
            {
                PngPath = null;
                ImagePreview = null;
                ImageInfoText = "";
                StatusText = "Could not read that image.";
            }
            OnPropertyChanged(nameof(HasPng));
            OnPropertyChanged(nameof(HasImage));
            RebuildSlotOptions();
        }

        public void SetRawBtx(string path)
        {
            PngPath = null;
            OnPropertyChanged(nameof(HasPng));
            RawBtxPath = path;
            try
            {
                var raw = BTX0.ReadRaw(File.ReadAllBytes(path));
                if (raw == null)
                {
                    RawBtxPath = null;
                    ImagePreview = null;
                    ImageInfoText = "";
                    StatusText = "That file isn't a texture DSPRE can read (BTX0, 16-color format).";
                }
                else
                {
                    _targetWidth = raw.Width; _targetHeight = raw.Height; _targetColors = BTX0.ColorCount;
                    ImagePreview = ImageConverter.ToAvaloniaBitmap(raw);
                    ImageInfoText = $"Your texture: {_targetWidth}×{_targetHeight}, {_targetColors} colors (already ROM-native) — pick a slot below marked ✓ (fits your image).";
                    StatusText = "";
                }
            }
            catch
            {
                RawBtxPath = null;
                ImagePreview = null;
                ImageInfoText = "";
                StatusText = "Could not read that file.";
            }
            OnPropertyChanged(nameof(HasRawBtx));
            OnPropertyChanged(nameof(HasImage));
            RebuildSlotOptions();
        }

        public void ClearImage()
        {
            PngPath = null;
            RawBtxPath = null;
            ImagePreview = null;
            ImageInfoText = "";
            StatusText = "";
            _targetWidth = _targetHeight = 0;
            _targetColors = 0;
            OnPropertyChanged(nameof(HasPng));
            OnPropertyChanged(nameof(HasRawBtx));
            OnPropertyChanged(nameof(HasImage));
            RebuildSlotOptions();
        }

        public void Confirm() => Confirmed = true;

        private static bool TryReadTextureInfo(string path, out int width, out int height, out uint colorLimit)
        {
            width = height = 0; colorLimit = 0;
            try
            {
                var raw = BTX0.ReadRaw(File.ReadAllBytes(path));
                if (raw == null) return false;
                width = raw.Width; height = raw.Height; colorLimit = BTX0.ColorCount;
                return true;
            }
            catch { return false; }
        }

        private static uint CountColors(RawImage img)
        {
            var seen = new HashSet<uint>();
            for (int i = 0; i < img.Bgra.Length; i += 4)
                seen.Add(System.BitConverter.ToUInt32(img.Bgra, i));
            return (uint)seen.Count;
        }
    }
}
