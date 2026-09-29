[Research](../../ResearchNotes.md) / [Field Research](../FieldResearch.md) / Overworld Events Logic

# Overworld Events Logic, Diamond/Pearl, Platinum and HeartGold/SoulSilver

Source: the [pokediamond](https://github.com/pret/pokediamond), [pokeplatinum](https://github.com/pret/pokeplatinum) and [pokeheartgold](https://github.com/pret/pokeheartgold) decomps, cited by file and function. This was structured into a document with AI.

An overworld, or object event, is one 32 byte record in a map's events file, and the same record in all three games. Four of its fields decide what kind of thing it is and what it does: the type, the movement, the script, and three data words whose meaning depends on the first two. DSPRE originally knew three types, normal, trainer and item, and anything else was loaded as normal and overwritten the moment a type was picked. This covers every type the games have, what each reads, and how an object's script is chosen.

## The record

Checked in Platinum `include/map_header_data.h`, HeartGold `include/map_events_internal.h` and Diamond `include/map_object.h`; every retail events file in all three games parses to exactly its length with these sizes.

| Offset | Size | Field |
|---|---|---|
| 0x00 | 2 | local id |
| 0x02 | 2 | graphics id |
| 0x04 | 2 | movement type |
| 0x06 | 2 | type |
| 0x08 | 2 | hidden flag |
| 0x0A | 2 | script |
| 0x0C | 2 | facing, signed |
| 0x0E | 2 | data 0 |
| 0x10 | 2 | data 1 |
| 0x12 | 2 | data 2 |
| 0x14 | 2 | movement range X, signed |
| 0x16 | 2 | movement range Z, signed |
| 0x18 | 2 | tile X |
| 0x1A | 2 | tile Z |
| 0x1C | 4 | height, tiles shifted left by sixteen |

The type is copied into the live map object unchanged when the object is created (Platinum `src/map_object.c`), and the readers found are listed below.

## Types

Platinum names twelve values, 0 to 11 (`generated/trainer_types.txt`), and the per type step tables have twelve entries in all three games. The types that make a trainer are resolved by `GetTrainerType` in `src/trainer_encounter.c`, which folds 4 to 8 into an ordinary trainer.

| Type | What it does | Games | Fields it reads |
|---|---|---|---|
| 0 | A plain object: talking to it runs its script | all | none |
| 1 | A trainer that looks straight ahead along its facing | all | data 0, sight |
| 2 | A trainer that looks in all four directions | all | data 0, sight |
| 3 | No behaviour found | all | none |
| 4 | A trainer that, every data 1 steps it walks, stops and looks to its two sides, then turns back | all | data 0 sight, data 1 steps |
| 5 | As 4, but turns through all four directions anticlockwise | all | data 0, data 1 |
| 6 | As 4, but clockwise | all | data 0, data 1 |
| 7 | A trainer that turns anticlockwise as it walks a route | all | data 0 |
| 8 | A trainer that turns clockwise as it walks a route | all | data 0 |
| 9 | Talking to it runs script 0 instead of its own | all | none |
| 10 | Jumps on the spot instead of taking its next step when the player is within data 0 tiles ahead of it; never starts a battle | Platinum | data 0 |
| 11 | Named, never read | Platinum | none |

**Sight.** A trainer sees the player when the player is within data 0 tiles in the direction it looks and nothing blocks the tiles between them (`IsPathInterrupted` in `trainer_encounter.c`); type 2 tries all four directions. A sight of 0 means the trainer is never seen coming and only fights when talked to, which is how 57 Diamond, 49 Platinum and 27 HeartGold trainers are set. Retail sights run from 0 to 6 in Diamond and Platinum and to 7 in HeartGold.

**Looking around, 4 to 6.** The look sequence is set off by walking, not by time: it counts steps actually taken and only then stops to look, four turns of eight frames each, before restoring the facing (the type 4 to 6 step functions in Platinum `src/unk_020673B8.c`). A trainer of these types that never moves never looks.

**Turning while walking, 7 and 8.** These only do anything on the route walking movement types, 20 and 21 to 44, which turn the object as it goes (Platinum `src/unk_0206450C.c`).

**Type 9.** Pressing A on it runs script 0, a single `End`, rather than the object's own script (the object interaction check in `src/overlay005/field_control.c`, and its HeartGold and Diamond counterparts). The object is therefore inert, and its script field is not used by talking to it.

**Type 10.** Platinum only, and only on movement types 37 to 44. It is not a trainer: `GetTrainerType` returns it unchanged, so it never matches the sight check, and the Vs. Seeker and double battle partner searches skip it. Diamond and HeartGold have no comparison against 10 anywhere.

**Type 3.** No behaviour in Platinum or Diamond beyond being included in the partner search. HeartGold has one function answering whether an object is type 3, reached only from overlays 27 and 28, whose purpose has not been established. Retail item balls work as type 0; seven Platinum item balls happen to be type 3.

Other places that read the type: trainers of types 1 and 2 on look around movements turn to face a running player within data 0 tiles (Platinum `src/unk_0206450C.c`); the Vs. Seeker treats 1, 2 and 4 to 8 as trainers (`src/overlay005/vs_seeker.c`); in HeartGold a defeated trainer and its partner turn to face their current direction for good (`src/overlay_26_022599D0.c`); and a common script prints the message whose number is the object's type value (Platinum common script 2026, `ScrCmd_MessageFromTrainerType` in `src/scrcmd.c`; HeartGold 2016), which no retail Platinum object uses.

## Movement types

| Game | Count | Table |
|---|---|---|
| Diamond/Pearl | 55, 0 to 54 | `arm9/src/map_object.c` |
| Platinum | 68, 0 to 67 | `src/unk_020EDBAC.c`, names in `generated/movement_types.txt` |
| HeartGold/SoulSilver | 57, 0 to 56 | `src/map_object.c`; entry 47 is empty |

The groups share their numbers in all three games: 0 none, 1 the player marker, 2 look around, 3 to 5 wander anywhere, north and south or west and east, 6 to 13 look toward subsets of directions, 14 to 17 face one direction, 18 and 19 rotate, 20 walk back and forth, 21 to 44 walk a route of four legs each, 45 and 46 look north and south or west and east, 47 a berry patch (Diamond and Platinum; HeartGold's slot is empty), 48 follow the player, 49 the spin of a trainer ready for a rematch, 50 follow a partner trainer, 51 to 54 disguises in snow, sand, rock and grass.

**Routes, 21 to 44.** Each walks four legs in order and moves on to the next leg when the next step would leave its range, so the range decides the size of the loop. The legs are lists in Platinum `src/unk_0206450C.c`, U up, D down, L left, R right:

| Type | Legs | Type | Legs | Type | Legs | Type | Legs |
|---|---|---|---|---|---|---|---|
| 21 | U R L D | 27 | D U L R | 33 | R U D L | 39 | L D R U |
| 22 | R L D U | 28 | R D U L | 34 | U D L R | 40 | R U L D |
| 23 | D U R L | 29 | L U D R | 35 | L R U D | 41 | U R D L |
| 24 | L D U R | 30 | U D R L | 36 | D L R U | 42 | D L U R |
| 25 | L R D U | 31 | R L U D | 37 | U L D R | 43 | L U R D |
| 26 | L R D U | 32 | D R L U | 38 | D R U L | 44 | R D L U |

25 and 26 walk the same legs even though their names in the decomp suggest otherwise; they differ only in which axis the third leg waits on before turning, which gives the same path.

**Platinum 55 to 67.** The decomp leaves them unnamed. What their handlers do (`src/unk_02069BE0.c` for 55 to 66, `src/unk_0206450C.c` for 67):

| Type | What it does |
|---|---|
| 55 to 58 | Copies the player's steps: each time the player moves, it moves the same way. The four share one handler. |
| 59 to 62 | The same, but refuses any step that would leave very tall grass |
| 63 | Follows a wall on its left, turning where the wall turns |
| 64 | Follows a wall on its right |
| 65, 66 | As 63 and 64, but turning round when it reaches the edge of its range |
| 67 | Wanders left and right like 5, stopped only by its range, so it walks through walls and other objects |

No retail events file uses 55 or above. 67 is used by Cyrus on B4F of the Distortion World, whose objects are in overlay 9 rather than an events file (see [Distortion World Logic](../DistortionWorld/DistortionWorldLogic.md)).

**HeartGold 55 and 56** are used by the walking Pokémon: it is switched to 55 on a map change (pokeheartgold `src/unk_02055BF0.c`) and to 56 while the player cycles or surfs (`src/follow_mon.c`). How the two follow differently from each other is not yet confirmed.

Movement range X and Z fence the object to its starting tile plus or minus the range on each axis, and -1 means no fence (Platinum `src/map_object_move.c`).

## The data words and the hidden flag

**Data 0** is the sight range for every trainer type and for type 10. It is also the patch number for a Diamond or Platinum berry patch (movement 47), the tree number of a HeartGold apricorn tree, and the sign graphic when a sign command is given 0 (`src/scrcmd.c`).

**Data 1** is read only by types 4, 5 and 6, as the number of steps between looks. **Data 2** is never read for an object placed on a map, and is 0 in every retail file.

**The hidden flag.** An object whose flag is set is not created (Platinum `src/map_object.c`). Removing an object with the remove command sets its flag, which is how a picked up item ball stays gone. Defeated trainers are tracked by trainer flags instead.

**Script 0xFFFF** marks an alias rather than an object: its hidden flag field holds a map header id and its local id is an object in that map, so an object standing on a map seam is the same live object from both sides (Platinum `src/map_object.c`; `tools/jsoncnv/event.py` names it `clone_id`). Diamond has 48 of these, Platinum 47, HeartGold 10.

## How an object's script is chosen

Talking to an object runs its script, except for type 9 (see "Types"). For the rest, the kind of thing it is decides the number, which is read against the shared ranges described in [Script Numbers Logic](../Scripts/ScriptNumbersLogic.md):

| Kind | Script | Where it comes from |
|---|---|---|
| Trainer, first battler | 3000 + trainer id - 1 | `Script_GetTrainerID`; `tools/jsoncnv/convert.py` |
| Trainer, second battler | 5000 + trainer id - 1 | the same |
| Item ball | 7000 + index, with a hidden flag of its own | `res/field/scripts/scripts_visible_items.s` |
| Berry patch, Diamond and Platinum | 2800, movement 47, data 0 the patch | retail maps: 118 Platinum objects |
| Apricorn tree, HeartGold | 2800, type 0, movement 0, data 0 the tree | `src/unk_02055418.c` |

A trainer in sight is found once per step by `FieldSystem_CheckForTrainersWantingBattle` (`trainer_encounter.c`), which takes the first undefeated trainer that can see the player and starts the approach script, 3928 in Platinum. Whether the battle is double is decided by the trainer's data; the partner is the other object of type 1 or 2 with the same trainer id (`FindTrainerPartner`). Some trainers carry a script of their own map instead of a trainer number, 22 in Platinum, 30 in Diamond and 3 in HeartGold; no object that is not a trainer type carries a trainer number.

## In retail maps

| Type | Diamond | Platinum | HeartGold | For example |
|---|---|---|---|---|
| 0 | 2711 | 3109 | 2260 | including alias records; without them 2663, 3062 and 2250 |
| 1 | 411 | 407 | 403 | Platinum Route 203 |
| 2 | 11 | 11 | 0 | Platinum Route 210 south, on the disguise movements |
| 3 | 6 | 7 | 0 | Platinum Route 203 item balls |
| 4 | 13 | 13 | 3 | Platinum Mt. Coronet 3F; HeartGold Route 17 |
| 5 | 3 | 3 | 0 | Platinum Route 214 |
| 6 | 2 | 2 | 0 | Platinum Route 223, data 1 of 5 |
| 7 | 3 | 3 | 1 | Platinum Route 213 |
| 8 to 11 | 0 | 0 | 0 | |

No retail map uses a movement type of 55 or above.

## Changing one by hand

Every change is to the 32 bytes of one record, found after the events file's sign section: a `u32` sign count and that many 20 byte signs, then a `u32` object count and the objects (see [Script Numbers Logic](../Scripts/ScriptNumbersLogic.md#changing-a-script-number-by-hand)).

Platinum Route 201's first object, at offset 0x08 of its events file, is a plain wanderer:

```
00 00 09 00 03 00 00 00 00 00 09 00 02 00 00 00 00 00 00 00 01 00 01 00 7B 00 56 03 00 00 01 00
```

To make it trainer 4, looking west, with a sight of 3: set the movement at +0x04 to `10 00` (face west), the type at +0x06 to `01 00`, the script at +0x0A to `BB 0B` (3003), the sight at +0x0E to `03 00`, and clear the ranges at +0x14 and +0x16:

```
00 00 09 00 10 00 01 00 00 00 BB 0B 02 00 03 00 00 00 00 00 00 00 00 00 7B 00 56 03 00 00 01 00
```

For a trainer that looks around while it walks, use type 4, 5 or 6 with a wandering or route movement and put the step count in data 1 at +0x10.

## What DSPRE does

Overworlds are edited in the Event editor, on its Overworlds tab.

| What | Edited in DSPRE | Written | Left untouched |
|---|---|---|---|
| Type | picked by name from the types this game has (`OverworldEventTypes.For`): 0 to 9 in every game, 10 "Jumps when approached" in Platinum only | the `u16` at +0x06 | a value the game does not define, shown as unknown and kept as read |
| Movement | picked by name from this game's own list (`OverworldMovements.For`): 0 to 54 in Diamond/Pearl, 0 to 67 in Platinum, 0 to 56 without 47 in HeartGold | +0x04 | a value the game does not define, kept as read |
| Sight, data 0 | a number, shown for the trainer types; "Jump distance" for type 10 | +0x0E | |
| Data 1 | "Steps between looks", shown for types 4 to 6 | +0x10 | |
| Data 2 | a raw number | +0x12 | |
| Script | a script from the map's file, a shared range or "No script"; trainers and items by number (`TrainerScripts`); type 9 keeps the field, marked as never run | +0x0A | |
| Facing | up, down, left, right, or "None" for -1 | +0x0C | |
| Ranges | -1 (no fence) to 32767 | +0x14, +0x16 | |
| Local id, graphics, hidden flag, position | fields and the 3D view | their fields | |

The preview walks the wander, route, look and spin movements with the game's timings (`OverworldAnimator`), using the leg lists above; following, copying, wall following and the other special movements stand still.

Save writes the whole events file in place; nothing else is touched.
