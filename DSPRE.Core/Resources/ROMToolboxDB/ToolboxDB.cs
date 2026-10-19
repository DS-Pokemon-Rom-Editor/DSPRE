using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Resources;
using static DSPRE.RomInfo;

namespace DSPRE.Resources.ROMToolboxDB
{
    public class ToolboxDB
    {
        internal class ARM9PatchData
        {
            internal string InitString;
            internal string BranchString;

            internal uint BranchOffset;
            internal uint InitOffset;

            internal string Y9String;

            // What DSPRE wrote, and where, before every dump's sites were measured into RomLocator.Arm9ExpansionSitesIn.
            // Only recognising those installs reads it: some places differ from the measured ones (Pearl took Diamond's,
            // and German HGSS's loader offset was mistyped), and HGSS English and Spanish called the loader through r5.
            public static Dictionary<string, string> Arm9ExpansionCodeDB = new Dictionary<string, string>() {
                // Branches
                // DP
                ["branchString" + "_" + RomInfo.GameFamilies.DP + "_" + RomInfo.GameLanguages.English]      = "05 F1 5C FF",
                ["branchString" + "_" + RomInfo.GameFamilies.DP + "_" + RomInfo.GameLanguages.Spanish]      = "06 F1 2C F8",
                ["branchString" + "_" + RomInfo.GameFamilies.DP + "_" + RomInfo.GameLanguages.Italian]      = "05 F1 CC FF",
                ["branchString" + "_" + RomInfo.GameFamilies.DP + "_" + RomInfo.GameLanguages.French]       = "06 F1 1C F8",
                ["branchString" + "_" + RomInfo.GameFamilies.DP + "_" + RomInfo.GameLanguages.German]       = "00 F1 46 FD",

                // Plat
                ["branchString" + "_" + RomInfo.GameFamilies.Plat + "_" + RomInfo.GameLanguages.English]    = "00 F1 5E FC",
                ["branchString" + "_" + RomInfo.GameFamilies.Plat + "_" + RomInfo.GameLanguages.Spanish]    = "00 F1 5C FD", 
                ["branchString" + "_" + RomInfo.GameFamilies.Plat + "_" + RomInfo.GameLanguages.Italian]    = "00 F1 0C FD", 
                ["branchString" + "_" + RomInfo.GameFamilies.Plat + "_" + RomInfo.GameLanguages.French]     = "00 F1 4C FD", 
                ["branchString" + "_" + RomInfo.GameFamilies.Plat + "_" + RomInfo.GameLanguages.German]     = "00 F1 2C FD", 
                ["branchString" + "_" + RomInfo.GameFamilies.Plat + "_" + RomInfo.GameLanguages.Japanese]   = "FF F0 58 FE",

                // HGSS
                ["branchString" + "_" + RomInfo.GameFamilies.HGSS + "_" + RomInfo.GameLanguages.English]    = "0F F1 30 FB",
                ["branchString" + "_" + RomInfo.GameFamilies.HGSS + "_" + RomInfo.GameLanguages.Spanish]    = "0F F1 40 FB",
                ["branchString" + "_" + RomInfo.GameFamilies.HGSS + "_" + RomInfo.GameLanguages.Italian]    = "10 F1 CE F9",
                ["branchString" + "_" + RomInfo.GameFamilies.HGSS + "_" + RomInfo.GameLanguages.French]     = "10 F1 0E FA",
                ["branchString" + "_" + RomInfo.GameFamilies.HGSS + "_" + RomInfo.GameLanguages.German]     = "10 F1 EE F9",
                ["branchString" + "_" + RomInfo.GameFamilies.HGSS + "_" + RomInfo.GameLanguages.Japanese]   = "0F F1 A6 FC",

                // Initialization Routines
                // DP
                ["initString" + "_" + RomInfo.GameFamilies.DP + "_" + RomInfo.GameLanguages.English]    = "FC B5 04 48 41 21 09 22 FF F6 D4 FC 00 20 03 21 FC BD 00 00 00 80 3C 02 00",  //Valid also for P            
                ["initString" + "_" + RomInfo.GameFamilies.DP + "_" + RomInfo.GameLanguages.Spanish]    = "FC B5 04 48 41 21 09 22 FF F6 04 FC 00 20 03 21 FC BD 00 00 00 80 3C 02 00",  //Valid also for P            
                ["initString" + "_" + RomInfo.GameFamilies.DP + "_" + RomInfo.GameLanguages.Italian]    = "FC B5 04 48 41 21 09 22 FF F6 64 FC 00 20 03 21 FC BD 00 00 00 80 3C 02 00",  //Valid also for P            
                ["initString" + "_" + RomInfo.GameFamilies.DP + "_" + RomInfo.GameLanguages.French]     = "FC B5 04 48 41 21 09 22 FF F6 14 FC 00 20 03 21 FC BD 00 00 00 80 3C 02 00",  //Valid also for P            
                ["initString" + "_" + RomInfo.GameFamilies.DP + "_" + RomInfo.GameLanguages.German]     = "FC B5 04 48 41 21 09 22 FF F6 34 FC 00 20 03 21 FC BD 00 00 00 80 3C 02 00",  //Valid also for P            

                // Plat
                ["initString" + "_" + RomInfo.GameFamilies.Plat + "_" + RomInfo.GameLanguages.English]  = "FC B5 04 48 41 21 09 22 05 F7 92 FA 00 20 03 21 FC BD 00 00 00 80 3C 02 00", 
                ["initString" + "_" + RomInfo.GameFamilies.Plat + "_" + RomInfo.GameLanguages.Spanish]  = "FC B5 04 48 41 21 09 22 05 F7 9E F9 00 20 03 21 FC BD 00 00 00 80 3C 02 00",
                ["initString" + "_" + RomInfo.GameFamilies.Plat + "_" + RomInfo.GameLanguages.Italian]  = "FC B5 04 48 41 21 09 22 05 F7 EE F9 00 20 03 21 FC BD 00 00 00 80 3C 02 00",
                ["initString" + "_" + RomInfo.GameFamilies.Plat + "_" + RomInfo.GameLanguages.French]   = "FC B5 04 48 41 21 09 22 05 F7 AE F9 00 20 03 21 FC BD 00 00 00 80 3C 02 00",
                ["initString" + "_" + RomInfo.GameFamilies.Plat + "_" + RomInfo.GameLanguages.German]   = "FC B5 04 48 41 21 09 22 05 F7 CE F9 00 20 03 21 FC BD 00 00 00 80 3C 02 00",
                ["initString" + "_" + RomInfo.GameFamilies.Plat + "_" + RomInfo.GameLanguages.Japanese] = "FC B5 04 48 41 21 09 22 06 F7 38 F8 00 20 03 21 FC BD 00 00 00 80 3C 02 00",

                // HGSS
                ["initString" + "_" + RomInfo.GameFamilies.HGSS + "_" + RomInfo.GameLanguages.English]  = "FC B5 05 48 C0 46 1C 21 00 22 02 4D A8 47 00 20 03 21 FC BD 09 75 00 02 00 80 3C 02",
                ["initString" + "_" + RomInfo.GameFamilies.HGSS + "_" + RomInfo.GameLanguages.Spanish]  = "FC B5 05 48 C0 46 1C 21 00 22 02 4D A8 47 00 20 03 21 FC BD 09 75 00 02 00 80 3C 02",
                ["initString" + "_" + RomInfo.GameFamilies.HGSS + "_" + RomInfo.GameLanguages.Italian]  = "FC B5 04 48 1C 21 00 22 F6 F6 46 FA 00 20 03 21 FC BD 00 00 00 80 3C 02 00",
                ["initString" + "_" + RomInfo.GameFamilies.HGSS + "_" + RomInfo.GameLanguages.French]   = "FC B5 04 48 1C 21 00 22 F6 F6 06 FA 00 20 03 21 FC BD 00 00 00 80 3C 02 00",
                ["initString" + "_" + RomInfo.GameFamilies.HGSS + "_" + RomInfo.GameLanguages.German]   = "FC B5 04 48 1C 21 00 22 F6 F6 26 FA 00 20 03 21 FC BD 00 00 00 80 3C 02 00",
                ["initString" + "_" + RomInfo.GameFamilies.HGSS + "_" + RomInfo.GameLanguages.Japanese] = "FC B5 04 48 1C 21 00 22 F6 F6 08 FF 00 20 03 21 FC BD 00 00 00 80 3C 02 00",

            };
            public static Dictionary<string, uint> Arm9ExpansionOffsetsDB = new Dictionary<string, uint>() {
                // Branch offsets
                ["branchOffset" + "_" + RomInfo.GameFamilies.DP]    = 0x02000C80, //Valid also for P
                ["branchOffset" + "_" + RomInfo.GameFamilies.Plat]  = 0x02000CB4, 
                ["branchOffset" + "_" + RomInfo.GameFamilies.HGSS]  = 0x02000CD0, //Valid also for SS
                ["branchOffset" + "_" + RomInfo.GameFamilies.HGSS + "_" + RomInfo.GameLanguages.Japanese]  = 0x02000CB4, // JP HGSS is different

                // DP init offsets
                ["initOffset" + "_" + RomInfo.GameFamilies.DP + "_" + RomInfo.GameLanguages.English]    = 0x02106B3C,
                ["initOffset" + "_" + RomInfo.GameFamilies.DP + "_" + RomInfo.GameLanguages.Spanish]    = 0x02106CDC,
                ["initOffset" + "_" + RomInfo.GameFamilies.DP + "_" + RomInfo.GameLanguages.Italian]    = 0x02106C1C,
                ["initOffset" + "_" + RomInfo.GameFamilies.DP + "_" + RomInfo.GameLanguages.French]     = 0x02106CBC,
                ["initOffset" + "_" + RomInfo.GameFamilies.DP + "_" + RomInfo.GameLanguages.German]     = 0x02106C7C,

                // Plat init offsets
                ["initOffset" + "_" + RomInfo.GameFamilies.Plat + "_" + RomInfo.GameLanguages.English]  = 0x02101574,
                ["initOffset" + "_" + RomInfo.GameFamilies.Plat + "_" + RomInfo.GameLanguages.Spanish]  = 0x02101770,
                ["initOffset" + "_" + RomInfo.GameFamilies.Plat + "_" + RomInfo.GameLanguages.Italian]  = 0x021016D0,
                ["initOffset" + "_" + RomInfo.GameFamilies.Plat + "_" + RomInfo.GameLanguages.French]   = 0x02101750,
                ["initOffset" + "_" + RomInfo.GameFamilies.Plat + "_" + RomInfo.GameLanguages.German]   = 0x02101710,
                ["initOffset" + "_" + RomInfo.GameFamilies.Plat + "_" + RomInfo.GameLanguages.Japanese] = 0x02100968,

                // HGSS init offsets
                ["initOffset" + "_" + RomInfo.GameFamilies.HGSS + "_" + RomInfo.GameLanguages.English]  = 0x02110334,
                ["initOffset" + "_" + RomInfo.GameFamilies.HGSS + "_" + RomInfo.GameLanguages.Spanish]  = 0x02110354,
                ["initOffset" + "_" + RomInfo.GameFamilies.HGSS + "_" + RomInfo.GameLanguages.Italian]  = 0x02111070,
                ["initOffset" + "_" + RomInfo.GameFamilies.HGSS + "_" + RomInfo.GameLanguages.French]   = 0x021110F0,
                ["initOffset" + "_" + RomInfo.GameFamilies.HGSS + "_" + RomInfo.GameLanguages.German]   = 0x02110B00,
                ["initOffset" + "_" + RomInfo.GameFamilies.HGSS + "_" + RomInfo.GameLanguages.Japanese] = 0x02110604,
            };

