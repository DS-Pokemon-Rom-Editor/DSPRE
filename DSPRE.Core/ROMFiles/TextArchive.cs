using DSPRE.CharMaps;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    public class TextArchive
    {
        #region Fields

        public int ID { get;}
        public List<string> Messages;
        private UInt16 _key = 0;

        #endregion Fields

        #region Constructors (1)

        public TextArchive(int ID, List<string> msg = null)
        {
            this.ID = ID;

            if (msg != null)
            {
                Messages = msg;
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
            if (TryReadJsonFile(jsonPath))
            {
                return;
            }

            if (File.Exists(jsonPath))
            {
                // Decoding the .bin below writes over the JSON, so a hand edit that doesn't parse is kept aside.
                string kept = jsonPath + ".unreadable";
                try
                {
                    PlainFiles.Copy(jsonPath, kept, overwrite: true);
                    AppMessages.Error($"Text archive {ID:D4}'s JSON couldn't be read, so it was read from the .bin again. "
                        + $"The JSON was kept as {Path.GetFileName(kept)}.", "Text archive");
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    AppMessages.Error($"Text archive {ID:D4}'s JSON couldn't be read or kept aside ({ex.Message}). "
                        + "It is being replaced from the .bin.", "Text archive");
                }
            }

            if (TryReadPlainTextFile(jsonPath))
            {
                return;
            }

            if (!ReadFromBinFile(jsonPath, binPath))
            {
                AppMessages.Error($"Text archive {ID:D4} couldn't be read, so its messages were left empty.", "Error");
                Messages = new List<string> { "" };
                return;
            }
        }

        public static (string binPath, string jsonPath) GetFilePaths(int ID)
        {
            string baseDir = GameDirs[DirNames.TextArchives].unpackedDir;
            string binPath = Path.Combine(baseDir, $"{ID:D4}");
            string expandedDir = TextConverter.GetExpandedFolderPath();
            string jsonPath = Path.Combine(expandedDir, $"{ID:D4}.json");
            return (binPath, jsonPath);
        }

        public static bool BuildRequiredBins() => BuildRequiredBins(out _);

        /// <param name="error">What stopped the build, naming the archive and line of any unknown control code.</param>
        public static bool BuildRequiredBins(out string error)
        {
            error = null;
            string expandedDir = TextConverter.GetExpandedFolderPath();

            if (!Directory.Exists(expandedDir))
            {
                AppLogger.Info("Text Archive: No expanded text archive directory found, skipping .bin rebuild.");
                return true;
            }

            if (!Directory.Exists(GameDirs[DirNames.TextArchives].unpackedDir))
            {
                Directory.CreateDirectory(GameDirs[DirNames.TextArchives].unpackedDir);
                AppLogger.Info($"Text Archive: Unpacked folder was unexpectedly missing. Created directory at {GameDirs[DirNames.TextArchives].unpackedDir}");
            }

            if (SettingsManager.Settings.ConvertLegacyText)
            {
                if (!ConvertLegacyText())
                {
                    AppMessages.Error("Some old text files couldn't be converted, so the build stopped.", "Text files not converted");
                    error = "One or more legacy text files couldn't be converted.";
                    return false;
                }
            }

            AppLogger.Info("Building .bin files for Text Archives from expanded directory...");
            if (!TextConverter.FolderToBin(expandedDir, GameDirs[DirNames.TextArchives].unpackedDir,
                    CharMapManager.GetCharMapPath(), out string encodeError))
            {
                error = "Text archives couldn't be encoded:\n" + encodeError;
                return false;
            }

            return true;
        }

        private static bool ConvertLegacyText()
        {

            string expandedDir = TextConverter.GetExpandedFolderPath();

            string[] txtFiles = Directory.GetFiles(expandedDir, "*.txt", SearchOption.TopDirectoryOnly);

            if (txtFiles.Length > 0)
            {
                AppLogger.Info($"Found {txtFiles.Length} legacy text files to convert to JSON format.");

                AppMessages.Info($"This project has {Plural.Of(txtFiles.Length, "text file")} in the old format. DSPRE will convert them to JSON.",
                    "Old text files found");
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
                    AppLogger.Error($"Couldn't parse Text Archive ID from legacy text file name: {fileName}. Skipping conversion for this file.");
                    failed = true;
                }
            }

            return !failed;
        }

        public List<string> GetSimpleTrainerNames()
        {
            List<string> simpleMessages = new List<string>();
            foreach (string msg in Messages)
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
                AppLogger.Error($"Text archive {ID:D4} has no message {messageIndex}.");
                return false;
            }

            if (messageIndex >= Messages.Count)
            {
                Messages.Add("{TRAINER_NAME:" + newSimpleName + "}");
                return true;
            }

            string currentMessage = Messages[messageIndex];
            string updatedMessage = TextConverter.ReplaceTrainerName(currentMessage, newSimpleName);
            if (updatedMessage == currentMessage)
            {
                return false;
            }
            Messages[messageIndex] = updatedMessage;
            return true;
        }

        /// <summary>The messages in a text archive file outside the project: its JSON, or a .bin read through a temporary JSON.</summary>
        public static List<string> ReadMessagesFrom(string path)
        {
            TextArchive read = new TextArchive(-1, new List<string>());
            string temp = null;
            string json = path;
            if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                temp = Path.Combine(Path.GetTempPath(), "dspre_text_" + Guid.NewGuid().ToString("N") + ".json");
                TextConverter.BinToJSON(path, temp, CharMapManager.GetCharMapPath());
                json = temp;
            }
            try
            {
                if (!read.TryReadJsonFile(json)) throw new InvalidDataException("That file holds no text archive.");
                return read.Messages;
            }
            finally
            {
                if (temp != null && File.Exists(temp)) File.Delete(temp);
            }
        }

        private bool TryReadJsonFile(string jsonPath)
        {
            if (!File.Exists(jsonPath))
            {
                return false;
            }

            try
            {
                string jsonContent = File.ReadAllText(jsonPath, System.Text.Encoding.UTF8);
                
                JsonDocument doc = JsonDocument.Parse(jsonContent);
                
                JsonElement root = doc.RootElement;
                
                if (root.TryGetProperty("key", out JsonElement keyElement))
                {
                    _key = (UInt16)keyElement.GetInt32();
                }
                else
                {
                    _key = 0;
                    AppLogger.Warn($"No 'key' property found in JSON file {jsonPath}. Defaulting to 0.");
                }

                if (root.TryGetProperty("messages", out JsonElement messagesElement) && 
                    messagesElement.ValueKind == JsonValueKind.Array)
                {
                    Messages = new List<string>();
                    
                    foreach (JsonElement messageElement in messagesElement.EnumerateArray())
                    {
                        string langCode = TextConverter.LangCodes[RomInfo.GameLanguage];
                        JsonElement textElement;

                        if (messageElement.TryGetProperty(langCode, out textElement))
                        {
                            string parsedMessage = ParseMessageValue(textElement);
                            Messages.Add(parsedMessage);
                        }
                        else if (messageElement.TryGetProperty("en_US", out textElement))
                        {
                            string parsedMessage = ParseMessageValue(textElement);
                            Messages.Add(parsedMessage);
                        }
                        else
                        {
                            Messages.Add("");
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
                AppLogger.Error($"Couldn't read JSON file {jsonPath}: {ex.Message}\nStack trace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>Reads an old plain text archive, for converting old projects; false when it is missing or malformed.</summary>
        private bool TryReadPlainTextFile(string jsonPath)
        {
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
                    AppLogger.Error($"Text file {txtPath} is empty. It will be read from the .bin again.");
                    return false;
                }

                string firstLine = lines[0];
                if (!firstLine.StartsWith("# Key: "))
                {
                    AppLogger.Error($"Text file {txtPath} is missing the key in the first line. It will be read from the .bin again.");
                    return false;
                }

                string keyHex = firstLine.Substring(7).Trim();
                if (!UInt16.TryParse(keyHex.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, null, out _key))
                {
                    AppLogger.Error($"Text file {txtPath} has an invalid key format. It will be read from the .bin again.");
                    return false;
                }

                // ReadAllLines drops a final newline, so check the last character and add the blank line back.
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

                lines.RemoveAt(0);

                Messages = lines;
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Couldn't read text file {txtPath}: {ex.Message}. It will be read from the .bin again.");
                return false;
            }
        }

        /// <summary>A JSON message value, a string or an array of strings.</summary>
        private string ParseMessageValue(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                return element.GetString() ?? "";
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
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
                AppMessages.Error($"Text archive {ID:D4} is missing: {binPath}", "File not found");
                return false;
            }
            try
            {
                if (!Directory.Exists(TextConverter.GetExpandedFolderPath()))
                {
                    Directory.CreateDirectory(TextConverter.GetExpandedFolderPath());
                }

                TextConverter.BinToJSON(binPath, jsonPath, charmapPath);
                
                return TryReadJsonFile(jsonPath);
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Couldn't read .bin file {binPath}: {ex.Message}");
                return false;
            }
        }

        public override string ToString()
        {
            return string.Join(Environment.NewLine, Messages);
        }

        /// <summary>
        /// An archive's expanded JSON was written. The sender is the editor that wrote it, or null, so
        /// an editor can tell its own saves from another one's. Raised on the writer's thread.
        /// </summary>
        public static event EventHandler<int> Saved;

        /// <summary>For writers that put the JSON on disk without <see cref="SaveToExpandedDir"/>.</summary>
        public static void RaiseSaved(object sender, int id)
        {
            try { Saved?.Invoke(sender, id); }
            catch (Exception ex) { AppLogger.Warn($"A text archive listener failed: {ex.Message}"); }
        }

        public void SaveToExpandedDir(int IDtoReplace, bool showSuccessMessage = true, object sender = null)
        {
            string jsonPath = GetFilePaths(IDtoReplace).jsonPath;

            if (!Directory.Exists(TextConverter.GetExpandedFolderPath()))
            {
                Directory.CreateDirectory(TextConverter.GetExpandedFolderPath());
            }

            PlainFiles.WriteBytes(jsonPath, ToExpandedJsonBytes(IDtoReplace));
            AppLogger.Debug($"Saved {Messages.Count} messages to {jsonPath}");
            RaiseSaved(sender, IDtoReplace);

            if (showSuccessMessage)
            {
                AppMessages.Notice($"Text archive {IDtoReplace:D4} saved.");
            }
        }

        private static string JoinedText(JsonElement value) => value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Array => string.Concat(value.EnumerateArray().Select(e => e.GetString())),
            _ => null,
        };

        // serde_json's escaping, which chatot uses.
        private static string JsonQuote(string text)
        {
            StringBuilder sb = new System.Text.StringBuilder(text.Length + 2).Append('"');
            foreach (char c in text)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }

        /// <summary>Serializes the expanded multilingual JSON without writing it.</summary>
        public byte[] ToExpandedJsonBytes(int IDtoReplace)
        {
            string jsonPath = GetFilePaths(IDtoReplace).jsonPath;

            string langCode = TextConverter.LangCodes[RomInfo.GameLanguage];
            
            Dictionary<string, JsonElement> existingMessages = new Dictionary<string, JsonElement>();
            UInt16 existingKey = _key;
            
            if (File.Exists(jsonPath))
            {
                try
                {
                    string existingJson = File.ReadAllText(jsonPath, System.Text.Encoding.UTF8);
                    JsonDocument existingDoc = JsonDocument.Parse(existingJson);
                    
                    if (existingDoc.RootElement.TryGetProperty("key", out JsonElement existingKeyElement))
                    {
                        existingKey = (UInt16)existingKeyElement.GetInt32();
                    }
                    
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
                    AppLogger.Warn($"Couldn't read existing JSON file {jsonPath} for merging: {ex.Message}. Creating new file.");
                    existingMessages.Clear();
                }
            }

            using (MemoryStream stream = new MemoryStream())
            {
                // Keep the file's own line ending; chatot writes LF.
                bool crlf = File.Exists(jsonPath) && File.ReadAllText(jsonPath).Contains("\r\n");
                JsonWriterOptions options = new JsonWriterOptions
                {
                    Indented = true,
                    NewLine = crlf ? "\r\n" : "\n",
                    // Unicode stays unescaped so people can read the file.
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping 
                };
                
                using (Utf8JsonWriter writer = new Utf8JsonWriter(stream, options))
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("key", existingKey);
                    writer.WriteStartArray("messages");

                    int messageIndex = 0;
                    foreach (string message in Messages)
                    {
                        writer.WriteStartObject();
                        
                        string msgId = $"msg_{ID:D4}_{messageIndex:D5}";
                        writer.WriteString("id", msgId);
                        
                        // A message already in the file keeps its other languages.
                        if (existingMessages.ContainsKey(msgId))
                        {
                            JsonElement existingMessage = existingMessages[msgId];
                            
                            // Copy all properties except "id" and the current language, exactly as the file has them
                            foreach (JsonProperty prop in existingMessage.EnumerateObject())
                            {
                                if (prop.Name != "id" && prop.Name != langCode)
                                {
                                    writer.WritePropertyName(prop.Name);
                                    writer.WriteRawValue(prop.Value.GetRawText(), skipInputValidation: true);
                                }
                            }
                        }

                        // An unchanged message keeps the exact JSON it was read from, whichever tool wrote it.
                        if (existingMessages.TryGetValue(msgId, out JsonElement had)
                            && had.TryGetProperty(langCode, out JsonElement hadText) && JoinedText(hadText) == message)
                        {
                            writer.WritePropertyName(langCode);
                            writer.WriteRawValue(hadText.GetRawText(), skipInputValidation: true);
                            writer.WriteEndObject();
                            messageIndex++;
                            continue;
                        }

                        // Otherwise as chatot writes it: split after each \n, \r and \f; one piece is a plain string,
                        // anything else (an empty message too) is an array, with only quotes, backslashes and
                        // control characters escaped.
                        List<string> lines = new List<string>();
                        string currentLine = "";
                        for (int i = 0; i < message.Length; i++)
                        {
                            if (i < message.Length - 1 && message[i] == '\\')
                            {
                                char nextChar = message[i + 1];
                                if (nextChar == 'n' || nextChar == 'r' || nextChar == 'f')
                                {
                                    currentLine += message.Substring(i, 2);
                                    lines.Add(currentLine);
                                    currentLine = "";
                                    i++;
                                    continue;
                                }
                            }
                            currentLine += message[i];
                        }
                        if (currentLine.Length > 0) lines.Add(currentLine);

                        writer.WritePropertyName(langCode);
                        // A single piece ending in a break is a string in most retail messages and a one-element
                        // array in some, so it keeps whichever shape the message already had.
                        bool wasArray = existingMessages.TryGetValue(msgId, out JsonElement before)
                            && before.TryGetProperty(langCode, out JsonElement beforeText) && beforeText.ValueKind == JsonValueKind.Array;
                        if (lines.Count == 1 && !wasArray)
                        {
                            writer.WriteRawValue(JsonQuote(message), skipInputValidation: true);
                        }
                        else if (lines.Count == 0)
                        {
                            writer.WriteRawValue("[]", skipInputValidation: true);
                        }
                        else
                        {
                            // Raw values skip the writer's indentation, so lay the array out as chatot does.
                            string nl = options.NewLine;
                            writer.WriteRawValue("[" + nl + string.Join("," + nl, lines.Select(l => "        " + JsonQuote(l))) + nl + "      ]",
                                skipInputValidation: true);
                        }

                        writer.WriteEndObject();
                        messageIndex++;
                    }

                    writer.WriteEndArray();
                    writer.WriteEndObject();
                    writer.Flush();

                    return stream.ToArray();
                }
            }
        }

        #endregion Methods (2)
    }
}
