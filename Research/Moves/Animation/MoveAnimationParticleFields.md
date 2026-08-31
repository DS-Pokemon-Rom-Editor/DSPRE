[Research](../../ResearchNotes.md) / [Move Research](../MoveResearch.md) / Move Animation Particle Fields

# Particle emitter fields

Generated from `SpaFieldNotes.cs`. Do not edit by hand; `SpaFieldDocTests` rewrites it.

An emitter record in a `.spa` archive holds 130 fields. Every one of them is read. This says which ones the preview acts on, which ones only the drawing code looks at, and which ones it deliberately ignores.

## Read but not acted on

| field | what acting on it would change | read from |
|---|---|---|
| `SelfDestruct` | When a move is judged to have finished, by at most a frame or two. The emitter throws itself away once it has stopped emitting instead of sitting there empty, and nothing is drawn after its particles are gone either way. | battle_particle.c |

## Used when drawing, not when moving

Where a particle goes and how it is drawn are separate jobs. These decide how it looks on screen and nothing about its path, so the movement code never reads them.

- `DrawType`
- `PosX`
- `PosY`
- `PosZ`
- `RepeatS`
- `RepeatT`
- `Aspect`
- `FlipS`
- `FlipT`
- `PolyRotAxis`
- `PolyRefPlane`
- `DpolFaceEmitter`
- `ChildDrawType`
- `ChildPolyRotAxis`
- `ChildPolyRefPlane`
- `DbbScale`
- `OffsetX`
- `OffsetY`

## Acted on

The remaining fields drive the preview: how many particles there are, where they start, how fast and in what direction they leave, how long they and the emitter live, and how their size, colour, transparency, texture and spin change over that life.

- `AirResist`
- `AlpE`
- `AlpFlick`
- `AlpIn`
- `AlpLoop`
- `AlpN`
- `AlpOut`
- `AlpS`
- `AxisX`
- `AxisY`
- `AxisZ`
- `BaseAlpha`
- `BaseScale`
- `ChildB`
- `ChildG`
- `ChildGenIntvl`
- `ChildGenNum`
- `ChildGenStart`
- `ChildHasAlpAnm`
- `ChildHasSclAnm`
- `ChildLife`
- `ChildR`
- `ChildRandVel`
- `ChildRotType`
- `ChildSclEnd`
- `ChildSclRatioRaw`
- `ChildTexNo`
- `ChildUseClr`
- `ChildUsesBehaviors`
- `ChildVelRatio`
- `CircleAxis`
- `ClrEB`
- `ClrEG`
- `ClrER`
- `ClrIn`
- `ClrInterp`
- `ClrLoop`
- `ClrOut`
- `ClrPeak`
- `ClrRndm`
- `ClrSB`
- `ClrSG`
- `ClrSR`
- `CollBounce`
- `CollEvent`
- `CollY`
- `ColorB`
- `ColorG`
- `ColorR`
- `ConvRatio`
- `ConvX`
- `ConvY`
- `ConvZ`
- `DrawChildrenFirst`
- `EmitterLife`
- `FollowEmtr`
- `GenInterval`
- `GenNum`
- `GravityX`
- `GravityY`
- `GravityZ`
- `HideParent`
- `InitPosType`
- `InitRot`
- `InitVelAxis`
- `InitVelPos`
- `Length`
- `LoopFrames`
- `MagnetMag`
- `MagnetX`
- `MagnetY`
- `MagnetZ`
- `ParticleLife`
- `Radius`
- `RandIntvl`
- `RandMagX`
- `RandMagY`
- `RandMagZ`
- `RandomLoopAnm`
- `RndLife`
- `RndScale`
- `RndVel`
- `RotRate`
- `RttMaxRot`
- `RttMinRot`
- `ScaleAnimDir`
- `SclE`
- `SclIn`
- `SclLoop`
- `SclN`
- `SclOut`
- `SclS`
- `SpinAxis`
- `SpinRadian`
- `StartOffset`
- `TexDiff`
- `TexLoop`
- `TexNo`
- `TexSeq`
- `TexUseNum`
- `TexUseRndm`
- `UseAlphaAnm`
- `UseChild`
- `UseColl`
- `UseColorAnm`
- `UseConv`
- `UseInitRttRndm`
- `UseMagnet`
- `UseRttAnm`
- `UseScaleAnm`
- `UseTexAnm`
