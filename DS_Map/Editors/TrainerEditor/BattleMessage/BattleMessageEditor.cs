using DSPRE.ROMFiles;
using Ekona.Images;
using Images;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using static DSPRE.RomInfo;

namespace DSPRE.Editors
{
    public partial class BattleMessageEditor : Form
    {

        private static readonly string trainerTextOffsetPath = Path.Combine(RomInfo.gameDirs[RomInfo.DirNames.trainerTextOffset].unpackedDir, "0000");
        private static readonly string trainerTextTablePath = Path.Combine(RomInfo.gameDirs[RomInfo.DirNames.trainerTextTable].unpackedDir, "0000");

        public struct TrainerTextTableEntry
        {
            public int messageID { get; set; }
            public uint trainerId { get; set; }
            public ushort messageTriggerId { get; set; }

            public TrainerTextTableEntry(int messageID, uint trainerId, ushort messageTriggerId)
            {
                this.messageID = messageID;
                this.trainerId = trainerId;
                this.messageTriggerId = messageTriggerId;
            }
        }

        public enum TrainerMessageTrigger : ushort
        {
            PRE_BATTLE = 0,
            DEFEAT = 1,
            POST_BATTLE = 2,
            PRE_DOUBLE_BATTLE_1 = 3,
            DOUBLE_BATTLE_DEFEAT_1 = 4,
            POST_DOUBLE_BATTLE_1 = 5,
            DOUBLE_BATTLE_NOT_ENOUGH_POKEMON_1 = 6,
            PRE_DOUBLE_BATTLE_2 = 7,
            DOUBLE_BATTLE_DEFEAT_2 = 8,
            POST_DOUBLE_BATTLE_2 = 9,
            DOUBLE_BATTLE_NOT_ENOUGH_POKEMON_2 = 10,
            NOT_MORNING_UNUSED = 11,
            NOT_NIGHT_UNUSED = 12,
            FIRST_DAMAGE = 13,
            ACTIVE_BATTLER_HALF_HP = 14,
            LAST_BATTLER = 15,
            LAST_BATTLER_HALF_HP = 16,
            REMATCH = 17,
            DOUBLE_BATTLE_REMATCH_1 = 18,
            DOUBLE_BATTLE_REMATCH_2 = 19,
            WIN = 100,
        }

        public Dictionary<uint, List<TrainerTextTableEntry>> trainerTextEntriesByTrainerId = new Dictionary<uint, List<TrainerTextTableEntry>>();
        
        private int currentTrainerID;
        private int currentTrainerClass;
        private bool currentTrainerIsDouble;
        private List<TrainerTextTableEntry> currentTextEntries;
        private TextArchive localTrainerMessageArchive;
        private bool dirty = false;

        private PaletteBase trainerPal;
        private ImageBase trainerTile;
        private SpriteBase trainerSprite;

        public BattleMessageEditor()
        {
            InitializeComponent();
        }

        public BattleMessageEditor(int trainerID)
            : this()
        {
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames>() 
            { 
                RomInfo.DirNames.textArchives,
                RomInfo.DirNames.trainerProperties,
                RomInfo.DirNames.trainerGraphics, 
                RomInfo.DirNames.trainerTextTable, 
                RomInfo.DirNames.trainerTextOffset 
            });

            // Make a copy of the trainer message text archive to work with
            localTrainerMessageArchive = new TextArchive(RomInfo.trainerMessageTextNumber);
           
            SetUpTrainerTextEntries();
            InitControls();

            trainerIDUpDown.Value = trainerID;
            trainerIDUpDown_ValueChanged(null, null);
        }

        private List<TrainerTextTableEntry> ReadTrainerTextTable()
        {
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames>() { RomInfo.DirNames.trainerTextTable });

            List<TrainerTextTableEntry> trainerTextEntries = new List<TrainerTextTableEntry>();

            try
            {
                var reader = new DSUtils.EasyReader(trainerTextTablePath);

                // Read the trainer text table entries
                while (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    int offset = (int)reader.BaseStream.Position;
                    ushort trainerID = reader.ReadUInt16();
                    ushort messageTriggerID = reader.ReadUInt16();

                    trainerTextEntries.Add(new TrainerTextTableEntry(offset / 4, trainerID, messageTriggerID));
                }

                reader.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reading trainer text table: {ex.Message}");
            }

