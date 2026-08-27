[Research](../../ResearchNotes.md) / [Pokemon Resear…](../PokemonResearch.md) / Icons Notes

Notes for [IconsLogic.md](IconsLogic.md).

covers party/box icon sheet (poke_icon.narc), separate from battle/box sprite otherpoke redirect (AltFormSpritesLogic.md)

pokemon_icon_idx.h / pokemon_icon_idx.c:
  GetMonIconNaixEx(species, isEgg, form)
  GetBattleMonIconNaixEx(species, isEgg, form)
  GetMonIconPaletteEx(species, form, isEgg)
  GetBattleMonIconPaletteEx(species, form, isEgg)

Pokemon_GetIconNaix -> Boxmon_GetIconNaix -> GetMonIconNaixEx (pokemon_icon_idx.c:11-29)

BoxMonGetForm (:85): nonzero form only for Unown (letter via GetBoxMonUnownLetter), Deoxys, Shellos, Gastrodon, Burmy, Wormadam, Giratina, Shaymin, Rotom. everything else forced to 0.

GetMonIconNaixEx:
  isEgg: Manaphy->502, else->501
  form = sub_02070438(species, form) (same clamp helper as sprite otherpoke redirect)
  form!=0: Deoxys+503-1, Unown+507-1, Burmy+534-1, Wormadam+536-1, Shellos+538-1, Gastrodon+539-1, Giratina+540-1, Shaymin+541-1, Rotom+542-1
  else: species+7 (first 7 slots = non-species icons, egg placeholders etc)

GetBattleMonIconNaixEx (:68): adds Castform(+547-1) and Cherrim(+550-1) on top, else falls to GetMonIconNaixEx
  Castform/Cherrim only get alt icon frames in the battle-only set, not the party/box set

GetMonIconPaletteEx:
  isEgg: Manaphy->species=495, else->494
  species>MAX_SPECIES -> 0
  form!=0: Deoxys 496+form-1, Unown 499+form-1, Burmy 527+form-1, Wormadam 529+form-1, Shellos 531+form-1, Gastrodon 532+form-1, Giratina 533+form-1, Shaymin 534+form-1, Rotom 535+form-1
  return sPokemonPalNoBySpeciesAndForm[species] (pokemon_icon_idx.c:103) - real lookup table, not a formula

DSPRE side:
  RomInfo.cs:2212 monIcons -> poketool/icongra/poke_icon.narc (HGSS)
  RomInfo.cs:2327 monIcons -> pl_poke_icon.narc (Platinum)
  RomInfo.cs:2406 monIcons -> a/0/2/0 (DP-generation game code, raw archive path)
  RomInfo.monIconPalTableAddress (DSUtils.cs ~115-127, ~1295-1303) = same table as sPokemonPalNoBySpeciesAndForm
  DSPRE reads poke_icon.narc as pre-split per-slot NCGR/NCER files by filename (DSUtils.cs:1230/1279/1319/1324), does not reimplement the NAIX index math

sPokemonPalNoBySpeciesAndForm (pokemon_icon_idx.c:103) = real array, fully decompiled, ~546 entries, one byte per icon slot

GetBattleMonIconPaletteEx (:683) mirrors GetBattleMonIconNaixEx
  Castform: sPokemonPalNoBySpeciesAndForm[540+form-1] if form!=0
  Cherrim: sPokemonPalNoBySpeciesAndForm[543+form-1] if form!=0
  else falls through to GetMonIconPaletteEx
