using System;
using System.Collections.Generic;
using System.Linq;
using DSPRE;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Which battle scenery each place in the game uses.
    ///
    /// A battle backdrop is not picked when the battle starts: every map header carries the number of the
    /// backdrop that place fights on, so the graphic and the game data are two halves of one thing. Seeing
    /// the drawing without knowing which places use it, or the number without seeing the drawing, is half
    /// the picture either way.
    ///
    /// The ground the Pokemon stand on is separate and comes from what you are standing on rather than
    /// from the header, so it is offered as a choice rather than tied to a backdrop.
    /// </summary>
    public static class BattleScenes
    {
        public sealed class Scene
        {
            /// <summary>The number a map header carries to ask for this scenery.</summary>
            public int BackgroundId;
            /// <summary>The headers that fight here, by number.</summary>
            public List<int> Headers = new();
            /// <summary>Those headers by name, as far as the game names them.</summary>
            public List<string> PlaceNames = new();

            public string Label => $"{BackgroundId,3}   {Where}";

            public string Where
            {
                get
                {
                    if (Headers.Count == 0) return "spare, no place uses it";
                    string first = PlaceNames.FirstOrDefault() ?? ("header " + Headers[0]);
                    return Headers.Count == 1 ? first : $"{first} and {Headers.Count - 1} more";
                }
            }

            /// <summary>The files this scenery is drawn from.</summary>
            public int Drawing, Arrangement, PaletteDay;
        }

        /// <summary>Reads every header and gathers which scenery each place fights on.</summary>
        public static List<Scene> Read()
        {
            var byId = new Dictionary<int, Scene>();

            // The internal names are codes like D02 and R213. Every header also carries the name the game
            // shows the player, so use that where there is one and fall back to the code where there is
            // not: "Route 213" beats "R213" for somebody deciding which scenery to change.
            int headers = 0;
            try { headers = RomInfo.GetHeaderCount(); } catch { }

            List<string> internalNames = null, placeNames = null;
            try { internalNames = HeaderLists.GetHeaderListBoxNames(); } catch { }
            try { placeNames = RomInfo.GetLocationNames(); } catch { }

            bool dynamic = false;
            try { dynamic = RomInfo.gameDirs.ContainsKey(DirNames.dynamicHeaders); } catch { }

            // The displayed-name lookup only works properly in some games. Measured over every header:
            // Diamond answers for all 559, Platinum for 12 of 593 and HeartGold for 3 of 540, and the few
            // it does answer there are wrong, which is how nine different places all came out as Route
            // 201. So it is used only when it answers for nearly every header, and the internal codes are
            // used throughout otherwise. A name that is wrong is worse than a code that is terse.
            bool trustDisplayedNames = false;
            if (placeNames != null && headers > 0)
            {
                int answered = 0;
                for (ushort i = 0; i < headers; i++)
                {
                    try
                    {
                        if (MapHeader.TryReadLocationNameIndex(i, dynamic, out int at)
                            && at >= 0 && at < placeNames.Count) answered++;
                    }
                    catch { }
                }
                trustDisplayedNames = answered * 10 >= headers * 9;
            }

            string NameOfPlace(ushort id)
            {
                if (trustDisplayedNames && placeNames != null)
                {
                    try
                    {
                        if (MapHeader.TryReadLocationNameIndex(id, dynamic, out int at)
                            && at >= 0 && at < placeNames.Count)
                        {
                            string shown = placeNames[at]?.Trim();
                            if (!string.IsNullOrEmpty(shown) && shown.Trim('-').Length > 0) return shown;
                        }
                    }
                    catch { }
                }
                if (internalNames != null && id < internalNames.Count)
                {
                    string code = internalNames[id]?.Trim();
                    if (!string.IsNullOrWhiteSpace(code)) return code;
                }
                return null;
            }

            for (ushort i = 0; i < headers; i++)
            {
                MapHeader h;
                try { h = MapHeader.LoadFromARM9(i); } catch { continue; }
                if (h == null) continue;

                int id = h.battleBackground;
                if (!byId.TryGetValue(id, out var scene))
                {
                    var files = BattleBgRenderer.BackdropFiles(id);
                    byId[id] = scene = new Scene
                    {
                        BackgroundId = id,
                        Drawing = files.Drawing,
                        Arrangement = files.Tilemap,
                        PaletteDay = files.PaletteDay,
                    };
                }
                scene.Headers.Add(i);
                string where = NameOfPlace(i);
                if (where != null) scene.PlaceNames.Add(where);
            }

            // Scenery the games ship but no place asks for still belongs in the list: somebody adding a
            // map may want it, and leaving it out would look like it does not exist.
            for (int id = 0; id < BattleBgRenderer.BackdropCount; id++)
            {
                if (byId.ContainsKey(id)) continue;
                var files = BattleBgRenderer.BackdropFiles(id);
                byId[id] = new Scene
                {
                    BackgroundId = id,
                    Drawing = files.Drawing,
                    Arrangement = files.Tilemap,
                    PaletteDay = files.PaletteDay,
                };
            }

            return byId.Values.OrderBy(s => s.BackgroundId).ToList();
        }
    }
}
