using System;
using System.Collections.Generic;

namespace DSPRE.Tracking
{
    /// <summary>Clean retail dumps by SHA-1, to tell a vanilla ROM from an edited one. An xdelta for players starts from one of these.</summary>
    public static class VanillaDumps
    {
        private static readonly Dictionary<string, (string Code, int Revision)> Known = new Dictionary<string, (string, int)>(StringComparer.OrdinalIgnoreCase)
        {
            ["432dbe312bc51e36bb8cb6fcb5e08f6968f124a4"] = ("ADAD", 5),
            ["66d2fbfb0dbc1f86a3d726971196989b950092bc"] = ("ADAE", 13),
            ["a46233d8b79a69ea87aa295a0efad5237d02841e"] = ("ADAE", 5),
            ["e961e632ca6f72f81e19c5be3d93936772e8544a"] = ("ADAF", 5),
            ["cd7bcb4de080f3548ca3fb14d946d5ddc717b03b"] = ("ADAI", 5),
            ["fb0bba9fca12d418c8b430e8950171cc356ee981"] = ("ADAJ", 0),
            ["d53c22b2e5fd96eed843d3b45eb192c7e9379da0"] = ("ADAK", 0),
            ["ce2595eba1e23eacb37056598319619f81aefc29"] = ("ADAS", 5),
            ["c13395f2a35bb9d2f8e867d9f504896398f795cb"] = ("APAD", 5),
            ["b5652db288c75417de159d93d297689c885dbbd1"] = ("APAE", 13),
            ["99083bf15ec7c6b81b4ba241ee10abd9e80999ac"] = ("APAE", 5),
            ["dbe7ff557a2d166038f5294ed82868150fe84442"] = ("APAF", 5),
            ["23c5695662e906d9d57ff6e0b3e7842b9e7a1b84"] = ("APAI", 5),
            ["d2ccb37242a39df000c8be83002a62db126aa5ea"] = ("APAJ", 0),
            ["6f7592402cd5f0fb57b219a508df3826d82f75dd"] = ("APAJ", 5),
            ["2cec46f24c0463350ed7de961dc6d3256a734772"] = ("APAJ", 6),
            ["d1b07c0aec0da5ba865ec1b4bc39a2be4c9fb8fc"] = ("APAK", 0),
            ["3f0e08537c5579c9492d4a9cdff3ae30bb037f3b"] = ("APAS", 5),
            ["d40f19ac24d67c721a4ab25e1c013154443d75b4"] = ("CPUD", 0),
            ["0862ec35b24de5c7e2dcb88c9eea0873110d755c"] = ("CPUE", 1),
            ["8d15cd443d4e83e20493c79d3f9dc408c4f51ae0"] = ("CPUE", 10),
            ["41ea92f794560e347cd9509f5ec9e9d479a4bbf1"] = ("CPUF", 0),
            ["19148e7a38e731680fb2a045ed112b115be90fc2"] = ("CPUI", 0),
            ["5b460ab10358fba74d14a704db0d9517172ee3b6"] = ("CPUJ", 0),
            ["12faf0dbcf0fb558438e464afbfe7c5b5e79e8c6"] = ("CPUS", 0),
            ["576cdb7eec728c49aed85b9dd7e1e084ba1f08f9"] = ("IPGD", 0),
            ["f8dc38ea20c17541a43b58c5e6d18c1732c7e582"] = ("IPGE", 0),
            ["27a25c23aa1c0ecabe48a30a004b96c9dbc97730"] = ("IPGE", 10),
            ["e137acf711e0b14c297abf0254e5b4a9263338e7"] = ("IPGI", 0),
            ["facb67d1b6eece6e2b9d5c614427ac6f99b653d3"] = ("IPGJ", 0),
            ["1aa8c3e63f9848d8677c02f96db78d0bfa6aaa7f"] = ("IPGK", 0),
            ["beaae88bb1dc7d09de5f393a67ef0559bde508c8"] = ("IPGS", 0),
            ["13b6853aa93121069df1d17446d3ecb36872aecf"] = ("IPKD", 0),
            ["4fcded0e2713dc03929845de631d0932ea2b5a37"] = ("IPKE", 0),
            ["eb47ab4ba0326ae842135f62c7ec68cf85c9785f"] = ("IPKE", 10),
            ["6b7f9bff57eb58bc8d6e48e9e5c370719458c721"] = ("IPKI", 0),
            ["82451ee0ca9c3b768ba21367b92d8aa0e8defda7"] = ("IPKJ", 0),
            ["52516d1690d01b59e03c7fe3b1259239f5daea38"] = ("IPKK", 0),
        };

        /// <summary>"Pokémon HeartGold - US - IPKE" for a known clean dump, or null. Takes the hash with or without its "sha1:" prefix.</summary>
        public static string NameOf(string sha1)
        {
            if (sha1 == null) return null;
            if (sha1.StartsWith("sha1:", StringComparison.OrdinalIgnoreCase)) sha1 = sha1.Substring(5);
            return Known.TryGetValue(sha1, out (string Code, int Revision) dump) ? RomInfo.DumpName(dump.Code, dump.Revision) : null;
        }
    }
}
