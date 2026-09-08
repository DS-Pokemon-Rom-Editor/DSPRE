using DSPRE.HgEngine;
using Xunit;

namespace DSPRE.Tests.Core
{
    /// <summary>
    /// The same checkout folder is spelled differently by each shell, and getting it wrong means
    /// `make -C` runs against a path that does not exist, which surfaces as a confusing build error
    /// rather than as a link problem. These are the two translations and the UNC parse they replace.
    /// </summary>
    public class HgEnginePathTranslationTests
    {
        [Theory]
        [InlineData(@"C:\msys64\home\me\git\hg-engine", "/c/msys64/home/me/git/hg-engine")]
        [InlineData(@"D:\hg-engine", "/d/hg-engine")]
        [InlineData(@"C:\", "/c")]
        public void Msys2SeesADriveUnderItsLetter(string windows, string expected)
        {
            Assert.Equal(expected, HgEngineProject.ToPosix(windows, HgEngineShell.Msys2));
        }

        [Theory]
        [InlineData(@"C:\msys64\home\me\git\hg-engine", "/mnt/c/msys64/home/me/git/hg-engine")]
        [InlineData(@"D:\hg-engine", "/mnt/d/hg-engine")]
        [InlineData(@"C:\", "/mnt/c")]
        public void WslSeesTheSameDriveUnderMnt(string windows, string expected)
        {
            // The identical folder, a different answer. This is the whole reason the shell is stored
            // with the link instead of being inferred at build time.
            Assert.Equal(expected, HgEngineProject.ToPosix(windows, HgEngineShell.Wsl));
        }

        [Theory]
        [InlineData(@"\\wsl.localhost\Ubuntu\home\me\hg-engine", "Ubuntu", "/home/me/hg-engine")]
        [InlineData(@"\\wsl$\Debian\srv\hg-engine", "Debian", "/srv/hg-engine")]
        public void AWslCheckoutKeepsItsOwnPathAndNamesItsDistro(string unc, string distro, string posix)
        {
            Assert.True(HgEngineProject.TryParseWslUncPath(unc, out string gotDistro, out string gotPosix));
            Assert.Equal(distro, gotDistro);
            Assert.Equal(posix, gotPosix);
        }

        [Theory]
        [InlineData(@"C:\msys64\home\me\git\hg-engine")]
        [InlineData(@"D:\hg-engine")]
        [InlineData("")]
        [InlineData(null)]
        public void APathOnAWindowsDriveIsNotAWslPath(string path)
        {
            // What used to reject the link outright. It still has to answer false here, or a local
            // checkout would be mistaken for a WSL one and never get asked which shell builds it.
            Assert.False(HgEngineProject.IsWslPath(path));
        }
    }
}
