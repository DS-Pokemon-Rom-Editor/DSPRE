.nds
.thumb

; =============================================================================
; Trainer Class Metadata Relocation v1.0.0
; =============================================================================
;
; HeartGold (USA) cumulative runtime patch. Trainer-class records in a/1/5/5 own gender, prize coefficient, eye-contact music, battle music, VS style, and the style-specific presentation data defined by the release and usage notes.
;
; Native writes are limited to the audited hook/literal manifest and the deliberate species-to-combo table extension. Patch-added executable code and storage are emitted as one contiguous payload in an author-owned resident executable region. Existing zero or 0xFF bytes do not establish ownership.
;
; Installation controls below run before any output file is opened and emit no runtime bytes. Leave the modified-target bypass disabled for first install. Enabling it permits deliberate reapplication or installation over a modified target and transfers collision responsibility to the author.

.definelabel PATCH, 1

; =============================================================================
; Author configuration
; =============================================================================

; Installation policy.
TCM_ALLOW_MODIFIED_INSTALL_TARGETS    equ 0

; Payload placement. Set both values together. INJECT_FILE identifies the resident executable container; INJECT_ADDR is the payload's runtime address.
INJECT_FILE                           equ "unpacked/synthOverlay/0000"
INJECT_ADDR                           equ 0x023C8000

; General battle policies. These remain independent even where native defaults currently share effect or sequence IDs.
TCM_TRAINER_DOUBLE_EFFECT              equ 38
TCM_TRAINER_DOUBLE_MUSIC               equ 1117
TCM_LINK_SINGLE_EFFECT                 equ 37
TCM_LINK_DOUBLE_EFFECT                 equ 38
TCM_LINK_BATTLE_MUSIC                  equ 1117
TCM_FRONTIER_SINGLE_EFFECT             equ 37
TCM_FRONTIER_DOUBLE_EFFECT             equ 38
TCM_FRONTIER_BATTLE_MUSIC              equ 1117
TCM_FRONTIER_BRAIN_PRESENTATION_MUSIC  equ 1147

; Derived mapping for the two supported injection files. The file selection, not a broad address range, determines which load base applies.
TCM_PAYLOAD_SIZE                       equ 0x2068
.if INJECT_FILE == "arm9/arm9.bin"
INJECT_FILE_BASE                       equ 0x02000000
.elseif INJECT_FILE == "unpacked/synthOverlay/0000"
INJECT_FILE_BASE                       equ 0x023C8000
.else
    .error "Unsupported INJECT_FILE; use arm9/arm9.bin or unpacked/synthOverlay/0000"
INJECT_FILE_BASE                       equ 0
.endif
INJECT_FILE_OFFSET                     equ INJECT_ADDR - INJECT_FILE_BASE
INJECT_REQUIRED_FILE_SIZE              equ INJECT_FILE_OFFSET + TCM_PAYLOAD_SIZE

; =============================================================================
; Patch map
; =============================================================================
;
; arm9   - trainer/wild routing, species-combo extension, gender, eye music
; ov001  - effect dispatch and ordinary Style 13 renderer
; ov012  - prize coefficient lookup
; ov080  - native Frontier Brain recipe, music, and resource routing
; ov115  - Styles 1 and 2
; ov117  - Styles 3 and 6
; ov118  - Style 4
; ov119  - Style 0 and Styles 7 through 12
; ov120  - Style 5
; payload container - all patch-owned executable helpers and runtime storage
; a109   - presentation assets, authored separately from this ASM
; a155   - per-class metadata records, authored separately from this ASM

; =============================================================================
; Installation-time validation only - emits no runtime bytes
; =============================================================================

.if INJECT_ADDR < INJECT_FILE_BASE
    .error "INJECT_ADDR precedes the configured INJECT_FILE mapping"
.endif
.if INJECT_ADDR & 3
    .error "INJECT_ADDR must be word aligned"
.endif

.if TCM_ALLOW_MODIFIED_INSTALL_TARGETS == 0
    .if fileexists("arm9/arm9.bin") == 0
        .error "Missing installation target: arm9/arm9.bin"
    .else
        .if filesize("arm9/arm9.bin") < 0xFC40A
            .error "Installation target is too short: arm9/arm9.bin"
        .else
            .if readu32("arm9/arm9.bin", 0x51800) != 0xFF9AF7FF
                .error "Modified native target: BattleSetup_GetWildBattleMusic_RouteHook + 0x0"
            .endif
            .if readu32("arm9/arm9.bin", 0x51804) != 0x1C061C29
                .error "Modified native target: BattleSetup_GetWildBattleMusic_RouteHook + 0x4"
            .endif
            .if readu32("arm9/arm9.bin", 0x51808) != 0xFFE2F7FF
                .error "Modified native target: BattleSetup_GetWildBattleMusic_RouteHook + 0x8"
            .endif
            .if readu32("arm9/arm9.bin", 0x5180C) != 0x20071C04
                .error "Modified native target: BattleSetup_GetWildBattleMusic_RouteHook + 0xC"
            .endif
            .if readu32("arm9/arm9.bin", 0x51810) != 0x58280180
                .error "Modified native target: BattleSetup_GetWildBattleMusic_RouteHook + 0x10"
            .endif
            .if readu32("arm9/arm9.bin", 0x517BE) != 0xF19F1C20
                .error "Modified native target: BattleStartDynamicTransition_Hook + 0x0"
            .endif
            .if readu16("arm9/arm9.bin", 0x517C2) != 0xFAAE
                .error "Modified native target: BattleStartDynamicTransition_Hook + 0x4"
            .endif
            .if readu32("arm9/arm9.bin", 0x517EC) != 0xFFA4F7FF
                .error "Modified native target: BattleSetup_GetWildTransitionEffect_RouteHook + 0x0"
            .endif
            .if readu32("arm9/arm9.bin", 0x517F0) != 0xF7FF1C21
                .error "Modified native target: BattleSetup_GetWildTransitionEffect_RouteHook + 0x4"
            .endif
            .if readu16("arm9/arm9.bin", 0x517F4) != 0xFFD7
                .error "Modified native target: BattleSetup_GetWildTransitionEffect_RouteHook + 0x8"
            .endif
            .if readu32("arm9/arm9.bin", 0x51868) != 0x22004B09
                .error "Modified native target: NPCTrainerGetBattleIntro_Hook + 0x0"
            .endif
            .if readu32("arm9/arm9.bin", 0x5186C) != 0x05898819
                .error "Modified native target: NPCTrainerGetBattleIntro_Hook + 0x4"
            .endif
            .if readu32("arm9/arm9.bin", 0x51870) != 0x42880D89
                .error "Modified native target: NPCTrainerGetBattleIntro_Hook + 0x8"
            .endif
            .if readu32("arm9/arm9.bin", 0x51874) != 0x4806D105
                .error "Modified native target: NPCTrainerGetBattleIntro_Hook + 0xC"
            .endif
            .if readu32("arm9/arm9.bin", 0x51878) != 0x5A400051
                .error "Modified native target: NPCTrainerGetBattleIntro_Hook + 0x10"
            .endif
            .if readu32("arm9/arm9.bin", 0x5187C) != 0x0E800400
                .error "Modified native target: NPCTrainerGetBattleIntro_Hook + 0x14"
            .endif
            .if readu32("arm9/arm9.bin", 0x51880) != 0x1C524770
                .error "Modified native target: NPCTrainerGetBattleIntro_Hook + 0x18"
            .endif
            .if readu32("arm9/arm9.bin", 0x51884) != 0x2A201C9B
                .error "Modified native target: NPCTrainerGetBattleIntro_Hook + 0x1C"
            .endif
            .if readu32("arm9/arm9.bin", 0x51888) != 0x2029D3F0
                .error "Modified native target: NPCTrainerGetBattleIntro_Hook + 0x20"
            .endif
            .if readu32("arm9/arm9.bin", 0x5188C) != 0x46C04770
                .error "Modified native target: NPCTrainerGetBattleIntro_Hook + 0x24"
            .endif
            .if readu32("arm9/arm9.bin", 0x51890) != 0x020FC3CA
                .error "Modified native target: NPCTrainerGetBattleIntro_Hook + 0x28"
            .endif
            .if readu16("arm9/arm9.bin", 0x518CA) != 0x2A0B
                .error "Modified native target: WildPokemonSpeciesComboLoopBound + 0x0"
            .endif
            .if readu32("arm9/arm9.bin", 0xFC3CA) != 0x04430042
                .error "Modified native target: TCM_SPECIES_COMBO_EXTENSION_ADDR + 0x0"
            .endif
            .if readu32("arm9/arm9.bin", 0xFC3CE) != 0x0C480846
                .error "Modified native target: TCM_SPECIES_COMBO_EXTENSION_ADDR + 0x4"
            .endif
            .if readu32("arm9/arm9.bin", 0xFC3D2) != 0x144B104A
                .error "Modified native target: TCM_SPECIES_COMBO_EXTENSION_ADDR + 0x8"
            .endif
            .if readu32("arm9/arm9.bin", 0xFC3D6) != 0x1C4C1849
                .error "Modified native target: TCM_SPECIES_COMBO_EXTENSION_ADDR + 0xC"
            .endif
            .if readu32("arm9/arm9.bin", 0xFC3DA) != 0x24672062
                .error "Modified native target: TCM_SPECIES_COMBO_EXTENSION_ADDR + 0x10"
            .endif
            .if readu32("arm9/arm9.bin", 0xFC3DE) != 0x2C692868
                .error "Modified native target: TCM_SPECIES_COMBO_EXTENSION_ADDR + 0x14"
            .endif
            .if readu32("arm9/arm9.bin", 0xFC3E2) != 0x346B306A
                .error "Modified native target: TCM_SPECIES_COMBO_EXTENSION_ADDR + 0x18"
            .endif
            .if readu32("arm9/arm9.bin", 0xFC3E6) != 0x3C6E386C
                .error "Modified native target: TCM_SPECIES_COMBO_EXTENSION_ADDR + 0x1C"
            .endif
            .if readu32("arm9/arm9.bin", 0xFC3EA) != 0x44594057
                .error "Modified native target: TCM_SPECIES_COMBO_EXTENSION_ADDR + 0x20"
            .endif
            .if readu32("arm9/arm9.bin", 0xFC3EE) != 0x4C584870
                .error "Modified native target: TCM_SPECIES_COMBO_EXTENSION_ADDR + 0x24"
            .endif
            .if readu32("arm9/arm9.bin", 0xFC3F2) != 0x54175056
                .error "Modified native target: TCM_SPECIES_COMBO_EXTENSION_ADDR + 0x28"
            .endif
            .if readu32("arm9/arm9.bin", 0xFC3F6) != 0x80745477
                .error "Modified native target: TCM_SPECIES_COMBO_EXTENSION_ADDR + 0x2C"
            .endif
            .if readu32("arm9/arm9.bin", 0xFC3FA) != 0x7C758472
                .error "Modified native target: TCM_SPECIES_COMBO_EXTENSION_ADDR + 0x30"
            .endif
            .if readu32("arm9/arm9.bin", 0xFC3FE) != 0x887C7876
                .error "Modified native target: TCM_SPECIES_COMBO_EXTENSION_ADDR + 0x34"
            .endif
            .if readu32("arm9/arm9.bin", 0xFC402) != 0x743E7437
                .error "Modified native target: TCM_SPECIES_COMBO_EXTENSION_ADDR + 0x38"
            .endif
            .if readu32("arm9/arm9.bin", 0xFC406) != 0xB06DAC2F
                .error "Modified native target: TCM_SPECIES_COMBO_EXTENSION_ADDR + 0x3C"
            .endif
            .if readu32("arm9/arm9.bin", 0x735F8) != 0x5C084901
                .error "Modified native target: TrainerClass_GetGender_Hook + 0x0"
            .endif
            .if readu32("arm9/arm9.bin", 0x735FC) != 0x46C04770
                .error "Modified native target: TrainerClass_GetGender_Hook + 0x4"
            .endif
            .if readu32("arm9/arm9.bin", 0x73600) != 0x020FFB90
                .error "Modified native target: TrainerClass_GetGender_Hook + 0x8"
            .endif
            .if readu32("arm9/arm9.bin", 0x55098) != 0x1C0CB5F8
                .error "Modified native target: Trainer_GetEncounterMusic_Hook + 0x0"
            .endif
            .if readu32("arm9/arm9.bin", 0x5509C) != 0x2C021C05
                .error "Modified native target: Trainer_GetEncounterMusic_Hook + 0x4"
            .endif
            .if readu32("arm9/arm9.bin", 0x550A0) != 0xF7D0DB01
                .error "Modified native target: Trainer_GetEncounterMusic_Hook + 0x8"
            .endif
            .if readu32("arm9/arm9.bin", 0x550A4) != 0x1C28FA3B
                .error "Modified native target: Trainer_GetEncounterMusic_Hook + 0xC"
            .endif
            .if readu32("arm9/arm9.bin", 0x550A8) != 0xF01E2101
                .error "Modified native target: Trainer_GetEncounterMusic_Hook + 0x10"
            .endif
            .if readu32("arm9/arm9.bin", 0x550AC) != 0x0600F9E1
                .error "Modified native target: Trainer_GetEncounterMusic_Hook + 0x14"
            .endif
            .if readu32("arm9/arm9.bin", 0x550B0) != 0x480A0E05
                .error "Modified native target: Trainer_GetEncounterMusic_Hook + 0x18"
            .endif
            .if readu32("arm9/arm9.bin", 0x550B4) != 0x2300490A
                .error "Modified native target: Trainer_GetEncounterMusic_Hook + 0x1C"
            .endif
            .if readu32("arm9/arm9.bin", 0x550B8) != 0x1C1F2606
                .error "Modified native target: Trainer_GetEncounterMusic_Hook + 0x20"
            .endif
            .if readu32("arm9/arm9.bin", 0x550BC) != 0x19CA4377
                .error "Modified native target: Trainer_GetEncounterMusic_Hook + 0x24"
            .endif
            .if readu32("arm9/arm9.bin", 0x550C0) != 0x42BD5BCF
                .error "Modified native target: Trainer_GetEncounterMusic_Hook + 0x28"
            .endif
            .if readu32("arm9/arm9.bin", 0x550C4) != 0x1C60D103
                .error "Modified native target: Trainer_GetEncounterMusic_Hook + 0x2C"
            .endif
            .if readu32("arm9/arm9.bin", 0x550C8) != 0x5A100040
                .error "Modified native target: Trainer_GetEncounterMusic_Hook + 0x30"
            .endif
            .if readu32("arm9/arm9.bin", 0x550CC) != 0x1C5ABDF8
                .error "Modified native target: Trainer_GetEncounterMusic_Hook + 0x34"
            .endif
            .if readu32("arm9/arm9.bin", 0x550D0) != 0x0C130412
                .error "Modified native target: Trainer_GetEncounterMusic_Hook + 0x38"
            .endif
            .if readu32("arm9/arm9.bin", 0x550D4) != 0xD3F02B2C
                .error "Modified native target: Trainer_GetEncounterMusic_Hook + 0x3C"
            .endif
            .if readu32("arm9/arm9.bin", 0x550D8) != 0x46C0BDF8
                .error "Modified native target: Trainer_GetEncounterMusic_Hook + 0x40"
            .endif
            .if readu32("arm9/arm9.bin", 0x550DC) != 0x00000454
                .error "Modified native target: Trainer_GetEncounterMusic_Hook + 0x44"
            .endif
            .if readu32("arm9/arm9.bin", 0x550E0) != 0x020FC61C
                .error "Modified native target: Trainer_GetEncounterMusic_Hook + 0x48"
            .endif
        .endif
    .endif

    .if fileexists("arm9_overlays/ov119.bin") == 0
        .error "Missing installation target: arm9_overlays/ov119.bin"
    .else
        .if filesize("arm9_overlays/ov119.bin") < 0x12C2
            .error "Installation target is too short: arm9_overlays/ov119.bin"
        .else
            .if readu32("arm9_overlays/ov119.bin", 0x62) != 0x90002001
                .error "Modified native target: Ov119_NormalLateSpriteLoad_Hook + 0x0"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0x66) != 0x90012007
                .error "Modified native target: Ov119_NormalLateSpriteLoad_Hook + 0x4"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0x6A) != 0x90022009
                .error "Modified native target: Ov119_NormalLateSpriteLoad_Hook + 0x8"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0x6E) != 0x90032008
                .error "Modified native target: Ov119_NormalLateSpriteLoad_Hook + 0xC"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0x72) != 0x226148B7
                .error "Modified native target: Ov119_NormalLateSpriteLoad_Hook + 0x10"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0x76) != 0x1C219004
                .error "Modified native target: Ov119_NormalLateSpriteLoad_Hook + 0x14"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0x7A) != 0x6A380092
                .error "Modified native target: Ov119_NormalLateSpriteLoad_Hook + 0x18"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0x7E) != 0x18A23148
                .error "Modified native target: Ov119_NormalLateSpriteLoad_Hook + 0x1C"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0x82) != 0xF7912300
                .error "Modified native target: Ov119_NormalLateSpriteLoad_Hook + 0x20"
            .endif
            .if readu16("arm9_overlays/ov119.bin", 0x86) != 0xFAB6
                .error "Modified native target: Ov119_NormalLateSpriteLoad_Hook + 0x24"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0x3BC) != 0x90002001
                .error "Modified native target: Ov119_NormalEarlySpriteLoad_Hook + 0x0"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0x3C0) != 0x90012004
                .error "Modified native target: Ov119_NormalEarlySpriteLoad_Hook + 0x4"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0x3C4) != 0x90022006
                .error "Modified native target: Ov119_NormalEarlySpriteLoad_Hook + 0x8"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0x3C8) != 0x90032005
                .error "Modified native target: Ov119_NormalEarlySpriteLoad_Hook + 0xC"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0x3CC) != 0x225A489C
                .error "Modified native target: Ov119_NormalEarlySpriteLoad_Hook + 0x10"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0x3D0) != 0x1C219004
                .error "Modified native target: Ov119_NormalEarlySpriteLoad_Hook + 0x14"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0x3D4) != 0x6A300092
                .error "Modified native target: Ov119_NormalEarlySpriteLoad_Hook + 0x18"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0x3D8) != 0x18A2312C
                .error "Modified native target: Ov119_NormalEarlySpriteLoad_Hook + 0x1C"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0x3DC) != 0xF7912300
                .error "Modified native target: Ov119_NormalEarlySpriteLoad_Hook + 0x20"
            .endif
            .if readu16("arm9_overlays/ov119.bin", 0x3E0) != 0xF909
                .error "Modified native target: Ov119_NormalEarlySpriteLoad_Hook + 0x24"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0xA7E) != 0x90002001
                .error "Modified native target: Ov119_WaterEarlySpriteLoad_Hook + 0x0"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0xA82) != 0x90012004
                .error "Modified native target: Ov119_WaterEarlySpriteLoad_Hook + 0x4"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0xA86) != 0x90022006
                .error "Modified native target: Ov119_WaterEarlySpriteLoad_Hook + 0x8"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0xA8A) != 0x90032005
                .error "Modified native target: Ov119_WaterEarlySpriteLoad_Hook + 0xC"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0xA8E) != 0x224F48CE
                .error "Modified native target: Ov119_WaterEarlySpriteLoad_Hook + 0x10"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0xA92) != 0x98079004
                .error "Modified native target: Ov119_WaterEarlySpriteLoad_Hook + 0x14"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0xA96) != 0x6A000092
                .error "Modified native target: Ov119_WaterEarlySpriteLoad_Hook + 0x18"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0xA9A) != 0x18A21C21
                .error "Modified native target: Ov119_WaterEarlySpriteLoad_Hook + 0x1C"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0xA9E) != 0xF7902300
                .error "Modified native target: Ov119_WaterEarlySpriteLoad_Hook + 0x20"
            .endif
            .if readu16("arm9_overlays/ov119.bin", 0xAA2) != 0xFDA8
                .error "Modified native target: Ov119_WaterEarlySpriteLoad_Hook + 0x24"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0x6BE) != 0x90002001
                .error "Modified native target: Ov119_WaterLateSpriteLoad_Hook + 0x0"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0x6C2) != 0x90012007
                .error "Modified native target: Ov119_WaterLateSpriteLoad_Hook + 0x4"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0x6C6) != 0x90022009
                .error "Modified native target: Ov119_WaterLateSpriteLoad_Hook + 0x8"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0x6CA) != 0x90032008
                .error "Modified native target: Ov119_WaterLateSpriteLoad_Hook + 0xC"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0x6CE) != 0x226348C8
                .error "Modified native target: Ov119_WaterLateSpriteLoad_Hook + 0x10"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0x6D2) != 0x1C219004
                .error "Modified native target: Ov119_WaterLateSpriteLoad_Hook + 0x14"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0x6D6) != 0x6A380092
                .error "Modified native target: Ov119_WaterLateSpriteLoad_Hook + 0x18"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0x6DA) != 0x18A23150
                .error "Modified native target: Ov119_WaterLateSpriteLoad_Hook + 0x1C"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0x6DE) != 0xF7902300
                .error "Modified native target: Ov119_WaterLateSpriteLoad_Hook + 0x20"
            .endif
            .if readu16("arm9_overlays/ov119.bin", 0x6E2) != 0xFF88
                .error "Modified native target: Ov119_WaterLateSpriteLoad_Hook + 0x24"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0x129C) != 0x90002001
                .error "Modified native target: Ov119_CaveEarlySpriteLoad_Hook + 0x0"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0x12A0) != 0x90012004
                .error "Modified native target: Ov119_CaveEarlySpriteLoad_Hook + 0x4"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0x12A4) != 0x90022006
                .error "Modified native target: Ov119_CaveEarlySpriteLoad_Hook + 0x8"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0x12A8) != 0x90032005
                .error "Modified native target: Ov119_CaveEarlySpriteLoad_Hook + 0xC"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0x12AC) != 0x224F48C9
                .error "Modified native target: Ov119_CaveEarlySpriteLoad_Hook + 0x10"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0x12B0) != 0x98069004
                .error "Modified native target: Ov119_CaveEarlySpriteLoad_Hook + 0x14"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0x12B4) != 0x6A000092
                .error "Modified native target: Ov119_CaveEarlySpriteLoad_Hook + 0x18"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0x12B8) != 0x18A21C21
                .error "Modified native target: Ov119_CaveEarlySpriteLoad_Hook + 0x1C"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0x12BC) != 0xF7902300
                .error "Modified native target: Ov119_CaveEarlySpriteLoad_Hook + 0x20"
            .endif
            .if readu16("arm9_overlays/ov119.bin", 0x12C0) != 0xF999
                .error "Modified native target: Ov119_CaveEarlySpriteLoad_Hook + 0x24"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0xFD6) != 0x90002001
                .error "Modified native target: Ov119_CaveLateSpriteLoad_Hook + 0x0"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0xFDA) != 0x90012004
                .error "Modified native target: Ov119_CaveLateSpriteLoad_Hook + 0x4"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0xFDE) != 0x90022006
                .error "Modified native target: Ov119_CaveLateSpriteLoad_Hook + 0x8"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0xFE2) != 0x90032005
                .error "Modified native target: Ov119_CaveLateSpriteLoad_Hook + 0xC"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0xFE6) != 0x22664890
                .error "Modified native target: Ov119_CaveLateSpriteLoad_Hook + 0x10"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0xFEA) != 0x1C219004
                .error "Modified native target: Ov119_CaveLateSpriteLoad_Hook + 0x14"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0xFEE) != 0x6A280092
                .error "Modified native target: Ov119_CaveLateSpriteLoad_Hook + 0x18"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0xFF2) != 0x18A2315C
                .error "Modified native target: Ov119_CaveLateSpriteLoad_Hook + 0x1C"
            .endif
            .if readu32("arm9_overlays/ov119.bin", 0xFF6) != 0xF7902300
                .error "Modified native target: Ov119_CaveLateSpriteLoad_Hook + 0x20"
            .endif
            .if readu16("arm9_overlays/ov119.bin", 0xFFA) != 0xFAFC
                .error "Modified native target: Ov119_CaveLateSpriteLoad_Hook + 0x24"
            .endif
        .endif
    .endif

    .if fileexists("arm9_overlays/ov012.bin") == 0
        .error "Missing installation target: arm9_overlays/ov012.bin"
    .else
        .if filesize("arm9_overlays/ov012.bin") < 0x8366
            .error "Installation target is too short: arm9_overlays/ov012.bin"
        .else
            .if readu32("arm9_overlays/ov012.bin", 0x828E) != 0xA9015B60
                .error "Modified native target: CalcPrizeMoney_TrainerRead_Hook + 0x0"
            .endif
            .if readu32("arm9_overlays/ov012.bin", 0x8292) != 0xFD41F633
                .error "Modified native target: CalcPrizeMoney_TrainerRead_Hook + 0x4"
            .endif
            .if readu32("arm9_overlays/ov012.bin", 0x82FE) != 0x7842A801
                .error "Modified native target: CalcPrizeMoney_PrizeScan_Hook + 0x0"
            .endif
            .if readu32("arm9_overlays/ov012.bin", 0x8302) != 0x2400491F
                .error "Modified native target: CalcPrizeMoney_PrizeScan_Hook + 0x4"
            .endif
            .if readu32("arm9_overlays/ov012.bin", 0x8306) != 0x42828808
                .error "Modified native target: CalcPrizeMoney_PrizeScan_Hook + 0x8"
            .endif
            .if readu32("arm9_overlays/ov012.bin", 0x830A) != 0x1C64D003
                .error "Modified native target: CalcPrizeMoney_PrizeScan_Hook + 0xC"
            .endif
            .if readu32("arm9_overlays/ov012.bin", 0x830E) != 0x2C811D09
                .error "Modified native target: CalcPrizeMoney_PrizeScan_Hook + 0x10"
            .endif
            .if readu32("arm9_overlays/ov012.bin", 0x8312) != 0x2C81DBF8
                .error "Modified native target: CalcPrizeMoney_PrizeScan_Hook + 0x14"
            .endif
            .if readu32("arm9_overlays/ov012.bin", 0x8316) != 0xF5E5DB01
                .error "Modified native target: CalcPrizeMoney_PrizeScan_Hook + 0x18"
            .endif
            .if readu32("arm9_overlays/ov012.bin", 0x831A) != 0x2C81FCA0
                .error "Modified native target: CalcPrizeMoney_PrizeScan_Hook + 0x1C"
            .endif
            .if readu32("arm9_overlays/ov012.bin", 0x831E) != 0x2402DB00
                .error "Modified native target: CalcPrizeMoney_PrizeScan_Hook + 0x20"
            .endif
            .if readu32("arm9_overlays/ov012.bin", 0x8330) != 0x00A14814
                .error "Modified native target: CalcPrizeMoney_TagLoad_Hook + 0x0"
            .endif
            .if readu16("arm9_overlays/ov012.bin", 0x8334) != 0x5A40
                .error "Modified native target: CalcPrizeMoney_TagLoad_Hook + 0x4"
            .endif
            .if readu32("arm9_overlays/ov012.bin", 0x834A) != 0x00A1480E
                .error "Modified native target: CalcPrizeMoney_DoubleLoad_Hook + 0x0"
            .endif
            .if readu16("arm9_overlays/ov012.bin", 0x834E) != 0x5A40
                .error "Modified native target: CalcPrizeMoney_DoubleLoad_Hook + 0x4"
            .endif
            .if readu32("arm9_overlays/ov012.bin", 0x8360) != 0x00A14808
                .error "Modified native target: CalcPrizeMoney_SingleLoad_Hook + 0x0"
            .endif
            .if readu16("arm9_overlays/ov012.bin", 0x8364) != 0x5A40
                .error "Modified native target: CalcPrizeMoney_SingleLoad_Hook + 0x4"
            .endif
        .endif
    .endif

    .if fileexists("arm9_overlays/ov115.bin") == 0
        .error "Missing installation target: arm9_overlays/ov115.bin"
    .else
        .if filesize("arm9_overlays/ov115.bin") < 0xE9E
            .error "Installation target is too short: arm9_overlays/ov115.bin"
        .else
            .if readu32("arm9_overlays/ov115.bin", 0x200) != 0xB08AB5F8
                .error "Modified native target: Ov115_Vs20StateMachine_Hook + 0x0"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0x204) != 0x1C0F1C05
                .error "Modified native target: Ov115_Vs20StateMachine_Hook + 0x4"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0x208) != 0x1C166829
                .error "Modified native target: Ov115_Vs20StateMachine_Hook + 0x8"
            .endif
            .if readu16("arm9_overlays/ov115.bin", 0x20C) != 0x68EC
                .error "Modified native target: Ov115_Vs20StateMachine_Hook + 0xC"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0x2EE) != 0x22067B70
                .error "Modified native target: Ov115_Vs20PortraitLoad_Hook + 0x0"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0x2F2) != 0x90011C21
                .error "Modified native target: Ov115_Vs20PortraitLoad_Hook + 0x4"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0x2F6) != 0x01927BB0
                .error "Modified native target: Ov115_Vs20PortraitLoad_Hook + 0x8"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0x2FA) != 0x90023144
                .error "Modified native target: Ov115_Vs20PortraitLoad_Hook + 0xC"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0x2FE) != 0x18A27BF0
                .error "Modified native target: Ov115_Vs20PortraitLoad_Hook + 0x10"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0x302) != 0x48C29003
                .error "Modified native target: Ov115_Vs20PortraitLoad_Hook + 0x14"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0x306) != 0x7B339004
                .error "Modified native target: Ov115_Vs20PortraitLoad_Hook + 0x18"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0x30A) != 0xF7916A28
                .error "Modified native target: Ov115_Vs20PortraitLoad_Hook + 0x1C"
            .endif
            .if readu16("arm9_overlays/ov115.bin", 0x30E) != 0xF972
                .error "Modified native target: Ov115_Vs20PortraitLoad_Hook + 0x20"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0x310) != 0x90002001
                .error "Modified native target: Ov115_Vs20SymbolLoad_Hook + 0x0"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0x314) != 0x9001203C
                .error "Modified native target: Ov115_Vs20SymbolLoad_Hook + 0x4"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0x318) != 0x9002203D
                .error "Modified native target: Ov115_Vs20SymbolLoad_Hook + 0x8"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0x31C) != 0x9003203E
                .error "Modified native target: Ov115_Vs20SymbolLoad_Hook + 0xC"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0x320) != 0x226D48BC
                .error "Modified native target: Ov115_Vs20SymbolLoad_Hook + 0x10"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0x324) != 0x1C219004
                .error "Modified native target: Ov115_Vs20SymbolLoad_Hook + 0x14"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0x328) != 0x6A280092
                .error "Modified native target: Ov115_Vs20SymbolLoad_Hook + 0x18"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0x32C) != 0x18A23144
                .error "Modified native target: Ov115_Vs20SymbolLoad_Hook + 0x1C"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0x330) != 0xF791233B
                .error "Modified native target: Ov115_Vs20SymbolLoad_Hook + 0x20"
            .endif
            .if readu16("arm9_overlays/ov115.bin", 0x334) != 0xF95F
                .error "Modified native target: Ov115_Vs20SymbolLoad_Hook + 0x24"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0x386) != 0x58207B32
                .error "Modified native target: Ov115_Vs20PortraitSetup1_Hook + 0x0"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0x38A) != 0x230E1C39
                .error "Modified native target: Ov115_Vs20PortraitSetup1_Hook + 0x4"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0x38E) != 0xFF51F000
                .error "Modified native target: Ov115_Vs20PortraitSetup1_Hook + 0x8"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0x3D4) != 0x7C727CB1
                .error "Modified native target: Ov115_Vs20BackgroundLoad_Hook + 0x0"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0x3D8) != 0x6A287C33
                .error "Modified native target: Ov115_Vs20BackgroundLoad_Hook + 0x4"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0x3DC) != 0xF82AF791
                .error "Modified native target: Ov115_Vs20BackgroundLoad_Hook + 0x8"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0x548) != 0x58207B32
                .error "Modified native target: Ov115_Vs20PortraitSetup7_Hook + 0x0"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0x54C) != 0xF0001C39
                .error "Modified native target: Ov115_Vs20PortraitSetup7_Hook + 0x4"
            .endif
            .if readu16("arm9_overlays/ov115.bin", 0x550) != 0xFE71
                .error "Modified native target: Ov115_Vs20PortraitSetup7_Hook + 0x8"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0x958) != 0xB091B5F0
                .error "Modified native target: Ov115_Vs8StateMachine_Hook + 0x0"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0x95C) != 0x68301C06
                .error "Modified native target: Ov115_Vs8StateMachine_Hook + 0x4"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0x960) != 0x92069105
                .error "Modified native target: Ov115_Vs8StateMachine_Hook + 0x8"
            .endif
            .if readu16("arm9_overlays/ov115.bin", 0x964) != 0x68F4
                .error "Modified native target: Ov115_Vs8StateMachine_Hook + 0xC"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0xA28) != 0x22739806
                .error "Modified native target: Ov115_Vs8PortraitLoad_Hook + 0x0"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0xA2C) != 0x20018803
                .error "Modified native target: Ov115_Vs8PortraitLoad_Hook + 0x4"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0xA30) != 0x90001C21
                .error "Modified native target: Ov115_Vs8PortraitLoad_Hook + 0x8"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0xA34) != 0x90011C58
                .error "Modified native target: Ov115_Vs8PortraitLoad_Hook + 0xC"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0xA38) != 0x90021C98
                .error "Modified native target: Ov115_Vs8PortraitLoad_Hook + 0x10"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0xA3C) != 0x90031CD8
                .error "Modified native target: Ov115_Vs8PortraitLoad_Hook + 0x14"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0xA40) != 0x009248BE
                .error "Modified native target: Ov115_Vs8PortraitLoad_Hook + 0x18"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0xA44) != 0x6A309004
                .error "Modified native target: Ov115_Vs8PortraitLoad_Hook + 0x1C"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0xA48) != 0x18A2315C
                .error "Modified native target: Ov115_Vs8PortraitLoad_Hook + 0x20"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0xA4C) != 0xFDD2F790
                .error "Modified native target: Ov115_Vs8PortraitLoad_Hook + 0x24"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0xA50) != 0x9000200C
                .error "Modified native target: Ov115_Vs8BackgroundLoad_Hook + 0x0"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0xA54) != 0x90012030
                .error "Modified native target: Ov115_Vs8BackgroundLoad_Hook + 0x4"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0xA58) != 0x90022031
                .error "Modified native target: Ov115_Vs8BackgroundLoad_Hook + 0x8"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0xA5C) != 0x90032032
                .error "Modified native target: Ov115_Vs8BackgroundLoad_Hook + 0xC"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0xA60) != 0x220248B7
                .error "Modified native target: Ov115_Vs8BackgroundLoad_Hook + 0x10"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0xA64) != 0x9B069004
                .error "Modified native target: Ov115_Vs8BackgroundLoad_Hook + 0x14"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0xA68) != 0x02121C21
                .error "Modified native target: Ov115_Vs8BackgroundLoad_Hook + 0x18"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0xA6C) != 0x6A30789B
                .error "Modified native target: Ov115_Vs8BackgroundLoad_Hook + 0x1C"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0xA70) != 0x18A2315C
                .error "Modified native target: Ov115_Vs8BackgroundLoad_Hook + 0x20"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0xA74) != 0xFDBEF790
                .error "Modified native target: Ov115_Vs8BackgroundLoad_Hook + 0x24"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0xA78) != 0x90002001
                .error "Modified native target: Ov115_Vs8SymbolLoad_Hook + 0x0"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0xA7C) != 0x9001203C
                .error "Modified native target: Ov115_Vs8SymbolLoad_Hook + 0x4"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0xA80) != 0x9002203D
                .error "Modified native target: Ov115_Vs8SymbolLoad_Hook + 0x8"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0xA84) != 0x9003203E
                .error "Modified native target: Ov115_Vs8SymbolLoad_Hook + 0xC"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0xA88) != 0x228D48AE
                .error "Modified native target: Ov115_Vs8SymbolLoad_Hook + 0x10"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0xA8C) != 0x1C219004
                .error "Modified native target: Ov115_Vs8SymbolLoad_Hook + 0x14"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0xA90) != 0x6A300092
                .error "Modified native target: Ov115_Vs8SymbolLoad_Hook + 0x18"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0xA94) != 0x18A2315C
                .error "Modified native target: Ov115_Vs8SymbolLoad_Hook + 0x1C"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0xA98) != 0xF790233B
                .error "Modified native target: Ov115_Vs8SymbolLoad_Hook + 0x20"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0xA9C) != 0x6830FDAB
                .error "Modified native target: Ov115_Vs8SymbolLoad_Hook + 0x24"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0xB54) != 0xFB6EF000
                .error "Modified native target: Ov115_Vs8OpponentSilhouetteBlend_Hook + 0x0"
            .endif
            .if readu32("arm9_overlays/ov115.bin", 0xE9A) != 0xF9CBF000
                .error "Modified native target: Ov115_Vs8OpponentRevealBlend_Hook + 0x0"
            .endif
        .endif
    .endif

    .if fileexists("arm9_overlays/ov117.bin") == 0
        .error "Missing installation target: arm9_overlays/ov117.bin"
    .else
        .if filesize("arm9_overlays/ov117.bin") < 0x80A
            .error "Installation target is too short: arm9_overlays/ov117.bin"
        .else
            .if readu32("arm9_overlays/ov117.bin", 0x56) != 0x90002001
                .error "Modified native target: Ov117_GenericRocketSymbolLoad_Hook + 0x0"
            .endif
            .if readu32("arm9_overlays/ov117.bin", 0x5A) != 0x9001209C
                .error "Modified native target: Ov117_GenericRocketSymbolLoad_Hook + 0x4"
            .endif
            .if readu32("arm9_overlays/ov117.bin", 0x5E) != 0x9002209E
                .error "Modified native target: Ov117_GenericRocketSymbolLoad_Hook + 0x8"
            .endif
            .if readu32("arm9_overlays/ov117.bin", 0x62) != 0x9203229D
                .error "Modified native target: Ov117_GenericRocketSymbolLoad_Hook + 0xC"
            .endif
            .if readu32("arm9_overlays/ov117.bin", 0x66) != 0x329F488C
                .error "Modified native target: Ov117_GenericRocketSymbolLoad_Hook + 0x10"
            .endif
            .if readu32("arm9_overlays/ov117.bin", 0x6A) != 0x98069004
                .error "Modified native target: Ov117_GenericRocketSymbolLoad_Hook + 0x14"
            .endif
            .if readu32("arm9_overlays/ov117.bin", 0x6E) != 0x6A001C21
                .error "Modified native target: Ov117_GenericRocketSymbolLoad_Hook + 0x18"
            .endif
            .if readu32("arm9_overlays/ov117.bin", 0x72) != 0x230318A2
                .error "Modified native target: Ov117_GenericRocketSymbolLoad_Hook + 0x1C"
            .endif
            .if readu32("arm9_overlays/ov117.bin", 0x76) != 0xFABDF791
                .error "Modified native target: Ov117_GenericRocketSymbolLoad_Hook + 0x20"
            .endif
            .if readu32("arm9_overlays/ov117.bin", 0x7A) != 0x2600274F
                .error "Modified native target: Ov117_GenericRocketSymbolLoad_Hook + 0x24"
            .endif
            .if readu32("arm9_overlays/ov117.bin", 0x7E) != 0x00BF1C25
                .error "Modified native target: Ov117_GenericRocketSymbolLoad_Hook + 0x28"
            .endif
            .if readu32("arm9_overlays/ov117.bin", 0x504) != 0xB086B5F8
                .error "Modified native target: Ov117_RocketAdminStateMachine_Hook + 0x0"
            .endif
            .if readu32("arm9_overlays/ov117.bin", 0x508) != 0x90051C0D
                .error "Modified native target: Ov117_RocketAdminStateMachine_Hook + 0x4"
            .endif
            .if readu32("arm9_overlays/ov117.bin", 0x50C) != 0x1C166828
                .error "Modified native target: Ov117_RocketAdminStateMachine_Hook + 0x8"
            .endif
            .if readu32("arm9_overlays/ov117.bin", 0x510) != 0x68EC2704
                .error "Modified native target: Ov117_RocketAdminStateMachine_Hook + 0xC"
            .endif
            .if readu32("arm9_overlays/ov117.bin", 0x514) != 0xD900280C
                .error "Modified native target: Ov117_RocketAdminStateMachine_Hook + 0x10"
            .endif
            .if readu16("arm9_overlays/ov117.bin", 0x518) != 0xE2AE
                .error "Modified native target: Ov117_RocketAdminStateMachine_Hook + 0x14"
            .endif
            .if readu32("arm9_overlays/ov117.bin", 0x562) != 0x90002001
                .error "Modified native target: Ov117_RocketAdminPortraitLoad_Hook + 0x0"
            .endif
            .if readu32("arm9_overlays/ov117.bin", 0x566) != 0x22057870
                .error "Modified native target: Ov117_RocketAdminPortraitLoad_Hook + 0x4"
            .endif
            .if readu32("arm9_overlays/ov117.bin", 0x56A) != 0x90010192
                .error "Modified native target: Ov117_RocketAdminPortraitLoad_Hook + 0x8"
            .endif
            .if readu32("arm9_overlays/ov117.bin", 0x56E) != 0x1D2178B0
                .error "Modified native target: Ov117_RocketAdminPortraitLoad_Hook + 0xC"
            .endif
            .if readu32("arm9_overlays/ov117.bin", 0x572) != 0x900218A2
                .error "Modified native target: Ov117_RocketAdminPortraitLoad_Hook + 0x10"
            .endif
            .if readu32("arm9_overlays/ov117.bin", 0x576) != 0x900378F0
                .error "Modified native target: Ov117_RocketAdminPortraitLoad_Hook + 0x14"
            .endif
            .if readu32("arm9_overlays/ov117.bin", 0x57A) != 0x900448CF
                .error "Modified native target: Ov117_RocketAdminPortraitLoad_Hook + 0x18"
            .endif
            .if readu32("arm9_overlays/ov117.bin", 0x57E) != 0x6A287833
                .error "Modified native target: Ov117_RocketAdminPortraitLoad_Hook + 0x1C"
            .endif
            .if readu32("arm9_overlays/ov117.bin", 0x582) != 0xF837F791
                .error "Modified native target: Ov117_RocketAdminPortraitLoad_Hook + 0x20"
            .endif
            .if readu32("arm9_overlays/ov117.bin", 0x61A) != 0x90002000
                .error "Modified native target: Ov117_RocketAdminBg1Load_Hook + 0x0"
            .endif
            .if readu32("arm9_overlays/ov117.bin", 0x61E) != 0x90012002
                .error "Modified native target: Ov117_RocketAdminBg1Load_Hook + 0x4"
            .endif
            .if readu32("arm9_overlays/ov117.bin", 0x622) != 0x21D96928
                .error "Modified native target: Ov117_RocketAdminBg1Load_Hook + 0x8"
            .endif
            .if readu32("arm9_overlays/ov117.bin", 0x626) != 0x22D86880
                .error "Modified native target: Ov117_RocketAdminBg1Load_Hook + 0xC"
            .endif
            .if readu32("arm9_overlays/ov117.bin", 0x62A) != 0x20019002
                .error "Modified native target: Ov117_RocketAdminBg1Load_Hook + 0x10"
            .endif
            .if readu32("arm9_overlays/ov117.bin", 0x62E) != 0x6A289003
                .error "Modified native target: Ov117_RocketAdminBg1Load_Hook + 0x14"
            .endif
            .if readu32("arm9_overlays/ov117.bin", 0x632) != 0xF79023D7
                .error "Modified native target: Ov117_RocketAdminBg1Load_Hook + 0x18"
            .endif
            .if readu16("arm9_overlays/ov117.bin", 0x636) != 0xFEFE
                .error "Modified native target: Ov117_RocketAdminBg1Load_Hook + 0x1C"
            .endif
            .if readu32("arm9_overlays/ov117.bin", 0x6A2) != 0x90002000
                .error "Modified native target: Ov117_RocketAdminBg3Load_Hook + 0x0"
            .endif
            .if readu32("arm9_overlays/ov117.bin", 0x6A6) != 0x90012002
                .error "Modified native target: Ov117_RocketAdminBg3Load_Hook + 0x4"
            .endif
            .if readu32("arm9_overlays/ov117.bin", 0x6AA) != 0x21DA6928
                .error "Modified native target: Ov117_RocketAdminBg3Load_Hook + 0x8"
            .endif
            .if readu32("arm9_overlays/ov117.bin", 0x6AE) != 0x22D86880
                .error "Modified native target: Ov117_RocketAdminBg3Load_Hook + 0xC"
            .endif
            .if readu32("arm9_overlays/ov117.bin", 0x6B2) != 0x20039002
                .error "Modified native target: Ov117_RocketAdminBg3Load_Hook + 0x10"
            .endif
            .if readu32("arm9_overlays/ov117.bin", 0x6B6) != 0x6A289003
                .error "Modified native target: Ov117_RocketAdminBg3Load_Hook + 0x14"
            .endif
            .if readu32("arm9_overlays/ov117.bin", 0x6BA) != 0xF79023D7
                .error "Modified native target: Ov117_RocketAdminBg3Load_Hook + 0x18"
            .endif
            .if readu16("arm9_overlays/ov117.bin", 0x6BE) != 0xFEBA
                .error "Modified native target: Ov117_RocketAdminBg3Load_Hook + 0x1C"
            .endif
            .if readu32("arm9_overlays/ov117.bin", 0x800) != 0x21011C28
                .error "Modified native target: Ov117_RocketAdminLaterBgLoad_Hook + 0x0"
            .endif
            .if readu32("arm9_overlays/ov117.bin", 0x804) != 0xF7FF22DD
                .error "Modified native target: Ov117_RocketAdminLaterBgLoad_Hook + 0x4"
            .endif
            .if readu16("arm9_overlays/ov117.bin", 0x808) != 0xFDFB
                .error "Modified native target: Ov117_RocketAdminLaterBgLoad_Hook + 0x8"
            .endif
        .endif
    .endif

    .if fileexists("arm9_overlays/ov118.bin") == 0
        .error "Missing installation target: arm9_overlays/ov118.bin"
    .else
        .if filesize("arm9_overlays/ov118.bin") < 0xCA
            .error "Installation target is too short: arm9_overlays/ov118.bin"
        .else
            .if readu32("arm9_overlays/ov118.bin", 0xAA) != 0x90002000
                .error "Modified native target: Ov118_KimonoShutterLoad_Hook + 0x0"
            .endif
            .if readu32("arm9_overlays/ov118.bin", 0xAE) != 0x69209001
                .error "Modified native target: Ov118_KimonoShutterLoad_Hook + 0x4"
            .endif
            .if readu32("arm9_overlays/ov118.bin", 0xB2) != 0x688021A8
                .error "Modified native target: Ov118_KimonoShutterLoad_Hook + 0x8"
            .endif
            .if readu32("arm9_overlays/ov118.bin", 0xB6) != 0x900222A7
                .error "Modified native target: Ov118_KimonoShutterLoad_Hook + 0xC"
            .endif
            .if readu32("arm9_overlays/ov118.bin", 0xBA) != 0x90032001
                .error "Modified native target: Ov118_KimonoShutterLoad_Hook + 0x10"
            .endif
            .if readu32("arm9_overlays/ov118.bin", 0xBE) != 0x90042003
                .error "Modified native target: Ov118_KimonoShutterLoad_Hook + 0x14"
            .endif
            .if readu32("arm9_overlays/ov118.bin", 0xC2) != 0x23A66A20
                .error "Modified native target: Ov118_KimonoShutterLoad_Hook + 0x18"
            .endif
            .if readu32("arm9_overlays/ov118.bin", 0xC6) != 0xFA0BF791
                .error "Modified native target: Ov118_KimonoShutterLoad_Hook + 0x1C"
            .endif
        .endif
    .endif

    .if fileexists("arm9_overlays/ov120.bin") == 0
        .error "Missing installation target: arm9_overlays/ov120.bin"
    .else
        .if filesize("arm9_overlays/ov120.bin") < 0x79E
            .error "Installation target is too short: arm9_overlays/ov120.bin"
        .else
            .if readu32("arm9_overlays/ov120.bin", 0x786) != 0x20002306
                .error "Modified native target: Ov120_RedBlockLoad_Hook + 0x0"
            .endif
            .if readu32("arm9_overlays/ov120.bin", 0x78A) != 0x9000019B
                .error "Modified native target: Ov120_RedBlockLoad_Hook + 0x4"
            .endif
            .if readu32("arm9_overlays/ov120.bin", 0x78E) != 0x1C2218E1
                .error "Modified native target: Ov120_RedBlockLoad_Hook + 0x8"
            .endif
            .if readu32("arm9_overlays/ov120.bin", 0x792) != 0x6A283B34
                .error "Modified native target: Ov120_RedBlockLoad_Hook + 0xC"
            .endif
            .if readu32("arm9_overlays/ov120.bin", 0x796) != 0x18E33210
                .error "Modified native target: Ov120_RedBlockLoad_Hook + 0x10"
            .endif
            .if readu32("arm9_overlays/ov120.bin", 0x79A) != 0xF90BF000
                .error "Modified native target: Ov120_RedBlockLoad_Hook + 0x14"
            .endif
        .endif
    .endif

    .if fileexists("arm9_overlays/ov080.bin") == 0
        .error "Missing installation target: arm9_overlays/ov080.bin"
    .else
        .if filesize("arm9_overlays/ov080.bin") < 0x10B24
            .error "Installation target is too short: arm9_overlays/ov080.bin"
        .else
            .if readu32("arm9_overlays/ov080.bin", 0x4174) != 0x0000047B
                .error "Modified native target: Ov80_FrontierBrainPresentationMusic + 0x0"
            .endif
            .if readu16("arm9_overlays/ov080.bin", 0x10386) != 0x7938
                .error "Modified native target: Ov80_FrontierPortraitPalette_Load + 0x0"
            .endif
            .if readu16("arm9_overlays/ov080.bin", 0x103C4) != 0x797B
                .error "Modified native target: Ov80_FrontierPortraitImage_Load + 0x0"
            .endif
            .if readu32("arm9_overlays/ov080.bin", 0x103CC) != 0xF92AF5D3
                .error "Modified native target: Ov80_FrontierPortraitImageCall_Hook + 0x0"
            .endif
            .if readu16("arm9_overlays/ov080.bin", 0x103D8) != 0x79BB
                .error "Modified native target: Ov80_FrontierPortraitCell_Load + 0x0"
            .endif
            .if readu32("arm9_overlays/ov080.bin", 0x103E0) != 0xFA14F5D3
                .error "Modified native target: Ov80_FrontierPortraitCellCall_Hook + 0x0"
            .endif
            .if readu16("arm9_overlays/ov080.bin", 0x103EC) != 0x79FB
                .error "Modified native target: Ov80_FrontierPortraitAnim_Load + 0x0"
            .endif
            .if readu32("arm9_overlays/ov080.bin", 0x10432) != 0xF9BBF5D3
                .error "Modified native target: Ov80_FrontierSymbolPaletteCall_Hook + 0x0"
            .endif
            .if readu32("arm9_overlays/ov080.bin", 0x1045A) != 0xF8E3F5D3
                .error "Modified native target: Ov80_FrontierSymbolCharCall_Hook + 0x0"
            .endif
            .if readu32("arm9_overlays/ov080.bin", 0x1046E) != 0xF9CDF5D3
                .error "Modified native target: Ov80_FrontierSymbolCellCall_Hook + 0x0"
            .endif
            .if readu32("arm9_overlays/ov080.bin", 0x10482) != 0xF9DBF5D3
                .error "Modified native target: Ov80_FrontierSymbolAnimCall_Hook + 0x0"
            .endif
            .if readu16("arm9_overlays/ov080.bin", 0x10AC8) != 0x7A22
                .error "Modified native target: Ov80_FrontierBackgroundPalette_Load + 0x0"
            .endif
            .if readu16("arm9_overlays/ov080.bin", 0x10AE0) != 0x7A61
                .error "Modified native target: Ov80_FrontierBackgroundImage_Load + 0x0"
            .endif
            .if readu16("arm9_overlays/ov080.bin", 0x10AF8) != 0x7AA1
                .error "Modified native target: Ov80_FrontierBackgroundScreen_Load + 0x0"
            .endif
            .if readu16("arm9_overlays/ov080.bin", 0x10B22) != 0x7A21
                .error "Modified native target: Ov80_FrontierBackgroundPalette_Copy + 0x0"
            .endif
            .if readu32("arm9_overlays/ov080.bin", 0x101B0) != 0x312C1C21
                .error "Modified native target: Ov80_FrontierRowCall_Hook + 0x0"
            .endif
            .if readu32("arm9_overlays/ov080.bin", 0x101B4) != 0x210C780A
                .error "Modified native target: Ov80_FrontierRowCall_Hook + 0x4"
            .endif
            .if readu32("arm9_overlays/ov080.bin", 0x101B8) != 0x43514B07
                .error "Modified native target: Ov80_FrontierRowCall_Hook + 0x8"
            .endif
            .if readu32("arm9_overlays/ov080.bin", 0x101BC) != 0x18591C20
                .error "Modified native target: Ov80_FrontierRowCall_Hook + 0xC"
            .endif
            .if readu32("arm9_overlays/ov080.bin", 0x101C0) != 0xFC4AF000
                .error "Modified native target: Ov80_FrontierRowCall_Hook + 0x10"
            .endif
            .if readu32("arm9_overlays/ov080.bin", 0x10264) != 0x1C0CB570
                .error "Modified native target: Ov80_FrontierMainTask_Hook + 0x0"
            .endif
            .if readu32("arm9_overlays/ov080.bin", 0x10268) != 0x332C1C23
                .error "Modified native target: Ov80_FrontierMainTask_Hook + 0x4"
            .endif
            .if readu32("arm9_overlays/ov080.bin", 0x1026C) != 0x230C781E
                .error "Modified native target: Ov80_FrontierMainTask_Hook + 0x8"
            .endif
            .if readu32("arm9_overlays/ov080.bin", 0x10270) != 0x43734A07
                .error "Modified native target: Ov80_FrontierMainTask_Hook + 0xC"
            .endif
            .if readu32("arm9_overlays/ov080.bin", 0x10274) != 0x1C201C05
                .error "Modified native target: Ov80_FrontierMainTask_Hook + 0x10"
            .endif
            .if readu32("arm9_overlays/ov080.bin", 0x10278) != 0x18D22165
                .error "Modified native target: Ov80_FrontierMainTask_Hook + 0x14"
            .endif
            .if readu32("arm9_overlays/ov080.bin", 0x1027C) != 0xF80AF000
                .error "Modified native target: Ov80_FrontierMainTask_Hook + 0x18"
            .endif
            .if readu32("arm9_overlays/ov080.bin", 0x10280) != 0xD1032801
                .error "Modified native target: Ov80_FrontierMainTask_Hook + 0x1C"
            .endif
            .if readu32("arm9_overlays/ov080.bin", 0x10284) != 0x1C291C20
                .error "Modified native target: Ov80_FrontierMainTask_Hook + 0x20"
            .endif
            .if readu32("arm9_overlays/ov080.bin", 0x10288) != 0xFFAAF7FF
                .error "Modified native target: Ov80_FrontierMainTask_Hook + 0x24"
            .endif
            .if readu32("arm9_overlays/ov080.bin", 0x1028C) != 0x46C0BD70
                .error "Modified native target: Ov80_FrontierMainTask_Hook + 0x28"
            .endif
            .if readu32("arm9_overlays/ov080.bin", 0x10290) != 0x0223DB98
                .error "Modified native target: Ov80_FrontierMainTask_Hook + 0x2C"
            .endif
        .endif
    .endif

    .if fileexists("arm9_overlays/ov001.bin") == 0
        .error "Missing installation target: arm9_overlays/ov001.bin"
    .else
        .if filesize("arm9_overlays/ov001.bin") < 0xA240
            .error "Installation target is too short: arm9_overlays/ov001.bin"
        .else
            .if readu32("arm9_overlays/ov001.bin", 0xA238) != 0x4A08B508
                .error "Modified native target: Ov01_EffectDispatch_Hook + 0x0"
            .endif
            .if readu32("arm9_overlays/ov001.bin", 0xA23C) != 0x00936852
                .error "Modified native target: Ov01_EffectDispatch_Hook + 0x4"
            .endif
        .endif
    .endif

    .if fileexists(INJECT_FILE) == 0
        .error "Missing configured TCM payload container"
    .else
        .if filesize(INJECT_FILE) < INJECT_REQUIRED_FILE_SIZE
            .error "Configured TCM payload does not fit in its derived container file"
        .else
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x10) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x18) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x20) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x28) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x30) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x38) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x40) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x48) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x50) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x58) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x60) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x68) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x70) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x78)) != 0
                .error "TCM payload first-install footprint is not empty near +0x0"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x80) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x88) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x90) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x98) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xA0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xA8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xB0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xB8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xC0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xC8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xD0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xD8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xE0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xE8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xF0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xF8)) != 0
                .error "TCM payload first-install footprint is not empty near +0x80"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x100) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x108) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x110) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x118) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x120) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x128) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x130) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x138) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x140) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x148) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x150) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x158) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x160) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x168) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x170) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x178)) != 0
                .error "TCM payload first-install footprint is not empty near +0x100"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x180) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x188) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x190) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x198) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1A0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1A8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1B0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1B8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1C0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1C8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1D0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1D8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1E0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1E8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1F0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1F8)) != 0
                .error "TCM payload first-install footprint is not empty near +0x180"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x200) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x208) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x210) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x218) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x220) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x228) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x230) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x238) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x240) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x248) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x250) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x258) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x260) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x268) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x270) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x278)) != 0
                .error "TCM payload first-install footprint is not empty near +0x200"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x280) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x288) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x290) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x298) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x2A0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x2A8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x2B0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x2B8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x2C0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x2C8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x2D0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x2D8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x2E0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x2E8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x2F0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x2F8)) != 0
                .error "TCM payload first-install footprint is not empty near +0x280"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x300) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x308) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x310) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x318) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x320) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x328) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x330) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x338) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x340) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x348) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x350) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x358) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x360) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x368) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x370) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x378)) != 0
                .error "TCM payload first-install footprint is not empty near +0x300"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x380) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x388) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x390) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x398) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x3A0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x3A8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x3B0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x3B8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x3C0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x3C8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x3D0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x3D8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x3E0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x3E8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x3F0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x3F8)) != 0
                .error "TCM payload first-install footprint is not empty near +0x380"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x400) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x408) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x410) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x418) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x420) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x428) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x430) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x438) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x440) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x448) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x450) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x458) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x460) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x468) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x470) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x478)) != 0
                .error "TCM payload first-install footprint is not empty near +0x400"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x480) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x488) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x490) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x498) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x4A0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x4A8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x4B0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x4B8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x4C0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x4C8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x4D0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x4D8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x4E0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x4E8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x4F0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x4F8)) != 0
                .error "TCM payload first-install footprint is not empty near +0x480"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x500) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x508) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x510) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x518) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x520) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x528) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x530) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x538) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x540) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x548) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x550) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x558) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x560) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x568) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x570) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x578)) != 0
                .error "TCM payload first-install footprint is not empty near +0x500"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x580) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x588) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x590) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x598) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x5A0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x5A8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x5B0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x5B8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x5C0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x5C8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x5D0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x5D8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x5E0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x5E8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x5F0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x5F8)) != 0
                .error "TCM payload first-install footprint is not empty near +0x580"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x600) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x608) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x610) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x618) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x620) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x628) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x630) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x638) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x640) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x648) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x650) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x658) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x660) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x668) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x670) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x678)) != 0
                .error "TCM payload first-install footprint is not empty near +0x600"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x680) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x688) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x690) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x698) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x6A0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x6A8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x6B0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x6B8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x6C0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x6C8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x6D0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x6D8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x6E0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x6E8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x6F0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x6F8)) != 0
                .error "TCM payload first-install footprint is not empty near +0x680"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x700) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x708) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x710) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x718) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x720) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x728) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x730) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x738) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x740) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x748) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x750) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x758) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x760) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x768) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x770) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x778)) != 0
                .error "TCM payload first-install footprint is not empty near +0x700"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x780) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x788) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x790) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x798) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x7A0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x7A8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x7B0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x7B8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x7C0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x7C8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x7D0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x7D8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x7E0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x7E8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x7F0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x7F8)) != 0
                .error "TCM payload first-install footprint is not empty near +0x780"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x800) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x808) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x810) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x818) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x820) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x828) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x830) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x838) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x840) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x848) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x850) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x858) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x860) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x868) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x870) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x878)) != 0
                .error "TCM payload first-install footprint is not empty near +0x800"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x880) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x888) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x890) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x898) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x8A0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x8A8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x8B0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x8B8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x8C0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x8C8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x8D0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x8D8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x8E0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x8E8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x8F0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x8F8)) != 0
                .error "TCM payload first-install footprint is not empty near +0x880"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x900) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x908) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x910) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x918) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x920) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x928) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x930) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x938) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x940) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x948) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x950) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x958) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x960) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x968) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x970) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x978)) != 0
                .error "TCM payload first-install footprint is not empty near +0x900"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x980) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x988) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x990) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x998) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x9A0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x9A8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x9B0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x9B8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x9C0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x9C8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x9D0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x9D8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x9E0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x9E8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x9F0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x9F8)) != 0
                .error "TCM payload first-install footprint is not empty near +0x980"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xA00) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xA08) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xA10) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xA18) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xA20) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xA28) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xA30) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xA38) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xA40) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xA48) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xA50) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xA58) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xA60) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xA68) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xA70) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xA78)) != 0
                .error "TCM payload first-install footprint is not empty near +0xA00"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xA80) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xA88) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xA90) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xA98) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xAA0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xAA8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xAB0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xAB8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xAC0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xAC8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xAD0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xAD8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xAE0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xAE8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xAF0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xAF8)) != 0
                .error "TCM payload first-install footprint is not empty near +0xA80"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xB00) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xB08) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xB10) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xB18) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xB20) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xB28) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xB30) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xB38) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xB40) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xB48) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xB50) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xB58) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xB60) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xB68) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xB70) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xB78)) != 0
                .error "TCM payload first-install footprint is not empty near +0xB00"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xB80) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xB88) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xB90) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xB98) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xBA0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xBA8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xBB0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xBB8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xBC0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xBC8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xBD0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xBD8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xBE0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xBE8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xBF0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xBF8)) != 0
                .error "TCM payload first-install footprint is not empty near +0xB80"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xC00) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xC08) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xC10) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xC18) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xC20) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xC28) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xC30) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xC38) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xC40) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xC48) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xC50) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xC58) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xC60) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xC68) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xC70) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xC78)) != 0
                .error "TCM payload first-install footprint is not empty near +0xC00"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xC80) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xC88) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xC90) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xC98) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xCA0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xCA8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xCB0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xCB8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xCC0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xCC8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xCD0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xCD8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xCE0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xCE8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xCF0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xCF8)) != 0
                .error "TCM payload first-install footprint is not empty near +0xC80"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xD00) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xD08) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xD10) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xD18) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xD20) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xD28) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xD30) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xD38) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xD40) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xD48) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xD50) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xD58) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xD60) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xD68) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xD70) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xD78)) != 0
                .error "TCM payload first-install footprint is not empty near +0xD00"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xD80) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xD88) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xD90) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xD98) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xDA0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xDA8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xDB0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xDB8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xDC0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xDC8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xDD0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xDD8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xDE0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xDE8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xDF0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xDF8)) != 0
                .error "TCM payload first-install footprint is not empty near +0xD80"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xE00) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xE08) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xE10) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xE18) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xE20) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xE28) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xE30) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xE38) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xE40) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xE48) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xE50) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xE58) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xE60) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xE68) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xE70) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xE78)) != 0
                .error "TCM payload first-install footprint is not empty near +0xE00"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xE80) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xE88) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xE90) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xE98) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xEA0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xEA8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xEB0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xEB8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xEC0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xEC8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xED0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xED8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xEE0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xEE8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xEF0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xEF8)) != 0
                .error "TCM payload first-install footprint is not empty near +0xE80"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xF00) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xF08) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xF10) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xF18) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xF20) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xF28) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xF30) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xF38) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xF40) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xF48) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xF50) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xF58) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xF60) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xF68) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xF70) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xF78)) != 0
                .error "TCM payload first-install footprint is not empty near +0xF00"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xF80) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xF88) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xF90) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xF98) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xFA0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xFA8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xFB0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xFB8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xFC0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xFC8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xFD0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xFD8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xFE0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xFE8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xFF0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0xFF8)) != 0
                .error "TCM payload first-install footprint is not empty near +0xF80"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1000) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1008) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1010) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1018) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1020) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1028) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1030) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1038) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1040) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1048) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1050) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1058) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1060) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1068) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1070) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1078)) != 0
                .error "TCM payload first-install footprint is not empty near +0x1000"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1080) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1088) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1090) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1098) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x10A0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x10A8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x10B0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x10B8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x10C0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x10C8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x10D0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x10D8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x10E0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x10E8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x10F0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x10F8)) != 0
                .error "TCM payload first-install footprint is not empty near +0x1080"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1100) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1108) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1110) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1118) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1120) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1128) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1130) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1138) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1140) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1148) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1150) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1158) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1160) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1168) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1170) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1178)) != 0
                .error "TCM payload first-install footprint is not empty near +0x1100"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1180) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1188) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1190) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1198) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x11A0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x11A8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x11B0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x11B8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x11C0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x11C8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x11D0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x11D8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x11E0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x11E8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x11F0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x11F8)) != 0
                .error "TCM payload first-install footprint is not empty near +0x1180"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1200) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1208) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1210) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1218) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1220) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1228) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1230) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1238) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1240) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1248) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1250) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1258) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1260) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1268) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1270) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1278)) != 0
                .error "TCM payload first-install footprint is not empty near +0x1200"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1280) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1288) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1290) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1298) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x12A0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x12A8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x12B0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x12B8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x12C0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x12C8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x12D0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x12D8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x12E0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x12E8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x12F0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x12F8)) != 0
                .error "TCM payload first-install footprint is not empty near +0x1280"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1300) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1308) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1310) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1318) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1320) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1328) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1330) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1338) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1340) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1348) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1350) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1358) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1360) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1368) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1370) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1378)) != 0
                .error "TCM payload first-install footprint is not empty near +0x1300"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1380) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1388) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1390) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1398) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x13A0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x13A8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x13B0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x13B8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x13C0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x13C8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x13D0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x13D8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x13E0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x13E8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x13F0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x13F8)) != 0
                .error "TCM payload first-install footprint is not empty near +0x1380"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1400) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1408) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1410) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1418) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1420) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1428) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1430) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1438) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1440) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1448) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1450) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1458) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1460) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1468) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1470) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1478)) != 0
                .error "TCM payload first-install footprint is not empty near +0x1400"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1480) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1488) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1490) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1498) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x14A0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x14A8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x14B0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x14B8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x14C0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x14C8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x14D0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x14D8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x14E0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x14E8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x14F0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x14F8)) != 0
                .error "TCM payload first-install footprint is not empty near +0x1480"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1500) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1508) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1510) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1518) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1520) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1528) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1530) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1538) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1540) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1548) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1550) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1558) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1560) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1568) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1570) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1578)) != 0
                .error "TCM payload first-install footprint is not empty near +0x1500"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1580) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1588) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1590) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1598) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x15A0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x15A8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x15B0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x15B8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x15C0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x15C8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x15D0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x15D8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x15E0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x15E8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x15F0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x15F8)) != 0
                .error "TCM payload first-install footprint is not empty near +0x1580"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1600) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1608) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1610) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1618) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1620) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1628) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1630) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1638) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1640) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1648) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1650) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1658) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1660) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1668) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1670) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1678)) != 0
                .error "TCM payload first-install footprint is not empty near +0x1600"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1680) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1688) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1690) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1698) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x16A0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x16A8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x16B0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x16B8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x16C0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x16C8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x16D0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x16D8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x16E0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x16E8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x16F0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x16F8)) != 0
                .error "TCM payload first-install footprint is not empty near +0x1680"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1700) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1708) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1710) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1718) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1720) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1728) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1730) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1738) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1740) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1748) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1750) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1758) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1760) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1768) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1770) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1778)) != 0
                .error "TCM payload first-install footprint is not empty near +0x1700"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1780) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1788) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1790) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1798) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x17A0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x17A8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x17B0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x17B8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x17C0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x17C8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x17D0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x17D8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x17E0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x17E8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x17F0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x17F8)) != 0
                .error "TCM payload first-install footprint is not empty near +0x1780"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1800) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1808) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1810) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1818) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1820) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1828) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1830) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1838) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1840) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1848) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1850) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1858) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1860) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1868) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1870) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1878)) != 0
                .error "TCM payload first-install footprint is not empty near +0x1800"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1880) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1888) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1890) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1898) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x18A0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x18A8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x18B0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x18B8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x18C0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x18C8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x18D0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x18D8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x18E0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x18E8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x18F0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x18F8)) != 0
                .error "TCM payload first-install footprint is not empty near +0x1880"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1900) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1908) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1910) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1918) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1920) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1928) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1930) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1938) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1940) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1948) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1950) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1958) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1960) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1968) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1970) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1978)) != 0
                .error "TCM payload first-install footprint is not empty near +0x1900"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1980) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1988) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1990) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1998) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x19A0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x19A8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x19B0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x19B8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x19C0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x19C8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x19D0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x19D8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x19E0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x19E8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x19F0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x19F8)) != 0
                .error "TCM payload first-install footprint is not empty near +0x1980"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1A00) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1A08) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1A10) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1A18) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1A20) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1A28) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1A30) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1A38) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1A40) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1A48) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1A50) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1A58) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1A60) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1A68) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1A70) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1A78)) != 0
                .error "TCM payload first-install footprint is not empty near +0x1A00"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1A80) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1A88) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1A90) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1A98) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1AA0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1AA8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1AB0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1AB8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1AC0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1AC8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1AD0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1AD8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1AE0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1AE8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1AF0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1AF8)) != 0
                .error "TCM payload first-install footprint is not empty near +0x1A80"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1B00) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1B08) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1B10) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1B18) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1B20) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1B28) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1B30) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1B38) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1B40) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1B48) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1B50) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1B58) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1B60) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1B68) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1B70) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1B78)) != 0
                .error "TCM payload first-install footprint is not empty near +0x1B00"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1B80) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1B88) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1B90) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1B98) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1BA0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1BA8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1BB0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1BB8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1BC0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1BC8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1BD0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1BD8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1BE0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1BE8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1BF0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1BF8)) != 0
                .error "TCM payload first-install footprint is not empty near +0x1B80"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1C00) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1C08) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1C10) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1C18) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1C20) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1C28) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1C30) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1C38) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1C40) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1C48) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1C50) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1C58) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1C60) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1C68) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1C70) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1C78)) != 0
                .error "TCM payload first-install footprint is not empty near +0x1C00"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1C80) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1C88) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1C90) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1C98) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1CA0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1CA8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1CB0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1CB8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1CC0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1CC8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1CD0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1CD8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1CE0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1CE8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1CF0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1CF8)) != 0
                .error "TCM payload first-install footprint is not empty near +0x1C80"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1D00) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1D08) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1D10) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1D18) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1D20) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1D28) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1D30) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1D38) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1D40) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1D48) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1D50) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1D58) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1D60) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1D68) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1D70) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1D78)) != 0
                .error "TCM payload first-install footprint is not empty near +0x1D00"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1D80) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1D88) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1D90) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1D98) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1DA0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1DA8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1DB0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1DB8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1DC0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1DC8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1DD0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1DD8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1DE0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1DE8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1DF0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1DF8)) != 0
                .error "TCM payload first-install footprint is not empty near +0x1D80"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1E00) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1E08) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1E10) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1E18) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1E20) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1E28) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1E30) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1E38) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1E40) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1E48) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1E50) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1E58) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1E60) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1E68) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1E70) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1E78)) != 0
                .error "TCM payload first-install footprint is not empty near +0x1E00"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1E80) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1E88) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1E90) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1E98) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1EA0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1EA8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1EB0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1EB8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1EC0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1EC8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1ED0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1ED8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1EE0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1EE8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1EF0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1EF8)) != 0
                .error "TCM payload first-install footprint is not empty near +0x1E80"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1F00) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1F08) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1F10) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1F18) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1F20) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1F28) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1F30) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1F38) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1F40) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1F48) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1F50) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1F58) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1F60) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1F68) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1F70) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1F78)) != 0
                .error "TCM payload first-install footprint is not empty near +0x1F00"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1F80) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1F88) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1F90) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1F98) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1FA0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1FA8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1FB0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1FB8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1FC0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1FC8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1FD0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1FD8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1FE0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1FE8) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1FF0) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x1FF8)) != 0
                .error "TCM payload first-install footprint is not empty near +0x1F80"
            .endif
            .if (readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x2000) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x2008) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x2010) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x2018) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x2020) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x2028) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x2030) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x2038) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x2040) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x2048) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x2050) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x2058) | readu64(INJECT_FILE, INJECT_ADDR - INJECT_FILE_BASE + 0x2060)) != 0
                .error "TCM payload first-install footprint is not empty near +0x2000"
            .endif
        .endif
    .endif
.endif


; =============================================================================
; Native addresses and constants
; =============================================================================

ReadFromNarcMemberByIdPair             equ 0x02007560
TrainerData_GetAttr                    equ 0x02073470
TrainerData_ReadTrData                 equ 0x020735D8
GF_AssertFail                          equ 0x0202551C
BattleSetup_GetTransitionAndMusicParam equ 0x02051738
BattleStartGetTransition               equ 0x020517A4
BattleStartGetMusic                    equ 0x020517D0
Ov01_DynamicBattleTransitionSelector   equ 0x021F0D20
Ov01_LoadBgFromOpenNarc                equ 0x021F0454
Ov01_LoadKimonoShuttersFromOpenNarc    equ 0x021F0500
Ov01_LoadSpriteResourcesFromOpenNarc   equ 0x021F0614
AddCharResObjFromOpenNarcWithAtEndFlag equ 0x0200A424
AddCharResObjFromOpenNarc              equ 0x0200A3C8
AddPlttResObjFromOpenNarc              equ 0x0200A480
AddCellOrAnimResObjFromOpenNarc        equ 0x0200A540
GF2DGfxResObjExistsById                equ 0x0200A728
DestroySingle2DGfxResObj               equ 0x0200A75C
RegisterLoadedResources                equ 0x0200DAE4
ObjCharRes_Transfer                    equ 0x0200ADA4
SpriteTransfer_CreatePlttTransferTask  equ 0x0200B00C
SpriteTransfer_DeletePlttTransferTask  equ 0x0200B0A8
SpriteTransfer_GetPaletteProxy         equ 0x0200B0F8
SpriteSystem_LoadPaletteBufferFromOpenNarc equ 0x0200D68C
SpriteSystem_LoadAnimResObjFromOpenNarc    equ 0x0200D71C
ObjCharRes_DropRawData                 equ 0x0200A740
ObjPlttRes_Transfer                    equ 0x0200B00C
CreateSpriteResourcesHeader            equ 0x02009D48
Ov117_LoadRectScreenFromOpenNarc       equ 0x0225F420
Ov120_LoadSharedSpriteFromOpenNarc     equ 0x0225F9D4
Heap_Alloc                             equ 0x0201AA8C
Heap_Free                              equ 0x0201AB0C
memset                                 equ 0x020E5B44
GfGfx_EngineATogglePlanes              equ 0x02022C60
SpriteList_RenderAndAnimateSprites     equ 0x0202457C
Sprite_Delete                          equ 0x02024758
Sprite_SetDrawFlag                     equ 0x02024830
Sprite_SetOamMode                      equ 0x02024B78
GfGfxLoader_GetPlttData                equ 0x020079F4
GfGfxLoader_GetCellBankFromOpenNarc    equ 0x02007C60
BlendPalette                           equ 0x02003DE8
Sprite_GetPaletteProxy                 equ 0x02024B34
; The following entries are Ghidra-confirmed ARM targets; Thumb callers use BLX.
DC_FlushRange                          equ 0x020D2894
NNS_G2dGetImagePaletteLocation         equ 0x020B8078
GX_LoadOBJPltt                         equ 0x020CFD18

NARC_a_1_5_5                           equ 155

TCM_GENDER_OFFSET                      equ 0x00
TCM_GENDER_SIZE                        equ 2
TCM_PRIZE_OFFSET                       equ 0x02
TCM_PRIZE_SIZE                         equ 2
TCM_EYE_MUSIC_MAIN_OFFSET              equ 0x04
TCM_EYE_MUSIC_ALT_OFFSET               equ 0x06
TCM_EYE_MUSIC_SIZE                     equ 2
TCM_BATTLE_MUSIC_OFFSET                equ 0x08
TCM_ROUTE_METADATA_OFFSET              equ TCM_BATTLE_MUSIC_OFFSET
TCM_ROUTE_METADATA_SIZE                equ 0x04
TCM_ROUTE_STYLE_RELATIVE_OFFSET        equ 0x02
TCM_VS_STYLE_OFFSET                    equ 0x0A
TCM_VS_STYLE_SIZE                      equ 2
TCM_VS_STYLE_20BYTE                    equ 1
TCM_VS_STYLE_8BYTE                     equ 2
TCM_VS_STYLE_ROCKET_ADMIN              equ 3
TCM_VS_STYLE_KIMONO                    equ 4
TCM_VS_STYLE_RED                       equ 5
TCM_VS_STYLE_ROCKET_GRUNT              equ 6
TCM_VS_STYLE_NORMAL_EARLY              equ 7
TCM_VS_STYLE_NORMAL_LATE               equ 8
TCM_VS_STYLE_WATER_EARLY               equ 9
TCM_VS_STYLE_WATER_LATE                equ 10
TCM_VS_STYLE_CAVE_EARLY                equ 11
TCM_VS_STYLE_CAVE_LATE                 equ 12
TCM_VS_STYLE_FRONTIER_BRAIN            equ 13
TCM_TRAINER_NAME_OFFSET                equ 0x0C
TCM_TRAINER_NAME_SIZE                  equ 0x02
TCM_STYLE1_SAVED_RIVAL_BOOL_OFFSET     equ 0x0E
TCM_STYLE1_SAVED_RIVAL_BOOL_SIZE       equ 0x01
TCM_STYLE1_MOTION_OFFSET               equ 0x10
TCM_STYLE1_MOTION_SIZE                 equ 0x04
TCM_STYLE2_DURATION_OFFSET             equ 0x14
TCM_STYLE2_DURATION_SIZE               equ 0x02
TCM_ASSET_GROUP1_QUARTET_OFFSET        equ 0x16
TCM_ASSET_GROUP1_QUARTET_SIZE          equ 0x08
TCM_ASSET_GROUP1_PALETTE_IMAGE_SIZE    equ 0x04
TCM_ASSET_GROUP1_RCSN1_OFFSET          equ 0x1E
TCM_ASSET_GROUP1_RCSN2_OFFSET          equ 0x20
TCM_ASSET_GROUP1_RCSN3_OFFSET          equ 0x22
TCM_ASSET_GROUP1_RCSN_SIZE             equ 0x02
TCM_ASSET_GROUP2_QUARTET_OFFSET        equ 0x24
TCM_ASSET_GROUP2_QUARTET_SIZE          equ 0x08
TCM_ASSET_GROUP3_QUARTET_OFFSET        equ 0x2C
TCM_ASSET_GROUP3_QUARTET_SIZE          equ 0x08
TCM_STYLE0_SMALL_ASSET_OFFSET          equ TCM_ASSET_GROUP1_QUARTET_OFFSET
TCM_STYLE0_LARGE_ASSET_OFFSET          equ TCM_ASSET_GROUP2_QUARTET_OFFSET
TCM_STYLE0_ASSET_SIZE                  equ 0x08
TCM_ROCKET_BG_SCRATCH_BG1_SCREEN       equ 0x08
TCM_ROCKET_BG_SCRATCH_BG1_CHAR         equ 0x0A
TCM_ROCKET_BG_SCRATCH_BG1_PALETTE      equ 0x0C
TCM_ROCKET_BG_SCRATCH_BG3_SCREEN       equ 0x0E
TCM_ROCKET_BG_SCRATCH_BG3_CHAR         equ 0x10
TCM_ROCKET_BG_SCRATCH_BG3_PALETTE      equ 0x12
TCM_ROCKET_BG_SCRATCH_LATER_SCREEN     equ 0x14
TCM_ROCKET_PORTRAIT_WIDE_SCRATCH0      equ 0x00
TCM_ROCKET_PORTRAIT_WIDE_SCRATCH1      equ 0x02
TCM_ROCKET_PORTRAIT_WIDE_SCRATCH2      equ 0x04
TCM_ROCKET_PORTRAIT_WIDE_SCRATCH3      equ 0x06
TCM_FRONTIER_SCRATCH_NAME              equ 0x00
TCM_FRONTIER_SCRATCH_PORTRAIT0         equ 0x04
TCM_FRONTIER_SCRATCH_PORTRAIT1         equ 0x06
TCM_FRONTIER_SCRATCH_PORTRAIT2         equ 0x08
TCM_FRONTIER_SCRATCH_PORTRAIT3         equ 0x0A
TCM_FRONTIER_SCRATCH_BG_PALETTE        equ 0x0C
TCM_FRONTIER_SCRATCH_BG_IMAGE          equ 0x0E
TCM_FRONTIER_SCRATCH_BG_SCREEN         equ 0x10
TCM_FRONTIER_SCRATCH_PRESERVED         equ 0x12
TCM_FRONTIER_SCRATCH_SIZE              equ 0x14

NPCTrainerGetBattleIntro_Hook          equ 0x02051868
WildPokemonGetBattleIntro_Next         equ 0x02051894
WildPokemonSpeciesComboLoopBound       equ 0x020518CA
BattleSetup_GetWildBattleMusic_RouteHook equ 0x02051800
BattleSetup_GetWildBattleMusic_RouteResume equ 0x02051814
TCM_SPECIES_COMBO_TABLE_ADDR           equ 0x020FC3B4
TCM_SPECIES_COMBO_NATIVE_COUNT         equ 0x0B
TCM_SPECIES_COMBO_TOTAL_COUNT          equ 0x2B
TCM_SPECIES_COMBO_EXTENSION_ADDR       equ 0x020FC3CA
TCM_SPECIES_COMBO_EXTENSION_COUNT      equ 0x20
TCM_SPECIES_COMBO_EMPTY                equ 0xA800
BattleStartDynamicTransition_Hook      equ 0x020517BE
BattleStartDynamicTransition_Resume    equ 0x020517C4
BattleSetup_GetWildTransitionEffect_RouteHook equ 0x020517EC
BattleSetup_GetWildTransitionEffect_RouteResume equ 0x020517F6
TCM_VS_TRANSITION_TABLE_ADDR           equ 0x020FC40A
TCM_BATTLE_TYPE_TRAINER                equ 0x01
TCM_BATTLE_TYPE_DOUBLES                equ 0x02
TCM_BATTLE_TYPE_LINK                   equ 0x04
TCM_BATTLE_TYPE_FRONTIER               equ 0x80

TCM_TRANSITION_EFFECT_NORMAL_EARLY     equ 6
TCM_TRANSITION_EFFECT_VS20             equ 12
TCM_TRANSITION_EFFECT_VS8              equ 29
TCM_TRANSITION_EFFECT_ROCKET_GRUNT     equ 39
TCM_TRANSITION_EFFECT_ROCKET_ADMIN     equ 40
TCM_TRANSITION_EFFECT_KIMONO           equ 45
TCM_TRANSITION_EFFECT_RED              equ 46

TrainerClass_GetGender_Hook            equ 0x020735F8
CreateNPCTrainerParty_Next             equ 0x02073604

Trainer_GetEncounterMusic_Hook         equ 0x02055098
FieldSystem_BeginFadeOutMusic_Next     equ 0x020550E4
Ov01_EffectDispatch_Hook               equ 0x021EFB38
Ov01_EffectDispatch_Tail               equ 0x021EFB46
Ov01_EffectDispatch_Global             equ 0x02209B64
Ov01_EffectDispatch_Table              equ 0x022068C4
Ov01_EffectCleanup                     equ 0x021EFCDC
Ov01_InitSpriteResourceBundle          equ 0x021F05C4
Ov01_DestroySpriteResourceBundle       equ 0x021F05F4
Ov01_DestroySpriteResources            equ 0x021F06EC
Ov01_CreateSprite                      equ 0x021F0718
Ov01_InitAffineTimeline                equ 0x021EFE70
Ov01_AdvanceAffineTimeline             equ 0x021EFE80
Ov01_WriteVecFx32                      equ 0x021F074C
Sprite_SetPositionXY                   equ 0x0200DD88
Sprite_SetAnimActiveFlag               equ 0x0202484C
Sprite_SetAffineOverwriteMode          equ 0x0202487C
Sprite_SetAnimCtrlSeq                  equ 0x020248F0
Sprite_SetAffineScale                  equ 0x020247F4
BeginNormalPaletteFade                 equ 0x0200FA24
IsPaletteFadeFinished                  equ 0x0200FB5C
SetMasterBrightnessWhite               equ 0x0200FBF4
SetBlendBrightness                     equ 0x0200B4F0
FontID_String_GetWidth                 equ 0x02002F30
InitWindow                             equ 0x0201D3C4
AddTextWindowTopLeftCorner             equ 0x0201D494
AddTextPrinterParameterizedWithColorAndSpacing equ 0x02020150
RemoveWindow                           equ 0x0201D520
TextOBJ_LayoutPlan                     equ 0x02013BD4
TextOBJ_FreeLayoutPlan                 equ 0x02013FA8
FontSystem_NewInit                     equ 0x02013534
FontSystem_Delete                      equ 0x020135AC
TextOBJ_CalcCharDemand                 equ 0x02013688
TextOBJ_New                            equ 0x020135D8
FontOAM_Delete                         equ 0x02013660
TextOBJ_SetPosition                    equ 0x020136B4
TextOBJ_SetSpritesDrawFlag             equ 0x020137C0
TextOBJ_SetPaletteOffset               equ 0x020138E0
ObjCharTransfer_Reserve                equ 0x02021AC8
ObjCharTransfer_Release                equ 0x02021B5C
NewMsgDataFromNarc                     equ 0x0200BAF8
NewString_ReadMsgData                  equ 0x0200BBA0
DestroyMsgData                         equ 0x0200BB44
MsgDataGetCount                        equ 0x0200BBCC
String_New                             equ 0x02026354
String_Delete                          equ 0x02026380
String_GetLength                       equ 0x02026800
String_Cat_HandleTrainerName           equ 0x02026B88
GfGfxLoader_GetPlttDataFromOpenNarc    equ 0x02007C48
SysTask_CreateOnVWaitQueue             equ 0x0200E374
SysTask_Destroy                        equ 0x0200E390
GX_LoadBGPltt                          equ 0x020CFC6C

TCM_STYLE13_WORK_SIZE                   equ 0x37C
TCM_STYLE13_WORK_STATE                  equ 0x00
TCM_STYLE13_WORK_BG_SELECTOR            equ 0x04
TCM_STYLE13_WORK_BG_VWAIT_TASK          equ 0x08
TCM_STYLE13_WORK_VWAIT_ACK              equ 0x0C
TCM_STYLE13_WORK_BUNDLE                 equ 0x10
TCM_STYLE13_WORK_SYMBOL_RESOURCES       equ 0x150
TCM_STYLE13_WORK_SYMBOL_GROUP           equ 0x184
TCM_STYLE13_WORK_SYMBOL_DELAY           equ 0x1E8
TCM_STYLE13_WORK_BG_FRAMES              equ 0x1F0
TCM_STYLE13_WORK_SAVED_DISPCNT          equ 0x2F0
TCM_STYLE13_WORK_SAVED_WIN0H            equ 0x2F4
TCM_STYLE13_WORK_SAVED_WIN1H            equ 0x2F6
TCM_STYLE13_WORK_SAVED_WIN0V            equ 0x2F8
TCM_STYLE13_WORK_SAVED_WIN1V            equ 0x2FA
TCM_STYLE13_WORK_SAVED_WININ            equ 0x2FC
TCM_STYLE13_WORK_SAVED_WINOUT           equ 0x2FE
TCM_STYLE13_WORK_WIN_TOP                equ 0x300
TCM_STYLE13_WORK_WIN_BOTTOM             equ 0x304
TCM_STYLE13_WORK_PORTRAIT_RESOURCES     equ 0x308
TCM_STYLE13_WORK_PORTRAIT_SPRITE        equ 0x33C
TCM_STYLE13_WORK_PORTRAIT_CURRENT_X     equ 0x340
TCM_STYLE13_WORK_NAME_ID                equ 0x344
TCM_STYLE13_WORK_NAME_LENGTH            equ 0x346
TCM_STYLE13_WORK_NAME_WIDTH             equ 0x348
TCM_STYLE13_WORK_NAME_STATUS            equ 0x34C
TCM_STYLE13_WORK_TEXT_PALETTE_RESOURCE  equ 0x350
TCM_STYLE13_WORK_FONT_SYSTEM            equ 0x354
TCM_STYLE13_WORK_CHAR_DESCRIPTOR        equ 0x358
TCM_STYLE13_WORK_CHAR_DEMAND            equ 0x364
TCM_STYLE13_WORK_NAME_WINDOW            equ 0x368
TCM_STYLE13_WORK_TEXT_OBJ               equ 0x378
TCM_STYLE13_NAME_STATUS_OK              equ 1
TCM_STYLE13_NAME_STATUS_MSG_FAIL        equ 2
TCM_STYLE13_NAME_STATUS_ID_RANGE        equ 3
TCM_STYLE13_NAME_STATUS_SOURCE_FAIL     equ 4
TCM_STYLE13_NAME_STATUS_CAPACITY        equ 5
TCM_STYLE13_NAME_STATUS_NORMALIZED_FAIL equ 6
TCM_STYLE13_NAME_STATUS_WIDTH           equ 7
TCM_STYLE13_NAME_STATUS_OWNER           equ 8
TCM_STYLE13_NAME_STATUS_WINDOW          equ 9
TCM_STYLE13_NAME_STATUS_PRINTER         equ 10
TCM_STYLE13_NAME_STATUS_CHAR_DEMAND     equ 11
TCM_STYLE13_CHAR_DESC_SIZE              equ 0x00
TCM_STYLE13_CHAR_DESC_OFFSET            equ 0x04
TCM_STYLE13_CHAR_DESC_VRAM              equ 0x08
TCM_STYLE13_CHAR_DESC_IS_AT_END         equ 0x0A
TCM_STYLE13_TEXT_MSG_NARC               equ 0x1B
TCM_STYLE13_TEXT_MSG_MEMBER             equ 0x2D9
TCM_STYLE13_TEXT_COLOR                  equ 0x00010200
TCM_STYLE13_TEXT_MAX_PIXEL_WIDTH        equ 0x7F8
TCM_STYLE13_TEXT_SYNC_RESULT            equ 8
TCM_STRING_SIZE_OFFSET                  equ 0x02
TCM_STRING_DATA_OFFSET                  equ 0x08
TCM_TRAINER_NAME_PACKED_MARKER          equ 0xF100
TCM_OV01_ENV_FIELD_SYSTEM_OFFSET        equ 0x10
TCM_FIELD_SYSTEM_BG_CONFIG_OFFSET       equ 0x08
TCM_STYLE13_NAME_STACK_PLAN_HEAD        equ 0x00
TCM_STYLE13_NAME_STACK_PLAN_COUNT       equ 0x14
TCM_STYLE13_NAME_STACK_TILE_WIDTH       equ 0x28
TCM_STYLE13_NAME_STACK_BG_CONFIG        equ 0x2C
TCM_STYLE13_NAME_STACK_SAVED_ENV        equ 0x38
TCM_STYLE13_BG_FRAME_SIZE               equ 0x20
TCM_STYLE13_BG_FRAME_COUNT              equ 8
TCM_STYLE13_BG_FRAME_WORD_COUNT         equ 0x40
TCM_STYLE13_STATE_WAIT_FADE_IDLE        equ 0
TCM_STYLE13_STATE_WAIT_TO_WHITE         equ 1
TCM_STYLE13_STATE_ARM_UNDER_WHITE       equ 2
TCM_STYLE13_STATE_BEGIN_FROM_WHITE      equ 3
TCM_STYLE13_STATE_WAIT_FROM_WHITE       equ 4
TCM_STYLE13_STATE_EXPAND_BANNER         equ 5
TCM_STYLE13_STATE_WAIT_FINAL_COMMIT     equ 6
TCM_STYLE13_STATE_POST_CLAMP_COMPLETION equ 7
TCM_STYLE13_STATE_REVEAL_SYMBOL_GROUP   equ 8
TCM_STYLE13_STATE_STATE8_SEPARATOR      equ 9
TCM_STYLE13_STATE_PORTRAIT_REVEAL       equ 10
TCM_STYLE13_STATE_PORTRAIT_SLIDE        equ 11
TCM_STYLE13_STATE_PORTRAIT_TEARDOWN     equ 12
TCM_STYLE13_STATE_POST_SLIDE_DELAY      equ 13
TCM_STYLE13_STATE_WAIT_SECOND_WHITE     equ 14
TCM_STYLE13_STATE_BEGIN_SECOND_RETURN   equ 15
TCM_STYLE13_STATE_WAIT_SECOND_RETURN    equ 16
TCM_STYLE13_STATE_SECOND_FLASH_TEARDOWN equ 17
TCM_STYLE13_STATE_RESTORE_PORTRAIT_PALETTE equ 18
TCM_STYLE13_STATE_POST_REVEAL_HOLD     equ 19
TCM_STYLE13_STATE_BEGIN_EXIT_WHITE     equ 20
TCM_STYLE13_STATE_WAIT_EXIT_WHITE      equ 21
TCM_STYLE13_SYMBOL_RESOURCE_ID          equ 0x000927D1
TCM_STYLE13_PORTRAIT_RESOURCE_KEY       equ 0x000927C0
TCM_STYLE13_TEXT_PALETTE_RESOURCE_KEY   equ 0x000927D2
TCM_STYLE13_SPRITELIST_CAPACITY         equ 5
TCM_STYLE13_RESOURCE_SET_CAPACITY        equ 3
TCM_STYLE13_PORTRAIT_ENTRY_X            equ 0x00010000
TCM_STYLE13_PORTRAIT_ENTRY_PIXELS       equ 256
TCM_STYLE13_PORTRAIT_TARGET_X           equ 0x0000D000
TCM_STYLE13_PORTRAIT_STEP               equ 0x00000F00
TCM_STYLE13_PORTRAIT_Y                  equ 96
TCM_STYLE13_PORTRAIT_NATIVE_Y           equ 80
TCM_STYLE13_GROUP_COUNTDOWN             equ 0x00
TCM_STYLE13_GROUP_EXPOSED_COUNT         equ 0x02
TCM_STYLE13_GROUP_SPRITES               equ 0x04
TCM_STYLE13_GROUP_TIMELINES             equ 0x14
TCM_STYLE13_GROUP_MEMBER_COUNT          equ 4

CalcPrizeMoney_TrainerRead_Hook        equ 0x0223FB4E
CalcPrizeMoney_TrainerRead_Resume      equ 0x0223FB56
CalcPrizeMoney_PrizeScan_Hook          equ 0x0223FBBE
CalcPrizeMoney_PrizeScan_Resume        equ 0x0223FBE2
CalcPrizeMoney_TagLoad_Hook            equ 0x0223FBF0
CalcPrizeMoney_DoubleLoad_Hook         equ 0x0223FC0A
CalcPrizeMoney_SingleLoad_Hook         equ 0x0223FC20

Ov115_Vs20StateMachine_Hook            equ 0x0225F220
Ov115_Vs20StateMachine_Resume          equ 0x0225F22E
Ov115_Vs20StateMachine_Next            equ 0x0225F704
Ov115_Vs20PortraitLoad_Hook            equ 0x0225F30E
Ov115_Vs20PortraitLoad_Resume          equ 0x0225F330
Ov115_Vs20SymbolLoad_Hook              equ 0x0225F330
Ov115_Vs20SymbolLoad_Resume            equ 0x0225F356
Ov115_Vs20PortraitSetup1_Hook          equ 0x0225F3A6
Ov115_Vs20PortraitSetup1_Resume        equ 0x0225F3B2
Ov115_Vs20BackgroundLoad_Hook          equ 0x0225F3F4
Ov115_Vs20BackgroundLoad_Resume        equ 0x0225F400
Ov115_Vs20PortraitSetup7_Hook          equ 0x0225F568
Ov115_Vs20PortraitSetup7_Resume        equ 0x0225F572
Ov115_Vs8StateMachine_Hook             equ 0x0225F978
Ov115_Vs8StateMachine_Resume           equ 0x0225F986
Ov115_SetupOpponentPortrait            equ 0x02260254
Ov115_Vs8PortraitLoad_Hook             equ 0x0225FA48
Ov115_Vs8PortraitLoad_Resume           equ 0x0225FA70
Ov115_Vs8BackgroundLoad_Hook           equ 0x0225FA70
Ov115_Vs8BackgroundLoad_Resume         equ 0x0225FA98
Ov115_Vs8SymbolLoad_Hook               equ 0x0225FA98
Ov115_Vs8SymbolLoad_Resume             equ 0x0225FAC0
Ov115_Vs8OpponentSilhouetteBlend_Hook  equ 0x0225FB74
Ov115_Vs8OpponentRevealBlend_Hook      equ 0x0225FEBA
Ov117_RocketAdminStateMachine_Hook     equ 0x0225F524
Ov117_RocketAdminStateMachine_Resume   equ 0x0225F53A
Ov117_RocketAdminStateMachine_Exit     equ 0x0225FA98
Ov117_RocketAdminStateMachine_Next     equ 0x0225FACC
Ov117_RocketAdminPortraitLoad_Hook     equ 0x0225F582
Ov117_RocketAdminPortraitLoad_Resume   equ 0x0225F5A6
Ov117_GenericRocketSymbolLoad_Hook     equ 0x0225F076
Ov117_GenericRocketSymbolLoad_Resume   equ 0x0225F0A2
Ov117_RocketAdminBg1Load_Hook          equ 0x0225F63A
Ov117_RocketAdminBg1Load_Resume        equ 0x0225F658
Ov117_RocketAdminBg3Load_Hook          equ 0x0225F6C2
Ov117_RocketAdminBg3Load_Resume        equ 0x0225F6E0
Ov117_RocketAdminLaterBgLoad_Hook      equ 0x0225F820
Ov117_RocketAdminLaterBgLoad_Resume    equ 0x0225F82A
Ov118_KimonoShutterLoad_Hook           equ 0x0225F0CA
Ov118_KimonoShutterLoad_Resume         equ 0x0225F0EA
Ov118_KimonoShutterLoad_Next           equ 0x0225F2C0
Ov119_NormalEarlySpriteLoad_Hook       equ 0x0225F3DC
Ov119_NormalEarlySpriteLoad_Resume     equ 0x0225F402
Ov119_NormalLateSpriteLoad_Hook        equ 0x0225F082
Ov119_NormalLateSpriteLoad_Resume      equ 0x0225F0A8
Ov119_WaterEarlySpriteLoad_Hook        equ 0x0225FA9E
Ov119_WaterEarlySpriteLoad_Resume      equ 0x0225FAC4
Ov119_WaterLateSpriteLoad_Hook         equ 0x0225F6DE
Ov119_WaterLateSpriteLoad_Resume       equ 0x0225F704
Ov119_CaveEarlySpriteLoad_Hook         equ 0x022602BC
Ov119_CaveEarlySpriteLoad_Resume       equ 0x022602E2
Ov119_CaveLateSpriteLoad_Hook          equ 0x0225FFF6
Ov119_CaveLateSpriteLoad_Resume        equ 0x0226001C
Ov120_RedBlockLoad_Hook                equ 0x0225F7A6
Ov120_RedBlockLoad_Resume              equ 0x0225F7BE
Ov120_RedStateMachine_Next             equ 0x0225F8B0
Ov120_RedSharedImageTable              equ 0x022601F4
Ov120_RedSharedAnimTable               equ 0x022601F8
Ov120_RedSharedCellTable               equ 0x022601FC
Ov120_RedSharedPaletteTable            equ 0x02260200
Ov80_FrontierBrainPresentationMusic    equ 0x0222E054
Ov80_FrontierRowCall_Hook              equ 0x0223A090
Ov80_FrontierRowCall_Resume            equ 0x0223A0A4
Ov80_FrontierRowConsumer               equ 0x0223A938
Ov80_FrontierMainTask_Hook             equ 0x0223A144
Ov80_FrontierMainTask_Next             equ 0x0223A174
Ov80_FrontierMainTask_Consumer         equ 0x0223A174
Ov80_FrontierMainTask_Destroy          equ 0x0223A0C0
Ov80_FrontierPortraitPalette_Load      equ 0x0223A266
Ov80_FrontierPortraitImage_Load        equ 0x0223A2A4
Ov80_FrontierPortraitImageCall_Hook    equ 0x0223A2AC
Ov80_FrontierPortraitCell_Load         equ 0x0223A2B8
Ov80_FrontierPortraitCellCall_Hook     equ 0x0223A2C0
Ov80_FrontierPortraitAnim_Load         equ 0x0223A2CC
Ov80_FrontierSymbolPaletteCall_Hook    equ 0x0223A312
Ov80_FrontierSymbolCharCall_Hook       equ 0x0223A33A
Ov80_FrontierSymbolCellCall_Hook       equ 0x0223A34E
Ov80_FrontierSymbolAnimCall_Hook       equ 0x0223A362
Ov80_FrontierBackgroundPalette_Load    equ 0x0223A9A8
Ov80_FrontierBackgroundImage_Load      equ 0x0223A9C0
Ov80_FrontierBackgroundScreen_Load     equ 0x0223A9D8
Ov80_FrontierBackgroundPalette_Copy    equ 0x0223AA02

; =============================================================================
; Patch arm9.bin
; =============================================================================

.ifdef PATCH
.open "arm9/arm9.bin", 0x02000000

; -----------------------------------------------------------------------------
; BattleSetup_GetWildBattleMusic direct trainer/wild route split
; -----------------------------------------------------------------------------

.org BattleSetup_GetWildBattleMusic_RouteHook
    bl TCM_SelectBattleMusicDirect
    add r4, r0, #0
    add r6, r1, #0
    mov r0, #0x7
    lsl r0, r0, #0x6
    ldr r0, [r5, r0]
    b BattleSetup_GetWildBattleMusic_RouteResume
    .fill BattleSetup_GetWildBattleMusic_RouteResume - ., 0x00

; Landing guard: 0x02051814 is the native Save_VarsFlags_Get call.

; -----------------------------------------------------------------------------
; Restore the native BattleStartGetTransition dynamic selector call
; -----------------------------------------------------------------------------

.if . != 0x02051814
    .error "Native write span mismatch: BattleSetup_GetWildBattleMusic_RouteHook"
.endif
.org BattleStartDynamicTransition_Hook
    add r0, r4, #0
    bl Ov01_DynamicBattleTransitionSelector

; Landing guard: execution resumes at 0x020517C4, the native function epilogue.

; -----------------------------------------------------------------------------
; BattleSetup_GetWildTransitionEffect direct trainer/wild route split
; -----------------------------------------------------------------------------

.if . != 0x020517C4
    .error "Native write span mismatch: BattleStartDynamicTransition_Hook"
.endif
.org BattleSetup_GetWildTransitionEffect_RouteHook
    bl TCM_SelectBattleTransitionDirect
    b BattleSetup_GetWildTransitionEffect_RouteResume
    .fill BattleSetup_GetWildTransitionEffect_RouteResume - ., 0x00

; Landing guard: 0x020517F6 is the native function epilogue.

; -----------------------------------------------------------------------------
; NPCTrainerGetBattleIntroAndMusicParam
; -----------------------------------------------------------------------------

.if . != 0x020517F6
    .error "Native write span mismatch: BattleSetup_GetWildTransitionEffect_RouteHook"
.endif
.org NPCTrainerGetBattleIntro_Hook
    mov r0, #0x29
    bx lr
    .fill WildPokemonGetBattleIntro_Next - ., 0x00

; Boundary guard: WildPokemonGetBattleIntroAndMusicParam starts at 0x02051894.

.if . != 0x02051894
    .error "Native write span mismatch: NPCTrainerGetBattleIntro_Hook"
.endif
.org WildPokemonSpeciesComboLoopBound
    cmp r2, #TCM_SPECIES_COMBO_TOTAL_COUNT

; Same-width replacement inside WildPokemonGetBattleIntroAndMusicParam. The original instruction was cmp r2,#0x0B; its following blo at 0x020518CC and every register/branch contract remain unchanged.

.if . != 0x020518CC
    .error "Native write span mismatch: WildPokemonSpeciesComboLoopBound"
.endif
.org TCM_SPECIES_COMBO_EXTENSION_ADDR
TCM_ExtendedSpeciesComboTable:
    ; Replace an empty row with: .halfword (comboId << 10) | speciesId
    .halfword TCM_SPECIES_COMBO_EMPTY ; extension 00 / lookup 11
    .halfword TCM_SPECIES_COMBO_EMPTY ; extension 01 / lookup 12
    .halfword TCM_SPECIES_COMBO_EMPTY ; extension 02 / lookup 13
    .halfword TCM_SPECIES_COMBO_EMPTY ; extension 03 / lookup 14
    .halfword TCM_SPECIES_COMBO_EMPTY ; extension 04 / lookup 15
    .halfword TCM_SPECIES_COMBO_EMPTY ; extension 05 / lookup 16
    .halfword TCM_SPECIES_COMBO_EMPTY ; extension 06 / lookup 17
    .halfword TCM_SPECIES_COMBO_EMPTY ; extension 07 / lookup 18
    .halfword TCM_SPECIES_COMBO_EMPTY ; extension 08 / lookup 19
    .halfword TCM_SPECIES_COMBO_EMPTY ; extension 09 / lookup 20
    .halfword TCM_SPECIES_COMBO_EMPTY ; extension 10 / lookup 21
    .halfword TCM_SPECIES_COMBO_EMPTY ; extension 11 / lookup 22
    .halfword TCM_SPECIES_COMBO_EMPTY ; extension 12 / lookup 23
    .halfword TCM_SPECIES_COMBO_EMPTY ; extension 13 / lookup 24
    .halfword TCM_SPECIES_COMBO_EMPTY ; extension 14 / lookup 25
    .halfword TCM_SPECIES_COMBO_EMPTY ; extension 15 / lookup 26
    .halfword TCM_SPECIES_COMBO_EMPTY ; extension 16 / lookup 27
    .halfword TCM_SPECIES_COMBO_EMPTY ; extension 17 / lookup 28
    .halfword TCM_SPECIES_COMBO_EMPTY ; extension 18 / lookup 29
    .halfword TCM_SPECIES_COMBO_EMPTY ; extension 19 / lookup 30
    .halfword TCM_SPECIES_COMBO_EMPTY ; extension 20 / lookup 31
    .halfword TCM_SPECIES_COMBO_EMPTY ; extension 21 / lookup 32
    .halfword TCM_SPECIES_COMBO_EMPTY ; extension 22 / lookup 33
    .halfword TCM_SPECIES_COMBO_EMPTY ; extension 23 / lookup 34
    .halfword TCM_SPECIES_COMBO_EMPTY ; extension 24 / lookup 35
    .halfword TCM_SPECIES_COMBO_EMPTY ; extension 25 / lookup 36
    .halfword TCM_SPECIES_COMBO_EMPTY ; extension 26 / lookup 37
    .halfword TCM_SPECIES_COMBO_EMPTY ; extension 27 / lookup 38
    .halfword TCM_SPECIES_COMBO_EMPTY ; extension 28 / lookup 39
    .halfword TCM_SPECIES_COMBO_EMPTY ; extension 29 / lookup 40
    .halfword TCM_SPECIES_COMBO_EMPTY ; extension 30 / lookup 41
    .halfword TCM_SPECIES_COMBO_EMPTY ; extension 31 / lookup 42
TCM_ExtendedSpeciesComboTable_End:

; Boundary guard: _020FC40A transition/music table starts immediately after the 32-entry extension. Do not write beyond the table above.

; -----------------------------------------------------------------------------
; TrainerClass_GetGenderOrTrainerCount
; -----------------------------------------------------------------------------

.if . != 0x020FC40A
    .error "Native write span mismatch: TCM_SPECIES_COMBO_EXTENSION_ADDR"
.endif
.org TrainerClass_GetGender_Hook
    ldr r1, [pc, #0]
    bx r1
    .word TCM_GetTrainerClassGender | 1
    .fill CreateNPCTrainerParty_Next - ., 0x00

; Boundary guard: CreateNPCTrainerParty starts at 0x02073604.

; -----------------------------------------------------------------------------
; Trainer_GetEncounterMusic
; -----------------------------------------------------------------------------

.if . != 0x02073604
    .error "Native write span mismatch: TrainerClass_GetGender_Hook"
.endif
.org Trainer_GetEncounterMusic_Hook
    ldr r2, [pc, #0]
    bx r2
    .word TCM_GetTrainerClassEyeContactMusic | 1
    .fill FieldSystem_BeginFadeOutMusic_Next - ., 0x00

; Boundary guard: FieldSystem_BeginFadeOutMusic starts at 0x020550E4.

.if . != 0x020550E4
    .error "Native write span mismatch: Trainer_GetEncounterMusic_Hook"
.endif
.close

; =============================================================================
; Patch-owned executable payload
; =============================================================================

.open INJECT_FILE, INJECT_FILE_BASE
.org INJECT_ADDR

TCM_Payload_Start:
TCM_CurrentOrdinaryStyle:
    .halfword 0
    .align 4

TCM_GetTrainerClassGender:
    push {r4, lr}
    sub sp, #0x8

    add r4, r0, #0              ; Preserve trainerClass across loader call.

    mov r0, #TCM_GENDER_SIZE
    str r0, [sp, #0x0]          ; Fifth arg: size.

    add r0, sp, #0x4            ; First arg: stack buffer destination.
    mov r1, #NARC_a_1_5_5
    add r2, r4, #0              ; Third arg: member id == trainerClass.
    mov r3, #TCM_GENDER_OFFSET
    bl ReadFromNarcMemberByIdPair

    add r0, sp, #0x4
    ldrh r0, [r0, #0x0]

    add sp, #0x8
    pop {r4, pc}

TCM_GetTrainerClassGender_End:
    .align 4

TCM_Ov115Vs20StateMachinePrologue:
    push {r3, r4, r5, r6, r7, lr}
    sub sp, #0x28

    add r5, r0, #0              ; Restore original prologue.
    add r7, r1, #0
    ldr r1, [r5, #0x0]

    push {r1, r3}               ; Keep the call boundary 8-byte aligned.
    add r0, r2, #0              ; Original static record pointer.
    bl TCM_SelectVs20Record
    pop {r1, r3}
    add r6, r0, #0              ; Selected record pointer.

    ldr r4, [r5, #0xC]
    ldr r0, =(Ov115_Vs20StateMachine_Resume | 1)
    bx r0
    .pool

TCM_GetTrainerClassEyeContactMusic:
    push {r4, r5, r6, lr}
    sub sp, #0x8

    add r4, r1, #0              ; selector: 0 main, 1 alt.
    add r5, r0, #0              ; trainer id.

    cmp r4, #0x2
    blt TCM_EyeMusic_SelectorOk
    bl GF_AssertFail

TCM_EyeMusic_SelectorOk:
    add r0, r5, #0
    mov r1, #0x1                ; TrainerData attr: trainer class.
    bl TrainerData_GetAttr
    add r5, r0, #0              ; Preserve the full trainerClass result.

    mov r0, #TCM_EYE_MUSIC_SIZE
    str r0, [sp, #0x0]          ; Fifth arg: size.

    add r3, r4, r4              ; offset = 0x04 + selector * 2.
    add r3, #TCM_EYE_MUSIC_MAIN_OFFSET

    add r0, sp, #0x4            ; First arg: stack buffer destination.
    mov r1, #NARC_a_1_5_5
    add r2, r5, #0              ; Third arg: member id == trainerClass.
    bl ReadFromNarcMemberByIdPair

    add r0, sp, #0x4
    ldrh r0, [r0, #0x0]

    add sp, #0x8
    pop {r4, r5, r6, pc}

TCM_GetTrainerClassEyeContactMusic_End:
    .align 4

TCM_CurrentTrainerClass:
    .word 0

TCM_Vs20ScratchRecord:
    .fill 0x14, 0x00

TCM_Vs20SymbolScratch:
    .fill 0x08, 0x00

TCM_SelectVs20Record:
    ldr r3, =(TCM_SelectVs20RecordRelease | 1)
    bx r3
    .pool

TCM_Ov117LoadRocketLaterBg:
    push {r3, lr}

    ldr r2, =TCM_RocketAdminScratchRecord
    ldrh r2, [r2, #TCM_ROCKET_BG_SCRATCH_LATER_SCREEN]
    add r0, r5, #0
    mov r1, #1
    bl Ov117_LoadRectScreenFromOpenNarc

    pop {r3, pc}
    .pool

; -----------------------------------------------------------------------------
; E4/Champion Style 2 selector helpers
; -----------------------------------------------------------------------------

TCM_Ov115LoadVs8PortraitQuartet:
    push {r3, r4, r5, r6, r7, lr}

    ; Original caller [sp, #0x18] is at [sp, #0x30] after this push.
    ldr r0, [sp, #0x30]
    ldr r7, =TCM_Vs8ScratchRecord
    cmp r0, r7
    bne TCM_Ov115LoadVs8PortraitQuartet_Assert

    sub sp, #0x18

    ldrh r3, [r7, #0x08]        ; Palette / RLCN.
    ldrh r0, [r7, #0x0A]        ; Image / RGCN.
    str r0, [sp, #0x04]
    ldrh r0, [r7, #0x0C]        ; Cell / RECN.
    str r0, [sp, #0x08]
    ldrh r0, [r7, #0x0E]        ; Animation / RNAN.
    str r0, [sp, #0x0C]

    ; Reserve exactly the palette banks referenced by this portrait's RECN. The reader returns the temporary RECN buffer to Heap_Free itself.
    ldr r0, [r6, #0x20]
    ldrh r1, [r7, #0x0C]
    ; Native ov115_0225F978 saved the heap id at its [sp, #0x14]. After this helper's push and local frame, that value is at [sp, #0x44].
    ldr r2, [sp, #0x44]
    bl TCM_Vs8GetPaletteBankCountFromOpenNarc
    str r0, [sp, #0x00]
    ldr r0, =0x000927C1
    str r0, [sp, #0x10]
    mov r0, #0
    str r0, [sp, #0x14]

    mov r2, #0x73
    lsl r2, r2, #2
    add r2, r4, r2
    add r1, r4, #0
    add r1, #0x5C
    ldr r0, [r6, #0x20]
    bl TCM_LoadMappedSpriteResourcesFromOpenNarc

    add sp, #0x18
    pop {r3, r4, r5, r6, r7, pc}

TCM_Ov115LoadVs8PortraitQuartet_Assert:
    bl GF_AssertFail
    b TCM_Ov115LoadVs8PortraitQuartet_Assert
    .pool

; This is called only after the ARM9 selector has established vsStyle 13, before the ordinary Overlay-1 effect is created.
TCM_CacheStyle13SymbolQuartet:
    push {r4, lr}
    sub sp, #0x08
    ldr r2, =TCM_CurrentTrainerClass
    ldr r2, [r2, #0x00]
    mov r0, #TCM_ASSET_GROUP3_QUARTET_SIZE
    str r0, [sp, #0x00]
    ldr r0, =TCM_Style13SymbolScratch
    mov r1, #NARC_a_1_5_5
    mov r3, #TCM_ASSET_GROUP3_QUARTET_OFFSET
    bl ReadFromNarcMemberByIdPair
    add sp, #0x08
    pop {r4, pc}
    .pool

TCM_Style13SymbolScratch:
    .fill TCM_ASSET_GROUP3_QUARTET_SIZE, 0x00

TCM_Arm9Vs8QuartetCodeCave_End:
    .align 4

TCM_Ov115Vs8StateMachinePrologue:
    push {r4, r5, r6, r7, lr}
    sub sp, #0x44

    add r6, r0, #0              ; Restore original prologue.
    ldr r0, [r6, #0x0]
    str r1, [sp, #0x14]

    push {r0, r3}               ; Keep the call boundary 8-byte aligned.
    add r0, r2, #0              ; Original static 8-byte record pointer.
    bl TCM_SelectVs8Record
    add r2, r0, #0
    pop {r0, r3}

    str r2, [sp, #0x18]
    ldr r4, [r6, #0xC]
    ldr r3, =(Ov115_Vs8StateMachine_Resume | 1)
    bx r3
    .pool

TCM_SelectVs8Record:
    ldr r3, =(TCM_SelectVs8RecordRelease | 1)
    bx r3
    .pool

TCM_Vs8ScratchRecord:
    .fill 0x10, 0x00
    .align 4

TCM_Ov117RocketAdminStateMachinePrologue:
    push {r3, r4, r5, r6, r7, lr}
    sub sp, #0x18

    add r5, r1, #0              ; Restore original prologue.
    str r0, [sp, #0x14]
    ldr r0, [r5, #0x0]

    push {r0, r3}               ; Keep the call boundary 8-byte aligned.
    add r0, r2, #0              ; Original static 8-byte admin record pointer.
    bl TCM_SelectRocketAdminRecord
    add r6, r0, #0              ; Selected record pointer.
    pop {r0, r3}

    mov r7, #4
    ldr r4, [r5, #0xC]
    cmp r0, #0xC
    bhi TCM_Ov117RocketAdminStateMachinePrologue_Exit

    ldr r3, =(Ov117_RocketAdminStateMachine_Resume | 1)
    bx r3

TCM_Ov117RocketAdminStateMachinePrologue_Exit:
    ldr r3, =(Ov117_RocketAdminStateMachine_Exit | 1)
    bx r3
    .pool

TCM_SelectRocketAdminRecord:
    ldr r3, =(TCM_SelectRocketAdminRecord_FailLoud | 1)
    bx r3
    .pool

TCM_Ov117LoadRocketBg:
    push {r3, r4, r6, lr}
    sub sp, #0x10

    add r6, r0, #0              ; 0 = BG1 args, non-zero = BG3 args.

    mov r0, #0
    str r0, [sp, #0x0]
    mov r0, #2
    str r0, [sp, #0x4]
    ldr r0, [r5, #0x10]
    ldr r0, [r0, #0x8]
    str r0, [sp, #0x8]

    ldr r4, =TCM_RocketAdminScratchRecord
    cmp r6, #0
    bne TCM_Ov117LoadRocketBg_Bg3

    mov r0, #1
    str r0, [sp, #0xC]
    ldrh r1, [r4, #TCM_ROCKET_BG_SCRATCH_BG1_SCREEN]
    ldrh r2, [r4, #TCM_ROCKET_BG_SCRATCH_BG1_CHAR]
    ldrh r3, [r4, #TCM_ROCKET_BG_SCRATCH_BG1_PALETTE]
    b TCM_Ov117LoadRocketBg_Call

TCM_Ov117LoadRocketBg_Bg3:
    mov r0, #3
    str r0, [sp, #0xC]
    ldrh r1, [r4, #TCM_ROCKET_BG_SCRATCH_BG3_SCREEN]
    ldrh r2, [r4, #TCM_ROCKET_BG_SCRATCH_BG3_CHAR]
    ldrh r3, [r4, #TCM_ROCKET_BG_SCRATCH_BG3_PALETTE]

TCM_Ov117LoadRocketBg_Call:
    ldr r0, [r5, #0x20]
    bl Ov01_LoadBgFromOpenNarc

    add sp, #0x10
    pop {r3, r4, r6, pc}
    .pool

TCM_RocketAdminScratchRecord:
    .fill 0x18, 0x00
    .align 4

TCM_Arm9Vs8CodeCave_End:
    .align 4

; -----------------------------------------------------------------------------
; Rocket admin portrait quartet loader
; -----------------------------------------------------------------------------

TCM_Ov117LoadRocketAdminPortraitWide:
    push {r3, r4, r5, r6, r7, lr}
    sub sp, #0x18

    ldr r7, =TCM_RocketAdminPortraitWideScratch
    mov r0, #1
    str r0, [sp, #0x0]
    ldrh r0, [r7, #TCM_ROCKET_PORTRAIT_WIDE_SCRATCH1]
    str r0, [sp, #0x4]
    ldrh r0, [r7, #TCM_ROCKET_PORTRAIT_WIDE_SCRATCH2]
    str r0, [sp, #0x8]
    ldrh r0, [r7, #TCM_ROCKET_PORTRAIT_WIDE_SCRATCH3]
    str r0, [sp, #0xC]
    ldr r0, =0x000927C0
    str r0, [sp, #0x10]
    mov r0, #0
    str r0, [sp, #0x14]

    ldrh r3, [r7, #TCM_ROCKET_PORTRAIT_WIDE_SCRATCH0]
    mov r2, #5
    lsl r2, r2, #6
    add r1, r4, #4
    add r2, r4, r2
    ldr r0, [r5, #0x20]
    bl TCM_LoadMappedSpriteResourcesFromOpenNarc

    add sp, #0x18
    pop {r3, r4, r5, r6, r7, pc}
    .pool
    .align 4

TCM_RocketAdminPortraitWideScratch:
    .fill 0x8, 0x00
    .align 4

TCM_Arm9RocketPortraitCodeCave_End:
    .align 4

; -----------------------------------------------------------------------------
; E4/Champion 8-byte shared-background quartet cache/helper
; -----------------------------------------------------------------------------

TCM_CacheVs8BackgroundQuartet:
    push {r4, r5, r6, r7, lr}
    sub sp, #0x4

    mov r0, #TCM_ASSET_GROUP1_QUARTET_SIZE
    str r0, [sp, #0x0]

    ldr r0, =TCM_Vs8BackgroundScratch
    mov r1, #NARC_a_1_5_5
    add r2, r7, #0
    mov r3, #TCM_ASSET_GROUP1_QUARTET_OFFSET
    bl ReadFromNarcMemberByIdPair

    mov r0, #TCM_ASSET_GROUP3_QUARTET_SIZE
    str r0, [sp, #0x0]

    ldr r0, =TCM_Vs20SymbolScratch
    mov r1, #NARC_a_1_5_5
    add r2, r7, #0
    mov r3, #TCM_ASSET_GROUP3_QUARTET_OFFSET
    bl ReadFromNarcMemberByIdPair

    add sp, #0x4
    pop {r4, r5, r6, r7, pc}
    .pool

TCM_Ov115LoadVs8BackgroundQuartet:
    push {r4, r5, r6, r7, lr}
    sub sp, #0x14

    ; Original caller [sp, #0x18] is at [sp, #0x40] after this full frame.
    ldr r0, [sp, #0x40]
    ldr r7, =TCM_Vs8ScratchRecord
    cmp r0, r7
    bne TCM_Ov115LoadVs8BackgroundQuartet_Assert

    ldr r7, =TCM_Vs8BackgroundScratch
    ldrh r3, [r7, #0x00]        ; Palette / RLCN, native r3 argument.

    mov r0, #0x0C               ; Native constant stack arg.
    str r0, [sp, #0x00]
    ldrh r0, [r7, #0x02]        ; Image / RGCN.
    str r0, [sp, #0x04]
    ldrh r0, [r7, #0x04]        ; Cell / RECN.
    str r0, [sp, #0x08]
    ldrh r0, [r7, #0x06]        ; Animation / RNAN.
    str r0, [sp, #0x0C]
    ldr r0, =0x000927C2
    str r0, [sp, #0x10]

    mov r2, #2
    lsl r2, r2, #8
    add r2, r4, r2
    add r1, r4, #0
    add r1, #0x5C
    ldr r0, [r6, #0x20]
    bl Ov01_LoadSpriteResourcesFromOpenNarc

    add sp, #0x14
    pop {r4, r5, r6, r7, pc}

TCM_Ov115LoadVs8BackgroundQuartet_Assert:
    bl GF_AssertFail
    b TCM_Ov115LoadVs8BackgroundQuartet_Assert

TCM_Vs8BackgroundScratch:
    .fill 0x08, 0x00
    .pool

    .align 4
TCM_SelectRocketAdminRecord_FailLoud:
    push {r5, r6, r7, lr}
    sub sp, #0x40

    ldr r5, =TCM_CurrentTrainerClass
    ldr r7, [r5, #0x0]          ; Member id == active trainer class.

    ; Read style through group 2 as one bounded release-record span.
    mov r0, #0x22
    str r0, [sp, #0x0]

    add r0, sp, #0x4
    mov r1, #NARC_a_1_5_5
    add r2, r7, #0
    mov r3, #TCM_VS_STYLE_OFFSET
    bl ReadFromNarcMemberByIdPair

    add r5, sp, #0x4
    ldrh r0, [r5, #0x0]
    cmp r0, #TCM_VS_STYLE_ROCKET_ADMIN
    bne TCM_SelectRocketAdminRecord_FailLoud_Assert

    ldr r6, =TCM_RocketAdminScratchRecord
    ldr r7, =TCM_RocketAdminPortraitWideScratch

    ; Group 2 begins at release-relative +0x1A.
    ldrh r0, [r5, #0x1A]
    strb r0, [r6, #0x0]
    strh r0, [r7, #TCM_ROCKET_PORTRAIT_WIDE_SCRATCH0]
    ldrh r0, [r5, #0x1C]
    strb r0, [r6, #0x1]
    strh r0, [r7, #TCM_ROCKET_PORTRAIT_WIDE_SCRATCH1]
    ldrh r0, [r5, #0x1E]
    strb r0, [r6, #0x2]
    strh r0, [r7, #TCM_ROCKET_PORTRAIT_WIDE_SCRATCH2]
    ldrh r0, [r5, #0x20]
    strb r0, [r6, #0x3]
    strh r0, [r7, #TCM_ROCKET_PORTRAIT_WIDE_SCRATCH3]

    mov r0, #0
    str r0, [r6, #0x4]
    ldrh r0, [r5, #0x2]         ; Zero-extend release trainerNameId.
    strh r0, [r6, #0x4]

    ldrh r0, [r5, #0x14]        ; Group-1 RCSN 1.
    strh r0, [r6, #TCM_ROCKET_BG_SCRATCH_BG1_SCREEN]
    ldrh r0, [r5, #0x0E]        ; Group-1 RGCN.
    strh r0, [r6, #TCM_ROCKET_BG_SCRATCH_BG1_CHAR]
    ldrh r0, [r5, #0x0C]        ; Group-1 RLCN.
    strh r0, [r6, #TCM_ROCKET_BG_SCRATCH_BG1_PALETTE]

    ldrh r0, [r5, #0x16]        ; Group-1 RCSN 2.
    strh r0, [r6, #TCM_ROCKET_BG_SCRATCH_BG3_SCREEN]
    ldrh r0, [r5, #0x0E]
    strh r0, [r6, #TCM_ROCKET_BG_SCRATCH_BG3_CHAR]
    ldrh r0, [r5, #0x0C]
    strh r0, [r6, #TCM_ROCKET_BG_SCRATCH_BG3_PALETTE]

    ldrh r0, [r5, #0x18]        ; Group-1 RCSN 3.
    strh r0, [r6, #TCM_ROCKET_BG_SCRATCH_LATER_SCREEN]

    add r0, r6, #0
    add sp, #0x40
    pop {r5, r6, r7, pc}

TCM_SelectRocketAdminRecord_FailLoud_Assert:
    bl GF_AssertFail
    b TCM_SelectRocketAdminRecord_FailLoud_Assert
    .pool

TCM_Arm9Vs8BgCodeCave_End:
    .align 4

; -----------------------------------------------------------------------------
; Trainer Red overlay 120 block asset helper
; -----------------------------------------------------------------------------

TCM_Ov120LoadRedBlocks:
    push {r3, r4, r5, r6, r7, lr}
    sub sp, #0x10

    add r6, r4, #0              ; Red work block from ov120_0225F714.
    add r7, r5, #0              ; Red transition state/context.

    mov r0, #TCM_VS_STYLE_SIZE
    str r0, [sp, #0x0]

    add r0, sp, #0x4
    mov r1, #NARC_a_1_5_5
    ldr r2, =TCM_CurrentTrainerClass
    ldr r2, [r2, #0x0]
    mov r3, #TCM_VS_STYLE_OFFSET
    bl ReadFromNarcMemberByIdPair

    add r0, sp, #0x4
    ldrh r0, [r0, #0x0]
    cmp r0, #TCM_VS_STYLE_RED
    bne TCM_Ov120LoadRedBlocks_Fail

    mov r0, #TCM_ASSET_GROUP1_QUARTET_SIZE
    str r0, [sp, #0x0]

    add r0, sp, #0x4
    mov r1, #NARC_a_1_5_5
    ldr r2, =TCM_CurrentTrainerClass
    ldr r2, [r2, #0x0]
    mov r3, #TCM_ASSET_GROUP1_QUARTET_OFFSET
    bl ReadFromNarcMemberByIdPair

    add r4, sp, #0x4

    ldrh r0, [r4, #0x0]         ; Palette/RLCN arg.
    ldr r1, =Ov120_RedSharedPaletteTable
    str r0, [r1, #0x0]

    ldrh r0, [r4, #0x2]         ; Image/RGCN arg.
    ldr r1, =Ov120_RedSharedImageTable
    str r0, [r1, #0x0]

    ldrh r0, [r4, #0x4]         ; Cell/RECN arg.
    ldr r1, =Ov120_RedSharedCellTable
    str r0, [r1, #0x0]

    ldrh r0, [r4, #0x6]         ; Anim/RNAN arg.
    ldr r1, =Ov120_RedSharedAnimTable
    str r0, [r1, #0x0]

    mov r3, #6
    mov r0, #0
    lsl r3, r3, #6
    str r0, [sp, #0x0]          ; Shared loader variant 0.
    add r1, r6, r3
    add r2, r6, #0
    sub r3, #0x34
    ldr r0, [r7, #0x20]
    add r2, #0x10
    add r3, r6, r3
    bl Ov120_LoadSharedSpriteFromOpenNarc

    add sp, #0x10
    pop {r3, r4, r5, r6, r7, pc}

TCM_Ov120LoadRedBlocks_Fail:
    bl GF_AssertFail
    b TCM_Ov120LoadRedBlocks_Fail
    .pool

TCM_Ov117LoadGenericRocketSymbol:
    push {r4, lr}
    sub sp, #0x28

    mov r0, #TCM_VS_STYLE_SIZE
    str r0, [sp, #0x0]

    add r0, sp, #0x4
    mov r1, #NARC_a_1_5_5
    ldr r2, =TCM_CurrentTrainerClass
    ldr r2, [r2, #0x0]
    mov r3, #TCM_VS_STYLE_OFFSET
    bl ReadFromNarcMemberByIdPair

    add r0, sp, #0x4
    ldrh r0, [r0, #0x0]
    cmp r0, #TCM_VS_STYLE_ROCKET_GRUNT
    bne TCM_Ov117LoadGenericRocketSymbol_Fail

    mov r0, #TCM_ASSET_GROUP1_QUARTET_SIZE
    str r0, [sp, #0x0]

    add r0, sp, #0x14
    mov r1, #NARC_a_1_5_5
    ldr r2, =TCM_CurrentTrainerClass
    ldr r2, [r2, #0x0]
    mov r3, #TCM_ASSET_GROUP1_QUARTET_OFFSET
    bl ReadFromNarcMemberByIdPair

    b TCM_Ov117LoadGenericRocketSymbol_Call

TCM_Ov117LoadGenericRocketSymbol_Fail:
    bl GF_AssertFail
    b TCM_Ov117LoadGenericRocketSymbol_Fail

TCM_Ov117LoadGenericRocketSymbol_Call:
    add r1, sp, #0x14
    ldrh r3, [r1, #0x0]         ; Palette/RLCN arg is native call arg 4.
    mov r0, #1                  ; Native stack arg 5.
    str r0, [sp, #0x0]
    ldrh r0, [r1, #0x2]         ; Image/RGCN arg.
    str r0, [sp, #0x4]
    ldrh r0, [r1, #0x4]         ; Cell/RECN arg.
    str r0, [sp, #0x8]
    ldrh r0, [r1, #0x6]         ; Anim/RNAN arg.
    str r0, [sp, #0xC]
    ldr r0, =0x000927C0
    str r0, [sp, #0x10]

    mov r2, #0x9D
    add r2, #0x9F              ; Native work-block resource destination.
    ldr r0, [sp, #0x48]        ; Original ov117 state/context pointer.
    add r1, r4, #0
    ldr r0, [r0, #0x20]
    add r2, r4, r2
    bl Ov01_LoadSpriteResourcesFromOpenNarc

    mov r7, #0x4F              ; Recreate original loop setup.
    mov r6, #0
    add r5, r4, #0
    lsl r7, r7, #2

    add sp, #0x28
    pop {r4, pc}
    .pool

; Ordinary style-13 consumes its class-local assets before Overlay-1 exists. The name-ID cache is a two-byte metadata transfer only; no message archive or rendering dependency is introduced here.
TCM_CacheStyle13AssetQuartets:
    push {r4, lr}
    bl TCM_CacheStyle13SymbolQuartet
    sub sp, #0x08
    ldr r2, =TCM_CurrentTrainerClass
    ldr r2, [r2, #0x00]
    mov r0, #TCM_ASSET_GROUP2_QUARTET_SIZE
    str r0, [sp, #0x00]
    ldr r0, =TCM_Style13PortraitScratch
    mov r1, #NARC_a_1_5_5
    mov r3, #TCM_ASSET_GROUP2_QUARTET_OFFSET
    bl ReadFromNarcMemberByIdPair
    ; Four-byte tail transfer replaces the original add/pop epilogue without moving the established literal pool or portrait scratch.
    bl TCM_CacheStyle13NameIdAndReturn
    .pool

TCM_Style13PortraitScratch:
    .fill TCM_ASSET_GROUP2_QUARTET_SIZE, 0x00

; Non-returning tail leaf for TCM_CacheStyle13AssetQuartets. The outer 8-byte local area remains the outgoing-argument block and its saved r4/lr pair is unwound here after the second metadata read.
TCM_CacheStyle13NameIdAndReturn:
    ldr r2, =TCM_CurrentTrainerClass
    ldr r2, [r2, #0x00]
    mov r0, #TCM_TRAINER_NAME_SIZE
    str r0, [sp, #0x00]
    ldr r0, =TCM_Style13NameScratch
    mov r1, #NARC_a_1_5_5
    mov r3, #TCM_TRAINER_NAME_OFFSET
    bl ReadFromNarcMemberByIdPair
    add sp, #0x08
    pop {r4, pc}
    .pool

TCM_Style13NameScratch:
    .fill TCM_TRAINER_NAME_SIZE, 0x00

TCM_Ov120LoadRedBlocks_End:
    .align 4

; -----------------------------------------------------------------------------
; Overlay 115 20-byte VS portrait widening helper
; -----------------------------------------------------------------------------


TCM_Ov115LoadVs20PortraitWide:
    push {r3, r4, r5, r6, r7, lr}
    sub sp, #0x18

    ldr r0, =TCM_Vs20ScratchRecord
    cmp r6, r0
    bne TCM_Ov115LoadVs20PortraitWide_Fallback

    mov r0, #0x8
    str r0, [sp, #0x00]
    add r0, sp, #0x04
    mov r1, #NARC_a_1_5_5
    ldr r2, =TCM_CurrentTrainerClass
    ldr r2, [r2, #0x0]
    mov r3, #TCM_ASSET_GROUP2_QUARTET_OFFSET
    bl ReadFromNarcMemberByIdPair

    add r7, sp, #0x04
    ldrh r3, [r7, #0x00]
    ldrh r0, [r7, #0x02]
    str r0, [sp, #0x04]
    ldrh r0, [r7, #0x04]
    str r0, [sp, #0x08]
    ldrh r0, [r7, #0x06]
    str r0, [sp, #0x0C]
    b TCM_Ov115LoadVs20PortraitWide_Call

TCM_Ov115LoadVs20PortraitWide_Fallback:
    ldrb r3, [r6, #0x0C]
    ldrb r0, [r6, #0x0D]
    str r0, [sp, #0x04]
    ldrb r0, [r6, #0x0E]
    str r0, [sp, #0x08]
    ldrb r0, [r6, #0x0F]
    str r0, [sp, #0x0C]

TCM_Ov115LoadVs20PortraitWide_Call:
    mov r0, #1
    str r0, [sp, #0x00]
    ldr r0, =0x000927C0
    str r0, [sp, #0x10]
    mov r0, #0
    str r0, [sp, #0x14]

    mov r2, #6
    lsl r2, r2, #6
    add r2, r4, r2
    add r1, r4, #0
    add r1, #0x44
    ldr r0, [r5, #0x20]
    bl TCM_LoadMappedSpriteResourcesFromOpenNarc

    add sp, #0x18
    pop {r3, r4, r5, r6, r7, pc}
    .pool

TCM_Ov115SetupVs20PortraitAsset0Wide_State1:
    push {r4, r5, r6, r7, lr}
    sub sp, #0x0C

    str r0, [sp, #0x08]         ; Preserve original work-block offset.
    bl TCM_Ov115ReadVs20PortraitAsset0Wide

    mov r3, #0
    str r3, [sp, #0x00]         ; Vanilla stack arg for Ov115_SetupOpponentPortrait.
    add r2, r0, #0              ; Asset id argument for Ov115_SetupOpponentPortrait.
    ldr r0, [sp, #0x08]
    ldr r0, [r4, r0]
    add r1, r7, #0
    mov r3, #0x0E
    bl Ov115_SetupOpponentPortrait

    add sp, #0x0C
    pop {r4, r5, r6, r7, pc}

TCM_Ov115SetupVs20PortraitAsset0Wide_State7:
    push {r4, r5, r6, r7, lr}
    sub sp, #0x0C

    str r0, [sp, #0x08]         ; Preserve original work-block offset.
    bl TCM_Ov115ReadVs20PortraitAsset0Wide

    mov r3, #0
    str r3, [sp, #0x00]         ; Vanilla stack arg for Ov115_SetupOpponentPortrait.
    add r2, r0, #0              ; Asset id argument for Ov115_SetupOpponentPortrait.
    ldr r0, [sp, #0x08]
    ldr r0, [r4, r0]
    add r1, r7, #0
    mov r3, #0
    bl Ov115_SetupOpponentPortrait

    add sp, #0x0C
    pop {r4, r5, r6, r7, pc}

TCM_Ov115ReadVs20PortraitAsset0Wide:
    push {r3, lr}
    sub sp, #0x08
    mov r0, #2
    str r0, [sp, #0x00]
    add r0, sp, #0x04
    mov r1, #NARC_a_1_5_5
    ldr r2, =TCM_CurrentTrainerClass
    ldr r2, [r2, #0x0]
    mov r3, #TCM_ASSET_GROUP2_QUARTET_OFFSET
    bl ReadFromNarcMemberByIdPair
    add r0, sp, #0x04
    ldrh r0, [r0, #0x0]
    add sp, #0x08
    pop {r3, pc}
    .pool

TCM_Ov115LoadVs20BackgroundWide:
    push {r3, r4, r5, r6, r7, lr}
    sub sp, #0x18

    ldr r0, =TCM_Vs20ScratchRecord
    cmp r6, r0
    bne TCM_Ov115LoadVs20BackgroundWide_Fallback

    mov r0, #TCM_ASSET_GROUP1_PALETTE_IMAGE_SIZE
    str r0, [sp, #0x00]
    add r0, sp, #0x04
    mov r1, #NARC_a_1_5_5
    ldr r2, =TCM_CurrentTrainerClass
    ldr r2, [r2, #0x0]
    mov r3, #TCM_ASSET_GROUP1_QUARTET_OFFSET
    bl ReadFromNarcMemberByIdPair

    mov r0, #TCM_ASSET_GROUP1_RCSN_SIZE
    str r0, [sp, #0x00]
    add r0, sp, #0x08
    mov r1, #NARC_a_1_5_5
    ldr r2, =TCM_CurrentTrainerClass
    ldr r2, [r2, #0x0]
    mov r3, #TCM_ASSET_GROUP1_RCSN1_OFFSET
    bl ReadFromNarcMemberByIdPair

    add r7, sp, #0x04
    ldrh r3, [r7, #0x00]       ; Asset 0 / original record offset 0x10.
    ldrh r2, [r7, #0x02]       ; Asset 1 / original record offset 0x11.
    ldrh r1, [r7, #0x04]       ; Asset 2 / original record offset 0x12.
    b TCM_Ov115LoadVs20BackgroundWide_Call

TCM_Ov115LoadVs20BackgroundWide_Fallback:
    ldrb r1, [r6, #0x12]       ; Native order: asset2, asset1, asset0.
    ldrb r2, [r6, #0x11]
    ldrb r3, [r6, #0x10]

TCM_Ov115LoadVs20BackgroundWide_Call:
    mov r0, #0
    str r0, [sp, #0x00]
    mov r0, #1
    str r0, [sp, #0x04]
    ldr r0, [r5, #0x10]
    ldr r0, [r0, #0x08]
    str r0, [sp, #0x08]
    mov r0, #3
    str r0, [sp, #0x0C]
    ldr r0, [r5, #0x20]
    bl Ov01_LoadBgFromOpenNarc

    add sp, #0x18
    pop {r3, r4, r5, r6, r7, pc}
    .pool

TCM_Ov115LoadVs20PortraitWide_End:
    .align 4

; -----------------------------------------------------------------------------
; Overlay 115 20-byte VS standard symbol widening helper
; -----------------------------------------------------------------------------


TCM_Ov115LoadVs20SymbolWide:
    push {r3, r4, r5, r6, r7, lr}
    sub sp, #0x18

    ldr r0, =TCM_Vs20ScratchRecord
    cmp r6, r0
    bne TCM_Ov115LoadVs20SymbolWide_Fallback

    ldr r7, =TCM_Vs20SymbolScratch
    ldrh r3, [r7, #0x00]
    ldrh r0, [r7, #0x02]
    str r0, [sp, #0x04]
    ldrh r0, [r7, #0x04]
    str r0, [sp, #0x08]
    ldrh r0, [r7, #0x06]
    str r0, [sp, #0x0C]
    b TCM_Ov115LoadVs20SymbolWide_Call

TCM_Ov115LoadVs20SymbolWide_Fallback:
    bl GF_AssertFail
    b TCM_Ov115LoadVs20SymbolWide_Fallback

TCM_Ov115LoadVs20SymbolWide_Call:
    mov r0, #1
    str r0, [sp, #0x00]
    ldr r0, =0x000927C1
    str r0, [sp, #0x10]
    mov r0, #0
    str r0, [sp, #0x14]

    mov r2, #0x6D
    lsl r2, r2, #2
    add r2, r4, r2
    add r1, r4, #0
    add r1, #0x44
    ldr r0, [r5, #0x20]
    bl TCM_LoadMappedSpriteResourcesFromOpenNarc

    add sp, #0x18
    pop {r3, r4, r5, r6, r7, pc}
    .pool

TCM_Ov115LoadVs20SymbolWide_End:

TCM_Ov115LoadVs8SymbolWide:
    push {r3, r4, r5, r6, r7, lr}

    ; Original caller [sp, #0x18] is at [sp, #0x30] after this push.
    ldr r0, [sp, #0x30]
    ldr r7, =TCM_Vs8ScratchRecord
    cmp r0, r7
    bne TCM_Ov115LoadVs8SymbolWide_Assert

    sub sp, #0x18

    ldr r7, =TCM_Vs20SymbolScratch
    ldrh r3, [r7, #0x00]        ; Palette / RLCN.
    ldrh r0, [r7, #0x02]        ; Image / RGCN.
    str r0, [sp, #0x04]
    ldrh r0, [r7, #0x04]        ; Cell / RECN.
    str r0, [sp, #0x08]
    ldrh r0, [r7, #0x06]        ; Animation / RNAN.
    str r0, [sp, #0x0C]

    mov r0, #1
    str r0, [sp, #0x00]
    ldr r0, =0x000927C3
    str r0, [sp, #0x10]
    mov r0, #0
    str r0, [sp, #0x14]

    mov r2, #0x8D
    lsl r2, r2, #2
    add r2, r4, r2
    add r1, r4, #0
    add r1, #0x5C
    ldr r0, [r6, #0x20]
    bl TCM_LoadMappedSpriteResourcesFromOpenNarc

    ldr r0, [r6, #0x0]     ; Native 0x0225FABE restore before 0x0225FAC0 increment.
    add sp, #0x18
    pop {r3, r4, r5, r6, r7, pc}

TCM_Ov115LoadVs8SymbolWide_Assert:
    bl GF_AssertFail
    b TCM_Ov115LoadVs8SymbolWide_Assert
    .pool

TCM_Ov118LoadKimonoShutters:
    push {r4, r5, lr}
    sub sp, #0x24

    ; Compact read from vsStyle through group-1 RCSN 1 (0x0A..0x1F).
    mov r0, #0x16
    str r0, [sp, #0x0]

    add r0, sp, #0x4
    mov r1, #NARC_a_1_5_5
    ldr r2, =TCM_CurrentTrainerClass
    ldr r2, [r2, #0x0]
    mov r3, #TCM_VS_STYLE_OFFSET
    bl ReadFromNarcMemberByIdPair

    add r5, sp, #0x4
    ldrh r0, [r5, #0x0]         ; vsStyle.
    cmp r0, #TCM_VS_STYLE_KIMONO
    bne TCM_Ov118LoadKimonoShutters_Fail

    ldrh r3, [r5, #0x0C]        ; Group-1 palette/RLCN.
    ldrh r2, [r5, #0x0E]        ; Group-1 image/RGCN.
    ldrh r1, [r5, #0x14]        ; Group-1 screen/RCSN 1.

    mov r0, #0
    str r0, [sp, #0x0]
    str r0, [sp, #0x4]
    ldr r0, [r4, #0x10]
    ldr r0, [r0, #0x8]
    str r0, [sp, #0x8]
    mov r0, #1
    str r0, [sp, #0xC]
    mov r0, #3
    str r0, [sp, #0x10]
    ldr r0, [r4, #0x20]
    bl Ov01_LoadKimonoShuttersFromOpenNarc

    add sp, #0x24
    pop {r4, r5, pc}

TCM_Ov118LoadKimonoShutters_Fail:
    bl GF_AssertFail
    b TCM_Ov118LoadKimonoShutters_Fail
    .pool

TCM_Ov115LoadVs8SymbolWide_End:
    .align 4

; -----------------------------------------------------------------------------
; Overlay 119 static ordinary normal/default early asset helper
; -----------------------------------------------------------------------------

TCM_Ov119LoadNormalEarlySprite:
    push {r3, r4, r5, r6, lr}
    sub sp, #0x1C

    ldr r0, =TCM_CurrentOrdinaryStyle
    ldrh r0, [r0, #0x0]
    cmp r0, #TCM_VS_STYLE_NORMAL_EARLY
    beq TCM_Ov119LoadNormalEarlySprite_ReadAssets
    cmp r0, #0
    bne TCM_Ov119LoadNormalEarlySprite_Fail

TCM_Ov119LoadNormalEarlySprite_ReadAssets:
    mov r0, #TCM_STYLE0_ASSET_SIZE
    str r0, [sp, #0x0]

    add r0, sp, #0x14
    mov r1, #NARC_a_1_5_5
    ldr r2, =TCM_CurrentTrainerClass
    ldr r2, [r2, #0x0]
    mov r3, #TCM_STYLE0_SMALL_ASSET_OFFSET
    bl ReadFromNarcMemberByIdPair

    add r5, sp, #0x14
    ldrh r3, [r5, #0x0]         ; Palette/RLCN arg.
    ldrh r0, [r5, #0x2]         ; Image/RGCN arg.
    str r0, [sp, #0x4]
    ldrh r0, [r5, #0x4]         ; Cell/RECN arg.
    str r0, [sp, #0x8]
    ldrh r0, [r5, #0x6]         ; Anim/RNAN arg.
    str r0, [sp, #0xC]

TCM_Ov119LoadNormalEarlySprite_Call:
    mov r0, #1
    str r0, [sp, #0x0]
    ldr r0, =0x000927C0
    str r0, [sp, #0x10]

    mov r2, #0x5A
    lsl r2, r2, #0x2
    add r1, r4, #0
    add r1, #0x2C
    ldr r0, [r6, #0x20]
    add r2, r4, r2
    bl Ov01_LoadSpriteResourcesFromOpenNarc

    add sp, #0x1C
    pop {r3, r4, r5, r6, pc}

TCM_Ov119LoadNormalEarlySprite_Fail:
    bl GF_AssertFail
    b TCM_Ov119LoadNormalEarlySprite_Fail
    .pool

TCM_Ov119LoadNormalEarlySprite_End:
    .align 4

; -----------------------------------------------------------------------------
; Overlay 119 static ordinary normal/default late asset helper
; -----------------------------------------------------------------------------

TCM_Ov119LoadNormalLateSprite:
    push {r3, r4, r5, r7, lr}
    sub sp, #0x1C

    ldr r0, =TCM_CurrentOrdinaryStyle
    ldrh r0, [r0, #0x0]
    cmp r0, #TCM_VS_STYLE_NORMAL_LATE
    beq TCM_Ov119LoadNormalLateSprite_ReadSharedAssets
    cmp r0, #0
    bne TCM_Ov119LoadNormalLateSprite_Fail
    mov r3, #TCM_STYLE0_LARGE_ASSET_OFFSET
    b TCM_Ov119LoadNormalLateSprite_ReadAssets

TCM_Ov119LoadNormalLateSprite_ReadSharedAssets:
    mov r3, #TCM_ASSET_GROUP1_QUARTET_OFFSET

TCM_Ov119LoadNormalLateSprite_ReadAssets:
    mov r0, #TCM_STYLE0_ASSET_SIZE
    str r0, [sp, #0x0]

    add r0, sp, #0x14
    mov r1, #NARC_a_1_5_5
    ldr r2, =TCM_CurrentTrainerClass
    ldr r2, [r2, #0x0]
    bl ReadFromNarcMemberByIdPair

    add r5, sp, #0x14
    ldrh r3, [r5, #0x0]         ; Palette/RLCN arg.
    ldrh r0, [r5, #0x2]         ; Image/RGCN arg.
    str r0, [sp, #0x4]
    ldrh r0, [r5, #0x4]         ; Cell/RECN arg.
    str r0, [sp, #0x8]
    ldrh r0, [r5, #0x6]         ; Anim/RNAN arg.
    str r0, [sp, #0xC]

TCM_Ov119LoadNormalLateSprite_Call:
    mov r0, #1
    str r0, [sp, #0x0]
    ldr r0, =0x000927C0
    str r0, [sp, #0x10]

    mov r2, #0x61
    lsl r2, r2, #0x2
    add r1, r4, #0
    add r1, #0x48
    ldr r0, [r7, #0x20]
    add r2, r4, r2
    bl Ov01_LoadSpriteResourcesFromOpenNarc

    add sp, #0x1C
    pop {r3, r4, r5, r7, pc}

TCM_Ov119LoadNormalLateSprite_Fail:
    bl GF_AssertFail
    b TCM_Ov119LoadNormalLateSprite_Fail
    .pool

TCM_Ov119LoadNormalLateSprite_End:
    .align 4

; -----------------------------------------------------------------------------
; Overlay 119 static ordinary water-early asset helper
; -----------------------------------------------------------------------------

TCM_Ov119LoadWaterEarlySprite:
    ; At this hook r0 is a transient return value, not a NARC handle. Preserve the callee-saved registers and recover the outer context below after allocating locals.
    push {r0, r4, r5, r6, lr}
    sub sp, #0x1C

    ldr r0, =TCM_CurrentOrdinaryStyle
    ldrh r0, [r0, #0x0]
    cmp r0, #TCM_VS_STYLE_WATER_EARLY
    beq TCM_Ov119LoadWaterEarlySprite_ReadAssets
    cmp r0, #0
    bne TCM_Ov119LoadWaterEarlySprite_Fail

TCM_Ov119LoadWaterEarlySprite_ReadAssets:
    mov r0, #TCM_STYLE0_ASSET_SIZE
    str r0, [sp, #0x0]

    add r0, sp, #0x14
    mov r1, #NARC_a_1_5_5
    ldr r2, =TCM_CurrentTrainerClass
    ldr r2, [r2, #0x0]
    mov r3, #TCM_STYLE0_SMALL_ASSET_OFFSET
    bl ReadFromNarcMemberByIdPair

    add r5, sp, #0x14
    ldrh r3, [r5, #0x0]         ; Palette/RLCN arg.
    ldrh r0, [r5, #0x2]         ; Image/RGCN arg.
    str r0, [sp, #0x4]
    ldrh r0, [r5, #0x4]         ; Cell/RECN arg.
    str r0, [sp, #0x8]
    ldrh r0, [r5, #0x6]         ; Anim/RNAN arg.
    str r0, [sp, #0xC]

TCM_Ov119LoadWaterEarlySprite_Call:
    mov r0, #1
    str r0, [sp, #0x0]
    ldr r0, =0x000927C0
    str r0, [sp, #0x10]

    mov r2, #0x4F
    lsl r2, r2, #0x2
    add r1, r4, #0
    ; Outer [sp,#0x1C] is helper [sp,#0x4C] after push (0x14) + locals (0x1C).
    ldr r0, [sp, #0x4C]
    ldr r0, [r0, #0x20]         ; Original open a109 NARC handle.
    add r2, r4, r2
    bl Ov01_LoadSpriteResourcesFromOpenNarc

    add sp, #0x1C
    pop {r0, r4, r5, r6, pc}

TCM_Ov119LoadWaterEarlySprite_Fail:
    bl GF_AssertFail
    b TCM_Ov119LoadWaterEarlySprite_Fail
    .pool

TCM_Ov119LoadWaterEarlySprite_End:
    .align 4

; -----------------------------------------------------------------------------
; Overlay 119 static ordinary water-late asset helper
; -----------------------------------------------------------------------------

TCM_Ov119LoadWaterLateSprite:
    ; r4 is the water-late work allocation and r7 owns the open a109 NARC handle. Both remain live at the native continuation.
    push {r4, r5, r6, r7, lr}
    sub sp, #0x1C

    ldr r0, =TCM_CurrentOrdinaryStyle
    ldrh r0, [r0, #0x0]
    cmp r0, #TCM_VS_STYLE_WATER_LATE
    beq TCM_Ov119LoadWaterLateSprite_ReadSharedAssets
    cmp r0, #0
    bne TCM_Ov119LoadWaterLateSprite_Fail
    mov r3, #TCM_STYLE0_LARGE_ASSET_OFFSET
    b TCM_Ov119LoadWaterLateSprite_ReadAssets

TCM_Ov119LoadWaterLateSprite_ReadSharedAssets:
    mov r3, #TCM_ASSET_GROUP1_QUARTET_OFFSET

TCM_Ov119LoadWaterLateSprite_ReadAssets:
    mov r0, #TCM_STYLE0_ASSET_SIZE
    str r0, [sp, #0x0]

    add r0, sp, #0x14
    mov r1, #NARC_a_1_5_5
    ldr r2, =TCM_CurrentTrainerClass
    ldr r2, [r2, #0x0]
    bl ReadFromNarcMemberByIdPair

    add r5, sp, #0x14
    ldrh r3, [r5, #0x0]         ; Palette/RLCN arg.
    ldrh r0, [r5, #0x2]         ; Image/RGCN arg.
    str r0, [sp, #0x4]
    ldrh r0, [r5, #0x4]         ; Cell/RECN arg.
    str r0, [sp, #0x8]
    ldrh r0, [r5, #0x6]         ; Anim/RNAN arg.
    str r0, [sp, #0xC]

TCM_Ov119LoadWaterLateSprite_Call:
    mov r0, #1
    str r0, [sp, #0x0]
    ldr r0, =0x000927C0
    str r0, [sp, #0x10]

    mov r2, #0x63
    lsl r2, r2, #0x2
    add r1, r4, #0
    add r1, #0x50
    ldr r0, [r7, #0x20]         ; Original open a109 NARC handle.
    add r2, r4, r2
    bl Ov01_LoadSpriteResourcesFromOpenNarc

    add sp, #0x1C
    pop {r4, r5, r6, r7, pc}

TCM_Ov119LoadWaterLateSprite_Fail:
    bl GF_AssertFail
    b TCM_Ov119LoadWaterLateSprite_Fail
    .pool

TCM_Ov119LoadWaterLateSprite_End:
    .align 4

; -----------------------------------------------------------------------------
; Overlay 119 static ordinary cave-early asset helper
; -----------------------------------------------------------------------------

TCM_Ov119LoadCaveEarlySprite:
    ; The native cave-early caller keeps r4 live and recovers the open a109 handle from its outer stack frame. r3 is saved only to keep nested calls 8-byte aligned; r4 remains the sole live callee-saved input.
    push {r3, r4, lr}
    sub sp, #0x1C

    ldr r0, =TCM_CurrentOrdinaryStyle
    ldrh r0, [r0, #0x0]
    cmp r0, #TCM_VS_STYLE_CAVE_EARLY
    beq TCM_Ov119LoadCaveEarlySprite_ReadAssets
    cmp r0, #0
    bne TCM_Ov119LoadCaveEarlySprite_Fail

TCM_Ov119LoadCaveEarlySprite_ReadAssets:
    mov r0, #TCM_STYLE0_ASSET_SIZE
    str r0, [sp, #0x0]

    add r0, sp, #0x14
    mov r1, #NARC_a_1_5_5
    ldr r2, =TCM_CurrentTrainerClass
    ldr r2, [r2, #0x0]
    mov r3, #TCM_STYLE0_SMALL_ASSET_OFFSET
    bl ReadFromNarcMemberByIdPair

    add r0, sp, #0x14
    ldrh r3, [r0, #0x0]         ; Palette/RLCN arg.
    ldrh r1, [r0, #0x2]         ; Image/RGCN arg.
    str r1, [sp, #0x4]
    ldrh r1, [r0, #0x4]         ; Cell/RECN arg.
    str r1, [sp, #0x8]
    ldrh r0, [r0, #0x6]         ; Anim/RNAN arg.
    str r0, [sp, #0xC]

TCM_Ov119LoadCaveEarlySprite_Call:
    mov r0, #1
    str r0, [sp, #0x0]
    ldr r0, =0x000927C0
    str r0, [sp, #0x10]

    mov r2, #0x4F
    lsl r2, r2, #0x2
    add r1, r4, #0
    ; Native [sp,#0x18] is helper [sp,#0x40] after saved regs + locals.
    ldr r0, [sp, #0x40]
    ldr r0, [r0, #0x20]
    add r2, r4, r2
    bl Ov01_LoadSpriteResourcesFromOpenNarc

    add sp, #0x1C
    pop {r3, r4, pc}

TCM_Ov119LoadCaveEarlySprite_Fail:
    bl GF_AssertFail
    b TCM_Ov119LoadCaveEarlySprite_Fail
    .pool

TCM_Ov119LoadCaveEarlySprite_End:
    .align 4

; -----------------------------------------------------------------------------
; Overlay 119 static ordinary cave-late asset helper
; -----------------------------------------------------------------------------

TCM_Ov119LoadCaveLateSprite:
    ; Cave late uses a different native work object from cave early: the open a109 handle remains in r5 and the resource header begins at r4 + 0x5C. Preserve both registers for the native continuation at 0x0226001C.
    push {r4, r5, lr}
    sub sp, #0x1C

    ldr r0, =TCM_CurrentOrdinaryStyle
    ldrh r0, [r0, #0x0]
    cmp r0, #TCM_VS_STYLE_CAVE_LATE
    beq TCM_Ov119LoadCaveLateSprite_ReadAssets
    cmp r0, #0
    bne TCM_Ov119LoadCaveLateSprite_Fail

TCM_Ov119LoadCaveLateSprite_ReadAssets:
    mov r0, #TCM_STYLE0_ASSET_SIZE
    str r0, [sp, #0x0]

    add r0, sp, #0x14
    mov r1, #NARC_a_1_5_5
    ldr r2, =TCM_CurrentTrainerClass
    ldr r2, [r2, #0x0]
    mov r3, #TCM_STYLE0_SMALL_ASSET_OFFSET
    bl ReadFromNarcMemberByIdPair

    add r0, sp, #0x14
    ldrh r3, [r0, #0x0]         ; Palette/RLCN arg.
    ldrh r1, [r0, #0x2]         ; Image/RGCN arg.
    str r1, [sp, #0x4]
    ldrh r1, [r0, #0x4]         ; Cell/RECN arg.
    str r1, [sp, #0x8]
    ldrh r0, [r0, #0x6]         ; Anim/RNAN arg.
    str r0, [sp, #0xC]

TCM_Ov119LoadCaveLateSprite_Call:
    mov r0, #1
    str r0, [sp, #0x0]
    ldr r0, =0x000927C0
    str r0, [sp, #0x10]

    mov r2, #0x66
    lsl r2, r2, #0x2
    add r1, r4, #0
    add r1, #0x5C
    ldr r0, [r5, #0x20]         ; Original open a109 NARC handle.
    add r2, r4, r2
    bl Ov01_LoadSpriteResourcesFromOpenNarc

    add sp, #0x1C
    pop {r4, r5, pc}

TCM_Ov119LoadCaveLateSprite_Fail:
    bl GF_AssertFail
    b TCM_Ov119LoadCaveLateSprite_Fail
    .pool

TCM_Ov119LoadCaveLateSprite_End:
    .align 4

; r0=NARC*, r1=resource manager bundle, r2=SpriteResource*[4], r3=RLCN member.
; [sp+00]=palette bank count, [sp+04]=RGCN, [sp+08]=RECN, [sp+0C]=RNAN,
; [sp+10]=resource id base, [sp+14]=target mapping family (0=1D-32, 2=1D-128).
; This mirrors ov01_021F0614 but adapts loaded RGCN/RECN data before transfer.
TCM_LoadMappedSpriteResourcesFromOpenNarc:
    push {r3, r4, r5, r6, r7, lr}
    sub sp, #0x30

    add r7, r0, #0              ; Open a109 NARC.
    add r5, r1, #0              ; Resource manager bundle.
    add r4, r2, #0              ; Output SpriteResource*[4].
    str r3, [sp, #0x2C]         ; Palette file id.
    ldr r6, [sp, #0x58]         ; Resource id base.
    ldr r0, [sp, #0x5C]         ; Target family.
    str r0, [sp, #0x28]

    ; CHAR / RGCN.
    str r6, [sp, #0x00]         ; id
    mov r0, #1
    str r0, [sp, #0x04]         ; main OBJ
    mov r0, #4
    str r0, [sp, #0x08]         ; heap
    mov r0, #1
    str r0, [sp, #0x0C]         ; atEnd
    mov r0, #0x4B
    lsl r0, r0, #2
    ldr r0, [r5, r0]
    add r1, r7, #0
    ldr r2, [sp, #0x4C]         ; RGCN member
    mov r3, #0
    bl AddCharResObjFromOpenNarcWithAtEndFlag
    str r0, [r4, #0x00]
    ldr r1, [sp, #0x28]
    bl TCM_AdaptSpriteCharMapping

    ; PLTT / RLCN.
    str r6, [sp, #0x00]         ; id
    mov r0, #1
    str r0, [sp, #0x04]         ; main OBJ
    ldr r0, [sp, #0x48]
    str r0, [sp, #0x08]         ; palette bank count
    mov r0, #4
    str r0, [sp, #0x0C]         ; heap
    mov r0, #0x13
    lsl r0, r0, #4
    ldr r0, [r5, r0]
    add r1, r7, #0
    ldr r2, [sp, #0x2C]         ; RLCN member
    mov r3, #0
    bl AddPlttResObjFromOpenNarc
    str r0, [r4, #0x04]

    ; CELL / RECN.
    str r6, [sp, #0x00]         ; id
    mov r0, #2
    str r0, [sp, #0x04]         ; GF_GFX_RES_TYPE_CELL
    mov r0, #4
    str r0, [sp, #0x08]         ; heap
    mov r0, #0x4D
    lsl r0, r0, #2
    ldr r0, [r5, r0]
    add r1, r7, #0
    ldr r2, [sp, #0x50]         ; RECN member
    mov r3, #0
    bl AddCellOrAnimResObjFromOpenNarc
    str r0, [r4, #0x08]
    ldr r1, [sp, #0x28]
    bl TCM_AdaptSpriteCellMapping

    ; ANIM / RNAN.
    str r6, [sp, #0x00]         ; id
    mov r0, #3
    str r0, [sp, #0x04]         ; GF_GFX_RES_TYPE_ANIM
    mov r0, #4
    str r0, [sp, #0x08]         ; heap
    mov r0, #0x4E
    lsl r0, r0, #2
    ldr r0, [r5, r0]
    add r1, r7, #0
    ldr r2, [sp, #0x54]         ; RNAN member
    mov r3, #0
    bl AddCellOrAnimResObjFromOpenNarc
    str r0, [r4, #0x0C]

    ldr r0, [r4, #0x00]
    bl ObjCharRes_Transfer
    ldr r0, [r4, #0x00]
    bl ObjCharRes_DropRawData
    ldr r0, [r4, #0x04]
    bl ObjPlttRes_Transfer

    mov r0, #0
    mov r2, #0x4B
    str r6, [sp, #0x00]
    mvn r0, r0
    str r0, [sp, #0x04]
    str r0, [sp, #0x08]
    mov r0, #0
    str r0, [sp, #0x0C]
    str r0, [sp, #0x10]
    lsl r2, r2, #2
    ldr r1, [r5, r2]
    add r4, #0x10
    str r1, [sp, #0x14]
    add r1, r2, #4
    ldr r1, [r5, r1]
    add r3, r6, #0
    str r1, [sp, #0x18]
    add r1, r2, #0
    add r1, #8
    ldr r1, [r5, r1]
    add r2, #0x0C
    str r1, [sp, #0x1C]
    ldr r1, [r5, r2]
    add r2, r6, #0
    str r1, [sp, #0x20]
    str r0, [sp, #0x24]
    str r0, [sp, #0x28]
    add r0, r4, #0
    add r1, r6, #0
    bl CreateSpriteResourcesHeader

    add sp, #0x30
    pop {r3, r4, r5, r6, r7, pc}

; Replacement for ov80_0223A174's portrait char resource call.
; Entry mirrors SpriteSystem_LoadCharResObjFromOpenNarc:
;   r0=SpriteSystem*, r1=SpriteManager*, r2=open a109 NARC*, r3=RGCN member
;   caller [sp+00]=compressed, [sp+04]=vram, [sp+08]=resId
; Returns r0=1 on success, r0=0 if the native "id already present" guard fails.
TCM_Ov80LoadFrontierPortraitCharMapped:
    push {r4, r5, r6, r7, lr}
    sub sp, #0x0C

    add r5, r0, #0              ; SpriteSystem*.
    add r4, r1, #0              ; SpriteManager*.
    add r6, r2, #0              ; Open a109 NARC.
    add r7, r3, #0              ; RGCN member.

    ldr r0, [r4, #0x0C]         ; _2dGfxResMan[CHAR].
    ldr r1, [sp, #0x28]         ; Native resId.
    bl GF2DGfxResObjExistsById
    cmp r0, #0
    beq TCM_Ov80LoadFrontierPortraitChar_ReturnFalse

    ldr r0, [sp, #0x28]         ; id
    str r0, [sp, #0x00]
    ldr r0, [sp, #0x24]         ; vram
    str r0, [sp, #0x04]
    ldr r0, [r5, #0x00]         ; heap id
    str r0, [sp, #0x08]
    ldr r0, [r4, #0x0C]         ; _2dGfxResMan[CHAR].
    add r1, r6, #0
    add r2, r7, #0
    ldr r3, [sp, #0x20]         ; compressed
    bl AddCharResObjFromOpenNarc
    add r6, r0, #0
    cmp r6, #0
    beq TCM_Ov80LoadFrontierPortraitChar_Assert

    add r0, r6, #0
    mov r1, #2                  ; Frontier renderer expects 1D-128.
    bl TCM_AdaptSpriteCharMapping
    add r0, r6, #0
    bl ObjCharRes_Transfer
    ldr r0, [r4, #0x24]         ; _2dGfxResObjList[CHAR].
    add r1, r6, #0
    bl RegisterLoadedResources
    cmp r0, #1
    bne TCM_Ov80LoadFrontierPortraitChar_Assert
    mov r0, #1
    b TCM_Ov80LoadFrontierPortraitChar_Done

TCM_Ov80LoadFrontierPortraitChar_Assert:
    bl GF_AssertFail
    b TCM_Ov80LoadFrontierPortraitChar_Assert

TCM_Ov80LoadFrontierPortraitChar_ReturnFalse:
    mov r0, #0

TCM_Ov80LoadFrontierPortraitChar_Done:
    add sp, #0x0C
    pop {r4, r5, r6, r7, pc}

; Replacement for ov80_0223A174's portrait cell resource call.
; Entry mirrors SpriteSystem_LoadCellResObjFromOpenNarc:
;   r0=SpriteSystem*, r1=SpriteManager*, r2=open a109 NARC*, r3=RECN member
;   caller [sp+00]=compressed, [sp+04]=resId
; Returns r0=1 on success, r0=0 if the native "id already present" guard fails.
TCM_Ov80LoadFrontierPortraitCellMapped:
    push {r4, r5, r6, r7, lr}
    sub sp, #0x0C

    add r5, r0, #0              ; SpriteSystem*.
    add r4, r1, #0              ; SpriteManager*.
    add r6, r2, #0              ; Open a109 NARC.
    add r7, r3, #0              ; RECN member.

    ldr r0, [r4, #0x14]         ; _2dGfxResMan[CELL].
    ldr r1, [sp, #0x24]         ; Native resId.
    bl GF2DGfxResObjExistsById
    cmp r0, #0
    beq TCM_Ov80LoadFrontierPortraitCell_ReturnFalse

    ldr r0, [sp, #0x24]         ; id
    str r0, [sp, #0x00]
    mov r0, #2
    str r0, [sp, #0x04]         ; GF_GFX_RES_TYPE_CELL
    ldr r0, [r5, #0x00]         ; heap id
    str r0, [sp, #0x08]
    ldr r0, [r4, #0x14]         ; _2dGfxResMan[CELL].
    add r1, r6, #0
    add r2, r7, #0
    ldr r3, [sp, #0x20]         ; compressed
    bl AddCellOrAnimResObjFromOpenNarc
    add r6, r0, #0
    cmp r6, #0
    beq TCM_Ov80LoadFrontierPortraitCell_Assert

    add r0, r6, #0
    mov r1, #2                  ; Frontier renderer expects 1D-128.
    bl TCM_AdaptSpriteCellMapping
    ldr r0, [r4, #0x2C]         ; _2dGfxResObjList[CELL].
    add r1, r6, #0
    bl RegisterLoadedResources
    cmp r0, #1
    bne TCM_Ov80LoadFrontierPortraitCell_Assert
    mov r0, #1
    b TCM_Ov80LoadFrontierPortraitCell_Done

TCM_Ov80LoadFrontierPortraitCell_Assert:
    bl GF_AssertFail
    b TCM_Ov80LoadFrontierPortraitCell_Assert

TCM_Ov80LoadFrontierPortraitCell_ReturnFalse:
    mov r0, #0

TCM_Ov80LoadFrontierPortraitCell_Done:
    add sp, #0x0C
    pop {r4, r5, r6, r7, pc}

; Replacement for ov80_0223A174's symbol palette call.
; Entry mirrors SpriteSystem_LoadPaletteBufferFromOpenNarc:
;   r0=SpriteSystem*, r1=SpriteManager*, r2=open a109 NARC, r3=palette bank
;   caller [sp+04]=RLCN member, [sp+14]=resource id
; Returns the native palette-buffer index in r0.
TCM_Ov80LoadFrontierSymbolPalette:
    push {r4, lr}
    sub sp, #0x18

    ldr r4, [sp, #0x20]        ; Original caller [sp+00]: open NARC.
    str r4, [sp, #0x00]
    ldr r4, =TCM_Vs20SymbolScratch
    ldrh r4, [r4, #0x00]
    str r4, [sp, #0x04]        ; RLCN member from shared symbol quartet.
    ldr r4, [sp, #0x28]        ; Original caller [sp+08].
    str r4, [sp, #0x08]
    ldr r4, [sp, #0x2C]        ; Original caller [sp+0C].
    str r4, [sp, #0x0C]
    ldr r4, [sp, #0x30]        ; Original caller [sp+10].
    str r4, [sp, #0x10]
    ldr r4, [sp, #0x34]        ; Original caller [sp+14]: resource id.
    str r4, [sp, #0x14]

    bl SpriteSystem_LoadPaletteBufferFromOpenNarc
    add sp, #0x18
    pop {r4, pc}

; Replacement for ov80_0223A174's symbol char resource call.
; Entry mirrors SpriteSystem_LoadCharResObjFromOpenNarc:
;   r0=SpriteSystem*, r1=SpriteManager*, r2=open a109 NARC*, r3=RGCN member
;   caller [sp+00]=compressed, [sp+04]=vram, [sp+08]=resId
; The native Frontier symbol char resource id is 0x000007DB.
TCM_Ov80LoadFrontierSymbolCharMapped:
    push {r4, r5, r6, r7, lr}
    sub sp, #0x0C

    add r5, r0, #0              ; SpriteSystem*.
    add r4, r1, #0              ; SpriteManager*.
    add r6, r2, #0              ; Open a109 NARC.
    ldr r7, =TCM_Vs20SymbolScratch
    ldrh r7, [r7, #0x02]        ; RGCN member from shared symbol quartet.

    ldr r0, [r4, #0x0C]         ; _2dGfxResMan[CHAR].
    ldr r1, [sp, #0x28]         ; Native resId.
    bl GF2DGfxResObjExistsById
    cmp r0, #0
    beq TCM_Ov80LoadFrontierSymbolChar_ReturnFalse

    ldr r0, [sp, #0x28]         ; id
    str r0, [sp, #0x00]
    ldr r0, [sp, #0x24]         ; vram
    str r0, [sp, #0x04]
    ldr r0, [r5, #0x00]         ; heap id
    str r0, [sp, #0x08]
    ldr r0, [r4, #0x0C]         ; _2dGfxResMan[CHAR].
    add r1, r6, #0
    add r2, r7, #0
    ldr r3, [sp, #0x20]         ; compressed
    bl AddCharResObjFromOpenNarc
    add r6, r0, #0
    cmp r6, #0
    beq TCM_Ov80LoadFrontierSymbolChar_Assert

    add r0, r6, #0
    mov r1, #2                  ; Frontier renderer expects 1D-128.
    bl TCM_AdaptSpriteCharMapping
    add r0, r6, #0
    bl ObjCharRes_Transfer
    ldr r0, [r4, #0x24]         ; _2dGfxResObjList[CHAR].
    add r1, r6, #0
    bl RegisterLoadedResources
    cmp r0, #1
    bne TCM_Ov80LoadFrontierSymbolChar_Assert
    mov r0, #1
    b TCM_Ov80LoadFrontierSymbolChar_Done

TCM_Ov80LoadFrontierSymbolChar_Assert:
    bl GF_AssertFail
    b TCM_Ov80LoadFrontierSymbolChar_Assert

TCM_Ov80LoadFrontierSymbolChar_ReturnFalse:
    mov r0, #0

TCM_Ov80LoadFrontierSymbolChar_Done:
    add sp, #0x0C
    pop {r4, r5, r6, r7, pc}

; Replacement for ov80_0223A174's symbol cell resource call.
; Entry mirrors SpriteSystem_LoadCellResObjFromOpenNarc:
;   r0=SpriteSystem*, r1=SpriteManager*, r2=open a109 NARC*, r3=RECN member
;   caller [sp+00]=compressed, [sp+04]=resId
; The native Frontier symbol cell resource id is 0x000007D3.
TCM_Ov80LoadFrontierSymbolCellMapped:
    push {r4, r5, r6, r7, lr}
    sub sp, #0x0C

    add r5, r0, #0              ; SpriteSystem*.
    add r4, r1, #0              ; SpriteManager*.
    add r6, r2, #0              ; Open a109 NARC.
    ldr r7, =TCM_Vs20SymbolScratch
    ldrh r7, [r7, #0x04]        ; RECN member from shared symbol quartet.

    ldr r0, [r4, #0x14]         ; _2dGfxResMan[CELL].
    ldr r1, [sp, #0x24]         ; Native resId.
    bl GF2DGfxResObjExistsById
    cmp r0, #0
    beq TCM_Ov80LoadFrontierSymbolCell_ReturnFalse

    ldr r0, [sp, #0x24]         ; id
    str r0, [sp, #0x00]
    mov r0, #2
    str r0, [sp, #0x04]         ; GF_GFX_RES_TYPE_CELL
    ldr r0, [r5, #0x00]         ; heap id
    str r0, [sp, #0x08]
    ldr r0, [r4, #0x14]         ; _2dGfxResMan[CELL].
    add r1, r6, #0
    add r2, r7, #0
    ldr r3, [sp, #0x20]         ; compressed
    bl AddCellOrAnimResObjFromOpenNarc
    add r6, r0, #0
    cmp r6, #0
    beq TCM_Ov80LoadFrontierSymbolCell_Assert

    add r0, r6, #0
    mov r1, #2                  ; Frontier renderer expects 1D-128.
    bl TCM_AdaptSpriteCellMapping
    ldr r0, [r4, #0x2C]         ; _2dGfxResObjList[CELL].
    add r1, r6, #0
    bl RegisterLoadedResources
    cmp r0, #1
    bne TCM_Ov80LoadFrontierSymbolCell_Assert
    mov r0, #1
    b TCM_Ov80LoadFrontierSymbolCell_Done

TCM_Ov80LoadFrontierSymbolCell_Assert:
    bl GF_AssertFail
    b TCM_Ov80LoadFrontierSymbolCell_Assert

TCM_Ov80LoadFrontierSymbolCell_ReturnFalse:
    mov r0, #0

TCM_Ov80LoadFrontierSymbolCell_Done:
    add sp, #0x0C
    pop {r4, r5, r6, r7, pc}

; Replacement for ov80_0223A174's symbol animation call.
; Entry mirrors SpriteSystem_LoadAnimResObjFromOpenNarc:
;   r0=SpriteSystem*, r1=SpriteManager*, r2=open a109 NARC*, r3=RNAN member
;   caller [sp+00]=compressed, [sp+04]=resource id
TCM_Ov80LoadFrontierSymbolAnim:
    push {r4, lr}
    sub sp, #0x08

    ldr r4, [sp, #0x10]        ; Original caller [sp+00]: compressed flag.
    str r4, [sp, #0x00]
    ldr r4, [sp, #0x14]        ; Original caller [sp+04]: resource id.
    str r4, [sp, #0x04]

    ldr r4, =TCM_Vs20SymbolScratch
    ldrh r3, [r4, #0x06]
    bl SpriteSystem_LoadAnimResObjFromOpenNarc
    add sp, #0x08
    pop {r4, pc}

    .pool

; r0=SpriteResource*, r1=target family (0=1D-32, 2=1D-128).
TCM_AdaptSpriteCharMapping:
    push {r4, r5, r6, lr}
    cmp r0, #0
    beq TCM_AdaptSpriteChar_Assert
    cmp r1, #0
    beq TCM_AdaptSpriteChar_Target32
    cmp r1, #2
    bne TCM_AdaptSpriteChar_Assert
    mov r6, #2
    lsl r6, r6, #20
    add r6, #0x10
    b TCM_AdaptSpriteChar_TargetReady

TCM_AdaptSpriteChar_Target32:
    mov r6, #0x10

TCM_AdaptSpriteChar_TargetReady:
    ldr r4, [r0, #0x08]         ; CharResExtraData*.
    cmp r4, #0
    beq TCM_AdaptSpriteChar_Assert
    ldr r4, [r4, #0x00]         ; NNSG2dCharacterData*.
    cmp r4, #0
    beq TCM_AdaptSpriteChar_Assert
    ldr r5, [r4, #0x08]         ; mapingType.
    cmp r5, r6
    beq TCM_AdaptSpriteChar_Write
    cmp r5, #0x10
    beq TCM_AdaptSpriteChar_Write
    ldr r0, =0x00200010
    cmp r5, r0
    bne TCM_AdaptSpriteChar_Assert

TCM_AdaptSpriteChar_Write:
    str r6, [r4, #0x08]
    pop {r4, r5, r6, pc}

TCM_AdaptSpriteChar_Assert:
    bl GF_AssertFail
    b TCM_AdaptSpriteChar_Assert
    .pool

; r0=SpriteResource*, r1=target family (0=1D-32, 2=1D-128).
TCM_AdaptSpriteCellMapping:
    push {r4, r5, r6, r7, lr}
    sub sp, #0x0C
    str r1, [sp, #0x00]         ; target family
    cmp r0, #0
    beq TCM_AdaptSpriteCell_Assert
    cmp r1, #0
    beq TCM_AdaptSpriteCell_TargetOK
    cmp r1, #2
    bne TCM_AdaptSpriteCell_Assert

TCM_AdaptSpriteCell_TargetOK:
    ldr r4, [r0, #0x08]         ; CellResExtraData*.
    cmp r4, #0
    beq TCM_AdaptSpriteCell_Assert
    ldr r4, [r4, #0x00]         ; NNSG2dCellDataBank*.
    cmp r4, #0
    beq TCM_AdaptSpriteCell_Assert
    str r4, [sp, #0x08]         ; Preserve bank pointer for concluding mapping write.
    ldr r6, [r4, #0x08]         ; source mappingMode.
    ldr r0, [sp, #0x00]
    cmp r6, r0
    beq TCM_AdaptSpriteCell_Done
    cmp r6, #0
    beq TCM_AdaptSpriteCell_SourceOK
    cmp r6, #2
    bne TCM_AdaptSpriteCell_Assert

TCM_AdaptSpriteCell_SourceOK:
    ldrh r5, [r4, #0x00]        ; numCells.
    cmp r5, #0
    beq TCM_AdaptSpriteCell_Assert
    ldrh r7, [r4, #0x02]        ; cellBankAttr.
    ldr r4, [r4, #0x04]         ; pCellDataArrayHead.
    cmp r4, #0
    beq TCM_AdaptSpriteCell_Assert
    mov r0, #8
    mov r1, #1
    tst r7, r1
    beq TCM_AdaptSpriteCell_StoreStride
    add r0, #8

TCM_AdaptSpriteCell_StoreStride:
    str r0, [sp, #0x04]         ; cell stride.

TCM_AdaptSpriteCell_CellLoop:
    ldrh r7, [r4, #0x00]        ; numOAMAttrs.
    ldr r0, [r4, #0x04]         ; pOamAttrArray.
    cmp r7, #0
    beq TCM_AdaptSpriteCell_NextCell
    cmp r0, #0
    beq TCM_AdaptSpriteCell_Assert

TCM_AdaptSpriteCell_OamLoop:
    ldrh r1, [r0, #0x04]        ; attr2.
    ldr r2, =0x000003FF
    add r3, r1, #0
    and r3, r2                  ; tile index.
    bic r1, r2                  ; preserved attr2 high bits.
    ldr r2, [sp, #0x00]
    cmp r6, #0
    bne TCM_AdaptSpriteCell_128To32

    ; source 1D-32 -> target 1D-128.
    cmp r2, #2
    bne TCM_AdaptSpriteCell_Assert
    mov r2, #3
    tst r3, r2
    bne TCM_AdaptSpriteCell_Assert
    lsr r3, r3, #2
    b TCM_AdaptSpriteCell_WriteOam

TCM_AdaptSpriteCell_128To32:
    cmp r2, #0
    bne TCM_AdaptSpriteCell_Assert
    lsl r3, r3, #2
    ldr r2, =0x000003FF
    cmp r3, r2
    bhi TCM_AdaptSpriteCell_Assert

TCM_AdaptSpriteCell_WriteOam:
    orr r1, r3
    strh r1, [r0, #0x04]
    add r0, #6
    sub r7, #1
    bne TCM_AdaptSpriteCell_OamLoop

TCM_AdaptSpriteCell_NextCell:
    ldr r0, [sp, #0x04]
    add r4, r4, r0
    sub r5, #1
    bne TCM_AdaptSpriteCell_CellLoop

    ldr r4, [sp, #0x08]
    ldr r0, [sp, #0x00]
    str r0, [r4, #0x08]
    add sp, #0x0C
    pop {r4, r5, r6, r7, pc}

TCM_AdaptSpriteCell_Done:
    add sp, #0x0C
    pop {r4, r5, r6, r7, pc}

TCM_AdaptSpriteCell_Assert:
    bl GF_AssertFail
    b TCM_AdaptSpriteCell_Assert
    .pool

; r0=NARC*, r1=RECN member id, r2=heap id
; returns r0=max(RECN OAM attr2 palette bank) + 1.
; The temporary raw RECN allocation returned by the native loader is released
; before return. Cell-bank entries are 8 bytes normally or 16 bytes when the
; native cellBankAttr transfer-data bit is set.
TCM_Vs8GetPaletteBankCountFromOpenNarc:
    push {r4, r5, r6, r7, lr}
    sub sp, #0x0C

    str r2, [sp, #0x00]         ; Fifth native argument: heap id.
    add r3, sp, #0x04            ; Native out: unpacked NNSG2dCellDataBank*.
    mov r2, #0                   ; RECN resources are not compressed here.
    bl GfGfxLoader_GetCellBankFromOpenNarc
    cmp r0, #0
    beq TCM_Vs8PaletteCountOpenNarc_Assert

    ldr r4, [sp, #0x04]
    cmp r4, #0
    beq TCM_Vs8PaletteCountOpenNarc_Assert
    str r0, [sp, #0x04]          ; Retain raw allocation for Heap_Free.

    ldrh r5, [r4, #0x00]         ; NNSG2dCellDataBank::numCells.
    cmp r5, #0
    beq TCM_Vs8PaletteCountOpenNarc_Assert
    ldrh r7, [r4, #0x02]         ; NNSG2dCellDataBank::cellBankAttr.
    ldr r4, [r4, #0x04]          ; NNSG2dCellDataBank::pCellDataArrayHead.
    cmp r4, #0
    beq TCM_Vs8PaletteCountOpenNarc_Assert

    mov r6, #0                   ; Maximum palette bank observed.
    mov r3, #8                   ; sizeof(NNSG2dCellData).
    mov r2, #1
    tst r7, r2
    beq TCM_Vs8PaletteCountOpenNarc_CellLoop
    add r3, #8                   ; Transfer-enabled entries carry 8 extra bytes.

TCM_Vs8PaletteCountOpenNarc_CellLoop:
    ldrh r0, [r4, #0x00]         ; NNSG2dCellData::numOAMAttrs.
    ldr r1, [r4, #0x04]          ; NNSG2dCellData::pOamAttrArray.
    cmp r0, #0
    beq TCM_Vs8PaletteCountOpenNarc_NextCell
    cmp r1, #0
    beq TCM_Vs8PaletteCountOpenNarc_Assert

TCM_Vs8PaletteCountOpenNarc_OamLoop:
    ldrh r2, [r1, #0x04]         ; NNSG2dCellOAMAttrData::attr2.
    lsr r2, r2, #12              ; OBJ palette bank selector, 0..15.
    cmp r2, r6
    bls TCM_Vs8PaletteCountOpenNarc_NotHigher
    add r6, r2, #0
TCM_Vs8PaletteCountOpenNarc_NotHigher:
    add r1, #6                   ; sizeof(NNSG2dCellOAMAttrData).
    sub r0, #1
    bne TCM_Vs8PaletteCountOpenNarc_OamLoop

TCM_Vs8PaletteCountOpenNarc_NextCell:
    add r4, r3
    sub r5, #1
    bne TCM_Vs8PaletteCountOpenNarc_CellLoop

    cmp r6, #0x0F
    bhi TCM_Vs8PaletteCountOpenNarc_Assert
    add r0, r6, #1
    push {r0, r3}
    ldr r0, [sp, #0x0C]
    bl Heap_Free
    pop {r0, r3}
    add sp, #0x0C
    pop {r4, r5, r6, r7, pc}

TCM_Vs8PaletteCountOpenNarc_Assert:
    bl GF_AssertFail
    b TCM_Vs8PaletteCountOpenNarc_Assert
    .pool

; r0=Sprite*. This mirrors the RECN scan above over the live unpacked cell bank. It deliberately derives from the loaded sprite so blend allocation cannot drift from the active animation/cell resource.
TCM_Vs8GetPaletteBankCountFromSprite:
    push {r4, r5, r6, r7, lr}
    sub sp, #0x04

    ldr r4, [r0, #0x40]          ; Sprite::animationData[0] == cellBank.
    cmp r4, #0
    beq TCM_Vs8PaletteCountSprite_Assert
    ldrh r5, [r4, #0x00]
    cmp r5, #0
    beq TCM_Vs8PaletteCountSprite_Assert
    ldrh r7, [r4, #0x02]
    ldr r4, [r4, #0x04]
    cmp r4, #0
    beq TCM_Vs8PaletteCountSprite_Assert

    mov r6, #0
    mov r3, #8
    mov r2, #1
    tst r7, r2
    beq TCM_Vs8PaletteCountSprite_CellLoop
    add r3, #8

TCM_Vs8PaletteCountSprite_CellLoop:
    ldrh r0, [r4, #0x00]
    ldr r1, [r4, #0x04]
    cmp r0, #0
    beq TCM_Vs8PaletteCountSprite_NextCell
    cmp r1, #0
    beq TCM_Vs8PaletteCountSprite_Assert

TCM_Vs8PaletteCountSprite_OamLoop:
    ldrh r2, [r1, #0x04]
    lsr r2, r2, #12
    cmp r2, r6
    bls TCM_Vs8PaletteCountSprite_NotHigher
    add r6, r2, #0
TCM_Vs8PaletteCountSprite_NotHigher:
    add r1, #6
    sub r0, #1
    bne TCM_Vs8PaletteCountSprite_OamLoop

TCM_Vs8PaletteCountSprite_NextCell:
    add r4, r3
    sub r5, #1
    bne TCM_Vs8PaletteCountSprite_CellLoop

    cmp r6, #0x0F
    bhi TCM_Vs8PaletteCountSprite_Assert
    add r0, r6, #1
    add sp, #0x04
    pop {r4, r5, r6, r7, pc}

TCM_Vs8PaletteCountSprite_Assert:
    bl GF_AssertFail
    b TCM_Vs8PaletteCountSprite_Assert
    .pool

; Replacement for the two Style-2 opponent calls to Ov115_SetupOpponentPortrait. Input contract is unchanged: r0=Sprite*, r1=heap, r2=RLCN member, r3=blend amount, [sp]=blend target. Unlike the native helper, this covers every palette bank selected by the active portrait RECN.
TCM_Ov115BlendVs8OpponentPortraitWide:
    push {r3, r4, r5, r6, r7, lr}
    sub sp, #0x10
    add r5, r1, #0
    add r7, r0, #0
    add r6, r2, #0
    str r3, [sp, #0x04]

    add r0, r7, #0
    bl TCM_Vs8GetPaletteBankCountFromSprite
    str r0, [sp, #0x00]
    str r0, [sp, #0x0C]          ; Preserve count across BlendPalette.
    lsl r1, r0, #5               ; 0x20 bytes per OBJ palette bank.
    add r0, r5, #0
    bl Heap_Alloc
    cmp r0, #0
    beq TCM_Ov115BlendVs8OpponentPortraitWide_Assert
    add r4, r0, #0

    mov r0, #0x6D                ; NARC /a/1/0/9.
    add r1, r6, #0
    add r2, sp, #0x08
    add r3, r5, #0
    bl GfGfxLoader_GetPlttData
    cmp r0, #0
    beq TCM_Ov115BlendVs8OpponentPortraitWide_Assert
    add r5, r0, #0

    ldr r2, [sp, #0x00]
    lsl r2, r2, #4               ; 16 colours per palette bank.
    add r0, sp, #0x18
    ldrh r0, [r0, #0x10]         ; Original fifth argument after this frame.
    str r0, [sp, #0x00]
    ldr r0, [sp, #0x08]
    ldr r0, [r0, #0x0C]
    add r1, r4, #0
    ldr r3, [sp, #0x04]
    bl BlendPalette

    add r0, r7, #0
    bl Sprite_GetPaletteProxy
    add r6, r0, #0
    add r0, r4, #0
    ldr r1, [sp, #0x0C]
    lsl r1, r1, #5
    blx DC_FlushRange
    add r0, r6, #0
    mov r1, #1
    blx NNS_G2dGetImagePaletteLocation
    add r1, r0, #0
    add r0, r4, #0
    ldr r2, [sp, #0x0C]
    lsl r2, r2, #5
    blx GX_LoadOBJPltt

    add r0, r4, #0
    bl Heap_Free
    add r0, r5, #0
    bl Heap_Free
    add sp, #0x10
    pop {r3, r4, r5, r6, r7, pc}

TCM_Ov115BlendVs8OpponentPortraitWide_Assert:
    bl GF_AssertFail
    b TCM_Ov115BlendVs8OpponentPortraitWide_Assert
    .pool

; -----------------------------------------------------------------------------
; Ordinary Style 13 renderer.
;
; The wrapper replaces the first eight bytes of ov01_021EFB38. Other styles recreate the native table dispatch and rejoin its preserved tail. Style 13 owns the complete ordinary background, symbol, portrait, name, palette, timing, restoration, and cleanup lifecycle in the ordinary effect environment. Native Frontier Brain execution remains in Overlay 80.
; r0 = SysTask*, r1 = ordinary Overlay-1 effect environment* at entry.
; -----------------------------------------------------------------------------

TCM_Ov01EffectDispatchWrapper:
    push {r3, lr}

    ldr r2, =TCM_CurrentOrdinaryStyle
    ldrh r2, [r2, #0x0]
    cmp r2, #TCM_VS_STYLE_FRONTIER_BRAIN
    beq TCM_Ov01Style13Dispatch

    ; Recreate ov01_021EFB38's overwritten native table dispatch.
    ldr r2, =Ov01_EffectDispatch_Global
    ldr r2, [r2, #0x4]
    lsl r3, r2, #0x2
    ldr r2, =Ov01_EffectDispatch_Table
    ldr r2, [r2, r3]
    blx r2
    b TCM_Ov01EffectDispatchWrapper_Tail

TCM_Ov01Style13Dispatch:
    push {r4-r7}
    add r4, r0, #0              ; SysTask*.
    add r5, r1, #0              ; Ordinary effect environment*.
    ldr r6, [r5, #0x0C]         ; Cleanup-owned work block.
    cmp r6, #0
    bne TCM_Ov01Style13HaveWork

    mov r0, #4
    ldr r1, =TCM_STYLE13_WORK_SIZE
    bl Heap_Alloc
    cmp r0, #0
    bne TCM_Ov01Style13DispatchHaveAllocation
    ldr r0, =(TCM_Ov01Style13Fail | 1)
    bx r0
TCM_Ov01Style13DispatchHaveAllocation:
    add r6, r0, #0
    mov r1, #0
    ldr r2, =TCM_STYLE13_WORK_SIZE
    blx memset
    str r6, [r5, #0x0C]

    ; The shared symbol and portrait quartets consume five SpriteList entries. Reserve one additional inert slot per proved native name-planner node. Every pre-planner failure returns zero and therefore preserves capacity 5. Same four-byte footprint as the displaced bundle-address sequence. The helper caches the u16 ID, proves temporary name ownership, then returns the original bundle address in r0.
    bl TCM_Ov01Style13CacheNameIdAndGetBundle
    add r1, #TCM_STYLE13_SPRITELIST_CAPACITY
    mov r2, #TCM_STYLE13_RESOURCE_SET_CAPACITY
    bl Ov01_InitSpriteResourceBundle
    add r0, r5, #0
    bl TCM_Ov01Style13InitSymbolGate
    add r0, r5, #0
    bl TCM_Ov01Style13InitPortrait

    ; Capture only. Display/window registers are untouched until white.
    add r0, r5, #0
    bl TCM_Ov01Style13InitNamePaletteAndCapturePreBanner

TCM_Ov01Style13HaveWork:
    ; Submit the ordinary sprite list once per lifecycle tick. Symbols remain hidden until state 7 but their RNAN state follows native state-0 setup.
    ldr r0, =TCM_STYLE13_WORK_BUNDLE
    add r0, r6, r0
    ldr r0, [r0, #0x00]
    cmp r0, #0
    bne TCM_Ov01Style13DispatchHaveSpriteList
    ldr r0, =(TCM_Ov01Style13Fail | 1)
    bx r0
TCM_Ov01Style13DispatchHaveSpriteList:
    bl SpriteList_RenderAndAnimateSprites

    ; Native ov80_0223AA4C advances on the main lifecycle. VWait transfers only the selected frame, and frame zero remains intact before task setup.
    ldr r0, [r6, #TCM_STYLE13_WORK_BG_VWAIT_TASK]
    cmp r0, #0
    beq TCM_Ov01Style13DispatchState
    ldr r0, [r6, #TCM_STYLE13_WORK_BG_SELECTOR]
    add r0, #1
    cmp r0, #TCM_STYLE13_BG_FRAME_COUNT
    blo TCM_Ov01Style13StoreBgSelector
    mov r0, #0
TCM_Ov01Style13StoreBgSelector:
    str r0, [r6, #TCM_STYLE13_WORK_BG_SELECTOR]

TCM_Ov01Style13DispatchState:
    ldr r0, [r6, #TCM_STYLE13_WORK_STATE]
    cmp r0, #TCM_STYLE13_STATE_WAIT_FADE_IDLE
    bne TCM_Ov01Style13CheckToWhite
    b TCM_Ov01Style13WaitFadeIdle

TCM_Ov01Style13CheckToWhite:
    cmp r0, #TCM_STYLE13_STATE_WAIT_TO_WHITE
    bne TCM_Ov01Style13CheckArm
    b TCM_Ov01Style13WaitToWhite

TCM_Ov01Style13CheckArm:
    cmp r0, #TCM_STYLE13_STATE_ARM_UNDER_WHITE
    bne TCM_Ov01Style13CheckFromWhite
    b TCM_Ov01Style13ArmUnderWhite

TCM_Ov01Style13CheckFromWhite:
    cmp r0, #TCM_STYLE13_STATE_BEGIN_FROM_WHITE
    bne TCM_Ov01Style13CheckWaitFromWhite
    b TCM_Ov01Style13BeginFromWhite

TCM_Ov01Style13CheckWaitFromWhite:
    cmp r0, #TCM_STYLE13_STATE_WAIT_FROM_WHITE
    bne TCM_Ov01Style13CheckExpand
    b TCM_Ov01Style13WaitFromWhite

TCM_Ov01Style13CheckExpand:
    cmp r0, #TCM_STYLE13_STATE_EXPAND_BANNER
    bne TCM_Ov01Style13CheckFinalCommit
    b TCM_Ov01Style13ExpandBanner

TCM_Ov01Style13CheckFinalCommit:
    cmp r0, #TCM_STYLE13_STATE_WAIT_FINAL_COMMIT
    bne TCM_Ov01Style13CheckPostClampCompletion
    b TCM_Ov01Style13WaitFinalCommit

TCM_Ov01Style13CheckPostClampCompletion:
    cmp r0, #TCM_STYLE13_STATE_POST_CLAMP_COMPLETION
    bne TCM_Ov01Style13CheckRevealSymbols
    b TCM_Ov01Style13PostClampCompletion

TCM_Ov01Style13CheckRevealSymbols:
    cmp r0, #TCM_STYLE13_STATE_REVEAL_SYMBOL_GROUP
    bne TCM_Ov01Style13CheckState8
    b TCM_Ov01Style13RevealSymbolGroup

TCM_Ov01Style13CheckState8:
    cmp r0, #TCM_STYLE13_STATE_STATE8_SEPARATOR
    beq TCM_Ov01Style13State8Teardown
    ; States 9..12 share the state-9 router. It retains the fail path for every other state, while allowing the separator's next callback to expose and slide the already-created portrait.
    b TCM_Ov01Style13State9Dispatch
    nop

TCM_Ov01Style13WaitFadeIdle:
    bl IsPaletteFadeFinished
    cmp r0, #0
    beq TCM_Ov01Style13Return
    bl TCM_Ov01Style13BeginFadeToWhite
    mov r0, #TCM_STYLE13_STATE_WAIT_TO_WHITE
    str r0, [r6, #TCM_STYLE13_WORK_STATE]
    b TCM_Ov01Style13Return

TCM_Ov01Style13WaitToWhite:
    bl IsPaletteFadeFinished
    cmp r0, #0
    beq TCM_Ov01Style13Return
    mov r0, #TCM_STYLE13_STATE_ARM_UNDER_WHITE
    str r0, [r6, #TCM_STYLE13_WORK_STATE]
    b TCM_Ov01Style13Return

TCM_Ov01Style13ArmUnderWhite:
    add r0, r5, #0
    bl TCM_Ov01Style13LoadBackground
    add r0, r5, #0
    bl TCM_Ov01Style13ActivatePreBanner
    add r0, r5, #0
    bl TCM_Ov01Style13InitBackgroundCycle
    mov r0, #TCM_STYLE13_STATE_BEGIN_FROM_WHITE
    str r0, [r6, #TCM_STYLE13_WORK_STATE]
    b TCM_Ov01Style13Return

TCM_Ov01Style13BeginFromWhite:
    bl TCM_Ov01Style13BeginFadeFromWhite
    mov r0, #TCM_STYLE13_STATE_WAIT_FROM_WHITE
    str r0, [r6, #TCM_STYLE13_WORK_STATE]
    b TCM_Ov01Style13Return

TCM_Ov01Style13WaitFromWhite:
    bl IsPaletteFadeFinished
    cmp r0, #0
    beq TCM_Ov01Style13Return
    mov r0, #TCM_STYLE13_STATE_EXPAND_BANNER
    str r0, [r6, #TCM_STYLE13_WORK_STATE]
    b TCM_Ov01Style13Return

TCM_Ov01Style13ExpandBanner:
    mov r7, #0xC0
    lsl r7, r7, #0x02           ; work + 0x300 live bounds.
    add r7, r6, r7
    mov r2, #0x02
    lsl r2, r2, #0x0A           ; native 0x0800 Q-format step.
    ldr r3, [r7, #0x00]
    sub r3, r3, r2
    ldr r2, [r7, #0x04]
    mov r0, #0x02
    lsl r0, r0, #0x0A
    add r2, r2, r0
    mov r0, #0x2E
    lsl r0, r0, #0x08           ; y = 46.
    cmp r3, r0
    bhi TCM_Ov01Style13StoreBounds
    str r0, [r7, #0x00]
    mov r0, #0x72
    lsl r0, r0, #0x08           ; y = 114.
    str r0, [r7, #0x04]
    mov r0, #0
    str r0, [r6, #TCM_STYLE13_WORK_VWAIT_ACK]
    mov r0, #TCM_STYLE13_STATE_WAIT_FINAL_COMMIT
    str r0, [r6, #TCM_STYLE13_WORK_STATE]
    b TCM_Ov01Style13Return

TCM_Ov01Style13StoreBounds:
    str r3, [r7, #0x00]
    str r2, [r7, #0x04]
    b TCM_Ov01Style13Return

TCM_Ov01Style13WaitFinalCommit:
    ldr r0, [r6, #TCM_STYLE13_WORK_VWAIT_ACK]
    cmp r0, #0
    beq TCM_Ov01Style13Return
    mov r0, #TCM_STYLE13_STATE_POST_CLAMP_COMPLETION
    str r0, [r6, #TCM_STYLE13_WORK_STATE]
    b TCM_Ov01Style13Return

TCM_Ov01Style13PostClampCompletion:
    mov r0, #10
    mov r1, #0x7A
    lsl r1, r1, #0x02           ; work + 0x1E8 delay field.
    add r1, r6, r1
    str r0, [r1, #0x00]
    mov r0, #TCM_STYLE13_STATE_REVEAL_SYMBOL_GROUP
    str r0, [r6, #TCM_STYLE13_WORK_STATE]
    b TCM_Ov01Style13Return

TCM_Ov01Style13RevealSymbolGroup:
    mov r7, #0x7A
    lsl r7, r7, #0x02           ; work + 0x1E8 delay field.
    add r7, r6, r7
    ldr r0, [r7, #0x00]
    sub r0, #1
    str r0, [r7, #0x00]
    cmp r0, #0
    bpl TCM_Ov01Style13Return
    ldr r0, =TCM_STYLE13_WORK_SYMBOL_GROUP
    add r0, r6, r0
    bl TCM_Ov01Style13UpdateSymbolGroup
    cmp r0, #1
    bne TCM_Ov01Style13Return
    mov r0, #TCM_STYLE13_STATE_STATE8_SEPARATOR
    str r0, [r6, #TCM_STYLE13_WORK_STATE]
    b TCM_Ov01Style13Return

; The original state-8 leaf now enters the external state-9 router. The two-byte unconditional branch preserves the established handler footprint.
TCM_Ov01Style13State8Teardown:
    b TCM_Ov01Style13State9Dispatch

TCM_Ov01Style13Teardown:
    ; Destroy the VWait owner before restoring its registers or releasing work.
    add r0, r5, #0
    bl TCM_Ov01Style13DestroyBackgroundCycle
    ldr r0, =TCM_STYLE13_WORK_SYMBOL_GROUP
    add r0, r6, r0
    bl TCM_Ov01Style13DestroySymbolGroupSprites
    ldr r0, =TCM_STYLE13_WORK_BUNDLE
    add r0, r6, r0
    ldr r1, =TCM_STYLE13_WORK_SYMBOL_RESOURCES
    add r1, r6, r1
    bl Ov01_DestroySpriteResources
    ldr r0, =TCM_STYLE13_WORK_BUNDLE
    add r0, r6, r0
TCM_Ov01Style13NameOwnersTeardownCall:
    bl TCM_Ov01Style13DestroyNameOwnersPaletteAndBundle
    add r0, r5, #0
    bl TCM_Ov01Style13RestorePreBanner

    ldr r0, [r5, #0x14]
    cmp r0, #0
    beq TCM_Ov01Style13ClearLatch
    mov r1, #1
    str r1, [r0, #0x00]

TCM_Ov01Style13ClearLatch:
    ldr r0, =TCM_CurrentOrdinaryStyle
    mov r1, #0
    strh r1, [r0, #0x00]
    add r0, r5, #0
    add r1, r4, #0
    bl Ov01_EffectCleanup
    b TCM_Ov01Style13Return

TCM_Ov01Style13Return:
    pop {r4-r7}

TCM_Ov01EffectDispatchWrapper_Tail:
    ldr r2, =(Ov01_EffectDispatch_Tail | 1)
    bx r2

TCM_Ov01Style13Fail:
    bl GF_AssertFail
    b TCM_Ov01Style13Fail
    .pool

; Native state 8 is one callback between the completed symbol gate and the state-9 portrait callback. It performs no resource work. These new leaves sit after the accepted state-0..7 return boundary, preserving the established pre-banner branch layout.
TCM_Ov01Style13State9Dispatch:
    ldr r2, [pc, #0x00]
    bx r2
    .word TCM_Ov01Style13PostSlideDispatch | 1
    .fill 0x12 - (.-TCM_Ov01Style13State9Dispatch), 0x00

TCM_Ov01Style13State8Separator:
    mov r0, #TCM_STYLE13_STATE_PORTRAIT_REVEAL
    str r0, [r6, #TCM_STYLE13_WORK_STATE]
    b TCM_Ov01Style13Return

TCM_Ov01Style13PortraitReveal:
    ldr r7, =TCM_STYLE13_WORK_PORTRAIT_SPRITE
    add r7, r6, r7
    ldr r0, [r7, #0x00]
    cmp r0, #0
    beq TCM_Ov01Style13Fail
    mov r1, #0
    bl Sprite_SetOamMode
    ldr r0, [r7, #0x00]
    mov r1, #1
    bl Sprite_SetDrawFlag
    ldr r7, =TCM_STYLE13_WORK_PORTRAIT_CURRENT_X
    add r7, r6, r7
    ldr r0, =TCM_STYLE13_PORTRAIT_ENTRY_X
    str r0, [r7, #0x00]
    mov r0, #TCM_STYLE13_STATE_PORTRAIT_SLIDE
    str r0, [r6, #TCM_STYLE13_WORK_STATE]
    b TCM_Ov01Style13Return

TCM_Ov01Style13PortraitSlide:
    ; The dispatch frame is eight-byte aligned. Sprite_SetPositionXY is Thumb; a Thumb BL preserves state and the native Q-format motion contract.
    ldr r7, =TCM_STYLE13_WORK_PORTRAIT_SPRITE
    add r7, r6, r7
    ldr r0, [r7, #0x00]
    cmp r0, #0
    beq TCM_Ov01Style13Fail
    add r7, r0, #0
    ldr r0, =TCM_STYLE13_WORK_PORTRAIT_CURRENT_X
    add r0, r6, r0
    ldr r1, [r0, #0x00]
    ldr r2, =TCM_STYLE13_PORTRAIT_STEP
    sub r1, r1, r2
    ldr r2, =TCM_STYLE13_PORTRAIT_TARGET_X
    cmp r1, r2
    bhi TCM_Ov01Style13PortraitSlideStore
    add r1, r2, #0
    mov r3, #TCM_STYLE13_STATE_PORTRAIT_TEARDOWN
    str r3, [r6, #TCM_STYLE13_WORK_STATE]

TCM_Ov01Style13PortraitSlideStore:
    str r1, [r0, #0x00]
    add r0, r7, #0
    lsr r1, r1, #0x08
    ldr r2, =TCM_STYLE13_PORTRAIT_NATIVE_Y
    bl Sprite_SetPositionXY
    b TCM_Ov01Style13Return

TCM_Ov01Style13PortraitTeardown:
    ldr r7, =TCM_STYLE13_WORK_PORTRAIT_SPRITE
    add r7, r6, r7
    ldr r0, [r7, #0x00]
    cmp r0, #0
    beq TCM_Ov01Style13Fail
    mov r1, #0
    bl Sprite_SetDrawFlag
    ldr r0, [r7, #0x00]
    bl Sprite_Delete
    mov r0, #0
    str r0, [r7, #0x00]
    ldr r0, =TCM_STYLE13_WORK_BUNDLE
    add r0, r6, r0
    ldr r1, =TCM_STYLE13_WORK_PORTRAIT_RESOURCES
    add r1, r6, r1
    bl Ov01_DestroySpriteResources
    b TCM_Ov01Style13Teardown
    .pool

; Input r0 = ordinary effect environment. This is the ordinary equivalent of native state 0's shared VS-symbol resource and hidden-group setup.
TCM_Ov01Style13InitSymbolGate:
    push {r3, r4, r5, r6, r7, lr}
    sub sp, #0x18
    add r4, r0, #0
    ldr r5, [r4, #0x0C]
    cmp r5, #0
    bne TCM_Ov01Style13InitSymbolGateHaveWork
    ldr r0, =(TCM_Ov01Style13Fail | 1)
    bx r0
TCM_Ov01Style13InitSymbolGateHaveWork:
    ; The ARM9 style-13 selector cached this before Overlay-1 resource work. A late ReadFromNarcMemberByIdPair here is a recorded unsafe sequence.
    ldr r6, =TCM_Style13SymbolScratch

    ; Reserve the exact palette span referenced by this symbol RECN.
    ldr r0, [r4, #0x20]
    add r1, r6, #0
    ldrh r1, [r1, #0x04]
    mov r2, #4
    bl TCM_Vs8GetPaletteBankCountFromOpenNarc
    str r0, [sp, #0x08]

    ldr r0, [sp, #0x08]
    str r0, [sp, #0x00]
    ldrh r0, [r6, #0x02]
    str r0, [sp, #0x04]
    ldrh r0, [r6, #0x04]
    str r0, [sp, #0x08]
    ldrh r0, [r6, #0x06]
    str r0, [sp, #0x0C]
    ldr r0, =TCM_STYLE13_SYMBOL_RESOURCE_ID
    str r0, [sp, #0x10]
    mov r0, #0                  ; Ordinary renderer target: 1D-32.
    str r0, [sp, #0x14]

    ldr r0, [r4, #0x20]
    ldr r1, =TCM_STYLE13_WORK_BUNDLE
    add r1, r5, r1
    ldr r2, =TCM_STYLE13_WORK_SYMBOL_RESOURCES
    add r2, r5, r2
    ldrh r3, [r6, #0x00]
    bl TCM_LoadMappedSpriteResourcesFromOpenNarc

    ; The mapped loader leaves four resources at +0x150 and builds the header at +0x160. Ov01_CreateSprite receives the unadvanced block at +0x150.
    ldr r7, =TCM_STYLE13_WORK_SYMBOL_RESOURCES
    add r7, r5, r7
    ldr r0, [r7, #0x00]
    cmp r0, #0
    bne TCM_Ov01Style13SymbolResource0Present
    ldr r0, =(TCM_Ov01Style13Fail | 1)
    bx r0
TCM_Ov01Style13SymbolResource0Present:
    ldr r0, [r7, #0x04]
    cmp r0, #0
    bne TCM_Ov01Style13SymbolResource1Present
    ldr r0, =(TCM_Ov01Style13Fail | 1)
    bx r0
TCM_Ov01Style13SymbolResource1Present:
    ldr r0, [r7, #0x08]
    cmp r0, #0
    bne TCM_Ov01Style13SymbolResource2Present
    ldr r0, =(TCM_Ov01Style13Fail | 1)
    bx r0
TCM_Ov01Style13SymbolResource2Present:
    ldr r0, [r7, #0x0C]
    cmp r0, #0
    bne TCM_Ov01Style13SymbolResource3Present
    ldr r0, =(TCM_Ov01Style13Fail | 1)
    bx r0
TCM_Ov01Style13SymbolResource3Present:

    ldr r0, =TCM_STYLE13_WORK_SYMBOL_GROUP
    add r0, r5, r0
    ldr r1, =TCM_STYLE13_WORK_BUNDLE
    add r1, r5, r1
    add r2, r7, #0
    bl TCM_Ov01Style13CreateSymbolGroup

    add sp, #0x18
    pop {r3, r4, r5, r6, r7, pc}
    .pool

; Input r0 = ordinary effect environment. The portrait quartet was cached by ARM9 before Overlay-1 creation; this initializer performs no class-NARC read.
TCM_Ov01Style13InitPortrait:
    push {r4, r5, r6, r7, lr}
    sub sp, #0x1C
    add r4, r0, #0
    ldr r5, [r4, #0x0C]
    cmp r5, #0
    bne TCM_Ov01Style13InitPortraitHaveWork
    ldr r0, =(TCM_Ov01Style13Fail | 1)
    bx r0
TCM_Ov01Style13InitPortraitHaveWork:
    ldr r6, =TCM_Style13PortraitScratch

    ; Derive the complete palette span from the configured RECN before routing either mapping family through the ordinary 1D-32 resource consumer.
    ldr r0, [r4, #0x20]
    ldrh r1, [r6, #0x04]
    mov r2, #4
    bl TCM_Vs8GetPaletteBankCountFromOpenNarc
    str r0, [sp, #0x00]
    ldrh r3, [r6, #0x00]
    ldrh r0, [r6, #0x02]
    str r0, [sp, #0x04]
    ldrh r0, [r6, #0x04]
    str r0, [sp, #0x08]
    ldrh r0, [r6, #0x06]
    str r0, [sp, #0x0C]
    ldr r0, =TCM_STYLE13_PORTRAIT_RESOURCE_KEY
    str r0, [sp, #0x10]
    mov r0, #0
    str r0, [sp, #0x14]
    ldr r0, [r4, #0x20]
    ldr r1, =TCM_STYLE13_WORK_BUNDLE
    add r1, r5, r1
    ldr r2, =TCM_STYLE13_WORK_PORTRAIT_RESOURCES
    add r2, r5, r2
    bl TCM_LoadMappedSpriteResourcesFromOpenNarc

    ldr r7, =TCM_STYLE13_WORK_PORTRAIT_RESOURCES
    add r7, r5, r7
    ldr r0, [r7, #0x00]
    cmp r0, #0
    beq TCM_Ov01Style13InitPortraitFail
    ldr r0, [r7, #0x04]
    cmp r0, #0
    beq TCM_Ov01Style13InitPortraitFail
    ldr r0, [r7, #0x08]
    cmp r0, #0
    beq TCM_Ov01Style13InitPortraitFail
    ldr r0, [r7, #0x0C]
    cmp r0, #0
    beq TCM_Ov01Style13InitPortraitFail

    sub sp, #0x08
    mov r0, #0
    str r0, [sp, #0x00]
    str r0, [sp, #0x04]
    ldr r0, =TCM_STYLE13_WORK_BUNDLE
    add r0, r5, r0
    add r1, r7, #0
    ldr r2, =TCM_STYLE13_PORTRAIT_ENTRY_PIXELS << 0x0C
    mov r3, #TCM_STYLE13_PORTRAIT_NATIVE_Y
    lsl r3, r3, #0x0C
    bl Ov01_CreateSprite
    add sp, #0x08
    cmp r0, #0
    beq TCM_Ov01Style13InitPortraitFail

    ldr r7, =TCM_STYLE13_WORK_PORTRAIT_SPRITE
    add r7, r5, r7
    str r0, [r7, #0x00]
    mov r1, #0
    bl Sprite_SetDrawFlag
    ldr r0, [r7, #0x00]
    mov r1, #1
    bl Sprite_SetOamMode
    b TCM_Ov01Style13DarkPortraitEntry
    mov r1, #1
TCM_Ov01Style13PlaneToggle:
    bl GfGfx_EngineATogglePlanes

    add sp, #0x1C
    pop {r4, r5, r6, r7, pc}

TCM_Ov01Style13InitPortraitFail:
    ldr r0, =(TCM_Ov01Style13Fail | 1)
    bx r0
    .pool

; Input r0=symbol group, r1=ordinary bundle, r2=resource pointer block. The group is native-format: two s16 controls, four Sprite*, then four 0x14-byte affine timelines. Every sprite stays hidden until state 7.
TCM_Ov01Style13CreateSymbolGroup:
    push {r4, r5, r6, r7, lr}
    sub sp, #0x14
    add r4, r0, #0
    add r5, r1, #0
    add r6, r2, #0
    mov r0, #0
    strh r0, [r4, #TCM_STYLE13_GROUP_COUNTDOWN]
    strh r0, [r4, #TCM_STYLE13_GROUP_EXPOSED_COUNT]

TCM_Ov01Style13CreateSymbolGroup_Loop:
    str r0, [sp, #0x08]
    mov r1, #0
    str r1, [sp, #0x00]
    str r1, [sp, #0x04]
    add r0, r5, #0
    add r1, r6, #0
    mov r2, #0x48
    lsl r2, r2, #0x0C           ; Native zero-offset X = 72 pixels.
    mov r3, #0x52
    lsl r3, r3, #0x0C           ; Native zero-offset Y = 82 pixels.
    bl Ov01_CreateSprite
    cmp r0, #0
    bne TCM_Ov01Style13CreateSymbolGroupCreated
    ldr r0, =(TCM_Ov01Style13Fail | 1)
    bx r0
TCM_Ov01Style13CreateSymbolGroupCreated:
    str r0, [sp, #0x0C]

    ldr r1, [sp, #0x08]
    lsl r2, r1, #0x02
    add r2, #TCM_STYLE13_GROUP_SPRITES
    add r2, r4, r2
    ldr r0, [sp, #0x0C]
    str r0, [r2, #0x00]

    ldr r0, [sp, #0x0C]
    mov r1, #0
    bl Sprite_SetDrawFlag

    ldr r3, [sp, #0x08]
    cmp r3, #3
    beq TCM_Ov01Style13CreateSymbolGroup_InitTimeline
    ldr r0, [sp, #0x0C]
    mov r1, #2
    bl Sprite_SetAffineOverwriteMode
    ldr r0, [sp, #0x0C]
    mov r1, #1
    bl Sprite_SetAnimCtrlSeq

TCM_Ov01Style13CreateSymbolGroup_InitTimeline:
    ldr r3, [sp, #0x08]
    mov r0, r3
    lsl r0, r0, #0x02
    add r0, r0, r3
    lsl r0, r0, #0x02           ; member * 0x14.
    add r0, r4, r0
    add r0, #TCM_STYLE13_GROUP_TIMELINES
    cmp r3, #3
    beq TCM_Ov01Style13CreateSymbolGroup_FourthTimeline
    mov r1, #2
    lsl r1, r1, #0x0C
    mov r2, #1
    lsl r2, r2, #0x0C
    b TCM_Ov01Style13CreateSymbolGroup_StartTimeline

TCM_Ov01Style13CreateSymbolGroup_FourthTimeline:
    mov r1, #1
    lsl r1, r1, #0x0C
    add r2, r1, #0

TCM_Ov01Style13CreateSymbolGroup_StartTimeline:
    mov r3, #6
    bl Ov01_InitAffineTimeline
    ldr r0, [sp, #0x08]
    add r0, #1
    cmp r0, #TCM_STYLE13_GROUP_MEMBER_COUNT
    blo TCM_Ov01Style13CreateSymbolGroup_Loop
    add sp, #0x14
    pop {r4, r5, r6, r7, pc}
    .pool

; Input r0=symbol group. Returns r0=1 when all four timelines complete. Completed members remain visible, matching native Overlay-80 state 7.
TCM_Ov01Style13UpdateSymbolGroup:
    push {r4, r5, r6, r7, lr}
    sub sp, #0x0C
    add r4, r0, #0
    mov r7, #1
    ldrh r0, [r4, #TCM_STYLE13_GROUP_EXPOSED_COUNT]
    cmp r0, #TCM_STYLE13_GROUP_MEMBER_COUNT
    bhs TCM_Ov01Style13UpdateSymbolGroup_UpdateMembers
    mov r7, #0
    ldrh r0, [r4, #TCM_STYLE13_GROUP_COUNTDOWN]
    sub r0, #1
    cmp r0, #1
    bge TCM_Ov01Style13UpdateSymbolGroup_StoreCountdown
    mov r0, #3
    strh r0, [r4, #TCM_STYLE13_GROUP_COUNTDOWN]
    ldrh r0, [r4, #TCM_STYLE13_GROUP_EXPOSED_COUNT]
    add r0, #1
    strh r0, [r4, #TCM_STYLE13_GROUP_EXPOSED_COUNT]
    b TCM_Ov01Style13UpdateSymbolGroup_UpdateMembers

TCM_Ov01Style13UpdateSymbolGroup_StoreCountdown:
    strh r0, [r4, #TCM_STYLE13_GROUP_COUNTDOWN]

TCM_Ov01Style13UpdateSymbolGroup_UpdateMembers:
    mov r6, #0

TCM_Ov01Style13UpdateSymbolGroup_MemberLoop:
    ldrh r5, [r4, #TCM_STYLE13_GROUP_EXPOSED_COUNT]
    cmp r6, r5
    bhs TCM_Ov01Style13UpdateSymbolGroup_Return
    mov r0, r6
    lsl r0, r0, #0x02
    add r0, #TCM_STYLE13_GROUP_SPRITES
    add r0, r4, r0
    ldr r5, [r0, #0x00]
    cmp r5, #0
    bne TCM_Ov01Style13UpdateSymbolGroupHaveSprite
    ldr r0, =(TCM_Ov01Style13Fail | 1)
    bx r0
TCM_Ov01Style13UpdateSymbolGroupHaveSprite:

    mov r0, r6
    lsl r0, r0, #0x02
    add r0, r0, r6
    lsl r0, r0, #0x02
    add r0, r4, r0
    add r0, #TCM_STYLE13_GROUP_TIMELINES
    str r0, [sp, #0x08]
    bl Ov01_AdvanceAffineTimeline
    cmp r0, #0
    bne TCM_Ov01Style13UpdateSymbolGroup_TimelineDone
    mov r7, #0

TCM_Ov01Style13UpdateSymbolGroup_TimelineDone:
    ldr r1, [sp, #0x08]
    ldr r1, [r1, #0x00]
    add r0, sp, #0x00
    add r2, r1, #0
    add r3, r1, #0
    bl Ov01_WriteVecFx32
    add r0, r5, #0
    add r1, sp, #0x00
    bl Sprite_SetAffineScale
    add r0, r5, #0
    mov r1, #1
    bl Sprite_SetDrawFlag
    add r6, #1
    b TCM_Ov01Style13UpdateSymbolGroup_MemberLoop

TCM_Ov01Style13UpdateSymbolGroup_Return:
    add r0, r7, #0
    add sp, #0x0C
    pop {r4, r5, r6, r7, pc}
    .pool

; Input r0=symbol group. Delete all members before shared resources/bundle.
TCM_Ov01Style13DestroySymbolGroupSprites:
    push {r3, r4, r5, lr}
    add r4, r0, #0
    mov r5, #0

TCM_Ov01Style13DestroySymbolGroupSprites_Loop:
    mov r0, r5
    lsl r0, r0, #0x02
    add r0, #TCM_STYLE13_GROUP_SPRITES
    add r0, r4, r0
    ldr r0, [r0, #0x00]
    cmp r0, #0
    beq TCM_Ov01Style13DestroySymbolGroupSprites_Next
    bl Sprite_Delete
    mov r0, r5
    lsl r0, r0, #0x02
    add r0, #TCM_STYLE13_GROUP_SPRITES
    add r0, r4, r0
    mov r1, #0
    str r1, [r0, #0x00]

TCM_Ov01Style13DestroySymbolGroupSprites_Next:
    add r5, #1
    cmp r5, #TCM_STYLE13_GROUP_MEMBER_COUNT
    blo TCM_Ov01Style13DestroySymbolGroupSprites_Loop
    pop {r3, r4, r5, pc}
    .pool

; Input r0 = ordinary effect environment. Capture performs no display write.
TCM_Ov01Style13CapturePreBanner:
    push {r4, r5, r6, lr}
    ldr r4, [r0, #0x0C]
    cmp r4, #0
    bne TCM_Ov01Style13CapturePreBannerHaveWork
    ldr r0, =(TCM_Ov01Style13Fail | 1)
    bx r0
TCM_Ov01Style13CapturePreBannerHaveWork:

    mov r5, #0x01
    lsl r5, r5, #0x1A           ; 0x04000000 main display registers.
    mov r0, #0xBC
    lsl r0, r0, #0x02           ; work + 0x2F0 snapshot storage.
    add r1, r4, r0
    ldr r6, [r5, #0x00]
    str r6, [r1, #0x00]
    add r5, #0x40
    ldrh r6, [r5, #0x00]
    strh r6, [r1, #0x04]
    ldrh r6, [r5, #0x02]
    strh r6, [r1, #0x06]
    ldrh r6, [r5, #0x04]
    strh r6, [r1, #0x08]
    ldrh r6, [r5, #0x06]
    strh r6, [r1, #0x0A]
    ldrh r6, [r5, #0x08]
    strh r6, [r1, #0x0C]
    ldrh r6, [r5, #0x0A]
    strh r6, [r1, #0x0E]

    mov r0, #0xC0
    lsl r0, r0, #0x02
    add r1, r4, r0
    mov r0, #0x05
    lsl r0, r0, #0x0C           ; y = 80 in signed 8.8 format.
    str r0, [r1, #0x00]
    str r0, [r1, #0x04]
    pop {r4, r5, r6, pc}

; Input r0 = ordinary effect environment. Called only after white is complete.
TCM_Ov01Style13ActivatePreBanner:
    push {r4, r5, r6, lr}
    ldr r4, [r0, #0x0C]
    cmp r4, #0
    bne TCM_Ov01Style13ActivatePreBannerHaveWork
    ldr r0, =(TCM_Ov01Style13Fail | 1)
    bx r0
TCM_Ov01Style13ActivatePreBannerHaveWork:

    mov r5, #0x01
    lsl r5, r5, #0x1A           ; 0x04000000.
    ldr r6, [r5, #0x00]
    mov r0, #0x20
    lsl r0, r0, #0x08
    bic r6, r0
    mov r0, #0x06
    lsl r0, r0, #0x0C
    orr r6, r0
    str r6, [r5, #0x00]

    add r5, #0x40               ; 0x04000040 window registers.
    ldrh r6, [r5, #0x08]
    mov r0, #0x3F
    bic r6, r0
    orr r6, r0
    ldr r0, =0xFFFFC0FF
    and r6, r0
    mov r0, #0x3F
    lsl r0, r0, #0x08
    orr r6, r0
    strh r6, [r5, #0x08]

    ldrh r6, [r5, #0x0A]
    mov r0, #0x3F
    bic r6, r0
    mov r0, #0x37
    orr r6, r0
    strh r6, [r5, #0x0A]

    ; Native ov80_0223A938 masks both windows before either white fade. The VWait owner later supplies the live zero-height/expansion bounds.
    mov r0, #0
    strh r0, [r5, #0x00]        ; WIN0H
    strh r0, [r5, #0x02]        ; WIN1H
    strh r0, [r5, #0x04]        ; WIN0V
    strh r0, [r5, #0x06]        ; WIN1V
    pop {r4, r5, r6, pc}

; Input r0 = ordinary effect environment. VWait is already destroyed.
TCM_Ov01Style13RestorePreBanner:
    push {r4, r5, r6, lr}
    ldr r4, [r0, #0x0C]
    cmp r4, #0
    beq TCM_Ov01Style13RestoreReturn

    mov r5, #0x01
    lsl r5, r5, #0x1A
    mov r0, #0xBC
    lsl r0, r0, #0x02
    add r1, r4, r0
    ldr r6, [r1, #0x00]
    str r6, [r5, #0x00]
    add r5, #0x40
    ldrh r6, [r1, #0x04]
    strh r6, [r5, #0x00]
    ldrh r6, [r1, #0x06]
    strh r6, [r5, #0x02]
    ldrh r6, [r1, #0x08]
    strh r6, [r5, #0x04]
    ldrh r6, [r1, #0x0A]
    strh r6, [r5, #0x06]
    ldrh r6, [r1, #0x0C]
    strh r6, [r5, #0x08]
    ldrh r6, [r1, #0x0E]
    strh r6, [r5, #0x0A]

TCM_Ov01Style13RestoreReturn:
    pop {r4, r5, r6, pc}

TCM_Ov01Style13BeginFadeToWhite:
    push {r4, lr}
    sub sp, #0x10
    mov r0, #3
    str r0, [sp, #0x00]
    mov r0, #1
    str r0, [sp, #0x04]
    mov r0, #4
    str r0, [sp, #0x08]
    mov r0, #0
    mov r1, #0
    mov r2, #0
    ldr r3, =0x00007FFF
    bl BeginNormalPaletteFade
    add sp, #0x10
    pop {r4, pc}

TCM_Ov01Style13BeginFadeFromWhite:
    push {r4, lr}
    sub sp, #0x10
    mov r0, #3
    str r0, [sp, #0x00]
    mov r0, #1
    str r0, [sp, #0x04]
    mov r0, #4
    str r0, [sp, #0x08]
    mov r0, #3
    mov r1, #1
    mov r2, #1
    ldr r3, =0x00007FFF
    bl BeginNormalPaletteFade
    add sp, #0x10
    pop {r4, pc}

; Input r0 = ordinary effect environment. Read and retain eight palette frames.
TCM_Ov01Style13InitBackgroundCycle:
    push {r3, r4, r5, r6, r7, lr}
    sub sp, #0x10
    add r4, r0, #0
    ldr r5, [r4, #0x0C]
    cmp r5, #0
    beq TCM_Ov01Style13CycleFail

    mov r0, #TCM_ASSET_GROUP1_PALETTE_IMAGE_SIZE
    str r0, [sp, #0x00]
    add r0, sp, #0x08
    mov r1, #NARC_a_1_5_5
    ldr r2, =TCM_CurrentTrainerClass
    ldr r2, [r2, #0x00]
    mov r3, #TCM_ASSET_GROUP1_QUARTET_OFFSET
    bl ReadFromNarcMemberByIdPair

    ldr r0, [r4, #0x20]
    add r7, sp, #0x08
    ldrh r1, [r7, #0x00]
    mov r2, #0
    str r2, [sp, #0x04]
    add r2, sp, #0x04
    mov r3, #4
    bl GfGfxLoader_GetPlttDataFromOpenNarc
    add r6, r0, #0
    cmp r6, #0
    beq TCM_Ov01Style13CycleFail

    ldr r0, [sp, #0x04]
    cmp r0, #0
    beq TCM_Ov01Style13CycleFreeAndFail
    ldr r1, [r0, #0x08]
    mov r2, #0x80
    lsl r2, r2, #0x01
    cmp r1, r2
    blo TCM_Ov01Style13CycleFreeAndFail
    ldr r1, [r0, #0x0C]
    cmp r1, #0
    beq TCM_Ov01Style13CycleFreeAndFail

    mov r2, #0x7C
    lsl r2, r2, #0x02
    add r2, r5, r2
    mov r3, #TCM_STYLE13_BG_FRAME_WORD_COUNT
TCM_Ov01Style13CycleCopyLoop:
    ldr r0, [r1, #0x00]
    str r0, [r2, #0x00]
    add r1, #0x04
    add r2, #0x04
    sub r3, #0x01
    bne TCM_Ov01Style13CycleCopyLoop

    add r0, r6, #0
    bl Heap_Free
    ldr r0, =(TCM_Ov01Style13BackgroundVWait | 1)
    add r1, r5, #0
    mov r2, #1
    bl SysTask_CreateOnVWaitQueue
    cmp r0, #0
    beq TCM_Ov01Style13CycleFail
    str r0, [r5, #TCM_STYLE13_WORK_BG_VWAIT_TASK]
    add sp, #0x10
    pop {r3, r4, r5, r6, r7, pc}

TCM_Ov01Style13CycleFreeAndFail:
    add r0, r6, #0
    bl Heap_Free
TCM_Ov01Style13CycleFail:
    bl GF_AssertFail
    b TCM_Ov01Style13CycleFail
    .pool

; SysTask callback: r0 = task, r1 = style-13 work block.
TCM_Ov01Style13BackgroundVWait:
    push {r3, r4, r5, lr}
    add r4, r1, #0

    mov r0, #0x7C
    lsl r0, r0, #0x02
    add r5, r4, r0
    ldr r0, [r4, #TCM_STYLE13_WORK_BG_SELECTOR]
    lsl r0, r0, #0x05
    add r5, r5, r0
    add r0, r5, #0
    mov r1, #TCM_STYLE13_BG_FRAME_SIZE
    blx DC_FlushRange
    add r0, r5, #0
    mov r1, #0
    mov r2, #TCM_STYLE13_BG_FRAME_SIZE
    blx GX_LoadBGPltt

    ldr r0, =0x04000040
    ldr r1, =0x000000FF
    strh r1, [r0, #0x00]
    mov r1, #0xC0
    lsl r1, r1, #0x02
    add r1, r4, r1
    ldr r2, [r1, #0x00]
    lsr r2, r2, #0x08
    ldr r3, [r1, #0x04]
    lsr r3, r3, #0x08
    mov r5, #0x01
    lsl r5, r5, #0x08
    strh r5, [r0, #0x02]        ; WIN1H = 0x0100
    lsl r2, r2, #0x08
    orr r2, r3
    strh r2, [r0, #0x04]
    strh r2, [r0, #0x06]        ; WIN1V shares WIN0V's vertical band
    mov r0, #1
    str r0, [r4, #TCM_STYLE13_WORK_VWAIT_ACK]
    pop {r3, r4, r5, pc}
    .pool

; Input r0 = ordinary effect environment.
TCM_Ov01Style13DestroyBackgroundCycle:
    push {r3, r4, r5, lr}
    ldr r4, [r0, #0x0C]
    cmp r4, #0
    beq TCM_Ov01Style13DestroyReturn
    ldr r0, [r4, #TCM_STYLE13_WORK_BG_VWAIT_TASK]
    cmp r0, #0
    beq TCM_Ov01Style13DestroyReturn
    bl SysTask_Destroy
    mov r0, #0
    str r0, [r4, #TCM_STYLE13_WORK_BG_VWAIT_TASK]
TCM_Ov01Style13DestroyReturn:
    pop {r3, r4, r5, pc}

; Input r0 = ordinary effect environment. Loads class-local RLCN/RGCN/RCSN.
TCM_Ov01Style13LoadBackground:
    push {r4, r5, r6, lr}
    sub sp, #0x20
    add r4, r0, #0
    mov r0, #TCM_ASSET_GROUP1_PALETTE_IMAGE_SIZE
    str r0, [sp, #0x00]
    add r0, sp, #0x10
    mov r1, #NARC_a_1_5_5
    ldr r2, =TCM_CurrentTrainerClass
    ldr r2, [r2, #0x00]
    mov r3, #TCM_ASSET_GROUP1_QUARTET_OFFSET
    bl ReadFromNarcMemberByIdPair
    mov r0, #TCM_ASSET_GROUP1_RCSN_SIZE
    str r0, [sp, #0x00]
    add r0, sp, #0x14
    mov r1, #NARC_a_1_5_5
    ldr r2, =TCM_CurrentTrainerClass
    ldr r2, [r2, #0x00]
    mov r3, #TCM_ASSET_GROUP1_RCSN1_OFFSET
    bl ReadFromNarcMemberByIdPair

    add r5, sp, #0x10
    mov r0, #0
    str r0, [sp, #0x00]
    mov r0, #1
    str r0, [sp, #0x04]
    ldr r0, [r4, #0x10]
    ldr r0, [r0, #0x08]
    str r0, [sp, #0x08]
    mov r0, #3
    str r0, [sp, #0x0C]
    ldrh r3, [r5, #0x00]
    ldrh r2, [r5, #0x02]
    ldrh r1, [r5, #0x04]
    ldr r0, [r4, #0x20]
    bl Ov01_LoadBgFromOpenNarc
    add sp, #0x20
    pop {r4, r5, r6, pc}
    .pool

; -----------------------------------------------------------------------------
; Ordinary style-13 portrait darkening.
;
; The initializer branches here with r4=environment, r5=work, r6=portrait scratch, and r7=work+portrait Sprite* slot.  The entry creates a fully aligned fifth-argument frame, then restores the two displaced plane-toggle arguments before branching to the preserved native call.
; -----------------------------------------------------------------------------

TCM_Ov01Style13DarkPortraitEntry:
    sub sp, #0x08
    mov r0, #0
    str r0, [sp, #0x00]         ; Blend target: RGB555 black.
    ldr r0, [r7, #0x00]
    mov r1, #4                  ; Ordinary effect heap.
    ldrh r2, [r6, #0x00]        ; Portrait RLCN member.
    mov r3, #0x0E               ; Native state-0 dark blend amount.
    bl TCM_Ov01Style13BlendPortraitWide
    add sp, #0x08

    mov r0, #0x10               ; Displaced GfGfx_EngineATogglePlanes mask.
    mov r1, #1                  ; Displaced GfGfx_EngineATogglePlanes engine.
    ldr r2, =(TCM_Ov01Style13PlaneToggle | 1)
    bx r2

; r0=Sprite*, r1=heap, r2=RLCN member, r3=blend amount, [sp]=RGB555 target. The active RECN determines the complete OBJ palette-bank span.
TCM_Ov01Style13BlendPortraitWide:
    push {r4, r5, r6, r7, lr}
    sub sp, #0x14
    add r5, r1, #0
    add r7, r0, #0
    add r6, r2, #0
    str r3, [sp, #0x04]

    add r0, r7, #0
    bl TCM_Vs8GetPaletteBankCountFromSprite
    str r0, [sp, #0x00]
    str r0, [sp, #0x0C]         ; Preserve count across BlendPalette.
    lsl r1, r0, #5               ; 0x20 bytes per OBJ palette bank.
    add r0, r5, #0
    bl Heap_Alloc
    cmp r0, #0
    beq TCM_Ov01Style13BlendPortraitWide_Assert
    add r4, r0, #0

    mov r0, #0x6D                ; NARC /a/1/0/9.
    add r1, r6, #0
    add r2, sp, #0x08
    add r3, r5, #0
    bl GfGfxLoader_GetPlttData
    cmp r0, #0
    beq TCM_Ov01Style13BlendPortraitWide_Assert
    add r5, r0, #0

    ldr r2, [sp, #0x00]
    lsl r2, r2, #4               ; 16 colours per OBJ palette bank.
    add r0, sp, #0x18
    ldrh r0, [r0, #0x10]         ; Original fifth argument at caller [sp].
    str r0, [sp, #0x00]
    ldr r0, [sp, #0x08]
    ldr r0, [r0, #0x0C]
    add r1, r4, #0
    ldr r3, [sp, #0x04]
    bl BlendPalette

    add r0, r7, #0
    bl Sprite_GetPaletteProxy
    add r6, r0, #0
    add r0, r4, #0
    ldr r1, [sp, #0x0C]
    lsl r1, r1, #5
    blx DC_FlushRange
    add r0, r6, #0
    mov r1, #1
    blx NNS_G2dGetImagePaletteLocation
    add r1, r0, #0
    add r0, r4, #0
    ldr r2, [sp, #0x0C]
    lsl r2, r2, #5
    blx GX_LoadOBJPltt

    add r0, r4, #0
    bl Heap_Free
    add r0, r5, #0
    bl Heap_Free
    add sp, #0x14
    pop {r4, r5, r6, r7, pc}

TCM_Ov01Style13BlendPortraitWide_Assert:
    bl GF_AssertFail
    b TCM_Ov01Style13BlendPortraitWide_Assert
    .pool

TCM_Ov01Style13DarkPortrait_End:

; Post-slide state dispatch. The decremented delay remains stored as -1, but native fade mode 3 must replace r0 at BeginNormalPaletteFade entry. Later states own palette restoration, name drawing, brightness, and teardown.
TCM_Ov01Style13PostSlideDispatch:
    cmp r0, #TCM_STYLE13_STATE_STATE8_SEPARATOR
    beq TCM_Ov01Style13PostSlideState8
    cmp r0, #TCM_STYLE13_STATE_PORTRAIT_REVEAL
    beq TCM_Ov01Style13PostSlidePortraitReveal
    cmp r0, #TCM_STYLE13_STATE_PORTRAIT_SLIDE
    beq TCM_Ov01Style13PostSlidePortraitSlide
    cmp r0, #TCM_STYLE13_STATE_PORTRAIT_TEARDOWN
    beq TCM_Ov01Style13PostSlideArmDelay
    cmp r0, #TCM_STYLE13_STATE_POST_SLIDE_DELAY
    beq TCM_Ov01Style13PostSlideDelay
    cmp r0, #TCM_STYLE13_STATE_WAIT_SECOND_WHITE
    beq TCM_Ov01Style13PostSlideWaitSecondWhite
    cmp r0, #TCM_STYLE13_STATE_BEGIN_SECOND_RETURN
    beq TCM_Ov01Style13PostSlideBeginSecondReturn
    cmp r0, #TCM_STYLE13_STATE_WAIT_SECOND_RETURN
    beq TCM_Ov01Style13PostSlideWaitSecondReturn
    cmp r0, #TCM_STYLE13_STATE_SECOND_FLASH_TEARDOWN
    bne TCM_Ov01Style13PostSlideCheckRestorePortraitPalette
    b TCM_Ov01Style13PostSlideTeardown
TCM_Ov01Style13PostSlideCheckRestorePortraitPalette:
    cmp r0, #TCM_STYLE13_STATE_RESTORE_PORTRAIT_PALETTE
    beq TCM_Ov01Style13PostSlideRestorePortraitPalette
    cmp r0, #TCM_STYLE13_STATE_POST_REVEAL_HOLD
    beq TCM_Ov01Style13PostSlideHold
    cmp r0, #TCM_STYLE13_STATE_BEGIN_EXIT_WHITE
    beq TCM_Ov01Style13PostSlideBeginExitWhite
    cmp r0, #TCM_STYLE13_STATE_WAIT_EXIT_WHITE
    beq TCM_Ov01Style13PostSlideWaitExitWhiteVeneer
    ldr r2, =TCM_Ov01Style13Fail | 1
    bx r2
TCM_Ov01Style13PostSlideWaitExitWhiteVeneer:
    b TCM_Ov01Style13PostSlideWaitExitWhite

TCM_Ov01Style13PostSlideState8:
    mov r0, #TCM_STYLE13_STATE_PORTRAIT_REVEAL
    str r0, [r6, #TCM_STYLE13_WORK_STATE]
    b TCM_Ov01Style13PostSlideReturn

TCM_Ov01Style13PostSlidePortraitReveal:
    ldr r2, =TCM_Ov01Style13PortraitReveal | 1
    bx r2

TCM_Ov01Style13PostSlidePortraitSlide:
    ldr r2, =TCM_Ov01Style13PortraitSlide | 1
    bx r2

TCM_Ov01Style13PostSlideArmDelay:
    mov r0, #10
    mov r1, #0x7A
    lsl r1, r1, #2
    add r1, r6, r1
    str r0, [r1, #0x00]
    mov r0, #TCM_STYLE13_STATE_POST_SLIDE_DELAY
    str r0, [r6, #TCM_STYLE13_WORK_STATE]
    b TCM_Ov01Style13PostSlideReturn

TCM_Ov01Style13PostSlideDelay:
    mov r1, #0x7A
    lsl r1, r1, #2
    add r1, r6, r1
    ldr r0, [r1, #0x00]
    sub r0, #1
    str r0, [r1, #0x00]
    cmp r0, #0
    bpl TCM_Ov01Style13PostSlideReturn
    sub sp, #0x10
    mov r1, #3
    str r1, [sp, #0x00]
    mov r1, #1
    str r1, [sp, #0x04]
    mov r1, #4
    str r1, [sp, #0x08]
    mov r0, #3                  ; Native state-11 FADE_MAIN_ONLY mode.
    mov r1, #0
    mov r2, #0
    ldr r3, =0x00007FFF
    bl BeginNormalPaletteFade
    add sp, #0x10
    mov r0, #TCM_STYLE13_STATE_WAIT_SECOND_WHITE
    str r0, [r6, #TCM_STYLE13_WORK_STATE]
    b TCM_Ov01Style13PostSlideReturn

TCM_Ov01Style13PostSlideWaitSecondWhite:
    bl IsPaletteFadeFinished
    cmp r0, #0
    beq TCM_Ov01Style13PostSlideReturn
    mov r0, #0x0D
    mvn r0, r0                     ; -14, matching native state-12 brightness.
    mov r1, #0x21                  ; Ordinary BG0 field plane plus backdrop.
    mov r2, #1                     ; Main engine only.
    bl SetBlendBrightness
    mov r0, #TCM_STYLE13_STATE_RESTORE_PORTRAIT_PALETTE
    str r0, [r6, #TCM_STYLE13_WORK_STATE]
    b TCM_Ov01Style13PostSlideReturn

; Native state-12 portrait restoration and name reveal. The ordinary adapter restores only the palette span it owns, then exposes the retained TextOBJ while the completed fade still holds the screen white.
TCM_Ov01Style13PostSlideRestorePortraitPalette:
    ldr r7, =TCM_STYLE13_WORK_PORTRAIT_SPRITE
    add r7, r6, r7
    ldr r0, [r7, #0x00]
    cmp r0, #0
    bne TCM_Ov01Style13PostSlideRestorePortraitPaletteHaveSprite
    ldr r2, =TCM_Ov01Style13Fail | 1
    bx r2
TCM_Ov01Style13PostSlideRestorePortraitPaletteHaveSprite:
    sub sp, #0x08
    mov r0, #0
    str r0, [sp, #0x00]
    ldr r0, [r7, #0x00]
    mov r1, #4
    ldr r2, =TCM_Style13PortraitScratch
    ldrh r2, [r2, #0x00]
    mov r3, #0
    bl TCM_Ov01Style13BlendPortraitWide
    add sp, #0x08
    ldr r0, =TCM_STYLE13_WORK_TEXT_OBJ
    ldr r0, [r6, r0]
    cmp r0, #0
    bne TCM_Ov01Style13NameRevealHaveTextOBJ
    ldr r2, =TCM_Ov01Style13Fail | 1
    bx r2
TCM_Ov01Style13NameRevealHaveTextOBJ:
    mov r1, #1
TCM_Ov01Style13NameRevealCall:
    bl TextOBJ_SetSpritesDrawFlag
TCM_Ov01Style13NameRevealed:
    mov r0, #TCM_STYLE13_STATE_BEGIN_SECOND_RETURN
    str r0, [r6, #TCM_STYLE13_WORK_STATE]
    b TCM_Ov01Style13PostSlideReturn

TCM_Ov01Style13PostSlideBeginSecondReturn:
    bl TCM_Ov01Style13BeginFadeFromWhite
    mov r0, #TCM_STYLE13_STATE_WAIT_SECOND_RETURN
    str r0, [r6, #TCM_STYLE13_WORK_STATE]
    b TCM_Ov01Style13PostSlideReturn

TCM_Ov01Style13PostSlideWaitSecondReturn:
    bl IsPaletteFadeFinished
    cmp r0, #0
    beq TCM_Ov01Style13PostSlideReturn
TCM_Ov01Style13PostRevealHoldArm:
    mov r0, #0x1A
    mov r1, #0x7A
    lsl r1, r1, #2
    add r1, r6, r1
    str r0, [r1, #0x00]
TCM_Ov01Style13PostRevealHoldArmed:
    mov r0, #TCM_STYLE13_STATE_POST_REVEAL_HOLD
    str r0, [r6, #TCM_STYLE13_WORK_STATE]
    b TCM_Ov01Style13PostSlideReturn

TCM_Ov01Style13PostSlideHold:
    mov r1, #0x7A
    lsl r1, r1, #2
    add r1, r6, r1
    ldr r0, [r1, #0x00]
    sub r0, #1
    str r0, [r1, #0x00]
    cmp r0, #0
    bpl TCM_Ov01Style13PostSlideReturn
TCM_Ov01Style13PostRevealHoldExpired:
    mov r0, #TCM_STYLE13_STATE_BEGIN_EXIT_WHITE
    str r0, [r6, #TCM_STYLE13_WORK_STATE]
    b TCM_Ov01Style13PostSlideReturn

TCM_Ov01Style13PostSlideBeginExitWhite:
    sub sp, #0x10
    mov r1, #0x0F
    str r1, [sp, #0x00]
    mov r1, #1
    str r1, [sp, #0x04]
    mov r1, #4
    str r1, [sp, #0x08]
    mov r0, #3
    mov r1, #0
    mov r2, #0
    ldr r3, =0x00007FFF
TCM_Ov01Style13ExitWhiteFadeCall:
    bl BeginNormalPaletteFade
    add sp, #0x10
    mov r0, #TCM_STYLE13_STATE_WAIT_EXIT_WHITE
    str r0, [r6, #TCM_STYLE13_WORK_STATE]
TCM_Ov01Style13ExitWhiteFadeArmed:
    b TCM_Ov01Style13PostSlideReturn

TCM_Ov01Style13PostSlideWaitExitWhite:
    bl IsPaletteFadeFinished
    cmp r0, #0
    beq TCM_Ov01Style13PostSlideReturn
TCM_Ov01Style13ExitWhiteFinished:
    mov r0, #TCM_STYLE13_STATE_SECOND_FLASH_TEARDOWN
    str r0, [r6, #TCM_STYLE13_WORK_STATE]
    b TCM_Ov01Style13PostSlideReturn

TCM_Ov01Style13PostSlideTeardown:
    mov r0, #1
    ldr r1, =0x00007FFF
TCM_Ov01Style13MasterWhiteCall:
    bl SetMasterBrightnessWhite
TCM_Ov01Style13MasterWhiteApplied:
    ldr r2, =TCM_Ov01Style13PortraitTeardown | 1
    bx r2

TCM_Ov01Style13PostSlideReturn:
    ldr r2, =TCM_Ov01Style13Return | 1
    bx r2
    .pool

TCM_Ov01Style13PostSlide_End:

; Style 13 name planning and setup. Entry r5 is the ordinary environment and r6 is the zeroed Style 13 work block. The configured message is normalised, measured, synchronously rastered into the work-owned CPU Window, and planned on ordinary heap 4. Demand success retains the sole Window owner for later resource setup; local failures release it. The displaced bundle-address result is restored in r0 and the transient planner count is returned in r1 so the caller can reserve matching slots.
TCM_Ov01Style13CacheNameIdAndGetBundle:
    ldr r1, =TCM_Style13NameScratch
    ldrh r1, [r1, #0x00]
    ldr r2, =TCM_STYLE13_WORK_NAME_ID
    strh r1, [r6, r2]
TCM_Ov01Style13NameIdCached:
    push {r3-r7, lr}            ; 24 bytes keeps every nested BL aligned.
    sub sp, #0x30               ; Outgoing args and two scalar locals.
    add r4, r6, #0              ; Zeroed style-13 work.

    mov r0, #0
    str r0, [sp, #TCM_STYLE13_NAME_STACK_PLAN_COUNT]
    ldr r1, =TCM_STYLE13_WORK_NAME_LENGTH
    strh r0, [r4, r1]
    add r1, #2
    str r0, [r4, r1]
    add r1, #4
    str r0, [r4, r1]

    mov r0, #1
    mov r1, #TCM_STYLE13_TEXT_MSG_NARC
    ldr r2, =TCM_STYLE13_TEXT_MSG_MEMBER
    mov r3, #4
TCM_Ov01Style13NameOpenMsgCall:
    bl NewMsgDataFromNarc
    cmp r0, #0
    bne TCM_Ov01Style13NameHaveMsgData
    mov r0, #TCM_STYLE13_NAME_STATUS_MSG_FAIL
    b TCM_Ov01Style13NameStoreStatusAndReturn

TCM_Ov01Style13NameHaveMsgData:
    add r5, r0, #0
TCM_Ov01Style13NameCountCall:
    bl MsgDataGetCount
    ldr r2, =TCM_STYLE13_WORK_NAME_ID
    ldrh r1, [r4, r2]
    cmp r1, r0
    blo TCM_Ov01Style13NameIdInRange
    mov r0, #TCM_STYLE13_NAME_STATUS_ID_RANGE
    b TCM_Ov01Style13NameStoreStatusAndDestroyMsg

TCM_Ov01Style13NameIdInRange:
    add r0, r5, #0
TCM_Ov01Style13NameReadSourceCall:
    bl NewString_ReadMsgData
    cmp r0, #0
    bne TCM_Ov01Style13NameHaveSource
    mov r0, #TCM_STYLE13_NAME_STATUS_SOURCE_FAIL
    b TCM_Ov01Style13NameStoreStatusAndDestroyMsg

TCM_Ov01Style13NameHaveSource:
    add r6, r0, #0
    ldrh r0, [r6, #TCM_STRING_SIZE_OFFSET]
    ldrh r1, [r6, #TCM_STRING_DATA_OFFSET]
    ldr r2, =TCM_TRAINER_NAME_PACKED_MARKER
    cmp r1, r2
    beq TCM_Ov01Style13NamePackedCapacity

    ldr r2, =0xFFFF
    cmp r0, r2
    beq TCM_Ov01Style13NameCapacityFailed
    add r0, #1
    b TCM_Ov01Style13NameAllocateNormalized

TCM_Ov01Style13NamePackedCapacity:
    cmp r0, #2
    blo TCM_Ov01Style13NameCapacityFailed
    ldr r2, =0x8000
    cmp r0, r2
    bhi TCM_Ov01Style13NameCapacityFailed
    sub r0, #1
    lsl r0, r0, #1
    add r0, #1

TCM_Ov01Style13NameAllocateNormalized:
    mov r1, #4
TCM_Ov01Style13NameAllocateNormalizedCall:
    bl String_New
    cmp r0, #0
    bne TCM_Ov01Style13NameHaveNormalized
    mov r0, #TCM_STYLE13_NAME_STATUS_NORMALIZED_FAIL
    b TCM_Ov01Style13NameStoreStatusAndDestroySource

TCM_Ov01Style13NameCapacityFailed:
    mov r0, #TCM_STYLE13_NAME_STATUS_CAPACITY
    b TCM_Ov01Style13NameStoreStatusAndDestroySource

TCM_Ov01Style13NameHaveNormalized:
    add r7, r0, #0
    add r1, r6, #0
TCM_Ov01Style13NameNormalizeCall:
    bl String_Cat_HandleTrainerName

    add r0, r7, #0
TCM_Ov01Style13NameLengthCall:
    bl String_GetLength
    ldr r1, =TCM_STYLE13_WORK_NAME_LENGTH
    strh r0, [r4, r1]

    mov r0, #0
    add r1, r7, #0
    mov r2, #0
TCM_Ov01Style13NameWidthCall:
    bl FontID_String_GetWidth
    ldr r1, =TCM_STYLE13_WORK_NAME_WIDTH
    str r0, [r4, r1]
TCM_Ov01Style13NameMeasured:

    ; The Window stores tile width in one byte. Accept every representable positive width, independent of the names present in vanilla msg_0729.
    cmp r0, #0
    beq TCM_Ov01Style13NameWidthFailed
    ldr r1, =TCM_STYLE13_TEXT_MAX_PIXEL_WIDTH
    cmp r0, r1
    bhi TCM_Ov01Style13NameWidthFailed
    add r0, #7
    lsr r0, r0, #3
    str r0, [sp, #TCM_STYLE13_NAME_STACK_TILE_WIDTH]

    ; The accepted caller's saved r5 is the ordinary effect environment. Resolve the Window allocator owner through its live FieldSystem/BgConfig.
    ldr r0, [sp, #TCM_STYLE13_NAME_STACK_SAVED_ENV]
    cmp r0, #0
    beq TCM_Ov01Style13NameOwnerFailed
    ldr r0, [r0, #TCM_OV01_ENV_FIELD_SYSTEM_OFFSET]
    cmp r0, #0
    beq TCM_Ov01Style13NameOwnerFailed
    ldr r0, [r0, #TCM_FIELD_SYSTEM_BG_CONFIG_OFFSET]
    cmp r0, #0
    beq TCM_Ov01Style13NameOwnerFailed
    str r0, [sp, #TCM_STYLE13_NAME_STACK_BG_CONFIG]

    ldr r0, =TCM_STYLE13_WORK_NAME_WINDOW
    add r0, r4, r0
TCM_Ov01Style13NameInitWindowCall:
    bl InitWindow
    mov r0, #0
    str r0, [sp, #0x00]        ; baseTile = 0.
    str r0, [sp, #0x04]        ; paletteNum = 0.
    ldr r0, [sp, #TCM_STYLE13_NAME_STACK_BG_CONFIG]
    ldr r1, =TCM_STYLE13_WORK_NAME_WINDOW
    add r1, r4, r1
    ldr r2, [sp, #TCM_STYLE13_NAME_STACK_TILE_WIDTH]
    lsl r2, r2, #24            ; Native Overlay-80 u8 call-site narrowing.
    lsr r2, r2, #24
    mov r3, #2
TCM_Ov01Style13NameAddWindowCall:
    bl AddTextWindowTopLeftCorner
    ldr r0, =TCM_STYLE13_WORK_NAME_WINDOW
    add r0, r4, r0
    ldr r0, [r0, #0x0C]
    cmp r0, #0
    beq TCM_Ov01Style13NameWindowFailed

    mov r3, #0
    str r3, [sp, #0x00]        ; y = 0.
    mov r0, #0xFF
    str r0, [sp, #0x04]        ; synchronous, no VRAM transfer.
    ldr r0, =TCM_STYLE13_TEXT_COLOR
    str r0, [sp, #0x08]
    str r3, [sp, #0x0C]        ; letter spacing = 0.
    str r3, [sp, #0x10]        ; line spacing = 0.
    str r3, [sp, #0x14]        ; callback = NULL.
    ldr r0, =TCM_STYLE13_WORK_NAME_WINDOW
    add r0, r4, r0
    mov r1, #0                 ; font 0.
    add r2, r7, #0             ; normalised String.
TCM_Ov01Style13NamePrintCall:
    bl AddTextPrinterParameterizedWithColorAndSpacing
    cmp r0, #TCM_STYLE13_TEXT_SYNC_RESULT
    bne TCM_Ov01Style13NamePrinterFailed
TCM_Ov01Style13NameWindowRasterized:

    ; The synchronous printer's outgoing arguments are now dead. Reuse their 0x18-byte area as a native self-linked 0x14-byte planner header plus a count scalar. The work Window remains the sole live pixel-buffer owner.
    add r0, sp, #TCM_STYLE13_NAME_STACK_PLAN_HEAD
    str r0, [sp, #0x0C]
    str r0, [sp, #0x10]
    ldr r0, [sp, #TCM_STYLE13_NAME_STACK_TILE_WIDTH]
    mov r1, #2
    mov r2, #4
    add r3, sp, #TCM_STYLE13_NAME_STACK_PLAN_HEAD
TCM_Ov01Style13NamePlannerCall:
    bl TextOBJ_LayoutPlan
    str r0, [sp, #TCM_STYLE13_NAME_STACK_PLAN_COUNT]
TCM_Ov01Style13NamePlannerReturned:
    add r0, sp, #TCM_STYLE13_NAME_STACK_PLAN_HEAD
TCM_Ov01Style13NameFreePlannerCall:
    bl TextOBJ_FreeLayoutPlan
TCM_Ov01Style13NamePlannerReleased:

    ldr r0, =TCM_STYLE13_WORK_NAME_WINDOW
    add r0, r4, r0
TCM_Ov01Style13NameRetainWindowSuccessCall:
    bl TCM_Ov01Style13CalcCharDemandAndRetainWindow
TCM_Ov01Style13NameWindowRetained:

    add r0, r7, #0
TCM_Ov01Style13NameDeleteNormalizedCall:
    bl String_Delete
    add r0, r6, #0
TCM_Ov01Style13NameDeleteSourceSuccessCall:
    bl String_Delete
    add r0, r5, #0
TCM_Ov01Style13NameDestroyMsgSuccessCall:
    bl DestroyMsgData
    mov r0, #TCM_STYLE13_NAME_STATUS_OK
    ldr r1, =TCM_STYLE13_WORK_NAME_STATUS
    str r0, [r4, r1]
TCM_Ov01Style13NameReleased:
    b TCM_Ov01Style13NameReturnBundle

TCM_Ov01Style13NameWidthFailed:
    mov r0, #TCM_STYLE13_NAME_STATUS_WIDTH
    b TCM_Ov01Style13NameStoreStatusAndDestroyNormalized

TCM_Ov01Style13NameOwnerFailed:
    mov r0, #TCM_STYLE13_NAME_STATUS_OWNER
    b TCM_Ov01Style13NameStoreStatusAndDestroyNormalized

TCM_Ov01Style13NameWindowFailed:
    mov r0, #TCM_STYLE13_NAME_STATUS_WINDOW
    b TCM_Ov01Style13NameStoreStatusAndDestroyNormalized

TCM_Ov01Style13NamePrinterFailed:
    mov r0, #TCM_STYLE13_NAME_STATUS_PRINTER
    ldr r1, =TCM_STYLE13_WORK_NAME_STATUS
    str r0, [r4, r1]
    ldr r0, =TCM_STYLE13_WORK_NAME_WINDOW
    add r0, r4, r0
TCM_Ov01Style13NameRemoveWindowFailureCall:
    bl RemoveWindow
    b TCM_Ov01Style13NameDeleteNormalizedFailure

TCM_Ov01Style13NameStoreStatusAndDestroyNormalized:
    ldr r1, =TCM_STYLE13_WORK_NAME_STATUS
    str r0, [r4, r1]
TCM_Ov01Style13NameDeleteNormalizedFailure:
    add r0, r7, #0
TCM_Ov01Style13NameDeleteNormalizedFailureCall:
    bl String_Delete
TCM_Ov01Style13NameDestroySourceFailure:
    add r0, r6, #0
TCM_Ov01Style13NameDeleteSourceAfterNormalizedFailureCall:
    bl String_Delete
    b TCM_Ov01Style13NameDestroyMsg

TCM_Ov01Style13NameStoreStatusAndDestroySource:
    ldr r1, =TCM_STYLE13_WORK_NAME_STATUS
    str r0, [r4, r1]
    add r0, r6, #0
TCM_Ov01Style13NameDeleteSourceFailureCall:
    bl String_Delete
    b TCM_Ov01Style13NameDestroyMsg

TCM_Ov01Style13NameStoreStatusAndDestroyMsg:
    ldr r1, =TCM_STYLE13_WORK_NAME_STATUS
    str r0, [r4, r1]
TCM_Ov01Style13NameDestroyMsg:
    add r0, r5, #0
TCM_Ov01Style13NameDestroyMsgFailureCall:
    bl DestroyMsgData
    b TCM_Ov01Style13NameReturnBundle

TCM_Ov01Style13NameStoreStatusAndReturn:
    ldr r1, =TCM_STYLE13_WORK_NAME_STATUS
    str r0, [r4, r1]

TCM_Ov01Style13NameReturnBundle:
    ldr r1, [sp, #TCM_STYLE13_NAME_STACK_PLAN_COUNT]
    mov r0, #TCM_STYLE13_WORK_BUNDLE
    add r0, r4, r0
    add sp, #0x30
    pop {r3-r7, pc}
    .pool

TCM_Ov01Style13NameCacheHelper_End:

; Input r0 = ordinary effect environment. Symbol and portrait resources are already live, so this palette takes the third manager slot without changing their allocation order. The retained palette is consumed by TextOBJ setup.
TCM_Ov01Style13InitNamePaletteAndCapturePreBanner:
    push {r3-r7, lr}
    sub sp, #0x10
    add r4, r0, #0
    ldr r5, [r4, #0x0C]
    cmp r5, #0
    beq TCM_Ov01Style13InitNamePaletteFail

    ldr r6, =TCM_STYLE13_WORK_TEXT_PALETTE_RESOURCE
    add r6, r5, r6
    ldr r0, [r6, #0x00]
    cmp r0, #0
    bne TCM_Ov01Style13InitNamePaletteFail

    ldr r0, =TCM_STYLE13_WORK_BUNDLE + 0x130
    add r0, r5, r0
    ldr r7, [r0, #0x00]
    cmp r7, #0
    beq TCM_Ov01Style13InitNamePaletteFail
    ldr r0, [r7, #0x10]
    cmp r0, #1
    bne TCM_Ov01Style13InitNamePaletteFail

    ldr r1, [r4, #0x20]
    cmp r1, #0
    beq TCM_Ov01Style13InitNamePaletteFail
    add r0, r7, #0
    mov r2, #0x10
    mov r3, #0
    ldr r1, =TCM_STYLE13_TEXT_PALETTE_RESOURCE_KEY
    str r1, [sp, #0x00]
    mov r1, #1
    str r1, [sp, #0x04]
    str r1, [sp, #0x08]
    mov r1, #4
    str r1, [sp, #0x0C]
    ldr r1, [r4, #0x20]
TCM_Ov01Style13NamePaletteCreateCall:
    bl AddPlttResObjFromOpenNarc
    cmp r0, #0
    beq TCM_Ov01Style13InitNamePaletteFail
    str r0, [r6, #0x00]
TCM_Ov01Style13NamePaletteResourceStored:

TCM_Ov01Style13NamePaletteTransferCall:
    bl SpriteTransfer_CreatePlttTransferTask
    cmp r0, #0
    beq TCM_Ov01Style13InitNamePaletteTransferFailed
TCM_Ov01Style13NamePaletteTransferCreated:

    ldr r0, [r6, #0x00]
    mov r1, #0
TCM_Ov01Style13NamePaletteProxyCall:
    bl SpriteTransfer_GetPaletteProxy
    cmp r0, #0
    beq TCM_Ov01Style13InitNamePaletteProxyFailed
TCM_Ov01Style13NamePaletteProxyResolved:

    add r0, r4, #0
TCM_Ov01Style13NamePaletteCaptureCall:
    bl TCM_Ov01Style13InitNameFontCharAndCapturePreBanner
    add sp, #0x10
    pop {r3-r7, pc}

TCM_Ov01Style13InitNamePaletteProxyFailed:
    ldr r0, [r6, #0x00]
TCM_Ov01Style13NamePaletteDeletePartialTransferCall:
    bl SpriteTransfer_DeletePlttTransferTask

TCM_Ov01Style13InitNamePaletteTransferFailed:
    add r0, r7, #0
    ldr r1, [r6, #0x00]
TCM_Ov01Style13NamePaletteDestroyPartialResourceCall:
    bl DestroySingle2DGfxResObj
    mov r0, #0
    str r0, [r6, #0x00]

TCM_Ov01Style13InitNamePaletteFail:
    add r0, r5, #0
TCM_Ov01Style13NameWindowReleaseOnSetupFailureCall:
    bl TCM_Ov01Style13ReleaseNameWindowIfOwned
    ldr r0, =(TCM_Ov01Style13Fail | 1)
    bx r0
    .pool

; Input r0 = bundle at work+0x10. Portrait and symbol resources have already been destroyed. Release the private transfer/resource before freeing the manager that owns its SpriteResource slot.
TCM_Ov01Style13DestroyNamePaletteAndBundle:
    push {r4-r6, lr}
    add r4, r0, #0
    add r5, r4, #0
    sub r5, #TCM_STYLE13_WORK_BUNDLE
    ldr r6, =TCM_STYLE13_WORK_TEXT_PALETTE_RESOURCE
    add r6, r5, r6
    ldr r0, [r6, #0x00]
    cmp r0, #0
    beq TCM_Ov01Style13DestroyNamePaletteBundle

TCM_Ov01Style13NamePaletteDeleteTransferCall:
    bl SpriteTransfer_DeletePlttTransferTask
    ldr r0, =0x130
    ldr r0, [r4, r0]
    cmp r0, #0
    beq TCM_Ov01Style13InitNamePaletteFail
    ldr r1, [r6, #0x00]
TCM_Ov01Style13NamePaletteDestroyResourceCall:
    bl DestroySingle2DGfxResObj
    mov r0, #0
    str r0, [r6, #0x00]
TCM_Ov01Style13NamePaletteDestroyed:

TCM_Ov01Style13DestroyNamePaletteBundle:
    add r0, r4, #0
TCM_Ov01Style13NamePaletteDestroyBundleCall:
    bl Ov01_DestroySpriteResourceBundle
    pop {r4-r6, pc}
    .pool

TCM_Ov01Style13NamePaletteHelper_End:

; -----------------------------------------------------------------------------
; Ordinary style-13 persistent name font/character reservation ownership
; -----------------------------------------------------------------------------

; Input r0 = live work-owned Window, r4 = style-13 work. Calculate persistent Main-OBJ character demand and retain the Window on success. A zero result removes the Window before rejoining normalised/source/message cleanup.
TCM_Ov01Style13CalcCharDemandAndRetainWindow:
    push {r3-r7, lr}
    add r6, r0, #0
    add r5, r4, #0
    mov r1, #1
    mov r2, #4
TCM_Ov01Style13NameCharDemandCall:
    bl TextOBJ_CalcCharDemand
    add r7, r0, #0
    ldr r1, =TCM_STYLE13_WORK_CHAR_DEMAND
    str r7, [r5, r1]
TCM_Ov01Style13NameCharDemandStored:
    cmp r7, #0
    beq TCM_Ov01Style13NameCharDemandReleaseFailed
    pop {r3-r7, pc}

TCM_Ov01Style13NameCharDemandReleaseFailed:
    add r0, r6, #0
TCM_Ov01Style13NameRemoveWindowAfterDemandFailureCall:
    bl RemoveWindow
TCM_Ov01Style13NameWindowRemovedAfterDemandFailure:
TCM_Ov01Style13NameCharDemandFailed:
    mov r0, #TCM_STYLE13_NAME_STATUS_CHAR_DEMAND
    ldr r1, =TCM_STYLE13_WORK_NAME_STATUS
    str r0, [r5, r1]
    ; Wrapper frame is 0x18 bytes below the outer helper frame. Clear the outer plan count at 0x18+0x14 so the caller retains fallback capacity 5.
    mov r0, #0
    str r0, [sp, #0x2C]
    ldr r0, =(TCM_Ov01Style13NameDeleteNormalizedFailure | 1)
    str r0, [sp, #0x14]
    pop {r3-r7, pc}

; Input r0 = nullable style-13 work. Window+0x0C is the ownership predicate: this specialized text Window deliberately keeps bgId 0xFF while allocated.
TCM_Ov01Style13ReleaseNameWindowIfOwned:
    push {r3-r5, lr}
    add r4, r0, #0
    cmp r4, #0
    beq TCM_Ov01Style13ReleaseNameWindowReturn
    ldr r5, =TCM_STYLE13_WORK_NAME_WINDOW
    add r5, r4, r5
    ldr r0, [r5, #0x0C]
    cmp r0, #0
    beq TCM_Ov01Style13ReleaseNameWindowReturn
    add r0, r5, #0
TCM_Ov01Style13NameOptionalRemoveWindowCall:
    bl RemoveWindow
TCM_Ov01Style13NameOptionalWindowReleased:

TCM_Ov01Style13ReleaseNameWindowReturn:
    pop {r3-r5, pc}
    .pool

; Input r0 = style-13 work. Build the exact native-shaped template on an aligned local frame, synchronously create the glyph sprites, and hide them before any SpriteList submission can occur. Return r0 = TextOBJ or zero only when a checked pre-constructor owner is missing. A null constructor return is an assertion-contract failure and stops without speculative partial cleanup.
TCM_Ov01Style13ConstructHiddenTextOBJ:
    push {r3-r7, lr}
    sub sp, #0x30
    add r4, r0, #0

    ldr r0, =TCM_STYLE13_WORK_TEXT_PALETTE_RESOURCE
    ldr r0, [r4, r0]
    cmp r0, #0
    beq TCM_Ov01Style13ConstructHiddenTextOBJPreconditionFail
    mov r1, #0
TCM_Ov01Style13NameTextObjPaletteProxyCall:
    bl SpriteTransfer_GetPaletteProxy
    cmp r0, #0
    beq TCM_Ov01Style13ConstructHiddenTextOBJPreconditionFail
    str r0, [sp, #0x0C]

    ldr r0, =TCM_STYLE13_WORK_FONT_SYSTEM
    ldr r0, [r4, r0]
    cmp r0, #0
    beq TCM_Ov01Style13ConstructHiddenTextOBJPreconditionFail
    str r0, [sp, #0x00]

    ldr r0, =TCM_STYLE13_WORK_NAME_WINDOW
    add r0, r4, r0
    ldr r1, [r0, #0x0C]
    cmp r1, #0
    beq TCM_Ov01Style13ConstructHiddenTextOBJPreconditionFail
    str r0, [sp, #0x04]

    ldr r0, [r4, #TCM_STYLE13_WORK_BUNDLE]
    cmp r0, #0
    beq TCM_Ov01Style13ConstructHiddenTextOBJPreconditionFail
    str r0, [sp, #0x08]

    mov r0, #0
    str r0, [sp, #0x10]
    ldr r1, =TCM_STYLE13_WORK_CHAR_DESCRIPTOR
    add r1, r4, r1
    ldr r1, [r1, #TCM_STYLE13_CHAR_DESC_OFFSET]
    str r1, [sp, #0x14]
    mov r1, #116
    str r1, [sp, #0x18]
    mov r1, #80
    str r1, [sp, #0x1C]
    str r0, [sp, #0x20]
    mov r1, #11
    str r1, [sp, #0x24]
    mov r1, #1
    str r1, [sp, #0x28]
    mov r1, #4
    str r1, [sp, #0x2C]

    add r0, sp, #0
TCM_Ov01Style13NameTextObjConstructorCall:
    bl TextOBJ_New
TCM_Ov01Style13NameTextObjConstructorReturned:
    cmp r0, #0
    beq TCM_Ov01Style13NameTextObjConstructorInvariantFail
    add r5, r0, #0
    ldr r1, =TCM_STYLE13_WORK_TEXT_OBJ
    str r5, [r4, r1]
TCM_Ov01Style13NameTextObjStored:

    add r0, r5, #0
    mov r1, #0
TCM_Ov01Style13NameTextObjPaletteOffsetCall:
    bl TextOBJ_SetPaletteOffset
    add r0, r5, #0
    mov r1, #116
    mov r2, #80
TCM_Ov01Style13NameTextObjPositionCall:
    bl TextOBJ_SetPosition
    add r0, r5, #0
    mov r1, #0
TCM_Ov01Style13NameTextObjHideCall:
    bl TextOBJ_SetSpritesDrawFlag
TCM_Ov01Style13NameTextObjHidden:

    add r0, r5, #0
    add sp, #0x30
    pop {r3-r7, pc}

TCM_Ov01Style13ConstructHiddenTextOBJPreconditionFail:
    mov r0, #0
    add sp, #0x30
    pop {r3-r7, pc}

TCM_Ov01Style13NameTextObjConstructorInvariantFail:
    ldr r0, =(TCM_Ov01Style13Fail | 1)
    bx r0
    .pool

; Input r0 = ordinary environment. The accepted palette helper retains r4 = environment, r5 = work, r6 = palette owner slot, and r7 = palette manager. Acquire persistent font/character/TextOBJ owners, then invoke capture once.
TCM_Ov01Style13InitNameFontCharAndCapturePreBanner:
    push {r3-r7, lr}
    cmp r5, #0
    beq TCM_Ov01Style13InitNameFontCharCleanup

    ldr r0, =TCM_STYLE13_WORK_NAME_STATUS
    ldr r0, [r5, r0]
    cmp r0, #TCM_STYLE13_NAME_STATUS_OK
    bne TCM_Ov01Style13InitNameFontCharCleanup
    ldr r0, =TCM_STYLE13_WORK_CHAR_DEMAND
    ldr r0, [r5, r0]
    cmp r0, #0
    beq TCM_Ov01Style13InitNameFontCharCleanup

    ldr r0, =TCM_STYLE13_WORK_TEXT_OBJ
    ldr r0, [r5, r0]
    cmp r0, #0
    bne TCM_Ov01Style13InitNameTextObjInvariantFail

    ldr r0, =TCM_STYLE13_WORK_FONT_SYSTEM
    ldr r0, [r5, r0]
    cmp r0, #0
    bne TCM_Ov01Style13InitNameFontCharCleanup
    ldr r0, =TCM_STYLE13_WORK_CHAR_DESCRIPTOR
    add r0, r5, r0
    ldr r1, [r0, #0x00]
    ldr r2, [r0, #0x04]
    ldr r3, [r0, #0x08]
    orr r1, r2
    orr r1, r3
    cmp r1, #0
    bne TCM_Ov01Style13InitNameFontCharCleanup

    mov r0, #4
    mov r1, #4
TCM_Ov01Style13NameFontSystemNewCall:
    bl FontSystem_NewInit
    cmp r0, #0
    beq TCM_Ov01Style13InitNameFontCharCleanup
    ldr r1, =TCM_STYLE13_WORK_FONT_SYSTEM
    str r0, [r5, r1]
TCM_Ov01Style13NameFontSystemStored:

    ldr r0, =TCM_STYLE13_WORK_CHAR_DEMAND
    ldr r0, [r5, r0]
    mov r1, #1
    mov r2, #1
    ldr r3, =TCM_STYLE13_WORK_CHAR_DESCRIPTOR
    add r3, r5, r3
TCM_Ov01Style13NameCharReserveCall:
    bl ObjCharTransfer_Reserve
TCM_Ov01Style13NameCharReserveReturned:
    cmp r0, #1
    bne TCM_Ov01Style13InitNameFontCharCleanup

    ldr r0, =TCM_STYLE13_WORK_CHAR_DESCRIPTOR
    add r0, r5, r0
    ldr r1, [r0, #TCM_STYLE13_CHAR_DESC_SIZE]
    cmp r1, #0
    beq TCM_Ov01Style13InitNameFontCharCleanup
    ldrh r1, [r0, #TCM_STYLE13_CHAR_DESC_VRAM]
    cmp r1, #1
    bne TCM_Ov01Style13InitNameFontCharCleanup
    ldrh r1, [r0, #TCM_STYLE13_CHAR_DESC_IS_AT_END]
    cmp r1, #1
    bne TCM_Ov01Style13InitNameFontCharCleanup
TCM_Ov01Style13NameCharReserved:

    ldr r0, =TCM_STYLE13_WORK_NAME_WINDOW
    add r0, r5, r0
    ldr r1, [r0, #0x0C]
    cmp r1, #0
    beq TCM_Ov01Style13InitNameFontCharCleanup
TCM_Ov01Style13NameWindowReadyForTextOBJ:
    add r0, r5, #0
TCM_Ov01Style13NameTextObjCreateCall:
    bl TCM_Ov01Style13ConstructHiddenTextOBJ
    cmp r0, #0
    beq TCM_Ov01Style13InitNameFontCharCleanup
TCM_Ov01Style13NameTextObjReady:

    add r0, r5, #0
TCM_Ov01Style13NameWindowReleaseAfterResourceSetupCall:
    bl TCM_Ov01Style13ReleaseNameWindowIfOwned
TCM_Ov01Style13NameWindowReleasedAfterResourceSetup:

    add r0, r4, #0
TCM_Ov01Style13NameFontCharCaptureCall:
    bl TCM_Ov01Style13CapturePreBanner
    pop {r3-r7, pc}

; The reserve API leaves a failed descriptor untouched. Because entry requires a zero descriptor, size nonzero proves reservation ownership and permits the paired release. Then clear all new fields before accepted palette cleanup.
TCM_Ov01Style13InitNameFontCharCleanup:
    cmp r5, #0
    beq TCM_Ov01Style13InitNameFontCharJumpPaletteFailure
    ldr r6, =TCM_STYLE13_WORK_CHAR_DESCRIPTOR
    add r6, r5, r6
    ldr r0, [r6, #TCM_STYLE13_CHAR_DESC_SIZE]
    cmp r0, #0
    beq TCM_Ov01Style13InitNameFontCharDeleteFont
    add r0, r6, #0
TCM_Ov01Style13NameCharReleasePartialCall:
    bl ObjCharTransfer_Release

TCM_Ov01Style13InitNameFontCharDeleteFont:
    ldr r1, =TCM_STYLE13_WORK_FONT_SYSTEM
    ldr r0, [r5, r1]
    cmp r0, #0
    beq TCM_Ov01Style13InitNameFontCharClear
TCM_Ov01Style13NameFontSystemDeletePartialCall:
    bl FontSystem_Delete

TCM_Ov01Style13InitNameFontCharClear:
    mov r0, #0
    ldr r1, =TCM_STYLE13_WORK_FONT_SYSTEM
    str r0, [r5, r1]
    str r0, [r6, #0x00]
    str r0, [r6, #0x04]
    str r0, [r6, #0x08]
    ldr r1, =TCM_STYLE13_WORK_CHAR_DEMAND
    str r0, [r5, r1]

TCM_Ov01Style13InitNameFontCharJumpPaletteFailure:
    ldr r0, =(TCM_Ov01Style13InitNamePaletteProxyFailed | 1)
    str r0, [sp, #0x14]
    pop {r3-r7, pc}

TCM_Ov01Style13InitNameTextObjInvariantFail:
    ldr r0, =(TCM_Ov01Style13Fail | 1)
    str r0, [sp, #0x14]
    pop {r3-r7, pc}

; Input r0 = bundle at work+0x10. Destroy the TextOBJ and its glyph sprites before releasing character reservation, FontSystem, palette/resource, and the bundle-owned SpriteList.
TCM_Ov01Style13DestroyNameOwnersPaletteAndBundle:
    push {r3-r7, lr}
    add r4, r0, #0
    add r5, r4, #0
    sub r5, #TCM_STYLE13_WORK_BUNDLE
    ldr r6, =TCM_STYLE13_WORK_CHAR_DESCRIPTOR
    add r6, r5, r6

    ldr r0, [r6, #TCM_STYLE13_CHAR_DESC_SIZE]
    cmp r0, #0
    beq TCM_Ov01Style13DestroyNameOwnersFail
    ldrh r0, [r6, #TCM_STYLE13_CHAR_DESC_VRAM]
    cmp r0, #1
    bne TCM_Ov01Style13DestroyNameOwnersFail
    ldrh r0, [r6, #TCM_STYLE13_CHAR_DESC_IS_AT_END]
    cmp r0, #1
    bne TCM_Ov01Style13DestroyNameOwnersFail
    ldr r1, =TCM_STYLE13_WORK_FONT_SYSTEM
    ldr r0, [r5, r1]
    cmp r0, #0
    beq TCM_Ov01Style13DestroyNameOwnersFail

    ldr r1, =TCM_STYLE13_WORK_TEXT_OBJ
    ldr r7, [r5, r1]
    cmp r7, #0
    beq TCM_Ov01Style13DestroyNameOwnersFail
    add r0, r7, #0
TCM_Ov01Style13NameTextObjDeleteCall:
    bl FontOAM_Delete
    mov r0, #0
    ldr r1, =TCM_STYLE13_WORK_TEXT_OBJ
    str r0, [r5, r1]
TCM_Ov01Style13NameTextObjDeleted:

    add r0, r6, #0
TCM_Ov01Style13NameCharReleaseCall:
    bl ObjCharTransfer_Release
    mov r0, #0
    str r0, [r6, #0x00]
    str r0, [r6, #0x04]
    str r0, [r6, #0x08]
TCM_Ov01Style13NameCharReleased:

    ldr r1, =TCM_STYLE13_WORK_FONT_SYSTEM
    ldr r0, [r5, r1]
TCM_Ov01Style13NameFontSystemDeleteCall:
    bl FontSystem_Delete
    mov r0, #0
    ldr r1, =TCM_STYLE13_WORK_FONT_SYSTEM
    str r0, [r5, r1]
    ldr r1, =TCM_STYLE13_WORK_CHAR_DEMAND
    str r0, [r5, r1]
TCM_Ov01Style13NameFontSystemDeleted:

    add r0, r4, #0
TCM_Ov01Style13NameOwnersPaletteBundleCall:
    bl TCM_Ov01Style13DestroyNamePaletteAndBundle
    pop {r3-r7, pc}

TCM_Ov01Style13DestroyNameOwnersFail:
    ldr r0, =(TCM_Ov01Style13Fail | 1)
    str r0, [sp, #0x14]
    pop {r3-r7, pc}
    .pool

TCM_Ov01Style13NameFontCharHelper_End:

; r0=BattleSetup. Returns r0=vsStyle and r1=raw class battleMusic. The active trainer class is cached for the existing overlay-specific recipe readers.
TCM_ReadTrainerRoutingMetadata:
    push {r4, lr}
    sub sp, #0x10
    add r4, r0, #0

    ldr r0, [r4, #0x1C]         ; Enemy trainer ID, not embedded class byte.
    mov r1, #0x1                ; TrainerData attr: trainer class.
    bl TrainerData_GetAttr
    add r2, r0, #0              ; Preserve the full trainerClass result.

    ldr r1, =TCM_CurrentTrainerClass
    str r2, [r1, #0x00]

    mov r0, #TCM_ROUTE_METADATA_SIZE
    str r0, [sp, #0x00]
    add r0, sp, #0x04
    mov r1, #NARC_a_1_5_5
    mov r3, #TCM_ROUTE_METADATA_OFFSET
    bl ReadFromNarcMemberByIdPair

    add r2, sp, #0x04
    ldrh r1, [r2, #0x00]
    ldrh r0, [r2, #TCM_ROUTE_STYLE_RELATIVE_OFFSET]
    cmp r0, #TCM_VS_STYLE_FRONTIER_BRAIN
    bhi TCM_ReadTrainerRoutingMetadata_BadRecord

    add sp, #0x10
    pop {r4, pc}

TCM_ReadTrainerRoutingMetadata_BadRecord:
    bl GF_AssertFail
    b TCM_ReadTrainerRoutingMetadata_BadRecord
    .pool

TCM_ReadTrainerRoutingMetadata_End:

; r0=BattleSetup. Returns r0=selected music and r1=native combo for wild or zero for trainer. The public caller copies these into r4 and r6 before its unchanged regional-substitution block.
TCM_SelectBattleMusicDirect:
    push {r4-r7, lr}
    sub sp, #0x0C
    add r5, r0, #0
    ldr r6, [r5, #0x00]

    mov r0, #TCM_BATTLE_TYPE_TRAINER
    tst r6, r0
    beq TCM_SelectBattleMusicDirect_Wild

    mov r0, #TCM_BATTLE_TYPE_LINK
    tst r6, r0
    bne TCM_SelectBattleMusicDirect_Link

    mov r0, #TCM_BATTLE_TYPE_FRONTIER
    tst r6, r0
    bne TCM_SelectBattleMusicDirect_Frontier

    add r0, r5, #0
    bl TCM_ReadTrainerRoutingMetadata
    add r7, r0, #0
    add r4, r1, #0

    ; Only Style 0 uses the global ordinary-double music policy.
    cmp r7, #0
    bne TCM_SelectBattleMusicDirect_ClassMusic
    b TCM_SelectBattleMusicDirect_CheckOrdinaryDouble
    nop

TCM_SelectBattleMusicDirect_CheckOrdinaryDouble:
    mov r0, #TCM_BATTLE_TYPE_DOUBLES
    tst r6, r0
    beq TCM_SelectBattleMusicDirect_ClassMusic
    ldr r0, =TCM_TRAINER_DOUBLE_MUSIC
    b TCM_SelectBattleMusicDirect_TrainerReturn

TCM_SelectBattleMusicDirect_ClassMusic:
    add r0, r4, #0
    b TCM_SelectBattleMusicDirect_TrainerReturn

TCM_SelectBattleMusicDirect_Link:
    ldr r0, =TCM_LINK_BATTLE_MUSIC
    b TCM_SelectBattleMusicDirect_TrainerReturn

TCM_SelectBattleMusicDirect_Frontier:
    ldr r0, =TCM_FRONTIER_BATTLE_MUSIC

TCM_SelectBattleMusicDirect_TrainerReturn:
    mov r1, #0x00
    b TCM_SelectBattleMusicDirect_Return

TCM_SelectBattleMusicDirect_Wild:
    add r0, r5, #0
    bl BattleSetup_GetTransitionAndMusicParam
    add r4, r0, #0
    bl BattleStartGetMusic
    add r1, r4, #0

TCM_SelectBattleMusicDirect_Return:
    add sp, #0x0C
    pop {r4-r7, pc}
    .pool

TCM_SelectBattleMusicDirect_End:

; r0=BattleSetup. Returns the selected EFFECT ID. Trainer origins never call a shared combo consumer; wild origins replay the complete native route.
TCM_SelectBattleTransitionDirect:
    push {r4-r7, lr}
    ; Five saved registers plus 0x0C locals preserve eight-byte alignment.
    sub sp, #0x0C
    add r5, r0, #0              ; BattleSetup.
    ldr r6, [r5, #0x00]         ; Battle type.

    ; Start every transition selection from a neutral presentation state. Only an ordinary route surviving the policy checks below may set this latch.
    ldr r0, =TCM_CurrentOrdinaryStyle
    mov r1, #0
    strh r1, [r0, #0x00]

    mov r0, #TCM_BATTLE_TYPE_TRAINER
    tst r6, r0
    beq TCM_SelectBattleTransitionDirect_Wild

    ; Link setup does not have a stable enemy trainer class at this point.
    mov r0, #TCM_BATTLE_TYPE_LINK
    tst r6, r0
    bne TCM_SelectBattleTransitionDirect_Link

    mov r0, #TCM_BATTLE_TYPE_FRONTIER
    tst r6, r0
    bne TCM_SelectBattleTransitionDirect_Frontier

    add r0, r5, #0
    bl TCM_ReadTrainerRoutingMetadata
    add r7, r0, #0

    ; Only Style 0 uses the global ordinary-double presentation policy.
    cmp r7, #0
    bne TCM_SelectBattleTransitionDirect_DispatchStyle
    b TCM_SelectBattleTransitionDirect_CheckOrdinaryDouble
    nop

TCM_SelectBattleTransitionDirect_CheckOrdinaryDouble:
    mov r0, #TCM_BATTLE_TYPE_DOUBLES
    tst r6, r0
    bne TCM_SelectBattleTransitionDirect_OrdinaryDouble

TCM_SelectBattleTransitionDirect_DispatchStyle:
    ldr r0, =TCM_CurrentOrdinaryStyle
    strh r7, [r0, #0x00]

    cmp r7, #0
    beq TCM_SelectBattleTransitionDirect_Dynamic
    cmp r7, #TCM_VS_STYLE_20BYTE
    beq TCM_SelectBattleTransitionDirect_Vs20
    cmp r7, #TCM_VS_STYLE_8BYTE
    beq TCM_SelectBattleTransitionDirect_Vs8
    cmp r7, #TCM_VS_STYLE_ROCKET_ADMIN
    beq TCM_SelectBattleTransitionDirect_RocketAdmin
    cmp r7, #TCM_VS_STYLE_KIMONO
    beq TCM_SelectBattleTransitionDirect_Kimono
    cmp r7, #TCM_VS_STYLE_RED
    beq TCM_SelectBattleTransitionDirect_Red
    cmp r7, #TCM_VS_STYLE_ROCKET_GRUNT
    beq TCM_SelectBattleTransitionDirect_RocketGrunt
    cmp r7, #TCM_VS_STYLE_FRONTIER_BRAIN
    beq TCM_SelectBattleTransitionDirect_Style13

    ; Valid remaining styles 7..12 map to native effects 6..11.
    add r0, r7, #0
    sub r0, #1
    b TCM_SelectBattleTransitionDirect_Return

TCM_SelectBattleTransitionDirect_Dynamic:
    add r0, r5, #0
    bl Ov01_DynamicBattleTransitionSelector
    b TCM_SelectBattleTransitionDirect_Return

TCM_SelectBattleTransitionDirect_Vs20:
    mov r0, #TCM_TRANSITION_EFFECT_VS20
    b TCM_SelectBattleTransitionDirect_Return

TCM_SelectBattleTransitionDirect_Vs8:
    mov r0, #TCM_TRANSITION_EFFECT_VS8
    b TCM_SelectBattleTransitionDirect_Return

TCM_SelectBattleTransitionDirect_RocketAdmin:
    mov r0, #TCM_TRANSITION_EFFECT_ROCKET_ADMIN
    b TCM_SelectBattleTransitionDirect_Return

TCM_SelectBattleTransitionDirect_Kimono:
    mov r0, #TCM_TRANSITION_EFFECT_KIMONO
    b TCM_SelectBattleTransitionDirect_Return

TCM_SelectBattleTransitionDirect_Red:
    mov r0, #TCM_TRANSITION_EFFECT_RED
    b TCM_SelectBattleTransitionDirect_Return

TCM_SelectBattleTransitionDirect_Style13:
    ; RCA-010 requires class assets cached before Overlay1 registration.
    bl TCM_CacheStyle13AssetQuartets
    mov r0, #TCM_TRANSITION_EFFECT_NORMAL_EARLY
    b TCM_SelectBattleTransitionDirect_Return

TCM_SelectBattleTransitionDirect_RocketGrunt:
    mov r0, #TCM_TRANSITION_EFFECT_ROCKET_GRUNT
    b TCM_SelectBattleTransitionDirect_Return

TCM_SelectBattleTransitionDirect_OrdinaryDouble:
    mov r0, #TCM_TRAINER_DOUBLE_EFFECT
    b TCM_SelectBattleTransitionDirect_Return

TCM_SelectBattleTransitionDirect_Link:
    mov r0, #TCM_BATTLE_TYPE_DOUBLES
    tst r6, r0
    beq TCM_SelectBattleTransitionDirect_LinkSingle
    mov r0, #TCM_LINK_DOUBLE_EFFECT
    b TCM_SelectBattleTransitionDirect_Return

TCM_SelectBattleTransitionDirect_LinkSingle:
    mov r0, #TCM_LINK_SINGLE_EFFECT
    b TCM_SelectBattleTransitionDirect_Return

TCM_SelectBattleTransitionDirect_Frontier:
    mov r0, #TCM_BATTLE_TYPE_DOUBLES
    tst r6, r0
    beq TCM_SelectBattleTransitionDirect_FrontierSingle
    mov r0, #TCM_FRONTIER_DOUBLE_EFFECT
    b TCM_SelectBattleTransitionDirect_Return

TCM_SelectBattleTransitionDirect_FrontierSingle:
    mov r0, #TCM_FRONTIER_SINGLE_EFFECT
    b TCM_SelectBattleTransitionDirect_Return

TCM_SelectBattleTransitionDirect_Wild:
    add r0, r5, #0
    bl BattleSetup_GetTransitionAndMusicParam
    add r1, r5, #0
    bl BattleStartGetTransition

TCM_SelectBattleTransitionDirect_Return:
    add sp, #0x0C
    pop {r4-r7, pc}
    .pool

TCM_SelectBattleTransitionDirect_End:
    .align 4

; Reconstruct Overlay-115's private 20-byte Style-1 row from the flat release record. r0 is the native fallback row and remains the return value when the active class is not configured for Style 1.
TCM_SelectVs20RecordRelease:
    push {r4, r5, r6, r7, lr}
    sub sp, #0x34
    add r4, r0, #0

    ldr r0, =TCM_CurrentTrainerClass
    ldr r7, [r0, #0x00]
    mov r0, #TCM_VS_STYLE_SIZE
    str r0, [sp, #0x00]
    add r0, sp, #0x04
    mov r1, #NARC_a_1_5_5
    add r2, r7, #0
    mov r3, #TCM_VS_STYLE_OFFSET
    bl ReadFromNarcMemberByIdPair
    add r0, sp, #0x04
    ldrh r0, [r0, #0x00]
    cmp r0, #TCM_VS_STYLE_20BYTE
    bne TCM_SelectVs20RecordRelease_Fallback

    ; 0x0C..0x33 contains name, Style-1 controls, and all asset groups.
    mov r0, #0x28
    str r0, [sp, #0x00]
    add r0, sp, #0x04
    mov r1, #NARC_a_1_5_5
    add r2, r7, #0
    mov r3, #TCM_TRAINER_NAME_OFFSET
    bl ReadFromNarcMemberByIdPair

    add r5, sp, #0x04
    ldr r6, =TCM_Vs20ScratchRecord

    ldr r0, [r5, #0x04]         ; Style-1 motion.
    str r0, [r6, #0x00]
    mov r1, #0
    str r1, [r6, #0x04]
    ldrh r0, [r5, #0x00]        ; Zero-extended trainerNameId.
    strh r0, [r6, #0x04]

    ldrb r0, [r5, #0x02]        ; useSavedRivalName.
    cmp r0, #1
    bhi TCM_SelectVs20RecordRelease_Assert
    cmp r0, #0
    beq TCM_SelectVs20RecordRelease_StaticName
    mov r0, #0x17
TCM_SelectVs20RecordRelease_StaticName:
    strh r0, [r6, #0x08]
    strh r1, [r6, #0x0A]        ; Unused native-format halfword.

    ; Group 2 is the portrait quartet. Native private fields remain byte-shaped; widened loaders continue to consume the release u16 rows directly.
    ldrh r0, [r5, #0x18]
    strb r0, [r6, #0x0C]
    ldrh r0, [r5, #0x1A]
    strb r0, [r6, #0x0D]
    ldrh r0, [r5, #0x1C]
    strb r0, [r6, #0x0E]
    ldrh r0, [r5, #0x1E]
    strb r0, [r6, #0x0F]

    ; Group 1 supplies Style-1 background RLCN, RGCN, and RCSN 1.
    ldrh r0, [r5, #0x0A]
    strb r0, [r6, #0x10]
    ldrh r0, [r5, #0x0C]
    strb r0, [r6, #0x11]
    ldrh r0, [r5, #0x12]
    strb r0, [r6, #0x12]
    strb r1, [r6, #0x13]

    ; Group 3 is the standard VS-symbol quartet.
    ldr r6, =TCM_Vs20SymbolScratch
    ldrh r0, [r5, #0x20]
    strh r0, [r6, #0x00]
    ldrh r0, [r5, #0x22]
    strh r0, [r6, #0x02]
    ldrh r0, [r5, #0x24]
    strh r0, [r6, #0x04]
    ldrh r0, [r5, #0x26]
    strh r0, [r6, #0x06]

    ldr r4, =TCM_Vs20ScratchRecord
TCM_SelectVs20RecordRelease_Fallback:
    add r0, r4, #0
    add sp, #0x34
    pop {r4, r5, r6, r7, pc}

TCM_SelectVs20RecordRelease_Assert:
    bl GF_AssertFail
    b TCM_SelectVs20RecordRelease_Assert
    .pool

; Reconstruct Overlay-115's private Style-2 row. The release duration is u16 for a stable schema, but the native state machine consumes one byte.
TCM_SelectVs8RecordRelease:
    push {r4, r5, r6, r7, lr}
    sub sp, #0x34
    add r4, r0, #0

    ldr r0, =TCM_CurrentTrainerClass
    ldr r7, [r0, #0x00]
    mov r0, #0x2A              ; vsStyle at 0x0A through record end 0x34.
    str r0, [sp, #0x00]
    add r0, sp, #0x04
    mov r1, #NARC_a_1_5_5
    add r2, r7, #0
    mov r3, #TCM_VS_STYLE_OFFSET
    bl ReadFromNarcMemberByIdPair

    add r5, sp, #0x04
    ldrh r0, [r5, #0x00]
    cmp r0, #TCM_VS_STYLE_8BYTE
    bne TCM_SelectVs8RecordRelease_Fallback

    ldr r6, =TCM_Vs8ScratchRecord
    mov r0, #0
    str r0, [r6, #0x00]
    str r0, [r6, #0x04]
    str r0, [r6, #0x08]
    str r0, [r6, #0x0C]

    ldrh r0, [r5, #0x0A]       ; Style-2 interpolation duration.
    add r1, r0, #0
    lsr r1, r1, #0x08
    cmp r1, #0
    bne TCM_SelectVs8RecordRelease_Assert
    strb r0, [r6, #0x03]

    ldrh r0, [r5, #0x02]       ; trainerNameId.
    strh r0, [r6, #0x06]

    ; Group 2 is the portrait quartet; palette is also private row field 0.
    ldrh r0, [r5, #0x1A]
    strh r0, [r6, #0x08]
    strh r0, [r6, #0x00]
    ldrh r0, [r5, #0x1C]
    strh r0, [r6, #0x0A]
    ldrh r0, [r5, #0x1E]
    strh r0, [r6, #0x0C]
    ldrh r0, [r5, #0x20]
    strh r0, [r6, #0x0E]

    bl TCM_CacheVs8BackgroundQuartet
    ldr r4, =TCM_Vs8ScratchRecord
TCM_SelectVs8RecordRelease_Fallback:
    add r0, r4, #0
    add sp, #0x34
    pop {r4, r5, r6, r7, pc}

TCM_SelectVs8RecordRelease_Assert:
    bl GF_AssertFail
    b TCM_SelectVs8RecordRelease_Assert
    .pool

; -----------------------------------------------------------------------------
; Overlay 12 prize helpers
; -----------------------------------------------------------------------------

TCM_PrizeReadTrainerAndCacheClass:
    push {r4, lr}

    ldrh r4, [r4, r5]           ; Displaced opponent trainer-ID load.
    add r0, r4, #0
    mov r1, #0x1                ; TrainerData attr: trainer class.
    bl TrainerData_GetAttr
    str r0, [sp, #0x40]         ; Caller [sp,#0x38], beneath this 8-byte push.

    add r0, r4, #0
    add r1, sp, #0x0C           ; Caller local Trainer at [sp,#0x04].
    bl TrainerData_ReadTrData

    pop {r4, pc}

TCM_PrizeReadTrainerAndCacheClass_End:
    .align 4

TCM_GetTrainerClassPrizeCoefficient:
    push {r4, lr}
    sub sp, #0x8

    add r4, r0, #0              ; Preserve trainerClass across loader call.

    mov r0, #TCM_PRIZE_SIZE
    str r0, [sp, #0x0]          ; Fifth arg: size.

    add r0, sp, #0x4            ; First arg: stack buffer destination.
    mov r1, #NARC_a_1_5_5
    add r2, r4, #0              ; Third arg: member id == trainerClass.
    mov r3, #TCM_PRIZE_OFFSET
    bl ReadFromNarcMemberByIdPair

    add r0, sp, #0x4
    ldrh r0, [r0, #0x0]

    add sp, #0x8
    pop {r4, pc}

TCM_GetTrainerClassPrizeCoefficient_End:
    .align 4

; -----------------------------------------------------------------------------
; Overlay 80 Frontier Brain recipe helpers
; -----------------------------------------------------------------------------

TCM_Ov80LoadFrontierBrainRecipe:
    push {r4, lr}

    add r0, r4, #0
    bl TCM_Ov80SelectFrontierRow
    add r1, r0, #0

    add r0, r4, #0
    bl Ov80_FrontierRowConsumer

    pop {r4, pc}

TCM_Ov80FrontierMainTask:
    push {r3, r4, r5, lr}

    add r5, r0, #0              ; Original task argument for cleanup call.
    add r4, r1, #0              ; Frontier state pointer.

    add r0, r4, #0
    bl TCM_Ov80SelectFrontierRow
    add r2, r0, #0

    add r0, r4, #0
    mov r1, #0x65
    bl Ov80_FrontierMainTask_Consumer

    cmp r0, #1
    bne TCM_Ov80FrontierMainTask_Return

    add r0, r4, #0
    add r1, r5, #0
    bl Ov80_FrontierMainTask_Destroy

TCM_Ov80FrontierMainTask_Return:
    pop {r3, r4, r5, pc}

TCM_Ov80SelectFrontierRow:
    push {r4, r5, r6, r7, lr}
    sub sp, #0x24               ; 20-byte push + 36-byte frame = 8-byte aligned.

    add r4, r0, #0              ; Frontier state pointer.
    add r0, #0x2C
    ldrb r6, [r0, #0x0]         ; selector index already stored as selector - 1.

    cmp r6, #5
    bhi TCM_Ov80Frontier_Fail

    lsl r0, r6, #0x1
    ldr r1, =TCM_Ov80FrontierSelectorClasses
    ldrh r7, [r1, r0]           ; member id == Frontier Brain trainer class.

    mov r0, #TCM_VS_STYLE_SIZE
    str r0, [sp, #0x0]

    add r0, sp, #0x4
    mov r1, #NARC_a_1_5_5
    add r2, r7, #0
    mov r3, #TCM_VS_STYLE_OFFSET
    bl ReadFromNarcMemberByIdPair

    add r0, sp, #0x4
    ldrh r0, [r0, #0x0]
    cmp r0, #TCM_VS_STYLE_FRONTIER_BRAIN
    bne TCM_Ov80Frontier_Fail

    ldr r6, =TCM_Ov80FrontierScratchRow
    mov r0, #0
    str r0, [r6, #0x00]
    str r0, [r6, #0x04]
    str r0, [r6, #0x08]
    str r0, [r6, #0x0C]
    str r0, [r6, #0x10]

    mov r0, #TCM_TRAINER_NAME_SIZE
    str r0, [sp, #0x0]
    add r0, r6, #0
    mov r1, #NARC_a_1_5_5
    add r2, r7, #0
    mov r3, #TCM_TRAINER_NAME_OFFSET
    bl ReadFromNarcMemberByIdPair

    mov r0, #TCM_ASSET_GROUP2_QUARTET_SIZE
    str r0, [sp, #0x0]
    add r0, r6, #0
    add r0, #TCM_FRONTIER_SCRATCH_PORTRAIT0
    mov r1, #NARC_a_1_5_5
    add r2, r7, #0
    mov r3, #TCM_ASSET_GROUP2_QUARTET_OFFSET
    bl ReadFromNarcMemberByIdPair

    mov r0, #TCM_ASSET_GROUP1_PALETTE_IMAGE_SIZE
    str r0, [sp, #0x0]
    add r0, r6, #0
    add r0, #TCM_FRONTIER_SCRATCH_BG_PALETTE
    mov r1, #NARC_a_1_5_5
    add r2, r7, #0
    mov r3, #TCM_ASSET_GROUP1_QUARTET_OFFSET
    bl ReadFromNarcMemberByIdPair

    mov r0, #TCM_ASSET_GROUP1_RCSN_SIZE
    str r0, [sp, #0x0]
    add r0, r6, #0
    add r0, #TCM_FRONTIER_SCRATCH_BG_SCREEN
    mov r1, #NARC_a_1_5_5
    add r2, r7, #0
    mov r3, #TCM_ASSET_GROUP1_RCSN1_OFFSET
    bl ReadFromNarcMemberByIdPair

    ; The unused native-format scratch halfword remains zero.
    mov r0, #TCM_ASSET_GROUP3_QUARTET_SIZE
    str r0, [sp, #0x0]

    ldr r0, =TCM_Vs20SymbolScratch
    mov r1, #NARC_a_1_5_5
    add r2, r7, #0
    mov r3, #TCM_ASSET_GROUP3_QUARTET_OFFSET
    bl ReadFromNarcMemberByIdPair

    ldr r0, =TCM_Vs20SymbolScratch
    ldrh r1, [r0, #0x00]
    cmp r1, #0
    beq TCM_Ov80Frontier_Fail
    ldrh r1, [r0, #0x02]
    cmp r1, #0
    beq TCM_Ov80Frontier_Fail
    ldrh r1, [r0, #0x04]
    cmp r1, #0
    beq TCM_Ov80Frontier_Fail
    ldrh r1, [r0, #0x06]
    cmp r1, #0
    beq TCM_Ov80Frontier_Fail

    add r4, r6, #0
    b TCM_Ov80Frontier_ReturnRow

TCM_Ov80Frontier_ReturnRow:
    add r0, r4, #0
    add sp, #0x24
    pop {r4, r5, r6, r7, pc}

TCM_Ov80Frontier_Fail:
    bl GF_AssertFail
    b TCM_Ov80Frontier_Fail
    .pool

TCM_Ov80FrontierScratchRow:
    .fill TCM_FRONTIER_SCRATCH_SIZE, 0x00
    .align 2

TCM_Ov80FrontierSelectorClasses:
    .halfword 97, 100, 100, 102, 99, 101
    .align 4

TCM_Payload_End:

.if TCM_Payload_End - TCM_Payload_Start != TCM_PAYLOAD_SIZE
    .error "TCM payload size differs from its first-install guard"
.endif

.close

; =============================================================================
; Patch overlay 119
; =============================================================================

.open "arm9_overlays/ov119.bin", 0x0225F020

; -----------------------------------------------------------------------------
; ov119_0225F020 case 0 normal/default late ordinary-trainer sprite load
; -----------------------------------------------------------------------------

.org Ov119_NormalLateSpriteLoad_Hook
    bl TCM_Ov119LoadNormalLateSprite
TCM_Ov119NormalLateSpriteLoadPatch_End:
    .fill Ov119_NormalLateSpriteLoad_Resume - TCM_Ov119NormalLateSpriteLoadPatch_End, 0x00

; Landing guard: resumes at 0x0225F0A8, immediately after the native ov01_021F0614 resource-load call. The helper preserves r4/r7 and recreates the complete native stack-argument contract.

; -----------------------------------------------------------------------------
; ov119_0225F37C case 0 normal/default early ordinary-trainer sprite load
; -----------------------------------------------------------------------------

.if . != 0x0225F0A8
    .error "Native write span mismatch: Ov119_NormalLateSpriteLoad_Hook"
.endif
.org Ov119_NormalEarlySpriteLoad_Hook
    bl TCM_Ov119LoadNormalEarlySprite
TCM_Ov119NormalEarlySpriteLoadPatch_End:
    .fill Ov119_NormalEarlySpriteLoad_Resume - TCM_Ov119NormalEarlySpriteLoadPatch_End, 0x00

; Landing guard: resumes at 0x0225F402, immediately after the native ov01_021F0614 resource-load call. The helper preserves r4/r6 and recreates the complete native stack-argument contract.

; -----------------------------------------------------------------------------
; ov119_0225FA2C water-early ordinary-trainer sprite load
; -----------------------------------------------------------------------------

.if . != 0x0225F402
    .error "Native write span mismatch: Ov119_NormalEarlySpriteLoad_Hook"
.endif
.org Ov119_WaterEarlySpriteLoad_Hook
    bl TCM_Ov119LoadWaterEarlySprite
TCM_Ov119WaterEarlySpriteLoadPatch_End:
    .fill Ov119_WaterEarlySpriteLoad_Resume - TCM_Ov119WaterEarlySpriteLoadPatch_End, 0x00

; Landing guard: resumes at 0x0225FAC4, the native first instruction after ov01_021F0614. The helper recreates the original stack-argument contract and preserves r4 plus the caller's original stack pointer.

; -----------------------------------------------------------------------------
; ov119_0225F670 water-late ordinary-trainer sprite load
; -----------------------------------------------------------------------------

.if . != 0x0225FAC4
    .error "Native write span mismatch: Ov119_WaterEarlySpriteLoad_Hook"
.endif
.org Ov119_WaterLateSpriteLoad_Hook
    bl TCM_Ov119LoadWaterLateSprite
TCM_Ov119WaterLateSpriteLoadPatch_End:
    .fill Ov119_WaterLateSpriteLoad_Resume - TCM_Ov119WaterLateSpriteLoadPatch_End, 0x00

; Landing guard: resumes at 0x0225F704 (`add r0, r4, #0`), immediately after the native ov01_021F0614 resource-loader call. The helper preserves r4/r7, restores the original stack pointer, and recreates the full native loader contract including the water-late image/cell/animation instance shape.

; -----------------------------------------------------------------------------
; ov119_02260258 cave-early ordinary-trainer sprite load
; -----------------------------------------------------------------------------

.if . != 0x0225F704
    .error "Native write span mismatch: Ov119_WaterLateSpriteLoad_Hook"
.endif
.org Ov119_CaveEarlySpriteLoad_Hook
    bl TCM_Ov119LoadCaveEarlySprite
TCM_Ov119CaveEarlySpriteLoadPatch_End:
    .fill Ov119_CaveEarlySpriteLoad_Resume - TCM_Ov119CaveEarlySpriteLoadPatch_End, 0x00

; Landing guard: resumes at 0x022602E2 (`mov r7,#0x4F`). The helper leaves r4 intact, restores the native stack pointer, and recreates the resource loader contract for each of cave-early's three sprite instances.

; -----------------------------------------------------------------------------
; ov119_0225FF9C cave-late ordinary-trainer sprite load
; -----------------------------------------------------------------------------

.if . != 0x022602E2
    .error "Native write span mismatch: Ov119_CaveEarlySpriteLoad_Hook"
.endif
.org Ov119_CaveLateSpriteLoad_Hook
    bl TCM_Ov119LoadCaveLateSprite
TCM_Ov119CaveLateSpriteLoadPatch_End:
    .fill Ov119_CaveLateSpriteLoad_Resume - TCM_Ov119CaveLateSpriteLoadPatch_End, 0x00

; Landing guard: resumes at 0x0226001C (`mov r0,#0`). The helper preserves r4/r5, restores the native stack pointer, and recreates the cave-late single-sprite resource-loader contract.

.if . != 0x0226001C
    .error "Native write span mismatch: Ov119_CaveLateSpriteLoad_Hook"
.endif
.close

; =============================================================================
; Patch overlay 12
; =============================================================================

.open "arm9_overlays/ov012.bin", 0x022378C0

; -----------------------------------------------------------------------------
; CalcPrizeMoney
; -----------------------------------------------------------------------------

.org CalcPrizeMoney_TrainerRead_Hook
TCM_PrizeTrainerReadPatch_Start:
    bl TCM_PrizeReadTrainerAndCacheClass
    nop
    nop
TCM_PrizeTrainerReadPatch_End:
.if TCM_PrizeTrainerReadPatch_End > CalcPrizeMoney_TrainerRead_Resume
    .error "CalcPrizeMoney trainer-read hook exceeds its native span"
.endif
.fill CalcPrizeMoney_TrainerRead_Resume - TCM_PrizeTrainerReadPatch_End, 0x00

.if . != 0x0223FB56
    .error "Native write span mismatch: CalcPrizeMoney_TrainerRead_Hook"
.endif
.org CalcPrizeMoney_PrizeScan_Hook
TCM_PrizeScanPatch_Start:
    ldr r0, [sp, #0x38]          ; Full class cached in saved volatile r3 slot.
    bl TCM_GetTrainerClassPrizeCoefficient
    add r4, r0, #0              ; vanilla payout paths use r4 below.
    b CalcPrizeMoney_PrizeScan_Resume
TCM_PrizeScanPatch_End:
    .fill CalcPrizeMoney_PrizeScan_Resume - TCM_PrizeScanPatch_End, 0x00

.if . != 0x0223FBE2
    .error "Native write span mismatch: CalcPrizeMoney_PrizeScan_Hook"
.endif
.org CalcPrizeMoney_TagLoad_Hook
    add r0, r4, #0
    nop
    nop

.if . != 0x0223FBF6
    .error "Native write span mismatch: CalcPrizeMoney_TagLoad_Hook"
.endif
.org CalcPrizeMoney_DoubleLoad_Hook
    add r0, r4, #0
    nop
    nop

.if . != 0x0223FC10
    .error "Native write span mismatch: CalcPrizeMoney_DoubleLoad_Hook"
.endif
.org CalcPrizeMoney_SingleLoad_Hook
    add r0, r4, #0
    nop
    nop

.if . != 0x0223FC26
    .error "Native write span mismatch: CalcPrizeMoney_SingleLoad_Hook"
.endif
.close

; =============================================================================
; Patch overlay 115
; =============================================================================

.open "arm9_overlays/ov115.bin", 0x0225F020

; -----------------------------------------------------------------------------
; ov115_0225F220 20-byte VS state-machine prologue
; -----------------------------------------------------------------------------

.org Ov115_Vs20StateMachine_Hook
    ldr r3, [pc, #0]
    bx r3
    .word TCM_Ov115Vs20StateMachinePrologue | 1
    .fill Ov115_Vs20StateMachine_Resume - ., 0x00

; Boundary guard: ov115_0225F704 is the first 20-byte wrapper after the shared state machine. The trampoline resumes at 0x0225F22E, the first untouched instruction of the original state-machine dispatch.

; -----------------------------------------------------------------------------
; ov115_0225F220 case 0 first 20-byte portrait resource load
; -----------------------------------------------------------------------------

.if . != 0x0225F22E
    .error "Native write span mismatch: Ov115_Vs20StateMachine_Hook"
.endif
.org Ov115_Vs20PortraitLoad_Hook
    bl TCM_Ov115LoadVs20PortraitWide
TCM_Ov115Vs20PortraitLoadPatch_End:
    .fill Ov115_Vs20PortraitLoad_Resume - TCM_Ov115Vs20PortraitLoadPatch_End, 0x00

; Landing guard: resumes at 0x0225F330, the first mov r0, #1 after the original ov01_021F0614 portrait resource-loader call.

; -----------------------------------------------------------------------------
; ov115_0225F220 case 0 standard VS symbol resource load
; -----------------------------------------------------------------------------

.if . != 0x0225F330
    .error "Native write span mismatch: Ov115_Vs20PortraitLoad_Hook"
.endif
.org Ov115_Vs20SymbolLoad_Hook
    bl TCM_Ov115LoadVs20SymbolWide
TCM_Ov115Vs20SymbolLoadPatch_End:
    .fill Ov115_Vs20SymbolLoad_Resume - TCM_Ov115Vs20SymbolLoadPatch_End, 0x00

; Landing guard: resumes at 0x0225F356, the native mov r0, #0 after the original standard VS symbol ov01_021F0614 resource-loader call.

; -----------------------------------------------------------------------------
; ov115_0225F220 later 20-byte portrait asset0 setup paths
; -----------------------------------------------------------------------------

.if . != 0x0225F356
    .error "Native write span mismatch: Ov115_Vs20SymbolLoad_Hook"
.endif
.org Ov115_Vs20PortraitSetup1_Hook
    bl TCM_Ov115SetupVs20PortraitAsset0Wide_State1
TCM_Ov115Vs20PortraitSetup1Patch_End:
    .fill Ov115_Vs20PortraitSetup1_Resume - TCM_Ov115Vs20PortraitSetup1Patch_End, 0x00

; Landing guard: resumes at 0x0225F3B2, the native post-setup ov01_021F0B44 call that consumes the return value from Ov115_SetupOpponentPortrait.

; -----------------------------------------------------------------------------
; ov115_0225F220 case 4 20-byte background resource load
; -----------------------------------------------------------------------------

.if . != 0x0225F3B2
    .error "Native write span mismatch: Ov115_Vs20PortraitSetup1_Hook"
.endif
.org Ov115_Vs20BackgroundLoad_Hook
    bl TCM_Ov115LoadVs20BackgroundWide
TCM_Ov115Vs20BackgroundLoadPatch_End:
    .fill Ov115_Vs20BackgroundLoad_Resume - TCM_Ov115Vs20BackgroundLoadPatch_End, 0x00

; Landing guard: resumes at 0x0225F400, the native mov r0, #0xA3 after the original ov01_021F0454 background-loader call.

.if . != 0x0225F400
    .error "Native write span mismatch: Ov115_Vs20BackgroundLoad_Hook"
.endif
.org Ov115_Vs20PortraitSetup7_Hook
    bl TCM_Ov115SetupVs20PortraitAsset0Wide_State7
TCM_Ov115Vs20PortraitSetup7Patch_End:
    .fill Ov115_Vs20PortraitSetup7_Resume - TCM_Ov115Vs20PortraitSetup7Patch_End, 0x00

; Landing guard: resumes at 0x0225F572, the native mov r0, #0x0D after the second Ov115_SetupOpponentPortrait portrait asset0 setup call.

; -----------------------------------------------------------------------------
; ov115_0225F978 8-byte E4/Champion state-machine prologue
; -----------------------------------------------------------------------------

.if . != 0x0225F572
    .error "Native write span mismatch: Ov115_Vs20PortraitSetup7_Hook"
.endif
.org Ov115_Vs8StateMachine_Hook
    ldr r3, [pc, #0]
    bx r3
    .word TCM_Ov115Vs8StateMachinePrologue | 1
    .fill Ov115_Vs8StateMachine_Resume - ., 0x00

; Boundary guard: Ov115_SetupOpponentPortrait starts at 0x02260254. The trampoline resumes at 0x0225F986, the original cmp r0, #0xE dispatch guard.

; -----------------------------------------------------------------------------
; ov115_0225F978 case 0 E4/Champion portrait resource load
; -----------------------------------------------------------------------------

.if . != 0x0225F986
    .error "Native write span mismatch: Ov115_Vs8StateMachine_Hook"
.endif
.org Ov115_Vs8PortraitLoad_Hook
    ldr r3, [pc, #4]
    blx r3
    b Ov115_Vs8PortraitLoad_Resume
    .align 4
    .word TCM_Ov115LoadVs8PortraitQuartet | 1
TCM_Ov115Vs8PortraitLoadPatch_End:
    .fill Ov115_Vs8PortraitLoad_Resume - TCM_Ov115Vs8PortraitLoadPatch_End, 0x00

; Landing guard: VS8 portrait helper resumes at 0x0225FA70. The patch replaces that following E4/Champion shared-background load with the static hook below.

; -----------------------------------------------------------------------------
; ov115_0225F978 case 0 E4/Champion shared-background resource load
; -----------------------------------------------------------------------------

.if . != 0x0225FA70
    .error "Native write span mismatch: Ov115_Vs8PortraitLoad_Hook"
.endif
.org Ov115_Vs8BackgroundLoad_Hook
    ldr r3, [pc, #4]
    blx r3
    b Ov115_Vs8BackgroundLoad_Resume
    .align 4
    .word TCM_Ov115LoadVs8BackgroundQuartet | 1
TCM_Ov115Vs8BackgroundLoadPatch_End:
    .fill Ov115_Vs8BackgroundLoad_Resume - TCM_Ov115Vs8BackgroundLoadPatch_End, 0x00

; Landing guard: resumes at 0x0225FA98, the native mov r0, #1 that begins the next E4/Champion resource-load setup block.

; -----------------------------------------------------------------------------
; ov115_0225F978 case 0 E4/Champion standard VS symbol resource load
; -----------------------------------------------------------------------------

.if . != 0x0225FA98
    .error "Native write span mismatch: Ov115_Vs8BackgroundLoad_Hook"
.endif
.org Ov115_Vs8SymbolLoad_Hook
    ldr r3, [pc, #4]
    blx r3
    b Ov115_Vs8SymbolLoad_Resume
    .align 4
    .word TCM_Ov115LoadVs8SymbolWide | 1
TCM_Ov115Vs8SymbolLoadPatch_End:
    .fill Ov115_Vs8SymbolLoad_Resume - TCM_Ov115Vs8SymbolLoadPatch_End, 0x00

; Landing guard: resumes at 0x0225FAC0, the native state increment after the standard VS symbol ov01_021F0614 resource-loader call.

; -----------------------------------------------------------------------------
; Ov115_SetupOpponentPortrait opponent-only multi-bank palette blend
; -----------------------------------------------------------------------------

; Player-side calls retain the native single-bank helper. These two opponent calls receive the Style-2 portrait whose RECN can select more than one OBJ palette bank.
.if . != 0x0225FAC0
    .error "Native write span mismatch: Ov115_Vs8SymbolLoad_Hook"
.endif
.org Ov115_Vs8OpponentSilhouetteBlend_Hook
    bl TCM_Ov115BlendVs8OpponentPortraitWide

.if . != 0x0225FB78
    .error "Native write span mismatch: Ov115_Vs8OpponentSilhouetteBlend_Hook"
.endif
.org Ov115_Vs8OpponentRevealBlend_Hook
    bl TCM_Ov115BlendVs8OpponentPortraitWide

.if . != 0x0225FEBE
    .error "Native write span mismatch: Ov115_Vs8OpponentRevealBlend_Hook"
.endif
.close

; =============================================================================
; Patch overlay 117
; =============================================================================

.open "arm9_overlays/ov117.bin", 0x0225F020

; -----------------------------------------------------------------------------
; ov117_0225F020 case 0 generic Team Rocket symbol resource load
; -----------------------------------------------------------------------------

.org Ov117_GenericRocketSymbolLoad_Hook
    bl TCM_Ov117LoadGenericRocketSymbol
TCM_Ov117GenericRocketSymbolLoadPatch_End:
    .fill Ov117_GenericRocketSymbolLoad_Resume - TCM_Ov117GenericRocketSymbolLoadPatch_End, 0x00

; Landing guard: resumes at 0x0225F0A2, the original six-sprite animation creation loop. The helper recreates the replaced r7/r6/r5 loop setup.

; -----------------------------------------------------------------------------
; ov117_0225F524 Rocket admin VS state-machine prologue
; -----------------------------------------------------------------------------

.if . != 0x0225F0A2
    .error "Native write span mismatch: Ov117_GenericRocketSymbolLoad_Hook"
.endif
.org Ov117_RocketAdminStateMachine_Hook
    ldr r3, [pc, #0]
    bx r3
    .word TCM_Ov117RocketAdminStateMachinePrologue | 1
    .fill Ov117_RocketAdminStateMachine_Resume - ., 0x00

; Boundary guard: the Rocket admin static records begin at 0x0225FACC. The trampoline resumes at 0x0225F53A, the original state dispatch sequence.

; -----------------------------------------------------------------------------
; ov117_0225F524 case 0 Rocket admin portrait resource load
; -----------------------------------------------------------------------------

.if . != 0x0225F53A
    .error "Native write span mismatch: Ov117_RocketAdminStateMachine_Hook"
.endif
.org Ov117_RocketAdminPortraitLoad_Hook
    bl TCM_Ov117LoadRocketAdminPortraitWide
TCM_Ov117RocketAdminPortraitLoadPatch_End:
    .fill Ov117_RocketAdminPortraitLoad_Resume - TCM_Ov117RocketAdminPortraitLoadPatch_End, 0x00

; Landing guard: resumes at 0x0225F5A6, the native mov r0, #0 after the original byte-sized ov01_021F0614 portrait resource-loader call.

; -----------------------------------------------------------------------------
; ov117_0225F524 case 0 first Rocket admin shared background load
; -----------------------------------------------------------------------------

.if . != 0x0225F5A6
    .error "Native write span mismatch: Ov117_RocketAdminPortraitLoad_Hook"
.endif
.org Ov117_RocketAdminBg1Load_Hook
    mov r0, #0
    bl TCM_Ov117LoadRocketBg
TCM_Ov117RocketAdminBg1LoadPatch_End:
    .fill Ov117_RocketAdminBg1Load_Resume - TCM_Ov117RocketAdminBg1LoadPatch_End, 0x00

; Landing guard: resumes at 0x0225F658, the original palette load that follows the first ov01_021F0454 background call.

; -----------------------------------------------------------------------------
; ov117_0225F524 case 0 second Rocket admin shared background load
; -----------------------------------------------------------------------------

.if . != 0x0225F658
    .error "Native write span mismatch: Ov117_RocketAdminBg1Load_Hook"
.endif
.org Ov117_RocketAdminBg3Load_Hook
    mov r0, #1
    bl TCM_Ov117LoadRocketBg
TCM_Ov117RocketAdminBg3LoadPatch_End:
    .fill Ov117_RocketAdminBg3Load_Resume - TCM_Ov117RocketAdminBg3LoadPatch_End, 0x00

; Landing guard: resumes at 0x0225F6E0, the original display-control update that follows the second ov01_021F0454 background call.

; -----------------------------------------------------------------------------
; ov117_0225F524 case 6 later Rocket admin screen/tilemap load
; -----------------------------------------------------------------------------

.if . != 0x0225F6E0
    .error "Native write span mismatch: Ov117_RocketAdminBg3Load_Hook"
.endif
.org Ov117_RocketAdminLaterBgLoad_Hook
    bl TCM_Ov117LoadRocketLaterBg
TCM_Ov117RocketAdminLaterBgLoadPatch_End:
    .fill Ov117_RocketAdminLaterBgLoad_Resume - TCM_Ov117RocketAdminLaterBgLoadPatch_End, 0x00

; Landing guard: resumes at 0x0225F82A, the original SetBgPriority sequence after ov117_0225F420 returns.

.if . != 0x0225F82A
    .error "Native write span mismatch: Ov117_RocketAdminLaterBgLoad_Hook"
.endif
.close

; =============================================================================
; Patch overlay 118
; =============================================================================

.open "arm9_overlays/ov118.bin", 0x0225F020

; -----------------------------------------------------------------------------
; ov118_0225F020 case 0 Kimono Girl shutter asset load
; -----------------------------------------------------------------------------

.org Ov118_KimonoShutterLoad_Hook
    bl TCM_Ov118LoadKimonoShutters
TCM_Ov118KimonoShutterLoadPatch_End:
    .fill Ov118_KimonoShutterLoad_Resume - TCM_Ov118KimonoShutterLoadPatch_End, 0x00

; Landing guard: resumes at 0x0225F0EA, the first SetBgPriority setup after the original ov01_021F0500 shutter asset call. Overlay 118 ends at 0x0225F2BF.

.if . != 0x0225F0EA
    .error "Native write span mismatch: Ov118_KimonoShutterLoad_Hook"
.endif
.close

; =============================================================================
; Patch overlay 120
; =============================================================================

.open "arm9_overlays/ov120.bin", 0x0225F020

; -----------------------------------------------------------------------------
; ov120_0225F714 case 0 Trainer Red block asset load
; -----------------------------------------------------------------------------

.org Ov120_RedBlockLoad_Hook
    bl TCM_Ov120LoadRedBlocks
TCM_Ov120RedBlockLoadPatch_End:
    .fill Ov120_RedBlockLoad_Resume - TCM_Ov120RedBlockLoadPatch_End, 0x00

; Landing guard: resumes at 0x0225F7BE, the original GfGfx_EngineATogglePlanes setup immediately after the shared sprite-loader call. Boundary guard: ov120_0225F8B0 is the next function after Red's handler and literal pool.

.if . != 0x0225F7BE
    .error "Native write span mismatch: Ov120_RedBlockLoad_Hook"
.endif
.close

; =============================================================================
; Patch overlay 80
; =============================================================================

.open "arm9_overlays/ov080.bin", 0x02229EE0

; -----------------------------------------------------------------------------
; Native Frontier Brain presentation music: global Overlay-80 policy
; -----------------------------------------------------------------------------
; FrtCmd_071 loads this aligned word into r1 before its unchanged Sound_SetSceneAndPlayBGM(5, sequenceId, 1) call. The following word at 0x0222E058 is the untouched Thumb callback pointer for ov80_0222E05C.

.org Ov80_FrontierBrainPresentationMusic
    .word TCM_FRONTIER_BRAIN_PRESENTATION_MUSIC

; -----------------------------------------------------------------------------
; ov80 Frontier Brain native resource consumers: byte scratch reads -> u16
; -----------------------------------------------------------------------------
; The replacement instructions retain the native registers, stack frame, and immediate loader continuations. Each site is exactly one Thumb instruction wide, so no trailing bytes require hygiene fill.

.if . != 0x0222E058
    .error "Native write span mismatch: Ov80_FrontierBrainPresentationMusic"
.endif
.org Ov80_FrontierPortraitPalette_Load
    ldrh r0, [r7, #TCM_FRONTIER_SCRATCH_PORTRAIT0]

.if . != 0x0223A268
    .error "Native write span mismatch: Ov80_FrontierPortraitPalette_Load"
.endif
.org Ov80_FrontierPortraitImage_Load
    ldrh r3, [r7, #TCM_FRONTIER_SCRATCH_PORTRAIT1]

.if . != 0x0223A2A6
    .error "Native write span mismatch: Ov80_FrontierPortraitImage_Load"
.endif
.org Ov80_FrontierPortraitImageCall_Hook
    bl TCM_Ov80LoadFrontierPortraitCharMapped

.if . != 0x0223A2B0
    .error "Native write span mismatch: Ov80_FrontierPortraitImageCall_Hook"
.endif
.org Ov80_FrontierPortraitCell_Load
    ldrh r3, [r7, #TCM_FRONTIER_SCRATCH_PORTRAIT2]

.if . != 0x0223A2BA
    .error "Native write span mismatch: Ov80_FrontierPortraitCell_Load"
.endif
.org Ov80_FrontierPortraitCellCall_Hook
    bl TCM_Ov80LoadFrontierPortraitCellMapped

.if . != 0x0223A2C4
    .error "Native write span mismatch: Ov80_FrontierPortraitCellCall_Hook"
.endif
.org Ov80_FrontierPortraitAnim_Load
    ldrh r3, [r7, #TCM_FRONTIER_SCRATCH_PORTRAIT3]

.if . != 0x0223A2CE
    .error "Native write span mismatch: Ov80_FrontierPortraitAnim_Load"
.endif
.org Ov80_FrontierSymbolPaletteCall_Hook
    bl TCM_Ov80LoadFrontierSymbolPalette

.if . != 0x0223A316
    .error "Native write span mismatch: Ov80_FrontierSymbolPaletteCall_Hook"
.endif
.org Ov80_FrontierSymbolCharCall_Hook
    bl TCM_Ov80LoadFrontierSymbolCharMapped

.if . != 0x0223A33E
    .error "Native write span mismatch: Ov80_FrontierSymbolCharCall_Hook"
.endif
.org Ov80_FrontierSymbolCellCall_Hook
    bl TCM_Ov80LoadFrontierSymbolCellMapped

.if . != 0x0223A352
    .error "Native write span mismatch: Ov80_FrontierSymbolCellCall_Hook"
.endif
.org Ov80_FrontierSymbolAnimCall_Hook
    bl TCM_Ov80LoadFrontierSymbolAnim

.if . != 0x0223A366
    .error "Native write span mismatch: Ov80_FrontierSymbolAnimCall_Hook"
.endif
.org Ov80_FrontierBackgroundPalette_Load
    ldrh r2, [r4, #TCM_FRONTIER_SCRATCH_BG_PALETTE]

.if . != 0x0223A9AA
    .error "Native write span mismatch: Ov80_FrontierBackgroundPalette_Load"
.endif
.org Ov80_FrontierBackgroundImage_Load
    ldrh r1, [r4, #TCM_FRONTIER_SCRATCH_BG_IMAGE]

.if . != 0x0223A9C2
    .error "Native write span mismatch: Ov80_FrontierBackgroundImage_Load"
.endif
.org Ov80_FrontierBackgroundScreen_Load
    ldrh r1, [r4, #TCM_FRONTIER_SCRATCH_BG_SCREEN]

.if . != 0x0223A9DA
    .error "Native write span mismatch: Ov80_FrontierBackgroundScreen_Load"
.endif
.org Ov80_FrontierBackgroundPalette_Copy
    ldrh r1, [r4, #TCM_FRONTIER_SCRATCH_BG_PALETTE]

; -----------------------------------------------------------------------------
; ov80_0223A00C Frontier Brain row selection / row consumer call
; -----------------------------------------------------------------------------

.if . != 0x0223AA04
    .error "Native write span mismatch: Ov80_FrontierBackgroundPalette_Copy"
.endif
.org Ov80_FrontierRowCall_Hook
    bl TCM_Ov80LoadFrontierBrainRecipe
    b Ov80_FrontierRowCall_Resume
    .fill Ov80_FrontierRowCall_Resume - ., 0x00

; Landing guard: resumes at 0x0223A0A4, the original SysTask_CreateOnVWaitQueue setup immediately after the ov80_0223A938 row-consumer call.

; -----------------------------------------------------------------------------
; ov80_0223A144 Frontier Brain main task name/portrait row selection
; -----------------------------------------------------------------------------

.if . != 0x0223A0A4
    .error "Native write span mismatch: Ov80_FrontierRowCall_Hook"
.endif
.org Ov80_FrontierMainTask_Hook
    ldr r2, [pc, #0]
    bx r2
    .word TCM_Ov80FrontierMainTask | 1
    .fill Ov80_FrontierMainTask_Next - ., 0x00

; Boundary guard: ov80_0223A174 starts at 0x0223A174. The helper replaces the whole ov80_0223A144 wrapper and returns directly to its caller.

.if . != 0x0223A174
    .error "Native write span mismatch: Ov80_FrontierMainTask_Hook"
.endif
.close

; =============================================================================
; Patch overlay 001
; =============================================================================

.open "arm9_overlays/ov001.bin", 0x021E5900

; Replace only ov01_021EFB38..0x021EFB3F. The native tail remains at 0x021EFB46 and ov01_021EFB64 remains the next function boundary.
.org Ov01_EffectDispatch_Hook
    ldr r2, [pc, #0x0]
    bx r2
    .word TCM_Ov01EffectDispatchWrapper | 1

.if . != 0x021EFB40
    .error "Native write span mismatch: Ov01_EffectDispatch_Hook"
.endif
.close
.endif
