[Research](../../ResearchNotes.md) / [Move Research](../MoveResearch.md) / Move Animation Routines

# The move-effect support routines

What each routine a move-effect script can call reads out of the words handed to it, taken from its own
C body in the HeartGold leak, not from inference. This file is written from `WestRoutines.cs`, which is
what the editor itself reads, so the two cannot drift apart.

A script calls one with `FUNC_CALL id, count, words`. The id is the routine's index in
`WeSysSP_FuncTable` (`west_sp.c:218` indexes it directly, no offset) and the words land in
`waza_eff_gp_wk`. `WEST_FUNC_CALL` copies `count` words in and then **zeros the rest** of the ten
(`we_sys.h:92`), so a routine handed fewer words than it reads still runs and sees zeros; it is never
skipped. The routine ids are identical in Platinum and HeartGold, checked by comparing every
`WEST_SP_DEF_CMD` line in both `west_sp_def.h` files.

A word shown as never read is one the scripts hand over that the routine never looks at. Those are left
blank on purpose rather than invented.

Where a word picks out Pokemon it is a target flag. Those names are relative to the move, not to the
sides of the field: M1 is the attacker and E1 the defender, M2 and E2 are their allies and only exist in
a double battle, STAGE is everybody and OTHER is everybody but the attacker (`we_tool.c:1431`).

### 0. `TEST_1`

A sample routine the games left in. Does nothing.  
_WestSp_Sample, wsp_sample.c:64_

### 1. `TEST_2`

A sample routine the games left in. Does nothing.  
_WestSp_SampleEffectTCB, wsp_sample.c:126_

### 2. `TEST_3`

A sample routine the games left in. Does nothing.  
_WestSp_SampleSoundTCB, wsp_sample.c:193_

### 3. `TEST_4`

A sample routine the games left in. Does nothing.  
_WestSp_SampleTCB, wsp_sample.c:258_

### 4. `POKEROTA_00`

Turns the attacker on the spot.  
_WestSp_EffectTCBPokeRota00, wsp_tool.c:697_

| word | meaning |
|---:|---|
| 0 | angle to start at |
| 1 | angle to end at |
| 2 | how many frames the turn takes |
| 3 | 1 to turn around the point given below, anything else around the middle of the sprite |
| 4 | the point to turn around, across |
| 5 | the point to turn around, down |

### 5. `WE_070`

Squashes the attacker down (Strength).  
_WestSp_WE_070, wsp_goto.c:424_

| word | meaning |
|---:|---|
| 0 | how far down to squash, as a percentage |
| 1 | _never read_ |
| 2 | how many frames the squash takes |
| 3 | _never read_ |

### 6. `WE_339`

One move's own effect.  
_WestSp_WE_339, wsp_goto.c:592_

| word | meaning |
|---:|---|
| 0 | _never read_ |

### 7. `WE_104`

One move's own effect.  
_WestSp_WE_104, wsp_goto.c:781_

| word | meaning |
|---:|---|
| 0 | _never read_ |

### 8. `WE_098`

One move's own effect.  
_WestSp_WE_098, wsp_tomoya.c:131_

### 9. `WE_065`

One move's own effect.  
_WestSp_WE_065, wsp_tomoya.c:344_

### 10. `WE_066`

Turns the attacker while moving it.  
_WestSp_WE_066, wsp_tool.c:824_

| word | meaning |
|---:|---|
| 0 | where the turn starts |
| 1 | where it ends |
| 2 | which of the routine's ways of doing it |

### 11. `WE_093`

One move's own effect.  
_WestSp_WE_093, wsp_tomoya.c:960_

### 12. `WE_151`

One move's own effect.  
_WestSp_WE_151, wsp_tomoya.c:1226_

### 13. `WE_074`

One move's own effect.  
_WestSp_WE_074, wsp_goto.c:944_

### 14. `WE_096`

One move's own effect.  
_WestSp_WE_096, wsp_goto.c:1045_

### 15. `WE_100`

One move's own effect.  
_WestSp_WE_100, wsp_goto.c:1196_

### 16. `WE_148`

Whitens the background and darkens the attacker together, holds, then brings both back.  
_WestSp_WE_148, wsp_goto.c:1352_

### 17. `WE_101AT`

One move's own effect, on the attacker.  
_WestSp_WE_101AT, wsp_tomoya.c:1525_

