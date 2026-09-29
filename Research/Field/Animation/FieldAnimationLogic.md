[Research](../../ResearchNotes.md) / [Field Research](../FieldResearch.md) / Field Animation

# What makes things move on a field map

Source: the [pokeheartgold](https://github.com/pret/pokeheartgold), [pokeplatinum](https://github.com/pret/pokeplatinum) and [pokediamond](https://github.com/pret/pokediamond) decomps, cited by file and function. This was structured into a document with AI.

Everything the games animate on an overworld map, and what DSPRE's animated preview does about each. The page follows HeartGold and SoulSilver, whose field has the most moving parts; where Platinum differs it says so. Many of HeartGold's field functions are still unnamed in pokeheartgold and are cited by their addresses.

The field runs at 30 frames a second: the main loop in `src/main.c` waits for two vertical blanks each frame. A normal walking step takes eight frames (`MovementAction_WalkNormalNorth_Step0` and its siblings in pokeplatinum, which move two units a frame for eight frames), so walking covers 3.75 tiles a second.

## What runs every frame

The field map's per-frame function, `ov01_021E5FC0` in pokeheartgold `src/field/fieldmap.c`, runs once a frame from `FieldMap_Main`. It calls nine things:

| Call | What it does |
|---|---|
| `FieldSystem_StartBugContestTimer` | the clock events (the name is a misnomer) |
| `AreaLightManager_UpdateActiveTemplate` | the area lighting |
| `ov01_022047DC` | the time-of-day animation swap |
| `ov01_021EAD8C` | a camera controller |
| `Signpost_DoCurrentCommand` | signposts and notice boards |
| `FieldTextureManager_Free` | the map texture frame swaps (the name is a misnomer) |
| `ov01_02204350` | the building and terrain animations |
| `MapLoadManager_Tick` | map streaming |
| `ov01_021E6220` | the draw |

Platinum's per-frame function (`ov5_021D134C` in `src/overlay005/fieldmap.c`) has seven of these: no time-of-day swap and no camera controller.

**Every 3D animation on a field model** is attached through `MapPropAnimation_AddToRenderObj` (pokeheartgold `src/field/overlay_01_02204004.c`), called from six places in HeartGold: four in the building animator (`map_prop_animation.c`), one for the terrain animation and one for the time-of-day swap. Two animation pools are created, one for buildings (`MapPropAnimationManager_Init`) and one for the terrain, and both are advanced by the one call per frame above (`MapPropAnimationManager_AdvanceAnimations` in Platinum).

## Scenery that animates on its own

| What | Where it lives | What drives it | Preview |
|---|---|---|---|
| Terrain texture scrolling, HeartGold | `a/1/4/0`, NSBTA | the area's terrain animation number, looped forever | yes; this is the moving water |
| Map texture frame swaps | `a/1/3/9` in HeartGold, `fldtanime.narc` in Platinum | a list of texture names and frame timings | yes, in both games |
| Building texture scrolling | the building animation archive, NSBTA | the building's list entry | yes |
| Building texture swapping | same archive, NSBTP | same | yes: lanterns, lit windows |
| Building joint movement | same archive, NSBCA | same | yes: windmills, waterwheels |
| Building material fading | same archive, NSBMA | same | yes |

**The terrain animation, HeartGold.** An area can name one texture scroll from `a/1/4/0`, `0xFFFF` for none. `AreaDataManager_Load` passes it to `ov01_0220463C`, which loops it forever. The map loader attaches it to each loaded map model (`ov01_021F4C6C`) and to a second render object (`ov01_021F6620`), except on three maps listed in `asm/unk_02054648.s`. `ov01_02204678` is the only call that puts it on a map model and `ov01_02204688` the only one that takes it off.

The terrain animator builds its animation object itself (`ov01_022046A4`) and always installs the texture scroll handler, `NNS_G3dFuncAnmMatNsBtaDefault`, with no check of the animation's type (`overlay_01_02204004.c`). Only an NSBTA works there: a joint, visibility, pattern or material animation would be read as a texture scroll.

**The map texture frame swaps.** Map textures named in `a/1/3/9` (HeartGold) or `fldtanime.narc` (Platinum) change frame on a timer: the field copies the next frame's texels into texture memory (`TextureResourceManager_Free` in pokeplatinum `src/overlay005/texture_resource_manager.c`, which, despite its name, advances the frames).

**Buildings.** Building animations are in `a/1/0/6` in HeartGold and `bm_anime.narc` in Platinum, one list entry per building model, `MapPropAnimListFile` (24 bytes in HeartGold, `include/field/map_prop_animation.h`; 20 bytes in Platinum, `MapPropAnimeListFile`). Its first three bytes are public:

- `hasAnimations`.
- `flags`: bit 0 means the animation is not loaded with the map but loaded paused when something asks for it (`MapPropAnimation_CheckDeferredLoadingFlag`); in HeartGold the value 8 marks a time-of-day animation.
- `isBicycleSlope`: when set, the animation is loaded to play once and starts paused.

Building models get their animations when the area loads (`AreaDataManager_Load` calling `ov01_021E8F3C`).

## Scenery that waits to be set off

Most of these start through `MapPropOneShotAnimationManager_PlayAnimation` or its variant with a sound, `MapPropOneShotAnimationManager_PlayAnimationWithSoundEffect`. Some also attach or unpause animations directly: gym gimmicks in overlay 4, the legendary bird cutscene camera and the shop menu, so this is not a closed list.

| What | Where (HeartGold) | Platinum | Preview |
|---|---|---|---|
| Door, stepping onto a warp | `ov01_021E90E4`, from the warp code | `src/overlay005/ov5_021D431C.c` | yes, played once |
| Door, arriving through it | `ov01_021E9374` | same file | yes, played once |
| Door, from a script | `ov01_021E9BB8`, script command 310 | same file | no: the viewer reports scripts rather than running them |
| Escalators | two more starters in `asm/overlay_01_021E90C0.s`; which one is stepping on and which off is not named publicly | same file | no |
| A white fade on changing map | a further starter in the same file; its role is not named publicly | | no |
| Lift | `ScrCmd_ElevatorAnim` | `src/overlay006/elevator_animation.c` | no, script-driven |
| Pokémon Center healing machine | `ScrCmd_PokeCenAnim` | `src/overlay006/healing_machine_animation/pokecenter.c` | no, script-driven |
| Hall of Fame machine | `ScrCmd_HallOfFameAnim` | `.../hall_of_fame.c` | no, script-driven |
| PC switching on and off | script commands 501 and 502 | `src/overlay006/pc_animation.c` | no, script-driven |
| A gym gimmick | a task in overlay 4 (`ov04_02254724`); which gym is not named publicly | | no |
| Everything not loaded paused | `ov01_021E8F3C`, when the area loads | `MapPropAnimationManager_LoadPropAnimations` | yes; this is the ordinary building animation |

Platinum also starts building animations from the Great Marsh tram (`great_marsh_tram.c`) and the boat cutscene.

DSPRE reads byte 4 of the HeartGold list entry as the door sound (door, automatic, glass or sliding); the decomps name that part `unk4`, so the meaning is DSPRE's reading, not a public one.

## Scenery that changes with the clock, HeartGold

`ov01_022047DC` swaps time-of-day animations once a frame. A model carries up to four and `sTimeOfDayVisualState` (`overlay_01_02204004.c`) picks one: morning the first, day the second, evening the third, night and late night the fourth. The hour table is `sTimeOfDayByHour` in `src/gf_rtc.c`, the same in all three games (`TimeOfDayForHour` in pokeplatinum `src/rtc.c`, `GF_RTC_GetTimeOfDayByHour` in pokediamond): late night 00 to 03, morning 04 to 09, day 10 to 16, evening 17 to 19, night 20 to 23.

The preview handles this, with a picker that starts at the computer's clock.

The area lighting also changes through the day (`AreaLightManager_UpdateActiveTemplate` every frame). That is a light colour rather than an animation, and the preview does not tint the scene for it.

## Things that move because somebody moved

Walking and turning are map object movement actions (pokeplatinum `src/unk_020655F4.c`; HeartGold's are still assembly), and the preview does them on the tile grid, one tile per eight frames.

Everything else here is a field effect renderer, and the preview draws none of them, because they need the effect graphics, a separate archive from anything it reads. HeartGold has 23 (`FIELD_EFFECT_RENDERER_COUNT` in `include/constants/field/field_effect_renderer.h`), all registered when the field map loads (`defaultFieldEffectRenderers`, passed to `FieldEffectManager_InitRenderers` in `src/field/fieldmap.c`). The decomps name them only by number, so what each one draws is not given here. Platinum has 34 (`sFieldEffectRendererHandlers` in `src/overlay005/field_effect_renderer.c`), registered in three sets: 20 for the ordinary field, 11 for the Underground and 5 for the Distortion World.

## Not scenery at all

Of the per-frame calls, the camera controller, map streaming and the draw move nothing on the map by themselves. Weather is its own manager (`WeatherManager_*`), set from the map header's weather id; the encounter run-ins, poisoning, the warp point marker and Strength are screen-wide or battle-entry effects. The preview attempts none of these.

## Known gaps in the preview

- No field effect is drawn, which is everything in the section above bar walking and turning.
- Escalators are not handled, in either direction.
- The terrain animation's second render object is not animated separately.
- Weather is not shown, and the time of day does not tint the scene the way the area lighting does.
- Lifts, healing machines, the Hall of Fame machine, PCs, the gym gimmick and the script door command animate only when a script says so; the script viewer reports what a script would do rather than running it.
