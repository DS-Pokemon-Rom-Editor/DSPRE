using System;
using System.IO;

namespace DSPRE.HgEngine
{
    /// <summary>Which extracted trees a checkout has, and therefore how DSPRE can read it.</summary>
    public enum HgEngineBaseLayout
    {
        /// <summary>Nothing extracted, usually because the checkout has never been built.</summary>
        None,

        /// <summary>base/ only: root/, arm9.bin, overlay/overlay_NNNN.bin, a synthesised overlay table.</summary>
        Ndstool,

        /// <summary>base_dsrom/ alongside it: a ds-rom project, which DSPRE reads natively.</summary>
        DsRom,
    }

    /// <summary>
    /// The extracted ROM a checkout builds into, which is a separate tree from the DSPRE project and is
    /// what the build actually reads and writes.
    ///
    /// With ROM_TOOL=dsrom the checkout keeps two: base_dsrom/ is a ds-rom project, and base/ is the flat
    /// mirror the makefile, armips and the datagen tools all still expect. The build copies base/ over
    /// base_dsrom/ when it packs, so base/ stays the one to write to and base_dsrom/ is where the clean
    /// metadata lives.
    /// </summary>
    public static class HgEngineBase
    {
        public const string BaseDirName = "base";
        public const string DsRomDirName = "base_dsrom";
        public const string BridgeRelPath = "scripts/dsrom_bridge.py";

        private static string Root => HgEngineProject.IsLinked ? HgEngineProject.RepoRootWindows : null;

        public static string BaseDir => Root == null ? null : Path.Combine(Root, BaseDirName);

        public static string DsRomDir => Root == null ? null : Path.Combine(Root, DsRomDirName);

        /// <summary>Whether the checkout can build through ds-rom at all, built or not.</summary>
        public static bool SupportsDsRom => SupportsDsRomAt(Root);

        internal static bool SupportsDsRomAt(string root)
        {
            if (root == null) return false;
            return File.Exists(Path.Combine(root, BridgeRelPath.Replace('/', Path.DirectorySeparatorChar)))
                || File.Exists(Path.Combine(root, DsRomDirName, "config.yaml"));
        }

        public static HgEngineBaseLayout Layout => LayoutOf(Root);

        internal static HgEngineBaseLayout LayoutOf(string root)
        {
            if (root == null) return HgEngineBaseLayout.None;

            if (File.Exists(Path.Combine(root, DsRomDirName, "config.yaml")))
                return HgEngineBaseLayout.DsRom;

            string baseDir = Path.Combine(root, BaseDirName);
            return Directory.Exists(Path.Combine(baseDir, "root")) || File.Exists(Path.Combine(baseDir, "arm9.bin"))
                ? HgEngineBaseLayout.Ndstool
                : HgEngineBaseLayout.None;
        }

        public static bool Exists => Layout != HgEngineBaseLayout.None;

        /// <summary>
        /// Why the ds-rom path is unavailable, or null when it is available. Shown when DSPRE would
        /// rather have read the checkout as a ds-rom project.
        /// </summary>
        public static string DsRomUnavailableNotice => NoticeFor(Root);

        internal static string NoticeFor(string root)
        {
            if (root == null) return "No hg-engine checkout is linked.";
            if (SupportsDsRomAt(root) && LayoutOf(root) == HgEngineBaseLayout.DsRom) return null;

            if (!SupportsDsRomAt(root))
            {
                return "This checkout is not a ds-rom hg-engine. DSPRE will read its flat base/ tree "
                     + "instead, which carries no overlay table or header of its own. Build with "
                     + "ROM_TOOL=dsrom to get one.";
            }
            return "This checkout can build through ds-rom but has not done so yet, so there is no "
                 + "base_dsrom/ to read. Run a build with ROM_TOOL=dsrom.";
        }

        /// <summary>
        /// Where DSPRE's own writes belong. Always base/, because packing copies base/ over base_dsrom/
        /// and anything written to the ds-rom tree would be replaced by the next build.
        /// </summary>
        public static string WriteRoot => BaseDir;

        /// <summary>
        /// The ds-rom project to read metadata from, or null when the checkout has none. This is the
        /// layout DSPRE already opens, so its existing readers work against it unchanged.
        /// </summary>
        public static string DsRomProject => Layout == HgEngineBaseLayout.DsRom ? DsRomDir : null;

        /// <summary>The ROM filesystem root, which is what an archive path like "a/0/2/7" hangs off.</summary>
        public static string FileSystemRoot => FileSystemRootOf(Root, Layout);

        internal static string FileSystemRootOf(string root, HgEngineBaseLayout layout)
        {
            if (root == null) return null;
            return layout switch
            {
                // base/root is what the build reads, in both layouts.
                HgEngineBaseLayout.Ndstool or HgEngineBaseLayout.DsRom
                    => Path.Combine(root, BaseDirName, "root"),
                _ => null,
            };
        }

        /// <summary>Where one of the checkout's own archives sits, given the path its targets are written in.</summary>
        public static string ArchivePath(string archive)
        {
            string root = FileSystemRoot;
            if (root == null || string.IsNullOrEmpty(archive)) return null;
            return Path.Combine(root, archive.Replace('/', Path.DirectorySeparatorChar));
        }

        public static string Arm9Path => Root == null ? null : Path.Combine(Root, BaseDirName, "arm9.bin");

        public static string OverlayPath(int overlayNumber) =>
            Root == null ? null
            : Path.Combine(Root, BaseDirName, "overlay", $"overlay_{overlayNumber:D4}.bin");

        /// <summary>
        /// The overlay table. ds-rom keeps a real one; without it the build synthesises a flat
        /// overarm9.bin, which is the same 32-byte entries DSPRE's legacy reader already understands.
        /// </summary>
        public static string OverlayTablePath => Layout switch
        {
            HgEngineBaseLayout.DsRom => Path.Combine(DsRomDir, "arm9_overlays", "overlays.yaml"),
            HgEngineBaseLayout.Ndstool => Path.Combine(BaseDir, "overarm9.bin"),
            _ => null,
        };

        /// <summary>
        /// The ROM a build writes, which is not the one DSPRE has open. Reading its timestamp is how
        /// DSPRE can tell whether a compile has happened since an edit.
        /// </summary>
        public static string BuiltRomPath => Root == null ? null : Path.Combine(Root, "test.nds");

        public static DateTime? BuiltRomWrittenUtc
        {
            get
            {
                string path = BuiltRomPath;
                try { return path != null && File.Exists(path) ? File.GetLastWriteTimeUtc(path) : null; }
                catch (Exception ex)
                {
                    AppLogger.Error("HgEngineBase.BuiltRomWrittenUtc: " + ex.Message);
                    return null;
                }
            }
        }
    }
}
