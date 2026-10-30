using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DSPRE.Tracking
{
    /// <summary>
    /// .dspre-sync.json in the project. It holds each ROM file as last exported or applied, and each changes folder
    /// file as DSPRE last wrote or applied it. git never tracks it.
    /// </summary>
    public sealed class SyncState
    {
        public const string FileName = ".dspre-sync.json";

        public sealed class FileMark
        {
            public long Size { get; set; }
            public long Ticks { get; set; }
            public string Sha1 { get; set; }
        }

        public sealed class CodeWrite
        {
            public long At { get; set; }
            public int Length { get; set; }
            public string By { get; set; }
        }

        public string Repo { get; set; }
        public string BaseSha1 { get; set; }
        public string ProjectId { get; set; }
        /// <summary>The DSPRE build that last wrote this project's records.</summary>
        public string Writer { get; set; }
        public Dictionary<string, FileMark> Files { get; set; } = new Dictionary<string, FileMark>(StringComparer.Ordinal);
        public Dictionary<string, string> RepoFiles { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
        public Dictionary<string, List<CodeWrite>> CodeWrites { get; set; } = new Dictionary<string, List<CodeWrite>>(StringComparer.Ordinal);

        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions { WriteIndented = false };

        public static string PathIn(string projectDir) => Path.Combine(projectDir, FileName);

        public static SyncState Load(string projectDir)
        {
            string path = PathIn(projectDir);
            if (!File.Exists(path)) return new SyncState();
            try { return JsonSerializer.Deserialize<SyncState>(File.ReadAllText(path), Options) ?? new SyncState(); }
            catch (Exception ex) when (ex is IOException || ex is JsonException)
            {
                AppLogger.Warn("Track changes: the sync state couldn't be read, so every file is checked again: " + ex.Message);
                return new SyncState();
            }
        }

        public void Save(string projectDir) => PlainFiles.WriteText(PathIn(projectDir), JsonSerializer.Serialize(this, Options));
    }
}
