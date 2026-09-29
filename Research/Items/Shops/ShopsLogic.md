[Research](../../ResearchNotes.md) / [Item Research](../ItemResearch.md) / Shops Logic

# Shops Logic: Poké Marts and the Battle Point shops, Diamond/Pearl, Platinum and HeartGold/SoulSilver

Source: the [pokeplatinum](https://github.com/pret/pokeplatinum), [pokeheartgold](https://github.com/pret/pokeheartgold) and [pokediamond](https://github.com/pret/pokediamond) decomps, cited by file and function, and the tables read from the US retail ROMs (Diamond v05, Platinum revision 1, HeartGold). This was structured into a document with AI.

A Poké Mart's stock is one of two things: the common list, filtered by how many badges the player has, or a fixed specialty list picked by an id in the script. Both are compiled-in tables reached through a single pointer each. The Battle Point shops are different in every game.

## The common marts

The mart command ignores the argument it is given. It counts the badges, turns the count into a tier, and lists every common row whose tier is at or below it, into a stack buffer of 64 items ended by 0xFFFF (`ScrCmd_PokeMartCommon` in pokeplatinum `src/scrcmd_shop.c`, `ScrCmd_NormalMart` in pokediamond `arm9/src/scrcmd_mart.c`, `ScrCmd_MartBuy` in pokeheartgold `src/scrcmd_mart.c`).

| Badges | 0 | 1 to 2 | 3 to 4 | 5 to 6 | 7 | 8 | 9 to 16 (HeartGold) |
|---|---|---|---|---|---|---|---|
| Tier | 1 | 2 | 3 | 4 | 5 | 6 | 6 |

The rows are u16 item and u16 tier, 19 of them, the same in all three games (`PokeMartCommonItems` in pokeplatinum `include/data/mart_items.h`):

| Tier | Items |
|---|---|
| 1 | Poké Ball, Potion, Antidote, Parlyz Heal |
| 2 | Super Potion, Awakening, Burn Heal, Ice Heal, Escape Rope, Repel |
| 3 | Great Ball, Revive, Super Repel |
| 4 | Ultra Ball, Hyper Potion, Full Heal, Max Repel |
| 5 | Max Potion |
| 6 | Full Restore |

The row count is the immediate of a `cmp r2, #19`, and the table and the specialty list table are each reached through one literal in the ARM9:

| Game | Count | Common table literal | Specialty table literal |
|---|---|---|---|
| Diamond | `0x3FD94` | `0x3FDB4` | `0x3FE04` |
| Platinum | `0x46B74` | `0x46B94` | `0x46BF0` |
| HeartGold | `0x48100` | `0x48124` | `0x48190` |

In HeartGold the Poké Ball is removed from every list, common or specialty, while flag `0x9A` is clear, and a list of 255 items or more asserts (`src/overlay_03/shop_menu.c`). Platinum's shop stops reading a list at 256 items (`MAX_SHOP_ITEMS`).

## The specialty marts

The script passes an id, and the command takes that entry of a table of pointers to lists of u16 items ended by 0xFFFF, with no bounds check (`ScrCmd_PokeMartSpecialties`, `ScrCmd_SpecialMart`, `ScrCmd_SpecialMartBuy`). Diamond has 19 lists, Platinum 20 (id 19 is the Veilstone Department Store B1F berries) and HeartGold 30. Diamond's list contents are stubbed out in pokediamond, so the ROM is the only public source for them; they match Platinum's ids 0 to 18.

In Diamond, Pearl and Platinum, ids 8 to 13, and 19 in Platinum, are the Veilstone Department Store and count towards its purchase record; HeartGold has no such count.

HeartGold's ids, from the `SetVar 0x8004` before each special mart call in its scripts:

| Id | Mart | Id | Mart |
|---:|---|---:|---|
| 0 | Cherrygrove City | 15 | Saffron City |
| 1 | Violet City | 16 | Lavender Town |
| 2 | Azalea Town | 17 | Cerulean City |
| 3, 4 | Goldenrod Department Store 2F | 18, 19 | Celadon Department Store 2F |
| 5 | Goldenrod Department Store 3F | 20 | Celadon Department Store 3F |
| 6 | Goldenrod Department Store 4F, also the Safari Zone Gate | 21 | Celadon Department Store 4F |
| 7 | Goldenrod Department Store 5F | 22, 23 | Celadon Department Store 5F |
| 8 | Goldenrod Underground herb shop | 24 | Fuchsia City |
| 9 | Ecruteak City | 25 | Pewter City |
| 10 | Olivine City | 26 | Viridian City |
| 11 | Cianwood City pharmacy | 27 | Mt. Moon Square |
| 12 | Blackthorn City, also Frontier Access | 28, 29 | Mahogany Town souvenir shop |
| 13 | Pokémon League entrance | | |
| 14 | Vermilion City, also the Safari Zone Gate | | |

## The Battle Point shops

**Platinum.** The Battle Frontier's two counters call `PokeMartFrontier` with 0 for the right counter (15 TMs) and 1 for the left (26 items). Each list is items ended by 0xFFFF in the ARM9, pointed at from `0x100AF0` and `0x100AF4`. The prices are a separate table of 41 rows, u16 item and u16 price, in overlay 7 at `0x5A98`, searched from the top so the first row for an item wins and an item with no row costs 0 (`Shop_GetItemBPPrice` in pokeplatinum `src/overlay007/shop_menu.c`); its count is a `cmp r2, #41` at overlay 7 `0x4F82`. The ARM9 has a second, identical 41-row item and price table, split into the two counters by a `movs r1, #26`, read only by a script command whose only callers are unused labels, so retail Platinum never reads it.

**Diamond and Pearl.** One 41-row table of item and price in the ARM9 at `0xF433E`, split between the two counters by a `mov r1, #26` at `0x42AEA`: rows 0 to 25 are one counter's items and 26 to 40 the TMs. The table is the same as Platinum's.

**HeartGold.** There is no table. The Battle Frontier's exchange is written out in its script: each purchase sets the item, the quantity and the price in variables `0x8004` to `0x8006`, checks the Battle Points and gives the item. The menu text comes from a message archive.

Decoration, Seal and Pokéathlon shops are separate lists; the Pokéathlon lists carry their own prices.

## Space for longer lists

When a list grows past what fits where it is, DSPRE moves it into the synthetic overlay, the ARM9 expansion file loaded at `0x023C8000` (Diamond, Pearl and Platinum: `weather_sys.narc` member 9; HeartGold: `a/0/2/8` member 0). Every block DSPRE writes there starts with a 12-character marker, a version at `+0x0C` and its total length at `+0x10`, and new blocks are placed in the first free, 4-byte aligned run of zeroes outside:

- every marked block, by its stored length (`MARTEXPANDV1`, `BPSHOPEXPV1`, `TYPECHARTXP1`, `SWARMTABLEX1`, `VSCLASSTBL01`),
- the overworld sprite table expansion, in Diamond, Pearl and Platinum,
- a type chart moved there by another patch without a marker,
- the blocks of the PlatPatches expansions (`EXTRATMSV1`, `ITEMEXPV2`, `ITEMEXPV1`),
- in Platinum, the fixed range `0x10000` to `0x16000`.

## What DSPRE does

The marts are edited in the Mart Editor and the Battle Point shops in the Battle Point Shop editor, both under the Items menu behind the beta gate. The Battle Point Shop editor is hidden in HeartGold, which has no table.

| What | Edited in DSPRE | Written |
|---|---|---|
| Common list | items and their tiers, 0 to 6, up to 63 items | in place while the count is unchanged; otherwise a `MARTEXPANDV1` block in the expansion (header, common rows, pointer table, lists), the two ARM9 literals and the count byte |
| Specialty lists | each list's items | in place, or in the same block |
| Battle Point shop, Platinum | both counters' items and every price | in place; a `BPSHOPEXPV1` block when a counter passes 26 items or 15 TMs or there are more than 41 prices, with both ARM9 list pointers, the two overlay 7 price literals and the count; the unused ARM9 copy is kept in step while everything fits in 41 rows |
| Battle Point shop, Diamond | items and prices, with the 26 and 15 split fixed | the table in place |
