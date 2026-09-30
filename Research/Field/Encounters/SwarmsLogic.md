[Research](../../ResearchNotes.md) / [Field Research](../FieldResearch.md) / Swarms Logic

# Swarms Logic, Diamond/Pearl, Platinum and HeartGold/SoulSilver

Source: the [pokeplatinum](https://github.com/pret/pokeplatinum), [pokeheartgold](https://github.com/pret/pokeheartgold) and [pokediamond](https://github.com/pret/pokediamond) decomps, cited by file and function, and the tables read from the US retail ROMs. This was structured into a document with AI.

A swarm is one place, picked each day, where a Pokémon is suddenly common. The table only lists the places; the Pokémon comes from that place's own wild file, and the number of places is compiled into the code.

## Picking the day's swarm

**Diamond, Pearl and Platinum.** The place is `swarmDaily % NUM_SWARMS` (pokeplatinum `src/overlay006/swarm.c`, `NUM_SWARMS` 22 in `include/overlay006/swarm.h`). `swarmDaily` is set when the save is made and replaced with a new random number every day (`SpecialEncounter_SetMixedRecordDailies`, called from `src/unk_020559DC.c`); the same call sets the Great Marsh daily number to the same value, so the day's swarm and the Great Marsh Pokémon come from one number. Swarms only happen after the script command that enables them (`SpecialEncounter_EnableSwarms`).

**HeartGold.** The place is the day's random number `% SWARM_MAP_COUNT`, 20 (`sub_02097F6C` in pokeheartgold `src/unk_02097F6C.c`). The number is set when the save is made and replaced daily. A row applies only where both its map and its method match. Swarms only happen after `ScrCmd_EnableMassOutbreaks`.

## The table

| Game | Where | Rows | Row |
|---|---|---:|---|
| Diamond and Pearl | overlay 6 `0x17CA0` (`ov06_02251340`) | 28 | u32 map header |
| Platinum | overlay 6 `0xAF50` | 22 | u32 map header |
| HeartGold | ARM9 `0x108F4C` (`sSwarmMapLUT`) | 20 | u16 map header, u16 method: 0 walking, 1 surfing, 2 fishing |

Platinum's rows are headers 342, 343, 344, 350, 353, 354, 356, 380, 382, 385, 388, 392, 395, 399, 400, 469, 403, 406, 407, 471, 200 and 203; Diamond has six more (373, 383, 312, 315, 318 and 204). HeartGold's first row is Route 1 walking, and its fishing and surfing rows include Routes 12, 32, 44 and Violet City (fishing) and Routes 19, 27 and Vermilion City (surfing).

**The row count is compiled in, twice per game**, and everything that reads the table goes through one of those two functions:

| Game | Functions | Count, as a `movs r1, #count` | Other callers |
|---|---|---|---|
| Diamond | `ov06_022458FC`, `GetSwarmInfoFromRand` | overlay 6 `0xC25E` and `0xC276`, 28 | |
| Platinum | `Swarm_GetMapId`, `Swarm_GetMapIdAndSpecies` | overlay 6 `0x50DA` and `0x50F2`, 22 | the TV report and a script command use the second |
| HeartGold | `sub_02097F6C`, `GetSwarmInfoFromRand` | ARM9 `0x97F70` and `0x97FA2`, 20 | the Pokégear radio and a script command |

## Which slots a swarm takes

The swarm Pokémon is read from the chosen place's own wild file: in Diamond, Pearl and Platinum its first swarm species, in HeartGold the swarm species for the row's method. HeartGold asserts that species is not zero.

| Game | Walking | Surfing | Old Rod | Good Rod | Super Rod |
|---|---|---|---|---|---|
| Diamond, Pearl, Platinum | slots 0, 1 (40%), on grass only, including Sweet Scent and the Great Marsh mud | no | no | no | no |
| HeartGold | slots 0, 1 (40%) | slot 0 (60%) | slot 2 (15%) | slots 0, 2, 3 (65%) | all five (100%) |

## What DSPRE does

The table is edited on the Swarms tab of the Special Encounters Editor (`SwarmsViewModel`, through `SwarmTable` in `DSPRE.Core/ROMFiles`), behind the beta gate, in all three games.

| What | Edited in DSPRE | Written |
|---|---|---|
| Places | the rows, each a map header and, in HeartGold, a method | the table in place while it fits |
| More places than fit | the table moves | a block marked `SWARMTABLEX1` in the synthetic overlay (a 0x20-byte header with its version, length and row count), the table's address words, and both count bytes |

The count is an 8-bit immediate, so a table holds at most 255 rows, and an empty table is refused because the game divides by the count. DSPRE checks that each place has a wild file. In HeartGold it also refuses a row whose wild file has no swarm species for the row's method, since the game asserts on it; Diamond, Pearl and Platinum are not checked for the species.
