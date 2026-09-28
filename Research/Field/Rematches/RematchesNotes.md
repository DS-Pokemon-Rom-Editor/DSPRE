[Research](../../ResearchNotes.md) / [Field Research](../FieldResearch.md) / Rematches Notes

Notes for [RematchesLogic.md](RematchesLogic.md).

offsets: US HG, Pt rev 1, Diamond; file offset in decompressed ARM9/overlay, RAM beside

rematch row: 6 x u16 = base trainer, levels 1-5; 0 ends row, 0xFFFF skips level
  pick (HG TryGetRematchTrainerIdByBaseTrainerId overlay_26_022598C0.c; Pt VsSeeker_GetRematchTrainerID vs_seeker.c):
    first row with slot0 == trainer (none -> no rematch)
    first slot 1-5 not 0xFFFF and not beaten; 0 stops at previous; all beaten -> slot 5
    locked level -> step back to nearest earlier non-0xFFFF slot or slot 0, no re-check
  level flags DP/Pt 0x97F-0x983: Route 207, Celestic, Spear Pillar, Hall of Fame, Stark Mountain deepest (Pt system_flags.c, DP unk_0205EC84.c)
  level flags HG 0x97C-0x980: none, Radio Tower deck, Hall of Fame, Viridian Gym after Blue, none (sys_flags.c)
    HG slot 1 = copy of slot 0 only, slot 5 dead, selected 0xFFFF returned as trainer id

HG phone book tel/pmtel_book.dat: u32 count (75, effectively fixed) + 20 B records (LoadPhoneBookEntryI, phonebook_dat.c)
  0 id = position, 1 handler, 2 unused, 3 title, 4 trainer u16, 6 map u16, 8 gift u16, A local script u16, C greeting (0xFF none), D weekday (7 any), E time (0 morn 1 day 2 night 3 any), F random group, 10-13 sort ranks
  handler 0 generic 1 Mom 2 Elm 3 Oak 4 Kurt 5 bike shop 6 Kenji 7 Bill 8 day-care man 9 day-care lady 10 Buena 11 rival 12 gym leader 13 Baoba 14 Irwin
  title: class name; 200 none; 201-207 msg 271 line 38+; > 207 reads past the archive
  Joey (contact 10) @0xCC: 0a 00 00 02 08 00 22 00 2d 00 1d 00 00 01 01 01 4b 28 2f 00
  seeking set by: rematch-kind call script, gym leader (16 badges + day/time), Kenji handler; Kenji day only (PhoneBookTrainerGetRematchInfo, unk_020932A4.c)
  contacts 47 + 52 no rematch call, Irwin never sets it -> their rematch rows dead
  after win: trainer flag, clear seeking, gift; Cheri Berry -> random berry of first ten
  calls: 16 headers/contact (8 player-calls, 8 incoming), {cond, chance, script kind, script} (overlay_101_021F1D74.c, phone_scripts_generic.c)
    cond 0 never 1 random 2 post-Rocket 3 post-Rocket on day/time 4 bug contest days 5 during Rocket 6 random 7 post-Rocket not bug contest 8 gift not bug contest, 0xFF none
  random calls: group roll < 500 g0, < 800 g1, else g2; handlers 0/10/11/12/14; not current map, not recent (overlay_2_gear_phone.c)
  morning 4-9, day 10-19, night 20-3 (gf_rtc.c)
  tables: rematch ov26 0x20C / 0x02259ACC (63 rows, cmp r2 #0x3F at ov26 0x44)
          names ARM9 0x10847C / 0x0210847C (75 u16 archive numbers)
          greetings ov101 0x11EEC / 0x021F962C; call headers ov101 0x11F4C / 0x021F968C; call scripts ov101 0x1143C / 0x021F8B7C
          scripted calls ov2 0xE104 / 0x02253A84 (13 x 6 B)

Vs Seeker (Pt vs_seeker.c; DP asm ov05_021E1374.s):
  battery var +1/step with item in bag, max 100, use at 100 empties it
  scan box 7 left/right, 7 up / 6 down; skips disguised; unbeaten "!" untouched; beaten 50% "!!" + spin, partner too
  100-step timer then spinners go back to looking around
  GetRematchTrainerID -> same pick; same flags drive daily Pokemon Center trainers
  table overlay 5, 240 x 6 u16, identical D and Pt:
    Pt 0x280C8 / 0x021F8E48, cmp r2 #0xF0 at 0xB070, ptrs 0xB078 0xB0C0 0xB11C 0xB134
    D  0x1F43C / 0x021F691C, cmp r2 #0xF0 at 0xA3D0, ptrs 0xA3D8 0xA420 0xA47C 0xA494
    0xFF row index = not found -> 255 rows max
  retail: 160 rows no rematch, 72 skip level 1, none can return 0xFFFF
  DP battery/scan constants not checked

DSPRE side:
  Phone Book: PokegearPhoneBookViewModel + PokegearPhoneBook.cs, whole file, names via PokegearContactArchives.Find, titles archive 271, beta gated
    title 208+ "(undefined)" + warning (TitleReadsPastEnd); count != 75 warns; Kenji (16) "Day only (10:00-19:59)", greyed
  Pokegear Rematch: PokegearRematchTable over RematchTable (RematchTable.cs), found via overlay 26 pointers, fallback fixed offset, rows in place, warns on dead levels / 0xFFFF / duplicates
  Vs Seeker: VsSeekerRematchTable.cs + VsSeekerRematchViewModel, English DP/Pt, rows in place
    located via the 4 overlay 5 pointers + row count from the cmp (RematchTable.Resolve), rows validated; fixed offset only as validated fallback, else refuse
    Problems(): skip before end, skip in slot 5 with no end, duplicate encounter trainer; levels labelled Route 207 / Celestic / Spear Pillar / Hall of Fame / Stark Mountain
