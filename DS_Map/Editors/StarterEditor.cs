using DSPRE.ROMFiles;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace DSPRE.Editors
{
    public partial class StarterEditor : Form, IEditorWithUnsavedChanges
    {
        private bool isDirty = false;
        private bool isLoading = false;

        #region IEditorWithUnsavedChanges Implementation
        public bool HasUnsavedChanges => isDirty;
        public string UnsavedChangesDescription => "Starter Pokemon Editor";
        public void SaveChanges() => buttonSave_Click(null, null);
        public void DiscardChanges() { isDirty = false; }
        #endregion

        public StarterEditor()
        {
            InitializeComponent();
            LoadFromRom();
        }

        private void LoadFromRom()
        {
            isLoading = true;
            try
            {
                DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.monIcons, RomInfo.DirNames.itemIcons });
                RomInfo.SetMonIconsPalTableAddress();

                string[] pokemonNames = RomInfo.GetPokemonNames();
                comboBoxStarter1.Items.AddRange(pokemonNames);
                comboBoxStarter2.Items.AddRange(pokemonNames);
                comboBoxStarter3.Items.AddRange(pokemonNames);

                int[] starters = StarterPokemonData.GetStarters();
                comboBoxStarter1.SelectedIndex = starters[0] < pokemonNames.Length ? starters[0] : 0;
                comboBoxStarter2.SelectedIndex = starters[1] < pokemonNames.Length ? starters[1] : 0;
                comboBoxStarter3.SelectedIndex = starters[2] < pokemonNames.Length ? starters[2] : 0;
                UpdatePokemonIcon(pictureBoxStarter1, comboBoxStarter1.SelectedIndex);
                UpdatePokemonIcon(pictureBoxStarter2, comboBoxStarter2.SelectedIndex);
                UpdatePokemonIcon(pictureBoxStarter3, comboBoxStarter3.SelectedIndex);

                bool isHgss = RomInfo.gameFamily == RomInfo.GameFamilies.HGSS;
                bool heldItemSupported = !isHgss || RomInfo.IsHgssStarterExtrasAvailable();
                labelHeldItem.Visible = heldItemSupported;
                comboBoxHeldItem.Visible = heldItemSupported;
                pictureBoxHeldItem.Visible = heldItemSupported;
                if (heldItemSupported)
                {
                    string[] itemNames = RomInfo.GetItemNames();
                    comboBoxHeldItem.Items.AddRange(itemNames);
                    int heldItem = StarterPokemonData.GetHeldItem();
                    comboBoxHeldItem.SelectedIndex = heldItem < itemNames.Length ? heldItem : 0;
                    UpdateItemIcon(comboBoxHeldItem.SelectedIndex);
                }

                bool levelSupported = isHgss && RomInfo.IsHgssStarterExtrasAvailable();
                labelLevel.Visible = levelSupported;
                numericLevel.Visible = levelSupported;
                if (levelSupported)
                {
                    numericLevel.Value = Math.Max(numericLevel.Minimum, Math.Min(numericLevel.Maximum, StarterPokemonData.GetStarterLevel()));
                }

                isDirty = false;
            }
            finally
            {
                isLoading = false;
            }
        }

        private static void UpdatePokemonIcon(PictureBox box, int species)
        {
            box.Image = DSUtils.GetPokePic(species, box.Width, box.Height);
        }

        private void UpdateItemIcon(int itemId)
        {
            pictureBoxHeldItem.Image = itemId == 0 ? null : DSUtils.GetItemPic(itemId, pictureBoxHeldItem.Width, pictureBoxHeldItem.Height);
        }

        private void comboBoxStarter_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (sender == comboBoxStarter1) { UpdatePokemonIcon(pictureBoxStarter1, comboBoxStarter1.SelectedIndex); }
            else if (sender == comboBoxStarter2) { UpdatePokemonIcon(pictureBoxStarter2, comboBoxStarter2.SelectedIndex); }
            else if (sender == comboBoxStarter3) { UpdatePokemonIcon(pictureBoxStarter3, comboBoxStarter3.SelectedIndex); }

            if (isLoading) { return; }
            isDirty = true;
        }

        private void comboBoxHeldItem_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateItemIcon(comboBoxHeldItem.SelectedIndex);

            if (isLoading) { return; }
            isDirty = true;
        }

        private void numericLevel_ValueChanged(object sender, EventArgs e)
        {
            if (isLoading) { return; }
            isDirty = true;
        }

        private void buttonSave_Click(object sender, EventArgs e)
        {
            bool isHgss = RomInfo.gameFamily == RomInfo.GameFamilies.HGSS;
            if (comboBoxHeldItem.Visible && isHgss && comboBoxHeldItem.SelectedIndex > 255)
            {
                MessageBox.Show(
                    "The HGSS starter held item byte can only hold item IDs up to 255. Pick a lower item, " +
                    "or leave it as-is.",
                    "Starter Pokemon Editor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var newSpecies = new[] { comboBoxStarter1.SelectedIndex, comboBoxStarter2.SelectedIndex, comboBoxStarter3.SelectedIndex };
            bool ok = StarterPokemonData.ApplyStarters(newSpecies);
            if (!ok)
            {
                MessageBox.Show(
                    "Couldn't safely locate the starter species table on this ROM (it may already be modified " +
                    "by another tool). Nothing was changed.",
                    "Starter Pokemon Editor", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (comboBoxHeldItem.Visible)
            {
                StarterPokemonData.SetHeldItem(comboBoxHeldItem.SelectedIndex);
            }

            if (numericLevel.Visible)
            {
                StarterPokemonData.SetStarterLevel((int)numericLevel.Value);
            }

            isDirty = false;
            MessageBox.Show("Starters saved!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
