[Research](../../ResearchNotes.md) / [Field Research](../FieldResearch.md) / Tile Permissions Logic

# Tile Permissions Logic, Diamond/Pearl, Platinum and HeartGold/SoulSilver

Source: the [pokediamond](https://github.com/pret/pokediamond), [pokeplatinum](https://github.com/pret/pokeplatinum) and [pokeheartgold](https://github.com/pret/pokeheartgold) decomps, cited by file and function. This was structured into a document with AI.

Every map tile carries one `u16` that says whether the player can step on it and what kind of ground it is: grass, water, a warp mat, a bookshelf. DSPRE calls the two halves collision and type. This covers the word itself, where it sits in a map's land data, every value each game gives a meaning, the HeartGold footstep sounds, and what DSPRE's painters edit.

## The tile word

The game reads a tile as one `u16` at index `z * 32 + x` of the map's 32 by 32 grid. The low byte is the behaviour (`TerrainCollisionManager_GetTileBehavior`) and bit 15 alone blocks the tile (`TerrainCollisionManager_CheckCollision`, both in Platinum `src/terrain_collision_manager.c`). Diamond, Pearl and Platinum read nothing else from the high byte, and retail maps only use `00` and `80` there.

HeartGold keeps bit 15 as the block, and uses bits 8 to 14 as a footstep sound, 0 to 15 (`sub_020548EC` in `asm/unk_02054648.s` returns the high byte with bit 15 masked off; the sixteen sounds are a table in `asm/unk_0205CB48.s`). A blocked tile can still carry a sound, and retail HeartGold maps use `81`, `82`, `84`, `85`, `86` and `8A` as well as `00` to `0D` and `80`.

## Where it is in a map

A land data file starts with four `u32` section lengths: permissions (always 2048), buildings, model and terrain heights. HeartGold then has a sound block, the signature `34 12`, a `u16` length and that many bytes. The permissions follow, 1024 `u16` values in the order above, the behaviour byte first.

| Game | Tile (x, z) is at |
|---|---|
| Diamond, Pearl, Platinum | `0x10 + (z * 32 + x) * 2` |
| HeartGold, SoulSilver | `0x14 + sound block length + (z * 32 + x) * 2` |

## What each value means

The names below are plain descriptions of what the game does with each value. Platinum's are from `include/constants/field/map_tile_behaviors.h` and the checks in `src/map_tile_behavior.c`, HeartGold's from `include/constants/metatile_behavior.h` and `src/metatile_behavior.c`. The Diamond decomp has no list; Diamond's values are Platinum's without the ones Platinum added, which the retail maps agree with. "No effect" marks a value the game registers but no code reads, and "(unused)" one that no retail map uses. "same" means the same meaning as the game to its left.

| Value | Diamond/Pearl | Platinum | HeartGold/SoulSilver |
|---|---|---|---|
| `00` | Ground | same | same |
| `02` | Tall grass | same | same |
| `03` | Very tall grass (no bike) | same | same |
| `06` |  |  | Headbutt tree |
| `08` | Cave floor, wild encounters | same | same |
| `0A` | No bike (unused) | same | same |
| `0B` | Indoor floor, wild encounters | same | same |
| `0C` | Rocky ground (no effect) | same |  |
| `10` | Pond water | same | same |
| `11` |  |  | Whirlpool |
| `13` | Waterfall | same | same |
| `15` | Sea water | same | same |
| `16` | Puddle | same | same |
| `17` | Shallow water | same | same |
| `1D` |  | Puddle, no splash | Puddle, no splash (unused) |
| `20` | Ice | same | same |
| `21` | Sand | same | same |
| `22` |  |  | Behind a waterfall |
| `23` |  |  | Safari Zone object |
| `24` |  |  | Safari Zone, no objects |
| `2C` |  | Reflective floor | Magma |
| `2D` |  | No Explorer Kit | Reflective floor |
| `2E` |  |  | No follower bubble |
| `30` | Blocks moving right | same | same |
| `31` | Blocks moving left | same | same |
| `32` | Blocks moving up | same | same |
| `33` | Blocks moving down | same | same |
| `34` | Blocks moving right and up | same | same |
| `35` | Blocks moving left and up | same | same |
| `36` | Blocks moving right and down | same | same |
| `37` | Blocks moving left and down | same | same |
| `38` | Jump right | same | same |
| `39` | Jump left | same | same |
| `3A` | Jump up (unused) | same | same |
| `3B` | Jump down | same | same |
| `3C` | No effect (3C) | same | Ladder up (walk up) |
| `3D` | No effect (3D) | same | Ladder up (walk down) |
| `3E` | No effect (3E) | same | Ladder down |
| `3F` | No effect (3F) | same |  |
| `40` | Slide right | same | same |
| `41` | Slide left | same | same |
| `42` | Slide up | same | same |
| `43` | Slide down | same | same |
| `44` | Push one step right (unused) | same |  |
| `45` | Push one step left (unused) | same |  |
| `46` | Push one step up (unused) | same |  |
| `47` | Push one step down (unused) | same |  |
| `48` | Fast slide (unused) | same |  |
| `49` | Blocks moving up and down | same | same |
| `4A` | Blocks moving left and right | same | same |
| `4B` | Rock Climb, up and down | same | same |
| `4C` | Rock Climb, left and right | same | same |
| `4D` |  |  | Stop sliding |
| `50` | Water current right (unused) | same |  |
| `51` | Water current left (unused) | same |  |
| `52` | Water current up (unused) | same |  |
| `53` | Water current down (unused) | same |  |
| `54` | No effect (54, unused) | same |  |
| `55` | No effect (55, unused) | same |  |
| `56` | Water gym floor, high | same |  |
| `57` | Water gym floor, middle | same |  |
| `58` | Water gym floor, low | same |  |
| `59` | Water gym water | same |  |
| `5A` |  | Jump 2 tiles up |  |
| `5B` |  | Jump 2 tiles down |  |
| `5C` |  | Jump 2 tiles left |  |
| `5D` |  | Jump 2 tiles right |  |
| `5E` | Stairs warp, right | same | same |
| `5F` | Stairs warp, left | same | same |
| `60` | Warp, then step down (unused) | same |  |
| `61` | Warp, keep facing (unused) | same |  |
| `62` | Warp mat, right | same | same |
| `63` | Warp mat, left | same | same |
| `64` | Warp mat, up | same | same |
| `65` | Warp mat, down | same | same |
| `66` | Pitfall warp (unused) | same |  |
| `67` | Warp panel | same | same |
| `68` | Sand geyser warp (unused) | same |  |
| `69` | Door | same | same |
| `6A` | Escalator, turns you around | same | same |
| `6B` | Escalator | same | same |
| `6C` | Warp right, no arrow | same | same |
| `6D` | Warp left, no arrow | same | same |
| `6E` | Warp up, no arrow | same | same |
| `6F` | Warp down, no arrow | same | same |
| `70` | Bridge start or end | same | same |
| `71` | Bridge over ground | same | same |
| `72` | Bridge over encounter ground | same | same |
| `73` | Bridge over water | same | same |
| `74` | Bridge over sand | same |  |
| `75` | Bridge over snow | same |  |
| `76` | Bike bridge up-down, over ground | same |  |
| `77` | Bike bridge up-down, over encounter ground | same |  |
| `78` | Bike bridge up-down, over water | same |  |
| `79` | Bike bridge up-down, over sand | same |  |
| `7A` | Bike bridge left-right, over ground | same |  |
| `7B` | Bike bridge left-right, over encounter ground | same |  |
| `7C` | Bike bridge left-right, over water | same |  |
| `7D` | Bike bridge left-right, over sand | same |  |
| `80` | Counter (talk across) | same | same |
| `83` | PC | same | same |
| `84` | Sign (unused) | same | same |
| `85` | Town map | same | same |
| `86` | TV | same | same |
| `87` | Sign 2 (unused) | same | same |
| `88` | Shelf (no effect) | same | Shelf (unused) |
| `89` | Slot machine (unused) | same | same |
| `8A` | Roulette (unused) | same | same |
| `8B` | Furniture (unused) | same | same |
| `8C` | Furniture 2 (unused) | same | same |
| `8D` | Fake door (unused) | same | same |
| `8E` | Notebook (no effect) | same | Notebook (unused) |
| `8F` | Survey (no effect) | same | Survey (unused) |
| `90` | Secret Power spot (unused) | same |  |
| `91` | Secret base entrance (unused) | same |  |
| `92` | Secret Power spot 2 (unused) | same |  |
| `A0` | Berry soil | same |  |
| `A1` | Snow | same |  |
| `A2` | Deep snow | same |  |
| `A3` | Very deep snow | same |  |
| `A4` | Mud | same | same |
| `A5` | Deep mud | same |  |
| `A6` | Mud with grass | same |  |
| `A7` | Deep mud with grass | same |  |
| `A8` | Light snow | same | same |
| `A9` |  | Snow with shadows | same |
| `D0` | Cycling Road (unused) | same |  |
| `D1` | Cycling Road 2 (unused) | same | same |
| `D7` | Bike ramp, right | same |  |
| `D8` | Bike ramp, left | same |  |
| `D9` | Sand slope, top (fast bike) | same |  |
| `DA` | Sand slope, bottom (fast bike) | same |  |
| `DB` | Bike stopper | same |  |
| `E0` | Small bookshelf | same | same |
| `E1` | Bookshelf | same | same |
| `E2` | Bookshelf 2 | same | same |
| `E3` | Pot (unused) | same | same |
| `E4` | Trash can | same | same |
| `E5` | Shop shelf | same | same |
| `E6` | Blueprint (unused) | same | same |
| `EA` | Small bookshelf 2 | same | same |
| `EB` | Shop shelf 2 | same | same |
| `EC` | Shop shelf 3 | same | same |
| `FF` | None | same | same |

Two values changed meaning between the games: `2C` is a reflective floor in Platinum and magma in HeartGold, and `2D` is "no Explorer Kit" in Platinum and a reflective floor in HeartGold. Sinnoh's `3C` to `3F` do nothing, while HeartGold's `3C` to `3E` are ladders.

Values that appear in retail land data: Platinum 666 maps, Diamond 578, HeartGold 676. Every one of them is in the table, including Platinum's `3C` to `3F` and Diamond's `3E` and `3F`, which no code reads.

## HeartGold footstep sounds

| Sound | Heard on | Sound | Heard on |
|---|---|---|---|
| 0 | default | 8 | tall plants |
| 1 | running | 9 | straw |
| 2 | leaves | 10 | rocky cave |
| 3 | twigs | 11 | hollow floor |
| 4 | soft grass | 12 | splashing |
| 5 | sand | 13 | wooden planks |
| 6 | hard floor | 14 | spare, unused |
| 7 | metal | 15 | spare, unused |

The collision byte is the sound, plus `0x80` if the tile is blocked.

## Changing one by hand

Find the land data member of the map, then the tile's offset from the table above. To make tile (5, 3) of a Platinum map tall grass that can be walked on, write `02 00` at `0x10 + (3 * 32 + 5) * 2 = 0xDA`. To block it, make the second byte `80`; in HeartGold, keep the sound in the low seven bits, so a blocked hard floor tile is `86`. Nothing else in the file changes size.

## What DSPRE does

| What | Edited in DSPRE | Written | Left untouched |
|---|---|---|---|
| Type, the low byte | the Map editor's Type painter, the Distortion World editor's Tiles painter and the map tileset editor, each offering every value this game gives a meaning, named for the game (`TilePermissions`) | the tile's low byte in the land data | |
| Collision, the high byte | the Collision painter: Walkable and Blocked in Diamond, Pearl and Platinum; every sound, walkable and blocked, in HeartGold | the tile's high byte | |
| Any other value | "Raw value" beside each painter paints a number as is; the grids and a hover line name any value, and one the game does not define shows as "Unknown (0xNN)" | | a value read from the file is never rewritten unless painted over |
| Warnings | an event or warp on open water is flagged using the game's surf water values only | | |

Colours on the grids follow the meaning, so the same kind of ground looks the same in every game.
