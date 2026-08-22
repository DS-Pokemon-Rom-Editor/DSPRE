using System;
using System.Windows.Forms;

namespace DSPRE.Editors
{
    public partial class ItemTableEditor : UserControl, IEditorWithUnsavedChanges
    {
        public bool itemTableEditorIsReady { get; set; } = false;

        #region IEditorWithUnsavedChanges Implementation
        public bool HasUnsavedChanges
        {
            get
            {
                // Aggregate dirty state from child editors
                return (pickupTableEditor?.HasUnsavedChanges ?? false) ||
                       (hiddenItemsEditor?.HasUnsavedChanges ?? false) ||
                       (rockSmashEditor?.HasUnsavedChanges ?? false);
            }
        }

        public string UnsavedChangesDescription
        {
            get
            {
                var descriptions = new System.Collections.Generic.List<string>();
                if (pickupTableEditor?.HasUnsavedChanges ?? false)
                    descriptions.Add(pickupTableEditor.UnsavedChangesDescription);
                if (hiddenItemsEditor?.HasUnsavedChanges ?? false)
                    descriptions.Add(hiddenItemsEditor.UnsavedChangesDescription);
                if (rockSmashEditor?.HasUnsavedChanges ?? false)
                    descriptions.Add(rockSmashEditor.UnsavedChangesDescription);
                return descriptions.Count > 0 ? string.Join(", ", descriptions) : "Item Table Editor";
            }
        }

        public void SaveChanges()
        {
            // Save all child editors with unsaved changes
            if (pickupTableEditor?.HasUnsavedChanges ?? false)
                pickupTableEditor.SaveChanges();
            if (hiddenItemsEditor?.HasUnsavedChanges ?? false)
                hiddenItemsEditor.SaveChanges();
            if (rockSmashEditor?.HasUnsavedChanges ?? false)
                rockSmashEditor.SaveChanges();
        }

        public void DiscardChanges()
        {
            // Discard changes in all child editors
            pickupTableEditor?.DiscardChanges();
            hiddenItemsEditor?.DiscardChanges();
            rockSmashEditor?.DiscardChanges();
        }
        #endregion

        public ItemTableEditor()
        {
            InitializeComponent();
        }

        public void SetupItemTableEditor()
        {
            itemTableEditorIsReady = true;

            // Hide Pickup Table tab if not supported for this ROM version
            if (RomInfo.pickupTableOverlayNumber == -1)
            {
                // Remove the Pickup Table tab if it exists
                if (tabControl.TabPages.Contains(tabPagePickup))
                {
                    tabControl.TabPages.Remove(tabPagePickup);
                }
            }

            // Hide Hidden Items tab if not supported for this ROM version
            if (!RomInfo.IsHiddenItemsEditorAvailable())
            {
                // Remove the Hidden Items tab if it exists
                if (tabControl.TabPages.Contains(tabPageHiddenItems))
                {
                    tabControl.TabPages.Remove(tabPageHiddenItems);
                }
            }

            // Hide Rock Smash tab if not supported for this ROM version
            if (!RomInfo.IsRockSmashEditorAvailable())
            {
                if (tabControl.TabPages.Contains(tabPageRockSmash))
                {
                    tabControl.TabPages.Remove(tabPageRockSmash);
                }
            }

            // Select the first tab
            if (tabControl.TabPages.Count > 0)
            {
                tabControl.SelectedIndex = 0;
                OnTabEnter(tabControl.SelectedTab);
            }
        }

        private void OnTabEnter(TabPage tab)
        {
            if (tab == tabPagePickup)
            {
                tabPagePickup_Enter(null, null);
            }
            else if (tab == tabPageHiddenItems)
            {
                tabPageHiddenItems_Enter(null, null);
            }
            else if (tab == tabPageRockSmash)
            {
                tabPageRockSmash_Enter(null, null);
            }
        }

        private void tabPagePickup_Enter(object sender, EventArgs e)
        {
            if (pickupTableEditor != null)
            {
                pickupTableEditor.SetupPickupTableEditor();
            }
        }

        private void tabPageHiddenItems_Enter(object sender, EventArgs e)
        {
            if (hiddenItemsEditor != null)
            {
                hiddenItemsEditor.SetupHiddenItemsEditor();
            }
        }

        private void tabPageRockSmash_Enter(object sender, EventArgs e)
        {
            if (rockSmashEditor != null)
            {
                rockSmashEditor.SetupRockSmashEditor();
            }
        }

        private void tabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabControl.SelectedTab != null)
            {
                OnTabEnter(tabControl.SelectedTab);
            }
        }

        public void Reset()
        {
            Helpers.DisableHandlers();
            try
            {
                itemTableEditorIsReady = false;
                pickupTableEditor?.Reset();
                hiddenItemsEditor?.Reset();
                rockSmashEditor?.Reset();

                // Re-add pickup tab if it was removed (for ROM switching scenarios)
                if (!tabControl.TabPages.Contains(tabPagePickup))
                {
                    // Add it back at index 0
                    tabControl.TabPages.Insert(0, tabPagePickup);
                }

                // Re-add hidden items tab if it was removed (for ROM switching scenarios)
                if (!tabControl.TabPages.Contains(tabPageHiddenItems))
                {
                    // Add it back at index 1
                    tabControl.TabPages.Insert(1, tabPageHiddenItems);
                }

                // Re-add Rock Smash tab if it was removed (for ROM switching scenarios)
                if (!tabControl.TabPages.Contains(tabPageRockSmash))
                {
                    tabControl.TabPages.Insert(2, tabPageRockSmash);
                }

                // Reset to first tab
                if (tabControl.TabPages.Count > 0)
                {
                    tabControl.SelectedIndex = 0;
                }
            }
            finally
            {
                Helpers.EnableHandlers();
            }
        }
    }
}
