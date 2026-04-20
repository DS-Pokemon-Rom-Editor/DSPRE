using MKDS_Course_Editor.Export3DTools;
using System.Windows.Forms;

namespace DSPRE.Editors
{
  public partial class EncountersEditor : UserControl, IEditorWithUnsavedChanges
  {
    public bool encounterEditorIsReady { get; set; } = false;

    #region IEditorWithUnsavedChanges Implementation
    public bool HasUnsavedChanges {
      get {
        // Aggregate dirty state from child editors based on game family
        if (RomInfo.gameFamily == RomInfo.GameFamilies.DP || RomInfo.gameFamily == RomInfo.GameFamilies.Plat) {
          return honeyTreeEncounterEditor?.HasUnsavedChanges ?? false;
        } else if (RomInfo.gameFamily == RomInfo.GameFamilies.HGSS) {
          return (headbuttEncounterEditor?.HasUnsavedChanges ?? false) ||
                 (safariZoneEditor?.HasUnsavedChanges ?? false) ||
                 (bugContestEncounterEditor?.HasUnsavedChanges ?? false);
        }
        return false;
      }
    }

    public string UnsavedChangesDescription {
      get {
        var descriptions = new System.Collections.Generic.List<string>();
        if (RomInfo.gameFamily == RomInfo.GameFamilies.DP || RomInfo.gameFamily == RomInfo.GameFamilies.Plat) {
          if (honeyTreeEncounterEditor?.HasUnsavedChanges ?? false)
            descriptions.Add(honeyTreeEncounterEditor.UnsavedChangesDescription);
        } else if (RomInfo.gameFamily == RomInfo.GameFamilies.HGSS) {
          if (headbuttEncounterEditor?.HasUnsavedChanges ?? false)
            descriptions.Add(headbuttEncounterEditor.UnsavedChangesDescription);
          if (safariZoneEditor?.HasUnsavedChanges ?? false)
            descriptions.Add(safariZoneEditor.UnsavedChangesDescription);
          if (bugContestEncounterEditor?.HasUnsavedChanges ?? false)
            descriptions.Add(bugContestEncounterEditor.UnsavedChangesDescription);
        }
        return descriptions.Count > 0 ? string.Join(", ", descriptions) : "Encounters Editor";
      }
    }

    public void SaveChanges() {
      // Save all child editors with unsaved changes
      if (RomInfo.gameFamily == RomInfo.GameFamilies.DP || RomInfo.gameFamily == RomInfo.GameFamilies.Plat) {
        if (honeyTreeEncounterEditor?.HasUnsavedChanges ?? false)
          honeyTreeEncounterEditor.SaveChanges();
      } else if (RomInfo.gameFamily == RomInfo.GameFamilies.HGSS) {
        if (headbuttEncounterEditor?.HasUnsavedChanges ?? false)
          headbuttEncounterEditor.SaveChanges();
        if (safariZoneEditor?.HasUnsavedChanges ?? false)
          safariZoneEditor.SaveChanges();
        if (bugContestEncounterEditor?.HasUnsavedChanges ?? false)
          bugContestEncounterEditor.SaveChanges();
      }
    }

    public void DiscardChanges() {
      // Discard changes in all child editors
      if (RomInfo.gameFamily == RomInfo.GameFamilies.DP || RomInfo.gameFamily == RomInfo.GameFamilies.Plat) {
        honeyTreeEncounterEditor?.DiscardChanges();
      } else if (RomInfo.gameFamily == RomInfo.GameFamilies.HGSS) {
        headbuttEncounterEditor?.DiscardChanges();
        safariZoneEditor?.DiscardChanges();
        bugContestEncounterEditor?.DiscardChanges();
      }
    }
    #endregion

    public EncountersEditor()
    {
      InitializeComponent();
    }