### 18. `WE_101DF`

One move's own effect, on the defender.  
_WestSp_WE_101DF, wsp_tomoya.c:1577_

### 19. `WE_150`

One move's own effect.  
_WestSp_WE_150, wsp_goto.c:1510_

### 20. `WE_180`

One move's own effect.  
_WestSp_WE_180, wsp_tomoya.c:1874_

### 22. `WE_107`

One move's own effect.  
_WestSp_WE_107, wsp_goto.c:1821_

| word | meaning |
|---:|---|
| 0 | _never read_ |

### 23. `WE_185`

One move's own effect.  
_WestSp_WE_185, wsp_tomoya.c:2820_

### 24. `WE_089`

One move's own effect.  
_WestSp_WE_089, wsp_goto.c:1999_

| word | meaning |
|---:|---|
| 0 | _never read_ |

### 25. `WE_204`

One move's own effect.  
_WestSp_WE_204, wsp_tomoya.c:3776_

| word | meaning |
|---:|---|
| 0 | which of the routine's ways of doing it |

### 26. `WE_171`

One move's own effect.  
_WestSp_WE_171, wsp_goto.c:2123_

| word | meaning |
|---:|---|
| 0 | which of the routine's ways of doing it |

### 27. `WE_175 / SHAKE`

Shakes a Pokemon, in one of two ways.  
_WestSp_WE_175, wsp_goto.c:2313_

| word | meaning |
|---:|---|
| 0 | 0 for one way of shaking, anything else for the other |
| 1 | how far it moves across |
| 2 | how far it moves down |
| 3 | how many frames each shake takes |
| 4 | how many shakes |
| 5 | who it acts on (a target flag) |

### 28. `WE_222`

One move's own effect.  
_WestSp_WE_222, wsp_goto.c:2412_

| word | meaning |
|---:|---|
| 0 | _never read_ |

### 29. `WE_216`

One move's own effect.  
_WestSp_WE_216, wsp_tomoya.c:4386_

### 30. `WE_233`

One move's own effect.  
_WestSp_WE_233, wsp_tomoya.c:4562_

### 31. `WE_207_MAIN`

One move's own effect.  
_WestSp_WE_207_MAIN, wsp_tomoya.c:3944_

### 32. `WE_262`

One move's own effect.  
_WestSp_WE_262, wsp_tomoya.c:5471_

### 33. `HAIKEI_PAL_FADE`

Fades the background's colours toward one colour and back.  
_WestSp_WE_HaikeiPalFade, wsp_tool.c:1114_

| word | meaning |
|---:|---|
| 0 | which palette set: 0 the backdrop, 1 the first effect layer, 2 the second |
| 1 | how many frames each step of the fade takes |
| 2 | how strong it starts, out of 16 |
| 3 | how strong it ends, out of 16 |
| 4 | the colour to fade toward |

### 34. `SSP_POKE_PAL_FADE`

Flashes a Pokemon a colour, over and over.  
_WestSp_WE_SSPPokePalFade, wsp_tool.c:1243_

| word | meaning |
|---:|---|
| 0 | who it acts on (a target flag) |
| 1 | how many frames each step of the fade takes |
| 2 | how many times it flashes |
| 3 | the colour to flash |
| 4 | how strong the flash gets, out of 16 |
| 5 | how many frames it holds at full strength |

### 35. `CAP_POKE_SCALE_UPDOWN`

Grows and shrinks a dropped copy of a Pokemon.  
_WestSp_WE_CAPPokeScaleUpDown, wsp_tool.c:1523_

| word | meaning |
|---:|---|
| 0 | 0 for the attacker's copy, anything else for the defender's |
| 1 | how see-through it is, out of 16 |
| 2 | the size it starts at |
| 3 | the size it ends at |
| 4 | what to divide those two sizes by |
| 5 | how many times it grows and shrinks |
| 6 | how many frames each step takes |
| 7 | which of the four dropped copies |

### 36. `WT_SHAKE`

Shakes a Pokemon, a dropped copy, or the background.  
_WestSp_WE_T01, wsp_tool.c:115_

| word | meaning |
|---:|---|
| 0 | how far it moves across, in pixels |
| 1 | how far it moves down, in pixels |
| 2 | how many frames each shake takes |
| 3 | how many shakes |
| 4 | who it acts on (a target flag) |

### 37. `WE_326`

