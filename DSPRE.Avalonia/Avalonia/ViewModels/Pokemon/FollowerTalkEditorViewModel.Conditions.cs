using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;
using Slot = DSPRE.ROMFiles.FollowerTalk.Slot;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    public partial class FollowerTalkEditorViewModel
    {
        /// <summary>Slot conditions and what each value means in game.</summary>
        public static class Conditions
        {
            public sealed record Field(string Label, Func<Slot, int> Get, Action<Slot, int> Set, int Max,
                Dictionary<int, string> Names, Func<int, bool> Never = null, string Help = null, Func<Dictionary<int, string>> FromRom = null)
            {
                /// <summary>The named values, read from the ROM for maps and tiles.</summary>
                public Dictionary<int, string> NamesNow() => FromRom?.Invoke() ?? Names;
            }

            // Read once per load.
            private static readonly Dictionary<int, string> MapNamesRead = new Dictionary<int, string>();
            private static readonly Dictionary<int, string> TileNamesRead = new Dictionary<int, string>();

            static Conditions() => ProjectSession.ClearOnLoad(() => { MapNamesRead.Clear(); TileNamesRead.Clear(); });

            private static void ReadNames()
            {
                if (TileNamesRead.Count > 0) return;
                IReadOnlyList<string> headers = HeaderLabels.Friendly();
                for (int h = 0; h < headers.Count; h++) MapNamesRead[h + 1] = $"{h}: {headers[h]}";
                for (int v = 1; v < 256; v++) TileNamesRead[v] = $"{v:X2} {TilePermissions.BehaviourName((byte)v, GameFamily)}";
            }

            private static Dictionary<int, string> MapNames() { ReadNames(); return MapNamesRead; }
            private static Dictionary<int, string> TileNames() { ReadNames(); return TileNamesRead; }

            private static Dictionary<int, string> Named(params string[] names)
            {
                Dictionary<int, string> d = new Dictionary<int, string>();
                for (int i = 0; i < names.Length; i++) d[i + 1] = names[i];
                return d;
            }

            public static readonly string[] Types = { "Normal", "Fire", "Water", "Electric", "Grass", "Ice", "Fighting", "Poison", "Ground", "Flying", "Psychic", "Bug", "Rock", "Ghost", "Dragon", "Dark", "Steel" };

            public static readonly List<Field> All = new List<Field>
            {
                new("HP", s => s.Hp, (s, v) => s.Hp = v, 255, Named("Full", "75% or more", "50% or more", "25% or more", "Below 25%"), v => v > 5,
                    "The follower's HP against its max."),
                new("Friendship", s => s.Friendship, (s, v) => s.Friendship = v, 15, Named("255", "200 to 254", "150 to 199", "90 to 149", "60 to 89", "30 to 59", "1 to 29", "0", "90 or more", "Below 60"), v => v > 10,
                    "The follower's friendship, out of 255."),
                new("Mood", s => s.Mood, (s, v) => s.Mood = v, 15, Named("127", "100 to 126", "50 to 99", "30 to 49", "-29 to 29", "-49 to -30", "-126 to -50", "-127", "0 or more", "Below 0"), v => v > 10,
                    "A hidden mood from -127 to 127 that only counts while the Pokémon follows the player. Reactions raise and lower it."),
                new("Status", s => s.Status, (s, v) => s.Status = v, 7, Named("Healthy", "Burned", "Frozen", "Paralysed", "Poisoned", "Fainted", "Any problem"), v => v == 6,
                    "The follower's status problem. A sleeping follower only matches Any problem, and Fainted never matches because a fainted Pokémon can't follow the player."),
                new("Held item", s => s.HeldItem, (s, v) => s.HeldItem = v, 31, Named("Ball", "Medicine", "Battle item", "Item", "Mail", "Berry", "TM", "Nothing", "Anything"), v => v > 9,
                    "The bag pocket of the follower's held item. A held key item matches both Nothing and Anything."),
                new("Type", s => s.Type, (s, v) => s.Type = v, 31, Named(Types), v => v > 17,
                    "Matches if either of the follower's types is this one."),
                new("Nature group", s => s.Nature, (s, v) => s.Nature = v, 7, Named("Cheerful", "Calm", "Careful", "Bold", "Lonely", "Easygoing"), v => v > 6,
                    "The game sorts natures into six groups. Cheerful: Naughty, Impish, Jolly, Naive, Sassy. Calm: Relaxed, Lax, Calm, Gentle. Careful: Docile, Serious, Modest, Quiet, Careful. Bold: Hardy, Brave, Adamant, Bold. Lonely: Lonely, Timid, Bashful. Easygoing: Hasty, Mild, Rash, Quirky."),
                new("Gender", s => s.Gender, (s, v) => s.Gender = v, 3, Named("Male", "Female", "No gender"), v => v == 3,
                    "Genderless followers count as female, so No gender never matches."),
                new("Best Pokéathlon stat", s => s.Pokeathlon, (s, v) => s.Pokeathlon = v, 7, Named("Power", "Speed", "Stamina", "Jump", "Skill"), v => v > 5,
                    "The follower's highest Pokéathlon stat. A tie goes to the first in this order: Power, Speed, Jump, Stamina, Skill."),
                new("Time", s => s.Time, (s, v) => s.Time = v, 7, Named("Morning", "Day", "Evening", "Night", "Late night"), v => v > 5,
                    "The time of day on the game clock."),
                new("Weather", s => s.Weather, (s, v) => s.Weather = v, 7, Named("Clear", "Cloudy", "Rain", "Heavy rain", "Thunder"), v => v != 1 && v != 3,
                    "The weather on the current map. The game only reports clear and rain, so Cloudy, Heavy rain and Thunder never match."),
                new("Facing", s => s.Facing, (s, v) => s.Facing = v, 7, Named("Right", "Left", "Up", "Down"), v => v > 4,
                    "The way the follower faces when talked to."),
                new("People next to the player", s => s.People, (s, v) => s.People = v, 7, Named("1", "2", "3", "4", "5 or more"), v => v > 5,
                    "People and objects on the eight tiles around the player. The follower, smashable rocks, Strength boulders and cuttable trees don't count."),
                new("Hidden items on the map", s => s.HiddenItems, (s, v) => s.HiddenItems = v, 7, Named("1", "2", "3", "4 or more"), v => v > 4,
                    "Hidden items on the current map that haven't been found yet."),
                new("Encounter tile", s => s.EncounterTile, (s, v) => s.EncounterTile = v, 255, Named("Yes", "No"), v => v > 2,
                    "Whether the follower stands where wild Pokémon can appear, like tall grass."),
                new("Species group", s => s.SpeciesGroup, (s, v) => s.SpeciesGroup = v, 255,
                    new Dictionary<int, string> { [250] = "Groups 0 to 19", [251] = "Groups 0 to 130", [252] = "Groups 140 to 149", [253] = "Groups 160 and up", [254] = "Groups 220 and up" }, null,
                    "The follower's group from the Species groups tab, or a range of groups."),
                new("Shiny Leaf missing", s => s.Leaf, (s, v) => s.Leaf = v, 255, Named("Leaf 1", "Leaf 2", "Leaf 3", "Leaf 4", "Leaf 5"), null,
                    "Matches until the follower has this Shiny Leaf, so a reaction that gives the leaf only plays once."),
                new("Map", s => s.Map, (s, v) => s.Map = v, 65535, new Dictionary<int, string>(), null,
                    "Only on this map.", MapNames),
                new("Tile behaviour", s => s.Tile, (s, v) => s.Tile = v, 65535, new Dictionary<int, string>(), null,
                    "The kind of tile the follower stands on, like shallow water or a puddle.", TileNames),
                new("Event flag set", s => s.Flag, (s, v) => s.Flag = v, 65535, new Dictionary<int, string>(), null,
                    "Only once this flag is set."),
                new("Level", s => s.Level, (s, v) => s.Level = v, 3, new Dictionary<int, string>(), v => v > 0,
                    "Broken in the game: any value stops the slot."),
                new("Field move", s => s.FieldMove, (s, v) => s.FieldMove = v, 3, new Dictionary<int, string>(), v => v > 0,
                    "Broken in the game: any value stops the slot."),
            };

            public static IEnumerable<string> Describe(Slot s)
            {
                foreach (Field f in All)
                {
                    int v = f.Get(s);
                    if (v == 0) continue;
                    yield return f.Label + " " + (f.NamesNow().TryGetValue(v, out string n) ? n : v.ToString());
                }
            }
        }
    }
}
