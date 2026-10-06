using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;
using static DSPRE.ROMFiles.VsIntroTables;

namespace DSPRE.Avalonia.Data
{
    /// <summary>What to call the intros, classes, trainers, species and battle themes both intro editors list.</summary>
    public sealed class VsIntroNames
    {
        public string[] Trainers = Array.Empty<string>(), Classes = Array.Empty<string>(), Species = Array.Empty<string>();
        private Dictionary<int, string> _sequences = new();
        // Diamond and Pearl records name a class, not a trainer, so the first trainer of that class names them.
        private readonly Dictionary<int, int> _firstOfClass = new();

        /// <summary>Reads every name. Does file work, so call it off the UI thread.</summary>
        public static VsIntroNames Read()
        {
            VsIntroNames n = new VsIntroNames();
            try { n.Trainers = GetSimpleTrainerNames(); } catch { }
            try { n.Classes = GetTrainerClassNames(); } catch { }
            try { n.Species = GetPokemonNames(); } catch { }
            try
            {
                SdatArchive sdat = SoundArchive.Load();
                if (sdat?.SeqNames != null) n._sequences = new Dictionary<int, string>(sdat.SeqNames);
            }
            catch { }
            if (gameFamily == GameFamilies.DP)
            {
                try
                {
                    DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.trainerProperties });
                    for (int i = 1; i < n.Trainers.Length; i++)
                    {
                        string path = Filesystem.GetTrainerPropertiesPath(i);
                        if (!File.Exists(path)) continue;
                        byte[] b = File.ReadAllBytes(path);
                        if (b.Length > 1 && !n._firstOfClass.ContainsKey(b[1])) n._firstOfClass[b[1]] = i;
                    }
                }
                catch { }
            }
            return n;
        }

        private static string At(string[] list, int i, string fallback) =>
            i >= 0 && i < list.Length && !string.IsNullOrWhiteSpace(list[i]) ? list[i].Trim() : fallback;

        public string Trainer(int id) => At(Trainers, id, $"Trainer {id}");
        private HashSet<string> _sharedClassNames;

        // Many classes share a name (HeartGold has sixteen "Leader"s), so those get their number to tell them apart.
        public string Class(int id)
        {
            _sharedClassNames ??= Classes.Where(c => !string.IsNullOrWhiteSpace(c)).GroupBy(c => c.Trim())
                                         .Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
            string name = At(Classes, id, $"Class {id}");
            return _sharedClassNames.Contains(name) ? $"{name} (class {id})" : name;
        }
        public string SpeciesName(int id) => At(Species, id, $"Pokémon {id}");

        public string Sequence(int id)
        {
            if (!_sequences.TryGetValue(id, out string name) || string.IsNullOrEmpty(name)) return $"Theme {id}";
            string raw = name.StartsWith("SEQ_", StringComparison.Ordinal) ? name.Substring(4) : name;
            string plain = PlainTheme(raw);
            return plain == null ? raw : $"{plain} · {raw}";
        }

        // DP and Platinum abbreviate their battle themes; the meanings are the Platinum decomp's sequence names.
        private static readonly Dictionary<string, string> SinnohThemes = new(StringComparer.Ordinal)
        {
            ["BA_POKE"] = "Wild Pokémon battle", ["BA_GYM"] = "Gym Leader battle", ["BA_DPOKE1"] = "Lake guardian battle",
            ["BA_TRAIN"] = "Trainer battle", ["BA_AKAGI"] = "Cyrus battle", ["BA_DPOKE2"] = "Dialga and Palkia battle",
            ["BA_CHANP"] = "Champion battle", ["BA_GINGA"] = "Team Galactic battle", ["BA_RIVAL"] = "Rival battle",
            ["BA_SECRET1"] = "Arceus battle", ["BA_SECRET2"] = "Legendary battle", ["BA_GINGA3"] = "Galactic Commander battle",
            ["BA_TENNO"] = "Elite Four battle", ["BA_TOWER"] = "Battle Tower", ["PL_BA_GIRA"] = "Giratina battle",
            ["PL_BA_BRAIN"] = "Frontier Brain battle", ["PL_BA_REGI"] = "Regi trio battle",
        };

        private static readonly Dictionary<string, string> JohtoThemes = new(StringComparer.Ordinal)
        {
            ["VS_NORAPOKE"] = "Wild Pokémon battle", ["VS_TRAINER"] = "Trainer battle", ["VS_GYMREADER"] = "Gym Leader battle",
            ["VS_RIVAL"] = "Rival battle", ["VS_ROCKET"] = "Team Rocket battle", ["VS_SUICUNE"] = "Suicune battle",
            ["VS_ENTEI"] = "Entei battle", ["VS_RAIKOU"] = "Raikou battle", ["VS_CHAMP"] = "Champion battle",
            ["VS_HOUOU"] = "Ho-Oh battle", ["VS_LUGIA"] = "Lugia battle", ["VS_KODAI"] = "Groudon, Kyogre and Rayquaza battle",
            ["BA_BRAIN"] = "Frontier Brain battle", ["BATTLETOWER"] = "Battle Tower", ["BATTLETOWER2"] = "Battle Tower 2",
        };

        private static string PlainTheme(string raw)
        {
            if (SinnohThemes.TryGetValue(raw, out string s)) return s;
            if (!raw.StartsWith("GS_", StringComparison.Ordinal)) return null;
            string key = raw.Substring(3);
            // The P_ copies are what the GB Sounds item switches the music to.
            bool gb = key.StartsWith("P_", StringComparison.Ordinal);
            if (gb) key = key.Substring(2);
            bool kanto = key.EndsWith("_KANTO", StringComparison.Ordinal);
            if (kanto) key = key.Substring(0, key.Length - 6);
            if (!JohtoThemes.TryGetValue(key, out string j)) return null;
            return j + (kanto ? " (Kanto)" : "") + (gb ? " (GB Sounds)" : "");
        }

        /// <summary>The battle themes to offer, plus whatever the tables already use.</summary>
        public List<int> SequenceChoices(IEnumerable<int> used)
        {
            SortedSet<int> set = new SortedSet<int>(used);
            foreach (KeyValuePair<int, string> kv in _sequences)
            {
                string s = kv.Value ?? "";
                if (s.Contains("_VS_") || s.Contains("BATTLE") || s.Contains("_BA_")) set.Add(kv.Key);
            }
            return set.ToList();
        }

        /// <summary>Who a record's intro is for.</summary>
        public string Record(VsIntroTables t, Record r)
        {
            if (r == null) return "";
            if (r.Kind == RecordKind.Rival) return "Rival";
            if (r.Has(RecordField.TrainerId)) return Trainer(r.Get(RecordField.TrainerId));
            int cls = r.Get(RecordField.Class);
            return _firstOfClass.TryGetValue(cls, out int trainer) ? Trainer(trainer) : Class(cls);
        }

        /// <summary>What a combo is for: its record's trainer, what the battle setup uses it for, or who picks it.</summary>
        public string Combo(VsIntroTables t, int combo)
        {
            Record record = t.RecordFor(t.EffectOf(combo));
            if (record != null) return Record(t, record);
            bool dp = t.Family == GameFamilies.DP;
            switch (t.RoleOf(combo))
            {
                case ComboRole.Rival: return "Rival";
                case ComboRole.Frontier: return dp ? "Battle Tower" : "Battle Frontier";
                case ComboRole.Link: return "Link battles";
                case ComboRole.Double: return "Double battles";
                case ComboRole.FrontierBrain: return dp ? "Tower Tycoon" : "Frontier Brains";
                case ComboRole.DoubleLeader: return "Double battles with Volkner";
                case ComboRole.Ordinary: return "Ordinary trainers";
                case ComboRole.WildDouble: return "Wild double battles";
                case ComboRole.OrdinaryWild: return "Wild Pokémon";
            }
            List<string> classes = t.ClassesUsing(combo).Select(Class).Distinct().ToList();
            if (classes.Count > 0) return string.Join(", ", classes.Take(3)) + (classes.Count > 3 ? "…" : "");
            List<string> species = t.SpeciesUsing(combo).Select(SpeciesName).ToList();
            if (species.Count > 0) return string.Join(", ", species.Take(3)) + (species.Count > 3 ? "…" : "");
            return "Not used";
        }

        /// <summary>What an intro effect looks like.</summary>
        public static string Kind(VsIntroTables t, int effect)
        {
            bool hgss = t.Family == GameFamilies.HGSS;
            return t.KindOfEffect(effect) switch
            {
                IntroKind.Terrain or IntroKind.Generic => "Terrain transition",
                IntroKind.Gym => t.Family == GameFamilies.DP ? "Gym Leader face cut-in" : "Gym Leader mugshot",
                IntroKind.Rival => "Rival mugshot",
                IntroKind.League => t.Family == GameFamilies.DP ? "Elite Four face cut-in" : "Elite Four mugshot",
                IntroKind.Legendary => "Legendary Pokémon",
                IntroKind.TeamGrunt => hgss ? "Team Rocket balls" : "Team Galactic balls",
                IntroKind.TeamLeader => hgss ? "Rocket executive mugshot" : "Team Galactic logo",
                IntroKind.Kimono => "Sliding doors",
                IntroKind.OldBall => "Old-style Poké Ball",
                IntroKind.BallTrainer => "Poké Ball zoom",
                IntroKind.DoubleBalls => "Four Poké Balls",
                _ => "Unknown",
            };
        }
    }
}
