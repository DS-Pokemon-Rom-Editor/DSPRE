# AGENTS.md

This file provides guidance to LLM agents when working with code in this repository.

## Project Overview

DS Pokemon ROM Editor (DSPRE) Reloaded is a C# application for editing Nintendo DS Pokemon ROM files. This is a major overhaul of the original DSPRE by Nomura with significant new features, performance improvements, and bug fixes. The editor supports multiple Pokemon games: Diamond/Pearl/Platinum (DPPt), and HeartGold/SoulSilver (HGSS).

DSPRE runs on .NET 8. It has a cross-platform **Avalonia** UI and a cross-platform ROM core, with a legacy Windows-only **WinForms** shell kept alongside during the porting transition.

## Build Commands

The solution builds with `dotnet` (or Visual Studio 2022+) on Windows and Linux.

### Build the application
```bash
dotnet build DS_Map.sln -c Release
```

### Build the cross-platform Avalonia shell only (Linux/macOS)
```bash
dotnet build DSPRE.Avalonia/DSPRE.Avalonia.csproj -c Release-Linux
```

### Run
- **Windows (legacy WinForms shell, default)**: `dotnet run --project DS_Map/DSPRE.csproj -c Debug`
- **Pure-Avalonia shell (any OS)**: `dotnet run --project DSPRE.Avalonia/DSPRE.Avalonia.csproj`
- The Windows exe can also be forced into the Avalonia shell with `DSPRE_AVALONIA_SHELL=1`.

### Test
```bash
dotnet test DSPRE.Tests/DSPRE.Tests.csproj
```
On non-Windows hosts the test project targets `net8.0` only; set `IncludeWindowsTests=true` to also compile the `net8.0-windows` target (requires `EnableWindowsTargeting=true`, which the project sets automatically).

### Build configurations
- `Debug` / `Release` — Windows defaults
- `Debug-Linux` / `Release-Linux` — build `DSPRE.Avalonia` against `linux-x64`