            private static string Key(string field) => field + "_" + RomInfo.GameFamily + "_" + RomInfo.GameLanguage;

            /// <summary>Whether an older DSPRE had a per-language install for this ROM.</summary>
            internal static bool HasLanguageEntry => Arm9ExpansionCodeDB.ContainsKey(Key(nameof(BranchString)));

            /// <summary>Whether DSPRE can apply the expansion to this ROM.</summary>
            internal static bool Supported => RomLocator.Arm9ExpansionSites != null || HasLanguageEntry;

            /// <summary>The dumps with measured sites or a per-language entry, the two ways <see cref="Supported"/> passes.</summary>
            internal static List<(string id, int rev)> SupportedDumps =>
                RomLocator.WorksOn(RomLocator.Arm9ExpansionSitesIn)
                    .Concat(RomInfo.KnownDumps.Where(d => Arm9ExpansionCodeDB.ContainsKey(nameof(BranchString) + "_" + RomInfo.FamilyOfCode(d.id) + "_" + RomInfo.LanguageOfCode(d.id))))
                    .Distinct().ToList();

            /// <summary>The expansion for this ROM: its measured sites where it has them, else the per-language entry.</summary>
            internal ARM9PatchData() : this(RomLocator.Arm9ExpansionSites == null) { }

            /// <param name="languageEntry">The per-language entry even where measured sites exist, to recognise an older apply.</param>
            internal ARM9PatchData(bool languageEntry)
            {
                if (!languageEntry && RomLocator.Arm9ExpansionSites is Arm9ExpansionSites sites)
                {
                    // push {r2-r7, lr}; ldr r0, =synthetic overlay; movs r1, #archive; movs r2, #member; bl file loader;
                    // movs r0, #0; movs r1, #3 (the boot pair it replaced); pop {r2-r7, pc}; .word synthetic overlay.
                    List<byte> loader = new List<byte> { 0xFC, 0xB5, 0x04, 0x48, RomInfo.SynthOverlayArchiveId, 0x21,
                        (byte)SyntheticOverlayFileNumbersDB[RomInfo.GameFamily], 0x22 };
                    loader.AddRange(PatchToolboxLogic.BuildThumbBl(ARM9.Address + sites.Loader + 8, sites.FileLoader));
                    loader.AddRange(new byte[] { 0x00, 0x20, 0x03, 0x21, 0xFC, 0xBD, 0x00, 0x00 });
                    loader.AddRange(BitConverter.GetBytes(RomInfo.SynthOverlayLoadAddress));
                    loader.Add(0x00);
                    BranchOffset = sites.Boot;
                    InitOffset = sites.Loader;
                    BranchString = Hex(PatchToolboxLogic.BuildThumbBl(ARM9.Address + sites.Boot, ARM9.Address + sites.Loader));
                    InitString = Hex(loader);
                    return;
                }
                if (RomInfo.GameFamily == GameFamilies.HGSS && RomInfo.GameLanguage == GameLanguages.Japanese)
                {
                    BranchOffset = Arm9ExpansionOffsetsDB[nameof(BranchOffset) + "_" + RomInfo.GameFamily + "_" + RomInfo.GameLanguage] - ARM9.Address;
                }
                else
                {
                    BranchOffset = Arm9ExpansionOffsetsDB[nameof(BranchOffset) + "_" + RomInfo.GameFamily] - ARM9.Address;
                }
                InitOffset = Arm9ExpansionOffsetsDB[nameof(InitOffset) + "_" + RomInfo.GameFamily + "_" + RomInfo.GameLanguage] - ARM9.Address;
                BranchString = Arm9ExpansionCodeDB[nameof(BranchString) + "_" + RomInfo.GameFamily + "_" + RomInfo.GameLanguage];
                InitString = Arm9ExpansionCodeDB[nameof(InitString) + "_" + RomInfo.GameFamily + "_" + RomInfo.GameLanguage];
            }

