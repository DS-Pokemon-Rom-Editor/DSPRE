using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.LibNDSFormats;
using DSPRE.ROMFiles;
using NarcAPI;
using Xunit;
using Xunit.Abstractions;
using static DSPRE.RomInfo;

namespace DSPRE.Tests
{
    /// <summary>
    /// Builds disposable Issue 155 runtime fixtures without copying an unpacked project. The configured
    /// project is repacked to a temporary ROM, that ROM is unpacked normally, and only the generated
    /// project is edited.
    /// </summary>
    [Collection("rom")]
    public sealed class Issue155RuntimeStager
    {
        private readonly ITestOutputHelper _out;

        public Issue155RuntimeStager(ITestOutputHelper output)
        {
            _out = output;
        }

        [SkippableFact]
        public void BuildPlatinumDifferentProfileFixture()
        {
            Skip.If(!ModeIs("platinum"), "DSPRE_ISSUE155_STAGE is not platinum");
            BuildFixture("CPUE", TestRoms.Platinum, "platinum", headerId: 411, overworldId: 0,
                preferredSourceAppearance: 252);
        }

        [SkippableFact]
        public void BuildHeartGoldDifferentProfileFixture()
        {
            Skip.If(!ModeIs("heartgold"), "DSPRE_ISSUE155_STAGE is not heartgold");
            BuildFixture("IPKE", TestRoms.HeartGold, "heartgold", headerId: 300, overworldId: 1,
                preferredSourceAppearance: 415);
        }

