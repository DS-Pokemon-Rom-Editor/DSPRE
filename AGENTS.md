# AGENTS.md

This file provides guidance to LLM agents when working with code in this repository. It has been updated with research on migrating the project to .NET 8.

## Project Overview

DS Pokemon ROM Editor (DSPRE) Reloaded is a C# Windows Forms application for editing Nintendo DS Pokemon ROM files. This is a major overhaul of the original DSPRE by Nomura with significant new features, performance improvements, and bug fixes. The editor supports multiple Pokemon games: Diamond/Pearl/Platinum (DPPt), and HeartGold/SoulSilver (HGSS).

## .NET 8 Migration Research

A feasibility study was conducted to assess migrating this project from .NET Framework 4.8 to .NET 8.

### Migration Feasibility: **FEASIBLE with Significant Effort**

The migration is technically possible but requires a substantial overhaul of the graphics and UI layers.

### Key Migration Blockers & Challenges

| Component | Severity | Issue | Migration Path |
|-----------|----------|-------|----------------|
| **OpenGL Stack** | **CRITICAL** | Uses legacy **Tao.OpenGl** and **OpenTK 1.0**. These use immediate mode rendering (glBegin/glEnd) which is deprecated and incompatible with modern .NET Core/8. | Complete rewrite using **OpenTK 4.x** or **Silk.NET**. Requires implementing shaders and VBOs to replace the fixed-function pipeline. |
| **WinForms UI** | **HIGH** | 110+ files using Windows Forms. While supported in .NET 8, it remains Windows-only. | Port to .NET 8 WinForms (remains Windows-only) or migrate to **Avalonia** for true cross-platform support. |
| **WPF Interop** | **HIGH** | Uses WPF components for 3D rendering in some editors. | Replace WPF interop with pure OpenGL (OpenTK) or modern WinUI 3 controls. |
| **ScintillaNET** | **MEDIUM** | Version 3.6.3 is archived and does not support .NET Core. | Migrate to **Scintilla5.NET** (maintained fork). |
| **Platform Code** | **MEDIUM** | 1,400+ references to Windows-specific code (P/Invoke, backslash paths, Registry). | Refactor to use `Path.Combine`, cross-platform dialogs, and managed alternatives for native calls. |

### Migration Roadmap Recommendation

1.  **Phase 1: Dependency Modernization** - Replace WindowsAPICodePack with modern .NET dialogs. Update LibGit2Sharp and Velopack to .NET 8 versions.
2.  **Phase 2: Project Conversion** - Convert `.csproj` files to SDK-style format and target `net8.0-windows`.
3.  **Phase 3: OpenGL Layer Rewrite** - (The most complex phase) Replace Tao.OpenGl with OpenTK 4.x. This involves writing shaders to mimic the old fixed-function pipeline.
4.  **Phase 4: Script Editor Port** - Migrate ScintillaNET to Scintilla5.NET.
5.  **Phase 5: Cross-Platform Refactoring** - (Optional) Replace WinForms with Avalonia and eliminate remaining P/Invokes to support Linux/macOS.

---

## Build Commands

Currently, this is a .NET Framework 4.8 Windows Forms application.

### Build the application:
```bash
# From Visual Studio: Build > Build Solution (Ctrl+Shift+B)
# Or from command line:
msbuild DS_Map.sln /t:Build /p:Configuration=Release
```