One move's own effect.  
_WestSp_WE_326DF, wsp_tomoya.c:6319_

### 38. `CAP_ALPHA_FADE`

Fades dropped copies in or out.  
_WestSp_WE_CAP_NormalAlphaFade, wsp_tool.c:1895_

| word | meaning |
|---:|---|
| 0 | which of the four dropped copies, one bit each |
| 1 | how solid the copy starts |
| 2 | how solid it ends |
| 3 | how solid what is behind it starts |
| 4 | how solid that ends |
| 5 | how many frames the fade takes |

### 40. `SSP_POKE_VANISH`

Hides or shows a Pokemon.  
_WestSp_WE_SSP_PokeVanish, wsp_tool.c:1955_

| word | meaning |
|---:|---|
| 0 | who it acts on (a target flag) |
| 1 | 0 to show it, anything else to hide it |

### 41. `WE_252_BACK`

One move's own effect, on the background.  
_WestSp_WE_252Back, wsp_tomoya.c:6514_

### 42. `SSP_POKE_SCALE_UPDOWN`

Squashes and stretches a Pokemon, over and over.  
_WestSp_WE_SSPPokeScaleUpDown, wsp_tool.c:1716_

| word | meaning |
|---:|---|
| 0 | who it acts on (a target flag) |
| 1 | the width it starts at |
| 2 | the width it ends at |
| 3 | the height it starts at |
| 4 | the height it ends at |
| 5 | what to divide those sizes by |
| 6 | packed: the low half is how many times, the high half is how many frames it holds |
| 7 | how many frames each step takes |

### 43. `WE_252_POKE`

One move's own effect, on a Pokemon.  
_WestSp_WE_252SSPPoke, wsp_tomoya.c:6775_

### 44. `WE_T02`

Slides a background across the screen behind the battle.  
_WestSp_WE_T02, wsp_tool.c:299_

| word | meaning |
|---:|---|
| 0 | which background to use |
| 1 | where it starts, across |
| 2 | where it starts, down |
| 3 | how fast it moves across |
| 4 | how fast it moves down |
| 5 | whether to turn it around when the enemy is attacking |
| 6 | how solid it is |
| 7 | how many frames it lasts |

### 45. `WE_T22`

Slides a background across the screen behind the battle.  
_WestSp_WE_T22, wsp_tool.c:528_

| word | meaning |
|---:|---|
| 0 | which background to use |
| 1 | where it starts, across |
| 2 | where it starts, down |
| 3 | how fast it moves across |
| 4 | how fast it moves down |
| 5 | whether to turn it around when the enemy is attacking |
| 6 | how solid it is |
| 7 | how many frames it lasts |

### 47. `WE_224AT`

One move's own effect, on the attacker.  
_WestSp_WE_224AT, wsp_tomoya.c:7027_

### 48. `WE_224DF`

One move's own effect, on the defender.  
_WestSp_WE_224DF, wsp_tomoya.c:7168_

### 49. `WE_057`

The Surf wave.  
_WestSp_WE_057, wsp_goto.c:2789_

| word | meaning |
|---:|---|
| 0 | which of the routine's ways of doing it |

### 50. `WE_T03`

Blinks a Pokemon in and out.  
_WestSp_WE_T03, wsp_tool.c:2018_

| word | meaning |
|---:|---|
| 0 | how many times it blinks (the routine doubles this) |
| 1 | how many frames each blink takes |

### 51. `WE_T04`

Slides a Pokemon sideways and back.  
_WestSp_WE_T04, wsp_tool.c:2078_

| word | meaning |
|---:|---|
| 0 | how many frames the slide takes |
| 1 | how far it goes across |
| 2 | who it acts on (a target flag) |

### 52. `WE_T05`

Slides a Pokemon sideways and back.  
_WestSp_WE_T05, wsp_tool.c:2181_

| word | meaning |
|---:|---|
| 0 | how many frames the slide takes |
| 1 | how far it goes across |
| 2 | who it acts on (a target flag) |

### 53. `WE_T06`

Slides a Pokemon and holds it there.  
_WestSp_WE_T06, wsp_tool.c:2372_

| word | meaning |
|---:|---|
| 0 | where the slide starts |
| 1 | _never read_ |
| 2 | where it ends |
| 3 | _never read_ |
| 4 | how many frames to hold before coming back |
| 5 | who it acts on (a target flag) |

