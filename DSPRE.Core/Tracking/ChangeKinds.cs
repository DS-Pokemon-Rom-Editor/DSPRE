using System;
using System.Collections.Generic;
using System.Linq;
using DSPRE.Tracking.Kinds;
using static DSPRE.RomInfo;

namespace DSPRE.Tracking
{
    /// <summary>Every kind for the open game, most specific first, ending with whole-file kinds that take anything left.</summary>
    public sealed class ChangeKinds
    {
        public IReadOnlyList<ChangeKind> All { get; }

        private readonly Dictionary<string, (ChangeKind Kind, string Id)> _owners = new Dictionary<string, (ChangeKind, string)>(StringComparer.Ordinal);

        public ChangeKinds(IEnumerable<ChangeKind> specific)
        {
            List<ChangeKind> all = specific.ToList();
            HashSet<DirNames> covered = new HashSet<DirNames>();
            foreach (DirNames dir in GameDirs.Keys)
            {
                TrackedFile probe = TrackedFile.Member(dir, 0);
                if (all.Any(k => k.Owns(probe, out _))) covered.Add(dir);
            }
            foreach (DirNames dir in GameDirs.Keys.Where(d => !covered.Contains(d)).OrderBy(d => d.ToString(), StringComparer.Ordinal))
                all.Add(new GraphicKind(dir, KindCatalog.IsGraphics(dir)));
            all.Add(new TreeFileKind());
            All = all;
        }

        public static ChangeKinds ForOpenGame() => new ChangeKinds(KindCatalog.Specific());

        public bool TryOwner(TrackedFile file, out ChangeKind kind, out string id)
        {
            if (_owners.TryGetValue(file.Key, out (ChangeKind Kind, string Id) known))
            {
                kind = known.Kind;
                id = known.Id;
                return kind != null;
            }
            foreach (ChangeKind k in All)
            {
                if (!k.Owns(file, out id)) continue;
                kind = k;
                _owners[file.Key] = (k, id);
                return true;
            }
            _owners[file.Key] = (null, null);
            kind = null;
            id = null;
            return false;
        }

        /// <summary>The kind and record a file of the changes folder belongs to, from its path.</summary>
        public bool TryRecordAt(string repoRelativePath, out ChangeKind kind, out string id)
        {
            string path = repoRelativePath.Replace('\\', '/');
            string dir = path.Contains('/') ? path.Substring(0, path.LastIndexOf('/')) : "";
            foreach (ChangeKind k in All)
            {
                string stem;
                if (k is TreeFileKind)
                {
                    if (!path.StartsWith(k.Folder + "/", StringComparison.Ordinal)) continue;
                    stem = path.Substring(k.Folder.Length + 1);
                    if (stem.EndsWith(".json", StringComparison.Ordinal)) stem = stem.Substring(0, stem.Length - 5);
                }
                else
                {
                    if (dir != k.Folder) continue;
                    stem = path.Substring(dir.Length + 1);
                    int dot = stem.IndexOf('.');
                    if (dot >= 0) stem = stem.Substring(0, dot);
                }
                if (k.TryParseStem(stem, out id))
                {
                    kind = k;
                    return true;
                }
            }
            kind = null;
            id = null;
            return false;
        }
    }
}
