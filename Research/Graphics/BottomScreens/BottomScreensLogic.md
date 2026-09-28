[Research](../../ResearchNotes.md) / Bottom Screens Logic

# Bottom Screens Logic, Diamond/Pearl, Platinum and HeartGold/SoulSilver

Source: the [pokeplatinum](https://github.com/pret/pokeplatinum), [pokeheartgold](https://github.com/pret/pokeheartgold) and [pokediamond](https://github.com/pret/pokediamond) decomps, cited by file and function. This was structured into a document with AI.

The touch screen is drawn differently in battle and on the field, and differently again in HeartGold. This covers where each part lives, how the layers and palettes are put together, what the game changes at runtime, the rules a replacement has to follow, and what DSPRE's Battle Screen and Bottom Screen editors show and save. Type pictures are in [Type Icons](../TypeIconsLogic.md) and [Battle Icons Logic](../BattleIconsLogic.md); the HP bars are on the top screen, not here.

## Battle

### Where the pieces are

The battle backdrop archive holds the touch screen too: `battle/graphic/batt_bg.narc` in Diamond, `pl_batt_bg.narc` in Platinum and `a/0/0/7` in HeartGold. The code is the battle touch input module, `src/battle/battle_cursor.c` in Platinum and `src/battle/battle_input.c` in HeartGold; Diamond's is still assembly (`arm9/overlays/11/asm/ov11_02258428.s`).

| Part | Diamond | Platinum | HeartGold |
|---|---|---|---|
| Tile sheet for every layer | 17 | 28 | 28 |
| Plain background | 37 | 49 | 43 |
| Command buttons, Fight, Bag, Pokémon, Run | 31 | 42 | 36 |
| Grey silhouette behind the command buttons | 35 | 47 | 41 |
| Move buttons and cancel | 32 | 43 | 37 |
| Target select | 33 | 44 | 38 |
| Target outlines | 36 | 48 | 42 |
| Yes and no | 34 | 45 | 39 |
| Battle video stop | | 46 | 40 |
| Palette, 16 rows | 195 | 242 | 246 |
| Background tint per backdrop, row 0 | | 243 + backdrop | 247 + backdrop |
| Touch and hold palette | 202 | 267 + backdrop | 271 + backdrop |

Frontier battles use their own sheet, background and palettes (Platinum 169, 170 and 340 to 341; HeartGold 173, 174 and 349 to 350). Backdrop 17 has its own tint and hold palettes (Platinum 284 and 285, HeartGold 288 and 289), and backdrops 18 to 22 have no tint. Some members in the same range are never loaded by the touch code, among them Platinum 29 to 41 and 50 and 51.

The sheet is 1024 tiles, but only the first 768 are loaded, and tile 767 is the blank every layer is cleared with. All of these are LZ10 compressed 16 colour files; the screens are 256 by 256.

### How the layers are put together

The touch screen is four 16 colour background layers sharing that one sheet. Each menu picks a screen for layers 0 to 2 and sets their order (the menu table in `battle_cursor.c` and `sBattleMenuTemplates` in `battle_input.c`):

| Menu | Layer 0 | Layer 1 | Layer 2 |
|---|---|---|---|
| Idle | | | background |
| Command | command buttons | silhouette | background |
| Safari, Pal Park | command buttons | | background |
| Fight | move buttons | | background |
| Target | target select | target outlines | background |
| Yes and no | yes and no | | background |

Layer 1 is blended over everything below it on every menu change, so the silhouette and the target outlines are always half transparent. The command menu slides in with the two top layers scrolling in opposite directions, and the background scrolls separately.

### Palettes, and what the game changes

The 16 row palette is loaded whole and cached, and reloaded on every menu change. On top of it:

1. **Row 0 is tinted per backdrop** in Platinum and HeartGold: its sixteen colours are replaced from the tint palette of the battle's backdrop, the same backdrop number that picks the scenery. The plain background only uses row 0, so this is what makes it match the scenery.
2. **Rows 8 to 11 are the move buttons, and their colours are not in the file.** Each row is loaded from a type palette compiled into a battle overlay, chosen by the move's type (`LoadMoveSelectPltt` in Platinum `src/overlay011/move_palettes.c`; HeartGold overlay 6; Diamond overlay 8). The file's rows 8 to 11 are placeholders. See [Type Icons](../TypeIconsLogic.md#things-that-are-type-coloured-but-are-not-the-pictures).
3. **Row 14 is the disabled grey.** An empty move slot, or a target that cannot be chosen, has its row replaced with row 14 and its tiles swapped for fixed spare tiles.
4. **Colour 1 of row 0 pulses** continuously.
5. **Holding the stylus** to advance battle text swaps part of row 0 for the hold palette.
6. **A pressed button is a different set of tiles, not a different palette.** Every button is drawn three times in the sheet, 192 tiles apart, and pressing it swaps the button to the third copy, then the second, then back.

Rows 1 to 4 are the command buttons, rows 5, 6, 12 and 13 the targets. The sprites on this screen, the party balls, the cursor, type pictures and every piece of text, are sprites and fonts drawn over the layers, not part of any screen file.

### Replacing it by hand

- Replace only the sheet, member 28 (17 in Diamond), keeping it LZ10 and 16 colour, with tile 767 blank and nothing that matters past tile 767.
- Keep each button's three copies exactly 192 and 384 tiles apart, and the disabled slot and target art at the tiles the code copies from.
- Keep screens 256 by 256, LZ10, with each button inside the fixed touch rectangles.
- Change the background's colour in the tint palettes, not only in row 0 of the main palette, which is overwritten on Platinum and HeartGold.
- Move button colours can only be changed in the overlay.

### What DSPRE does

The Battle Screen editor (`BattleScreenEditorView`, drawn by `BattleScreenRenderer`) composes the touch screen as the game does, behind the beta gate. A Menu picker shows no menu, Command, Fight, Target, Yes/No or, in Platinum and HeartGold, Playback, each with the layers and order of the game's menu table, and a Backdrop picker chooses the backdrop whose tint goes into row 0.

| What | Edited in DSPRE | Written | Left untouched |
|---|---|---|---|
| Tile sheet, member 28 (17 in Diamond) | "Open in Graphics" hands it to the Graphics window | the sheet member, recompressed as it was | |
| Screens and palettes of the touch screen | shown per menu; layer 1 blended over the layers below (8/16 over 12/16 in Platinum and HeartGold, 16/16 over 4/16 in Diamond); row 0 tinted from the chosen backdrop's tint palette | | untouched by this editor |
| Move button type palettes | read from the overlay; the Fight menu shows Normal, Fire, Water and Grass in rows 8 to 11 | | untouched |
| Sprites on the screen | | | untouched |

## Diamond, Pearl and Platinum on the field: the Pokétch

### Where the pieces are

`graphic/poketch.narc` has 126 members in both games, in the order of `res/graphics/poketch/poketch.order`; everything is LZ10 compressed except members 0, 1, 8, 9, 12 and 13.

| Member | Content |
|---|---|
| 0 | theme palette, 16 rows |
| 2, 3, 4 | shared digits: tiles, cells, animation |
| 10, 11, 12 | the "no Pokétch yet" screen: tiles, screen, palette |
| 13 | casing palette, 2 rows |
| 14, 15 | casing tiles and screen |
| 23 to 29 | the digital and analog watches |
| 30 to 125 | one block per app: screen, background tiles, cells, animation, sprite tiles |

Members 1, 8, 9, 58, 59 and 100 to 105 are never loaded. Eighteen members differ between Diamond and Platinum, among them the casing.

### Layers and palettes

The casing is layer 0 at the front, then a shutter layer, the app on layer 2, and text or calendar art on layer 3 (`poketch_graphics.c`). Nothing scrolls.

The theme palette has two rows per theme, green, yellow, orange, red, purple, blue, teal and white in that order, the second of each pair being the backlit version, used only by the two watches. Only colours 1, 4, 8 and 15 of a row carry the theme. The casing palette has one row for a girl and one for a boy, copied into row 15. The "no Pokétch yet" members 10 to 12 are also used by egg hatching, evolution, the Frontier records, the dress-up photos (overlay 22), record mixing (overlay 59), the Spear Pillar scene (overlay 100) and Spin Trade (overlay 109), and the naming screen takes the palette, member 12, so changing them changes those screens too. The decomp's evolution code names these member numbers after the diploma, but the numbers are 10, 11 and 12.

### Replacing it by hand

- Casing: every screen entry uses palette 15 and a tile of 64 or more, within 192 tiles, with the shutter and button tiles where they are. Platinum's casing fills its space exactly.
- Theme: change only colours 1, 4, 8 and 15 of each row.
- Apps: palette row 0, 16 colour, under 512 sprite tiles; five apps number their sprite tiles after the 80 shared digits.
- Recompress every member that was compressed.

### What DSPRE does

The Bottom Screen editor's Pokétch tab (`BottomScreenEditorViewModel`, with `PoketchApps`) shows the casing, the theme rows and every app, each on the layer the game uses (the calendar's art on layer 3). Each piece says which other screens share its members.

| What | Edited in DSPRE | Written | Left untouched |
|---|---|---|---|
| Palette colours | picked per colour, with undo | the palette member, at once, recompressed as it was | |
| Pictures | PNG import; an import that would leave more tiles than the piece has room for (the casing's 192, an app's sprite room) is put back and refused | the member, recompressed as it was | |
| Screens, cells, animations | shown | | untouched |
| Members 1, 8, 9, 58, 59, 100 to 105 | not offered, never loaded | | untouched |

## HeartGold and SoulSilver on the field

### The kinds of bottom screen

| Kind | Screen | Archive |
|---|---|---|
| 0 | touch menu | `a/0/1/4` |
| 1 | save screen | `a/0/1/4`, members 75 to 77 |
| 2 | shop | `a/0/6/0` |
| 3 | Poké Ball screen for yes and no and lists | `a/2/3/7` |
| 4 | Dowsing Machine | `a/2/5/1` |
| 5 | bank | `a/2/3/6` |

The Pokégear is not one of these; it is an application of its own that replaces the menu.

### The touch menu

`a/0/1/4` has 78 members, compressed except the palettes; its code, overlay 27, is still assembly. The panel is layer 0, tiles 8 and screen 9, with palette 7 loaded whole: the panel uses row 0, text uses row 4, and rows 0 to 3 double as the button sprite palettes. The menu icons are sprites, 20 tiles each: Pokédex 18, Pokémon 21, Bag 24 (27 for a girl), Pokégear 30, Trainer 33, Save 36, Options 39, then Retire 42, Chat 45 and Log 48, with shared cells and animation 16 and 17 and palette 14. Each icon has a sprite row of its own, loaded from row 1 for the selected icon and row 0 for the rest. The item frames, running shoes, light, A button and X mark are sprites from tiles 70, cells 68, animation 69. While a script or menu is busy, the shoes, light, item icons and frames go half transparent, and the icons too, except the selected one while the menu is open. The dimming is hardware blending, the sprite at 6/16 over the background and the words at 9/16, and never over another sprite. The panel, the A button, the X mark, and in the Bug Contest the ball and the caught Pokémon never dim.

Member 8 has to stay at 210 tiles or fewer, because the text windows start at tile `0xD2` in the same space, and the sprite space is nearly full.

### The Poké Ball screen

`a/2/3/7` has 18 uncompressed members: the ball on layer 2 (tiles 1, screen 9), the bars on layer 0 (screen 10 for yes and no, and for a list the screen whose number is the count of entries, 2 to 8), and text on layer 1 from tile `0x80`. Palette 0 loads five rows; the ball uses rows 0 and 2 and text row 4. The cursor is a sprite (tiles 12, palette 11, cells 13, animation 14). Member 1 has to stay at 128 tiles or fewer, because of the text. The evolution scene and two other screens reuse some of these members.

### What DSPRE does

The Bottom Screen editor's Menu and Choices tabs (`HgssTouchScreen` with `BottomScreenEditorViewModel.PiecesFor`) draw the touch menu and the Poké Ball screen from the game's own members, rows and positions, with the touch rectangles and A button label table taken from overlay 27. Toggles show the busy state (dimming exactly what the game dims), the menu open, registered items, the running shoes on or off, and the Bug Contest layout with RETIRE, a ball and a caught Pokémon; the A label picks CHECK, TALK, FISHING or NEXT in the ROM's own words.

| What | Edited in DSPRE | Written | Left untouched |
|---|---|---|---|
| Palette colours | picked per colour, with undo | the palette member, at once, recompressed as it was | |
| Pictures | PNG import | the member, recompressed as it was | |
| Screens, cells, animations, overlay 27 | shown | | untouched |

Not shown: the Bug Contest ball count and level text, the Safari, Pal Park and CHAT/LOG layouts, and a girl's BAG icon (member 27).

In every tab, Discard and Undo write the old bytes back, Save clears the editor's backups, and the members reach the ROM when it is saved.
