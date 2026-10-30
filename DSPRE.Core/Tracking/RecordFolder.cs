using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE.Tracking.Kinds;

namespace DSPRE.Tracking
{
    /// <summary>Reads the records in a folder laid out like a changes folder: the changes folder itself, or an unpacked patch.</summary>
    public sealed class RecordFolder
    {
        public string Folder { get; }
        public ChangeKinds Kinds { get; }

        public RecordFolder(string folder, ChangeKinds kinds)
        {
            Folder = folder;
            Kinds = kinds;
        }

        /// <summary>The record's file name without extension, or null when the folder has no file for it.</summary>
        public string ExistingStem(ChangeKind kind, string id)
        {
            if (kind is TreeFileKind) return id;
            string dir = Path.Combine(Folder, kind.Folder);
            if (!Directory.Exists(dir)) return null;
            foreach (string json in Directory.EnumerateFiles(dir, "*.json"))
            {
                string stem = Path.GetFileNameWithoutExtension(json);
                if (kind.TryParseStem(stem, out string other) && other == id) return stem;
            }
            return null;
        }

        /// <summary>The record's JSON and every file kept beside it.</summary>
        public List<string> RecordFilePaths(ChangeKind kind, string stem)
        {
            string jsonPath = Path.Combine(Folder, kind.Folder, stem + ".json");
            string dir = Path.GetDirectoryName(jsonPath);
            if (!Directory.Exists(dir)) return new List<string>();
            string name = Path.GetFileName(stem);
            if (kind is TreeFileKind)
                return new[] { Path.Combine(dir, name), jsonPath }.Where(File.Exists).ToList();
            return Directory.EnumerateFiles(dir)
                .Where(p => Path.GetFileName(p) is string n && (n == name || n.StartsWith(name + ".", StringComparison.Ordinal)))
                .ToList();
        }

        public string Key(string path) => Path.GetRelativePath(Folder, path).Replace('\\', '/');

        /// <summary>Every record file, skipping the folder's own files (dot folders and top-level files).</summary>
        public IEnumerable<string> Files()
        {
            if (!Directory.Exists(Folder)) yield break;
            foreach (string file in Directory.EnumerateFiles(Folder, "*", SearchOption.AllDirectories))
            {
                string key = Key(file);
                if (key.StartsWith(".", StringComparison.Ordinal) || key.Contains("/.", StringComparison.Ordinal) || !key.Contains('/')) continue;
                yield return key;
            }
        }

        public IEnumerable<(ChangeKind Kind, string Id)> Records() =>
            Files().Select(key => Kinds.TryRecordAt(key, out ChangeKind kind, out string id) ? (kind, id) : (null, null))
                .Where(r => r.Item1 != null)
                .Distinct();

        /// <summary>The record as the folder holds it, or null when it has no file there.</summary>
        public RecordFiles Read(ChangeKind kind, string id)
        {
            string stem = ExistingStem(kind, id);
            if (stem == null) return null;
            string jsonPath = Path.Combine(Folder, kind.Folder, stem + ".json");
            if (!File.Exists(jsonPath)) return null;
            RecordFiles files = new RecordFiles { Json = ChangesJson.ReadFile(jsonPath) };
            foreach (string path in RecordFilePaths(kind, stem))
                if (path != jsonPath) files.Sidecars[Path.GetFileName(path)] = File.ReadAllBytes(path);
            return files;
        }
    }
}
