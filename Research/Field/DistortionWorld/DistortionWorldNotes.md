[Research](../../ResearchNotes.md) / [Field Research](../FieldResearch.md) / Distortion World Notes

Notes for [DistortionWorldLogic.md](DistortionWorldLogic.md) and [DistortionWorldFiles.md](DistortionWorldFiles.md).

Pt only. HG has the battle bg + map section name, and a dead overlay branch in fieldmap.c
overlay 9 names below are in src/overlay009/ov9_02249960.c unless another file is given
units: tile = 16 world units, fx32 20.12, facing 0 up 1 down 2 left 3 right
  every archive/table coordinate is whole tiles incl. Y; only the map object's own grid Y is half tiles (GetPlayerPos halves it)

maps: headers 573-577, 579-583 (10); 570 + 578 labelled but in no overlay table
  header -> matrix: 570 268, 573 269 (2x2), 574 270 (2x1), 575 271, 576 272, 577 273, 578 274, 579 275, 580 276 (all 2x2), 581 277 (1x2), 582 278 (1x1), 583 279 (2x2)
  matrices have no header list, no altitude list; floor matrix max 2x2 (land_data.c fixed quadrant rule)
  shared area_data_074, MAP_TYPE_CAVE, clear weather, battleBG DISTORTION_WORLD
  only 1F has an events file; the rest use events_empty
  chain sDistWorldMapConnectionList: 1F -> B7F; Giratina Room + Turnback Cave room no neighbours (593)

marker: persisted map feature id 9 (DYNAMIC_MAP_FEATURES_DISTORTION_WORLD), FieldMap_InDistortionWorld (fieldmap.c)
  set by InitPersistedMapFeaturesForDistortionWorld in every floor's OnTransition script
  same id selects overlay 9

gravity: FloatingPlatformKind 0 floor 1 west wall 2 east wall 3 ceiling 4 invalid
  platform = kind + box (inclusive both ends, size 0 = one tile) + attr grid id + grid tiles vert/horiz
  avatar states AVATAR_DISTORTION_STATE_*; direction remap tables in player_move.c
  platforms only on B1F (W), B2F (4 floor + W), B3F (W), B4F (E + ceiling), Turnback Cave room (E)
  jump point: facing + box + displacement + sprite rot + steps + jumpAxis (0 X 1 Y 2 Z) + invertedJump + final facing + landing kind/platform
    handler must be 0 (one entry in sFloatingPlatformJumpPointHandlers)
    hop arc sFloatingPlatformJumpOffsets 4,6,8,10,11,12,12,12,11,10,9,8,6,4,0,0
    landing kind 4 + platform 0xFFFF = back to ground
    retail: every jump 90 deg, 16 steps, 4 per floor that has them

collision: tw_arc_attr grid per platform, column major, index vert + horiz * tilesVertical
  floor: v x-sx, h z-sz | west: v sizeY-(y-sy) | east: v y-sy | ceiling: v sizeX-(x-sx); h always z-sz
  outside the box = out of bounds = blocked
  bit 15 collision, low byte behaviour, nothing else set
  retail: all grids 32x32 (2048 bytes); members 0 + 1 all zero unused; ids 2-11 one per platform
  behaviours: 0x08 cave floor (grid 8), 0x15 sea water (grid 10, B4F ceiling surfable), 0x5A/0x5B two-tile jumps (grid 7)
  code blocks (DistWorld_DynamicMapFeaturesCheckCollision): Giratina Room (15,26), B7F (89,56)
  Cynthia block (15,1,15) only while progress == 14 (DistWorld_IsBlockedByCynthia)
  bg events only checked in AVATAR_DISTORTION_STATE_NONE/_ACTIVE (ACTIVE = ordinary ground), so skipped on floor/wall/ceiling platforms (field_control.c)

camera: CameraInit config in overlay: dist 0x29AEC1, angle (-10750,0,0), perspective, fov 1473 (+0xC0 outside Giratina Room)
  header camera type ignored; roll path via Camera_ComputeViewMatrixWithRoll (fieldmap.c)
  region = box + facing + angle xyz + steps; matched on tile AND facing, every step
  angle units *0x100 into DS angle = 1.40625 deg, ADDED to base angle; shortest-path ease
  regions come in pairs over one box (one per direction)
  angles persisted in save

ghost props: header (template count, trigger count, default visible mask) + 12 B templates + 20 B triggers, no count prefix
  groups < 24; entry hides ~default; triggers only on active floor, real movement, matching facing
  visual only: fade/opacity/sound, no collision reads it

