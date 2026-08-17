using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.HgEngine
{
    /// <summary>One field write for <see cref="HgEngineWriter.TryWriteFields"/>: where in the entry
    /// (dotted/positional path) and the exact C source literal to put there (already formatted:
    /// a plain number, or a resolved symbolic constant like "TYPE_GRASS").</summary>
    public readonly struct HgEngineFieldWrite
    {
        public IReadOnlyList<FieldPathSegment> Path { get; }
        public string ValueLiteral { get; }
        public HgEngineFieldWrite(IReadOnlyList<FieldPathSegment> path, string valueLiteral)
        {
            Path = path;
            ValueLiteral = valueLiteral;
        }
    }

    /// <summary>Ties designator resolution + the anchored patcher together into the one call every
    /// editor's Save needs: write these curated fields of this entry back to hg-engine source. A field
    /// that can't be located is reported back rather than silently dropped; every other field in the
    /// same call still gets written.</summary>
    public static class HgEngineWriter
    {
        /// <param name="allowInsert">When true, an absent field is inserted into its parent block instead
        /// of being reported unresolved. Used by Trainers' optional per-mon fields, which only exist once
        /// their gating flag is first turned on.</param>
        public static bool TryWriteFields(HgEngineDomain domain, int id, IEnumerable<HgEngineFieldWrite> fields, out List<string> unresolvedFields, out string error, bool allowInsert = false)
        {
            unresolvedFields = new List<string>();
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout linked."; return false; }

            var domainInfo = HgEngineDomains.All.FirstOrDefault(d => d.Domain == domain);
            if (domainInfo == null) { error = $"Unknown hg-engine domain: {domain}"; return false; }

            string sourcePath = Path.Combine(HgEngineProject.RepoPathUnc, domainInfo.SourceFileRelPath.Replace('/', '\\'));
            if (!File.Exists(sourcePath)) { error = $"Source file not found: {sourcePath}"; return false; }

            if (!HgEngineDesignators.TryResolve(domain, id, out string designator))
            {
                error = $"Could not resolve a source designator for id {id} in {domain}.";
                return false;
            }

            string text = HgEngineFileCache.GetText(sourcePath);
            bool anyWritten = false;
            foreach (var field in fields)
            {
                bool ok = allowInsert
                    ? HgEngineSourcePatcher.TryUpsertField(ref text, designator, field.Path, field.ValueLiteral)
                    : HgEngineSourcePatcher.TryReplaceField(ref text, designator, field.Path, field.ValueLiteral);
                if (ok)
                    anyWritten = true;
                else
                    unresolvedFields.Add(string.Concat(field.Path.Select(p => p.ToString())));
            }
            if (anyWritten) File.WriteAllText(sourcePath, text);
            return anyWritten || unresolvedFields.Count == 0;
        }
    }
}
