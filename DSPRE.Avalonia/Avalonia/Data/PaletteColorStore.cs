using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>The project's 16 favourite and 8 last-used colours for the sprite painter's swatches, kept in dspreDir/dspre_palette_colors.json.</summary>
    public static class PaletteColorStore
    {
        public const int FavoriteSlots = 16;
        public const int LastUsedSlots = 8;

        public static uint?[] Favorites { get; } = new uint?[FavoriteSlots];
        public static List<uint> LastUsed { get; } = new List<uint>();

        private static string _loadedProjectDir;

        private static string FilePath => string.IsNullOrEmpty(DspreDir) ? null : Path.Combine(DspreDir, "dspre_palette_colors.json");

        // Re-loads whenever the open ROM's working directory changes, same guard as LabelStore.Ensure.
        private static void Ensure()
        {
            string pdir = string.IsNullOrEmpty(DspreDir) ? null : DspreDir;
            if (pdir == _loadedProjectDir) return;
            _loadedProjectDir = pdir;
            Load();
        }

        public static void SetFavorite(int slot, uint color)
        {
            Ensure();
            if (slot < 0 || slot >= FavoriteSlots) return;
            Favorites[slot] = color;
            Save();
        }

        public static void ClearFavorite(int slot)
        {
            Ensure();
            if (slot < 0 || slot >= FavoriteSlots) return;
            Favorites[slot] = null;
            Save();
        }

        public static void RecordUsed(uint color)
        {
            Ensure();
            LastUsed.RemoveAll(c => c == color);
            LastUsed.Insert(0, color);
            if (LastUsed.Count > LastUsedSlots) LastUsed.RemoveRange(LastUsedSlots, LastUsed.Count - LastUsedSlots);
            Save();
        }

        private sealed class PaletteColorFile
        {
            public uint?[] Favorites { get; set; }
            public List<uint> LastUsed { get; set; }
        }

        // Files saved before the names went PascalCase use camelCase keys.
        private static readonly JsonSerializerOptions ReadOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        private static void Load()
        {
            Array.Clear(Favorites, 0, Favorites.Length);
            LastUsed.Clear();
            string path = FilePath;
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
                PaletteColorFile file = JsonSerializer.Deserialize<PaletteColorFile>(File.ReadAllText(path), ReadOptions);
                if (file?.Favorites != null)
                    for (int i = 0; i < Math.Min(FavoriteSlots, file.Favorites.Length); i++) Favorites[i] = file.Favorites[i];
                if (file?.LastUsed != null)
                    LastUsed.AddRange(file.LastUsed.Take(LastUsedSlots));
            }
            catch (Exception ex) { AppLogger.Error("PaletteColorStore.Load: " + ex.Message); }
        }

        public static void Save()
        {
            string path = FilePath;
            if (path == null) return;
            try
            {
                PaletteColorFile file = new PaletteColorFile { Favorites = Favorites, LastUsed = LastUsed };
                PlainFiles.WriteText(path, JsonSerializer.Serialize(file, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex) { AppLogger.Error("PaletteColorStore.Save: " + ex.Message); }
        }
    }
}
