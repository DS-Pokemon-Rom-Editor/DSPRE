using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace DSPRE {
    public partial class InputComboBox : ComboBox {
        private Color normalColor;

        public InputComboBox() {
            normalColor = this.BackColor;
            DropDownStyle = ComboBoxStyle.DropDown;

            AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            AutoCompleteSource = AutoCompleteSource.ListItems;
        }

        // Kept as a no-op for callers from the fuzzy-search experiment; native AutoComplete needs no refresh.
        public void RefreshMasterList() { }

        private bool UpdateText() {
            string input = Text;
            int index = FindStringExact(input.Trim());
            if (index == -1) {
                this.BackColor = Color.IndianRed;
                return false;
            }
            this.BackColor = normalColor;
            SelectedIndex = index;
            return true;
        }
        protected override void OnKeyDown(KeyEventArgs e) {
            if (e.KeyCode == Keys.Enter) {
                // Eat Enter on no match so it can't fall through to a default button with a stale selection.
                if (!UpdateText()) {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    return;
                }
            }
            base.OnKeyDown(e);
        }

        protected override void OnKeyPress(KeyPressEventArgs e) {
            // Typing while the dropdown is open does native list-navigation, not text entry, so Enter
            // afterward can't see what was typed. Closing it first routes typing through AutoComplete.
            if (DroppedDown && !char.IsControl(e.KeyChar)) {
                DroppedDown = false;
            }
            base.OnKeyPress(e);
        }
        protected override void OnLeave(EventArgs e) {
            base.OnLeave(e);
            UpdateText();
        }

        [Browsable(false)]
        public new ComboBoxStyle DropDownStyle {
            get { return base.DropDownStyle; }
            set { base.DropDownStyle = ComboBoxStyle.DropDown; }
        }
    }
}
