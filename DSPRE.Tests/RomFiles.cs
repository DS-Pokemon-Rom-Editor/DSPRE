using System.IO;
using System.Linq;
using System.Threading;

namespace DSPRE.Tests
{
    /// <summary>
    /// Reading a folder the tests may still be filling.
    ///
    /// The suite runs its two target frameworks side by side against the same unpacked project, so on
    /// the first run after a build one process can be part way through unpacking an archive while the
    /// other starts counting what is in it. That produced failures saying a sweep had only covered a
    /// fraction of the files, which had nothing to do with what was being tested. Waiting until two
    /// readings in a row agree on the count removes it.
    /// </summary>
    internal static class RomFiles
    {
        public static string[] Settled(string dir)
        {
            if (!Directory.Exists(dir)) return System.Array.Empty<string>();

            int last = -1;
            for (int tries = 0; tries < 40; tries++)
            {
                var now = Directory.GetFiles(dir);
                if (now.Length > 0 && now.Length == last) return now.OrderBy(x => x).ToArray();
                last = now.Length;
                Thread.Sleep(250);
            }
            return Directory.GetFiles(dir).OrderBy(x => x).ToArray();
        }
    }
}
