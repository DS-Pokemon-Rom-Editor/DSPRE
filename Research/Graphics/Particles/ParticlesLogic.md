[Research](../../ResearchNotes.md) / Particles Logic

# Particles Logic, Diamond/Pearl, Platinum and HeartGold/SoulSilver

Source: the [pokeplatinum](https://github.com/pret/pokeplatinum) decomp, whose `lib/spl` is the particle library all three games share, with [pokeheartgold](https://github.com/pret/pokeheartgold) and [pokediamond](https://github.com/pret/pokediamond) for where each game uses it; cited by file and function. This was structured into a document with AI.

Every particle effect in the three games, the sparks of a move, the burst when a ball opens, the swirl of an evolution, is one file format, SPA, read by one library. The move animation pages cover how battle scripts start them ([Move Animation Logic](../../Moves/Animation/MoveAnimationLogic.md)); this page covers the format, what the library does with it, where the files are, and how to change them.

Units: `fx32` and `fx16` are fixed point with 4096 as 1.0, angles are `u16` with `0x10000` a full turn, and colours are RGB555.

## Where the particle files are

| Archive (Diamond and Platinum path) | Diamond | Platinum | HeartGold | Used for |
|---|---|---|---|---|
| `wazaeffect/effectdata/waza_particle.narc` | 485 | 485 | `a/0/2/9`, 486 | moves and battle effects |
| `wazaeffect/effectdata/ball_particle.narc` | 117 | 117 | `a/0/9/5`, 133 | balls opening and catching, and the Ball Capsule seals |
| `particledata/particledata.narc` | 6 | 6 | `a/0/5/9`, 5 | contests, the opening, a battle overlay, HeartGold's intro |
| egg hatching | 2 | 2 | `a/1/1/6`, 2 | a normal egg and Manaphy's |
| evolution | 2 | 2 | `a/1/1/9`, 2 | the evolution scene |
| `particledata/pl_frontier/frontier_particle.narc` | | 7 | `a/1/8/8`, 7 | Battle Frontier facilities |
| `particledata/pl_pokelist/pokelist_particle.narc` | | 2 | `a/2/0/6`, 2 | Shaymin's and Giratina's form change in the party menu |
| `particledata/pl_etc/pl_etc_particle.narc` | | 2 | `a/2/1/1`, 2 | a Wi-Fi Plaza screen |
| `graphic/field_encounteffect.narc`, members 107 and 108 | | yes | `a/1/0/9`, 151 and 152 | Elite Four and Champion battle intros |
| Wi-Fi lobby minigame, member 19 | | yes | `a/1/9/8`, member 19 | a lobby minigame |

Battle loads `waza_particle` in `src/battle_anim/battle_particle_util.c`, and HeartGold loads its archive 29 the same way. Diamond also ships a two member debug particle archive that Platinum no longer has. HeartGold has two more, `a/0/9/6` and member 10 of `a/2/4/2`, whose purpose has not been established. No particle file anywhere is compressed.

`waza_particle` begins with 0 and 1 for hits, 2 for level up, 3 to 26 for the wild encounter terrain effects in pairs, 27 status, 28 item use, 29 weather and 30 a dummy, then the moves from 31, in the order of Platinum's `res/battle/particles/battle_particles.order`; the file number is not the move number. 34 of its files are an empty 32 byte header, the same in all three games, for moves drawn without particles. HeartGold inserts one extra file at 31 and every later file moves up by one, byte for byte the same as Platinum's. Platinum retuned most move effects from Diamond, keeping the pictures but changing the emitter settings.

## The file

A 32 byte header, then the emitters, then the pictures (`SPLFileHeader` in `lib/spl/include/spl_resource.h`; the loader is `SPLManager_LoadResources` in `lib/spl/src/spl_manager.c`).

| Offset | Size | Field |
|---|---|---|
| 0x00 | 4 | `20 41 50 53`, " APS" |
| 0x04 | 4 | version, "12_1" in every retail file |
| 0x08 | 2 | emitter count |
| 0x0A | 2 | picture count |
| 0x0C | 4 | 0 |
| 0x10 | 4 | emitter bytes |
| 0x14 | 4 | picture bytes |
| 0x18 | 4 | offset of the pictures, 32 plus the emitter bytes |
| 0x1C | 4 | 0 |

### An emitter

An 88 byte base (`SPLResourceHeader`), then optional blocks in a fixed order, each present only when its flag is set:

| Flag bit | Block | Bytes |
|---|---|---|
| 8 | scale over life | 12 |
| 9 | colour over life | 12 |
| 10 | alpha over life | 8 |
| 11 | picture animation | 12 |
| 16 | child particles | 20 |
| 24 | gravity | 8 |
| 25 | random push | 8 |
| 26 | magnet | 16 |
| 27 | spin | 4 |
| 28 | floor plane | 8 |
| 29 | convergence | 16 |

The flags word at the start of the base holds, besides those layout bits: the emission shape in bits 0 to 3, the draw type in 4 and 5 (billboard, billboard stretched along the velocity, flat polygon, directional polygon), the axis a circle shape lies across in 6 and 7, rotation in 12, a random starting angle in 13, "remove the emitter when it has finished" in 14, following the emitter in 15, the polygon's rotation axis and reference plane in 17 to 19, drawing children first in 21, hiding the parent in 22 and view space positions in 23.

| Offset | Type | Field |
|---|---|---|
| +0 | u32 | flags |
| +4 | fx32 x3 | emitter position |
| +16 | fx32 | particles per emission, the fraction carried over |
| +20 | fx32 | shape radius |
| +24 | fx32 | cylinder half length |
| +28 | fx16 x3 | emitter axis |
| +34 | u16 | colour; with a colour block, the peak colour |
| +36 | fx32 | speed outward from the shape |
| +40 | fx32 | speed along the axis |
| +44 | fx32 | scale |
| +48 | fx16 | aspect ratio |
| +50 | u16 | delay before starting |
| +52 | s16 x2 | per frame rotation range |
| +56 | u16 | starting angle |
| +58 | u16 | reserved, not always 0 |
| +60 | u16 | emitter life, 0 for forever |
| +62 | u16 | particle life |
| +64 | u8 x3 | randomness of scale, life and speed |
| +68 | u32 | emission interval, alpha (0 to 31), air resistance, picture |
| +72 | u32 | loop length, stretch, tiling across and down, scale direction, polygon facing |
| +76 | u32 | flip across and down, then bits the game ignores but retail files have set |
| +80 | fx16 x2 | polygon centre offset |
| +84 | u32 | user data, 0 in retail |

The optional blocks:

| Block | Layout |
|---|---|
| Scale | fx16 start, middle, end; in and out points; loop flag |
| Colour | u16 start and end; in, peak and out points; random start, loop and interpolate flags |
| Alpha | start, middle and end in five bits each; randomness and loop; in and out points |
| Picture animation | eight picture numbers; frame count, step, random start, loop |
| Child | flags, random speed, end scale, life, speed ratio, scale ratio, colour, emission count, delay, interval and picture, tiling and flips |
| Gravity | fx16 x3 acceleration |
| Random push | fx16 x3 strength and an interval |
| Magnet | fx32 x3 target and a strength |
| Spin | angle per frame and axis |
| Floor plane | fx32 height, fx16 bounce, and whether particles die or bounce |
| Convergence | fx32 x3 target and a strength |

An in, peak or out point is a byte, 0 to 255 of the particle's life.

### A picture

Each picture is a 32 byte header, the texels and the palette (`SPLTextureResource` in `lib/spl/include/spl_texture.h`). The header starts `20 54 50 53`, " TPS", then one word holding the format, width and height (8 shifted by the size code), repeat and flip across and down, whether colour 0 is transparent, and whether the picture borrows another's; then the texel size, palette offset and size, and the step to the next picture. Retail uses format 6 (A5I3) for most, format 1 (A3I5) for about 350, and a few 16 and 4 colour pictures.

## What the library does

All of this is in `lib/spl/src` of the Platinum decomp.

**Emitting.** After its delay, an emitter emits every interval frames while its life lasts, 0 meaning forever (`spl_emitter.c`). One with the "finished" flag removes itself once its life is over and its particles are gone.

**Shapes** (`spl_emit.c`): 0 a point, 1 a sphere's surface, 2 a circle's edge, 3 a circle's edge evenly spaced rather than at random, 4 a sphere's volume, 5 a disc, 6 and 7 a cylinder's surface and volume, 8 and 9 a hemisphere's surface and volume. Circles, cylinders and hemispheres lie across the axis chosen by the flags, 3 meaning the emitter's own.

**Starting a particle.** Its speed is outward from the shape centre times one speed, plus the axis times the other, plus the emitter's own velocity; scale and life are randomised by the randomness bytes. The random numbers come from the library's own generator, `x * 0x5eedf715 + 0x1b0cb173`.

**Each frame** (`spl_emitter.c`): the curves run, the behaviours add acceleration, velocity is multiplied by `(air resistance + 384) / 512`, so 128 is no drag, less slows and more speeds up, then position moves by velocity. A particle dies when its age passes its life.

**Curves** (`spl_anim.c`). Scale goes from start to middle until the in point, holds, then goes to end after the out point. Colour starts at the start colour, moves to the emitter's colour by the peak point and to the end colour by the out point, so with a colour block the base colour is the middle one. Alpha works like scale. A picture animation steps every `step` frames. Because every point is a fraction of life, a longer life stretches every curve.

**Behaviours** (`spl_behavior.c`). Gravity is a constant acceleration, random push kicks the velocity every interval, magnet pulls towards a point like a spring, spin turns positions about an axis each frame, the floor plane kills or bounces particles at a height, and convergence moves the position itself towards a point.

**Children.** An emitter with a child block starts emitting children part way through each particle's life, and every interval after. A child takes the parent's speed times the speed ratio over 256, plus a random push, and the parent's scale times the scale ratio plus one over 64, so 63 is the same size and 255 four times it. Children fade out and scale to their end scale in a straight line.

**Drawing** (`spl_draw.c`). A particle's alpha is its emitter's alpha times its curve; at 0 it is skipped. Its colour is multiplied with the picture. The polygon spans one unit either side of its centre offset, and a picture only tiles or mirrors if the picture's own repeat and flip bits allow it.

**The camera.** A particle system has 16 slots, with 20 emitters and 200 particles each (`include/particle_system.h`). Its default camera is a perspective one; battle animations switch every slot to an orthographic camera (`src/battle_anim/battle_anim_system.c`), as do the egg hatch, the encounter effects, the opening and evolution.

**What battle scripts change.** A move script attaches an emitter to the attacker, the defender, either side or the screen, and can override its position, its axis, its gravity, its magnet, its spin and its convergence at runtime (the emitter callbacks in `src/battle_anim/generic_emitter_callback*.c`, and the setters in `src/particle_system.c`). The magnet and convergence targets written in a move's SPA file are therefore often replaced while the move plays.

## Changing one by hand

The file never has to change size for a value edit, so the same bytes can be changed inside the packed archive.

Platinum's Pound is `waza_particle` 31 (HeartGold's 32, the same bytes), 884 bytes, two emitters and two pictures:

