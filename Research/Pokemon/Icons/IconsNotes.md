[Research](../../ResearchNotes.md) / [Pokemon Resear…](../PokemonResearch.md) / Icons Notes

Notes for [IconsLogic.md](IconsLogic.md).

covers party/box icon sheet (poke_icon.narc), separate from battle/box sprite otherpoke redirect (AltFormSpritesLogic.md)

pokemon_icon_idx.h / pokemon_icon_idx.c:
  GetMonIconNaixEx(species, isEgg, form)
  GetBattleMonIconNaixEx(species, isEgg, form)
  GetMonIconPaletteEx(species, form, isEgg)
  GetBattleMonIconPaletteEx(species, form, isEgg)

Pokemon_GetIconNaix -> Boxmon_GetIconNaix -> GetMonIconNaixEx (pokemon_icon_idx.c)

BoxMonGetForm : nonzero form only for Unown (letter via GetBoxMonUnownLetter), Deoxys, Shellos, Gastrodon, Burmy, Wormadam, Giratina, Shaymin, Rotom. everything else forced to 0.

GetMonIconNaixEx:
  isEgg: Manaphy->502, else->501
  form = sub_02070438(species, form) (same clamp helper as sprite otherpoke redirect)
  form!=0: Deoxys+503-1, Unown+507-1, Burmy+534-1, Wormadam+536-1, Shellos+538-1, Gastrodon+539-1, Giratina+540-1, Shaymin+541-1, Rotom+542-1
  else: species+7 (first 7 files = the shared palettes, cells and animations; eggs are files 501, 502)

GetBattleMonIconNaixEx : adds Castform(+547-1) and Cherrim(+550-1) on top, else falls to GetMonIconNaixEx
  Castform/Cherrim only get alt icon frames in the battle-only set, not the party/box set

GetMonIconPaletteEx:
  isEgg: Manaphy->species=495, else->494
  species>MAX_SPECIES -> 0
  form!=0: Deoxys 496+form-1, Unown 499+form-1, Burmy 527+form-1, Wormadam 529+form-1, Shellos 531+form-1, Gastrodon 532+form-1, Giratina 533+form-1, Shaymin 534+form-1, Rotom 535+form-1
  return sPokemonPalNoBySpeciesAndForm[species] (pokemon_icon_idx.c) - real lookup table, not a formula

DSPRE side:
  RomInfo.SetNarcDirs, monIcons: poketool/icongra/poke_icon.narc (DP), pl_poke_icon.narc (Pt), a/0/2/0 (HGSS)
  palette byte table = sPokemonPalNoBySpeciesAndForm, at RomInfo.monIconPalTableAddress
    DSUtils.GetMonIconPaletteId / SetMonIconPaletteId, TryResolveMonIconPalTable picks ARM9 vs moved-to overlay
  PokemonIconFiles (ROMFiles/PokemonIconFiles.cs) = GetMonIconNaixEx as data:
    SharedFiles 7 (species + 7), egg 501, Manaphy egg 502
    form runs: Deoxys 503, Unown 506 (B = 507), Burmy 534, Wormadam 536, Shellos 538, Gastrodon 539,
      Giratina 540 + Shaymin 541 + Rotom 542 from Pt, Castform 547 + Cherrim 550 HGSS only
    Describe(file) -> species/form/owning editor entry
  pictures: DSUtils.GetMonIconGraphicRaw / ValidateMonIconGraphic / SetMonIconGraphic, per unpacked file

sPokemonPalNoBySpeciesAndForm (pokemon_icon_idx.c) = real array, fully decompiled, 544 entries, one byte per icon slot

GetBattleMonIconPaletteEx  mirrors GetBattleMonIconNaixEx
  Castform: sPokemonPalNoBySpeciesAndForm[540+form-1] if form!=0
  Cherrim: sPokemonPalNoBySpeciesAndForm[543+form-1] if form!=0
  else falls through to GetMonIconPaletteEx
