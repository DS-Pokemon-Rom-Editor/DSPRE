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

using DSPRE.Avalonia.Data;
namespace DSPRE.Avalonia.ViewModels.World
{
    /// <summary>An entry in one of the dialog's ID pickers: a NARC/appearance ID plus a
    /// human-readable label, so the user never has to guess or type an internal number.</summary>
    public class OwIdOption
    {
        public uint Id;
        public string Label;
        public bool Fits;
        public override string ToString() => Label;
    }

    /// <summary>Backs the Add custom entry dialog of the Overworld editor;
    /// <see cref="BtxEditorViewModel.AddEntryWithImage"/> writes once it closes with <see cref="Confirmed"/>.
    /// DSPRE can't make a slot from scratch, so slots are reused at their size and colour count, and slots
    /// that fit the picked image go first.</summary>
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

        /// <summary>hg-engine numbers new NPCs itself, so there is no appearance ID to type.</summary>
        public bool IsHgEngine => DSPRE.HgEngine.HgEngineProject.IsActive;

        private string _appearanceIdText = "";
        public string AppearanceIdText { get => _appearanceIdText; set => Set(ref _appearanceIdText, value); }

        public ObservableCollection<OwIdOption> SlotOptions { get; } = new();
        private OwIdOption _selectedSlot;
        public OwIdOption SelectedSlot { get => _selectedSlot; set => Set(ref _selectedSlot, value); }

        public ObservableCollection<OwIdOption> CloneOptions { get; } = new();
        private OwIdOption _selectedCloneSource;
        public OwIdOption SelectedCloneSource
        {
            get => _selectedCloneSource;
            set
            {
                if (Set(ref _selectedCloneSource, value)) RebuildSlotOptions();
            }
        }

        private string _pngPath;
        public string PngPath { get => _pngPath; private set => Set(ref _pngPath, value); }
        public bool HasPng => _pngPath != null;

        private string _rawBtxPath;
        public string RawBtxPath { get => _rawBtxPath; private set => Set(ref _rawBtxPath, value); }
        public bool HasRawBtx => _rawBtxPath != null;

        public bool HasImage => HasPng || HasRawBtx;

        /// <summary>With an image the picked slot is only a size template, never written; without one the
        /// entry shares its art.</summary>
        public string SlotSectionLabel => HasImage ? "Size template" : "Texture slot";
        public string SlotSectionTooltip => HasImage
            ? "Your image goes into a new texture slot made for this entry, and nothing here changes. The template has to fit the image and the animation profile; slots that fit say so and come first."
            : "No image picked, so this entry uses the chosen slot's art. The slot has to fit the animation profile; slots that fit say so.";

