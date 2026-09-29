[Research](../../ResearchNotes.md) / [Move Research](../MoveResearch.md) / Move Animation Routines

# The move animation routines

What each routine a move animation script can call reads out of the words handed to it. The names
are pokeplatinum's (`sBattleAnimScriptFuncs` in `src/battle_anim/script_func_tables.c`); the page is
written from DSPRE's `BattleAnimFuncs.cs`, which is what the editor reads, so the two cannot drift
apart.

A script calls one with `CallFunc id, count, words`. The id indexes the 84-entry table directly.
`BattleAnimScriptCmd_CallFunc` (`src/battle_anim/battle_anim_system.c`) copies `count` words into the
ten `scriptVars` (`BATTLE_ANIM_SCRIPT_VAR_COUNT`) and zeroes the rest, so a routine handed fewer words
than it reads still runs and sees zeros; it is never skipped. HeartGold's scripts call the same ids
with the same word layouts.

A word shown as never read is one the scripts hand over that the routine never looks at. Those are
left blank on purpose rather than invented.

Where a word picks out Pokemon it is a target flag, pokeplatinum's `BATTLE_ANIM_*` masks in
`include/constants/battle/battle_anim.h`. `BATTLE_ANIM_ATTACKER` and `BATTLE_ANIM_DEFENDER` are relative
to the move, not to the sides of the field; the `_PARTNER` flags only exist in a double battle;
`BATTLE_ANIM_ALL_BATTLERS` is everybody and `BATTLE_ANIM_NOT_ATTACKER` everybody but the attacker. With
`BATTLE_ANIM_SPECIFIC_BATTLER` set the same bits name fixed slots instead (`BATTLE_ANIM_BATTLER_PLAYER_1`,
`BATTLE_ANIM_BATTLER_ENEMY_1`), whoever is attacking; Cosmic Power, Lava Plume and Muddy Water fade those.

### 0. `Nop`

A sample routine the games left in. Does nothing.

### 1. `AnimExample`

A sample routine the games left in. Does nothing.

### 2. `SoundExample`

A sample routine the games left in. Does nothing.

### 3. `GenericExample`

A sample routine the games left in. Does nothing.

### 4. `RotateMon`

Turns a Pokemon on the spot.

| word | meaning |
|---:|---|
| 0 | angle to start at |
| 1 | angle to end at |
| 2 | how many frames the turn takes |
| 3 | 0 turns the defender, 1 the attacker around the point given below, 2 the defender the other way |
| 4 | the point to turn around, across |
| 5 | the point to turn around, down |

### 5. `Strength`

Squashes the attacker down (Strength).

| word | meaning |
|---:|---|
| 0 | how far down to squash, as a percentage |
| 1 | _never read_ |
| 2 | how many frames the squash takes |
| 3 | _never read_ |

### 6. `BulkUp`

One move's own effect.

| word | meaning |
|---:|---|
| 0 | _never read_ |

### 7. `DoubleTeam`

One move's own effect.

| word | meaning |
|---:|---|
| 0 | _never read_ |

### 8. `QuickAttack`

One move's own effect.

### 9. `DrillPeck`

One move's own effect.

### 10. `Submission`

Spins a Pokemon round (Submission).

| word | meaning |
|---:|---|
| 0 | how many times it goes round |
| 1 | how many frames each turn takes |
| 2 | which battler it spins |

### 11. `Confusion`

One move's own effect.

### 12. `AcidArmor`

One move's own effect.

### 13. `Growth`

One move's own effect.

### 14. `Meditate`

One move's own effect.

### 15. `Teleport`

One move's own effect.

### 16. `Flash`

Whitens the background and darkens the attacker together, holds, then brings both back.

### 17. `NightShadeAttacker`

One move's own effect, on the attacker.

### 18. `NightShadeDefender`

One move's own effect, on the defender.

### 19. `Splash`

One move's own effect.

### 20. `Spite`

One move's own effect.

