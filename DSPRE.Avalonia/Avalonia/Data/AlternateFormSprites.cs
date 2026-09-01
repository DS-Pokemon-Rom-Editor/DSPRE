using System;
using System.Collections.Generic;
using System.Linq;
using DSPRE;

namespace DSPRE.Avalonia.Data
{
    /// <summary>Which files in the alternate form archive belong to which form.</summary>
    public static class AlternateFormSprites
    {
        /// <summary>One form: what it is called and the four files that make it up.</summary>
        public sealed class Form
        {
            public string Name;
            public int BackSpriteIndex;
            public int FrontSpriteIndex;
            public int NormalPaletteIndex;
            public int ShinyPaletteIndex;
            /// <summary>Set for an hg-engine form that has no place in the vanilla archive at all, which
            /// is read straight from that species instead.</summary>
            public int HgEngineSpeciesId = -1;

            public Form(string name, int backIdx, int frontIdx, int normalPal, int shinyPal)
            {
                Name = name; BackSpriteIndex = backIdx; FrontSpriteIndex = frontIdx;
                NormalPaletteIndex = normalPal; ShinyPaletteIndex = shinyPal;
            }

            public Form(string name, int hgEngineSpeciesId)
            {
                Name = name;
                BackSpriteIndex = FrontSpriteIndex = NormalPaletteIndex = ShinyPaletteIndex = -1;
                HgEngineSpeciesId = hgEngineSpeciesId;
            }
        }

        /// <summary>The table for whichever game is open.</summary>
        public static Form[] ForCurrentGame() => RomInfo.gameFamily switch
        {
            RomInfo.GameFamilies.DP => GetFormDataDP(),
            RomInfo.GameFamilies.Plat => GetFormDataPt(),
            _ => GetFormDataHGSS(),
        };

        /// <summary>The colours a form's drawing uses. Both its drawings share one set, and the only
        /// other set is the shiny one, so there is nothing to work out from the file's position.</summary>
        public static int ColoursFor(int fileIndex, bool shiny)
        {
            foreach (var f in ForCurrentGame())
            {
                if (f.HgEngineSpeciesId >= 0) continue;
                if (fileIndex == f.BackSpriteIndex || fileIndex == f.FrontSpriteIndex)
                    return shiny ? f.ShinyPaletteIndex : f.NormalPaletteIndex;
            }
            return -1;
        }

        /// <summary>The forms, each as its four files, plus whatever the table does not account for.</summary>
        public static List<GraphicAssets.Unit> UnitsFor(GraphicAssets.Archive archive, int fileCount)
        {
            var units = new List<GraphicAssets.Unit>();
            var spokenFor = new HashSet<int>();

            // Several forms can share one pair of drawings: the table gives Egg and Bad Egg the same art,
            // and listing them as separate rows showing the same picture helps nobody.
            var byDrawing = new Dictionary<(int, int), List<Form>>();
            foreach (var f in ForCurrentGame())
            {
                if (f.HgEngineSpeciesId >= 0) continue;     // no place in this archive at all
                var key = (f.BackSpriteIndex, f.FrontSpriteIndex);
                if (!byDrawing.TryGetValue(key, out var list)) byDrawing[key] = list = new List<Form>();
                list.Add(f);
            }

            foreach (var group in byDrawing.Values)
            {
                var first = group[0];
                var names = group.Select(g => g.Name).Distinct().ToList();
                var u = new GraphicAssets.Unit
                {
                    Archive = archive,
                    Name = names.Count == 1 ? names[0] : string.Join(" / ", names),
                };
                void Add(int index, string what)
                {
                    if (index < 0 || index >= fileCount) return;
                    if (u.Parts.Any(x => x.Index == index)) return;
                    u.Parts.Add(new GraphicAssets.UnitPart { Archive = archive, Index = index, Name = what });
                    spokenFor.Add(index);
                }
                Add(first.BackSpriteIndex, "Back");
                Add(first.FrontSpriteIndex, "Front");
                foreach (var g in group)
                {
                    Add(g.NormalPaletteIndex, "Colours");
                    Add(g.ShinyPaletteIndex, "Shiny colours");
                }
                if (u.Parts.Count > 0) units.Add(u);
            }

            // Anything the table does not name still gets a row of its own rather than disappearing.
            for (int i = 0; i < fileCount; i++)
            {
                if (spokenFor.Contains(i)) continue;
                var u = new GraphicAssets.Unit { Archive = archive, Name = archive.Title };
                u.Parts.Add(new GraphicAssets.UnitPart { Archive = archive, Index = i, Name = "File " + i });
                units.Add(u);
            }

            units.Sort((x, y) => x.First.CompareTo(y.First));
            return units;
        }

