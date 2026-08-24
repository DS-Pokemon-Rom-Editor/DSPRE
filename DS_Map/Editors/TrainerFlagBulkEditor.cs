using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Editors
{
    public partial class TrainerFlagBulkEditor : Form, IEditorWithUnsavedChanges
    {
        private enum ViewMode { ByTrainer, ByFlag }

        private class TrainerClassGroup
        {
            public byte ClassId;
            public List<int> MemberIds;
        }

        // Choose Items/Choose Moves aren't included: they control the trainerParty file's binary layout,
        // not just a flag, so editing them here without touching that file corrupts the party data.
        private static readonly string[] FlagNames =
        {
            "AI: Basic", "AI: Evaluate Attack", "AI: Expert", "AI: Setup", "AI: Risky",
            "AI: Prioritize Extremes", "AI: Baton Pass", "AI: Tag Strategy", "AI: Check HP",
            "AI: Weather", "AI: Harassment",
            "Double Battle"
        };
        private const int AI_FLAG_COUNT = TrainerProperties.AI_COUNT;
        private const int FLAG_DOUBLE_BATTLE = AI_FLAG_COUNT;

        private string[] trainerNames;
        private string[] trainerClassNames;
        private int trainerCount;
        private Dictionary<int, TrainerProperties> trainerData = new Dictionary<int, TrainerProperties>();
        private List<TrainerClassGroup> classGroups;
        private HashSet<int> selectedTrainerIds = new HashSet<int>();
        private int currentFlagIndex = 0;
        private ViewMode currentMode = ViewMode.ByTrainer;
        private bool suppressTreeEvents = false;
        private bool suppressChecklistEvents = false;
        private bool isDirty = false;
        private bool changesSaved = false;

        private ToolStrip toolStrip;
        private StatusStrip statusStrip;
        private ToolStripButton modeByTrainerButton, modeByFlagButton, selectAllButton, selectNoneButton;
        private ToolStripLabel flagPickerLabel;
        private ToolStripComboBox flagPickerCombo;
        private ToolStripButton saveButton;
        private ToolStripTextBox filterTextBoxItem;
        private TreeView trainerTree;
        private CheckedListBox flagsChecklist;
        private Label byFlagHintLabel;

        #region IEditorWithUnsavedChanges Implementation
        public bool HasUnsavedChanges => isDirty && !changesSaved;
        public string UnsavedChangesDescription => "Trainer Flag Bulk Editor";
        public void SaveChanges() => SaveAllChanges();
        public void DiscardChanges()
        {
            isDirty = false;
            changesSaved = false;
        }
        #endregion

        public TrainerFlagBulkEditor()
        {
            OpenEditorsRegistry.Register(this);

            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.trainerProperties });

            trainerNames = Helpers.GetTrainerNames();
            trainerClassNames = RomInfo.GetTrainerClassNames();
            trainerCount = Filesystem.GetTrainerPropertiesCount();

            LoadAllTrainerData();
            classGroups = BuildClassGroups();

            SetupControls();
            SetMode(ViewMode.ByTrainer);

            this.FormClosed += (s, e) => OpenEditorsRegistry.Unregister(this);
        }

        private void LoadAllTrainerData()
        {
            string dir = RomInfo.gameDirs[DirNames.trainerProperties].unpackedDir;
            for (int i = 0; i < trainerCount; i++)
            {
                using (var fs = new FileStream(Path.Combine(dir, i.ToString("D4")), FileMode.Open))
                {
                    trainerData[i] = new TrainerProperties((ushort)i, fs);
                }
            }
        }

        private List<TrainerClassGroup> BuildClassGroups()
        {
            var groups = new Dictionary<byte, List<int>>();
            for (int i = 0; i < trainerCount; i++)
            {
                byte classId = trainerData[i].trainerClass;
                if (!groups.TryGetValue(classId, out var list))
                {
                    list = new List<int>();
                    groups[classId] = list;
                }
                list.Add(i);
            }

            return groups
                .Select(kvp => new TrainerClassGroup { ClassId = kvp.Key, MemberIds = kvp.Value })
                .OrderBy(g => g.ClassId)
                .ToList();
        }

        private void SetupControls()
        {
            this.Size = new Size(1050, 700);
            UpdateWindowTitle();

            toolStrip = new ToolStrip { Dock = DockStyle.Top };

            modeByTrainerButton = new ToolStripButton("By Trainer") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            modeByTrainerButton.Click += (s, e) => SetMode(ViewMode.ByTrainer);

            modeByFlagButton = new ToolStripButton("By Flag") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            modeByFlagButton.Click += (s, e) => SetMode(ViewMode.ByFlag);

            flagPickerLabel = new ToolStripLabel("Flag:");
            flagPickerCombo = new ToolStripComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
            flagPickerCombo.Items.AddRange(FlagNames);
            flagPickerCombo.SelectedIndexChanged += (s, e) =>
            {
                if (flagPickerCombo.SelectedIndex < 0) return;
                currentFlagIndex = flagPickerCombo.SelectedIndex;
                if (currentMode == ViewMode.ByFlag)
                {
                    RebuildTrainerTree();
                    UpdateStatus();
                }
            };
            flagPickerCombo.SelectedIndex = 0;

            saveButton = new ToolStripButton("Save All") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            saveButton.Click += (s, e) => SaveAllChanges();

            selectAllButton = new ToolStripButton("Select All") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            selectAllButton.Click += (s, e) => SetAllVisibleLeavesChecked(true);

            selectNoneButton = new ToolStripButton("Select None") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            selectNoneButton.Click += (s, e) => SetAllVisibleLeavesChecked(false);

            var lblFilter = new ToolStripLabel("Filter:");
            filterTextBoxItem = new ToolStripTextBox { Width = 200 };
            filterTextBoxItem.TextChanged += (s, e) => RebuildTrainerTree();

            toolStrip.Items.AddRange(new ToolStripItem[] {
                modeByTrainerButton, modeByFlagButton, new ToolStripSeparator(),
                flagPickerLabel, flagPickerCombo, new ToolStripSeparator(),
                saveButton, new ToolStripSeparator(),
                selectAllButton, selectNoneButton, new ToolStripSeparator(),
                lblFilter, filterTextBoxItem
            });

            var splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 480
            };

            trainerTree = new TreeView
            {
                Dock = DockStyle.Fill,
                CheckBoxes = true,
                HideSelection = false
            };
            trainerTree.AfterCheck += TrainerTree_AfterCheck;
            splitContainer.Panel1.Controls.Add(trainerTree);

            flagsChecklist = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                CheckOnClick = true
            };
            flagsChecklist.Items.AddRange(FlagNames);
            flagsChecklist.ItemCheck += FlagsChecklist_ItemCheck;

            byFlagHintLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft,
                Padding = new Padding(8),
                Text = "Check or uncheck a trainer (or a whole trainer class) on the left to enable\r\n" +
                       "or disable the flag selected above for it."
            };

            splitContainer.Panel2.Controls.Add(flagsChecklist);
            splitContainer.Panel2.Controls.Add(byFlagHintLabel);

            statusStrip = new StatusStrip { Dock = DockStyle.Bottom };
            var statusLabel = new ToolStripStatusLabel();
            statusStrip.Items.Add(statusLabel);

            this.Controls.Add(splitContainer);
            this.Controls.Add(toolStrip);
            this.Controls.Add(statusStrip);
        }

        private void SetMode(ViewMode mode)
        {
            currentMode = mode;
            modeByTrainerButton.Checked = mode == ViewMode.ByTrainer;
            modeByFlagButton.Checked = mode == ViewMode.ByFlag;

            flagPickerLabel.Visible = mode == ViewMode.ByFlag;
            flagPickerCombo.Visible = mode == ViewMode.ByFlag;

            flagsChecklist.Visible = mode == ViewMode.ByTrainer;
            byFlagHintLabel.Visible = mode == ViewMode.ByFlag;

            selectAllButton.Text = mode == ViewMode.ByTrainer ? "Select All" : "Enable All";
            selectNoneButton.Text = mode == ViewMode.ByTrainer ? "Select None" : "Disable All";

            RebuildTrainerTree();
            if (mode == ViewMode.ByTrainer) RefreshFlagChecklistFromSelection();
            UpdateStatus();
        }

        private bool GetFlag(TrainerProperties tp, int flagIndex)
        {
            if (flagIndex < AI_FLAG_COUNT) return tp.AI[flagIndex];
            return tp.doubleBattle;
        }

        private void SetFlag(TrainerProperties tp, int flagIndex, bool value)
        {
            if (flagIndex < AI_FLAG_COUNT) tp.AI[flagIndex] = value;
            else tp.doubleBattle = value;
        }

        private string TrainerLabel(int id) =>
            id >= 0 && id < trainerNames.Length ? trainerNames[id] : $"[{id:D2}] ???";

        private string ClassLabel(byte classId) =>
            classId < trainerClassNames.Length ? trainerClassNames[classId] : $"Class {classId}";

        private void RebuildTrainerTree()
        {
            suppressTreeEvents = true;
            trainerTree.BeginUpdate();
            trainerTree.Nodes.Clear();

            string filter = filterTextBoxItem?.Text?.Trim();
            bool hasFilter = !string.IsNullOrEmpty(filter);

            foreach (var grp in classGroups)
            {
                var matchingIds = hasFilter
                    ? grp.MemberIds.Where(id => TrainerLabel(id).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).ToList()
                    : grp.MemberIds;

                if (matchingIds.Count == 0) continue;

                var classNode = new TreeNode { Tag = grp };
                foreach (var id in matchingIds)
                {
                    classNode.Nodes.Add(MakeLeafNode(id));
                }
                UpdateClassNodeDisplay(classNode);
                classNode.Expand();
                trainerTree.Nodes.Add(classNode);
            }

            trainerTree.EndUpdate();
            suppressTreeEvents = false;
        }

        private TreeNode MakeLeafNode(int id)
        {
            var node = new TreeNode(TrainerLabel(id)) { Tag = id };
            node.Checked = currentMode == ViewMode.ByTrainer
                ? selectedTrainerIds.Contains(id)
                : GetFlag(trainerData[id], currentFlagIndex);
            return node;
        }

        private void UpdateClassNodeDisplay(TreeNode classNode)
        {
            var grp = (TrainerClassGroup)classNode.Tag;
            int total = classNode.Nodes.Count;
            int checkedCount = classNode.Nodes.Cast<TreeNode>().Count(n => n.Checked);
            classNode.Text = $"{ClassLabel(grp.ClassId)} [{checkedCount}/{total}]";
            classNode.Checked = total > 0 && checkedCount == total;
        }

        private void TrainerTree_AfterCheck(object sender, TreeViewEventArgs e)
        {
            if (suppressTreeEvents) return;

            suppressTreeEvents = true;
            try
            {
                if (e.Node.Tag is TrainerClassGroup)
                {
                    foreach (TreeNode child in e.Node.Nodes)
                    {
                        child.Checked = e.Node.Checked;
                        ApplyLeafCheckSideEffect((int)child.Tag, child.Checked);
                    }
                    UpdateClassNodeDisplay(e.Node);
                }
                else if (e.Node.Tag is int id)
                {
                    ApplyLeafCheckSideEffect(id, e.Node.Checked);
                    if (e.Node.Parent != null) UpdateClassNodeDisplay(e.Node.Parent);
                }
            }
            finally
            {
                suppressTreeEvents = false;
            }

            if (currentMode == ViewMode.ByTrainer) RefreshFlagChecklistFromSelection();
            UpdateStatus();
        }

        private void ApplyLeafCheckSideEffect(int trainerId, bool isChecked)
        {
            if (currentMode == ViewMode.ByTrainer)
            {
                if (isChecked) selectedTrainerIds.Add(trainerId);
                else selectedTrainerIds.Remove(trainerId);
            }
            else
            {
                SetFlagForTrainer(trainerId, currentFlagIndex, isChecked);
            }
        }

        private void SetFlagForTrainer(int trainerId, int flagIndex, bool enabled)
        {
            var tp = trainerData[trainerId];
            bool changed = GetFlag(tp, flagIndex) != enabled;
            if (changed)
            {
                SetFlag(tp, flagIndex, enabled);
                SetDirty();
            }
        }

        private void SetAllVisibleLeavesChecked(bool value)
        {
            suppressTreeEvents = true;
            foreach (TreeNode top in trainerTree.Nodes)
            {
                if (top.Tag is TrainerClassGroup)
                {
                    foreach (TreeNode child in top.Nodes)
                    {
                        child.Checked = value;
                        ApplyLeafCheckSideEffect((int)child.Tag, value);
                    }
                    UpdateClassNodeDisplay(top);
                }
            }
            suppressTreeEvents = false;

            if (currentMode == ViewMode.ByTrainer) RefreshFlagChecklistFromSelection();
            UpdateStatus();
        }

        private void RefreshFlagChecklistFromSelection()
        {
            suppressChecklistEvents = true;
            try
            {
                for (int f = 0; f < flagsChecklist.Items.Count; f++)
                {
                    CheckState state;
                    if (selectedTrainerIds.Count == 0)
                    {
                        state = CheckState.Unchecked;
                    }
                    else
                    {
                        int haveCount = selectedTrainerIds.Count(id => GetFlag(trainerData[id], f));
                        state = haveCount == 0 ? CheckState.Unchecked
                            : haveCount == selectedTrainerIds.Count ? CheckState.Checked
                            : CheckState.Indeterminate;
                    }
                    flagsChecklist.SetItemCheckState(f, state);
                }
            }
            finally
            {
                suppressChecklistEvents = false;
            }
        }

        private void FlagsChecklist_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (suppressChecklistEvents) return;

            if (selectedTrainerIds.Count == 0)
            {
                e.NewValue = e.CurrentValue;
                MessageBox.Show("Select at least one trainer on the left first.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // A user click always means "toggle for the whole selection", ignoring WinForms' indeterminate cycle.
            bool enable = e.CurrentValue != CheckState.Checked;
            e.NewValue = enable ? CheckState.Checked : CheckState.Unchecked;

            foreach (var id in selectedTrainerIds)
            {
                SetFlagForTrainer(id, e.Index, enable);
            }

            // Deferred: SetItemCheckState can't safely be called back into from inside ItemCheck itself.
            BeginInvoke((MethodInvoker)(() =>
            {
                RefreshFlagChecklistFromSelection();
                UpdateStatus();
            }));
        }

        private void SaveAllChanges()
        {
            try
            {
                string dir = RomInfo.gameDirs[DirNames.trainerProperties].unpackedDir;
                foreach (var kvp in trainerData)
                {
                    File.WriteAllBytes(Path.Combine(dir, kvp.Key.ToString("D4")), kvp.Value.ToByteArray());
                }
                SetClean();
                UpdateStatus("All changes saved successfully!");
                MessageBox.Show("All trainer flag changes have been saved.", "Save Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving changes: {ex.Message}", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateStatus(string message = null)
        {
            if (statusStrip.Items.Count == 0) return;

            if (message != null)
            {
                statusStrip.Items[0].Text = message;
                return;
            }

            if (currentMode == ViewMode.ByTrainer)
            {
                statusStrip.Items[0].Text =
                    $"{trainerCount} trainers in {classGroups.Count} classes. {selectedTrainerIds.Count} selected." +
                    $"{(isDirty ? " [Unsaved Changes]" : "")}";
            }
            else
            {
                string flagLabel = currentFlagIndex >= 0 && currentFlagIndex < FlagNames.Length ? FlagNames[currentFlagIndex] : "?";
                int enabledCount = trainerData.Count(kvp => GetFlag(kvp.Value, currentFlagIndex));
                statusStrip.Items[0].Text =
                    $"{flagLabel}: {enabledCount} of {trainerCount} trainers have it enabled.{(isDirty ? " [Unsaved Changes]" : "")}";
            }
        }

        #region Dirty Tracking Methods
        private void SetDirty()
        {
            if (!isDirty)
            {
                isDirty = true;
                changesSaved = false;
                UpdateWindowTitle();
            }
        }

        private void SetClean()
        {
            isDirty = false;
            changesSaved = true;
            UpdateWindowTitle();
        }

        private void UpdateWindowTitle()
        {
            this.Text = "Trainer Flag Bulk Editor" + (isDirty ? "*" : "");
        }
        #endregion

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (isDirty && !changesSaved)
            {
                var result = MessageBox.Show(
                    "You have unsaved changes. Are you sure you want to exit?",
                    "Unsaved Changes",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning
                );

                if (result == DialogResult.No)
                {
                    e.Cancel = true;
                    return;
                }
            }
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            this.DialogResult = changesSaved ? DialogResult.OK : DialogResult.Cancel;
            base.OnFormClosed(e);
        }
    }
}
