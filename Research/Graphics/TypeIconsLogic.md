[Research](../ResearchNotes.md) / Type Icons

# Type Icons, Diamond/Pearl, Platinum and HeartGold/SoulSilver

Source: the [pokeplatinum](https://github.com/pret/pokeplatinum), [pokeheartgold](https://github.com/pret/pokeheartgold) and [pokediamond](https://github.com/pret/pokediamond) decomps, cited by file and function. This was structured into a document with AI.

[Battle Icons Logic](BattleIconsLogic.md) already covers the 32 by 16 type, contest and category pictures in the battle object archive: their member numbers, the shared palette, the palette bank each type uses and the Graphics Browser hooks. This page adds what the game keeps around them, where they appear, the other type coloured things that are not those pictures, and what happens to all of it when a type is added.

## Compression

The type, contest and category pictures and their shared cells and animation (Platinum and HeartGold members 242 and 243) are LZ10 compressed in the archive; the shared palette is not. A replacement has to be compressed the same way. The animation member is a generic one frame animation used by many sprites, not something specific to types.

## The tables that pick a picture and a bank

The member for each type and the palette bank it is drawn with are not in the archive. They are two tables in the ARM9, one after the other, with the same pair for the three move categories just before them (`src/type_icon.c` in Platinum, `asm/unk_0206E0F0.s` in Diamond, `asm/unk_02077678.s` in HeartGold):

| Game | Category banks | Category members | Type and contest members | Type and contest banks |
|---|---|---|---|---|
| Diamond | `0x020F83D0` | `0x020F83D4` | `0x020F83E0` | `0x020F843C` |
| Platinum | `0x020F0AE0` | `0x020F0AE4` | `0x020F0AF0` | `0x020F0B4C` |
| HeartGold | `0x02100038` | `0x0210003C` | `0x02100048` | `0x021000A4` |

These are RAM addresses; in the decompressed ARM9 the file offset is the address minus `0x02000000`. The member tables are `u32`, the bank tables `u8`. Each type table has 23 entries, the eighteen types and then Cool, Beauty, Cute, Smart and Tough, and the banks are `0 0 1 1 0 0 2 1 0 2 0 1 2 0 1 1 2 0` for Normal to Dark and `0 1 1 2 0` for the contest conditions. The categories use banks 0, 1 and 0 for physical, special and status. Changing which of the three shared palette banks a type uses is a one byte edit in the bank table.

## Where the game draws them

Each screen that shows a type picture loads the shared palette into its own sprite palette slots and adds the type's bank to a base slot of its own. In Platinum:

| Screen | Base slot | Where |
|---|---|---|
| Summary | 3 | `pokemon_summary_screen/sprites.c` |
| Bag, TM and HM types | 6 | `bag/sprites.c` |
| Move reminder | 2 | `move_reminder.c` |
| Battle party and summary menus, with contest pictures | 4 | `battle_party_sprites.c` |
| Battle move buttons | | `battle_cursor.c`, through `TypeIcon_NewTypeIconSprite` |
| PC box preview | 10 | `pc_boxes` |

HeartGold draws them from its battle input code, its battle sub menus, the PC, the bag and the move relearner through the same helpers.

## Things that are type coloured but are not the pictures

**The move buttons in battle.** The coloured background of each move button on the touch screen is not art in the archive. It is one of eighteen 16 colour palettes compiled into a battle overlay, chosen by the move's type and loaded into the button's palette row when the fight menu opens (`LoadMoveSelectPltt` in Platinum `src/overlay011/move_palettes.c`), plus a separate grey palette for an empty slot. The palettes are not stored in type order; a table of eighteen pointers picks them.

| Game | Overlay | Normal's palette | Pointer table |
|---|---|---|---|
| Diamond | 8 | `0x18900` | `0x18B84` |
| Platinum | 11 | `0x14C` | `0x3D0` |
| HeartGold | 6, compressed | `0x14C` | `0x3D0` |

The rows of the battle touch screen's palette file that these land in are placeholders; changing a move button's colour means changing the overlay. See [Bottom Screens](BottomScreens/BottomScreensLogic.md) for the rest of that screen.

**The Pokédex.** The Pokédex has type pictures of its own in `zukan.narc`: one palette (member 13), cells and animation (88 and 89) and one shared sheet (90), in Platinum and Diamond alike. A type selects an animation rather than a palette (`PokedexGraphics_GetAnimIDfromType` in Platinum's Pokédex `infomain.c`): Normal 0, Fire 1, Grass 2, Water 3, Electric 4, Rock 5, Fighting 6, Ghost and ??? 7, Ground 8, Steel 9, Poison 10, Bug 11, Dark 12, Ice 13, Flying 14, Psychic 15, Dragon 16.

## A nineteenth type

The pictures are the first eighteen entries of a 23 entry table, guarded by an assertion that only stops the game during communication. A type 18 therefore draws the Cool contest picture, 19 to 22 draw Beauty, Cute, Smart and Tough, and 23 or more reads past the tables. The move button palettes stop at eighteen, and the Pokédex lookup has no default case. A new type needs new entries in all of these, not only a picture. See [Type Chart Logic](../Moves/Types/TypeChartLogic.md#adding-a-type) for the rest.

## What DSPRE does

| What | Edited in DSPRE | Written | Left untouched |
|---|---|---|---|
| The eighteen type pictures and the shared palette | Graphics Browser, as in [Battle Icons Logic](BattleIconsLogic.md) | the battle object archive members | |
| Type pictures elsewhere in DSPRE | shown by `TypeIcons.For` in the Type Chart editor and wherever a move's type is shown; types 18 to 22 show the contest pictures, as the game does | | |
| ARM9 picture and bank tables | read (`BattleUiTables`) and used for each picture's bank when all 23 members match the archive's names, otherwise the retail banks | | untouched |
| Move button palettes | read from the battle overlay and shown on the Battle Screen editor's Fight menu | | untouched |
| Pokédex type pictures | | | untouched |
