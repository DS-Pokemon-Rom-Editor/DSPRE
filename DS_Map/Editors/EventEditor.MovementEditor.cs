using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DSPRE.ROMFiles;
using DSPRE.Resources;
using static DSPRE.RomInfo;
using DSPRE;

namespace DSPRE.Editors
{
    public partial class EventEditor
    {
        #region Movement Editor constants

        private const ushort MovementEndCommandId = 0x00FE;
        private static readonly string[] MovementTypeOptions = { "Face", "Delay", "Walk", "Jump", "JumpFar", "JumpVeryFar" };
        private static readonly (string Label, int SpeedValue)[] MovementStyleOptions =
        {
            ("Slowest", 32),
            ("Slow", 16),
            ("Normal", 8),
            ("Normal+", 7),
            ("Quick", 6),
            ("Quick+", 4),
            ("Fast", 3),
            ("Faster", 2),
            ("Fastest", 1)
        };

        #endregion

        #region Movement Editor fields
        private ComboBox movementScriptFileComboBox;
        private ComboBox movementActionComboBox;
        private ComboBox movementOverworldIdComboBox;
        private Button movementDeleteActionButton;
        private Button movementNewActionButton;
        private GroupBox movementModeGroup;
        private ComboBox movementSetComboBox;
        private ComboBox movementTypeComboBox;
        private SplitContainer movementSplitContainer;
        private Button movementMoveUpButton;
        private Button movementMoveDownButton;
        private Button movementCopyButton;
        private Button movementPasteButton;
        private Button movementUndoButton;
        private Button movementRedoButton;
        private Button movementClearButton;
        private Button movementSaveButton;
        private Button movementCreateActionButton;
        private Button movementInsertButton;
        private Button movementDeleteButton;
        private RadioButton movementCopyStepsOnlyRadio;
        private RadioButton movementCopyFullActionRadio;
        private Button movementDeleteSelectedButton;
        private CheckBox movementShowPathCheckBox;
        private CheckBox movementShowGhostCheckBox;
        private CheckBox movementShowMarkersCheckBox;

        private ScriptFile _movementEditorScriptFile;
        private int _selectedActionIndex = -1;
        private List<(int x, int y)> _previewPathTiles = new List<(int x, int y)>();
        private List<(int x, int y, int commandIndex)> _previewCommandMarkers = new List<(int x, int y, int commandIndex)>();
        private List<(int x1, int y1, int x2, int y2, int commandRow)> _previewPathSegments = new List<(int x1, int y1, int x2, int y2, int commandRow)>();
        private readonly HashSet<int> _previewSelectedCommandRows = new HashSet<int>();
        private int? _previewAnchorX;
        private int? _previewAnchorY;
        private readonly Stack<MovementEditorUndoState> _movementUndoStack = new Stack<MovementEditorUndoState>();
        private readonly Stack<MovementEditorUndoState> _movementRedoStack = new Stack<MovementEditorUndoState>();
        private string _sessionMovementSet;
        private string _sessionMovementType;
        private bool _sessionShowPath = true;
        private bool _sessionShowGhost = true;
        private bool _sessionShowMarkers = true;
        private int _sessionScriptFileIndex = -1;
        private int _sessionActionIndex = -1;
        private bool _sessionOnSpot;
        private readonly Dictionary<string, ushort> _movementCommandLookup = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<ushort, string> _movementCommandNameById = new Dictionary<ushort, string>();
        private bool _movementEditorEventsWired;
        private bool _movementHasPendingEdits;
        private int _pendingScriptSelectionId = -1;
        private int _pendingActionSelectionIndex = -1;
        private bool _suppressMovementSelectionEvents;

        #endregion

        #region Movement Editor tab creation

        // ═══════════════════════════════════════════════════════════════════════════════════
        // ADJUSTMENT REFERENCE – All Movement Editor layout/size values. Change these to tune.
        // Splitter is LOCKED (user cannot drag). Adjust in code only.
        // ═══════════════════════════════════════════════════════════════════════════════════

        // [Left Panel Width] Fixed width of left panel in pixels. Right panel gets the rest.
        private const int MovementEditorLeftPanelWidth = 224;

        // [Header Section] Row heights: OW row, Script row, Action row, Create/Delete row
        private const int MovementEditorHeaderRowHeight = 20;
        private const int MovementEditorHeaderLastRowHeight = 24;

        // [Command List] Height of bottom button row (Up, Copy, Undo, Clear, Down, Paste, Redo, Save)
        private const int MovementEditorButtonRowHeight = 40;
        private const int MovementEditorButtonHeight = 18;

        // [Direction Pad] Height of the ↑↓←→ direction buttons area
        private const int MovementEditorDirectionPadRowHeight = 120;

        // [Movement Command] Row height for Type, Style, On Spot
        private const int MovementEditorModeRowHeight = 18;

        // [Command ListView] Column widths: "Use" checkbox col, "Rep" col (-2 = Command col auto-fills)
        private const int MovementEditorListViewUseColWidth = 20;
        private const int MovementEditorListViewCommandColWidth = 118;
        private const int MovementEditorListViewRepColWidth = 44;

        // [Header] Left column width for labels (OW, etc.)
        private const int MovementEditorHeaderLabelColWidth = 90;

        // [Movement Command] Left column width for Type/Style/<#> labels
        private const int MovementEditorModeLabelColWidth = 50;

        // NOTE: FlowLayoutPanel uses AutoSize = false with explicit Size to avoid layout drift.
        // If you need AutoSize: set AutoSize = true and remove Size; control sizes to content.

        // QUICK LOOKUP – Constant → Component:
        // MovementEditorLeftPanelWidth → Left panel width (splitter position, LOCKED)
        // MovementEditorHeaderRowHeight → OW/Script/Action rows
        // MovementEditorHeaderLastRowHeight → Create/Delete buttons row
        // MovementEditorButtonRowHeight → Up/Copy/Undo/Clear/Down/Paste/Redo/Save row
        // MovementEditorButtonHeight → Each of those 8 buttons
        // MovementEditorDirectionPadRowHeight → Direction pad (↑↓←→) area
        // MovementEditorModeRowHeight → Type/Style/On Spot rows
        // MovementEditorListViewUseColWidth → "Use" column
        // MovementEditorListViewRepColWidth → "Rep" column
        // MovementEditorHeaderLabelColWidth → Placeholder/Overworld label column
        // MovementEditorModeLabelColWidth → Type/Style/<#> label column

        /// <summary>
        /// Idempotent: ensures the Movement Editor tab exists and is attached to eventsTabControl.
        /// Call from constructor and SetupEventEditor. Safe to call multiple times.
        /// </summary>
        private void EnsureMovementEditorTab()
        {
            if (eventsTabControl == null)
            {
                AppLogger.Debug("EnsureMovementEditorTab: eventsTabControl is null, skipping.");
                return;
            }

            if (!eventsTabControl.TabPages.Contains(signsTabPage))
            {
                AppLogger.Warn("EnsureMovementEditorTab: eventsTabControl does not contain Spawnables tab - wrong TabControl? TabPages.Count=" + eventsTabControl.TabPages.Count);
                return;
            }

            if (movementEditorTabPage == null || movementEditorTabPage.IsDisposed)
            {
                AppLogger.Debug("EnsureMovementEditorTab: Creating Movement Editor tab. TabPages.Count before=" + eventsTabControl.TabPages.Count);
                CreateMovementEditorTab();
                AppLogger.Debug("EnsureMovementEditorTab: movementEditorTabPage created and added. TabPages.Count after=" + eventsTabControl.TabPages.Count);
                return;
            }

            if (!eventsTabControl.TabPages.Contains(movementEditorTabPage))
            {
                AppLogger.Debug("EnsureMovementEditorTab: Tab exists but was detached, re-adding. TabPages.Count=" + eventsTabControl.TabPages.Count);
                eventsTabControl.TabPages.Add(movementEditorTabPage);
                AppLogger.Debug("EnsureMovementEditorTab: movementEditorTabPage re-added.");
            }

            if (movementEditorTabPage.Controls.Count == 0)
            {
                AppLogger.Debug("EnsureMovementEditorTab: Tab exists but is empty (Designer placeholder), populating content.");
                CreateMovementEditorTab();
            }
            else if (!_movementEditorEventsWired)
            {
                AppLogger.Debug("EnsureMovementEditorTab: Tab has content (from Designer), wiring events and initializing.");
                WireMovementEditorEventsAndInitialize();
            }
        }

        private void CreateMovementEditorTab()
        {
            if (movementEditorTabPage != null && !movementEditorTabPage.IsDisposed && movementEditorTabPage.Controls.Count > 0)
                return;

            if (movementEditorTabPage == null || movementEditorTabPage.IsDisposed)
                movementEditorTabPage = new TabPage("Movement Editor");

            bool needToAddToTabControl = eventsTabControl != null && !eventsTabControl.TabPages.Contains(movementEditorTabPage);
            movementSplitContainer = new SplitContainer {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                FixedPanel = FixedPanel.Panel1,
                IsSplitterFixed = true
            };
            var split = movementSplitContainer;
            movementEditorTabPage.Controls.Add(movementSplitContainer);

            EventHandler handler = null;
            handler = (s, e) =>
            {
                if (split.Width <= 0) return;
                int leftW = Math.Min(MovementEditorLeftPanelWidth, Math.Max(80, split.Width - 80));
                split.SplitterDistance = leftW;
                movementEditorTabPage.SizeChanged -= handler;
            };
            movementEditorTabPage.SizeChanged += handler;

            // [Left Panel] Container for header, command list, and buttons
            var leftPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(1) };
            // [Right Panel] Container for preview, direction pad, movement command
            var rightPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = false, Padding = new Padding(1) };

