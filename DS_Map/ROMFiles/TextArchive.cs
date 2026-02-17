using DSPRE.CharMaps;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

            string jsonPath = GetFilePaths(ID).jsonPath;
            string binPath = GetFilePaths(ID).binPath;
            
            ReadMessages(jsonPath, binPath);

        }


        #endregion Constructors (1)

        #region Methods (2)

        private void ReadMessages(string jsonPath, string binPath)
        {
            // First try to read from json file if it exists
            if (TryReadJsonFile(jsonPath))
            {
                return;
            }

            // Next try to read from legacy plain text file if it exists
            if (TryReadPlainTextFile(jsonPath))
            {
                return;
            }

            // If not, extract from the .bin file
            if (!ReadFromBinFile(jsonPath, binPath))
            {
                MessageBox.Show($"Failed to read messages from .bin file {ID:D4}. Contents were replaced with empty message!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                messages = new List<string> { "" };
                return;
            }
        }

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

            if (!Directory.Exists(gameDirs[DirNames.textArchives].unpackedDir))
            {
                Directory.CreateDirectory(gameDirs[DirNames.textArchives].unpackedDir);
                AppLogger.Info($"Text Archive: Unpacked folder was unexpectedly missing. Created directory at {gameDirs[DirNames.textArchives].unpackedDir}");
            }

            if (SettingsManager.Settings.convertLegacyText)
            {
                if (!ConvertLegacyText())
                {
                    MessageBox.Show("One or more legacy text files could not be converted. " +
                        "The build will be aborted.", "Legacy Text Conversion Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
            }
            
            AppLogger.Info("Building .bin files for Text Archives from expanded directory...");
            TextConverter.FolderToBin(expandedDir, gameDirs[DirNames.textArchives].unpackedDir, CharMapManager.GetCharMapPath());

            return true;
        }

        private static bool ConvertLegacyText()
        {

            string expandedDir = TextConverter.GetExpandedFolderPath();

            // Convert any legacy .txt files to .json first
            string[] txtFiles = Directory.GetFiles(expandedDir, "*.txt", SearchOption.TopDirectoryOnly);

            if (txtFiles.Length > 0)
            {
                AppLogger.Info($"Found {txtFiles.Length} legacy text files to convert to JSON format.");

                MessageBox.Show($"Found {txtFiles.Length} legacy text files in the expanded text archive directory.\n" +
                    "These files need to be converted to the new JSON format to ensure compatibility with the current version of the tool.\n" +
                    "DSPRE will now convert the files.",
                    "Legacy Text Files Found", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                AppLogger.Info("No legacy text files found for conversion.");
                return true;
            }

            bool failed = false;

            foreach (string txtFile in txtFiles)
            {
                int id;

                string fileName = Path.GetFileNameWithoutExtension(txtFile);
                
                if (int.TryParse(fileName, out id))
                {
                    TextArchive archive = new TextArchive(id);
                    archive.SaveToExpandedDir(id, showSuccessMessage: false);
                    File.Delete(txtFile); // Delete legacy .txt file after conversion
                }
                else
                {
                    AppLogger.Error($"Could not parse Text Archive ID from legacy text file name: {fileName}. Skipping conversion for this file.");
                    failed = true;
                }
            }

            return !failed;
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

        private bool TryReadJsonFile(string jsonPath)
        {
            if (!File.Exists(jsonPath))
            {
                return false;
            }

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
        /// Attempts to read and parse a plain text file containing message data and a key in a specific format.
        /// </summary>
        /// <remarks>This method exists as legacy support for old projects to enable conversion.</remarks>
        /// <returns>true if the text file exists, is properly formatted, and its contents are successfully read and parsed;
        /// otherwise, false.</returns>
        private bool TryReadPlainTextFile(string jsonPath)
        {
            // Convert .json path to .txt path
            string txtPath = Path.ChangeExtension(jsonPath, ".txt");

            if (!File.Exists(txtPath))
            {
                return false;
            }

            try
            {
                List<string> lines = File.ReadAllLines(txtPath).ToList();
                if (lines.Count == 0)
                {
                    AppLogger.Error($"Text file {txtPath} is empty. Bin file will be reextracted.");
                    return false;
                }

                // First line should be the key
                string firstLine = lines[0];
                if (!firstLine.StartsWith("# Key: "))
                {
                    AppLogger.Error($"Text file {txtPath} is missing the key in the first line. Bin file will be reextracted.");
                    return false;
                }

                string keyHex = firstLine.Substring(7).Trim();
                if (!UInt16.TryParse(keyHex.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, null, out key))
                {
                    AppLogger.Error($"Text file {txtPath} has an invalid key format. Bin file will be reextracted.");
                    return false;
                }

                // Check for newline character in last line and add a blank line if needed
                // Since ReadAllLines() trims the newline, we read the last character of the file directly
                // I hate this - Yako
                using (FileStream fs = new FileStream(txtPath, FileMode.Open, FileAccess.Read))
                {
                    if (fs.Length > 0)
                    {
                        fs.Seek(-1, SeekOrigin.End);
                        int lastByte = fs.ReadByte();
                        if (lastByte == '\n' || lastByte == '\r')
                        {
                            lines.Add(string.Empty);
                        }
                    }
                    fs.Close();
                }

                // Remove the first line (the key) from the messages
                lines.RemoveAt(0);

                messages = lines;
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Error reading text file {txtPath}: {ex.Message}. Bin file will be reextracted.");
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

        private bool ReadFromBinFile(string jsonPath, string binPath)
        {
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
                return TryReadJsonFile(jsonPath);
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

            string langCode = TextConverter.langCodes[RomInfo.gameLanguage];
            
            // Read existing JSON if it exists to preserve other languages
            Dictionary<string, JsonElement> existingMessages = new Dictionary<string, JsonElement>();
            UInt16 existingKey = key;
            
            if (File.Exists(jsonPath))
            {
                try
                {
                    string existingJson = File.ReadAllText(jsonPath, System.Text.Encoding.UTF8);
                    JsonDocument existingDoc = JsonDocument.Parse(existingJson);
                    
                    // Preserve the existing key
                    if (existingDoc.RootElement.TryGetProperty("key", out JsonElement existingKeyElement))
                    {
                        existingKey = (UInt16)existingKeyElement.GetInt32();
                    }
                    
                    // Store existing messages to merge with current language
                    if (existingDoc.RootElement.TryGetProperty("messages", out JsonElement existingMessagesElement) &&
                        existingMessagesElement.ValueKind == JsonValueKind.Array)
                    {
                        int index = 0;
                        foreach (JsonElement messageElement in existingMessagesElement.EnumerateArray())
                        {
                            existingMessages[$"msg_{ID:D4}_{index:D5}"] = messageElement.Clone();
                            index++;
                        }
                    }
                    
                    existingDoc.Dispose();
                }
                catch (Exception ex)
                {
                    AppLogger.Warn($"Could not read existing JSON file {jsonPath} for merging: {ex.Message}. Creating new file.");
                    existingMessages.Clear();
                }
            }

            // Create JSON structure using System.Text.Json's native types with Unicode support
            using (var stream = new MemoryStream())
            {
                var options = new JsonWriterOptions 
                { 
                    Indented = true,
                    // Don't escape Unicode characters, this is primarily for readability by humans
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping 
                };
                
                using (var writer = new Utf8JsonWriter(stream, options))
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("key", existingKey);
                    writer.WriteStartArray("messages");

                    int messageIndex = 0;
                    foreach (string message in messages)
                    {
                        writer.WriteStartObject();
                        
                        string msgId = $"msg_{ID:D4}_{messageIndex:D5}";
                        writer.WriteString("id", msgId);
                        
                        // If this message exists in the file, copy all language properties except the current one
                        if (existingMessages.ContainsKey(msgId))
                        {
                            JsonElement existingMessage = existingMessages[msgId];
                            
                            // Copy all properties except "id" and the current language
                            foreach (JsonProperty prop in existingMessage.EnumerateObject())
                            {
                                if (prop.Name != "id" && prop.Name != langCode)
                                {
                                    prop.WriteTo(writer);
                                }
                            }
                        }

                        // Now write the current language
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
            }

            if (showSuccessMessage)
            {
                MessageBox.Show($"Text Archive ID {IDtoReplace:D4} saved to expanded directory:\n{jsonPath}", "Save Successful", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

        }

        #endregion Methods (2)
    }
}