        private void BuildFixture(string gameCode, string sourceProject, string label, int headerId,
            ushort overworldId, uint preferredSourceAppearance)
        {
            string output = RequireOutputDirectory();
            (string root, string project) = CreateGeneratedProject(gameCode, sourceProject, label);
            try
            {
                new RomInfo(gameCode, project);
                DSUtils.TryUnpackNarcs(new List<DirNames>
                {
                    DirNames.OWSprites,
                    DirNames.eventFiles,
                });
                Assert.True(Directory.Exists(gameDirs[DirNames.OWSprites].unpackedDir),
                    "The overworld graphics archive could not be unpacked");
                Assert.True(Directory.Exists(gameDirs[DirNames.eventFiles].unpackedDir),
                    "The event archive could not be unpacked");
                RomInfo.SetOWtable();
                RomInfo.Set3DOverworldsDict();
                RomInfo.ReadOWTable();

                MapHeader header = MapHeader.GetMapHeader(checked((ushort)headerId));
                var events = new EventFile(header.eventFileID);
                Overworld visible = Assert.Single(events.overworlds, entry => entry.owID == overworldId);
                uint targetAppearance = visible.overlayTableEntry;
                Assert.True(OverworldTable.TryGetValue(targetAppearance, out var targetEntry),
                    $"Visible overworld appearance {targetAppearance} is absent from the table");

                ushort? runtimeOverworldId = null;
                if (label == "platinum")
                {
                    const int globalX = 116;
                    const int globalY = 889;
                    ushort id = checked((ushort)(events.overworlds.Max(entry => entry.owID) + 1));
                    var runtimeOverworld = new Overworld(visible)
                    {
                        owID = id,
                        flag = 0,
                        movement = 0x0E,
                        xMatrixPosition = checked((ushort)(globalX / MapFile.mapSize)),
                        yMatrixPosition = checked((ushort)(globalY / MapFile.mapSize)),
                        xMapPosition = checked((short)(globalX % MapFile.mapSize)),
                        yMapPosition = checked((short)(globalY % MapFile.mapSize)),
                        zPosition = 0,
                    };
                    events.overworlds.Add(runtimeOverworld);
                    File.WriteAllBytes(Filesystem.GetEventPath(header.eventFileID), events.ToByteArray());
                    var reopenedEvents = new EventFile(new MemoryStream(
                        File.ReadAllBytes(Filesystem.GetEventPath(header.eventFileID))));
                    Overworld reopenedRuntime = Assert.Single(reopenedEvents.overworlds,
                        entry => entry.owID == id);
                    Assert.Equal(targetAppearance, reopenedRuntime.overlayTableEntry);
                    Assert.Equal(globalX, reopenedRuntime.xMapPosition +
                        MapFile.mapSize * reopenedRuntime.xMatrixPosition);
                    Assert.Equal(globalY, reopenedRuntime.yMapPosition +
                        MapFile.mapSize * reopenedRuntime.yMatrixPosition);
                    runtimeOverworldId = id;
                }

                string sprites = gameDirs[DirNames.OWSprites].unpackedDir;
                string targetPath = Path.Combine(sprites, targetEntry.spriteID.ToString("D4"));
                Assert.True(Btx0Structure.TryInspect(File.ReadAllBytes(targetPath),
                    out Btx0Structure before, out string beforeError), beforeError);

                uint sourceAppearance = SelectDifferentProfile(
                    preferredSourceAppearance, targetAppearance, before, sprites);
                var sourceEntry = OverworldTable[sourceAppearance];
                string sourcePath = Path.Combine(sprites, sourceEntry.spriteID.ToString("D4"));
                byte[] sourceBtx = File.ReadAllBytes(sourcePath);
                Assert.True(Btx0Structure.TryInspect(sourceBtx, out Btx0Structure source, out string sourceError),
                    sourceError);
                Assert.False(before.HasSameProfileAs(source),
                    "The runtime source must use a genuinely different BTX profile");

                Assert.True(OverworldSpriteProfileMetadata.TryCreatePatch(
                    targetAppearance, sourceAppearance, out OverworldSpriteProfileMetadataPatch patch,
                    out string patchError), patchError);
                Assert.True(patch.TryApply(out string applyError), applyError);
                File.WriteAllBytes(targetPath, sourceBtx);

                Assert.True(Btx0Structure.TryInspect(File.ReadAllBytes(targetPath),
                    out Btx0Structure reopened, out string reopenError), reopenError);
                Assert.True(reopened.HasSameProfileAs(source),
                    "The replacement BTX did not reopen with the selected profile");
                Assert.Equal(sourceBtx, File.ReadAllBytes(targetPath));

                RepackArchive(DirNames.OWSprites);
                if (runtimeOverworldId.HasValue)
                    RepackArchive(DirNames.eventFiles);
                Narc packed = Narc.Open(gameDirs[DirNames.OWSprites].packedDir);
                Assert.NotNull(packed);
                byte[] packedTarget = packed.GetElementBytes((int)targetEntry.spriteID);
                Assert.True(Btx0Structure.TryInspect(packedTarget,
                    out Btx0Structure packedStructure, out string packedError), packedError);
                Assert.True(packedStructure.HasSameProfileAs(source),
                    "The rebuilt mmodel archive lost the selected profile");
                Assert.Equal(sourceBtx, packedTarget);
                if (runtimeOverworldId.HasValue)
                {
                    Narc packedEvents = Narc.Open(gameDirs[DirNames.eventFiles].packedDir);
                    Assert.NotNull(packedEvents);
                    var packedEvent = new EventFile(new MemoryStream(
                        packedEvents.GetElementBytes(header.eventFileID)));
                    Assert.Contains(packedEvent.overworlds,
                        entry => entry.owID == runtimeOverworldId.Value &&
                                 entry.overlayTableEntry == targetAppearance);
                }

                string rom = Path.Combine(output, $"issue155-{label}-different-profile.nds");
                Assert.True(DSUtils.RepackROM(rom) && File.Exists(rom), $"{label} ROM repack failed");

                string manifest = Path.Combine(output, $"{label}-different-profile.txt");
                File.WriteAllLines(manifest, new[]
                {
                    $"Issue 155 {label} different-profile runtime fixture",
                    $"Header: {headerId}",
                    $"Event file: {header.eventFileID}",
                    $"Visible overworld ID: {overworldId}",
                    $"Runtime clone overworld ID: {(runtimeOverworldId.HasValue ? runtimeOverworldId.Value.ToString() : "not added")}",
                    $"Target appearance: {targetAppearance}",
                    $"Target mmodel member: {targetEntry.spriteID}",
                    $"Original sheet: {before.SheetWidth}x{before.SheetHeight}",
                    $"Source appearance: {sourceAppearance}",
                    $"Source mmodel member: {sourceEntry.spriteID}",
                    $"Replacement sheet: {source.SheetWidth}x{source.SheetHeight}",
                    $"ROM: {Path.GetFileName(rom)}",
                });

                _out.WriteLine($"{label}: appearance {targetAppearance} changed from " +
                    $"{before.SheetWidth}x{before.SheetHeight} to the {source.SheetWidth}x{source.SheetHeight} " +
                    $"profile of appearance {sourceAppearance}");
                _out.WriteLine($"Built {rom}");
            }
            finally
            {
                DeleteGeneratedProject(root);
            }
        }

