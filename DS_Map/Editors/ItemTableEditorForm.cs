using System;
using System.Windows.Forms;

namespace DSPRE.Editors
{
    /// <summary>
    /// Form wrapper for ItemTableEditor that implements IEditorWithUnsavedChanges
    /// to properly track unsaved changes for the ROM switching prompt.
    /// </summary>
    public class ItemTableEditorForm : Form, IEditorWithUnsavedChanges
    {
        private ItemTableEditor itemTableEditor;

        public ItemTableEditorForm()
        {
            Text = $"Item Table Editor - {RomInfo.GetGameDisplayName()}";

            itemTableEditor = new ItemTableEditor
            {
                Dock = DockStyle.Fill
            };

            Controls.Add(itemTableEditor);
            itemTableEditor.SetupItemTableEditor();

            // Update title when dirty state changes
            Load += (s, e) => UpdateTitle();
        }

        #region IEditorWithUnsavedChanges Implementation
        public bool HasUnsavedChanges => itemTableEditor?.HasUnsavedChanges ?? false;

        public string UnsavedChangesDescription => itemTableEditor?.UnsavedChangesDescription ?? "Item Table Editor";

        public void SaveChanges()
        {
            itemTableEditor?.SaveChanges();
            UpdateTitle();
        }

        public void DiscardChanges()
        {
            itemTableEditor?.DiscardChanges();
            UpdateTitle();
        }
        #endregion

        private void UpdateTitle()
        {
            if (this.IsDisposed || this.Disposing)
                return;

            string baseTitle = $"Item Table Editor - {RomInfo.GetGameDisplayName()}";
            Text = HasUnsavedChanges ? $"{baseTitle}*" : baseTitle;
        }

        /// <summary>
        /// Public method to allow child editors to trigger title refresh
        /// </summary>
        public void RefreshTitle()
        {
            if (!this.IsDisposed && !this.Disposing)
            {
                UpdateTitle();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (HasUnsavedChanges && !e.Cancel)
            {
                DialogResult result = MessageBox.Show(
                    "You have unsaved changes. Do you want to save before closing?",
                    "Unsaved Changes",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    SaveChanges();
                }
                else if (result == DialogResult.Cancel)
                {
                    e.Cancel = true;
                }
                // If No - just let form close, don't call DiscardChanges() as it might touch controls
            }

            base.OnFormClosing(e);
        }
    }
}
