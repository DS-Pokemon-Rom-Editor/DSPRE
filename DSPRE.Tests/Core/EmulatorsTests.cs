using System;
using System.IO;
using System.Linq;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>Which emulator a program is, and the command that opens a ROM in it.</summary>
    public class EmulatorsTests
    {
        [Theory]
        [InlineData(@"C:\Emulators\BizHawk-2.10-win-x64\EmuHawk.exe", EmulatorKind.BizHawk)]
        [InlineData(@"C:\Emulators\DeSmuME_0.9.13_x64.exe", EmulatorKind.DeSmuME)]
        [InlineData("/usr/bin/desmume", EmulatorKind.DeSmuME)]
        [InlineData(@"C:\Emulators\melonDS\melonDS.exe", EmulatorKind.MelonDS)]
        public void AProgramIsRecognisedByItsName(string path, EmulatorKind expected)
            => Assert.Equal(expected, Emulators.Guess(path));

        [Fact]
        public void AnUnknownProgramIsNotGuessed()
            => Assert.Null(Emulators.Guess(@"C:\Tools\notepad.exe"));

        [Fact]
        public void TheRomIsTheOnlyArgumentAndTheEmulatorRunsFromItsOwnFolder()
        {
            string emulator = Path.Combine(Path.GetTempPath(), "Some Emulator", "EmuHawk.exe");
            string rom = Path.Combine(Path.GetTempPath(), "My Hack (DSPRE build).nds");

            var info = Emulators.StartInfo(emulator, rom);

            Assert.Equal(emulator, info.FileName);
            Assert.Equal(new[] { rom }, info.ArgumentList.ToArray());
            Assert.Equal(Path.GetDirectoryName(emulator), info.WorkingDirectory);
            Assert.False(info.UseShellExecute);
        }

        [Fact]
        public void AMissingEmulatorOrRomIsReportedWithoutStartingAnything()
        {
            string missing = Path.Combine(Path.GetTempPath(), $"dspre-no-such-{Guid.NewGuid():N}.exe");
            Assert.Contains("was not found", Emulators.Launch(EmulatorKind.MelonDS, missing, missing));

            string emulator = Path.GetTempFileName();
            try
            {
                Assert.Contains("The ROM was not found", Emulators.Launch(EmulatorKind.MelonDS, emulator, missing));
            }
            finally { File.Delete(emulator); }
        }
    }
}