            return trainerTextEntries;
        }

        private void WriteTrainerTextTable(List<TrainerTextTableEntry> trainerTextEntries)
        {
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames>() { RomInfo.DirNames.trainerTextTable, RomInfo.DirNames.trainerTextOffset });

            try
            {
                var writer = new DSUtils.EasyWriter(trainerTextTablePath);

                Dictionary<uint, ushort> trainerIdToOffset = new Dictionary<uint, ushort>();

                // Ensure the entries are sorted by trainer ID and then by message trigger ID (this is how the game expects to read them)
                var sortedEntries = trainerTextEntries.OrderBy(entry => entry.trainerId).ThenBy(entry => entry.messageTriggerId).ToList();

                List<string> messages = new List<string>();

                foreach (var entry in sortedEntries)
                {
                    // Store the offset for this trainer ID (if not already stored)
                    if (!trainerIdToOffset.ContainsKey(entry.trainerId))
                    {
                        trainerIdToOffset[entry.trainerId] = (ushort) writer.BaseStream.Position;
                    }

                    // Write the trainer ID and message trigger ID to the trainer text table
                    writer.Write((ushort)entry.trainerId);
                    writer.Write((ushort)entry.messageTriggerId);

                    // Store the message (we need to ensure they are in the correct order)
                    messages.Add(localTrainerMessageArchive.messages[entry.messageID]);
                }

                writer.Close();

                // Write the messages to the text archive
                var tempArchive = new TextArchive(RomInfo.trainerMessageTextNumber, messages);
                tempArchive.SaveToExpandedDir(RomInfo.trainerMessageTextNumber, false);

                var offsetWriter = new DSUtils.EasyWriter(trainerTextOffsetPath);

                // Write the offsets for each trainer ID
                foreach (var kvp in trainerIdToOffset)
                {
                    offsetWriter.Seek((int) kvp.Key * 2, SeekOrigin.Begin);
                    offsetWriter.Write(kvp.Value);
                }

                offsetWriter.Close();

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error writing trainer text table: {ex.Message}");
            }
        }

        private void SetUpTrainerTextEntries()
        {
            var trainerTextEntries = ReadTrainerTextTable();
            // Group trainer text entries by trainer ID
            trainerTextEntriesByTrainerId = trainerTextEntries
                .GroupBy(entry => entry.trainerId)
                .ToDictionary(group => group.Key, group => group.ToList());
        }

        private void SetDirty(bool isDirty)
        {
            dirty = isDirty;
            this.Text = dirty ? "Trainer Message Editor*" : "Trainer Message Editor";
        }

        private void InitControls()
        {
            var trainerNames = RomInfo.GetSimpleTrainerNames();
            var trainerClassArchive = new TextArchive(RomInfo.trainerClassMessageNumber);

            int trainerCount = trainerNames.Length;
            trainerIDUpDown.Maximum = trainerCount - 1;

            // Scintilla control setup
            dsTextBox.UpdateDisplayScale();

            // Set up combobox for message trigger types
            try
            {
                triggerTypeComboBox.BeginUpdate();
                triggerTypeComboBox.Items.Clear();
                foreach (var trigger in Enum.GetValues(typeof(TrainerMessageTrigger)).Cast<TrainerMessageTrigger>())
                {
                    triggerTypeComboBox.Items.Add(trigger.ToString());
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing message trigger combobox: {ex.Message}");
            }
            finally
            {
                triggerTypeComboBox.EndUpdate();
            }

            // Set up comboboxes for trainer class + name
            try
            {
                trainerComboBox.BeginUpdate();
                trainerComboBox.Items.Clear();

                for (ushort i = 0; i < trainerCount; i++)
                {
                    int trainerClassID = GetTrainerClass(i);

                    string trainerName = trainerNames[i];
                    string trainerClass = trainerClassArchive.messages[trainerClassID];

                    string displayText = $"[{i}]: {trainerClass} {trainerName}";

                    trainerComboBox.Items.Add(displayText);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing trainer name/class comboboxes: {ex.Message}");
            }
            finally
            {
                trainerComboBox.EndUpdate();
            }
        }
        

        private void ReadCurrentTrainerTextEntries()
        {

            // Update listbox with current trainer text entries
            trainerTextListBox.BeginUpdate();
            trainerTextListBox.Items.Clear();

            foreach (var entry in currentTextEntries)
            {
                string messageTrigger = ((TrainerMessageTrigger)entry.messageTriggerId).ToString();
                string messageText = localTrainerMessageArchive.messages[entry.messageID];
                trainerTextListBox.Items.Add($"[{messageTrigger}] {messageText}");
            }

            trainerTextListBox.EndUpdate();

            CheckForMistakes();
        }

        private int GetTrainerClass(int trainerID)
        {
            string filePath = RomInfo.gameDirs[RomInfo.DirNames.trainerProperties].unpackedDir + "\\" + trainerID.ToString("D4");
            var stream = File.OpenRead(filePath);

            var trainerProperties = new TrainerProperties((ushort)trainerID, stream);
            int trainerClassID = trainerProperties.trainerClass;
            stream.Close();

            return trainerClassID;
        }

        private void UpdateCurrentTrainerInfo(int trainerID)
        {
            string filePath = RomInfo.gameDirs[RomInfo.DirNames.trainerProperties].unpackedDir + "\\" + trainerID.ToString("D4");
            var stream = File.OpenRead(filePath);

            var trainerProperties = new TrainerProperties((ushort)trainerID, stream);

            currentTrainerClass = trainerProperties.trainerClass;
            currentTrainerIsDouble = trainerProperties.doubleBattle;
        }

        public int LoadTrainerClassPic(int trClassID)
        {
            int paletteFileID = (trClassID * 5 + 1);
            string paletteFilename = paletteFileID.ToString("D4");
            trainerPal = new NCLR(gameDirs[DirNames.trainerGraphics].unpackedDir + "\\" + paletteFilename, paletteFileID, paletteFilename);

            int tilesFileID = trClassID * 5;
            string tilesFilename = tilesFileID.ToString("D4");
            trainerTile = new NCGR(gameDirs[DirNames.trainerGraphics].unpackedDir + "\\" + tilesFilename, tilesFileID, tilesFilename);

            if (gameFamily == GameFamilies.DP)
            {
                return 0;
            }

            int spriteFileID = (trClassID * 5 + 2);
            string spriteFilename = spriteFileID.ToString("D4");
            trainerSprite = new NCER(gameDirs[DirNames.trainerGraphics].unpackedDir + "\\" + spriteFilename, spriteFileID, spriteFilename);

            return trainerSprite.Banks.Length - 1;
        }

        public void UpdateTrainerClassPic(PictureBox pb, int frameNumber = 0)
        {
            if (trainerSprite == null)
            {
                AppLogger.Error("Trainer Class sprite is null!");
                return;
            }

            int bank0OAMcount = trainerSprite.Banks[0].oams.Length;
            int[] OAMenabled = new int[bank0OAMcount];
            for (int i = 0; i < OAMenabled.Length; i++)
            {
                OAMenabled[i] = i;
            }

            frameNumber = Math.Min(trainerSprite.Banks.Length, frameNumber);
            Image trSprite = trainerSprite.Get_Image(trainerTile, trainerPal, frameNumber, trainerClassPicBox.Width, 
                trainerClassPicBox.Height, false, false, false, true, true, -1, OAMenabled);

            pb.Image = trSprite;
            pb.Update();
        }

        private void UpdateScintillaText(string text)
        {
            // Insert line break on in-game line break (better readability)
            string[] breakChars = {"\\n", "\\r", "\\f" };

            foreach (string breakChar in breakChars)
            {
                text = text.Replace(breakChar, breakChar + Environment.NewLine);
            }

            dsTextBox.scintilla.Text = text;
        }

        private string GetScintillaText()
        {
            string text = dsTextBox.scintilla.Text;
            // Remove line breaks
            text = text.Replace(Environment.NewLine, "");
            return text;
        }

        private bool ConfirmUnsavedChanges()
        {
            if (!dirty) return true;

            var result = MessageBox.Show("You have unsaved changes. Do you want to save them?", "Unsaved Changes", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                // User could decide to cancel save if they want to go back and check for mistakes, so we need to check the result of the save action
                return SaveAndSort();
            }
            else if (result == DialogResult.No)
            {
                return true;
            }
            else // Cancel
            {
                return false;
            }
        }

        private bool SaveAndSort()
        {
            // Save current trainer text entries back to the main dictionary
            if (currentTextEntries != null)
            {
                trainerTextEntriesByTrainerId[(uint)currentTrainerID] = currentTextEntries;
            }

            var result = MessageBox.Show("This will sort and write all trainer text entries back to the ROM. " +
                $"Text archive {RomInfo.trainerMessageTextNumber} will be overwritten entirely and unused messages will be lost. " +
                "Are you sure you want to continue?", "Confirm Save", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (result != DialogResult.Yes) return false;

            var trainerTextEntries = trainerTextEntriesByTrainerId.SelectMany(kvp => kvp.Value).ToList();

            WriteTrainerTextTable(trainerTextEntries);
            SetDirty(false);

            // Reload everything to ensure offsets are correct and there are no issues with the new data
            trainerIDUpDown_ValueChanged(null, null);

            return true;
        }

        private void CheckForMistakes()
        {
            if (currentTextEntries == null || localTrainerMessageArchive == null)
            {
                infoLabel.Text = "";
                return;
            }

            // Check for trainer ID mismatch in this trainer's entry list (proper error)
            if (currentTextEntries.Any(entry => entry.trainerId != (uint)currentTrainerID))
            {
                infoLabel.Text = "Error: Some entries have a trainer ID that does not match the selected trainer.";
                infoLabel.ForeColor = Color.Red;
                return;
            }

            // Check if there are duplicates
            var duplicates = currentTextEntries
                .GroupBy(entry => entry.messageTriggerId)
                .Where(group => group.Count() > 1)
                .Select(group => ((TrainerMessageTrigger)group.Key).ToString());

            if (duplicates.Any())
            {
                infoLabel.Text = $"Warning: Duplicate message trigger types detected: {string.Join(", ", duplicates)}";
                infoLabel.ForeColor = Color.Orange;
                return;
            }

            // Check for empty messages
            if (currentTextEntries.Any(entry => string.IsNullOrWhiteSpace(localTrainerMessageArchive.messages[entry.messageID])))
            {
                infoLabel.Text = "Warning: One or more messages are empty.";
                infoLabel.ForeColor = Color.Orange;
                return;
            }

            // Check if the message won't work with the trainer type (e.g. double battle triggers for a single battle trainer)
            if (!currentTrainerIsDouble && currentTextEntries.Any(entry => entry.messageTriggerId >= (ushort)TrainerMessageTrigger.PRE_DOUBLE_BATTLE_1 && entry.messageTriggerId <= (ushort)TrainerMessageTrigger.DOUBLE_BATTLE_NOT_ENOUGH_POKEMON_2))
            {
                infoLabel.Text = "Warning: This trainer is not a double battle trainer, but has double battle message triggers.";
                infoLabel.ForeColor = Color.Orange;
                return;
            }

            if (currentTrainerIsDouble && currentTextEntries.Count > 0 && !currentTextEntries.Any(entry => entry.messageTriggerId >= (ushort)TrainerMessageTrigger.PRE_DOUBLE_BATTLE_1 && entry.messageTriggerId <= (ushort)TrainerMessageTrigger.DOUBLE_BATTLE_NOT_ENOUGH_POKEMON_2))
            {
                infoLabel.Text = "Warning: This trainer is a double battle trainer, but has no double battle message triggers.";
                infoLabel.ForeColor = Color.Orange;
                return;
            }

            // Check for invalid trigger IDs (not defined in enum)
            var invalidTriggerIds = currentTextEntries
                .Where(entry => !Enum.IsDefined(typeof(TrainerMessageTrigger), entry.messageTriggerId))
                .Select(entry => entry.messageTriggerId)
                .Distinct()
                .OrderBy(id => id)
                .ToList();

            if (invalidTriggerIds.Any())
            {
                infoLabel.Text = $"Warning: Unknown message trigger ID(s): {string.Join(", ", invalidTriggerIds)}";
                infoLabel.ForeColor = Color.Orange;
                return;
            }

            // Check for invalid message IDs
            var invalidMessageIds = currentTextEntries
                .Where(entry => entry.messageID < 0 || entry.messageID >= localTrainerMessageArchive.messages.Count)
                .Select(entry => entry.messageID)
                .Distinct()
                .OrderBy(id => id)
                .ToList();

            if (invalidMessageIds.Any())
            {
                infoLabel.Text = $"Warning: Invalid message ID(s): {string.Join(", ", invalidMessageIds)}";
                infoLabel.ForeColor = Color.Orange;
                return;
            }

            // Reset info label
            infoLabel.Text = "";
        }

        private void trainerIDUpDown_ValueChanged(object sender, EventArgs e)
        {
            if (Helpers.HandlersDisabled) return;

            Helpers.DisableHandlers();

            // Write back current list
            if (currentTextEntries != null)
            {
                trainerTextEntriesByTrainerId[(uint)currentTrainerID] = currentTextEntries;
            }            

            currentTrainerID = (int)trainerIDUpDown.Value;
            trainerComboBox.SelectedIndex = currentTrainerID;

            UpdateCurrentTrainerInfo(currentTrainerID);

            // Load and display trainer class pic
            LoadTrainerClassPic(currentTrainerClass);
            UpdateTrainerClassPic(trainerClassPicBox);

            if (trainerTextEntriesByTrainerId.TryGetValue((uint)currentTrainerID, out List<TrainerTextTableEntry> entries))
            {
                currentTextEntries = entries;
            }
            else
            {
                currentTextEntries = new List<TrainerTextTableEntry>();
                AppLogger.Error($"No trainer text entries found for trainer ID {currentTrainerID}. This should not have happened.");
            }

            ReadCurrentTrainerTextEntries();

            CheckForMistakes();

            Helpers.EnableHandlers();
        }

        private void trainerComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (Helpers.HandlersDisabled) return;

            trainerIDUpDown.Value = trainerComboBox.SelectedIndex;
        }

        private void trainerTextListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (Helpers.HandlersDisabled) return;

            // Get Entry from index
            int selectedIndex = trainerTextListBox.SelectedIndex;
            if (selectedIndex < 0)
            {
                dsTextBox.scintilla.Enabled = false;
                return;
            }

            Helpers.DisableHandlers();

            TrainerTextTableEntry selectedEntry = currentTextEntries[selectedIndex];

            // Set trigger type combobox to this entry's trigger type
            triggerTypeComboBox.SelectedIndex = selectedEntry.messageTriggerId;

            // Update scintilla control with this entry's message text
            string messageText = localTrainerMessageArchive.messages[selectedEntry.messageID];

            UpdateScintillaText(messageText);
            dsTextBox.scintilla.Enabled = true;

            Helpers.EnableHandlers();
        }

        private void deleteButton_Click(object sender, EventArgs e)
        {
            int selectedIndex = trainerTextListBox.SelectedIndex;

            if (selectedIndex < 0) return;

            TrainerTextTableEntry entryToRemove = currentTextEntries[selectedIndex];
            currentTextEntries.Remove(entryToRemove);

            ReadCurrentTrainerTextEntries();
            SetDirty(true);
        }

        private void editButton_Click(object sender, EventArgs e)
        {
            int selectedIndex = trainerTextListBox.SelectedIndex;
            if (selectedIndex < 0) return;

            int selectedTriggerTypeIndex = triggerTypeComboBox.SelectedIndex;
            if (selectedTriggerTypeIndex < 0) return;

            // Try to get enum from selected trigger
            Enum.TryParse(triggerTypeComboBox.SelectedItem.ToString(), out TrainerMessageTrigger selectedTrigger);

            var entryToEdit = currentTextEntries[selectedIndex];
            entryToEdit.messageTriggerId = (ushort)selectedTrigger;
            currentTextEntries[selectedIndex] = entryToEdit;

            ReadCurrentTrainerTextEntries();
            SetDirty(true);

        }

        private void addButton_Click(object sender, EventArgs e)
        {
            int selectedTriggerTypeIndex = triggerTypeComboBox.SelectedIndex;
            if (selectedTriggerTypeIndex < 0)
            {
                MessageBox.Show("Please select a message trigger type before adding a new message.");
                return;
            }

            // Try to get enum from selected trigger
            if (!Enum.TryParse(triggerTypeComboBox.SelectedItem.ToString(), out TrainerMessageTrigger selectedTrigger)) 
            {
                AppLogger.Error($"Failed to parse selected trigger type: {triggerTypeComboBox.SelectedItem.ToString()}");
                return; 
            }

            // Add at the end of the archive (this will need to be fixed by sorting later)
            int newMessageID = localTrainerMessageArchive.messages.Count;
            string text = GetScintillaText();
            localTrainerMessageArchive.messages.Add(text);

            TrainerTextTableEntry newEntry = new TrainerTextTableEntry(newMessageID, (uint)currentTrainerID, (ushort)selectedTrigger);
            currentTextEntries.Add(newEntry);

            ReadCurrentTrainerTextEntries();
            SetDirty(true);

        }

        private void saveMessageButton_Click(object sender, EventArgs e)
        {
            int selectedIndex = trainerTextListBox.SelectedIndex;
            if (selectedIndex < 0)
            {
                MessageBox.Show("Please select a message to save over.");
                return;
            }

            string text = GetScintillaText();

            int messageID = currentTextEntries[selectedIndex].messageID;

            if (messageID < 0 || messageID >= localTrainerMessageArchive.messages.Count)
            {
                MessageBox.Show("Invalid message ID. Cannot save message.");
                return;
            }

            localTrainerMessageArchive.messages[messageID] = text;

            ReadCurrentTrainerTextEntries();
            SetDirty(true);

        }

        private void saveButton_Click(object sender, EventArgs e)
        {
            SaveAndSort();
        }

        private void BattleMessageEditor_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!ConfirmUnsavedChanges())
            {
                e.Cancel = true;
            }
        }
    }
}