            private static string Hex(IEnumerable<byte> bytes) => string.Join(" ", bytes.Select(b => b.ToString("X2")));
        }
        internal class BDHCAMPatchData
        {
            internal byte OverlayNumber;

            internal uint BranchOffset;
            internal string BranchString;

            internal uint OverlayOffset1;
            internal uint OverlayOffset2;

            internal string OverlayString1;
            internal string OverlayString2;

            internal byte[] Subroutine;

            public static Dictionary<string, string> BDHCamCodeDB = new Dictionary<string, string>() {
                ["branchString" + "_" + RomInfo.GameFamilies.Plat + "_" + RomInfo.GameLanguages.English] = "B9 F3 E2 F8",
                ["branchString" + "_" + RomInfo.GameFamilies.Plat + "_" + RomInfo.GameLanguages.Spanish] = "B9 F3 AA F8",
                ["branchString" + "_" + RomInfo.GameFamilies.HGSS] = "B6 F3 2E FA", //Also valid for SS, both ESP and ENG

                ["overlayString1"] = "00 4B 18 47 41 9C 3D 02",
                ["overlayString2"] = "00 4B 18 47 01 9C 3D 02",
            };
            public static uint BDHCamSubroutineOffset = 0x000115B0;

            internal static bool SupportsCurrentRom()
            {
                try
                {
                    new BDHCAMPatchData();
                    return true;
                }
                catch (Exception ex)
                {
                    AppLogger.WarnOnce("BDHCAMPatchData.SupportsCurrentRom", ex);
                    return false;
                }
            }

