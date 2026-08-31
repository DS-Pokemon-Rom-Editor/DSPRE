[Research](../../ResearchNotes.md) / [Move Research](../MoveResearch.md) / Move Animation Test Coverage

# Which moves to record, and what each one is for

Generated from both ROMs by `MoveCoverageTests`. Do not edit by hand.

Comparing DSPRE's animation preview against the real game means recording moves, and recording every move is not practical. These are the moves that between them exercise everything a move animation can do.

A mechanism is counted once per game, because most scripts differ between HeartGold and Platinum, so covering one says nothing about the other.

- **IPKE**: 501 scripts, 197 distinct mechanisms
- **CPUE**: 501 scripts, 192 distinct mechanisms
- **Together**: 197 distinct mechanisms, 389 game-and-mechanism pairs, covered by 77 moves

## The order to record them in

The first 17 cover every opcode and every drawing path between them, so an error affecting many moves at once shows up early. The rest fill in the operator settings and the routines only one or two moves ever call.

| # | move | first covers |
|---:|---|---|
| 1 | 143 Sky Attack | draws with: a background swap, draws with: dropped sprite copies, draws with: particles, opcode: WEST_ADD_PARTICLE and 39 more |
| 2 | 232 Metal Claw | draws with: a status overlay, draws with: cell actors, draws with: motion only, opcode: WEST_CATS_ACT_ADD and 11 more |
| 3 | 352 Water Pulse | opcode: WEST_ADD_PARTICLE_EMIT_SET, opcode: WEST_END_CALL, opcode: WEST_HAIKEI_HALF_WAIT, opcode: WEST_SEQ_CALL and 6 more |
| 4 | 151 Acid Armor | draws with: a Pokemon background, opcode: WEST_POKEBG_DROP, opcode: WEST_POKEBG_DROP_RESET, opcode: WEST_SE_WAITPLAY and 1 more |
| 5 | 272 Role Play | draws with: a replaced Pokemon graphic, opcode: WEST_HENSIN_ON, opcode: WEST_HENSIN_ON_RC, routine: WSP_272 |
| 6 | 45 Growl | opcode: WEST_VOICE_PLAY, opcode: WEST_VOICE_WAIT_STOP |
| 7 | 55 Water Gun | opcode: WEST_ADD_PARTICLE_PTAT, opcode: WEST_ADD_PARTICLE_SEP, setting: Anchor = Attacker |
| 8 | 16 Gust | opcode: WEST_CAMERA_CHG, setting: Camera = Defender |
| 9 | 57 Surf | opcode: WEST_CATS_ACT_ADD_EZ, routine: WE_057, routine: WE_T02 |
| 10 | 59 Blizzard | opcode: WEST_SE_STOP |
| 11 | 69 Seismic Toss | opcode: WEST_HAIKEI_PARA_CHG, routine: BG_SHAKE |
| 12 | 224 Megahorn | opcode: WEST_FLASH, routine: WE_224AT, routine: WE_224DF, screen: a flash |
| 13 | 225 DragonBreath | opcode: WEST_KEY_WAIT, setting: Position = Dragon breath, setting: Position = Ring start |
| 14 | 311 Weather Ball | opcode: WEST_TENKI_JP, routine: SSP_POKE_SCALE_UPDOWN |
| 15 | 475 | opcode: WEST_SE, routine: TEST_1, routine: TEST_2, routine: TEST_3 and 1 more |
| 16 | 192 Zap Cannon | opcode: WEST_HAIKEI_CHG_EX |
| 17 | 226 Baton Pass | opcode: WEST_BATONTATTI_JP, routine: DISP_MOVE, setting: Position = Baton pass |
| 18 | 145 Bubble | setting: Camera = Move 145, setting: Direction = Bubble, setting: Direction = Contest bubble, setting: Position = Bubble and 1 more |
| 19 | 464 Dark Void | routine: POKE_OAM_VIEW, routine: WE_T10, setting: Position = Defender-side + offset, setting: Priority = Behind |
| 20 | 50 Disable | routine: CAP_POKE_SCALE_UPDOWN, routine: PALCOL_CHANGE, routine: WE_OAM_PAL_FADE |
| 21 | 61 BubbleBeam | setting: Direction = Sideways (defender), setting: Field = Converge to point, setting: Position = End (target) |
| 22 | 245 ExtremeSpeed | routine: WE_100, routine: WE_T03, routine: WE_T04 |
| 23 | 18 Whirlwind | routine: WE_DISP_DEF, routine: WE_DISP_OUT |
| 24 | 56 Hydro Pump | setting: Direction = Toward target (legacy), setting: Position = Laser-2 start |
| 25 | 60 Psybeam | setting: Direction = Arc 3, setting: Position = Laser-3 start |
| 26 | 70 Strength | routine: WE_066, routine: WE_070 |
| 27 | 95 Hypnosis | setting: Direction = Arc 095, setting: Position = Laser-095 start |
| 28 | 101 Night Shade | routine: WE_101AT, routine: WE_101DF |
| 29 | 131 Spike Cannon | setting: Field = Magnet (pull to point), setting: Priority = By depth |
| 30 | 161 Tri Attack | setting: Direction = Arc 161, setting: Position = Laser-161 start |
| 31 | 194 Destiny Bond | setting: Direction = Move 194, setting: Position = Move 194 |
| 32 | 217 Present | routine: EMIT_PARABOLIC, routine: ST_EFF_RECOVER |
| 33 | 246 AncientPower | setting: Direction = Custom, setting: Field = Gravity |
| 34 | 252 Fake Out | routine: WE_252_BACK, routine: WE_252_POKE |
| 35 | 304 Hyper Voice | setting: Direction = Arc 304, setting: Position = Laser-304 start |
| 36 | 308 Hydro Cannon | setting: Direction = Arc 308, setting: Position = Laser-308 start |
| 37 | 320 GrassWhistle | setting: Direction = Arc 320, setting: Position = Laser-320 start |
| 38 | 406 Dragon Pulse | setting: Direction = Arc 406, setting: Position = Laser-406 start |
| 39 | 0 - | routine: WSP_166 |
| 40 | 19 Fly | routine: EMIT_SIMPLE_UD |
| 41 | 27 Rolling Kick | routine: WE_098 |
| 42 | 35 Wrap | routine: WE_KAITEN |
| 43 | 64 Peck | routine: POKEROTA_00 |
| 44 | 65 Drill Peck | routine: WE_065 |
| 45 | 74 Growth | routine: WE_074 |
| 46 | 89 Earthquake | routine: WE_089 |
| 47 | 91 Dig | routine: RECT_VIEW |
| 48 | 93 Confusion | routine: WE_093 |
| 49 | 96 Meditate | routine: WE_096 |
| 50 | 102 Mimic | routine: WE_102 |
| 51 | 104 Double Team | routine: WE_104 |
| 52 | 107 Minimize | routine: WE_107 |
| 53 | 109 Confuse Ray | setting: Camera = Spin |
| 54 | 144 Transform | routine: MOSAIC |
| 55 | 148 Flash | routine: WE_148 |
| 56 | 150 Splash | routine: WE_150 |
| 57 | 165 Struggle | routine: WE_175 / SHAKE |
| 58 | 171 Nightmare | routine: WE_171 |
| 59 | 180 Spite | routine: WE_180 |
| 60 | 185 Faint Attack | routine: WE_185 |
| 61 | 204 Charm | routine: WE_204 |
| 62 | 207 Swagger | routine: WE_207_MAIN |
| 63 | 216 Return | routine: WE_216 |
| 64 | 222 Magnitude | routine: WE_222 |
| 65 | 230 Sweet Scent | setting: Position = Custom point |
| 66 | 233 Vital Throw | routine: WE_233 |
| 67 | 255 Spit Up | routine: WE_T06 |
| 68 | 262 Memento | routine: WE_262 |
| 69 | 276 Superpower | routine: WE_T08 |
| 70 | 289 Snatch | routine: WSP_289 |
| 71 | 293 Camouflage | routine: WE_293 |
| 72 | 307 Blast Burn | routine: EMIT_ROTATION |
| 73 | 322 Cosmic Power | setting: Position = Start + offset |
| 74 | 325 Shadow Punch | routine: WE_325 |
| 75 | 326 Extrasensory | routine: WE_326 |
| 76 | 330 Muddy Water | routine: WE_T22 |
| 77 | 339 Bulk Up | routine: WE_339 |