### Build configurations:
- **Debug**: `bin\Debug\` - Full debug symbols, no optimization
- **Release**: `bin\Release\` - Optimized, no debug info

### Run the application:
```bash
# From Visual Studio: Debug > Start Debugging (F5)
# Or directly:
.\DS_Map\bin\Debug\DSPRE.exe
```

## Solution Structure

The solution consists of three main projects:

1. **DSPRE.csproj** (`DS_Map\`) - Main Windows Forms application
2. **Ekona.csproj** (`Ekona\`) - Image/sprite processing library with plugin architecture
3. **Images.csproj** (`Images\Images\`) - Nintendo DS image format handlers (NCGR, NCER, NCLR, etc.)

## Architecture

### Core Components

#### ROM File System (`DS_Map\Filesystem.cs`, `DS_Map\Narc.cs`)
- **Filesystem**: Static utility class for ROM file operations, NARC packing/unpacking
- **Narc**: Handles Nitro Archive (NARC) files - the standard Nintendo DS archive format
- All ROM data is extracted to/from NARC archives for editing

#### ROM Data Model (`DS_Map\ROMFiles\`)
All ROM data structures inherit from the abstract `RomFile` base class with serialization methods:

- **MapFile**: Complete map data (collisions, permissions, buildings, terrain, BGS)
- **MapHeader**: Map metadata and properties
- **EventFile**: Map events (spawns, warps, triggers, overworlds)
- **ScriptFile**: Script commands and scripting data (supports both binary and plaintext formats)
- **LevelScriptFile**: Level scripts (trigger-based scripts for map events)
- **EncounterFile**: Wild Pokemon encounters
- **TrainerFile**: Trainer data with party and movesets
- **TradeData**: In-game trade Pokemon with IVs, natures, items
- **SafariZoneEncounterFile**: Safari Zone encounter data
- **HeadbuttEncounterFile**: Headbutt tree encounters (HGSS)
- **TextArchive**: In-game text strings
- **GameMatrix**: Area matrix layout
- **AreaData**: Area type and terrain information
- **Building**: 3D building objects with position/rotation/scale

Each ROM data type implements `ToByteArray()` for binary serialization back to ROM format.

#### ROM Version Management (`DS_Map\RomInfo.cs`)
- **GameVersions**: Enum of individual game versions (DP, Pt, HGSS, etc.)
- **GameFamilies**: Groups of related games
- **DirNames**: Directory mapping for different ROM sections
- **gameDirs**: Static dictionary mapping sections to file paths per game

#### 3D Graphics System (`DS_Map\LibNDSFormats\`)
Handles Nintendo DS 3D formats:

- **NSBMD** (`NSBMD\`): Nitro Polygon Model format
  - `NSBMDLoader`: Parsing and loading
  - `NSBMDGlRenderer`: OpenGL-based rendering
  - `MTX44`: 4x4 matrix transformations (column-major)
- **NSBTX** (`NSBTX\`): Nitro Texture format with palette management
- **NSBCA/NSBTA/NSBTP**: Animation formats (skeletal, texture, and texture pattern)
- **NSBUtils**: Utilities for merging models with textures, extracting textures
- **OBJWriter**: Export to Wavefront OBJ format
- **ModelUtils**: Export to DAE (via Apicula) and GLB formats

#### Script System (Major Feature - New Plaintext Support)

**Script Files and Formats** (`DS_Map\ROMFiles\ScriptFile.cs`):
- **Binary Format**: Original ROM format stored in NARC archives at `/fielddata/script/`
- **Plaintext Format**: NEW - Human-readable `.script` files exported to `expanded/scripts/` directory
- **Dual Representation**: Scripts maintain both binary (for ROM) and plaintext (for editing) versions
- **Automatic Sync**: Binary files automatically rebuilt from plaintext when plaintext is newer

**Plaintext Script Format**:
```
//===== SCRIPTS =====//
Script 1:
    Command1 param1 param2
    Command2 param1
Script 2:
    UseScript_#1

//===== FUNCTIONS =====//
Function 1:
    Command1 param1

//===== ACTIONS =====//
Action 1:
    Movement1