            internal BDHCAMPatchData()
            {
                RomInfo.BdhcamSites sites = RomLocator.BdhcamSites ?? throw new NotSupportedException();
                string codeKey = RomInfo.GameFamily == GameFamilies.HGSS
                    ? nameof(BranchString) + "_" + RomInfo.GameFamily
                    : nameof(BranchString) + "_" + RomInfo.GameFamily + "_" + RomInfo.GameLanguage;
                if (!BDHCamCodeDB.TryGetValue(codeKey, out BranchString)) throw new NotSupportedException();
                OverlayNumber = (byte)sites.Overlay;
                BranchOffset = sites.Branch;
                OverlayOffset1 = sites.Hook1;
                OverlayOffset2 = sites.Hook2;
                OverlayString1 = BDHCamCodeDB[nameof(OverlayString1)];
                OverlayString2 = BDHCamCodeDB[nameof(OverlayString2)];
                Subroutine = (byte[])new ResourceManager("DSPRE.Resources.ROMToolboxDB.BDHCAMPatchDB", Assembly.GetExecutingAssembly()).GetObject(RomInfo.RomID + "_cam");
                if (Subroutine == null)
                {
                    throw new NotSupportedException();
                }
            }
        }
        internal class BuildingRotationPatchData
        {
            internal byte OverlayNumber;
            internal uint HookOverlayOffset;
            internal uint HookRuntimeAddress;
            internal uint RotationMatrixFunctionAddress;
            internal const uint PayloadInternalBranchOffset = 0x08;

