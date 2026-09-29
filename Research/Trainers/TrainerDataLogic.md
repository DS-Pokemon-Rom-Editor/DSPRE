[Research](../ResearchNotes.md) / [Trainer Research](TrainerResearch.md) / Trainer Data Logic

# Trainer Data Logic, Diamond/Pearl, Platinum and HeartGold/SoulSilver

Source: the [pokeplatinum](https://github.com/pret/pokeplatinum), [pokeheartgold](https://github.com/pret/pokeheartgold) and [pokediamond](https://github.com/pret/pokediamond) decomps, cited by file and function, and the files read from the US retail ROMs (Diamond v05, Platinum revision 1, HeartGold). This was structured into a document with AI.

A trainer is two files, its properties and its party, plus a name, battle messages and a class; the class carries the sprite, gender, prize money and more. How scripts reach a trainer is in [Script Numbers Logic](../Field/Scripts/ScriptNumbersLogic.md); the sprites are in [Trainer Sprites](../Graphics/TrainerSprites/TrainerSpritesLogic.md).

## Properties: 20 bytes a trainer

One NARC member per trainer (`TrainerData` in pokeheartgold `include/trainer_data.h`, `TrainerHeader` in pokeplatinum `include/struct_defs/trainer_data.h`, and pokediamond `include/trainer_data.h`).

| Offset | Content |
|---|---|
| 0 | party type: bit 0 set means the party lists moves, bit 1 set means it lists held items (`TRTYPE_*`) |
| 1 | trainer class |
| 2 | unused: Platinum names it `sprite`, but its data tool always writes 0 and nothing reads it; 0 in every retail trainer |
| 3 | party size |
| 4 | four u16 items for the trainer to use |
| 12 | u32 AI flags |
| 16 | u32 battle type, ORed into the battle; 2 is a double battle |

The battle type comes from the last trainer loaded, and scripts test it for nonzero (HeartGold `TrainerNumIsDouble`) or for not single (Platinum `Script_IsTrainerDoubleBattle`). Retail uses only 0 and 2: 23 double battle trainers in Diamond, 28 in Platinum and 25 in HeartGold.

**AI flags.** Platinum names bits 0 to 10: basic, evaluate attacks, expert, set up on the first turn, risky, prioritize extremes, Baton Pass, tag strategy, check HP, weather and harassment; bits 29 to 31 are the roaming, Safari Zone and catching tutorial AIs (pokeplatinum `generated/ai_flags.txt`). Bit 7 is forced on in double battles, and HeartGold's roaming Pokémon use bit 29 instead of a trainer's flags. Retail masks use bits up to 0x26F in HeartGold, 0x3F in Platinum and only bit 0 in Diamond, where all but four trainers are 1.

## The party

Per Pokémon, in order:

| Game | Base | Then | Last |
|---|---|---|---|
| Diamond, Pearl | u16 difficulty, u16 level, u16 species (no form) | the item if listed, the four moves if listed | |
| Platinum | u16 difficulty, u16 level, u16 species in the low 10 bits and form above | same | u16 ball capsule |
| HeartGold | u8 difficulty, u8 gender and ability override, u16 level, u16 species and form | same | u16 ball capsule |

The item comes before the moves, and the file is padded to 4 bytes (pokeplatinum `tools/dataproc/src/trainerproc.c`). HeartGold's override byte sets the gender in its low half (1 male, 2 female) and the ability in its high half (1 first, 2 second); retail uses 0, `0x20` and 2. In Platinum ten Pokémon use the form bits (Shellos, Gastrodon, Burmy and Wormadam) and 105 have a ball capsule; in HeartGold one has a capsule. A capsule of N copies record N − 1 of the ball capsule archive, and 0 means none (`Pokemon_SetBallSeal`).

**How the Pokémon are made**, in all three games (pokeplatinum `src/trainer_data.c`, pokeheartgold `src/trainer_data.c`):

- Every IV is difficulty × 31 / 255.
- The personality value is seeded from difficulty + level + species + trainer id, the random number is advanced once per class number, then shifted left 8 and 120 added for a female class or 136 otherwise.
- HeartGold applies the gender and ability override, and gives a Pokémon that knows Frustration 0 friendship. The override is kept in a variable shared by the whole party, so it carries on to the later Pokémon.

Platinum's difficulty is 16 bits: Volkner's Electivire has 2500, which gives 2500 × 31 / 255 = 303, stored in a byte as 47, and any IV value of 32 or more means random IVs. So that Electivire has random IVs.

## Names, messages, classes and prize money

| | Diamond, Pearl | Platinum | HeartGold |
|---|---:|---:|---:|
| Trainer names, text archive | 559 | 618 | 729 |
| Battle messages | 558 | 617 | 728 |
| Class names | 560 | 619 | 730 |
| Class names with an article ("a Youngster") | 561 | 620 | 731 |
| Classes | 98 names, a gender table of 97 | 105 | 129 names and prize rows, a gender table of 128 |

HeartGold's gender lookup for class 128 reads past its table.

**Battle messages.** A message table lists u16 trainer and u16 kind, grouped by trainer but not sorted, and a second table holds one u16 byte offset per trainer into it, the message line being that offset / 4. Checking whether a trainer has a kind of message stops at the first entry for another trainer; loading one scans to the end (pokeplatinum and pokeheartgold `src/trainer_data.c`). HeartGold's offset table has 735 entries for 738 trainers, so the last three already read past it.

**Name length.** Battle copies a trainer's name into a buffer of 8 characters, 7 and the end mark, and a longer name is not copied at all (`String_ToChars` asserts and returns). DSPRE reads that buffer size from the `movs r2, #8` in all three games and allows one less, 7 letters.

**Prize money** is the level of the last Pokémon in the party × 4 × 2 with an Amulet Coin or Luck Incense × the class's multiplier, doubled in a double battle that is not a tag battle. Platinum's multipliers are one byte per class (overlay 16 `0x359E0`); HeartGold's are u16 class and multiplier pairs, not in class order, searched from the top, with a missing class asserting and then taking the Youngster's (overlay 12 `0x34C04`). Diamond's overlay 11 has 98 bytes at `0x32960` that look like Platinum's table; there is no public source to confirm it.

## Adding trainers

Each trainer has a generic script entry in the shared trainer script bank, all aliases of one body, followed by the "eyes meet" approach body, whose script number is compiled into the code:

| Game | Bank | Entries | Approach entry | Trainers | Approach number, ARM9 |
|---|---:|---:|---|---:|---|
| Diamond | 1040 | 851 | 850, script 3850 | 850 | `0x5C6B8` |
| Platinum | 1114 | 929 | 928, script 3928 | 928 | `0x67BA4`, and overlay 8 `0x2C78` |
| HeartGold | 953 | 740 | 739, script 3739 | 738 | `0x641E8` |

HeartGold already has spare entries for trainers 738 and 739; Diamond and Platinum have one, for the id equal to the trainer count. Platinum's defeated flags leave room up to trainer 1039 before `FLAG_BAG_ACQUIRED`; HeartGold's limit is its last trainer index, 740, which the phone code uses to tell trainers from other callers.

Adding a trainer means a properties file, a party file, a name, a generic script entry before the approach entry and the approach number moved. The message offset table is not extended, so the new trainer reads past it as HeartGold's last three already do; what that does to its messages has not been established.

## What DSPRE does

Trainers are edited in the Trainer Editor, with a bulk editor for flags. Adding trainers goes through `TrainerRosterService`, which writes the properties, party and name, inserts the script entry, repatches the approach number (`TrainerScriptExecutablePatch`, Platinum and HeartGold; Diamond is refused) and updates rematch and phone references (`TrainerReferenceScanner`). Classes are added through `TrainerClassTableExpansion`, for English Platinum, with both the class name and the name with its article.

The name is limited to 7 letters, the length battle keeps. The battle type is shown as a double battle box and as its whole value, and bits other than 2 are kept as read. A new trainer starts as a Youngster (class 2 in all three games) with one Pokémon, a level 5 Bulbasaur, so prize money has a last Pokémon to read. In Diamond, Pearl and Platinum, byte 1 of each party record is kept as read, since there it is the high byte of the difficulty.
