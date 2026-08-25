# DSPRE 2.2 User Changelog

*Changes since [2.1](CHANGELOG_2.1_User.md).*

---

## 2.2.1
- Fixed a crash opening the TM/HM Bulk Editor on Diamond/Pearl ROMs. The editor tried to load every
  Pokémon form up through the ones Platinum introduced, but Diamond/Pearl's own data was never extended
  to include those forms, so opening the Bulk Editor (or selecting one of those forms, IDs 501-507, in
  the Pokémon Editor) threw a file-not-found error instead of working normally.
- Fixed newly-added custom Overworld entries on Diamond/Pearl (via hzla's Overworld Sprites Expansion
  patch) not showing up in the Overworld Editor or Event Editor dropdowns after being added. The entry
  was written correctly, it just wasn't appearing in the list, so it showed a placeholder image and a
  blank selection field instead of the one you just created. Already worked correctly on Platinum.

---

## 2.2
- New Overworld Watcher tab in the Research Helper: find every event using a given OW Entry ID, with double-click to jump straight to it.
- New Trainer Flag Bulk Editor (Other Editors menu): edit AI flags and Double Battle for many trainers at once, by trainer or by flag.
- Overworld Editor: Draw Type/Shadow/Footmark/Reflection editing and hzla's Overworld Sprites Expansion patch detection now also work on Diamond/Pearl (English), not just Platinum.
- Event Editor: jumping to an overworld NPC's script now follows Common/global scripts instead of just saying it couldn't be found.
- Research Helper's Script Watcher now also finds Common Script references (the CommonScript command and level script triggers), which it used to miss entirely.
- Event Editor: sorting the Overworlds list no longer marks the file as having unsaved changes when nothing actually moved.
- New Vs. Seeker Rematch Editor (Other Editors menu, Diamond/Pearl/Platinum English): view and edit each trainer's Vs. Seeker rematch chain (who they become at each rematch level) instead of needing a hex editor or the decomp source.
- New Trainer Watcher tab in the Research Helper: find every script command, overworld NPC, and Vs. Seeker rematch chain that references a given trainer, with double-click to jump straight to it.
- Battle Tower Editor, Vs. Seeker Rematch Editor, and the Rock Smash tabs are now disabled on hg-engine ROMs, since hg-engine owns that data itself.
- Event Editor: jumping to a map from elsewhere (Research Helper, etc.) no longer shows a black map view.
- Event Editor: selecting an overworld no longer marks the file as having unsaved changes by itself.
- Trainer Editor: new Prize Multiplier field in the Trainer Class Editor (Diamond/Pearl/Platinum/HeartGold/SoulSilver English), for the coefficient that scales how much money a trainer class pays out on defeat. Thanks to MrHam88 for the confirmed table locations.
- Battle Display: fixed the party icon's Palette Bank not actually being saved, it looked like it applied but reverted back the next time you loaded that Pokémon.
- Battle Display: new "Full palette" option when exporting a party icon PNG, so the file keeps all 16 real palette colors even ones the icon's own pixels don't use, instead of only whatever colors happen to appear in the image.
