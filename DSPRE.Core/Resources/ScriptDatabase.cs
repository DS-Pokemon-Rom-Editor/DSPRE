using DSPRE;
using DSPRE.Resources;
using DSPRE.ROMFiles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using static DSPRE.RomInfo;

public static class ScriptDatabaseJsonLoader
{
    /// <summary>Loads the script command database; call once before any script lookup.</summary>
    public static void InitializeFromJson(string jsonPath, GameVersions gameVersion)
    {
        string expandedPath = Environment.ExpandEnvironmentVariables(jsonPath);
        string text = File.ReadAllText(expandedPath);
        JsonDocument doc = JsonDocument.Parse(text);
        try
        {
            JsonElement root = doc.RootElement;

            ScriptDatabase.MovementsDict = root
                .GetProperty("movements")
                .EnumerateObject()
                .ToDictionary(
                    prop => Convert.ToUInt16(prop.Name.Substring(2), 16),
                    prop => new MovementCommandInfo
                    {
                        CommandId = Convert.ToUInt16(prop.Name.Substring(2), 16),
                        Name = prop.Value.GetProperty("name").GetString(),
                        DecompName = prop.Value.GetProperty("decomp_name").GetString(),
                        Description = prop.Value.GetProperty("description").GetString(),
                    }
                );

            ScriptDatabase.ComparisonOperatorsDict = root
                .GetProperty("comparisonOperators")
                .EnumerateObject()
                .ToDictionary(
                    prop => Convert.ToUInt16(prop.Name.Substring(2), 16),
                    prop => prop.Value.GetString()
                );

            ScriptDatabase.SpecialOverworlds = root
                .GetProperty("specialOverworlds")
                .EnumerateObject()
                .ToDictionary(
                    prop => Convert.ToUInt16(prop.Name.Substring(2), 16),
                    prop => prop.Value.GetString()
                );

            ScriptDatabase.OverworldDirections = root
                .GetProperty("overworldDirections")
                .EnumerateObject()
                .ToDictionary(
                    prop => (byte)Convert.ToUInt16(prop.Name.Substring(2), 16),
                    prop => prop.Value.GetString()
                );

            Dictionary<ushort, ScriptCommandInfo> commandInfoDict;

            switch (gameVersion)
            {
                case GameVersions.Platinum:
                    commandInfoDict = ScriptDatabase.PlatScrCmdInfo;
                    break;
                case GameVersions.Diamond:
                case GameVersions.Pearl:
                    commandInfoDict = ScriptDatabase.DPScrCmdInfo;
                    break;
                case GameVersions.HeartGold:
                case GameVersions.SoulSilver:
                    commandInfoDict = ScriptDatabase.HGSSScrCmdInfo;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(gameVersion), gameVersion, "Unsupported game");
            }
            // Cleared first: the dictionary is shared per family, so another ROM's custom commands would stay.
            commandInfoDict.Clear();

            JsonElement scrRoot;
            if (!root.TryGetProperty("scrcmd", out scrRoot))
                throw new InvalidOperationException("JSON is missing the \"scrcmd\" key");

            foreach (JsonProperty prop in scrRoot.EnumerateObject())
            {
                ushort code = Convert.ToUInt16(prop.Name.Substring(2), 16);
                JsonElement entry = prop.Value;

                ScriptCommandInfo cmdInfo = new ScriptCommandInfo
                {
                    CommandId = code,
                    Name = entry.GetProperty("name").GetString(),
                    DecompName = entry.TryGetProperty("decomp_name", out JsonElement decompElem) ? decompElem.GetString() : "",
                    Description = entry.TryGetProperty("description", out JsonElement descElem) ? descElem.GetString() : "",
                };

                if (entry.TryGetProperty("parameters", out JsonElement paramsElem))
                {
                    cmdInfo.ParameterSizes = paramsElem
                        .EnumerateArray()
                        .Select(x => (byte)x.GetInt32())
                        .ToArray();
                }

                if (entry.TryGetProperty("parameter_types", out JsonElement paramTypesElem))
                {
                    cmdInfo.ParameterTypes = new List<ScriptParameter.ParameterType>();
                    foreach (JsonElement typeElement in paramTypesElem.EnumerateArray())
                    {
                        string typeStr = typeElement.GetString();
                        if (!string.IsNullOrEmpty(typeStr))
                        {
                            ScriptParameter.ParameterType paramType = ScriptParameter.ParseTypeString(typeStr);
                            cmdInfo.ParameterTypes.Add(paramType);
                        }
                    }
                }

                if (entry.TryGetProperty("parameter_values", out JsonElement paramNamesElem))
                {
                    cmdInfo.ParameterNames = new List<string>();
                    foreach (JsonElement nameElement in paramNamesElem.EnumerateArray())
                    {
                        string paramName = nameElement.GetString();
                        cmdInfo.ParameterNames.Add(paramName ?? "");
                    }
                }

                cmdInfo.LegacyName = cmdInfo.Name;
                commandInfoDict[code] = cmdInfo;
            }

            // The names come from the old database, where command 0 is "Nop".
            ApplyRotomNames(expandedPath, commandInfoDict, gameVersion);

            if (root.TryGetProperty("sounds", out JsonElement soundsRoot))
            {
                Dictionary<ushort, string> soundsDict = ScriptDatabase.SoundNames;
                soundsDict.Clear();
                foreach (JsonProperty prop in soundsRoot.EnumerateObject())
                {
                    if (ushort.TryParse(prop.Name, out ushort id))
                    {
                        JsonElement entry = prop.Value;
                        if (entry.TryGetProperty("name", out JsonElement nameElement))
                        {
                            string name = nameElement.GetString();
                            if (!string.IsNullOrEmpty(name))
                            {
                                soundsDict[id] = name;
                            }
                        }
                    }
                }
            }
        }
        finally
        {
            doc.Dispose();
        }
    }

        /// <summary>The Rotom v2 database for a game, as the scrcmd-database checkout names it.</summary>
        internal static string V2FileName(GameVersions gameVersion)
        {
            switch (gameVersion)
            {
                case GameVersions.Diamond:
                case GameVersions.Pearl:
                    return "diamond_pearl_v2.json";
                case GameVersions.Platinum:
                    return "platinum_v2.json";
                case GameVersions.HeartGold:
                case GameVersions.SoulSilver:
                    return "hgss_v2.json";
                default:
                    return null;
            }
        }

        /// <summary>
        /// Swaps in the rotom command names from a v2 database. A Rotom project's own database comes first, so the
        /// app shows the names its sources use; otherwise <c>&lt;game&gt;_v2.json</c> beside the per-ROM copy in
        /// <c>edited_databases/&lt;rom&gt;/scrcmd_database.json</c>, then the shared one in <see cref="AppPaths.DatabasePath"/>.
        /// </summary>
        internal static int ApplyRotomNames(string legacyJsonPath, Dictionary<ushort, ScriptCommandInfo> commands,
            GameVersions gameVersion)
        {
            string dir = Path.GetDirectoryName(legacyJsonPath);
            string file = Path.GetFileNameWithoutExtension(legacyJsonPath) ?? "";
            string stem = file.Replace("_scrcmd_database", "");
            string byGame = V2FileName(gameVersion);
        List<string> candidates = new List<string>();
            if (RomInfo.HasRotomProject && DSPRE.RotomTool.ProjectDatabaseFile() is string project) candidates.Add(project);
            candidates.AddRange(new[]
            {
                Path.Combine(dir ?? "", stem + "_v2.json"),
                Path.Combine(dir ?? "", file + "_v2.json"),
            });
            if (byGame != null)
            {
                candidates.Add(Path.Combine(dir ?? "", byGame));
                candidates.Add(Path.Combine(AppPaths.DatabasePath, byGame));
            }

            string v2 = candidates.FirstOrDefault(File.Exists);
            ScriptDatabase.VarNames.Clear();
            if (v2 == null) return 0;

            int renamed = 0;
            try
            {
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(v2));
                if (doc.RootElement.TryGetProperty("vars", out JsonElement vars))
                    ReadVarNames(vars);
                if (!doc.RootElement.TryGetProperty("commands", out JsonElement cmds)) return 0;

                foreach (JsonProperty prop in cmds.EnumerateObject())
                {
                    // The v2 file holds script commands, movements and macros in one list, and their ids
                    // overlap: script command 0 is Noop and movement 0 is FaceNorth.
                    if (!prop.Value.TryGetProperty("type", out JsonElement typeElem)) continue;
                    if (typeElem.GetString() != "script_cmd") continue;

                    if (!prop.Value.TryGetProperty("id", out JsonElement idElem)) continue;
                    if (!idElem.TryGetInt32(out int id) || id < 0 || id > ushort.MaxValue) continue;
                    if (!commands.TryGetValue((ushort)id, out ScriptCommandInfo info)) continue;

                    string rotom = prop.Name;
                    if (string.IsNullOrWhiteSpace(rotom)) continue;
                    if (!string.Equals(info.Name, rotom, StringComparison.Ordinal)) renamed++;
                    info.Name = rotom;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Info("Couldn't read the rotom command names from " + v2 + ": " + ex.Message);
            }
            return renamed;
        }

        // A number can have a plain alias (VAR_0x800C) and a meaningful one (VAR_RESULT); the meaningful one wins.
        // Range markers such as VAR_OBJ_GFX_BASE share an id with a variable (VAR_OBJ_0) but are not its
        // name, so they lose to both, whichever order the file lists them in.
        private static void ReadVarNames(JsonElement vars)
        {
        Dictionary<ushort, int> rankOf = new Dictionary<ushort, int>();
            foreach (JsonProperty prop in vars.EnumerateObject())
            {
                if (!prop.Name.StartsWith("VAR_", StringComparison.Ordinal)) continue;
                if (!prop.Value.TryGetProperty("id", out JsonElement idElem)) continue;
                if (!idElem.TryGetInt32(out int id) || id < 0 || id > ushort.MaxValue) continue;

                int rank = VarNameRank(prop.Name);
                bool known = rankOf.TryGetValue((ushort)id, out int existing);
                // Equal meaningful names: the later one wins. Otherwise the first stays.
                if (known && (rank < existing || (rank == existing && rank != MeaningfulName))) continue;
                rankOf[(ushort)id] = rank;
                ScriptDatabase.VarNames[(ushort)id] = prop.Name;
            }
        }

        private const int RangeMarker = 0, PlainName = 1, MeaningfulName = 2;

        internal static int VarNameRank(string name)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(name, "_(BASE|START|END|FIRST|LAST|COUNT)$"))
                return RangeMarker;
            if (System.Text.RegularExpressions.Regex.IsMatch(name, "^VAR_(SPECIAL_)?0?x[0-9A-Fa-f]+$"))
                return PlainName;
            return MeaningfulName;
        }
}

