# DSPRE 2.0 Changelog

> This file is the original combined draft. It has been split into two focused changelogs:
>
> - **[CHANGELOG_2.0_USER.md](CHANGELOG_2.0_USER.md)** — for users: new editors, fixes, migration steps
> - **[CHANGELOG_2.0_DEV.md](CHANGELOG_2.0_DEV.md)** — for contributors: architecture, new systems, patterns, gaps

**Release Date:** TBD  
**Previous Version:** 1.14.2.4

---

## 🎉 Major New Features

### **Brand New Editors**

#### **Research Helper Tool** 🔬
A comprehensive research and analysis tool for ROM data exploration:
- **Scripts Tab**: View and analyze all script files with statistics (command count, size, complexity)
- **Level Scripts Tab**: Explore level scripts with detailed breakdowns
- **Variables Tab**: Track variable usage across scripts, level scripts, and event files
- **Flags Tab**: Monitor flag usage throughout the ROM
- **Headers Tab**: Quick header reference and research
- Advanced filtering and search capabilities for all data types
- Export research data to CSV for external analysis
- Helps identify unused content and analyze ROM structure

#### **Move Data Editor** ⚔️
Complete move editing capabilities:
- Edit all move properties (power, accuracy, PP, type, category, split)
- Battle effect descriptions with detailed tooltips
- Move flag editing (contact, protect-affected, magic coat, snatch, etc.)
- Range/target selection (single, both foes, all, etc.)
- Import/Export functionality for bulk editing via CSV
- Comprehensive validation for all move parameters
- Support for all Gen IV games (DP/Pt/HGSS)
- Integrated unsaved changes tracking

#### **Bug Contest Encounter Editor** 🐛
Edit Bug Catching Contest encounters (HGSS only):
- Manage all contest encounter sets
- Configure Pokémon species, levels, and encounter rates
- Rate system with detailed help documentation and formula explanation
- Import/Export contest data
- Visual feedback and proper validation
- Supports the unique Bug Contest encounter mechanics

#### **Great Marsh Encounter Editor** 🦆
Edit Great Marsh daily encounters (DPPt only):
- Configure daily rotating Pokémon species
- Manage all encounter slots for the Safari Zone-style area
- Import/Export marsh encounter data
- Game-specific validation
- Comprehensive tooltips explaining mechanics

#### **Honey Tree Encounter Editor** 🍯
Edit Honey Tree encounters (DPPt only):
- Manage encounter groups for different honey tree categories
- Edit species, levels, and encounter slots
- Import/Export honey tree data
- Full encounter configuration with validation
- Supports the complex honey tree encounter system

#### **Headbutt Tree Encounter Editor** 🌳
Edit Headbutt encounters (HGSS only):
- Visual tree placement editor with 3D map rendering
- Edit encounters for different tree types (normal and rare)
- Per-map configuration with tree visualization
- Interactive tree selection on the map
- Import/Export headbutt encounter data
- Support for multiple encounter groups per map

#### **Safari Zone Encounter Editor** 🦁
Edit Safari Zone encounters (HGSS only):
- Manage encounters for all 6 Safari Zone areas
- Configure Grass, Surf, Old Rod, Good Rod, and Super Rod encounters
- Object placement requirement system
- Per-area encounter configuration
- Import/Export safari zone data
- Full support for HGSS Safari Zone mechanics

#### **Pickup Table Editor** 💎
Edit Pickup ability item tables (HGSS/Pt):
- **Common Items**: 18 item pairs with level thresholds
- **Rare Items**: 11 rare item pairs
- **Activation Odds**: Configure pickup activation rates (9-level weight table)
- Visual datagrid interface for easy editing
- Adjustable activation divisor (must be multiple of 10)
- Tooltips explaining the pickup mechanics
- Support for Platinum and HGSS

#### **Hidden Items Editor** 🔍
Edit hidden item locations (HGSS only):
- Manage all hidden items in the game
- Configure item ID, quantity, and associated script
- Add, edit, and remove hidden items
- Dynamic capacity tracking
- Search and filter functionality
- Automatic script ID management

#### **Item Table Editor** 📦
Unified editor for item-related tables:
- **Pickup Table** sub-editor
- **Hidden Items** sub-editor
- Tab-based interface for easy navigation
- Game-version detection (automatically hides unsupported tabs)

#### **Trainer Battle Message Editor** 💬
Edit trainer battle dialogue (HGSS):
- Edit pre-battle, defeat, and post-battle messages
- Support for all 21 message trigger types
- Double battle message support (Trainer 1 and Trainer 2)
- Special messages: rematch, victory, last Pokémon, etc.
- Visual trainer preview with sprite rendering
- Pokemon DS font rendering for authentic text display
- Add, edit, and remove trainer messages
- Search functionality by trainer or message
- Import/Export message data

