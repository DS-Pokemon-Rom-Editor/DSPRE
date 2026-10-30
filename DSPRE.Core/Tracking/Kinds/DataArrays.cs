using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Tracking.Kinds
{
    /// <summary>The data archives that are plain arrays, with each member's layout for the open game.</summary>
    internal static class DataArrays
    {
        private static readonly string[] HeightSlots = { "female back", "male back", "female front", "male front" };
        private static readonly string[] Times = { "morning", "day", "night" };
        private static readonly string[] DpptTimes = { "morning", "day", "night", "other", "National Dex" };
        private static readonly string[] SortLists = { "national order", "regional order", "alphabetical", "heaviest", "lightest", "tallest", "shortest" };

        /// <summary>A species' battle sprite record: per side its cry delay, animation, start delay and ten frame steps, then the offsets.</summary>
        private static ArrayField[] SpriteDataFields()
        {
            List<ArrayField> fields = new List<ArrayField>();
            int at = 0;
            foreach (string side in new[] { "front", "back" })
            {
                fields.Add(new ArrayField(side + "_cry_delay", at, 1));
                fields.Add(new ArrayField(side + "_animation", at + 1, 1));
                fields.Add(new ArrayField(side + "_start_delay", at + 2, 1));
                for (int f = 0; f < SpeciesSpriteData.FrameCount; f++)
                {
                    string step = side + "_step_" + (f + 1).ToString(CultureInfo.InvariantCulture);
                    int o = at + 3 + f * 4;
                    fields.Add(new ArrayField(step + "_frame", o, 1, Signed: true));
                    fields.Add(new ArrayField(step + "_ticks", o + 1, 1));
                    fields.Add(new ArrayField(step + "_x", o + 2, 1, Signed: true));
                    fields.Add(new ArrayField(step + "_y", o + 3, 1, Signed: true));
                }
                at += 3 + SpeciesSpriteData.FrameCount * 4;
            }
            fields.Add(new ArrayField("front_y", at, 1, Signed: true));
            fields.Add(new ArrayField("shadow_x", at + 1, 1, Signed: true));
            fields.Add(new ArrayField("shadow_size", at + 2, 1));
            return fields.ToArray();
        }

        // FollowerTalk.Slot's fields; bits it doesn't name are kept as unused_ fields so every byte round-trips.
        private static readonly ArrayField[] FollowerSlotFields =
        {
            new ArrayField("hp", 0, 1), new ArrayField("mood", 1, 1, Bits: 4), new ArrayField("friendship", 1, 1, Shift: 4, Bits: 4),
            new ArrayField("nature", 2, 1, Bits: 3), new ArrayField("gender", 2, 1, Shift: 3, Bits: 2), new ArrayField("status", 2, 1, Shift: 5, Bits: 3),
            new ArrayField("held_item", 3, 1, Bits: 5), new ArrayField("unused_3", 3, 1, Shift: 5, Bits: 3),
            new ArrayField("type", 4, 1, Bits: 5), new ArrayField("pokeathlon", 4, 1, Shift: 5, Bits: 3),
            new ArrayField("encounter_tile", 5, 1), new ArrayField("species_group", 6, 1), new ArrayField("leaf", 7, 1),
            new ArrayField("weather", 8, 2, Bits: 3), new ArrayField("unused_8", 8, 2, Shift: 3, Bits: 10), new ArrayField("facing", 8, 2, Shift: 13, Bits: 3),
            new ArrayField("people", 10, 2, Bits: 3), new ArrayField("time", 10, 2, Shift: 3, Bits: 3), new ArrayField("reaction", 10, 2, Shift: 6, Bits: 10),
            new ArrayField("map_plus_1", 12, 2), new ArrayField("tile", 14, 2),
            new ArrayField("unused_16", 16, 1, Bits: 1), new ArrayField("level", 16, 1, Shift: 1, Bits: 2), new ArrayField("field_move", 16, 1, Shift: 3, Bits: 2),
            new ArrayField("hidden_items", 16, 1, Shift: 5, Bits: 3), new ArrayField("chance", 17, 1), new ArrayField("flag", 18, 2),
        };

        private static ArrayField[] FollowerReactionFields()
        {
            List<ArrayField> fields = new List<ArrayField>();
            for (int i = 0; i < FollowerTalk.StepCount; i++)
            {
                string step = "step_" + (i + 1).ToString(CultureInfo.InvariantCulture);
                int at = i * FollowerTalk.StepSize;
                fields.AddRange(new[]
                {
                    new ArrayField(step + "_movement", at, 2), new ArrayField(step + "_line", at + 2, 2), new ArrayField(step + "_sound", at + 4, 2),
                    new ArrayField(step + "_bubble", at + 6, 1), new ArrayField(step + "_wait", at + 7, 1),
                });
            }
            fields.AddRange(new[]
            {
                new ArrayField("question", 40, 4), new ArrayField("yes_reaction", 44, 2), new ArrayField("no_reaction", 46, 2),
                new ArrayField("friendship_change", 48, 1, Signed: true), new ArrayField("mood_change", 49, 1, Signed: true),
                new ArrayField("accessory", 50, 1), new ArrayField("leaf", 51, 1),
            });
            return fields.ToArray();
        }

        private static ArrayField[] FollowerMoveFields()
        {
            List<ArrayField> fields = new List<ArrayField>();
            for (int i = 0; i < FollowerTalk.FrameCount; i++)
            {
                string frame = "frame_" + (i + 1).ToString(CultureInfo.InvariantCulture);
                int at = i * FollowerTalk.FrameSize;
                fields.AddRange(new[]
                {
                    new ArrayField(frame + "_facing", at, 1), new ArrayField(frame + "_length", at + 1, 1), new ArrayField(frame + "_x", at + 2, 1, Signed: true),
                    new ArrayField(frame + "_hop", at + 3, 1, Signed: true), new ArrayField(frame + "_z", at + 4, 1, Signed: true),
                    new ArrayField(frame + "_sound", at + 5, 1), new ArrayField(frame + "_unused", at + 6, 2),
                });
            }
            return fields.ToArray();
        }

        private static byte[] FirstRows(byte[] table, byte[] old)
        {
            byte[] offsets = (byte[])old.Clone();
            HashSet<int> seen = new HashSet<int>();
            for (int at = 0; at + 4 <= table.Length; at += 4)
            {
                int trainer = BitConverter.ToUInt16(table, at);
                if (!seen.Add(trainer)) continue;
                if (trainer * 2 + 2 > offsets.Length) Array.Resize(ref offsets, trainer * 2 + 2);
                BitConverter.GetBytes((ushort)at).CopyTo(offsets, trainer * 2);
            }
            return offsets;
        }

        public static IEnumerable<ChangeKind> ForOpenGame()
        {
            bool hgss = GameFamily == GameFamilies.HGSS;
            Func<TrackingNames, Func<int, string>> species = n => n.Species;

            if (GameDirs.ContainsKey(DirNames.PokedexData))
            {
                // Platinum and HGSS keep a second copy the game reads while Altered Forme Giratina is the one registered.
                (DirNames, string)? altered = GameDirs.ContainsKey(DirNames.PokedexDataAltered) ? (DirNames.PokedexDataAltered, "altered_giratina_copy") : null;
                yield return new ArrayKind("pokedex-data", "pokemon/pokedex-data", DirNames.PokedexData, "Pokédex data", m => m switch
                {
                    0 => new ArrayShape("height", 4),
                    1 => new ArrayShape("weight", 4),
                    2 => new ArrayShape("body shape", 1, Values: _ => i => i >= 0 && i < PokedexLists.BodyShapeNames.Length ? PokedexLists.BodyShapeNames[i] : null),
                    3 => new ArrayShape("female trainer scale", 2),
                    4 => new ArrayShape("female Pokémon scale", 2),
                    5 => new ArrayShape("male trainer scale", 2),
                    6 => new ArrayShape("male Pokémon scale", 2),
                    7 => new ArrayShape("female trainer y offset", 2, Signed: true),
                    8 => new ArrayShape("female Pokémon y offset", 2, Signed: true),
                    9 => new ArrayShape("male trainer y offset", 2, Signed: true),
                    10 => new ArrayShape("male Pokémon y offset", 2, Signed: true),
                    _ => new ArrayShape(m - PokedexLists.National < SortLists.Length ? SortLists[m - PokedexLists.National] : "sort list", 2, Values: species),
                }, twin: altered);
            }

            if (GameDirs.ContainsKey(DirNames.PokedexAreas))
            {
                int header = hgss ? 2 : 4, lists = hgss ? PokedexAreaData.ListCount : 10;
                int speciesCount = -1;
                int SpeciesCount()
                {
                    if (speciesCount < 0)
                    {
                        DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.PokedexAreas });
                        speciesCount = Math.Max(1, (NarcAPI.Narc.MemberCount(GameDirs[DirNames.PokedexAreas].unpackedDir) - header) / lists);
                    }
                    return speciesCount;
                }
                List<(int Id, string Name)>[] areas = null;
                string Area(int group, int id)
                {
                    if (areas == null) areas = PokedexAreaData.TryLoad(out PokedexAreaData data, out _) ? data.Areas() : new[] { new List<(int, string)>(), new List<(int, string)>() };
                    return areas[group].FirstOrDefault(a => a.Id == id).Name;
                }
                yield return new ArrayKind("pokedex-areas", "pokemon/pokedex-areas", DirNames.PokedexAreas, "Pokédex areas", m =>
                {
                    if (m < header) return new ArrayShape("header", 4);
                    int list = (m - header) / SpeciesCount(), mon = (m - header) % SpeciesCount();
                    int group = hgss ? (list < 6 ? list / 3 : list - 6) : list / 5;
                    string time = hgss ? (list < 6 ? Times[list % 3] : "other") : DpptTimes[list % 5];
                    return new ArrayShape((group == 0 ? "special areas, " : "routes and towns, ") + time, 4, End: 0, Values: _ => id => Area(group, id), Species: mon);
                });
            }

            if (GameDirs.ContainsKey(DirNames.RegionalDexNumbers))
                yield return new ArrayKind("regional-dex-numbers", "pokemon/regional-dex-numbers", DirNames.RegionalDexNumbers, "Regional dex numbers",
                    _ => new ArrayShape("by species", 2));
            if (GameDirs.ContainsKey(DirNames.RegionalDexSpecies))
                yield return new ArrayKind("regional-dex-species", "pokemon/regional-dex-species", DirNames.RegionalDexSpecies, "Regional dex species",
                    _ => new ArrayShape("by regional number", 2, Values: species));
            if (GameDirs.ContainsKey(DirNames.PokedexSearchSteps))
                yield return new ArrayKind("pokedex-search-steps", "pokemon/pokedex-search-steps", DirNames.PokedexSearchSteps, "Pokédex search steps",
                    _ => new ArrayShape(null, 2));

            if (GameDirs.ContainsKey(DirNames.GrowthTable))
                yield return new ArrayKind("growth", "pokemon/growth-curves", DirNames.GrowthTable, "Growth curve",
                    m => new ArrayShape(m < GrowthTable.CurveNames.Length ? GrowthTable.CurveNames[m] : null, 4));

            if (GameDirs.ContainsKey(DirNames.PokeHeight))
                yield return new ArrayKind("battle-height", "pokemon/battle-heights", DirNames.PokeHeight, "Battle height",
                    m => new ArrayShape(HeightSlots[m % 4], 1, Signed: true, Species: m / 4));
            if (GameDirs.ContainsKey(DirNames.PokeHeightForms))
                yield return new ArrayKind("battle-height-form", "pokemon/battle-heights-forms", DirNames.PokeHeightForms, "Form battle height",
                    m => new ArrayShape("form " + (m / 4).ToString(CultureInfo.InvariantCulture) + " " + HeightSlots[m % 4], 1, Signed: true));
            if (GameDirs.ContainsKey(DirNames.PokemonSpriteOffsets))
                yield return new ArrayKind("battle-sprite", "pokemon/battle-sprite-data", DirNames.PokemonSpriteOffsets, "Battle sprite data",
                    _ => new ArrayShape(null, SpeciesSpriteData.Size, Fields: SpriteDataFields()));
            foreach ((DirNames dir, string name, string title) in new[]
                     {
                         (DirNames.PokeYofs, "battle-front-y", "Battle front Y"),
                         (DirNames.PokeShadowOfx, "battle-shadow-x", "Battle shadow X"),
                         (DirNames.PokeShadow, "battle-shadow-size", "Battle shadow size"),
                         (DirNames.PokeAnim, "battle-animation", "Battle animation"),
                         (DirNames.PokeAnimDefs, "battle-animation-script", "Battle animation script"),
                     })
                if (GameDirs.ContainsKey(dir))
                    yield return new ArrayKind(name, "pokemon/" + name, dir, title, _ => new ArrayShape(null, 1, Signed: dir != DirNames.PokeAnim && dir != DirNames.PokeAnimDefs));

            Func<TrackingNames, Func<int, string>> items = n => n.Item, moves = n => n.Move;

            if (GameDirs.ContainsKey(DirNames.TrainerCapsules))
            {
                List<ArrayField> seals = new List<ArrayField>();
                for (int s = 0; s < BallCapsule.Slots; s++)
                {
                    string k = (s + 1).ToString(CultureInfo.InvariantCulture);
                    seals.Add(new ArrayField("seal_" + k, s * 3, 1));
                    seals.Add(new ArrayField("x_" + k, s * 3 + 1, 1));
                    seals.Add(new ArrayField("y_" + k, s * 3 + 2, 1));
                }
                yield return new ArrayKind("trainer-capsules", "trainers/capsules", DirNames.TrainerCapsules, "Trainer capsules",
                    _ => new ArrayShape(null, BallCapsule.Bytes, Fields: seals, Key: (_, i) => (i + 1).ToString(CultureInfo.InvariantCulture)));
            }

            if (GameDirs.ContainsKey(DirNames.BattleTowerPokemon))
                yield return new ArrayKind("battle-tower-pokemon", "battle-tower/pokemon", DirNames.BattleTowerPokemon, "Battle Tower Pokémon",
                    _ => new ArrayShape(null, BattleTowerPokemonSetFile.EntrySize, Fields: new[]
                    {
                        new ArrayField("species", 0, 2, Values: species),
                        new ArrayField("move_1", 2, 2, Values: moves), new ArrayField("move_2", 4, 2, Values: moves),
                        new ArrayField("move_3", 6, 2, Values: moves), new ArrayField("move_4", 8, 2, Values: moves),
                        new ArrayField("max_evs", 10, 1, Flags: _ => new[] { "hp", "attack", "defense", "speed", "sp_attack", "sp_defense" }), new ArrayField("nature", 11, 1),
                        new ArrayField("item", 12, 2, Values: items), new ArrayField("form", 14, 2),
                    }));
            if (GameDirs.ContainsKey(DirNames.BattleTowerTrainers))
                yield return new ArrayKind("battle-tower-trainer", "battle-tower/trainers", DirNames.BattleTowerTrainers, "Battle Tower trainer",
                    _ => new ArrayShape(null, 2, Key: (_, i) => i == 0 ? "class" : i == 1 ? "set_count" : "set_" + (i - 1).ToString(CultureInfo.InvariantCulture)));

            if (GameDirs.ContainsKey(DirNames.TrainerTextTable))
                yield return new ArrayKind("trainer-messages", "trainers/messages", DirNames.TrainerTextTable, "Trainer messages",
                    _ => new ArrayShape(null, 4, Fields: new[] { new ArrayField("trainer", 0, 2, Values: n => n.Trainer), new ArrayField("trigger", 2, 2) }, Insertable: true),
                    GameDirs.ContainsKey(DirNames.TrainerTextOffset) ? DirNames.TrainerTextOffset : null, FirstRows);

            if (GameDirs.ContainsKey(DirNames.EncounterExtended))
            {
                HashSet<int> speciesSlots = new HashSet<int> { 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                yield return new ArrayKind("special-encounters", "encounters/special", DirNames.EncounterExtended, "Special encounters",
                    m => speciesSlots.Contains(m)
                        ? new ArrayShape(m == 8 ? "Trophy Garden" : m >= 9 ? "Great Marsh" : "honey trees", 4,
                            Fields: new[] { new ArrayField("species", 0, 2, Values: species), new ArrayField("padding", 2, 2) })
                        : new ArrayShape(null, 2));
            }

            if (GameDirs.ContainsKey(DirNames.BuildingConfigFiles))
                yield return new ArrayKind("building-texture-set", "maps/building-texture-sets", DirNames.BuildingConfigFiles, "Building texture set",
                    _ => new ArrayShape(null, 2, Key: (_, i) => i == 0 ? "count" : "model_" + i.ToString(CultureInfo.InvariantCulture)));

            if (GameDirs.ContainsKey(DirNames.TornWorldAttributes))
                yield return new ArrayKind("distortion-attributes", "maps/distortion-world-attributes", DirNames.TornWorldAttributes, "Distortion World attributes",
                    _ => new ArrayShape(null, 1));

            if (hgss)
            {
                if (GameDirs.ContainsKey(DirNames.FollowerTalkTriggers))
                    yield return new ArrayKind("follower-slots", "followers/slots", DirNames.FollowerTalkTriggers, "Follower talk slots",
                        _ => new ArrayShape(null, FollowerTalk.SlotSize, Fields: FollowerSlotFields));
                if (GameDirs.ContainsKey(DirNames.FollowerTalkReactions))
                    yield return new ArrayKind("follower-reaction", "followers/reactions", DirNames.FollowerTalkReactions, "Follower reaction",
                        _ => new ArrayShape(null, FollowerTalk.ReactionSize, Fields: FollowerReactionFields()));
                if (GameDirs.ContainsKey(DirNames.FollowerTalkMoves))
                    yield return new ArrayKind("follower-movement", "followers/movements", DirNames.FollowerTalkMoves, "Follower movement",
                        _ => new ArrayShape(null, FollowerTalk.MoveSize, Fields: FollowerMoveFields()));
                if (GameDirs.ContainsKey(DirNames.FollowerSpeciesGroups))
                    yield return new ArrayKind("follower-groups", "followers/species-groups", DirNames.FollowerSpeciesGroups, "Follower species groups",
                        _ => new ArrayShape("by species", 1));
                if (GameDirs.ContainsKey(DirNames.FollowerParams))
                    yield return new ArrayKind("follower-model", "followers/models", DirNames.FollowerParams, "Follower model",
                        _ => new ArrayShape(null, 1, Key: (_, i) => i == 1 ? "too_tall" : i == 2 ? "motion" : "byte_" + i.ToString(CultureInfo.InvariantCulture)));
                if (GameDirs.ContainsKey(DirNames.TrainerClassMetadata))
                    yield return new ArrayKind("trainer-class-metadata", "trainers/class-metadata", DirNames.TrainerClassMetadata, "Trainer class metadata",
                        _ => new ArrayShape(null, 1));
            }

            foreach ((DirNames dir, string name, string title, bool animation) in new[]
                     {
                         (DirNames.WazaSeq, "move-sequence", "Move battle sequence", false),
                         (DirNames.SubSeq, "battle-subsequence", "Battle subsequence", false),
                         (DirNames.BeSeq, "effect-sequence", "Move effect sequence", false),
                         (DirNames.WazaEffectScripts, "move-animation", "Move animation", true),
                         (DirNames.WazaEffectSub, "move-animation-sub", "Move animation subroutine", true),
                     })
                if (GameDirs.ContainsKey(dir)) yield return new BattleScriptKind(name, "battle/" + name, dir, title, animation);

            if (hgss && GameDirs.ContainsKey(DirNames.EggMoves))
                yield return new ArrayKind("egg-moves", "pokemon/egg-moves", DirNames.EggMoves, "Egg moves",
                    // Every entry is a row, the end marker too, so an inserted move is one row hunk.
                    _ => new ArrayShape(null, 2, Insertable: true,
                        Values: n => v => v == 0xFFFF ? "end" : v >= EggMoveData.SpeciesConstant ? "Pokémon " + n.Species(v - EggMoveData.SpeciesConstant) : n.Move(v)));
        }
    }
}
