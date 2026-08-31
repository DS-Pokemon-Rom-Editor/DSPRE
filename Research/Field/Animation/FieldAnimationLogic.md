[Research](../../ResearchNotes.md) / [Field Research](../FieldResearch.md) / Field Animation

# What makes things move on a field map

Everything the games animate on an overworld map, worked out from the leaked HeartGold source, with
what the animated preview does about each one. Nothing here is left as "not looked at": where the
preview does not handle something, the reason is written down.

Frame rate is **30 per second** throughout. `GF_RTC` aside, every timing in the field code is written
against it: a one frame wait is documented as a thirtieth of a second, and the standard walking step of
eight frames as 3.75 tiles per second.

## Why this list is complete

The list is not a reading of every file that looked relevant. It is four closed sets, each one taken
from a single point every member of it has to pass through, so anything missing from a set cannot run
at all.

**Every 3D animation on a field map.** All of them are attached by `G3dAnimeData_Add`, defined at
`g3d_anime_manager.c:514`. It has seven call sites in the whole source, in three files: five in
`field_3d_anime.c` (`:519`, `:591`, `:1161`, `:1752`, `:1770`), one in `ground_anime.c:107`, and one in
`time_anm.c:136`. Only two animation managers are ever created, `field_3d_anime.c:231` for map objects
and `ground_anime.c:43` for terrain, and both are stepped from a single call to `G3dAnime_Main` at
`fieldmap.c:901`. So 3D scenery animation is map objects and terrain, and there is no third thing.

**Everything the map steps each frame.** `FieldMap_Update` at `fieldmap.c:875-917` is the field map's
per frame function and it is called from exactly one place, `FieldMapProc_Main:563`. It calls nine
things and no others: `EVTIME_Update` (`:879`), `MainLightCont` (`:882`), `TM_ANM_Main` (`:885`),
`SMLS_CamCnt_Main` (`:887`), `BoardMain` (`:893`), `FieldAnimeMain` (`:898`), `G3dAnime_Main` (`:901`),
`DivMapLoadMain` (`:905`) and `Map3Dwrite` (`:909`).

**Every scenery animation something has to set off.** These all go through `F3DASub_StartAnimation`,
which has eight call sites across the source, listed in their own table below.

**Every field effect sprite.** These come from `DATA_FE_SubProcDataTbl` in `field_effect_data.c:22-51`,
the table the effect system dispatches from, so an effect absent from it cannot run. It holds twenty
three entries and all twenty three also appear in `DATA_FE_GroundProcRegistTbl` at `:56-85`, the list
registered on an outdoor map, so on the field the two sets are identical.

Two effect files sit outside that table and were found by searching for their own entry points instead,
so they are named separately rather than folded in. `fldeff_snowball.c` is driven from
`player_event.c:1594` when the player pushes a snowball, and `fldeff_elight.c` is reached only from
`d_kaga.c:510`, which is a debug menu, so the flashlight cone is not something a retail map shows.

Beware one name collision. `fldeff_board.c` is the field effect version of the notice board and it is
commented out of the ground register table at `field_effect_data.c:59`, marked 金銀で削除, "removed in
Gold and Silver". The notice board itself is alive and well in `board.c`, whose `BoardMain` runs every
frame from `fieldmap.c:893`. The effect was dropped, the board was not.

## Scenery that animates on its own

| What | Where it lives | What drives it | Preview |
|---|---|---|---|
| Terrain texture scrolling | `ground_anime.c`, `ARC_GROUND_ANM` | The area's `RESOURCE_PARAM.ground_anm`, looped forever | Yes. This is the moving water |
| Map model | `div_map.c:938` | Texture scrolling and nothing else, see below | Yes, by the same path |
| Moving model set | `div_map.c:3533` | The same terrain animation, under the same map check | Not separately |
| Building texture scrolling | `bm_anime` archive, NSBTA | The building's list entry | Yes |
| Building texture swapping | same archive, NSBTP | same | Yes. Lanterns, lit windows |
| Building joint movement | same archive, NSBCA | same | Yes. Windmills, waterwheels |
| Building material fading | same archive, NSBMA | same | Yes |

