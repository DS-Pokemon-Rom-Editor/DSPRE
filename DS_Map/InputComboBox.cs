using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace DSPRE {
    /// <summary>
    /// A ComboBox that filters its dropdown as the user types: substring match first, then a fuzzy
    /// Levenshtein match on longer queries. Still turns red on Enter/Leave if the text doesn't match.
    /// </summary>
    public partial class InputComboBox : ComboBox {
        private Color normalColor;
        private List<object> master = new List<object>();
        private bool filtering;

        public InputComboBox() {
            normalColor = this.BackColor;
            DropDownStyle = ComboBoxStyle.DropDown;
            AutoCompleteMode = AutoCompleteMode.None;
        }

        /// <summary>Snapshots the current (full) Items list as the fuzzy-search source. Call after any
        /// bulk repopulation of Items (Items.Clear() + Add/AddRange loop) so search isn't left stale.
        /// Also refreshed automatically the next time the dropdown opens.</summary>
        public void RefreshMasterList() {
            master = Items.Cast<object>().ToList();
        }

        private void UpdateText() {
            string input = (Text ?? "").Trim();
            if (master.Count == 0 && Items.Count > 0) RefreshMasterList();

            int index = -1;
            for (int i = 0; i < master.Count; i++) {
                if (string.Equals(master[i]?.ToString() ?? "", input, StringComparison.OrdinalIgnoreCase)) {
                    index = i;
                    break;
                }
            }

            // Other code reads Items.Count directly, so always restore the full list here. Skipped for
            // data-bound combos: WinForms forbids mutating Items when DataSource is set, and the binding
            // already keeps Items in sync on its own (see OnDataSourceChanged).
            if (DataSource == null) SetItems(master);

            if (index == -1) {
                this.BackColor = Color.IndianRed;
            } else {
                this.BackColor = normalColor;
                SelectedIndex = index;
            }
        }

        private void SetItems(List<object> items) {
            if (DataSource != null) return;
            filtering = true;
            try {
                BeginUpdate();
                Items.Clear();
                if (items.Count > 0) Items.AddRange(items.ToArray());
                EndUpdate();
            } finally { filtering = false; }
        }

        private void Filter(string query) {
            if (DataSource != null) return;   // native AutoComplete handles this; see OnDataSourceChanged
            if (master.Count == 0 && Items.Count > 0) RefreshMasterList();

            List<object> matches = string.IsNullOrWhiteSpace(query)
                ? master
                : master.Where(item => Matches(query, item?.ToString() ?? "")).ToList();

            SetItems(matches);
            if (matches.Count > 0 && Focused) {
                DroppedDown = true;
            }
        }

        private static bool Matches(string query, string itemText) {
            if (string.IsNullOrEmpty(itemText)) return false;

            string q = query.Trim();
            if (itemText.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (q.Length < 3) return false;

            int threshold = Math.Max(1, q.Length / 4);
            string ql = q.ToLowerInvariant();
            foreach (string token in itemText.Split(new[] { ' ', '_', '-', '.', ',', '[', ']', '(', ')', '/' }, StringSplitOptions.RemoveEmptyEntries)) {
                if (Extensions.Levenshtein(ql, token.ToLowerInvariant()) <= threshold) return true;
            }
            return false;
        }

        protected override void OnDropDown(EventArgs e) {
            base.OnDropDown(e);
            if (DataSource != null) return;   // native AutoComplete handles this; see OnDataSourceChanged
            // Always resync from Items, since a caller may have repopulated it since the last filter.
            if (!filtering) {
                RefreshMasterList();
            }
            if (string.IsNullOrEmpty(Text)) {
                SetItems(master);
            }
        }

        protected override void OnDataSourceChanged(EventArgs e) {
            base.OnDataSourceChanged(e);
            if (DataSource != null) {
                // Items-based filtering can't work on a data-bound combo (WinForms forbids mutating
                // Items when DataSource is set), so fall back to the native prefix autocomplete.
                base.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
                base.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            }
        }

        protected override void OnTextUpdate(EventArgs e) {
            base.OnTextUpdate(e);
            Filter(Text);
        }

        protected override void OnSelectedIndexChanged(EventArgs e) {
            // Items.Clear() (used by SetItems to narrow/restore the dropdown) resets SelectedIndex to
            // -1, which would otherwise spuriously fire this event mid-typing.
            if (filtering) return;
            base.OnSelectedIndexChanged(e);
        }

        protected override void OnKeyDown(KeyEventArgs e) {
            base.OnKeyDown(e);

            if (e.KeyCode == Keys.Enter) {
                UpdateText();
            }
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

        // Keeps WinForms' own AutoComplete off regardless of what Designer.cs sets, same trick as
        // DropDownStyle above. Data-bound combos are the exception: see OnDataSourceChanged.
        [Browsable(false)]
        public new AutoCompleteMode AutoCompleteMode {
            get { return base.AutoCompleteMode; }
            set { base.AutoCompleteMode = DataSource != null ? value : System.Windows.Forms.AutoCompleteMode.None; }
        }
    }
}
