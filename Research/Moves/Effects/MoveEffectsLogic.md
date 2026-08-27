# Move Effects Logic, HeartGold/SoulSilver

Source: [pokeheartgold decomp](https://github.com/pret/pokeheartgold). This was structured into a document with AI.

## Move data

`include/move.h`, `struct MoveTbl`:

```c
typedef struct MoveTbl {
    u16 effect;
    u8 category;
    u8 power;
    u8 type;
    u8 accuracy;
    u8 pp;
    u8 effectChance;
    u16 range;
    s8 priority;
    u8 unkB;
    struct {
        u8 unkC;
        u8 contestType;
        u16 unk_E;
    };
} MoveTbl;
```

`effect` is a separate ID from the move's own ID. Many moves share the same `effect` value.

`NUM_MOVES` is defined as `MOVE_SHADOW_FORCE`, the last real move ID (`include/constants/moves.h`). `MOVE_NONE` is 0, `MOVE_POUND` is 1.

`TrainerAIData.moveData` is declared as `MoveTbl moveData[NUM_MOVES + 1]` (`include/battle/battle.h`), one entry per move ID including the `MOVE_NONE` slot.

## Three script domains, one bytecode format

`asm/macros/btlcmd.inc` defines 225 macros shared by all three script domains below. Each macro assembles to a 4-byte opcode word followed by a fixed number of 4-byte argument words, one word per macro parameter. Opcode numbers are assigned in source order starting at 0: `PlayEncounterAnimation` is 0, `SetPokemonEncounter` is 1, `GoToSubscript` is 35, `GoToEffectScript` is 36, `GoToMoveScript` is 37.

`files/battledata/script/move_script/` holds 501 files, one per move ID, human named (`move_script_0000_None.s`, `move_script_0001_Pound.s`, ...). Built into `NARC_a_0_0_0` (`files/battledata/script/move_script.narc`, mapped in `filesystem.mk`).

`files/battledata/script/effect_script/` holds 277 files, numbered only (`effect_script_0000.s` through `effect_script_0276.s`). Built into `NARC_a_0_3_0` (`files/battledata/script/effect_script.narc`).

`files/battledata/script/subscript/` holds 297 files, human named (`subscript_0000_StartEncounter.s`, `subscript_0001_UseMove.s`, ...). Built into `NARC_a_0_0_1` (`files/battledata/script/subscript.narc`).

## How a move script reaches its effect script

A typical move script is nearly empty. `move_script_0001_Pound.s` in full:

```
    .include "macros/btlcmd.inc"

    .data

_000:
    GoToEffectScript
```

`GoToEffectScript` (opcode 36) is implemented by `BtlCmd_GoToEffectScript`, `src/battle/battle_command.c:1176`:

```c
BOOL BtlCmd_GoToEffectScript(BattleSystem *battleSystem, BattleContext *ctx) {
    BattleScriptIncrementPointer(ctx, 1);

    BattleScriptJump(ctx, NARC_a_0_3_0, ctx->trainerAIData.moveData[ctx->moveNoCur].effect);

    return FALSE;
}
```

It jumps into `NARC_a_0_3_0` (`effect_script.narc`) at the index given by the current move's `effect` field, not by the move's own ID. This is why one effect script file can serve many moves. `effect_script_0000.s` in full:

```
    .include "macros/btlcmd.inc"

    .data

_000:
    CalcCrit
    CalcDamage
    End
```

`subscript.narc` (`NARC_a_0_0_1`) is reached with the separate `GoToSubscript` opcode (35), implemented as `BtlCmd_GoToSubscript` the same way, and also referenced directly by C code, e.g. `BattleScriptGotoSubscript(ctx, NARC_a_0_0_1, BATTLE_SUBSCRIPT_WAIT_MOVE_ANIMATION)` inside `BtlCmd_PlayMoveAnimation` (see `Animation/MoveAnimationLogic.md`).

`subscript_0000_StartEncounter.s` is a longer, real example of this bytecode in use, covering wild vs trainer vs Safari Zone encounter setup, message printing (`PrintGlobalMessage`, `PrintMessage`), party gauge display, and Poke Ball throw sequencing, all through the same macro set.

## Not decompiled yet

No table linking a move's `effect` value to a human readable name (an `EFFECT_*` style enum) exists in `include/`. The 277 `effect_script_*.s` files are numbered only, with no descriptive suffix.