            // [Left Stack] Main layout: header | command list | buttons
            var leftStack = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(1) };
            leftStack.RowStyles.Add(new RowStyle(SizeType.Absolute, MovementEditorHeaderRowHeight * 3 + MovementEditorHeaderLastRowHeight));
            leftStack.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            leftStack.RowStyles.Add(new RowStyle(SizeType.Absolute, MovementEditorButtonRowHeight));
            // [Header Table] OW, Script, Action, Create/Delete
            var headerTable = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, RowCount = 4, Padding = new Padding(1) };
            headerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, MovementEditorHeaderLabelColWidth));
            headerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            for (int r = 0; r < 3; r++) headerTable.RowStyles.Add(new RowStyle(SizeType.Absolute, MovementEditorHeaderRowHeight));
            headerTable.RowStyles.Add(new RowStyle(SizeType.Absolute, MovementEditorHeaderLastRowHeight));
            // [Overworld ID Dropdown] OW 0, OW 1, etc. – anchor for path preview
            movementOverworldIdComboBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180, Height = 21 };
            // [Script File Dropdown] Script File 844 (Current Header)
            movementScriptFileComboBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DrawMode = DrawMode.OwnerDrawFixed, Width = 180, Height = 21 };
            // [Action Dropdown] Action 1, Action 2, etc.
            movementActionComboBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180, Height = 21 };
            // [Create Action Button] "Create Action" button
            movementNewActionButton = new Button { Text = "Create Action", Size = new Size(90, 22) };
            // [Delete Action Button] "Delete Action" button
            movementDeleteActionButton = new Button { Text = "Delete Action", Size = new Size(90, 22) };
            headerTable.Controls.Add(new Label { Text = "OW:", Size = new Size(30, 15) }, 0, 0);
            headerTable.Controls.Add(movementOverworldIdComboBox, 1, 0);
            movementScriptFileComboBox.Dock = DockStyle.Fill;
            movementActionComboBox.Dock = DockStyle.Fill;
            headerTable.Controls.Add(movementScriptFileComboBox, 0, 1);
            headerTable.SetColumnSpan(movementScriptFileComboBox, 2);
            headerTable.Controls.Add(movementActionComboBox, 0, 2);
            headerTable.SetColumnSpan(movementActionComboBox, 2);
            var actionButtonsRow = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = false, Size = new Size(190, 24) };
            actionButtonsRow.Controls.Add(movementNewActionButton);
            actionButtonsRow.Controls.Add(movementDeleteActionButton);
            headerTable.Controls.Add(actionButtonsRow, 0, 3);
            headerTable.SetColumnSpan(actionButtonsRow, 2);
            leftStack.Controls.Add(headerTable, 0, 0);

            // [Command List Panel] Wrapper for the command ListView
            var commandTablePanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 1, 0, 1), MinimumSize = new Size(64, 24) };
            // [Command ListView] Use | Command | Rep columns
            movementCommandListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                CheckBoxes = true,
                MinimumSize = new Size(64, 24)
            };
            movementCommandListView.Columns.Add("", MovementEditorListViewUseColWidth);
            movementCommandListView.Columns.Add("Command", MovementEditorListViewCommandColWidth);
            movementCommandListView.Columns.Add("Rep", MovementEditorListViewRepColWidth);
            commandTablePanel.Controls.Add(movementCommandListView);
            leftStack.Controls.Add(commandTablePanel, 0, 1);

            // [Button Grid Outline] Wrapper for Up/Down/Copy/Paste/etc.
            var buttonGridOutline = new GroupBox { Text = "", Dock = DockStyle.Fill, Padding = new Padding(1), MinimumSize = new Size(160, MovementEditorButtonRowHeight) };
            // [Button Grid] 4x2 grid of command buttons
            var buttonGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 2, Padding = new Padding(0) };
            for (int c = 0; c < 4; c++) buttonGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            buttonGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, MovementEditorButtonHeight));
            buttonGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            // [Up Button] Move selected command up
            movementMoveUpButton = new Button { Text = "Up", Size = new Size(36, 36 - 2) };
            // [Copy Button] Copy selected commands
            movementCopyButton = new Button { Text = "Copy", Size = new Size(36, MovementEditorButtonHeight - 2) };
            // [Undo Button] Undo last change
            movementUndoButton = new Button { Text = "Undo", Size = new Size(36, MovementEditorButtonHeight - 2) };
            // [Clear Button] Clear selected or all commands
            movementClearButton = new Button { Text = "Clear", Size = new Size(36, MovementEditorButtonHeight - 2) };
            // [Down Button] Move selected command down
            movementMoveDownButton = new Button { Text = "Down", Size = new Size(36, MovementEditorButtonHeight - 2) };
            // [Paste Button] Paste from clipboard
            movementPasteButton = new Button { Text = "Paste", Size = new Size(36, MovementEditorButtonHeight - 2) };
            // [Redo Button] Redo last undone change
            movementRedoButton = new Button { Text = "Redo", Size = new Size(36, MovementEditorButtonHeight - 2) };
            // [Save Button] Save action to script file
            movementSaveButton = new Button { Text = "Save", Size = new Size(24, MovementEditorButtonHeight - 2) };
            movementInsertButton = new Button { Text = "Ins", Visible = false };
            movementDeleteButton = new Button { Text = "Del", Visible = false };
            movementDeleteSelectedButton = new Button { Text = "Del sel", Visible = false };
            movementCopyStepsOnlyRadio = new RadioButton { Text = "Steps", Checked = true, Visible = false };
            movementCopyFullActionRadio = new RadioButton { Text = "Full", Visible = false };
            buttonGrid.Controls.Add(movementMoveUpButton, 0, 0);
            buttonGrid.Controls.Add(movementCopyButton, 1, 0);
            buttonGrid.Controls.Add(movementUndoButton, 2, 0);
            buttonGrid.Controls.Add(movementClearButton, 3, 0);
            buttonGrid.Controls.Add(movementMoveDownButton, 0, 1);
            buttonGrid.Controls.Add(movementPasteButton, 1, 1);
            buttonGrid.Controls.Add(movementRedoButton, 2, 1);
            buttonGrid.Controls.Add(movementSaveButton, 3, 1);
            buttonGridOutline.Controls.Add(buttonGrid);
            leftStack.Controls.Add(buttonGridOutline, 0, 2);

            leftPanel.Controls.Add(leftStack);
            split.Panel1.Controls.Add(leftPanel);

            // [Direction Pad Group] "Direction" group box with ↑↓←→
            movementDirectionPadGroup = new GroupBox { Text = "Direction", Dock = DockStyle.Fill, Padding = new Padding(1), MinimumSize = new Size(60, MovementEditorDirectionPadRowHeight) };
            var padTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 3 };
            padTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33f));
            padTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34f));
            padTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33f));
            padTable.RowStyles.Add(new RowStyle(SizeType.Percent, 33f));
            padTable.RowStyles.Add(new RowStyle(SizeType.Percent, 34f));
            padTable.RowStyles.Add(new RowStyle(SizeType.Percent, 33f));
            // [Up Arrow Button] Insert Face/Walk North
            DPadNorthButton = new Button { Text = "↑", Dock = DockStyle.Fill, Margin = new Padding(1), Font = new Font("Segoe UI", 7f, FontStyle.Bold), Size = new Size(24, 24) };
            // [Down Arrow Button] Insert Face/Walk South
            DPadSouthButton = new Button { Text = "↓", Dock = DockStyle.Fill, Margin = new Padding(2), Font = new Font("Segoe UI", 7f, FontStyle.Bold), Size = new Size(45, 45) };
            // [Left Arrow Button] Insert Face/Walk West
            DPadWestButton = new Button { Text = "←", Dock = DockStyle.Fill, Margin = new Padding(1), Font = new Font("Segoe UI", 7f, FontStyle.Bold), Size = new Size(24, 24) };
            // [Right Arrow Button] Insert Face/Walk East
            DPadEastButton = new Button { Text = "→", Dock = DockStyle.Fill, Margin = new Padding(1), Font = new Font("Segoe UI", 7f, FontStyle.Bold), Size = new Size(24, 24) };
            padTable.Controls.Add(DPadNorthButton, 1, 0);
            padTable.Controls.Add(DPadSouthButton, 1, 2);
            padTable.Controls.Add(DPadWestButton, 0, 1);
            padTable.Controls.Add(DPadEastButton, 2, 1);
            movementDirectionPadGroup.Controls.Add(padTable);

            // [Movement Command Group] Type, Style, On Spot
            movementModeGroup = new GroupBox { Text = "Movement Command", Dock = DockStyle.Fill, Padding = new Padding(1), MinimumSize = new Size(80, 100) };
            var modeTlp = new TableLayoutPanel { ColumnCount = 2, RowCount = 3, Dock = DockStyle.Fill };
            modeTlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, MovementEditorModeLabelColWidth));
            modeTlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            for (int r = 0; r < 3; r++) modeTlp.RowStyles.Add(new RowStyle(SizeType.Absolute, MovementEditorModeRowHeight));
            movementSetComboBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100, Height = 21 };
            movementTypeComboBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100, Height = 21 };
            DPadOnSpotCheckBoxButton = new CheckBox { Text = "On Spot", Size = new Size(70, 18) };
            modeTlp.Controls.Add(new Label { Text = "Type:", Size = new Size(35, 15) }, 0, 0);
            modeTlp.Controls.Add(movementSetComboBox, 1, 0);
            modeTlp.Controls.Add(new Label { Text = "Style:", Size = new Size(35, 15) }, 0, 1);
            modeTlp.Controls.Add(movementTypeComboBox, 1, 1);
            modeTlp.Controls.Add(DPadOnSpotCheckBoxButton, 0, 2);
            modeTlp.SetColumnSpan(DPadOnSpotCheckBoxButton, 2);
            movementModeGroup.Controls.Add(modeTlp);

            var rightStack = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(0)
            };
            rightStack.RowStyles.Add(new RowStyle(SizeType.Absolute, MovementEditorDirectionPadRowHeight));
            rightStack.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            rightStack.Controls.Add(movementDirectionPadGroup, 0, 0);
            rightStack.Controls.Add(movementModeGroup, 0, 1);
            rightPanel.Controls.Add(rightStack);

            split.Panel2.Controls.Add(rightPanel);
            movementEditorTabPage.Controls.Add(split);
            if (needToAddToTabControl && eventsTabControl != null)
                eventsTabControl.TabPages.Add(movementEditorTabPage);

            // [Show Path Checkbox] In Renderer Settings – toggles path line on map
            movementShowPathCheckBox = new CheckBox { Text = "Show path", Size = new Size(85, 18), Checked = true };
            // [Show Ghost Checkbox] In Renderer Settings – toggles ghost sprite at path end
            movementShowGhostCheckBox = new CheckBox { Text = "Show ghost", Size = new Size(90, 18), Checked = true };
            // [Step Markers Checkbox] In Renderer Settings – toggles step markers on path
            movementShowMarkersCheckBox = new CheckBox { Text = "Step markers", Size = new Size(95, 18), Checked = true };
            // [Renderer Settings] Show path/ghost/markers added to groupBox21 (EventEditor right panel).
            // Adjust Location (X,Y) and Size below. groupBox21 is in EventEditor.Designer.cs.
            if (groupBox21 != null)
            {
                groupBox21.SuspendLayout();
                movementShowPathCheckBox.Location = new Point(9, 108);
                movementShowGhostCheckBox.Location = new Point(9, 126);
                movementShowMarkersCheckBox.Location = new Point(9, 144);
                movementShowPathCheckBox.Font = groupBox21.Font;
                movementShowGhostCheckBox.Font = groupBox21.Font;
                movementShowMarkersCheckBox.Font = groupBox21.Font;
                groupBox21.Controls.Add(movementShowPathCheckBox);
                groupBox21.Controls.Add(movementShowGhostCheckBox);
                groupBox21.Controls.Add(movementShowMarkersCheckBox);
                groupBox21.Size = new Size(groupBox21.Width, 185);
                if (eventAreaDataUpDown != null) eventAreaDataUpDown.Location = new Point(9, 162);
                if (eventMapTextureLabel != null) eventMapTextureLabel.Location = new Point(51, 164);
                groupBox21.ResumeLayout(true);
            }

            WireMovementEditorEventsAndInitialize();
        }

        private void EnsureMovementEditorControlAliases()
        {
            if (movementScriptFileComboBox == null && ScriptFileDropdown != null)
            {
                movementScriptFileComboBox = ScriptFileDropdown;
                movementActionComboBox = ActionDropdown;
                movementOverworldIdComboBox = OverworldIDDropdown;
                movementDeleteActionButton = DeleteActionButton;
                if (movementNewActionButton == null) movementNewActionButton = NewActionButton;
                movementSetComboBox = MovementTypeDropdown;
                movementTypeComboBox = MovementStyleDropdown;
            }
            if (movementSetComboBox == null && MovementTypeDropdown != null)
                movementSetComboBox = MovementTypeDropdown;
            if (movementTypeComboBox == null && MovementStyleDropdown != null)
                movementTypeComboBox = MovementStyleDropdown;
            if (movementSetComboBox == null && movementEditorTabPage != null)
            {
                var found = movementEditorTabPage.Controls.Find("MovementTypeDropdown", true);
                if (found.Length > 0 && found[0] is ComboBox cbType) movementSetComboBox = cbType;
            }
            if (movementTypeComboBox == null && movementEditorTabPage != null)
            {
                var found = movementEditorTabPage.Controls.Find("MovementStyleDropdown", true);
                if (found.Length > 0 && found[0] is ComboBox cbStyle) movementTypeComboBox = cbStyle;
            }
        }

        private void WireMovementEditorEventsAndInitialize()
        {
            if (_movementEditorEventsWired) return;
            _movementEditorEventsWired = true;

            EnsureMovementEditorControlAliases();

            if (movementCommandListView != null && movementCommandListView.Columns.Count == 0)
            {
                movementCommandListView.Columns.Add("", MovementEditorListViewUseColWidth);
                movementCommandListView.Columns.Add("Command", MovementEditorListViewCommandColWidth);
                movementCommandListView.Columns.Add("Rep", MovementEditorListViewRepColWidth);
            }

            if (movementSplitContainer != null)
            {
                movementEditorTabPage.SizeChanged += (s, e) =>
                {
                    if (movementSplitContainer.Width <= 0) return;
                    int leftW = Math.Min(MovementEditorLeftPanelWidth, Math.Max(80, movementSplitContainer.Width - 80));
                    movementSplitContainer.SplitterDistance = leftW;
                };
            }

            if (movementShowPathCheckBox == null && groupBox21 != null)
            {
                movementShowPathCheckBox = new CheckBox { Text = "Show path", Size = new Size(85, 18), Checked = true };
                movementShowGhostCheckBox = new CheckBox { Text = "Show ghost", Size = new Size(90, 18), Checked = true };
                movementShowMarkersCheckBox = new CheckBox { Text = "Step markers", Size = new Size(95, 18), Checked = true };
                groupBox21.SuspendLayout();
                movementShowPathCheckBox.Location = new Point(9, 108);
                movementShowGhostCheckBox.Location = new Point(9, 126);
                movementShowMarkersCheckBox.Location = new Point(9, 144);
                movementShowPathCheckBox.Font = groupBox21.Font;
                movementShowGhostCheckBox.Font = groupBox21.Font;
                movementShowMarkersCheckBox.Font = groupBox21.Font;
                groupBox21.Controls.Add(movementShowPathCheckBox);
                groupBox21.Controls.Add(movementShowGhostCheckBox);
                groupBox21.Controls.Add(movementShowMarkersCheckBox);
                groupBox21.Size = new Size(groupBox21.Width, 185);
                if (eventAreaDataUpDown != null) eventAreaDataUpDown.Location = new Point(9, 162);
                if (eventMapTextureLabel != null) eventMapTextureLabel.Location = new Point(51, 164);
                groupBox21.ResumeLayout(true);
            }

            WireMovementEditorEvents();
            eventsTabControl.SelectedIndexChanged += (s, ev) =>
            {
                if (eventsTabControl.SelectedTab == movementEditorTabPage)
                {
                    EnsureMovementEditorControlAliases();
                    MovementDirectionDeltaMap.Invalidate();
                    BuildMovementCommandCaches();
                    PopulateMovementSetAndTypeComboBoxes();
                    PopulateMovementOverworldIdComboBox();
                    SyncMovementOverworldSelectionFromSelectedEvent();
                    PopulateMovementEditorScriptFileComboBox();
                    RestoreMovementEditorSessionState();
                    if (movementScriptFileComboBox != null && movementScriptFileComboBox.SelectedIndex < 0)
                        SetMovementScriptFileFromCurrentEventHeader();
                    SyncMovementAnchorUiState();
                    ComputeMovementPreviewPath();
                    DisplayActiveEvents();
                }
                else
                {
                    SaveMovementEditorSessionState();
                }
            };
            PopulateMovementEditorScriptFileComboBox();
            PopulateMovementOverworldIdComboBox();
            BuildMovementCommandCaches();
            PopulateMovementSetAndTypeComboBoxes();
        }

        #endregion

        #region Movement Editor event wiring

        private void WireDirectionButton(Button btn, string direction)
        {
            if (btn != null)
            {
                btn.Click += (s, e) => MovementDirectionPad_Click(direction);
            }
            else if (movementPadTable != null)
            {
                int col = direction == "North" || direction == "South" ? 1 : direction == "West" ? 0 : 2;
                int row = direction == "North" ? 0 : direction == "South" ? 2 : 1;
                var ctrl = movementPadTable.GetControlFromPosition(col, row);
                if (ctrl is Button b)
                    b.Click += (s, e) => MovementDirectionPad_Click(direction);
            }
        }

        private void WireMovementEditorEvents()
        {
            if (movementCommandListView != null)
            {
                movementCommandListView.CheckBoxes = false;
                movementCommandListView.MultiSelect = true;
                movementCommandListView.DoubleClick += MovementCommandListView_DoubleClick;
                movementCommandListView.ItemCheck += MovementCommandListView_ItemCheck;
                movementCommandListView.SelectedIndexChanged += MovementCommandListView_SelectedIndexChanged;
            }
            if (movementScriptFileComboBox != null)
            {
                movementScriptFileComboBox.SelectedIndexChanged += MovementScriptFileComboBox_SelectedIndexChanged;
                movementScriptFileComboBox.DrawItem += MovementScriptFileComboBox_DrawItem;
            }
            if (movementActionComboBox != null)
                movementActionComboBox.SelectedIndexChanged += MovementActionComboBox_SelectedIndexChanged;
            if (movementNewActionButton != null && movementNewActionButton != NewActionButton)
                movementNewActionButton.Click += MovementNewActionButton_Click;
            if (movementDeleteActionButton != null && movementDeleteActionButton != DeleteActionButton)
                movementDeleteActionButton.Click += MovementDeleteActionButton_Click;
            if (SetActionButton != null)
                SetActionButton.Click += MovementApplyActionSelection_Click;
            if (SetScriptButton != null)
                SetScriptButton.Click += MovementApplyScriptSelection_Click;
            if (ExportActionButton != null)
                ExportActionButton.Click += MovementExportButton_Click;
            if (movementSetComboBox != null)
                movementSetComboBox.SelectedIndexChanged += MovementSetOrTypeComboBox_SelectedIndexChanged;
            if (movementTypeComboBox != null)
                movementTypeComboBox.SelectedIndexChanged += MovementSetOrTypeComboBox_SelectedIndexChanged;
            if (DPadOnSpotCheckBoxButton != null)
                DPadOnSpotCheckBoxButton.CheckedChanged += MovementSetOrTypeComboBox_SelectedIndexChanged;
            bool useDesignerLayout = (NewActionButton != null && movementNewActionButton == NewActionButton);
            if (!useDesignerLayout)
            {
                WireDirectionButton(DPadNorthButton, "North");
                WireDirectionButton(DPadSouthButton, "South");
            }
            WireDirectionButton(DPadWestButton, "West");
            WireDirectionButton(DPadEastButton, "East");
            if (ExclamationButton != null)
                ExclamationButton.Click += (s, e) => MovementEmoteButton_Click("EmoteExclamation");
            if (DoubleExclamationButton != null)
                DoubleExclamationButton.Click += (s, e) => MovementEmoteButton_Click("EmoteDoubleExclamation");
            if (movementMoveUpButton != null) movementMoveUpButton.Click += MovementMoveUpButton_Click;
            else if (MoveListUpButton != null) MoveListUpButton.Click += MovementMoveUpButton_Click;
            if (movementMoveDownButton != null) movementMoveDownButton.Click += MovementMoveDownButton_Click;
            else if (MoveListDownButton != null) MoveListDownButton.Click += MovementMoveDownButton_Click;
            if (movementInsertButton != null) movementInsertButton.Click += MovementInsertButton_Click;
            if (movementDeleteButton != null) movementDeleteButton.Click += MovementDeleteButton_Click;
            if (movementClearButton != null) movementClearButton.Click += MovementClearButton_Click;
            else if (DeleteListSelectedButton != null) DeleteListSelectedButton.Click += MovementClearButton_Click;
            if (movementUndoButton != null) movementUndoButton.Click += MovementUndoButton_Click;
            else if (UndoListChangesButton != null) UndoListChangesButton.Click += MovementUndoButton_Click;
            if (movementRedoButton != null) movementRedoButton.Click += MovementRedoButton_Click;
            else if (RedoListChangesButton != null) RedoListChangesButton.Click += MovementRedoButton_Click;
            if (movementSaveButton != null) movementSaveButton.Click += MovementSaveButton_Click;
            else if (ConfirmListSelectedButton != null) ConfirmListSelectedButton.Click += MovementSaveButton_Click;
            if (movementCopyButton != null) movementCopyButton.Click += MovementCopyButton_Click;
            else if (CopyListButton != null) CopyListButton.Click += MovementCopyButton_Click;
            if (movementPasteButton != null) movementPasteButton.Click += MovementPasteButton_Click;
            else if (PasteListButton != null) PasteListButton.Click += MovementPasteButton_Click;
            if (movementDeleteSelectedButton != null) movementDeleteSelectedButton.Click += MovementDeleteSelectedButton_Click;
            if (movementShowPathCheckBox != null) movementShowPathCheckBox.CheckedChanged += MovementPreviewCheckBox_CheckedChanged;
            if (movementShowGhostCheckBox != null) movementShowGhostCheckBox.CheckedChanged += MovementPreviewCheckBox_CheckedChanged;
            if (movementShowMarkersCheckBox != null) movementShowMarkersCheckBox.CheckedChanged += MovementPreviewCheckBox_CheckedChanged;
            if (movementOverworldIdComboBox != null)
                movementOverworldIdComboBox.SelectedIndexChanged += MovementOverworldIdComboBox_SelectedIndexChanged;
            if (OverworldPointerCheckBox != null)
                OverworldPointerCheckBox.CheckedChanged += MovementAnchorModeCheckBox_CheckedChanged;
            if (PlaceholderPointerCheckBox != null)
                PlaceholderPointerCheckBox.CheckedChanged += MovementAnchorModeCheckBox_CheckedChanged;
            if (PointerXUpDown != null)
                PointerXUpDown.ValueChanged += MovementPointerValue_ValueChanged;
            if (PointerYUpDown != null)
                PointerYUpDown.ValueChanged += MovementPointerValue_ValueChanged;
            if (TeleportUpButton != null)
                TeleportUpButton.Click += (s, e) => MovementInsertNamedCommand("TeleportUp");
            if (TeleportDownButton != null)
                TeleportDownButton.Click += (s, e) => MovementInsertNamedCommand("TeleportDown");
            if (SetVisibleButton != null)
                SetVisibleButton.Click += (s, e) => MovementInsertNamedCommand("SetVisible");
            if (SetInvisibleButton != null)
                SetInvisibleButton.Click += (s, e) => MovementInsertNamedCommand("SetInvisible");
            if (LockDirectionButton != null)
                LockDirectionButton.Click += (s, e) => MovementInsertNamedCommand("LockDir");
            if (UnlockDirButton != null)
                UnlockDirButton.Click += (s, e) => MovementInsertNamedCommand("ReleaseDir");
            if (DeleteListSelectedButton != null)
                DeleteListSelectedButton.Click += MovementDeleteSelectedButton_Click;

            SyncMovementAnchorUiState();
        }

        private void MovementScriptFileComboBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();
            if (e.Index < 0 || e.Index >= movementScriptFileComboBox.Items.Count) return;
            if (!(movementScriptFileComboBox.Items[e.Index] is MovementScriptFileEntry entry)) return;

            Color textColor = e.ForeColor;
            if (entry.IsCurrentHeaderScript)
                textColor = Color.MediumBlue;
            else if (entry.IsRelatedScript)
                textColor = Color.MediumBlue;

            using (var brush = new SolidBrush(textColor))
                e.Graphics.DrawString(entry.ToString(), e.Font, brush, e.Bounds);
            e.DrawFocusRectangle();
        }

        #endregion

        #region Movement Editor population

        private void PopulateMovementEditorScriptFileComboBox()
        {
            if (_parent == null || movementScriptFileComboBox == null) return;
            int count = Filesystem.GetScriptCount();
            int previousScriptId = -1;
            if (movementScriptFileComboBox.SelectedItem is MovementScriptFileEntry prev)
                previousScriptId = prev.ScriptFileId;
            else if (movementScriptFileComboBox.SelectedIndex >= 0)
                previousScriptId = movementScriptFileComboBox.SelectedIndex;

            var currentAndRelated = GetRelatedScriptFileIdsForCurrentEvent();
            var prioritized = new List<int>();
            if (currentAndRelated.current >= 0 && currentAndRelated.current < count)
                prioritized.Add(currentAndRelated.current);
            foreach (int relatedId in currentAndRelated.related.OrderBy(x => x))
                if (relatedId >= 0 && relatedId < count && !prioritized.Contains(relatedId))
                    prioritized.Add(relatedId);

            movementScriptFileComboBox.BeginUpdate();
            movementScriptFileComboBox.Items.Clear();

            foreach (int scriptId in prioritized)
            {
                movementScriptFileComboBox.Items.Add(new MovementScriptFileEntry(
                    scriptId,
                    isCurrentHeaderScript: scriptId == currentAndRelated.current,
                    isRelatedScript: scriptId != currentAndRelated.current
                ));
            }

            for (int i = 0; i < count; i++)
            {
                if (prioritized.Contains(i)) continue;
                movementScriptFileComboBox.Items.Add(new MovementScriptFileEntry(i, false, false));
            }
            movementScriptFileComboBox.EndUpdate();

            if (movementScriptFileComboBox.Items.Count == 0)
                return;

            int desiredIndex = -1;
            if (previousScriptId >= 0)
            {
                for (int i = 0; i < movementScriptFileComboBox.Items.Count; i++)
                {
                    if (movementScriptFileComboBox.Items[i] is MovementScriptFileEntry entry && entry.ScriptFileId == previousScriptId)
                    {
                        desiredIndex = i;
                        break;
                    }
                }
            }
            if (desiredIndex < 0 && currentAndRelated.current >= 0)
            {
                for (int i = 0; i < movementScriptFileComboBox.Items.Count; i++)
                {
                    if (movementScriptFileComboBox.Items[i] is MovementScriptFileEntry entry && entry.ScriptFileId == currentAndRelated.current)
                    {
                        desiredIndex = i;
                        break;
                    }
                }
            }
            if (desiredIndex < 0) desiredIndex = 0;
            movementScriptFileComboBox.SelectedIndex = desiredIndex;
            if (movementScriptFileComboBox.SelectedItem is MovementScriptFileEntry selected)
            {
                _pendingScriptSelectionId = selected.ScriptFileId;
                if (_movementEditorScriptFile == null || _movementEditorScriptFile.fileID != selected.ScriptFileId)
                    ApplyMovementScriptSelection(selected.ScriptFileId, keepActionSelection: true);
            }
        }

        private (int current, HashSet<int> related) GetRelatedScriptFileIdsForCurrentEvent()
        {
            var related = new HashSet<int>();
            int current = -1;
            if (_parent == null || selectEventComboBox == null || selectEventComboBox.SelectedIndex < 0)
                return (current, related);

            ushort currentHeaderId;
            if (_preferredMovementHeaderId.HasValue)
            {
                currentHeaderId = _preferredMovementHeaderId.Value;
                MapHeader preferred = LoadHeaderById(currentHeaderId);
                if (preferred == null || preferred.eventFileID != selectEventComboBox.SelectedIndex)
                {
                    _preferredMovementHeaderId = null;
                    if (!_parent.eventToHeader.TryGetValue((ushort)selectEventComboBox.SelectedIndex, out currentHeaderId))
                        return (current, related);
                }
            }
            else if (!_parent.eventToHeader.TryGetValue((ushort)selectEventComboBox.SelectedIndex, out currentHeaderId))
            {
                return (current, related);
            }

            MapHeader currentHeader = LoadHeaderById(currentHeaderId);
            if (currentHeader != null)
                current = currentHeader.scriptFileID;

            if (EditorPanels.headerEditor?.internalNames == null)
                return (current, related);

            for (ushort i = 0; i < EditorPanels.headerEditor.internalNames.Count; i++)
            {
                MapHeader header = LoadHeaderById(i);
                if (header == null || header.eventFileID != selectEventComboBox.SelectedIndex)
                    continue;
                related.Add(header.scriptFileID);
            }
            if (current >= 0)
                related.Remove(current);
            return (current, related);
        }

        private static MapHeader LoadHeaderById(ushort headerId)
        {
            try
            {
                if (PatchToolboxDialog.flag_DynamicHeadersPatchApplied || PatchToolboxDialog.CheckFilesDynamicHeadersPatchApplied())
                    return MapHeader.LoadFromFile(RomInfo.gameDirs[DirNames.dynamicHeaders].unpackedDir + "\\" + headerId.ToString("D4"), headerId, 0);
                return MapHeader.LoadFromARM9(headerId);
            }
            catch
            {
                return null;
            }
        }

        private void PopulateMovementOverworldIdComboBox()
        {
            if (movementOverworldIdComboBox == null || currentEvFile == null) return;
            int prev = movementOverworldIdComboBox.SelectedIndex;
            movementOverworldIdComboBox.Items.Clear();
            for (int i = 0; i < currentEvFile.overworlds.Count; i++)
                movementOverworldIdComboBox.Items.Add($"OW {i}");
            if (movementOverworldIdComboBox.Items.Count > 0 && prev >= 0 && prev < movementOverworldIdComboBox.Items.Count)
                movementOverworldIdComboBox.SelectedIndex = prev;
            else if (movementOverworldIdComboBox.Items.Count > 0 && movementOverworldIdComboBox.SelectedIndex < 0)
                movementOverworldIdComboBox.SelectedIndex = 0;
        }

        private static List<string> ParseCsvLine(string line)
        {
            var list = new List<string>();
            int i = 0;
            while (i < line.Length)
            {
                if (line[i] == '"')
                {
                    i++;
                    var sb = new System.Text.StringBuilder();
                    while (i < line.Length && line[i] != '"') { sb.Append(line[i]); i++; }
                    if (i < line.Length) i++;
                    list.Add(sb.ToString());
                }
                else
                {
                    int start = i;
                    while (i < line.Length && line[i] != ',') i++;
                    list.Add(line.Substring(start, i - start));
                    if (i < line.Length) i++;
                }
            }
            return list;
        }

        private void BuildMovementCommandCaches()
        {
            _movementCommandLookup.Clear();
            _movementCommandNameById.Clear();

            var sourceDict = RomInfo.ScriptActionNamesDict;
            if (sourceDict == null || sourceDict.Count == 0)
            {
                if (ScriptDatabase.movementsDict != null && ScriptDatabase.movementsDict.Count > 0)
                {
                    foreach (var kvp in ScriptDatabase.movementsDict)
                    {
                        string name = kvp.Value?.Name;
                        if (string.IsNullOrWhiteSpace(name)) continue;
                        _movementCommandNameById[kvp.Key] = name;
                        _movementCommandLookup[NormalizeCommandName(name)] = kvp.Key;
                    }
                }
                return;
            }

            foreach (var kvp in sourceDict)
            {
                if (string.IsNullOrWhiteSpace(kvp.Value)) continue;
                _movementCommandNameById[kvp.Key] = kvp.Value;
                _movementCommandLookup[NormalizeCommandName(kvp.Value)] = kvp.Key;
            }
        }

        private List<KeyValuePair<ushort, string>> GetAllMovementCommandsForQuickInsert()
        {
            return _movementCommandNameById
                .Where(kvp => kvp.Key != MovementEndCommandId)
                .OrderBy(kvp => kvp.Value, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string NormalizeCommandName(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            var chars = name.Where(char.IsLetterOrDigit).ToArray();
            return new string(chars).ToLowerInvariant();
        }

        private void UpdateMovementTypeUiState()
        {
            if (movementSetComboBox == null || movementTypeComboBox == null) return;
            string type = movementSetComboBox.SelectedItem?.ToString() ?? "Walk";
            bool supportsStyle = !string.Equals(type, "Face", StringComparison.OrdinalIgnoreCase) &&
                                 !string.Equals(type, "JumpFar", StringComparison.OrdinalIgnoreCase) &&
                                 !string.Equals(type, "JumpVeryFar", StringComparison.OrdinalIgnoreCase);
            bool supportsOnSpot = !string.Equals(type, "Face", StringComparison.OrdinalIgnoreCase) &&
                                  !string.Equals(type, "Delay", StringComparison.OrdinalIgnoreCase);
            movementTypeComboBox.Enabled = supportsStyle;
            if (DPadOnSpotCheckBoxButton != null)
            {
                DPadOnSpotCheckBoxButton.Enabled = supportsOnSpot;
                if (!supportsOnSpot) DPadOnSpotCheckBoxButton.Checked = false;
            }

            bool isJumpVeryFar = string.Equals(type, "JumpVeryFar", StringComparison.OrdinalIgnoreCase);
            movementBtnNorthEnabled(!isJumpVeryFar);
            movementBtnSouthEnabled(!isJumpVeryFar);
            if (DPadEastButton != null) DPadEastButton.Enabled = true;
            if (DPadWestButton != null) DPadWestButton.Enabled = true;
        }

        private void movementBtnNorthEnabled(bool enabled) { if (DPadNorthButton != null) DPadNorthButton.Enabled = enabled; }
        private void movementBtnSouthEnabled(bool enabled) { if (DPadSouthButton != null) DPadSouthButton.Enabled = enabled; }

        private void PopulateMovementSetAndTypeComboBoxes()
        {
            if (movementSetComboBox == null || movementTypeComboBox == null) return;
            movementSetComboBox.Items.Clear();
            movementTypeComboBox.Items.Clear();
            foreach (var s in MovementTypeOptions) movementSetComboBox.Items.Add(s);
            foreach (var t in MovementStyleOptions.Select(x => x.Label)) movementTypeComboBox.Items.Add(t);
            if (movementSetComboBox.Items.Count > 0 && movementSetComboBox.SelectedIndex < 0) movementSetComboBox.SelectedIndex = 0;
            if (movementTypeComboBox.Items.Count > 0 && movementTypeComboBox.SelectedIndex < 0) movementTypeComboBox.SelectedIndex = 2;
            UpdateMovementTypeUiState();
        }

        private void MovementSetOrTypeComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateMovementTypeUiState();
        }

        private sealed class MovementCommandItem
        {
            public ushort Id { get; }
            public string Name { get; }
            public MovementCommandItem(ushort id, string name) { Id = id; Name = name; }
            public override string ToString() => Name;
        }

        private sealed class MovementScriptFileEntry
        {
            public int ScriptFileId { get; }
            public bool IsCurrentHeaderScript { get; }
            public bool IsRelatedScript { get; }

            public MovementScriptFileEntry(int scriptFileId, bool isCurrentHeaderScript, bool isRelatedScript)
            {
                ScriptFileId = scriptFileId;
                IsCurrentHeaderScript = isCurrentHeaderScript;
                IsRelatedScript = isRelatedScript;
            }

            public override string ToString()
            {
                return $"Script File {ScriptFileId}";
            }
        }

        #endregion

        #region Movement Editor helpers

        private ScriptActionContainer GetCurrentActionContainer()
        {
            if (_movementEditorScriptFile?.allActions == null || _selectedActionIndex < 0 ||
                _selectedActionIndex >= _movementEditorScriptFile.allActions.Count)
                return null;
            return _movementEditorScriptFile.allActions[_selectedActionIndex];
        }

        private void EnsureEndExists(ScriptActionContainer container)
        {
            if (container?.commands == null) return;
            if (container.commands.Count == 0 || container.commands[container.commands.Count - 1].id != MovementEndCommandId)
            {
                container.commands.Add(new ScriptAction(MovementEndCommandId, 0));
            }
        }

        private static List<ScriptAction> CloneCommandList(List<ScriptAction> commands)
        {
            if (commands == null) return new List<ScriptAction>();
            return commands.Select(c => c.id.HasValue
                ? new ScriptAction(c.id.Value, c.repetitionCount)
                : null).Where(c => c != null).ToList();
        }

        private static string RemoveTrailingHexToken(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;
            int lastSpace = input.LastIndexOf(' ');
            if (lastSpace <= 0 || lastSpace >= input.Length - 1)
                return input;
            string tail = input.Substring(lastSpace + 1);
            if (!tail.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return input;
            string hex = tail.Substring(2);
            return ushort.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _)
                ? input.Substring(0, lastSpace)
                : input;
        }

        private string GetDisplayCommandName(ScriptAction command)
        {
            if (command == null)
                return string.Empty;
            if (command.id == MovementEndCommandId)
                return "End";
            if (!string.IsNullOrWhiteSpace(command.name))
                return RemoveTrailingHexToken(command.name);
            if (command.id.HasValue && _movementCommandNameById.TryGetValue(command.id.Value, out string mappedName))
                return mappedName;
            return command.id?.ToString("X4") ?? string.Empty;
        }

        private void MarkMovementPendingEdits()
        {
            _movementHasPendingEdits = true;
        }

        private void ClearMovementPendingEdits()
        {
            _movementHasPendingEdits = false;
        }

        private bool ConfirmDiscardPendingMovementEdits(string targetLabel)
        {
            if (!_movementHasPendingEdits)
                return true;
            var result = MessageBox.Show(
                $"There are unsaved Movement Editor changes. Switching to {targetLabel} may discard local progress.\n\nDo you want to continue?",
                "Discard unsaved progress?",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            return result == DialogResult.Yes;
        }

        private void RefreshMovementCommandList()
        {
            var selectedRows = new HashSet<int>();
            foreach (ListViewItem selected in movementCommandListView.SelectedItems)
            {
                if (selected.Tag is int row)
                    selectedRows.Add(row);
            }

            movementCommandListView.Items.Clear();
            var container = GetCurrentActionContainer();
            if (container == null) return;
            for (int i = 0; i < container.commands.Count; i++)
            {
                var cmd = container.commands[i];
                bool isEnd = cmd.id == MovementEndCommandId;
                string rep = isEnd ? "" : (cmd.repetitionCount?.ToString() ?? "1");
                var item = new ListViewItem(new[] { "", GetDisplayCommandName(cmd), rep });
                item.Tag = i;
                if (selectedRows.Contains(i))
                    item.Selected = true;
                movementCommandListView.Items.Add(item);
            }
            ComputeMovementPreviewPath();
            DisplayActiveEvents();
        }

        private void PushMovementUndo()
        {
            var container = GetCurrentActionContainer();
            if (container == null) return;
            _movementUndoStack.Push(new MovementEditorUndoState
            {
                Snapshot = CloneCommandList(container.commands),
                ActionIndex = _selectedActionIndex
            });
            _movementRedoStack.Clear();
        }

        private void MovementNotifySessionUpdated(bool persistToDisk = true, bool suppressUnusedWarnings = true)
        {
            if (_movementEditorScriptFile?.fileID < 0 || _parent == null) return;
            if (_movementEditorScriptFile.parseFailedDueToInvalidCommand) return;

            if (persistToDisk)
            {
                bool previous = ScriptFile.SuppressUnusedReferenceWarnings;
                ScriptFile.SuppressUnusedReferenceWarnings = suppressUnusedWarnings;
                try
                {
                    _movementEditorScriptFile.SaveToFileDefaultDir(_movementEditorScriptFile.fileID, false);
                }
                finally
                {
                    ScriptFile.SuppressUnusedReferenceWarnings = previous;
                }
            }
            _parent.NotifyScriptFileSessionUpdated(_movementEditorScriptFile.fileID);
        }

        public void PrimeMovementEditorContext()
        {
            if (movementEditorTabPage == null)
                return;
            PopulateMovementEditorScriptFileComboBox();
            SetMovementScriptFileFromCurrentEventHeader();
            PopulateMovementOverworldIdComboBox();
            SyncMovementOverworldSelectionFromSelectedEvent();
            SyncMovementAnchorUiState();
            ComputeMovementPreviewPath();
        }

        private void MovementSaveButton_Click(object sender, EventArgs e)
        {
            var container = GetCurrentActionContainer();
            if (container == null) return;
            var rawLines = container.commands.Select(c => c.name ?? string.Empty).ToArray();
            string actionText = "Action " + (_selectedActionIndex + 1) + ":" + Environment.NewLine +
                               string.Join(Environment.NewLine, rawLines.Select(x => " " + x));
            string rawText = string.Join(Environment.NewLine, rawLines);

            using (var dlg = new Form
            {
                Text = "Confirm Movement Action",
                Width = 680,
                Height = 440,
                StartPosition = FormStartPosition.CenterParent
            })
            {
                var host = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(8) };
                host.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                host.RowStyles.Add(new RowStyle(SizeType.Percent, 55f));
                host.RowStyles.Add(new RowStyle(SizeType.Percent, 45f));
                host.RowStyles.Add(new RowStyle(SizeType.AutoSize));

                var info = new Label
                {
                    AutoSize = true,
                    Text = "Review the action output below. Confirming copies it for manual use and does not apply changes to the Script File."
                };

                var actionBox = new TextBox
                {
                    Multiline = true,
                    Dock = DockStyle.Fill,
                    ReadOnly = true,
                    ScrollBars = ScrollBars.Both,
                    Font = new Font("Consolas", 9f),
                    Text = actionText
                };
                var rawBox = new TextBox
                {
                    Multiline = true,
                    Dock = DockStyle.Fill,
                    ReadOnly = true,
                    ScrollBars = ScrollBars.Both,
                    Font = new Font("Consolas", 9f),
                    Text = rawText
                };
                var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
                var applyButton = new Button { Text = "Copy", DialogResult = DialogResult.OK, AutoSize = true };
                var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
                buttons.Controls.Add(applyButton);
                buttons.Controls.Add(cancelButton);

                host.Controls.Add(info, 0, 0);
                host.Controls.Add(actionBox, 0, 1);
                host.Controls.Add(rawBox, 0, 2);
                host.Controls.Add(buttons, 0, 3);
                dlg.Controls.Add(host);
                dlg.AcceptButton = applyButton;
                dlg.CancelButton = cancelButton;

                if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
                {
                    try
                    {
                        Clipboard.SetText(actionText);
                    }
                    catch { }
                    MessageBox.Show($"Action {_selectedActionIndex + 1} copied to clipboard. No Script File changes were applied.",
                        "Action Copied", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void MovementExportButton_Click(object sender, EventArgs e)
        {
            var container = GetCurrentActionContainer();
            if (container == null) return;
            var lines = container.commands.Select(c =>
            {
                string rep = c.id == MovementEndCommandId ? "" : " 0x" + (c.repetitionCount ?? 1).ToString("X");
                return (c.name ?? "") + rep;
            }).ToArray();
            string actionText = "Action " + (_selectedActionIndex + 1) + ":" + Environment.NewLine +
                               string.Join(Environment.NewLine, lines.Select(x => " " + x));
            if (!PersistSelectedActionToScriptFile(suppressUnusedWarnings: false))
                return;
            ClearMovementPendingEdits();
            try
            {
                Clipboard.SetText(actionText);
            }
            catch { }
            using (var dlg = new Form { Text = "Export Action", Size = new Size(400, 300), StartPosition = FormStartPosition.CenterParent })
            {
                var txt = new TextBox { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, Font = new Font("Consolas", 9f), Text = actionText };
                var copyBtn = new Button { Text = "Copy", Dock = DockStyle.Bottom };
                copyBtn.Click += (s, ev) => { try { Clipboard.SetText(actionText); } catch { } };
                dlg.Controls.Add(txt);
                dlg.Controls.Add(copyBtn);
                dlg.ShowDialog(FindForm());
            }
        }

        private bool PersistSelectedActionToScriptFile(bool suppressUnusedWarnings)
        {
            if (_movementEditorScriptFile == null || _selectedActionIndex < 0 || _parent == null)
                return false;

            int scriptFileId = _movementEditorScriptFile.fileID;
            var sourceAction = GetCurrentActionContainer();
            if (sourceAction == null)
                return false;

            var persistedScript = _parent.GetOrLoadScriptFile(scriptFileId);
            if (persistedScript == null)
                return false;
            if (_selectedActionIndex >= persistedScript.allActions.Count)
            {
                MessageBox.Show(
                    $"Action {_selectedActionIndex + 1} does not exist in Script File {scriptFileId} on disk.\n\n" +
                    "Create this Action in the script first before exporting from the Movement Editor.",
                    "Export Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return false;
            }

            var clonedCommands = CloneCommandList(sourceAction.commands);
            var replacement = new ScriptActionContainer(
                persistedScript.allActions[_selectedActionIndex].manualUserID,
                clonedCommands);
            EnsureEndExists(replacement);
            persistedScript.allActions[_selectedActionIndex] = replacement;

            bool previous = ScriptFile.SuppressUnusedReferenceWarnings;
            ScriptFile.SuppressUnusedReferenceWarnings = suppressUnusedWarnings;
            try
            {
                persistedScript.SaveToFileDefaultDir(scriptFileId, false);
            }
            finally
            {
                ScriptFile.SuppressUnusedReferenceWarnings = previous;
            }

            _movementEditorScriptFile = persistedScript;
            _parent.NotifyScriptFileSessionUpdated(scriptFileId);
            RefreshMovementCommandList();
            return true;
        }

        #endregion

        #region Movement Editor event handlers

        private void MovementScriptFileComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (Helpers.HandlersDisabled || _parent == null || _suppressMovementSelectionEvents) return;
            if (!(movementScriptFileComboBox.SelectedItem is MovementScriptFileEntry entry))
            {
                _pendingScriptSelectionId = -1;
                return;
            }
            _pendingScriptSelectionId = entry.ScriptFileId;
        }

        private void MovementActionComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (Helpers.HandlersDisabled || _suppressMovementSelectionEvents) return;
            _pendingActionSelectionIndex = movementActionComboBox.SelectedIndex;
        }

        private void MovementApplyScriptSelection_Click(object sender, EventArgs e)
        {
            if (_pendingScriptSelectionId < 0)
                return;
            if (!ConfirmDiscardPendingMovementEdits($"Script File {_pendingScriptSelectionId}"))
                return;
            ApplyMovementScriptSelection(_pendingScriptSelectionId, keepActionSelection: false);
        }

        private void ApplyMovementScriptSelection(int scriptFileId, bool keepActionSelection)
        {
            if (_parent == null || scriptFileId < 0)
                return;
            _movementEditorScriptFile = _parent.GetOrLoadScriptFile(scriptFileId);
            _pendingScriptSelectionId = scriptFileId;

            PopulateMovementActionComboBox();
            int desiredAction = keepActionSelection ? _pendingActionSelectionIndex : 0;
            if (_movementEditorScriptFile.allActions.Count == 0)
                desiredAction = -1;
            else if (desiredAction < 0 || desiredAction >= _movementEditorScriptFile.allActions.Count)
                desiredAction = 0;
            _selectedActionIndex = desiredAction;
            _pendingActionSelectionIndex = desiredAction;

            _suppressMovementSelectionEvents = true;
            try
            {
                if (movementActionComboBox != null)
                    movementActionComboBox.SelectedIndex = desiredAction;
            }
            finally
            {
                _suppressMovementSelectionEvents = false;
            }

            ClearMovementPendingEdits();
            RefreshMovementCommandList();
        }

        private void MovementApplyActionSelection_Click(object sender, EventArgs e)
        {
            if (_movementEditorScriptFile == null)
                return;
            int idx = _pendingActionSelectionIndex;
            if (idx < 0 || idx >= _movementEditorScriptFile.allActions.Count)
                idx = _movementEditorScriptFile.allActions.Count > 0 ? 0 : -1;
            _selectedActionIndex = idx;
            _pendingActionSelectionIndex = idx;
            ClearMovementPendingEdits();
            RefreshMovementCommandList();
            DisplayActiveEvents();
        }

        private void MovementNewActionButton_Click(object sender, EventArgs e)
        {
            if (_movementEditorScriptFile == null) return;
            var newContainer = new ScriptActionContainer((uint)(_movementEditorScriptFile.allActions.Count + 1),
                new List<ScriptAction> { new ScriptAction(MovementEndCommandId, 0) });
            _movementEditorScriptFile.allActions.Add(newContainer);
            PopulateMovementActionComboBox();
            _selectedActionIndex = _movementEditorScriptFile.allActions.Count - 1;
            _pendingActionSelectionIndex = _selectedActionIndex;
            movementActionComboBox.SelectedIndex = _selectedActionIndex;
            PushMovementUndo();
            MarkMovementPendingEdits();
            MarkMovementPendingEdits();
            if (OverworldPointerCheckBox != null) OverworldPointerCheckBox.Checked = false;
            if (PlaceholderPointerCheckBox != null) PlaceholderPointerCheckBox.Checked = false;
            SyncMovementAnchorUiState();
            RefreshMovementCommandList();
        }

        private void MovementDuplicateActionButton_Click(object sender, EventArgs e)
        {
            var container = GetCurrentActionContainer();
            if (container == null || _movementEditorScriptFile == null) return;
            var cloned = CloneCommandList(container.commands);
            EnsureEndExists(new ScriptActionContainer(0, cloned));
            var newContainer = new ScriptActionContainer((uint)(_movementEditorScriptFile.allActions.Count + 1), cloned);
            _movementEditorScriptFile.allActions.Add(newContainer);
            PopulateMovementActionComboBox();
            _selectedActionIndex = _movementEditorScriptFile.allActions.Count - 1;
            _pendingActionSelectionIndex = _selectedActionIndex;
            movementActionComboBox.SelectedIndex = _selectedActionIndex;
            PushMovementUndo();
            MarkMovementPendingEdits();
            MarkMovementPendingEdits();
            RefreshMovementCommandList();
        }

        private void MovementDeleteActionButton_Click(object sender, EventArgs e)
        {
            if (_movementEditorScriptFile == null || _movementEditorScriptFile.allActions.Count <= 0) return;
            if (_selectedActionIndex < 0) return;
            _movementEditorScriptFile.allActions.RemoveAt(_selectedActionIndex);
            for (uint i = 0; i < _movementEditorScriptFile.allActions.Count; i++)
                _movementEditorScriptFile.allActions[(int)i].manualUserID = i + 1;
            PopulateMovementActionComboBox();
            _selectedActionIndex = _movementEditorScriptFile.allActions.Count == 0
                ? -1
                : Math.Min(_selectedActionIndex, _movementEditorScriptFile.allActions.Count - 1);
            _pendingActionSelectionIndex = _selectedActionIndex;
            _suppressMovementSelectionEvents = true;
            try
            {
                movementActionComboBox.SelectedIndex = _selectedActionIndex;
            }
            finally
            {
                _suppressMovementSelectionEvents = false;
            }
            MarkMovementPendingEdits();
            RefreshMovementCommandList();
        }

        private void PopulateMovementActionComboBox()
        {
            movementActionComboBox.Items.Clear();
            if (_movementEditorScriptFile == null) return;
            for (int i = 0; i < _movementEditorScriptFile.allActions.Count; i++)
                movementActionComboBox.Items.Add($"Action {i + 1}");
        }

        private void MovementDirectionPad_Click(string direction)
        {
            var container = GetCurrentActionContainer();
            if (container == null) return;
            string type = movementSetComboBox?.SelectedItem?.ToString();
            string style = movementTypeComboBox?.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(type)) type = "Walk";
            if (string.IsNullOrEmpty(style)) style = "Normal";
            bool onSpot = DPadOnSpotCheckBoxButton?.Checked ?? false;
            ushort? cmdId = ResolveMovementCommandId(type, style, direction, onSpot);
            if (cmdId == null) return;
            MovementInsertCommand(cmdId.Value, 1);
        }

        private void MovementEmoteButton_Click(string emoteName)
        {
            var container = GetCurrentActionContainer();
            if (container == null) return;
            string normalized = NormalizeCommandName(emoteName);
            if (!_movementCommandLookup.TryGetValue(normalized, out ushort cmdId))
                return;
            MovementInsertCommand(cmdId, 1);
        }

        private int GetMovementInsertionIndex(ScriptActionContainer container)
        {
            if (container == null) return 0;
            int endIndex = container.commands.Count;
            if (container.commands.Count > 0 && container.commands[container.commands.Count - 1].id == MovementEndCommandId)
                endIndex = container.commands.Count - 1;

            if (movementCommandListView.SelectedIndices.Count <= 0)
                return endIndex;

            int selectedIndex = movementCommandListView.SelectedIndices[0];
            if (selectedIndex < 0) return endIndex;
            if (selectedIndex >= endIndex) return endIndex;
            return selectedIndex;
        }

        private int GetMovementInsertionIndexBelowSelected(ScriptActionContainer container)
        {
            if (container == null) return 0;
            int endIndex = container.commands.Count;
            if (container.commands.Count > 0 && container.commands[container.commands.Count - 1].id == MovementEndCommandId)
                endIndex = container.commands.Count - 1;
            if (movementCommandListView.SelectedIndices.Count <= 0)
                return endIndex;
            int selectedIndex = movementCommandListView.SelectedIndices[0];
            if (selectedIndex < 0) return endIndex;
            if (selectedIndex >= endIndex) return endIndex;
            return selectedIndex + 1;
        }

        private void ClearMovementCommandSelection()
        {
            if (movementCommandListView == null || movementCommandListView.SelectedIndices.Count == 0)
                return;
            foreach (ListViewItem item in movementCommandListView.Items)
                item.Selected = false;
            _previewSelectedCommandRows.Clear();
        }

        private void IncreaseMovementCommandRepetition(ScriptAction command, ushort deltaRep)
        {
            if (command == null || !command.id.HasValue || command.id == MovementEndCommandId)
                return;
            int current = command.repetitionCount ?? 1;
            int updated = Math.Min(ushort.MaxValue, current + Math.Max(1, (int)deltaRep));
            command.repetitionCount = (ushort)updated;
            string baseName = command.id.Value.ToString("X4");
            if (ScriptActionNamesDict != null && ScriptActionNamesDict.TryGetValue(command.id.Value, out string n))
                baseName = n;
            else if (_movementCommandNameById.TryGetValue(command.id.Value, out string n2))
                baseName = n2;
            command.name = baseName + " 0x" + command.repetitionCount.Value.ToString("X");
        }

        private ushort? ResolveMovementCommandId(string movementType, string styleLabel, string direction, bool onSpot)
        {
            string type = movementType?.Trim() ?? string.Empty;
            string dir = direction?.Trim() ?? string.Empty;
            int styleValue = MovementStyleOptions.FirstOrDefault(x => string.Equals(x.Label, styleLabel, StringComparison.OrdinalIgnoreCase)).SpeedValue;
            if (styleValue <= 0) styleValue = 8;

            if (string.Equals(type, "JumpVeryFar", StringComparison.OrdinalIgnoreCase) &&
                !(string.Equals(dir, "East", StringComparison.OrdinalIgnoreCase) || string.Equals(dir, "West", StringComparison.OrdinalIgnoreCase)))
                return null;

            var candidates = new List<string>();
            if (string.Equals(type, "Face", StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add($"Face{dir}");
            }
            else if (string.Equals(type, "Delay", StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add($"Delay{styleValue}");
            }
            else if (string.Equals(type, "Walk", StringComparison.OrdinalIgnoreCase))
            {
                if (onSpot)
                    candidates.Add($"WalkOnSpot{dir}{styleValue}");
                candidates.Add($"Walk{dir}{styleValue}");
            }
            else if (string.Equals(type, "Jump", StringComparison.OrdinalIgnoreCase))
            {
                if (onSpot)
                    candidates.Add($"JumpOnSpot{dir}{styleValue}");
                candidates.Add($"Jump{dir}{styleValue}");
            }
            else if (string.Equals(type, "JumpFar", StringComparison.OrdinalIgnoreCase))
            {
                if (onSpot)
                    candidates.Add($"JumpOnSpot{dir}8");
                candidates.Add($"JumpFar{dir}");
            }
            else if (string.Equals(type, "JumpVeryFar", StringComparison.OrdinalIgnoreCase))
            {
                if (onSpot)
                    candidates.Add($"JumpOnSpot{dir}8");
                candidates.Add($"JumpVeryFar{dir}");
            }

            foreach (string candidate in candidates)
            {
                string normalized = NormalizeCommandName(candidate);
                if (_movementCommandLookup.TryGetValue(normalized, out ushort id))
                    return id;
                if (RomInfo.ScriptActionNamesReverseDict != null &&
                    RomInfo.ScriptActionNamesReverseDict.TryGetValue(candidate.ToLowerInvariant(), out ushort id2))
                    return id2;
            }
            return null;
        }

        private void MovementCommandListView_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            e.NewValue = CheckState.Unchecked;
        }

        private void MovementCommandListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            _previewSelectedCommandRows.Clear();
            foreach (ListViewItem item in movementCommandListView.SelectedItems)
            {
                if (item.Tag is int idx)
                    _previewSelectedCommandRows.Add(idx);
            }
            DisplayActiveEvents();
        }

        private void MovementAnchorModeCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (_suppressMovementSelectionEvents)
                return;
            _suppressMovementSelectionEvents = true;
            try
            {
                if (ReferenceEquals(sender, OverworldPointerCheckBox) && OverworldPointerCheckBox.Checked)
                    PlaceholderPointerCheckBox.Checked = false;
                else if (ReferenceEquals(sender, PlaceholderPointerCheckBox) && PlaceholderPointerCheckBox.Checked)
                    OverworldPointerCheckBox.Checked = false;
            }
            finally
            {
                _suppressMovementSelectionEvents = false;
            }
            SyncMovementAnchorUiState();
            ComputeMovementPreviewPath();
            DisplayActiveEvents();
        }

        private void MovementPointerValue_ValueChanged(object sender, EventArgs e)
        {
            if (_suppressMovementSelectionEvents)
                return;
            ComputeMovementPreviewPath();
            DisplayActiveEvents();
        }

        private void SyncMovementAnchorUiState()
        {
            bool useOverworld = OverworldPointerCheckBox?.Checked ?? false;
            bool usePlaceholder = PlaceholderPointerCheckBox?.Checked ?? false;

            if (PointerXUpDown != null) PointerXUpDown.Enabled = usePlaceholder && !useOverworld;
            if (PointerYUpDown != null) PointerYUpDown.Enabled = usePlaceholder && !useOverworld;

            if (useOverworld)
            {
                var owAnchor = GetOverworldAnchorSafe();
                if (owAnchor.HasValue)
                {
                    _suppressMovementSelectionEvents = true;
                    try
                    {
                        if (PointerXUpDown != null) PointerXUpDown.Value = ClampNumericUpDown(PointerXUpDown, owAnchor.Value.x);
                        if (PointerYUpDown != null) PointerYUpDown.Value = ClampNumericUpDown(PointerYUpDown, owAnchor.Value.y);
                    }
                    finally
                    {
                        _suppressMovementSelectionEvents = false;
                    }
                }
            }
        }

        private decimal ClampNumericUpDown(NumericUpDown nud, int value)
        {
            decimal dec = value;
            if (dec < nud.Minimum) return nud.Minimum;
            if (dec > nud.Maximum) return nud.Maximum;
            return dec;
        }

        private (int x, int y)? GetOverworldAnchorSafe()
        {
            if (currentEvFile?.overworlds == null || movementOverworldIdComboBox == null)
                return null;
            int idx = movementOverworldIdComboBox.SelectedIndex;
            if (idx < 0 || idx >= currentEvFile.overworlds.Count)
                return null;
            var ow = currentEvFile.overworlds[idx];
            return ow == null ? ((int x, int y)?)null : (ow.xMapPosition, ow.yMapPosition);
        }

        private void MovementInsertNamedCommand(string commandName, ushort repetition = 1)
        {
            if (string.IsNullOrWhiteSpace(commandName))
                return;
            if (!_movementCommandLookup.TryGetValue(NormalizeCommandName(commandName), out ushort cmdId))
                return;
            MovementInsertCommand(cmdId, repetition);
        }

        private void MovementCommandListView_DoubleClick(object sender, EventArgs e)
        {
            var container = GetCurrentActionContainer();
            if (container == null) return;
            if (movementCommandListView.SelectedItems.Count == 0) return;
            var item = movementCommandListView.SelectedItems[0];
            if (item.Tag is int idx && idx >= 0 && idx < container.commands.Count)
            {
                var cmd = container.commands[idx];
                if (cmd.id == MovementEndCommandId) return;
                ShowEditMovementCommandDialog(container, idx, cmd);
            }
        }

        private void ShowEditMovementCommandDialog(ScriptActionContainer container, int index, ScriptAction cmd)
        {
            var commands = GetAllMovementCommandsForQuickInsert();
            using (var dlg = new Form { Text = "Edit step", Size = new Size(320, 140), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog })
            {
                var repLabel = new Label { Text = "Repetition:", Location = new Point(12, 14), AutoSize = true };
                var repNud = new NumericUpDown { Minimum = 1, Maximum = 65535, Value = Math.Max(1, (int)(cmd.repetitionCount ?? 1)), Location = new Point(100, 12), Width = 80 };
                var cmdLabel = new Label { Text = "Command:", Location = new Point(12, 44), AutoSize = true };
                var cmdCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(100, 42), Width = 190 };
                foreach (var kvp in commands)
                    cmdCombo.Items.Add(new MovementCommandItem(kvp.Key, kvp.Value));
                ushort? currentId = cmd.id;
                for (int i = 0; i < cmdCombo.Items.Count; i++)
                {
                    if (cmdCombo.Items[i] is MovementCommandItem mi && mi.Id == currentId)
                    { cmdCombo.SelectedIndex = i; break; }
                }
                if (cmdCombo.SelectedIndex < 0 && cmdCombo.Items.Count > 0) cmdCombo.SelectedIndex = 0;
                var okBtn = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(120, 78), Width = 75 };
                var cancelBtn = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(205, 78), Width = 75 };
                dlg.Controls.Add(repLabel);
                dlg.Controls.Add(repNud);
                dlg.Controls.Add(cmdLabel);
                dlg.Controls.Add(cmdCombo);
                dlg.Controls.Add(okBtn);
                dlg.Controls.Add(cancelBtn);
                dlg.AcceptButton = okBtn;
                dlg.CancelButton = cancelBtn;
                if (dlg.ShowDialog(FindForm()) == DialogResult.OK && cmdCombo.SelectedItem is MovementCommandItem selected)
                {
                    PushMovementUndo();
                    container.commands[index] = new ScriptAction(selected.Id, (ushort)repNud.Value);
                    MarkMovementPendingEdits();
                    RefreshMovementCommandList();
                }
            }
        }

        private void MovementMoveUpButton_Click(object sender, EventArgs e)
        {
            var container = GetCurrentActionContainer();
            if (container == null) return;
            var checkedIndices = GetCheckedCommandIndices(container);
            if (checkedIndices.Count > 1)
            {
                if (checkedIndices[0] <= 0) return;
                PushMovementUndo();
                var block = checkedIndices.Select(i => container.commands[i]).ToList();
                foreach (int i in checkedIndices.OrderByDescending(x => x))
                    container.commands.RemoveAt(i);
                int insertAt = checkedIndices.Min() - 1;
                for (int i = 0; i < block.Count; i++)
                    container.commands.Insert(insertAt + i, block[i]);
                MarkMovementPendingEdits();
                RefreshMovementCommandList();
                return;
            }
            int sel = checkedIndices.Count == 1 ? checkedIndices[0] : (movementCommandListView.SelectedIndices.Count > 0 ? movementCommandListView.SelectedIndices[0] : -1);
            if (sel <= 0 || sel >= container.commands.Count) return;
            if (container.commands[sel].id == MovementEndCommandId) return;
            PushMovementUndo();
            var tmp = container.commands[sel];
            container.commands.RemoveAt(sel);
            container.commands.Insert(sel - 1, tmp);
            MarkMovementPendingEdits();
            RefreshMovementCommandList();
            if (sel - 1 < movementCommandListView.Items.Count)
                movementCommandListView.Items[sel - 1].Selected = true;
        }

        private void MovementMoveDownButton_Click(object sender, EventArgs e)
        {
            var container = GetCurrentActionContainer();
            if (container == null) return;
            var checkedIndices = GetCheckedCommandIndices(container);
            if (checkedIndices.Count > 1)
            {
                int endRow = container.commands.Count - 1;
                if (container.commands[endRow].id == MovementEndCommandId) endRow--;
                if (checkedIndices.Max() >= endRow) return;
                PushMovementUndo();
                var block = checkedIndices.Select(i => container.commands[i]).ToList();
                foreach (int i in checkedIndices.OrderByDescending(x => x))
                    container.commands.RemoveAt(i);
                int insertAt = checkedIndices.Min() + 1;
                for (int i = 0; i < block.Count; i++)
                    container.commands.Insert(insertAt + i, block[i]);
                MarkMovementPendingEdits();
                RefreshMovementCommandList();
                return;
            }
            int sel = checkedIndices.Count == 1 ? checkedIndices[0] : (movementCommandListView.SelectedIndices.Count > 0 ? movementCommandListView.SelectedIndices[0] : -1);
            if (sel < 0 || sel >= container.commands.Count - 1) return;
            if (container.commands[sel].id == MovementEndCommandId) return;
            PushMovementUndo();
            var tmp = container.commands[sel];
            container.commands.RemoveAt(sel);
            container.commands.Insert(sel + 1, tmp);
            MarkMovementPendingEdits();
            RefreshMovementCommandList();
            if (sel + 1 < movementCommandListView.Items.Count)
                movementCommandListView.Items[sel + 1].Selected = true;
        }

        private List<int> GetCheckedCommandIndices(ScriptActionContainer container)
        {
            var list = new List<int>();
            foreach (ListViewItem lvItem in movementCommandListView.SelectedItems)
            {
                if (!(lvItem.Tag is int idx) || idx < 0 || idx >= container.commands.Count) continue;
                if (container.commands[idx].id == MovementEndCommandId) continue;
                list.Add(idx);
            }
            list.Sort();
            return list;
        }

        private void MovementInsertButton_Click(object sender, EventArgs e)
        {
            // kept hidden; no quick-action insert source in compact layout
        }

        private void MovementInsertCommand(ushort commandId, ushort rep, bool allowEnd = false)
        {
            var container = GetCurrentActionContainer();
            if (container == null) return;
            if (commandId == MovementEndCommandId && !allowEnd) return;
            PushMovementUndo();
            int insertIndex = GetMovementInsertionIndex(container);
            bool mergedWithPrevious = false;
            if (insertIndex > 0)
            {
                var prev = container.commands[insertIndex - 1];
                if (prev.id == commandId)
                {
                    IncreaseMovementCommandRepetition(prev, rep);
                    mergedWithPrevious = true;
                }
            }
            if (!mergedWithPrevious)
                container.commands.Insert(insertIndex, new ScriptAction(commandId, rep));
            MarkMovementPendingEdits();
            // A selected-row insertion is one-shot; next button insertion returns to default append-at-bottom.
            ClearMovementCommandSelection();
            RefreshMovementCommandList();
        }

        private void MovementDeleteSelectedButton_Click(object sender, EventArgs e)
        {
            var container = GetCurrentActionContainer();
            if (container == null) return;
            var toRemove = new List<int>();
            foreach (ListViewItem lvItem in movementCommandListView.SelectedItems)
            {
                if (!(lvItem.Tag is int idx) || idx < 0 || idx >= container.commands.Count) continue;
                if (container.commands[idx].id == MovementEndCommandId) continue;
                toRemove.Add(idx);
            }
            if (toRemove.Count == 0) return;
            toRemove.Sort((a, b) => b.CompareTo(a));
            PushMovementUndo();
            foreach (int idx in toRemove)
                container.commands.RemoveAt(idx);
            MarkMovementPendingEdits();
            RefreshMovementCommandList();
        }

        private void MovementDeleteButton_Click(object sender, EventArgs e)
        {
            var container = GetCurrentActionContainer();
            if (container == null) return;
            int sel = movementCommandListView.SelectedIndices.Count > 0 ? movementCommandListView.SelectedIndices[0] : -1;
            if (sel < 0 || sel >= container.commands.Count) return;
            if (container.commands[sel].id == MovementEndCommandId) return;
            PushMovementUndo();
            container.commands.RemoveAt(sel);
            MarkMovementPendingEdits();
            RefreshMovementCommandList();
        }

        private void MovementClearButton_Click(object sender, EventArgs e)
        {
            var container = GetCurrentActionContainer();
            if (container == null) return;
            var remove = GetCheckedCommandIndices(container);
            if (remove.Count == 0)
                remove = Enumerable.Range(0, container.commands.Count).Where(i => container.commands[i].id != MovementEndCommandId).ToList();
            if (remove.Count == 0) return;
            PushMovementUndo();
            foreach (int idx in remove.OrderByDescending(x => x))
                container.commands.RemoveAt(idx);
            EnsureEndExists(container);
            MarkMovementPendingEdits();
            RefreshMovementCommandList();
        }

        private void MovementUndoButton_Click(object sender, EventArgs e)
        {
            if (_movementUndoStack.Count == 0) return;
            var container = GetCurrentActionContainer();
            if (container == null) return;
            var state = _movementUndoStack.Pop();
            _movementRedoStack.Push(new MovementEditorUndoState { Snapshot = CloneCommandList(container.commands), ActionIndex = _selectedActionIndex });
            container.commands.Clear();
            container.commands.AddRange(state.Snapshot);
            MarkMovementPendingEdits();
            RefreshMovementCommandList();
        }

        private void MovementRedoButton_Click(object sender, EventArgs e)
        {
            if (_movementRedoStack.Count == 0) return;
            var container = GetCurrentActionContainer();
            if (container == null) return;
            var state = _movementRedoStack.Pop();
            _movementUndoStack.Push(new MovementEditorUndoState { Snapshot = CloneCommandList(container.commands), ActionIndex = _selectedActionIndex });
            container.commands.Clear();
            container.commands.AddRange(state.Snapshot);
            MarkMovementPendingEdits();
            RefreshMovementCommandList();
        }

        private void MovementCopyButton_Click(object sender, EventArgs e)
        {
            var container = GetCurrentActionContainer();
            if (container == null) return;
            var sb = new System.Text.StringBuilder();
            var checkedIndices = GetCheckedCommandIndices(container);
            if (checkedIndices.Count > 0)
            {
                foreach (int i in checkedIndices.OrderBy(x => x))
                    sb.AppendLine(container.commands[i].name ?? "");
            }
            else
            {
                bool stepsOnly = movementCopyStepsOnlyRadio?.Checked ?? true;
                foreach (var c in container.commands)
                {
                    if (c.id == MovementEndCommandId) { if (!stepsOnly) sb.AppendLine(c.name); break; }
                    sb.AppendLine(c.name ?? "");
                }
            }
            string text = sb.ToString().TrimEnd();
            if (string.IsNullOrWhiteSpace(text)) return;
            Clipboard.SetText(text);
            Helpers.statusLabelMessage("Movement commands copied to clipboard.");
        }

        private void MovementPasteButton_Click(object sender, EventArgs e)
        {
            var container = GetCurrentActionContainer();
            if (container == null) return;
            string text = null;
            try { text = Clipboard.GetText(); } catch { }
            if (string.IsNullOrWhiteSpace(text)) return;
            PushMovementUndo();
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            int insertIndex = GetMovementInsertionIndexBelowSelected(container);
            foreach (var line in lines)
            {
                var sa = TryParseMovementLine(line.Trim());
                if (sa == null || sa.id == MovementEndCommandId) continue;
                container.commands.Insert(insertIndex++, sa);
            }
            EnsureEndExists(container);
            MarkMovementPendingEdits();
            RefreshMovementCommandList();
        }

        private static ScriptAction TryParseMovementLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return null;
            var parts = line.Replace("\t", "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return null;
            if (!RomInfo.ScriptActionNamesReverseDict.TryGetValue(parts[0].ToLowerInvariant(), out ushort id))
            {
                if (ushort.TryParse(parts[0], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out ushort buf))
                    id = buf;
                else
                    return null;
            }
            ushort? rep = null;
            if (id != MovementEndCommandId && parts.Length >= 2)
            {
                string r = parts[1];
                if (r.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) r = r.Substring(2);
                if (ushort.TryParse(r, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out ushort rv))
                    rep = rv;
                else if (ushort.TryParse(parts[1], out ushort rv2))
                    rep = rv2;
            }
            if (id == MovementEndCommandId) rep = 0;
            if (rep == null && id != MovementEndCommandId) rep = 1;
            return new ScriptAction(id, rep ?? 0);
        }

        private void MovementPreviewCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            DisplayActiveEvents();
        }

        private void SetMovementScriptFileFromCurrentEventHeader()
        {
            if (_parent == null || currentEvFile == null || selectEventComboBox == null) return;
            ushort headerID;
            if (_preferredMovementHeaderId.HasValue)
            {
                headerID = _preferredMovementHeaderId.Value;
                MapHeader preferred = LoadHeaderById(headerID);
                if (preferred == null || preferred.eventFileID != selectEventComboBox.SelectedIndex)
                {
                    _preferredMovementHeaderId = null;
                    if (!_parent.eventToHeader.TryGetValue((ushort)selectEventComboBox.SelectedIndex, out headerID))
                        return;
                }
            }
            else if (!_parent.eventToHeader.TryGetValue((ushort)selectEventComboBox.SelectedIndex, out headerID))
                return;
            MapHeader header;
            try
            {
                if (PatchToolboxDialog.flag_DynamicHeadersPatchApplied || PatchToolboxDialog.CheckFilesDynamicHeadersPatchApplied())
                    header = MapHeader.LoadFromFile(RomInfo.gameDirs[DirNames.dynamicHeaders].unpackedDir + "\\" + headerID.ToString("D4"), headerID, 0);
                else
                    header = MapHeader.LoadFromARM9(headerID);
            }
            catch { return; }
            int scriptFileID = header.scriptFileID;
            int count = Filesystem.GetScriptCount();
            if (scriptFileID < 0 || scriptFileID >= count) return;
            for (int i = 0; i < movementScriptFileComboBox.Items.Count; i++)
            {
                if (movementScriptFileComboBox.Items[i] is MovementScriptFileEntry entry && entry.ScriptFileId == scriptFileID)
                {
                    _suppressMovementSelectionEvents = true;
                    try
                    {
                        if (movementScriptFileComboBox.SelectedIndex != i)
                            movementScriptFileComboBox.SelectedIndex = i;
                    }
                    finally
                    {
                        _suppressMovementSelectionEvents = false;
                    }
                    _pendingScriptSelectionId = scriptFileID;
                    ApplyMovementScriptSelection(scriptFileID, keepActionSelection: false);
                    break;
                }
            }
        }

        private void SyncMovementOverworldSelectionFromSelectedEvent()
        {
            if (movementOverworldIdComboBox == null || currentEvFile?.overworlds == null) return;
            if (selectedEvent is Overworld ow)
            {
                int idx = currentEvFile.overworlds.IndexOf(ow);
                if (idx >= 0 && idx < movementOverworldIdComboBox.Items.Count && movementOverworldIdComboBox.SelectedIndex != idx)
                    movementOverworldIdComboBox.SelectedIndex = idx;
            }
        }

        private void MovementOverworldIdComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (currentEvFile?.overworlds != null && movementOverworldIdComboBox.SelectedIndex >= 0 &&
                movementOverworldIdComboBox.SelectedIndex < currentEvFile.overworlds.Count &&
                overworldsListBox != null)
            {
                int idx = movementOverworldIdComboBox.SelectedIndex;
                selectedEvent = currentEvFile.overworlds[idx];
                overworldsListBox.SelectedIndex = idx;
            }
            SyncMovementAnchorUiState();
            ComputeMovementPreviewPath();
            DisplayActiveEvents();
        }

        private ((int x, int y)? anchor, string source, int owIndex) GetMovementPreviewAnchorWithSource()
        {
            bool useOverworld = OverworldPointerCheckBox?.Checked ?? false;
            bool usePlaceholder = PlaceholderPointerCheckBox?.Checked ?? false;

            if (useOverworld && movementOverworldIdComboBox != null && movementOverworldIdComboBox.SelectedIndex >= 0 && currentEvFile?.overworlds != null)
            {
                int idx = movementOverworldIdComboBox.SelectedIndex;
                if (idx < currentEvFile.overworlds.Count)
                {
                    var ow = currentEvFile.overworlds[idx];
                    if (ow != null && isEventOnCurrentMatrix(ow))
                        return ((ow.xMapPosition, ow.yMapPosition), "ow", idx);
                }
            }
            if (usePlaceholder && PointerXUpDown != null && PointerYUpDown != null)
                return (((int)PointerXUpDown.Value, (int)PointerYUpDown.Value), "placeholder", -1);
            if (useOverworld)
            {
                // Overworld mode is explicit. If invalid/out-of-range, fallback safely to selected event.
                if (selectedEvent is Overworld fallbackOw && isEventOnCurrentMatrix(fallbackOw))
                {
                    int idx = currentEvFile?.overworlds?.IndexOf(fallbackOw) ?? -1;
                    return ((fallbackOw.xMapPosition, fallbackOw.yMapPosition), "ow", idx);
                }
                return (null, null, -1);
            }
            if (selectedEvent is Overworld ow2 && isEventOnCurrentMatrix(ow2))
            {
                int idx = currentEvFile?.overworlds?.IndexOf(ow2) ?? -1;
                return ((ow2.xMapPosition, ow2.yMapPosition), "ow", idx);
            }
            return (null, null, -1);
        }

        private (int x, int y)? GetMovementPreviewAnchor()
        {
            var (anchor, _, _) = GetMovementPreviewAnchorWithSource();
            return anchor;
        }

        private void SaveMovementEditorSessionState()
        {
            _sessionMovementSet = movementSetComboBox?.SelectedItem?.ToString();
            _sessionMovementType = movementTypeComboBox?.SelectedItem?.ToString();
            _sessionOnSpot = DPadOnSpotCheckBoxButton?.Checked ?? false;
            if (movementShowPathCheckBox != null) _sessionShowPath = movementShowPathCheckBox.Checked;
            if (movementShowGhostCheckBox != null) _sessionShowGhost = movementShowGhostCheckBox.Checked;
            if (movementShowMarkersCheckBox != null) _sessionShowMarkers = movementShowMarkersCheckBox.Checked;
            _sessionScriptFileIndex = (movementScriptFileComboBox?.SelectedItem as MovementScriptFileEntry)?.ScriptFileId ?? -1;
            _sessionActionIndex = movementActionComboBox?.SelectedIndex ?? -1;
        }

        private void RestoreMovementEditorSessionState()
        {
            if (movementSetComboBox != null && !string.IsNullOrEmpty(_sessionMovementSet))
            {
                int idx = movementSetComboBox.Items.IndexOf(_sessionMovementSet);
                if (idx < 0)
                {
                    for (int i = 0; i < movementSetComboBox.Items.Count; i++)
                    {
                        if (string.Equals(movementSetComboBox.Items[i].ToString(), _sessionMovementSet, StringComparison.OrdinalIgnoreCase))
                        { idx = i; break; }
                    }
                }
                if (idx >= 0) movementSetComboBox.SelectedIndex = idx;
            }
            if (movementTypeComboBox != null && !string.IsNullOrEmpty(_sessionMovementType))
            {
                int idx = movementTypeComboBox.Items.IndexOf(_sessionMovementType);
                if (idx < 0)
                {
                    for (int i = 0; i < movementTypeComboBox.Items.Count; i++)
                    {
                        if (string.Equals(movementTypeComboBox.Items[i].ToString(), _sessionMovementType, StringComparison.OrdinalIgnoreCase))
                        { idx = i; break; }
                    }
                }
                if (idx >= 0) movementTypeComboBox.SelectedIndex = idx;
            }
            if (DPadOnSpotCheckBoxButton != null) DPadOnSpotCheckBoxButton.Checked = _sessionOnSpot;
            if (movementShowPathCheckBox != null) movementShowPathCheckBox.Checked = _sessionShowPath;
            if (movementShowGhostCheckBox != null) movementShowGhostCheckBox.Checked = _sessionShowGhost;
            if (movementShowMarkersCheckBox != null) movementShowMarkersCheckBox.Checked = _sessionShowMarkers;
            if (movementScriptFileComboBox != null && _sessionScriptFileIndex >= 0)
            {
                for (int i = 0; i < movementScriptFileComboBox.Items.Count; i++)
                {
                    if (movementScriptFileComboBox.Items[i] is MovementScriptFileEntry entry && entry.ScriptFileId == _sessionScriptFileIndex)
                    {
                        movementScriptFileComboBox.SelectedIndex = i;
                        break;
                    }
                }
            }
            if (movementActionComboBox != null && _sessionActionIndex >= 0 && _sessionActionIndex < movementActionComboBox.Items.Count)
                movementActionComboBox.SelectedIndex = _sessionActionIndex;
        }

        private void ComputeMovementPreviewPath()
        {
            _previewPathTiles.Clear();
            _previewCommandMarkers.Clear();
            _previewPathSegments.Clear();
            var anchor = GetMovementPreviewAnchor();
            if (anchor == null) { _previewAnchorX = _previewAnchorY = null; return; }
            _previewAnchorX = anchor.Value.x;
            _previewAnchorY = anchor.Value.y;
            var container = GetCurrentActionContainer();
            if (container == null) return;
            var map = MovementDirectionDeltaMap.Get();
            int x = anchor.Value.x, y = anchor.Value.y;
            _previewPathTiles.Add((x, y));
            int commandIndex = 1;
            foreach (var cmd in container.commands)
            {
                if (cmd.id == MovementEndCommandId) break;
                int n = cmd.repetitionCount ?? 1;
                if (map.TryGetValue(cmd.id ?? 0, out (int dx, int dy) delta))
                {
                    for (int i = 0; i < n; i++)
                    {
                        int prevX = x;
                        int prevY = y;
                        x += delta.dx;
                        y += delta.dy;
                        _previewPathSegments.Add((prevX, prevY, x, y, commandIndex - 1));
                        _previewPathTiles.Add((x, y));
                    }
                }
                _previewCommandMarkers.Add((x, y, commandIndex));
                commandIndex++;
            }
        }

        #endregion
    }

    internal static class MovementDirectionDeltaMap
    {
        private static Dictionary<ushort, (int dx, int dy)> _map;

        public static void Invalidate() => _map = null;

        public static Dictionary<ushort, (int dx, int dy)> Get()
        {
            if (_map != null && _map.Count > 0) return _map;
            _map = new Dictionary<ushort, (int dx, int dy)>();
            var source = DSPRE.RomInfo.ScriptActionNamesDict;
            if (source == null || source.Count == 0)
            {
                if (ScriptDatabase.movementsDict != null)
                {
                    foreach (var kvp in ScriptDatabase.movementsDict)
                    {
                        var d = DeltaFromName(kvp.Value?.Name);
                        if (d != null) _map[kvp.Key] = d.Value;
                    }
                }
            }
            else
            {
                foreach (var kvp in source)
                {
                    var d = DeltaFromName(kvp.Value);
                    if (d != null) _map[kvp.Key] = d.Value;
                }
            }
            if (_map.Count == 0) _map = null;
            return _map ?? new Dictionary<ushort, (int dx, int dy)>();
        }

        private static (int dx, int dy)? DeltaFromName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            string normalized = new string(name.Where(char.IsLetterOrDigit).ToArray());
            if (normalized.StartsWith("Face", StringComparison.OrdinalIgnoreCase)) return (0, 0);
            if (normalized.IndexOf("OnSpot", StringComparison.OrdinalIgnoreCase) >= 0) return (0, 0);
            (int dx, int dy) baseDelta;
            if (normalized.IndexOf("North", StringComparison.OrdinalIgnoreCase) >= 0) baseDelta = (0, -1);
            else if (normalized.IndexOf("South", StringComparison.OrdinalIgnoreCase) >= 0) baseDelta = (0, 1);
            else if (normalized.IndexOf("East", StringComparison.OrdinalIgnoreCase) >= 0) baseDelta = (1, 0);
            else if (normalized.IndexOf("West", StringComparison.OrdinalIgnoreCase) >= 0) baseDelta = (-1, 0);
            else return null;
            int mult = 1;
            if (normalized.StartsWith("JumpVeryFar", StringComparison.OrdinalIgnoreCase)) mult = 3;
            else if (normalized.StartsWith("JumpFar", StringComparison.OrdinalIgnoreCase)) mult = 2;
            return (baseDelta.dx * mult, baseDelta.dy * mult);
        }
    }

    internal struct MovementEditorUndoState
    {
        public List<ScriptAction> Snapshot;
        public int ActionIndex;
    }
}