    public void SetupEncountersEditor() {
            encounterEditorIsReady = true;

            // Configure tabs based on game family
            ConfigureTabsForGameFamily();

            // Select the first available tab
            if (tabControl.TabPages.Count > 0) {
                tabControl.SelectedIndex = 0;
                OnTabEnter(tabControl.SelectedTab);
            }
    }

    private void ConfigureTabsForGameFamily() {
        // DPPt: Only show Honey Tree tab
        // HGSS: Only show Headbutt, Safari Zone, Bug Contest tabs

        // Disable handlers to prevent Enter events from firing during tab manipulation
        Helpers.DisableHandlers();
        try {
            if (RomInfo.gameFamily == RomInfo.GameFamilies.DP || RomInfo.gameFamily == RomInfo.GameFamilies.Plat) {
                // Remove HGSS-only tabs
                if (tabControl.TabPages.Contains(tabPageHeadbuttEditor)) {
                    tabControl.TabPages.Remove(tabPageHeadbuttEditor);
                }
                if (tabControl.TabPages.Contains(tabPageSafariZoneEditor)) {
                    tabControl.TabPages.Remove(tabPageSafariZoneEditor);
                }
                if (tabControl.TabPages.Contains(tabPageBugContestEditor)) {
                    tabControl.TabPages.Remove(tabPageBugContestEditor);
                }
                // Ensure Honey Tree tab is present
                if (!tabControl.TabPages.Contains(tabPageHoneyTreeEditor)) {
                    tabControl.TabPages.Add(tabPageHoneyTreeEditor);
                }
            } else if (RomInfo.gameFamily == RomInfo.GameFamilies.HGSS) {
                // Remove DPPt-only tabs
                if (tabControl.TabPages.Contains(tabPageHoneyTreeEditor)) {
                    tabControl.TabPages.Remove(tabPageHoneyTreeEditor);
                }
                // Ensure HGSS tabs are present
                if (!tabControl.TabPages.Contains(tabPageHeadbuttEditor)) {
                    tabControl.TabPages.Insert(0, tabPageHeadbuttEditor);
                }
                if (!tabControl.TabPages.Contains(tabPageSafariZoneEditor)) {
                    tabControl.TabPages.Insert(1, tabPageSafariZoneEditor);
                }
                if (!tabControl.TabPages.Contains(tabPageBugContestEditor)) {
                    tabControl.TabPages.Insert(2, tabPageBugContestEditor);
                }
            }
        } finally {
            Helpers.EnableHandlers();
        }
    }

    private void OnTabEnter(TabPage tab) {
        if (tab == tabPageHeadbuttEditor) {
            tabPageHeadbuttEditor_Enter(null, null);
        } else if (tab == tabPageSafariZoneEditor) {
            tabPageSafariZoneEditor_Enter(null, null);
        } else if (tab == tabPageBugContestEditor) {
            tabPageBugContestEditor_Enter(null, null);
        } else if (tab == tabPageHoneyTreeEditor) {
            tabPageHoneyTreeEditor_Enter(null, null);
        }
    }

    private void tabPageHeadbuttEditor_Enter(object sender, System.EventArgs e)
    {
      if (Helpers.HandlersDisabled) return;
      headbuttEncounterEditor.SetupHeadbuttEncounterEditor();
      headbuttEncounterEditor.makeCurrent();
    }

    private void tabPageSafariZoneEditor_Enter(object sender, System.EventArgs e)
    {
      if (Helpers.HandlersDisabled) return;
      safariZoneEditor.SetupSafariZoneEditor();
    }

    private void tabPageBugContestEditor_Enter(object sender, System.EventArgs e)
    {
      if (Helpers.HandlersDisabled) return;
      bugContestEncounterEditor.SetupBugContestEncounterEditor();
    }

    private void tabPageHoneyTreeEditor_Enter(object sender, System.EventArgs e)
    {
      if (Helpers.HandlersDisabled) return;
      honeyTreeEncounterEditor.SetupHoneyTreeEncounterEditor();
    }
  }
}