        private Bitmap _imagePreview;
        public Bitmap ImagePreview { get => _imagePreview; private set => Set(ref _imagePreview, value); }

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
            public Btx0Structure Structure;
        }
        private readonly List<SlotInfo> _allSlots = new();
        private readonly Dictionary<uint, Btx0Structure> _cloneProfiles = new();

        public AddOverworldEntryViewModel()
        {
            if (Design.IsDesignMode) return;
            LoadOptions();
        }

        // mmodel.narc also holds 3D models, so only members BTX0 can read are offered.
        private const int MaxUnusedSlotCandidatesToScan = 400;
        private const int MaxUnusedSlotOptions = 60;

        private void LoadOptions()
        {
            HashSet<uint> used = new HashSet<uint>(RomInfo.OverworldTable.Values.Select(v => v.spriteID));
            string dir = RomInfo.GameDirs[DirNames.OWSprites].unpackedDir;
            List<uint> unusedCandidates = Directory.Exists(dir)
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
                if (!TryReadTextureInfo(Path.Combine(dir, id.ToString("D4")), out int w, out int h, out uint colorLimit, out Btx0Structure structure)) continue;
                _allSlots.Add(new SlotInfo { Id = id, Width = w, Height = h, ColorLimit = colorLimit, Structure = structure, BaseLabel = $"Unused slot #{id}" });
                added++;
            }
            foreach (KeyValuePair<uint, (uint spriteID, ushort properties)> kv in RomInfo.OverworldTable)
            {
                string path = Path.Combine(dir, kv.Value.spriteID.ToString("D4"));
                if (!TryReadTextureInfo(path, out int w, out int h, out uint colorLimit, out Btx0Structure structure)) continue;
                _cloneProfiles[kv.Key] = structure;
                _allSlots.Add(new SlotInfo { Id = kv.Value.spriteID, Width = w, Height = h, ColorLimit = colorLimit, Structure = structure, BaseLabel = $"Art from overworld {kv.Key} (slot #{kv.Value.spriteID})" });
            }
            RebuildSlotOptions();

            foreach (uint key in RomInfo.OverworldTable.Keys)
                CloneOptions.Add(new OwIdOption { Id = key, Label = OverworldLabels.Of(key) });
            SelectedCloneSource = CloneOptions.FirstOrDefault(o => o.Id == 0x78) ?? CloneOptions.FirstOrDefault();

            uint? suggested = OverworldSpriteTableExpansion.SuggestNewAppearanceId();
            if (suggested.HasValue) AppearanceIdText = $"0x{suggested.Value:X}";
        }

        private void RebuildSlotOptions()
        {
            uint? previouslySelectedId = SelectedSlot?.Id;
            SlotOptions.Clear();
            bool haveTarget = _targetWidth > 0;
            bool haveProfile = SelectedCloneSource != null && _cloneProfiles.ContainsKey(SelectedCloneSource.Id);

            IEnumerable<SlotInfo> ordered = haveTarget || haveProfile
                ? _allSlots.OrderByDescending(s => Fits(s))
                : _allSlots;

            foreach (SlotInfo s in ordered)
            {
                bool fits = (haveTarget || haveProfile) && Fits(s);
                string label = haveTarget || haveProfile
                    ? $"{s.BaseLabel}, {s.Width}×{s.Height}, up to {s.ColorLimit} colours{(fits ? " (matches image and profile)" : " (doesn't fit)")}"
                    : $"{s.BaseLabel}, {s.Width}×{s.Height}, up to {s.ColorLimit} colours";
                SlotOptions.Add(new OwIdOption { Id = s.Id, Label = label, Fits = fits });
            }

            OwIdOption previous = previouslySelectedId.HasValue
                ? SlotOptions.FirstOrDefault(o => o.Id == previouslySelectedId.Value)
                : null;
            if ((haveTarget || haveProfile) && previous != null && !previous.Fits)
                previous = null;
            SelectedSlot = previous
                ?? ((haveTarget || haveProfile) ? SlotOptions.FirstOrDefault(o => o.Fits) : null)
                ?? SlotOptions.FirstOrDefault();
        }

        private bool Fits(SlotInfo s)
        {
            bool imageFits = _targetWidth == 0 ||
                (s.Width == _targetWidth && s.Height == _targetHeight && s.ColorLimit >= _targetColors);
            bool profileFits = SelectedCloneSource == null ||
                (_cloneProfiles.TryGetValue(SelectedCloneSource.Id, out Btx0Structure profile) && s.Structure.HasSameProfileAs(profile));
            return imageFits && profileFits;
        }

        public void SetPng(string path)
        {
            RawBtxPath = null;
            OnPropertyChanged(nameof(HasRawBtx));
            PngPath = path;
            try
            {
                RawImage raw;
                using (FileStream fs = File.OpenRead(path))
                    raw = ImageConverter.DecodeRawImage(fs);
                if (raw == null)
                {
                    PngPath = null;
                    ImagePreview = null;
                    ImageInfoText = "";
                    StatusText = "Couldn't decode that image.";
                }
                else
                {
                    _targetWidth = raw.Width; _targetHeight = raw.Height; _targetColors = CountColors(raw);
                    ImagePreview = ImageConverter.ToAvaloniaBitmap(raw);
                    ImageInfoText = $"Image: {_targetWidth} × {_targetHeight}, {_targetColors} colours";
                    StatusText = "";
                }
            }
            catch (System.Exception ex)
            {
                AppLogger.WarnOnce("AddOverworldEntryViewModel.SetPng", ex);
                PngPath = null;
                ImagePreview = null;
                ImageInfoText = "";
                StatusText = "Couldn't read that image.";
            }
            OnPropertyChanged(nameof(HasPng));
            RaiseImageStateChanged();
            RebuildSlotOptions();
        }

        public void SetRawBtx(string path)
        {
            PngPath = null;
            OnPropertyChanged(nameof(HasPng));
            RawBtxPath = path;
            try
            {
                RawImage raw = BTX0.ReadRaw(File.ReadAllBytes(path), 0);
                if (raw == null)
                {
                    RawBtxPath = null;
                    ImagePreview = null;
                    ImageInfoText = "";
                    StatusText = "That file isn't a texture DSPRE can read (a 16-colour BTX0).";
                }
                else
                {
                    _targetWidth = raw.Width; _targetHeight = raw.Height; _targetColors = BTX0.ColorCount;
                    ImagePreview = ImageConverter.ToAvaloniaBitmap(raw);
                    ImageInfoText = $"Texture: {_targetWidth} × {_targetHeight}, {_targetColors} colours";
                    StatusText = "";
                }
            }
            catch (System.Exception ex)
            {
                AppLogger.WarnOnce("AddOverworldEntryViewModel.SetRawBtx", ex);
                RawBtxPath = null;
                ImagePreview = null;
                ImageInfoText = "";
                StatusText = "Couldn't read that file.";
            }
            OnPropertyChanged(nameof(HasRawBtx));
            RaiseImageStateChanged();
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
            RaiseImageStateChanged();
            RebuildSlotOptions();
        }

        private void RaiseImageStateChanged()
        {
            OnPropertyChanged(nameof(HasImage));
            OnPropertyChanged(nameof(SlotSectionLabel));
            OnPropertyChanged(nameof(SlotSectionTooltip));
        }

        public void Confirm() => Confirmed = true;

        private static bool TryReadTextureInfo(string path, out int width, out int height, out uint colorLimit, out Btx0Structure structure)
        {
            width = height = 0; colorLimit = 0; structure = null;
            try
            {
                byte[] data = File.ReadAllBytes(path);
                if (!Btx0Structure.TryInspect(data, out structure, out _)) return false;
                RawImage raw = BTX0.ReadRaw(data, 0);
                if (raw == null) return false;
                width = raw.Width; height = raw.Height; colorLimit = BTX0.ColorCount;
                return true;
            }
            catch (System.Exception ex) { AppLogger.WarnOnce("AddOverworldEntryViewModel.TryReadTextureInfo", ex); return false; }
        }

        private static uint CountColors(RawImage img) => (uint)BTX0.CountColors(img);
    }
}
