using System;
using System.Collections.Generic;
using System.IO;
using LibNDSFormats.NSBMD;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The overworld entries DP and Platinum draw as a small 3D model instead of a sprite: signposts, the book, a
    /// door and Rotom's wall. Each model sits in the field effect archive with its own textures, and is found by its
    /// name there, so a ROM whose archive was rebuilt in another order still shows the right one.
    /// </summary>
    public static class Overworld3DObjects
    {
        /// <summary>One object: its model's name and where it is drawn from its square's centre, in field units (16 per tile).</summary>
        private readonly record struct Kind(string Model, float OffsetZ, bool PlatinumOnly);

        // pokeplatinum src/overlay005/ov5_021F10E8.c Unk_ov5_02200230 pairs these codes with the models, and
        // object_event_gfx_data.c gObjectEventGfxOverworldAnimsInitialPosTable gives the offsets. Diamond's archive
        // holds the same models under the same names, without Rotom's wall.
        private static readonly Dictionary<uint, Kind> Kinds = new Dictionary<uint, Kind>
        {
            [0x5B] = new Kind("board_a", -2f, false),
            [0x5C] = new Kind("board_b", -2f, false),
            [0x5D] = new Kind("board_c", -2f, false),
            [0x5E] = new Kind("board_d", -2f, false),
            [0x5F] = new Kind("board_e", -2f, false),
            [0x60] = new Kind("board_f", -2f, false),
            [0xB7] = new Kind("book", 0f, false),
            [0xD1] = new Kind("door2", 0f, false),
            [0x106] = new Kind("rotomwall", 0f, true),
        };

        private static readonly HashSet<string> Wanted = new HashSet<string>(System.Linq.Enumerable.Select(Kinds.Values, k => k.Model), StringComparer.Ordinal);

        private static Dictionary<string, int> _memberByName;
        private static readonly Dictionary<int, byte[]> Files = new Dictionary<int, byte[]>();

        /// <summary>Forgets what was read from the previous ROM.</summary>
        public static void Forget()
        {
            _memberByName = null;
            Files.Clear();
        }

        /// <summary>Whether this overworld entry is drawn as a model in this game.</summary>
        public static bool IsModel(uint entry) =>
            (GameFamily == GameFamilies.DP || GameFamily == GameFamilies.Plat)
            && Kinds.TryGetValue(entry, out Kind kind) && (!kind.PlatinumOnly || GameFamily == GameFamilies.Plat);

        /// <summary>Where the model is drawn from its square's centre, along the map's depth, in field units.</summary>
        public static float OffsetZ(uint entry) => Kinds.TryGetValue(entry, out Kind kind) ? kind.OffsetZ : 0f;

        /// <summary>
        /// A freshly read model for the entry with its own textures bound, or null when this game draws the entry
        /// as a sprite or the model is missing. Fresh each time, as scene building binds into it.
        /// </summary>
        public static NSBMD Load(uint entry)
        {
            if (!IsModel(entry)) return null;
            byte[] file = FileFor(Kinds[entry].Model);
            if (file == null) return null;
            NSBMD model = NSBMDLoader.LoadNSBMD(new MemoryStream(file));
            if (model?.Models == null || model.Models.Length == 0) return null;
            try { model.MatchTextures(); }
            catch (Exception ex) when (ex is IndexOutOfRangeException || ex is ArgumentException || ex is NullReferenceException)
            {
                AppLogger.Error($"Overworld 3D object {entry} textures: {ex.Message}");
            }
            return model;
        }

        private static byte[] FileFor(string name)
        {
            if (!GameDirs.ContainsKey(DirNames.FieldEffectModels)) return null;
            if (_memberByName == null)
            {
                _memberByName = new Dictionary<string, int>(StringComparer.Ordinal);
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.FieldEffectModels });
                string dir = GameDirs[DirNames.FieldEffectModels].unpackedDir;
                if (!Directory.Exists(dir)) return null;
                foreach (string path in Directory.GetFiles(dir))
                {
                    if (!int.TryParse(Path.GetFileName(path), out int member)) continue;
                    try
                    {
                        byte[] data = File.ReadAllBytes(path);
                        if (data.Length < 4 || BitConverter.ToUInt32(data, 0) != NSBMD.NdsTypeBmd0) continue;
                        NSBMD parsed = NSBMDLoader.LoadNSBMD(new MemoryStream(data));
                        string modelName = parsed?.Models != null && parsed.Models.Length > 0 ? parsed.Models[0].Name : null;
                        if (!string.IsNullOrEmpty(modelName) && Wanted.Contains(modelName) && !_memberByName.ContainsKey(modelName))
                        {
                            _memberByName[modelName] = member;
                            Files[member] = data;
                        }
                    }
                    catch (Exception ex) when (ex is IOException || ex is EndOfStreamException || ex is ArgumentException || ex is IndexOutOfRangeException) { }   // a model that can't be read is left out of the name index
                }
            }
            return _memberByName.TryGetValue(name, out int found) && Files.TryGetValue(found, out byte[] bytes) ? bytes : null;
        }
    }
}
