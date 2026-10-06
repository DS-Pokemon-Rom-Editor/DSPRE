using System.Collections.Generic;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// What each particle file outside the per-move files is for, named from the code that loads it.
    /// Files the game never loads go under Unused rather than being guessed at.
    /// </summary>
    public static class ParticleFileNames
    {
        public const string Moves = "Moves", BattleEffects = "Battle effects", Balls = "Poké Balls",
                            Seals = "Ball Capsule seals", Intros = "Battle intros", Story = "Story scenes",
                            Menus = "Menus and minigames", Frontier = "Battle Frontier", Unused = "Unused",
                            Other = "Other";

        /// <summary>The categories in the order the library lists them.</summary>
        public static readonly string[] Order = { Moves, BattleEffects, Balls, Seals, Intros, Story, Menus, Frontier, Unused, Other };

        /// <summary>The leading files of the move archive, which effect scripts and battle code load rather than moves.</summary>
        public static (string Category, string Name)? MoveArchive(GameFamilies family, int file)
        {
            bool hgss = family == GameFamilies.HGSS;
            if (hgss && file == 31) return (BattleEffects, "Shiny sparkles");
            if (file < 0 || file > 30) return null;
            string[] terrain = { "grass", "water", "open ground", "indoors", "mountain", "snow", "cave", "puddle", "ice", "sand", "Great Marsh" };
            if (file >= 5 && file <= 26)
            {
                int t = (file - 5) / 2;
                string part = (file - 5) % 2 == 0 ? "part 1" : "part 2";
                // HGSS has no Great Marsh terrain, so its pair is never loaded there.
                if (hgss && t == 10) return (Unused, $"Battle start: Great Marsh, {part} (not used in this game)");
                return (BattleEffects, $"Battle start: {terrain[t]}, {part}");
            }
            switch (file)
            {
                case 0: return (BattleEffects, "Hit spark (Secret Power)");
                case 1: return (Unused, "Hit spark 2 (never loaded)");
                case 2: return (BattleEffects, "Level up");
                case 3: return (Unused, "Grass battle start test 1 (never loaded)");
                case 4: return (Unused, "Grass battle start test 2 (never loaded)");
                case 27: return (BattleEffects, "Status and healing (sleep, poison, burn, freeze, confusion, love, HP restore)");
                case 28: return (BattleEffects, "Item use");
                case 29: return (BattleEffects, "Weather: fog");
                case 30: return (Unused, "Turn damage (never loaded)");
            }
            return null;
        }

        /// <summary>Ball archive files that are not openings or seals: catches, the recall and the unused defaults.</summary>
        public static (string Category, string Name)? BallArchive(GameFamilies family, int file, IReadOnlyDictionary<int, string> ballNames)
        {
            bool hgss = family == GameFamilies.HGSS;
            int balls = hgss ? 25 : 17;              // the last id is the Park Ball
            int parkOpening = hgss ? 25 : 17, catchDefault = hgss ? 26 : 18, recall = hgss ? 52 : 36;
            string Ball(int id) => id == balls ? "Park Ball" : ballNames != null && ballNames.TryGetValue(id, out string n) ? n : "Ball " + id;

            if (file == 0) return (Unused, "Default ball opening (never loaded)");
            if (file == parkOpening) return (Balls, "Park Ball opening");
            if (file == catchDefault) return (Unused, "Default catch stars (never loaded)");
            if (file > catchDefault && file <= catchDefault + balls) return (Balls, Ball(file - catchDefault) + " caught stars");
            if (file == recall) return (Balls, "Pokémon returning to its ball");
            return null;
        }

        /// <summary>A file in one of the smaller particle archives, found by the archive's path.</summary>
        public static (string Category, string Name) Loose(GameFamilies family, string relative, int member)
        {
            string key = ArchiveKey(family, relative);
            switch (key)
            {
                case "particledata":
                    switch (member)
                    {
                        case 2: return (Intros, "VS intro, link battle: sparks");
                        case 3: return family == GameFamilies.HGSS ? (Unused, "Contest Dance leftover (never loaded)") : (Menus, "Contest Dance: flashes and hits");
                        case 4: return (Story, "Opening: starter cut-ins");
                        case 0: return (Unused, "Field test sparks (never loaded)");
                        case 1: return (Unused, "Rock break test (never loaded)");
                        case 5: return (Unused, "Title logo sparkle (never loaded)");
                    }
                    break;
                case "egg":
                    if (member == 0) return (Story, "Egg hatching: shell pieces and sparkles");
                    if (member == 1) return (Story, "Egg hatching: Manaphy egg glow");
                    break;
                case "evolution":
                    if (member == 0) return (Story, "Evolution: light and sparkles around the Pokémon");
                    if (member == 1) return (Unused, "Evolution placeholder (never loaded)");
                    break;
                case "encounter":
                    if (member == (family == GameFamilies.HGSS ? 151 : 107)) return (Intros, "VS intro, Elite Four and Champion: first burst");
                    if (member == (family == GameFamilies.HGSS ? 152 : 108)) return (Intros, "VS intro, Elite Four and Champion: second burst");
                    break;
                case "frontier":
                    switch (member)
                    {
                        case 0: return (Unused, "Battle Frontier test 1 (never loaded)");
                        case 1: return (Unused, "Battle Frontier test 2 (never loaded)");
                        case 2: return (Frontier, "Battle Hall: spotlights, confetti and camera flashes");
                        case 3: return (Frontier, "Battle Arcade: game board lights");
                        case 4: return (Frontier, "Battle Arcade: Dahlia's entrance");
                        case 5: return (Frontier, "Battle Factory: green smoke and rising blocks");
                        case 6: return (Frontier, "Battle Castle: sparkles");
                    }
                    break;
                case "pokelist":
                    if (member == 0) return (Menus, "Form change: Giratina");
                    if (member == 1) return (Menus, "Form change: Shaymin Sky Forme");
                    break;
                case "pl_etc":
                    if (member == 0) return (Menus, "Wobbuffet Pop (Wi-Fi Plaza): balloon bursts");
                    if (member == 1) return (Unused, "Title sparkle copy (never loaded)");
                    break;
                case "wlmngm":
                    if (member == 19) return (Unused, "Wi-Fi Plaza winner effect (never loaded)");
                    break;
                case "debug":
                    return (Unused, $"Debug particles {member + 1} (never loaded)");
                case "aprijuice":
                    if (member == 10) return (Menus, "Aprijuice: giving juice to a Pokémon");
                    break;
            }
            return (Other, $"{relative}, file {member}");
        }

        // HeartGold and SoulSilver name their archives by number, so the path says nothing about the contents.
        private static readonly Dictionary<string, string> HgssArchives = new()
        {
            ["a/0/5/9"] = "particledata", ["a/0/9/6"] = "debug", ["a/1/0/9"] = "encounter", ["a/1/1/6"] = "egg",
            ["a/1/1/9"] = "evolution", ["a/1/8/8"] = "frontier", ["a/1/9/8"] = "wlmngm", ["a/2/0/6"] = "pokelist",
            ["a/2/1/1"] = "pl_etc", ["a/2/4/2"] = "aprijuice",
        };

        private static string ArchiveKey(GameFamilies family, string relative)
        {
            string r = relative.Replace('\\', '/').Trim('/').ToLowerInvariant();
            if (family == GameFamilies.HGSS && HgssArchives.TryGetValue(r, out string key)) return key;
            if (r.Contains("egg_demo")) return "egg";
            if (r.Contains("shinka")) return "evolution";
            if (r.Contains("encounteffect")) return "encounter";
            if (r.Contains("frontier")) return "frontier";
            if (r.Contains("pokelist")) return "pokelist";
            if (r.Contains("pl_etc")) return "pl_etc";
            if (r.Contains("wifi_lobby") || r.Contains("wlmngm")) return "wlmngm";
            if (r.Contains("debug")) return "debug";
            if (r.Contains("particledata")) return "particledata";
            return "";
        }
    }
}
