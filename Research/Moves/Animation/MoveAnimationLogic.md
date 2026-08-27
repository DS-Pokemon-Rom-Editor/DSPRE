# Move Effect and Animation System, HeartGold/SoulSilver

Source: `pokeheartgold` decomp only (`C:\Romhacking\ROMs\NDS\pokeheartgold`). This was structured into a document with AI.

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

`asm/macros/btlcmd.inc` defines 225 macros shared by all three script domains below. Each macro assembles to a 4-byte opcode word followed by a fixed number of 4-byte argument words, one word per macro parameter. Opcode numbers are assigned in source order starting at 0: `PlayEncounterAnimation` is 0, `SetPokemonEncounter` is 1, `PlayMoveAnimation` is 23, `PlayMoveAnimationOnMons` is 24, `GoToSubscript` is 35, `GoToEffectScript` is 36, `GoToMoveScript` is 37, `PlayBattleAnimation` is 69, `PlayBattleAnimationOnMons` is 70, `PlayBattleAnimationFromVar` is 71.

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

`subscript.narc` (`NARC_a_0_0_1`) is reached with the separate `GoToSubscript` opcode (35), implemented as `BtlCmd_GoToSubscript` the same way, and also referenced directly by C code, e.g. `BattleScriptGotoSubscript(ctx, NARC_a_0_0_1, BATTLE_SUBSCRIPT_WAIT_MOVE_ANIMATION)` in `BtlCmd_PlayMoveAnimation`.

## Triggering the visual move animation

`PlayMoveAnimation` (opcode 23) is implemented by `BtlCmd_PlayMoveAnimation`, `src/battle/battle_command.c:879`:

```c
BOOL BtlCmd_PlayMoveAnimation(BattleSystem *battleSystem, BattleContext *ctx) {
    u16 move;

    BattleScriptIncrementPointer(ctx, 1);
    u32 battler = BattleScriptReadWord(ctx);

    if (battler == BATTLER_NONE) {
        move = ctx->moveTemp;
    } else {
        move = ctx->moveNoCur;
    }

    if ((!(ctx->battleStatus & BATTLE_STATUS_MOVE_ANIMATIONS_OFF) && BattleSystem_AreBattleAnimationsOn(battleSystem) == TRUE) || move == MOVE_TRANSFORM) {
        ctx->battleStatus |= BATTLE_STATUS_MOVE_ANIMATIONS_OFF;
        BattleController_SetMoveAnimation(battleSystem, ctx, move);
    }

    if (!BattleSystem_AreBattleAnimationsOn(battleSystem)) {
        BattleScriptGotoSubscript(ctx, NARC_a_0_0_1, BATTLE_SUBSCRIPT_WAIT_MOVE_ANIMATION);
    }

    return FALSE;
}
```

`PlayMoveAnimationOnMons` (opcode 24) is the same pattern for a two-target move, calling `ov12_0226343C(battleSystem, ctx, move, attacker, defender)` instead.

`BattleSystem_AreBattleAnimationsOn` (`src/battle/battle_system.c:748`) reads the player's "Battle effects" setting.

`BattleController_SetMoveAnimation` is declared in `include/battle/battle_controller.h:26` as `void BattleController_SetMoveAnimation(BattleSystem *battleSystem, BattleContext *ctx, u16 move);`. It takes the move ID and hands off to whatever actually loads and plays that move's visual animation.

`PlayBattleAnimation`, `PlayBattleAnimationOnMons`, and `PlayBattleAnimationFromVar` (opcodes 69, 70, 71) are a separate, simpler trigger used for non-move battle animations (status effects, fainting, encounter effects). Their handlers, `src/battle/battle_command.c:2258` onward, take an explicit animation ID argument rather than looking one up from move data, and are gated only on `BattleSystem_AreBattleAnimationsOn` plus a few specific `ctx` status values. `PlayFaintAnimation` (opcode 29) is its own dedicated opcode with no animation ID argument.

## The generic particle library

`include/library/spl.h`, `spl_resource.h`, `spl_emitter.h`, `spl_particle.h`, `spl_field.h`, and `spl_manager.h` are fully decompiled. `spl_resource.h` defines the particle emitter resource layout, `struct SPLResBase`, with real field names (`pos`, `gen_num`, `radius`, `length`, `axis`, `clr_n`, `init_vel_mag_pos`, `init_vel_mag_axis`, `base_scl`, `emtr_life`, `ptcl_life`, and a packed `SPLResBaseFlag` bitfield covering `init_pos_type`, `draw_type`, `circle_axis`, `use_scl_anm`, `use_clr_anm`, `use_alp_anm`, `use_tex_anm`, `use_fld_grvt`, `use_fld_rndm`, `use_fld_mgnt`, `use_fld_spin`, and more).

This library is used elsewhere in the game (`src/overlay_06.c`, `src/overlay_94.c`, `src/intro_movie_scene_4.c`, `src/register_hall_of_fame.c`), but nothing in `src/battle/` calls any `Spl*` function. Its use for move-effect particles, if any, is not wired up in the decompiled source.

## Not decompiled yet

`BattleController_SetMoveAnimation` has no matching definition anywhere in `src/`, only the declaration in `include/battle/battle_controller.h`. This is the real entry point into the move's visual animation and it is not decompiled.

`ov12_0226343C`, the two-target equivalent called from `BtlCmd_PlayMoveAnimationOnMons`, is an address-named stub with no assigned name.

No archive in `filesystem.mk` is named for per-move visual animation bytecode or particle resource data. `move_script.narc`, `effect_script.narc`, and `subscript.narc` all hold the logic/message/damage-calc scripts shown above, not visual animation data.

No call site anywhere in `src/battle/` uses the `spl_*` particle API, so its relationship (if any) to move animations is unconfirmed.

Address-named or partially matched battle files that could hold the missing move-animation loader:

- `src/battle/overlay_12_0224E4FC.c` (6719 lines, mix of named functions and `ov12_` address stubs)
- `include/battle/battle_controller.h` (declares `BattleController_SetMoveAnimation` alongside other controller functions, several of which are also unimplemented in `src/`)
