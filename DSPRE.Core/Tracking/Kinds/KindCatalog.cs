using System;
using System.Collections.Generic;
using System.IO;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Tracking.Kinds
{
    /// <summary>The record kinds with their own JSON, for the open game.</summary>
    public static class KindCatalog
    {
        public static IEnumerable<ChangeKind> Specific()
        {
            yield return new TextKind();
            yield return new ScriptKind();
            yield return new CodeKind();
            yield return new TrainerKind();
            yield return new LearnsetKind();
            yield return new MapFileKind();
            yield return new MatrixKind();
            yield return new HeaderKind();
            yield return new CodeTableKind();
            yield return new SoundArchiveKind();

            yield return new RecordKind<PokemonPersonalData>("personal", "pokemon/personal", DirNames.PersonalPokeData, "Pokémon",
                (b, _) => new PokemonPersonalData(new MemoryStream(b)), r => r.ToByteArray(),
                (id, n) => n.Species(id),
                n => member => member switch
                {
                    "Item1" or "Item2" => n.Item,
                    "FirstAbility" or "SecondAbility" => n.Ability,
                    _ => null,
                });
            yield return new RecordKind<EvolutionFile>("evolution", "pokemon/evolutions", DirNames.Evolutions, "Evolutions",
                (b, _) => new EvolutionFile(new MemoryStream(b)), r => r.ToByteArray(),
                (id, n) => n.Species(id),
                n => member => member == "Target" ? n.Species : null);
            yield return new RecordKind<MoveData>("move", "moves", DirNames.MoveData, "Move",
                (b, _) => new MoveData(new MemoryStream(b)), r => r.ToByteArray(),
                (id, n) => n.Move(id));
            yield return new RecordKind<ItemData>("item", "items", DirNames.ItemData, "Item",
                (b, id) => new ItemData(new MemoryStream(b), id), r => r.ToByteArray(),
                (id, n) => n.Item(id));
            yield return new RecordKind<TradeData>("trade", "trades", DirNames.TradeData, "Trade",
                (b, id) => new TradeData(id, new MemoryStream(b)), r => r.ToByteArray(),
                refs: n => member => member switch
                {
                    "Species" or "RequestedSpecies" => n.Species,
                    "HeldItem" => n.Item,
                    _ => null,
                });
            yield return new RecordKind<AreaData>("area-data", "maps/area-data", DirNames.AreaData, "Area data",
                (b, _) => new AreaData(new MemoryStream(b)), r => r.ToByteArray());
            yield return new RecordKind<EventFile>("events", "maps/events", DirNames.EventFiles, "Event file",
                (b, _) => new EventFile(new MemoryStream(b)), r => r.ToByteArray(),
                create: NewEvent);
            if (GameDirs.ContainsKey(DirNames.TornWorld))
                yield return new RecordKind<TornWorldFile>("distortion-map", "maps/distortion-world", DirNames.TornWorld, "Distortion World map",
                    (b, _) => new TornWorldFile(b), r => r.ToByteArray());
            yield return new RecordKind<BerryData>("berry", "items/berries", DirNames.BerryData, "Berry",
                (b, _) => new BerryData(b), r => r.ToBytes(), (id, n) => n.Item(BerryData.FirstBerryItem + id));
            yield return new RecordKind<BuildingAnimationInfo>("building-animation", "maps/building-animations/outside", DirNames.BuildingAnimListOut, "Building animation",
                (b, _) => new BuildingAnimationInfo(b), r => r.ToByteArray());
            yield return new RecordKind<BuildingAnimationInfo>("building-animation", "maps/building-animations/inside", DirNames.BuildingAnimListIn, "Building animation (inside)",
                (b, _) => new BuildingAnimationInfo(b), r => r.ToByteArray());
            yield return new RecordKind<SafariZoneEncounterFile>("safari-zone", "encounters/safari-zone", DirNames.SafariZone, "Safari Zone",
                (b, id) => new SafariZoneEncounterFile(id, b), r => r.ToByteArray(), refs: n => SpeciesRefs(n));
            yield return new RecordKind<HeadbuttEncounterFile>("headbutt", "encounters/headbutt", DirNames.Headbutt, "Headbutt",
                (b, _) => FromTemp(b, path => new HeadbuttEncounterFile(path)), r => r.ToByteArray(), refs: n => SpeciesRefs(n),
                create: t => t == typeof(HeadbuttTree) ? new HeadbuttTree() : Activator.CreateInstance(t));
            yield return new RecordKind<RockSmashData>("rock-smash", "encounters/rock-smash", DirNames.RockSmash, "Rock Smash",
                (b, id) => FromTemp(b, path => new RockSmashData((ushort)id, path)), r => r.ToByteArray(), refs: n => SpeciesRefs(n));
            yield return new PokeathlonKind();
            foreach (ChangeKind arrays in DataArrays.ForOpenGame()) yield return arrays;
            if (GameFamily == GameFamilies.HGSS)
                yield return new RecordKind<EncounterFileHGSS>("encounters", "encounters", DirNames.Encounters, "Encounters",
                    (b, _) => new EncounterFileHGSS(new MemoryStream(b)), r => r.ToByteArray(),
                    refs: n => EncounterRefs(n));
            else
                yield return new RecordKind<EncounterFileDPPt>("encounters", "encounters", DirNames.Encounters, "Encounters",
                    (b, _) => new EncounterFileDPPt(new MemoryStream(b)), r => r.ToByteArray(),
                    refs: n => EncounterRefs(n));
        }

        private static readonly HashSet<DirNames> Graphics = new HashSet<DirNames>
        {
            DirNames.PokemonBattleSprites, DirNames.OtherPokemonBattleSprites, DirNames.TrainerGraphics, DirNames.TrainerBackGraphics,
            DirNames.MonIcons, DirNames.ItemIcons, DirNames.WazaEffectChar, DirNames.WazaEffectPltt, DirNames.WazaEffectCell,
            DirNames.WazaEffectCellAnm, DirNames.BattleBg, DirNames.BattleObj, DirNames.DungeonCutinGraphics, DirNames.TitleScreenGraphics,
            DirNames.TrainerCardGraphics, DirNames.WeatherGraphics, DirNames.AreaWindowGraphics, DirNames.SealGraphics, DirNames.Fonts,
            DirNames.WindowFrames, DirNames.Poketch, DirNames.FieldTouchMenu, DirNames.FieldTouchChoices, DirNames.SummaryGraphics,
            DirNames.SummarySprites, DirNames.MoveRelearnerGraphics, DirNames.BattlePartyGraphics, DirNames.BattleBagGraphics,
            DirNames.BagGraphics, DirNames.EncounterEffectGraphics, DirNames.NameInputGraphics, DirNames.StartMenuGraphics,
            DirNames.PokegearGraphics, DirNames.PokegearMapGraphics, DirNames.PokegearPhoneGraphics, DirNames.PokegearRadioGraphics,
            DirNames.PokegearSkinGraphics, DirNames.PartyMenuGraphics, DirNames.BoxGraphics, DirNames.OptionsGraphics,
            DirNames.FootprintGraphics, DirNames.PokedexGraphics, DirNames.OpeningDemoGraphics, DirNames.PokewalkerSprites,
            DirNames.PokewalkerIcons, DirNames.UndergroundParts, DirNames.NumberPadGraphics, DirNames.BattleBgPlanm,
        };

        /// <summary>Whether an archive holds 2D graphics; other archives without their own kind are assets.</summary>
        public static bool IsGraphics(DirNames dir) => Graphics.Contains(dir);

        private static ReflectedFields.RefLookup EncounterRefs(TrackingNames n) => member =>
            member.IndexOf("Level", StringComparison.Ordinal) >= 0 || member.IndexOf("Rate", StringComparison.Ordinal) >= 0 ? null
            : member.IndexOf("Pokemon", StringComparison.Ordinal) >= 0 || member.IndexOf("Species", StringComparison.Ordinal) >= 0 || member.EndsWith("Mons", StringComparison.Ordinal)
                ? n.Species : null;

        private static ReflectedFields.RefLookup SpeciesRefs(TrackingNames n) => member =>
            member.IndexOf("Species", StringComparison.Ordinal) >= 0 || member.IndexOf("Pokemon", StringComparison.Ordinal) >= 0 ? n.Species : null;

        /// <summary>For readers that only open a path.</summary>
        private static T FromTemp<T>(byte[] bytes, Func<string, T> open)
        {
            string temp = Path.Combine(Path.GetTempPath(), "dspre_track_" + Guid.NewGuid().ToString("N"));
            try
            {
                PlainFiles.WriteBytes(temp, bytes);
                return open(temp);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }

        private static object NewEvent(Type type)
        {
            if (type == typeof(Spawnable)) return new Spawnable(0, 0);
            if (type == typeof(Overworld)) return new Overworld(0, 0, 0);
            if (type == typeof(Warp)) return new Warp(0, 0);
            if (type == typeof(Trigger)) return new Trigger(0, 0);
            return Activator.CreateInstance(type);
        }
    }
}
