# DSPRE 2.1 User Changelog

*Changes since [2.0](CHANGELOG_2.0_User.md).*

---

## 2.1.4
- Battle Display: HeartGold/SoulSilver now reads and edits per-gender battle-sprite height, same as
  Diamond/Pearl/Platinum. It was previously always locked to one shared position for both genders, which
  wasn't accurate to how the game actually renders them.
- Battle Display: both genders' height fields stay visible even when a species only has a real sprite for
  one, instead of hiding the whole panel. Only the gender with an actual sprite is editable.
- Pokémon Sprite Editor: "Add Opposite Gender Sprites" now also copies the new gender's battle-sprite
  height, not just the sprite graphics, so it doesn't end up rendered at a different Y position than the
  gender it was copied from.

---

## 2.1.3
- Removed the fuzzy-search dropdown filtering added in 2.1. It caused real bugs (crashes, wrong item/species
  picked), so dropdowns are back to standard, native search-as-you-type behavior.
- Fixed a crash and an infinite loop in the Text Editor's Search and Replace when replacing a name with a
  version of itself that only differs by letter case, e.g. "PIKABLU" to "Pikablu".
- The Patch Toolbox's Sentence Case patch now works on Pokémon names you've renamed yourself, not just
  unmodified vanilla names, and also fixes that name everywhere else it appears in the game's text.
- Event Editor: added a way to manage the list of ground items Overworld Item events can use. Add or remove
  item + quantity entries, search items by name, quantity capped at 99. Entries still referenced by an
  Overworld event can't be removed.
- Patch Toolbox: the Standardize Items patch is now disabled for hg-engine ROMs, same as the other patches
  it doesn't support.
- Patch Toolbox: new "Convert Item names to Sentence Case" patch, same as the existing Pokémon one. Works on
  items you've renamed yourself too, and fixes that name everywhere else it appears in the game's text.

---

## 2.1.2
- Fixed a crash and a wrong-Pokémon-loaded bug when picking a match from a fuzzy-search dropdown (e.g.
  Pokémon Editor's species field).
- Fixed the NSBTX Editor's Area Data light type list being in the wrong order for Diamond/Pearl.

---

## 2.1.1
- Fixed a crash when leaving certain dropdowns bound to a fixed list, such as Trainer Editor party
  moves/items, Wild Encounter species, and Personal Data fields.
- Battle Display: fixed showing the wrong Pokémon's sprite.
- Battle Display: fixed the party icon always showing palette 0 instead of the palette the ROM actually
  has assigned.
- Battle Display: auto-picks the gender that actually has sprite data for mono-gender species.

---

## New Editors / Features

### Pokémon Sprite Editor: Add Opposite Gender Sprites
New "Add Opposite Gender Sprites" button. If a species only has sprite data for one gender (or is
genderless), it duplicates the existing sprites into the missing slots so widening its gender ratio in
Personal Data doesn't leave it with broken graphics. Same fix people have long done by hand in Tinke, now
one click.

### Overworld Sprite Table Expansion (WinForms)
"Add Custom Entry" for the overworld sprite table, previously Avalonia-only, is now in the WinForms
Overworld/BTX editor too. Lets you add new overworld sprite entries beyond the ROM's original table.

### Pokémon Editor: Battle Display tab
New tab for positioning a species on the battle screen:
- Front-sprite Y offset, shadow X offset, shadow size.
- Per-gender front/back sprite heights (Diamond/Pearl and Platinum).
- Live preview of the actual front/back battle sprites and shadow at their real position, with a
  Male/Female toggle and a frame picker for 2-frame sprites (mainly the HGSS send-out bounce).
- Party icon editing: pick the palette bank, Import/Export the icon graphic as PNG.

### Fuzzy search in dropdowns
Editable dropdowns across the app (Personal Data, Learnset, Evolutions, Trainer Editor, Wild Encounter
editors, Move Data editor, and more) now filter as you type, matching anywhere in the name and tolerating
typos, not just prefixes. Type "chomp" and Garchomp shows up.

---

## Changes to Existing Editors

### Patch Toolbox
- New Building Rotation patch: lets supported games read a building's rotation from map data so it can
  face any direction. Diamond, Pearl, Platinum, HeartGold and SoulSilver. Replaces the old "Overlay 1
  mark as compressed" patch slot.
- New synthetic-overlay offset picker, shared by Building Rotation and the Move ScrCommands Table patch.
  Lets you pick where a patch's payload gets written, shows the footprint, enforces 4-byte alignment, and
  warns if the range already has data in it.
- Move ScrCommands Table patch now uses that picker and can go anywhere in the synthetic overlay instead
  of a fixed spot.
- Fixed Dynamic Cameras' internal pointers so the patch works at a custom injection address.
- Building Rotation, Dynamic Cameras and Move ScrCommands Table now require a ds-rom-format project and
  prompt you to convert if you're still on ndstool.
- Fixed several synthetic-overlay patch ordering and messaging bugs found while building the above.

### Map Editor
- Building rotation fields are only editable once the Building Rotation patch is applied.
- Fixed a rotation axis-order bug that made rotated buildings face the wrong way.
- Texture preview now respects each material's actual alpha value (1-30) instead of forcing full opacity.
  House/signpost shadows and puddle tiles no longer show as solid black. Preview-only.
- Non-matching materials render as white instead of reusing whatever texture was bound before, so
  mismatches are easier to spot.

### Camera Editor
- Blocks legacy ndstool HGSS projects and asks you to convert to ds-rom format first.

### Overworld / BTX Editor (WinForms)
- Added the overworld sprite table expansion / "Add Custom Entry" feature (see above).
- Fixed a texture-corruption bug in that port where adding a custom entry could overwrite an existing
  NPC's texture.

### Trainer Editor
- Pickup Table Editor: fixed HeartGold rare-item offsets being off by one.
- Trainer class sprite preview: numeric up/down works now, and switching frames animates the preview.
- Added a failsafe for trainer index 0.

### Script Editor
- Hovering a goto-script jump target now shows a popup with context before you follow it.

### Text Editor
- Text archives can be exported as `.msg` files again.
- Export defaults to JSON.
- Text archives can be imported from `.bin` files.

### Rotom Project Support (early groundwork)
- Scripts are disabled when a Rotom-based project is detected, so DSPRE doesn't misread Rotom's script
  format as vanilla data. More Rotom integration is coming.

---

## Infrastructure Changes

- Added a canary/alpha build workflow that runs from any branch, for testing in-progress changes without
  waiting on a nightly or full release build.

---

## Known Limitations

- Rotom projects have scripts disabled as a safety measure; full script/event support isn't there yet.
- Building Rotation, Dynamic Cameras and Move ScrCommands Table need a ds-rom-format project.
- Battle Display is position-only: no battle-animation editing, no arena backdrop preview.