        /// <summary>The form a given file belongs to, and which of its four parts that file is.</summary>
        public static (Form Form, string Part)? WhoOwns(int fileIndex)
        {
            foreach (var f in ForCurrentGame())
            {
                if (f.HgEngineSpeciesId >= 0) continue;
                if (fileIndex == f.BackSpriteIndex) return (f, "Back");
                if (fileIndex == f.FrontSpriteIndex) return (f, "Front");
                if (fileIndex == f.NormalPaletteIndex) return (f, "Colours");
                if (fileIndex == f.ShinyPaletteIndex) return (f, "Shiny colours");
            }
            return null;
        }

        private static Form[] GetFormDataDP() => new Form[]
        {
            new("Deoxys - Normal",   0,  1, 134, 135),
            new("Deoxys - Attack",   2,  3, 134, 135),
            new("Deoxys - Defense",  4,  5, 134, 135),
            new("Deoxys - Speed",    6,  7, 134, 135),
            new("Unown - A",  8,  9, 136, 137), new("Unown - B", 10, 11, 136, 137),
            new("Unown - C", 12, 13, 136, 137), new("Unown - D", 14, 15, 136, 137),
            new("Unown - E", 16, 17, 136, 137), new("Unown - F", 18, 19, 136, 137),
            new("Unown - G", 20, 21, 136, 137), new("Unown - H", 22, 23, 136, 137),
            new("Unown - I", 24, 25, 136, 137), new("Unown - J", 26, 27, 136, 137),
            new("Unown - K", 28, 29, 136, 137), new("Unown - L", 30, 31, 136, 137),
            new("Unown - M", 32, 33, 136, 137), new("Unown - N", 34, 35, 136, 137),
            new("Unown - O", 36, 37, 136, 137), new("Unown - P", 38, 39, 136, 137),
            new("Unown - Q", 40, 41, 136, 137), new("Unown - R", 42, 43, 136, 137),
            new("Unown - S", 44, 45, 136, 137), new("Unown - T", 46, 47, 136, 137),
            new("Unown - U", 48, 49, 136, 137), new("Unown - V", 50, 51, 136, 137),
            new("Unown - W", 52, 53, 136, 137), new("Unown - X", 54, 55, 136, 137),
            new("Unown - Y", 56, 57, 136, 137), new("Unown - Z", 58, 59, 136, 137),
            new("Unown - !", 60, 61, 136, 137), new("Unown - ?", 62, 63, 136, 137),
            new("Castform - Normal", 64, 68, 138, 142),
            new("Castform - Sunny",  65, 69, 139, 143),
            new("Castform - Rainy",  66, 70, 140, 144),
            new("Castform - Snowy",  67, 71, 141, 145),
            new("Burmy - Plant", 72, 73, 146, 147),
            new("Burmy - Sandy", 74, 75, 148, 149),
            new("Burmy - Trash", 76, 77, 150, 151),
            new("Wormadam - Plant", 78, 79, 152, 153),
            new("Wormadam - Sandy", 80, 81, 154, 155),
            new("Wormadam - Trash", 82, 83, 156, 157),
            new("Shellos - West",   84, 86, 158, 159),
            new("Shellos - East",   85, 87, 160, 161),
            new("Gastrodon - West", 88, 90, 162, 163),
            new("Gastrodon - East", 89, 91, 164, 165),
            new("Cherrim - Overcast",  92, 94, 166, 168),
            new("Cherrim - Sunshine",  93, 95, 167, 169),
            new("Arceus - Normal",   96,  97, 170, 171),
            new("Arceus - Fighting", 98,  99, 172, 173),
            new("Arceus - Flying",  100, 101, 174, 175),
            new("Arceus - Poison",  102, 103, 176, 177),
            new("Arceus - Ground",  104, 105, 178, 179),
            new("Arceus - Rock",    106, 107, 180, 181),
            new("Arceus - Bug",     108, 109, 182, 183),
            new("Arceus - Ghost",   110, 111, 184, 185),
            new("Arceus - Steel",   112, 113, 186, 187),
            new("Arceus - ???",     114, 115, 188, 189),
            new("Arceus - Fire",    116, 117, 190, 191),
            new("Arceus - Water",   118, 119, 192, 193),
            new("Arceus - Grass",   120, 121, 194, 195),
            new("Arceus - Electric",122, 123, 196, 197),
            new("Arceus - Psychic", 124, 125, 198, 199),
            new("Arceus - Ice",     126, 127, 200, 201),
            new("Arceus - Dragon",  128, 129, 202, 203),
            new("Arceus - Dark",    130, 131, 204, 205),
            new("Egg - Normal",  132, 132, 206, 206),
            new("Egg - Manaphy", 133, 133, 207, 207),
            new("Bad Egg - Normal", 132, 132, 206, 206),
        };

