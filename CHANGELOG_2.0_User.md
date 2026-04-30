# DSPRE 2.0 — User Changelog

---

## New Editors

### Research Helper
Browse and analyze ROM data without editing anything. Useful for finding variable/flag usage, looking up script references, or just checking what's in a file before touching it.
- Tabs: Scripts, Level Scripts, Variables, Flags, Headers
- Filter and search across all tabs
- Export data to CSV

### Bug Contest Encounter Editor *(HGSS only)*
Edit Bug Catching Contest Pokémon pools, levels, and encounter rates.

### Great Marsh Encounter Editor *(DPPt only)*
Edit the daily rotating Pokémon in the Great Marsh.

### Honey Tree Encounter Editor *(DPPt only)*
Edit Honey Tree encounter groups, species, and levels.

### Pickup Table Editor
Edit the item table for the Pickup ability: common items, rare items, and activation odds.

### Hidden Items Editor *(HGSS only)*
Edit hidden item locations: item ID, quantity, and associated script. Add and remove entries up to the maximum capacity.

### Trainer Battle Message Editor
Edit trainer pre-battle, defeat, and post-battle dialogue. Supports all 21 message trigger types and double battle messages. Renders text using the actual Pokémon DS font.

### Pokémon Form Sprite Editor
Edit form sprites for Pokémon that have multiple forms (Unown, Rotom, Giratina, Shaymin, Arceus, etc.). Supports both DPPt and HGSS.

---

## Changes to Existing Editors

### Global
- Added functionality to allow loading a new ROM or folder without restarting the application, with prompts to save unsaved changes

### Trainer Editor
- Search trainers by name via a pop-up dialog (contains/exact/excludes, case-sensitive option)
- DV Calculator: fixed gender modifier for Platinum AI Backport; added Diamond/Pearl support
- Fixed crash when a Pokémon has an ability slot it can't have
- Fixed crash on save with invalid trainer data
- Fixed trainer name encryption/compression
- Fixed eye contact music writing
- AI flag tooltips added
- Link to trainer AI documentation added

### Script Editor
- Hover over a message command to preview the actual text in a tooltip
- String replacements for ♂/♀ gender symbols now work correctly

### Event Editor
- Double-click an event in the list to jump to its associated script, trainer, or destination header
- Press delete key to delete an event
- Shift + right click allows to duplicate an event
- Spawnable and warp editing fixes

### Header Editor
- When adding a new header, option to automatically create a script file, event file, level script, and text archive at the same time
- Fixed various issues with the new header workflow 

### Level Script Editor
- Fixed hex value display and parsing 
- Fixed level script being added to the dropdown twice

### Text Editor
- Fixed severe performance issue caused by a debug log statement left in the text processing path, text editor loading is dramatically faster
- Fixed race condition in the loading form
- Fixed apostrophe handling
- Gender symbol (♂/♀) support

### Learnset Editor
- Warning popup when a learnset exceeds the move limit, with a link to the DS hacking wiki
- Warning can be dismissed at project level

### Personal Data Editor
- Import/export via CSV
- Item checkbox label corrected to "Prevent Toss & Hold"

### Matrix Editor
- Removed unused matrix colors that aren't present in vanilla games

### Fly Editor
- Fixed wrong fly table reading when using decomp names

### Spawn Editor
- Fixed out-of-range crash

### DocTool (Export)
- New CSV exports: Event Spawnables, Event Overworlds, wild held items, map headers, learnsets, egg moves
- New JSON export: Encounters

### Patch Toolbox
- Fixed BDHCam patch compatibility detection
- Fixed ARM9 expansion compatibility check
- ARM9 expansion now available for Japanese
- Reset patch flags on ROM load

---

## Bug Fixes

- Fixed crash when opening a ROM from file with an extracted folder already present
- Fixed crash when force-unpacking all NARCs if one is missing
- Fixed deadlock when packing/unpacking HGSS ROMs
- Fixed multiboot ROM issue related to the downlaod play functionality (ask Yako for details)
- Fixed crash during ROM opening
- Fixed German SoulSilver icon palette table reading
- Fixed form sprite saving with correct indexes
- Fixed move dropdown inconsistencies
- Fixed second ability slot for Pokémon with identical 1st and 2nd abilities
- Fixed Battle Frontier text length validation
- Fixed crash after unpacking all NARCs
- Fixed wrong fly table reading with decomp definitions

---

## Infrastructure Changes

### ROM extraction now uses ds-rom instead of ndstool

The folder layout of your extracted ROM has changed:

| | ndstool (old) | ds-rom (new) |
|---|---|---|
| Game files | `data/` | `files/` |
| Overlays | `overlay/overlay_0000.bin` | `arm9_overlays/ov000.bin` |
| ROM header | `header.bin` (binary) | `header.yaml` (text) |
| Overlay table | `y9.bin` (binary) | `arm9_overlays/overlays.yaml` (text) |
| Banner | `banner.bin` (binary) | `banner/banner.yaml` + `bitmap.png` |
| ARM9 | `arm9.bin` | `arm9/arm9.bin` + `arm9/arm9.yaml` |

All overlays and the arm9 are stored decompressed in the folder. They are recompressed automatically when packing.

**If you want accurate repacking** (file order matching the original ROM), replace `path_order.txt` in your project with one from a vanilla ROM of the same language.

### Text encoding now uses chatot

- Character map format changed from `charmap.xml` to `charmap.json`
- If you have a custom charmap, you will need to convert it to JSON format
- Text archives are rebuilt from plaintext automatically when saving

### Unsaved changes prompts

Most editors now warn you before discarding unsaved changes when switching ROMs or closing.

**Editors that do NOT have this yet:**
- Map Editor
- NSBTX Editor
- Trainer Editor (partial: prompts on ROM switch but not when closing the editor form)
- Header Editor (partial: prompts on ROM switch but not when closing the editor form)

---

## Migration from 1.14.2.4

1. **Back up your ROM project before first use.**
2. The `Tools/` folder must contain `chatot.exe` and `dsrom.exe` (included in the release).
3. Custom `charmap.xml` files need to be converted to `charmap.json`.
4. Re-extract your ROM if you run into issues with the new folder structure.
5. The first save after upgrading may be slightly slower due to text conversion.

---

## Known Limitations

- Map Editor and NSBTX Editor do not warn about unsaved changes
- Charmap Manager UI needs updating for the JSON format
- Overlay Editor compression is disabled (stability)