        private static uint SelectDifferentProfile(uint preferred, uint target, Btx0Structure targetStructure,
            string sprites)
        {
            IEnumerable<uint> candidates = new[] { preferred }
                .Concat(OverworldTable.Keys.OrderBy(id => id))
                .Distinct();
            foreach (uint appearance in candidates)
            {
                if (appearance == target || !OverworldTable.TryGetValue(appearance, out var entry) ||
                    entry.spriteID == 0x3D3D)
                    continue;

                string path = Path.Combine(sprites, entry.spriteID.ToString("D4"));
                if (!File.Exists(path) ||
                    !Btx0Structure.TryInspect(File.ReadAllBytes(path), out Btx0Structure candidate, out _) ||
                    targetStructure.HasSameProfileAs(candidate))
                    continue;

                if (OverworldSpriteProfileMetadata.TryCreatePatch(target, appearance, out _, out _))
                    return appearance;
            }

            throw new InvalidOperationException("No game-local appearance with a different usable profile was found.");
        }

        private static (string Root, string Project) CreateGeneratedProject(
            string gameCode, string sourceProject, string label)
        {
            Assert.True(Directory.Exists(sourceProject), $"The configured {label} project is unavailable");
            string root = Path.Combine(Path.GetTempPath(), "DSPRE", "Issue155", $"{label}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            string baseRom = Path.Combine(root, $"{label}-base.nds");

            new RomInfo(gameCode, sourceProject);
            Assert.True(DSUtils.RepackROM(baseRom) && File.Exists(baseRom),
                $"The configured {label} project could not be repacked to a temporary ROM");

            string generatedProject = DSUtils.WorkDirPathFromFile(baseRom);
            Assert.True(DSUtils.UnpackRom(baseRom, generatedProject),
                $"The temporary {label} ROM could not be unpacked through DSPRE");
            Assert.NotEqual(-1, DSUtils.GetFolderType(generatedProject));
            return (root, generatedProject);
        }

        private static bool ModeIs(string mode) =>
            string.Equals(Environment.GetEnvironmentVariable("DSPRE_ISSUE155_STAGE"), mode,
                StringComparison.OrdinalIgnoreCase);

        private static string RequireOutputDirectory()
        {
            string output = Environment.GetEnvironmentVariable("DSPRE_ISSUE155_OUTPUT");
            Assert.False(string.IsNullOrWhiteSpace(output), "DSPRE_ISSUE155_OUTPUT is required");
            output = Path.GetFullPath(output);
            Directory.CreateDirectory(output);
            return output;
        }

        private static void RepackArchive(DirNames archive)
        {
            var directory = gameDirs[archive];
            Narc.FromFolder(directory.unpackedDir).Save(directory.packedDir);
        }

        private static void DeleteGeneratedProject(string root)
        {
            string fullRoot = Path.GetFullPath(root);
            string safeRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "DSPRE", "Issue155"))
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Assert.StartsWith(safeRoot, fullRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
            if (Directory.Exists(fullRoot)) Directory.Delete(fullRoot, recursive: true);
        }
    }
}