            internal byte[] Payload;

            internal static bool SupportsCurrentRom()
            {
                try
                {
                    new BuildingRotationPatchData();
                    return true;
                }
                catch (Exception ex)
                {
                    AppLogger.WarnOnce("BuildingRotationPatchData.SupportsCurrentRom", ex);
                    return false;
                }
            }

            internal BuildingRotationPatchData()
            {
                RomInfo.BuildingRotationSites sites = RomLocator.BuildingRotationSites ?? throw new NotSupportedException();
                OverlayNumber = (byte)sites.Overlay;
                HookOverlayOffset = sites.Hook;
                HookRuntimeAddress = OverlayUtils.OverlayTable.GetRAMAddress(sites.Overlay) + sites.Hook;
                RotationMatrixFunctionAddress = sites.RotationRoutine;
                Payload = LoadPayload();
            }

            // Japanese DP's field code keeps the building in r4 and the matrix at sp+0x24, and its hook replaces
            // add r0, sp, #0x14; ldrh r0, [r0] (the US hook's pair is a compare and branch there):
            // push {r0, lr}; add r0, sp, #0x2C; mov r1, r4; adds r1, #0x74; bl <rotation>; pop {r0, r3};
            // add r0, sp, #0x14; ldrh r0, [r0]; bx r3. The call is rebuilt on apply.
            private static readonly byte[] JapaneseDpPayload =
            {
                0x01, 0xB5, 0x0B, 0xA8, 0x21, 0x1C, 0x74, 0x31, 0x00, 0x00, 0x00, 0x00, 0x09, 0xBC, 0x05, 0xA8, 0x00, 0x88, 0x18, 0x47,
            };

