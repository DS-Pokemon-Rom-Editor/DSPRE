# The seven 3D files, and how a viewer binds them together

Written before changing DSPRE's model browser, from two references: `turtleisaac/Nds4j`, which is the
format library, and `turtleisaac/NitroViewer`, the app built on it. The app is where the decisions about
what to show live; the library is where the byte layouts live. Every claim below carries the file and
line it came from. Anything not verified says so.

Fetch either with `gh api repos/<owner>/<repo>/contents/<path> --jq .content | base64 -d`.

## What each file is

A model on its own is a shape. Everything else is either the pictures painted on it or a way of changing
it over time, and each kind changes a different thing.

**NSBMD, magic `BMD0`.** The shape: nodes, materials and display lists. Can carry its own textures in an
embedded `TEX0`.

**NSBTX, magic `BTX0`.** A set of pictures with their palettes, to paint onto a model that has none of
its own. Which set belongs to which model is not recorded in either file, which is why a viewer has to
offer the choice rather than guess silently (NitroViewer `web/src/components/ModelViewer.tsx:162-188`
keeps the texture set as its own piece of state beside the model).

**NSBCA, magic `BCA0`.** Joint movement: the model's bones over time. This is the one that changes the
shape itself, and it is the only animation NitroViewer bakes into the exported model rather than
applying at draw time (`web/src/transport/types.ts:449`, "nsbca = null -> static model, otherwise the
NSBCA's skeletal animations are baked in").

**NSBTP, magic `BTP0`.** Which picture a material shows on which frame, like a flickering sign. A `PAT0`
block holds a dictionary of animations; each has a header of `char[4] tag, u16 numFrames, u8 numTextures,
u8 numPalettes, u16 ofsTexNames, u16 ofsPltNames` followed by a material dictionary of eight-byte
records, and each material carries keyframes of "from this frame, show this texture and this palette"
(Nds4j `TexturePatternAnimationSet.java:57-59, 148-187, 304-305`).

**NSBVA, magic `BVA0`.** Which parts of a model are showing on which frame. A `VIS0` block holds a
dictionary of animations; each is a twelve-byte header of `char[4] tag, u16 numFrames, u16 numNodes,
u16 unused, 2 pad` followed by a bit stream, one bit per frame and node, frame-major, drawn from a
32-bit word lowest bit first and refilled every 32 bits (Nds4j `VisibilityAnimationSet.java:159-199`).
Their code carries a warning worth copying: refill only while bits remain, because a stream padded to a
whole word otherwise reads one word past the end when that animation is the last thing in the file
(`VisibilityAnimationSet.java:189-192`).

**NSBMA, magic `BMA0`.** How a material is coloured over time. A `MAT0` block holds a dictionary of
animations; each has `char[4] tag, u16 numFrames, u16 flags` then a material dictionary of twenty-byte
records, being five `u32` channels: diffuse, ambient, specular, emission and alpha. Each channel is
either a constant held inline or a per-frame array of `u16` at an offset from the animation's start
(Nds4j `MaterialColorAnimationSet.java:44, 141-161, 252-253`).

**NSBTA, magic `BTA0`.** A picture sliding, turning or stretching across a material, like flowing water.
An `SRT0` block holds a dictionary of animations; each has `char[4] tag, u16 numFrames, u8, u8` then a
material dictionary of forty-byte records, being ten `u32` parameters, a pair per channel. The five
channels are scale S, scale T, rotation, translate S and translate T. Values are fixed point over 4096,
and rotation is stored as a sine and cosine pair rather than an angle (Nds4j
`TextureSrtAnimationSet.java:57-59, 129-142, 247-262, 274-276`).

## How they are meant to be bound together

Two different mechanisms, and the split matters for how DSPRE should implement them.

Joint movement is **baked into the model**. NitroViewer exports the model and the chosen NSBCA together
to glTF and hands the result to the scene (`ModelViewer.tsx:323`).

Everything else is a **track applied per frame** over the model already on screen. Their own comment says
so plainly: "NSBMA/NSBVA/NSBTP have no glTF path, so they're applied here per frame"
(`ModelViewer.tsx:33`). Each is fetched as a small plain structure and stepped through by the viewer's
own clock:

- material colour is `{ frameCount, materials: { name, diffuse[], alpha[] } }` (`types.ts:101`)
- visibility is `{ frameCount, nodeCount, visible[node][frame] }` (`types.ts:105`)
- texture pattern is `{ frameCount, materials: { name, frames[] } }` (`types.ts:110`)

So a material or node is matched **by name**, not by index. That is the piece DSPRE needs before any of
these can be applied to a loaded model.

## Which animation belongs to which model

NitroViewer answers this name-first and falls back to position, in `web/src/state/pairing.ts`.

A name is first reduced to a base: lower-cased, a leading game prefix of `pl_`, `dp_`, `hg_`, `ss_`,
`pt_`, `d_` or `p_` removed, and everything from the first underscore onwards dropped, so `manene_aruku`
and `pl_manene` both reduce to `manene` (`pairing.ts:49-54`). An NSBCA belongs to a model when any of its
clip names reduces to the same base as the model's name (`pairing.ts:62-66`).

The chosen animation is then the lowest-indexed candidate that belongs to the model, and only if none
does, position decides (`pairing.ts:73-87`). Position means the first candidate at or after the model's
own index, falling back to the last one before it, because a DS archive stores a model's animations
right after it (`pairing.ts:40-45`).

The architecture around all of this is stated as manifest-first with a heuristic fallback: a declared
game and archive gets exact answers, anything unlisted returns null and the caller drops back to the
guesswork, and the guess is never dressed up as a fact (`web/src/state/grouping.ts:1-5`).

## Where DSPRE stands against this

Read from `DSPRE.Avalonia/Avalonia/Data/ModelAssets.cs` as it is today.

`Identify` recognises all seven magics (`ModelAssets.cs:77-90`), but only joint movement is ever parsed:
`AnimationFor` returns a `JointAnimation` and nothing reads `BTP0`, `BVA0`, `BMA0` or `BTA0`
(`ModelAssets.cs:469-496`). So four of the seven kinds can be named but not shown.

Pairing is cruder than the reference in two specific ways. Names are compared by counting a shared
leading run of at least four characters and requiring one to start with the other
(`ModelAssets.cs:430-437`), with no game-prefix strip and no clip-suffix truncation, so `pl_manene`
against `manene_aruku` scores nothing where NitroViewer matches them. And the positional fallback looks
only at the three entries after the model (`ModelAssets.cs:484-491`) rather than the first candidate
anywhere at or after it, with no fallback to the last one before.

One thing DSPRE does that the reference does not: for buildings it reads the game's own animation table
through `BuildingAnimationSet.InfoFor` (`ModelAssets.cs:442-455`), which is a real manifest rather than a
guess. That is the right shape and should stay; it is the unlisted archives that need the reference's
fallback rules.

## A note on NSBTA

NitroViewer does not handle NSBTA at all. Its model viewer offers pickers for NSBCA, NSBMA, NSBVA and
NSBTP and no other (`ModelViewer.tsx:162-188`), and searching every one of the ninety-three files in the
repository for `NSBTA` or `BTA0` returns nothing; the Java core names NSBMD, NSBTX, NSBCA, NSBMA, NSBTP
and NSBVA and never NSBTA (`nitroviewer-core/src/main/java/com/nitroviewer/core/NitroViewerService.java`,
lines 168-334).

Nds4j does support it, in `TextureSrtAnimationSet.java`. So the format is readable and the gap is the
app's, not the library's. That makes NSBTA the one kind where DSPRE has no working reference to copy the
display decisions from, only the byte layout.
