[Research](../../ResearchNotes.md) / [Field Research](../FieldResearch.md) / Script Numbers Notes

Notes for [ScriptNumbersLogic.md](ScriptNumbersLogic.md).

DP script manager is still asm, no named constants anywhere. every DP number below decoded from a cited instruction/literal.

resolving one number:
  HG LoadScriptsAndMessagesByMapId (src/script_manager.c)
    walk sScriptBankMapping, first range where scriptId >= scriptIdLo -> load that bank pair, return scriptId - scriptIdLo
    else scriptId >= 1 -> map's own archive, return scriptId - 1
    else -> scr_seq 140 + msg 184, index 0
  Pt ScriptContext_LoadAndOffsetID (src/script_manager.c), fallback scripts_unk_0402 + TEXT_BANK_DUMMY_0355 
  DP LoadScriptsAndMessagesByMapId asm (arm9/asm/unk_02038C78.s), tail, fallback archive 369 (=0x171) msg 317 (0x171-0x34)
  index -> offset: ptr += 4*idx then += relative word there
    HG ScriptRunByIndex / Pt ScriptContext_JumpToOffsetID / DP ScriptRunByIndex asm

script 0 = NO SCRIPT in practice
  loads a fixed archive at index 0, that script is a single End
  still locks/unlocks the player, nothing observable, map's own file never touched
  NOT "the first script in the file"

range table shape differs, numbers barely do:
  DP: hand-written 27x cmp/blo chain, no table, no constants
  Pt: X-macro SCRIPT_RANGE_TABLE (src/script_manager.c) expanded to else-if chain , named SCRIPT_ID_OFFSET_*
  HG: real runtime table struct ScriptBankMapping { u16 scriptIdLo; u16 scriptBank; u16 msgBank; } , 30 entries 
    only one of the three that can be reordered/extended without touching code

low bases, same in all three:
  2000 common / 2500 scenery (bookshelves=HG, BG_EVENTS=Pt) / 2800 berry (HG: apricorn) / 3000 trainer 1 / 5000 trainer 2 / 7000 item balls / 8000 hidden items

range count: DP 27, Pt 30, HG 30
  Pt added vs DP: 10400, 10450, 10490 (DP chain stops at 10300)
  HG dropped: 8950 VS Seeker, 8970 Poke Radar, 9400 Poffin
  HG added: 9850 Pokeathlon, 10350 Trainer House, 10440 frontier move tutor

same number, different meaning:
  2800 berry (DP/Pt) vs apricorn (HG)
  9000 PC 2F common (DP/Pt) vs comm reception (HG)
  9100 communication club (DP/Pt) vs Colosseum (HG)
  9200 PC B1F common (DP/Pt) vs wifi reception (HG)
  2500 bookshelves (HG) vs BG events (Pt), same 9 scripts 2500-2508 in all three

individual common scripts HG moved:
  PC 2018 (DP/Pt) vs 2010 (HG, std_script.h)
  bike rack 2030 (DP/Pt) vs 2020 (HG)

trainers:
  trainerID = script - base + 1, base 3000 first battler / 5000 second
  HG ScriptNumToTrainerNum / Pt Script_GetTrainerID / DP asm
  slot = script >= 5000; double-ness comes from trainer data not the number
  approach script = one past last trainer: DP 3850, Pt 3928, HG 3739

items:
  item balls 7000 + idx
  hidden items 8000 + idx, flag = idx + flagbase; flagbase 730 (DP/Pt) vs 800 (HG, constants/flags.h)
  trainer defeated flag base 0x550 all three

event dispatch:
  overworld: passes its own script number; type 9 object forces script 0 instead
    HG field_control.c / Pt overlay005/field_control.c / DP ov05_021D80E8.s
    no "nothing" number, the facing-object lookup returns a bool
  BG/sign: matched by position+facing, returns 0xFFFF for nothing; hidden item type needs flag unset; wall signs only answer facing north
    20-byte record, same field order all three, dir value 4 = any
  trigger/coord: once per step before map transitions, rect + var==value, 0xFFFF for nothing, 16-byte record
    HG asm/unk_0203DB6C.s / Pt unk_0203C954.c / DP asm/unk_02037024.s
  0xFFFF = the lookups' "nothing here", not a value to write into an event
  (overworld 0xFFFF is a different thing: alias entry)

level scripts, identical format all three:
  own member of scripts archive, own header field, read into 256-byte buffer on map load
  5-byte records: type byte + 4 payload; type 0 TERMINATES
  types 2/3/4: payload[0..1] = script number (HG GetMapLoadScriptId)
  type 1: payload = 4-byte relative offset to 6-byte rows (varA, varB, script), varA 0 terminates, first row with VarGet(varA)==VarGet(varB) wins
  type codes: 1 on frame table, 2 on transition, 3 on resume, 4 on load (named in Pt/HG, bare immediates in DP)
  type 1 runs as a task; 2/3/4 run synchronously in a throwaway context
  no script of that type -> 0xFFFF

HG-only guard: TryStartMapScriptByType early-outs on fieldSystem->unkAC != 0 ; DP/Pt have no counterpart

DSPRE side:
  CommonScriptId.Resolve (CommonScriptId.cs) - per game: DpBrackets (27, from pokediamond LoadScriptsAndMessagesByMapId), Pt 30, HGSS 30
    DP id 0 -> script 369 / message 317 not in the table (0 is "No script")
  TrainerScripts (ROMFiles/FieldPlayer.cs) - 3000/5000 bases, same arithmetic as the games
  script 0: "No script" dropdown entry, labels say No script, preview says script 0 runs nothing
  level script editor knows the 4 type codes + 0 terminator, shows type 1 rows with their two variables

hand edits (Pt decomp, same layout DP/HG per DSPRE EventFile):
  events file = 4 sections, u32 count + records, order bg, object, warp, coord (map_header_data.c)
  record sizes: bg 20, object 32, warp 12, coord 16 (map_header_data.h)
  script u16 LE at: bg +0x00, object +0x0A, coord +0x00, warp none
  object: localID 0, graphicsID 2, movementType 4, trainerType 6, hiddenFlag 8, script A, dir C, data[3] E, rangeX 14, rangeZ 16, x 18, z 1A, y fx32 1C
level script table: 5-byte records, 0 ends, fits initScripts[64] = 256 bytes (map_header_data.h)
  add on-transition script 12 to an empty table: 02 0C 00 00 00 00
  type 1 offset counts from the end of its own 5 bytes (script_manager.c)
    record inserted before it: no change; inserted between it and its frame table: offset += 5