namespace DSPRE.Resources
{
    public static class ScriptDatabase
    {
        public static Dictionary<ushort, ScriptCommandInfo> DPScrCmdInfo = new Dictionary<ushort, ScriptCommandInfo>();
        public static Dictionary<ushort, ScriptCommandInfo> PlatScrCmdInfo = new Dictionary<ushort, ScriptCommandInfo>();
        public static Dictionary<ushort, ScriptCommandInfo> HGSSScrCmdInfo = new Dictionary<ushort, ScriptCommandInfo>();

        public static Dictionary<ushort, string> ComparisonOperatorsGenVappendix = new Dictionary<ushort, string>()
        {
            // Gen V only.
            [6] = "OR",
            [7] = "AND",
            [0xFF] = "TRUEUP"
        };

        // Reference data - populated from JSON at runtime
        public static Dictionary<ushort, string> ComparisonOperatorsDict = new Dictionary<ushort, string>();
        public static Dictionary<ushort, string> SpecialOverworlds = new Dictionary<ushort, string>();
        public static Dictionary<byte, string> OverworldDirections = new Dictionary<byte, string>();
        public static Dictionary<ushort, string> PokemonNames = new Dictionary<ushort, string>();
        public static Dictionary<ushort, string> ItemNames = new Dictionary<ushort, string>();
        public static Dictionary<ushort, string> MoveNames = new Dictionary<ushort, string>();
        public static Dictionary<ushort, string> SoundNames = new Dictionary<ushort, string>();
        public static Dictionary<ushort, string> TrainerNames = new Dictionary<ushort, string>();
        public static Dictionary<ushort, string> NpcTradeNames = new Dictionary<ushort, string>();
        public static Dictionary<ushort, string> TrainerClassNames = new Dictionary<ushort, string>();

