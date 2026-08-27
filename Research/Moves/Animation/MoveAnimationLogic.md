[Research](../../ResearchNotes.md) / [Move Research](../MoveResearch.md) / Move Animation…

# Move Animation Logic, HeartGold/SoulSilver

Source: [pokeheartgold decomp](https://github.com/pret/pokeheartgold). This was structured into a document with AI.

This covers the trigger into a move's visual animation, not the effect/damage logic that runs alongside it. For the script bytecode that calls into this, see `Effects/MoveEffectsLogic.md`.

## Triggering the visual move animation

`PlayMoveAnimation` (opcode 23 in `asm/macros/btlcmd.inc`) is implemented by `BtlCmd_PlayMoveAnimation`, `src/battle/battle_command.c:879`:

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

No archive in `filesystem.mk` is named for per-move visual animation bytecode or particle resource data. `move_script.narc`, `effect_script.narc`, and `subscript.narc` all hold the logic/message/damage-calc scripts described in `Effects/EffectsLogic.md`, not visual animation data.

No call site anywhere in `src/battle/` uses the `spl_*` particle API, so its relationship (if any) to move animations is unconfirmed.

Address-named or partially matched battle files that could hold the missing move-animation loader:

- `src/battle/overlay_12_0224E4FC.c` (6719 lines, mix of named functions and `ov12_` address stubs)
- `include/battle/battle_controller.h` (declares `BattleController_SetMoveAnimation` alongside other controller functions, several of which are also unimplemented in `src/`)
