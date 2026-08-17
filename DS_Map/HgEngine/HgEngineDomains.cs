using System.Collections.Generic;
using System.Linq;
using static DSPRE.RomInfo;

namespace DSPRE.HgEngine
{
    public enum HgEngineDomain { Species, Trainers, Items, Moves, Encounters, SpriteOffsets, PokemonSprites, TrainerGraphics }

    /// <summary>
    /// One hg-engine data domain: its source file, the isolated make target that rebuilds it (verified
    /// against a real checkout to run in 0.5-3s), and which
    /// RomInfo.DirNames each of its built narcs feeds when a source checkout is linked and enabled. A
    /// domain's single make target can produce more than one narc: Trainers' single source
    /// (data/Trainers.c) and single target (build/narc/a055.narc) also produces build/narc/a056.narc as
    /// part of the same recipe, feeding trainerProperties and trainerParty respectively.
    /// </summary>
    public sealed class HgEngineDomainInfo
    {
        public HgEngineDomain Domain { get; init; }
        public string SourceFileRelPath { get; init; }                 // relative to the repo root, e.g. "data/Species.c"
        public IReadOnlyList<string> MakeTargets { get; init; }        // e.g. ["build/narc/personal.narc"]; built together in one `make` call
        public IReadOnlyDictionary<DirNames, string> NarcByDir { get; init; }   // DirNames -> build/narc/*.narc it's fed from
        public IEnumerable<DirNames> OwnedDirs => NarcByDir.Keys;

        // Domains too broad to mtime-track (e.g. hundreds of sprite PNGs) sync once per link session
        // instead of on every touch.
        public bool SyncOncePerSession { get; init; }
    }

    /// <summary>The five v1 domains and the DirNames each one owns while an hg-engine checkout is linked+enabled.</summary>
    public static class HgEngineDomains
    {
        public static readonly IReadOnlyList<HgEngineDomainInfo> All = new[]
        {
            new HgEngineDomainInfo
            {
                Domain = HgEngineDomain.Species,
                SourceFileRelPath = "data/Species.c",
                // Level-up learnsets are a separate source (data/learnsets/learnsets.json) and narc
                // target, but editorially belong with Species. See HgEngineLearnsets for why a/0/3/3
                // needs a custom transform instead of a plain narc extract.
                MakeTargets = new[] { "build/narc/personal.narc", "build/narc/a033.narc" },
                NarcByDir = new Dictionary<DirNames, string>
                {
                    [DirNames.personalPokeData] = "build/narc/personal.narc",
                    [DirNames.learnsets] = "build/narc/a033.narc",
                },
            },
            new HgEngineDomainInfo
            {
                Domain = HgEngineDomain.Trainers,
                SourceFileRelPath = "data/Trainers.c",
                MakeTargets = new[] { "build/narc/a055.narc" },
                NarcByDir = new Dictionary<DirNames, string>
                {
                    [DirNames.trainerProperties] = "build/narc/a055.narc",
                    [DirNames.trainerParty] = "build/narc/a056.narc",
                },
            },
            new HgEngineDomainInfo
            {
                Domain = HgEngineDomain.Items,
                SourceFileRelPath = "data/itemdata/itemdata.c",
                MakeTargets = new[] { "build/narc/itemdata.narc" },
                NarcByDir = new Dictionary<DirNames, string> { [DirNames.itemData] = "build/narc/itemdata.narc" },
            },
            new HgEngineDomainInfo
            {
                Domain = HgEngineDomain.Moves,
                SourceFileRelPath = "data/Moves.c",
                MakeTargets = new[] { "build/narc/a011.narc" },
                NarcByDir = new Dictionary<DirNames, string> { [DirNames.moveData] = "build/narc/a011.narc" },
            },
            new HgEngineDomainInfo
            {
                Domain = HgEngineDomain.Encounters,
                SourceFileRelPath = "data/Encounters.c",
                MakeTargets = new[] { "build/narc/encounters.narc" },
                NarcByDir = new Dictionary<DirNames, string> { [DirNames.encounters] = "build/narc/encounters.narc" },
            },
            new HgEngineDomainInfo
            {
                // Species-indexed like Species itself, but its own source file and make target.
                Domain = HgEngineDomain.SpriteOffsets,
                SourceFileRelPath = "data/SpriteOffsets.c",
                MakeTargets = new[] { "build/narc/spriteoffsets.narc" },
                NarcByDir = new Dictionary<DirNames, string> { [DirNames.pokemonSpriteOffsets] = "build/narc/spriteoffsets.narc" },
            },
            new HgEngineDomainInfo
            {
                // Front/back battle sprites (pokegra.narc) and alternate-form sprites (otherpoke.narc), built together.
                Domain = HgEngineDomain.PokemonSprites,
                SourceFileRelPath = "data/graphics/pokegra.mk",
                MakeTargets = new[] { "build/narc/pokegra.narc", "build/narc/otherpoke.narc" },
                NarcByDir = new Dictionary<DirNames, string>
                {
                    [DirNames.pokemonBattleSprites] = "build/narc/pokegra.narc",
                    [DirNames.otherPokemonBattleSprites] = "build/narc/otherpoke.narc",
                },
                SyncOncePerSession = true,
            },
            new HgEngineDomainInfo
            {
                // Per-class front battle sprites (trainer_gfx.narc): NCGR/NCLR/NCER/NANR ×129 classes.
                Domain = HgEngineDomain.TrainerGraphics,
                SourceFileRelPath = "data/graphics/trainer_gfx",
                MakeTargets = new[] { "build/narc/trainer_gfx.narc" },
                NarcByDir = new Dictionary<DirNames, string> { [DirNames.trainerGraphics] = "build/narc/trainer_gfx.narc" },
                SyncOncePerSession = true,
            },
        };

        /// <summary>True if this DirNames is hg-engine-owned right now (checkout linked and enabled).
        /// Callers use this to route reads/writes to the source backend instead of the packed ROM, and
        /// Save ROM uses it to skip repacking NARCs that would be overwritten by the next Compile ROM anyway.</summary>
        public static bool IsOwned(DirNames dir) => HgEngineProject.IsActive && All.Any(d => d.OwnedDirs.Contains(dir));

        public static HgEngineDomainInfo ForDir(DirNames dir) => All.FirstOrDefault(d => d.OwnedDirs.Contains(dir));
    }
}
