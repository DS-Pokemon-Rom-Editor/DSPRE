Notes for [MoveEffectsLogic.md](MoveEffectsLogic.md).

== move data ==
move.h: struct MoveTbl { u16 effect; u8 category; u8 power; u8 type; u8 accuracy; u8 pp; u8 effectChance; u16 range; s8 priority; u8 unkB; struct { u8 unkC; u8 contestType; u16 unk_E; }; }
effect field is separate from move ID, many moves share one effect value

NUM_MOVES = MOVE_SHADOW_FORCE (last real move id)
MOVE_NONE = 0, MOVE_POUND = 1
TrainerAIData.moveData[NUM_MOVES + 1] (battle.h)

== effect logic ==
btlcmd.inc = 225 macros, shared bytecode format for move_script/effect_script/subscript
each macro = 4-byte opcode word + one 4-byte word per param
opcode numbers sequential from 0 in source order:
  0 PlayEncounterAnimation
  1 SetPokemonEncounter
  35 GoToSubscript
  36 GoToEffectScript
  37 GoToMoveScript

move_script/ = 501 files, one per move id, named (move_script_0000_None.s ...)
  -> NARC_a_0_0_0 = files/battledata/script/move_script.narc

effect_script/ = 277 files, numbered only (effect_script_0000.s ...)
  -> NARC_a_0_3_0 = files/battledata/script/effect_script.narc

subscript/ = 297 files, named (subscript_0000_StartEncounter.s ...)
  -> NARC_a_0_0_1 = files/battledata/script/subscript.narc

move_script_0001_Pound.s, full file:
  GoToEffectScript

BtlCmd_GoToEffectScript (battle_command.c:1176):
  BattleScriptJump(ctx, NARC_a_0_3_0, ctx->trainerAIData.moveData[ctx->moveNoCur].effect)
  -> jumps by move's effect id, not move id

effect_script_0000.s, full file:
  CalcCrit
  CalcDamage
  End

GoToSubscript (opcode 35) reaches subscript.narc the same way, also called directly from C
  e.g. BattleScriptGotoSubscript(ctx, NARC_a_0_0_1, BATTLE_SUBSCRIPT_WAIT_MOVE_ANIMATION)

subscript_0000_StartEncounter.s = long real example: encounter setup, PrintGlobalMessage/PrintMessage, party gauge, Poke Ball throw

not decompiled: no EFFECT_* name enum for the effect field, effect_script files numbered only
