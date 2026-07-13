using System;
using System.Diagnostics;
using System.IO;
using DSPRE;
using Xunit;

namespace DSPRE.Tests
{
    public class ToolProcessTests
    {
        [Fact]
        public void ConfigureToolStartInfo_PrefersNativeToolOrUsesWineForExe()
        {
            var startInfo = new ProcessStartInfo { Arguments = "--version" };

            Assert.True(DSUtils.ConfigureToolStartInfo(startInfo, "apicula"));

            if (OperatingSystem.IsWindows())
            {
                Assert.EndsWith(".exe", startInfo.FileName, StringComparison.OrdinalIgnoreCase);
                Assert.Equal("--version", startInfo.Arguments);
            }
            else
            {
                Assert.Equal("wine", startInfo.FileName);
                Assert.Equal("-all", startInfo.Environment["WINEDEBUG"]);
                Assert.Contains("apicula.exe", startInfo.Arguments, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("--version", startInfo.Arguments);
            }
        }

        [Fact]
        public void ConfigureToolStartInfo_UsesNativeBinaryWhenAvailable()
        {
            var startInfo = new ProcessStartInfo();

            Assert.True(DSUtils.ConfigureToolStartInfo(startInfo, "dsrom"));

            Assert.Equal(OperatingSystem.IsWindows() ? "dsrom.exe" : "dsrom", Path.GetFileName(startInfo.FileName));
        }

        [Fact]
        public void ConfigureToolStartInfo_PreservesArgumentListWhenUsingWine()
        {
            var startInfo = new ProcessStartInfo();
            startInfo.ArgumentList.Add("--version");
            startInfo.ArgumentList.Add("/home/user/input file.nsbmd");

            Assert.True(DSUtils.ConfigureToolStartInfo(startInfo, "apicula"));

            if (OperatingSystem.IsWindows())
            {
                Assert.EndsWith(".exe", startInfo.FileName, StringComparison.OrdinalIgnoreCase);
                Assert.Equal("--version", startInfo.ArgumentList[0]);
            }
            else
            {
                Assert.Equal("wine", startInfo.FileName);
                Assert.Equal("-all", startInfo.Environment["WINEDEBUG"]);
                Assert.EndsWith("apicula.exe", startInfo.ArgumentList[0], StringComparison.OrdinalIgnoreCase);
                Assert.Equal("--version", startInfo.ArgumentList[1]);
                Assert.Equal("Z:\\home\\user\\input file.nsbmd", startInfo.ArgumentList[2]);
                Assert.Empty(startInfo.Arguments);
            }
        }

        [Fact]
        public void ConfigureToolStartInfo_ReturnsFalseForMissingTool()
        {
            var startInfo = new ProcessStartInfo();

            Assert.False(DSUtils.ConfigureToolStartInfo(startInfo, "tool-that-does-not-exist"));
            Assert.Contains("not found", DSUtils.ToolAvailabilityError("tool-that-does-not-exist"),
                StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ConfigureToolStartInfo_ConvertsUnixPathsWhenUsingWine()
        {
            var startInfo = new ProcessStartInfo
            {
                Arguments = "convert \"/home/user/input file.nsbmd\" --output \"/tmp/output folder\""
            };

            Assert.True(DSUtils.ConfigureToolStartInfo(startInfo, "apicula"));

            if (OperatingSystem.IsWindows()) return;

            Assert.Equal("wine", startInfo.FileName);
            Assert.Contains("\"Z:\\home\\user\\input file.nsbmd\"", startInfo.Arguments);
            Assert.Contains("\"Z:\\tmp\\output folder\"", startInfo.Arguments);
        }
    }
}
