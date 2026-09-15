using System.IO;
using System.Linq;

namespace DSPRE.HgEngine
{
    /// <summary>Loads one entry of a source-owned domain from the checkout's own file, so an editor shows
    /// what the source says rather than the last built copy.</summary>
    public static class HgEngineEntrySource
    {
        public static bool TryLoad(HgEngineDomain domain, int id, out HgEngineSourceBlock entry, out string error)
        {
            entry = default;
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout is linked."; return false; }

            var info = HgEngineDomains.All.FirstOrDefault(d => d.Domain == domain);
            if (info == null) { error = $"{domain} has no source file."; return false; }
            if (!HgEngineDesignators.TryResolve(domain, id, out string designator))
            { error = $"No source name was found for {domain} {id}."; return false; }

            string path = Path.Combine(HgEngineProject.RepoPathUnc, info.SourceFileRelPath.Replace('/', '\\'));
            if (!File.Exists(path)) { error = $"Source file not found: {path}"; return false; }

            string text = HgEngineFileCache.GetText(path);
            if (!HgEngineSourcePatcher.TryFindEntry(text, designator, out int open, out int close))
            { error = $"{designator} is not in {info.SourceFileRelPath}."; return false; }

            entry = new HgEngineSourceBlock(text.Substring(open, close - open + 1));
            return true;
        }
    }
}
