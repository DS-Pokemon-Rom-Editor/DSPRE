using System;
using System.Collections.Generic;
using global::Avalonia.Media.Imaging;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    internal sealed class PokemonIconCache : IDisposable
    {
        private const int IconSize = 32;
        private readonly Dictionary<int, Bitmap> _icons = new();

        public Bitmap Get(int species)
        {
            if (species <= 0) return null;
            if (_icons.TryGetValue(species, out Bitmap icon)) return icon;

            try
            {
                RawImage raw = DSUtils.GetPokePicRaw(species, IconSize, IconSize);
                icon = ImageConverter.ToAvaloniaBitmap(raw);
            }
            catch (Exception ex)
            {
                AppLogger.WarnOnce("PokemonIconCache.Get", ex);
                icon = null;
            }

            _icons[species] = icon;
            return icon;
        }

        public void Dispose()
        {
            foreach (Bitmap icon in _icons.Values)
                icon?.Dispose();
            _icons.Clear();
        }
    }
}
