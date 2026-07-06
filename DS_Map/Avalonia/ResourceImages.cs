using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using AvaloniaBitmap = Avalonia.Media.Imaging.Bitmap;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// GDI-free replacement for the <c>Properties.Resources</c> image lookups in the Avalonia shell.
    /// The PNG/GIF sources behind the WinForms .resx (<c>Resources/Graphics/**</c>) are embedded as
    /// <c>avares://</c> assets (see DSPRE.csproj); this resolves a .resx key to the asset by file
    /// basename. Unknown keys return null, matching <c>ResourceManager.GetObject</c>.
    /// </summary>
    internal static class ResourceImages
    {
        private const string AssetRoot = "avares://DSPRE.Avalonia/Resources/Graphics/";

        /// <summary>.resx keys whose backing file has a different basename.</summary>
        private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["dpareaicon"] = "dp",                    // Area Icons\dp.png
            ["dpthunderstorm1"] = "dpthunderstorm",
            ["hgssrain1"] = "hgssrain",
        };

        private static Dictionary<string, Uri> _index;
        private static readonly Dictionary<string, AvaloniaBitmap> _bitmapCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, RawImage> _rawCache = new(StringComparer.OrdinalIgnoreCase);

        private static Dictionary<string, Uri> Index
        {
            get
            {
                if (_index != null) return _index;
                var idx = new Dictionary<string, Uri>(StringComparer.OrdinalIgnoreCase);
                foreach (Uri uri in AssetLoader.GetAssets(new Uri(AssetRoot), null))
                {
                    string file = Uri.UnescapeDataString(uri.AbsolutePath);
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ext != ".png" && ext != ".gif" && ext != ".bmp" && ext != ".jpg" && ext != ".jpeg") continue;
                    string key = Path.GetFileNameWithoutExtension(file);
                    // On basename collisions prefer .png (matches the .resx, e.g. hgssfog.png over .gif).
                    if (!idx.ContainsKey(key) || ext == ".png") idx[key] = uri;
                }
                _index = idx;
                return idx;
            }
        }

        private static Uri Resolve(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (Aliases.TryGetValue(name, out string alias)) name = alias;
            if (Index.TryGetValue(name, out Uri uri)) return uri;
            // Some files zero-pad a single trailing digit (hgsscamera8 → hgsscamera08.png).
            if (name.Length >= 2 && char.IsDigit(name[name.Length - 1]) && !char.IsDigit(name[name.Length - 2])
                && Index.TryGetValue(name.Substring(0, name.Length - 1) + "0" + name[name.Length - 1], out uri))
                return uri;
            return null;
        }

        /// <summary>Loads the asset as an Avalonia bitmap (for Image.Source bindings). Null if unknown.</summary>
        public static AvaloniaBitmap GetBitmap(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (_bitmapCache.TryGetValue(name, out AvaloniaBitmap cached)) return cached;

            AvaloniaBitmap bmp = null;
            Uri uri = Resolve(name);
            if (uri != null)
            {
                try
                {
                    using Stream s = AssetLoader.Open(uri);
                    bmp = new AvaloniaBitmap(s);
                }
                catch (Exception ex) { AppLogger.Error($"Resource image '{name}' failed to decode: {ex.Message}"); }
            }
            _bitmapCache[name] = bmp;
            return bmp;
        }

        /// <summary>Loads the asset as a <see cref="RawImage"/> (for pixel-level use, e.g. GL upload). Null if unknown.</summary>
        public static RawImage GetRaw(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (_rawCache.TryGetValue(name, out RawImage cached)) return cached;

            RawImage raw = null;
            Uri uri = Resolve(name);
            if (uri != null)
            {
                try
                {
                    using Stream s = AssetLoader.Open(uri);
                    raw = ImageConverter.DecodeRawImage(s);
                }
                catch (Exception ex) { AppLogger.Error($"Resource image '{name}' failed to decode: {ex.Message}"); }
            }
            _rawCache[name] = raw;
            return raw;
        }
    }
}