A building's list entry (`F3D_MDL_INFO`) decides whether any of that runs at all. `CheckAddConditional`
in `field_3d_anime.c` reads the bottom bit of `Type`: set means something has to start it off, and those
are registered stopped. `Suicide` means it plays once instead of looping. `Type == 8` means it changes
with the clock.

### What a map's own model can and cannot do

The floor gets exactly one animation and it can only ever be a texture scroll. Two things fix that.

The first is that `GrndAnm_AddAnm` at `div_map.c:938` is the only call that ever attaches an animation
to a `FloorData`, with the matching removal at `:1663`. The function's only other use, at `:3533`, hands
the same terrain animation to the moving model set's render object rather than to a floor, under the
same `MPTL_IsNotGroundAnimeMap` check and with the same assertion that `anmMat` starts empty (`:3525`,
matching the floor's at `:701`). Everything else `div_map.c` animates goes to `M3DO_LoadArc3DObjData`
with `Field3DAnmPtr`, which is the map object path.

The second is what that call builds. `ground_anime.c` does not use `NNS_G3dInitAnmObj`. It carries its
own copy with the resource type switch taken out, so it always casts the resource to
`NNSG3dResTexSRTAnm` (`:185`), always calls `CSTM_NNSi_G3dAnmObjInitNsBta` (`:289`), and always sets
`funcAnm` to `NNS_G3dFuncAnmMatNsBtaDefault` (`:234`). Hand it a joint or a visibility animation and it
would read it as a texture scroll and produce nonsense, so no joint or visibility animation can be on a
map's own model. It cannot take NSBMA material fading either, which is narrower than just ruling out
joints.

## Scenery that waits to be set off

Every one of these, and only these, calls `F3DASub_StartAnimation`.

| What | Where | What sets it off | Preview |
|---|---|---|---|
| Door, walking in | `field_3d_anime_ev.c:172` | Stepping onto the warp | Yes, played once |
| Door, walking out | `field_3d_anime_ev.c:365` | Arriving through it | Yes, played once |
| Door, from a script | `field_3d_anime_ev.c:1008` | The door script command | No. The viewer reports scripts rather than running them |
| Escalator, stepping off | `field_3d_anime_ev.c:716` | Reaching the end | No, not handled at all |
| Escalator, stepping on | `field_3d_anime_ev.c:851` | Stepping onto it | No, not handled at all |
| Map jump white fade | `field_3d_anime_ev.c:1235` | Changing map | No, screen effect rather than scenery |
| Lift | `elevator_anm.c:117` | A script | No, script-driven |
| Pokémon Centre healing | `pc_recover_anm.c:211`, `:213` | A script | No, script-driven |
| Hall of Fame ball | `dendou_ball_anm.c:191` | A script | No, script-driven |
| PC | `paso_anm.c:71`, `:86` | A script | No, script-driven |
| Bugsy's gym scenery | `gym_insect.c:742` | The gym's own task | No, one room only |
| Unconditional start | `field_3d_anime.c:1404` | The building list itself, for anything not registered stopped | Yes, this is the ordinary building animation |

`Door` in the building's list entry picks the sound: door, automatic, glass, sliding.

## Scenery that changes with the clock

`time_anm.c`, stepped by `TM_ANM_Main` from `fieldmap.c:885`, and binding through `G3dAnimeData_Add` at
`time_anm.c:136`. A model carries up to four animations and the current part of the day picks one,
through `TimeZoneAnmIdxTbl`: morning takes the first, day the second, evening the third, and both night
and the small hours the fourth. The hours come from `GF_RTC_ConvertHourToTimeZone`: the small hours
until 04:00, morning until 10:00, day until 17:00, evening until 20:00, night after that.

The preview handles this, with a picker that starts at the computer's clock.

The map's lighting also changes through the day, in `field_light.c`, stepped by `MainLightCont` from
`fieldmap.c:882`. That is a light colour rather than an animation, and the preview does not tint the
scene for it.

## Things that move because somebody moved

Walking and turning are in `fieldobj_move*.c` and the preview does them, on the tile grid, one tile per
eight frames. Everything else in this section is a field effect sprite and the preview draws none of
them, because they need the effect graphics, which are a separate archive from anything it reads. The
whole registered set is below so that the gap is a known size rather than an open one.

| Effect | File | What it is |
|---|---|---|
| `FE_FLD_SHADOW` | `fldeff_shadow.c` | The shadow under a person |
| `FE_FLD_REFLECT` | `fldeff_reflect.c` | Reflections in water |
| `FE_FLD_FOOTMARK` | `fldeff_footmark.c` | Footprints left in sand |
| `FE_FLD_ARROW` | `fldeff_arrow.c` | The arrow marking the way out |
| `FE_FLD_NAMIPOKE` | `fldeff_namipoke.c` | The Pokémon you surf on |
| `FE_FLD_ROCKRIDE` | `fldeff_rockride.c` | The Pokémon you climb walls with |
| `FE_FLD_RIPPLE` | `fldeff_ripple.c` | Ripples in water |
| `FE_FLD_NRIPPLE` | `fldeff_nripple.c` | Ripples in marshland |
| `FE_FLD_GRASS` | `fldeff_grass.c` | Grass rustling as you step in it |
| `FE_FLD_GYOE` | `fldeff_gyoe.c` | The surprise mark over a trainer |
| `FE_FLD_SPLASH` | `fldeff_splash.c` | Water thrown up |
| `FE_FLD_KEMURI` | `fldeff_kemuri.c` | Dust kicked up |
| `FE_FLD_LGRASS` | `fldeff_lgrass.c` | Tall grass |
| `FE_FLD_NGRASS` | `fldeff_ngrass.c` | Marsh grass |
| `FE_FLD_HIDE` | `fldeff_hide.c` | Someone lying in wait |
| `FE_FLD_HKEMURI` | `fldeff_hkemuri.c` | The dust they throw up coming out |
| `FE_FLD_SEED_EFF` | `fldeff_seed.c` | Berry tree effects |
| `FE_FLD_MBIO` | `fldeff_mb_io.c` | A Pokémon going into and out of its ball |
| `FE_FLD_P_BALLON` | `fldeff_pokeballon.c` | A Pokémon let out on the field |
| `FE_FLD_FLASH` | `fldeff_flash.c` | Flash lighting a cave |
| `FE_FLD_FLDROBJ` | `fldeff_fldrobj.c` | The generic 3D object an effect draws with |
| `FE_UG_REDFRAME` | `fldeff_redframe.c` | The red frame, Underground only |
| `FE_FLD_FCHG` | `fldeff_fchg.c` | A Pokémon changing form |
| not registered | `fldeff_snowball.c` | The snowball the player pushes, from `player_event.c:1594` |
| not registered | `fldeff_elight.c` | A flashlight cone, reachable only from the debug menu |
| removed | `fldeff_board.c` | The effect version of the notice board, commented out at `field_effect_data.c:59` |

## Not scenery at all

The remaining per frame entries from `FieldMap_Update` are the transfer animation (`FieldAnimeMain`,
`field_anime.c`), the seamless camera (`SMLS_CamCnt_Main`), map streaming (`DivMapLoadMain`) and the
draw itself (`Map3Dwrite`), none of which move anything on the map by themselves.

Outside that loop sit weather (`weather_sys.c`, `mapdata_weather.c`), the encounter run ins
(`encount_effect.c` and its seven companions for gyms, legendaries, the dancers, Rocket, trainers and
wild battles), poisoning (`poison_effect.c`), the warp point marker (`effect_warppoint.c`) and Strength
(`field_kairiki_eff.c`). These are screen wide or battle entry effects and the preview does not attempt
them. Weather is the one that would be visible on a static map, and it is driven by the header's weather
id rather than by any animation archive.

## Known gaps

- No field effect sprite is drawn, which is the whole table above bar walking and turning.
- Escalators are not handled, in either direction.
- The moving model set is not animated separately, though the games give it the same terrain animation
  the floor gets.
- Weather is not shown, and the time of day does not tint the scene the way `field_light.c` does.
- Lifts, healing machines, the Hall of Fame ball, PCs, Bugsy's gym and the script door command all
  animate only when a script says so. The script viewer reports what a script would do rather than
  running it, so these stay still.
