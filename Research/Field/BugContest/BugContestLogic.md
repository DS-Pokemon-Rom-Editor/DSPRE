[Research](../../ResearchNotes.md) / [Field Research](../FieldResearch.md) / Bug-Catching Contest Logic

# Bug-Catching Contest Logic, HeartGold and SoulSilver

Source: the [pokeheartgold](https://github.com/pret/pokeheartgold) decomp, mainly `src/overlay_bug_contest.c`, `include/bug_contest_internal.h` and `files/data/mushi/`, and the files read from the US retail ROM. This was structured into a document with AI.

The contest has its own encounter table and its own opponents, and scores every catch the same way for the player and the other contestants.

## The encounters

`data/mushi/mushi_encount.bin`: four sets of ten records of 8 bytes (`BUGMON`, `BUGMON_COUNT` 10), 320 bytes.

| Offset | Content |
|---|---|
| 0 | u16 species |
| 2 | u8 minimum level, u8 maximum level |
| 4 | u8 rate |
| 5 | u8 score |
| 6 | 2 bytes of padding, 0 in every record |

**Which set.** Before the National Pokédex, set 0; after it, the weekday divided by two (`BugContest_InitEncounters`). The contest only runs on Tuesdays, Thursdays and Saturdays, so after the National Pokédex those are sets 1, 2 and 3.

**Which record.** A roll of 0 to 99; the first record whose rate is at or below the roll wins (`BugContest_GetEncounterSlot`). Retail rates 80, 60, 50, 40, 30, 20, 15, 10, 5 and 0 give 20, 20, 10, 10, 10, 10, 5, 5, 5 and 5%. If the last rate is above 0 a low roll runs past the ten records.

The level is the minimum plus a random number up to the maximum. Like the Safari Zone's, contest Pokémon get one perfect IV.

## The opponents

`data/mushi/mushi_trainer.bin`: ten opponents with eight rows of 8 bytes each, 640 bytes.

| Offset | Content |
|---|---|
| 0 | u8 needs the National Pokédex |
| 1 | u8 day: a weekday, or 7 or more for any day |
| 2 | u16 species |
| 4 | u16 score |
| 6 | u16 spread |

Each contest picks five of the ten opponents at random (`BugContest_InitOpponents`), and each takes one of its allowed rows: a National Pokédex row only after the National Pokédex, and a day row only on its day. Rows without the National Pokédex stay allowed after it. The opponent scores score + random(2 × spread) − spread, from score − spread to score + spread − 1; a spread of 0, or no allowed row, divides by zero. The opponents' classes are fixed in the code (`sBugContestOpponentClasses`), and their names are lines 78 to 87 of text archive 246.

## Scoring and prizes

The player's catch scores 0 if there is none; otherwise the first record in the day's set with its species gives its score, plus level × 100 / that record's maximum level, plus the IV total × 100 / 186, plus HP × 100 / maximum HP (`BugContest_JudgePlayerMon`).

The six contestants are ranked by a selection sort with at-or-above comparisons, so the player, listed last, wins a tie for first (`BugContest_Judge`). The prizes:

| Place | Prize |
|---|---|
| 1st | a Sun Stone before the National Pokédex; after it, one at random of Sun, Moon, Fire, Thunder, Water and Leaf Stone (6 in 10) or Shiny, Dusk, Dawn and Oval Stone (4 in 10) |
| 2nd | Everstone |
| 3rd | Sitrus Berry |
| others | Shed Shell |

The player gets 20 Sport Balls, and scores show three digits.

## What DSPRE does

Both files are edited on the Bug Contest and Bug Contest Opponents tabs of the Special Encounters Editor (`BugContestEncounterFile`, `BugContestTrainerFile` in `DSPRE.Core/ROMFiles`); the opponents tab is behind the beta gate. The sets are named for when they are used: before the National Pokédex, then Tuesday, Thursday and Saturday after it. Rates are kept as read, up to 255. Saving is refused while a set's last rate is not 0, a maximum level is 0 or a maximum level is below the minimum, and while an opponent has a row with a spread of 0, or has no row it may use on a Tuesday, Thursday or Saturday, before or after the National Pokédex.
