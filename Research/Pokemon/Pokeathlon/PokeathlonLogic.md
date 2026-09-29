[Research](../../ResearchNotes.md) / [Pokemon Research](../PokemonResearch.md) / Pokéathlon Logic

# Pokéathlon Logic, HeartGold and SoulSilver

Source: the [pokeheartgold](https://github.com/pret/pokeheartgold) decomp, cited by file and function, and the archive read from the US retail ROM. This was structured into a document with AI.

Every species form has a Pokéathlon record: a base star rating for each of five stats and a range each can move in. The day, the Pokémon's personality and Aprijuice move the stars within that range.

## The records

`a/1/6/9`, `poketool/personal/performance.narc`: 554 records of 20 bytes (`PokeathlonBasePerformance` in `include/pokemon_types_def.h`).

| Offset | Content |
|---|---|
| 0 | five u8 base ratings |
| 5 to 8 | four bytes for the events, below; the decomp's struct calls them `dummy` |
| 9 | five pairs of u8 minimum and maximum |
| 19 | one byte, 0 in every record |

Stars are stored as 0 to 4 for 1 to 5, and every retail record keeps minimum ≤ base ≤ maximum ≤ 4.

**Bytes 5 to 8** are read only by the Pokéathlon events, in overlay 96 (pokeheartgold `asm/overlay_96.s`), which copies three records per participant when a course loads (`ov96_021E604C`). The decomp names neither the event functions nor the sprites, so which event and which sprite each read belongs to is not established. In retail, byte 5 is 0 or 1, byte 6 is 1, byte 7 is 2 or 3 and byte 8 is 1.

| Byte | What the events do with it |
|---|---|
| 5 | nonzero puts a sprite above the Pokémon 24 px up, zero 16 px (`ov96_021F5D3C`, `ov96_02206380`) |
| 6 | 1 to 3, else an assert (`ov96_021E6138`): picks entry 0 to 2, a pair of s32, from an `a/1/7/0` member fixed per event; the pair is a vertical offset and a radius for a circle overlap test, a hit when the distance between centres is at most the two radii added (`ov96_021EAF70`, `ov96_021F218C`) |
| 7 | 1, 2 or 3 raises a sprite 8, 8 or 16 px, else an assert (`ov96_021F1614`, `ov96_02211F38`) |
| 8 | 1, 2 or 3 becomes 3, 4 or 5 px, the offset between the Pokémon's bottom edge and two sprites drawn with it (`ov96_021E6108`, `ov96_021EA8A8`, `ov96_021EAF94`); one event instead raises a sprite 20, 32 or 40 px (`ov96_021FFAEC`) |

**Which column is which.** pokeheartgold labels the columns Power, Stamina, Jump, Skill and Speed (`ARCPERF_*` in `include/constants/pokemon.h`), but the retail data does not fit that. The species at the top of each column are Machamp in the first, Electrode and Ninjask in the second, Jumpluff in the third, Shuckle and Snorlax in the fourth and Alakazam in the fifth, which reads as Power, Speed, Jump, Stamina and Skill. That order is an inference from the data, not a public label.

**Which record a form uses.** The member is `sPokeathlonPerformanceArcIdxs[species] + form`, with no bounds check (the table is at ARM9 `0xFF7B4` in the US ROM). Species with more than one record: Pichu 2, Unown 28, Deoxys 4, Burmy 3, Wormadam 3, Shellos 2, Gastrodon 2, Rotom 6, Giratina 2, Shaymin 2 and Arceus 18.

## How the stars are worked out

`CalcBoxMonPokeathlonPerformance` and `CalcBoxmonPokeathlonStars` in `src/pokemon.c`:

1. Each stat's daily modifier is the nature's modifier for it (0, ±10 or ±35, `sPokeathlonPerformanceNatureMods`) plus 2 × the last digit of (a digit of the personality value + (day + 7 − i) × (day + i + 3)) − 9, where the day is the day of the month.
2. The modifier plus the Pokémon's Aprijuice for that stat is turned into a star change: −120 or less is −4, −80 or less −3, −40 or less −2, −15 or less −1, up to 14 is 0, up to 39 +1, up to 79 +2, up to 119 +3, and more +4 (`PokeathlonStatScoreToStars`).
3. The stars are the base plus that change, kept between the record's minimum and maximum.

The screen colours a stat by whether it ends at, below or above its base. Aprijuice is five signed bytes per party slot.

## What DSPRE does

The records are edited in the Personal Data editor's Pokéathlon section (`PokeathlonPerformance` in `DSPRE.Core/ROMFiles`), for HeartGold and SoulSilver, with the columns in the order Power, Speed, Jump, Stamina, Skill. The record of each species and form is found through the game's own table, read from the ARM9 of the US HeartGold ROM; other versions use a copy of the retail table. Bytes 5 to 8 are edited as Raised, Hitbox, Lift and Offset, named for what the events do with them.
