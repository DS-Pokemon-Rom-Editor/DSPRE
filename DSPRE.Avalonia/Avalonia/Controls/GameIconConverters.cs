using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace DSPRE.Avalonia.Controls
{
    /// <summary>Turns an item or species id into the ROM's 32x32 icon; null when there is none, so the Image shows nothing.</summary>
    public sealed class GameIconConverter : IValueConverter
    {
        public static readonly GameIconConverter Item = new(Kind.Item);
        public static readonly GameIconConverter Pokemon = new(Kind.Pokemon);
        /// <summary>A move id to the icon of the type it has in this ROM.</summary>
        public static readonly GameIconConverter MoveType = new(Kind.MoveType);
        /// <summary>A list label such as "Slot 0 (40%): COMBEE" to the icon of the Pokémon it names.</summary>
        public static readonly GameIconConverter PokemonInLabel = new(Kind.PokemonInLabel);

        private enum Kind { Item, Pokemon, MoveType, PokemonInLabel }
        private string[] _names;
        private readonly Kind _kind;
        private readonly Dictionary<int, Bitmap> _cache = new();
        private string _cachedFor;

        private GameIconConverter(Kind kind) { _kind = kind; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (_kind == Kind.PokemonInLabel)
                return value is string label ? Pokemon.Convert(SpeciesNamedIn(label), targetType, parameter, culture) : null;
            int id = value switch { int i => i, ushort u => u, short s => s, uint ui => (int)ui, decimal d => (int)d, string t when int.TryParse(t, out int n) => n, _ => -1 };
            if (id <= 0) return null;
            if (_cachedFor != RomInfo.workDir)
            {
                _cache.Clear(); _cachedFor = RomInfo.workDir;
                try { DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { _kind switch { Kind.Item => RomInfo.DirNames.itemIcons, Kind.Pokemon => RomInfo.DirNames.monIcons, _ => RomInfo.DirNames.moveData } }); }
                catch { }
            }
            if (_cache.TryGetValue(id, out Bitmap known)) return known;

            Bitmap icon = null;
            try
            {
                if (_kind == Kind.MoveType) icon = Data.TypeIcons.For((int)new DSPRE.MoveData(id).movetype);
                else
                {
                    RawImage raw = _kind == Kind.Item ? DSUtils.GetItemPicRaw(id, 32, 32) : DSUtils.GetPokePicRaw(id, 32, 32);
                    if (raw != null) icon = ImageConverter.ToAvaloniaBitmap(raw);
                }
            }
            catch { icon = null; }
            _cache[id] = icon;
            return icon;
        }

        // The longest ROM Pokémon name that appears in the label as a whole word, so "MIME JR." isn't read as "MR. MIME".
        private int SpeciesNamedIn(string label)
        {
            if (_cachedFor != RomInfo.workDir || _names == null) { _names = RomInfo.GetPokemonNames(); _cachedFor = RomInfo.workDir; }
            int best = 0, bestLength = 0;
            for (int i = 1; i < _names.Length; i++)
            {
                string n = _names[i];
                if (string.IsNullOrWhiteSpace(n) || n.Length <= bestLength) continue;
                int at = label.IndexOf(n, StringComparison.OrdinalIgnoreCase);
                while (at >= 0)
                {
                    bool before = at == 0 || !char.IsLetter(label[at - 1]);
                    bool after = at + n.Length >= label.Length || !char.IsLetter(label[at + n.Length]);
                    if (before && after) { best = i; bestLength = n.Length; break; }
                    at = label.IndexOf(n, at + 1, StringComparison.OrdinalIgnoreCase);
                }
            }
            return best;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
