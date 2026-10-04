using System;
using System.Collections.Generic;
using System.Linq;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// Tools for working on hg-engine itself rather than on a hack, switched on by starting DSPRE with
    /// <c>--hge-dev</c>. They stay out of the menus otherwise.
    /// </summary>
    public static class HgEngineDev
    {
        public const string Switch = "--hge-dev";

        public static bool Enabled { get; private set; }

        /// <summary>Reads the switch off the command line. Call this once, before any window opens.</summary>
        public static void ReadFrom(IEnumerable<string> args) =>
            Enabled = args != null && args.Any(a => string.Equals(a, Switch, StringComparison.OrdinalIgnoreCase));
    }
}
