[Research](../../../ResearchNotes.md) / [Pokemon Resear…](../../PokemonResearch.md) / [Sprites Resear…](../SpritesResearch.md) / [Alt Form Sprit…](AltFormSpritesResearch.md) / Alt Form Sprit…

Notes for [AltFormSpritesLogic.md](AltFormSpritesLogic.md).

covers battle/box sprite selection for alt-form species. icon selection is separate (Pokemon/Icons/IconsLogic.md)

GetMonSpriteCharAndPlttNarcIdsEx (src/pokemon.c:2186)
  form = sub_02070438(species, form) first
  switch(species): 13 species -> NARC_poketool_pokegra_otherpoke instead of NARC_poketool_pokegra_pokegra:
    Burmy, Wormadam, Shellos, Gastrodon, Cherrim, Arceus, Castform, Deoxys, Unown, Shaymin, Rotom, Giratina, Pichu
  + SPECIES_EGG, SPECIES_MANAPHY_EGG also -> otherpoke (egg placeholders)
  each has own charDataID/palDataID base + form*2 or form offset -> forms sit at consecutive indices in otherpoke.narc
  default case: pokegra.narc, charDataID = species*6 + whichFacing + (gender==FEMALE?0:1), palDataID = shiny + species*6+4
  SPINDA special case in default: isAnimated=TRUE, personality carried through (spot pattern)

sub_02070438 (src/pokemon.c:2280) = form clamp
  per-species <SPECIES>_FORM_MAX constant, form > MAX-1 -> reset to 0
  runs for same 13 species (minus the 2 egg entries)

DSPRE side:
  PokemonSpriteEditorViewModel.cs:205 FormSpriteData { Name, BackSpriteIndex, FrontSpriteIndex, NormalPaletteIndex, ShinyPaletteIndex, HgEngineSpeciesId }
  HgEngineSpeciesId >= 0 = hg-engine-native form (Mega/Gigantamax/regional), no vanilla otherpoke entry, jumps straight to that species id
  IsAlternateForms / VariantNames / SelectedVariantIndex (same file ~233-269) drive the picker
  ResolveHgEngineMigratedFormId checked before falling back to vanilla otherpoke read
  RomInfo.cs:2223 otherPokemonBattleSprites -> poketool/pokegra/otherpoke.narc (HGSS)
  RomInfo.cs:2280 otherPokemonBattleSprites -> poketool/pokegra/pl_otherpoke.narc (Platinum)

GetMonPicHeightBySpeciesGenderForm (pokemon.c:2499) = height-table equivalent of GetMonSpriteCharAndPlttNarcIdsEx
  same sub_02070438 clamp, same 13-species list, reads one byte via ReadWholeNarcMemberByIdPair
  13 species -> NARC_poketool_pokegra_height_o (otherpoke.narc's height counterpart), same fileId offsets as the sprite redirect
  default -> NARC_poketool_pokegra_height, fileId = species*4 + whichFacing + gender

GetMonPicHeightBySpeciesGenderForm_PBR (pokemon.c:2576) = Pokemon Battle Revolution equivalent
  uses NARC_pbr_dp_height_o / NARC_pbr_dp_height instead
  Shaymin/Rotom/Giratina: form!=0 -> NARC_poketool_pokegra_height_o (the DS table, not a pbr one), form==0 -> NARC_pbr_dp_height default formula
  Pichu spiky-ear case present but commented out (disabled, not removed)
