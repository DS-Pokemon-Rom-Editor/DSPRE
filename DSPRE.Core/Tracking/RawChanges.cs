using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace DSPRE.Tracking
{
    /// <summary>Changes kept as whole files next to the JSON, each with the SHA-1 it replaced.</summary>
    public static class RawChanges
    {
        public const string Key = "files";

        /// <summary>Adds a "files" entry for each file that differs; false when none do.</summary>
        public static bool Export(JsonObject changes, RecordFiles into, IReadOnlyList<TrackedFile> files, Func<TrackedFile, byte[]> baseBytes,
            Func<TrackedFile, byte[]> nowBytes, Func<TrackedFile, string> extension)
        {
            JsonObject entries = new JsonObject();
            foreach (TrackedFile file in files)
            {
                byte[] before = baseBytes(file), now = nowBytes(file);
                if (before == null ? now == null : now != null && before.AsSpan().SequenceEqual(now)) continue;
                string suffix = Suffix(file, files.Count, extension(file));
                JsonObject entry = new JsonObject();
                if (now != null)
                {
                    entry["file"] = suffix;
                    into.Sidecars[suffix] = now;
                }
                entry[ObjectDiff.Was] = before == null ? null : ChangesJson.Sha1(before);
                entry[ObjectDiff.Now] = now == null ? null : ChangesJson.Sha1(now);
                entries[file.Key] = entry;
            }
            if (entries.Count == 0) return false;
            changes[Key] = entries;
            return true;
        }

        public static void Apply(JsonObject entries, RecordFiles change, Func<TrackedFile, byte[]> start, string record,
            Func<Conflict, bool> resolve, Dictionary<TrackedFile, byte[]> result)
        {
            foreach (KeyValuePair<string, JsonNode> entry in entries)
            {
                TrackedFile file = new TrackedFile(entry.Key);
                string was = entry.Value[ObjectDiff.Was]?.GetValue<string>(), now = entry.Value[ObjectDiff.Now]?.GetValue<string>();
                byte[] current = start(file);
                string currentSha = current == null ? null : ChangesJson.Sha1(current);
                bool take = currentSha == was || currentSha == now
                            || resolve(new Conflict(entry.Key, was, now, currentSha) { Record = record });
                if (!take) continue;
                if (now == null)
                {
                    result[file] = null;
                    continue;
                }
                string suffix = entry.Value["file"]?.GetValue<string>();
                if (suffix == null || !change.Sidecars.TryGetValue(suffix, out byte[] bytes))
                    throw new InvalidOperationException($"{record}: the file for {entry.Key} is missing from the changes folder.");
                if (ChangesJson.Sha1(bytes) != now)
                    throw new InvalidOperationException($"{record}: the file for {entry.Key} doesn't match the SHA-1 its JSON names.");
                result[file] = bytes;
            }
        }

        /// <summary>What follows the record's name in a raw file's name: ".bin" for a one-file record, ".TrainerParty-0012.bin" otherwise.</summary>
        public static string Suffix(TrackedFile file, int fileCount, string extension)
        {
            string ext = extension ?? ".bin";
            if (fileCount <= 1) return ext;
            string name = new string(file.Key.Select(c => char.IsLetterOrDigit(c) || c == '.' || c == '_' ? c : '-').ToArray()).Trim('-');
            return "." + name + ext;
        }
    }
}
