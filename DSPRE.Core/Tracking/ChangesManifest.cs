using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;

namespace DSPRE.Tracking
{
    /// <summary>dspre.json: which ROM the changes folder's changes are measured against.</summary>
    public sealed class ChangesManifest
    {
        public const string FileName = "dspre.json";
        public const int CurrentFormat = 1;

        public int Format { get; init; } = CurrentFormat;
        public string Game { get; init; }
        public string Code { get; init; }
        public int Revision { get; init; }
        public string Rom { get; init; }
        public string BaseSha1 { get; init; }

        /// <summary>Names the hack, so a ROM built from it is recognised even though its SHA-1 differs from the original's.</summary>
        public string ProjectId { get; init; }
        public string ProjectName { get; init; }

        public static string NewProjectId() => System.Guid.NewGuid().ToString("N");

        public static string PathIn(string folder) => Path.Combine(folder, FileName);

        public static bool TryRead(string folder, out ChangesManifest manifest, out string error)
        {
            manifest = null;
            error = null;
            string path = PathIn(folder);
            if (!File.Exists(path))
            {
                error = "The folder has no dspre.json, so it isn't a changes folder.";
                return false;
            }
            try
            {
                JsonObject json = ChangesJson.ReadFile(path);
                JsonObject b = json["base"] as JsonObject;
                JsonObject project = json["project"] as JsonObject;
                manifest = new ChangesManifest
                {
                    Format = json["format"]?.GetValue<int>() ?? 0,
                    Game = json["game"]?.GetValue<string>(),
                    Code = json["code"]?.GetValue<string>(),
                    Revision = json["revision"]?.GetValue<int>() ?? 0,
                    Rom = b?["rom"]?.GetValue<string>(),
                    BaseSha1 = b?["sha1"]?.GetValue<string>(),
                    ProjectId = project?["id"]?.GetValue<string>(),
                    ProjectName = project?["name"]?.GetValue<string>(),
                };
            }
            catch (System.Exception ex) when (ex is IOException || ex is System.Text.Json.JsonException || ex is InvalidDataException || ex is System.InvalidOperationException)
            {
                error = "dspre.json couldn't be read: " + ex.Message;
                return false;
            }
            if (manifest.Format > CurrentFormat)
            {
                error = "This changes folder was written by a newer DSPRE. Update DSPRE to open it.";
                return false;
            }
            if (string.IsNullOrEmpty(manifest.BaseSha1) || string.IsNullOrEmpty(manifest.Code))
            {
                error = "dspre.json doesn't say which ROM it was made from.";
                return false;
            }
            return true;
        }

        public JsonObject ToJson()
        {
            JsonObject json = new JsonObject { ["format"] = Format };
            if (ProjectId != null) json["project"] = new JsonObject { ["id"] = ProjectId, ["name"] = ProjectName };
            json["game"] = Game;
            json["code"] = Code;
            json["revision"] = Revision;
            json["base"] = new JsonObject { ["rom"] = Rom, ["sha1"] = BaseSha1 };
            return json;
        }

        public void Write(string folder) => PlainFiles.WriteBytes(PathIn(folder), ChangesJson.WriteBytes(ToJson()));

        public string Describe() => $"{Game} ({Code}, revision {Revision.ToString(CultureInfo.InvariantCulture)})";
    }
}