            private byte[] LoadPayload()
            {
                if (RomInfo.GameFamily == GameFamilies.DP && RomInfo.GameLanguage == GameLanguages.Japanese) return (byte[])JapaneseDpPayload.Clone();
                // One routine per family; only its call into the rotation routine differs by dump, and that is rebuilt on apply.
                string key = RomInfo.GameFamily switch { GameFamilies.DP => "ADAE", GameFamilies.Plat => "CPUE", _ => "IPKE" };
                byte[] payload = (byte[])new ResourceManager("DSPRE.Resources.ROMToolboxDB.BuildingRotationPatchDB", Assembly.GetExecutingAssembly()).GetObject(key + "_rotation");
                if (payload == null)
                {
                    throw new NotSupportedException();
                }

                return payload;
            }
        }
        internal class DynamicHeadersPatchData
        {
            internal uint InitOffset;
            internal string InitString;
            internal string REFERENCE_STRING = "19 00 C0 46";

            // HGSS: one header read loads the table address between its (mov r1, #0x18) and its mul, so it is undone by
            // hand. Distances from the init site, like the pointer table below.
            internal int SpecialCaseOffset1 = 0x2BA;
            internal int SpecialCaseOffset2 = 0x2BE;
            internal int SpecialCaseOffset3 = 0x2D4;

            internal string SpecialCaseData1 = "19 00";
            internal string SpecialCaseData2 = "C0 46";
            internal string SpecialCaseData3 = "00 00 00 00";