### 22. `Minimize`

One move's own effect.

| word | meaning |
|---:|---|
| 0 | _never read_ |

### 23. `FaintAttack`

One move's own effect.

### 24. `Earthquake`

One move's own effect.

| word | meaning |
|---:|---|
| 0 | _never read_ |

### 25. `PlayfulHops`

One move's own effect.

| word | meaning |
|---:|---|
| 0 | which of the routine's ways of doing it |

### 26. `Nightmare`

One move's own effect.

| word | meaning |
|---:|---|
| 0 | which of the routine's ways of doing it |

### 27. `Flail`

Shakes a Pokemon, in one of two ways.

| word | meaning |
|---:|---|
| 0 | 0 for one way of shaking, anything else for the other |
| 1 | how far it moves across |
| 2 | how far it moves down |
| 3 | how many frames each shake takes |
| 4 | how many shakes |
| 5 | who it acts on (a target flag) |

### 28. `Magnitude`

One move's own effect.

| word | meaning |
|---:|---|
| 0 | _never read_ |

### 29. `Return`

One move's own effect.

### 30. `VitalThrow`

One move's own effect.

### 31. `Swagger`

One move's own effect.

### 32. `Memento`

One move's own effect.

### 33. `FadeBg`

Fades the background's colours toward one colour and back.

| word | meaning |
|---:|---|
| 0 | which palette set: 0 the backdrop, 1 the first effect layer, 2 the second |
| 1 | how many frames each step of the fade takes |
| 2 | how strong it starts, out of 16 |
| 3 | how strong it ends, out of 16 |
| 4 | the colour to fade toward |

### 34. `FadeBattlerSprite`

Flashes a Pokemon a colour, over and over.

| word | meaning |
|---:|---|
| 0 | who it acts on (a target flag) |
| 1 | how many frames each step of the fade takes |
| 2 | how many times it flashes |
| 3 | the colour to flash |
| 4 | how strong the flash gets, out of 16 |
| 5 | how many frames it holds at full strength |

### 35. `ScalePokemonSprite`

Grows and shrinks a dropped copy of a Pokemon.

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

### 36. `Shake`

Shakes a Pokemon, a dropped copy, or the background.

| word | meaning |
|---:|---|
| 0 | how far it moves across, in pixels |
| 1 | how far it moves down, in pixels |
| 2 | how many frames each shake takes |
| 3 | how many shakes |
| 4 | who it acts on (a target flag) |

### 37. `Extrasensory`

One move's own effect.

### 38. `AlphaFadePokemonSprite`

Fades dropped copies in or out.

| word | meaning |
|---:|---|
| 0 | which of the four dropped copies, one bit each |
| 1 | how solid the copy starts |
| 2 | how solid it ends |
| 3 | how solid what is behind it starts |
| 4 | how solid that ends |
| 5 | how many frames the fade takes |

### 40. `HideBattler`

Hides or shows a Pokemon.

| word | meaning |
|---:|---|
| 0 | who it acts on (a target flag) |
| 1 | 0 to show it, anything else to hide it |

### 41. `FakeOutCurtain`

One move's own effect, on the background.

### 42. `ScaleBattlerSprite`

Squashes and stretches a Pokemon, over and over.

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

### 43. `FakeOut`

One move's own effect, on a Pokemon.

### 44. `ScrollCustomBg`

Slides a background across the screen behind the battle.

| word | meaning |
|---:|---|
| 0 | which background to use |
| 1 | where it starts, across |
| 2 | where it starts, down |
| 3 | how fast it moves across |
| 4 | how fast it moves down |
| 5 | whether to turn it around when the enemy is attacking |
| 6 | how solid it is |
| 7 | how many frames between each slowing down |

### 45. `MuddyWater`

Slides a background across the screen behind the battle.

| word | meaning |
|---:|---|
| 0 | which background to use |
| 1 | where it starts, across |
| 2 | where it starts, down |
| 3 | how fast it moves across |
| 4 | how fast it moves down |
| 5 | whether to turn it around when the enemy is attacking |
| 6 | _never read_ |
| 7 | how many frames between each slowing down |

