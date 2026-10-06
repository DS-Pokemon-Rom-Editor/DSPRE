using DSPRE;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// Graphics and other assets a linked checkout builds from its own files. The source is what a
    /// picture should be edited as, the same way the Pokemon and trainer sprite editors already work,
    /// so importing over the ROM's copy is refused and the file to edit is named instead.
    /// </summary>
    public static class HgEngineSourceAssets
    {
        /// <summary>The checkout's source file for one archive member, or null when it has none.</summary>
        public static HgEngineOwnedFile SourceFor(RomInfo.DirNames dir, int index)
        {
            HgEngineOwnedFile file = HgEngineOwnedFiles.Get(HgEngineOwnedFiles.ArchiveOf(dir), index);
            return file?.Ownership == HgEngineOwnership.Asset ? file : null;
        }

        /// <summary>
        /// The checkout file the build copies over this member unchanged, or null when there is none. Such a member
        /// is read from that file and saved into it as well, so what DSPRE shows and writes is what the build uses.
        /// </summary>
        public static HgEngineOwnedFile VerbatimSourceFor(string archive, int index)
        {
            if (!HgEngineProject.IsActive || archive == null) return null;
            HgEngineOwnedFile file = HgEngineOwnedFiles.Get(archive, index);
            if (file?.FullPath == null || !file.Rule.CopiesSourceVerbatim) return null;
            // Only a file named exactly like a member lands as one; anything with an extension is copied in beside them.
            if (!System.Text.RegularExpressions.Regex.IsMatch(System.IO.Path.GetFileName(file.FullPath), @"^\d+_\d+$")) return null;
            return System.IO.File.Exists(file.FullPath) ? file : null;
        }

        /// <summary>The member's bytes from its checkout file, or null when it has none.</summary>
        public static byte[] ReadVerbatim(string archive, int index)
        {
            HgEngineOwnedFile file = VerbatimSourceFor(archive, index);
            if (file == null) return null;
            try { return System.IO.File.ReadAllBytes(file.FullPath); }
            catch (System.Exception ex) when (ex is System.IO.IOException || ex is System.UnauthorizedAccessException)
            {
                AppLogger.Error($"HgEngineSourceAssets: could not read {file.RelPath}: {ex.Message}");
                return null;
            }
        }

        /// <summary>Saves a member into its checkout file too. False when it has none; a failed write throws.</summary>
        public static bool WriteVerbatim(string archive, int index, byte[] bytes)
        {
            HgEngineOwnedFile file = VerbatimSourceFor(archive, index);
            if (file == null) return false;
            System.IO.File.WriteAllBytes(file.FullPath, bytes);
            return true;
        }

        private static readonly System.Text.RegularExpressions.Regex ConvertedPalette = new(
            @"\$\(GFX\)\s+\$\((\w+)_DEPENDENCIES_DIR\)/(\S+\.pal)\s+\$\(\1_DIR\)/\d+_(\d+)\.NCLR");

        /// <summary>
        /// The JASC palette the build converts into this member (as otherpoke's Arceus Fairy palettes), or null. The
        /// rest of such an archive comes from the ROM, so only these members have to be saved into the checkout.
        /// </summary>
        public static string ConvertedPaletteFor(RomInfo.DirNames dir, int member)
        {
            if (!HgEngineProject.IsActive) return null;
            string archive = HgEngineOwnedFiles.ArchiveOf(dir);
            string narcs = System.IO.Path.Combine(HgEngineProject.RepoPathUnc, HgEngineOwnedFiles.MakeFragmentRelPath);
            if (archive == null || !System.IO.File.Exists(narcs)) return null;
            string text = HgEngineFileCache.GetText(narcs);
            foreach (System.Text.RegularExpressions.Match m in ConvertedPalette.Matches(text))
            {
                if (int.Parse(m.Groups[3].Value) != member) continue;
                HgEngineRule rule = HgEngineOwnedFiles.RuleForArchive(archive);
                if (rule == null || rule.Variable != m.Groups[1].Value || rule.SourceDirRelPath == null) continue;
                return System.IO.Path.Combine(HgEngineProject.RepoPathUnc, (rule.SourceDirRelPath + "/" + m.Groups[2].Value).Replace('/', System.IO.Path.DirectorySeparatorChar));
            }
            return null;
        }

        // Members in the copy DSPRE edits, else in the packed archive; -1 when neither can be read.
        private static int MemberCount(RomInfo.DirNames dir)
        {
            if (RomInfo.gameDirs == null || !RomInfo.gameDirs.TryGetValue(dir, out (string packedDir, string unpackedDir) dirs)) return -1;
            try
            {
                if (System.IO.Directory.Exists(dirs.unpackedDir))
                {
                    int n = System.IO.Directory.GetFiles(dirs.unpackedDir).Length;
                    if (n > 0) return n;
                }
                if (System.IO.File.Exists(dirs.packedDir)) return new Editors.Utils.NarcReader(dirs.packedDir).Entrys;
            }
            catch (System.Exception ex) when (ex is System.IO.IOException || ex is System.UnauthorizedAccessException)
            {
                AppLogger.Error($"HgEngineSourceAssets: {dir} member count: {ex.Message}");
            }
            return -1;
        }

        /// <summary>Why a picture cannot go back into this archive member, or null when it can.</summary>
        public static string CannotImportBecause(RomInfo.DirNames dir, int index)
        {
            if (!HgEngineProject.IsActive) return null;

            // Saved into its source file as well, so the picture survives the build.
            string archive = HgEngineOwnedFiles.ArchiveOf(dir);
            if (VerbatimSourceFor(archive, index) != null) return null;
            if (HgEngineBuiltPngs.For(archive, index, MemberCount(dir)) != null) return null;

            HgEngineOwnedFile file = SourceFor(dir, index);
            if (file != null)
            {
                return $"hg-engine builds this from {file.RelPath} in your checkout, so a picture put "
                     + "here would be replaced on the next compile. Edit that file instead.";
            }

            // The rule may still own the archive even when this member has no source of its own.
            HgEngineRule rule = HgEngineOwnedFiles.RuleForArchive(HgEngineOwnedFiles.ArchiveOf(dir));
            if (rule == null || rule.Ownership != HgEngineOwnership.Asset || !rule.ReplacesWholeArchive)
                return null;

            return $"hg-engine builds the {rule.Label} from {rule.SourceDirRelPath} on every compile, so "
                 + "a picture put here would be replaced.";
        }
    }
}
