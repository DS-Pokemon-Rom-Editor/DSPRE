; ShinyChance_HG_SS.asm
; Changable shiny odds with every Pokemon still legal: each shiny is exactly what the
; unmodified game makes with an RNG state, so PID/IV/Nature/Slot/Level all correlate as in the retail.

.nds
.thumb

; Settings:
ShinyOdds equ 4096              ; total shiny odds you want.

; Common choices for ShinyOdds:
; 1/8192 = 1 in 8192.00 -> set ShinyOdds to 8192
; 2/8192 = 1 in 4096.00 -> set ShinyOdds to 4096
; 16/8192 = 1 in 512.00 -> set ShinyOdds to 512
; 32/8192 = 1 in 256.00 -> set ShinyOdds to 256
; 64/8192 = 1 in 128.00 -> set ShinyOdds to 128
; 128/8192 = 1 in 64.00 -> set ShinyOdds to 64
; 8192/8192 = 1 in 1.00 -> set ShinyOdds to 1           ; Always Shiny

; ------------------------------------------------------------------------------------
; DO NOT EDIT BELOW HERE UNLESS YOU KNOW WHAT YOURE DOING
; ------------------------------------------------------------------------------------

.if ShinyOdds < 8192
ExtraShinyOdds equ (8192 * ShinyOdds) / (8192 - ShinyOdds)
.else
ExtraShinyOdds equ 0
.endif

SEARCH_LIMIT equ 0x80000
MAX_BACK equ 96
MAX_PRE equ 8
PID_LOOP_LIMIT equ 256

ABILITY_SYNCHRONIZE equ 28
ABILITY_CUTE_CHARM equ 56
BASE_GENDER_RATIO equ 18
MON_RATIO_FEMALE equ 254

; EncounterGenState
ENCGEN_PLAYER_ID equ 0x00
ENCGEN_IS_EGG equ 0x0D
ENCGEN_ABILITY equ 0x0E

GetLCRNGSeed equ 0x0201FD2C
SetLCRNGSeed equ 0x0201FD38
PRandom equ 0x0201FD68
MTRandom equ 0x0201FDB8
Save_PlayerData_GetProfile equ 0x02028E9C
PlayerProfile_GetTrainerID equ 0x02028F84
Save_Daycare_MasudaCheck equ 0x02029344
CreateMon equ 0x0206DE38
GetMonData equ 0x0206E540
GetMonBaseStat equ 0x0206FBE8
CalcShininessByOtIdAndPersonality equ 0x02070068
_u32_div_f equ 0x020F2BA4
generateWildNonShinyAndAddToParty equ 0x02247A18
FieldSystem_GenerateRegularEncounter equ 0x02247B64
FieldSystem_GenerateSafariEncounter equ 0x02247DA0
FieldSystem_GenerateBugContestEncounter_Internal equ 0x02247ED8
FieldSystem_GenerateSingleWildPokemon equ 0x02247F30

; ShinyChance_Vars
V_DRYRUN equ 0
V_CAPTURED equ 1
V_FORCE equ 2
V_ISEGG equ 3
V_ABILITY equ 4
V_SYNCLEAD equ 5
V_CCLEAD equ 6
V_LEADNATURE equ 7
V_TSV equ 8
V_FUNC equ 12
V_ARGS equ 16
V_STATE equ 20
V_SPECIES equ 24
V_PLAYERID equ 28
V_LEAD equ 32
V_FOUND equ 36
V_SIMPID equ 40

INJECT_ADDR equ 0x023C8000

.ifdef PATCH
.open "arm9/arm9.bin", 0x02000000
.org 0x020541E0
    bl      ShinyChance_GiftHook
.org 0x0206C9D2
    bl      ShinyChance_EggHook
.org 0x02067780
    bl      ShinyChance_RoamerHook
.org 0x0205104C
    bl      ShinyChance_WrapStatic
.org 0x020510BC
    bl      ShinyChance_WrapStatic
.org 0x02051252
    bl      ShinyChance_WrapStatic
.close

.open "arm9_overlays/ov002.bin", 0x02245B80
.org 0x02247406
    bl      ShinyChance_WrapRegular
.org 0x0224743A
    bl      ShinyChance_WrapRegular
.org 0x022474B6
    bl      ShinyChance_WrapRegular
.org 0x022474D6
    bl      ShinyChance_WrapRegular
.org 0x0224750C
    bl      ShinyChance_WrapRegular
