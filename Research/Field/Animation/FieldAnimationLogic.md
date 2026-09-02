[Research](../../ResearchNotes.md) / [Field Research](../FieldResearch.md) / Field Animation

# What makes things move on a field map

Everything the games animate on an overworld map, worked out from what the games themselves do, with
what the animated preview does about each one. Nothing here is left as "not looked at": where the
preview does not handle something, the reason is written down.

Frame rate is **30 per second** throughout. `GF_RTC` aside, every timing in the field code is
written against it: a one frame wait is documented as a thirtieth of a second, and the standard
walking step of eight frames as 3.75 tiles per second.

## Why this list is complete

The list is not a reading of every module that looked relevant. It is four closed sets, each one
taken from a single point every member of it has to pass through, so anything missing from a set
cannot run at all.

**Every 3D animation on a field map.** All of them are attached by `G3dAnimeData_Add`, which lives in
the 3D animation manager. It has seven call sites in the whole of the field code, in three modules:
five in the map object animator, one in the terrain animator, and one in the time of day animator.
Only two animation managers are ever created, the map object animator for map objects and the
terrain animator for terrain, and both are stepped from a single call to `G3dAnime_Main` in the
field map's own code. So 3D scenery animation is map objects and terrain, and there is no third
thing.

**Everything the map steps each frame.** `FieldMap_Update` in the field map's own code is the field
map's per frame function and it is called from exactly one place, `FieldMapProc_Main`. It calls nine
things and no others: `EVTIME_Update`, `MainLightCont`, `TM_ANM_Main`, `SMLS_CamCnt_Main`,
`BoardMain`, `FieldAnimeMain`, `G3dAnime_Main`, `DivMapLoadMain` and `Map3Dwrite`.

**Every scenery animation something has to set off.** These all go through `F3DASub_StartAnimation`,
which has eight call sites in the whole of the field code, listed in their own table below.

**Every field effect sprite.** These come from `DATA_FE_SubProcDataTbl` in the field effect table,
the table the effect system dispatches from, so an effect absent from it cannot run. It holds twenty
three entries and all twenty three also appear in `DATA_FE_GroundProcRegistTbl` at , the list
registered on an outdoor map, so on the field the two sets are identical.

Two effect files sit outside that table and were found by searching for their own entry points
instead, so they are named separately rather than folded in. the snowball effect is driven from the
player event code when the player pushes a snowball, and the flashlight effect is reached only from
a debug menu, which is a debug menu, so the flashlight cone is not something a retail map shows.

Beware one name collision. the notice board effect is the field effect version of the notice board
and it is commented out of the ground register table in the field effect table, marked 金銀で削除,
"removed in Gold and Silver". The notice board itself is alive and well in the notice board code,
whose `BoardMain` runs every frame from the field map's own code. The effect was dropped, the board
was not.

## Scenery that animates on its own

| What | Where it lives | What drives it | Preview |
|---|---|---|---|
| Terrain texture scrolling | the terrain animator, `ARC_GROUND_ANM` | The area's `RESOURCE_PARAM.ground_anm`, looped forever | Yes. This is the moving water |
| Map model | the map model loader | Texture scrolling and nothing else, see below | Yes, by the same path |
| Moving model set | the map model loader | The same terrain animation, under the same map check | Not separately |
| Building texture scrolling | `bm_anime` archive, NSBTA | The building's list entry | Yes |
| Building texture swapping | same archive, NSBTP | same | Yes. Lanterns, lit windows |
| Building joint movement | same archive, NSBCA | same | Yes. Windmills, waterwheels |
| Building material fading | same archive, NSBMA | same | Yes |

A building's list entry (`F3D_MDL_INFO`) decides whether any of that runs at all.
`CheckAddConditional` in the map object animator reads the bottom bit of `Type`: set means something
has to start it off, and those are registered stopped. `Suicide` means it plays once instead of
looping. `Type == 8` means it changes with the clock.

### What a map's own model can and cannot do

The floor gets exactly one animation and it can only ever be a texture scroll. Two things fix that.