        private static Form[] GetFormDataPt() => new Form[]
        {
            new("Deoxys - Normal",   0,  1, 154, 155),
            new("Deoxys - Attack",   2,  3, 154, 155),
            new("Deoxys - Defense",  4,  5, 154, 155),
            new("Deoxys - Speed",    6,  7, 154, 155),
            new("Unown - A",  8,  9, 156, 157), new("Unown - B", 10, 11, 156, 157),
            new("Unown - C", 12, 13, 156, 157), new("Unown - D", 14, 15, 156, 157),
            new("Unown - E", 16, 17, 156, 157), new("Unown - F", 18, 19, 156, 157),
            new("Unown - G", 20, 21, 156, 157), new("Unown - H", 22, 23, 156, 157),
            new("Unown - I", 24, 25, 156, 157), new("Unown - J", 26, 27, 156, 157),
            new("Unown - K", 28, 29, 156, 157), new("Unown - L", 30, 31, 156, 157),
            new("Unown - M", 32, 33, 156, 157), new("Unown - N", 34, 35, 156, 157),
            new("Unown - O", 36, 37, 156, 157), new("Unown - P", 38, 39, 156, 157),
            new("Unown - Q", 40, 41, 156, 157), new("Unown - R", 42, 43, 156, 157),
            new("Unown - S", 44, 45, 156, 157), new("Unown - T", 46, 47, 156, 157),
            new("Unown - U", 48, 49, 156, 157), new("Unown - V", 50, 51, 156, 157),
            new("Unown - W", 52, 53, 156, 157), new("Unown - X", 54, 55, 156, 157),
            new("Unown - Y", 56, 57, 156, 157), new("Unown - Z", 58, 59, 156, 157),
            new("Unown - !", 60, 61, 156, 157), new("Unown - ?", 62, 63, 156, 157),
            new("Castform - Normal", 64, 68, 158, 162),
            new("Castform - Sunny",  65, 69, 159, 163),
            new("Castform - Rainy",  66, 70, 160, 164),
            new("Castform - Snowy",  67, 71, 161, 165),
            new("Burmy - Plant", 72, 73, 166, 167),
            new("Burmy - Sandy", 74, 75, 168, 169),
            new("Burmy - Trash", 76, 77, 170, 171),
            new("Wormadam - Plant", 78, 79, 172, 173),
            new("Wormadam - Sandy", 80, 81, 174, 175),
            new("Wormadam - Trash", 82, 83, 176, 177),
            new("Shellos - West",   84, 86, 178, 179),
            new("Shellos - East",   85, 87, 180, 181),
            new("Gastrodon - West", 88, 90, 182, 183),
            new("Gastrodon - East", 89, 91, 184, 185),
            new("Cherrim - Overcast", 92, 94, 186, 188),
            new("Cherrim - Sunshine", 93, 95, 187, 189),
            new("Arceus - Normal",   96,  97, 190, 191),
            new("Arceus - Fighting", 98,  99, 192, 193),
            new("Arceus - Flying",  100, 101, 194, 195),
            new("Arceus - Poison",  102, 103, 196, 197),
            new("Arceus - Ground",  104, 105, 198, 199),
            new("Arceus - Rock",    106, 107, 200, 201),
            new("Arceus - Bug",     108, 109, 202, 203),
            new("Arceus - Ghost",   110, 111, 204, 205),
            new("Arceus - Steel",   112, 113, 206, 207),
            new("Arceus - ???",     114, 115, 208, 209),
            new("Arceus - Fire",    116, 117, 210, 211),
            new("Arceus - Water",   118, 119, 212, 213),
            new("Arceus - Grass",   120, 121, 214, 215),
            new("Arceus - Electric",122, 123, 216, 217),
            new("Arceus - Psychic", 124, 125, 218, 219),
            new("Arceus - Ice",     126, 127, 220, 221),
            new("Arceus - Dragon",  128, 129, 222, 223),
            new("Arceus - Dark",    130, 131, 224, 225),
            new("Egg - Normal",  132, 132, 226, 226),
            new("Egg - Manaphy", 133, 133, 227, 227),
            new("Bad Egg - Normal", 132, 132, 226, 226),
            new("Shaymin - Land", 134, 135, 228, 229),
            new("Shaymin - Sky",  136, 137, 230, 231),
            new("Rotom - Normal", 138, 139, 232, 233),
            new("Rotom - Heat",   140, 141, 234, 235),
            new("Rotom - Wash",   142, 143, 236, 237),
            new("Rotom - Frost",  144, 145, 238, 239),
            new("Rotom - Fan",    146, 147, 240, 241),
            new("Rotom - Mow",    148, 149, 242, 243),
            new("Giratina - Altered", 150, 151, 244, 245),
            new("Giratina - Origin",  152, 153, 246, 247),
        };

