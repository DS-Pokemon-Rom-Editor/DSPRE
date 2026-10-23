using DSPRE.Resources;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    public class ScriptFile : RomFile
    {
        // Cache for parsed plaintext scripts to avoid reparsing during search
        private static Dictionary<string, (DateTime timestamp, ScriptFile cached)> _plaintextCache = new Dictionary<string, (DateTime, ScriptFile)>();

        private static List<(int fileID, ushort commandID, long offset)> _invalidCommandsEncountered = new List<(int, ushort, long)>();
        private static bool _suppressInvalidCommandErrors = false;

        /// <summary>Clears the plaintext script cache, as when a ROM closes.</summary>
        public static void ClearPlaintextCache()
        {
            _plaintextCache.Clear();
            AppLogger.Info("Script: Plaintext cache cleared.");
        }

        public static List<(int fileID, ushort commandID, long offset)> GetInvalidCommands()
        {
            return new List<(int, ushort, long)>(_invalidCommandsEncountered);
        }

        public static void ClearInvalidCommands()
        {
            _invalidCommandsEncountered.Clear();
        }

        /// <summary>
        /// Controls whether invalid command errors should show popups (false) or be collected silently (true)
        /// </summary>
        public static void SetSuppressInvalidCommandErrors(bool suppress)
        {
            _suppressInvalidCommandErrors = suppress;
        }

        public enum ContainerTypes
        {
            Function,
            Action,
            Script
        };

        public struct ContainerReference
        {
            public uint ID;
            public uint OffsetInFile;
        }

        public List<ScriptCommandContainer> AllScripts = new List<ScriptCommandContainer>();
        public List<ScriptCommandContainer> AllFunctions = new List<ScriptCommandContainer>();
        public List<ScriptActionContainer> AllActions = new List<ScriptActionContainer>();
        public int FileID = -1;
        public bool IsLevelScript = new bool();
        public bool ParseFailedDueToInvalidCommand = false;

        /// <summary>A plaintext export newer than the binary was there but did not parse, so the
        /// binary was read instead. That text holds edits and must not be overwritten.</summary>
        public bool PlaintextParseFailed;

        public bool HasNoScripts { get { return FileID == int.MaxValue; } }

        public static readonly char[] SpecialChars = { 'x', 'X', '#', '.', '_' };

        public ScriptFile(Stream fs, bool readFunctions = true, bool readActions = true, int fileID = -1)
        {
            this.FileID = fileID;

            List<int> scriptOffsets = new List<int>();
            List<int> functionOffsets = new List<int>();
            List<int> movementOffsets = new List<int>();

            using (BinaryReader br = new BinaryReader(fs))
            {
                /* Read script offsets from the header */
                IsLevelScript = true; // Is Level Script as long as magic number FD13 doesn't exist

                try
                {
                    while (true)
                    {
                        long headerPos = br.BaseStream.Position;
                        uint checker = br.ReadUInt16();
                        br.BaseStream.Position -= 0x2;
                        uint value = br.ReadUInt32();

                        if (value == 0 && scriptOffsets.Count == 0)
                        {
                            IsLevelScript = true;
                            break;
                        }

                        if (checker == 0xFD13)
                        {
                            br.BaseStream.Position -= 0x4;
                            IsLevelScript = false;
                            break;
                        }

                        int offsetFromStart = (int)(value + br.BaseStream.Position); // Don't change order of addition
                        scriptOffsets.Add(offsetFromStart);
                    }
                }
                catch (EndOfStreamException)
                {
                    if (!IsLevelScript)
                    {
                        AppMessages.Error("The script file couldn't be read.", "Script error");
                    }
                }

                if (IsLevelScript)
                {
                    return;
                }

                br.ReadUInt16();
                /* Read scripts */
                for (uint current = 0; current < scriptOffsets.Count; current++)
                {
                    int index = scriptOffsets.FindIndex(x => x == scriptOffsets[(int)current]); // Check for UseScript

                    if (index == current)
                    {
                        br.BaseStream.Position = scriptOffsets[(int)current];

                        List<ScriptCommand> cmdList = new List<ScriptCommand>();
                        bool endScript = new bool();
                        bool invalidCommandFound = false;
                        while (!endScript)
                        {
                            ScriptCommand cmd = ReadCommand(br, ref functionOffsets, ref movementOffsets);
                            if (cmd.CmdParams is null)
                            {
                                ParseFailedDueToInvalidCommand = true;
                                invalidCommandFound = true;
                                endScript = true;
                            }
                            else
                            {
                                cmdList.Add(cmd);

                                if (ScriptDatabase.EndCodes.Contains((ushort)cmd.Id))
                                {
                                    endScript = true;
                                }
                            }
                        }

                        AllScripts.Add(new ScriptCommandContainer(current + 1, ContainerTypes.Script, commandList: cmdList));

                        if (invalidCommandFound)
                        {
                            AppLogger.Warn($"Script file {fileID}: Stopped parsing at Script {current + 1} due to invalid command. Remaining scripts, functions, and actions won't be loaded.");
                            return;
                        }
                    }
                    else
                    {
                        AllScripts.Add(new ScriptCommandContainer(current + 1, ContainerTypes.Script, usedScriptID: index + 1));
                    }
                }

                /* Read functions */
                if (readFunctions)
                {
                    for (uint current = 0; current < functionOffsets.Count; current++)
                    {
                        br.BaseStream.Position = functionOffsets[(int)current];
                        int posInList = scriptOffsets.IndexOf(functionOffsets[(int)current]); // Check for UseScript

                        if (posInList == -1)
                        {
                            List<ScriptCommand> cmdList = new List<ScriptCommand>();
                            bool endFunction = new bool();
                            bool invalidCommandFound = false;
                            while (!endFunction)
                            {
                                ScriptCommand command = ReadCommand(br, ref functionOffsets, ref movementOffsets);
                                if (command.CmdParams is null)
                                {
                                    ParseFailedDueToInvalidCommand = true;
                                    invalidCommandFound = true;
                                    endFunction = true;
                                }
                                else
                                {
                                    cmdList.Add(command);
                                    if (ScriptDatabase.EndCodes.Contains((ushort)command.Id))
                                    {
                                        endFunction = true;
                                    }
                                }
                            }

                            AllFunctions.Add(new ScriptCommandContainer(current + 1, ContainerTypes.Function, commandList: cmdList));

                            if (invalidCommandFound)
                            {
                                AppLogger.Warn($"Script file {fileID}: Stopped parsing at Function {current + 1} due to invalid command. Remaining functions and actions won't be loaded.");
                                return;
                            }
                        }
                        else
                        {
                            AllFunctions.Add(new ScriptCommandContainer(current + 1, ContainerTypes.Function, usedScriptID: posInList + 1));
                        }
                    }
                }

                if (readActions)
                {
                    /* Read movements */
                    for (uint current = 0; current < movementOffsets.Count; current++)
                    {
                        br.BaseStream.Position = movementOffsets[(int)current];

                        List<ScriptAction> cmdList = new List<ScriptAction>();
                        bool endMovement = new bool();
                        while (!endMovement)
                        {
                            ushort id = br.ReadUInt16();
                            if (id == 0xFE)
                            {
                                endMovement = true;
                                cmdList.Add(new ScriptAction(id, 0));
                            }
                            else
                            {
                                cmdList.Add(new ScriptAction(id, br.ReadUInt16()));
                            }
                        }

                        AllActions.Add(new ScriptActionContainer(current + 1, commands: cmdList));
                    }
                }
            }
        }

        public ScriptFile(int fileID, bool readFunctions = true, bool readActions = true)
        {
            this.FileID = fileID;

            if (TryReadPlaintextIfNewer())
            {
                return;
            }

            if (!File.Exists(Filesystem.GetScriptPath(fileID)))
            {
                // Reads as an empty file did, without leaving one behind.
                AppLogger.Warn($"Script file {fileID:D4} doesn't exist.");
                IsLevelScript = true;
                return;
            }

            using (FileStream file = GetFileStream(fileID))
            using (MemoryStream copy = new MemoryStream())
            {
                file.CopyTo(copy);
                _read = copy.ToArray();
            }
            _readFull = readFunctions && readActions;
            using (MemoryStream fs = new MemoryStream(_read))
            {
                ScriptFile tempScript = new ScriptFile(fs, readFunctions, readActions, fileID);
                this.AllScripts = tempScript.AllScripts;
                this.AllFunctions = tempScript.AllFunctions;
                this.AllActions = tempScript.AllActions;
                this.IsLevelScript = tempScript.IsLevelScript;
                this.ParseFailedDueToInvalidCommand = tempScript.ParseFailedDueToInvalidCommand;
            }
        }

        static FileStream GetFileStream(int fileID)
        {
            string path = Filesystem.GetScriptPath(fileID);
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }

        public static (string binPath, string txtPath) GetFilePaths(int fileID)
        {
            string binPath = Filesystem.GetScriptPath(fileID);
            string expandedDir = Path.Combine(RomInfo.DspreDir, "expanded", "scripts");
            string txtPath = Path.Combine(expandedDir, $"{fileID:D4}.script");
            return (binPath, txtPath);
        }


        public string ToPlainText(bool includeActions)
        {
            if (FileID < 0) return string.Empty;

            // If parsing stopped due to invalid commands, don't emit partial output.
            // Matches the safety behavior in WritePlainTextFile().
            if (ParseFailedDueToInvalidCommand)
            {
                AppLogger.Warn($"Script file {FileID:D4} wasn't fully parsed due to invalid commands. Skipping plaintext generation to prevent data loss.");
                return string.Empty;
            }

            StringBuilder content = new StringBuilder();

            content.AppendLine("/*");
            content.AppendLine(" * DSPRE Script File");

            string romFileName = Path.GetFileNameWithoutExtension(RomInfo.ProjectName);
            string romFileNameClean = romFileName.EndsWith("_DSPRE_contents")
                ? romFileName.Substring(0, romFileName.Length - "_DSPRE_contents".Length)
                : romFileName;

            content.AppendLine(" * Rom ID: " + romFileNameClean);
            content.AppendLine(" * Game: " + RomInfo.GameFamily);
            content.AppendLine($" * File: {FileID:D4}");
            content.AppendLine($" * Generated: {DateTime.Now}");
            content.AppendLine(" */");
            content.AppendLine();

            RenumberContainers();

            content.AppendLine("//===== SCRIPTS =====//");
            AppendContainerList(content, AllScripts, ScriptFile.ContainerTypes.Script);

            content.AppendLine("//===== FUNCTIONS =====//");
            AppendContainerList(content, AllFunctions, ScriptFile.ContainerTypes.Function);

            if (includeActions)
            {
                content.AppendLine("//===== ACTIONS =====//");
                AppendActionList(content, AllActions, ScriptFile.ContainerTypes.Action);
            }

            return content.ToString();
        }

        public void WritePlainTextFile(string outputPath, bool includeActions)
        {
            if (string.IsNullOrWhiteSpace(outputPath)) return;

            string txt = ToPlainText(includeActions);
            if (string.IsNullOrEmpty(txt)) return;

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            PlainFiles.WriteText(outputPath, txt);
        }


        /// <summary>Reads the .script only when it is newer than the binary, so batch searches see outside edits; results are cached.</summary>
        private bool TryReadPlaintextIfNewer()
        {
            if (FileID < 0)
                return false;

            string txtPath = GetFilePaths(FileID).txtPath;
            string binPath = GetFilePaths(FileID).binPath;

            if (!File.Exists(txtPath))
            {
                return false;
            }

            DateTime txtTimestamp = File.GetLastWriteTimeUtc(txtPath);

            if (File.Exists(binPath) && txtTimestamp <= File.GetLastWriteTimeUtc(binPath))
            {
                return false;
            }

            if (_plaintextCache.TryGetValue(txtPath, out (DateTime timestamp, ScriptFile cached) cached))
            {
                if (cached.timestamp == txtTimestamp && cached.cached != null)
                {
                    // Callers edit these lists in place, so each one gets its own copy.
                    this.AllScripts = CloneContainers(cached.cached.AllScripts);
                    this.AllFunctions = CloneContainers(cached.cached.AllFunctions);
                    this.AllActions = CloneActions(cached.cached.AllActions);
                    this.IsLevelScript = cached.cached.IsLevelScript;
                    return true;
                }
            }

            bool success = TryReadPlainTextFileCore();

            if (success)
            {
                ScriptFile cacheEntry = new ScriptFile(CloneContainers(this.AllScripts), CloneContainers(this.AllFunctions),
                    CloneActions(this.AllActions), FileID);
                cacheEntry.IsLevelScript = this.IsLevelScript;
                _plaintextCache[txtPath] = (txtTimestamp, cacheEntry);
            }
            else
            {
                PlaintextParseFailed = true;
            }

            return success;
        }

        private static List<ScriptCommandContainer> CloneContainers(List<ScriptCommandContainer> source)
            => source?.Select(c => new ScriptCommandContainer(c.ManualUserID, c.ContainerType, c.UsedScriptID,
                c.Commands?.Select(CloneCommand).ToList())).ToList();

        private static ScriptCommand CloneCommand(ScriptCommand c)
        {
            if (c == null) return null;
            ScriptCommand copy = new ScriptCommand(c.Name, null, c.Id);
            copy.CmdParams = c.CmdParams?.Select(p => (byte[])p?.Clone()).ToList();
            return copy;
        }

        private static List<ScriptActionContainer> CloneActions(List<ScriptActionContainer> source)
            => source?.Select(a => new ScriptActionContainer(a.ManualUserID,
                a.Commands?.Select(x => x == null ? null
                    : new ScriptAction { Id = x.Id, RepetitionCount = x.RepetitionCount, Name = x.Name }).ToList()))
                .ToList();

        private bool TryReadPlainTextFileCore()
        {
            string txtPath = GetFilePaths(FileID).txtPath;

            try
            {
                string content = File.ReadAllText(txtPath);

                const string SCRIPTS_HEADER = "//===== SCRIPTS =====//";
                const string FUNCTIONS_HEADER = "//===== FUNCTIONS =====//";
                const string ACTIONS_HEADER = "//===== ACTIONS =====//";

                int scriptsStart = content.IndexOf(SCRIPTS_HEADER);
                int functionsStart = content.IndexOf(FUNCTIONS_HEADER);
                int actionsStart = content.IndexOf(ACTIONS_HEADER);

                if (scriptsStart == -1 || functionsStart == -1 || actionsStart == -1)
                {
                    AppLogger.Error($"Script file {FileID:D4} ({txtPath}) has invalid format. Missing section headers. Binary will be re-extracted.");
                    return false;
                }

                string scriptsSection = content.Substring(
                    scriptsStart + SCRIPTS_HEADER.Length,
                    functionsStart - scriptsStart - SCRIPTS_HEADER.Length
                ).Trim();

                string functionsSection = content.Substring(
                    functionsStart + FUNCTIONS_HEADER.Length,
                    actionsStart - functionsStart - FUNCTIONS_HEADER.Length
                ).Trim();

                string actionsSection = content.Substring(
                    actionsStart + ACTIONS_HEADER.Length
                ).Trim();

                string[] scriptLines = scriptsSection.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                string[] functionLines = functionsSection.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                string[] actionLines = actionsSection.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

                ScriptFile tempScript = new ScriptFile(scriptLines, functionLines, actionLines, FileID);

                if (tempScript.AllScripts == null)
                {
                    AppLogger.Error($"Script file {FileID:D4} ({txtPath}) failed to parse. Binary will be re-extracted.");
                    return false;
                }

                this.AllScripts = tempScript.AllScripts;
                this.AllFunctions = tempScript.AllFunctions;
                this.AllActions = tempScript.AllActions;
                this.IsLevelScript = tempScript.IsLevelScript;

                AppLogger.Info($"Script file {FileID:D4} loaded from plaintext: {txtPath}");
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Script file {FileID:D4} ({txtPath}) - Exception: {ex.Message}. Binary will be re-extracted.");
                return false;
            }
        }

        public void RenumberContainers()
        {
            Dictionary<uint, uint> scriptMap = new Dictionary<uint, uint>();
            Dictionary<uint, uint> functionMap = new Dictionary<uint, uint>();
            Dictionary<uint, uint> actionMap = new Dictionary<uint, uint>();

            if (AllScripts != null)
            {
                for (int i = 0; i < AllScripts.Count; i++)
                {
                    uint oldID = AllScripts[i].ManualUserID;
                    uint newID = (uint)(i + 1);
                    if (!scriptMap.ContainsKey(oldID))
                    {
                        scriptMap.Add(oldID, newID);
                    }
                    AllScripts[i].ManualUserID = newID;
                }
            }

            if (AllFunctions != null)
            {
                for (int i = 0; i < AllFunctions.Count; i++)
                {
                    uint oldID = AllFunctions[i].ManualUserID;
                    uint newID = (uint)(i + 1);
                    if (!functionMap.ContainsKey(oldID))
                    {
                        functionMap.Add(oldID, newID);
                    }
                    AllFunctions[i].ManualUserID = newID;
                }
            }

            if (AllActions != null)
            {
                for (int i = 0; i < AllActions.Count; i++)
                {
                    uint oldID = AllActions[i].ManualUserID;
                    uint newID = (uint)(i + 1);
                    if (!actionMap.ContainsKey(oldID))
                    {
                        actionMap.Add(oldID, newID);
                    }
                    AllActions[i].ManualUserID = newID;
                }
            }

            List<ScriptCommandContainer> containersToUpdate = new List<ScriptCommandContainer>();
            if (AllScripts != null) containersToUpdate.AddRange(AllScripts);
            if (AllFunctions != null) containersToUpdate.AddRange(AllFunctions);

            foreach (ScriptCommandContainer container in containersToUpdate)
            {
                if (container.UsedScriptID > 0)
                {
                    if (scriptMap.TryGetValue((uint)container.UsedScriptID, out uint newScriptID))
                    {
                        container.UsedScriptID = (int)newScriptID;
                    }
                }

                if (container.Commands != null)
                {
                    for (int i = 0; i < container.Commands.Count; i++)
                    {
                        ScriptCommand cmd = container.Commands[i];
                        bool commandUpdated = false;

                        if (cmd.CmdParams != null && cmd.Id.HasValue)
                    {
                            Dictionary<ushort, ScriptCommandInfo> dict = RomInfo.GetScriptCommandInfoDict();
                        if (dict != null && dict.TryGetValue(cmd.Id.Value, out ScriptCommandInfo cmdInfo) && cmdInfo.ParameterTypes != null)
                        {
                            for (int p = 0; p < Math.Min(cmd.CmdParams.Count, cmdInfo.ParameterTypes.Count); p++)
                            {
                                    ScriptParameter.ParameterType paramType = cmdInfo.ParameterTypes[p];
                                uint oldVal = 0;
                                
                                if (cmd.CmdParams[p].Length == 1) oldVal = cmd.CmdParams[p][0];
                                else if (cmd.CmdParams[p].Length == 2) oldVal = BitConverter.ToUInt16(cmd.CmdParams[p], 0);
                                else if (cmd.CmdParams[p].Length == 4) oldVal = BitConverter.ToUInt32(cmd.CmdParams[p], 0);

                                uint newVal = oldVal;
                                bool valChanged = false;

                                if (paramType == ScriptParameter.ParameterType.Function)
                                {
                                    if (functionMap.TryGetValue(oldVal, out uint mappedVal))
                                    {
                                        newVal = mappedVal;
                                        valChanged = true;
                                    }
                                }
                                else if (paramType == ScriptParameter.ParameterType.Action)
                                {
                                    if (actionMap.TryGetValue(oldVal, out uint mappedVal))
                                    {
                                        newVal = mappedVal;
                                        valChanged = true;
                                    }
                                }

                                if (valChanged && newVal != oldVal)
                                {
                                    if (cmd.CmdParams[p].Length == 1) cmd.CmdParams[p] = new byte[] { (byte)newVal };
                                    else if (cmd.CmdParams[p].Length == 2) cmd.CmdParams[p] = BitConverter.GetBytes((ushort)newVal);
                                    else if (cmd.CmdParams[p].Length == 4) cmd.CmdParams[p] = BitConverter.GetBytes(newVal);
                                    
                                    commandUpdated = true;
                                }
                            }
                        }
                    }

                    if (commandUpdated)
                    {
                        // Recreated so its text form shows the new reference.
                        container.Commands[i] = new ScriptCommand((ushort)cmd.Id, cmd.CmdParams);
                    }
                }
            }
        }
    }

        private static void AppendContainerList(StringBuilder content, List<ScriptCommandContainer> commandList, ScriptFile.ContainerTypes containerType)
        {
            for (int i = 0; i < commandList.Count; i++)
            {
                ScriptCommandContainer scriptCommandContainer = commandList[i];

                /* Write header */
                string header = containerType + " " + (i + 1);
                content.Append(header + ':' + Environment.NewLine);

                /* If current command is identical to another, print UseScript instead of commands */
                if (scriptCommandContainer.UsedScriptID < 0)
                {
                    for (int j = 0; j < scriptCommandContainer.Commands.Count; j++)
                    {
                        ScriptCommand command = scriptCommandContainer.Commands[j];
                        if (!ScriptDatabase.EndCodes.Contains(command.Id))
                        {
                            content.Append('\t');
                        }

                        content.Append(command.Name + Environment.NewLine);
                    }
                }
                else
                {
                    content.Append('\t' + "UseScript_#" + scriptCommandContainer.UsedScriptID + Environment.NewLine);
                }

                content.AppendLine();
            }
        }

        private static void AppendActionList(StringBuilder content, List<ScriptActionContainer> commandList, ScriptFile.ContainerTypes containerType)
        {
            for (int i = 0; i < commandList.Count; i++)
            {
                ScriptActionContainer currentCommand = commandList[i];

                string header = containerType + " " + (i + 1);
                content.Append(header + ':' + Environment.NewLine);

                for (int j = 0; j < currentCommand.Commands.Count; j++)
                {
                    ScriptAction command = currentCommand.Commands[j];
                    if (!ScriptDatabase.MovementEndCodes.Contains(command.Id))
                    {
                        content.Append('\t');
                    }

                    content.Append(command.Name + Environment.NewLine);
                }

                content.AppendLine();
            }
        }

        /// <summary>Writes the file as a .script in expanded/scripts.</summary>
        public void WritePlainTextFile()
        {
            if (FileID < 0)
                return;

            // expanded/scripts is Rotom's source root there, and a .script in it gets converted over
            // the Rotom source on the next Script editor open.
            if (RomInfo.HasRotomProject)
                return;

            if (ParseFailedDueToInvalidCommand)
            {
                AppLogger.Warn($"Script file {FileID:D4} wasn't fully parsed due to invalid commands. Skipping plaintext export to prevent data loss.");
                return;
            }

            if (PlaintextParseFailed)
            {
                AppLogger.Warn($"Script file {FileID:D4}: its .script didn't parse, so it wasn't regenerated.");
                return;
            }

            string txtPath = GetFilePaths(FileID).txtPath;
            Directory.CreateDirectory(Path.GetDirectoryName(txtPath));

            try
            {
                PlainFiles.WriteText(txtPath, BuildPlainTextFile());
                AppLogger.Info($"Script file {FileID:D4} written to plaintext: {txtPath}");
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Couldn't write plaintext script file {txtPath}: {ex.Message}");
            }
        }

        /// <summary>The whole .script export, header included, as <see cref="WritePlainTextFile()"/> writes it.</summary>
        public string BuildPlainTextFile()
        {
            StringBuilder content = new StringBuilder();

            content.AppendLine("/*");
            content.AppendLine(" * DSPRE Script File");

            string romFileName = Path.GetFileNameWithoutExtension(RomInfo.ProjectName);
            string romFileNameClean = romFileName.EndsWith("_DSPRE_contents")
                ? romFileName.Substring(0, romFileName.Length - "_DSPRE_contents".Length)
                : romFileName;
            content.AppendLine(" * Rom ID: " + romFileNameClean);
            content.AppendLine(" * Game: " + RomInfo.GameFamily);
            content.AppendLine($" * File: {FileID:D4}");
            content.AppendLine($" * Generated: {DateTime.Now}");
            content.AppendLine(" */");
            content.AppendLine();

            RenumberContainers();

            content.AppendLine("//===== SCRIPTS =====//");
            AppendContainerList(content, AllScripts, ScriptFile.ContainerTypes.Script);

            content.AppendLine("//===== FUNCTIONS =====//");
            AppendContainerList(content, AllFunctions, ScriptFile.ContainerTypes.Function);

            content.AppendLine("//===== ACTIONS =====//");
            AppendActionList(content, AllActions, ScriptFile.ContainerTypes.Action);

            return content.ToString();
        }

        /// <summary>Reloads the command database and reparses every script, for a custom or replaced database.</summary>
        /// <param name="databasePath">Path to the JSON database file to load</param>
        /// <param name="progressCallback">Optional callback to report progress during reparse</param>
        /// <returns>List of remaining invalid commands after reload, empty if all successful</returns>
        public static List<(int fileID, ushort commandID, long offset)> ReloadDatabaseAndReparseAll(string databasePath, Action<int, int> progressCallback = null)
        {
            string romDatabaseFolder = Path.GetDirectoryName(databasePath);

            // Another ROM's database would be applied to this ROM's scripts, and its name files
            // rewritten with this ROM's names.
            if (!IsCurrentRomDatabaseFolder(romDatabaseFolder))
            {
                AppLogger.Warn($"Script: {databasePath} belongs to another ROM, so it wasn't loaded into this one.");
                return new List<(int, ushort, long)>();
            }

            ScriptDatabaseJsonLoader.InitializeFromJson(databasePath, RomInfo.GameVersion);

            RomInfo.ReloadScriptCommandDictionaries();

            // The name dictionaries read the text archives.
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.TextArchives });

            Resources.ScriptDatabase.InitializePokemonNames();
            Resources.ScriptDatabase.InitializeItemNames();
            Resources.ScriptDatabase.InitializeMoveNames();
            Resources.ScriptDatabase.InitializeTrainerNames();
            Resources.ScriptDatabase.InitializeNpcTradeNames();
            Resources.ScriptDatabase.InitializeTrainerClassNames();

            // Rotom reads these enum JSONs, so they are rewritten to match this ROM.
            Resources.ScriptDatabase.ExportEnumJsons(romDatabaseFolder);

            ClearInvalidCommands();
            SetSuppressInvalidCommandErrors(true);
            try
            {
                RegenerateLegacyExports(progressCallback);
                WriteDatabaseHashMarker();
            }
            finally
            {
                SetSuppressInvalidCommandErrors(false);
            }

            return GetInvalidCommands();
        }

        /// <summary>
        /// Rewrites the legacy <c>.script</c> exports so they are named by the current command database.
        /// Nothing is deleted: Rotom sources are never touched, an export edited outside DSPRE (newer than
        /// its binary) is its own source and is kept, and a file that no longer decompiles keeps its
        /// previous text. In a Rotom project every binary is still parsed, to report unknown commands.
        /// </summary>
        private static void RegenerateLegacyExports(Action<int, int> progressCallback)
        {
            string expandedDir = Path.Combine(RomInfo.DspreDir, "expanded", "scripts");
            bool writeExports = !RomInfo.HasRotomProject;
            if (writeExports) Directory.CreateDirectory(expandedDir);

            int scriptCount = Filesystem.GetScriptCount();
            int rewritten = 0, kept = 0;

            for (int i = 0; i < scriptCount; i++)
            {
                try
                {
                    (string binPath, string txtPath) = GetFilePaths(i);
                    if (!File.Exists(binPath))
                    {
                        progressCallback?.Invoke(i + 1, scriptCount);
                        continue;
                    }

                    ScriptFile fromBinary;
                    using (FileStream fs = GetFileStream(i))
                        fromBinary = new ScriptFile(fs, true, true, i);

                    if (writeExports)
                    {
                        bool edited = File.Exists(txtPath)
                            && File.GetLastWriteTimeUtc(txtPath) > File.GetLastWriteTimeUtc(binPath);
                        string text = edited ? null : fromBinary.ToPlainText(includeActions: true);

                        if (string.IsNullOrEmpty(text))
                        {
                            kept++;
                            if (!edited)
                                AppLogger.Warn($"Script {i:D4} no longer decompiles with this database; its previous text is kept.");
                        }
                        else
                        {
                            PlainFiles.WriteText(txtPath, text);
                            // Newer than the export, so it reads as unedited.
                            File.SetLastWriteTimeUtc(binPath, DateTime.UtcNow.AddSeconds(1));
                            rewritten++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Error($"Couldn't regenerate script {i:D4}: {ex.Message}");
                }

                progressCallback?.Invoke(i + 1, scriptCount);
            }

            ClearPlaintextCache();
            AppLogger.Info($"Script: {Plural.Of(rewritten, ".script export")} regenerated, {kept} kept as they were.");
        }

        private static void WriteDatabaseHashMarker()
        {
            if (RomInfo.HasRotomProject) return;
            string currentDbHash = GetDatabaseHash();
            if (string.IsNullOrEmpty(currentDbHash)) return;
            string expandedDir = Path.Combine(RomInfo.DspreDir, "expanded", "scripts");
            Directory.CreateDirectory(expandedDir);
            PlainFiles.WriteText(Path.Combine(expandedDir, ".database_hash"), currentDbHash);
        }

        /// <summary>The loaded ROM's folder under <c>edited_databases</c>.</summary>
        public static string CurrentRomDatabaseFolder()
        {
            string baseFileName = Path.GetFileNameWithoutExtension(RomInfo.ProjectName ?? "");
            string romFileNameClean = baseFileName.EndsWith(RomInfo.FolderSuffix)
                ? baseFileName.Substring(0, baseFileName.Length - RomInfo.FolderSuffix.Length)
                : baseFileName;
            return Path.Combine(AppPaths.DatabasePath, "edited_databases", romFileNameClean);
        }

        public static bool IsCurrentRomDatabaseFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(RomInfo.ProjectName)) return false;
            try
            {
                return string.Equals(Path.GetFullPath(folder).TrimEnd('\\', '/'),
                    Path.GetFullPath(CurrentRomDatabaseFolder()).TrimEnd('\\', '/'),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex) { AppLogger.WarnOnce("ScriptFile.IsCurrentRomDatabaseFolder", ex); return false; }
        }

        /// <summary>The current script command database hash, to notice changes.</summary>
        private static string GetDatabaseHash()
        {
            string databasePath = Path.Combine(CurrentRomDatabaseFolder(), "scrcmd_database.json");

            if (!File.Exists(databasePath))
                return string.Empty;

            using (MD5 md5 = System.Security.Cryptography.MD5.Create())
            {
                using (FileStream stream = File.OpenRead(databasePath))
                {
                    byte[] hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        /// <summary>Rebuilds binaries older than their .script in expanded/scripts, during Save ROM.</summary>
        public static bool BuildRequiredBins() => BuildRequiredBins(out _);

        /// <param name="error">Names every .script that could not be built, which were all left untouched.</param>
        public static bool BuildRequiredBins(out string error)
        {
            error = null;
            // The name dictionaries read the text archives.
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.TextArchives });

            // .script parsing needs these for names such as SPECIES_PIKACHU.
            Resources.ScriptDatabase.InitializePokemonNames();
            Resources.ScriptDatabase.InitializeItemNames();
            Resources.ScriptDatabase.InitializeMoveNames();
            Resources.ScriptDatabase.InitializeTrainerNames();
            Resources.ScriptDatabase.InitializeNpcTradeNames();
            Resources.ScriptDatabase.InitializeTrainerClassNames();

            string expandedDir = Path.Combine(RomInfo.DspreDir, "expanded", "scripts");

            if (!Directory.Exists(expandedDir))
            {
                AppLogger.Info("Script: No expanded scripts directory found, skipping .bin rebuild.");
                return true;
            }

            string[] expandedScriptFiles = Directory.GetFiles(expandedDir, "*.script", SearchOption.AllDirectories);
            int newerBinCount = 0;
            int rebuiltCount = 0;
            List<string> failed = new List<string>();

            for (int i = 0; i < expandedScriptFiles.Length; i++)
            {
                string expandedScriptFile = expandedScriptFiles[i];
                string fileName = Path.GetFileNameWithoutExtension(expandedScriptFile);

                int scriptID;

                try
                {
                    scriptID = int.Parse(fileName);
                }
                catch
                {
                    AppLogger.Error($"Skipping invalid script file name: {fileName}");
                    continue;
                }

                string binPath = ScriptFile.GetFilePaths(scriptID).binPath;

                if (File.Exists(binPath) && File.GetLastWriteTimeUtc(binPath) > File.GetLastWriteTimeUtc(expandedScriptFile))
                {
                    newerBinCount++;
                    continue;
                }

                try
                {
                    ScriptFile scriptFile = new ScriptFile(scriptID);
                    // Falling back to the binary here would rebuild the old script and then regenerate
                    // the .script from it, throwing the edit away.
                    if (scriptFile.PlaintextParseFailed)
                    {
                        failed.Add(Path.GetFileName(expandedScriptFile) + " doesn't parse");
                        continue;
                    }

                    if (!scriptFile.SaveToFileDefaultDir(scriptID, false))
                    {
                        failed.Add(Path.GetFileName(expandedScriptFile) + " couldn't be compiled");
                        continue;
                    }
                    rebuiltCount++;

                    // Touch the .script so reopening the ROM doesn't regenerate it.
                    File.SetLastWriteTimeUtc(expandedScriptFile, DateTime.UtcNow);
                }
                catch (Exception ex)
                {
                    AppLogger.Error($"Couldn't rebuild script {scriptID:D4} from plaintext: {ex.Message}");
                    failed.Add(Path.GetFileName(expandedScriptFile) + ": " + ex.Message);
                }
            }

            AppLogger.Info($"Script: {rebuiltCount} .bin files built from .script, {newerBinCount} .bin files skipped because they were newer than the .script");

            if (failed.Count > 0)
            {
                error = "These script files couldn't be built and were left as they are:\n" + string.Join("\n", failed);
                AppLogger.Error(error);
                return false;
            }

            return true;
        }

        public override string ToString()
        {
            return (IsLevelScript ? "Level script " : "Script file ") + this.FileID;
        }

        public ScriptFile(List<ScriptCommandContainer> scripts, List<ScriptCommandContainer> functions, List<ScriptActionContainer> movements, int fileID = -1)
        {
            this.FileID = fileID;
            this.AllScripts = scripts;
            this.AllFunctions = functions;
            this.AllActions = movements;
            this.IsLevelScript = false;
        }

        public ScriptFile(IEnumerable<string> scriptLines, IEnumerable<string> functionLines, IEnumerable<string> actionLines, int fileID = -1)
        {
            bool functionEndCondition(List<(int linenum, string text)> source, int x, ushort? id)
            {
                return source[x].text.TrimEnd().IgnoreCaseEquals(RomInfo.ScriptCommandNamesDict[0x0002]) //End
                       || source[x].text.IndexOf(RomInfo.ScriptCommandNamesDict[0x0016] + ' ' + ContainerTypes.Function.ToString(), StringComparison.InvariantCultureIgnoreCase) >= 0 //Jump Function_#
                       || source[x].text.TrimEnd().IgnoreCaseEquals(RomInfo.ScriptCommandNamesDict[0x001B])
                       || ScriptDatabase.EndCodes.Contains(id);
            } //Return

            bool scriptEndCondition(List<(int linenum, string text)> source, int x, ushort? id)
            {
                return source[x].text.TrimEnd().IgnoreCaseEquals(RomInfo.ScriptCommandNamesDict[0x0002]) //End
                       || source[x].text.IndexOf(RomInfo.ScriptCommandNamesDict[0x0016] + ' ' + ContainerTypes.Function.ToString()) >= 0 //Jump Function_#
                       || ScriptDatabase.EndCodes.Contains(id);
            }

            AllScripts = ReadCommandsFromLines(scriptLines.ToList(), ContainerTypes.Script, scriptEndCondition); //Jump + whitespace
            if (AllScripts is null)
            {
                return;
            }

            if (AllScripts.Count <= 0)
            {
                this.FileID = int.MaxValue;
                return;
            }

            if (functionLines != null)
            {
                AllFunctions = ReadCommandsFromLines(functionLines.ToList(), ContainerTypes.Function, functionEndCondition); //Jump + whitespace
                if (AllFunctions is null)
                {
                    return;
                }
            }

            if (actionLines != null)
            {
                AllActions = ReadActionsFromLines(actionLines.ToList());
                if (AllActions is null)
                {
                    return;
                }
            }

            this.FileID = fileID;
        }

        private ScriptCommand ReadCommand(BinaryReader dataReader, ref List<int> functionOffsets, ref List<int> actionOffsets)
        {
            ushort id = dataReader.ReadUInt16();
            List<byte[]> parameterList = new List<byte[]>();

            /* How to read parameters for different commands for DPPt*/
            switch (RomInfo.GameFamily)
            {
                case GameFamilies.DP:
                case GameFamilies.Plat:
                    switch (id)
                    {
                        case 0x16: //Jump
                        case 0x1A: //Call
                            ProcessRelativeJump(dataReader, ref parameterList, ref functionOffsets);
                            break;
                        case 0x17: //JumpIfObjID
                        case 0x18: //JumpIfBgID
                        case 0x19: //JumpIfPlayerDir
                        case 0x1C: //JumpIf
                        case 0x1D: //CallIf
                            // The first parameter is a comparison for JumpIf and CallIf, a direction for JumpIfPlayerDir
                            // and an event for JumpIfObjID.
                            parameterList.Add(new byte[] { dataReader.ReadByte() });
                            ProcessRelativeJump(dataReader, ref parameterList, ref functionOffsets);
                            break;
                        case 0x5E: // Movement
                            parameterList.Add(BitConverter.GetBytes(dataReader.ReadUInt16()));
                            ProcessRelativeJump(dataReader, ref parameterList, ref actionOffsets);
                            break;
                        case 0x1CF:
                        case 0x1D0:
                        case 0x1D1:
                            {
                                byte parameter1 = dataReader.ReadByte();
                                parameterList.Add(new byte[] { parameter1 });
                                if (parameter1 == 0x2)
                                {
                                    parameterList.Add(dataReader.ReadBytes(2)); //Read additional u16 if first param read is 2
                                }
                            }
                            break;
                        case 0x21D:
                            {
                                ushort parameter1 = dataReader.ReadUInt16();
                                parameterList.Add(BitConverter.GetBytes(parameter1));

                                switch (parameter1)
                                {
                                    case 0:
                                    case 1:
                                    case 2:
                                    case 3:
                                        parameterList.Add(dataReader.ReadBytes(2));
                                        parameterList.Add(dataReader.ReadBytes(2));
                                        break;
                                    case 4:
                                    case 5:
                                        parameterList.Add(dataReader.ReadBytes(2));
                                        break;
                                    case 6:
                                        break;
                                }
                            }
                            break;
                        case 0x235:
                            {
                                short parameter1 = dataReader.ReadInt16();
                                parameterList.Add(BitConverter.GetBytes(parameter1));

                                switch (parameter1)
                                {
                                    case 0x1:
                                    case 0x3:
                                        parameterList.Add(dataReader.ReadBytes(2));
                                        parameterList.Add(dataReader.ReadBytes(2));
                                        parameterList.Add(dataReader.ReadBytes(2));
                                        break;
                                    case 0x4:
                                        parameterList.Add(dataReader.ReadBytes(2));
                                        parameterList.Add(dataReader.ReadBytes(2));
                                        break;
                                    case 0x0:
                                    case 0x6:
                                        parameterList.Add(dataReader.ReadBytes(2));
                                        break;
                                    default:
                                        break;
                                }
                            }
                            break;
                        case 0x23E:
                            {
                                short parameter1 = dataReader.ReadInt16();
                                parameterList.Add(BitConverter.GetBytes(parameter1));

                                switch (parameter1)
                                {
                                    case 0x1:
                                    case 0x3:
                                        parameterList.Add(dataReader.ReadBytes(2));
                                        break;
                                    case 0x5:
                                    case 0x6:
                                        parameterList.Add(dataReader.ReadBytes(2));
                                        parameterList.Add(dataReader.ReadBytes(2));
                                        break;
                                    default:
                                        break;
                                }
                            }
                            break;
                        case 0x2C5:
                            {
                                if (RomInfo.GameVersion == GameVersions.Platinum)
                                {
                                    parameterList.Add(dataReader.ReadBytes(2));
                                    parameterList.Add(dataReader.ReadBytes(2));
                                }
                                else
                                {
                                    goto default;
                                }
                            }
                            break;
                        case 0x2C6:
                        case 0x2C9:
                        case 0x2CA:
                        case 0x2CD:
                            if (RomInfo.GameVersion == GameVersions.Platinum)
                            {
                                break;
                            }
                            else
                            {
                                goto default;
                            }
                        case 0x2CF:
                            if (RomInfo.GameVersion == GameVersions.Platinum)
                            {
                                parameterList.Add(dataReader.ReadBytes(2));
                                parameterList.Add(dataReader.ReadBytes(2));
                            }
                            else
                            {
                                goto default;
                            }

                            break;
                        default:
                            AddParametersToList(ref parameterList, id, dataReader);
                            break;
                    }

                    break;
                case GameFamilies.HGSS:
                    switch (id)
                    {
                        case 0x16: //Jump
                        case 0x1A: //Call
                            ProcessRelativeJump(dataReader, ref parameterList, ref functionOffsets);
                            break;
                        case 0x17: //JumpIfObjID
                        case 0x18: //JumpIfBgID
                        case 0x19: //JumpIfPlayerDir
                        case 0x1C: //JumpIf
                        case 0x1D: //CallIf
                            parameterList.Add(new byte[] { dataReader.ReadByte() }); //in the case of JumpIf and CallIf, the first param is a comparisonOperator
                            ProcessRelativeJump(dataReader, ref parameterList, ref functionOffsets);
                            break;
                        case 0x5E: // Movement
                            parameterList.Add(BitConverter.GetBytes(dataReader.ReadUInt16())); //in the case of Movement, the first param is an overworld ID
                            ProcessRelativeJump(dataReader, ref parameterList, ref actionOffsets);
                            break;
                        case 0x190:
                        case 0x191:
                        case 0x192:
                            {
                                byte parameter1 = dataReader.ReadByte();
                                parameterList.Add(new byte[] { parameter1 });
                                if (parameter1 == 0x2)
                                {
                                    parameterList.Add(dataReader.ReadBytes(2));
                                }
                            }
                            break;
                        case 0x1D1: // The parameter count depends on the first parameter
                            {
                                short parameter1 = dataReader.ReadInt16();
                                parameterList.Add(BitConverter.GetBytes(parameter1));
                                switch (parameter1)
                                {
                                    case 0x0:
                                    case 0x1:
                                    case 0x2:
                                    case 0x3:
                                        parameterList.Add(dataReader.ReadBytes(2));
                                        parameterList.Add(dataReader.ReadBytes(2));
                                        break;
                                    case 0x4:
                                    case 0x5:
                                        parameterList.Add(dataReader.ReadBytes(2));
                                        break;
                                    case 0x6:
                                        break;
                                    case 0x7:
                                        parameterList.Add(dataReader.ReadBytes(2));
                                        break;
                                    default:
                                        break;
                                }
                            }
                            break;
                        case 0x1E9: // The parameter count depends on the first parameter
                            {
                                short parameter1 = dataReader.ReadInt16();
                                parameterList.Add(BitConverter.GetBytes(parameter1));
                                switch (parameter1)
                                {
                                    case 0x0:
                                        break;
                                    case 0x1:
                                    case 0x2:
                                    case 0x3:
                                        parameterList.Add(dataReader.ReadBytes(2));
                                        break;
                                    case 0x4:
                                        break;
                                    case 0x5:
                                    case 0x6:
                                        parameterList.Add(dataReader.ReadBytes(2));
                                        parameterList.Add(dataReader.ReadBytes(2));
                                        break;
                                    case 0x7:
                                    case 0x8:
                                        break;
                                    default:
                                        break;
                                }
                            }
                            break;
                        default:
                            AddParametersToList(ref parameterList, id, dataReader);
                            break;
                    }

                    break;
            }

            return new ScriptCommand(id, parameterList);
        }

        private void ProcessRelativeJump(BinaryReader dataReader, ref List<byte[]> parameterList, ref List<int> offsetsList)
        {
            int relativeOffset = dataReader.ReadInt32();
            int offsetFromScriptFileStart = (int)(relativeOffset + dataReader.BaseStream.Position);

            if (!offsetsList.Contains(offsetFromScriptFileStart))
            {
                offsetsList.Add(offsetFromScriptFileStart);
            }

            int functionNumber = offsetsList.IndexOf(offsetFromScriptFileStart);
            if (functionNumber < 0)
            {
                throw new InvalidOperationException();
            }

            parameterList.Add(BitConverter.GetBytes(functionNumber + 1));
        }

        private void AddParametersToList(ref List<byte[]> parameterList, ushort id, BinaryReader dataReader)
        {
            try
            {
                foreach (int bytesToRead in RomInfo.ScriptCommandParametersDict[id])
                {
                    parameterList.Add(dataReader.ReadBytes(bytesToRead));
                }
            }
            catch (NullReferenceException)
            {
                long offset = dataReader.BaseStream.Position;
                _invalidCommandsEncountered.Add((FileID, id, offset));

                if (!_suppressInvalidCommandErrors)
                {
                    AppMessages.Error("DSPRE doesn't know script command " + id + " (at 0x" + offset.ToString("X") + ").", "Unknown script command");
                }
                parameterList = null;
                return;
            }
            catch
            {
                long offset = dataReader.BaseStream.Position;
                _invalidCommandsEncountered.Add((FileID, id, offset));

                if (!_suppressInvalidCommandErrors)
                {
                    AppMessages.Error("Script command " + id + " couldn't be read (at 0x" + offset.ToString("X") + ").", "Unknown script command");
                }
                parameterList = null;
                return;
            }
        }

        private void AddReference(ref List<ScriptReference> references, ushort commandID, List<byte[]> parameterList, int pos, ScriptCommandContainer cont)
        {
            if (ScriptDatabase.CommandsWithRelativeJump.TryGetValue(commandID, out int parameterWithRelativeJump))
            {
                uint invokedID = BitConverter.ToUInt32(parameterList[parameterWithRelativeJump], 0); // Jump, Call

                if (commandID == 0x005E)
                    references.Add(new ScriptReference(cont.ContainerType, cont.ManualUserID, ContainerTypes.Action, invokedID, pos - 4));
                else
                {
                    references.Add(new ScriptReference(cont.ContainerType, cont.ManualUserID, ContainerTypes.Function, invokedID, pos - 4));
                }
            }
        }

        private List<(int linenum, string text)> PreprocessLines(List<string> linelist)
        {
            List<(int linenum, string text)> lineSource = new List<(int linenum, string text)>();
            bool inBlockComment = false;

            for (int l = 0; l < linelist.Count; l++)
            {
                string line = linelist[l];

                if (inBlockComment)
                {
                    int endComment = line.IndexOf("*/");
                    if (endComment >= 0)
                    {
                        line = line.Substring(endComment + 2);
                        inBlockComment = false;
                    }
                    else
                    {
                        continue;
                    }
                }

                while (!inBlockComment && line.Contains("/*"))
                {
                    int startComment = line.IndexOf("/*");
                    int endComment = line.IndexOf("*/", startComment);

                    if (endComment >= 0)
                    {
                        line = line.Substring(0, startComment) + line.Substring(endComment + 2);
                    }
                    else
                    {
                        line = line.Substring(0, startComment);
                        inBlockComment = true;
                    }
                }

                int inlineComment = line.IndexOf("//");
                if (inlineComment >= 0)
                {
                    line = line.Substring(0, inlineComment);
                }

                line = line.Trim();
                if (!string.IsNullOrWhiteSpace(line))
                {
                    lineSource.Add((l, line));
                }
            }

            return lineSource;
        }

        private List<ScriptCommandContainer> ReadCommandsFromLines(List<string> linelist, ContainerTypes containerType, Func<List<(int linenum, string text)>, int, ushort?, bool> endConditions)
        {
            List<(int linenum, string text)> lineSource = PreprocessLines(linelist);

            List<ScriptCommandContainer> ls = new List<ScriptCommandContainer>();
            int i = 0;

            try
            {
                uint scriptNumber = 0;

                while (i < lineSource.Count)
                {
                    if (scriptNumber == 0)
                    {
                        int positionOfScriptNumber;
                        int positionOfScriptKeyword = lineSource[i].text.IndexOf(containerType.ToString(), StringComparison.InvariantCultureIgnoreCase);

                        if (positionOfScriptKeyword > 0)
                        {
                            AppMessages.Error("Expected a script, function or action line here: \"" + lineSource[i] + '"', "Script error");
                            return null;
                        }
                        else if (positionOfScriptKeyword < 0)
                        {
                            i++;
                            continue;
                        }
                        else
                        {
                            if ((positionOfScriptNumber = lineSource[i].text.IndexOfFirstNumber()) < positionOfScriptKeyword)
                            {
                                AppMessages.Error("This script or function has no label.", "Script error");
                                return null;
                            }
                        }

                        scriptNumber = uint.Parse(lineSource[i++].text.Substring(positionOfScriptNumber).Split()[0].Replace(":", ""));
                    }

                    if (lineSource[i].text.IndexOf("UseScript", StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        int useScriptNumber = short.Parse(lineSource[i].text.Substring(1 + lineSource[i].text.IndexOf('#')));
                        ls.Add(new ScriptCommandContainer(scriptNumber, containerType, useScriptNumber));
                        i++;
                    }
                    else
                    {
                        /* Read script commands */
                        List<ScriptCommand> cmdList = new List<ScriptCommand>();
                        ScriptCommand lastRead;

                        do
                        {
                            lastRead = new ScriptCommand(lineSource[i].text, lineSource[i].linenum + 1);
                            if (lastRead.Id is null)
                            {
                                return null;
                            }

                            cmdList.Add(lastRead);
                        }
                        while (!endConditions(lineSource, i++, lastRead.Id));

                        ls.Add(new ScriptCommandContainer(scriptNumber, containerType, commandList: cmdList));
                    }

                    scriptNumber = 0;
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                AppMessages.Error($"The script ends before its last section is closed (line {lineSource[i].linenum}).", "Script error");
                return null;
            }

            return ls;
        }

        private List<ScriptActionContainer> ReadActionsFromLines(List<string> linelist)
        {
            List<(int linenum, string text)> lineSource = new List<(int linenum, string text)>();

            for (int l = 0; l < linelist.Count; l++)
            {
                string cur = linelist[l];
                if (!string.IsNullOrWhiteSpace(cur))
                {
                    lineSource.Add((l, cur));
                }
            }

            List<ScriptActionContainer> ls = new List<ScriptActionContainer>();
            int i = 0;

            try
            {
                uint actionNumber = 0;

                while (i < lineSource.Count)
                {
                    if (actionNumber == 0)
                    {
                        int positionOfActionNumber;
                        int positionOfActionKeyword = lineSource[i].text.IndexOf(ContainerTypes.Action.ToString(), StringComparison.InvariantCultureIgnoreCase);

                        if (positionOfActionKeyword > 0)
                        {
                            AppMessages.Error("Expected a script, function or action line here: \"" + lineSource[i] + '"', "Script error");
                            return null;
                        }
                        else if (positionOfActionKeyword < 0)
                        {
                            i++;
                            continue;
                        }
                        else
                        {
                            if ((positionOfActionNumber = lineSource[i].text.IndexOfFirstNumber()) < positionOfActionKeyword)
                            {
                                AppMessages.Error("This action has no label.", "Script error");
                                return null;
                            }
                        }

                        actionNumber = uint.Parse(lineSource[i].text.Substring(positionOfActionNumber).Split()[0].Replace(":", ""));
                        i++;
                    }

                    List<ScriptAction> cmdList = new List<ScriptAction>();
                    /* Read script actions */
                    do
                    {
                        ScriptAction toAdd = new ScriptAction(lineSource[i].text, lineSource[i].linenum + 1);
                        if (toAdd.Id is null)
                        {
                            return null;
                        }

                        cmdList.Add(toAdd);
                    }
                    while (!lineSource[i++].text.IgnoreCaseEquals(RomInfo.ScriptActionNamesDict[0x00FE]));

                    ls.Add(new ScriptActionContainer(actionNumber, commands: cmdList));
                    actionNumber = 0;
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                AppMessages.Error($"The script ends before its last section is closed (line {i}).", "Script error");
                return null;
            }

            return ls;
        }

        public override byte[] ToByteArray()
        {
            MemoryStream newData = new MemoryStream();
            using (BinaryWriter writer = new BinaryWriter(newData))
            {
                List<ContainerReference> scriptOffsets = new List<ContainerReference>(); //uint OFFSET, int Function/Script/Action ID
                List<ContainerReference> functionOffsets = new List<ContainerReference>();
                List<ContainerReference> actionOffsets = new List<ContainerReference>();

                List<ScriptReference> refList = new List<ScriptReference>();

                /* Allocate enough space for script pointers, which we do not know yet */
                try
                {
                    writer.BaseStream.Position += AllScripts.Count * 0x4;
                    writer.Write((ushort)0xFD13); // Signal the end of header section
                    List<ScriptCommandContainer> useScriptCallers = new List<ScriptCommandContainer>();

                    /* Write scripts */
                    foreach (ScriptCommandContainer currentScript in AllScripts)
                    {
                        if (currentScript.UsedScriptID == -1)
                        {
                            scriptOffsets.Add(new ContainerReference()
                            {
                                ID = currentScript.ManualUserID,
                                OffsetInFile = (uint)writer.BaseStream.Position
                            }
                            );

                            foreach (ScriptCommand currentCmd in currentScript.Commands)
                            {
                                writer.Write((ushort)currentCmd.Id);

                                List<byte[]> parameterList = currentCmd.CmdParams;
                                foreach (byte[] b in parameterList)
                                {
                                    writer.Write(b);
                                }

                                /* If command calls a function/movement, store reference position */
                                AddReference(ref refList, (ushort)currentCmd.Id, parameterList, (int)writer.BaseStream.Position, currentScript);
                            }
                        }
                        else
                        {
                            useScriptCallers.Add(currentScript);
                        }
                    }

                    int scriptsCount = scriptOffsets.Count;
                    foreach (ScriptCommandContainer caller in useScriptCallers)
                    {
                        bool resolved = false;
                        for (int i = 0; i < scriptsCount; i++)
                        {
                            ContainerReference scriptReference = scriptOffsets[i];

                            if (scriptReference.ID == caller.UsedScriptID)
                            {
                                scriptOffsets.Add(new ContainerReference()
                                {
                                    ID = caller.ManualUserID,
                                    OffsetInFile = scriptReference.OffsetInFile
                                }); // If script has UseScript, copy offset
                                resolved = true;
                                break;
                            }
                        }

                        // A missing header entry would shift every later script id by one.
                        if (!resolved)
                        {
                            AppMessages.Error($"Script {caller.ManualUserID} uses script {caller.UsedScriptID}, which doesn't exist, so the file can't be saved.", "Missing script");
                            return null;
                        }
                    }

                    /* Write functions */
                    foreach (ScriptCommandContainer currentFunction in AllFunctions)
                    {
                        if (currentFunction.UsedScriptID == -1)
                        {
                            functionOffsets.Add(new ContainerReference()
                            {
                                ID = currentFunction.ManualUserID,
                                OffsetInFile = (uint)writer.BaseStream.Position
                            }
                            );

                            foreach (ScriptCommand currentCmd in currentFunction.Commands)
                            {
                                writer.Write((ushort)currentCmd.Id);

                                List<byte[]> parameterList = currentCmd.CmdParams;
                                foreach (byte[] b in parameterList)
                                {
                                    writer.Write(b);
                                }

                                /* If command calls a function/movement, store reference position */

                                AddReference(ref refList, (ushort)currentCmd.Id, parameterList, (int)writer.BaseStream.Position, currentFunction);
                            }
                        }
                        else
                        {
                            int functionUsescript = currentFunction.UsedScriptID - 1;
                            if (functionUsescript >= scriptOffsets.Count)
                            {
                                AppMessages.Error($"Function {currentFunction.ManualUserID} uses script {currentFunction.UsedScriptID}, which doesn't exist, so the file can't be saved.", "Missing script");
                                return null;
                            }

                            functionOffsets.Add(new ContainerReference()
                            {
                                ID = currentFunction.ManualUserID,
                                OffsetInFile = scriptOffsets.Find(x => x.ID == currentFunction.UsedScriptID).OffsetInFile
                            });
                        }
                    }

                    // Movements must be halfword aligned
                    if (writer.BaseStream.Position % 2 == 1)
                    { //Check if the writer's head is on an odd byte
                        writer.Write((byte)0x00); //Add padding
                    }

                    /* Write movements */
                    foreach (ScriptActionContainer currentAction in AllActions)
                    {
                        actionOffsets.Add(new ContainerReference()
                        {
                            ID = currentAction.ManualUserID,
                            OffsetInFile = (uint)writer.BaseStream.Position
                        });

                        foreach (ScriptAction currentCmd in currentAction.Commands)
                        {
                            writer.Write((ushort)currentCmd.Id);
                            writer.Write((ushort)currentCmd.RepetitionCount);
                        }
                    }

                    /* Write script offsets to header */
                    writer.BaseStream.Position = 0x0;

                    scriptOffsets = scriptOffsets.OrderBy(x => x.ID).ToList(); //Write script offsets to header in the correct order
                    for (int i = 0; i < scriptOffsets.Count; i++)
                    {
                        writer.Write(scriptOffsets[i].OffsetInFile - (uint)writer.BaseStream.Position - 0x4);
                    }

                    SortedSet<uint> undeclaredFuncs = new SortedSet<uint>();
                    SortedSet<uint> undeclaredActions = new SortedSet<uint>();

                    SortedSet<uint> uninvokedFuncs = new SortedSet<uint>(AllFunctions.Select(x => x.ManualUserID).ToArray());
                    SortedSet<uint> unreferencedActions = new SortedSet<uint>(AllActions.Select(x => x.ManualUserID).ToArray());


                    for (int i = 0; i < refList.Count; i++)
                    {
                        writer.BaseStream.Position = refList[i].InvokedAt; //place seek head on parameter that is supposed to store the jump address
                        ContainerReference result;

                        if (refList[i].TypeOfInvoked is ContainerTypes.Action)
                        { //isApplyMovement
                            result = actionOffsets.Find(entry => entry.ID == refList[i].InvokedID);

                            if (result.Equals(default(ContainerReference)))
                            {
                                undeclaredActions.Add(refList[i].InvokedID);
                            }
                            else
                            {
                                int relativeOffset = (int)(result.OffsetInFile - refList[i].InvokedAt - 4);
                                writer.Write(relativeOffset);
                                unreferencedActions.Remove(refList[i].InvokedID);
                            }
                        }
                        else
                        {
                            result = functionOffsets.Find(entry => entry.ID == refList[i].InvokedID);

                            if (result.Equals(default(ContainerReference)))
                            {
                                undeclaredFuncs.Add(refList[i].InvokedID);
                            }
                            else
                            {
                                int relativeOffset = (int)(result.OffsetInFile - refList[i].InvokedAt - 4);
                                writer.Write(relativeOffset);

                                if (FunctionIsInvoked(refList, uninvokedFuncs, refList[i].InvokedID, 0))
                                {
                                    uninvokedFuncs.Remove(refList[i].InvokedID);
                                }

                            }
                        }
                    }

                    string errorMsg = "";
                    if (undeclaredFuncs.Count > 0)
                    {
                        string[] errorFunctionsUndeclared = undeclaredFuncs.ToArray().Select(x => x.ToString()).ToArray();
                        errorMsg += "These functions are called but don't exist: " + Environment.NewLine + string.Join(separator: ",", errorFunctionsUndeclared);
                        errorMsg += Environment.NewLine;
                    }

                    if (undeclaredActions.Count > 0)
                    {
                        string[] errorActionsUndeclared = undeclaredActions.ToArray().Select(x => x.ToString()).ToArray();
                        errorMsg += "These actions are used but don't exist: " + Environment.NewLine + string.Join(separator: ",", errorActionsUndeclared);
                        errorMsg += Environment.NewLine;
                    }

                    if (!string.IsNullOrEmpty(errorMsg))
                    {
                        AppMessages.Error(errorMsg + Environment.NewLine + "The script file wasn't saved.", "Script not saved");
                        errorMsg = "";
                        return null;
                    }

                    if (uninvokedFuncs.Count > 0)
                    {
                        string[] orphanedFunctions = uninvokedFuncs.ToArray().Select(x => x.ToString()).ToArray();
                        errorMsg += "These functions aren't called by anything: " + Environment.NewLine + string.Join(", ", orphanedFunctions);
                        errorMsg += Environment.NewLine;
                        errorMsg += "\nA function is only saved when a script or a used function calls it.";
                        errorMsg += Environment.NewLine;
                        errorMsg += Environment.NewLine;
                    }

                    if (unreferencedActions.Count > 0)
                    {
                        string[] orphanedActions = unreferencedActions.ToArray().Select(x => x.ToString()).ToArray();
                        errorMsg += "These actions aren't used by anything: " + Environment.NewLine + string.Join(", ", orphanedActions);
                        errorMsg += Environment.NewLine;
                        errorMsg += "\nAn action is only saved when a script or a used function uses it.";
                        errorMsg += Environment.NewLine;
                        errorMsg += Environment.NewLine;
                    }

                    if (!string.IsNullOrEmpty(errorMsg))
                    {
                        AppMessages.Info(errorMsg + Environment.NewLine + "Unused functions and actions are lost when the file is reloaded.", "Unused code");
                        errorMsg = "";
                    }
                }
                catch (NullReferenceException nre)
                {
                    AppLogger.Error(nre.ToString());
                    return null;
                }
            }

            return newData.ToArray();
        }

        private bool FunctionIsInvoked(List<ScriptReference> refList, SortedSet<uint> uninvokedFuncsSet, uint funcID, int callCount = 0, uint? excludedCaller = null)
        {
            if (callCount >= 30)
            {
                AppMessages.Error("The script couldn't be saved. Copy its code somewhere safe before you go on.", "Script not saved");
                return false;
            }

            AppLogger.Debug("Checking calls of function " + funcID + (excludedCaller == null ? "" : " excluding Function " + excludedCaller + " as the caller."));

            if (!uninvokedFuncsSet.Contains(funcID))
            {
                AppLogger.Debug("Function " + funcID + " has already been invoked before. Nothing to check.");
                return true; //Abort
            }

            if (refList is null || refList.Count <= 0)
            {
                return false;
            }

            ScriptReference sr = refList.Find(x => x.InvokedID == funcID && (excludedCaller == null || x.CallerID != excludedCaller));

            if (sr is null)
            {
                AppLogger.Debug("No reference found.");
                return false;
            }

            if (sr.TypeOfCaller is ContainerTypes.Script)
            {
                AppLogger.Debug("Function " + funcID + " is directly called by Script " + sr.CallerID);
                return true;
            }

            if (sr.TypeOfCaller is ContainerTypes.Function)
            {
                if (FunctionIsInvoked(refList, uninvokedFuncsSet, sr.CallerID, ++callCount, excludedCaller: sr.InvokedID))
                { //check if caller function is invoked as well
                    AppLogger.Debug("Function " + funcID + " is called by Function " + sr.CallerID);
                    return true;
                }
            }

            AppLogger.Debug("Function " + funcID + " is unused");
            return false;
        }

        // The binary this file was read from. The writer lays a file out its own way (script order, shared tails,
        // padding), so a file whose scripts did not change is written back as these bytes instead.
        private byte[] _read;
        private bool _readFull;

        private byte[] ReadBytesIfUnchanged(int id)
        {
            if (_read == null || !_readFull || id != FileID) return null;
            byte[] now = ToByteArray();
            if (now == null) return null;
            using MemoryStream ms = new MemoryStream(_read);
            byte[] asRead = new ScriptFile(ms, true, true, FileID).ToByteArray();
            return asRead != null && now.AsSpan().SequenceEqual(asRead) ? _read : null;
        }

        public bool SaveToFileDefaultDir(int IDtoReplace, bool showSuccessMessage = true)
        {
            bool success;
            if (ReadBytesIfUnchanged(IDtoReplace) is byte[] read)
            {
                string path = Filesystem.GetScriptPath(IDtoReplace);
                if (showSuccessMessage) AppMessages.Info($"Saved {GetType().Name} {IDtoReplace:D4}.");
                // Still the file on disk: nothing to write, and no Rotom source to regenerate.
                if (File.Exists(path) && File.ReadAllBytes(path).AsSpan().SequenceEqual(read)) return true;
                ArchiveMembers.Put(RomInfo.DirNames.Scripts, IDtoReplace, read);
                success = true;
            }
            else success = SaveToFileDefaultDir(RomInfo.DirNames.Scripts, IDtoReplace, showSuccessMessage);

            if (success)
            {
                WritePlainTextFile();
                _ = ScriptSourceSync.BinaryWrittenAsync(IDtoReplace);
            }

            return success;
        }

        public void SaveToFileExplorePath(string suggestedFileName, bool blindmode)
        {
            string chosenPath = AppMessages.PickSaveFile("Export script file",
                "Script file (*.scr)|*.scr", suggestedFileName);

            if (string.IsNullOrEmpty(chosenPath))
            {
                return;
            }

            if (blindmode)
            {
                string path = Filesystem.GetScriptPath(FileID);
                PlainFiles.Copy(path, chosenPath, overwrite: true);

                AppMessages.Notice(IsLevelScript ? "Level script exported." : "Exported the last saved version of this script file.");
            }
            else
            {
                this.SaveToFile(chosenPath, showSuccessMessage: true);
            }
        }
    }
}
