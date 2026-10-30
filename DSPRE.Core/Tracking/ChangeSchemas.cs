using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace DSPRE.Tracking
{
    /// <summary>JSON Schemas for the changes folder's files, so editors can check and complete them.</summary>
    public static class ChangeSchemas
    {
        public static void Write(string folder)
        {
            Directory.CreateDirectory(folder);
            foreach (ChangeKind kind in ChangeKinds.ForOpenGame().All.GroupBy(k => k.Name).Select(g => g.First()))
            {
                byte[] bytes = ChangesJson.WriteBytes(For(kind));
                string path = Path.Combine(folder, kind.Name + ".json");
                if (File.Exists(path) && File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes)) continue;
                PlainFiles.WriteBytes(path, bytes);
            }
        }

        private static JsonObject For(ChangeKind kind) => new JsonObject
        {
            ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
            ["title"] = char.ToUpperInvariant(kind.Name[0]) + kind.Name.Substring(1).Replace('-', ' ') + " changes",
            ["type"] = "object",
            ["required"] = new JsonArray("kind", "id", "members", "op", "changes"),
            ["properties"] = new JsonObject
            {
                ["$schema"] = new JsonObject { ["type"] = "string", ["description"] = "Schema" },
                ["kind"] = new JsonObject { ["const"] = kind.Name, ["description"] = "Record type" },
                ["id"] = new JsonObject { ["type"] = new JsonArray("integer", "string"), ["description"] = "Record number" },
                ["name"] = new JsonObject { ["type"] = "string", ["description"] = "Record name" },
                ["members"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = new JsonObject { ["type"] = "string" },
                    ["description"] = "Game files changed",
                },
                ["op"] = new JsonObject { ["enum"] = new JsonArray("edit", "add", "delete"), ["description"] = "Edit, add or delete" },
                ["format"] = new JsonObject { ["type"] = "string", ["description"] = "Script format" },
                ["changes"] = kind is IDescribedKind described
                    ? described.ChangesSchema()
                    : new JsonObject { ["type"] = "object", ["description"] = "Changed values" },
            },
            ["$defs"] = Defs(),
        };

        private static JsonObject Defs() => new JsonObject
        {
            ["change"] = new JsonObject
            {
                ["description"] = "Changed value",
                ["type"] = "object",
                ["required"] = new JsonArray("was", "now"),
                ["properties"] = new JsonObject
                {
                    ["was"] = new JsonObject { ["description"] = "Original value" },
                    ["now"] = new JsonObject { ["description"] = "New value" },
                },
                ["additionalProperties"] = false,
            },
            ["file"] = new JsonObject
            {
                ["description"] = "File",
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["file"] = new JsonObject { ["type"] = "string", ["description"] = "File location" },
                    ["was"] = new JsonObject { ["type"] = new JsonArray("string", "null"), ["description"] = "Original SHA-1" },
                    ["now"] = new JsonObject { ["type"] = new JsonArray("string", "null"), ["description"] = "New SHA-1" },
                },
            },
        };
    }

    /// <summary>A kind that can describe the shape of its "changes" for the schema.</summary>
    public interface IDescribedKind
    {
        JsonObject ChangesSchema();
    }
}
