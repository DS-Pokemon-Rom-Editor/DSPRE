[Research](../../ResearchNotes.md) / Trainer Sprites Logic

# Trainer Sprites Logic, Diamond/Pearl, Platinum and HeartGold/SoulSilver

Source: the [pokediamond](https://github.com/pret/pokediamond), [pokeplatinum](https://github.com/pret/pokeplatinum) and [pokeheartgold](https://github.com/pret/pokeheartgold) decomps, cited by file and function, and the retail US ROMs, whose archives were read and drawn entry by entry. This was structured into a document with AI.

Every trainer the player battles is drawn from a front sprite chosen by trainer class, and the player's side of a battle is drawn from a back sprite. This covers where both live, how each entry is laid out, which back sprite the game picks for which class, how the sprites animate, the extra "scan" copy some screens read, and what DSPRE edits.

## The archives

| Game | Front sprites | Back sprites | Files per sprite |
|---|---|---|---|
| Diamond and Pearl | `poketool/trgra/trfgra.narc`, 196 files, 98 classes | `poketool/trgra/trbgra.narc`, 16 files, 8 sprites | 2 |
| Platinum | `poketool/trgra/trfgra.narc`, 525 files, 105 classes | `poketool/trgra/trbgra.narc`, 55 files, 11 sprites | 5 |
| HeartGold and SoulSilver | `a/0/5/8`, 645 files, 129 classes | `a/0/0/6`, 85 files, 17 sprites | 5 |

Front sprite `n` is trainer class `n`: its first file is at `n × files per sprite`. Back sprites are numbered separately, see below.

## One sprite's files

**Platinum and HeartGold** store five files per sprite (`SpriteSystem_SetTrainerClassGraphicsIndex` in pokeplatinum `src/pokemon.c`, `sub_02070D3C` in pokeheartgold `src/pokemon.c`; the order is `tools/ordergen/trainer_graphics.py` in pokeplatinum):

| File | Contents |
|---|---|
| 0 | the drawing (NCGR), tiles for 2D cells |
| 1 | the colours (NCLR), one 16-colour palette |
| 2 | the cells (NCER), which tiles make each pose |
| 3 | the animation (NANR), which pose shows for how long |
| 4 | the scan (NCGR), a 160 by 80 bitmap (20 by 10 tiles, 4 bits per pixel, not tiled): for most sprites frame 0 on the left and frame 1 on the right, each the middle 80 by 80 of the pose, with retail exceptions listed in [Battle Sprite Cipher and Scan](../SpriteCipher/SpriteCipherLogic.md); scrambled |

A sprite drawn from files 0 to 3 is built from cells, so a pose can be any arrangement of the drawing's tiles and poses can share tiles. The Castle Valet's front sprite is the one exception in Platinum: its cells overlap, so its drawing is built differently, and its colours are loaded with `paletteIdx` 2 instead of 1 (`SpriteSystem_NewManagedSpriteTrainer`).

**Diamond and Pearl** store two files per sprite, the drawing and the colours (`sub_02068FE0` in pokediamond `arm9/src/pokemon.c`). The drawing is a 160 by 80 image of two 80 by 80 frames side by side, in the same format as a Pokémon battle sprite, pixels scrambled the same way. There are no cells or animation files.

**The scramble.** Scrambled pixels are XORed with a running key, stepped `key = key × 1103515245 + 24691` per 16-bit word. Diamond, Pearl and HeartGold seed the key from the last word and walk backwards (`UnscanPokepic` in pokeheartgold `src/pokepic.c` names both HeartGold trainer archives as using the Diamond and Pearl way); Platinum seeds it from the first word and walks forwards. The key is seeded from the data itself, so the seeding word always decodes to 0.

## Front sprites

The battle shows the front sprite of the trainer's class, from the trainer's data.

**Animation, Platinum** (`BattleDisplay` trainer encounter task in pokeplatinum `src/battle/battle_display.c`). Every front sprite has sequence 0, a still pose. A sprite with three sequences plays sequence 2 while it slides in, and once it arrives any sprite with more than one sequence plays sequence 1. The count the code compares is the number of sequences (`ManagedSprite_GetNumFrames` returns `Sprite_GetAnimCount`, `src/sprite_system.c`). The animation's per-frame user attributes steer it: `1` flashes the screen white once (every layer except background 1), `4095` marks the end, and `0x1NN` jumps to sequence `NN - 1`. 25 of Platinum's 105 front sprites have more than one sequence: the gym leaders, the Elite Four, Cynthia, the rival, Cyrus, the five partners (Cheryl, Riley, Marley, Buck, Mira) and the Frontier Brains. Four of them have the third, slide-in sequence: Aaron, Gardenia, Fantina and Volkner.

**Animation, HeartGold.** The files have the same shape: 31 of the 129 front sprites have more than one sequence, 19 of them three (the rival, the gym leaders of both regions, the Elite Four, Lance, the Frontier Brains, Giovanni and a few others). The HeartGold battle code that plays them is not named in the decomp and was not traced.

Diamond and Pearl's front sprites do not animate.

## Back sprites

| # | Diamond and Pearl | Platinum | HeartGold and SoulSilver |
|---|---|---|---|
| 0 | Lucas | Lucas | Ethan |
| 1 | Dawn | Dawn | Lyra |
| 2 | Barry | Barry | Silver |
| 3 | Cheryl | Cheryl | Lance |
| 4 | Riley | Riley | Cheryl |
| 5 | Marley | Marley | Riley |
| 6 | Buck | Buck | Marley |
| 7 | Mira | Mira | Buck |
| 8 | | Lucas (Diamond and Pearl) | Mira |
| 9 | | Dawn (Diamond and Pearl) | Lucas (Diamond and Pearl) |
| 10 | | Barry (Diamond and Pearl) | Dawn (Diamond and Pearl) |
| 11 | | | Barry (Diamond and Pearl) |
| 12 | | | Lucas (Platinum) |
| 13 | | | Dawn (Platinum) |
| 14 | | | Barry (Platinum) |
| 15 | | | Ethan, single battles |
| 16 | | | Lyra, single battles |

Checked by drawing every entry from the retail ROMs. pokeheartgold's `TRAINER_BACKPIC_*` constants name 6, 7 and 8 Buck, Mira and Marley; the drawings are Marley, Buck and Mira.

**How the game picks one.** Each game maps the class to a back sprite, and any class not listed falls back to the male or female player by the class's gender:

- **Diamond and Pearl** (`sub_0206AA30`): the Barry class to Barry, the five partner classes to their own sprites.
- **Platinum** (`SpriteSystem_TrainerClassBackSpriteIndex`): the two player classes to themselves, the rival to Barry, the five partners to 3 to 7, and the two "Diamond and Pearl player" classes (103 and 104) to 8 and 9. In a link battle, an opponent playing Diamond or Pearl is shown with those two classes (`ov16_02264768`, `battle_display.c`).
- **HeartGold** (`TrainerClassToBackpicID`): Ethan and Lyra to 0 and 1, or to 15 and 16 when its second argument is set (see below); the rival to Silver; Lance to 3; classes 90 to 94 to 4 to 8 in order; the Diamond and Pearl and Platinum Lucas and Dawn classes to 9, 10, 12 and 13. Classes 90 and 91 have Cheryl's and Riley's front sprites; 92 to 94 have other trainers' front sprites, so which battles show Marley, Buck and Mira from behind was not established.

No class reaches Platinum's 10 or HeartGold's 11 and 14 (the Barrys from the other games) in those functions.

**The player in battle.** Which files each battle draws:

- **Platinum** (Route 201 rival battle): the slide-in, the stance and the throw are all drawn from entry 0 or 1, files 0 to 3.
- **HeartGold** (a wild battle in Victory Road): the stance and the throw are drawn from entry 15 (Ethan), files 0 to 3; the slide-in is entry 0's scan. Recolouring entry 0's drawing changed nothing on screen. The argument pokeheartgold names `isLink` is set by `ov12_0225A07C` to 1 unless the battle is a double battle that is not a multi battle, or `ov12_0223C140` returns 0xFF, which it does in link, Safari, Frontier and Pal Park battles (`src/battle/battle_system.c`). So 15 and 16 are the ordinary single-battle backs, and 0 and 1 are used in those double battles and, going by the code alone, in link, Safari, Frontier and Pal Park battles; `ov12_0225A37C` always asks for 0 or 1. Lyra's 1 and 16 follow the same code.

In HeartGold, 0 and 15 (and 1 and 16) are two separate drawings of the same trainer: same palette, different drawing, cells, animation and scan, 8 poses each. The visible difference is the throw, open-handed in 15 and 16.

**Animation.** Every Platinum and HeartGold back sprite has two sequences: a still pose and the throw, which the battle plays when the Poké Ball is thrown (`battle_display.c`, trainer throw task). Platinum's throw is 9 frames for the players and the rival and 6 for the partners.

## The scan copy

File 4 is a second, simpler copy of the sprite: two still frames in the Pokémon battle sprite format, with no cells. The game draws it wherever it shows a trainer as a plain picture:

- **HeartGold:** the player's slide-in at the start of a battle, and the Hall of Fame (`register_hall_of_fame.c`, statically).
- **Platinum:** the player's picture in the Hall of Fame (`src/cutscenes/hall_of_fame.c`) and each friend's picture on the Wi-Fi friend roster (`src/overlay064`). The battle and one screen in overlay 70 use files 0 to 3; the VS intro reads only file 1, the colours, to fade the trainer (`EncounterEffect_BlendTrainerSpritePltt`, `src/overlay005/encounter_effect.c`).

The scan is built separately from the drawing, so the two can differ, and an edit to files 0 and 1 alone leaves the old picture on those screens. It is scrambled: Platinum seeds the key from the first word and walks forwards, HeartGold from the last word and walks backwards (hand-patching HeartGold's scan this way changed the slide-in in game). In both, the seeding word's plain value is 0, the top-left or bottom-right pixels of an empty corner.

## Changing one by hand

- **Recolour or redraw a sprite:** edit files 0 and 1 of the entry (Diamond and Pearl: unscramble, edit and rescramble file 0). In Platinum and HeartGold, also redraw the scan from frames 0 and 1 and rescramble it. For HeartGold's player, edit 15 or 16 for single battles, 0 or 1 for double battles, and 0 or 1's scan for the slide-in.
- **Change a pose or animation:** edit the cells or the animation file; keep sequence 0 a still pose. A front sprite plays sequence 1 when it arrives and sequence 2, if it has one, while sliding in; a back sprite throws with sequence 1.
- **Give a class a different back sprite:** the mapping is code, so it needs a patch to the function above; the back sprite entries themselves are plain data.

## What DSPRE does

| What | Edited in DSPRE | Written |
|---|---|---|
| Front sprites | Trainers > Trainer Sprite Editor, or Edit Sprite… in Trainer Classes: paint, colours, PNG import and export, sprite sheets | files 0 and 1 on every save, and file 4, the scan, redrawn from frames 0 and 1 and rescrambled; a sheet import can also add or remove frames and set an animation, which writes files 2 and 3 |
| Back sprites | Trainers > Trainer Back Sprite Editor, the same editor with the back sprites | the same; HeartGold's 0 and 15, and 1 and 16, are edited as one sprite across both battle sets |
| Animations | shown and played from the ROM, and set from an animation sheet | file 3 |

Both editors are beta editors. In Platinum and HeartGold a stroke on a pose writes into the tiles that pose uses, so poses sharing tiles change together; Diamond and Pearl are edited as the flat two-frame drawing and rescrambled.

Export PNG writes colour 0 as the palette's own colour; the editor only shows it see-through, and Import PNG reads see-through pixels as colour 0. The frame strip redraws as the sprite is painted or imported.

A frames sheet is every frame side by side in 128 by 128 cells, one row per set, with the frame's origin in the middle of each cell; an animation sheet is one cell per step as it plays. A JSON file beside the picture lists what each cell is and, for an animation, each step's hold. Import matches frames by position, and an animation's drawings to the frames already drawn, adding new frames for new drawings. An animation added to a sprite that had none plays once, as the battle expects.

Back sprite names are listed per game in `DSPRE.Core/ROMFiles/TrainerBackSprites.cs`, where HeartGold's 15 and 16 are "Ethan, single battles" and "Lyra, single battles". The layout (two or five files per sprite, scrambled or not) is `DSPRE.Core/TrainerGraphicsLayout.cs`.