            public static string GetDynamicHeadersInitString(string romID)
            {
                switch (romID)
                {
                    case "CPUE":
                        return "00 B5 01 1C 94 20 00 22 CC F7 48 FD 03 1C DE F7 C7 F8 00 BD";
                    case "CPUS":
                    case "CPUI":
                    case "CPUF":
                    case "CPUD":
                        return "00 B5 01 1C 94 20 00 22 CC F7 00 FD 03 1C CC F7 74 FC 00 BD";
                    case "CPUJ":
                        return "00 B5 01 1C 94 20 00 22 CC F7 0A FF 03 1C DE F7 3D F9 00 BD";
                    case "IPKS":
                        return "00 B5 01 1C 32 20 00 22 CC F7 5C F9 03 1C DF F7 4D FC 00 BD";
                    case "IPKJ":
                    case "IPGJ":
                        return "00 B5 01 1C 32 20 00 22 CC F7 08 FB 03 1C DF F7 C7 FC 00 BD";
                    default:
                        return "00 B5 01 1C 32 20 00 22 CC F7 58 F9 03 1C DF F7 49 FC 00 BD";
                }
            }
            // Format: the (headerID * 0x18) instruction and the (ARM9_HEADER_TABLE_OFFSET + n) literal, as distances from the
            // init site (RomLocator.Thing.DynamicHeadersInit), which every dump keeps the same.
            public static Dictionary<GameFamilies, Tuple<uint, uint>[]> DynamicHeadersPointersDB = new Dictionary<GameFamilies, Tuple<uint, uint>[]>() {
                [GameFamilies.Plat] = new Tuple<uint, uint>[] {
                    new Tuple<uint, uint>(0x1A, 0x24),
                    new Tuple<uint, uint>(0x2E, 0x38),
                    new Tuple<uint, uint>(0x42, 0x5C),
                    new Tuple<uint, uint>(0x6A, 0x74),
                    new Tuple<uint, uint>(0x7E, 0x88),
                    new Tuple<uint, uint>(0x92, 0x9C),
                    new Tuple<uint, uint>(0xA6, 0xB0),
                    new Tuple<uint, uint>(0xBA, 0xC4),
                    new Tuple<uint, uint>(0xCE, 0xE4),
                    new Tuple<uint, uint>(0xF2, 0xFC),
                    new Tuple<uint, uint>(0x106, 0x110),
                    new Tuple<uint, uint>(0x11A, 0x12C),
                    new Tuple<uint, uint>(0x136, 0x14C),
                    new Tuple<uint, uint>(0x156, 0x160),
                    new Tuple<uint, uint>(0x16A, 0x174),
                    new Tuple<uint, uint>(0x17E, 0x190),
                    new Tuple<uint, uint>(0x19A, 0x1AC),
                    new Tuple<uint, uint>(0x1B6, 0x1C8),
                    new Tuple<uint, uint>(0x1D2, 0x1E4),
                    new Tuple<uint, uint>(0x1EE, 0x200),
                },
                [GameFamilies.HGSS] = new Tuple<uint, uint>[] {
                    new Tuple<uint, uint>(0x1A, 0x24),
                    new Tuple<uint, uint>(0x2E, 0x40),
                    new Tuple<uint, uint>(0x4A, 0x54),
                    new Tuple<uint, uint>(0x5E, 0x68),
                    new Tuple<uint, uint>(0x72, 0x7C),
                    new Tuple<uint, uint>(0x86, 0x90),
                    new Tuple<uint, uint>(0x9A, 0xA4),
                    new Tuple<uint, uint>(0xAE, 0xB8),
                    new Tuple<uint, uint>(0xC2, 0xD8),
                    new Tuple<uint, uint>(0xE2, 0xEC),
                    new Tuple<uint, uint>(0xF6, 0x100),
                    new Tuple<uint, uint>(0x10A, 0x11C),
                    new Tuple<uint, uint>(0x126, 0x13C),
                    new Tuple<uint, uint>(0x146, 0x15C),
                    new Tuple<uint, uint>(0x166, 0x178),
                    new Tuple<uint, uint>(0x182, 0x194),
                    new Tuple<uint, uint>(0x19E, 0x1B0),
                    new Tuple<uint, uint>(0x1BA, 0x1CC),
                    new Tuple<uint, uint>(0x1D6, 0x1E8),
                    new Tuple<uint, uint>(0x1F2, 0x204),
                    new Tuple<uint, uint>(0x20E, 0x220),
                    new Tuple<uint, uint>(0x22A, 0x23C),
                    new Tuple<uint, uint>(0x246, 0x258),
                    new Tuple<uint, uint>(0x262, 0x270),
                    new Tuple<uint, uint>(0x27A, 0x28C),
                    new Tuple<uint, uint>(0x296, 0x2AC),
                },
            };

            internal static bool SupportsCurrentRom => RomLocator.Where(RomLocator.Thing.DynamicHeadersInit) != null;

            internal DynamicHeadersPatchData()
            {
                InitOffset = (uint)(RomLocator.Where(RomLocator.Thing.DynamicHeadersInit)?.Offset ?? throw new NotSupportedException());
                InitString = GetDynamicHeadersInitString(RomInfo.RomID);
            }
        }

        public static Dictionary<GameFamilies, uint> SyntheticOverlayFileNumbersDB = new Dictionary<GameFamilies, uint>() {
            [GameFamilies.DP] = 9,
            [GameFamilies.Plat] = 9,
            [GameFamilies.HGSS] = 0,
        };

        // Distances from RomLocator.Thing.MatrixExpansionCode, and what goes there.
        public static Dictionary<uint[], string> MatrixExpansionDB = new Dictionary<uint[], string>() {
            [new uint[] { 0x0 }] = "FF 01",
            [new uint[] { 0x2 }] = "76 01",
            [new uint[] { 0x9A }] = "C9 01",
            [new uint[] { 0xB4 }] = "49 01",
            [new uint[] { 0xCE }] = "3E 06 00 00",
            [new uint[] { 0xD2 }] = "3C 1F 00 00",
            [new uint[] { 0xEA }] = "50 1F 00 00",
            [new uint[] { 0x13A,
                          0x24A,
                          0x332,
                          0x39E }] = "C4 12 00 00",
            [new uint[] { 0x1CA }] = "84 0C 00 00",
            [new uint[] { 0x1FE }] = "7C 0C 00 00",
        };
    }
}