moving platforms: sMovingPlatformsMapTemplates (8 floors, fixed) -> null-terminated ptr lists -> 24 B templates
  dir 0 up 1 down 2 none; persisted flag 11 = always
  up/down start on step; none (16 on B2F) only via MOVE_PLATFORM trigger commands
  sElevatorPlatformPaths 22 x 32 B, 22 also = no next path; chains 8->9, 15->16; 20 + 21 unused, 21 self-inconsistent
  code specials: paths 13/10/11 extra flags, path 9 B5F flag + template, B6F template 1 Giratina lift, B4F template 1 Cyrus
  runtime: invisible map objects local id 0xFD
  floor change in place, next floor preloaded (InitialLoadInactiveFloor, DistWorld_RenderInactiveFloor)

coord triggers: sMapEvents -> 16 B triggers (x, y, z s32, cond, val, cmd ptr), null cmd ends
  conditions 0 none 1 boulder unsolved 2 solved 3 prog == 4 prog <= 5 prog >= 6 manual 7 shadow unseen 8 cyrus ==
  commands 8 B {kind, params ptr}, kind 18 ends; 18 kinds via sEventCmdHandlers
  waterfalls: lists outside sMapEvents, tiles in code (DistWorld_HandlePlayerMoved)
  retail lists on 8 floors (not B6F, not Turnback Cave room)

objects: sMapObjectEvents -> 40 B records: cond, val, rotated, rot angle, then standard 32 B ObjectEvent (y fx32)
  local ids from 128; 19 slots shared by current + next floor
  add/remove via AddDistortionWorldMapObject / DeleteDistortionWorldMapObject (scrcmd.c) or trigger commands

props: 25 kinds; model fldeff.narc 0x7C-0x94; anims 0xBF 0xC0 0xC1 0xC6 0xC8
  sPropScaleByKind = culling box only (IsPropInView); draws use no scale
  fixed props sSimplePropsMapTemplates: 1F portal, B5F waterfall, Giratina Room portal (prog >= 14), Turnback portal

sky: tw_arc_etc 25 members: 0-2 bg ncgr/nclr/nscr, 3-23 seven clouds (ncer/ncgr/nanr), 24 cloud palette x5
  names in res/prebuilt/data/tw_arc_etc.naix

zone change: FieldMap_ChangeZone returns FALSE; floors via FieldMap_ChangeZoneDistortionWorld (no popup, no warp)
  DistWorld_CheckMapTransition replaces walk-into-transition only; coord events + step transitions still run (field_control.c)
  code transitions: B7F (89,65,57) up + prog >= 10 -> script 2; Giratina Room (15,1,25) down -> script 4

save: MiscSaveBlock persistedMapFeatures {id, 32 B}; while 9: DistWorldPersistedData
  hidden ghost groups (active floor), current platform, camera xyz, 11 platform flags, 17 boulder flags
  cleared on every warp in; InitPersistedData defaults; ResetDistortionWorldPersistedCameraAngles scrcmd
  VAR_DISTORTION_WORLD_PROGRESS values in include/constants/distortion_world.h

tw_arc member 0 rows (hdr file x alt z): 573 1 21 288 10 | 574 2 0 256 35 | 575 3 15 224 0 | 576 4 47 192 21 | 577 5 57 160 34 | 579 6 57 128 34 | 580 7 56 114 38 | 581 8 74 64 32 | 582 9 0 0 0 | 583 10 70 64 30

overlay 9: uncompressed, RAM 0x02249960 + file offset; same in both US revisions; other languages unchecked
  offsets and records: DistortionWorldFiles.md

DSPRE side:
  paths: RomInfo.SetNarcDirs tornWorld / tornWorldAttributes, Pt only
  TornWorldMapTable (member 0): read, shown with offsets
  TornWorldFile floor files: read + written, fixed row counts
    Gravity tab: kind, grid, box, tile counts; Gravity changes tab: facing, position, displacement, sprite angle, steps, final facing, landing surface
    kept as read: jump handler, unused word, axis, inversion, jump box size, file header word 0, default visible mask
    Camera tab: every field; Fading props + Prop triggers tabs: every template/trigger
  tw_arc_attr via DistortionWorldViewModel.Grid: painted with the Map editor's painters, written per member
    written in the game's order (vertical + horizontal * tilesVertical); tile count change resizes the member
    refused: tile counts outside 1-256, grid id that is not an existing member
  ground of a floor: Map editor; map models edited in place, saved as land data
  TornWorldCodeTables: overlay 9 tables found by shape (no fixed offsets), shown not edited
    platforms, props, prop models/anims/offsets, hover, elevator paths
  walk preview: gravity overlay, surfaces flattened side by side (TornWorldSurfaces), camera regions with roll
    TornWorldRuntime: platform flags (default or B7F set), jump points only on matching facing, hop arc + sprite turn,
      ghost groups on entry and on facing-matched trigger steps (instant, no fade),
      elevator rides frame by frame: shake, floor change at the change offset, last-path flags, template swap, paths 9/10/11/13
    not played: B4F Cyrus scene, Giratina Room lift wait
  props drawn at true size; sPropScaleByKind only as a culling box
  beta gated, World menu, Pt only; Save writes unpacked members, repacked on Save ROM
