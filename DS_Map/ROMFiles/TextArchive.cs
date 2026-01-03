using DSPRE.CharMaps;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Class to store message data from DS Pokémon games
    /// </summary>
    public class TextArchive
    {
        #region Fields

        public int ID { get;}
        public List<string> messages;
        private UInt16 key = 0;

        #endregion Fields

        #region Constructors (1)

        public TextArchive(int ID, List<string> msg = null)
        {
            this.ID = ID;

            if (msg != null)
            {
                messages = msg;
                return;
            }

            // First try to read from plain text file if it exists
            if (TryReadJsonFile())
            {
                return;
            }

            // If not, extract from the .bin file
            if (!ReadFromBinFile())
            {
                MessageBox.Show($"Failed to read messages from .bin file {ID:D4}. Contents were replaced with empty message!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                messages = new List<string> { "" };
                return;
            }

        }


        #endregion Constructors (1)

        #region Methods (2)

        public static (string binPath, string jsonPath) GetFilePaths(int ID)
        {
            string baseDir = gameDirs[DirNames.textArchives].unpackedDir;
            string binPath = Path.Combine(baseDir, $"{ID:D4}");
            string expandedDir = TextConverter.GetExpandedFolderPath();
            string jsonPath = Path.Combine(expandedDir, $"{ID:D4}.json");
            return (binPath, jsonPath);
        }

        public static bool BuildRequiredBins()
        {
            string expandedDir = TextConverter.GetExpandedFolderPath();

            if (!Directory.Exists(expandedDir))
            {
                AppLogger.Info("Text Archive: No expanded text archive directory found, skipping .bin rebuild.");
                return true;
            }

            var expandedTextFiles = Directory.GetFiles(expandedDir, "*.json", SearchOption.AllDirectories);
            int unchangedBinCount = 0;

            for (int i = 0; i < expandedTextFiles.Length; i++)
            {
                string expandedTextFile = expandedTextFiles[i];
                string fileName = Path.GetFileNameWithoutExtension(expandedTextFile);

                int archiveID;

                try
                {
                    archiveID = int.Parse(fileName);
                }
                catch
                {
                    AppLogger.Error($"Skipping invalid text archive file name: {fileName}");
                    continue;
                }

                string binPath = GetFilePaths(archiveID).binPath;

                // Skip if .bin is newer than .json or if there are no changes
                if (File.Exists(binPath) && File.GetLastWriteTimeUtc(binPath) >= File.GetLastWriteTimeUtc(expandedTextFile))
                {
                    unchangedBinCount++;
                    continue;
                }

                TextConverter.JSONToBin(expandedTextFile, binPath, CharMapManager.GetCharMapPath());
                // Update .json last write time to prevent it being overwritten when reopening the ROM
                File.SetLastWriteTimeUtc(expandedTextFile, DateTime.UtcNow);
            }

            AppLogger.Info($"Text Archive: {expandedTextFiles.Length - unchangedBinCount} .bin files built from .json. " +
                $"{unchangedBinCount} .bin files skipped because they were newer than the .json or because there were no changes.");

            return true;
        }

        public List<string> GetSimpleTrainerNames()
        {
            List<string> simpleMessages = new List<string>();
            foreach (string msg in messages)
            {
                string simpleMsg = TextConverter.GetSimpleTrainerName(msg);
                simpleMessages.Add(simpleMsg);
            }
            return simpleMessages;
        }

        public bool SetSimpleTrainerName(int messageIndex, string newSimpleName)
        {
            if (messageIndex < 0)
            {
                AppLogger.Error($"Invalid message index {messageIndex} for Text Archive ID {ID:D4}");
                return false;
            }

            if (messageIndex >= messages.Count)
            {
                messages.Add("{TRAINER_NAME:" + newSimpleName + "}");
                return true;
            }

            string currentMessage = messages[messageIndex];
            string updatedMessage = TextConverter.ReplaceTrainerName(currentMessage, newSimpleName);
            if (updatedMessage == currentMessage)
            {
                // No change made
                return false;
            }
            messages[messageIndex] = updatedMessage;
            return true;
        }

        private bool TryReadJsonFile()
        {
            string jsonPath = GetFilePaths(ID).jsonPath;
            string binPath = GetFilePaths(ID).binPath;

            if (!File.Exists(jsonPath))
            {
                return false;
            }

            // If the .json file is older than the .bin file, ignore it and re-extract from .bin
            //if (File.GetLastWriteTimeUtc(jsonPath) < File.GetLastWriteTimeUtc(binPath))
            //{
            //    return false;
            //}

            try
            {
                // Explicitly use UTF-8 encoding when reading the file
                string jsonContent = File.ReadAllText(jsonPath, System.Text.Encoding.UTF8);
                
                JsonDocument doc = JsonDocument.Parse(jsonContent);
                
                JsonElement root = doc.RootElement;
                
                // Read key if present
                if (root.TryGetProperty("key", out JsonElement keyElement))
                {
                    key = (UInt16)keyElement.GetInt32();
                }
                else {
                    key = 0;
                    AppLogger.Warn($"No 'key' property found in JSON file {jsonPath}. Defaulting to 0.");
                }

                // Read messages array
                if (root.TryGetProperty("messages", out JsonElement messagesElement) && 
                    messagesElement.ValueKind == JsonValueKind.Array)
                {
                    messages = new List<string>();
                    
                    foreach (JsonElement messageElement in messagesElement.EnumerateArray())
                    {
                        string langCode = TextConverter.langCodes[RomInfo.gameLanguage];
                        JsonElement textElement;

                        // Try to get the message in the current game language
                        if (messageElement.TryGetProperty(langCode, out textElement))
                        {
                            string parsedMessage = ParseMessageValue(textElement);
                            messages.Add(parsedMessage);
                        }
                        // Fallback to en_US if current language not present
                        else if (messageElement.TryGetProperty("en_US", out textElement))
                        {
                            string parsedMessage = ParseMessageValue(textElement);
                            messages.Add(parsedMessage);
                        }
                        else
                        {
                            // If neither language is present, add an empty string
                            messages.Add("");
                        }
                    }
                    
                    doc.Dispose();
                    return true;
                }
                
                doc.Dispose();
                return false;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Error reading JSON file {jsonPath}: {ex.Message}\nStack trace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Parse a JSON message value that can be either a string or an array of strings
        /// </summary>
        private string ParseMessageValue(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                return element.GetString() ?? "";
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                // Join array elements into a single string
                List<string> lines = new List<string>();
                foreach (JsonElement line in element.EnumerateArray())
                {
                    if (line.ValueKind == JsonValueKind.String)
                    {
                        lines.Add(line.GetString() ?? "");
                    }
                }
                return string.Join("", lines);
            }
            
            return "";
        }

        private bool ReadFromBinFile()
        {
            string binPath = GetFilePaths(ID).binPath;
            string jsonPath = GetFilePaths(ID).jsonPath;
            string charmapPath = CharMapManager.GetCharMapPath();

            if (!File.Exists(binPath))
            {
                MessageBox.Show($"The .bin file for Text Archive ID {ID:D4} does not exist at the expected path: {binPath}", "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            try
            {
                if (!Directory.Exists(TextConverter.GetExpandedFolderPath()))
                {
                    Directory.CreateDirectory(TextConverter.GetExpandedFolderPath());
                }

                TextConverter.BinToJSON(binPath, jsonPath, charmapPath);
                
                // After conversion, try to read the JSON file
                return TryReadJsonFile();
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Error reading .bin file {binPath}: {ex.Message}");
                return false;
            }
        }

        public override string ToString()
        {
            return string.Join(Environment.NewLine, messages);
        }

        public void SaveToExpandedDir(int IDtoReplace, bool showSuccessMessage = true)
        {
            (string binPath, string jsonPath) =  GetFilePaths(IDtoReplace);

            if (!Directory.Exists(TextConverter.GetExpandedFolderPath()))
            {
                Directory.CreateDirectory(TextConverter.GetExpandedFolderPath());
            }

            // Create JSON structure using System.Text.Json's native types
            using (var stream = new MemoryStream())
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                writer.WriteNumber("key", key);
                writer.WriteStartArray("messages");

                int messageIndex = 0;
                foreach (string message in messages)
                {
                    writer.WriteStartObject();
                    writer.WriteString("id", $"msg_{ID:D4}_{messageIndex:D5}");
                    
                    string langCode = TextConverter.langCodes[RomInfo.gameLanguage];

                    // Check if message contains any newline control characters
                    if (message.Contains("\\n") || message.Contains("\\r") || message.Contains("\\f"))
                    {
                        // Split by newline types but preserve the delimiter in the output
                        List<string> lines = new List<string>();
                        string currentLine = "";
                        
                        for (int i = 0; i < message.Length; i++)
                        {
                            if (i < message.Length - 1 && message[i] == '\\')
                            {
                                char nextChar = message[i + 1];
                                if (nextChar == 'n' || nextChar == 'r' || nextChar == 'f')
                                {
                                    // Add the escape sequence to current line
                                    currentLine += message.Substring(i, 2);
                                    lines.Add(currentLine);
                                    currentLine = "";
                                    i++; // Skip the next character since we already processed it
                                    continue;
                                }
                            }
                            currentLine += message[i];
                        }
                        
                        // Add any remaining text
                        if (currentLine.Length > 0)
                        {
                            lines.Add(currentLine);
                        }
                        
                        // Write as array
                        writer.WriteStartArray(langCode);
                        foreach (string line in lines)
                        {
                            writer.WriteStringValue(line);
                        }
                        writer.WriteEndArray();
                    }
                    else
                    {
                        // Write as simple string
                        writer.WriteString(langCode, message);
                    }

                    writer.WriteEndObject();
                    messageIndex++;
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
                writer.Flush();

                string jsonString = System.Text.Encoding.UTF8.GetString(stream.ToArray());
                
                // Write with UTF-8 encoding WITHOUT BOM
                File.WriteAllText(jsonPath, jsonString, new System.Text.UTF8Encoding(false));
                
                AppLogger.Debug($"Saved {messages.Count} messages to {jsonPath}");
            }

            if (showSuccessMessage)
            {
                MessageBox.Show($"Text Archive ID {IDtoReplace:D4} saved to expanded directory:\n{jsonPath}", "Save Successful", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

        }

        #endregion Methods (2)
    }
}