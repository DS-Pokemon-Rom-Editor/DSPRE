[Research](../../ResearchNotes.md) / [Move Research](../MoveResearch.md) / Move Animation…

Notes for [MoveAnimationLogic.md](MoveAnimationLogic.md).

PlayMoveAnimation (opcode 23) -> BtlCmd_PlayMoveAnimation (battle_command.c:879)
  picks move = ctx->moveTemp or ctx->moveNoCur
  gated on BATTLE_STATUS_MOVE_ANIMATIONS_OFF + BattleSystem_AreBattleAnimationsOn (battle_system.c:748, "Battle effects" setting), or move == MOVE_TRANSFORM
  calls BattleController_SetMoveAnimation(battleSystem, ctx, move)

PlayMoveAnimationOnMons (opcode 24) -> BtlCmd_PlayMoveAnimationOnMons (battle_command.c:903)
  same gate, calls ov12_0226343C(battleSystem, ctx, move, attacker, defender) instead

BattleController_SetMoveAnimation: declared battle_controller.h:26, void, args (BattleSystem*, BattleContext*, u16 move)
  NOT DEFINED anywhere in src/ - declaration only

ov12_0226343C: address-named stub, unmatched, not decompiled

PlayBattleAnimation/OnMons/FromVar (opcodes 69/70/71) -> battle_command.c:2258 onward
  separate simpler trigger, explicit animation id arg, used for non-move animations (status/faint/encounter)
  gated on BattleSystem_AreBattleAnimationsOn + specific ctx status values (15/16/25/26)

PlayFaintAnimation = opcode 29, own dedicated opcode, no animation id arg

particle library, fully decompiled, generic (not battle-specific):
  include/library/spl.h, spl_resource.h, spl_emitter.h, spl_particle.h, spl_field.h, spl_manager.h
  struct SPLResBase (spl_resource.h): pos, gen_num, radius, length, axis, clr_n, init_vel_mag_pos, init_vel_mag_axis, base_scl, emtr_life, ptcl_life
  SPLResBaseFlag bitfield: init_pos_type, draw_type, circle_axis, use_scl_anm, use_clr_anm, use_alp_anm, use_tex_anm, use_fld_grvt, use_fld_rndm, use_fld_mgnt, use_fld_spin, ...
  used in: src/overlay_06.c, src/overlay_94.c, src/intro_movie_scene_4.c, src/register_hall_of_fame.c
  zero call sites in src/battle/ - no confirmed link to move effects

not decompiled:
- BattleController_SetMoveAnimation body (the real move-animation loader/interpreter)
- ov12_0226343C (two-mon variant)
- archive name/constant for per-move visual animation bytecode or particle resource data (not in move_script/effect_script/subscript, none of those hold visual data)
- any link between spl_* particle API and move effects

possible loader locations (address-named / partial, battle overlay):
- src/battle/overlay_12_0224E4FC.c (6719 lines, mixed named + ov12_ stubs)
- include/battle/battle_controller.h (other controller functions also unimplemented in src/)