```

**Script File Structure**:
- Three sections: Scripts, Functions, Actions
- Each section can contain multiple numbered containers
- Commands within containers are indented
- UseScript references allow code reuse between scripts

**Script Database System** (`DS_Map\Resources\ScriptDatabase.cs`):
- **JSON-Based**: Script commands loaded from JSON database files
- **Version-Specific**: Separate command databases for Diamond/Pearl, Platinum, and HeartGold/SoulSilver
- **Custom Databases**: Users can load custom script command databases for ROM hacks
- **Database Hashing**: MD5 hash tracking detects database changes and triggers automatic re-export
- **Reference Data**: Built-in dictionaries for Pokemon, items, moves, sounds, trainers
- **Command Metadata**: Each command includes ID, name, parameter types, parameter names, descriptions

**Script Commands** (`DS_Map\ROMFiles\ScriptCommand.cs`, `DS_Map\Script\ScriptParameter.cs`):
- **ScriptCommand**: Represents individual script commands with ID and parameters
- **ScriptCommandContainer**: Groups related commands into scripts or functions
- **ScriptActionContainer**: Groups movement/action commands
- **Parameter Types**: 15+ types including Integer, Variable, Pokemon, Item, Move, Sound, Trainer, etc.
- **Smart Formatting**: Parameters displayed with friendly names (e.g., "Pikachu" instead of "25")

**Custom Database Management** (`DS_Map\Resources\CustomScrcmdManager.cs`):
- **CustomScrcmdManager**: Form for managing custom script databases
- **Auto-Detection**: Scans scripts on load and prompts user to load custom database if invalid commands found
- **Database Storage**: Custom databases stored in `edited_databases/` with naming: `{romname}_scrcmd_database.json`
- **Reparse Support**: Can reload database and reparse all scripts with progress tracking
- **Import/Export**: Share custom databases between users

**Database Hashing and Change Detection**:
- **Hash File**: `.database_hash` marker file in `expanded/scripts/` directory stores MD5 hash
- **Automatic Detection**: On editor load, compares current database hash against stored hash
- **Auto Re-export**: If database changed, automatically deletes and rebuilds all plaintext scripts
- **Prevents Corruption**: Ensures scripts and database are always in sync

**Plaintext Caching**:
- **Performance Optimization**: Dictionary cache stores parsed plaintext scripts with timestamps
- **Avoids Re-parsing**: During batch operations (like search), uses cache instead of re-reading files
- **Cache Invalidation**: Timestamps validate whether cached version is still current

**VS Code Integration**:
- **External Editing**: "Open in VSCode" button launches Visual Studio Code
- **Command**: `code "{scriptsFolder}" "{txtPath}"` opens both folder and specific file
- **Timestamp-based Sync**: On script load and ROM save, DSPRE checks if plaintext files are newer than binary and rebuilds if needed
- **Bidirectional Sync**: Changes in VSCode reflected in DSPRE on next load/save, changes in DSPRE reflected in plaintext files on save

**Script Export/Import Workflow**:
1. **Initial Load**: On first ROM open, all binary scripts exported to plaintext in `expanded/scripts/`
2. **Selective Export**: Existing plaintext files preserved (not overwritten) to maintain user edits
3. **External Editing**: User can edit `.script` files in VSCode or any text editor
4. **Auto-Rebuild**: On ROM save, DSPRE scans for plaintext files newer than binary and rebuilds them
5. **Binary Update**: Rebuilt binary scripts packed back into ROM NARC archive

**Progress Tracking** (`DS_Map\Editors\Utils\LoadingForm.cs`):
- **LoadingForm**: Progress bar dialog for long-running script operations
- **Pokemon Facts**: Displays random Pokemon facts during loading to entertain users
- **Thread-Safe Updates**: Real-time progress updates via Invoke pattern
- **Used For**: Initial script export, database reparsing, batch operations

#### Editor Framework (`DS_Map\Editors\`)
Editors follow two patterns:
- **UserControl editors**: Embedded in main MDI window (MapEditor, HeaderEditor, ScriptEditor, etc.)
- **Form editors**: Standalone windows (BuildingEditor, ItemEditor, PokemonEditor, etc.)

Key editors:
- **MapEditor**: 3D map visualization with OpenGL, collision editing, building placement
- **ScriptEditor**: Syntax-highlighted script editing using ScintillaNET with plaintext export, VSCode integration, custom database support
- **LevelScriptEditor**: Level script trigger management (separate from regular scripts)
- **EventEditor**: Event placement with mouse support, navigation, and sprite rendering
- **HeaderEditor**: Map header properties with copy/paste support
- **MatrixEditor**: Area matrix editing with visual grid
- **EncountersEditor**: Wild Pokemon encounter editing
- **TrainerEditor**: Complete trainer data editing with party, movesets, and AI flags
- **TextEditor**: In-game text editing with search/replace
- **SafariZoneEditor**: Safari Zone encounter management
- **HeadbuttEncounterEditor**: Headbutt tree encounter editing (HGSS)
- **TradeEditor**: In-game trade Pokemon editor with IV/nature/item support
- **EvolutionsEditor**: Evolution method and trigger editing
- **LearnsetEditor**: Move learning method editor
- **BtxEditor**: Dedicated NSBTX texture/palette editor with confirmation dialogs

#### Main Application (`DS_Map\Main Window.cs`)
- **MainProgram**: MDI (Multiple Document Interface) main window
- Manages ROM project loading, editor lifecycle, and user preferences
- Uses Velopack for automatic updates
- Settings persisted via `SettingsManager` and `App.config`
- Recent project reopening with confirmation dialog
- Version label display showing game version and ROM ID

### Architectural Patterns

1. **Abstract Base Class Pattern**: `RomFile` base class for all ROM data types
2. **Static Helpers**: `Helpers.cs` (rendering, UI, ROM operations), `Filesystem.cs` (ROM I/O)
3. **Stream-Based I/O**: Heavy use of `MemoryStream`, `BinaryReader`/`BinaryWriter`, `EndianBinaryReader`
4. **Plugin Architecture**: `IPlugin`, `IGamePlugin` interfaces in Ekona library
5. **OpenGL Rendering**: 3D visualization via Tao.OpenGl and HelixToolkit
6. **Dual File Format**: Binary (ROM) + Plaintext (editing) for script files
7. **Caching with Validation**: Timestamp-based cache invalidation for performance

### External Dependencies

Key NuGet packages:
- **ScintillaNET**: Syntax-highlighted code editor for scripts
- **Velopack**: Application update framework
- **OpenTK, Tao.OpenGl, HelixToolkit**: 3D graphics rendering
- **Microsoft.WindowsAPICodePack**: Windows integration
- **LibGit2Sharp**: Git integration
- **System.Text.Json**: JSON serialization for settings and databases
- **YamlDotNet**: YAML parsing for ds-rom project files

### File Locations

DSPRE uses specific directory structures within ROM files:
- Map data: NARC files in `/fielddata/land_data/`
- Scripts (binary): `/fielddata/script/` (NARC archive)
- Scripts (plaintext): `expanded/scripts/` (working directory, `.script` files)
- Events: `/fielddata/eventdata/`
- Encounters: `/fielddata/encountdata/`
- Text: `/msgdata/`
- Graphics: `/data/` (NSBMD, NSBTX files)

DSPRE user data directories:
- Database path: `Program.DatabasePath` (typically `~/.dspre/databases/`)
- Custom databases: `edited_databases/` subdirectory
- Database hash marker: `expanded/scripts/.database_hash`

Game-specific paths are defined in `RomInfo.gameDirs`.

## Development Guidelines

### Code Style and Formatting

**IMPORTANT: Avoid Useless Comments**
- Do NOT write comments that simply restate what the code does
- Comments should only explain "why" the code exists, not "what" it does
- Only add comments when there's a non-obvious reason, tricky logic, or important context

### ROM File Editing Pattern
When editing ROM data:
1. Load ROM project (unpacks to working directory)
2. Open NARC archives using `Narc.Open()`
3. Parse binary data into data structures (e.g., `MapFile.FromByteArray()`)
4. Modify data in memory
5. Serialize back using `ToByteArray()`
6. Save NARC using `narc.Save()`
7. Save entire ROM project to repack into ROM file

### Script Editing Pattern (NEW)
When working with scripts:
1. **Loading**: ScriptFile automatically checks for plaintext version via `TryReadPlaintextIfNewer()`
2. **Editing**: User can edit in ScriptEditor (Scintilla) or external editor (VSCode)
3. **Plaintext Export**: First load exports all scripts to `expanded/scripts/{ID:D4}.script`
4. **External Changes**: DSPRE detects when plaintext files are newer than binary
5. **Rebuilding**: `RebuildBinaryScriptsFromPlaintext()` converts plaintext back to binary on save
6. **Database Changes**: Hash comparison triggers automatic re-export when database changes

### 3D Rendering Conventions
- Uses column-major matrices (`MTX44`)
- OpenGL coordinate system (right-handed)
- Separate rendering paths for textured vs untextured models
- Camera position managed by `GameCamera` class

### Binary Format Handling
- Nintendo DS uses little-endian architecture (use `EndianBinaryReader` for big-endian sections)
- NARC format: BTAF (File Allocation Table), BTNF (Name Table), GMIF (File Image)
- Many formats use magic numbers for identification (e.g., "NSBMD", "NSBTX", "NARC")

### Editor State Management
- Editors use event handlers to disable/enable during bulk operations
- State saved via `SettingsManager` to `App.config`
- User preferences include UI layout, rendering toggles, export paths

### Script Command System
- **Primary Database**: `Resources\ScriptDatabase.cs` with JSON loader
- **Custom Databases**: User-provided JSON files in `edited_databases/` directory
- **Version-Specific**: Different command sets for DP, Platinum, HGSS
- **Parameters**: Parsed using `ScriptParameter` class with 15+ parameter types
- **Smart Display**: Friendly names for Pokemon, items, moves, etc. from reference dictionaries
- **Variable Length**: Commands can have variable-length parameters

### Error Handling
- Use structured exception handling with user-friendly messages
- `CrashReporter.cs` logs errors
- `AppLogger.cs` for application logging
- `correctnessFlag` in data structures tracks integrity
- **Script Validation**: Invalid commands detected on load with detailed error messages
- **Database Prompts**: User prompted to load custom database when invalid commands found

## ROM Toolbox Patches

DSPRE includes a ROM Toolbox with patches:
- **ARM9 Expansion**: Expand ARM9 usable memory
- **Dynamic Cameras**: BDH camera patch for dynamic positioning
- **Overlay Management**: Set Overlay1 as uncompressed
- **Pokemon Names**: Convert Pokemon names to Sentence Case
- **Item Standardization**: Standardize item numbers across games
- **Matrix Expansion**: Expand matrix 0 for larger areas
- **Dynamic Headers**: Extended header functionality
- **Script Command Repointing**: Support for custom script databases
- **Trainer Name Expansion**: Extended trainer name length
- **Texture Animation Killswitch**: Disable texture animation patches

Patch data stored in `Resources\ROMToolboxDB\`.

## Important Considerations

### Game Version Detection
Always check `RomInfo.gameFamily` or `RomInfo.gameVersion` as different Pokemon games have:
- Different file offsets and structures
- Different header formats
- Different script command sets (DP vs Pt vs HGSS)
- Different encounter table layouts
- Different event structures

### Performance
- Original DSPRE had slow load/save times - optimizations focused on streaming I/O
- Parallel processing used for ROM unpacking
- In-memory caching for frequently accessed data
- **Script Caching**: Plaintext scripts cached with timestamps to avoid re-parsing
- **Selective Export**: Only re-export scripts when database hash changes
- **Lazy Loading**: Plaintext only read if newer than binary

### Unsafe Code
The project uses `AllowUnsafeBlocks=true` for performance-critical binary operations.

### Tools Directory
External tools in `DS_Map\Tools\`:
- **dsrom.exe**: Primary ROM extraction and building tool (ds-rom format)
- **apicula.exe**: DAE export support
- **ndstool.exe**: Legacy ROM manipulation (kept for conversion from ndstool projects only)
- **blz.exe**: Legacy compression utilities (kept for conversion from ndstool projects only)
- **charmap.xml**: Character encoding map
- **pokefacts.txt**: Pokemon facts for loading screens (optional)

## ROM Extraction and Building

DSPRE uses **ds-rom** as the default ROM extraction and building tool. The legacy **ndstool** format is still supported for conversion purposes.

### Project Formats

DSPRE supports two ROM project formats:

#### ds-rom Format (Current Default)
- **Tool**: `dsrom.exe` ([ds-rom project](https://github.com/Prof9/ds-rom))
- **Detection**: Presence of `header.yaml` and `config.yaml` files
- **Directory Structure**:
  - `files/` - ROM filesystem root
  - `arm9/arm9.bin` - ARM9 binary
  - `arm9_overlays/ov{ID}.bin` - ARM9 overlays (e.g., `ov001.bin`)
  - `header.yaml` - ROM header metadata (YAML)
  - `config.yaml` - Build configuration (YAML)
- **Overlay Compression**: Automatically handled by ds-rom during build
- **Benefits**: 
  - Cleaner directory structure
  - YAML-based metadata (human-readable)
  - Automatic overlay compression management
  - Better suited for version control

#### ndstool Format (Legacy)
- **Tool**: `ndstool.exe` (legacy)
- **Detection**: Presence of `header.bin` file
- **Directory Structure**:
  - `data/` - ROM filesystem root
  - `arm9.bin` - ARM9 binary (root level)
  - `overlay/overlay_{ID}.bin` - ARM9 overlays (e.g., `overlay_0001.bin`)
  - `header.bin` - ROM header (binary)
  - `banner.bin` - Banner/icon data
- **Overlay Compression**: Manual decompression required via `blz.exe`
- **Status**: Supported for conversion only; new projects use ds-rom

### Project Format Detection

The `RomInfo.IsDsRomProject` property automatically detects the project format:

```csharp
public static bool IsDsRomProject => File.Exists(Path.Combine(workDir, "header.yaml"));
```

When DSPRE loads a ROM project:
1. Checks for `header.yaml` (ds-rom) vs `header.bin` (ndstool)
2. Sets `RomInfo.IsDsRomProject` accordingly
3. Uses appropriate extraction/repacking logic throughout the application

### Conversion Workflow

When a legacy ndstool project is detected on first save:

1. **User Prompt**: Dialog asks if user wants to convert to ds-rom format
2. **Backup Creation**: Original ndstool project backed up to `{workDir}.ndstool_backup.zip`
3. **Conversion Process**:
   - Decompresses all overlays using `blz.exe`
   - Creates ds-rom directory structure:
     - Moves `data/` → `files/`
     - Moves `arm9.bin` → `arm9/arm9.bin`
     - Moves `overlay/overlay_{ID}.bin` → `arm9_overlays/ov{ID}.bin`
   - Generates `header.yaml` from `header.bin` binary data
   - Generates `config.yaml` with compression settings
   - Removes legacy files (`header.bin`, `banner.bin`, empty `overlay/` and `data/` directories)
4. **Verification**: Sets `RomInfo.IsDsRomProject = true`
5. **Continue Save**: Proceeds with ds-rom format save

If user declines conversion, DSPRE continues using ndstool format (will prompt again on next save).

### YAML Parsing

The `YamlUtils.cs` utility class handles YAML parsing for ds-rom projects:

- **Game ID Extraction**: Reads `game_code` from `header.yaml` for ROM identification
- **YamlDotNet**: Uses YamlDotNet library for robust YAML deserialization
- **Fallback Handling**: Falls back to binary header parsing if YAML is corrupted

Example `header.yaml` structure:
```yaml
game_title: "POKEMON D"
game_code: "ADAE"
maker_code: "01"
unit_code: 0x00
...
```

### Overlay Path Resolution

Overlay utilities (`OverlayUtils.cs`) automatically resolve overlay paths based on project format:

```csharp
public static string GetOverlayPath(int overlayID)
{
    if (RomInfo.IsDsRomProject)
        return Path.Combine(RomInfo.workDir, "arm9_overlays", $"ov{overlayID:D3}.bin");
    else
        return Path.Combine(RomInfo.workDir, "overlay", $"overlay_{overlayID:D4}.bin");
}
```

This ensures editors (Overlay Editor, Map Editor, etc.) work seamlessly with both formats.

### Building ROMs

When saving a ROM project:

**ds-rom Format**:
1. Pack modified files back into `files/` directory
2. Ensure ARM9 overlays are in `arm9_overlays/`
3. Run `dsrom.exe build` with `config.yaml`
4. ds-rom automatically compresses overlays as specified in `config.yaml`
5. Outputs `.nds` file

**ndstool Format**:
1. Pack modified files back into `data/` directory
2. Manually compress overlays using `blz.exe` (if required)
3. Run `ndstool.exe` with binary header
4. Outputs `.nds` file

## Application Configuration

Key settings in `DS_Map\App.config` and `SettingsManager`:
- `menuLayout`: UI layout preference
- `lastColorTablePath`: User's palette path
- `textEditorPreferHex`: Text format preference
- `scriptEditorFormatPreference`: Script display format (binary or plaintext)
- `useDecompNames`: Option to use decompilation project names
- `automaticallyUpdateDBs`: Auto-sync online databases
- `renderSpawnables`, `renderOverworlds`, `renderWarps`, `renderTriggers`: Event rendering toggles
- `exportPath`, `mapImportStarterPoint`: Import/export paths
- `openDefaultRom`: ROM opening behavior
- `databasesPulled`: Online database sync status

## Key File Paths

### Core Application Files
- Entry point: `DS_Map\Program.cs`
- Main window: `DS_Map\Main Window.cs`
- Settings: `DS_Map\SettingsManager.cs`
- ROM file base: `DS_Map\ROMFiles\RomFile.cs`
- File system: `DS_Map\Filesystem.cs`
- NARC handler: `DS_Map\Narc.cs`
- ROM info: `DS_Map\RomInfo.cs`
- Helpers: `DS_Map\Helpers.cs`

### Script System Files (Important)
- Script file I/O: `DS_Map\ROMFiles\ScriptFile.cs`
- Script commands: `DS_Map\ROMFiles\ScriptCommand.cs`
- Script containers: `DS_Map\ROMFiles\ScriptCommandContainer.cs`
- Script parameters: `DS_Map\Script\ScriptParameter.cs`
- Script database: `DS_Map\Resources\ScriptDatabase.cs`
- Custom DB manager: `DS_Map\Resources\CustomScrcmdManager.cs`
- Script editor: `DS_Map\Editors\ScriptEditor.cs`
- Level scripts: `DS_Map\ROMFiles\LevelScriptFile.cs`
- Level script editor: `DS_Map\Editors\LevelScriptEditor.cs`

### Utility Files
- Loading form: `DS_Map\Editors\Utils\LoadingForm.cs`
- ARM9 tools: `DS_Map\DSUtils\ARM9.cs`
- Text converter: `DS_Map\DSUtils\TextConverter.cs`
- Overlay utils: `DS_Map\DSUtils\OverlayUtils.cs`
- YAML utilities: `DS_Map\DSUtils\YamlUtils.cs`

## Script System Architecture (Detailed)

### File Structure
```
DS_Map/
├── Editors/
│   ├── ScriptEditor.cs              # Main script editor UI with ScintillaNET
│   ├── LevelScriptEditor.cs         # Level script trigger editor
│   └── Utils/
│       └── LoadingForm.cs           # Progress bar with Pokemon facts
├── ROMFiles/
│   ├── ScriptFile.cs                # Binary/plaintext I/O, caching, hashing
│   ├── ScriptCommand.cs             # Command representation
│   ├── ScriptCommandContainer.cs    # Script/function containers
│   ├── ScriptAction.cs              # Action/movement commands
│   ├── ScriptActionContainer.cs     # Action containers
│   └── LevelScriptFile.cs           # Level script handling
├── Resources/
│   ├── ScriptDatabase.cs            # JSON database loader, reference data
│   ├── ScriptCommandInfo.cs         # Command metadata structure
│   └── CustomScrcmdManager.cs       # Custom database management UI
├── Script/
│   ├── ScriptParameter.cs           # Parameter type and formatting
│   ├── ScriptCommandPosition.cs     # Position tracking for navigation
│   └── ScriptLabeledSection.cs      # Section labels and organization
└── ScintillaUtils/
    └── ScriptTooltip.cs             # Syntax-highlighted tooltips
