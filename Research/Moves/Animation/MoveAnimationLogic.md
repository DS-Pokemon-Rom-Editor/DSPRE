[Research](../../ResearchNotes.md) / [Move Research](../MoveResearch.md) / Move Animation…

# Move Animation Logic, HeartGold/SoulSilver

Source: [pokeheartgold decomp](https://github.com/pret/pokeheartgold). This was structured into a document with AI.

This covers the trigger into a move's visual animation, not the effect/damage logic that runs alongside it. For the script bytecode that calls into this, see `Effects/MoveEffectsLogic.md`.

## Triggering the visual move animation

`PlayMoveAnimation` (opcode 23 in `asm/macros/btlcmd.inc`) is implemented by `BtlCmd_PlayMoveAnimation`, `src/battle/battle_command.c`:

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

`BattleSystem_AreBattleAnimationsOn` (`src/battle/battle_system.c`) reads the player's "Battle effects" setting.

`BattleController_SetMoveAnimation` is declared in `include/battle/battle_controller.h` as `void BattleController_SetMoveAnimation(BattleSystem *battleSystem, BattleContext *ctx, u16 move);`. It takes the move ID and hands off to whatever actually loads and plays that move's visual animation.

`PlayBattleAnimation`, `PlayBattleAnimationOnMons`, and `PlayBattleAnimationFromVar` (opcodes 69, 70, 71) are a separate, simpler trigger used for non-move battle animations (status effects, fainting, encounter effects). Their handlers, `src/battle/battle_command.c` onward, take an explicit animation ID argument rather than looking one up from move data, and are gated only on `BattleSystem_AreBattleAnimationsOn` plus a few specific `ctx` status values. `PlayFaintAnimation` (opcode 29) is its own dedicated opcode with no animation ID argument.

## The generic particle library

`include/library/spl.h`, `spl_resource.h`, `spl_emitter.h`, `spl_particle.h`, `spl_field.h`, and `spl_manager.h` are fully decompiled. `spl_resource.h` defines the particle emitter resource layout, `struct SPLResBase`, with real field names (`pos`, `gen_num`, `radius`, `length`, `axis`, `clr_n`, `init_vel_mag_pos`, `init_vel_mag_axis`, `base_scl`, `emtr_life`, `ptcl_life`, and a packed `SPLResBaseFlag` bitfield covering `init_pos_type`, `draw_type`, `circle_axis`, `use_scl_anm`, `use_clr_anm`, `use_alp_anm`, `use_tex_anm`, `use_fld_grvt`, `use_fld_rndm`, `use_fld_mgnt`, `use_fld_spin`, and more).

The same library draws move particles. HeartGold's battle animation code loads its particle archive, `a/0/2/9`, through the particle loader in overlay 7 (`asm/overlay_07.s`, still assembly), which is why no named battle function in `src/battle/` calls it; Platinum's decomp shows the same path by name, loading `waza_particle` in `src/battle_anim/battle_particle_util.c`. The library is also used outside battle (`src/overlay_06.c`, `src/overlay_94.c`, `src/intro_movie_scene_4.c`, `src/register_hall_of_fame.c`). The format, the archives and what the library does are in [Particles Logic](../../Graphics/Particles/ParticlesLogic.md).

## Not decompiled yet

`BattleController_SetMoveAnimation` has no matching definition anywhere in `src/`, only the declaration in `include/battle/battle_controller.h`. This is the real entry point into the move's visual animation and it is not decompiled.

`ov12_0226343C`, the two-target equivalent called from `BtlCmd_PlayMoveAnimationOnMons`, is an address-named stub with no assigned name.

`move_script.narc`, `effect_script.narc`, and `subscript.narc` hold the logic, message and damage scripts described in `Effects/EffectsLogic.md`, not visual animation data. The particle data a move draws is `a/0/2/9`, the same files as Platinum's `waza_particle` shifted up by one (see [Particles Logic](../../Graphics/Particles/ParticlesLogic.md)).

Address-named or partially matched battle files that could hold the missing move-animation loader:

- `src/battle/overlay_12_0224E4FC.c` (6719 lines, mix of named functions and `ov12_` address stubs)
- `include/battle/battle_controller.h` (declares `BattleController_SetMoveAnimation` alongside other controller functions, several of which are also unimplemented in `src/`)
