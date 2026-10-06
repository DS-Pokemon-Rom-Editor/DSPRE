# Trainer Class Metadata Patch v1.0.0: Release and Usage Notes

## Contents

- [Overview](#overview)
- [Credits and Technical Sources](#credits-and-technical-sources)
- [Presentation Videos](#presentation-videos)
- [Routing Overview](#routing-overview)
- [Supported ROMs](#supported-roms)
- [Building the Initial Metadata NARC](#building-the-initial-metadata-narc)
- [Installation and Payload Placement](#installation-and-payload-placement)
- [Data Model](#data-model)
- [Trainer-Class Capacity](#trainer-class-capacity)
- [Presentation Assets](#presentation-assets)
- [Patch Scope and Native Wild Encounters](#patch-scope-and-native-wild-encounters)
- [Style 0: Standard Dynamic Trainer Presentation](#style-0-standard-dynamic-trainer-presentation)
- [Common Name and Music Behaviour](#common-name-and-music-behaviour)
- [Styles 1-12](#styles-1-12)
- [Style 13](#style-13)
- [Licence](#licence)

## Overview

The Trainer Class Metadata patch gives each trainer class direct ownership of its gender, prize coefficient, eye-contact music, battle music, presentation style, display-name choice, and presentation assets. These values are stored in one fixed-size record per class in `/a/1/5/5`. Presentation graphics remain in `/a/1/0/9`, but their member IDs are stored as independent `u16` values so assets above member `255` can be used.

The patch also removes its own dependency on the vanilla 129-class table sizes. Additional trainer-class records can be appended to `/a/1/5/5`; the game's remaining native `u8` trainer-class carriers permit ordinary class IDs through `255` unless they are widened independently. The patch does not narrow an independently widened class value back to `u8`.

The patch defines presentation **styles** `0..13` as a user-facing configuration layer. Styles `0..12` select and configure the game's existing presentation families, while Style `13` exposes the Frontier Brain presentation to ordinary trainer battles as well as native Frontier Brain encounters. A style is not the same thing as a native **effect** or **combo**. An effect is a native presentation renderer or transition ID with a fixed contract for the number, type, and purpose of the assets it consumes. A combo is a vanilla table row that pairs one of those effects with battle music.

In the vanilla game, trainer classes and selected wild species both route through the shared combo table. The patch removes that dependency for trainers: trainer presentation and ordinary class music come directly from the class record, with explicit rules preserving doubles, link, general Frontier, and native Frontier Brain behaviour. Wild encounters retain the native combo system. The old trainer-to-combo table is repurposed to expand the adjacent species-to-combo table from 11 to 43 entries, allowing more species-specific wild presentations without changing ordinary wild encounters.

Music used by Style 0 ordinary doubles, link battles, general Frontier battles, and native Frontier Brain presentations remains governed by separate named ASM parameters. Styles 1-13 retain class-local battle music in ordinary doubles.

Ordinary Style 13 battles use a patch-owned Overlay 1 lifecycle. Native Frontier Brain battles continue to use Overlay 80. Both routes read the same class record and presentation assets, but remain separate renderers with separate lifecycle and music ownership.

The patch has undergone extensive static and runtime testing across its presentation styles, metadata routing, trainer and wild encounters, doubles behaviour, Frontier handling, class expansion, supported-ROM coverage, DSPRE workflows, and qualified multi-patch use.

## Credits and Technical Sources

This project is fundamentally built on the [pret pokeheartgold decompilation](https://github.com/pret/pokeheartgold). Its recovered source, symbols, structures, constants, and function boundaries are the primary technical source for the patch and made the work possible.

- [MrHam88](https://github.com/DevHam88): project design, research direction, implementation decisions, asset preparation, and hardware-in-the-loop/runtime validation.
- [Mixone](https://github.com/Mixone-FinallyHere): the Ghidra symbol map that was crucial for joining pokeheartgold source knowledge to live HGSS disassembly and runtime analysis.
- AI-assisted research, implementation, review, and documentation used OpenAI Codex with GPT-5.6 Sol and GPT-5.5, and Claude Sonnet 4.6. Technical direction, acceptance decisions, and runtime validation remained under human control.
- [DSPRE](https://github.com/DS-Pokemon-Rom-Editor/DSPRE): unpacking, editing, and rebuilding the working ROM projects, plus the target of the companion compatibility contribution.
- [armips](https://github.com/Kingcom/armips): patch assembly.
- [melonDS](https://github.com/melonDS-emu/melonDS) and [DeSmuME](https://github.com/TASEmulators/desmume): runtime testing and debugging.

## Presentation Videos

The `release_media` directory contains cropped top-screen recordings of the native presentation families and representative custom configurations. Styles 7–12 deliberately reuse the six native Style 0 handlers, so the corresponding Style 0 recordings also show their choreography.

| Presentation | Recording |
|---|---|
| Style 0 / Style 7: normal early | [`normal-early.mp4`](release_media/normal-early.mp4) |
| Style 0 / Style 8: normal late | [`normal-late.mp4`](release_media/normal-late.mp4) |
| Style 0 / Style 9: water early | [`water-early.mp4`](release_media/water-early.mp4) |
| Style 0 / Style 10: water late | [`water-late.mp4`](release_media/water-late.mp4) |
| Style 0 / Style 11: cave early | [`cave-early.mp4`](release_media/cave-early.mp4) |
| Style 0 / Style 12: cave late | [`cave-late.mp4`](release_media/cave-late.mp4) |
| Style 1: Gym Leader | [`gym-leader-falkner.mp4`](release_media/gym-leader-falkner.mp4) |
| Style 1: saved rival | [`rival.mp4`](release_media/rival.mp4) |
| Style 2: Elite Four | [`elite-four-karen.mp4`](release_media/elite-four-karen.mp4) |
| Style 3: Rocket Admin | [`rocket-admin-petrel.mp4`](release_media/rocket-admin-petrel.mp4) |
| Style 4: Kimono Girl | [`kimono.mp4`](release_media/kimono.mp4) |
| Style 5: Red | [`red.mp4`](release_media/red.mp4) |
| Style 6: generic Team Rocket | [`rocket-grunt.mp4`](release_media/rocket-grunt.mp4) |
| Style 13: ordinary custom presentation | [`style-13-falkner.mp4`](release_media/style-13-falkner.mp4) |
| Style 13: high-ID, cross-family configuration | [`style-13-high-id-hiker.mp4`](release_media/style-13-high-id-hiker.mp4) |
| Style 0 ordinary-doubles override | [`doubles-override.mp4`](release_media/doubles-override.mp4) |

The high-ID Hiker example uses Platinum Volkner portrait members `270..273`, Platinum Gym background members `315..317`, and Platinum symbol members `326..329`. Its strong periodic banner flash is expected source-asset behaviour: background palette member `315` contains one populated Style 13 palette frame followed by seven zero-filled frames. It demonstrates valid `u16` asset access and cross-family composition, not a recommended smooth palette cycle.

## Routing Overview

### Vanilla

```text
Trainer battle
  -> trainer class
  -> trainer-to-combo table
  -> shared combo row
  -> native effect + battle music

Wild encounter
  -> species-to-combo table, or ordinary-wild fallback
  -> shared combo row
  -> native effect + battle music

Native Frontier Brain
  -> Frontier script/application route
  -> Overlay 80 presentation and Brain music
```

### Patched

```text
Trainer battle
  -> trainer class
  -> matching /a/1/5/5 record
  -> vsStyle + class assets/name
  -> class-local battle music, unless a Style 0 doubles, link, or Frontier rule takes precedence
  -> native Style 0..12 renderer, or patch-owned ordinary Style 13 renderer

Wild encounter
  -> expanded species-to-combo table, or ordinary-wild fallback
  -> shared combo row
  -> native effect + battle music

Native Frontier Brain
  -> Frontier script/application route
  -> matching /a/1/5/5 Style 13 record and shared assets
  -> Overlay 80 renderer + global Frontier Brain presentation music
```

## Supported ROMs

- Pokemon HeartGold USA is the primary supported and fully regression-tested target.
- The exact clean reference used for development and full regression is ROM title `POKEMON HG`, game code `IPKE`, revision `0`, SHA-1 `4FCDED0E2713DC03929845DE631D0932EA2B5A37`, SHA-256 `65F02A56842B75AA92D775D56D657A56FE3FA993550B04DC20704AB82D760105`.
- This reference identifies a known-good reproducible baseline, not an exhaustive compatibility whitelist. A different whole-ROM hash may result from unrelated edits or repacking and is not, by itself, evidence of incompatibility.
- Pokemon SoulSilver USA has passed static binary, address, overlay, asset, and NARC-builder parity checks together with packed-ROM runtime smoke tests of Styles 0–6. SoulSilver support is therefore qualified on this representative scope rather than a duplicate of the complete HeartGold regression campaign.
- Non-USA HeartGold and SoulSilver ROMs are unsupported by both the ASM patch and the NARC builder; their executable layouts and native source tables require separate regional ports.
- The patch is not compatible with HG-Engine ROMs.

The default installation guard is a more specific compatibility check than the whole-ROM hash. It compares every byte in all 57 patch-owned native write spans across ARM9 and the affected overlays with the expected pre-patch instructions and data, and separately requires an empty configured payload footprint. Passing this check is strong evidence that the executable surfaces modified by the patch are laid out as expected. It is not exhaustive: it does not establish ownership of the payload region, certify every surrounding ROM system, validate custom NARC contents, or convert an otherwise untested ROM into a fully regression-qualified target.

## Building the Initial Metadata NARC

Back up the DSPRE-unpacked ROM contents before running `build_trainer_class_metadata_narc.py`. Put the builder anywhere convenient, open a terminal in that directory, and pass the path to the unpacked ROM contents. The tool deliberately makes no backup of its own and replaces the existing `files/a/1/5/5` archive in place.

```powershell
python build_trainer_class_metadata_narc.py `
  --rom-contents "PATH_TO_UNPACKED_HGSS_USA"
```

The supported input is near-vanilla HeartGold USA or SoulSilver USA specifically in its trainer-class tables, presentation recipes, and trainer/combo/effect routing; unrelated parts of the ROM may be extensively modified. The command transfers the native 129 trainer classes into the class-record layout, preserving supported in-place edits to gender, prize coefficient, eye-contact music, and combo battle music. Repointed or expanded trainer tables, changed trainer/combo/effect routing, custom a109 layouts, extra trainer classes, post-patch inputs, HG-Engine ROMs, and non-USA ROMs are outside the builder's supported-input contract. US SoulSilver uses the same required executable and table layouts and has passed direct builder parity testing. For a more extensively modified trainer system, build from a supported base and edit a155 and a109 afterwards.

## Installation and Payload Placement

Prepare the ROM's executable expansion or other reclaimed executable region before applying the ASM patch. Payload placement has two settings which must be changed together: `INJECT_FILE` identifies the executable file and `INJECT_ADDR` gives the payload's runtime RAM address. The patch supports `arm9/arm9.bin`, loaded from `0x02000000`, and `unpacked/synthOverlay/0000`, loaded from `0x023C8000`; selecting the file explicitly prevents addresses belonging to transient overlays from being mistaken for ARM9 locations. The configured address must begin an independently owned, resident, executable region large enough for the `0x2068`-byte payload. The defaults are `INJECT_FILE equ "unpacked/synthOverlay/0000"` and `INJECT_ADDR equ 0x023C8000`. For reclaimed ARM9 file offset `0xF7730`, use `INJECT_FILE equ "arm9/arm9.bin"` and `INJECT_ADDR equ 0x020F7730`.

The patch has 57 native write spans across ARM9 and eight overlays, so an unnoticed collision has a broad failure surface. It must be applied to the supported unmodified hook and literal bytes. Its default installation guard checks every byte in those spans before opening an output file, and requires the configured payload footprint to be zero on first installation. These checks detect a collision; they do not prove that the selected payload region is genuinely unowned.

`TCM_ALLOW_MODIFIED_INSTALL_TARGETS` bypasses both checks for deliberate reapplication or installation over an already modified target. Enable it only after independently checking every native hook and the complete payload footprint. A guarded reapplication is expected to fail; a bypassed reapplication of the same configuration is byte-stable.

The patch modifies ARM9, Overlays 1, 12, 80, 115, 117, 118, 119, and 120, the configured payload container, and `/a/1/5/5`. It reads presentation assets from `/a/1/0/9` but does not itself add or replace those assets.

### Application Order

1. Back up the unpacked ROM contents.
2. Run the trainer class NARC builder against the pre-patch US HeartGold or SoulSilver data. It must read the native tables before the ASM retires or repurposes them.
3. Optionally add custom `/a/1/0/9` assets and edit the generated `/a/1/5/5` records. This may be done now or at any later point in development.
4. Prepare an independently owned, resident executable payload region, then set its file and runtime start address in `INJECT_FILE` and `INJECT_ADDR`.
5. Copy `ham_hgss_trainer-class-metadata-expansion_v1.0.0.asm` to the root of the unpacked ROM contents, leave `TCM_ALLOW_MODIFIED_INSTALL_TARGETS = 0` for a first installation, and assemble that file with armips.
6. Repack the ROM and test the affected trainer, wild, doubles, link, and Frontier routes relevant to the project.

The release package contains the armips patch source, the one-shot trainer class NARC builder, these release and usage notes, the presentation recordings, the MIT Licence, and a generated `SHA256SUMS.txt` for the final packaged files. It does not contain a ROM, a built `/a/1/5/5`, a built or modified `/a/1/0/9`, copyrighted game assets, private test builders, or the project's development archive.

### Patched Executable Map

| Executable | Patch role |
|---|---|
| ARM9 | trainer/wild routing, expanded species-to-combo lookup, gender, and eye-contact music |
| Overlay 1 | native effect dispatch and the ordinary-battle Style 13 renderer |
| Overlay 12 | prize-coefficient lookup |
| Overlay 80 | native Frontier Brain recipe, music, and resource routing |
| Overlay 115 | Styles 1 and 2 |
| Overlay 117 | Styles 3 and 6 |
| Overlay 118 | Style 4 |
| Overlay 119 | Style 0 and static Styles 7–12 |
| Overlay 120 | Style 5 |
| Configured payload container | all patch-owned executable helpers and runtime storage |

This is an ownership and collision-planning summary, not a replacement for the assembler's exact preimage checks.

## Data Model

This section is the definitive data model for trainer-class metadata archive `/a/1/5/5` (NARC 155).

### Record Contract

```text
/a/1/5/5 member N == trainer class N
record size == 0x34 bytes
integer encoding == little-endian
asset IDs == /a/1/0/9 member IDs stored as independent u16 values
```

Every record has the same physical layout. A field marked as consumed by the selected style must contain a valid value. An unused field is not read by that style, so a nonzero value is harmless at runtime; the NARC builder writes unused fields as zero. Reserved byte `0x0F` is part of the format contract and must be zero.

### Style Key

| ID | Presentation |
|---:|---|
| `0` | Standard dynamic trainer presentation: terrain/time selection across normal, water, and cave variants |
| `1` | Gym Leader / Rival VS20 presentation |
| `2` | Elite Four / Champion VS8 presentation |
| `3` | Rocket admin presentation |
| `4` | Kimono Girl shutter presentation |
| `5` | Red block presentation |
| `6` | Generic Team Rocket presentation |
| `7` | Static normal/default early presentation |
| `8` | Static normal/default late presentation |
| `9` | Static water early presentation |
| `10` | Static water late presentation |
| `11` | Static cave early presentation |
| `12` | Static cave late presentation |
| `13` | Frontier Brain presentation |

### Style Applicability Matrix

`🟢` means the style consumes the field. `-` means unused and canonically zero. `0` means reserved and required to be zero.

| Offset | Type | Field / asset type | S0 | S1 | S2 | S3 | S4 | S5 | S6 | S7 | S8 | S9 | S10 | S11 | S12 | S13 |
|---:|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `0x00` | `u16` | gender | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 |
| `0x02` | `u16` | prize coefficient | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 |
| `0x04` | `u16` | main eye-contact music | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 |
| `0x06` | `u16` | alternate eye-contact music | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 |
| `0x08` | `u16` | battle music | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 |
| `0x0A` | `u16` | VS style ID | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 |
| `0x0C` | `u16` | trainer-name message ID | - | 🟢 | 🟢 | 🟢 | - | - | - | - | - | - | - | - | - | 🟢 |
| `0x0E` | `u8` | Style 1 `useSavedRivalName` | - | 🟢 | - | - | - | - | - | - | - | - | - | - | - | - |
| `0x0F` | `u8` | reserved | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| `0x10` | `u32` | Style 1 portrait position/motion | - | 🟢 | - | - | - | - | - | - | - | - | - | - | - | - |
| `0x14` | `u16` | Style 2 timing/interpolation parameter | - | - | 🟢 | - | - | - | - | - | - | - | - | - | - | - |
| `0x16` | `u16` | group 1 RLCN | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 |
| `0x18` | `u16` | group 1 RGCN | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 |
| `0x1A` | `u16` | group 1 RECN | 🟢 | - | 🟢 | - | - | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | - |
| `0x1C` | `u16` | group 1 RNAN | 🟢 | - | 🟢 | - | - | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | 🟢 | - |
| `0x1E` | `u16` | group 1 RCSN 1 | - | 🟢 | - | 🟢 | 🟢 | - | - | - | - | - | - | - | - | 🟢 |
| `0x20` | `u16` | group 1 RCSN 2 | - | - | - | 🟢 | - | - | - | - | - | - | - | - | - | - |
| `0x22` | `u16` | group 1 RCSN 3 | - | - | - | 🟢 | - | - | - | - | - | - | - | - | - | - |
| `0x24` | `u16` | group 2 RLCN | 🟢 | 🟢 | 🟢 | 🟢 | - | - | - | - | - | - | - | - | - | 🟢 |
| `0x26` | `u16` | group 2 RGCN | 🟢 | 🟢 | 🟢 | 🟢 | - | - | - | - | - | - | - | - | - | 🟢 |
| `0x28` | `u16` | group 2 RECN | 🟢 | 🟢 | 🟢 | 🟢 | - | - | - | - | - | - | - | - | - | 🟢 |
| `0x2A` | `u16` | group 2 RNAN | 🟢 | 🟢 | 🟢 | 🟢 | - | - | - | - | - | - | - | - | - | 🟢 |
| `0x2C` | `u16` | group 3 RLCN | - | 🟢 | 🟢 | - | - | - | - | - | - | - | - | - | - | 🟢 |
| `0x2E` | `u16` | group 3 RGCN | - | 🟢 | 🟢 | - | - | - | - | - | - | - | - | - | - | 🟢 |
| `0x30` | `u16` | group 3 RECN | - | 🟢 | 🟢 | - | - | - | - | - | - | - | - | - | - | 🟢 |
| `0x32` | `u16` | group 3 RNAN | - | 🟢 | 🟢 | - | - | - | - | - | - | - | - | - | - | 🟢 |

For Style 0, groups 1 and 2 are the small and large asset families. For Styles 1, 2, and 13, groups 1, 2, and 3 are respectively the background, opponent portrait, and VS symbol; Style 3 uses groups 1 and 2 for its background and opponent portrait. Other styles consume only the group shown by the dots and are not restricted to reproducing the vanilla artwork described by the style name.

### Validation Rules

- `gender` is stored as a raw `u16`. Value `1` selects female behaviour; every other value follows the male-or-multi behaviour. The builder preserves the native value, including native value `2`, and compatible editors should preserve an unchanged non-`1` value rather than silently normalising it.
- `vsStyle` must be in `0..13`.
- Style 1 `useSavedRivalName` must be `0` or `1`. When it is `1`, the saved rival name is used and `trainerNameId` is unused and canonically zero.
- Styles 1, 2, 3, and 13 require an in-range `trainerNameId` when they use a static name. Both packed and plain message formats are supported.
- Style 2's native timing/interpolation parameter is stored as `u16` but must fit `0..255` because the native state machine consumes one byte.
- Every consumed asset ID must resolve to an existing `/a/1/0/9` member with the fixed Nitro type shown by its row.
- Group 1 `RECN`, `RNAN`, and `RCSN` rows never change file type according to style. Styles that need screen data use the dedicated `RCSN` rows.

The common music fields are independent of `vsStyle`. Link battles, general Frontier battles, native Frontier Brain presentation, and Style 0 ordinary doubles can apply global routing policy instead of class-local `battleMusic`; those behavioural overrides do not change this record layout. Styles 1-13 retain class-local music in ordinary doubles.

### Worked Records

These examples show only the style-specific portion of a record. Keep the class's intended common fields at `0x00..0x08`, zero every unlisted field, and store all asset values as little-endian `u16` member IDs.

| Example | Required values |
|---|---|
| Vanilla-equivalent ordinary class | `vsStyle=0`; group 1 `0,4,6,5`; group 2 `0,7,9,8` |
| Falkner-equivalent Style 1 | `vsStyle=1`; static name ID; `useSavedRivalName=0`; motion `0x000D6000`; background `21,20,22`; portrait `63,64,65,66`; symbol `59,60,61,62` |
| Saved-rival Style 1 | `vsStyle=1`; `trainerNameId=0`; `useSavedRivalName=1`; valid motion, background, portrait, and symbol groups |
| Will-equivalent Style 2 | `vsStyle=2`; static name ID; timing/interpolation parameter `0x20`; background `47,48,49,50`; portrait `131,132,133,134`; symbol `59,60,61,62` |
| Palmer-equivalent Style 13 | `vsStyle=13`; static name ID; background `202,201,203`; portrait `186,185,187,188`; symbol `59,204,205,206` |

## Trainer-Class Capacity

The patch does not impose the vanilla 129-class count as a configuration limit. `/a/1/5/5` member index remains identical to trainer class ID. The native trainer-data field at `/a/0/5/5` member offset `+0x01`, and its BattleSetup copy, are `u8` class references; without a wider trainer-data redesign the practical ordinary-battle range is usable class IDs `1..255`, with ID `0` retaining its native no-trainer meaning. The Pokegear's independent `PhoneBookEntry.trainerClass` field at `+0x03` is also `u8`, but matters only when a class is used as a phone contact.

The patch consumes the full class value returned by native `TrainerData_GetAttr` at ARM9 `0x02073470`. Its ordinary routing, eye-contact music, prize, gender, and shared class cache do not narrow that value again or compare it with 129 or 255. Extending beyond `255` still requires an independent widening of the game's native trainer-data and related carriers, but requires no further compatibility change to this patch. The Frontier interface `GetFrontierTrainerOverworld(u8 trainerClass)` remains a separate byte-sized class boundary.

The a155 NARC count is `u16`: it can contain 65,535 members, representing class IDs `0..65534`. The trainer class NARC builder emits exactly the 129 records derived from supported native HG data and does not add trainer classes. Additional records may be appended afterwards, but they must remain contiguous because NARC member position is class identity.

A complete added HGSS trainer class also needs matching entries in the trainer-class name and description/article text archives (`730` and `731` decimal in English HGSS), plus the corresponding five-member trainer battle-graphics set. Editing and coordinated class management through DSPRE may become available in a future DSPRE release. The proposed operation copies all of these from an existing class, accepts any consistently expanded dataset count, and refuses to add or remove when the class-indexed quantities disagree rather than guessing how manually changed data should be repaired. DSPRE's existing Trainer Class Editor name field edits the normal class-name message only; edit the separate description/article message through the Text Editor when required.

These are class IDs, not `/a/1/0/9` member IDs. Presentation asset references are a separate namespace and remain independent `u16` values in class metadata. Any old byte-sized presentation recipe field is an asset-width issue that must be widened or retired, regardless of trainer-class count.

All presentation asset references used by the patch are independent `u16` a109 member IDs, including IDs above `255`.

### Native Boss-Class Friendship Trigger

HGSS has a separate trainer-class behaviour that is not presentation metadata. When a trainer battle is created, the game checks whether either opposing trainer uses one of the hardcoded classes for the 16 Gym Leaders, the Elite Four, or Champion Lance. If so, it applies friendship event `3` to every non-Egg Pokemon in the player-side parties. The vanilla event values are `+3`, `+2`, or `+1` according to the Pokemon's current friendship band.

The qualifying classes are implemented as class-ID comparisons and a compiler-generated branch table in Overlay 12, not as an editable trainer-class data table. See the [HGSS class check in pokeheartgold](https://github.com/pret/pokeheartgold/blob/0985e8718df4f25e64d6507d89c0c97c0d288981/asm/overlay_12_022378C0.s#L2908).

This behaviour is independent of `vsStyle` and every field relocated by this patch. Assigning Style `1` or `2` to a new or ordinary class changes its presentation but does not make it qualify for the friendship event. Conversely, a native qualifying class still triggers the event if its presentation style is changed. The patch deliberately leaves this native class-ID check unchanged; changing or expanding the qualifying set requires a separate code modification.

## Presentation Assets

The patch does not bundle or build `/a/1/0/9`. Each configured asset ID must refer to a compatible member already present in that archive. Add or replace custom assets with the usual NARC tooling, then place their member IDs in the corresponding a155 record fields. The release does not require or distribute Platinum assets or any other external asset set, but its independent `u16` references allow compatible imported assets, including Platinum assets, to be used.

## Patch Scope and Native Wild Encounters

The patch relocates and extends trainer-class-owned metadata. Wild encounters do not have a trainer class and retain their native transition and music ownership. Every trainer-origin dependency on the shared combo table is removed while the required behaviour of Style 0 ordinary doubles, link battles, general Frontier trainers, and Frontier Brains is reproduced directly. Styles 1-13 retain their configured presentation and class-local music in ordinary doubles. The combo mechanism is therefore exclusively wild/species-owned.

### Native Table Retirement and Reuse

Zero-valued native bytes, array members, fixed-record fields, and alignment are owned data and must not be treated as payload space.

The only repurposed native data range is `0x020FC3CA..0x020FC409`: all trainer consumers of the former 32-row trainer-to-combo table are retired, and that exact range extends the adjacent species-to-combo table. Gender `0x020FFB90..0x020FFC0F`, eye-contact music `0x020FC61C..0x020FC723`, Overlay-12 prize `0x0226C4C4..0x0226C6C7`, and Overlay-80 Frontier recipe `0x0223DB98..0x0223DBDF` are behaviourally retired but remain present byte-for-byte. Their replacement readers and all associated runtime storage live in the configured patch-owned payload. None of these preserved ranges is available for reuse.

### Native Effect Dispatch Reference

`vsStyle` is not a native effect ID. Styles 0-12 ultimately use native effect handlers; ordinary Style 13 uses the patch-owned Overlay 1 lifecycle, while a native Frontier Brain uses Overlay 80.

| Native effect | Handler / family | Meaning | Style relation |
|---:|---|---|---|
| `0..5` | Overlay 120 low dynamic handlers | ordinary dynamic variants | Style 0 only |
| `6` | `ov119_0225F37C` | normal/default early | Style 0 or static Style 7 |
| `7` | `ov119_0225F020` | normal/default late | Style 0 or static Style 8 |
| `8` | `ov119_0225FA2C` | water early | Style 0 or static Style 9 |
| `9` | `ov119_0225F670` | water late | Style 0 or static Style 10 |
| `10` | `ov119_02260258` | cave early | Style 0 or static Style 11 |
| `11` | `ov119_0225FF9C` | cave late | Style 0 or static Style 12 |
| `12..28` | Overlay 115 VS20 handlers | Gym Leaders and Rival | Style 1 |
| `29..33` | Overlay 115 VS8 handlers | Elite Four and Champion | Style 2 |
| `34..36` | Overlay 116 | legendary, Ho-Oh, and Lugia wild routes | no trainer style |
| `37..38` | Overlay 114 | general/special battle routes | global trainer policy and wild combo use |
| `39` | `ov117_0225F020` | generic Team Rocket | Style 6 |
| `40..44` | Overlay 117 admin handlers | Petrel, Proton, Archer, Ariana, Giovanni | Style 3 |
| `45` | `ov118_0225F020` | Kimono Girl | Style 4 |
| `46` | `ov120_0225F714` | Red | Style 5 |
| none | patch-owned Overlay 1 / native Overlay 80 | Frontier Brain presentation | Style 13 |

### General Trainer Behaviour Contract

These are direct routing rules, not trainer combo assignments:

| Circumstance | Required EFFECT ID | Required music | Configuration point |
|---|---:|---|---|
| Style 0 ordinary trainer double | `38` | `SEQ_GS_VS_TRAINER` (`1117`) | named trainer-double policy constants in the patch configuration block |
| Styles 1-13 ordinary trainer double | selected direct style effect | class `battleMusic` | trainer-class record |
| Link single | `37` | `SEQ_GS_VS_TRAINER` | named link policy constants; class metadata is not consulted |
| Link double/multi | `38` | `SEQ_GS_VS_TRAINER` | named link policy constants; class metadata is not consulted |
| General Frontier single | `37` | `SEQ_GS_VS_TRAINER` | named general-Frontier policy constants |
| General Frontier double | `38` | `SEQ_GS_VS_TRAINER` | named general-Frontier policy constants |
| Frontier Brain introduction | Overlay 80 renderer | `SEQ_GS_BA_BRAIN` (`1147`, `0x047B`) | Overlay 80 Frontier script/application policy |

#### Ordinary-Double Style Ownership

The ordinary-double policy is selected by `vsStyle`, not by a hardcoded trainer-class list. Style 0 uses the global trainer-double presentation and music. Every class configured with Style 1-13 instead retains its configured presentation and class-local `battleMusic` in both 1v1 and 1v2 double battles.

For a 1v2 battle, the first opposing trainer's class owns the single presentation and music decision. The patch does not merge the two opponents' class records.

#### Where Override Music Is Defined

Edit the named configuration constants in the patch source, not the old combo rows:

| Policy | Patch configuration symbol |
|---|---|
| Style 0 ordinary trainer double | `TCM_TRAINER_DOUBLE_MUSIC` |
| Link single/double/multi | `TCM_LINK_BATTLE_MUSIC` |
| General Frontier single/double | `TCM_FRONTIER_BATTLE_MUSIC` |
| Native Frontier Brain presentation | `TCM_FRONTIER_BRAIN_PRESENTATION_MUSIC` |

The first three default to `SEQ_GS_VS_TRAINER` (`1117`, `0x045D`) but remain separate so one global behaviour can be changed without changing the others. `TCM_TRAINER_DOUBLE_MUSIC` applies only to Style 0 ordinary doubles. The Frontier Brain parameter defaults to `SEQ_GS_BA_BRAIN` (`1147`, `0x047B`) and controls the native Overlay-80 presentation globally. Styles 1-13 use each trainer-class record's `battleMusic` field at `/a/1/5/5 + 0x08` in ordinary doubles.

For vanilla comparison, `_020FC40A` in ARM9 stores the old combo rows. The music halfwords are:

| Vanilla circumstance | Combo | Music address | Native value |
|---|---:|---:|---|
| General trainer / general Frontier single | `35` | `0x020FC498` | `SEQ_GS_VS_TRAINER` |
| Link single | `36` | `0x020FC49C` | `SEQ_GS_VS_TRAINER` |
| Generic, link, or general Frontier double | `37` | `0x020FC4A0` | `SEQ_GS_VS_TRAINER` |
| Generic wild double | `38` | `0x020FC4A4` | `SEQ_GS_VS_NORAPOKE` |
| Vanilla combo-39 Brain row | `39` | `0x020FC4A8` | `SEQ_GS_BA_BRAIN` |

Those ARM9 addresses describe the vanilla game and are not trainer configuration points after applying the patch. The assembler may deduplicate equal policy values into one payload literal near the routing helper; that emitted address is an implementation detail and must not be hex-edited.

The patched router produces the native-equivalent outputs without reading rows `35..37` from trainer-origin paths. This permits those rows to be configured for species-specific encounters without changing doubles, link, or Frontier trainer behaviour.

For technical reference, pokeheartgold defines the sequence names in `include/constants/sndseq.h` and the native combo rows in `asm/unk_020517A4.s::_020FC40A`. Overlay 80 does not obtain its Brain presentation music from that table: `FrtCmd_071 @ 0x0222DFF4` directly starts `0x047B`, loaded from the 32-bit literal at runtime address `0x0222E054`. For a decompressed `ov080.bin` loaded at `0x02229EE0`, this is file offset `0x4174` (`7B 04 00 00` little-endian). Change `TCM_FRONTIER_BRAIN_PRESENTATION_MUSIC` rather than editing that address. The parameter changes global native Frontier Brain presentation music without changing the class-local Style 13 visual recipe or ordinary `battleMusic`. The separate `FrtCmd_110 @ 0x0222CD94` battle-launch path loads generic trainer music `0x045D`.

### Species-Specific Combo Lookup

Vanilla HeartGold stores three immediately contiguous tables in ARM9 read-only data. The patch repurposes the middle range without moving either boundary:

| Range | Size | Contents |
|---|---:|---|
| `0x020FC3B4..0x020FC3C9` | `0x16` | 11 species-to-combo entries |
| `0x020FC3CA..0x020FC409` | `0x40` | 32 added species-to-combo entries; formerly trainer-class mappings |
| `0x020FC40A..0x020FC4BD` | `0xB4` | 45 combo effect/music rows |

Each species entry is a packed little-endian `u16`: bits `0..9` hold the species ID and bits `10..15` hold the combo ID. The native entries are:

| Species | Combo |
|---|---:|
| Raikou | `22` |
| Entei | `23` |
| Suicune | `24` |
| Lugia | `26` |
| Ho-Oh | `25` |
| Groudon, Kyogre, Rayquaza | `27` |
| Mewtwo, Latios, Latias | `28` |

`WildPokemonGetBattleIntroAndMusicParam @ 0x02051894` performs the lookup. Its sole call reference is `0x02051790` in `BattleSetup_GetTransitionAndMusicParam`. The table pointer is stored once at literal `0x020518D4`; code loads that literal at `0x020518AC` and `0x020518BA`, then reads entries at `0x020518B0` and `0x020518BE`. Vanilla uses the immediate `11` in `cmp r2, #0x0B @ 0x020518CA`; the patch changes that same-width instruction to `cmp r2, #0x2B`. A miss still returns ordinary-wild combo `42`.

Mewtwo's native combo 28 retains the vanilla presentation and music. Changing only Mewtwo's packed row to combo 25 instead selects the native Ho-Oh transition effect and music, demonstrating that the retained species lookup continues to consume the shared combo table after trainer decoupling.

The adjacent trainer table was read only by `NPCTrainerGetBattleIntroAndMusicParam @ 0x02051868`; its sole native call reference is `0x02051748` in the common selector. The patch routes trainer origins before that selector, replaces the reader with a default-only stub, retains only the two-byte presentation latch at `0x02051870`, and repurposes `0x020FC3CA..0x020FC409` as the 32-row species-table extension. Trainer routes no longer call either combo-table consumer; wild routes retain both.

The shared combo table is consumed as two `u16` columns. Transition/effect selection is performed by `BattleStartGetTransition @ 0x020517A4`, using the base-pointer literal at `0x020517C8`. Music selection is performed by `BattleStartGetMusic @ 0x020517D0`, using the `base + 2` pointer literal at `0x020517E4`. Native species-specific encounters and vanilla trainer selection therefore draw from the same combo rows, although patched trainer routes use direct style and music fields instead.

### Extended Native Species Table

The adjacent trainer-class lookup is no longer reachable from trainer routes, so the species table extends through its former `0x40`-byte range. This raises capacity from 11 to 43 entries without moving either the species-table base or the shared combo table; the lookup bound is `0x2B`.

Unused added rows use `SPECIES_NONE` (`0`) paired with ordinary-wild combo `42`, packed as `0xA800`. A legitimate wild party cannot match species zero, and tools can recognise the row as empty. Repeating the Raikou row would also be inert while the original Raikou entry remains first because lookup is first-match-wins, but it would leave hidden Raikou mappings if that first row were later edited.

This extension preserves the native wild mechanism and provides additional species-specific mappings. Shared combo-row ownership is:

| Combo rows | Ownership/status |
|---|---|
| `22..28` | native species mappings; wild-owned and proven |
| `38` | generic double-wild override; wild-owned/reserved |
| `42` | unmatched ordinary-wild fallback and empty-row convention; wild-owned/reserved |
| `0..21`, `29..37`, `39..41`, `43..44` | no stock producer after trainer decoupling; available for species mappings only after confirming that the selected effect works in a wild encounter |

An orphaned trainer row is not automatically safe for wild use merely because it is no longer selected. Its presentation effect may still assume trainer state and must be tested in a wild encounter before use.

The extended range has been exercised with Rattata/combo 22, Zubat/combo 25, and Caterpie/combo 28. Unmatched species retain ordinary wild behaviour, and native Mewtwo retains combo 28.

DSPRE retains the 45-row combo table, displays the enlarged 43-row species table, and exposes no trainer mappings because their count is zero. DSPRE `v2.3.1.0` repairs the previously unwired species-entry Save path, including the required dirty-state handling; the integrated companion branch builds successfully, passes its 1,932 automated assertions, and has manually persisted an edited species-level combo. The companion trainer-class integration adds safe patch detection and read/write support for the relocated common fields, the complete `/a/1/5/5` schema, and coordinated trainer-class addition/removal. Its combined Stage 1 and Stage 2 contribution is being prepared separately and is not required to apply the ASM patch or use the NARC builder.

## Style 0: Standard Dynamic Trainer Presentation

### What Style 0 Is

Style 0 preserves the standard presentation used by ordinary single trainer battles. It is dynamic: the trainer class chooses Style 0, but the battle's location and time choose one of six native animation methods at runtime. The patch retains the native selector rather than replacing it with a single generic animation.

In an ordinary double battle, a class configured with Style 0 is routed to the global trainer-double presentation and music rather than this terrain/time-selected renderer. Link battles, general Frontier battles, and classes configured for Styles 1 through 13 follow their separately documented presentation rules.

### Vanilla Terrain Selection

The game derives presentation terrain from the player's standing tile when the battle setup is created. The relevant checks occur in this order:

1. ice;
2. tall or very tall grass;
3. sand;
4. snow;
5. mud;
6. cave floor;
7. surfable water;
8. the map's battle background, if no preceding tile rule matched.

Only two terrain values receive dedicated Style 0 families:

| Derived terrain | Style 0 family |
|---|---|
| `TERRAIN_WATER` (`7`) | water |
| `TERRAIN_CAVE` (`5`) | cave |
| every other supported terrain | normal/default |

This means the map's broad `mapType` is not itself the selector input. A cave map can produce the normal, water, or cave family according to its standing tile and battle-background fallback. Surfing also changes the effective battle background to ocean, while the standing tile normally supplies water terrain directly.

### Vanilla Time Selection

Each terrain family has an early and a late result. These names refer to the game's five saved RTC time families:

| Saved RTC hour | Time family | Style 0 branch |
|---|---|---|
| `04:00..09:59` | morning | early |
| `10:00..16:59` | day | early |
| `17:00..19:59` | evening | early |
| `20:00..23:59` | night | late |
| `00:00..03:59` | late night | late |

The hour comes from the RTC value cached in the save data when battle setup is created. After changing emulator time, use a clean boot or otherwise confirm that the save-cached hour has updated; loading an older emulator save state can restore its earlier cached value.

### The Cave-Background Exception

Before deriving terrain, the vanilla setup code checks the map header's battle background. `BATTLE_BG_CAVE_1`, `BATTLE_BG_CAVE_2`, and `BATTLE_BG_CAVE_3` force the setup time to `NITE`, regardless of the saved RTC hour. They force the late **time branch**, not the terrain itself:

- an ordinary cave-floor or cave-background fallback selects cave late;
- a surfable-water tile selects water late;
- an ice, grass, sand, snow, mud, or other non-water/non-cave result selects normal late.

This is why changing the clock in a conventional cave does not normally reveal the cave-early animation. Cave early requires `TERRAIN_CAVE` without one of the three cave battle backgrounds forcing night.

Burned Tower 1F is a shipped example. Its map header uses `BATTLE_BG_BUILDING_3`, but its floor around Firebreathers Richard and Ned is marked `TILE_BEHAVIOR_CAVE_FLOOR`. A Style 0 battle there therefore uses cave early from `04:00..19:59` and cave late from `20:00..03:59`.

This setup-time override is also visible to the vanilla Dusk Ball catch formula, but it is not required for ordinary cave-floor tiles. A Dusk Ball receives its `3.5x` multiplier when the setup time is night/late **or** the derived terrain is `TERRAIN_CAVE`. Consequently, a map using one of the three cave battle backgrounds qualifies through forced night even on a higher-priority non-cave tile. Burned Tower 1F and B1F both use `BATTLE_BG_BUILDING_3`, so neither is forced to night; however, their traversed cave-floor tiles derive `TERRAIN_CAVE`, and Dusk Balls receive the bonus on both floors during the day as well as at night. This relationship is confirmed behaviour, not evidence that enabling Dusk Balls was the original purpose of the force-night rule.

### Six Dynamic Methods

| Environment | Native effect | Equivalent static style | Ball assets | Choreography note |
|---|---:|---:|---|---|
| normal/default, early | `6` | `7` | small quartet | native normal-early method |
| normal/default, late | `7` | `8` | large quartet | native normal-late method |
| water, early | `8` | `9` | small quartet | native water-early method |
| water, late | `9` | `10` | large quartet | native water-late method |
| cave, early | `10` | `11` | small quartet | three small-ball sprites |
| cave, late | `11` | `12` | small quartet | one small-ball sprite |

Styles 7 through 12 expose the same six handlers as explicit static choices. A static style ignores terrain and time, allowing a particular animation to be used everywhere.

### Class-Local Ball Assets

The record model gives each trainer class two independently addressable `u16` asset quartets:

| Flat `0x34` record range | Assets | Vanilla migration default |
|---:|---|---|
| `0x16..0x1D` | small RLCN, RGCN, RECN, RNAN | `0, 4, 6, 5` |
| `0x24..0x2B` | large RLCN, RGCN, RECN, RNAN | `0, 7, 9, 8` |

Changing these quartets changes that class's ball artwork without changing the environmental method selected. To recreate the vanilla presentation, use Style 0 with both default quartets above. A custom class may instead provide compatible small and large artwork while retaining all six dynamic methods.

"Early" and "late" do not directly mean small and large. Normal late and water late use the large quartet, but cave late retains the small quartet because its native one-sprite state machine was designed for the small resource family.

RECN and RNAN shape is part of each quartet's contract. Merely supplying files of the correct type is insufficient if their cell count, tile indices, or animation structure do not support the selected handler. OBJ image palette index `0` must remain transparent, and every image index in use must have a corresponding RLCN entry.

The later water splash shown after battle handoff is selected by battle terrain, not by the pre-battle presentation style. Selecting static Style 9 or 10 does not independently request that in-battle splash.

## Common Name and Music Behaviour

Styles 1, 2, 3, and 13 read `trainerNameId` at `0x0C` as a `u16` message ID from archive member `0x2D9` (`729` decimal). Both packed and plain message-file formats are supported by all four styles. The name belongs to the trainer class, so every trainer instance sharing that class receives the same configured static name.

Style 1 alone reads `useSavedRivalName` at `0x0E`. A value of `0` displays the static `trainerNameId`; a value of `1` displays the rival name stored in the save and leaves `trainerNameId` unused. Any other value is invalid and enters the patch's deliberate fail-loud path. No other style infers rival behaviour from a class ID, combo, or name value.

The three class-local music fields are independent of `vsStyle`:

| Offset | Role |
|---:|---|
| `0x04` | main eye-contact music |
| `0x06` | alternate/Kanto eye-contact music |
| `0x08` | ordinary class-local battle music |

Eye-contact music plays before the battle presentation and retains the native main/alternate selector. An eligible ordinary trainer battle then uses the class-local `battleMusic`. The Style 0 doubles, link, general Frontier, and native Frontier Brain rules documented under General Trainer Behaviour can take precedence. The patch does not consult a legacy trainer combo to fill in a missing class-local song.

The patch neither validates nor substitutes battle-music sequence IDs. An invalid or nonexistent value, including `0xFFFF`, produces no battle music; presentation and battle continue. This follows the native sound-call behaviour and is not a special sentinel or patch-defined fallback.

## Styles 1-12

This section summarises each native presentation family at the level needed to configure and recognise it. The patch changes data ownership and asset width; it retains the native choreography for Styles 1-12.

In ordinary 1v1 and 1v2 double battles, every Style 1-12 class retains its configured presentation and class-local battle music. Style 0 alone uses the global ordinary-double policy.

### Style 1: Gym Leader or Rival

The native VS20 sequence introduces a coloured centre-line banner, stages the opponent portrait through its silhouette/reveal motion, displays the trainer name, and animates the standard VS symbol before handing off to battle. Group 1 supplies the banner `RLCN/RGCN/RCSN`, group 2 the portrait quartet, and group 3 the VS-symbol quartet. `portrait position/motion` at `0x10` controls the horizontal endpoint/motion used by the sequence.

Set `useSavedRivalName=0` for a static configured name or `1` for the name in the save. This choice is independent of the selected portrait, banner, symbol, and music. Standard, Platinum, and native Frontier portrait families are supported through the mapping adapter; supplied RECN/RNAN data must still match the sequence expected by this renderer.

### Style 2: Elite Four or Champion

The native VS8 sequence presents the opponent portrait and name over its class-coloured banner, with pale translucent horizontal bars moving vertically behind the main layer and the standard VS-symbol sequence in front. It also displays the player portrait selected by the player's chosen gender. Group 1 is a background sprite quartet, group 2 is the opponent portrait quartet, and group 3 is the VS-symbol quartet.

The field at `0x14` preserves a consumed native one-byte parameter provisionally described as an interpolation duration; its precise visible effect has not been isolated or proven. Vanilla uses `0x20` (`32`) for Will, Koga, Bruno, and Karen, and `0x09` (`9`) for Champion Lance. Although stored as `u16`, it must remain in `0..255`; a larger value fails loudly instead of being truncated. Portrait and symbol mapping adapters accept standard, Platinum, and Frontier families.

### Style 3: Rocket Admin

The native Rocket-admin sequence builds three staged Rocket background screen layers, then presents the configured admin portrait and static trainer name before transition to battle. Group 1 supplies `RLCN`, `RGCN`, and three distinct `RCSN` members; group 2 supplies the portrait quartet. Style 3 has no class-local VS-symbol group.

### Style 4: Kimono Girl

The native Kimono presentation uses a background shutter/door effect to cover and release the screen. It displays neither a class-local portrait nor a trainer name. Group 1 supplies exactly `RLCN`, `RGCN`, and `RCSN 1`; the remaining style-specific fields are unused.

### Style 5: Red

The native Red presentation animates a field of block/checker sprites across the screen and completes through the original transition choreography. It displays neither a class-local portrait nor a trainer name. Group 1 supplies the block `RLCN/RGCN/RECN/RNAN` quartet.

### Style 6: Generic Team Rocket

The native generic-Rocket presentation moves small Team Rocket symbols from the screen edges towards the centre before battle handoff. It displays neither a class-local portrait nor a trainer name. Group 1 supplies the symbol `RLCN/RGCN/RECN/RNAN` quartet.

### Styles 7-12: Static Ordinary Variants

Styles 7-12 expose the six native Style 0 trainer handlers directly. They keep the selected effect fixed, so terrain and time do not choose another variant. Each consumes one group-1 sprite quartet and no name, portrait, or VS-symbol fields.

| Style | Fixed visual state | Native effect | Native-sized asset family |
|---:|---|---:|---|
| `7` | normal/default early | `6` | small quartet |
| `8` | normal/default late | `7` | large quartet |
| `9` | water early | `8` | small quartet |
| `10` | water late | `9` | large quartet |
| `11` | cave early; three small-ball sprites | `10` | small quartet |
| `12` | cave late; one small-ball sprite | `11` | small quartet |

The style name describes the native handler being requested, not the actual map terrain. Selecting Style 9, for example, invokes the water-early opening animation on any map but does not independently change the later in-battle terrain splash. Asset cell counts, animation sequences, palette transparency, and image indices must satisfy the chosen native handler.

## Style 13

### What Style 13 Is

Style 13 is the Frontier Brain presentation: white flash, an initially absent banner, thin-strip entry and vertical expansion, animated/cycling background, staged VS-symbol reveal, shadowed portrait slide-in, second flash, restored portrait plus trainer name, post-reveal hold, fade to white, cleanup, and battle handoff.

The ordinary-battle implementation reproduces this presentation in a patch-owned Overlay 1 lifecycle because Overlay 80 cannot be resident alongside the field overlay used by ordinary trainer encounters. It therefore does not load Overlay 80 or manufacture Frontier application state. Its contiguous lifecycle block occupies `0xEE8` bytes, approximately 46% of the `0x2068`-byte payload; related Style 13 cache and adaptation helpers elsewhere in the payload make the complete share slightly larger. Native Frontier Brain battles retain their own Overlay 80 lifecycle.

Frame-by-frame comparison with the vanilla Frontier presentation found no confirmed discrepancy reaching `0.1` seconds. The largest credible transient difference is approximately `67` ms, and the complete presentation differs by approximately `33` ms.

### Configuration Groups

Style 13 uses `vsStyle = 13`, a static `trainerNameId`, and three asset groups from trainer-class member `N` in `/a/1/5/5`, where `N` is the trainer class:

- Group 1: background `RLCN`, `RGCN`, and `RCSN 1`.
- Group 2: opponent portrait `RLCN`, `RGCN`, `RECN`, and `RNAN`.
- Group 3: VS-symbol `RLCN`, `RGCN`, `RECN`, and `RNAN`.

The background is tile/screen data, not a sprite quartet. Group 1 `RECN`, `RNAN`, `RCSN 2`, and `RCSN 3` are unused and canonically zero for this style. The Style Applicability Matrix gives the definitive offsets and types. All asset references are `u16` `/a/1/0/9` member IDs, and high member IDs are supported where the selected loader and mapping family are compatible.

### Name Behaviour

The display name is not hardcoded. The active class record's `u16` message ID at `+0x0C` is cached before Overlay 1 setup and used to read the configured message from archive member `0x2D9` (`729` decimal).

This is class-local metadata. One class therefore has one configured Style 13 display name. It does not automatically use the personal name of each trainer instance sharing that class. Distinct per-trainer names require an additional trainer-instance source or indirection layer.

### Encounter Origins

#### Ordinary Battles

Ordinary presentation is selected directly from `vsStyle` without a legacy combo or `vsIntro` gate. Style 13 retains its configured presentation and class-local music in ordinary 1v1 and 1v2 doubles. Link and general Frontier policies still take precedence where applicable.

#### Native Frontier Brains

Overlay 80's presentation selector, trainer class, and actual Frontier trainer index are separate identifiers:

| Selector | Brain | Class | Actual Frontier records | Native boundary |
|---:|---|---:|---:|---|
| `1` | Palmer | `97` | `305/306` | Tower single battles `21/49` |
| `2` | Thorton | `100` | `309/310` | Factory single battles `21/49` |
| `3` | Thorton alias | `100` | none found | no HG script use found |
| `4` | Darach | `102` | `313/314` | Castle single battles `21/49` |
| `5` | Argenta | `99` | `307/308` | Hall single battles `50/170` |
| `6` | Dahlia | `101` | `311/312` | Arcade single battles `21/49` |

Ordinary trainer IDs `707..711` are dummy Brain trainer/name records and must not be used as Frontier indexes: `/a/2/0/2` ends at member `314`. Actual opponent name and record-backed dialogue are keyed by the Frontier index; facility dialogue and the VS presentation selector remain script-owned.

The selected record must have `vsStyle = 13`. Any other style enters the intentional fail-loud assertion loop. Native Frontier selection of ordinary styles `0..12` is not supported.

A non-Brain trainer class appearing in a Frontier facility is not selected by this table. Location alone does not invoke the Brain presentation.

### Shared Data Without Renderer Collision

Ordinary Overlay 1 and native Overlay 80 are independent renderer/task paths. They share the class record and `/a/1/0/9` assets. This means:

- configuring an ordinary class does not redirect it through Overlay 80;
- configuring Palmer changes the recipe seen by both an ordinary-origin class 97 encounter and a native Palmer encounter;
- each origin still applies its own ownership, timing, and mapping adapter; and
- compatibility on one route does not automatically establish compatibility for every asset family on the other route.

### Asset Compatibility

| Component | Ordinary Overlay 1 | Native Overlay 80 |
|---|---|---|
| Name | class-local `u16` message ID | class-local name value from the same row |
| Background | widened `u16` RLCN/RGCN/RCSN | widened `u16` RLCN/RGCN/RCSN |
| VS symbol | mapping-adapted to 1D-32 | mapping-adapted to 1D-128 |
| Portrait | standard, imported Platinum, and Frontier-family inputs supported through the ordinary renderer adapter | standard, imported Platinum, and Frontier-family inputs supported through the native renderer adapter |

Standard HG Gym/Elite portraits and inspected imported Platinum portraits use 1D-32 mapping. Vanilla Frontier Brain portraits use 1D-128 mapping. The patch supports both directions through its mapping adapters and derives palette count from the selected RECN data.

#### Background Palette Animation

Style 13 does not animate the banner by changing tiles or tilemaps. The RGCN and RCSN selected at `0x18` and `0x1E` remain static while the renderer treats the first `0x100` bytes of raw colour data in the RLCN selected at `0x16` as a temporal stream:

```text
frame 0: bytes 0x00..0x1F   (16 BGR555 colours)
frame 1: bytes 0x20..0x3F   (16 BGR555 colours)
...
frame 7: bytes 0xE0..0xFF   (16 BGR555 colours)
```

The ordinary renderer copies all eight frames into presentation-owned work. Once the cycle task exists, the main lifecycle advances selector `0..7` once per lifecycle tick and the VWait callback uploads the selected `0x20`-byte frame to main-BG palette memory. Under normal emulation this is one palette state per displayed game frame, so the eight-state pattern repeats roughly every `133` ms. The native Overlay 80 route uses the same eight-frame data interpretation through its own task lifecycle.

An RLCN can therefore be type-correct, large enough, and addressable through a valid `u16` member ID while still being visually unsuitable for Style 13. Many palettes created for static backgrounds contain one populated palette bank followed by unused zero-filled banks. Style 13 displays those unused banks as real animation frames, producing a strong periodic flash or blackout. This is content behaviour, not evidence that high member IDs were narrowed or that the renderer read the wrong asset.

For example, vanilla RLCN member `21` contains one populated first frame followed by seven all-zero `0x20`-byte frames and therefore produces a visible flash. By comparison, vanilla Frontier RLCN member `202` contains eight distinct, closely related frames designed as a smooth cycle.

For a moving Style 13 banner, provide eight deliberately related palette frames. For a static-colour banner, repeat the same `0x20`-byte palette eight times. Do not pad a single frame with zeroes. Keep global white fades and portrait shadow/restoration conceptually separate: those are presentation states, whereas this loop affects only the banner background palette.

## Licence

The original patch source, builder, and documentation are released under the MIT Licence in `LICENSE`. This licence does not grant rights to Pokemon ROMs, Nintendo/Game Freak assets, or third-party projects and tools. No ROM or built game-asset archive is distributed with the patch.