```

### Script Parameter Types
```csharp
enum ParameterType {
    Integer,              // Raw integer value
    Variable,             // Game variable reference (0x4000+)
    Flex,                 // Flexible size parameter
    Overworld,            // Overworld/NPC ID
    OwMovementType,       // Overworld movement type
    OwMovementDirection,  // Movement direction (Up, Down, Left, Right)
    ComparisonOperator,   // Comparison operator (==, !=, <, >, <=, >=)
    Function,             // Function reference (#1, #2, etc.)
    Action,               // Action/movement reference (#1, #2, etc.)
    CMDNumber,            // Script command number
    Pokemon,              // Pokemon species ID (friendly name: "Pikachu")
    Item,                 // Item ID (friendly name: "Potion")
    Move,                 // Move ID (friendly name: "Thunderbolt")
    Sound,                // Sound ID
    Trainer               // Trainer ID
}
```

### Script Workflow Diagram
```
ROM Load → Unpack NARC → Binary Scripts
                              ↓
                    Check Database Hash
                              ↓
                    ┌─────────┴─────────┐
                    ↓                   ↓
            Hash Matches         Hash Changed
                    ↓                   ↓
          Skip Re-export       Delete & Re-export All
                    ↓                   ↓
                    └─────────┬─────────┘
                              ↓
                  Export to Plaintext (.script)
                              ↓
                    Store Database Hash
                              ↓
        ┌───────────────┬─────┴─────┬────────────────┐
        ↓               ↓           ↓                ↓
   Edit in DSPRE   Edit in VSCode  Search       View Only
   (ScintillaNET)  (external)      Scripts      Read Cache
        ↓               ↓           ↓                ↓
   Auto-save to    Detect newer    Parse all    No re-parse
   plaintext       plaintext       (with cache)  (use cache)
        ↓               ↓           ↓                ↓
        └───────────────┴───────────┴────────────────┘
                              ↓
                       ROM Save Event
                              ↓
              Scan for Newer Plaintext Files
                              ↓
              Rebuild Binary from Plaintext
                              ↓
                      Pack into NARC
                              ↓
                    Save ROM Project
```

### Script Database Structure (JSON)
```json
{
  "commands": [
    {
      "id": 123,
      "name": "GiveItem",
      "parameters": [
        {
          "type": "Item",
          "name": "item",
          "size": 2,
          "description": "Item to give"
        },
        {
          "type": "Integer",
          "name": "quantity",
          "size": 2,
          "description": "Number of items"
        }
      ],
      "decompName": "ScriptCmd_GiveItem"
    }
  ],
  "movements": [...],
  "comparisons": [...],
  "specialOverworlds": [...],
  "overworldDirections": [...]
}
```
