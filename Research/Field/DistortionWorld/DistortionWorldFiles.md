[Research](../../ResearchNotes.md) / [Field Research](../FieldResearch.md) / [Distortion World Logic](DistortionWorldLogic.md) / Distortion World Files

# Distortion World Files, Platinum

Source: [pokeplatinum decomp](https://github.com/pret/pokeplatinum), the same names as [Distortion World Logic](DistortionWorldLogic.md); overlay 9 names are in `src/overlay009/ov9_02249960.c`. This was structured into a document with AI.

The byte layouts behind the Distortion World, for editing it by hand. Everything is little endian. Offsets inside overlay 9 are file offsets in the decompressed overlay; overlay 9 is stored uncompressed in Platinum and loads at RAM `0x02249960`, so a RAM address is that base plus the file offset, and every pointer inside the overlay's tables is such a RAM address. The overlay is the same in both US revisions of Platinum, and the archives are the same files. Other languages have not been checked.

Units: tiles are whole tiles on every axis, facing is 0 up, 1 down, 2 left, 3 right, and `fx32` is 20.12 fixed point.

## tw_arc.narc

`fielddata/tornworld/tw_arc.narc`, eleven members: the map info file, then one file per floor.

### Member 0: map info

A `u32` count, then that many 12 byte rows (`DistWorldMapInfo`, `src/overlay009/ov9_02249960.c`). The game searches it for the current header (`DistWorldMapInfoFile_FindForMap`) and loads the floor file at `mapFileIndex + 1`.

| Offset | Type | Field |
|---|---|---|
| +0 | u32 | header id |
| +4 | u16 | map file index; the floor file is this plus one |
| +6 | s16 | X offset |
| +8 | s16 | altitude offset |
| +A | s16 | Z offset |

The offsets place the floor's land data in the shared coordinate space, multiplied by the tile size (`src/overlay005/land_data.c`). The retail rows:

| Row at | Header | Floor | File | X | Altitude | Z |
|---|---|---|---|---|---|---|
| 0x04 | 573 | 1F | 1 | 21 | 288 | 10 |
| 0x10 | 574 | B1F | 2 | 0 | 256 | 35 |
| 0x1C | 575 | B2F | 3 | 15 | 224 | 0 |
| 0x28 | 576 | B3F | 4 | 47 | 192 | 21 |
| 0x34 | 577 | B4F | 5 | 57 | 160 | 34 |
| 0x40 | 579 | B5F | 6 | 57 | 128 | 34 |
| 0x4C | 580 | B6F | 7 | 56 | 114 | 38 |
| 0x58 | 581 | B7F | 8 | 74 | 64 | 32 |
| 0x64 | 582 | Giratina Room | 9 | 0 | 0 | 0 |
| 0x70 | 583 | Turnback Cave room | 10 | 70 | 64 | 30 |

The first row reads `3d 02 00 00 00 00 15 00 20 01 0a 00`.

### Floor files, members 1 to 10

A 20 byte header, then four sections back to back (`DistWorldMapFileHeader`; loaded by `DistWorldMapFile_Load`):

| Offset | Field |
|---|---|
| +0 | unused, 0 in every file |
| +4 | byte size of the floating platform section |
| +8 | byte size of the jump point section |
| +C | byte size of the camera section |
| +10 | byte size of the ghost prop section |

A size of 0 means the section is absent, and the header stays 20 bytes. The four sizes plus 20 are the file's length. The first three sections are a `u32` count followed by that many records.

| Floor | Platforms | Jump points | Camera regions | Ghost templates | Ghost triggers | Default visible groups |
|---|---|---|---|---|---|---|
| 1F | 0 | 0 | 0 | 18 | 0 | `0x2` |
| B1F | 1 | 4 | 4 | 21 | 0 | `0x1` |
| B2F | 5 | 4 | 4 | 18 | 0 | `0x1` |
| B3F | 1 | 4 | 6 | 49 | 74 | `0x008BFB40` |
| B4F | 2 | 4 | 6 | 24 | 47 | `0xF55` |
| B5F | 0 | 0 | 0 | 14 | 0 | `0x2` |
| B6F | 0 | 0 | 0 | 9 | 0 | `0x2` |
| B7F | 0 | 0 | 0 | 13 | 1 | `0x2` |
| Giratina Room | 0 | 0 | 2 | 11 | 0 | `0x1` |
| Turnback Cave room | 1 | 4 | 4 | 35 | 22 | `0x7A` |

#### Floating platforms, 20 bytes

`DistWorldFloatingPlatformTemplate`,.

| Offset | Type | Field |
|---|---|---|
| +0 | s16 | kind: 0 floor, 1 west wall, 2 east wall, 3 ceiling |
| +2 | u16 | `tw_arc_attr` member of its collision grid |
| +4 | s16 x3 | box start X, Y, Z |
| +A | s16 x3 | box size X, Y, Z; the box includes both ends |
| +10 | u16 | grid tiles vertical |
| +12 | u16 | grid tiles horizontal |

B1F's west wall reads `01 00 02 00 0b 00 02 01 30 00 00 00 03 00 09 00 20 00 20 00`: west wall, grid 2, from (11, 258, 48), size (0, 3, 9), a 32 by 32 grid. The retail platforms are B1F west wall grid 2; B2F floor sections on grids 4, 6, 5 and 7 and a west wall on grid 3; B3F west wall grid 8; B4F east wall grid 9 and ceiling grid 10; and the Turnback Cave room east wall grid 11.

#### Jump points, 40 bytes

`DistWorldFloatingPlatformJumpPointTemplate`.

| Offset | Type | Field |
|---|---|---|
| +0 | u16 | handler, must be 0 |
| +2 | s16 | facing the player needs |
| +4 | s32 | unused, 0 in retail |
| +8 | 12 bytes | box |
| +14 | s16 x3 | displacement X, Y, Z |
| +1A | s16 | sprite rotation, degrees |
| +1C | s16 | steps |
| +1E | u16 | hop axis: 0 X, 1 Y, 2 Z |
| +20 | u16 | 1 inverts the hop |
| +22 | s16 | facing after the jump |
| +24 | s16 | kind to land on; 4 is the ordinary ground |
| +26 | u16 | platform to land on; `0xFFFF` for none |

B1F's first reads `00 00 02 00 00 00 00 00 0c 00 01 01 31 00 00 00 00 00 00 00 ff ff 01 00 ff ff 5a 00 10 00 01 00 00 00 02 00 01 00 00 00`: facing left at (12, 257, 49), move (-1, 1, -1), turn 90 degrees over 16 steps hopping on Y, still facing left, onto west wall platform 0.

#### Camera regions, 24 bytes

`DistWorldCameraAngleTemplate`.

| Offset | Type | Field |
|---|---|---|
| +0 | 12 bytes | box |
| +C | u16 x3 | angle X, Y, Z, in 1.40625 degree units added to the base angle |
| +12 | s16 | facing |
| +14 | s32 | transition steps |

B1F's first reads `0e 00 01 01 31 00 00 00 00 00 00 00 14 00 2d 00 00 00 02 00 10 00 00 00`, turning to (0x14, 0x2D, 0) when entered facing left; its partner over the same box turns back to (0, 0, 0) facing right.

### Ghost props

The fourth section has no count of its own; it is a 12 byte header, the templates, then the triggers (`DistWorldGhostPropHeader`, `DistWorldGhostPropTemplate` and `DistWorldGhostPropTrigger`).

| Offset | Type | Header field |
|---|---|---|
| +0 | s32 | template count, at least 1 when the section exists |
| +4 | s32 | trigger count |
| +8 | u32 | groups visible on entry, one bit per group |

| Offset | Type | Template, 12 bytes |
|---|---|---|
| +0 | u32 | group, below 24 |
| +4 | u16 | prop kind |
| +6 | s16 x3 | tile X, Y, Z |

| Offset | Type | Trigger, 20 bytes |
|---|---|---|
| +0 | u32 | group |
| +4 | s16 | facing |
| +6 | s16 | 0 hides the group, anything else shows it |
| +8 | 12 bytes | box |

B1F's header reads `15 00 00 00 00 00 00 00 01 00 00 00` (21 templates, no triggers, group 0 visible), and its first template `00 00 00 00 01 00 17 00 01 01 3d 00` (group 0, kind 1, at 23, 257, 61).

## tw_arc_attr.narc

`fielddata/tornworld/tw_arc_attr.narc`, twelve members of 2048 bytes: one 32 by 32 grid of `u16` per platform, stored column major, entry `vertical + horizontal * tilesVertical`. Bit 15 blocks the tile and the low byte is its behaviour, the same masks as land data (`include/constants/field/map.h`); no other bits are set. Members 0 and 1 are all zero and unused. Behaviours in use: `0x08` cave floor on grid 8, `0x15` sea water on grid 10, and `0x5A` and `0x5B`, the two tile jumps north and south, on grid 7.

## tw_arc_etc.narc

`data/tw_arc_etc.narc`, 25 members, named in `res/prebuilt/data/tw_arc_etc.naix`.

| Members | Content |
|---|---|
| 0, 1, 2 | sky background graphics, palette and screen |
| 3 to 23 | seven clouds, each cell, graphics and animation |
| 24 | cloud palette, five palettes |

The prop models are not here but in `data/mmodel/fldeff.narc`: members `0x7C` to `0x94` for prop kinds 0 to 24, and animations `0xBF`, `0xC0`, `0xC1`, `0xC6` and `0xC8`.

## Overlay 9 tables

Values can be changed in place. Growing a list means writing it somewhere unused and repointing to it, and no free space in the overlay is known; tables marked fixed are bounded by a count compiled into the code.

| Table | Source | File offset | RAM | Size |
|---|---|---|---|---|
| Floor chain | `sDistWorldMapConnectionList` | `0x9744` | `0x022530A4` | 10 x 12, fixed |
| Coordinate trigger floors | `sMapEvents` | `0x93D8` | `0x02252D38` | 9 x 8, ends at header 593 |
| Trigger command handlers | `sEventCmdHandlers` | `0xA284` | `0x02253BE4` | 18 pointers |
| Moving platform floors | `sMovingPlatformsMapTemplates` | `0x92D8` | `0x02252C38` | 8 x 8, fixed |
| Elevator paths | `sElevatorPlatformPaths` | `0x9ED0` | `0x02253830` | 22 x 32, fixed |
| Object floors | `sMapObjectEvents` | `0x9554` | `0x02252EB4` | 11 x 8, ends at header 593 |
| Fixed props | `sSimplePropsMapTemplates` | `0x8BE8` | `0x02252548` | 5 x 8, ends at header 593 |
| Boulder falls and pits | `sBoulderFallLocations` | `0x97BC` | `0x0225311C` | 15 x 12, ends at header 593 |
| Prop models | `sProp3DModelNARCIndexByKind` | `0x9670` | | 25 x u32, fixed |
| Prop animation kinds | `sPropAnimInfoByKind` | `0x9870` | | 25 x 8, fixed |
| Prop starting offsets | `sPropInitialPosOffsetByKind` | `0x9938` | | 25 x 12, fixed |
| Prop culling scale | `sPropScaleByKind` | `0x9A64` | | 25 x 12, fixed |
| Prop behaviours | `sPropAnimFuncsByKind` | `0x960C` | | 25 pointers, fixed |
| Prop animation members | `sPropAnimSetNARCIndexByKind` | `0x7B44` | | 5 x u32, fixed |
| Platform bob | `sPlatformPropAnimOffsets` | `0x8884` | | 8 x fx32 |
| Camera start | `CameraInit` | `0x7B6C` | `0x022514CC` | 20 bytes |

The floor chain rows are current, previous and next header, in that order; 593 means none. The camera start reads `c1 ae 29 00 02 d6 00 00 00 00 00 00 00 00 c1 05 00 00`: distance, angle (-10750, 0, 0), perspective, field of view 1473.

### Coordinate triggers

Each `sMapEvents` row is a header and a pointer to that floor's list. A list is 16 byte triggers ending at one whose command pointer is null (`DistWorldEvent`):

| Offset | Type | Field |
|---|---|---|
| +0 | s16 | tile X |
| +2 | s16 | tile Y |
| +4 | s32 | tile Z |
| +8 | u16 | condition |
| +A | u16 | condition value |
| +C | pointer | command list |

Conditions: 0 none, 1 boulder puzzle unsolved, 2 solved, 3 progress equal to, 4 progress at most, 5 progress at least, 6 manual only, 7 Giratina shadow not yet seen, 8 Cyrus appearance equal to. Lists are at `0x86E4` (1F, 1 trigger), `0x8724` (B1F, 1), `0x9B90` (B2F, 24), `0x9278` (B3F, 2), `0x9318` (B4F, 3), `0x96D4` (B5F, 6), `0x9358` (B7F, 3) and `0x95AC` (Giratina Room, 5). B2F's first reads `21 00 e1 00 24 00 00 00 00 00 00 00 a4 20 25 02`: tile (33, 225, 36), no condition, commands at `0x022520A4`, which is file `0x8744`.

A command list is 8 byte entries, a `u32` kind and a pointer to its parameters, ending at kind 18:

| Kind | Command | Parameters |
|---|---|---|
| 0 | set an object's animation | u32 local id, u32 movement action |
| 1 | move a platform | 24 bytes: u16 platform, u16 carry the player, u16 unused, s16 x3 final offset, fx32 x3 speed per frame |
| 2 | add an object | u16 header, u16 local id |
| 3 | remove an object | u32 local id |
| 4 | cascade up | 36 bytes, as kind 10 |
| 5 | start a map script | u32 script number in the map's own file |
| 6 | set the progress variable | u32 value |
| 7 | show the Giratina shadow | 36 bytes: s16 x3 tile, s8 rotation set, u8 sound, fx32 x3 scale, fx32 x3 speed, s32 steps |
| 8 | set the Giratina animation flag | u32 0 or 1 |
| 9 | set a puzzle flag | u32 flag |
| 10 | cascade down | 36 bytes: u32 axis, u16 direction, s16 x3 finishing position fix, s16 x3 final offset, s16 x3 floor change offset, fx32 x3 speed |
| 11 to 16 | Giratina's arrival, the three boulder tutorials, show and hide the Giratina Room platforms | none |
| 17 | clear a puzzle flag | u32 flag |

The waterfalls use two command lists that no trigger list holds, B4F's at `0x7AD8` and B5F's at `0x7A78`, run from code on tiles x 104, y 170 or 128, z 76 to 79, facing right.

### Moving platforms and elevators

Each floor row points to a null terminated list of pointers to 24 byte templates (`DistWorldMovingPlatformTemplate`). The lists are at `0xA1B0` (1F), `0xA1EC` (B1F), `0xA2CC` (B2F, 18 platforms), `0xA224` (B3F), `0xA214` (B4F), `0xA204` (B5F), `0xA1C8` (B6F) and `0xA1A0` (B7F).

| Offset | Type | Field |
|---|---|---|
| +0 | u16 | index |
| +2 | s16 x3 | tile X, Y, Z |
| +8 | u16 | elevator path |
| +A | u16 | direction: 0 up, 1 down, 2 none |
| +C | u32 | template it becomes on the next floor |
| +10 | u32 | prop kind |
| +14 | u32 | persisted flag, 11 for always present |

1F's reads `00 00 28 00 21 01 36 00 00 00 01 00 00 00 00 00 02 00 00 00 0b 00 00 00`.

An elevator path is 32 bytes:

| Offset | Type | Field |
|---|---|---|
| +0 | u16 | index |
| +2 | u16 | next path; 22 for none |
| +4 | s16 x3 | final offset |
| +A | s16 x3 | offset at which the floor changes |
| +10 | fx32 x3 | speed per frame |
| +1C | u16 | flag to set, 11 for none |
| +1E | u16 | flag to clear, 11 for none |

Path 0 reads `00 00 16 00 00 00 e0 ff 00 00 00 00 f0 ff 00 00 00 00 00 00 00 c0 ff ff 00 00 00 00 00 00 0b 00`: no next path, 32 tiles down, change floors after 16, 4 per frame downward.

### Objects

Each `sMapObjectEvents` row points to a null terminated list of 40 byte records (`DistWorldObjectEvent`): `u16` condition, `u16` condition value, `u16` rotated, `u16` rotation angle, then an ordinary 32 byte object event with its Y as `fx32`. The lists are at `0xA1D4` (1F), `0xA1A8` (B1F), `0xA1F8` (B2F), `0xA1C0` (B3F), `0xA1B8` (B4F), `0xA268` (B5F), `0xA318` (B6F, 20 objects), `0xA238` (B7F), `0xA250` (Giratina Room) and `0xA1E0` (Turnback Cave room). Cynthia on 1F, at `0x8C88`, reads `04 00 02 00 00 00 00 00 81 00 8a 00 00 00 00 00 00 00 05 00 03 00 00 00 00 00 00 00 00 00 00 00 27 00 34 00 00 00 21 01`: progress at most 2, local id 129, graphics 138, script 5, facing right, at (39, 52), height 289.

### Fixed props

Each row of `sSimplePropsMapTemplates` points to 16 byte records ending at prop kind 25: `u32` unused, `u16` kind, `s16` x3 tile, `u16` condition, `u16` condition value. Retail has the 1F portal at (55, 289, 39), the B5F waterfall at (106, 153, 78), the Giratina Room portal at (15, 1, 12) once progress reaches 14, and the Turnback Cave room portal at (116, 65, 74).

### Tiles in code

The always blocked tiles are compare immediates in `DistWorld_DynamicMapFeaturesCheckCollision`: Giratina Room x 15 and z 26 at `0x506` and `0x50A` (`0f 2d`, `1a 2e`), B7F x 89 and z 56 at `0x51A` and `0x51E` (`59 2d`, `38 2e`), with the Giratina Room header 582 as a literal at `0x530` and B7F derived from it as 581.
