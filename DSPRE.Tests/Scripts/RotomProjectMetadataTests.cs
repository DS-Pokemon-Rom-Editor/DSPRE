using System;
using System.IO;
using DSPRE;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>Rotom can read the ROM metadata from an hg-engine project's separate working folder.</summary>
    [Collection("rom")]
    public class RotomProjectMetadataTests
    {
        /// <summary>Checks both header layouts, repeated preparation, and replacement of the extracted ARM9.</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void MetadataRemainsAvailableAndFollowsTheExtractedFiles(bool dsRom)
        {
            string root = Path.Combine(Path.GetTempPath(), "dspre-rotom-metadata-" + Guid.NewGuid().ToString("N"));
            string baseDir = Path.Combine(root, "base");
            string project = Path.Combine(root, ".dspre");
            string meta = dsRom ? Path.Combine(root, "base_dsrom") : baseDir;
            string arm9 = Path.Combine(baseDir, "arm9.bin");
            string header = Path.Combine(meta, dsRom ? "header.yaml" : "header.bin");
            (string Name, object Value)[] previous =
            {
                (nameof(RomInfo.IsHgEngineBaseProject), RomInfo.IsHgEngineBaseProject),
                (nameof(RomInfo.HgEngineDsRomMetaDir), RomInfo.HgEngineDsRomMetaDir),
                (nameof(RomInfo.DspreDir), RomInfo.DspreDir),
                (nameof(RomInfo.WorkDir), RomInfo.WorkDir),
                (nameof(RomInfo.Arm9Path), RomInfo.Arm9Path),
                (nameof(RomInfo.HeaderPath), RomInfo.HeaderPath),
            };
            try
            {
                Directory.CreateDirectory(baseDir);
                Directory.CreateDirectory(project);
                Directory.CreateDirectory(meta);
                File.WriteAllText(arm9, "first ARM9");
                File.WriteAllText(header, "header metadata");
                if (dsRom) File.WriteAllText(Path.Combine(meta, "config.yaml"), "header: header.yaml");
                typeof(RomInfo).GetProperty(nameof(RomInfo.IsHgEngineBaseProject)).SetValue(null, true);
                typeof(RomInfo).GetProperty(nameof(RomInfo.HgEngineDsRomMetaDir)).SetValue(null, dsRom ? meta : null);
                typeof(RomInfo).GetProperty(nameof(RomInfo.DspreDir)).SetValue(null, project);
                typeof(RomInfo).GetProperty(nameof(RomInfo.WorkDir)).SetValue(null, baseDir);
                typeof(RomInfo).GetProperty(nameof(RomInfo.Arm9Path)).SetValue(null, arm9);
                RomInfo.HeaderPath = header;

                // A copy from an older session must not keep old ROM data.
                string linkedArm9 = Path.Combine(project, "arm9.bin");
                File.WriteAllText(linkedArm9, "stale ARM9");
                RotomTool.EnsureProjectMetadata();
                RotomTool.EnsureProjectMetadata();
                Assert.Equal("first ARM9", File.ReadAllText(linkedArm9));
                Assert.Equal("header metadata", File.ReadAllText(Path.Combine(project, Path.GetFileName(header))));
                if (dsRom) Assert.Equal("header: header.yaml", File.ReadAllText(Path.Combine(project, "config.yaml")));

                string target = new FileInfo(linkedArm9).LinkTarget;
                if (target != null)
                {
                    Assert.False(Path.IsPathRooted(target));
                    File.Delete(linkedArm9);
                    File.CreateSymbolicLink(linkedArm9, "missing-arm9.bin");
                    RotomTool.EnsureProjectMetadata();
                    Assert.Equal("first ARM9", File.ReadAllText(linkedArm9));
                }

                File.Delete(arm9);
                File.WriteAllText(arm9, "replacement ARM9");
                if (target != null) Assert.Equal("replacement ARM9", File.ReadAllText(linkedArm9));
                RotomTool.EnsureProjectMetadata();
                Assert.Equal("replacement ARM9", File.ReadAllText(linkedArm9));

                typeof(RomInfo).GetProperty(nameof(RomInfo.DspreDir)).SetValue(null, baseDir);
                RotomTool.EnsureProjectMetadata();
                Assert.Equal("replacement ARM9", File.ReadAllText(arm9));
                typeof(RomInfo).GetProperty(nameof(RomInfo.DspreDir)).SetValue(null, project);

                RomInfo.IsHgEngineBaseProject = false;
                File.Delete(linkedArm9);
                RotomTool.EnsureProjectMetadata();
                Assert.False(File.Exists(linkedArm9));
                Assert.Equal("replacement ARM9", File.ReadAllText(arm9));
            }
            finally
            {
                foreach ((string name, object value) in previous)
                    typeof(RomInfo).GetProperty(name).SetValue(null, value);
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
    }
}
