using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// The folder reader the sweeps use, checked against the thing it exists for.
    ///
    /// A plain read of a folder another process is still unpacking into returns whatever happened to be
    /// there, which is how a sweep ends up reporting that it only covered a fraction of the files. This
    /// fills a folder slowly from another thread and requires the reader to come back with all of it.
    /// A reader that took the first reading would return roughly a tenth here, so this can fail.
    /// </summary>
    public class RomFilesTests
    {
        [Fact]
        public void ReadingAFolderThatIsStillBeingFilledWaitsForAllOfIt()
        {
            string dir = Path.Combine(Path.GetTempPath(), "dspre_settle_" + Path.GetRandomFileName());
            Directory.CreateDirectory(dir);
            try
            {
                const int total = 40;
                var filling = Task.Run(() =>
                {
                    for (int i = 0; i < total; i++)
                    {
                        File.WriteAllText(Path.Combine(dir, i.ToString("D3") + ".bin"), "x");
                        Thread.Sleep(60);
                    }
                });

                var files = RomFiles.Settled(dir);
                filling.Wait();
                Assert.Equal(total, files.Length);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void AFolderThatIsNotThereReadsAsEmptyInsteadOfThrowing()
        {
            Assert.Empty(RomFiles.Settled(Path.Combine(Path.GetTempPath(), "dspre_missing_" + Path.GetRandomFileName())));
        }
    }
}