The first is that `GrndAnm_AddAnm` in the map model loader is the only call that ever attaches an
animation to a `FloorData`, with the matching removal at . The function's only other use, at , hands
the same terrain animation to the moving model set's render object rather than to a floor, under the
same `MPTL_IsNotGroundAnimeMap` check and with the same assertion that `anmMat` starts empty (,
matching the floor's at ). Everything else the map model loader animates goes to
`M3DO_LoadArc3DObjData` with `Field3DAnmPtr`, which is the map object path.

The second is what that call builds. the terrain animator does not use `NNS_G3dInitAnmObj`. It
carries its own copy with the resource type switch taken out, so it always casts the resource to
`NNSG3dResTexSRTAnm`, always calls `CSTM_NNSi_G3dAnmObjInitNsBta`, and always sets `funcAnm` to
`NNS_G3dFuncAnmMatNsBtaDefault`. Hand it a joint or a visibility animation and it would read it as a
texture scroll and produce nonsense, so no joint or visibility animation can be on a map's own
model. It cannot take NSBMA material fading either, which is narrower than just ruling out joints.

## Scenery that waits to be set off

Every one of these, and only these, calls `F3DASub_StartAnimation`.

| What | Where | What sets it off | Preview |
|---|---|---|---|
| Door, walking in | the map object animator's event side | Stepping onto the warp | Yes, played once |
| Door, walking out | the map object animator's event side | Arriving through it | Yes, played once |
| Door, from a script | the map object animator's event side | The door script command | No. The viewer reports scripts rather than running them |
| Escalator, stepping off | the map object animator's event side | Reaching the end | No, not handled at all |
| Escalator, stepping on | the map object animator's event side | Stepping onto it | No, not handled at all |
| Map jump white fade | the map object animator's event side | Changing map | No, screen effect rather than scenery |
| Lift | the lift code | A script | No, script-driven |
| Pokémon Centre healing | the Pokemon Centre healing code,  | A script | No, script-driven |
| Hall of Fame ball | the Hall of Fame code | A script | No, script-driven |
| PC | the PC code,  | A script | No, script-driven |
| Bugsy's gym scenery | Bugsy's gym code | The gym's own task | No, one room only |
| Unconditional start | the map object animator | The building list itself, for anything not registered stopped | Yes, this is the ordinary building animation |

`Door` in the building's list entry picks the sound: door, automatic, glass, sliding.

## Scenery that changes with the clock

the time of day animator, stepped by `TM_ANM_Main` from the field map's own code, and binding
through `G3dAnimeData_Add` in the time of day animator. A model carries up to four animations and
the current part of the day picks one, through `TimeZoneAnmIdxTbl`: morning takes the first, day the
second, evening the third, and both night and the small hours the fourth. The hours come from
`GF_RTC_ConvertHourToTimeZone`: the small hours until 04:00, morning until 10:00, day until 17:00,
evening until 20:00, night after that.

The preview handles this, with a picker that starts at the computer's clock.

The map's lighting also changes through the day, in the map lighting code, stepped by
`MainLightCont` from the field map's own code. That is a light colour rather than an animation, and
the preview does not tint the scene for it.

## Things that move because somebody moved

Walking and turning are in `fieldobj_move*.c` and the preview does them, on the tile grid, one tile
per eight frames. Everything else in this section is a field effect sprite and the preview draws
none of them, because they need the effect graphics, which are a separate archive from anything it
reads. The whole registered set is below so that the gap is a known size rather than an open one.

| Effect | File | What it is |
|---|---|---|
| `FE_FLD_SHADOW` | the shadow effect | The shadow under a person |
| `FE_FLD_REFLECT` | the reflect effect | Reflections in water |
| `FE_FLD_FOOTMARK` | the footmark effect | Footprints left in sand |
| `FE_FLD_ARROW` | the arrow effect | The arrow marking the way out |
| `FE_FLD_NAMIPOKE` | the namipoke effect | The Pokémon you surf on |
| `FE_FLD_ROCKRIDE` | the rockride effect | The Pokémon you climb walls with |
| `FE_FLD_RIPPLE` | the ripple effect | Ripples in water |
| `FE_FLD_NRIPPLE` | the nripple effect | Ripples in marshland |
| `FE_FLD_GRASS` | the grass effect | Grass rustling as you step in it |
| `FE_FLD_GYOE` | the gyoe effect | The surprise mark over a trainer |
| `FE_FLD_SPLASH` | the splash effect | Water thrown up |
| `FE_FLD_KEMURI` | the kemuri effect | Dust kicked up |
| `FE_FLD_LGRASS` | the lgrass effect | Tall grass |
| `FE_FLD_NGRASS` | the ngrass effect | Marsh grass |
| `FE_FLD_HIDE` | the hide effect | Someone lying in wait |
| `FE_FLD_HKEMURI` | the hkemuri effect | The dust they throw up coming out |
| `FE_FLD_SEED_EFF` | the seed effect | Berry tree effects |
| `FE_FLD_MBIO` | the bike gate effect | A Pokémon going into and out of its ball |
| `FE_FLD_P_BALLON` | the pokeballon effect | A Pokémon let out on the field |
| `FE_FLD_FLASH` | the flash effect | Flash lighting a cave |
| `FE_FLD_FLDROBJ` | the fldrobj effect | The generic 3D object an effect draws with |
| `FE_UG_REDFRAME` | the redframe effect | The red frame, Underground only |
| `FE_FLD_FCHG` | the fchg effect | A Pokémon changing form |
| not registered | the snowball effect | The snowball the player pushes, from the player event code |
| not registered | the flashlight effect | A flashlight cone, reachable only from the debug menu |
| removed | the notice board effect | The effect version of the notice board, commented out in the field effect table |

## Not scenery at all

The remaining per frame entries from `FieldMap_Update` are the transfer animation (`FieldAnimeMain`,
the field animation code), the seamless camera (`SMLS_CamCnt_Main`), map streaming
(`DivMapLoadMain`) and the draw itself (`Map3Dwrite`), none of which move anything on the map by
themselves.

Outside that loop sit weather (the weather system, the per map weather table), the encounter run ins
(the encounter effect code and its seven companions for gyms, legendaries, the dancers, Rocket,
trainers and wild battles), poisoning (the poison effect code), the warp point marker (the warp
point effect) and Strength (the Strength effect). These are screen wide or battle entry effects and
the preview does not attempt them. Weather is the one that would be visible on a static map, and it
is driven by the header's weather id rather than by any animation archive.

## Known gaps

- No field effect sprite is drawn, which is the whole table above bar walking and turning.
- Escalators are not handled, in either direction.
- The moving model set is not animated separately, though the games give it the same terrain animation
  the floor gets.
- Weather is not shown, and the time of day does not tint the scene the way the map lighting code does.
- Lifts, healing machines, the Hall of Fame ball, PCs, Bugsy's gym and the script door command all
  animate only when a script says so. The script viewer reports what a script would do rather than
  running it, so these stay still.