#### **Pokémon Form Sprite Editor** 🎨
Edit form-specific sprites for Pokémon with multiple forms:
- Support for DPPt and HGSS form sprites
- Edit sprites for Unown, Castform, Deoxys, Burmy, Wormadam, Shellos, Gastrodon, Rotom, Giratina, Shaymin, and Arceus
- Visual sprite preview with palette rendering
- Form selector for each Pokémon
- Proper form index handling per game version
- Integrated with main Pokemon Editor

---

## Major Infrastructure Changes

### **ds-rom Integration** 
Complete integration of the ds-rom library for improved ROM handling:
- Uses `dsrom` for all ROM packing/unpacking operations
- More reliable ROM extraction and repacking via better handling of ROM structure and metadata
- YAML-based ROM header management (`header.yaml`)
- Improved support for multiboot ROMs
- Fixes deadlocks when packing/unpacking HGSS ROMs

#### **ds-rom vs ndstool: Folder Structure Differences**
The extracted ROM folder structure produced by ds-rom differs significantly from the legacy ndstool layout:

**ndstool (legacy):**
```
MyROM_DSPRE_contents/
├── arm9.bin
├── arm7.bin
├── y9.bin            (overlay table - binary)
├── y7.bin
├── header.bin        (ROM header - binary)
├── banner.bin        (banner - binary)
├── data/             (game files)
└── overlay/          (overlays)
    ├── overlay_0000.bin
    ├── overlay_0001.bin
    └── ...
```

**ds-rom (new):**
```
MyROM_DSPRE_contents/
├── arm9/
│   ├── arm9.bin
│   └── arm9.yaml     (compression config)
├── arm7/
│   ├── arm7.bin
│   └── arm7.yaml
├── arm9_overlays/    (overlays - always decompressed)
│   ├── ov000.bin
│   ├── ov001.bin
│   └── overlays.yaml (overlay table - YAML, replaces y9.bin)
├── files/            (game files, replaces data/)
├── banner/
│   ├── banner.yaml
│   └── bitmap.png
├── header.yaml       (ROM header - YAML, replaces header.bin)
└── config.yaml       (build config)
```

Key behavioral differences:
- **Overlays and `arm9.bon` are always decompressed** while in the contents folder; ds-rom handles recompression automatically at pack time based on `overlays.yaml` settings
- **ROM header, banner, overlay table (`y9`/`y7`)** are now human-readable YAML files instead of binary blobs — much easier to inspect and edit manually
- **Game files live in `files/`** instead of `data/`
- **`path_order.txt`**: Controls the order of files in the ROM filesystem during packing. For maximum accuracy and compatibility (especially for clean repacks), replace the `path_order.txt` with one extracted from a **vanilla ROM of the same language** — this ensures the internal file order matches the original ROM exactly

### **Chatot Integration**
Integrated chatot library for text encoding/decoding:
- Replaces legacy character mapping system completely
- Uses `chatot` for all text conversion operations
- Character map format changed from XML (`charmap.xml`) to JSON (`charmap.json`)
- Significantly improved text parsing accuracy and performance
- Better support for special characters and international versions
- Proper handling of male (♂) and female (♀) gender symbols with prefix escaping
- Text archives automatically rebuilt from plaintext using chatot
- Fixed text length validation (Battle Frontier messages)
- Language-specific text encoding fixes

#### **Chatot Message Format Differences**
Chatot uses **JSON** instead of plain text (`.txt`) for message files:
- More robust parsing — resistant to whitespace and formatting issues that could corrupt legacy `.txt` files
- Better human-readable formatting
- Messages include **language keys** (e.g., `"en"`, `"ja"`, `"de"`) so a single file can technically contain text for multiple languages; the correct language is selected automatically based on the ROM's detected language
  - This opens the door for community translation projects to ship multi-language text archives in a single file

### **Unsaved Changes Tracking System** 💾
Brand new comprehensive system for tracking and managing unsaved changes:
- **`IEditorWithUnsavedChanges` interface** for standardized dirty tracking across all editors
- **`OpenEditorsRegistry`** for tracking all open standalone editor windows (Forms)
- **`UnsavedChangesDialog`** for user-friendly save prompts with details
- Editors now prompt before discarding unsaved changes when:
  - Switching to a different ROM
  - Closing the application
  - Loading a different file in the same editor
  - Opening a new ROM from file with extracted folder present
- Race condition fixes in editor loading
- Proper cleanup when switching ROMs

> ⚠️ **Not yet implemented** — the following editors do **not** have dirty tracking and will **not** warn before discarding unsaved changes:
> - **Header Editor**
> - **Map Editor**
> - **Building Editor**
> - **NSBTX Editor**

### **ARM9 Expansion Support** 🔧
- ARM9 expansion support for Japanese HeartGold and SoulSilver
- Proper compatibility checking for ARM9 expansion
- Improved ARM9 compression detection and handling
- Force ARM9 decompression for stability