.org 0x02247544
    bl      ShinyChance_WrapRegular
.org 0x02247560
    bl      ShinyChance_WrapRegular
.org 0x02247456
    bl      ShinyChance_WrapSafari
.org 0x022474F6
    bl      ShinyChance_WrapSafari
.org 0x0224752C
    bl      ShinyChance_WrapSafari
.org 0x02247486
    bl      ShinyChance_WrapBugContest
.org 0x02247D96
    bl      ShinyChance_GenHook
.org 0x02247EC8
    bl      ShinyChance_GenHook
.org 0x02247F20
    bl      ShinyChance_GenHook
.org 0x02247F92
    bl      ShinyChance_GenHook
.close
.endif

.ifdef PREASSEMBLE
.create "temp.bin", 0x023C8000
.elseifdef PATCH
.open "unpacked/synthOverlay/0000", 0x023C8000
.endif

.org INJECT_ADDR
.ascii "ShinyChance_start"
.align 4

ShinyChance_WrapRegular:
    push    {r4, lr}
    ldr     r4, =FieldSystem_GenerateRegularEncounter | 1
    b       ShinyChance_Encounter
ShinyChance_WrapSafari:
    push    {r4, lr}
    ldr     r4, =FieldSystem_GenerateSafariEncounter | 1
    b       ShinyChance_Encounter
ShinyChance_WrapBugContest:
    push    {r4, lr}
    ldr     r4, =FieldSystem_GenerateBugContestEncounter_Internal | 1
    b       ShinyChance_Encounter
ShinyChance_WrapStatic:
    push    {r4, lr}
    ldr     r4, =FieldSystem_GenerateSingleWildPokemon | 1
