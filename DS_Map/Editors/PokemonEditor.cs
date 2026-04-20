using DSPRE.Editors;
using System.Windows.Forms;

namespace DSPRE
{
    public partial class PokemonEditor : Form, IEditorWithUnsavedChanges
    {
        PersonalDataEditor personalEditor;
        LearnsetEditor learnsetEditor;
        EvolutionsEditor evoEditor;
        PokemonSpriteEditor spriteEditor;

        #region IEditorWithUnsavedChanges Implementation
        public bool HasUnsavedChanges =>
            (personalEditor?.HasUnsavedChanges ?? false) ||
            (learnsetEditor?.HasUnsavedChanges ?? false) ||
            (evoEditor?.HasUnsavedChanges ?? false) ||
            (spriteEditor?.HasUnsavedChanges ?? false);

        public string UnsavedChangesDescription {
            get {
                var descriptions = new System.Collections.Generic.List<string>();
                if (personalEditor?.HasUnsavedChanges ?? false)
                    descriptions.Add(personalEditor.UnsavedChangesDescription);
                if (learnsetEditor?.HasUnsavedChanges ?? false)
                    descriptions.Add(learnsetEditor.UnsavedChangesDescription);
                if (evoEditor?.HasUnsavedChanges ?? false)
                    descriptions.Add(evoEditor.UnsavedChangesDescription);
                if (spriteEditor?.HasUnsavedChanges ?? false)
                    descriptions.Add(spriteEditor.UnsavedChangesDescription);
                return descriptions.Count > 0 ? string.Join(", ", descriptions) : "Pokemon Editor";
            }
        }

        public void SaveChanges() {
            if (personalEditor?.HasUnsavedChanges ?? false)
                personalEditor.SaveChanges();
            if (learnsetEditor?.HasUnsavedChanges ?? false)
                learnsetEditor.SaveChanges();
            if (evoEditor?.HasUnsavedChanges ?? false)
                evoEditor.SaveChanges();
            if (spriteEditor?.HasUnsavedChanges ?? false)
                ((IEditorWithUnsavedChanges)spriteEditor).SaveChanges();
        }

        public void DiscardChanges() {
            personalEditor?.DiscardChanges();
            learnsetEditor?.DiscardChanges();
            evoEditor?.DiscardChanges();
            spriteEditor?.DiscardChanges();
        }
        #endregion

        public PokemonEditor(string[] itemNames, string[] abilityNames, string[] moveNames)
        {
            InitializeComponent();
            IsMdiContainer = true;

            // Register with OpenEditorsRegistry for ROM switching support
            OpenEditorsRegistry.Register(this);

            personalEditor = new PersonalDataEditor(itemNames, abilityNames, personalPage, this);
            personalEditor.TopLevel = false;
            personalEditor.Show();
            personalPage.Controls.Add(personalEditor);

            learnsetEditor = new LearnsetEditor(moveNames, learnsetPage, this);
            learnsetEditor.TopLevel = false;
            learnsetEditor.Show();
            learnsetPage.Controls.Add(learnsetEditor);

            evoEditor = new EvolutionsEditor(evoPage, this);
            evoEditor.TopLevel = false;
            evoEditor.Show();
            evoPage.Controls.Add(evoEditor);

            spriteEditor = new PokemonSpriteEditor(spritePage, this);
            spriteEditor.TopLevel = false;
            spriteEditor.Show();
            spritePage.Controls.Add(spriteEditor);

            toolTip1.SetToolTip(syncChangesCheckbox, "When this CheckBox is marked, mon selection will be synchronized accross all tabs below.");
        }

