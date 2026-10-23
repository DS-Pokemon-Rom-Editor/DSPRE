using DSPRE.Resources;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace DSPRE.ROMFiles
{
    public class ScriptCommand
    {

        public ushort? Id;
        public List<byte[]> CmdParams;
        public string Name;

        public ScriptCommand(ushort id, List<byte[]> parameterData)
        {
            this.Id = id;
            CmdParams = parameterData;

            Dictionary<ushort, ScriptCommandInfo> commandInfoDict = RomInfo.GetScriptCommandInfoDict();
            ScriptCommandInfo cmdInfo = null;
            commandInfoDict?.TryGetValue(id, out cmdInfo);

            if (cmdInfo != null && !string.IsNullOrEmpty(cmdInfo.Name))
            {
                Name = cmdInfo.Name;
            }
            else if (!RomInfo.ScriptCommandNamesDict.TryGetValue(id, out Name))
            {
                Name = $"CMD_{id:X3}";
            }

            List<ScriptParameter.ParameterType> paramTypes = cmdInfo?.ParameterTypes;

            // Format parameters based on their types
            if (paramTypes != null && paramTypes.Count > 0 && parameterData != null)
            {
                for (int i = 0; i < Math.Min(paramTypes.Count, parameterData.Count); i++)
                {
                    ScriptParameter param = new ScriptParameter(parameterData[i], paramTypes[i]);
                    Name += " " + param.DisplayValue;
                }
            }
            else if (parameterData != null)
            {
                foreach (byte[] param in parameterData)
                {
                    Name += " " + new ScriptParameter(param, ScriptParameter.ParameterType.Integer).DisplayValue;
                }
            }
        }

        /// <summary>Builds one directly from an already-known name and parameters, without going through
        /// the command database. Used where a command is being described rather than assembled.</summary>
        public ScriptCommand(string commandName, List<byte[]> parameterData, ushort? commandId = null)
        {
            Name = commandName;
            CmdParams = parameterData ?? new List<byte[]>();
            Id = commandId;
        }

        public ScriptCommand(string wholeLine, int lineNumber = 0)
        {
            Name = wholeLine;
            CmdParams = new List<byte[]>();

            string processedLine = ProcessBracketedItems(wholeLine);
            string[] nameParts = processedLine.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries); // Separate command code from parameters
            /* Get command id, which is always first in the description */

            if (RomInfo.ScriptCommandNamesReverseDict.TryGetValue(nameParts[0].ToLower(), out ushort cmdID))
            {
                Id = cmdID;
            }
            else
            {
                try
                {
                    Id = ushort.Parse(nameParts[0].PurgeSpecial(ScriptFile.SpecialChars), nameParts[0].GetNumberStyle());
                }
                catch
                {
                    string details;
                    if (wholeLine.Contains(':') && wholeLine.ContainsNumber())
                    {
                        details = "The script or function above may be missing its End. Only functions can end with Return.";
                    }
                    else
                    {
                        details = "It doesn't look like a script command.";
                    }

                    AppMessages.Error($"Line {lineNumber} can't be read: \"{wholeLine}\".\n\n{details}", "Script not saved");
                    return;
                }
            }

            /* Read parameters from remainder of the description */
            byte[] parametersSizeArr = RomInfo.ScriptCommandParametersDict[(ushort)Id];

            int paramLength = 0;
            int paramsProcessed = 0;

            if (parametersSizeArr.Length > 0 && parametersSizeArr.First() == 0xFF)
            {
                int firstParamValue = int.Parse(nameParts[1].PurgeSpecial(ScriptFile.SpecialChars), nameParts[1].GetNumberStyle());
                byte firstParamSize = parametersSizeArr[1];

                CmdParams.Add(firstParamValue.ToByteArrayChooseSize(firstParamSize));
                paramsProcessed++;

                int i = 2;
                int optionsCount = 0;

                bool found = false;
                while (i < parametersSizeArr.Length)
                {
                    paramLength = parametersSizeArr[i + 1];

                    if (parametersSizeArr[i] == firstParamValue)
                    {
                        // The sizes for the chosen option, after the option byte; for example CMD 0x235 with option 5:

                        // { 0xFF, 2,
                        // 0, 1,   2,
                        // 1, 3,   2, 2, 2,
                        // 2, 0,
                        // 3, 3,   2, 2, 2,
                        // 4, 2,   2, 2,
                        // 5, 3,   (2, 2, 2) => this will be the parameters subarray
                        // 6, 1,   2
                        // },
                        byte[] subParametersSize = parametersSizeArr.SubArray(i + 2, paramLength++);

                        byte[] temp = new byte[1 + subParametersSize.Length];

                        temp[0] = firstParamSize;

                        Array.Copy(subParametersSize, 0, temp, 1, temp.Length - 1);

                        parametersSizeArr = temp;
                        found = true;
                        break;
                    }

                    i += 2 + paramLength;
                    optionsCount++;
                }

                if (!found)
                {
                    AppMessages.Error($"{nameParts[0]} needs a first parameter from 0 to {optionsCount}.\n\n" +
                                    $"Line {lineNumber}: {wholeLine}", "Error");
                    Id = null;
                    return;
                }
            }
            else if (parametersSizeArr.Length == 1 && parametersSizeArr.First() == 0)
            {
                paramLength = 0;
            }
            else
            {
                paramLength = parametersSizeArr.Length;
            }

            if (nameParts.Length - 1 == paramLength)
            {
                for (int i = paramsProcessed; i < paramLength; i++)
                {

                    if (RomInfo.ScriptComparisonOperatorsReverseDict.TryGetValue(nameParts[i + 1].ToLower(), out cmdID))
                    { //Check succeeds when command is like "asdfg LESS" or "asdfg DIFFERENT"
                        CmdParams.Add(new byte[] { (byte)cmdID });
                    }
                    else
                    { //Not a comparison
                        /* Convert strings of parameters to the correct datatypes */
                        NumberStyles numStyle = nameParts[i + 1].GetNumberStyle();
                        if (!nameParts[i + 1].StartsWith("SEQ_")
                            && !nameParts[i + 1].StartsWith("SPECIES_")
                            && !nameParts[i + 1].StartsWith("ITEM_")
                            && !nameParts[i + 1].StartsWith("MOVE_")
                            && !nameParts[i + 1].StartsWith("TRAINER_")
                            && !nameParts[i + 1].StartsWith("NPC_TRADE_"))
                        {
                            nameParts[i + 1] = nameParts[i + 1].PurgeSpecial(ScriptFile.SpecialChars);
                        }

                        int result = 0;

                        try
                        {
                            result = int.Parse(nameParts[i + 1], numStyle);
                        }
                        catch (FormatException)
                        {
                            try
                            {
                                string paramToCheck = CheckAndCompareParam(nameParts[i + 1]);
                                KeyValuePair<ushort, string> first = ScriptDatabase.SpecialOverworlds.FirstOrDefault(x => x.Value.IgnoreCaseEquals(paramToCheck));
                                if (!string.IsNullOrWhiteSpace(first.Value))
                                {
                                    result = first.Key;
                                }
                                else
                                {
                                    KeyValuePair<byte, string> direction = ScriptDatabase.OverworldDirections.FirstOrDefault(x => x.Value.IgnoreCaseEquals(paramToCheck));
                                    if (!string.IsNullOrWhiteSpace(direction.Value))
                                    {
                                        result = direction.Key;
                                    }
                                    else
                                    {
                                        KeyValuePair<ushort, string> pokemon = ScriptDatabase.PokemonNames.FirstOrDefault(x => x.Value.IgnoreCaseEquals(paramToCheck));
                                        if (!string.IsNullOrWhiteSpace(pokemon.Value))
                                        {
                                            result = pokemon.Key;
                                        }
                                        else
                                        {
                                            KeyValuePair<ushort, string> item = ScriptDatabase.ItemNames.FirstOrDefault(x => x.Value.IgnoreCaseEquals(paramToCheck));
                                            if (!string.IsNullOrWhiteSpace(item.Value))
                                            {
                                                result = item.Key;
                                            }
                                            else
                                            {
                                                KeyValuePair<ushort, string> move = ScriptDatabase.MoveNames.FirstOrDefault(x => x.Value.IgnoreCaseEquals(paramToCheck));
                                                if (!string.IsNullOrWhiteSpace(move.Value))
                                                {
                                                    result = move.Key;
                                                }
                                                else
                                                {
                                                    KeyValuePair<ushort, string> sound = ScriptDatabase.SoundNames.FirstOrDefault(x => x.Value.IgnoreCaseEquals(paramToCheck));
                                                    if (!string.IsNullOrWhiteSpace(sound.Value))
                                                    {
                                                        result = sound.Key;
                                                    }
                                                    else
                                                    {
                                                        KeyValuePair<ushort, string> trainerClass = ScriptDatabase.TrainerClassNames.FirstOrDefault(x => x.Value.IgnoreCaseEquals(paramToCheck));
                                                        KeyValuePair<ushort, string> trainer = ScriptDatabase.TrainerNames.FirstOrDefault(x => x.Value.IgnoreCaseEquals(paramToCheck));
                                                        KeyValuePair<ushort, string> trade = ScriptDatabase.NpcTradeNames.FirstOrDefault(x => x.Value.IgnoreCaseEquals(paramToCheck));
                                                        // Class names also start with TRAINER_, so they are looked up first.
                                                        if (!string.IsNullOrWhiteSpace(trainerClass.Value))
                                                        {
                                                            result = trainerClass.Key;
                                                        }
                                                        else if (!string.IsNullOrWhiteSpace(trainer.Value))
                                                        {
                                                            result = trainer.Key;
                                                        }
                                                        else if (!string.IsNullOrWhiteSpace(trade.Value))
                                                        {
                                                            result = trade.Key;
                                                        }
                                                        else
                                                        {
                                                            AppMessages.Error($"{paramToCheck} isn't a condition, overworld, direction, Pokémon, item, move, sound, trainer, trainer class, trade, or script, function or action number.\n\n" +
                                                                $"Line {lineNumber}: {wholeLine}", "Invalid identifier");
                                                            Id = null;
                                                            return;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            catch (ArgumentException ex)
                            {
                                AppMessages.Error($"{ex.Message}\n\nLine {lineNumber}: {wholeLine}",
                                    "Invalid syntax");
                                Id = null;
                                return;
                            }
                        }

                        try
                        {
                            CmdParams.Add(result.ToByteArrayChooseSize(parametersSizeArr[i]));
                        }
                        catch (OverflowException)
                        {
                            string errorMsg = $"{nameParts[i + 1]} on line {lineNumber} must be 0 to {Math.Pow(2, 8 * parametersSizeArr[i]) - 1}.";
                            AppLogger.Error($"ScriptCommand parse error: {errorMsg} | Command: {nameParts[0]} (ID: 0x{Id:X3}) | Full line: {wholeLine} | Expected param size: {parametersSizeArr[i]} bytes | This may indicate a database error for conditional commands.");
                            AppMessages.Error(errorMsg + $"\n\nCommand: {nameParts[0]} (ID: 0x{Id:X3})\nFull line: {wholeLine}\n\nIf this is a conditional command such as UnionGroup, check its parameter sizes in the command database.", "Argument error");
                            Id = null;
                        }
                    }
                }
            }
            else
            {
                AppMessages.Error($"Wrong number of parameters for command {nameParts[0]} at line {lineNumber}.\n" +
                                $"Received: {nameParts.Length - 1}\n" +
                                $"Expected: {paramLength}", "Error");
                Id = null;
            }

            if (Id != null)
            {
                Dictionary<ushort, ScriptCommandInfo> commandInfoDict = RomInfo.GetScriptCommandInfoDict();
                ScriptCommandInfo cmdInfo = null;
                commandInfoDict?.TryGetValue((ushort)Id, out cmdInfo);

                if (cmdInfo != null && !string.IsNullOrEmpty(cmdInfo.Name))
                {
                    Name = cmdInfo.Name;
                }
                else if (!RomInfo.ScriptCommandNamesDict.TryGetValue((ushort)Id, out Name))
                {
                    Name = $"CMD_{Id:X3}";
                }

                List<ScriptParameter.ParameterType> paramTypes = cmdInfo?.ParameterTypes;

                if (paramTypes != null && paramTypes.Count > 0 && CmdParams != null)
                {
                    for (int i = 0; i < Math.Min(paramTypes.Count, CmdParams.Count); i++)
                    {
                        ScriptParameter param = new ScriptParameter(CmdParams[i], paramTypes[i]);
                        Name += " " + param.DisplayValue;
                    }
                }
                else if (CmdParams != null)
                {
                    foreach (byte[] param in CmdParams)
                    {
                        Name += " " + new ScriptParameter(param, ScriptParameter.ParameterType.Integer).DisplayValue;
                    }
                }
            }
        }

        private string ProcessBracketedItems(string line)
        {
            if (!line.Contains('[')) return line;

            StringBuilder result = new StringBuilder(line);
            int currentPos = 0;

            while (true)
            {
                int start = result.ToString().IndexOf('[', currentPos);
                if (start == -1) break;

                int end = result.ToString().IndexOf(']', start);
                if (end == -1) break;

                // Process only the current bracket pair
                for (int i = start + 1; i < end; i++)
                {
                    if (result[i] == ' ')
                    {
                        result[i] = '§';
                    }
                }

                currentPos = end + 1;
            }

            return result.ToString();
        }

        private string FindClosestMatch(string input, IEnumerable<string> possibilities, int threshold = 3)
        {
            input = input.Trim('[', ']').Replace(" ", "").ToLower();

            var closest = possibilities
                .Select(x => new
                {
                    Name = x,
                    Distance = CoreExtensions.Levenshtein(
                        input,
                        x.Replace(" ", "").ToLower()
                    )
                })
                .Where(x => x.Distance <= threshold)
                .OrderBy(x => x.Distance)
                .FirstOrDefault();

            return closest?.Name;
        }

        private string CheckAndCompareParam(string parameter)
        {

            // Check for Pokemon names first
            KeyValuePair<ushort, string> pokemon = ScriptDatabase.PokemonNames.FirstOrDefault(x =>
                x.Value.IgnoreCaseEquals(parameter));
            if (!string.IsNullOrWhiteSpace(pokemon.Value))
            {
                return pokemon.Value;
            }
            KeyValuePair<ushort, string> item = ScriptDatabase.ItemNames.FirstOrDefault(x =>
                x.Value.IgnoreCaseEquals(parameter));
            if (!string.IsNullOrWhiteSpace(item.Value))
            {
                return item.Value;
            }
            KeyValuePair<ushort, string> move = ScriptDatabase.MoveNames.FirstOrDefault(x =>
                x.Value.IgnoreCaseEquals(parameter));
            if (!string.IsNullOrWhiteSpace(move.Value))
            {
                return move.Value;
            }
            KeyValuePair<ushort, string> sound = ScriptDatabase.SoundNames.FirstOrDefault(x =>
                x.Value.IgnoreCaseEquals(parameter));
            if (!string.IsNullOrWhiteSpace(sound.Value))
            {
                return sound.Value;
            }
            KeyValuePair<ushort, string> trainerClass = ScriptDatabase.TrainerClassNames.FirstOrDefault(x =>
                x.Value.IgnoreCaseEquals(parameter));
            if (!string.IsNullOrWhiteSpace(trainerClass.Value))
            {
                return trainerClass.Value;
            }
            KeyValuePair<ushort, string> trainer = ScriptDatabase.TrainerNames.FirstOrDefault(x =>
                x.Value.IgnoreCaseEquals(parameter));
            if (!string.IsNullOrWhiteSpace(trainer.Value))
            {
                return trainer.Value;
            }
            KeyValuePair<ushort, string> trade = ScriptDatabase.NpcTradeNames.FirstOrDefault(x =>
                x.Value.IgnoreCaseEquals(parameter));
            if (!string.IsNullOrWhiteSpace(trade.Value))
            {
                return trade.Value;
            }

            string closestItem = FindClosestMatch(parameter, ScriptDatabase.ItemNames.Values);
            if (!string.IsNullOrWhiteSpace(closestItem))
            {
                throw new ArgumentException($"'{parameter}' isn't an item.\nDid you mean {closestItem}?");
            }
            string closestPokemon = FindClosestMatch(parameter, ScriptDatabase.PokemonNames.Values);
            if (!string.IsNullOrWhiteSpace(closestPokemon))
            {
                throw new ArgumentException($"'{parameter}' isn't a Pokémon.\nDid you mean {closestPokemon}?");
            }
            string closestMove = FindClosestMatch(parameter, ScriptDatabase.MoveNames.Values);
            if (!string.IsNullOrWhiteSpace(closestMove))
            {
                throw new ArgumentException($"'{parameter}' isn't a move.\nDid you mean {closestMove}?");
            }
            string closestSound = FindClosestMatch(parameter, ScriptDatabase.SoundNames.Values);
            if (!string.IsNullOrWhiteSpace(closestSound))
            {
                throw new ArgumentException($"'{parameter}' isn't a sound.\nDid you mean {closestSound}?");
            }
            string closestTrainer = FindClosestMatch(parameter, ScriptDatabase.TrainerNames.Values);
            if (!string.IsNullOrWhiteSpace(closestTrainer))
            {
                throw new ArgumentException($"'{parameter}' isn't a trainer.\nDid you mean {closestTrainer}?");
            }
            string closestClass = FindClosestMatch(parameter, ScriptDatabase.TrainerClassNames.Values);
            if (!string.IsNullOrWhiteSpace(closestClass))
            {
                throw new ArgumentException($"'{parameter}' isn't a trainer class.\nDid you mean {closestClass}?");
            }
            string closestTrade = FindClosestMatch(parameter, ScriptDatabase.NpcTradeNames.Values);
            if (!string.IsNullOrWhiteSpace(closestTrade))
            {
                throw new ArgumentException($"'{parameter}' isn't a trade.\nDid you mean {closestTrade}?");
            }

            return parameter;
        }
    }
}
