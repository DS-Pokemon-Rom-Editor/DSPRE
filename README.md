# DSPRE - DS Pokémon ROM Editor Reloaded

<p align="center">
  <img src="readmeImages/Map%20Editor.png" alt="DSPRE" width="700"/>
</p>

**DSPRE (DS Pokémon ROM Editor Reloaded)** is a tool for editing Nintendo DS Pokémon games (Generation IV). It allows you to modify maps, scripts, events, Pokémon data, trainers, and more.

For additional documentation, tutorials, and research on DS Pokémon ROM hacking, see the **[DS Pokémon Hacking Wiki](https://ds-pokemon-hacking.github.io/)**.

---

## Table of Contents

- [Supported Games](#supported-games)
- [Editors](#editors)
- [ROM Toolbox Patches](#rom-toolbox-patches)
- [Related Tools](#related-tools)
- [Installation](#installation)
- [Getting Started](#getting-started)
- [Enhancements Over Original DSPRE](#enhancements-over-original-dspre)
- [Development Guide](#development-guide)
- [Architecture Overview](#architecture-overview)
- [Creating New Editors](#creating-new-editors)
- [Contributing](#contributing)
- [External Resources](#external-resources)

---

## Supported Games

| Game | EN | ES/IT/FR/DE | JP | KR | Notes |
|------|-----|----------------|-----|-----|-------|
| **Diamond** | Full Support | Extensive Support | Partial Support | Minimal Support | |
| **Pearl** | Full Support | Extensive Support | Partial Support | Minimal Support | |
| **Platinum** | Full Support | Extensive Support | Partial Support | Minimal Support | |
| **HeartGold** | Full Support | Extensive Support | Partial Support | Minimal Support | |
| **SoulSilver** | Full Support | Extensive Support | Partial Support | Minimal Support | |
| **hg-engine ROMs** | Partial Support | N/A | N/A | N/A | Some editors disabled |

---

## Editors

### Map Editor

View and edit 3D map-related data including collisions, permissions, building placement, import and export of map models.

- OpenGL 3D visualization
- Mouse-based building placement and movement
- Collision editing with type painters
- Permissions editing with flood fill
- BGS (Background Sound) section editing
- BDHCAM import/export
- Map NSBMD and BIN import/export
- DAE, GLB, and NSBMD model export
- 512px map screenshot export (for PDSMS backgrounds)

<img src="readmeImages/Map%20Editor.png" width="450" alt="Map Editor"/>

**Note:** To create new maps from scratch, use **[Pokémon DS Map Studio (PDSMS)](https://github.com/Trifindo/Pokemon-DS-Map-Studio)**.

---

### Script Editor

Edit game scripts in binary or plaintext format.

- Syntax highlighting via ScintillaNET
- Dual-format support (binary `.bin` and plaintext `.script`)
- External editing support with VS Code
- Script Navigator for quick navigation
- Search for any command
- Text Search and Replace
- Syntax error detection
- Level script clearing
- Number format selection (hex or decimal)
- Custom script command database support

<img src="readmeImages/Script%20Editor.png" width="550" alt="Script Editor"/>

**VS Code Extension:** Install the **[DSPRE Script Compatibility extension](https://github.com/DS-Pokemon-Rom-Editor/vsc-dspre-script-compat)** for syntax highlighting when editing scripts externally.

**Script Command Databases:** The command databases are maintained at **[scrcmd-database](https://github.com/DS-Pokemon-Rom-Editor/scrcmd-database)**. Contributions are welcome.

<img src="readmeImages/Script%20Command%20Database.png" width="450" alt="Script Command Database"/>

---

### Header Editor

Edit map header properties.

- Copy and paste headers
- Import and export
- Add and remove headers (if relevant patch applied)
- Location search
- WorldMap Coordinates (HGSS)
- Weather effects with preview
- Internal names editing
- Battle Background and Following Pokémon properties
- Camera settings
- Music settings

---

### Matrix Editor

Edit world map layout and connections.

- Custom color table support
- Matrix resizing
- Status bar coordinates
- Matrix names in selector

<img src="readmeImages/Matrix%20Editor.png" width="450" alt="Matrix Editor"/>

---

### Event Editor

Place and configure NPCs, warps, triggers, and spawnables.

- Mouse-based event placement
- Warp navigation
- Event duplication
- Selective event import from other files
- Overworld sprite rendering
- 512px customisable map and event data screenshot export

<img src="readmeImages/Event%20Editor.png" width="450" alt="Event Editor"/>

---

### Wild Encounters Editor

Configure wild Pokémon encounters.

- Encounter slot editing
- Import and export
- Add and remove encounter files
- Broken encounter file repair
- Separate editors for DPPt and HGSS formats

<img src="readmeImages/Wild%20Encounters%20Grass.png" width="450" alt="Wild Encounters Editor"/>

---

### Trainer Editor

Edit trainer data including party, AI, and items.

- Trainer Class editing
- Party composition (up to 6 Pokémon)
- Custom movesets
- AI flags
- Trainer and held items
- Party reordering
- DV Calculator (IV & Nature determination)
- Export to Showdown format

<img src="readmeImages/Trainer%20Editor.png" width="450" alt="Trainer Editor"/>

**Note:** For more advanced trainer editing features, see **[VSMaker2](https://github.com/Chvlkie/VSMaker2)**, which supports DSPRE unpacked ROMs.

**Note:** See [Generation IV Trainer Move Selection AI Reference](https://gist.github.com/lhearachel/ff61af1f58c84c96592b0b8184dba096) for details on AI flags.

---

### Pokémon Data Editors

#### Personal Data Editor
Edit Pokémon base stats and attributes.

- Base stats (HP, Attack, Defense, Sp.Atk, Sp.Def, Speed)
- Types
- Abilities
- EV yield
- TM/HM compatibility
- Catch rate, base EXP, growth rate
- Gender ratio
- Egg groups
- CSV import/export

<img src="readmeImages/Personal%20Data%20Editor.png" width="450" alt="Personal Data Editor"/>

#### Learnset Editor
Edit level-up moves.

- Add, edit, and delete moves
- Auto-sorting by level
- Manual sorting of moves learned at the same level
- Entry count warning for 20+ moves (see [maximum move threshold](https://ds-pokemon-hacking.github.io/docs/generation-iv/guides/editing_moves/#maximum-move-threshold))
- Bulk Learnset Editor with:
  - CSV import/export
  - Copy learnset to other Pokémon
  - Global move replacement
  - Level adjustment
  - Filter and search

<img src="readmeImages/Leanrset%20Ediotr.png" width="350" alt="Learnset Editor"/>

#### Evolution Editor
Edit evolution methods and targets.

- All evolution methods supported
- Evolution parameters (level, item, move, etc.)
- Multiple evolution paths

<img src="readmeImages/Evolution%20Editor.png" width="350" alt="Evolution Editor"/>

#### Egg Move Editor
Edit egg moves by species.

- Bulk replace and delete
- Search by Pokémon name
- CSV import/export
- Size limit tracking

---

### Move Data Editor

Edit move properties.

- Type, Split, Power, Accuracy, PP
- Priority
- Side Effect Probability
- Attack Range/Target
- Battle Effect Sequence
- Move Flags
- Contest data
- Natural Gift, Fling, Pluck effects
- CSV import/export

<img src="readmeImages/Move%20Data%20Editor.png" width="450" alt="Move Data Editor"/>

For more information, see the [DS Pokémon Hacking Wiki - Editing Moves](https://ds-pokemon-hacking.github.io/docs/generation-iv/guides/editing_moves/).

---

### Item Editor

Edit item data and effects.

- Icon and palette selection
- Price
- Hold effects
- Field and Battle pocket
- Field and Battle use functions
- Natural Gift, Fling, Pluck effects
- Party Use Parameters (status healing, HP/PP restoration, stat boosters, EV modifiers, friendship modifiers)

<img src="readmeImages/Item%20Data%20Editor.png" width="450" alt="Item Editor"/>

---

### TM Editor

Edit TM/HM move assignments and item palettes.

---

### Trade Editor

Edit in-game trade Pokémon.

- Species (given and requested)
- IVs
- Held item
- PID
- OT ID and gender
- Contest stats
- Origin language
- Nickname and OT name

<img src="readmeImages/Trade%20Editor.png" width="350" alt="Trade Editor"/>

---

### Safari Zone Editor (HGSS)

Edit Safari Zone encounters.

- All zone areas
- Grass, Surf, and Rod encounters
- Block placement requirements
- Import/export

<img src="readmeImages/Safari%20Editor.png" width="450" alt="Safari Zone Editor"/>

---

### Headbutt Encounter Editor (HGSS)

Edit Headbutt tree encounters with 3D map visualization.

- Normal and Special tree groups
- Click-to-select trees on map
- Import/export

<img src="readmeImages/Headbut%20Editor.png" width="450" alt="Headbutt Encounter Editor"/>

---

### Fly Editor

Edit fly destination tables.

- Game-over respawn locations
- Fly warp destinations
- Unlock conditions

---

### Camera Editor

Edit camera positions and angles.

<img src="readmeImages/Camera%20Editor.png" width="350" alt="Camera Editor"/>

---

### Building Editor

View and manage 3D building models.

- Interior and exterior models
- NSBMD model viewing
- Building NSBMD import/export
- DAE building export

---

### NSBTX Editor

Add, edit and delete AreaData and texture packs.

- Add and remove map and building texture packs (NSBTX)
- Add, edit and remove AreaData files
- Import/export NSBTX and AreaData
- Palette matching
- Texture preview

<img src="readmeImages/NSBTX%20Editor.png" width="350" alt="NSBTX Editor"/>

---

### Text Editor

Edit in-game text.

- Line reordering
- Row number display (decimal or hex)
- Search and Replace
- Chinese text support
- STRVAR helper with type reference
- Import/export

<img src="readmeImages/Text%20Editor.png" width="450" alt="Text Editor"/>

---

### Table Editor

Edit various game tables.

- Conditional Music Table (HGSS)
- Pre-Battle Effects (VS. sequences and battle tracks)

<img src="readmeImages/Table%20Editor.png" width="450" alt="Table Editor"/>

---

### Overlay Editor

View and manage overlays.

- Compression status
- Decompression operations
- RAM address mapping

---

### Spawn Settings Editor

Edit starting location and money.

---

### Utilities

- NARC Packer/Unpacker
- Batch Rename (list-based and content-based)
- Advanced Header Search
- NSBMD/NSBTX utilities
- DocTool (export data to CSV)

---

## ROM Toolbox Patches

Apply patches to expand ROM capabilities:

| Patch | Description |
|-------|-------------|
| Expand ARM9 Memory | Increase usable ARM9 memory |
| Dynamic Cameras | BDH camera positioning |
| Overlay1 Uncompressed | Keep Overlay1 uncompressed |
| Sentence Case Names | Convert Pokémon names to sentence case |
| Standardize Items | Standardize item numbers across games |
| Expand Matrix 0 | Larger world map matrices |
| Dynamic Headers | Extended header functionality |
| Disable Texture Animations | HGSS texture animation killswitch |
| Script Command Repointing | Custom script database support |
| Trainer Name Expansion | Extended trainer name length |

<img src="readmeImages/Patch%20Toolbox.png" width="350" alt="ROM Toolbox"/>

---

## Related Tools

| Tool | Purpose |
|------|---------|
| **[Pokémon DS Map Studio](https://github.com/Trifindo/Pokemon-DS-Map-Studio)** | Create new 3D maps from scratch |
| **[VSMaker2](https://github.com/Chvlkie/VSMaker2)** | Advanced trainer editor (supports DSPRE unpacked ROMs) |
| **[VS Code Extension](https://github.com/DS-Pokemon-Rom-Editor/vsc-dspre-script-compat)** | Syntax highlighting for DSPRE scripts |

---

## Installation

### Requirements
- Windows 7 or later
- .NET Framework 4.8

### Download
1. Download the latest release from [GitHub Releases](https://github.com/DS-Pokemon-Rom-Editor/DSPRE/releases)
2. Extract to a folder of your choice
3. Run `DSPRE.exe`

---

## Getting Started

1. Open DSPRE and click "Open ROM" or drag-drop a `.nds` file
2. Wait for ROM extraction (first-time setup)
3. Select an editor tab to begin editing
4. Save your work frequently in each editor
5. Click "Save ROM" to repack changes to a modified `.nds` file
6. Test your ROM in an emulator

---

## Enhancements Over Original DSPRE

These improvements were made compared to the original DSPRE by Nomura:

- Faster load and save times
- Fixed Japanese DP ROM support
- Configurable toolbar layouts
- User preference persistence
- Automatic updates via Velopack
- Auto-updating script command databases
- Application logging with crash reporter
- Load extracted data from directory without ROM
- ARM9 mismatch warnings
- ALT key shortcuts
- Custom character map support
- Recent project quick-open

---

## Development Guide

### Prerequisites
- Visual Studio 2019 or later
- .NET Framework 4.8 SDK
- Git

### Building from Source
```powershell
git clone https://github.com/DS-Pokemon-Rom-Editor/DSPRE.git
cd DSPRE
msbuild DSPRE.sln /p:Configuration=Release
```

### Solution Structure

```
DSPRE/
??? DS_Map/           # Main application (DSPRE.csproj)
?   ??? Editors/      # All editor UserControls and Forms
?   ??? ROMFiles/     # Data model classes
?   ??? DSUtils/      # Low-level DS utilities
?   ??? Resources/    # Databases and static resources
?   ??? LibNDSFormats/# 3D model format handling
?   ??? ...
??? Ekona/            # Image/sprite library (Ekona.csproj)
??? Images/           # Nintendo image format plugin (Images.csproj)
```

---

## Architecture Overview

### Key Design Patterns

#### 1. Static ROM Context (`RomInfo.cs`)
All ROM metadata is stored statically for global access:
```csharp
string workDir = RomInfo.workDir;
GameFamilies family = RomInfo.gameFamily;
string scriptsPath = RomInfo.gameDirs[DirNames.scripts].unpackedDir;
```

#### 2. RomFile Base Class
All ROM data structures inherit from `RomFile`:
```csharp
public abstract class RomFile {
    public abstract byte[] ToByteArray();
    public bool SaveToFile(string path, bool showSuccessMessage = true);
    protected bool SaveToFileDefaultDir(DirNames dir, int IDtoReplace, bool showSuccessMessage = true);
}
```

#### 3. NARC Archive System
Game data is stored in NARC archives that are unpacked for editing:
```csharp
DSUtils.TryUnpackNarcs(new List<DirNames> { 
    DirNames.scripts, 
    DirNames.eventFiles 
});

string path = Filesystem.GetScriptPath(fileID);
```

#### 4. Handler State Pattern
Prevent event handler recursion:
```csharp
private void SomeControl_ValueChanged(object sender, EventArgs e)
{
    if (Helpers.HandlersDisabled) return;
    
    Helpers.DisableHandlers();
    try {
        otherControl.Value = newValue;
    } finally {
        Helpers.EnableHandlers();
    }
}
```

#### 5. Dirty Tracking Pattern
Track unsaved changes:
```csharp
private bool dirty = false;

private void SetDirty(bool status) {
    dirty = status;
    this.Text = formName + (dirty ? "*" : "");
}

private bool CheckDiscardChanges() {
    if (!dirty) return true;
    
    var result = MessageBox.Show("Unsaved changes. Discard?", 
        "Warning", MessageBoxButtons.YesNo);
    return result == DialogResult.Yes;
}
```

---

## Creating New Editors

### Example: Creating a Simple Data Editor

```csharp
using DSPRE.ROMFiles;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace DSPRE.Editors
{
    public partial class MyNewEditor : Form
    {
        private MyDataFile currentFile;
        private int currentId = 0;
        private bool dirty = false;
        private readonly string formName = "My New Editor";
        private readonly string[] itemNames;

        public MyNewEditor()
        {
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { 
                RomInfo.DirNames.myDataFiles 
            });
            
            itemNames = RomInfo.GetItemNames();
            
            InitializeComponent();
            
            Helpers.DisableHandlers();
            fileIdNumeric.Maximum = GetFileCount() - 1;
            itemComboBox.Items.AddRange(itemNames);
            Helpers.EnableHandlers();
            
            LoadFile(0);
        }

        private int GetFileCount()
        {
            return Directory.GetFiles(
                RomInfo.gameDirs[RomInfo.DirNames.myDataFiles].unpackedDir
            ).Length;
        }

        private void LoadFile(int id)
        {
            Helpers.DisableHandlers();
            
            currentId = id;
            currentFile = new MyDataFile(id);
            
            itemComboBox.SelectedIndex = currentFile.ItemId;
            valueNumeric.Value = currentFile.Value;
            
            SetDirty(false);
            Helpers.EnableHandlers();
        }

        private void SetDirty(bool status)
        {
            dirty = status;
            this.Text = formName + (dirty ? "*" : "");
        }

        private bool CheckDiscardChanges()
        {
            if (!dirty) return true;
            
            var result = MessageBox.Show(
                "Unsaved changes. Save before switching?",
                "Unsaved Changes",
                MessageBoxButtons.YesNoCancel
            );
            
            if (result == DialogResult.Yes)
            {
                SaveFile();
                return true;
            }
            return result == DialogResult.No;
        }

        private void SaveFile()
        {
            currentFile.SaveToFileDefaultDir(currentId, showSuccessMessage: false);
            SetDirty(false);
        }

        private void fileIdNumeric_ValueChanged(object sender, EventArgs e)
        {
            if (Helpers.HandlersDisabled) return;
            
            if (CheckDiscardChanges())
            {
                LoadFile((int)fileIdNumeric.Value);
            }
            else
            {
                Helpers.DisableHandlers();
                fileIdNumeric.Value = currentId;
                Helpers.EnableHandlers();
            }
        }

        private void itemComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (Helpers.HandlersDisabled) return;
            currentFile.ItemId = itemComboBox.SelectedIndex;
            SetDirty(true);
        }

        private void valueNumeric_ValueChanged(object sender, EventArgs e)
        {
            if (Helpers.HandlersDisabled) return;
            currentFile.Value = (int)valueNumeric.Value;
            SetDirty(true);
        }

        private void saveButton_Click(object sender, EventArgs e)
        {
            SaveFile();
        }

        private void MyNewEditor_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!CheckDiscardChanges())
            {
                e.Cancel = true;
            }
        }
    }
}
```

### Creating the Data Model

```csharp
using System.IO;

namespace DSPRE.ROMFiles
{
    public class MyDataFile : RomFile
    {
        public int ID { get; }
        public int ItemId { get; set; }
        public int Value { get; set; }

        public MyDataFile(int id)
        {
            ID = id;
            string path = Filesystem.GetMyDataPath(id);
            
            using (var reader = new BinaryReader(File.OpenRead(path)))
            {
                ItemId = reader.ReadUInt16();
                Value = reader.ReadInt32();
            }
        }

        public override byte[] ToByteArray()
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write((ushort)ItemId);
                writer.Write(Value);
                return ms.ToArray();
            }
        }

        public void SaveToFileDefaultDir(int id, bool showSuccessMessage = true)
        {
            SaveToFileDefaultDir(RomInfo.DirNames.myDataFiles, id, showSuccessMessage);
        }
    }
}
```

### Adding Filesystem Helper

In `Filesystem.cs`:
```csharp
public static string myDataFiles => 
    RomInfo.gameDirs[RomInfo.DirNames.myDataFiles].unpackedDir;

public static string GetMyDataPath(int id)
{
    return GetPath(myDataFiles, id);
}

public static int GetMyDataCount()
{
    return Directory.GetFiles(myDataFiles).Length;
}
```

### Registering the Editor

In `Main Window.cs`:
```csharp
private void myEditorToolStripMenuItem_Click(object sender, EventArgs e)
{
    DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.myDataFiles });
    new MyNewEditor().Show();
}
```

---

## Contributing

### Code Style Guidelines

1. Follow existing patterns - look at similar editors for reference
2. Use Helpers - `Helpers.DisableHandlers()`, `Helpers.statusLabelMessage()`, etc.
3. Log appropriately - use `AppLogger.Info()`, `AppLogger.Error()`, etc.
4. Check game version - use `RomInfo.gameFamily` for version-specific logic
5. Avoid useless comments - only comment "why", not "what"

### Pull Request Process

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/my-feature`
3. Commit your changes with clear messages
4. Ensure the solution builds without errors
5. Create a Pull Request with a detailed description

---

## External Resources

### DS Pokémon Hacking Wiki
The **[DS Pokémon Hacking Wiki](https://ds-pokemon-hacking.github.io/)** is the primary documentation resource for DS Pokémon ROM hacking:

- **[Generation IV Guides](https://ds-pokemon-hacking.github.io/docs/category/guides)**
- **[Generation IV Resources](https://ds-pokemon-hacking.github.io/docs/category/resources)**
- **[Getting Started](https://ds-pokemon-hacking.github.io/docs/generation-iv/guides/getting_started/)**

### Repositories
- **[DSPRE](https://github.com/DS-Pokemon-Rom-Editor/DSPRE)** - This repository
- **[Script Command Database](https://github.com/DS-Pokemon-Rom-Editor/scrcmd-database)** - Script command definitions (contributions welcome)
- **[VS Code Extension](https://github.com/DS-Pokemon-Rom-Editor/vsc-dspre-script-compat)** - Syntax highlighting for external script editing

---

## License

This project is open source. See the repository for license details.

---

## Credits

- Original DSPRE by Nomura
- All contributors to the DSPRE Reloaded project
- The DS Pokémon ROM hacking community