### 55. `WE_293`

One move's own effect.  
_WestSp_WE_293, wsp_goto2.c:997_

### 56. `WE_T08`

Puts a glow around the attacker (Superpower).  
_WestSp_WE_T08, wsp_tool.c:2623_

| word | meaning |
|---:|---|
| 0 | which of the routine's ways of doing it |
| 1 | _never read_ |

### 57. `WE_T10`

Slides a Pokemon and brings it back.  
_WestSp_WE_T10, wsp_tool.c:2695_

| word | meaning |
|---:|---|
| 0 | how many frames the slide takes |
| 1 | how far it goes across |
| 2 | how far it goes down |
| 3 | who it acts on (a target flag) |

### 58. `WE_102`

One move's own effect.  
_WestSp_WE_102, wsp_100.c:85_

### 59. `WE_325`

One move's own effect.  
_WestSp_WE_325, wsp_300.c:120_

| word | meaning |
|---:|---|
| 0 | _never read_ |

### 60. `WE_KAITEN`

Swings a Pokemon around in a circle.  
_WestSp_WE_Kaiten, wsp_tool.c:2802_

| word | meaning |
|---:|---|
| 0 | who it acts on (a target flag) |
| 1 | where the swing starts |
| 2 | where it ends |

### 61. `WE_DISP_OUT`

Slides a Pokemon off the screen.  
_WestSp_WE_DispOut, wsp_tool.c:2861_

| word | meaning |
|---:|---|
| 0 | who it acts on (a target flag) |
| 1 | how many frames it takes |

### 62. `WE_DISP_DEF`

Puts a Pokemon straight back where it belongs.  
_WestSp_WE_DispDef, wsp_tool.c:2997_

| word | meaning |
|---:|---|
| 0 | who it acts on (a target flag) |

### 63. `WE_OAM_PAL_FADE`

Fades the colours of dropped copies toward one colour.  
_WestSp_WE_OAM_PalFade, wsp_tool.c:3067_

| word | meaning |
|---:|---|
| 0 | which of the four dropped copies, one bit each |
| 1 | how many frames each step takes |
| 2 | how the fade is applied |
| 3 | how strong it starts |
| 4 | how strong it ends |
| 5 | the colour to fade toward |

### 65. `EMIT_STRAIGHT`

Moves a particle emitter in a straight line.  
_WSP_Emitter_Straight, wsp_tool.c:3820_

| word | meaning |
|---:|---|
| 0 | which emitter to move |
| 1 | how far past the target it ends up, across |
| 2 | how far past the target it ends up, down |
| 3 | how many frames to wait before starting |
| 4 | how many frames the move takes |
| 5 | how high the arc goes |
| 6 | 0 from the attacker toward the defender, 1 the other way |
| 7 | packed: the low half is when to stop looping, the high half a spare loop count |
| 8 | how much the path curves |

### 66. `EMIT_PARABOLIC`

Moves a particle emitter along an arc.  
_WSP_Emitter_Parabolic, wsp_tool.c:3981_

| word | meaning |
|---:|---|
| 0 | which emitter to move |
| 1 | how far past the target it ends up, across |
| 2 | how far past the target it ends up, down |
| 3 | how many frames to wait before starting |
| 4 | how many frames the move takes |
| 5 | how high the arc goes |
| 6 | 0 from the attacker toward the defender, 1 the other way |
| 7 | packed: the low half is when to stop looping, the high half a spare loop count |
| 8 | how much the path curves |

### 67. `RECT_VIEW`

Wipes a Pokemon in or out behind a moving edge.  
_WSP_RectView, wsp_tool.c:3359_

| word | meaning |
|---:|---|
| 0 | who it acts on (a target flag) |
| 1 | _never read_ |
| 2 | where the edge starts |
| 3 | where the edge ends |
| 4 | how many frames the wipe takes |
| 5 | 0 to wipe one way, anything else the other |

### 68. `BG_SHAKE`

Shakes the background.  
_WestSp_WE_BgShake, wsp_tool.c:3536_

| word | meaning |
|---:|---|
| 0 | how far it moves across |
| 1 | how far it moves down |
| 2 | how many frames each shake takes |
| 3 | how many shakes |
| 4 | how many extra times to run the whole thing |
| 5 | 0 for one background frame, anything else for the other |

### 69. `MOSAIC`

