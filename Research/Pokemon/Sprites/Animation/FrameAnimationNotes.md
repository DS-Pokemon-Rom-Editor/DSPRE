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

Sprite_SetAnimCtrlSeq (sprite.c:295): NNS_G2dGetAnimSequenceByIdx -> NNS_G2dSetCellAnimationSequence -> NNS_G2dStartAnimCtrl
Sprite_TryChangeAnimSeq (:311): only calls SetAnimCtrlSeq if animationNo actually changed
Sprite_ResetAnimCtrlState (:317): resets control state, forces frame to 0

DSPRE side: DS_Map/Avalonia/Data/CellAnim.cs
  CFrame (cell, dur, pos, rot, scale per frame), CellSequence (CFrame[]), CellActor (timeline/playback state)
  same shape as SpriteAnimationData/NNSG2dCellAnimation, parsed straight from ROM NANR/NCER instead of loaded via g2d lib

not decompiled:
- NNS_G2dTickCellAnimation/NNS_G2dTickMCAnimation bodies (actual per-frame delay/loop math) - g2d lib source not in this project
- NNSG2dCellAnimation/NNSG2dAnimSequenceData/NNSG2dCellDataBank struct layouts - no g2d header in include/
