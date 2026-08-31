[Research](../../ResearchNotes.md) / [Move Research](../MoveResearch.md) / Move Animation Particle Sizes

# How big the game's particles are

Generated from the Platinum ROM by `ParticleEmitterReportTests`. Do not edit by hand.

A particle's drawn size does not come from its texture. The quad is sized by the emitter's
base scale, and the texture is stretched onto it, so a 32 by 32 texture and a 128 by 128 one
at the same base scale come out the same size on screen. The preview turns a base scale of 1.0
into a half-size of 23.8 pixels, which is 4096/172: the particle library's own unit
divided by the battle camera's pixels per unit.

- 485 particle archives read, 1468 emitters, 1437 textures
- 0 textures do not decode

## Texture formats

| format | decodes | does not |
|---:|---:|---:|
| 1 | 276 | 0 |
| 6 | 1161 | 0 |

## How many emitters draw at each size

Half-size in screen pixels, so a quad is twice this across. The screen is 256 by 192.

| half-size px | emitters |
|---:|---:|
| 0 | 24 |
| 8 | 292 |
| 16 | 378 |
| 24 | 347 |
| 32 | 148 |
| 40 | 91 |
| 48 | 43 |
| 56 | 31 |
| 64 | 20 |
| 72 | 2 |
| 80 | 16 |
| 88 | 27 |
| 96 | 1 |
| 112 | 8 |
| 120 | 2 |
| 144 | 5 |
| 160 | 1 |
| 168 | 2 |
| 184 | 2 |
| 264 | 2 |
| 280 | 4 |
| 320 | 2 |
| 336 | 4 |
| 368 | 1 |
| 384 | 1 |
| 512 | 6 |
| 1080 | 2 |
| 1200 | 6 |

## The 443 emitters that draw at 48 pixels or more

These are the ones worth checking against a real battle first: at this size a particle covers
a Pokemon. Some are real (a full-screen wave sheet is one enormous quad), some are not.

| archive | emitter | base scale | aspect | texture | texture size |
|---:|---:|---:|---:|---:|---|
| 449 | 2 | 11.182 | 0.56 | 1 | 32x32 |
| 449 | 3 | 11.182 | 0.56 | 1 | 32x32 |
| 449 | 1 | 11.063 | 1.40 | 0 | 16x16 |
| 483 | 0 | 11.063 | 4.10 | 0 | 128x128 |
| 483 | 1 | 11.063 | 4.10 | 0 | 128x128 |
| 106 | 0 | 9.993 | 1.61 | 1 | 16x16 |
| 274 | 0 | 8.803 | 5.71 | 0 | 128x128 |
| 274 | 1 | 8.803 | 5.71 | 0 | 128x128 |
| 335 | 1 | 8.803 | 5.71 | 1 | 128x128 |
| 373 | 2 | 8.803 | 5.71 | 6 | 128x128 |
| 383 | 2 | 8.803 | 5.71 | 1 | 128x128 |
| 383 | 3 | 8.803 | 5.71 | 1 | 128x128 |
| 16 | 2 | 6.186 | 1.00 | 1 | 32x32 |
| 29 | 1 | 5.115 | 4.22 | 2 | 128x64 |
| 29 | 2 | 5.115 | 4.22 | 2 | 128x64 |
| 29 | 3 | 5.115 | 4.22 | 2 | 128x64 |
| 140 | 0 | 5.115 | 4.22 | 0 | 128x64 |
| 140 | 1 | 5.115 | 4.22 | 0 | 128x64 |
| 219 | 1 | 5.115 | 4.22 | 2 | 128x64 |
| 3 | 0 | 4.639 | 1.55 | 0 | 128x64 |
| 5 | 0 | 4.639 | 1.55 | 0 | 128x64 |
| 247 | 2 | 4.521 | 2.97 | 2 | 128x64 |
| 247 | 3 | 4.521 | 2.97 | 2 | 128x64 |
| 313 | 1 | 4.045 | 2.88 | 3 | 128x64 |
| 313 | 2 | 4.045 | 2.88 | 4 | 128x64 |
| 313 | 3 | 4.045 | 2.88 | 3 | 128x64 |
| 313 | 4 | 4.045 | 2.88 | 4 | 128x64 |
| 3 | 3 | 3.926 | 1.55 | 0 | 128x64 |
| 5 | 3 | 3.926 | 1.55 | 0 | 128x64 |
| 3 | 1 | 3.807 | 1.55 | 0 | 128x64 |
| 5 | 1 | 3.807 | 1.55 | 0 | 128x64 |
| 241 | 4 | 3.807 | 0.65 | 1 | 64x64 |
| 244 | 2 | 3.807 | 0.62 | 2 | 8x64 |
| 117 | 2 | 3.450 | 0.24 | 0 | 16x16 |
| 417 | 2 | 3.450 | 0.21 | 2 | 16x16 |
| 417 | 3 | 3.450 | 0.21 | 2 | 16x16 |
| 3 | 2 | 3.331 | 1.55 | 0 | 128x64 |
| 5 | 2 | 3.331 | 1.55 | 0 | 128x64 |
| 106 | 10 | 3.331 | 1.00 | 2 | 32x64 |
| 106 | 12 | 3.331 | 1.00 | 2 | 32x64 |
| 106 | 14 | 3.331 | 1.00 | 2 | 32x64 |
| 106 | 15 | 3.331 | 1.00 | 2 | 32x64 |
| 354 | 1 | 3.331 | 0.03 | 1 | 32x64 |
| 106 | 11 | 3.212 | 1.00 | 2 | 32x64 |
| 106 | 13 | 3.212 | 1.00 | 2 | 32x64 |
| 106 | 16 | 3.212 | 1.00 | 2 | 32x64 |
| 106 | 17 | 3.212 | 1.00 | 2 | 32x64 |
| 304 | 0 | 3.212 | 1.00 | 0 | 64x64 |
| 116 | 1 | 3.093 | 1.00 | 0 | 64x64 |
| 7 | 2 | 2.974 | 1.55 | 3 | 128x64 |
| 7 | 3 | 2.974 | 1.55 | 4 | 128x64 |
| 7 | 4 | 2.974 | 1.55 | 3 | 128x64 |
| 7 | 5 | 2.974 | 1.55 | 4 | 128x64 |
| 373 | 1 | 2.974 | 1.00 | 0 | 64x64 |
| 8 | 0 | 2.855 | 0.83 | 1 | 128x64 |
| 285 | 2 | 2.736 | 1.00 | 0 | 16x16 |
| 451 | 3 | 2.736 | 0.03 | 1 | 16x16 |
| 482 | 1 | 2.736 | 0.18 | 2 | 64x64 |
| 4 | 6 | 2.617 | 0.09 | 1 | 32x64 |
| 6 | 6 | 2.617 | 0.09 | 1 | 32x64 |
