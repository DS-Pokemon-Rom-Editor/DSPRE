using DSPRE.ROMFiles;
using Ekona.Images;
using Images;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static DSPRE.ARM9;
using static DSPRE.RomInfo;
using static Images.NCOB.sNCOB;

namespace DSPRE.Editors
{
    public partial class BattleMessageEditor : Form
    {

        private static readonly string trainerTextOffsetPath = Path.Combine(RomInfo.gameDirs[RomInfo.DirNames.trainerTextOffset].unpackedDir, "0000");
        private static readonly string trainerTextTablePath = Path.Combine(RomInfo.gameDirs[RomInfo.DirNames.trainerTextTable].unpackedDir, "0000");

        public struct TrainerTextTableEntry
        {
            public int messageID { get; private set; }
            public uint trainerId { get; private set; }
            public ushort messageTriggerId { get; private set; }

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
        private List<TrainerTextTableEntry> currentTextEntries;
        private TextArchive localTrainerMessageArchive;

        private PaletteBase trainerPal;
        private ImageBase trainerTile;
        private SpriteBase trainerSprite;

        public BattleMessageEditor(int trainerID)
        {
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames>() 
            { 
                RomInfo.DirNames.textArchives,
                RomInfo.DirNames.trainerProperties,
                RomInfo.DirNames.trainerGraphics, 
                RomInfo.DirNames.trainerTextTable, 
                RomInfo.DirNames.trainerTextOffset 
            });

            InitializeComponent();

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

        private void WriteTrainerTextTable()
        {
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames>() { RomInfo.DirNames.trainerTextTable, RomInfo.DirNames.trainerTextOffset });

            try
            {
                var writer = new DSUtils.EasyWriter(trainerTextTablePath);

                Dictionary<uint, ushort> trainerIdToOffset = new Dictionary<uint, ushort>();

                // Write the trainer text table entries, sorted by trainer ID
                foreach (var kvp in trainerTextEntriesByTrainerId)
                {
                    if (!kvp.Value.Any())
                        continue;

                    // Save new offset for this trainer ID in the trainer table
                    trainerIdToOffset[kvp.Key] = (ushort)writer.BaseStream.Position;

                    foreach (var entry in kvp.Value)
                    {
                        writer.Write((ushort)entry.trainerId);
                        writer.Write((ushort)entry.messageTriggerId);                            
                    }
                }
    
                writer.Close();

                // Update the trainer text offset table with the new offsets for each trainer ID
                var offsetWriter = new DSUtils.EasyWriter(trainerTextOffsetPath);

                foreach (var kvp in trainerIdToOffset)
                {
                    // Each offset is 2 bytes, so seek to the correct position for this trainer ID
                    offsetWriter.BaseStream.Seek(kvp.Key * 2, SeekOrigin.Begin); 
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

        private void InitControls()
        {
            var trainerNames = RomInfo.GetSimpleTrainerNames();
            var trainerClassArchive = new TextArchive(RomInfo.trainerClassMessageNumber);

            int trainerCount = trainerNames.Length;
            trainerIDUpDown.Maximum = trainerCount - 1;

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
            if (trainerTextEntriesByTrainerId.TryGetValue((uint)currentTrainerID, out List<TrainerTextTableEntry> entries))
            {
                currentTextEntries = entries;
            }
            else
            {
                currentTextEntries = new List<TrainerTextTableEntry>();
            }

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
            Image trSprite = trainerSprite.Get_Image(trainerTile, trainerPal, frameNumber, trainerClassPicBox.Width / 2, 
                trainerClassPicBox.Height / 2, false, false, false, true, true, -1, OAMenabled);

            // Scale up image using nearest neighbor
            Bitmap scaledSprite = new Bitmap(trSprite.Width * 2, trSprite.Height * 2);
            using (Graphics g = Graphics.FromImage(scaledSprite))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                g.DrawImage(trSprite, new Rectangle(0, 0, scaledSprite.Width, scaledSprite.Height));
            }
            pb.Image = scaledSprite;
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

            messageScintilla.Text = text;
        }

        private void trainerIDUpDown_ValueChanged(object sender, EventArgs e)
        {
            if (Helpers.HandlersDisabled) return;

            Helpers.DisableHandlers();

            currentTrainerID = (int)trainerIDUpDown.Value;
            trainerComboBox.SelectedIndex = currentTrainerID;

            // Load and display trainer class pic
            int trainerClassID = GetTrainerClass(currentTrainerID);
            LoadTrainerClassPic(trainerClassID);
            UpdateTrainerClassPic(trainerClassPicBox);

            ReadCurrentTrainerTextEntries();

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
                messageScintilla.Enabled = false;
                return;
            }

            Helpers.DisableHandlers();

            TrainerTextTableEntry selectedEntry = currentTextEntries[selectedIndex];

            // Set trigger type combobox to this entry's trigger type
            triggerTypeComboBox.SelectedIndex = selectedEntry.messageTriggerId;

            // Update scintilla control with this entry's message text
            string messageText = localTrainerMessageArchive.messages[selectedEntry.messageID];

            UpdateScintillaText(messageText);
            messageScintilla.Enabled = true;

            Helpers.EnableHandlers();
        }

        private void manageLayoutPanel_Paint(object sender, PaintEventArgs e)
        {

        }
    }
}
