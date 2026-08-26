using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>Per-project favorite (16) and last-used (8) palette colors for the Sprite Editor's swatch picker, persisted at workDir/dspre_palette_colors.json (same convention as LabelStore's dspre_labels.json).</summary>
    public static class PaletteColorStore
    {
        public const int FavoriteSlots = 16;
        public const int LastUsedSlots = 8;

        public static uint?[] Favorites { get; } = new uint?[FavoriteSlots];
        public static List<uint> LastUsed { get; } = new List<uint>();

        private static string _loadedProjectDir;

        private static string FilePath => string.IsNullOrEmpty(workDir) ? null : Path.Combine(workDir, "dspre_palette_colors.json");

        // Re-loads whenever the open ROM's working directory changes, same guard as LabelStore.Ensure.
        private static void Ensure()
        {
            string pdir = string.IsNullOrEmpty(workDir) ? null : workDir;
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
            public uint?[] favorites { get; set; }
            public List<uint> lastUsed { get; set; }
        }

        private static void Load()
        {
            Array.Clear(Favorites, 0, Favorites.Length);
            LastUsed.Clear();
            string path = FilePath;
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
                var file = JsonSerializer.Deserialize<PaletteColorFile>(File.ReadAllText(path));
                if (file?.favorites != null)
                    for (int i = 0; i < Math.Min(FavoriteSlots, file.favorites.Length); i++) Favorites[i] = file.favorites[i];
                if (file?.lastUsed != null)
                    LastUsed.AddRange(file.lastUsed.Take(LastUsedSlots));
            }
            catch (Exception ex) { AppLogger.Error("PaletteColorStore.Load: " + ex.Message); }
        }

        public static void Save()
        {
            string path = FilePath;
            if (path == null) return;
            try
            {
                var file = new PaletteColorFile { favorites = Favorites, lastUsed = LastUsed };
                File.WriteAllText(path, JsonSerializer.Serialize(file, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex) { AppLogger.Error("PaletteColorStore.Save: " + ex.Message); }
        }
    }
}