### Notes for non-Windows developers
- `DS_Map/DSPRE.csproj` is `net8.0-windows` but sets `EnableWindowsTargeting=true` off-Windows so the whole solution still builds (it just can't run there). It is the legacy shell; prefer `DSPRE.Avalonia` for day-to-day cross-platform work.
- Native helper binaries live in `DS_Map/Tools/`. Linux builds ship native versions (`dsrom`, `rotom`, `rotom-lsp`, `chatot`, `libnitroarc_ffi.so`) and fall back to the Windows `.exe` via Wine when a native binary is missing.

## Solution Structure

Six projects, organized into a cross-platform core, a cross-platform UI, and a legacy Windows shell:

| Project | Target | Role |
|---------|--------|------|
| **DSPRE.Core** (`DSPRE.Core/`) | `net8.0` | Cross-platform ROM core: file formats, ROM data model, script system, databases. No WinForms, no Avalonia. |
| **DSPRE.Avalonia** (`DSPRE.Avalonia/`) | `net8.0` | Cross-platform Avalonia UI (MVVM) + thin entry point. |
| **DSPRE** (`DS_Map/DSPRE.csproj`) | `net8.0-windows` | Legacy WinForms shell exe. Hosts the Avalonia UI alongside WinForms editors during the porting transition. |
| **Ekona** (`Ekona/`) | `net8.0` | Image/sprite processing library (formerly Tinke plugin host; WinForms controls stripped out). Also hosts `AppPaths`. |
| **Images** (`Images/Images/`) | `net8.0` | Nintendo DS image format handlers (NCGR, NCER, NCLR, etc.). |
| **DSPRE.Tests** (`DSPRE.Tests/`) | `net8.0` (+`net8.0-windows` opt-in) | xUnit test project. |

### Source-sharing via `.props` files (important)

Most source files still physically live under `DS_Map/` but are compiled into the cross-platform projects through two shared item-group files:

- **`CoreFiles.props`** — defines `@(CoreCompile)` / `@(CoreEmbeddedResource)`:
  - `DSPRE.Core.csproj` **includes** them.
  - `DS_Map/DSPRE.csproj` **removes** them (gets the core via `ProjectReference`).
  - Only files with no WinForms/Avalonia dependency belong here (ROMFiles, DSUtils, LibNDSFormats, Script, Narc, RomInfo, Filesystem, SettingsManager, databases, etc.).
- **`AvaloniaFiles.props`** — defines `@(AvaloniaLayerCompile)` / `@(AvaloniaLayerXaml)` / `@(AvaloniaLayerAsset)`:
  - `DSPRE.Avalonia.csproj` **includes** them.
  - `DS_Map/DSPRE.csproj` **removes** them.
  - Nothing in this set may reference WinForms.

A file's *physical* location (`DS_Map/...`) and its *compile* location (Core vs Avalonia vs WinForms shell) are decoupled on purpose. When adding a new file, decide which layer owns it and put it in the matching folder; update the relevant `.props` if you need to add a whole new glob. Files are expected to migrate physically under `DSPRE.Core/` and `DSPRE.Avalonia/` over time.

`DSPRE.Core` marks `DSPRE`, `DSPRE.Avalonia`, and `DSPRE.Tests` as `InternalsVisibleTo` so UI/test code can still reach internal core types from the one-project era.

## Architecture

### Core Components

#### ROM File System (`Filesystem.cs`, `Narc.cs`)
- **Filesystem**: Static utility class for ROM file operations, NARC packing/unpacking
- **Narc**: Handles Nitro Archive (NARC) files - the standard Nintendo DS archive format
- All ROM data is extracted to/from NARC archives for editing

#### ROM Data Model (`DS_Map/ROMFiles/`)
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

#### ROM Version Management (`RomInfo.cs`)
- **GameVersions**: Enum of individual game versions (DP, Pt, HGSS, etc.)
- **GameFamilies**: Groups of related games
- **DirNames**: Directory mapping for different ROM sections
- **gameDirs**: Static dictionary mapping sections to file paths per game
- **IsDsRomProject**: Detects whether the loaded project uses the ds-rom format (see "ROM Extraction and Building")

#### 3D Graphics System

3D rendering is split into format parsing (core) and GPU rendering (Avalonia):

- **Format parsing** (`DS_Map/LibNDSFormats/`) — cross-platform, no GL dependency:
  - **NSBMD** (`NSBMD/`): Nitro Polygon Model format — `NSBMDLoader` parsing, `MTX44` 4x4 column-major transforms
  - **NSBTX** (`NSBTX/`): Nitro Texture format with palette management
  - **NSBCA/NSBTA/NSBTP**: Animation formats (skeletal, texture, texture pattern)
  - **NSBUtils**: Merge models with textures, extract textures
  - **OBJWriter**: Export to Wavefront OBJ
  - **ModelUtils** (`DSUtils/`): Export to DAE (via Apicula) and GLB
- **GPU rendering** (`DS_Map/Avalonia/Gl/`) — modern, shader-based:
  - `GlFunctions`: Binds a minimal modern-GL function set (VBO/VAO/shaders) through Avalonia's `GlInterface.GetProcAddress`. This replaced the old fixed-function Tao.OpenGl/OpenTK stack.
  - `NsbmdGlControl`, `NsbmdGeometry`, `NsbmdTextureDecoder`: Render NSBMD models in an Avalonia `OpenGlControl`.
  - `MapGeometry`, `MatrixSceneBuilder`, `MatrixGridControl`, `PermissionGridControl`: Map/matrix 3D rendering.
  - `Mat4`: Column-major 4x4 matrix (successor to the old `MTX44`).

The legacy WinForms MapEditor is being replaced by the Avalonia `MapEditorView` / `MapEditorViewModel`. Camera behaviour is configured via `GameCamera` plus the `cam*` settings in `DspreSettings`.

#### UI Layer (Avalonia, MVVM)

`DS_Map/Avalonia/` contains the cross-platform UI, organized in MVVM fashion:

- **Views** (`Views/`): `.axaml` + `.axaml.cs` — one (or more) per editor.
- **ViewModels** (`ViewModels/`): View logic, dirty tracking, async saving, command binding.
- **App entry** (`App.axaml.cs`, `DSPRE.Avalonia/Program.cs`): `AvaloniaApp` boots either the pure-Avalonia `MainWindowView` or, on Windows, the WinForms host via `WinFormsHostHook`.
- **Editors** (`OpenEditors.cs`, `EditorWindowChrome.cs`, `EditorHostWindow`): Editor windows are registered and hosted in tabs/windows; `OpenEditorsRegistry` tracks open editors.
- **Scripting UX** (`ScriptSyntax.cs`, `SquiggleRenderer.cs`, `RotomLanguageServerClient.cs`, `AvaloniaEdit`/TextMate): AvaloniaEdit-based script editor with squiggles and diagnostics from the `rotom-lsp` language server.
- **Theming** (`ThemeManager.cs`, `Themes/`, `Avalonia.Fonts.Inter`, `Avalonia.Themes.Fluent`/`Simple`).
- **Dialogs** (`DialogHelper.cs`, `CoreDialogs.cs`, `PatchDialogs.cs`, `UnsavedChangesDialog.cs`): Native Avalonia dialogs wired as the user-message surface for core code (`RomInfo.ShowWarning`, `CoreDialogs.Install()`).
- **Misc**: `BattleSceneCompositor`, `OverworldSprites`, `TrainerClassSpriteRenderer`, `WeCellAnimRenderer`, `SpaParticlePreview`, `UndoHistory` (`ISupportsUndo`), `GuidedTour`, `WindowPlacement`, `Behaviors/`, `Controls/FusionAutoCompleteBox`.

The script editor in the Avalonia shell uses **AvaloniaEdit** + the **rotom** toolchain (formatter + LSP). The legacy WinForms `ScriptEditor.cs` still uses ScintillaNET during the bridge phase.

### Script System (Major Feature)

**Script Files and Formats** (`ROMFiles/ScriptFile.cs`):
- **Binary Format**: Original ROM format stored in NARC archives at `/fielddata/script/`
- **Plaintext Format**: Human-readable `.script` files exported to `expanded/scripts/`
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

**Script Database System** (`Resources/ScriptDatabase.cs`):
- **JSON-Based**: Script commands loaded from JSON database files
- **Version-Specific**: Separate command databases for Diamond/Pearl, Platinum, and HeartGold/SoulSilver
- **Custom Databases**: Users can load custom script command databases for ROM hacks
- **Database Hashing**: MD5 hash tracking detects database changes and triggers automatic re-export
- **Reference Data**: Built-in dictionaries for Pokemon, items, moves, sounds, trainers
- **Command Metadata**: Each command includes ID, name, parameter types, parameter names, descriptions

**Script Commands** (`ROMFiles/ScriptCommand.cs`, `Script/ScriptParameter.cs`):
- **ScriptCommand**: Individual script commands with ID and parameters
- **ScriptCommandContainer**: Groups related commands into scripts or functions
- **ScriptActionContainer**: Groups movement/action commands
- **Parameter Types**: 15+ types including Integer, Variable, Pokemon, Item, Move, Sound, Trainer, etc.
- **Smart Formatting**: Parameters displayed with friendly names (e.g., "Pikachu" instead of "25")

**Custom Database Management** (`Resources/CustomScrcmdManager.cs` WinForms; `Avalonia/ViewModels/CustomScrcmdManagerViewModel.cs` Avalonia):
- **Auto-Detection**: Scans scripts on load and prompts user to load a custom database if invalid commands are found
- **Database Storage**: Custom databases stored under `AppPaths.DatabasePath/edited_databases/` with naming `{romname}_scrcmd_database.json`
- **Reparse Support**: Reload database and reparse all scripts with progress tracking
- **Import/Export**: Share custom databases between users

**Database Hashing and Change Detection**:
- **Hash File**: `.database_hash` marker file in `expanded/scripts/` stores the MD5 hash
- **Automatic Detection**: On editor load, compares current database hash against the stored hash
- **Auto Re-export**: If database changed, deletes and rebuilds all plaintext scripts
- **Prevents Corruption**: Keeps scripts and database in sync

**Plaintext Caching**:
- **Performance Optimization**: Dictionary cache stores parsed plaintext scripts with timestamps
- **Avoids Re-parsing**: During batch operations (like search), uses cache instead of re-reading files
- **Cache Invalidation**: Timestamps validate whether cached version is still current

**rotom toolchain** (`Tools/rotom`, `Tools/rotom-lsp`, `DSUtils/RotomTool.cs`, `Avalonia/RotomLanguageServerClient.cs`):
- External `rotom` formatter and `rotom-lsp` language server provide syntax validation, squiggles, and diagnostics inside the Avalonia script editor via `AvaloniaEdit.TextMate` grammar (`Avalonia/TextMate/rotom.tmLanguage.json`).

**External Editing / VS Code Integration**:
- "Open in VSCode" launches Visual Studio Code against the scripts folder + the specific file
- Timestamp-based sync: DSPSE rebuilds binary from plaintext when the plaintext is newer (on load and on ROM save)
- Bidirectional: changes made externally are reflected on next load/save

**Script Export/Import Workflow**:
1. **Initial Load**: On first ROM open, all binary scripts exported to plaintext in `expanded/scripts/`
2. **Selective Export**: Existing plaintext files preserved (not overwritten) to maintain user edits
3. **External Editing**: User can edit `.script` files in VSCode or any text editor
4. **Auto-Rebuild**: On ROM save, DSPRE scans for plaintext files newer than binary and rebuilds them
5. **Binary Update**: Rebuilt binary scripts packed back into ROM NARC archive

**Progress Tracking** (`Editors/Utils/LoadingForm.cs`):
- `LoadingForm`: Progress bar dialog for long-running script operations (WinForms shell)
- Displays random Pokemon facts during loading
- Thread-safe real-time progress updates via the `Invoke` pattern

### Editor Framework

Editors exist in two parallel implementations during the porting transition:
- **Legacy WinForms editors** (`DS_Map/Editors/`): `UserControl`s and `Form`s (MapEditor, ScriptEditor, HeaderEditor, …). Referenced by the WinForms shell only.
- **Avalonia editors** (`DS_Map/Avalonia/Views/` + `ViewModels/`): MVVM equivalents (one view + one viewmodel per editor). Used by both the pure-Avalonia shell and the Windows shell.

Key editors (Avalonia views exist for all of these):
MapEditor, ScriptEditor, LevelScriptEditor, EventEditor, HeaderEditor (with HeaderSearch), MatrixEditor, EncountersEditor (DPPt/HGSS/BugContest/GreatMarsh/HoneyTree/Headbutt/SafariZone), TrainerEditor (+ classes + search + DV calculator), TextEditor, TradeEditor, EvolutionsEditor, LearnsetEditor (+ bulk), MoveDataEditor, PersonalDataEditor, PokemonEditor (+ sprite editor), ItemEditor (+ item table), OverlayEditor, AreaDataEditor, BannerEditor, BattleDisplay/BattleMessage/BattleScript editors, BtxEditor/NsbtxEditor, CameraEditor, EggMoveEditor, FlyEditor, LabelEditor, PatchToolbox, PickupTableEditor, SpawnEditor, TableEditor, TMEditor, CharMapManager.

Both shells share an unsaved-changes contract (`Editors/IEditorWithUnsavedChanges.cs`, `Avalonia/ISupportsUndo.cs`, `UnsavedChangesDialog`).

### Main Application
- **WinForms shell** (`DS_Map/Main Window.cs`, `DS_Map/Program.cs`): `MainProgram` MDI window; hosts the Avalonia UI inside it on Windows via `WinFormsShellHost`/`WinFormsHostHook`. Manages ROM project loading, editor lifecycle, preferences.
- **Avalonia shell** (`DSPRE.Avalonia/Program.cs`, `Avalonia/App.axaml.cs`, `Avalonia/Views/MainWindowView.axaml`): pure cross-platform entry point. Performs the same boot duties (load settings, init logger, copy bundled databases, wire `RomInfo.ShowWarning`/`CoreDialogs`, Velopack update check, Welcome/GuidedTour, "Open Default ROM").
- Uses **Velopack** for automatic updates (cross-platform: Windows installers and Linux AppImages).
- Settings persisted as JSON via `SettingsManager` (see Application Configuration).

### Architectural Patterns

1. **Abstract Base Class Pattern**: `RomFile` base class for all ROM data types
2. **Static Helpers**: `Helpers.cs` (rendering/UI/ROM ops), `Filesystem.cs` (ROM I/O), `AppPaths` (data locations)
3. **Stream-Based I/O**: Heavy use of `MemoryStream`, `BinaryReader`/`BinaryWriter`, `EndianBinaryReader`
4. **MVVM (Avalonia layer)**: ViewModels with commands + dirty tracking; views in `.axaml`
5. **Modern OpenGL via Avalonia**: shader/VBO/VAO rendering through `GlInterface.GetProcAddress` (no fixed-function pipeline, no Tao/OpenTK/HelixToolkit)
6. **Dual File Format**: Binary (ROM) + Plaintext (editing) for script files
7. **Source-shared projects**: `.props` files route the same on-disk sources into Core/Avalonia/WinForms-shell projects
8. **Caching with Validation**: Timestamp-based cache invalidation for performance

### External Dependencies

Key NuGet packages:
- **Avalonia 12.0.4** + **AvaloniaEdit 12.0** + **AvaloniaEdit.TextMate**: cross-platform UI and script editor (Avalonia layer)
- **Velopack**: application update framework (cross-platform)
- **LibGit2Sharp**: git integration
- **System.Text.Json** + **Newtonsoft.Json**: JSON serialization for settings and databases
- **YamlDotNet**: YAML parsing for ds-rom project files
- **System.Drawing.Common**: GDI image paths (WinForms-shell-only; cross-platform callers use `RawImage` twins in Ekona/Images)
- **ScintillaNET** (`jacobslusser.ScintillaNET 3.6.3`) and **Microsoft.WindowsAPICodePack**: kept only in the legacy WinForms shell during the bridge phase; will be removed once the Avalonia port of the ScriptEditor and file dialogs is complete.
- **xUnit**: tests

### File Locations

DSPRE uses specific directory structures within ROM files:
- Map data: NARC files in `/fielddata/land_data/`
- Scripts (binary): `/fielddata/script/` (NARC archive)
- Scripts (plaintext): `expanded/scripts/` (working directory, `.script` files)
- Events: `/fielddata/eventdata/`
- Encounters: `/fielddata/encountdata/`
- Text: `/msgdata/`
- Graphics: `/data/` (NSBMD, NSBTX files)

DSPRE user data locations (cross-platform, defined in `Ekona/AppPaths.cs`):
- App data root: `AppPaths.DspreDataPath` = `Environment.SpecialFolder.ApplicationData` / `"DSPRE"` (e.g. `~/.config/DSPRE` on Linux, `%APPDATA%\DSPRE` on Windows)
- Databases: `AppPaths.DatabasePath` = `DspreDataPath/databases`
- Settings file: `DspreDataPath/userSettings.json`
- Custom databases: `DatabasePath/edited_databases/` with naming `{romname}_scrcmd_database.json`
- Crash reports: `DspreDataPath/CrashReports/`
- Custom charmap: `DspreDataPath/charmap.json`
- Database hash marker: `expanded/scripts/.database_hash`

`DS_Map/Program.cs` still forwards `Program.DspreDataPath` / `Program.DatabasePath` to `AppPaths` for legacy call sites.

Game-specific paths are defined in `RomInfo.gameDirs`.

## Development Guidelines

### Code Style and Formatting

**IMPORTANT: Avoid Useless Comments**
- Do NOT write comments that simply restate what the code does
- Comments should only explain "why" the code exists, not "what" it does
- Only add comments when there's a non-obvious reason, tricky logic, or important context

### Layer discipline
- **DSPRE.Core**: no `System.Windows.Forms`, no `Avalonia.*` references. GDI (`System.Drawing.Common`) code may live here but is only reachable from the WinForms shell.
- **DSPRE.Avalonia**: no WinForms references.
- When you add a new cross-platform file, place it where its dependencies allow and make sure the right `.props` glob picks it up.

### ROM File Editing Pattern
When editing ROM data:
1. Load ROM project (unpacks to working directory)
2. Open NARC archives using `Narc.Open()`
3. Parse binary data into data structures (e.g., `MapFile.FromByteArray()`)
4. Modify data in memory
5. Serialize back using `ToByteArray()`
6. Save NARC using `narc.Save()`
7. Save entire ROM project to repack into ROM file

### Script Editing Pattern
When working with scripts:
1. **Loading**: ScriptFile automatically checks for plaintext version via `TryReadPlaintextIfNewer()`
2. **Editing**: User can edit in the Avalonia ScriptEditor (AvaloniaEdit + rotom LSP) or an external editor (VSCode)
3. **Plaintext Export**: First load exports all scripts to `expanded/scripts/{ID:D4}.script`
4. **External Changes**: DSPRE detects when plaintext files are newer than binary
5. **Rebuilding**: `RebuildBinaryScriptsFromPlaintext()` converts plaintext back to binary on save
6. **Database Changes**: Hash comparison triggers automatic re-export when database changes

### 3D Rendering Conventions
- Column-major matrices (`Mat4` in Avalonia/Gl; legacy `MTX44` in LibNDSFormats)
- Modern, shader-based pipeline (VBO + VAO + GLSL) bound through Avalonia's `GlInterface`
- Separate rendering paths for textured vs untextured models
- Camera position managed by `GameCamera`; user-tunable via the `cam*` settings

### Binary Format Handling
- Nintendo DS uses little-endian architecture (use `EndianBinaryReader` for big-endian sections)
- NARC format: BTAF (File Allocation Table), BTNF (Name Table), GMIF (File Image)
- Many formats use magic numbers for identification (e.g., "NSBMD", "NSBTX", "NARC")

### Editor State Management
- Editors implement `IEditorWithUnsavedChanges` (WinForms) / `ISupportsUndo` (Avalonia) for dirty tracking
- State saved via `SettingsManager` to `userSettings.json`
- User preferences include UI layout, rendering toggles, export paths

### Script Command System
- **Primary Database**: `Resources/ScriptDatabase.cs` with JSON loader
- **Custom Databases**: User-provided JSON files under `DatabasePath/edited_databases/`
- **Version-Specific**: Different command sets for DP, Platinum, HGSS
- **Parameters**: Parsed using `ScriptParameter` with 15+ parameter types
- **Smart Display**: Friendly names for Pokemon, items, moves, etc. from reference dictionaries
- **Variable Length**: Commands can have variable-length parameters

### Error Handling
- Structured exception handling with user-friendly messages
- `CrashReporter.cs` logs errors to `DspreDataPath/CrashReports/`; initialized at app start in both shells
- `AppLogger.cs` for application logging
- `AvaloniaErrorHandler.Install()` catches exceptions from async-void UI handlers so one editor throwing doesn't kill the process
- `correctnessFlag` in data structures tracks integrity
- **Script Validation**: Invalid commands detected on load with detailed error messages
- **Database Prompts**: User prompted to load a custom database when invalid commands are found

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
- **Building Rotation**: Building rotation patch

Patch data stored in `Resources/ROMToolboxDB/`. Logic in `PatchToolboxLogic.cs` (core), UI in `Editors/PatchToolboxDialog.cs` (WinForms) and `Avalonia/Views/PatchToolboxView.axaml` (Avalonia).

## Important Considerations

### Game Version Detection
Always check `RomInfo.gameFamily` or `RomInfo.gameVersion` as different Pokemon games have:
- Different file offsets and structures
- Different header formats
- Different script command sets (DP vs Pt vs HGSS)
- Different encounter table layouts
- Different event structures

### Performance
- Parallel processing used for ROM unpacking
- In-memory caching for frequently accessed data
- **Script Caching**: Plaintext scripts cached with timestamps to avoid re-parsing
- **Selective Export**: Only re-export scripts when database hash changes
- **Lazy Loading**: Plaintext only read if newer than binary

### Unsafe Code
The core uses `AllowUnsafeBlocks=true` for performance-critical binary operations.

### Tools Directory
External tools in `DS_Map/Tools/` (copied to output on build; Linux native variants provided, Wine fallback otherwise):
- **dsrom** (`dsrom.exe`): Primary ROM extraction and building tool (ds-rom format)
- **apicula.exe**: DAE export support
- **ndstool.exe** + **blz.exe**: Legacy ROM manipulation (kept for conversion from ndstool projects only)
- **nitroarc_ffi** (`nitroarc_ffi.dll` / `libnitroarc_ffi.so`): native NARC handling
- **chatot** (`chatot.exe`): audio handling
- **rotom** (`rotom.exe`) + **rotom-lsp** (`rotom-lsp.exe`): script formatter and language server
- **charmap.json**: Character encoding map
- **pokefatcs.txt**: helper data

## ROM Extraction and Building

DSPRE uses **ds-rom** as the default ROM extraction and building tool. The legacy **ndstool** format is still supported for conversion purposes.

### Project Formats

DSPRE supports two ROM project formats:

#### ds-rom Format (Current Default)
- **Tool**: `dsrom.exe` ([ds-rom project](https://github.com/Prof9/ds-rom))
- **Detection**: Presence of `header.yaml` and `config.yaml` files (i.e. `RomInfo.IsDsRomProject`)
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
     - moves `data/` → `files/`
     - moves `arm9.bin` → `arm9/arm9.bin`
     - moves `overlay/overlay_{ID}.bin` → `arm9_overlays/ov{ID}.bin`
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

Overlay utilities (`DSUtils/OverlayUtils.cs`) automatically resolve overlay paths based on project format:

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

Settings live in `DspreSettings` (`SettingsManager.cs`) and are persisted as JSON to `AppPaths.DspreDataPath/userSettings.json` (via Newtonsoft.Json). `DS_Map/App.config` remains only as a legacy WinForms shell artifact — it is **not** the source of runtime settings.

Key settings in `DspreSettings`:
- `menuLayout`: UI layout preference
- `lastColorTablePath`: User's palette path
- `textEditorPreferHex`: Text format preference
- `scriptEditorFormatPreference`: Script display format (binary or plaintext)
- `useDecompNames`: Option to use decompilation project names
- `automaticallyUpdateDBs`: Auto-sync online databases
- `automaticallyCheckForUpdates`: Velopack update check at boot
- `renderSpawnables`, `renderOverworlds`, `renderWarps`, `renderTriggers`: Event rendering toggles
- `exportPath`, `mapImportStarterPoint`: Import/export paths
- `openDefaultRom` / `neverAskForOpening`: ROM opening behavior
- `databasesPulled`: Online database sync status
- `convertLegacyText`: legacy text conversion toggle
- `rotomEditorTheme`: script editor theme (default `"OneDark"`)
- `camPanSpeed`, `camOrbitSpeed`, `camZoomSpeed`, `camInvert*`: 3D-view camera behaviour
- `showWelcomeOnStartup` / `guidedTourShown`: onboarding
- `mainWindowWidth` / `mainWindowHeight` / `mainWindowMaximized`: window placement
- `recentProjects`: most-recently-opened projects (capped at `MaxRecentProjects = 10`)

## Key File Paths

### Core Application Files
- WinForms shell entry point: `DS_Map/Program.cs`
- WinForms main window: `DS_Map/Main Window.cs`
- Avalonia shell entry point: `DSPRE.Avalonia/Program.cs`
- Avalonia app/window: `DS_Map/Avalonia/App.axaml.cs`, `DS_Map/Avalonia/Views/MainWindowView.axaml`
- App data locations: `Ekona/AppPaths.cs`
- Settings: `DS_Map/SettingsManager.cs`
- ROM file base: `DS_Map/ROMFiles/RomFile.cs`
- File system: `DS_Map/Filesystem.cs`
- NARC handler: `DS_Map/Narc.cs`
- ROM info: `DS_Map/RomInfo.cs`
- Helpers: `DS_Map/Helpers.cs`
- Crash reporting: `DS_Map/CrashReporter.cs`
- WinForms↔Avalonia host bridge: `DS_Map/WinFormsShellHost.cs`, `DS_Map/Avalonia/App.axaml.cs` (`WinFormsHostHook`)

### Script System Files
- Script file I/O: `DS_Map/ROMFiles/ScriptFile.cs`
- Script commands: `DS_Map/ROMFiles/ScriptCommand.cs`
- Script containers: `DS_Map/ROMFiles/ScriptCommandContainer.cs`
- Script parameters: `DS_Map/Script/ScriptParameter.cs`
- Script database: `DS_Map/Resources/ScriptDatabase.cs`
- Custom DB manager (WinForms): `DS_Map/Resources/CustomScrcmdManager.cs`
- Custom DB manager (Avalonia): `DS_Map/Avalonia/ViewModels/CustomScrcmdManagerViewModel.cs`
- Script editor (WinForms): `DS_Map/Editors/ScriptEditor.cs`
- Script editor (Avalonia): `DS_Map/Avalonia/Views/ScriptEditorView.axaml` + `ViewModels/ScriptEditorViewModel.cs`
- rotom LSP client: `DS_Map/Avalonia/RotomLanguageServerClient.cs`; rotom wrapper: `DS_Map/DSUtils/RotomTool.cs`
- Level scripts: `DS_Map/ROMFiles/LevelScriptFile.cs`
- Level script editor: `DS_Map/Editors/LevelScriptEditor.cs` / `DS_Map/Avalonia/Views/LevelScriptEditorView.axaml`

### Utility Files
- Loading form: `DS_Map/Editors/Utils/LoadingForm.cs`
- ARM9 tools: `DS_Map/DSUtils/ARM9.cs`
- Text converter: `DS_Map/DSUtils/TextConverter.cs`
- Overlay utils: `DS_Map/DSUtils/OverlayUtils.cs`
- YAML utilities: `DS_Map/DSUtils/YamlUtils.cs`
- Model export: `DS_Map/DSUtils/ModelUtils.cs`
- 3D GL rendering: `DS_Map/Avalonia/Gl/` (GlFunctions, NsbmdGlControl, MapGeometry, Mat4, …)

## Script System Architecture (Detailed)

### File Structure
```
DS_Map/
├── Avalonia/
│   ├── Views/
│   │   ├── ScriptEditorView.axaml        # AvaloniaEdit-based script editor
│   │   └── LevelScriptEditorView.axaml
│   ├── ViewModels/
│   │   ├── ScriptEditorViewModel.cs
│   │   └── LevelScriptEditorViewModel.cs
│   ├── ScriptSyntax.cs                   # syntax highlighting setup
│   ├── SquiggleRenderer.cs               # LSP diagnostics rendering
│   ├── RotomLanguageServerClient.cs      # rotom-lsp integration
│   ├── AvaloniaEditorLauncher.cs         # "Open in VSCode" etc.
│   └── TextMate/rotom.tmLanguage.json    # TextMate grammar
├── Editors/
│   ├── ScriptEditor.cs                   # legacy WinForms ScintillaNET editor (bridge phase)
│   ├── LevelScriptEditor.cs
│   └── Utils/LoadingForm.cs              # progress bar with Pokemon facts
├── ROMFiles/
│   ├── ScriptFile.cs                     # binary/plaintext I/O, caching, hashing
│   ├── ScriptCommand.cs
│   ├── ScriptCommandContainer.cs
│   ├── ScriptAction.cs
│   ├── ScriptActionContainer.cs
│   └── LevelScriptFile.cs
├── Resources/
│   ├── ScriptDatabase.cs                 # JSON database loader, reference data
│   └── ScriptCommandInfo.cs              # command metadata structure
├── Script/
│   ├── ScriptParameter.cs                # parameter type and formatting
│   ├── ScriptCommandPosition.cs          # position tracking for navigation
│   └── ScriptLabeledSection.cs           # section labels and organization
├── ScintillaUtils/
│   └── ScriptTooltip.cs                  # legacy WinForms ScintillaNET tooltips
└── DSUtils/RotomTool.cs                  # rotom / rotom-lsp process wrapper
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
   (AvaloniaEdit +  (external)      Scripts      Read Cache
    rotom LSP)
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