### **Code Organization Improvements** 📁
Major refactoring of utilities and better separation of concerns:
- **DSUtils** refactored into specialized subdirectory:
  - `ARM9.cs`: ARM9 binary handling with Reader/Writer inner classes
  - `DSUtils.cs`: Core ROM utilities, NARC handling, EasyReader/EasyWriter
  - `NSBUtils.cs`: NSBMD/NSBTX model utilities
  - `ModelUtils.cs`: 3D model conversion utilities
  - `OverlayUtils.cs`: Overlay management and compression
  - `TextConverter.cs`: Character encoding (now uses chatot)
  - `YamlUtils.cs`: YAML parsing for ROM headers
- Better error handling and logging throughout codebase
- Improved `Filesystem.cs` with comprehensive accessor methods
- More consistent use of `RomInfo` for game-specific data

---

## ✨ Editor Enhancements

### **Trainer Editor** 👤
- **NEW: Battle Message Editor** - Complete dialogue editing system (HGSS)
  - Edit pre-battle, defeat, post-battle, rematch, victory messages
  - 21 different message trigger types
  - Visual trainer sprite preview
  - Pokemon DS font rendering
  - Add/edit/remove messages with full validation
- **NEW: Trainer Search by Name** - Search trainers by name with pop-up dialog
  - Text operators: Contains, Does Not Contain, Is Exactly, Is Not
  - Case-sensitive option
  - Live search results
- **AI Flag Tooltips**: Added detailed tooltips explaining each AI flag
- **Link to AI Documentation**: Direct link to trainer AI documentation for reference
- **DV Calculator Improvements**:
  - Fixed gender modifier calculation for Platinum AI Backport
  - Added support for Diamond/Pearl
  - Improved nature viewer
  - Better hidden power calculation
- Fixed crash when saving trainers with invalid ability slots
- Improved Pokémon party editing with reorder functionality (`MonReorderForm`)
- Added dirty tracking to prevent accidental data loss
- Better handling of trainer class and special scripts
- Fixed trainer name encryption/compression
- Fixed eye contact music writing
- Trainer name expansion check improvements

### **Script Editor** 📜
- **Message Preview on Hover**: Hover over message commands to see the actual text content in a tooltip
  - Shows the full message text inline
  - Helps with script debugging
  - Reduces need to switch to Text Editor
- Added support for HGSS-specific script commands (documented in `Resources/HGSSCommands.md`)
- Better command highlighting and autocomplete
- Improved script database with more accurate command definitions
- Alpha Ruins script command names added
- String replacements for special characters (gender symbols)
- Better handling of script parameters with tooltips

### **Event Editor** 🎯
- **Double-click Navigation**: Double-click any event in the list to jump directly to its associated action:
  - Spawnable → Opens the associated script
  - Overworld → Opens the associated script or shows trainer info
  - Warp → Jumps to destination header/map
  - Trigger → Opens the associated script
