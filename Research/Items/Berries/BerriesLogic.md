[Research](../../ResearchNotes.md) / [Item Research](../ItemResearch.md) / Berries Logic

# Berries Logic, Diamond/Pearl, Platinum and HeartGold/SoulSilver

Source: the [pokeplatinum](https://github.com/pret/pokeplatinum) and [pokeheartgold](https://github.com/pret/pokeheartgold) decomps, cited by file and function, and the records read from the US retail ROMs. This was structured into a document with AI.

Every berry has a small record of how it looks, how it grows and what it tastes like. The records are the same bytes in all three games; what differs is which parts each game reads.

## The records

64 NARC members of 12 bytes, one per berry in item order from Cheri (item 149) to Rowap (212). The count is compiled in. `BerryData` in pokeplatinum `include/berry_data.h`, with the values in `res/items/nuts_data.csv`; `BerryFile` in pokeheartgold `src/overlay_16_022014A0.c`.

| Offset | Content | Retail range |
|---|---|---|
| `0x00` | u16 size in millimetres | |
| `0x02` | u8 firmness: 1 Very Soft, 2 Soft, 3 Hard, 4 Very Hard, 5 Super Hard | |
| `0x03` | u8 yield | 1 to 3 |
| `0x04` | u8 hours per growth stage | 2 to 24 |
| `0x05` | u8 how fast it dries the soil | 4 to 35 |
| `0x06` | u8 spicy, dry, sweet, bitter and sour | |
| `0x0B` | u8 smoothness | 20 to 60 |

| Game | Archive |
|---|---|
| Diamond, Pearl and Platinum | `itemtool/itemdata/nuts_data.narc` |
| HeartGold and SoulSilver | `a/0/6/6` |

## Who reads what

| Reader | Fields |
|---|---|
| Platinum berry growth (`src/berry_patches.c`) | hours per stage, drying, yield |
| Platinum berry tag | size, firmness, the five flavours |
| Platinum Poffin making | the five flavours, smoothness |
| HeartGold berry growth (`ov16_022014A0`) | hours per stage, drying, yield |

A stage lasts the berry's hours × 60 minutes, changed by mulch, and a plant bears yield × a rating that starts at 5 and drops as the soil dries (`CalcBerryYield`, `CalcMinutesRemainingInStage`, `CalcMoistureDrainRate`; HeartGold's overlay 16 has the same shape). In HeartGold the only named reader of the archive is the growth code, so size, firmness, flavours and smoothness look unread there; other readers that load the archive by number have not been searched for.

## What DSPRE does

The records are edited in the Berry Data editor (`BerryDataEditorView`, through `BerryData` in `DSPRE.Core/ROMFiles`), under the Items menu behind the beta gate, for all three games. Each berry's fields are edited and written back to its own member. In HeartGold the fields the game appears not to read are marked as such (`GameReadsLooks`).