ShinyChance_Encounter:
    push    {r5-r7}
    sub     sp, #28
    str     r0, [sp, #12]
    str     r1, [sp, #16]
    str     r2, [sp, #20]
    str     r3, [sp, #24]
    ldr     r0, [sp, #48]
    str     r0, [sp, #0]
    ldr     r0, [sp, #52]
    str     r0, [sp, #4]
    ldr     r0, [sp, #56]
    str     r0, [sp, #8]
    ldr     r0, =FieldSystem_GenerateSingleWildPokemon | 1
    cmp     r0, r4
    bne     @@roll
    ldr     r0, [sp, #24]
    cmp     r0, #0
    bne     @@call
@@roll:
    bl      ShinyChance_Roll
    cmp     r0, #0
    beq     @@call
    ldr     r0, =ShinyChance_Vars
    str     r4, [r0, #V_FUNC]
    mov     r1, sp
    str     r1, [r0, #V_ARGS]
    bl      ShinyChance_Search
@@call:
    ldr     r0, [sp, #12]
    ldr     r1, [sp, #16]
    ldr     r2, [sp, #20]
    ldr     r3, [sp, #24]
    blx     r4
    add     sp, #28
    pop     {r5-r7}
    pop     {r4, pc}

ShinyChance_GenHook:
    push    {r0, r1}
    ldr     r0, =ShinyChance_Vars
    ldrb    r1, [r0, #V_DRYRUN]
    cmp     r1, #0
    pop     {r0, r1}
    bne     @@test
    push    {r0}
    ldr     r0, =generateWildNonShinyAndAddToParty | 1
    mov     r12, r0
    pop     {r0}
    bx      r12
@@test:
    push    {r4, lr}
    ldr     r4, =ShinyChance_Vars
    str     r0, [r4, #V_SPECIES]
    strb    r3, [r4, #V_FORCE]
    ldr     r0, [sp, #8]
    ldr     r1, [r0, #ENCGEN_PLAYER_ID]
    str     r1, [r4, #V_PLAYERID]
    ldrb    r1, [r0, #ENCGEN_IS_EGG]
    strb    r1, [r4, #V_ISEGG]
    ldrb    r1, [r0, #ENCGEN_ABILITY]
    strb    r1, [r4, #V_ABILITY]
    ldr     r0, [sp, #12]
    str     r0, [r4, #V_LEAD]
    bl      GetLCRNGSeed
    str     r0, [r4, #V_STATE]
    mov     r0, #1
    strb    r0, [r4, #V_CAPTURED]
    pop     {r4, pc}

ShinyChance_DryRun:
    push    {r4, lr}
    sub     sp, #12
    bl      SetLCRNGSeed
    ldr     r4, =ShinyChance_Vars
    mov     r0, #1
    strb    r0, [r4, #V_DRYRUN]
    mov     r0, #0
    strb    r0, [r4, #V_CAPTURED]
    ldr     r3, [r4, #V_ARGS]
    ldr     r0, [r3, #0]
    str     r0, [sp, #0]
    ldr     r0, [r3, #4]
    str     r0, [sp, #4]
    ldr     r0, [r3, #8]
    str     r0, [sp, #8]
    ldr     r0, [r4, #V_FUNC]
    mov     r12, r0
    ldr     r0, [r3, #12]
    ldr     r1, [r3, #16]
    ldr     r2, [r3, #20]
    ldr     r3, [r3, #24]
    blx     r12
    mov     r0, #0
    strb    r0, [r4, #V_DRYRUN]
    ldrb    r0, [r4, #V_CAPTURED]
    add     sp, #12
    pop     {r4, pc}

ShinyChance_Search:
    push    {r4-r7, lr}
    bl      GetLCRNGSeed
    mov     r7, r0
    mov     r5, r0
    mov     r6, #16
@@learn:
    mov     r0, r5
    bl      ShinyChance_DryRun
    cmp     r0, #0
    bne     @@learned
    mov     r0, r5
    bl      ShinyChance_Next
    mov     r5, r0
    sub     r6, #1
    bne     @@learn
    b       @@restore
@@learned:
    bl      ShinyChance_Prepare
    mov     r4, r7
    ldr     r6, =SEARCH_LIMIT
@@scan:
    mov     r0, r4
    bl      ShinyChance_IsShinyPairAt
    cmp     r0, #0
    beq     @@nextState
    mov     r0, r4
    bl      ShinyChance_TryCandidate
    cmp     r0, #0
    bne     @@found
@@nextState:
    mov     r0, r4
    bl      ShinyChance_Next
    mov     r4, r0
    sub     r6, #1
    bne     @@scan
@@restore:
    mov     r0, r7
    bl      SetLCRNGSeed
    b       @@ret
@@found:
    ldr     r0, =ShinyChance_Vars
    ldr     r0, [r0, #V_FOUND]
    bl      SetLCRNGSeed
@@ret:
    pop     {r4-r7, pc}

.pool

ShinyChance_Prepare:
    push    {r4, lr}
    ldr     r4, =ShinyChance_Vars
    ldr     r0, [r4, #V_PLAYERID]
    bl      ShinyChance_TSV
    strh    r1, [r4, #V_TSV]
    mov     r0, #0
    strb    r0, [r4, #V_SYNCLEAD]
    strb    r0, [r4, #V_CCLEAD]
    ldrb    r0, [r4, #V_ISEGG]
    cmp     r0, #0
    bne     @@ret
    ldrb    r0, [r4, #V_ABILITY]
    cmp     r0, #ABILITY_CUTE_CHARM
    bne     @@sync
    mov     r0, #1
    strb    r0, [r4, #V_CCLEAD]
    b       @@ret
@@sync:
    cmp     r0, #ABILITY_SYNCHRONIZE
    bne     @@ret
    mov     r0, #1
    strb    r0, [r4, #V_SYNCLEAD]
    ldr     r0, [r4, #V_LEAD]
    mov     r1, #0
    mov     r2, #0
    bl      GetMonData
    mov     r1, #25
    blx     _u32_div_f
    strb    r1, [r4, #V_LEADNATURE]
@@ret:
    pop     {r4, pc}

ShinyChance_TryCandidate:
    push    {r4-r7, lr}
    mov     r4, r0
    mov     r6, #MAX_BACK
@@nextT:
    mov     r0, r4
    bl      ShinyChance_Prev
    mov     r4, r0
    mov     r1, #0
    bl      ShinyChance_SimGen
    cmp     r0, #0
    beq     @@tFailed
    mov     r5, r4
    mov     r7, #MAX_PRE + 1
@@tryS:
    mov     r0, r5
    bl      ShinyChance_DryRun
    cmp     r0, #0
    beq     @@sFailed
    ldr     r0, =ShinyChance_Vars
    ldr     r0, [r0, #V_STATE]
    mov     r1, #1
    bl      ShinyChance_SimGen
    cmp     r0, #0
    beq     @@sFailed
    ldr     r0, =ShinyChance_Vars
    str     r5, [r0, #V_FOUND]
    mov     r0, #1
    b       @@ret
@@sFailed:
    mov     r0, r5
    bl      ShinyChance_Prev
    mov     r5, r0
    sub     r7, #1
    bne     @@tryS
@@tFailed:
    sub     r6, #1
    bne     @@nextT
    mov     r0, #0
@@ret:
    pop     {r4-r7, pc}

ShinyChance_SimGen:
    push    {r4-r7, lr}
    sub     sp, #8
    mov     r4, r0
    ldr     r7, =ShinyChance_Vars
    mov     r5, #1
    ldrb    r0, [r7, #V_FORCE]
    cmp     r0, #0
    beq     @@cuteCharm
    mov     r5, #4
@@cuteCharm:
    cmp     r1, #0
    beq     @@attempt
    ldrb    r0, [r7, #V_CCLEAD]
    cmp     r0, #0
    beq     @@attempt
    ldr     r0, [r7, #V_SPECIES]
    mov     r1, #BASE_GENDER_RATIO
    bl      GetMonBaseStat
    cmp     r0, #0
    beq     @@attempt
    cmp     r0, #MON_RATIO_FEMALE
    bhs     @@attempt
    mov     r0, r4
    bl      ShinyChance_Next
    mov     r4, r0
    lsr     r0, r0, #16
    mov     r1, #3
    blx     _u32_div_f
    cmp     r1, #0
    bne     @@no
@@attempt:
    ldrb    r0, [r7, #V_SYNCLEAD]
    cmp     r0, #0
    beq     @@natureRoll
    mov     r0, r4
    bl      ShinyChance_Next
    mov     r4, r0
    lsr     r0, r0, #16
    mov     r1, #1
    tst     r0, r1
    bne     @@natureRoll
    ldrb    r6, [r7, #V_LEADNATURE]
    b       @@pid
@@natureRoll:
    mov     r0, r4
    bl      ShinyChance_Next
    mov     r4, r0
    lsr     r0, r0, #16
    mov     r1, #25
    blx     _u32_div_f
    mov     r6, r1
@@pid:
    ldr     r0, =PID_LOOP_LIMIT
    str     r0, [sp, #0]
@@pidLoop:
    mov     r0, r4
    bl      ShinyChance_Next
    mov     r4, r0
    lsr     r0, r0, #16
    str     r0, [r7, #V_SIMPID]
    mov     r0, r4
    bl      ShinyChance_Next
    mov     r4, r0
    lsr     r0, r0, #16
    lsl     r0, r0, #16
    ldr     r1, [r7, #V_SIMPID]
    orr     r0, r1
    str     r0, [r7, #V_SIMPID]
    mov     r1, #25
    blx     _u32_div_f
    cmp     r1, r6
    beq     @@ivs
    ldr     r0, [sp, #0]
    sub     r0, #1
    str     r0, [sp, #0]
    bne     @@pidLoop
    b       @@no
@@ivs:
    mov     r0, r4
    bl      ShinyChance_Next
    mov     r4, r0
    lsr     r0, r0, #16
    bl      ShinyChance_Has31
    str     r0, [sp, #4]
    mov     r0, r4
    bl      ShinyChance_Next
    mov     r4, r0
    lsr     r0, r0, #16
    bl      ShinyChance_Has31
    ldr     r1, [sp, #4]
    orr     r0, r1
    ldrb    r1, [r7, #V_FORCE]
    cmp     r1, #0
    beq     @@made
    cmp     r0, #0
    bne     @@made
    sub     r5, #1
    bne     @@attempt
@@made:
    ldr     r0, [r7, #V_SIMPID]
    bl      ShinyChance_IsShinyPid
    b       @@ret
@@no:
    mov     r0, #0
@@ret:
    add     sp, #8
    pop     {r4-r7, pc}

.pool

ShinyChance_Has31:
    mov     r1, #0x1F
    mov     r2, r0
    and     r2, r1
    cmp     r2, r1
    beq     @@yes
    lsr     r2, r0, #5
    and     r2, r1
    cmp     r2, r1
    beq     @@yes
    lsr     r2, r0, #10
    and     r2, r1
    cmp     r2, r1
    beq     @@yes
    mov     r0, #0
    bx      lr
@@yes:
    mov     r0, #1
    bx      lr

ShinyChance_IsShinyPid:
    lsr     r1, r0, #16
    lsl     r0, r0, #16
    lsr     r0, r0, #16
    eor     r0, r1
    ldr     r1, =ShinyChance_Vars
    ldrh    r1, [r1, #V_TSV]
    eor     r0, r1
    mov     r1, #0
    cmp     r0, #8
    bhs     @@ret
    mov     r1, #1
@@ret:
    mov     r0, r1
    bx      lr

ShinyChance_IsShinyPairAt:
    push    {r4, lr}
    bl      ShinyChance_Next
    mov     r4, r0
    bl      ShinyChance_Next
    lsr     r0, r0, #16
    lsl     r0, r0, #16
    lsr     r1, r4, #16
    orr     r0, r1
    bl      ShinyChance_IsShinyPid
    pop     {r4, pc}

ShinyChance_RoamerHook:
    push    {r4-r7, lr}
    sub     sp, #16
    mov     r4, r0
    mov     r5, r1
    mov     r6, r2
    mov     r7, r3
    bl      ShinyChance_Roll
    cmp     r0, #0
    beq     @@create
    ldr     r0, [sp, #16 + 20 + 24]
    bl      PlayerProfile_GetTrainerID
    bl      ShinyChance_TSV
    bl      ShinyChance_FindMethod1
@@create:
    ldr     r0, [sp, #36]
    str     r0, [sp, #0]
    ldr     r0, [sp, #40]
    str     r0, [sp, #4]
    ldr     r0, [sp, #44]
    str     r0, [sp, #8]
    ldr     r0, [sp, #48]
    str     r0, [sp, #12]
    mov     r0, r4
    mov     r1, r5
    mov     r2, r6
    mov     r3, r7
    bl      CreateMon
    add     sp, #16
    pop     {r4-r7, pc}

ShinyChance_GiftHook:
    mov     r6, r1
    mov     r5, r0
    push    {r0-r3, lr}
    bl      ShinyChance_Roll
    cmp     r0, #0
    beq     @@done
    mov     r0, r6
    bl      Save_PlayerData_GetProfile
    bl      PlayerProfile_GetTrainerID
    bl      ShinyChance_TSV
    bl      ShinyChance_FindMethod1
@@done:
    pop     {r0-r3, pc}

ShinyChance_EggHook:
    push    {r5, lr}
    bl      Save_Daycare_MasudaCheck
    cmp     r0, #0
    bne     @@ret
    mov     r0, r6
    mov     r1, r4
    bl      CalcShininessByOtIdAndPersonality
    cmp     r0, #0
    bne     @@no
    mov     r0, r4
    bl      PRandom
    mov     r5, r0
    mov     r0, r6
    mov     r1, r5
    bl      CalcShininessByOtIdAndPersonality
    cmp     r0, #0
    beq     @@no
    mov     r4, r5
@@no:
    mov     r0, #0
@@ret:
    pop     {r5, pc}

ShinyChance_Roll:
.if ExtraShinyOdds == 0
    mov     r0, #0
    bx      lr
.else
    push    {r3, lr}
    bl      MTRandom
    ldr     r1, =ExtraShinyOdds
    blx     _u32_div_f
    mov     r0, #0
    cmp     r1, #0
    bne     @@ret
    mov     r0, #1
@@ret:
    pop     {r3, pc}
.endif

ShinyChance_TSV:
    lsr     r1, r0, #16
    eor     r1, r0
    lsl     r1, r1, #16
    lsr     r1, r1, #16
    bx      lr

ShinyChance_Next:
    ldr     r1, =0x41C64E6D
    mul     r0, r1
    ldr     r1, =0x6073
    add     r0, r1
    bx      lr

ShinyChance_Prev:
    ldr     r1, =0xEEB9EB65
    mul     r0, r1
    ldr     r1, =0x0A3561A1
    add     r0, r1
    bx      lr

ShinyChance_FindMethod1:
    push    {r4-r7, lr}
    mov     r7, r1
    bl      GetLCRNGSeed
    mov     r4, r0
    ldr     r6, =SEARCH_LIMIT
@@loop:
    mov     r0, r4
    bl      ShinyChance_Next
    mov     r5, r0
    bl      ShinyChance_Next 
    lsr     r0, r0, #16
    lsr     r1, r5, #16
    eor     r0, r1
    eor     r0, r7
    cmp     r0, #8
    blo     @@found
    mov     r4, r5
    sub     r6, #1
    bne     @@loop
    b       @@ret
@@found:
    mov     r0, r4
    bl      SetLCRNGSeed
@@ret:
    pop     {r4-r7, pc}

.pool

.align 4
ShinyChance_Vars:
    .fill 44, 0

end:
.ascii "ShinyChance_end"

.close
