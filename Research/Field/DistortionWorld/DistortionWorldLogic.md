[Research](../../ResearchNotes.md) / [Field Research](../FieldResearch.md) / Distortion World Logic

# Distortion World Logic, Platinum

Source: [pokeplatinum decomp](https://github.com/pret/pokeplatinum). Overlay 9 names are in `src/overlay009/ov9_02249960.c` unless another file is given. Names are the current upstream ones; older checkouts call many of the same things `UnkStruct_ov9_...` and keep the player movement code in `src/unk_0205F180.c`. This was structured into a document with AI.

The Distortion World is Platinum only. HeartGold keeps the battle backdrop and the map section name but has no field system for it, and its own field map code still carries the dead branch that used to select the overlay (`pokeheartgold/src/field/fieldmap.c`).

Almost none of it is ordinary map data. The maps are ordinary headers, but gravity, collision on walls and ceilings, the camera, the moving platforms, the objects and most of the scenery come from overlay 9 and two archives of its own. Each part below says what the game does, how to change it by hand, and what DSPRE does with it. The byte layouts are collected in [Distortion World Files](DistortionWorldFiles.md), which the hand editing notes point into.

Units used throughout: a tile is 16 world units, `fx32` is 20.12 fixed point (`0x1000` is 1.0), and facing is 0 up, 1 down, 2 left, 3 right (`include/location.h`). Every coordinate in the archives and the overlay tables is in whole tiles, Y included. Only the map object's own grid Y is kept in half tiles, which is why `GetPlayerPos` halves it (`src/overlay009/ov9_02249960.c`); an editor never meets that value in the data.

## The maps

Ten headers make up the place, 573 to 583 without 578:

| Header | Floor | Map file | Matrix | Matrix shape |
|---|---|---|---|---|
| 573 | 1F | `tw_arc` member 1 | 269 | 2 by 2 |
| 574 | B1F | 2 | 270 | 2 by 1 |
| 575 | B2F | 3 | 271 | 2 by 2 |
| 576 | B3F | 4 | 272 | 2 by 2 |
| 577 | B4F | 5 | 273 | 2 by 2 |
| 579 | B5F | 6 | 275 | 2 by 2 |
| 580 | B6F | 7 | 276 | 2 by 2 |
| 581 | B7F | 8 | 277 | 1 by 2 |
| 582 | Giratina Room | 9 | 278 | 1 by 1 |
| 583 | Turnback Cave room | 10 | 279 | 2 by 2 |

The first eight are a chain, 1F down to B7F, held in `sDistWorldMapConnectionList` (`src/overlay009/ov9_02249960.c`); the Giratina Room and the Turnback Cave room have no neighbours. Headers 570 and 578 carry the same label and music, with matrices 268 and 274, but appear in no table of the overlay. Header 578 does have scripts of its own, and they run the Distortion World setup command (`InitPersistedMapFeaturesForDistortionWorld`), so how it is reached is an open question.

The headers themselves are unremarkable. They share `area_data_074`, say `MAP_TYPE_CAVE`, have clear weather, and the one field specific to the place is `.battleBG = BACKGROUND_DISTORTION_WORLD` (`include/data/map_headers.h` for 1F). Only 1F has an events file; the other nine point at `events_empty`, because their objects live in the overlay (see "Objects"). None of the matrices has a header list or an altitude list.

A floor's matrix can be at most two cells wide and two tall. The land data loader fills its four slots from a fixed two by two block rather than following the player (`src/overlay005/land_data.c`), so a wider matrix would load the wrong cells.

**By hand.** The header, matrix and land data are ordinary files and edit the ordinary way. The one rule that is not ordinary is the two by two limit above.

**In DSPRE.** The Distortion World editor lists the ten floors from `tw_arc` member 0 and builds each floor's 3D scene from its header, matrix and land data (`BuildScene` in `DistortionWorldViewModel`). Headers, matrices, land data and events are edited in their usual editors; the map model of a floor can also be edited from inside the Distortion World editor, which saves it as ordinary land data.

## What makes a map part of it

Not the header. The test is a value in the save, the persisted map feature id, which is 9 for the Distortion World (`DYNAMIC_MAP_FEATURES_DISTORTION_WORLD`, `include/constants/field/dynamic_map_features.h`):

```c
static BOOL FieldMap_InDistortionWorld(FieldSystem *fieldSystem)
{
    PersistedMapFeatures *v0 = MiscSaveBlock_GetPersistedMapFeatures(FieldSystem_GetSaveData(fieldSystem));
    int v1 = PersistedMapFeatures_GetID(v0);

    if (v1 == DYNAMIC_MAP_FEATURES_DISTORTION_WORLD) {
        return TRUE;
    }

    return FALSE;
}
```

`src/overlay005/fieldmap.c`. The same id selects overlay 9. It is written by the floor's own transition script, the same way on all ten floors:

```
DistortionWorld1F_OnTransition:
    InitPersistedMapFeaturesForDistortionWorld
    End
```

`res/field/scripts/scripts_distortion_world_1f.s`, command at `src/scrcmd.c`.

**By hand.** A map joins the Distortion World by running that command in its on transition level script, and by having a row in `tw_arc` member 0 and in the overlay tables that expect it. The command alone switches the overlay on; without a map info row the overlay has nothing to load.

**In DSPRE.** DSPRE treats a header as part of it when `tw_arc` member 0 names it (`TornWorldMapTable`), not by reading the script.

## Gravity

There is no gravity flag and no rotated room. There are boxes, each with an orientation, and the player is bound to one of them at a time:

```c
enum FloatingPlatformKind {
    FLOATING_PLATFORM_KIND_FLOOR = 0,
    FLOATING_PLATFORM_KIND_WEST_WALL,
    FLOATING_PLATFORM_KIND_EAST_WALL,
    FLOATING_PLATFORM_KIND_CEILING,
    FLOATING_PLATFORM_KIND_INVALID
};
```

`src/overlay009/ov9_02249960.c`. A floating platform is that kind, a box and a collision grid (`DistWorldFloatingPlatformTemplate`). The box is inclusive on both ends, so a size of 0 is one tile thick (`DistWorldBounds_AreCoordinatesInBounds`), which is how a wall lies flat against the axis it faces.

The orientation becomes an avatar state, `AVATAR_DISTORTION_STATE_FLOOR`, `WEST_WALL`, `EAST_WALL` or `CEILING` (`include/constants/player_avatar.h`), and movement is remapped for it by four direction tables in `src/player_move.c`: on a wall, up and down move the player along Y.

Only five floors have platforms at all: B1F has a west wall, B2F four floor sections and a west wall, B3F a west wall, B4F an east wall and a ceiling, and the Turnback Cave room an east wall. Gravity never turns on 1F, B5F, B6F, B7F or in the Giratina Room.

**Changing gravity: jump points.** The player moves between surfaces at jump points, each a box, a required facing, a displacement, a sprite rotation, a step count and a landing surface (`DistWorldFloatingPlatformJumpPointTemplate`). A jump point fires when the player faces its direction inside its box and runs the one handler in `sFloatingPlatformJumpPointHandlers`. The hop is a fixed arc, 4, 6, 8, 10, 11, 12, 12, 12, 11, 10, 9, 8, 6, 4, 0, 0 world units (`sFloatingPlatformJumpOffsets`), added to the sprite on the axis named by `jumpAxis` (0 X, 1 Y, 2 Z) and negated when `invertedJump` is 1 (`TickJumpOnFloatingPlatformMovementAnimation`). The sprite turns by `playerSpriteRotAngle` degrees over the steps (`RotateMapObject`). A landing kind of 4 with platform `0xFFFF` means back onto the ordinary ground. Walking off one box onto another of the same orientation rebinds without a jump (`src/player_move.c`).

In retail every jump rotates the sprite by 90 degrees one way or the other, takes 16 steps, and every floor that has jump points has four.

**By hand.** Platforms and jump points are the first two sections of the floor's `tw_arc` member ([layout](DistortionWorldFiles.md#floor-files-members-1-to-10)). Moving or resizing a platform, or retargeting a jump point, is an in place edit. Adding one means growing the section and rewriting its size in the file header; the game reads the counts, so the file can grow. `handlerIndex` has to stay 0, since there is only one handler.

**In DSPRE.** `TornWorldFile` reads and writes both sections. The Gravity tab edits each platform's kind, collision grid, box and tile counts, and the Gravity changes tab each jump point's facing, position, displacement, sprite angle, steps, final facing and landing surface; a jump point's handler, unused word, axis, inversion and box size are kept as read. Changing a platform's tile counts resizes its `tw_arc_attr` grid, keeping the cells that still fit and blocking new ones; a count outside 1 to 256, or a grid id that is not an existing member, is refused. The walk preview binds the player to surfaces and takes a jump point only when the player faces its way, hopping along the arc and turning the sprite over the jump's steps (`TornWorldRuntime`). On a floor's own ground, the preview compares jump points, prop triggers and camera regions with the height the player stands at, the floor's altitude plus the land data's terrain height (`TornWorldSurfaces.Surface.EventAt`), which is one tile above the altitude on every retail floor.

## Collision on walls and ceilings

Two collision formats are live at once. The ordinary land data grid, 32 by 32 `u16` values with collision in bit 15 and the behaviour in the low byte (`include/constants/field/map.h`), is what applies on ordinary ground. On a platform, collision comes from that platform's own grid in `tw_arc_attr`, chosen by the platform's `distortionWorldAttrID` and read whole (`LoadFloatingPlatformTerrainAttributes`).

The grid is column major over the platform's `tileCountVertical`, and a world tile becomes a grid position according to the orientation:

| Orientation | Vertical index | Horizontal index |
|---|---|---|
| Floor | `x - startX` | `z - startZ` |
| West wall | `sizeY - (y - startY)` | `z - startZ` |
| East wall | `y - startY` | `z - startZ` |
| Ceiling | `sizeX - (x - startX)` | `z - startZ` |

Outside the box the lookup answers out of bounds, which counts as blocked. Inside it the entry is read with the ordinary masks, so a platform tile can carry any behaviour: the B4F ceiling is surfable water (`0x15`), and one B2F floor section has the two tile jumps (`0x5A`, `0x5B`).

A few tiles are blocked in code. Giratina Room tile (15, 26) and B7F tile (89, 56) always are (`DistWorld_DynamicMapFeaturesCheckCollision`), and Giratina Room tile (15, 1, 15) is blocked only while the progress variable is 14 (`DistWorld_IsBlockedByCynthia`). Background events, signs included, are only checked in the avatar states `AVATAR_DISTORTION_STATE_NONE` and `_ACTIVE`, ordinary ground inside the Distortion World being `_ACTIVE`, so they are skipped while the avatar is on a floor, wall or ceiling platform (`src/overlay005/field_control.c`).

**By hand.** Each `tw_arc_attr` member is one platform's grid, `tileCountVertical` by `tileCountHorizontal` little endian `u16` values ([layout](DistortionWorldFiles.md#tw_arc_attrnarc)). Blocking a wall tile is setting bit 15 of its entry. In retail every grid is 32 by 32, members 0 and 1 are all zero and unused, and ids 2 to 11 belong to the ten platforms in order. The code-blocked tiles are compare immediates in overlay 9, listed on the files page.

**In DSPRE.** The editor lays every platform out flat next to the floor's ground (`TornWorldSurfaces.ForFloor`) and paints its grid with the same Collision and Tiles painters as the Map editor. A platform's painted grid is saved to its `tw_arc_attr` member in the game's order, the vertical tile plus the horizontal tile times the vertical count, and cells outside the grid are never written. The ground of a floor is painted and saved in the Map editor.

## Camera

The header's camera type is ignored. Every floor says `CAMERA_TYPE_INTERIOR_ORTHOGRAPHIC`, and the overlay builds its own perspective camera from a configuration compiled into it (`CameraInit`): distance `0x29AEC1`, angle (-10750, 0, 0), field of view 1473, widened by `0xC0` outside the Giratina Room. The field map then takes its roll path, which is what lets the world appear sideways and upside down:

```c
    if (fieldSystem->unk_20 == 1) {
        if (FieldMap_InDistortionWorld(fieldSystem) == TRUE) {
            DistWorld_UpdateCameraAngle(fieldSystem);
        }

        Camera_ComputeViewMatrixWithRoll();
    } else {
        Camera_ComputeViewMatrix();
    }
```

`src/overlay005/fieldmap.c`.

The angle comes from camera regions in the floor's file: a box, a facing, three angle components and a transition length (`DistWorldCameraAngleTemplate`). On every step the game takes the region that contains the player and matches the way they face and eases towards it the short way round (`CalculateCameraAngleDelta`). The components are in units of 256, so one unit is 1.40625 degrees, and they are added to the base angle rather than replacing it (`DistWorld_UpdateCameraAngle`; `DoCameraTransition`). Regions come in pairs over the same box, one for each way through it, which is how walking back undoes a turn. The current angles are saved, so a reloaded game resumes where it was.

**By hand.** Camera regions are the third section of the floor file. Changing an angle is changing three `u16` values; the scripted camera moves for the waterfalls and the Giratina Room use the same 24 byte record in tables compiled into overlay 9, listed on the files page.

**In DSPRE.** The Camera tab edits every field of every region, and the walk preview turns the camera as the player crosses them (`CameraAt`), roll included.

## Scenery that fades in and out

The platforms and rocks that appear as the player approaches are ghost props: groups of props, each group shown or hidden by trigger boxes. On floor entry the hidden groups are the complement of the file's default mask, and a trigger only fires on the active floor, when the player has really moved, and when the facing matches (`HandleGhostPropTriggerAt`). There can be 24 groups (`GHOST_PROP_GROUP_MAX_COUNT`, `include/overlay009/ov9_02249960.h`).

Ghost props are only something to look at. Their visibility drives fading, opacity and a sound and nothing in the collision code reads it, so a path that looks empty is walkable if its collision says so. The props are models from `data/mmodel/fldeff.narc`, one per prop kind (see "Props").

**By hand.** The fourth section of the floor file, with a twelve byte header and no count prefix of its own ([layout](DistortionWorldFiles.md#ghost-props)). A section that exists must hold at least one template.

**In DSPRE.** The Fading props and Prop triggers tabs edit every template and trigger; the file's default mask is kept as read. The walk preview draws every template, shows the default groups on floor entry and switches groups when the player steps on a trigger facing its way; it shows and hides them at once rather than fading.

## Moving platforms and elevators

The floating stones the player rides are map templates compiled into the overlay, per floor (`sMovingPlatformsMapTemplates`; record `DistWorldMovingPlatformTemplate`). Each has a tile, an elevator path, a direction (0 up, 1 down, 2 none), the template it becomes on the next floor, a prop kind and a persisted flag. Up and down platforms start when the player steps onto them (`HandleElevatorPlatformPropAnimatorAt`) and follow one of 22 elevator paths (`sElevatorPlatformPaths`): a final offset, the offset at which the floor switches, a speed and flags to set and clear. Platforms with no direction, the sixteen on B2F, only move when a coordinate trigger says so.

A floor change is not a warp. When a path reaches its change offset, or the B4F waterfall runs, the floor loader swaps floors in place. Only the next floor is kept loaded ahead of time, as an inactive floor with its own matrix and land data (`InitialLoadInactiveFloor`), rendered beside the active one (`DistWorld_RenderInactiveFloor`, called from `src/overlay005/fieldmap.c`). Going down promotes it; going up reloads the floor above.

Several paths are special in code rather than in data: paths 13, 10 and 11 toggle extra flags, path 9 sets B5F's first flag and spawns its first template, B6F template 1 is the Giratina Room lift and waits for the stepping stones flag, and arriving on B4F on template 1 plays Cyrus's scene. Paths 20 and 21 are not used by any template or chain, and path 21 could never finish, since its target is 32 tiles down and its speed is upwards.

**By hand.** Everything here is overlay 9 data: the floor list, the per floor pointer lists, the 24 byte templates and the 22 paths, at the offsets on the files page. Values change in place freely. Adding a platform needs a longer pointer list, which means writing it somewhere unused and repointing, and there is no known free space in the overlay; the floor list is a fixed eight and the path table a fixed 22.

**In DSPRE.** `TornWorldCodeTables` finds these tables by their shape rather than by fixed offsets and reads them; they are shown, not edited. The editor places each platform's model on its tile and bobs it. The walk preview starts from the game's platform flags, the default set or the B7F set when the walk starts there, and hides a platform whose flag is off. Stepping onto an elevator rides it as the game does (`TornWorldRuntime.ElevatorRide`): the shake before it moves, the floor change at the path's change offset, the flags of the last path of a chain, the template it becomes, and the special cases of paths 9, 10, 11 and 13. With "This floor" the view moves to the next floor and carries the platform over; with "Whole world" the ride continues in the same scene. The B4F Cyrus scene and the Giratina Room lift's wait are not played.

## Coordinate triggers

The set pieces are driven by a small event system of the overlay's own, not by scripts or ordinary triggers. Each floor has a list of trigger tiles (`sMapEvents`), each firing on an exact tile match when its condition holds (`HandleEventAt`), checked on every step after the elevators (`DistWorld_HandlePlayerPositionChanged`). A condition is one of nine kinds: none, the boulder puzzle unsolved or solved, the progress variable equal to, at most or at least a value, manual only, the Giratina shadow not yet seen, and Cyrus's appearance equal to a value (`CheckFlagCondition`).

A trigger runs a list of commands from a set of eighteen (`enum EventCmdKind`, dispatched through `sEventCmdHandlers`): set an object's animation, move a platform, add or remove an object, cascade up or down, start a map script, set the progress variable, show the Giratina shadow, set its animation flag, set or clear a puzzle flag, play Giratina's arrival, show the three boulder tutorials, and show or hide the Giratina Room platforms. The two waterfalls are not in these lists at all; their tiles are constants in code, checked in `DistWorld_HandlePlayerMoved`.

**By hand.** Trigger lists and their commands are overlay 9 data, 16 byte triggers pointing at 8 byte commands with their own parameter blocks ([layout](DistortionWorldFiles.md#coordinate-triggers)). A trigger can be moved, re-conditioned or pointed at another existing command list in place. Retail triggers are on eight floors; B6F and the Turnback Cave room have none.

**In DSPRE.** Not shown yet.

## Objects

Nine of the ten floors have an empty events file, so their people are compiled into the overlay (`sMapObjectEvents`). Each record is a condition, a rotation, and then an ordinary 32 byte object event, the same layout as in an events file (`DistWorldObjectEvent`). Local ids start at 128 (`include/constants/distortion_world.h`), and objects are added and removed by the script commands `AddDistortionWorldMapObject` and `DeleteDistortionWorldMapObject` (`src/scrcmd.c`) or by trigger commands. The current floor and the next one share nineteen object slots. Cyrus on B4F uses movement 67, which walks left and right through walls, stopped only by its range (see [Overworld Events Logic](../Events/OverworldEventsLogic.md#movement-types)).

**By hand.** The object lists are overlay 9 data; an object's graphics, script, facing or position changes in place, at the offsets on the files page. Its Y is `fx32`, the tile shifted left by sixteen.

**In DSPRE.** Not read; the Event editor shows the nine floors as empty, which is what their events files say.

## Props

Everything the overlay draws that is not the map, the floating stones, the portal, the waterfall, rocks and the Giratina shadow, is a prop of one of 25 kinds. A kind's model is a `fldeff.narc` member (`sProp3DModelNARCIndexByKind`, members `0x7C` to `0x94`), five kinds animate from further members (`sPropAnimSetNARCIndexByKind`), each kind has a starting offset (`sPropInitialPosOffsetByKind`) and a behaviour (`sPropAnimFuncsByKind`), and platforms bob through eight fixed offsets (`sPlatformPropAnimOffsets`). Four floors also have one fixed prop each, the portals and the B5F waterfall (`sSimplePropsMapTemplates`).

`sPropScaleByKind` is not a drawing scale. It only sizes the box used to decide whether a prop is in view (`IsPropInView`); every draw goes through `Simple3D_DrawRenderObjWithPos` with no scale.

**By hand.** The 25 entry tables are fixed size overlay 9 data; swapping a kind's model is changing one `u32` member index. The models themselves are ordinary NSBMD files in `fldeff.narc`.

**In DSPRE.** `TornWorldCodeTables` reads all of these tables, and the editor draws every prop at its true size with its model, offset and animation; the scale table is only used as the game uses it. They are shown, not edited.

## The sky

The sky is `data/tw_arc_etc.narc`: a background of tiles, palette and screen (members 0 to 2, `InitSkyBackground`), seven cloud sprites as cell, graphics and animation triples (members 3 to 23), and one palette of five for all the clouds (member 24, `InitSkyClouds`). Nine clouds are placed from `sSkyCloudsTemplates` and turn at speeds from `sSkyCloudsRotAngleDeltas`, faster or reversed in the Giratina Room depending on the progress variable. The member names are in the decomp's `res/prebuilt/data/tw_arc_etc.naix`.

**By hand.** Ordinary 2D graphics files; replacing a member keeps its format and palette count.

**In DSPRE.** Not read by the Distortion World editor. The members open like any other in the Graphics Browser.

## Floors, warps and what is saved

Ordinary zone change is off: `FieldMap_ChangeZone` returns false at once (`src/overlay005/fieldmap.c`), so crossing a matrix edge never changes the header. Floors change through `FieldMap_ChangeZoneDistortionWorld`, which sets the header, reloads its data and fades the music without a map name or a warp. The walk into a transition check is replaced by the overlay's own (`DistWorld_CheckMapTransition`, hooked at `src/overlay005/field_control.c`), but step based coordinate events and ordinary step transitions still run after it (`field_control.c`).

Getting in and out uses ordinary scripted warps. The two special transitions are tiles in code: B7F (89, 65, 57), facing up, with progress at least 10, runs map script 2 to go to the Giratina Room, and the Giratina Room (15, 1, 25), facing down, runs script 4 to go back.

All floors share one coordinate space. Each floor's X, altitude and Z offset is in `tw_arc` member 0 and is applied to its land data in tiles (`LandDataManager_DistortionWorldSetOffsets`, `src/overlay005/land_data.c`). The altitudes fall floor by floor from 288 on 1F to 64 on B7F, except B6F at 114, with the Giratina Room at 0.

What survives a save is 32 bytes in the misc save block's persisted map features (`include/persisted_map_features.h`), which while the id is 9 hold the hidden ghost groups of the active floor, the current platform, the three camera angles, eleven moving platform flags and seventeen boulder puzzle flags (`DistWorldPersistedData`, `include/overlay009/ov9_02249960.h`). The buffer is cleared on every warp in and filled with defaults by `InitPersistedData`. Moving platforms are saved as invisible map objects with local id `0xFD` and rebuilt from them on load. The story state is `VAR_DISTORTION_WORLD_PROGRESS`, whose values are listed in `include/constants/distortion_world.h`.

**By hand.** Floor offsets are the last three fields of each 12 byte row of `tw_arc` member 0. Moving a floor there moves where the game draws it relative to the others; the walkable positions in the floor file and the overlay tables are world tiles and would have to move with it.

**In DSPRE.** The floor list is read and shown with its offsets, and the whole world view places each floor at them; the list itself is shown, not edited. The walk preview changes floor when an elevator reaches its change offset.

## What DSPRE does, in one place

The editor is World menu, Distortion World, Platinum only and behind the beta gate (`DistortionWorldView` in `BetaEditors`). `AvaloniaEditorLauncher.OpenDistortionWorldEditor` refuses other games and ROMs without `tw_arc` member 0. The archives are found through the `tornWorld` and `tornWorldAttributes` entries of `RomInfo.SetNarcDirs`, which exist only for Platinum.

| Data | Read | Edited | Written by Save |
|---|---|---|---|
| `tw_arc` member 0, floor list | `TornWorldMapTable` | no | no |
| `tw_arc` floor files: platforms, jump points, camera regions, ghost props | `TornWorldFile` | yes, fixed row counts | yes, the floor's member |
| `tw_arc_attr` grids | `DistortionWorldViewModel.Grid` | painted | yes, the platform's member |
| Land data map models | `MapFile` | through the map model editor | yes, as land data |
| Overlay 9 tables: platforms, paths, props, models, offsets | `TornWorldCodeTables`, located by shape | no | no |
| Overlay 9 triggers, commands, objects, chain, code tiles | no | no | no |
| `tw_arc_etc` sky | no | no | no |

Save writes the unpacked members only; they reach the ROM when the ROM is saved, like every other archive. Discard reloads the unpacked files.
