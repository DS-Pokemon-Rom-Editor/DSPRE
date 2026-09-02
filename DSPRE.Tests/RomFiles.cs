using System.IO;
using System.Linq;
using System.Threading;

namespace DSPRE.Tests
{
    /// <summary>Reading a folder the tests may still be filling.</summary>
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