        // A class added, copied, removed or renamed is saved through its text bank, so the names follow at once.
        static ScriptDatabase()
        {
            TextArchive.Saved += (_, id) => { if (id == RomInfo.TrainerClassMessageNumber && TrainerClassNames.Count > 0) InitializeTrainerClassNames(); };
        }
        public static Dictionary<ushort, string> VarNames = new Dictionary<ushort, string>();
        public static Dictionary<ushort, MovementCommandInfo> MovementsDict = new Dictionary<ushort, MovementCommandInfo>();
        public static Dictionary<ushort, string> MovementsDictIDName => MovementsDict.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Name);

        public static Dictionary<ushort, int> CommandsWithRelativeJump = new Dictionary<ushort, int>()
        {
            //commandID, ID of parameter With Jump Address

            [0x0016] = 0,   //Jump
            [0x0017] = 1,   //Call
            [0x0018] = 1,   //Call
            [0x0019] = 1,   //Call
            [0x001A] = 0,   //Call
            [0x001C] = 1,   //JumpIf
            [0x001D] = 1,   //CallIf
            [0x005E] = 1,   //Movement
        };

        public static HashSet<ushort?> EndCodes = new HashSet<ushort?>() {
            0x2,
            0x16,
            0x1B
        };

        public static HashSet<ushort?> MovementEndCodes = new HashSet<ushort?>() {
            0x00FE,
        };

