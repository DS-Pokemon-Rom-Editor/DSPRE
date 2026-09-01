# DSPRE 2.3 User Changelog

*Changes since [2.2](https://github.com/DS-Pokemon-Rom-Editor/DSPRE/blob/main/Changelogs/CHANGELOG_2.2_User.md).*

---

## 2.3

### Sprite Editor

- The Sprite Editor now shows one animation frame at a time, with 1 and 2 buttons beside each pose
  and an Animate button that cycles them. Poses that only ever had one frame drawn hide the buttons
  instead of offering a blank second frame.
- Added palette swatches under the Normal and Shiny sprites. Clicking a colour opens an editor with
  RGB and hex boxes, the colours you used recently, and slots you can pin your own colours to.
- Added sprite sheet import and export, for one gender or for both genders in a single image.
- Added an Import Wizard and an Export Wizard for moving several poses in or out at once.
- Alternate forms are now a dropdown next to the species picker instead of a separate mode you had
  to switch into, and picking one keeps the rest of the PokÃ©mon Editor on the matching entry.
- Fixed the editor reading the wrong data for the species whose default form is stored in the
  alternate forms archive rather than the main one: Deoxys, Unown, Castform, Burmy, Wormadam,
  Cherrim, Shellos, Gastrodon, Arceus, and depending on the game Shaymin, Rotom, Giratina and Pichu.
  Editing those species used to change bytes the game never reads, so nothing happened in game.
- The alternate form entries such as Deoxys Attack can now be picked by name from the species list.
- Fixed alternate forms showing the same artwork under both genders. A form of a genderless or
  single-gender PokÃ©mon now leaves the absent gender blank, matching what can actually be saved.
- Fixed saving an alternate form writing into the main sprite archive's unpacked folder instead of
  its own, which left the wrong entries on disk.
- Fixed the Egg and Bad Egg entries never matching a species, so they showed no sprite at all.
- Importing artwork whose colours do not fit the saved palette now warns that this also changes how
  the shiny sprites look, and lets you cancel.
- Removed the Load Sprite Set button, which was never wired to anything, and Load Sprite Sheet,
  which the sheet buttons now cover.

### Evolutions Editor

- The Evolutions Editor now lists the alternate form entries, with a note that forms have no
  evolutions of their own and that they belong on the base PokÃ©mon.

### Battle Display

- Removed the placeholder HP numbers from the gauge, which were never real values.

### ROM building

- Fixed building a ROM from a project that was extracted by a different DSPRE version. config.yaml
  now carries the padding fields every bundled ds-rom version expects, so the same project builds in
  both the current release and the Avalonia preview.
