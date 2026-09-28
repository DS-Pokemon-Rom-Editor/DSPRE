[Research](../../ResearchNotes.md) / [Field Research](../FieldResearch.md) / Overworld Events Notes

Notes for [OverworldEventsLogic.md](OverworldEventsLogic.md).

record 32 B, same DP/Pt/HG (Pt map_header_data.h, HG map_events_internal.h, DP map_object.h):
  0 localID, 2 gfx, 4 movement, 6 type, 8 hidden flag, A script, C dir s16, E data0, 10 data1, 12 data2, 14 rangeX s16, 16 rangeZ s16, 18 x, 1A z, 1C y fx32 (tile << 16)
  every retail events file = bg*20 + obj*32 + warp*12 + coord*16 exactly

type (u16 +6), copied unchanged into the map object (Pt map_object.c); 12 values named in Pt generated/trainer_types.txt
  GetTrainerType (trainer_see.c) folds 4-8 into NORMAL
  0 plain, runs own script
  1 trainer, looks along facing, data0 sight
  2 trainer, all four directions, data0 sight
  3 no behaviour found (partner search only); HG one query function, called from ov27/ov28, purpose unknown
  4/5/6 trainer; every data1 steps WALKED: look to sides / all 4 anticlockwise / clockwise, 4 turns x 8 frames, restore facing (unk_020673B8.c)
    stationary object never looks
  7/8 trainer, turns ccw / cw on route movements 20, 21-44 (unk_0206450C.c)
  9 A-press runs script 0 (single End) instead of own script (field_control.c, all 3 games)
  10 Pt only, movements 37-44: jump on the spot instead of next step when player within data0 ahead; never battles (not NORMAL in GetTrainerType); skipped by Vs Seeker + partner search; no compare against 10 in DP/HG
  11 Pt name only, no reader
sight: data0 tiles, path must be clear (IsPathInterrupted); 0 = never spotted (DP 57, Pt 49, HG 27 trainers); retail 0-6 DP/Pt, 0-7 HG
other type readers: types 1/2 on look-around movements face a running player within data0 (unk_0206450C.c)
  Vs Seeker trainers = 1, 2, 4-8 (vs_seeker.c)
  HG defeated trainer + partner turn to current facing (overlay_26_022599D0.c)
  common script prints message # = type value: Pt 2026 (ScrCmd_MessageFromTrainerType), HG 2016; unused in retail Pt

movement types: DP 55 (0-54, arm9/src/map_object.c), Pt 68 (0-67, unk_020EDBAC.c, generated/movement_types.txt), HG 57 (0-56, entry 47 empty)
  0 none, 1 player, 2 look around, 3-5 wander, 6-13 look subsets, 14-17 face N S W E, 18-19 rotate, 20 back and forth, 21-44 routes (4 legs each), 45-46 look N/S W/E, 47 berry patch (DP/Pt), 48 follow player, 49 rematch-ready spin, 50 follow partner, 51-54 disguise snow/sand/rock/grass
  Pt 55-67 unused in retail events files (decomp leaves them unnamed; handlers unk_02069BE0.c, unk_0206450C.c):
    55-58 copy player's steps (one handler), 59-62 same but only inside very tall grass,
    63/64 follow wall on left/right, 65/66 same + turn round at range edge, 67 wander L/R stopped only by range (walks through walls; DW B4F Cyrus)
  route legs (unk_0206450C.c lists): 25 and 26 are both L R D U; full list in the Logic page
  HG 55/56 follow with no delay (/ + copy player's actions), walking Pokemon after warps/items: non-public source, unconfirmed in decomp
  range X/Z fence start +/- range; -1 = no fence (map_object_move.c)

data0: sight (trainers, type 10), berry patch id (movement 47), HG apricorn tree, sign graphic when sign command gets 0 (scrcmd.c)
data1: only types 4-6 (steps between looks); data2: never read for map objects, 0 in all retail
hidden flag: set = not spawned; remove command sets it (item balls stay collected); trainers use trainer flags
script 0xFFFF = alias: hidden flag field = map header id, local id = object in that map (clone_id in tools/jsoncnv/event.py); DP 48, Pt 47, HG 10

scripts: trainer 3000 + id - 1, partner slot 5000 + id - 1 (Script_GetTrainerID); approach script Pt 3928 (FieldSystem_CheckForTrainersWantingBattle)
  item ball 7000 + index + own hidden flag; berry patch 2800 movement 47; HG apricorn 2800 type 0 movement 0 data0 tree
  double battle from trainer data; partner = other type 1/2 with same trainer id (FindTrainerPartner)
  trainers with a local map script: Pt 22, DP 30, HG 3; no non-trainer type holds 3000-6999

retail type counts (DP / Pt / HG): 0 2711/3109/2260, 1 411/407/403, 2 11/11/0, 3 6/7/0, 4 13/13/3, 5 3/3/0, 6 2/2/0, 7 3/3/1, 8-11 none
  no retail movement >= 55

hand example Pt Route 201 obj 0 (events +0x08): -> trainer 4, face west, sight 3:
  +04 10 00, +06 01 00, +0A BB 0B, +0E 03 00, +14/+16 00 00

DSPRE side:
  base DSPRE: OwType NORMAL 0 / TRAINER 1 / ITEM 3 radio buttons, other types loaded as normal and overwritten
  now: OverworldEventTypes.For lists every type by name; unlisted values kept as read (OwTypeUnknown)
  types per game: 0-9 all, 10 "Jumps when approached" Pt only (not a trainer), 9 "Silent" (script field kept)
  sight shown for trainer types ("Jump distance" for 10), data1 "Steps between looks" for 4-6, data2 raw
  facing adds None (-1); ranges -1..32767; item check uses the hidden-item bounds (8000 is not an item ball)
  TrainerScripts (FieldPlayer.cs) trainer/item arithmetic = the games'; movements from OverworldMovements.For(family), unknown kept as read
  walk preview: data1 is a step count, not a frame interval