### 47. `MegahornAttacker`

One move's own effect, on the attacker.

### 48. `MegahornDefender`

One move's own effect, on the defender.

### 49. `Surf`

The Surf wave.

| word | meaning |
|---:|---|
| 0 | which of the routine's ways of doing it |

### 50. `BlinkAttacker`

Blinks a Pokemon in and out.

| word | meaning |
|---:|---|
| 0 | how many times it blinks (the routine doubles this) |
| 1 | how many frames each blink takes |

### 51. `MoveBattlerX`

Slides a Pokemon sideways and back.

| word | meaning |
|---:|---|
| 0 | how many frames the slide takes |
| 1 | how far it goes across |
| 2 | who it acts on (a target flag) |

### 52. `MoveBattlerX2`

Slides a Pokemon sideways and back.

| word | meaning |
|---:|---|
| 0 | how many frames the slide takes |
| 1 | how far it goes across |
| 2 | who it acts on (a target flag) |

### 53. `ShakeAndScaleAttacker`

Shakes and stretches the attacker, then holds it.

| word | meaning |
|---:|---|
| 0 | the first stretch |
| 1 | the second stretch |
| 2 | how many frames the first takes |
| 3 | how many frames the second takes |
| 4 | how many frames to hold before coming back |
| 5 | _never read_ |

### 55. `Camouflage`

One move's own effect.

### 56. `Superpower`

Puts a glow around the attacker (Superpower).

| word | meaning |
|---:|---|
| 0 | _never read_ |
| 1 | _never read_ |

### 57. `MoveBattler`

Slides a Pokemon and brings it back.

| word | meaning |
|---:|---|
| 0 | how many frames the slide takes |
| 1 | how far it goes across |
| 2 | how far it goes down |
| 3 | who it acts on (a target flag) |

### 58. `Mimic`

One move's own effect.

### 59. `ShadowPunch`

One move's own effect.

| word | meaning |
|---:|---|
| 0 | _never read_ |

### 60. `RevolveBattler`

Swings a Pokemon around in a circle.

| word | meaning |
|---:|---|
| 0 | who it acts on (a target flag) |
| 1 | how many times it goes round |
| 2 | how many frames each turn takes |

### 61. `MoveBattlerOffScreen`

Slides a Pokemon off the screen.

| word | meaning |
|---:|---|
| 0 | who it acts on (a target flag) |
| 1 | how many frames it takes |

### 62. `MoveBattlerToDefaultPos`

Puts a Pokemon straight back where it belongs.

| word | meaning |
|---:|---|
| 0 | who it acts on (a target flag) |

### 63. `FadePokemonSprite`

Fades the colours of dropped copies toward one colour.

| word | meaning |
|---:|---|
| 0 | which of the four dropped copies, one bit each |
| 1 | how many frames each step takes |
| 2 | how much each step changes it |
| 3 | how strong it starts |
| 4 | how strong it ends |
| 5 | the colour to fade toward |

### 65. `MoveEmitterA2BLinear`

Moves a particle emitter in a straight line.

### 66. `MoveEmitterA2BParabolic`

Moves a particle emitter along an arc.

| word | meaning |
|---:|---|
| 0 | which emitter to move |
| 1 | how far past the target it ends up, across |
| 2 | how far past the target it ends up, down |
| 3 | how many frames to wait before starting |
| 4 | how many frames the move takes |
| 5 | how high the arc goes |
| 6 | 0 from the attacker toward the defender, 1 the other way |
| 7 | packed: the low half caps the move at that many frames, the high half skips that many frames at the start |
| 8 | how much the path curves |

### 67. `BattlerPartialDraw`

Wipes a Pokemon in or out behind a moving edge.