## What these moves do not cover

Every mechanism the sweep can see is covered, so what is listed here is what the sweep cannot see or the recordings cannot reach.

- A move has to actually happen in a staged battle to be recorded. Whirlwind has nothing to force out and Baton Pass has nobody to pass to when the other side holds one Pokemon, so both need a second one on the other side.
- Moves are counted by what their script asks for. A routine that behaves differently depending on the Pokemon, the damage or the weather is counted once, so covering it proves the routine runs, not that it runs right in every case.
- The second half of a move that has two animations is reached only by the turn check. Five of the chosen moves have one; the other moves with a turn check in the two games are not in this set.
- Only these two games are swept. Diamond and Pearl share the format but are not read here.

## Every mechanism, and how many moves use it

| mechanism | HeartGold | Platinum |
|---|---:|---:|
| draws with: a Pokemon background | 5 | 5 |
| draws with: a background swap | 55 | 54 |
| draws with: a replaced Pokemon graphic | 3 | 3 |
| draws with: a status overlay | 5 | 5 |
| draws with: cell actors | 31 | 32 |
| draws with: dropped sprite copies | 440 | 440 |
| draws with: motion only | 65 | 65 |
| draws with: particles | 426 | 426 |
| opcode: WEST_ADD_PARTICLE | 420 | 420 |
| opcode: WEST_ADD_PARTICLE_EMIT_SET | 24 | 24 |
| opcode: WEST_ADD_PARTICLE_PTAT | 5 | 5 |
| opcode: WEST_ADD_PARTICLE_SEP | 5 | 5 |
| opcode: WEST_BATONTATTI_JP | 1 | - |
| opcode: WEST_CAMERA_CHG | 4 | 4 |
| opcode: WEST_CATS_ACT_ADD | 28 | 29 |
| opcode: WEST_CATS_ACT_ADD_EZ | 2 | 2 |
| opcode: WEST_CATS_CAHR_RES_LOAD | 30 | 31 |
| opcode: WEST_CATS_CELLANM_RES_LOAD | 30 | 31 |
| opcode: WEST_CATS_CELL_RES_LOAD | 30 | 31 |
| opcode: WEST_CATS_PLTT_RES_LOAD | 30 | 31 |
| opcode: WEST_CATS_RES_FREE | 30 | 30 |
| opcode: WEST_CATS_RES_INIT | 30 | 31 |
| opcode: WEST_CONTEST_JP | 32 | 32 |
| opcode: WEST_END_CALL | 1 | 1 |
| opcode: WEST_EXIT_PARTICLE | 415 | 415 |
| opcode: WEST_EX_DATA | 164 | 164 |
| opcode: WEST_FLASH | 2 | - |
| opcode: WEST_FUNC_CALL | 491 | 491 |
| opcode: WEST_HAIKEI_CHG | 53 | 54 |
| opcode: WEST_HAIKEI_CHG_EX | 2 | - |
| opcode: WEST_HAIKEI_CHG_WAIT | 55 | 54 |
| opcode: WEST_HAIKEI_HALF_WAIT | 4 | 3 |
| opcode: WEST_HAIKEI_PARA_CHG | 1 | 1 |
| opcode: WEST_HAIKEI_RECOVER | 55 | 54 |
| opcode: WEST_HENSIN_ON | 3 | 3 |
| opcode: WEST_HENSIN_ON_RC | 1 | 1 |
| opcode: WEST_KEY_WAIT | 1 | 1 |
| opcode: WEST_LOAD_PARTICLE | 425 | 425 |
| opcode: WEST_LOOP | 58 | 58 |
| opcode: WEST_LOOP_LABEL | 58 | 58 |
| opcode: WEST_POKEBG_DROP | 5 | 5 |
| opcode: WEST_POKEBG_DROP_RESET | 5 | 5 |
| opcode: WEST_POKEOAM_DROP | 440 | 440 |
| opcode: WEST_POKEOAM_DROP_RESET | 440 | 440 |
| opcode: WEST_POKEOAM_RES_FREE | 440 | 440 |
| opcode: WEST_POKEOAM_RES_INIT | 440 | 440 |
| opcode: WEST_POKEOAM_RES_LOAD | 440 | 440 |
| opcode: WEST_POKE_OAM_ENABLE | 6 | 6 |
| opcode: WEST_PTAT_JP | 11 | 11 |
| opcode: WEST_PT_DROP | 21 | 21 |
| opcode: WEST_PT_DROP_RESET | 21 | 21 |
| opcode: WEST_SE | 26 | 26 |
| opcode: WEST_SEPAN_FLOW | 78 | 78 |
| opcode: WEST_SEPLAY_PAN | 347 | 347 |
| opcode: WEST_SEQEND | 501 | 501 |
| opcode: WEST_SEQ_CALL | 1 | 1 |
| opcode: WEST_SE_REPEAT | 157 | 156 |
| opcode: WEST_SE_STOP | 10 | 10 |
| opcode: WEST_SE_WAITPLAY | 99 | 99 |
| opcode: WEST_SIDE_JP | 36 | 36 |
| opcode: WEST_TENKI_JP | 1 | 1 |
| opcode: WEST_TURN_CHK | 22 | 22 |
| opcode: WEST_VOICE_PLAY | 5 | 5 |
| opcode: WEST_VOICE_WAIT_STOP | 5 | 5 |
| opcode: WEST_WAIT | 382 | 382 |
| opcode: WEST_WAIT_FLAG | 465 | 465 |
| opcode: WEST_WAIT_PARTICLE | 415 | 415 |
| opcode: WEST_WORK_CLEAR | 38 | 35 |
| opcode: WEST_WORK_SET | 51 | 50 |
| plays: sound | 501 | 501 |
| routine: ALL_DROP | 425 | 425 |
| routine: BG_SHAKE | 30 | 29 |
| routine: CAP_ALPHA_FADE | 2 | 2 |
| routine: CAP_POKE_SCALE_UPDOWN | 2 | 2 |
| routine: DISP_MOVE | 1 | - |
| routine: EMIT_PARABOLIC | 22 | 22 |
| routine: EMIT_ROTATION | 2 | 2 |
| routine: EMIT_SIMPLE_UD | 1 | 1 |
| routine: EMIT_STRAIGHT | 15 | 15 |
| routine: HAIKEI_PAL_FADE | 109 | 108 |
| routine: LASTER | 4 | 3 |
| routine: MOSAIC | 1 | 1 |
| routine: PALCOL_CHANGE | 5 | 5 |
| routine: POKEROTA_00 | 2 | 2 |
| routine: POKE_OAM_VIEW | 6 | 6 |
| routine: RECT_VIEW | 1 | 1 |
| routine: SSP_POKE_PAL_FADE | 153 | 153 |
| routine: SSP_POKE_SCALE_UPDOWN | 24 | 24 |
| routine: SSP_POKE_VANISH | 17 | 16 |
| routine: ST_EFF_METAL | 4 | 4 |
| routine: ST_EFF_RECOVER | 1 | 1 |
| routine: TEST_1 | 33 | 33 |
| routine: TEST_2 | 33 | 33 |
| routine: TEST_3 | 33 | 33 |
| routine: TEST_4 | 33 | 33 |
| routine: WE_057 | 2 | 2 |
| routine: WE_065 | 1 | 1 |
| routine: WE_066 | 2 | 2 |
| routine: WE_070 | 1 | 1 |
| routine: WE_074 | 2 | 2 |
| routine: WE_089 | 1 | 1 |
| routine: WE_093 | 3 | 3 |
| routine: WE_096 | 1 | 1 |
| routine: WE_098 | 4 | 4 |
| routine: WE_100 | 2 | 2 |
| routine: WE_101AT | 1 | 1 |
| routine: WE_101DF | 1 | 1 |
| routine: WE_102 | 1 | 1 |
| routine: WE_104 | 1 | 1 |
| routine: WE_107 | 1 | 1 |
| routine: WE_148 | 1 | 1 |
| routine: WE_150 | 1 | 1 |
| routine: WE_151 | 1 | 1 |
| routine: WE_171 | 1 | 1 |
| routine: WE_175 / SHAKE | 5 | 5 |
| routine: WE_180 | 1 | 1 |
| routine: WE_185 | 1 | 1 |
| routine: WE_204 | 4 | 4 |
| routine: WE_207_MAIN | 1 | 1 |
| routine: WE_216 | 1 | 1 |
| routine: WE_222 | 1 | 1 |
| routine: WE_224AT | 1 | 1 |
| routine: WE_224DF | 1 | 1 |
| routine: WE_233 | 1 | 1 |
| routine: WE_252_BACK | 1 | 1 |
| routine: WE_252_POKE | 1 | 1 |
| routine: WE_262 | 1 | 1 |
| routine: WE_293 | 1 | 1 |
| routine: WE_325 | 1 | 1 |
| routine: WE_326 | 1 | 1 |
| routine: WE_339 | 1 | 1 |
| routine: WE_DISP_DEF | 2 | 2 |
| routine: WE_DISP_OUT | 2 | 2 |
| routine: WE_KAITEN | 6 | 6 |
| routine: WE_OAM_PAL_FADE | 3 | 3 |
| routine: WE_T02 | 1 | 1 |
| routine: WE_T03 | 3 | 3 |
| routine: WE_T04 | 1 | 1 |
| routine: WE_T05 | 27 | 27 |
| routine: WE_T06 | 1 | 1 |
| routine: WE_T08 | 1 | 1 |
| routine: WE_T10 | 39 | 39 |
| routine: WE_T22 | 1 | 1 |
| routine: WSP_166 | 2 | 2 |
| routine: WSP_272 | 1 | 1 |
| routine: WSP_289 | 1 | 1 |
| routine: WT_SHAKE | 279 | 279 |
| screen: a flash | 2 | - |
| setting: Anchor = Attacker | 36 | 36 |
| setting: Anchor = Defender | 131 | 131 |
| setting: Camera = Defender | 1 | 1 |
| setting: Camera = Move 145 | 4 | 4 |
| setting: Camera = Spin | 3 | 3 |
| setting: Direction = Arc 095 | 1 | 1 |
| setting: Direction = Arc 161 | 1 | 1 |
| setting: Direction = Arc 3 | 1 | 1 |
| setting: Direction = Arc 304 | 1 | 1 |
| setting: Direction = Arc 308 | 1 | 1 |
| setting: Direction = Arc 320 | 1 | 1 |
| setting: Direction = Arc 406 | 1 | 1 |
| setting: Direction = Bubble | 1 | 1 |
| setting: Direction = Contest bubble | 3 | 3 |
| setting: Direction = Custom | 1 | 1 |
| setting: Direction = Move 194 | 1 | 1 |
| setting: Direction = Sideways (defender) | 6 | 6 |
| setting: Direction = Toward target | 57 | 57 |
| setting: Direction = Toward target (legacy) | 1 | 1 |
| setting: Field = Converge to point | 4 | 4 |
| setting: Field = Gravity | 1 | 1 |
| setting: Field = Magnet (pull to point) | 9 | 9 |
| setting: Position = Baton pass | 1 | 1 |
| setting: Position = Bubble | 1 | 1 |
| setting: Position = Contest bubble | 3 | 3 |
| setting: Position = Custom point | 1 | 1 |
| setting: Position = Defender-side + offset | 1 | 1 |
| setting: Position = Dragon breath | 1 | 1 |
| setting: Position = End (target) | 73 | 73 |
| setting: Position = End + offset | 25 | 25 |
| setting: Position = Laser start | 42 | 42 |
| setting: Position = Laser-095 start | 1 | 1 |
| setting: Position = Laser-161 start | 1 | 1 |
| setting: Position = Laser-2 start | 1 | 1 |
| setting: Position = Laser-3 start | 1 | 1 |
| setting: Position = Laser-304 start | 1 | 1 |
| setting: Position = Laser-308 start | 1 | 1 |
| setting: Position = Laser-320 start | 1 | 1 |
| setting: Position = Laser-406 start | 1 | 1 |
| setting: Position = Move 194 | 1 | 1 |
| setting: Position = Ring start | 2 | 2 |
| setting: Position = Start (attacker) | 33 | 33 |
| setting: Position = Start + offset | 1 | 1 |
| setting: Priority = Behind | 7 | 7 |
| setting: Priority = By depth | 6 | 6 |
| setting: Priority = In front | 26 | 26 |
| structure: a branch | 76 | 76 |
| structure: a loop | 58 | 58 |
| structure: a subroutine call | 1 | 1 |
