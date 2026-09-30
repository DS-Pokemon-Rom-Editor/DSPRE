[Research](../../ResearchNotes.md) / [Field Research](../FieldResearch.md) / Special Encounters Logic

# Special Encounters Logic: Headbutt, the Safari Zone, honey trees, the Great Marsh and the Trophy Garden

Source: the [pokeplatinum](https://github.com/pret/pokeplatinum), [pokeheartgold](https://github.com/pret/pokeheartgold) and [pokediamond](https://github.com/pret/pokediamond) decomps, cited by file and function, and the files read from the US retail ROMs. This was structured into a document with AI.

These are the encounters that do not come from a map's ordinary wild file, or that change it in their own way. The slot chances of the ordinary methods and the order in which these replace walking slots are in [Encounter Slots Logic](EncounterSlotsLogic.md).

## Headbutt, HeartGold and SoulSilver

**The files.** One file per map header, in the headbutt archive, the member numbered by the header (pokeheartgold `include/field/headbutt.h`):

| Part | Content |
|---|---|
| header | u16 count of regular tree groups, u16 count of secret tree groups |
| slots | common 6, rare 6 and secret 6, each u16 species, u8 min level, u8 max level |
| trees | (regular + secret) groups × 6 trees of s16 x, s16 y; −1, −1 for an unused place |

Of the 540 retail files, 480 are 4 bytes with no trees and 60 have trees, each exactly the size the counts give; the most regular groups in one file is 56. Only six files have secret groups: 29 (11 regular, 4 secret), 42 (13, 1), 51 (21, 2), 67 (7, 4), 96 (22, 5) and 151 (10, 4).

**Picking the table** (`src/field/headbutt.c`). The tree in front of the player is matched against the coordinate lists. A regular tree's group is its position divided by six, and `Headbutt_GetTreeType_Regular` looks the table up by the trainer ID mod 10 and the group in one of five tables chosen by how many groups the map has (`sRareTreeLUT_1` to `_5Plus`; from five groups on, the group is taken mod 5). The result is common, rare or nothing. A tree in the secret list always uses the secret table. A match gives a battle with no chance roll, though a lead with Keen Eye or Intimidate can still turn away a low level one. The slot chances are 50, 15, 15, 10, 5 and 5.

**The trainer ID is the full one.** The ID used is `PlayerProfile_GetTrainerID`, the whole 32-bit ID (pokeheartgold `src/player_data.c`), not the 16-bit number on the Trainer Card. Its last decimal digit is (6 × secret ID + visible ID) mod 10, which is usually not the last digit of the number the player sees.

## The Safari Zone, HeartGold and SoulSilver

**The files.** One file per area, `a/2/3/0` members 0 to 11, the areas named as in pokeheartgold `include/constants/safari.h`, Plains to Desert. Each file is 912 bytes (`SafariZoneAreaSet_LoadAreaEncounters` in `src/unk_02097268.c`):

| Part | Content |
|---|---|
| bytes 0 to 4 | bonus slot count for walking, surfing, Old Rod, Good Rod, Super Rod; bytes 5 to 7 padding |
| then, per method | 10 base slots for each of morning, day and night, u16 species and u16 level |
| | the bonus slots for each time, the same layout |
| | per bonus slot: object type and level needed, then a second object type and level (type 0 for none) |

Every retail area has 10, 3, 2, 2 and 2 bonus slots. The object types are 0 none, 1 plains, 2 forest, 3 peak and 4 water.

**How placed objects unlock slots.** Each object placed in the area adds points for its type, 1 to 7 depending on how long the area has been in the layout (days counted up to 255, in tens, through `sObjectLevelBoosts`). A bonus slot unlocks when the first type's points reach its level and, if it has a second type, that type's points reach its level too. Unlocked bonus slots overwrite base slots 0, 1, 2 and on, up to ten. The values in the file are points, not a count of objects.

**Picking a Pokémon.** The slot is picked evenly out of ten and the level is the slot's single value. Safari Zone and Bug-Catching Contest Pokémon get one perfect IV. The encounter rates are still the map's ordinary wild file's. An area with no surfing bonus slots makes surfing and all three rods give ten Magikarp at level 5 instead; no retail area has that.

## Honey trees, Diamond, Pearl and Platinum

The honey tree Pokémon are in `arc/encdata_ex`: groups A, B and C are members 2, 3 and 4 in Diamond and Platinum, and 5, 6 and 7 in Pearl (pokediamond's overlay 5, under `.ifdef DIAMOND`; `sEncounterTableIndexes_*` in pokeplatinum `honey_tree.c`). In Platinum members 5 to 7 are the same bytes as 2 to 4; in Diamond they differ, Pearl having Cascoon where Diamond has Silcoon.

| Tree | Group C | Nothing | Group A | Group B |
|---|---:|---:|---:|---:|
| Ordinary | | 10% | 70% | 20% |
| Munchlax tree | 1% | 9% | 20% | 70% |

Slathering the same tree again keeps its group 90% of the time and picks a new slot. The slot chances are 40, 20, 20, 10, 5 and 5, and the level is 5 to 15, with a 50% chance of 15 behind a lead with Hustle, Vital Spirit or Pressure. Each player has four Munchlax trees, taken from the bytes of the 32-bit trainer ID mod 21, with a clash moved to the next tree (`IsMunchlaxTree`).

## The Great Marsh, Diamond, Pearl and Platinum

The daily Pokémon are `arc/encdata_ex` member 9 after the National Pokédex and member 10 before it, 32 u32 species each. Each of the six areas takes its own five bits of one daily number, `(daily >> (5 × area)) & 0x1F`, and the species replaces both walking slots 6 and 7, only during a Safari Game (pokeplatinum `great_marsh_daily_encounters.c`). The binoculars show the same species, with their coordinates in member 11. The daily number is the same one that picks the day's swarm (see [Swarms Logic](SwarmsLogic.md)).

## The Trophy Garden, Diamond, Pearl and Platinum

Member 8 holds 16 u32 species. A script command (`TrophyGarden_AddNewMon`) picks a new one different from both current ones, and the old first one becomes the second. They replace walking slots 6 and 7 after the National Pokédex. Diamond and Platinum differ in one entry, index 9.

## What DSPRE does

All of these are edited in the Special Encounters Editor (`SpecialEncountersEditorViewModel`), one tab each for the game: Headbutt, Safari Zone, Bug Contest and Bug Contest Opponents in HeartGold, Honey Tree, Great Marsh and Trophy Garden in Diamond, Pearl and Platinum, and Swarms in all three.

| What | Edited in DSPRE | Written |
|---|---|---|
| Headbutt | the slots, and the trees drawn on the map; the counts are read as u16, and slots filled in on a map with no trees read back | the header's headbutt file; a file with no trees and empty slots stays 4 bytes |
| Tree tables | a tree's hover shows which ID keys, (6 × secret ID + trainer ID) mod 10, get the common table, the rare one or nothing (`HeadbuttRules`, which matches the game's tables) | |
| Safari Zone | each area's base and bonus slots and the object requirements, as a type and the points needed; a warning shows on object type 0 and on a surfing bonus count of 0 | the area file |
| Honey trees, Great Marsh, Trophy Garden | the species lists | the `encdata_ex` members |

The Trophy Garden list is refused with fewer than three different entries, since the game would never finish picking a new daily Pokémon.
