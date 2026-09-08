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

        /// <summary>Why a picture cannot go back into this archive member, or null when it can.</summary>
        public static string CannotImportBecause(RomInfo.DirNames dir, int index)
        {
            if (!HgEngineProject.IsActive) return null;

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
