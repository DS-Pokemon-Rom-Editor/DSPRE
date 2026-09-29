[Research](../../ResearchNotes.md) / [Move Research](../MoveResearch.md) / Move Tutors Logic

# Move Tutors Logic, Platinum and HeartGold/SoulSilver

Source: the [pokeplatinum](https://github.com/pret/pokeplatinum) and [pokeheartgold](https://github.com/pret/pokeheartgold) decomps, cited by file and function, and the tables read from the US retail ROMs. This was structured into a document with AI.

Platinum and HeartGold sell moves from a fixed pool: each entry is a move and its price, and each species has a bit mask of which pool moves it can learn. Diamond and Pearl have no such table: the Pastoria house is the Move Relearner.

## The data

**Platinum**, overlay 5 (compressed on the cartridge).

| Offset (decompressed) | Content |
|---|---|
| `0x2FF64` | 38 moves of 12 bytes: u16 move, u8 red, blue, yellow and green shards, 2 bytes of padding (0), u32 place |
| `0x3012C` | 505 masks of 5 bytes, straight after the moves |

The places are 0 Route 212 (13 moves), 1 Survival Area (17) and 2 Snowpoint City (8), and every move costs eight shards in total. All 38 records match pokeplatinum's `res/pokemon/move_tutors.json` and all 505 masks match its per-species lists (4,187 bits set, none above bit 37). The struct is in `include/tutor_movesets.h`.

**HeartGold**, overlay 1, and one file.

| Where | Content |
|---|---|
| overlay 1 `0x23AE0` | 52 moves of 4 bytes: u16 move, u8 BP cost, u8 tutor (`sTutorMoves`, pokeheartgold `src/field/scrcmd_move_tutor.c`) |
| `fielddata/wazaoshie/waza_oshie.bin` | 505 masks of 8 bytes, 4,040 bytes; the game asserts that size |

The tutors are 0, 1 and 2 for the three Frontier Access tutors (top left, top right, bottom right) and 3 for Headbutt (`include/constants/scrcmd.h`), with 14, 21, 16 and 1 moves. Costs are 40 BP for 17 moves, 32 for 14, 48 for 13, 64 for 7, and 0 for Headbutt. Moves 0 to 37 are Platinum's in the same order; 38 to 50 are Super Fang, Pain Split, String Shot, Tailwind, Gravity, Worry Seed, Magic Coat, Role Play, Heal Bell, Low Kick, Sky Attack, Block and Bug Bite; 51 is Headbutt. The masks match `files/fielddata/wazaoshie/waza_oshie.json` (5,301 bits, none above bit 51). On the 38 shared moves HeartGold differs from Platinum only in Sunkern and Sunflora, which learn Earth Power.

**The counts are compiled in.** Loops and buffers use the table sizes (`NELEMS(sTeachableMoves)` and a 5-byte mask in Platinum, `NELEMS(sTutorMoves)` and a 52-entry buffer in HeartGold), so moves can be changed but not added without code changes.

**Prices take the first match.** Both games look a move's price up by the first pool entry with that move (`ScrCmd_CheckCanAffordMove`, `ScrCmd_PayShardCost` in Platinum, `ScrCmd_TutorMoveGetPrice` in HeartGold), so a move listed twice would always be charged the first entry's price.

## The tutors

**Platinum.** Each tutor script passes its place to the move selection commands (`ShowMoveTutorMoveSelectionMenu`, `CheckHasLearnableTutorMoves`) and charges shards from the pool: the Route 212 house, the Survival Area north house and the Snowpoint City east house. Two more tutors teach by script alone and are not in the pool: the Route 228 house and Grandma Wilma on Route 210.

**HeartGold.** The three Frontier Access tutors set tutor 0, 1 or 2 and charge BP from the pool. The Ilex Forest Headbutt tutor uses tutor 3 only to check that the Pokémon can learn a tutor 3 move it does not know, then teaches a hard-coded Headbutt. Blast Burn, Hydro Cannon, Frenzy Plant and Draco Meteor are taught by script alone in Blackthorn City (noted in `scrcmd_move_tutor.c`).

## Masks for species and forms

Bit n of a mask is pool move n. Row n is species n + 1 for species 1 to 493, then twelve form rows: Deoxys Attack, Defense and Speed, Wormadam Sandy and Trash, Giratina Origin, Shaymin Sky, and Rotom Heat, Wash, Frost, Fan and Mow (`MOVESET_FORM_*` in pokeplatinum `include/constants/forms.h`; the matching list in `scrcmd_move_tutor.c` in both decomps). The form rows are in the same order as personal files 496 to 507. Every other form, such as Unown, Castform, Burmy, Shellos or Arceus, shares its species' row.

## What DSPRE does

The pool and masks are edited in the Move Tutors editor (`MoveTutorEditorViewModel`, through `MoveTutorData` in `DSPRE.Core/ROMFiles`), under the Pokémon menu behind the beta gate, for Platinum and HeartGold; it is hidden for Diamond and Pearl.

| What | Edited in DSPRE | Written |
|---|---|---|
| Moves | the move, the four shard prices (Platinum) or the BP price and tutor (HeartGold), and the place (Platinum) | the pool, in place |
| Who learns what | by Pokémon, a checklist of the pool's moves; by move, a checklist of species | the masks, rows mapped from personal ids 496 to 507 to the form rows |
| Duplicates | refused, because the game charges the first entry's price | |

In HeartGold the Headbutt entry is locked and no other move can be given tutor 3, since changing it would not change what the Ilex Forest tutor teaches and would make that tutor offer Headbutt to Pokémon that cannot learn it.
