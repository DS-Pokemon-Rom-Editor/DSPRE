using System.IO;
using System.Linq;

namespace DSPRE.HgEngine
{
    /// <summary>Loads one entry from the checkout's source file, so an editor shows the source, not the last
    /// build.</summary>
    public static class HgEngineEntrySource
    {
        public static bool TryLoad(HgEngineDomain domain, int id, out HgEngineSourceBlock entry, out string error) =>
            TryLoad(domain, id, false, out entry, out error);

        /// <summary>Same as <see cref="TryLoad(HgEngineDomain, int, out HgEngineSourceBlock, out string)"/>, for an editor
        /// that will save this entry. The save compares against what this loads.</summary>
        public static bool TryLoadForEditing(HgEngineDomain domain, int id, out HgEngineSourceBlock entry, out string error) =>
            TryLoad(domain, id, true, out entry, out error);

        /// <summary>Returns the entry an editor last loaded, or the file's copy if none has.</summary>
        internal static HgEngineSourceBlock Basis(HgEngineDomain domain, int id, HgEngineSourceBlock disk)
        {
            HgEngineDomainInfo info = HgEngineDomains.All.FirstOrDefault(d => d.Domain == domain);
            if (info == null || !HgEngineDesignators.TryResolve(domain, id, out string designator)) return disk;
            string path = Path.Combine(HgEngineProject.RepoPathUnc, info.SourceFileRelPath.Replace('/', Path.DirectorySeparatorChar));
            return HgEngineLoadedEntries.Get(path, designator) ?? disk;
        }

        private static bool TryLoad(HgEngineDomain domain, int id, bool forEditing, out HgEngineSourceBlock entry, out string error)
        {
            entry = default;
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout is linked."; return false; }

            HgEngineDomainInfo info = HgEngineDomains.All.FirstOrDefault(d => d.Domain == domain);
            if (info == null) { error = $"{domain} has no source file."; return false; }
            if (!HgEngineDesignators.TryResolve(domain, id, out string designator))
            { error = $"No source name was found for {domain} {id}."; return false; }

            string path = Path.Combine(HgEngineProject.RepoPathUnc, info.SourceFileRelPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) { error = $"{path} is missing from the checkout."; return false; }

            string text = HgEngineFileCache.GetText(path);
            if (!HgEngineSourcePatcher.TryFindEntry(text, designator, out int open, out int close))
            { error = $"{designator} isn't in {info.SourceFileRelPath}."; return false; }

            entry = new HgEngineSourceBlock(text.Substring(open, close - open + 1));
            if (forEditing) HgEngineLoadedEntries.Store(path, designator, entry.Raw);
            return true;
        }
    }
}
