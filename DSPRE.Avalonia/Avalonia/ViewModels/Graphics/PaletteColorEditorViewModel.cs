using Avalonia.Media;
using DSPRE.Avalonia.Data;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    /// <summary>One favorite palette slot: empty (never saved to) or holding a color.</summary>
    public class FavoriteSlotVM
    {
        public int Slot { get; set; }
        public uint? Argb { get; set; }
        public bool IsEmpty => Argb == null;
        public IBrush Brush => Argb.HasValue ? ToBrush(Argb.Value) : Brushes.Transparent;
        internal static IBrush ToBrush(uint argb) =>
            new SolidColorBrush(Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb));
    }

    /// <summary>One last-used color chip (read-only, auto-populated).</summary>
    public class RecentColorVM
    {
        public uint Argb { get; set; }
        public IBrush Brush => FavoriteSlotVM.ToBrush(Argb);
    }

    /// <summary>RGB slider editor for one palette swatch, plus favorites and last-used; edits write straight into the sprite's live palette array, so the editor's own Save button already persists them.</summary>
    public class PaletteColorEditorViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private readonly PokemonSpriteEditorViewModel _sprite;
        private readonly bool _shiny;
        private readonly int _index;
        private readonly uint _initialArgb;

        public string Title => $"{(_shiny ? "Shiny" : "Normal")} Palette, Color {_index}";

        public ObservableCollection<FavoriteSlotVM> Favorites { get; } = new ObservableCollection<FavoriteSlotVM>();
        public ObservableCollection<RecentColorVM> LastUsed { get; } = new ObservableCollection<RecentColorVM>();
        public bool HasLastUsed => LastUsed.Count > 0;

        public PaletteColorEditorViewModel(PokemonSpriteEditorViewModel sprite, bool shiny, int index)
        {
            _sprite = sprite;
            _shiny = shiny;
            _index = index;
            uint[] pal = sprite.GetPalette(shiny);
            _initialArgb = (pal != null && index < pal.Length) ? pal[index] : 0xFF000000u;
            SetFieldsFrom(_initialArgb);
            RefreshFavorites();
            RefreshLastUsed();
        }

        private byte _r, _g, _b;
        public byte R { get => _r; set => ApplyChannel(value, _g, _b); }
        public byte G { get => _g; set => ApplyChannel(_r, value, _b); }
        public byte B { get => _b; set => ApplyChannel(_r, _g, value); }

        private string _hex;
        public string Hex
        {
            get => _hex;
            set
            {
                _hex = value;
                OnPropertyChanged();
                if (TryParseHex(value, out uint argb)) Apply(argb);
            }
        }

        public IBrush PreviewBrush => FavoriteSlotVM.ToBrush(CurrentArgb);
        private uint CurrentArgb => 0xFF000000u | ((uint)_r << 16) | ((uint)_g << 8) | _b;

        private void ApplyChannel(byte r, byte g, byte b) => Apply(0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | b);

        private void Apply(uint argb)
        {
            SetFieldsFrom(argb);
            _sprite.SetPaletteColor(_shiny, _index, argb);
        }

        private void SetFieldsFrom(uint argb)
        {
            _r = (byte)(argb >> 16); _g = (byte)(argb >> 8); _b = (byte)argb;
            _hex = $"#{_r:X2}{_g:X2}{_b:X2}";
            OnPropertyChanged(nameof(R)); OnPropertyChanged(nameof(G)); OnPropertyChanged(nameof(B));
            OnPropertyChanged(nameof(Hex)); OnPropertyChanged(nameof(PreviewBrush));
        }

        private static bool TryParseHex(string text, out uint argb)
        {
            argb = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string t = text.Trim().TrimStart('#');
            if (t.Length != 6 || !uint.TryParse(t, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb)) return false;
            argb = 0xFF000000u | rgb;
            return true;
        }

        private void RefreshFavorites()
        {
            Favorites.Clear();
            for (int i = 0; i < PaletteColorStore.FavoriteSlots; i++)
                Favorites.Add(new FavoriteSlotVM { Slot = i, Argb = PaletteColorStore.Favorites[i] });
        }

        private void RefreshLastUsed()
        {
            LastUsed.Clear();
            foreach (uint c in PaletteColorStore.LastUsed) LastUsed.Add(new RecentColorVM { Argb = c });
            OnPropertyChanged(nameof(HasLastUsed));
        }

        public void ApplyColor(uint argb) => Apply(argb);

        public void SaveCurrentToFavorite(int slot)
        {
            PaletteColorStore.SetFavorite(slot, CurrentArgb);
            RefreshFavorites();
        }

        public void ClearFavorite(int slot)
        {
            PaletteColorStore.ClearFavorite(slot);
            RefreshFavorites();
        }

        /// <summary>Called once when the popup closes: records the final color (if it actually changed) as last-used.</summary>
        public void CommitOnClose()
        {
            if (CurrentArgb == _initialArgb) return;
            PaletteColorStore.RecordUsed(CurrentArgb);
        }
    }
}
