using DSPRE.Resources;
using DSPRE.ROMFiles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Forms;
using static DSPRE.MoveData;
using static DSPRE.RomInfo;
using static Images.NCOB.sNCOB;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DSPRE
{
    internal class DocTool
    {
        private static bool _eventEditorPrereqsReady = false;
        public static void ExportAll()
        {
            // Backwards-compat wrapper
            ExportDexExports();
        }

        public static void ExportCsv()
        {
            // Create the subfolder Docs in the executable directory and write the CSV files there
            string executablePath = AppDomain.CurrentDomain.BaseDirectory;
            string docsFolderPath = Path.Combine(executablePath, "Docs");

            string pokePersonalDataPath = Path.Combine(docsFolderPath, "PokemonPersonalData.csv");
            string learnsetDataPath = Path.Combine(docsFolderPath, "LearnsetData.csv");
            string evolutionDataPath = Path.Combine(docsFolderPath, "EvolutionData.csv");
            string trainerDataPath = Path.Combine(docsFolderPath, "TrainerData.txt");
            string moveDataPath = Path.Combine(docsFolderPath, "MoveData.csv");
            string TMHMDataPath = Path.Combine(docsFolderPath, "TMHMData.csv");
            string eggMoveDataPath = Path.Combine(docsFolderPath, "EggMoveData.csv");

            DSUtils.TryUnpackNarcs(new List<DirNames> {
                DirNames.personalPokeData,
                DirNames.learnsets,
                DirNames.evolutions,
                DirNames.trainerParty,
                DirNames.trainerProperties,
                DirNames.moveData,
                DirNames.itemData
            });

            string[] pokeNames = RomInfo.GetPokemonNames();
            string[] itemNames = RomInfo.GetItemNames();
            string[] abilityNames = RomInfo.GetAbilityNames();
            string[] moveNames = RomInfo.GetAttackNames();
            string[] trainerNames = RomInfo.GetSimpleTrainerNames();
            string[] trainerClassNames = RomInfo.GetTrainerClassNames();
            string[] typeNames = RomInfo.GetTypeNames();

            // Handle Forms
            int extraCount = RomInfo.GetPersonalFilesCount() - pokeNames.Length;
            string[] extraNames = new string[extraCount];

            for (int i = 0; i < extraCount; i++)
            {
                PokeDatabase.PersonalData.PersonalExtraFiles extraEntry = PokeDatabase.PersonalData.personalExtraFiles[i];
                extraNames[i] = pokeNames[extraEntry.monId] + " - " + extraEntry.description;
            }

            pokeNames = pokeNames.Concat(extraNames).ToArray();

            // Create the Docs folder if it doesn't exist
            if (!Directory.Exists(docsFolderPath))
            {
                Directory.CreateDirectory(docsFolderPath);
            }

            var eggMoveEditor = new EggMoveEditor();
            eggMoveEditor.PopulateEggMoveData();
            ExportEggMoveDataToCSV(eggMoveEditor.GetEggMoveData(), eggMoveDataPath, pokeNames, moveNames);

            ExportPersonalDataToCSV(pokePersonalDataPath, pokeNames, abilityNames, typeNames, itemNames);
            ExportLearnsetDataToCSV(learnsetDataPath, pokeNames, moveNames);
            ExportEvolutionDataToCSV(evolutionDataPath, pokeNames, itemNames, moveNames);
            ExportTrainersToText(trainerDataPath, trainerNames, trainerClassNames, pokeNames, itemNames, moveNames, abilityNames);
            ExportMoveDataToCSV(moveDataPath, moveNames, typeNames);
            ExportTMHMDataToCSV(TMHMDataPath, pokeNames);

            MessageBox.Show($"CSV files exported successfully to path: {docsFolderPath}");
        }

        public static void ExportDexExports()
        {
            // Create the subfolder Docs in the executable directory and write the CSV files there
            string executablePath = AppDomain.CurrentDomain.BaseDirectory;
            string docsFolderPath = Path.Combine(executablePath, "Docs");

            string pokePersonalDataPath = Path.Combine(docsFolderPath, "PokemonPersonalData.csv");
            string learnsetDataPath = Path.Combine(docsFolderPath, "LearnsetData.csv");
            string evolutionDataPath = Path.Combine(docsFolderPath, "EvolutionData.csv");
            string trainerDataPath = Path.Combine(docsFolderPath, "TrainerData.txt");
            string moveDataPath = Path.Combine(docsFolderPath, "MoveData.csv");
            string TMHMDataPath = Path.Combine(docsFolderPath, "TMHMData.csv");
            string eggMoveDataPath = Path.Combine(docsFolderPath, "EggMoveData.csv");
            string eventOverworldsPath = Path.Combine(docsFolderPath, "EventOverworlds.csv");
            string mapHeadersPath = Path.Combine(docsFolderPath, "MapHeaders.csv");
            string encounterJsonPath = Path.Combine(docsFolderPath, "Encounters.json");

            EnsureEventEditorPrereqs();

            DSUtils.TryUnpackNarcs(new List<DirNames> {
                DirNames.personalPokeData,
                DirNames.learnsets,
                DirNames.evolutions,
                DirNames.trainerParty,
                DirNames.trainerProperties,
                DirNames.moveData,
                DirNames.itemData,
                DirNames.encounters,
                DirNames.scripts,
                DirNames.eventFiles
            });

            string[] pokeNames = RomInfo.GetPokemonNames();
            string[] itemNames = RomInfo.GetItemNames();
            string[] abilityNames = RomInfo.GetAbilityNames();
            string[] moveNames = RomInfo.GetAttackNames();
            string[] trainerNames = RomInfo.GetSimpleTrainerNames();
            string[] trainerClassNames = RomInfo.GetTrainerClassNames();
            string[] typeNames = RomInfo.GetTypeNames();

            // Handle Forms
            int extraCount = RomInfo.GetPersonalFilesCount() - pokeNames.Length;
            string[] extraNames = new string[extraCount];

            for (int i = 0; i < extraCount; i++)
            {
                PokeDatabase.PersonalData.PersonalExtraFiles extraEntry = PokeDatabase.PersonalData.personalExtraFiles[i];
                extraNames[i] = pokeNames[extraEntry.monId] + " - " + extraEntry.description;
            }

            pokeNames = pokeNames.Concat(extraNames).ToArray();

            // Create the Docs folder if it doesn't exist
            if (!Directory.Exists(docsFolderPath))
            {
                Directory.CreateDirectory(docsFolderPath);
            }

            ExportPersonalDataToCSV(pokePersonalDataPath, pokeNames, abilityNames, typeNames, itemNames);
            ExportLearnsetDataToCSV(learnsetDataPath, pokeNames, moveNames);
            ExportEvolutionDataToCSV(evolutionDataPath, pokeNames, itemNames, moveNames);
            ExportTrainersToText(trainerDataPath, trainerNames, trainerClassNames, pokeNames, itemNames, moveNames, abilityNames);
            ExportMoveDataToCSV(moveDataPath, moveNames, typeNames);
            ExportTMHMDataToCSV(TMHMDataPath, pokeNames);

            var eggMoveEditor = new EggMoveEditor();
            eggMoveEditor.PopulateEggMoveData();
            ExportEggMoveDataToCSV(eggMoveEditor.GetEggMoveData(), eggMoveDataPath, pokeNames, moveNames);

            ExportEventOverworldsToCSV(eventOverworldsPath);
            ExportMapHeadersToCSV(mapHeadersPath);
            ExportEncountersToJson(encounterJsonPath);
            ExportScriptsToDocs(Path.Combine(docsFolderPath, "scripts"));

            MessageBox.Show($"CSV files exported successfully to path: {docsFolderPath}");
        }


        private static void EnsureEventEditorPrereqs()
        {
            if (_eventEditorPrereqsReady) return;
            _eventEditorPrereqsReady = true;

            // Match the core unpack set from EventEditor.SetupEventEditor (minus UI stuff)
            DSUtils.TryUnpackNarcs(new List<DirNames> {
                DirNames.areaData,
                DirNames.trainerProperties,
            });

        }

        private static void ExportEncountersToJson(string outputPath)
        {
            string encountersDir = RomInfo.gameDirs[RomInfo.DirNames.encounters].unpackedDir;

            var files = Directory.GetFiles(encountersDir)
                .Where(p => int.TryParse(Path.GetFileName(p), out _))
                .OrderBy(p => int.Parse(Path.GetFileName(p)))
                .ToList();

  
            string[] pokeNames = GetPokemonNamesWithForms();

            var root = new
            {
                generatedAt = DateTime.UtcNow,
                projectName = RomInfo.projectName,
                romID = RomInfo.romID,
                gameFamily = RomInfo.gameFamily.ToString(),
                gameVersion = RomInfo.gameVersion.ToString(),
                gameLanguage = RomInfo.gameLanguage.ToString(),
                encounters = new List<object>()
            };

            foreach (var path in files)
            {
                int fileId = int.Parse(Path.GetFileName(path));

                object encObj;

                if (RomInfo.gameFamily == RomInfo.GameFamilies.DP ||
                    RomInfo.gameFamily == RomInfo.GameFamilies.Plat)
                {
                    encObj = ExportDPPt(fileId, path, pokeNames);
                }
                else if (RomInfo.gameFamily == RomInfo.GameFamilies.HGSS)
                {
                    encObj = ExportHGSS(fileId, path, pokeNames);
                }
                else
                {
                    encObj = new { fileId = fileId, unsupported = true };
                }

                root.encounters.Add(encObj);
            }

            var opts = new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };

            File.WriteAllText(outputPath, System.Text.Json.JsonSerializer.Serialize(root, opts));
        }


        private static object ExportDPPt(int fileId, string path, string[] pokeNames)
        {
            EncounterFileDPPt enc;
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                enc = new EncounterFileDPPt(fs);

            return new
            {
                fileId,

                rates = new
                {
                    walking = enc.walkingRate,
                    surf = enc.surfRate,
                    oldRod = enc.oldRodRate,
                    goodRod = enc.goodRodRate,
                    superRod = enc.superRodRate
                },

                walking = ExportWalkingDPPtNamed(enc, pokeNames),

                timeSpecific = new
                {
                    day = ExportU32SpeciesNamed(enc.dayPokemon, pokeNames),
                    night = ExportU32SpeciesNamed(enc.nightPokemon, pokeNames)
                },

                radar = ExportU32SpeciesNamed(enc.radarPokemon, pokeNames),

                dualSlot = new
                {
                    ruby = ExportU32SpeciesNamed(enc.rubyPokemon, pokeNames),
                    sapphire = ExportU32SpeciesNamed(enc.sapphirePokemon, pokeNames),
                    emerald = ExportU32SpeciesNamed(enc.emeraldPokemon, pokeNames),
                    fireRed = ExportU32SpeciesNamed(enc.fireRedPokemon, pokeNames),
                    leafGreen = ExportU32SpeciesNamed(enc.leafGreenPokemon, pokeNames)
                },

                // (keep regionalForms/unknownTable as-is; those are not species ids in DSPRE’s UI)

                swarms = ExportU16Named(enc.swarmPokemon, pokeNames),



                forms = new
                {
                    regionalForms = ExportU32(enc.regionalForms), // 5
                    unknownTable = enc.unknownTable
                },

                surf = ExportMinMaxU16Named(enc.surfPokemon, enc.surfMinLevels, enc.surfMaxLevels, pokeNames),
                oldRod = ExportMinMaxU16Named(enc.oldRodPokemon, enc.oldRodMinLevels, enc.oldRodMaxLevels, pokeNames),
                goodRod = ExportMinMaxU16Named(enc.goodRodPokemon, enc.goodRodMinLevels, enc.goodRodMaxLevels, pokeNames),
                superRod = ExportMinMaxU16Named(enc.superRodPokemon, enc.superRodMinLevels, enc.superRodMaxLevels, pokeNames),
            };
        }

        private static List<object> ExportU32SpeciesNamed(uint[] arr, string[] pokeNames)
        {
            if (arr == null || arr.Length == 0) return null;

            var list = new List<object>(arr.Length);
            for (int i = 0; i < arr.Length; i++)
            {
                int speciesId = unchecked((int)arr[i]);
                list.Add(SpeciesObj(i, speciesId, pokeNames));
            }
            return list;
        }

        private static object ExportWalkingDPPtNamed(EncounterFileDPPt enc, string[] pokeNames)
        {
            var slots = new List<object>(12);
            for (int i = 0; i < 12; i++)
            {
                int speciesId = unchecked((int)enc.walkingPokemon[i]);
                slots.Add(new
                {
                    slot = i,
                    level = enc.walkingLevels[i],
                    species = speciesId,
                    speciesName = NameForSpecies(speciesId, pokeNames)
                });
            }
            return slots;
        }

        private static object ExportWalkingDPPt(EncounterFileDPPt enc)
        {
            var slots = new List<object>();

            for (int i = 0; i < 12; i++)
            {
                uint raw = enc.walkingPokemon[i];

                ushort species = (ushort)(raw & 0xFFFF);
                byte minLv = (byte)((raw >> 16) & 0xFF);
                byte maxLv = (byte)((raw >> 24) & 0xFF);

                slots.Add(new
                {
                    slot = i,
                    raw,
                    species,
                    minLv,
                    maxLv,
                    levelTableEntry = enc.walkingLevels[i]
                });
            }

            return slots;
        }


        private static object ExportHGSS(int fileId, string path, string[] pokeNames)
        {
            EncounterFileHGSS enc;
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                enc = new EncounterFileHGSS(fs);

            return new
            {
                fileId,

                rates = new
                {
                    walking = enc.walkingRate,
                    surf = enc.surfRate,
                    rockSmash = enc.rockSmashRate,
                    oldRod = enc.oldRodRate,
                    goodRod = enc.goodRodRate,
                    superRod = enc.superRodRate
                },

                walkingLevels = ExportU8(enc.walkingLevels), // 12

                grass = new
                {
                    morning = ExportU16Named(enc.morningPokemon, pokeNames),
                    day = ExportU16Named(enc.dayPokemon, pokeNames),
                    night = ExportU16Named(enc.nightPokemon, pokeNames)
                },

                surf = ExportMinMaxU16Named(enc.surfPokemon, enc.surfMinLevels, enc.surfMaxLevels, pokeNames),
                rockSmash = ExportMinMaxU16Named(enc.rockSmashPokemon, enc.rockSmashMinLevels, enc.rockSmashMaxLevels, pokeNames),
                oldRod = ExportMinMaxU16Named(enc.oldRodPokemon, enc.oldRodMinLevels, enc.oldRodMaxLevels, pokeNames),
                goodRod = ExportMinMaxU16Named(enc.goodRodPokemon, enc.goodRodMinLevels, enc.goodRodMaxLevels, pokeNames),
                superRod = ExportMinMaxU16Named(enc.superRodPokemon, enc.superRodMinLevels, enc.superRodMaxLevels, pokeNames),

                swarms = ExportU16Named(enc.swarmPokemon, pokeNames),
                pokegearMusic = new
                {
                    hoenn = ExportU16Named(enc.hoennMusicPokemon, pokeNames),
                    sinnoh = ExportU16Named(enc.sinnohMusicPokemon, pokeNames)
                }
            };
        }



        private static string NameForSpecies(int speciesId, string[] pokeNames)
        {
            if (speciesId < 0 || speciesId >= pokeNames.Length) return $"UNKNOWN_{speciesId}";
            return pokeNames[speciesId];
        }

        private static object SpeciesObj(int slot, int speciesId, string[] pokeNames)
        {
            return new
            {
                slot,
                species = speciesId,
                speciesName = NameForSpecies(speciesId, pokeNames)
            };
        }

        private static string[] GetPokemonNamesWithForms()
        {
            // Base names from text archive
            string[] pokeNames = RomInfo.GetPokemonNames();

            // Append extra personal files (forms) like DocTool.ExportAll already does
            int extraCount = RomInfo.GetPersonalFilesCount() - pokeNames.Length;
            if (extraCount <= 0) return pokeNames;

            string[] extraNames = new string[extraCount];

            for (int i = 0; i < extraCount; i++)
            {
                var extraEntry = PokeDatabase.PersonalData.personalExtraFiles[i];
                string baseName = (extraEntry.monId >= 0 && extraEntry.monId < pokeNames.Length)
                    ? pokeNames[extraEntry.monId]
                    : $"UNKNOWN_{extraEntry.monId}";

                extraNames[i] = $"{baseName} - {extraEntry.description}";
            }

            return pokeNames.Concat(extraNames).ToArray();
        }

        private static List<object> ExportMinMaxU16Named(ushort[] mons, byte[] minLv, byte[] maxLv, string[] pokeNames)
        {
            if (mons == null || minLv == null || maxLv == null) return null;

            int n = Math.Min(mons.Length, Math.Min(minLv.Length, maxLv.Length));
            var list = new List<object>(n);

            for (int i = 0; i < n; i++)
            {
                int speciesId = mons[i];
                list.Add(new
                {
                    slot = i,
                    species = speciesId,
                    speciesName = NameForSpecies(speciesId, pokeNames),
                    minLv = minLv[i],
                    maxLv = maxLv[i]
                });
            }
            return list;
        }

        private static List<object> ExportU16Named(ushort[] arr, string[] pokeNames)
        {
            if (arr == null || arr.Length == 0) return null;
            var list = new List<object>(arr.Length);
            for (int i = 0; i < arr.Length; i++)
                list.Add(SpeciesObj(i, arr[i], pokeNames));
            return list;
        }


        private static List<object> ExportMinMaxU16(ushort[] mons, byte[] minLv, byte[] maxLv)
        {
            if (mons == null || minLv == null || maxLv == null) return null;

            int n = Math.Min(mons.Length, Math.Min(minLv.Length, maxLv.Length));
            var list = new List<object>(n);

            for (int i = 0; i < n; i++)
            {
                list.Add(new
                {
                    slot = i,
                    species = mons[i],
                    minLv = minLv[i],
                    maxLv = maxLv[i]
                });
            }
            return list;
        }

        private static ushort[] ExportU16(ushort[] arr) => arr == null ? null : (ushort[])arr.Clone();
        private static uint[] ExportU32(uint[] arr) => arr == null ? null : (uint[])arr.Clone();
        private static byte[] ExportU8(byte[] arr) => arr == null ? null : (byte[])arr.Clone();



        private static void ExportScriptsToDocs(string scriptsDocsDir)
        {
            Directory.CreateDirectory(scriptsDocsDir);

            // Make sure text archives + name dictionaries are available so ScriptCommand.name
            // can render friendly enums (TRAINER_, SPECIES_, ITEM_, MOVE_, etc.) consistently.
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.textArchives });

            Resources.ScriptDatabase.InitializePokemonNames();
            Resources.ScriptDatabase.InitializeItemNames();
            Resources.ScriptDatabase.InitializeMoveNames();
            Resources.ScriptDatabase.InitializeTrainerNames();

            int scriptCount = Filesystem.GetScriptCount();
            int exported = 0;

            for (int i = 0; i < scriptCount; i++)
            {
                try
                {
                    // Read scripts + functions only (no actions)
                    var sf = new ScriptFile(i, readFunctions: true, readActions: false);

                    // Skip “level scripts” / empty script files
                    if (sf.isLevelScript || sf.hasNoScripts)
                        continue;

                    // Emit Scripts + Functions only
                    string outPath = Path.Combine(scriptsDocsDir, $"{i:D4}.txt");
                    sf.WritePlainTextFile(outPath, includeActions: false);
                    exported++;
                }
                catch (Exception ex)
                {
                    AppLogger.Error($"Doc export: failed to export script {i:D4}: {ex.Message}");
                }
            }

            AppLogger.Info($"Doc export: wrote {exported} script plaintext files to {scriptsDocsDir}");
        }

        private static void ExportMapHeadersToCSV(string mapHeadersPath)
        {
            int headerCount = MapHeader.GetHeaderCount(); // handles dynamic headers patch vs ARM9 table :contentReference[oaicite:3]{index=3}

            using (var sw = new StreamWriter(mapHeadersPath))
            {
                sw.WriteLine("HeaderID,ScriptFileID,EventFileID,MapNameIndexInTextArchive,WildPokemonFileID,WeatherID");

                for (ushort headerId = 0; headerId < headerCount; headerId++)
                {
                    MapHeader h = MapHeader.GetMapHeader(headerId); // loads from ARM9 or dynamic headers path :contentReference[oaicite:4]{index=4}
                    if (h == null) continue;

                    int mapNameIndex = GetMapNameIndex(h);

                    sw.WriteLine(
                        $"{headerId}," +
                        $"{h.scriptFileID}," +
                        $"{h.eventFileID}," +
                        $"{mapNameIndex}," +
                        $"{h.wildPokemon}," +
                        $"{h.weatherID}"
                    );
                }
            }
        }

        private static int GetMapNameIndex(MapHeader h)
        {
            // DP: locationName is ushort; Pt/HGSS: locationName is byte :contentReference[oaicite:5]{index=5} :contentReference[oaicite:6]{index=6}
            if (h is HeaderDP dp) return dp.locationName;     // read as UInt16 in DP :contentReference[oaicite:7]{index=7}
            if (h is HeaderPt pt) return pt.locationName;     // read as Byte in Pt :contentReference[oaicite:8]{index=8}
            if (h is HeaderHGSS hgss) return hgss.locationName; // read as Byte in HGSS :contentReference[oaicite:9]{index=9}

            return -1; // should not happen, but keeps export robust
        }

        private static void ExportEventSpawnablesToCSV(string eventSpawnablesPath)
        {
            string eventsDir = RomInfo.gameDirs[DirNames.eventFiles].unpackedDir;

            if (!Directory.Exists(eventsDir))
                throw new DirectoryNotFoundException($"Event files directory not found: {eventsDir}");

            var files = Directory.GetFiles(eventsDir)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();

            using (var sw = new StreamWriter(eventSpawnablesPath))
            {
                sw.WriteLine("EventFileID,SpawnableIndex,ScriptNumber");

                foreach (var filePath in files)
                {
                    string name = Path.GetFileName(filePath);

                    // Event files are typically named "0000", "0001", ...
                    if (!int.TryParse(name, out int eventFileId))
                        continue;

                    EventFile ev;
                    using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        ev = new EventFile(fs);
                    }

                    for (int i = 0; i < ev.spawnables.Count; i++)
                    {
                        var sp = ev.spawnables[i];
                        sw.WriteLine($"{eventFileId},{i},{sp.scriptNumber}");
                    }
                }
            }
        }


        private static void ExportEventOverworldsToCSV(string eventOverworldsPath)
        {
            string eventsDir = RomInfo.gameDirs[DirNames.eventFiles].unpackedDir;

            if (!Directory.Exists(eventsDir))
                throw new DirectoryNotFoundException($"Event files directory not found: {eventsDir}");

            var files = Directory.GetFiles(eventsDir)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();

            using (var sw = new StreamWriter(eventOverworldsPath))
            {
                sw.WriteLine(
                    "EventFileID,OverworldIndex," +
                    "OwID,OverlayTableEntry,OwSpriteID,Movement,Type,Flag,ScriptNumber,Orientation,SightRange,Unknown1,Unknown2,XRange,YRange," +
                    "XMatrix,YMatrix,XMap,YMap,XCoord,YCoord,ZPosition,IsAlias"
                );

                foreach (var filePath in files)
                {
                    string name = Path.GetFileName(filePath);

                    // Event files are typically named "0000", "0001", ...
                    if (!int.TryParse(name, out int eventFileId))
                        continue;

                    EventFile ev;
                    using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        ev = new EventFile(fs);
                    }

                    for (int i = 0; i < ev.overworlds.Count; i++)
                    {
                        var ow = ev.overworlds[i];

                        // Combined coordinates (same scheme used in serialization)
                        int xCoord = ow.xMapPosition + (MapFile.mapSize * ow.xMatrixPosition);
                        int yCoord = ow.yMapPosition + (MapFile.mapSize * ow.yMatrixPosition);

                        bool isAlias = ow.scriptNumber == 0xFFFF;

                        // Match EventEditor.GetOverworldImage(...) logic:
                        // - If 3D overworld dict contains entry, there is no OWSprites file id.
                        // - Else use OverworldTable -> (spriteID, properties)
                        string owSpriteIdStr = "";
                        if (RomInfo.ow3DSpriteDict.TryGetValue(ow.overlayTableEntry, out _))
                        {
                            // 3D overworld (image comes from Resources). No OWSprites sprite ID.
                            owSpriteIdStr = "";
                        }
                        else if (RomInfo.OverworldTable.TryGetValue(ow.overlayTableEntry, out var result))
                        {
                            owSpriteIdStr = result.spriteID.ToString();
                        }
                        else
                        {
                            // No match; keep empty (or you could use "-1")
                            owSpriteIdStr = "";
                        }

                        sw.WriteLine(
                            $"{eventFileId},{i}," +
                            $"{ow.owID},{ow.overlayTableEntry},{owSpriteIdStr}," +
                            $"{ow.movement},{ow.type},{ow.flag},{ow.scriptNumber}," +
                            $"{ow.orientation},{ow.sightRange},{ow.unknown1},{ow.unknown2},{ow.xRange},{ow.yRange}," +
                            $"{ow.xMatrixPosition},{ow.yMatrixPosition},{ow.xMapPosition},{ow.yMapPosition}," +
                            $"{xCoord},{yCoord},{ow.zPosition},{(isAlias ? 1 : 0)}"
                        );
                    }
                }
            }
        }


        private static void ExportPersonalDataToCSV(string pokePersonalDataPath, string[] pokeNames, string[] abilityNames, string[] typeNames, string[] itemNames)
        {
            // Write the Pokemon Personal Data to the CSV file
            PokemonPersonalData curPersonalData = null;
            StreamWriter sw = new StreamWriter(pokePersonalDataPath);

            sw.WriteLine("ID,Name,Type1,Type2,BaseHP,BaseAttack,BaseDefense,BaseSpecialAttack,BaseSpecialDefense,BaseSpeed," +
                "Ability1,Ability2,Item1,Item2");

            for (int i = 0; i < RomInfo.GetPersonalFilesCount(); i++)
            {
                curPersonalData = new PokemonPersonalData(i);

                string type1String = (int)curPersonalData.type1 < typeNames.Length ? typeNames[(int)curPersonalData.type1] : "UnknownType_" + (int)curPersonalData.type1;
                string type2String = (int)curPersonalData.type2 < typeNames.Length ? typeNames[(int)curPersonalData.type2] : "UnknownType_" + (int)curPersonalData.type2;

                sw.WriteLine($"{i},{pokeNames[i]},{type1String},{type2String}," +
                    $"{curPersonalData.baseHP},{curPersonalData.baseAtk},{curPersonalData.baseDef}, " +
                    $"{curPersonalData.baseSpAtk},{curPersonalData.baseSpDef},{curPersonalData.baseSpeed}," +
                    $"{abilityNames[curPersonalData.firstAbility]},{abilityNames[curPersonalData.secondAbility]}," +
                    $"{itemNames[curPersonalData.item1]},{itemNames[curPersonalData.item2]}");
            }

            sw.Close();
        }

        private static void ExportLearnsetDataToCSV(string learnsetDataPath, string[] pokeNames, string[] moveNames)
        {
            using (StreamWriter sw = new StreamWriter(learnsetDataPath))
            {
                // ---- Write Header ----
                sw.Write("ID,Name");

                for (int i = 0; i < 20; i++)
                {
                    sw.Write($",LevelMove{i}");
                }

                sw.WriteLine();

                // ---- Write Data ----
                for (int i = 0; i < RomInfo.GetLearnsetFilesCount(); i++)
                {
                    LearnsetData curLearnsetData = new LearnsetData(i);

                    sw.Write($"{i},{pokeNames[i]}");

                    int entryIndex = 0;
                    // Write up to 20 entries
                    foreach (var entry in curLearnsetData.list)
                    {
                        if (entryIndex >= 20)
                            break;
                        string moveName = moveNames[entry.move];

                        sw.Write($",{entry.level}|{moveName}");

                        entryIndex++;
                    }
                    // Pad remaining columns if less than 20
                    while (entryIndex < 20)
                    {
                        sw.Write(",");
                        entryIndex++;
                    }
                    sw.WriteLine();
                }
            }
        }


        public static string ExportEditableLearnsetDataToCSV()
        {
            string executablePath = AppDomain.CurrentDomain.BaseDirectory;
            string docsFolderPath = Path.Combine(executablePath, "Docs");

            string learnsetDataPath = Path.Combine(docsFolderPath, "LearnsetData.csv");

            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.personalPokeData, DirNames.learnsets, DirNames.moveData });

            string[] pokeNames = RomInfo.GetPokemonNames();
            string[] moveNames = RomInfo.GetAttackNames();

            // Handle Forms
            int extraCount = RomInfo.GetPersonalFilesCount() - pokeNames.Length;
            string[] extraNames = new string[extraCount];

            for (int i = 0; i < extraCount; i++)
            {
                PokeDatabase.PersonalData.PersonalExtraFiles extraEntry = PokeDatabase.PersonalData.personalExtraFiles[i];
                extraNames[i] = pokeNames[extraEntry.monId] + " - " + extraEntry.description;
            }

            pokeNames = pokeNames.Concat(extraNames).ToArray();

            // Create the Docs folder if it doesn't exist
            if (!Directory.Exists(docsFolderPath))
            {
                Directory.CreateDirectory(docsFolderPath);
            }

            // Write the Editable Learnset Data to the CSV file
            LearnsetData curLearnsetData = null;
            StreamWriter sw = new StreamWriter(learnsetDataPath);

            // Write CSV header
            sw.WriteLine("ID,Name,Level,Move");

            for (int i = 0; i < RomInfo.GetLearnsetFilesCount(); i++)
            {
                curLearnsetData = new LearnsetData(i);
                string pokemonName = pokeNames[i];

                // If there are no moves in the learnset, still write one row for the Pokemon
                if (curLearnsetData.list.Count == 0)
                {
                    sw.WriteLine($"{i},{pokemonName},,");
                }
                else
                {
                    // Write one row for each move/level combination
                    foreach (var entry in curLearnsetData.list)
                    {
                        string moveName = moveNames[entry.move];
                        sw.WriteLine($"{i},{pokemonName},{entry.level},{moveName}");
                    }
                }
            }

            sw.Close();

            return learnsetDataPath;

        }

        public static string ExportLearnsetDataToJSON()
        {
            string executablePath = AppDomain.CurrentDomain.BaseDirectory;
            string docsFolderPath = Path.Combine(executablePath, "Docs");
            string learnsetDataPath = Path.Combine(docsFolderPath, "LearnsetData.json");
            ScriptDatabase.InitializeMoveNamesIfNeeded();
            ScriptDatabase.InitializePokemonNamesIfNeeded();
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.personalPokeData, DirNames.learnsets, DirNames.moveData });

            string[] pokeNames = RomInfo.GetPokemonNames();

            // Handle Forms
            int extraCount = RomInfo.GetPersonalFilesCount() - pokeNames.Length;
            string[] extraNames = new string[extraCount];

            for (int i = 0; i < extraCount; i++)
            {
                PokeDatabase.PersonalData.PersonalExtraFiles extraEntry = PokeDatabase.PersonalData.personalExtraFiles[i];
                extraNames[i] = pokeNames[extraEntry.monId] + " - " + extraEntry.description;
            }

            pokeNames = pokeNames.Concat(extraNames).ToArray();

            // Create the Docs folder if it doesn't exist
            if (!Directory.Exists(docsFolderPath))
            {
                Directory.CreateDirectory(docsFolderPath);
            }

            // Create JSON structure
            var learnsetJson = new Dictionary<string, object>();

            for (int i = 0; i < RomInfo.GetLearnsetFilesCount(); i++)
            {
                var curLearnsetData = new LearnsetData(i);
                string speciesName = "SPECIES_UNKNOWN";

                if (ScriptDatabase.pokemonNames.ContainsKey((ushort)i))
                {
                    speciesName = $"{ScriptDatabase.pokemonNames[(ushort)i].ToUpper().Replace(" ", "_")}";
                }
                else if (i < pokeNames.Length)
                {
                    // Fall back to the pokeNames array for forms and other Pokemon not in ScriptDatabase
                    speciesName = $"SPECIES_{pokeNames[i].ToUpper().Replace(" ", "_").Replace("-", "_").Replace("__", "_")}";
                }


                var levelMoves = new List<Dictionary<string, object>>();

                foreach (var entry in curLearnsetData.list)
                {
                    if (entry.move == 0) continue; // Skip empty moves

                    string moveName = "MOVE_UNKNOWN";

                    if (ScriptDatabase.moveNames.ContainsKey(entry.move))
                    {
                        moveName = $"{ScriptDatabase.moveNames[entry.move].ToUpper().Replace(" ", "_")}";
                    }

                    levelMoves.Add(new Dictionary<string, object>
                    {
                        { "Level", entry.level },
                        { "Move", moveName }
                    });
                }

                learnsetJson[speciesName] = new Dictionary<string, object>
                {
                    { "LevelMoves", levelMoves }
                };
            }

            // Write JSON to file
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(learnsetJson, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(learnsetDataPath, json);

            return learnsetDataPath;
        }

        private static void ExportEvolutionDataToCSV(string evolutionDataPath, string[] pokeNames, string[] itemNames, string[] moveNames)
        {
            // Write the Evolution Data to the CSV file
            EvolutionFile curEvolutionFile = null;
            StreamWriter sw = new StreamWriter(evolutionDataPath);

            sw.WriteLine("ID,Name,[Method|Param|Target]");

            for (int i = 0; i < RomInfo.GetEvolutionFilesCount(); i++)
            {
                curEvolutionFile = new EvolutionFile(i);

                sw.Write($"{i},{pokeNames[i]}");

                foreach (var entry in curEvolutionFile.data)
                {
                    EvolutionParamMeaning meaning = EvolutionFile.evoDescriptions[entry.method];

                    string paramString = "";

                    switch (meaning)
                    {
                        case EvolutionParamMeaning.Ignored:
                            paramString = "Ignored";
                            break;
                        case EvolutionParamMeaning.FromLevel:
                            paramString = entry.param.ToString();
                            break;
                        case EvolutionParamMeaning.ItemName:
                            paramString = itemNames[entry.param];
                            break;
                        case EvolutionParamMeaning.MoveName:
                            paramString = moveNames[entry.param];
                            break;
                        case EvolutionParamMeaning.PokemonName:
                            paramString = pokeNames[entry.param];
                            break;
                        case EvolutionParamMeaning.BeautyValue:
                            paramString = entry.param.ToString();
                            break;
                    }
                    if (entry.target == 0)
                    {
                        break;
                    }
                    sw.Write($",[{entry.method}|{paramString}|{pokeNames[entry.target]}]");
                }

                sw.WriteLine();

            }

            sw.Close();
        }

        public static bool ExportEggMoveDataToCSV(List<EggMoveEntry> eggMoveData, string filePath, string[] pokeNames, string[] moveNames)
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(filePath))
                {
                    // Write CSV header
                    writer.WriteLine("SpeciesID,SpeciesName,MoveID,MoveName");

                    // Write egg move data
                    foreach (var entry in eggMoveData)
                    {
                        string speciesName = (entry.speciesID >= 0 && entry.speciesID < pokeNames.Length) ? pokeNames[entry.speciesID] : $"SPECIES_{entry.speciesID}";
                        foreach (var moveID in entry.moveIDs)
                        {
                            string moveName = (moveID >= 0 && moveID < moveNames.Length) ? moveNames[moveID] : $"MOVE_{moveID}";
                            writer.WriteLine($"{entry.speciesID},{speciesName},{moveID},{moveName}");
                        }
                    }

                }
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Failed to export egg move data to CSV: {ex.Message}");
                return false;
            }
        }

        public static bool ImportEggMoveDataFromCSV(ref List<EggMoveEntry> eggMoveData, string filePath)
        {
            try
            {
                var lines = File.ReadAllLines(filePath);
                var speciesDict = new Dictionary<int, EggMoveEntry>();

                foreach (var line in lines.Skip(1))
                {
                    var values = line.Split(',');
                    if (values.Length < 4) continue;

                    int speciesID = int.Parse(values[0].Trim());
                    int moveID = int.Parse(values[2].Trim());

                    if (!speciesDict.ContainsKey(speciesID))
                    {
                        speciesDict[speciesID] = new EggMoveEntry(speciesID, new List<ushort>());
                    }

                    speciesDict[speciesID].moveIDs.Add((ushort)moveID);
                }

                eggMoveData = speciesDict.Values.ToList();

                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Failed to import egg move data from CSV: {ex.Message}");
                return false;
            }
        }

        private static void ExportTrainersToText(string trainerDataPath, string[] trainerNames, string[] trainerClassNames, string[] pokeNames, string[] itemNames, string[] moveNames, string[] abilityNames)
        {
            // Write the Trainer Data to the Text file
            TrainerFile curTrainerFile = null;
            TrainerProperties curTrainerProperties = null;
            FileStream curTrainerParty = null;
            StreamWriter sw = new StreamWriter(trainerDataPath);

            int trainerCount = Directory.GetFiles(RomInfo.gameDirs[DirNames.trainerProperties].unpackedDir).Length;

            for (int i = 1; i < trainerCount; i++)
            {
                string suffix = "\\" + i.ToString("D4");

                curTrainerProperties = new TrainerProperties((ushort)i,
                    new FileStream(RomInfo.gameDirs[DirNames.trainerProperties].unpackedDir + suffix, FileMode.Open));

                curTrainerParty = new FileStream(RomInfo.gameDirs[DirNames.trainerParty].unpackedDir + suffix, FileMode.Open);

                curTrainerFile = new TrainerFile(curTrainerProperties, curTrainerParty, trainerNames[i]);

                string trainerName = trainerNames[i];
                string trainerClass = trainerClassNames[curTrainerProperties.trainerClass];
                string[] trainerItems = curTrainerProperties.trainerItems.Select(item => item != 0 ? itemNames[(int)item] : "None").ToArray();

                // Create array of party pokemon
                PartyPokemon[] partyPokemon = new PartyPokemon[curTrainerProperties.partyCount];

                // Now that we have the party pokemons, we can declare the arrays to store the data
                string[] monNames = new string[partyPokemon.Length];
                PartyPokemon.GenderAndAbilityFlags[] monFlags = new PartyPokemon.GenderAndAbilityFlags[partyPokemon.Length];
                string[] items = new string[partyPokemon.Length];
                int[] levels = new int[partyPokemon.Length];
                int[] ivs = new int[partyPokemon.Length];
                string[][] moves = new string[partyPokemon.Length][];

                for (int j = 0; j < partyPokemon.Length; j++)
                {
                    // This assumes that the non-empty mons are at the beginning of the party array which they should be
                    // if there is some way for this not to be the case, the program will crash
                    partyPokemon[j] = curTrainerFile.party[j];
                    // Type cast can be done because CountNonEmptyMons() only returns non-empty mons i.e. mons with non-null pokeID
                    monNames[j] = pokeNames[(int)partyPokemon[j].pokeID];
                    monFlags[j] = partyPokemon[j].genderAndAbilityFlags;

                    // Need to account for the case where the mon has no held item
                    if (partyPokemon[j].heldItem != null)
                    {
                        items[j] = itemNames[(int)partyPokemon[j].heldItem];
                    }
                    else
                    {
                        items[j] = "None";
                    }

                    levels[j] = partyPokemon[j].level;
                    ivs[j] = partyPokemon[j].difficulty * 31 / 255;

                    // Need to account for the case where the mon has no moves
                    if (partyPokemon[j].moves == null)
                    {
                        LearnsetData learnset = new LearnsetData((int)partyPokemon[j].pokeID);
                        moves[j] = learnset.GetLearnsetAtLevel(levels[j]).Select(move => moveNames[move]).ToArray();
                    }
                    else
                    {
                        moves[j] = partyPokemon[j].moves.Select(move => moveNames[move]).ToArray();
                    }

                }

                string[] monGenders = new string[partyPokemon.Length];
                string[] abilities = new string[partyPokemon.Length];
                string[] natures = new string[partyPokemon.Length];

                // This function sets the monGenders, abilities and natures arrays
                // We hide this away in a function because it's a bit complex
                // and we don't want to clutter the main function more than it already is
                SetMonGendersAndAbilitiesAndNature(i, curTrainerProperties.trainerClass, partyPokemon, monFlags, ref abilityNames, ref monGenders, ref abilities, ref natures);


                sw.Write(TrainerToDocFormat(i, trainerName, trainerClass, trainerItems, monNames, monGenders, items, abilities, levels, natures, ivs, moves));
            }

            sw.Close();

        }

        private static void ExportMoveDataToCSV(string moveDataPath, string[] moveNames, string[] typeNames)
        {
            StreamWriter sw = new StreamWriter(moveDataPath);

            string[] moveFlags = Enum.GetNames(typeof(MoveData.MoveFlags));
            string[] battleSeqDesc = PokeDatabase.MoveData.battleSequenceDescriptions;

            sw.WriteLine("Move ID,Move Name,Move Type,Move Split,Power,Accuracy,Priority,Side Effect Probability,PP,Range,Flags,Effect Description");

            for (int i = 0; i < moveNames.Length; i++)
            {
                MoveData curMoveDataFile = new MoveData(i);

                // Lambda magic to select the flags that are set, skipping the first enum entry (no flags)
                string moveFlagsString = string.Join("|", moveFlags.Skip(1).Select((flag, index)
                    => (curMoveDataFile.flagField & (1 << index)) != 0 ? flag : "").Where(flag => !string.IsNullOrEmpty(flag)));

                // Use user-friendly range name from MoveData
                string attackRangeString = MoveData.GetAttackRangeName(curMoveDataFile.target);

                string battleSeqDescString = curMoveDataFile.battleeffect < battleSeqDesc.Length ?
                    battleSeqDesc[curMoveDataFile.battleeffect] : "UnknownEffect_" + curMoveDataFile.battleeffect;

                string typeString = (int)curMoveDataFile.movetype < typeNames.Length ?
                    typeNames[(int)curMoveDataFile.movetype] : "UnknownType_" + (int)curMoveDataFile.movetype;

                sw.WriteLine($"{i},{moveNames[i]},{typeString},{curMoveDataFile.split}," +
                             $"{curMoveDataFile.damage},{curMoveDataFile.accuracy},{curMoveDataFile.priority}," +
                             $"{curMoveDataFile.sideEffectProbability},{curMoveDataFile.pp}," +
                             $"{attackRangeString},[{moveFlagsString}],{battleSeqDescString}");
            }

            sw.Close();
        }

        private static void ExportTMHMDataToCSV(string THHMDataPath, string[] pokeNames)
        {
            // Write the TM/HM Data to the CSV file
            PokemonPersonalData curPersonalData = null;
            StreamWriter sw = new StreamWriter(THHMDataPath);

            sw.Write("ID,Name");

            string[] machineMoveNames = TMEditor.ReadMachineMoveNames();
            int totalTMs = PokemonPersonalData.tmsCount + PokemonPersonalData.hmsCount;

            // Write Header (List of all TMs/HMs)
            for (int i = 0; i < totalTMs; i++)
            {
                string currentItem = TMEditor.MachineLabelFromIndex(i);
                sw.Write($",{currentItem} - {machineMoveNames[i]}");

            }

            sw.WriteLine();

            for (int i = 0; i < RomInfo.GetPersonalFilesCount(); i++)
            {
                curPersonalData = new PokemonPersonalData(i);
                sw.Write($"{i},{pokeNames[i]},[");

                // Slight code duplication to PersonalDataEditor here
                for (byte b = 0; b < totalTMs; b++)
                {
                    sw.Write(b == 0 ? "" : ",");
                    sw.Write(curPersonalData.machines.Contains(b) ? "true" : "false");
                }

                sw.WriteLine("]");

            }
            sw.Close();
        }

        private static string TrainerToDocFormat(int index, string trainerName, string trainerClass, string[] trainerItems, string[] monNames, string[] monGenders, string[] items, string[] abilities,
                       int[] levels, string[] natures, int[] ivs, string[][] moves)
        {
            StringBuilder sb = new StringBuilder();

            sb.Append($"[{index}] {trainerClass} {trainerName}");

            // If trainer has at least one item then list all non-zero id items behind the trainer name
            if (trainerItems.Length > 0 && trainerItems[0] != "None")
            {
                sb.Append(" @ (");
                sb.Append(string.Join(", ", trainerItems.Where(item => item != "None")));
                sb.Append(")");
            }

            sb.Append(":\n\n");

            for (int i = 0; i < monNames.Length; i++)
            {
                sb.Append(MonToShowdownFormat(monNames[i], monGenders[i], items[i], abilities[i], levels[i], natures[i], ivs[i], moves[i]));
                sb.Append("\n\n");
            }

            sb.Append("\n\n\n");

            return sb.ToString();
        }

        private static string MonToShowdownFormat(string monName, string gender, string itemName, string ability,
            int level, string nature, int ivs, string[] moves)
        {
            StringBuilder sb = new StringBuilder();

            sb.Append($"{monName}");

            if (gender != "random")
            {
                sb.Append($" ({gender})");
            }

            if (itemName != "None")
            {
                sb.Append($" @ {itemName}");
            }

            sb.Append("\nAbility: " + ability);
            sb.Append("\nLevel: " + level);
            sb.Append("\n" + nature + " Nature");

            sb.Append("\nIVs: " + string.Join(" / ", Enumerable.Repeat(ivs.ToString(), 6)));

            moves = moves.Where(move => (move != "None" && move != "-")).ToArray();

            sb.Append("\n- " + string.Join("\n- ", moves));

            return sb.ToString();

        }

        private static void SetMonGendersAndAbilitiesAndNature(int trainerID, int trainerClassID, PartyPokemon[] partyPokemon,
            PartyPokemon.GenderAndAbilityFlags[] monFlags, ref string[] abilityNames,
            ref string[] monGenders, ref string[] abilities, ref string[] natures)
        {
            bool trainerMale = false;

            trainerMale = DVCalculator.TrainerClassGender.GetTrainerClassGender(trainerClassID);
            DVCalculator.ResetGenderMod(trainerMale);

            // Get Pokemon Genders and Abilities from flags
            for (int j = 0; j < partyPokemon.Length; j++)
            {

                byte baseGenderRatio = new PokemonPersonalData((int)partyPokemon[j].pokeID).genderVec;
                byte genderOverride = (byte)((byte)monFlags[j] & 0x0F); // Get the lower 4 bits
                byte abilityOverride = (byte)((byte)monFlags[j] >> 4); // Get the upper 4 bits

                uint PID = DVCalculator.generatePID((uint)trainerID, (uint)trainerClassID, (uint)partyPokemon[j].pokeID, (byte)partyPokemon[j].level, baseGenderRatio, genderOverride, abilityOverride, partyPokemon[j].difficulty);
                natures[j] = DVCalculator.Natures[DVCalculator.getNatureFromPID(PID)].Split(':')[0];

                switch (genderOverride)
                {
                    case 0: // Random
                        monGenders[j] = "random";
                        break;
                    case 1: // Male
                        monGenders[j] = "M";
                        break;
                    case 2: // Female
                        monGenders[j] = "F";
                        break;
                }

                switch (PID % 2) // Lowest bit of PID determines the ability
                {
                    case 0:
                        abilities[j] = abilityNames[new PokemonPersonalData((int)partyPokemon[j].pokeID).firstAbility];
                        break;
                    case 1:
                        abilities[j] = abilityNames[new PokemonPersonalData((int)partyPokemon[j].pokeID).secondAbility];
                        break;
                }
            }
        }
    }
}