| word | meaning |
|---:|---|
| 0 | who it acts on (a target flag) |
| 1 | _never read_ |
| 2 | _never read_ |
| 3 | how far the edge moves each step, its sign choosing in or out |
| 4 | how many frames between steps |
| 5 | 1 to draw it the way Sketch does |

### 68. `ShakeBg`

Shakes the background.

| word | meaning |
|---:|---|
| 0 | how far it moves across |
| 1 | how far it moves down |
| 2 | how many frames each shake takes |
| 3 | how many shakes |
| 4 | how many extra times to run the whole thing |
| 5 | 0 for one background frame, anything else for the other |

### 69. `PixelatePokemonSprite`

Breaks a dropped copy into blocks and back.

| word | meaning |
|---:|---|
| 0 | which of the four dropped copies |
| 1 | how much to change the block size each step, negative to go back to none |
| 2 | block size across |
| 3 | block size down |

### 70. `RolePlay`

One move's own effect.

| word | meaning |
|---:|---|
| 0 | _never read_ |

### 71. `Snatch`

One move's own effect.

| word | meaning |
|---:|---|
| 0 | who it acts on (a target flag) |

### 72. `RevolveEmitter`

Swings a particle emitter around a Pokemon.

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

### 73. `MoveEmitterViewportTop`

Moves a particle emitter up or down.

| word | meaning |
|---:|---|
| 0 | which emitter to move |
| 1 | 0 uses the attacker's position, anything else the defender's |
| 2 | 0 comes down onto the Pokemon from above the screen, anything else rises away from it |
| 3 | how many frames the move takes |
| 4 | how many frames to wait before starting |
| 5 | packed: the low half caps the move at that many frames, the high half skips that many frames at the start |

### 74. `SetBgGrayscale`

Drains the colour out of the scene, or puts it back.

| word | meaning |
|---:|---|
| 0 | 0 to put the colours back, anything else to drain them |

### 75. `SetPokemonSpritePriority`

Changes how a dropped copy is drawn and where it sits in the stack.

| word | meaning |
|---:|---|
| 0 | which of the four dropped copies |
| 1 | how many frames it lasts |
| 2 | which background layer to sit against |
| 3 | where it sits among the sprites |
| 4 | which battler it is |
| 5 | which of the routine's ways of doing it |
| 6 | the window type, used only by Dark Void |

### 76. `ScrollSwitchedBg`

Ripples the screen line by line.

| word | meaning |
|---:|---|
| 0 | how many frames the ripple lasts |

### 77. `MoveBattlerOnOrOffScreen`

Slides a Pokemon off the screen or back on.

| word | meaning |
|---:|---|
| 0 | 0 to send it off, anything else to bring it back |
| 1 | who it acts on (a target flag) |
| 2 | how many frames it takes |
| 3 | _never read_ |
| 4 | _never read_ |

### 78. `RenderPokemonSprites`

Keeps all four Pokemon drawn as sprites while the particle data loads.

| word | meaning |
|---:|---|
| 0 | how many frames to keep them drawn, or 0 for the usual loading wait |

### 79. `Sketch`

One move's own effect.

| word | meaning |
|---:|---|
| 0 | _never read_ |

### 82. `StatChangeHeal`

Scrolls an overlay upward behind a Pokemon, for getting its health back.

| word | meaning |
|---:|---|
| 0 | which background graphic to scroll |
| 1 | 0 behind the attacker, anything else behind the defender |

### 83. `StatChangeMetal`

Scrolls an overlay downward behind a Pokemon, for turning metallic.

| word | meaning |
|---:|---|
| 0 | which background graphic to scroll |
| 1 | 0 behind the attacker, anything else behind the defender |

## Not described yet

DSPRE has no word layout for these routines yet; the editor shows their words as plain numbers:

21 `Harden`, 39 `OdorSleuth`, 46 `Megahorn`, 54 `ShakeAndScaleAttacker2`, 64 `BattlerPartialDrawTest`, 80 `StatChangeUp`, 81 `StatChangeDown`.
