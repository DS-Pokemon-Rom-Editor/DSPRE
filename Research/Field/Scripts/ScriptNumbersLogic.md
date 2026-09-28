[Research](../../ResearchNotes.md) / [Field Research](../FieldResearch.md) / Script Numbers Logic

# Script Numbers Logic, Diamond/Pearl, Platinum and HeartGold/SoulSilver

Source: the [pokediamond](https://github.com/pret/pokediamond), [pokeplatinum](https://github.com/pret/pokeplatinum) and [pokeheartgold](https://github.com/pret/pokeheartgold) decomps. This was structured into a document with AI.

Every event on a map holds a script number, and that number is not an index into the map's own script file. It is read against a table of ranges first, so that most of the number space belongs to shared archives that no map owns. This covers what a given number means, what the numbers outside the map's own file do, and what the three games disagree about.

Diamond and Pearl's script manager is still assembly in its decomp, with no named constants anywhere, so every Diamond number below is decoded from a cited instruction or literal rather than read off a name.

## Resolving one number

HeartGold walks a table and stops at the first range the number falls into (`src/script_manager.c`):

```c
u16 LoadScriptsAndMessagesByMapId(FieldSystem *fieldSystem, ScriptContext *ctx, u16 scriptId) {
    const struct ScriptBankMapping *mapping_p = sScriptBankMapping;
    int i;

    for (i = 0; i < NELEMS(sScriptBankMapping); i++) {
        if (scriptId >= mapping_p[i].scriptIdLo) {
            LoadScriptsAndMessagesParameterized(fieldSystem, ctx, mapping_p[i].scriptBank, mapping_p[i].msgBank);
            return scriptId - mapping_p[i].scriptIdLo;
        }
    }
    if (scriptId >= 1) {
        LoadScriptsAndMessagesForCurrentMap(fieldSystem, ctx);
        return scriptId - 1;
    } else {
        LoadScriptsAndMessagesParameterized(fieldSystem, ctx, NARC_scr_seq_scr_seq_0140_bin, NARC_msg_msg_0184_bin);
        return 0;
    }
}
```

So there are three outcomes, and all three games share them.

**A number of 1 or more, below the lowest range, is the map's own.** It loads the archive named by the map header and returns `scriptId - 1`, so script 1 is the first entry of that file. The index becomes an offset through a table at the start of the file: the loaded pointer advances by four bytes per index and then by the relative word found there (`ScriptRunByIndex`; Platinum `ScriptContext_JumpToOffsetID`, `src/script_manager.c`; Diamond `ScriptRunByIndex`, `arm9/asm/unk_02038C78.s`).

**A number inside a range belongs to a shared archive.** The number minus the range's base is the index into that archive, and the range carries its own message archive with it, so a common script reads its text from somewhere the map never names.

**Zero is none of those.** It loads a fixed archive at index 0 rather than anything belonging to the map. In HeartGold that is scr_seq member 140 with msg member 184, whose only script is a single `End`. Platinum does the same with its own dummy pair (`src/script_manager.c`):

```c
    else
    {
        ScriptContext_Load(fieldSystem, ctx, scripts_unk_0402, TEXT_BANK_DUMMY_0355);
        retScriptID = 0;
    }
```

Diamond reaches the same place in assembly, loading archive 369 with message 317 (`arm9/asm/unk_02038C78.s`, the literal `=0x00000171` being 369 and `sub r3, #0x34` giving 317):

```
_020391EE:
	cmp r4, #0x1
	blo _020391FE
	bl LoadScriptsAndMessagesForCurrentMap
	sub r0, r4, #0x1
...
_020391FE:
	ldr r2, _020392B0 ; =0x00000171
	add r3, r2, #0x0
	sub r3, #0x34
	bl LoadScriptsAndMessagesParameterized
	mov r4, #0x0
```

The practical reading of script 0 is therefore **no script**. The game still loads something and still runs it, which is why an event set to 0 locks and unlocks the player rather than being skipped, but nothing observable happens and nothing in the map's own file is reached. An event left at 0 is an event with nothing attached to it, not an event pointing at the first script.

## The ranges

The shape of the lookup differs in all three games even though the numbers barely move.

Diamond has no table at all. The function is a hand written chain of twenty seven comparisons with the archive and message numbers as immediates (`arm9/asm/unk_02038C78.s`). Platinum keeps a compile time chain but generates it from an X macro so the bases have names (`SCRIPT_RANGE_TABLE`, `src/script_manager.c`, expanded in `ScriptContext_LoadAndOffsetID`). HeartGold is the only one with a real runtime table:

```c
struct ScriptBankMapping {
    u16 scriptIdLo;
    u16 scriptBank;
    u16 msgBank;
};
```

`src/script_manager.c`, as `sScriptBankMapping` with thirty entries. That is the only one of the three that could be reordered or extended without touching code.

The bases that matter for an editor are the low ones, and none of them moved between games:

| Base | Diamond/Pearl | Platinum | HeartGold/SoulSilver |
|---|---|---|---|
| 2000 | common scripts | `COMMON_SCRIPTS` | `_std_misc` |
| 2500 | scenery scripts | `BG_EVENTS` | `_std_bookshelves` |
| 2800 | berry trees | `BERRY_TREE_INTERACTIONS` | `_std_apricorn_tree` |
| 3000 | first trainer | `SINGLE_BATTLES` | `_std_npc_trainer` |
| 5000 | second trainer | `DOUBLE_BATTLES` | `_std_npc_trainer_2` |
| 7000 | item balls | `VISIBLE_ITEMS` | `_std_item_ball` |
| 8000 | hidden items | `HIDDEN_ITEMS` | `_std_hidden_item` |

Above 8000 the three diverge. Platinum added three high ranges Diamond never had, at 10400, 10450 and 10490, where Diamond's chain stops at 10300. HeartGold dropped the VS Seeker at 8950, the Poké Radar at 8970 and Poffins at 9400, and added the Pokéathlon at 9850, the Trainer House at 10350 and the Battle Frontier move tutor at 10440. Counting them gives Diamond twenty seven ranges and the other two thirty each.

Diamond's chain, decoded from `LoadScriptsAndMessagesByMapId` in `arm9/asm/unk_02038C78.s`, where every compared threshold is also the base that is subtracted. Each range loads the script archive and message archive given, and a number from 1 to 1999 loads the current map's own files:

| Base | Scripts | Messages | Base | Scripts | Messages | Base | Scripts | Messages |
|---|---|---|---|---|---|---|---|---|
| 10300 | 977 | 496 | 9700 | 387 | 378 | 8970 | 390 | 7 |
| 10200 | 373 | 332 | 9600 | 377 | 199 | 8950 | 463 | 486 |
| 10150 | 1042 | 562 | 9500 | 464 | 492 | 8900 | 389 | 380 |
| 10100 | 1041 | 563 | 9400 | 391 | 381 | 8800 | 462 | 485 |
| 10000 | 375 | 334 | 9300 | 372 | 329 | 8000 | 374 | 333 |
| 9950 | 376 | 335 | 9200 | 388 | 379 | 7000 | 370 | 325 |
| 9900 | 365 | 199 | 9100 | 0 | 9 | 5000 | 1040 | 199 |
| 9800 | 206 | 203 | 9000 | 207 | 207 | 3000 | 1040 | 199 |
| | | | | | | 2800 | 378 | 350 |
| | | | | | | 2500 | 1 | 13 |
| | | | | | | 2000 | 205 | 199 |

Message archive 199 is the common text that Platinum keeps in 213, 9000 is the communication club in both, and 9100 loads script archive 0 with the "take your designated position" text in both games.

Several bases kept their number and changed their meaning, which matters when reading a number out of one game with another game's names to hand. 2800 is a berry tree in Diamond and Platinum and an apricorn tree in HeartGold. 9000, 9100 and 9200 are Pokémon Center floors and the communication club in Diamond and Platinum, and wireless reception, the Colosseum and Wi-Fi reception in HeartGold. 2500 is called bookshelves in HeartGold and BG events in Platinum, but it is the same nine scenery scripts, 2500 to 2508, in all three.

Two individual common scripts also moved in HeartGold: the Pokémon Center PC is 2018 in Diamond and Platinum and 2010 in HeartGold (`include/constants/std_script.h`), and the bike rack is 2030 there against 2020 here.

## Trainers, items and hidden items

A trainer script number is the trainer id plus a base. The arithmetic is the same everywhere: `scriptId - base + 1`, with 3000 for the first battler and 5000 for the second, so script 3001 is trainer 1 and script 5001 is the same trainer as the partner in a double battle (`ScriptNumToTrainerNum`, HeartGold `src/script_manager.c`; `Script_GetTrainerID`, Platinum `src/script_manager.c`; Diamond `arm9/asm/unk_02038C78.s`). Which slot a number belongs to is simply whether it is 5000 or more, and whether the battle is double at all comes from the trainer's own data rather than from the number.

One script past the last trainer is the approach script, run when a trainer notices the player, and that number differs because the trainer counts do: 3850 in Diamond, 3928 in Platinum and 3739 in HeartGold.

Item balls take 7000 plus an index. Hidden items take 8000 plus an index, and their flag is that index plus a flag base, which is 730 in Diamond and Platinum and 800 in HeartGold (`include/constants/flags.h`). Trainer defeated flags use base 0x550 in all three.

## What each event kind passes in

The three event kinds each answer "what is here" differently, and only one of them has a sentinel.

**Overworlds** hand over their own script number, except that an object whose type is 9 always passes 0 instead, which is the shared message script rather than the object's own (HeartGold `src/field/field_control.c`; Platinum `src/overlay005/field_control.c`; Diamond `arm9/overlays/05/asm/ov05_021D80E8.s`). There is no "no object" number here: the lookup returns a boolean and the interaction simply does not happen.

**Signs and other BG events** are found by position and facing, and the lookup returns **0xFFFF** when there is nothing there. A hidden item BG event additionally requires its flag to be unset. Wall signs have their own narrower lookup that only answers when the player faces north. The record is twenty bytes in all three games, in the same field order, and a facing value of 4 means any direction.

**Triggers** are checked once per step, before map transitions. A trigger matches when the player is inside its rectangle and its watched variable equals its expected value, and the lookup again returns 0xFFFF for nothing (HeartGold `asm/unk_0203DB6C.s`; Platinum `src/unk_0203C954.c`; Diamond `arm9/asm/unk_02037024.s`). The record is sixteen bytes.

So 0xFFFF is the "nothing here" answer the lookups return, not a value an editor should write into an event. The overworld sentinel is a different matter: an overworld whose script number is 0xFFFF is an alias entry rather than a script.

## Level scripts

Level scripts are a separate table, in its own member of the scripts archive, named by its own header field and read into a 256 byte buffer when the map loads. The format is identical in all three games.

Each record is five bytes: a type byte and four bytes of payload. A type byte of **0 ends the table**. For types 2, 3 and 4 the first two payload bytes are the script number and the rest is skipped (`GetMapLoadScriptId`, HeartGold `src/script_manager.c`):

```c
u16 GetMapLoadScriptId(u8 *header, u8 type) {
    while (1) {
        if (header[0] == 0) {
            return 0xFFFF;
        } else if (header[0] == type) {
            return header[1] + (header[2] << 8);
        } else {
            header += 5;
        }
    }
}
```

The four types are the same numbers everywhere, named in Platinum and HeartGold and bare immediates in Diamond:

| Type | Name | When it runs |
|---|---|---|
| 1 | on frame table | before every frame of input processing |
| 2 | on transition | on first entering the map, before the other types |
| 3 | on resume | after the map is loaded and drawn |
| 4 | on load | after the layout is loaded, before it is drawn |

Type 1 is not a script number. Its payload is a four byte relative offset to a second table of six byte rows, each holding two variables and a script number, terminated by a first variable of 0; the first row whose two variables compare equal wins. Type 1 runs as a task, while types 2, 3 and 4 run synchronously to completion in a throw away context.

## Changing a script number by hand

Every event kind keeps its script number as a plain little endian `u16`, so pointing an event somewhere else is a two byte edit in the map's events file. The file is four sections, each a `u32` count followed by that many records, read in this order (Platinum `MapHeaderData_ParseEvents`, `src/map_header_data.c`, record layouts at `include/map_header_data.h`):

| Section | Record size | Script number at |
|---|---|---|
| BG events (signs, hidden items, scenery) | 20 bytes | +0x00 |
| Object events (overworlds) | 32 bytes | +0x0A |
| Warps | 12 bytes | no script |
| Coord events (triggers) | 16 bytes | +0x00 |

The overworld record in full, since its script sits in the middle:

| Offset | Size | Field |
|---|---|---|
| 0x00 | 2 | local id |
| 0x02 | 2 | graphics id |
| 0x04 | 2 | movement type |
| 0x06 | 2 | trainer type |
| 0x08 | 2 | hidden flag |
| 0x0A | 2 | script |
| 0x0C | 2 | facing direction |
| 0x0E | 6 | three data words (sight range, then two parameters) |
| 0x14 | 2 | movement range X |
| 0x16 | 2 | movement range Z |
| 0x18 | 2 | tile X |
| 0x1A | 2 | tile Z |
| 0x1C | 4 | height, `fx32` |

So to make the third overworld of a file run common script 2018, skip the first count and its signs, skip the overworld count, and write `E2 07` at `0x0A + 2 * 0x20` into the overworld section. The same bytes in a trigger go at the start of its record.

A level script table is a run of five byte records ending in a zero byte. To add an on-transition script 12 to a table that currently holds only the terminator, the bytes become `02 0C 00 00 00 00`: type 2, script 12 as a `u16`, two bytes the game skips, then the new terminator. The whole table has to fit the 256 byte buffer the game reads it into (`initScripts[64]`, `include/map_header_data.h`). A type 1 record's offset counts from the end of its own five bytes (`FieldSystem_GetFrameTableInitScriptID`, `src/script_manager.c`), so a record inserted before it moves both and needs nothing, while a record inserted between it and its frame table needs five added to the offset.

## What DSPRE does

| What | Edited in DSPRE | Written | Left untouched |
|---|---|---|---|
| Shared ranges | resolved per game by `CommonScriptId.Resolve`, with Diamond/Pearl's twenty seven ranges, Platinum's and HeartGold's thirty | | the range chains and table in the code |
| Trainer scripts | trainer id to script and back with the game's arithmetic (`TrainerScripts` in `DSPRE.Core/ROMFiles/FieldPlayer.cs`, 3000 is trainer 1) | the event's script field | |
| Script 0 | "No script" first in the script dropdown for overworlds, triggers and signs; labels and the preview say it runs nothing | 0 | a raw 0 is never treated as the first script in the file |
| Level scripts | the Level Script editor, with the four type codes and the terminator; the trigger list shows a type 1 row with its two variables | the level script file | |
