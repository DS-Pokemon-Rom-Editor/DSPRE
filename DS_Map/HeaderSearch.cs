﻿using DSPRE.ROMFiles;
using System.Collections.Generic;

using System;
using System.IO;
using System.Windows.Forms;
using static DSPRE.RomInfo;
using System.Reflection;
using System.Linq;

namespace DSPRE {

    public partial class HeaderSearch : Form {
        // Search logic + tables live in the core HeaderSearchEngine; these forward for existing call sites.
        public static Dictionary<MapHeader.SearchableFields, string> searchableHeaderFieldsDict => HeaderSearchEngine.SearchableFields;

        private static Dictionary<HeaderSearchEngine.NumOperators, string> numOperatorsDict => HeaderSearchEngine.NumOperatorNames;
        private static Dictionary<HeaderSearchEngine.TextOperators, string> textOperatorsDict => HeaderSearchEngine.TextOperatorNames;

        private List<string> intNames;
        private ListBox headerListBox;
        private ToolStripStatusLabel statusLabel;

        public string status = "Ready";

        public HeaderSearch(ref List<string> internalNames, ListBox headerListBox, ToolStripStatusLabel statusLabel) {
            InitializeComponent();

            intNames = internalNames;
            this.headerListBox = headerListBox;
            this.statusLabel = statusLabel;

            foreach (string elem in searchableHeaderFieldsDict.Values) {
                fieldToSearch1ComboBox.Items.Add(elem);
            }

            fieldToSearch1ComboBox.SelectedIndex = 0;
            operator1ComboBox.SelectedIndex = 0;
        }

        #region Helper Methods
        private void UpdateOperators(ComboBox operatorComboBox, ComboBox fieldToSearchComboBox) {
            operatorComboBox.Items.Clear();

            if (fieldToSearchComboBox.SelectedItem.ToString().Contains("ID")) {
                foreach (string elem in numOperatorsDict.Values) {
                    operatorComboBox.Items.Add(elem);
                }
                valueTextBox.MaxLength = 5;
            } else {
                foreach (string elem in textOperatorsDict.Values) {
                    operatorComboBox.Items.Add(elem);
                }
                valueTextBox.MaxLength = 16;
            }

            operatorComboBox.SelectedIndex = 0;
        }
        #endregion
        public static void ResetResults(ListBox headerListBox, List<string> intNames, bool prependNumbers) {
            if (headerListBox.Items.Count < intNames.Count) {

                headerListBox.Enabled = true;
                headerListBox.Items.Clear();
                
                if (prependNumbers) {
                    for (int i = 0; i < intNames.Count; i++) {
                        string name = intNames[i];
                        headerListBox.Items.Add(i.ToString("D3") + MapHeader.nameSeparator + name);
                    }
                } else {
                    headerListBox.Items.AddRange(intNames.ToArray());
                }
            }
        }
        public static HashSet<string> AdvancedSearch(ushort startID, ushort finalID, List<string> intNames, int fieldToSearch, int oper, string valToSearch)
            => HeaderSearchEngine.AdvancedSearch(startID, finalID, intNames, fieldToSearch, oper, valToSearch);
        private void startSearchButton_Click(object sender, EventArgs e) {
            StartSearch(showDialog: true);
        }

        private void StartSearch(bool showDialog = true) {
            if (valueTextBox.Text == "") {
                //if (showDialog) {
                //    MessageBox.Show("Value to search is empty", "Can't search", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                //}
                headerSearchResetButton_Click(null, null);
                return;
            }

            HashSet<string> result;
            headerListBox.Items.Clear();
            
            try {
                result = AdvancedSearch(0, (ushort)intNames.Count, intNames, fieldToSearch1ComboBox.SelectedIndex, operator1ComboBox.SelectedIndex, valueTextBox.Text);
            } catch (FormatException) {
                if (showDialog) {
                    MessageBox.Show("Make sure the value to search is correct.", "Format Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                valueTextBox.Clear();
                headerListBox.Items.Add("Search parameters are invalid");
                headerListBox.Enabled = false;
                return;
            }

            string searchConfiguration = fieldToSearch1ComboBox.Text + " " + operator1ComboBox.Text.ToLower() + " " + '"' + valueTextBox.Text + '"';
            if (result is null || result.Count <= 0) {
                string res = "No header's " + searchConfiguration;
                headerListBox.Items.Add(res);
                headerListBox.Enabled = false;
                statusLabel.Text = res;
            } else {
                string[] arr = new string[result.Count];
                result.CopyTo(arr);
                headerListBox.Items.AddRange(arr);
                headerListBox.SelectedIndex = 0;
                headerListBox.Enabled = true;

                statusLabel.Text = "Showing headers whose " + searchConfiguration;
            }
            Update();
        }

        private void valueTextBox_KeyUp(object sender, KeyEventArgs e) {
            if (autoSearchCB.Checked) {
                StartSearch(showDialog: false);
            } else if (e.KeyCode == Keys.Enter) {
                StartSearch(showDialog: true);
            }    
        }
        private void headerSearchResetButton_Click(object sender, EventArgs e) {
            ResetResults(headerListBox, intNames, prependNumbers: true);
            valueTextBox.Clear();
            statusLabel.Text = "Ready";
        }
        private void fieldToSearch1ComboBox_SelectedIndexChanged(object sender, EventArgs e) {
            UpdateOperators(operator1ComboBox, fieldToSearch1ComboBox);
            if (autoSearchCB.Checked) {
                StartSearch(showDialog: false);
            }
        }
        private void operator1ComboBox_SelectedIndexChanged(object sender, EventArgs e) {
            if (autoSearchCB.Checked) {
                StartSearch(showDialog: false);
            }
        }
    }
}
