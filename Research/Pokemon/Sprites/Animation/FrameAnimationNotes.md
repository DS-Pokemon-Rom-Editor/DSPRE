[Research](../../../ResearchNotes.md) / [Pokemon Resear…](../../PokemonResearch.md) / [Sprites Resear…](../SpritesResearch.md) / [Sprite Animati…](SpriteAnimationResearch.md) / Frame Animatio…

Notes for [FrameAnimationLogic.md](FrameAnimationLogic.md).

generic engine, applies to every sprite in the game, not pokemon-specific
separate from the idle animation data table (IdleAnimationLogic.md)

sprite.h: struct Sprite { ... u32 animationData[SPRITE_ANIMATION_DATA_WORD_COUNT]; ... SpriteAnimType flag; u16 animationNo; ... }

animationData layout depends on flag (SpriteAnimType):
  SPRITE_ANIM_TYPE_CELL / SPRITE_ANIM_TYPE_CELL_TRANSFER -> struct SpriteAnimationData { cellBank; animBankData; NNSG2dCellAnimation animation; }
  SPRITE_ANIM_TYPE_MULTICELL -> struct SpriteMultiAnimationData { cellBank; animBankData; NNSG2dMultiCellAnimation animation; multiCellBank; multiAnimBankData; node; cellAnim; }

frame stepping (src/sprite.c), all branch on sprite->flag then call into Nitro SDK g2d lib:
  Sprite_UpdateAnim(sprite, frames) -> NNS_G2dTickCellAnimation / NNS_G2dTickMCAnimation
  Sprite_SetAnimationFrame(sprite, frameIndex) -> NNS_G2dSetCellAnimationCurrentFrame / NNS_G2dSetMCAnimationCurrentFrame
  Sprite_GetAnimationFrame(sprite) -> NNS_G2dGetAnimCtrlCurrentFrame

Sprite_SetAnimCtrlSeq (sprite.c): NNS_G2dGetAnimSequenceByIdx -> NNS_G2dSetCellAnimationSequence -> NNS_G2dStartAnimCtrl
Sprite_TryChangeAnimSeq : only calls SetAnimCtrlSeq if animationNo actually changed
Sprite_ResetAnimCtrlState : resets control state, forces frame to 0

DSPRE side: DSPRE.Avalonia/Avalonia/Data/CellAnim.cs (CFrame, CellSequence, CellActor)
  CFrame (cell, dur, pos, rot, scale per frame), CellSequence (CFrame[]), CellActor (timeline/playback state)
  same shape as SpriteAnimationData/NNSG2dCellAnimation, parsed straight from ROM NANR/NCER instead of loaded via g2d lib

not decompiled:
(struct layouts: lib/include/nnsys/g2d/g2d_CellAnimation.h)
- NNS_G2dTickCellAnimation/NNS_G2dTickMCAnimation bodies (actual per-frame delay/loop math) - g2d lib source not in this project