```
00: 20 41 50 53 31 32 5F 31 02 00 02 00 00 00 00 00
10: 0C 01 00 00 48 02 00 00 2C 01 00 00 00 00 00 00
```

Its first emitter starts at `0x20`, with flags `0x01014701`: sphere surface, scale, colour and alpha curves, removes itself when done, children and gravity. It emits four particles for two frames, each living 19 frames.

- **A longer life.** The particle life is at `0x5E`, `13 00`. Writing `26 00` makes every particle live 38 frames, and every curve stretches with it.
- **A different colour.** Because the emitter has a colour curve, three colours make up the effect: the peak at `0x42` (`9F 01`, orange), and the curve's start and end at `0x84` and `0x86` (`DF 03` and `DF 00`). For blue, write `80 7D`, `10 7F` and `00 7C`. The children keep their own colour at `0xA2` (`FF 7F`, white) unless it is changed too.
- **Gravity** is at `0xAC`, `00 00 C3 FF 00 00`, a pull of -61 on Y.

The second emitter, the flash, starts at `0xB4`, and the first picture at `0x12C`.

## What DSPRE does

Particles are edited in the Particle Editor (`ParticleEditorViewModel`, through `SpaDocument` and `SpaFields`), found through the Particle Library, or opened from the Battle Script editor and the Ball Capsule editor; both windows are behind the beta gate. The Particle Library names every particle file after what loads it and groups the files into ten categories: moves, battle effects (the leading files of the move archive: hit spark, level up, the battle start effects per terrain, status and healing, item use, fog and HeartGold's shiny sparkles), Poké Balls (openings, caught stars and the return), Ball Capsule seals, battle intros (the link battle VS sparks and the Elite Four and Champion bursts), story scenes (evolution, egg hatching, the opening), menus and minigames, the Battle Frontier, unused files the game never loads, and other, for any file not otherwise named (`ParticleFileNames`). `SpaArchive.Parse` reads a file the way the library does, and `SpaSimulator` with `SpaParticlePreview` plays it with the battle camera. The preview draws its random numbers from the library's generator (`SplRandom`), in the library's order, from one generator shared by every emitter of a playback, so a replay repeats exactly; children start at the delay fraction of each particle's own randomised life.

| What | Edited in DSPRE | Written | Left untouched |
|---|---|---|---|
| Emitter fields | 141 fields, each masked into its own bits | only the bytes of the field edited | reserved words and padding |
| Layout flags, the blocks an emitter has | shown | | the flags, so no block is added or removed |
| Emitter and picture counts | | | both; nothing is added or removed |
| Pictures | replaced by a PNG of the same size, format and palette length (`ReplaceTexture`) | the picture's texels and palette | borrowed pictures |

A file therefore never changes size. Files in mapped archives such as `waza_particle` and `ball_particle` are saved to the unpacked archive and packed on Save ROM; files from other archives are written back at once.