- Added special sprite variable actors to overworld dropdown
- Fixed issues with spawnable and warp editing
- Improved event positioning (map-relative and matrix-relative)
- Better validation of event coordinates
- Fixed warp bug (#164)
- Export/Import event file functionality

### **Header Editor** 📋
- **New Header Creation**: Option to automatically create supporting files when adding a new header:
  - Script file (empty script container)
  - Event file (empty event file)
  - Level script file
  - Text archive (empty message bank)
  - Saves time and prevents missing file errors
- Improved header validation and error checking
- Better handling of dynamic headers patch compatibility
- Fixed issues with new header workflows
- Better matrix ID and map ID management

### **Level Script Editor** 🎬
- Fixed hex value formatting issues (proper hex display)
- Improved trigger editing interface with better UI
- Better visualization of level script structure
- Support for all trigger types: variables, screen load, etc.
- Fixed issue where level script was added to dropdown twice

### **Wild Encounter Editor** (DPPt & HGSS) 🌿
- Complete reorganization for clarity
- **Encounters Tab** now contains game-specific encounter editors:
  - **DPPt**: Honey Tree, Great Marsh
  - **HGSS**: Headbutt, Safari Zone, Bug Contest
- Improved dirty tracking across all sub-editors
- Better layout and formatting
- Import/Export for all encounter types
- Visual encounter rate display
- Shellos/Gastrodon form support
- Unown table support with all variants

### **Text Editor** 📝
- Improved loading performance (removed debug statement that caused severe lag)
- Better handling of text archive rebuilding with chatot integration
- Fixed race conditions in loading
- Dual-format support (.bin and .txt) with priority for plaintext
- Automatic text conversion when saving ROM
- Fixed apostrophe stripping in text
- Gender symbol support (♂/♀)
- Better handling of text archive counts

### **Learnset Editor** 📚
- **Move Limit Warning**: Pop-up warning when adding too many moves to a learnset
  - Configurable threshold
  - Links to DS-Pokemon-Hacking wiki documentation
  - Project-level warning dismissal option (`.learnset_warning_dismissed` file)
- Improved import/export with better CSV formatting
- Edit mode vs view mode for safer editing
- Better validation of learnset data
- Move duplication checking
- Level threshold validation

### **Personal Data Editor** 🎴
- Added import/export functionality for bulk editing
- CSV-based data exchange with proper formatting
- Better validation of personal data values
- Type effectiveness editor
- Base stats, abilities, held items, EV yields
- Egg groups and catch rate
- TM/HM compatibility editor
- Item checkbox: "Prevent Toss & Hold" (updated text)

### **Pokémon Editor** 🎮
- Unified editor combining all Pokémon-related sub-editors:
  - Personal Data Editor
  - Learnset Editor
  - Evolutions Editor
  - Sprite Editor (NEW: with form support)
- **Sync Changes** checkbox to synchronize Pokémon selection across all tabs
- Aggregate unsaved changes tracking
- Better navigation between related data
- Proper bounds checking for form-based Pokémon

### **Evolutions Editor** 🔄
- Edit all 7 evolution slots per Pokémon
- Evolution method dropdown with descriptions
- Parameter editing (level, item, move, etc.)
- Target Pokémon selection
- Better labeling and tooltips

### **Map Editor** 🗺️
- **"Find Unused Collisions" button**: Utility to identify unused collision types in maps
- Fixed OpenGL/scaling issues on high-DPI displays
- Better 3D rendering performance and stability
- Improved building selection and editing
- Camera controls refinement

### **Matrix Editor** 🎛️
- Added dirty change tracking
- Removed unused matrix colors from vanilla games
- Improved save functionality with validation
- Fixed matrix height calculation issues
- Better map header association
- Spawn coordinates helper improvements

### **Building Editor** 🏢
- Better texture management (embedded vs external)
- Improved 3D rendering controls
- Interior/exterior building support
- NSBMD name display
- Export/import building models
- Registered with `OpenEditorsRegistry` for ROM switching

### **Camera Editor** 📷
- Visual previews for all camera angles
- Game-specific camera support (DPPt vs HGSS)
- Better camera angle selection interface

### **Fly Editor** ✈️
- Fixed fly table reading issues with decomp definitions (closes issue #9)
- Better handling of fly destinations
- HGSS vs DPPt format differences

### **Overlay Editor** 🔧
- Added informative messages for size mismatches
- Shows RAM addresses and overlay metadata
- Compression temporarily disabled until stability improvements
- Better overlay table reading
- HGSS-only feature (disabled for DPPt)

### **Spawn Editor** 🎯
- Fixed out-of-range crash when editing spawn points
- Better spawn coordinate management

### **Item Editor** 💎
- Edit all item properties: price, battle effects, field effects
- Sort parameters and hold effects
- Item icon preview
- Better item name management

### **TM/HM Editor** 📀
- Edit TM and HM move assignments
- Visual move selection
- Validation for move compatibility

### **Egg Move Editor** 🥚
- Edit egg moves for all Pokémon
- Move selection with validation
- Import/Export to CSV for bulk editing
- Egg move export moved to DocTool

### **Trade Editor** 🔄
- Edit in-game trades
- Pokémon selection for both sides
- Trainer ID and held item configuration
- Nickname editing

### **Table Editor** 📊
- **Conditional Music Table** (HGSS):
  - Edit header-specific music triggered by flags
  - Add/remove conditional music entries
  - Header and flag selection
- **Pokémon Battle Effects**:
  - VS Screen graphics and battle music combinations
  - Trainer class effect combos
  - Pokémon species effect combos
- **VS Trainer Effects** (HGSS/Pt)
- Better dirty tracking for all sub-tables
- Pokémon icon display in tables

### **NSBTX Editor** 🎨
- Texture viewing and editing for maps and buildings
- Better palette management
- Export/import textures

### **BTX Editor** 🖼️
- Standalone texture editor
- Texture replacement
- Palette editing
- Exit confirmation dialog

---

## 🛠️ Tools & Utilities

### **DocTool Enhancements** 📄
Comprehensive data export system for documentation, wikis, and external tools:
- **New CSV Exports**:
  - Event Spawnable data (type, position, script)
  - Event Overworld data with sprite IDs
  - Event File data complete
  - Wild held items per Pokémon (common and rare)
  - Map headers complete data
  - Scripts (structured format for analysis)
  - Learnsets (valid CSV format)
  - Egg moves
- **New JSON Exports**:
  - Encounters (complete encounter data in JSON)
- **Existing Exports Improved**:
  - Personal data (Pokémon stats)
  - Move data
  - Evolution data
  - Trainer data
  - TM/HM data
- **Separate Export Types**:
  - `generateCSVToolStripMenuItem`: Generate CSV exports
  - `generateDexExportsToolStripMenuItem`: Generate Pokédex-style exports
- Export destination selection dialog (choose where to save)
- Better export organization and folder structure
- Fixed prerequisites loading for event file exports
- All exports properly preload required NARCs

### **Debug Screenshot Tool** 📸
Debug-only tool for capturing application screenshots:
- Ctrl+Click on any form to capture screenshot
- Automatically saves to `Screenshots/` folder
- Timestamped filenames
- PNG format
- Useful for bug reports and documentation
- Only available in DEBUG builds

### **Patch Toolbox** 🔧
(Renamed from "ROM Toolbox" for clarity)
- Updated patch database and descriptions
- Fixed BDHCam patch compatibility handling
- Better detection of applied patches
- BDHCAM camera patch database with ROM-specific binaries
- Custom script command management
- Dynamic headers patch detection

### **STRVAR Help System** ℹ️
- Added comprehensive help dialog for STRVAR (String Variables) system
- Based on Discord community documentation
- Improved type clarifications and examples
- Better explanations for: PLAYER, RIVAL, ITEM, POKEMON, etc.
- Code examples for common STRVAR usage

### **Header Search Tool** 🔍
- Search headers by various criteria
- Quick navigation to specific headers
- Better than scrolling through hundreds of headers

### **Address Helper** 📍
- Helper for finding and managing ROM addresses
- Useful for debugging and research

### **Commands Database Viewer** 📖
- View all script commands with parameters
- Game-specific command lists (DP/Pt/HGSS)
- Export/import command databases
- Custom command management

### **Charmap Manager** 🔤
- Character mapping management (now uses JSON format)
- Visual character table editor
- Import/export character maps
- Note: Currently needs updates for chatot compatibility

### **NARC Utilities** 📂
- **Build from Folder**: Create NARC from folder of files
- **Unpack to Folder**: Extract NARC to individual files
- **Unpack All**: Unpack all game NARCs at once (with safety checks)
- Better error handling for missing NARCs

### **NSBMD Utilities** 🎨
- **Texturize NSBMD**: Add external texture to model
- **Untexturize**: Remove embedded textures
- **Extract NSBTX**: Export textures from model
- Better model format handling

### **Batch Rename Utilities** ✏️
- **List-based Rename**: Rename files using a text list
- **Content-based Rename**: Rename based on file contents
- Better validation and error messages

### **List Builder Utilities** 📋
- **From C Enum**: Generate lists from C-style enums
- **From Folder Contents**: Create lists from file names

---

## 🐛 Major Bug Fixes

### ROM Loading & Handling
- Fixed issue when opening ROM from file with extracted folder already present (#138)
- Fixed crash when force unpacking all NARCs if a NARC is missing (#99)
- Fixed deadlock when packing/unpacking HGSS ROMs
- Fixed multiboot ROM issues with updated ds-rom build (#162)
- Better ARM9 decompression handling and stability
- Fixed race conditions in ROM loading
- Improved ARM9 expansion compatibility checking
- Fixed overlay compression/decompression issues

### Editor-Specific Fixes

**Trainer Editor:**
- Fixed crash when trainer has a Pokémon with an ability slot it can't have (#90)
- Fixed crash when saving trainers with invalid data
- Fixed DV calculator gender modifier for Platinum AI Backport
- Fixed trainer name encryption and compression
- Fixed eye contact music writing

**Event Editor:**
- Fixed warp editing bugs (#164)
- Fixed spawnable positioning issues
- Better event validation

**Script/Text:**
- Fixed level script being added to dropdown twice (#86)
- Fixed hex parsing issues in various editors (#88)
- Fixed text length message for Battle Frontier (#31)
- Fixed apostrophe stripping in text
- Fixed script database string replacements

**Wild Encounters:**
- Fixed Wild Editor formatting for new features
- Fixed Headbutt encounter display

**Move/Item Data:**
- Fixed move dropdown consistency issues (#8, #95, #96, #97)
- Fixed item data validation

**Sprites:**
- Fixed form sprite saving with correct indexes (#128)
- Fixed icon palette table reading for German SoulSilver version (#144)

**Map/Matrix:**
- Fixed collision type changes and added new tile collisions
- Fixed matrix height calculation
- Fixed out-of-range crash in Spawn Editor (#82)

**Pokemon Data:**
- Fixed second ability slot bug for Pokémon with identical 1st and 2nd abilities
- Fixed Pokémon sync issues across editor tabs

**Level Scripts:**
- Fixed hex value handling in level scripts (#134)

**Fly Table:**
- Fixed wrong fly table reading with decomp definitions (#9)

**General:**
- Fixed issue where DSPRE might crash during ROM opening (#109)
- Fixed compatibility issues with decomp-based projects
- Fixed various out-of-bounds exceptions

### Performance & Stability
- **Removed performance-destroying debug statement** in text processing (#123)
  - Was causing severe lag and log spam
  - Dramatically improved text editor loading
- Fixed OpenGL rendering issues on modern displays and high-DPI screens
- Improved handler disable/enable logic to prevent event recursion
- Better memory management for large data sets
- Reduced log spam for better debugging
- Fixed various deadlocks and race conditions

---

## 🎨 UI/UX Improvements

### General Improvements
- Better error messages and user feedback throughout the application
- Improved tooltips with more detailed information and explanations
- More consistent icon usage across editors
- Better form layouts and control spacing
- Improved loading indicators and progress feedback
- Pokemon DS font rendering for authentic text display
- Better datagrid interfaces for tabular data

### Navigation Improvements
- Added "Go To" buttons for quick navigation between related data:
  - Event → Script
  - Warp → Destination
  - Overworld → Trainer
- Double-click support in list views for faster workflow
- Better search and filter capabilities across editors
- Quick header navigation tools

### Validation & Help
- More comprehensive validation with helpful error messages
- Inline help documentation in many editors
- Links to external documentation where appropriate (wiki links)
- Better handling of invalid data with recovery options
- Warning dialogs for potentially destructive operations
- Help buttons with detailed explanations (e.g., Bug Contest rate system)

### Visual Feedback
- Color-coded validation (errors in red, warnings in yellow)
- Progress bars for long operations
- Status messages in status bar
- Dirty indicators (unsaved changes notifications)
- Better sprite and icon rendering throughout

---

## 📚 Code Architecture Improvements

### New Core Systems
- **`IEditorWithUnsavedChanges` interface**: Standardizes dirty tracking across editors
- **`OpenEditorsRegistry`**: Centralized tracking of standalone editor windows
- **`UnsavedChangesDialog`**: Reusable dialog for prompting unsaved changes
- **`YamlUtils`**: YAML parsing for ds-rom header files

### New Data Models
- `BugContestEncounterFile.cs`: Bug Contest encounter data
- `GreatMarshEncounterFile.cs`: Great Marsh encounter data
- `HoneyTreeEncounterFile.cs`: Honey Tree encounter data
- `HeadbuttEncounterFile.cs`, `HeadbuttEncounterMap.cs`, `HeadbuttTree.cs`, `HeadbuttTreeGroup.cs`: Headbutt system
- `SafariZoneEncounterFile.cs`, `SafariZoneEncounter.cs`, `SafariZoneEncounterGroup.cs`, `SafariZoneObjectRequirement.cs`: Safari Zone system
- Better ROM file serialization/deserialization patterns
- More consistent use of `RomFile` base class with `ToByteArray()` and `Save()` methods

### Improved Utilities
- `Filesystem.cs`: More comprehensive file accessor methods
  - `GetXxxPath(id)` methods for all NARC types
  - `GetXxxCount()` methods for file counts
  - Direct properties for directory access
- `Extensions.cs`: More extension methods for common operations
- `Helpers.cs`: Improved UI helper methods
- Better separation between ROM access and UI code

### Refactoring Highlights
- DSUtils split into specialized classes (ARM9, NSBUtils, ModelUtils, OverlayUtils, TextConverter, YamlUtils)
- Better error handling with `AppLogger` throughout
- More consistent coding patterns across editors
- Reduced code duplication with shared base classes
- Better null checking and validation

---

## 🌍 Localization & Compatibility

### Language Support
- Fixed German SoulSilver palette reading
- Added ARM9 expansion for Japanese HeartGold and SoulSilver
- Better support for all Gen IV language versions:
  - English, Japanese, German, French, Italian, Spanish
- Improved character encoding for international characters
- Fixed gender symbol rendering (♂/♀) across all languages:
  - Now properly prefixed to avoid text parsing issues
  - Works in all text editors and displays
- Better text archive handling for different languages

### Game Version Compatibility
- Improved detection and handling of different ROM revisions
- Better support for unofficial ROM hacks and modifications
- Fixed compatibility issues with decomp-based projects
- HGE (hg-engine) compatibility maintained and improved
- Better version detection with YAML header parsing
- Game-specific feature detection (pickup table, hidden items, etc.)

### Cross-Game Features
- Editors properly adapt to game family (DP/Pt/HGSS)
- Game-specific tabs and features hide/show appropriately
- Better handling of version-specific data structures
- Consistent behavior across all supported games

---

## 📦 Dependencies & External Tools

### Updated Dependencies
- Migrated to proper NuGet package management
- Updated OpenTK for better 3D rendering and modern GPU support
- Updated ScintillaNET for code editing improvements
- **Added YamlDotNet** for YAML parsing (ds-rom headers)
- Various dependency version updates for security and compatibility

### New External Tools (in `Tools/` folder)
- **`dsrom` / `dsrom.exe`**: ROM packing/unpacking library (replaces ndstool for some operations)
- **`chatot` / `chatot.exe`**: Text encoding/decoding library
- Linux/macOS binaries included: `dsrom` and `chatot` (non-.exe versions)
- Automatic workflow integration to build ds-rom from fork

### Updated External Tools
- **`charmap.json`** (NEW): Character mapping in JSON format (8681 lines)
- **`charmap.xml`** (REMOVED): Old XML format (2908 lines) - replaced by JSON
- **`pokefacts.txt`**: Updated with corrections and additions
- **`ndstool.exe`**: Still used for some operations
- **`blz.exe`**: BLZ compression utility
- **`apicula.exe`**: 3D model conversion

### Font Resources
- **`pokemon-ds-font.ttf`**: Authentic Pokémon DS font for text rendering
- Used in Battle Message Editor and other text displays

---

## 📝 Documentation & Resources

### New Documentation
- `Resources/HGSSCommands.md`: HGSS-specific script commands reference
- Improved inline code documentation throughout
- Better tooltips with detailed explanations
- Enhanced README with clearer feature descriptions
- Added multiple README images for editors:
  - Camera Editor, Event Editor, Evolution Editor
  - Headbutt Editor, Header Editor, Map Editor
  - And more...

### Updated Documentation
- **`pokefacts.txt`**: Corrections and additions to Pokémon data reference
- Updated script command database with better descriptions
- More accurate command parameter descriptions
- Better examples in help dialogs

### Icon Resources
- New icon resources for new editors and features
- Better icon organization
- `MessageBox.png` for message-related features

---

## 🔄 Migration Notes

When upgrading from 1.14.2.4 to 2.0:

### ⚠️ Required Actions

1. **Character Maps**: 
   - The character mapping format has changed from `charmap.xml` to `charmap.json`
   - DSPRE 2.0 includes a default `charmap.json` compatible with chatot
   - If you have custom character maps, you'll need to convert them to JSON format
   - Charmap Manager may need updates to work with the new format

2. **Text Archives**:
   - Text archives are now processed through chatot
   - Existing plaintext `.txt` files will be automatically converted when the ROM is saved
   - The first save after upgrading may take slightly longer due to text conversion
   - **Recommendation**: Backup your ROM before first save with v2.0

3. **ROM Extraction**:
   - ROMs are now unpacked using ds-rom, which produces a **different folder structure** from ndstool (see ds-rom integration section above for the full comparison)
   - Key changes: `data/` → `files/`, `overlay/overlay_XXXX.bin` → `arm9_overlays/ovXXX.bin`, binary `header.bin`/`y9.bin` → `header.yaml`/`overlays.yaml`, overlays are always stored decompressed
   - Existing projects should remain compatible, but you will see new files and a reorganized folder layout
   - **Important**: If you want accurate ROM repacking (matching the original file order), replace your project's `path_order.txt` with one from a **vanilla ROM of the same language**
   - **If you encounter issues**: Re-extract your ROM from the original .nds file

4. **Tools Folder**:
   - New executables (`chatot.exe` and `dsrom.exe`) are required
   - These are included in the v2.0 release
   - Linux/macOS users: `chatot` and `dsrom` (non-.exe) binaries are also included
   - Ensure all tools are present in the `Tools/` folder

### 💡 New Behaviors

5. **Unsaved Changes Prompts**:
   - You'll now receive prompts when attempting to discard unsaved changes
   - This prevents accidental data loss but may be a change from previous versions
   - Make sure to save your work before switching ROMs or closing editors
   - You can save changes directly from the prompt dialog

6. **Editor Windows**:
   - Standalone editor windows (Forms) are now tracked for ROM switching
   - When you open a new ROM, all open editor windows will be prompted to save
   - You can no longer accidentally have multiple ROMs open in different editors

7. **Import/Export Formats**:
   - Many editors now support CSV import/export for bulk editing
   - CSV format may be slightly different from previous unofficial tools
   - Test with a single entry first before bulk importing

### 🆕 Optional Features

8. **New Editors to Explore**:
   - Many new editors are available (see "Major New Features" section)
   - Some are game-specific (e.g., Bug Contest is HGSS-only, Great Marsh is DPPt-only)
   - Explore the "Other Editors" menu to find them all

9. **Research Helper**:
   - Great for analyzing your ROM and finding unused content
   - Can export data for use in wikis or external tools

10. **DocTool Exports**:
    - Use `Tools → Generate CSV` for comprehensive data exports
    - Useful for documentation, wikis, and external analysis

### 🔍 Troubleshooting

If you encounter issues after upgrading:
- Check the `Logs/` folder for error messages (AppLogger creates detailed logs)
- Try re-extracting your ROM from the original .nds file
- Verify all tools in `Tools/` folder are present and not corrupted
- Check GitHub Issues for known problems and solutions
- Join the Discord for community support

---

## 🙏 Credits


- **The DS-Pokemon-Rom-Editor Team** for collaborative development
- **Community Contributors on GitHub**:
  - DevHam88 - README improvements and documentation
  - anastarawneh - Platinum AI Backport fixes
  - Mixone-FinallyHere - Various contributions, Great Marsh editor refinements
  - Chvlkie - Trainer name encryption/compression
  - YakosWG - Difficulty calculator improvements
  - KalaayPT - Gender symbol prefixing
  - And all other PR contributors!
- **Discord Community Members** for extensive testing and feedback
- **SDSME (Spiky's DS Map Editor)** developers for the original inspiration
- **Chatot Project** for text encoding/decoding library
- **ds-rom Project** for ROM handling library

### Special Recognition
- All the bug reporters who helped identify and fix issues
- Wiki contributors who documented features and workflows
- Beta testers who tested new editors before release
- Everyone who provided feedback on new features

---

## 📈 Statistics

- **158 files changed** with **33,837 insertions** and **5,915 deletions**
- **15+ new major editors** added:
  - Research Helper
  - Move Data Editor
  - Bug Contest Editor
  - Great Marsh Editor
  - Honey Tree Editor
  - Headbutt Editor
  - Safari Zone Editor
  - Pickup Table Editor
  - Hidden Items Editor
  - Item Table Editor
  - Trainer Battle Message Editor
  - Trainer Search
  - Form Sprite Editor
  - And more!
- **100+ bug fixes** and improvements
- **Major infrastructure overhaul** with ds-rom and chatot integration
- **Comprehensive unsaved changes system** across all editors
- **New data models** for all special encounter types
- **Enhanced import/export** capabilities across multiple editors

---

## 🔮 Looking Ahead

DSPRE 2.0 represents a **major milestone** in the evolution of DS Pokémon ROM editing. With:
- ✅ Improved infrastructure (ds-rom, chatot)
- ✅ Comprehensive editing capabilities (15+ new editors)
- ✅ Better user experience (unsaved changes tracking, tooltips, validation)
- ✅ Enhanced data exchange (import/export, DocTool)
- ✅ Stability improvements (bug fixes, performance)

We're excited to see what the community creates with these powerful new tools!

### What's Next?
Future development may include:
- More research and analysis tools
- Additional import/export formats
- Community-requested features
- Performance optimizations
- UI/UX refinements

### Get Involved
For feature requests, bug reports, or contributions:
- **GitHub**: https://github.com/DS-Pokemon-Rom-Editor/DSPRE
- **Issues**: Report bugs and request features
- **Pull Requests**: Contribute code improvements
- **Discussions**: Share ideas and ask questions
- **Discord**: Join the community for support and collaboration

---

## ⚠️ Important Notes

### Backup Your ROMs
**ALWAYS backup your ROMs before editing!** Version 2.0 includes major changes to ROM handling and text processing. While extensively tested, unexpected issues may occur.

### Major Version Bump Reasons
This is a **major version bump** (1.x → 2.0) due to:
1. **Breaking infrastructure changes** (ds-rom and chatot integration)
2. **New character map format** (XML → JSON)
3. **Significant architectural changes** (unsaved changes system, editor refactoring)
4. **15+ new editors** representing substantial new functionality
5. **100+ bug fixes** and improvements

### Compatibility
- ✅ Existing ROM projects should work with v2.0
- ✅ Text files (.txt) will be automatically converted
- ⚠️ Character maps need conversion from XML to JSON
- ⚠️ Some unofficial tools may need updates for new export formats

### Known Limitations
- Charmap Manager needs updates for JSON format
- Overlay Editor compression temporarily disabled (stability)
- Some editors only support specific games (DPPt or HGSS)
- Gen V support is limited (basic features only)

---

## 📄 Version Information

**Version**: 2.0  
**Previous Stable**: 1.14.2.4  
**Codename**: "Research & Refinement"  
**Build Date**: TBD  
**Target Framework**: .NET Framework 4.8  
**Supported Games**: 
- Pokémon Diamond (ADAE, ADAJ, ADAD, ADAF, ADAI, ADAS)
- Pokémon Pearl (APAE, APAJ, APAD, APAF, APAI, APAS)
- Pokémon Platinum (CPUE, CPUJ, CPUD, CPUF, CPUI, CPUS)
- Pokémon HeartGold (IPKE, IPKJ, IPKD, IPKF, IPKI, IPKS)
- Pokémon SoulSilver (IPGE, IPGJ, IPGD, IPGF, IPGI, IPGS)

---

## 🎊 Thank You!

Thank you to everyone who has supported DSPRE over the years. From the original SDSME to DSPRE 1.x, and now DSPRE 2.0, this project has grown thanks to the dedication of developers, testers, and the ROM hacking community.

**Happy ROM Hacking! 🎮✨**

---

*This changelog was compiled from git commits, code analysis, and issue tracking. If anything was missed or needs clarification, please open an issue on GitHub.*
