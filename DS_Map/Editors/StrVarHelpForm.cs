using System;
using System.Drawing;
using System.Windows.Forms;

namespace DSPRE.Editors
{
    public partial class StrVarHelpForm : Form
    {
        public StrVarHelpForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.richTextBox = new RichTextBox();
            this.closeButton = new Button();
            this.SuspendLayout();

            // richTextBox
            this.richTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            this.richTextBox.BackColor = SystemColors.Window;
            this.richTextBox.BorderStyle = BorderStyle.None;
            this.richTextBox.Font = new Font("Consolas", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            this.richTextBox.Location = new Point(12, 12);
            this.richTextBox.Name = "richTextBox";
            this.richTextBox.ReadOnly = true;
            this.richTextBox.Size = new Size(660, 510);
            this.richTextBox.TabIndex = 0;
            this.richTextBox.Text = "";

            // closeButton
            this.closeButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            this.closeButton.DialogResult = DialogResult.OK;
            this.closeButton.Location = new Point(597, 528);
            this.closeButton.Name = "closeButton";
            this.closeButton.Size = new Size(75, 23);
            this.closeButton.TabIndex = 1;
            this.closeButton.Text = "Close";
            this.closeButton.UseVisualStyleBackColor = true;

            // StrVarHelpForm
            this.AcceptButton = this.closeButton;
            this.AutoScaleDimensions = new SizeF(6F, 13F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(684, 561);
            this.Controls.Add(this.closeButton);
            this.Controls.Add(this.richTextBox);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimizeBox = false;
            this.MinimumSize = new Size(500, 400);
            this.Name = "StrVarHelpForm";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "String Buffer (STRVAR) Reference";
            this.Load += StrVarHelpForm_Load;
            this.ResumeLayout(false);
        }

        private RichTextBox richTextBox;
        private Button closeButton;

        private void StrVarHelpForm_Load(object sender, EventArgs e)
        {
            PopulateHelpContent();
        }

        private void PopulateHelpContent()
        {
            richTextBox.Clear();

            // Title
            AppendText("STRING BUFFER (STRVAR) REFERENCE\n", Color.DarkBlue, FontStyle.Bold, 14);
            AppendText("For DP/Plat/HGSS\n\n", Color.Gray, FontStyle.Italic, 10);

            // Syntax section
            AppendText("===============================================================\n", Color.DarkGray);
            AppendText("SYNTAX\n", Color.DarkGreen, FontStyle.Bold, 11);
            AppendText("===============================================================\n\n", Color.DarkGray);

            AppendText("{STRVAR_1, Type, BufferNumber, 0}\n\n", Color.Blue, FontStyle.Bold);
            AppendText("- ", Color.Black);
            AppendText("Type", Color.Purple, FontStyle.Bold);
            AppendText(" - Refers to the associated script command category\n", Color.Black);
            AppendText("- ", Color.Black);
            AppendText("BufferNumber", Color.Purple, FontStyle.Bold);
            AppendText(" - The number specified in the script command's buffer parameter\n", Color.Black);
            AppendText("- The last ", Color.Black);
            AppendText("0", Color.Purple, FontStyle.Bold);
            AppendText(" can be ignored\n\n", Color.Black);

            // Type reference section
            AppendText("===============================================================\n", Color.DarkGray);
            AppendText("TYPE REFERENCE\n", Color.DarkGreen, FontStyle.Bold, 11);
            AppendText("===============================================================\n\n", Color.DarkGray);

            AddTypeEntry("0", "{STRVAR_1, 0, ?, 0}", "Pokemon Species", new[] {
                "TextPokemon", "TextPokeNickname (also Type 1)", "TextPokemonStored",
                "TextStarterPokemon", "TextRivalStarter", "TextCounterpartStarter",
                "TextPartyPokemonDefault (HGSS)"
            });

            AddTypeEntry("1", "{STRVAR_1, 1, ?, 0}", "Pokemon Nickname", new[] {
                "TextPokeNickname (also Type 0)", "TextBugContestPokeNickname (HGSS)"
            });

            AddTypeEntry("3", "{STRVAR_1, 3, ?, 0}", "Player/Character Names", new[] {
                "TextPlayerName", "TextRivalName", "TextCounterpart"
            });

            AddTypeEntry("4", "{STRVAR_1, 4, ?, 0}", "Map Name", new[] { "TextMapName" });

            AddTypeEntry("6", "{STRVAR_1, 6, ?, 0}", "Move Name", new[] {
                "TextMove", "TextMachineMove", "TextPartyPokemonMove", "TextAttackItem (HGSS)"
            });

            AddTypeEntry("7", "{STRVAR_1, 7, ?, 0}", "Nature", new[] { "TextNature" });

            AddTypeEntry("8", "{STRVAR_1, 8, ?, 0}", "Item Name", new[] {
                "TextItem", "TextBerry", "TextItemLowercase", "TextItemPlural",
                "TextTrap", "TextTreasure", "TextAccessory (also Type 31)",
                "TextApricorn (HGSS)", "TextBackgroundName (HGSS)"
            });

            AddTypeEntry("10", "{STRVAR_1, 10, ?, 0}", "Seal", new[] {
                "TextSeal", "TextSealPlural", "TextSealSingular"
            });

            AddTypeEntry("14", "{STRVAR_1, 14, ?, 0}", "Trainer Class", new[] {
                "TextPlayerTrainerType", "TextTrainerClass"
            });

            AddTypeEntry("15", "{STRVAR_1, 15, ?, 0}", "Type Name", new[] {
                "CMD_765 (Platinum)", "TextTypeName (HGSS)"
            });

            AddTypeEntry("18", "{STRVAR_1, 18, ?, 0}", "Pocket Name", new[] { "TextPocket (also Type 31)" });

            AddTypeEntry("24", "{STRVAR_1, 24, ?, 0}", "Poketch App", new[] { "TextPoketch" });

            AddTypeEntry("25", "{STRVAR_1, 25, ?, 0}", "Goods/Decoration", new[] { "TextGoods" });

            AddTypeEntry("28", "{STRVAR_1, 28, ?, 0}", "Stone Name", new[] { "CMD_581", "TextStoneName" });

            AddTypeEntry("31", "{STRVAR_1, 31, ?, 0}", "Misc", new[] {
                "TextPocket (also Type 18)", "TextAccessory (also Type 8)"
            });

            AddTypeEntry("39", "{STRVAR_1, 39, ?, 0}", "Ribbon", new[] { "TextRibbon" });

            AddTypeEntry("50-54", "{STRVAR_1, 50, ?, 0}", "Number", new[] {
                "TextNumber", "TextPartyPokemonSize", "TextPokemonSizeRecord",
                "TextNumberSp", "TextBugContestRemainingTime (HGSS)"
            });

            AddTypeEntry("55", "{STRVAR_1, 55, ?, 0}", "Battle Hall", new[] { "TextBattleHallStreak (HGSS)" });

            AppendText("\n", Color.Black);
            AppendText("STRVAR_4 Types:\n", Color.DarkBlue, FontStyle.Bold);
            AddTypeEntry("1 (STRVAR_4)", "{STRVAR_4, 1, ?, 0}", "Pokeathlon", new[] { "TextPokeathlonCourseName (HGSS)" });

            // Examples section
            AppendText("\n===============================================================\n", Color.DarkGray);
            AppendText("EXAMPLES\n", Color.DarkGreen, FontStyle.Bold, 11);
            AppendText("===============================================================\n\n", Color.DarkGray);

            AppendText("Example 1: Simple String Buffer\n", Color.DarkBlue, FontStyle.Bold);
            AppendText("----------------------------------------\n", Color.LightGray);
            AppendText("Script:\n", Color.Black, FontStyle.Bold);
            AppendText("  TextPlayerName 0\n", Color.DarkCyan);
            AppendText("  Message 0\n\n", Color.DarkCyan);
            AppendText("Message:\n", Color.Black, FontStyle.Bold);
            AppendText("  Hello, {STRVAR_1, 3, 0, 0}!\n\n", Color.Blue);

            AppendText("Example 2: Multiple Buffers in One Message\n", Color.DarkBlue, FontStyle.Bold);
            AppendText("----------------------------------------\n", Color.LightGray);
            AppendText("Script:\n", Color.Black, FontStyle.Bold);
            AppendText("  TextNumber 0 493\n", Color.DarkCyan);
            AppendText("  TextNumber 1 0x8005\n", Color.DarkCyan);
            AppendText("  Message 0\n\n", Color.DarkCyan);
            AppendText("Message:\n", Color.Black, FontStyle.Bold);
            AppendText("  {STRVAR_1, 50, 0, 0} blah blah {STRVAR_1, 50, 1, 0}\n\n", Color.Blue);

            AppendText("Example 3: Different Buffer Types\n", Color.DarkBlue, FontStyle.Bold);
            AppendText("----------------------------------------\n", Color.LightGray);
            AppendText("Script:\n", Color.Black, FontStyle.Bold);
            AppendText("  TextPlayerName 0\n", Color.DarkCyan);
            AppendText("  TextPokeNickname 1\n", Color.DarkCyan);
            AppendText("  Message 0\n\n", Color.DarkCyan);
            AppendText("Message:\n", Color.Black, FontStyle.Bold);
            AppendText("  {STRVAR_1, 3, 0, 0} fed an Oran Berry\\nto {STRVAR_1, 0, 1, 0}!\n\n", Color.Blue);

            // Tips section
            AppendText("===============================================================\n", Color.DarkGray);
            AppendText("TIPS\n", Color.DarkGreen, FontStyle.Bold, 11);
            AppendText("===============================================================\n\n", Color.DarkGray);

            AppendText("- Replace ", Color.Black);
            AppendText("?", Color.Purple, FontStyle.Bold);
            AppendText(" with your buffer number (0, 1, 2, etc.)\n", Color.Black);
            AppendText("- Refer to the ", Color.Black);
            AppendText("SCRCMD Database", Color.Blue, FontStyle.Bold);
            AppendText(" for detailed command usage\n", Color.Black);
            AppendText("- Check ", Color.Black);
            AppendText("vanilla scripts", Color.Blue, FontStyle.Bold);
            AppendText(" for real-world examples\n", Color.Black);
            AppendText("- Buffer numbers ", Color.Black);
            AppendText("must match", Color.Red, FontStyle.Bold);
            AppendText(" between script command and message\n", Color.Black);

            // Scroll to top
            richTextBox.SelectionStart = 0;
            richTextBox.ScrollToCaret();
        }

        private void AddTypeEntry(string typeNum, string strvarPattern, string description, string[] commands)
        {
            AppendText($"{strvarPattern}\n", Color.Blue, FontStyle.Bold);
            AppendText($"  Type {typeNum}", Color.Purple, FontStyle.Bold);
            AppendText($" - {description}\n", Color.Black, FontStyle.Bold);
            
            foreach (var cmd in commands)
            {
                AppendText($"      - {cmd}\n", Color.DarkSlateGray);
            }
            AppendText("\n", Color.Black);
        }

        private void AppendText(string text, Color color, FontStyle style = FontStyle.Regular, float size = 10f)
        {
            int start = richTextBox.TextLength;
            richTextBox.AppendText(text);
            int end = richTextBox.TextLength;

            richTextBox.Select(start, end - start);
            richTextBox.SelectionColor = color;
            richTextBox.SelectionFont = new Font(richTextBox.Font.FontFamily, size, style);
            richTextBox.SelectionLength = 0;
        }
    }
}
