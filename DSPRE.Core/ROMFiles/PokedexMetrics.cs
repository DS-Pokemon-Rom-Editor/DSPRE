using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>One species' Pokédex measurements: the height and weight its entry shows, the body shape the
    /// search sorts it under, and how the size check draws it beside the player.</summary>
    public sealed class PokedexMetrics
    {
        /// <summary>Decimetres.</summary>
        public int Height;
        /// <summary>Hectograms.</summary>
        public int Weight;
        public int BodyShape;
        public int FemaleTrainerScale, FemalePokemonScale, MaleTrainerScale, MalePokemonScale;
        public int FemaleTrainerYOffset, FemalePokemonYOffset, MaleTrainerYOffset, MalePokemonYOffset;

        public PokedexMetrics Clone() => (PokedexMetrics)MemberwiseClone();

        public bool SameAs(PokedexMetrics o) => o != null
            && Height == o.Height && Weight == o.Weight && BodyShape == o.BodyShape
            && FemaleTrainerScale == o.FemaleTrainerScale && FemalePokemonScale == o.FemalePokemonScale
            && MaleTrainerScale == o.MaleTrainerScale && MalePokemonScale == o.MalePokemonScale
            && FemaleTrainerYOffset == o.FemaleTrainerYOffset && FemalePokemonYOffset == o.FemalePokemonYOffset
            && MaleTrainerYOffset == o.MaleTrainerYOffset && MalePokemonYOffset == o.MalePokemonYOffset;
    }

    /// <summary>
    /// The game's Pokédex data archives (<see cref="DirNames.pokedexData"/>, and in Platinum and HGSS the
    /// <see cref="DirNames.pokedexDataAltered"/> copy that differs only in Giratina's forme). Members 0-10 hold one
    /// value per species: height and weight (u32), body shape (u8), then the female trainer, female Pokémon, male
    /// trainer and male Pokémon scales (u16) and Y offsets (s16). The sort lists built from them are left to
    /// <see cref="PokedexLists"/>, which rebuilds them only when asked.
    /// </summary>
    public sealed class PokedexDataArchive
    {
        public const int BodyShapes = 14;

        private sealed class Copy
        {
            public DirNames Dir;
            public string Folder;
            public byte[][] Members;
            public bool[] Dirty;
        }

        private readonly List<Copy> _copies = new();

        /// <summary>Species 0 to <see cref="SpeciesCount"/> - 1 have records.</summary>
        public int SpeciesCount { get; }

        /// <summary>Platinum and HGSS keep Origin Giratina in the main copy and Altered Giratina in the other;
        /// Diamond and Pearl have only Altered.</summary>
        public bool HasOriginGiratina => _copies.Count > 1;

        private PokedexDataArchive(int speciesCount)
        {
            SpeciesCount = speciesCount;
        }

        /// <summary>Reads every copy the game has, or explains why it can't.</summary>
        public static bool TryLoad(out PokedexDataArchive archive, out string error)
        {
            archive = null;
            error = null;
            if (gameDirs == null || !gameDirs.ContainsKey(DirNames.pokedexData)) { error = "This game's Pokédex data isn't mapped."; return false; }

            int memberCount = gameFamily == GameFamilies.HGSS ? 102 : 58;

            List<DirNames> dirs = new List<DirNames> { DirNames.pokedexData };
            if (gameDirs.ContainsKey(DirNames.pokedexDataAltered)) dirs.Add(DirNames.pokedexDataAltered);
            DSUtils.TryUnpackNarcs(dirs);

            List<Copy> copies = new List<Copy>();
            foreach (DirNames dir in dirs)
            {
                string folder = gameDirs[dir].unpackedDir;
                byte[][] members = new byte[memberCount][];
                for (int i = 0; i < memberCount; i++)
                {
                    string f = Path.Combine(folder, i.ToString("D4"));
                    if (!File.Exists(f)) { error = $"Pokédex data member {i} is missing from {folder}."; return false; }
                    members[i] = File.ReadAllBytes(f);
                }
                if (File.Exists(Path.Combine(folder, memberCount.ToString("D4"))))
                { error = $"The Pokédex data has more than the game's {memberCount} members, so its layout isn't the one DSPRE knows."; return false; }
                copies.Add(new Copy { Dir = dir, Folder = folder, Members = members, Dirty = new bool[memberCount] });
            }

            int species = copies[0].Members[0].Length / 4;
            foreach (Copy c in copies)
            {
                byte[][] m = c.Members;
                if (species < 2 || m[0].Length != species * 4 || m[1].Length != species * 4 || m[2].Length < species
                    || Enumerable.Range(3, 8).Any(i => m[i].Length < species * 2))
                { error = "The Pokédex data's per-species tables don't agree on how many species there are."; return false; }
            }

            archive = new PokedexDataArchive(species);
            archive._copies.AddRange(copies);
            return true;
        }

        private Copy CopyFor(int species, bool originGiratina) =>
            HasOriginGiratina && species == SpeciesFile.GIRATINA_ID_NUM && !originGiratina ? _copies[1] : _copies[0];

        /// <summary>The species' values; for Giratina, <paramref name="originGiratina"/> picks the forme where the
        /// game has both.</summary>
        public PokedexMetrics Get(int species, bool originGiratina = false)
        {
            if (species < 0 || species >= SpeciesCount) return null;
            byte[][] m = CopyFor(species, originGiratina).Members;
            int U(int member) => BitConverter.ToUInt16(m[member], species * 2);
            int S(int member) => BitConverter.ToInt16(m[member], species * 2);
            return new PokedexMetrics
            {
                Height = (int)BitConverter.ToUInt32(m[0], species * 4),
                Weight = (int)BitConverter.ToUInt32(m[1], species * 4),
                BodyShape = m[2][species],
                FemaleTrainerScale = U(3), FemalePokemonScale = U(4), MaleTrainerScale = U(5), MalePokemonScale = U(6),
                FemaleTrainerYOffset = S(7), FemalePokemonYOffset = S(8), MaleTrainerYOffset = S(9), MalePokemonYOffset = S(10),
            };
        }

        /// <summary>Returns why the values can't be stored, or null.</summary>
        public static string WhyNot(PokedexMetrics v)
        {
            if (v.Height < 0 || v.Weight < 0) return "Height and weight can't be negative.";
            if (v.BodyShape < 0 || v.BodyShape >= BodyShapes) return $"Body shape must be 0 to {BodyShapes - 1}.";
            int[] scales = { v.FemaleTrainerScale, v.FemalePokemonScale, v.MaleTrainerScale, v.MalePokemonScale };
            int[] offsets = { v.FemaleTrainerYOffset, v.FemalePokemonYOffset, v.MaleTrainerYOffset, v.MalePokemonYOffset };
            if (scales.Any(s => s < 0 || s > ushort.MaxValue)) return $"Scales must be 0 to {ushort.MaxValue}.";
            if (offsets.Any(s => s < short.MinValue || s > short.MaxValue)) return $"Offsets must be {short.MinValue} to {short.MaxValue}.";
            return null;
        }

        /// <summary>Sets the species' values in every copy, or for Giratina in the chosen forme's copy only.</summary>
        public void Set(int species, PokedexMetrics v, bool originGiratina = false)
        {
            if (species <= 0 || species >= SpeciesCount) throw new ArgumentOutOfRangeException(nameof(species));
            string why = WhyNot(v);
            if (why != null) throw new ArgumentException(why);

            Copy[] targets = HasOriginGiratina && species == SpeciesFile.GIRATINA_ID_NUM ? new[] { CopyFor(species, originGiratina) } : _copies.ToArray();
            foreach (Copy c in targets)
            {
                Put(c, 0, species * 4, BitConverter.GetBytes((uint)v.Height));
                Put(c, 1, species * 4, BitConverter.GetBytes((uint)v.Weight));
                Put(c, 2, species, new[] { (byte)v.BodyShape });
                int[] halves = { v.FemaleTrainerScale, v.FemalePokemonScale, v.MaleTrainerScale, v.MalePokemonScale,
                                 v.FemaleTrainerYOffset, v.FemalePokemonYOffset, v.MaleTrainerYOffset, v.MalePokemonYOffset };
                for (int i = 0; i < halves.Length; i++) Put(c, 3 + i, species * 2, BitConverter.GetBytes((ushort)halves[i]));
            }
        }

        private static void Put(Copy c, int member, int offset, byte[] bytes)
        {
            byte[] m = c.Members[member];
            for (int i = 0; i < bytes.Length; i++)
            {
                if (m[offset + i] == bytes[i]) continue;
                m[offset + i] = bytes[i];
                c.Dirty[member] = true;
            }
        }

        /// <summary>Writes the members that changed into the unpacked archives.</summary>
        public void Save()
        {
            foreach (Copy c in _copies)
                for (int i = 0; i < c.Members.Length; i++)
                {
                    if (!c.Dirty[i]) continue;
                    File.WriteAllBytes(Path.Combine(c.Folder, i.ToString("D4")), c.Members[i]);
                    c.Dirty[i] = false;
                }
        }
    }
}
