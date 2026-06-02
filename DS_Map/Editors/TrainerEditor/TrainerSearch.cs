using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace DSPRE.Editors
{
    public partial class TrainerSearch : Form
    {
        public enum TextOperators : byte
        {
            Contains,
            DoesNotContain,
            IsExactly,
            IsNot
        };

        private static readonly Dictionary<TextOperators, string> textOperatorsDict = new Dictionary<TextOperators, string>()
        {
            [TextOperators.Contains] = "Contains",
            [TextOperators.DoesNotContain] = "Does not contain",
            [TextOperators.IsExactly] = "Is Exactly",
            [TextOperators.IsNot] = "Is Not",
        };

        private ComboBox trainerComboBox;
        private string[] allTrainerNames;

        public TrainerSearch(ComboBox trainerComboBox)
        {
            InitializeComponent();

            this.trainerComboBox = trainerComboBox;

            // Store all trainer names from the combobox
            allTrainerNames = new string[trainerComboBox.Items.Count];
            for (int i = 0; i < trainerComboBox.Items.Count; i++)
            {
                allTrainerNames[i] = trainerComboBox.Items[i].ToString();
            }

            // Populate operator combobox
            foreach (string elem in textOperatorsDict.Values)
            {
                operatorComboBox.Items.Add(elem);
            }

            operatorComboBox.SelectedIndex = 0; // Default to "Contains"

            // Initialize results with all trainers
            ResetResults();
        }

        private void ResetResults()
        {
            resultsListBox.Enabled = true;
            resultsListBox.Items.Clear();
            resultsListBox.Items.AddRange(allTrainerNames);

            if (resultsListBox.Items.Count > 0)
            {
                resultsListBox.SelectedIndex = 0;
            }
        }

        private void StartSearch(bool showDialog = true)
        {
            if (string.IsNullOrWhiteSpace(searchTextBox.Text))
            {
                ResetResults();
                return;
            }

            resultsListBox.Items.Clear();

            List<string> matchingTrainers = SearchTrainers(
                allTrainerNames,
                operatorComboBox.SelectedIndex,
                searchTextBox.Text,
                caseSensitiveCheckBox.Checked
            );

            if (matchingTrainers.Count <= 0)
            {
                string searchConfig = operatorComboBox.Text.ToLower() + " \"" + searchTextBox.Text + "\"";
                resultsListBox.Items.Add("No trainer " + searchConfig);
                resultsListBox.Enabled = false;
            }
            else
            {
                resultsListBox.Items.AddRange(matchingTrainers.ToArray());
                resultsListBox.SelectedIndex = 0;
                resultsListBox.Enabled = true;
            }
        }

        private static List<string> SearchTrainers(string[] trainers, int operatorIndex, string searchValue, bool caseSensitive)
        {
            List<string> results = new List<string>();
            StringComparison comparison = caseSensitive ? StringComparison.InvariantCulture : StringComparison.InvariantCultureIgnoreCase;

            foreach (string trainer in trainers)
            {
                // Extract the trainer name part (everything after the class name)
                // Format: "[00] ClassName TrainerName"
                string searchableText = trainer;

                switch (operatorIndex)
                {
                    case (int)TextOperators.Contains:
                        if (searchableText.IndexOf(searchValue, comparison) >= 0)
                        {
                            results.Add(trainer);
                        }
                        break;
                    case (int)TextOperators.DoesNotContain:
                        if (searchableText.IndexOf(searchValue, comparison) < 0)
                        {
                            results.Add(trainer);
                        }
                        break;
                    case (int)TextOperators.IsExactly:
                        if (searchableText.Equals(searchValue, comparison))
                        {
                            results.Add(trainer);
                        }
                        break;
                    case (int)TextOperators.IsNot:
                        if (!searchableText.Equals(searchValue, comparison))
                        {
                            results.Add(trainer);
                        }
                        break;
                }
            }

            return results;
        }

        private void searchButton_Click(object sender, EventArgs e)
        {
            StartSearch(showDialog: true);
        }

        private void resetButton_Click(object sender, EventArgs e)
        {
            searchTextBox.Clear();
            ResetResults();
        }

        private void goToButton_Click(object sender, EventArgs e)
        {
            if (resultsListBox.SelectedIndex >= 0 && resultsListBox.Enabled)
            {
                // Find the trainer in the original combobox and select it
                string selectedTrainer = resultsListBox.SelectedItem.ToString();
                int index = trainerComboBox.Items.IndexOf(selectedTrainer);

                if (index >= 0)
                {
                    trainerComboBox.SelectedIndex = index;
                    this.Close();
                }
            }
        }

        private void searchTextBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (autoSearchCheckBox.Checked)
            {
                StartSearch(showDialog: false);
            }
            else if (e.KeyCode == Keys.Enter)
            {
                StartSearch(showDialog: true);
            }
        }

        private void operatorComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (autoSearchCheckBox.Checked && !string.IsNullOrWhiteSpace(searchTextBox.Text))
            {
                StartSearch(showDialog: false);
            }
        }

        private void caseSensitiveCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (autoSearchCheckBox.Checked && !string.IsNullOrWhiteSpace(searchTextBox.Text))
            {
                StartSearch(showDialog: false);
            }
        }

        private void resultsListBox_DoubleClick(object sender, EventArgs e)
        {
            // Double-click to quickly select and close
            goToButton_Click(sender, e);
        }

        private void resultsListBox_KeyDown(object sender, KeyEventArgs e)
        {
            // Press Enter to select
            if (e.KeyCode == Keys.Enter)
            {
                goToButton_Click(sender, e);
            }
        }
    }
}
