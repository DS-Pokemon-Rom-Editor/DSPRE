[Research](../../ResearchNotes.md) / [Field Research](../FieldResearch.md) / Underground Mining Logic

# Underground Mining Logic, Diamond/Pearl and Platinum

Source: the [pokeplatinum](https://github.com/pret/pokeplatinum) and [pokediamond](https://github.com/pret/pokediamond) decomps, cited by file and function, and the table read from the US retail ROMs (Diamond v05, Platinum revision 1). This was structured into a document with AI. HeartGold and SoulSilver have no Underground.

What a wall in the Underground can hold is one compiled-in table of 85 rows: each a treasure or a rock, with its shape, its picture and four weights. Which weight counts depends on the player's trainer ID and on the National Pokédex.

## The table

Each row is 20 bytes (`MiningObject` in pokeplatinum `src/underground/mining.c`):

| Offset | Content |
|---|---|
| `0x00` | pointer to the shape, or 0 for a solid rectangle |
| `0x04` | u16 weight, odd trainer ID |
| `0x06` | u16 weight, even trainer ID |
| `0x08` | u16 weight, odd trainer ID with the National Pokédex |
| `0x0A` | u16 weight, even trainer ID with the National Pokédex |
| `0x0C` | u8 width and u8 height, in 8-pixel tiles, two to a 16-pixel cell |
| `0x0E` | u8 id, then a padding byte |
| `0x10` | u16 picture member, u16 palette member |

| Game | Overlay | Table (decompressed) | Row count compares |
|---|---|---|---|
| Diamond | 18 | `0x17490` (`ov18_02250B30`) | `0x184E`, `0x187C`, `0x189E` |
| Platinum | 23 | `0x18D70` | `0x18A2`, `0x18D0`, `0x18F2` |

The count, 85, is compiled into three `cmp rN, #85`, one for each loop over the table. The first 71 rows are treasures and the last 14 are rocks, ids 60 to 66.

Rotated shapes are separate rows, and a rotation can have its own weights: Moon Stone 2 rows, Leaf Stone 2, Helix Fossil 4, Claw Fossil 4, Root Fossil 4, Old Amber 2 and Rare Bone 2. The treasure ids are 1 to 59 except 22, an unused Nugget; the Oval Stone has a row with every weight 0. Diamond and Platinum differ in seven rows, the small red, blue and green spheres, Revive and the four shards, and their weight columns total 1,014, 1,012, 1,001 and 1,005 in Diamond, 1,017, 1,015, 987 and 1,019 in Platinum.

## How a wall is filled

`Mining_GetWeightOfItem` picks the column: the trainer ID mod 2 chooses odd or even, and having the National Pokédex chooses the second pair. A wall gets 2 to 4 treasures, 3 on the very first dig, each a weighted pick over the rows up to the first row with id 60 (`Mining_GetTotalItemWeight`, `Mining_PickItem`). Rocks are then placed, up to 100 tries, picked evenly from the rows with ids 60 and above, which is why the rocks must stay last. A plate already dug, or picked twice for one wall, is skipped with a retry, so a column whose only weighted rows are plates the player has dug never finishes (`Mining_GenerateGameLayout`).

Treasure ids 11 to 59 are turned into bag items through a table of 49 u16 items in the ARM9 (`sMiningItems` in pokeplatinum `src/underground.c`; ARM9 `0x100920` in Platinum and `0x105C74` in Diamond, the same in both). Id 22 maps to Nugget.

## Shapes and pictures

The wall is 13 × 10 cells. A row with no shape fills its whole rectangle; otherwise the shape is one byte per cell, row by row, `width / 2` cells across: `x` for solid and `o` for empty (`Mining_AreCoordinatesWithinObjectShape`). The Heart Scale, for example, is `xo` over `xx`.

The picture is a drawing and a palette in `data/ug_parts.narc`, drawn over the whole rectangle row by row with the empty cells skipped (`Mining_DrawBuriedObject`). All sixteen plates share one drawing, 86, and differ only by palette; rotations share their palette.

## What DSPRE does

The weights are edited in the Underground Mining editor (`UndergroundMiningView`, through `MiningTable` in `DSPRE.Core/ROMFiles`), under the Items menu behind the beta gate, for Diamond, Pearl and Platinum; it refuses HeartGold.

| What | Edited in DSPRE | Written |
|---|---|---|
| Weights | the four columns for each row | the rows in place |
| Rows | fixed: the count, the order and the rocks' place are compiled in | |

DSPRE refuses a column whose only weighted treasures are plates, since the game would never finish filling a wall once they are dug.
