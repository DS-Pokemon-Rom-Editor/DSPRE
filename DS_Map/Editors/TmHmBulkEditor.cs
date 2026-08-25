using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Editors
{
    public partial class TmHmBulkEditor : Form, IEditorWithUnsavedChanges
    {
        private enum ViewMode { ByPokemon, ByMachine }
        private enum SyncStrategy { Union, Intersection }

        private class SpeciesFamily
        {
            public List<int> MemberIds;
        }

        private string[] pokemonNames;
        private string[] machineMoveNames;
        private int speciesCount;
        private Dictionary<int, PokemonPersonalData> personalData = new Dictionary<int, PokemonPersonalData>();
        private List<SpeciesFamily> families;
        private HashSet<int> selectedSpeciesIds = new HashSet<int>();
        private int currentMachineIndex = 0;
        private ViewMode currentMode = ViewMode.ByPokemon;
        private bool suppressTreeEvents = false;
        private bool suppressChecklistEvents = false;
        private bool isDirty = false;
        private bool changesSaved = false;

        private ToolStrip toolStrip;
        private StatusStrip statusStrip;
        private ToolStripButton modeByPokemonButton, modeByMachineButton, selectAllButton, selectNoneButton;
        private ToolStripLabel machinePickerLabel;
        private ToolStripComboBox machinePickerCombo;
        private ToolStripDropDownButton syncFamilyDropdown;
        private ToolStripButton copyToButton;
        private ToolStripTextBox filterTextBoxItem;
        private TreeView speciesTree;
        private CheckedListBox machinesChecklist;
        private Label byMachineHintLabel;

        #region IEditorWithUnsavedChanges Implementation
        public bool HasUnsavedChanges => isDirty && !changesSaved;
        public string UnsavedChangesDescription => "TM/HM Bulk Editor";
        public void SaveChanges() => SaveAllChanges();
        public void DiscardChanges()
        {
            isDirty = false;
            changesSaved = false;
        }
        #endregion

        public TmHmBulkEditor(string[] pokemonNames, string[] machineMoveNames)
        {
            OpenEditorsRegistry.Register(this);
            this.pokemonNames = pokemonNames;
            this.machineMoveNames = machineMoveNames;

            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.personalPokeData, DirNames.evolutions });

            LoadAllPersonalData();
            families = BuildFamilies();

            SetupControls();
            SetMode(ViewMode.ByPokemon);

            this.FormClosed += (s, e) => OpenEditorsRegistry.Unregister(this);
        }

        private void LoadAllPersonalData()
        {
            // DP's personalPokeData NARC has fewer files than the species-name text archive, which still
            // lists Platinum-introduced forms (501-507) DP never got data files for.
            speciesCount = Math.Min(pokemonNames.Length, RomInfo.GetPersonalFilesCount());
            for (int i = 0; i < speciesCount; i++)
            {
                personalData[i] = new PokemonPersonalData(i);
            }
        }

        // Species with no evolution link of their own become singleton families.
        private List<SpeciesFamily> BuildFamilies()
        {
            int evoCount = Math.Min(RomInfo.GetEvolutionFilesList().Length, speciesCount);

            var parent = new int[speciesCount];
            for (int i = 0; i < speciesCount; i++) parent[i] = i;

            int Find(int x)
            {
                while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; }
                return x;
            }
            void Union(int a, int b)
            {
                int ra = Find(a), rb = Find(b);
                if (ra != rb) parent[ra] = rb;
            }

            for (int i = 0; i < evoCount; i++)
            {
                EvolutionFile evo;
                try { evo = new EvolutionFile(i); } catch { continue; }

                foreach (var entry in evo.data)
                {
                    if (entry.method != EvolutionMethod.None && entry.target > 0 && entry.target < speciesCount)
                    {
                        Union(i, entry.target);
                    }
                }
            }

            var groups = new Dictionary<int, List<int>>();
            for (int i = 0; i < speciesCount; i++)
            {
                int root = Find(i);
                if (!groups.TryGetValue(root, out var list))
                {
                    list = new List<int>();
                    groups[root] = list;
                }
                list.Add(i);
            }

            return groups.Values
                .Select(members => { members.Sort(); return new SpeciesFamily { MemberIds = members }; })
                .OrderBy(f => f.MemberIds[0])
                .ToList();
        }

        private void SetupControls()
        {
            this.Size = new Size(1050, 700);
            UpdateWindowTitle();

            toolStrip = new ToolStrip { Dock = DockStyle.Top };

            modeByPokemonButton = new ToolStripButton("By Pokémon") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            modeByPokemonButton.Click += (s, e) => SetMode(ViewMode.ByPokemon);

            modeByMachineButton = new ToolStripButton("By TM/HM") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            modeByMachineButton.Click += (s, e) => SetMode(ViewMode.ByMachine);

            machinePickerLabel = new ToolStripLabel("Machine:");
            machinePickerCombo = new ToolStripComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
            for (int i = 0; i < 100; i++)
            {
                string moveName = i < machineMoveNames.Length ? machineMoveNames[i] : $"UNK_{i}";
                machinePickerCombo.Items.Add($"{TMEditor.MachineLabelFromIndex(i)} - {moveName}");
            }
            machinePickerCombo.SelectedIndexChanged += (s, e) =>
            {
                if (machinePickerCombo.SelectedIndex < 0) return;
                currentMachineIndex = machinePickerCombo.SelectedIndex;
                if (currentMode == ViewMode.ByMachine)
                {
                    RebuildSpeciesTree();
                    UpdateStatus();
                }
            };
            machinePickerCombo.SelectedIndex = 0;

            var btnSave = new ToolStripButton("Save All") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            btnSave.Click += (s, e) => SaveAllChanges();

            selectAllButton = new ToolStripButton("Select All") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            selectAllButton.Click += (s, e) => SetAllVisibleLeavesChecked(true);

            selectNoneButton = new ToolStripButton("Select None") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            selectNoneButton.Click += (s, e) => SetAllVisibleLeavesChecked(false);

            syncFamilyDropdown = new ToolStripDropDownButton("Sync Family");
            var syncUnion = new ToolStripMenuItem("Union (any member has it -> all get it)");
            syncUnion.Click += (s, e) => SyncFamilies(SyncStrategy.Union);
            var syncIntersect = new ToolStripMenuItem("Intersection (only shared machines stay)");
            syncIntersect.Click += (s, e) => SyncFamilies(SyncStrategy.Intersection);
            syncFamilyDropdown.DropDownItems.AddRange(new ToolStripItem[] { syncUnion, syncIntersect });

            copyToButton = new ToolStripButton("Copy Compatibility To...") { DisplayStyle = ToolStripItemDisplayStyle.Text };
            copyToButton.Click += (s, e) => CopyMachinesToOthers();

            var lblFilter = new ToolStripLabel("Filter:");
            filterTextBoxItem = new ToolStripTextBox { Width = 160 };
            filterTextBoxItem.TextChanged += (s, e) => RebuildSpeciesTree();

            toolStrip.Items.AddRange(new ToolStripItem[] {
                modeByPokemonButton, modeByMachineButton, new ToolStripSeparator(),
                machinePickerLabel, machinePickerCombo, new ToolStripSeparator(),
                btnSave, new ToolStripSeparator(),
                selectAllButton, selectNoneButton, new ToolStripSeparator(),
                syncFamilyDropdown, copyToButton, new ToolStripSeparator(),
                lblFilter, filterTextBoxItem
            });

            var splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 340
            };

            speciesTree = new TreeView
            {
                Dock = DockStyle.Fill,
                CheckBoxes = true,
                HideSelection = false
            };
            speciesTree.AfterCheck += SpeciesTree_AfterCheck;
            splitContainer.Panel1.Controls.Add(speciesTree);

            machinesChecklist = new CheckedListBox
            {
                Dock = DockStyle.Fill,
                CheckOnClick = true
            };
            for (int i = 0; i < 100; i++)
            {
                string moveName = i < machineMoveNames.Length ? machineMoveNames[i] : $"UNK_{i}";
                machinesChecklist.Items.Add($"{TMEditor.MachineLabelFromIndex(i)} - {moveName}", CheckState.Unchecked);
            }
            machinesChecklist.ItemCheck += MachinesChecklist_ItemCheck;

            byMachineHintLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft,
                Padding = new Padding(8),
                Text = "Check or uncheck a Pokémon (or a whole family) on the left to enable or\r\n" +
                       "disable the machine selected above for it."
            };

            splitContainer.Panel2.Controls.Add(machinesChecklist);
            splitContainer.Panel2.Controls.Add(byMachineHintLabel);

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
            modeByPokemonButton.Checked = mode == ViewMode.ByPokemon;
            modeByMachineButton.Checked = mode == ViewMode.ByMachine;

            machinePickerLabel.Visible = mode == ViewMode.ByMachine;
            machinePickerCombo.Visible = mode == ViewMode.ByMachine;
            syncFamilyDropdown.Enabled = mode == ViewMode.ByPokemon;
            copyToButton.Enabled = mode == ViewMode.ByPokemon;

            machinesChecklist.Visible = mode == ViewMode.ByPokemon;
            byMachineHintLabel.Visible = mode == ViewMode.ByMachine;

            selectAllButton.Text = mode == ViewMode.ByPokemon ? "Select All" : "Enable All";
            selectNoneButton.Text = mode == ViewMode.ByPokemon ? "Select None" : "Disable All";

            RebuildSpeciesTree();
            if (mode == ViewMode.ByPokemon) RefreshMachineChecklistFromSelection();
            UpdateStatus();
        }

        private string SpeciesLabel(int id) =>
            id >= 0 && id < pokemonNames.Length ? $"{id:0000} - {pokemonNames[id]}" : $"{id:0000} - ???";

        private void RebuildSpeciesTree()
        {
            suppressTreeEvents = true;
            speciesTree.BeginUpdate();
            speciesTree.Nodes.Clear();

            string filter = filterTextBoxItem?.Text?.Trim();
            bool hasFilter = !string.IsNullOrEmpty(filter);

            foreach (var fam in families)
            {
                var matchingIds = hasFilter
                    ? fam.MemberIds.Where(id => SpeciesLabel(id).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).ToList()
                    : fam.MemberIds;

                if (matchingIds.Count == 0) continue;

                if (fam.MemberIds.Count == 1)
                {
                    speciesTree.Nodes.Add(MakeLeafNode(fam.MemberIds[0]));
                }
                else
                {
                    var famNode = new TreeNode { Tag = fam };
                    foreach (var id in matchingIds)
                    {
                        famNode.Nodes.Add(MakeLeafNode(id));
                    }
                    UpdateFamilyNodeDisplay(famNode);
                    famNode.Expand();
                    speciesTree.Nodes.Add(famNode);
                }
            }

            speciesTree.EndUpdate();
            suppressTreeEvents = false;
        }

        private TreeNode MakeLeafNode(int id)
        {
            var node = new TreeNode(SpeciesLabel(id)) { Tag = id };
            node.Checked = currentMode == ViewMode.ByPokemon
                ? selectedSpeciesIds.Contains(id)
                : personalData[id].machines.Contains((byte)currentMachineIndex);
            return node;
        }

        private void UpdateFamilyNodeDisplay(TreeNode famNode)
        {
            var fam = (SpeciesFamily)famNode.Tag;
            int total = famNode.Nodes.Count;
            int checkedCount = famNode.Nodes.Cast<TreeNode>().Count(n => n.Checked);
            famNode.Text = $"{SpeciesLabel(fam.MemberIds[0])} family [{checkedCount}/{total}]";
            famNode.Checked = total > 0 && checkedCount == total;
        }

        private void SpeciesTree_AfterCheck(object sender, TreeViewEventArgs e)
        {
            if (suppressTreeEvents) return;

            suppressTreeEvents = true;
            try
            {
                if (e.Node.Tag is SpeciesFamily)
                {
                    foreach (TreeNode child in e.Node.Nodes)
                    {
                        child.Checked = e.Node.Checked;
                        ApplyLeafCheckSideEffect((int)child.Tag, child.Checked);
                    }
                    UpdateFamilyNodeDisplay(e.Node);
                }
                else if (e.Node.Tag is int id)
                {
                    ApplyLeafCheckSideEffect(id, e.Node.Checked);
                    if (e.Node.Parent != null) UpdateFamilyNodeDisplay(e.Node.Parent);
                }
            }
            finally
            {
                suppressTreeEvents = false;
            }

            if (currentMode == ViewMode.ByPokemon) RefreshMachineChecklistFromSelection();
            UpdateStatus();
        }

        private void ApplyLeafCheckSideEffect(int speciesId, bool isChecked)
        {
            if (currentMode == ViewMode.ByPokemon)
            {
                if (isChecked) selectedSpeciesIds.Add(speciesId);
                else selectedSpeciesIds.Remove(speciesId);
            }
            else
            {
                SetMachineCompat(speciesId, isChecked);
            }
        }

        private void SetMachineCompat(int speciesId, bool enabled)
        {
            var data = personalData[speciesId];
            bool changed = enabled ? data.machines.Add((byte)currentMachineIndex) : data.machines.Remove((byte)currentMachineIndex);
            if (changed) SetDirty();
        }

        private void SetAllVisibleLeavesChecked(bool value)
        {
            suppressTreeEvents = true;
            foreach (TreeNode top in speciesTree.Nodes)
            {
                if (top.Tag is SpeciesFamily)
                {
                    foreach (TreeNode child in top.Nodes)
                    {
                        child.Checked = value;
                        ApplyLeafCheckSideEffect((int)child.Tag, value);
                    }
                    UpdateFamilyNodeDisplay(top);
                }
                else if (top.Tag is int id)
                {
                    top.Checked = value;
                    ApplyLeafCheckSideEffect(id, value);
                }
            }
            suppressTreeEvents = false;

            if (currentMode == ViewMode.ByPokemon) RefreshMachineChecklistFromSelection();
            UpdateStatus();
        }

        private void RefreshMachineChecklistFromSelection()
        {
            suppressChecklistEvents = true;
            try
            {
                for (int m = 0; m < machinesChecklist.Items.Count; m++)
                {
                    CheckState state;
                    if (selectedSpeciesIds.Count == 0)
                    {
                        state = CheckState.Unchecked;
                    }
                    else
                    {
                        int haveCount = selectedSpeciesIds.Count(id => personalData[id].machines.Contains((byte)m));
                        state = haveCount == 0 ? CheckState.Unchecked
                            : haveCount == selectedSpeciesIds.Count ? CheckState.Checked
                            : CheckState.Indeterminate;
                    }
                    machinesChecklist.SetItemCheckState(m, state);
                }
            }
            finally
            {
                suppressChecklistEvents = false;
            }
        }

        private void MachinesChecklist_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (suppressChecklistEvents) return;

            if (selectedSpeciesIds.Count == 0)
            {
                e.NewValue = e.CurrentValue;
                MessageBox.Show("Select at least one Pokémon on the left first.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Ignore WinForms' proposed three-state cycle: a user click always means "toggle for the whole selection".
            bool enable = e.CurrentValue != CheckState.Checked;
            e.NewValue = enable ? CheckState.Checked : CheckState.Unchecked;

            foreach (var id in selectedSpeciesIds)
            {
                if (enable) personalData[id].machines.Add((byte)e.Index);
                else personalData[id].machines.Remove((byte)e.Index);
            }
            SetDirty();

            // Deferred: SetItemCheckState can't safely be called back into from inside ItemCheck itself.
            BeginInvoke((MethodInvoker)(() =>
            {
                RefreshMachineChecklistFromSelection();
                UpdateStatus();
            }));
        }

        private void SyncFamilies(SyncStrategy strategy)
        {
            var touched = families.Where(f => f.MemberIds.Count > 1 && f.MemberIds.Any(selectedSpeciesIds.Contains)).ToList();
            if (touched.Count == 0)
            {
                MessageBox.Show("Select at least one Pokémon from a multi-member evolution family (in By Pokémon view) first.", "Sync Family", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            foreach (var fam in touched)
            {
                if (strategy == SyncStrategy.Union)
                {
                    var union = new SortedSet<byte>();
                    foreach (var id in fam.MemberIds) union.UnionWith(personalData[id].machines);
                    foreach (var id in fam.MemberIds) personalData[id].machines = new SortedSet<byte>(union);
                }
                else
                {
                    var intersection = new SortedSet<byte>(personalData[fam.MemberIds[0]].machines);
                    foreach (var id in fam.MemberIds.Skip(1)) intersection.IntersectWith(personalData[id].machines);
                    foreach (var id in fam.MemberIds) personalData[id].machines = new SortedSet<byte>(intersection);
                }
            }
            AfterBulkFamilyChange($"Synced {touched.Count} famil{(touched.Count == 1 ? "y" : "ies")} ({strategy}).");
        }

        private void AfterBulkFamilyChange(string message)
        {
            SetDirty();
            if (currentMode == ViewMode.ByPokemon) RefreshMachineChecklistFromSelection();
            UpdateStatus(message);
        }

        // Unlike Sync Family, this isn't limited to the source's own evolution family.
        private void CopyMachinesToOthers()
        {
            int preselectedSource = selectedSpeciesIds.Count == 1 ? selectedSpeciesIds.First() : -1;
            var familyGroups = families.Select(f => f.MemberIds).ToList();

            using (var form = new CopyMachinesForm(pokemonNames, familyGroups, preselectedSource))
            {
                if (form.ShowDialog() == DialogResult.OK && form.SelectedTargetIds.Any())
                {
                    int sourceId = form.SelectedSourceId;
                    var sourceSet = new SortedSet<byte>(personalData[sourceId].machines);
                    var targetIds = form.SelectedTargetIds.Where(id => id != sourceId).ToList();

                    foreach (var id in targetIds)
                    {
                        personalData[id].machines = new SortedSet<byte>(sourceSet);
                    }

                    AfterBulkFamilyChange($"Copied TM/HM compatibility from {SpeciesLabel(sourceId)} to {targetIds.Count} Pokémon.");
                }
            }
        }

        private void SaveAllChanges()
        {
            try
            {
                foreach (var kvp in personalData)
                {
                    kvp.Value.SaveToFileDefaultDir(kvp.Key, false);
                }
                SetClean();
                UpdateStatus("All changes saved successfully!");
                MessageBox.Show("All TM/HM compatibility changes have been saved.", "Save Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

            if (currentMode == ViewMode.ByPokemon)
            {
                statusStrip.Items[0].Text =
                    $"{speciesCount} Pokémon in {families.Count} evolution families. {selectedSpeciesIds.Count} selected." +
                    $"{(isDirty ? " [Unsaved Changes]" : "")}";
            }
            else
            {
                string machineLabel = currentMachineIndex >= 0 && currentMachineIndex < 100
                    ? $"{TMEditor.MachineLabelFromIndex(currentMachineIndex)} - {(currentMachineIndex < machineMoveNames.Length ? machineMoveNames[currentMachineIndex] : "???")}"
                    : "?";
                int compatCount = personalData.Count(kvp => kvp.Value.machines.Contains((byte)currentMachineIndex));
                statusStrip.Items[0].Text =
                    $"{machineLabel}: {compatCount} of {speciesCount} Pokémon compatible.{(isDirty ? " [Unsaved Changes]" : "")}";
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
            this.Text = "TM/HM Bulk Editor" + (isDirty ? "*" : "");
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

    public class CopyMachinesForm : Form
    {
        private ComboBox sourceCombo;
        private TreeView targetTree;
        private Button btnOK;
        private Button btnCancel;
        private string[] pokemonNames;
        private bool suppressTreeEvents = false;

        public int SelectedSourceId => sourceCombo.SelectedIndex;
        public List<int> SelectedTargetIds
        {
            get
            {
                var result = new List<int>();
                foreach (TreeNode top in targetTree.Nodes)
                {
                    if (top.Tag is List<int>)
                    {
                        foreach (TreeNode child in top.Nodes)
                        {
                            if (child.Checked) result.Add((int)child.Tag);
                        }
                    }
                    else if (top.Checked)
                    {
                        result.Add((int)top.Tag);
                    }
                }
                return result;
            }
        }

        public CopyMachinesForm(string[] pokemonNames, List<List<int>> families, int preselectedSourceId)
        {
            this.pokemonNames = pokemonNames;
            InitializeComponent(families, preselectedSourceId);
        }

        private string SpeciesLabel(int id) =>
            id >= 0 && id < pokemonNames.Length ? $"{id:0000} - {pokemonNames[id]}" : $"{id:0000} - ???";

        private void InitializeComponent(List<List<int>> families, int preselectedSourceId)
        {
            this.Size = new Size(420, 620);
            this.Text = "Copy TM/HM Compatibility";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MinimizeBox = false;
            this.MaximizeBox = false;

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(8) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

            layout.Controls.Add(new Label { Text = "Copy compatibility FROM:", AutoSize = true, Margin = new Padding(0, 0, 0, 2) }, 0, 0);

            var items = pokemonNames.Select((name, idx) => $"{idx:000} - {name}").ToArray();

            sourceCombo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            sourceCombo.Items.AddRange(items);
            sourceCombo.SelectedIndex = preselectedSourceId >= 0 && preselectedSourceId < sourceCombo.Items.Count ? preselectedSourceId : 0;
            layout.Controls.Add(sourceCombo, 0, 1);

            layout.Controls.Add(new Label { Text = "Copy TO (check individuals or whole families):", AutoSize = true, Margin = new Padding(0, 8, 0, 2) }, 0, 2);

            targetTree = new TreeView { Dock = DockStyle.Fill, CheckBoxes = true };
            foreach (var fam in families)
            {
                if (fam.Count == 1)
                {
                    targetTree.Nodes.Add(new TreeNode(SpeciesLabel(fam[0])) { Tag = fam[0] });
                }
                else
                {
                    var famNode = new TreeNode($"{SpeciesLabel(fam[0])} family") { Tag = fam };
                    foreach (var id in fam)
                    {
                        famNode.Nodes.Add(new TreeNode(SpeciesLabel(id)) { Tag = id });
                    }
                    targetTree.Nodes.Add(famNode);
                }
            }
            targetTree.AfterCheck += TargetTree_AfterCheck;
            layout.Controls.Add(targetTree, 0, 3);

            var buttonPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
            btnOK = new Button { Text = "OK", DialogResult = DialogResult.OK };
            buttonPanel.Controls.AddRange(new Control[] { btnOK, btnCancel });
            layout.Controls.Add(buttonPanel, 0, 4);

            this.Controls.Add(layout);
            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;
        }

        private void TargetTree_AfterCheck(object sender, TreeViewEventArgs e)
        {
            if (suppressTreeEvents) return;

            suppressTreeEvents = true;
            if (e.Node.Tag is List<int>)
            {
                foreach (TreeNode child in e.Node.Nodes) child.Checked = e.Node.Checked;
            }
            else if (e.Node.Parent != null)
            {
                int total = e.Node.Parent.Nodes.Count;
                int checkedCount = 0;
                foreach (TreeNode sibling in e.Node.Parent.Nodes) if (sibling.Checked) checkedCount++;
                e.Node.Parent.Checked = checkedCount == total;
            }
            suppressTreeEvents = false;
        }
    }
}