        private static Form[] GetFormDataHGSS() => new Form[]
        {
            new("Deoxys - Normal",   0,  1, 158, 159),
            new("Deoxys - Attack",   2,  3, 158, 159),
            new("Deoxys - Defense",  4,  5, 158, 159),
            new("Deoxys - Speed",    6,  7, 158, 159),
            new("Unown - A",  8,  9, 160, 161), new("Unown - B", 10, 11, 160, 161),
            new("Unown - C", 12, 13, 160, 161), new("Unown - D", 14, 15, 160, 161),
            new("Unown - E", 16, 17, 160, 161), new("Unown - F", 18, 19, 160, 161),
            new("Unown - G", 20, 21, 160, 161), new("Unown - H", 22, 23, 160, 161),
            new("Unown - I", 24, 25, 160, 161), new("Unown - J", 26, 27, 160, 161),
            new("Unown - K", 28, 29, 160, 161), new("Unown - L", 30, 31, 160, 161),
            new("Unown - M", 32, 33, 160, 161), new("Unown - N", 34, 35, 160, 161),
            new("Unown - O", 36, 37, 160, 161), new("Unown - P", 38, 39, 160, 161),
            new("Unown - Q", 40, 41, 160, 161), new("Unown - R", 42, 43, 160, 161),
            new("Unown - S", 44, 45, 160, 161), new("Unown - T", 46, 47, 160, 161),
            new("Unown - U", 48, 49, 160, 161), new("Unown - V", 50, 51, 160, 161),
            new("Unown - W", 52, 53, 160, 161), new("Unown - X", 54, 55, 160, 161),
            new("Unown - Y", 56, 57, 160, 161), new("Unown - Z", 58, 59, 160, 161),
            new("Unown - !", 60, 61, 160, 161), new("Unown - ?", 62, 63, 160, 161),
            new("Castform - Normal", 64, 68, 162, 166),
            new("Castform - Sunny",  65, 69, 163, 167),
            new("Castform - Rainy",  66, 70, 164, 168),
            new("Castform - Snowy",  67, 71, 165, 169),
            new("Burmy - Plant", 72, 73, 170, 171),
            new("Burmy - Sandy", 74, 75, 172, 173),
            new("Burmy - Trash", 76, 77, 174, 175),
            new("Wormadam - Plant", 78, 79, 176, 177),
            new("Wormadam - Sandy", 80, 81, 178, 179),
            new("Wormadam - Trash", 82, 83, 180, 181),
            new("Shellos - West",   84, 86, 182, 183),
            new("Shellos - East",   85, 87, 184, 185),
            new("Gastrodon - West", 88, 90, 186, 187),
            new("Gastrodon - East", 89, 91, 188, 189),
            new("Cherrim - Overcast", 92, 94, 190, 192),
            new("Cherrim - Sunshine", 93, 95, 191, 193),
            new("Arceus - Normal",   96,  97, 194, 195),
            new("Arceus - Fighting", 98,  99, 196, 197),
            new("Arceus - Flying",  100, 101, 198, 199),
            new("Arceus - Poison",  102, 103, 200, 201),
            new("Arceus - Ground",  104, 105, 202, 203),
            new("Arceus - Rock",    106, 107, 204, 205),
            new("Arceus - Bug",     108, 109, 206, 207),
            new("Arceus - Ghost",   110, 111, 208, 209),
            new("Arceus - Steel",   112, 113, 210, 211),
            new("Arceus - ???",     114, 115, 212, 213),
            new("Arceus - Fire",    116, 117, 214, 215),
            new("Arceus - Water",   118, 119, 216, 217),
            new("Arceus - Grass",   120, 121, 218, 219),
            new("Arceus - Electric",122, 123, 220, 221),
            new("Arceus - Psychic", 124, 125, 222, 223),
            new("Arceus - Ice",     126, 127, 224, 225),
            new("Arceus - Dragon",  128, 129, 226, 227),
            new("Arceus - Dark",    130, 131, 228, 229),
            new("Egg - Normal",  132, 132, 230, 230),
            new("Egg - Manaphy", 133, 133, 231, 231),
            new("Bad Egg - Normal", 132, 132, 230, 230),
            new("Shaymin - Land", 134, 135, 232, 233),
            new("Shaymin - Sky",  136, 137, 234, 235),
            new("Rotom - Normal", 138, 139, 236, 237),
            new("Rotom - Heat",   140, 141, 238, 239),
            new("Rotom - Wash",   142, 143, 240, 241),
            new("Rotom - Frost",  144, 145, 242, 243),
            new("Rotom - Fan",    146, 147, 244, 245),
            new("Rotom - Mow",    148, 149, 246, 247),
            new("Giratina - Altered", 150, 151, 248, 249),
            new("Giratina - Origin",  152, 153, 250, 251),
            new("Pichu - Normal",    154, 155, 252, 253),
            new("Pichu - Spiky-ear", 156, 157, 254, 255),
        };
    }
}