        public static string StripSpecialCharacters(string input)
        {
            string stripped = input;
            stripped = stripped.Replace('É', 'E');
            stripped = stripped.Replace("&", "AND");
            stripped = stripped.Replace(".", "");
            stripped = stripped.Replace("-", "_");
            stripped = stripped.Replace("’", "");
            stripped = stripped.Replace("♂","_M");
            stripped = stripped.Replace("♀","_F");
            return stripped;
        }

        public static string FormatStringForScripting(string input)
        {
            string formatted = input.ToUpper().Replace(' ', '_');
            formatted = StripSpecialCharacters(formatted);
            return formatted;
        }

        public static void InitializePokemonNames()
        {
            PokemonNames = UnambiguousNames(GetPokemonNames(), "SPECIES_");
        }
        public static void InitializeItemNames()
        {
            ItemNames = UnambiguousNames(GetItemNames(), "ITEM_");
        }
        public static void InitializeMoveNames()
        {
            MoveNames = UnambiguousNames(GetAttackNames(), "MOVE_");
        }

        /// <summary>
        /// Script names by id. A name the ROM repeats (HeartGold has 23 items called "???") would read
        /// back as the first id that has it, so only that first one keeps it; the others are left out and
        /// show as their number, which reads back to the same id.
        /// </summary>
        internal static Dictionary<ushort, string> UnambiguousNames(IEnumerable<string> names, string prefix)
        {
            Dictionary<ushort, string> result = new Dictionary<ushort, string>();
            if (names == null) return result;
            HashSet<string> taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int index = 0;
            foreach (string name in names)
            {
                string scriptName = prefix + FormatStringForScripting(name ?? "");
                if (taken.Add(scriptName) && index <= ushort.MaxValue)
                    result[(ushort)index] = scriptName;
                index++;
            }
            return result;
        }
        public static void InitializeTrainerNames()
        {
            string[] names = GetSimpleTrainerNames();

            TrainerNames = Enumerable.Range(0, names.Length)
                .ToDictionary(
                    index => (ushort)index,
                    index => index == 0
                        ? "TRAINER_NONE"
                        : $"TRAINER_{FormatStringForScripting(names[index])}_{index:D3}"
            );
        }
        /// <summary>
        /// In-game trades by number, named like the decomps' NPC_TRADE_KENYA_SPEAROW from the ROM's own nickname
        /// and species, so an edited trade reads under its new name.
        /// </summary>
        public static void InitializeNpcTradeNames()
        {
            NpcTradeNames = new Dictionary<ushort, string>();
            try
            {
                DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.TradeData });
                if (TradeData.TextBank < 0) return;
                int count = TradeData.GetTradeCount();
                TextArchive bank = new TextArchive(TradeData.TextBank);
                string[] species = GetPokemonNames();
                List<string> names = new List<string>();
                for (int i = 0; i < count; i++)
                {
                    int mon = new TradeData(i).Species;
                    string nickname = i < bank.Messages.Count ? bank.Messages[i] : "";
                    string speciesName = mon >= 0 && mon < species.Length ? species[mon] : mon.ToString();
                    names.Add(nickname.Length > 0 ? nickname + " " + speciesName : speciesName);
                }
                NpcTradeNames = UnambiguousNames(names, "NPC_TRADE_");
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is ArgumentException || e is UnauthorizedAccessException)
            {
                AppLogger.Warn("Script trade names: " + e.Message);
            }
        }

        /// <summary>
        /// Trainer classes by number, named from the ROM's own class names like the decomps' TRAINER_CLASS_YOUNGSTER,
        /// so an added or renamed class reads under its name. A name the ROM repeats carries the class number each time.
        /// </summary>
        public static void InitializeTrainerClassNames()
        {
            Dictionary<ushort, string> names = new Dictionary<ushort, string>();
            try
            {
                List<string> messages = new TextArchive(RomInfo.TrainerClassMessageNumber).Messages;
                string[] keys = messages.Select(ClassKey).ToArray();
                HashSet<string> repeated = keys.GroupBy(k => k).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
                for (int i = 0; i < keys.Length && i <= ushort.MaxValue; i++)
                    if (keys[i].Length > 0)
                        names[(ushort)i] = "TRAINER_CLASS_" + keys[i] + (repeated.Contains(keys[i]) ? $"_{i:D3}" : "");
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is ArgumentException || e is UnauthorizedAccessException)
            {
                AppLogger.Warn("Script trainer class names: " + e.Message);
            }
            TrainerClassNames = names;
        }

        private static string ClassKey(string name)
        {
            string key = FormatStringForScripting((name ?? "").Replace("[PK][MN]", "PKMN"));
            key = System.Text.RegularExpressions.Regex.Replace(key, "[^A-Z0-9_]", "");
            return System.Text.RegularExpressions.Regex.Replace(key, "_+", "_").Trim('_');
        }

        internal static void InitializePokemonNamesIfNeeded()
        {
            if(PokemonNames.Count == 0)
            {
                InitializePokemonNames();
            }
        }

        internal static void InitializeMoveNamesIfNeeded()
        {
            if(MoveNames.Count == 0)
            {
                InitializeMoveNames();
            }
        }

        /// <summary>Writes the name lists Rotom reads (Pokémon, items, moves, trainers) from the current ROM.</summary>
        /// <param name="romFolderPath">Path to the ROM-specific database folder</param>
        public static void ExportEnumJsons(string romFolderPath)
        {
            Directory.CreateDirectory(romFolderPath);

            ExportDictionary(PokemonNames, Path.Combine(romFolderPath, "pokemon.json"));

            ExportDictionary(ItemNames, Path.Combine(romFolderPath, "items.json"));

            ExportDictionary(MoveNames, Path.Combine(romFolderPath, "moves.json"));

            ExportDictionary(TrainerNames, Path.Combine(romFolderPath, "trainers.json"));
        }

        /// <summary>
        /// Exports a dictionary to a JSON file with integer keys.
        /// </summary>
        private static void ExportDictionary(Dictionary<ushort, string> dictionary, string filePath)
        {
            // Convert Dictionary<ushort, string> to Dictionary<string, string> for JSON serialization
            Dictionary<string, string> exportDict = new Dictionary<string, string>();
            foreach (KeyValuePair<ushort, string> kvp in dictionary)
            {
                exportDict[kvp.Key.ToString()] = kvp.Value;
            }

            JsonSerializerOptions options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            string json = JsonSerializer.Serialize(exportDict, options);
            PlainFiles.WriteText(filePath, json);
    }
}
}