        public void TrySyncIndices(ComboBox sender)
        {
            if (!syncChangesCheckbox.Checked)
            {
                return;
            }

            Helpers.BackUpDisableHandler();
            Helpers.DisableHandlers();
            if (personalEditor.CheckDiscardChanges())
            {
                personalEditor.pokemonNameInputComboBox.SelectedIndex = sender.SelectedIndex;
                personalEditor.monNumberNumericUpDown.Value = sender.SelectedIndex;
                personalEditor.ChangeLoadedFile(sender.SelectedIndex);
            }
            if (learnsetEditor.CheckDiscardChanges())
            {
                learnsetEditor.pokemonNameInputComboBox.SelectedIndex = sender.SelectedIndex;
                learnsetEditor.monNumberNumericUpDown.Value = sender.SelectedIndex;
                learnsetEditor.ChangeLoadedFile(sender.SelectedIndex);
            }
            if (evoEditor.CheckDiscardChanges())
            {
                // SelectedIndex may be out of bounds
                if ((int)sender.SelectedIndex < evoEditor.pokemonNameInputComboBox.Items.Count)
                {
                    evoEditor.pokemonNameInputComboBox.SelectedIndex = sender.SelectedIndex;
                    evoEditor.monNumberNumericUpDown.Value = sender.SelectedIndex;
                    evoEditor.ChangeLoadedFile(sender.SelectedIndex);
                }

            }
            if (spriteEditor.CheckDiscardChanges())
            {
                // SelectedIndex may be out of bounds
                if (sender.SelectedIndex < spriteEditor.IndexBox.Items.Count)
                {
                    spriteEditor.IndexBox.SelectedIndex = sender.SelectedIndex;
                    spriteEditor.ChangeLoadedFile(sender.SelectedIndex);
                }
            }
            Helpers.RestoreDisableHandler();
        }

        public void TrySyncIndices(NumericUpDown sender)
        {
            if (!syncChangesCheckbox.Checked)
            {
                return;
            }

            Helpers.BackUpDisableHandler();
            Helpers.DisableHandlers();
            if (personalEditor.CheckDiscardChanges())
            {
                personalEditor.pokemonNameInputComboBox.SelectedIndex = (int)sender.Value;
                personalEditor.monNumberNumericUpDown.Value = sender.Value;
                personalEditor.ChangeLoadedFile((int)sender.Value);
            }
            if (learnsetEditor.CheckDiscardChanges())
            {
                learnsetEditor.pokemonNameInputComboBox.SelectedIndex = (int)sender.Value;
                learnsetEditor.monNumberNumericUpDown.Value = sender.Value;
                learnsetEditor.ChangeLoadedFile((int)sender.Value);
            }
            // SelectedIndex may be out of bounds
            if ((int)sender.Value < evoEditor.pokemonNameInputComboBox.Items.Count)
            {
                if (evoEditor.CheckDiscardChanges())
                {
                    evoEditor.pokemonNameInputComboBox.SelectedIndex = (int)sender.Value;
                    evoEditor.monNumberNumericUpDown.Value = sender.Value;
                    evoEditor.ChangeLoadedFile((int)sender.Value);
                }
            }
            if (spriteEditor.CheckDiscardChanges())
            {
                // SelectedIndex may be out of bounds
                if ((int)sender.Value < spriteEditor.IndexBox.Items.Count)
                {
                    spriteEditor.IndexBox.SelectedIndex = (int)sender.Value;
                    spriteEditor.ChangeLoadedFile((int)sender.Value);
                }
            }
            Helpers.RestoreDisableHandler();
        }

        public void UpdateTabPageNames()
        {
            if (personalEditor == null || learnsetEditor == null || evoEditor == null || spriteEditor == null)
            {
                return;
            }

            personalPage.Text = personalEditor.Text;
            learnsetPage.Text = learnsetEditor.Text;
            evoPage.Text = evoEditor.Text;
            spritePage.Text = spriteEditor.Text;
        }

        public bool GetSyncChangesCheckbox()
        {
            return syncChangesCheckbox.Checked;
        }

        private void PokemonEditor_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (personalEditor == null || learnsetEditor == null || evoEditor == null || spriteEditor == null)
            {
                return;
            }

            if (personalEditor.dirty || learnsetEditor.dirty || evoEditor.dirty || spriteEditor.dirty)
            {
                DialogResult result = MessageBox.Show("There are unsaved changes. Closing the editor will discard them!", "Unsaved Changes", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);

                if (result != DialogResult.OK)
                {
                    e.Cancel = true;
                    return;
                }

            }
        }
    }
}