Breaks a dropped copy into blocks and back.  
_WSP_Mosaic, wsp_tool.c:3620_

| word | meaning |
|---:|---|
| 0 | which of the four dropped copies |
| 1 | how much to change the block size each step, negative to go back to none |
| 2 | block size across |
| 3 | block size down |

### 70. `WSP_272`

One move's own effect.  
_WSP_272, wsp_300.c:404_

| word | meaning |
|---:|---|
| 0 | _never read_ |

### 71. `WSP_289`

One move's own effect.  
_WSP_289, wsp_300.c:594_

| word | meaning |
|---:|---|
| 0 | who it acts on (a target flag) |

### 72. `EMIT_ROTATION`

Swings a particle emitter around a Pokemon.  
_WSP_Emitter_Rotation, wsp_tool.c:4090_

| word | meaning |
|---:|---|
| 0 | which emitter to move |
| 1 | the angle it starts at, across, in degrees |
| 2 | the angle it ends at, across, in degrees |
| 3 | the angle it starts at, down, in degrees |
| 4 | the angle it ends at, down, in degrees |
| 5 | how wide the circle is |
| 6 | how tall the circle is |
| 7 | how many frames the swing takes |
| 8 | 0 to swing around the attacker, anything else around the defender |
| 9 | which set of particles to swing |

### 73. `EMIT_SIMPLE_UD`

Moves a particle emitter up or down.  
_WSP_Emitter_SimpleUD, wsp_tool.c:3877_

| word | meaning |
|---:|---|
| 0 | which emitter to move |
| 1 | 0 uses the attacker's position, anything else the defender's |
| 2 | 0 comes down onto the Pokemon from above the screen, anything else rises away from it |
| 3 | how many frames the move takes |
| 4 | how many frames to wait before starting |
| 5 | packed: the low half is when to stop looping, the high half a spare loop count |

### 74. `PALCOL_CHANGE`

Drains the colour out of the scene, or puts it back.  
_WSP_PalColChange, wsp_tool.c:4518_

| word | meaning |
|---:|---|
| 0 | 0 to put the colours back, anything else to drain them |

### 75. `POKE_OAM_VIEW`

Changes how a dropped copy is drawn and where it sits in the stack.  
_WSP_PokeOAM_View, wsp_tool.c:4701_

| word | meaning |
|---:|---|
| 0 | which of the four dropped copies |
| 1 | how many frames it lasts |
| 2 | which background layer to sit against |
| 3 | where it sits among the sprites |
| 4 | which copy is being dropped |
| 5 | which of the routine's ways of doing it |
| 6 | 0 for the attacker's side, anything else for the defender's |

### 76. `LASTER`

Ripples the screen line by line.  
_WestSp_WE_Laster, wsp_tool.c:4975_

| word | meaning |
|---:|---|
| 0 | how many frames the ripple lasts |

### 77. `DISP_MOVE`

Slides a Pokemon off the screen or back on.  
_WestSp_WE_DispMove, wsp_tool.c:2921_

| word | meaning |
|---:|---|
| 0 | 0 to send it off, anything else to bring it back |
| 1 | who it acts on (a target flag) |
| 2 | how many frames it takes |
| 3 | _never read_ |
| 4 | _never read_ |

### 78. `ALL_DROP`

Keeps all four Pokemon drawn as sprites while the particle data loads.  
_WSP_AllPokeDrop, wsp_tool.c:4873_

| word | meaning |
|---:|---|
| 0 | how many frames to keep them drawn, or 0 for the usual loading wait |

### 79. `WSP_166`

One move's own effect.  
_WSP_166, wsp_300.c:758_

| word | meaning |
|---:|---|
| 0 | _never read_ |

### 82. `ST_EFF_RECOVER`

Scrolls an overlay upward behind a Pokemon, for getting its health back.  
_StatusEffect_Recover, wsp_steff.c:355_

| word | meaning |
|---:|---|
| 0 | which background graphic to scroll |
| 1 | 0 behind the attacker, anything else behind the defender |

### 83. `ST_EFF_METAL`

Scrolls an overlay downward behind a Pokemon, for turning metallic.  
_StatusEffect_Metal, wsp_steff.c:377_

| word | meaning |
|---:|---|
| 0 | which background graphic to scroll |
| 1 | 0 behind the attacker, anything else behind the defender |

