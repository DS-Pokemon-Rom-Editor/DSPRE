using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// What DSPRE reads from a checkout's sources, table by table, so a reader that finds nothing shows as a problem
    /// and not as an empty table. A fork's new fields show up under <see cref="SourceRow.NotModelled"/>.
    /// </summary>
    public static class HgEngineSourceReport
    {
        public sealed class SourceRow
        {
            public string Label { get; init; }
            public string File { get; init; }
            public string Table { get; init; }
            public int Read { get; init; }
            public int Refused { get; init; }
            public string NotModelled { get; init; } = "";
            /// <summary>Why this source is unusable, or null.</summary>
            public string Problem { get; init; }
        }

        /// <summary>The report and anything else found while building it.</summary>
        public sealed record Report(List<SourceRow> Rows, List<(string Where, string Message)> Findings);

        // Each reader's own path, so a checkout that keeps a table somewhere older is read where the editor reads it.
        private static (string Label, string File, string Table)[] Tables() => new[]
        {
            ("Machine moves", HgEngineMachineMoves.SourceRelPath, "sMachineMoves"),
            ("Rock Smash items", HgEngineRockSmashItems.SourceRelPath, "RockSmashItemTable"),
            ("Move tutors", HgEngineMoveTutors.SourceRelPath, "sTutorMoves"),
            ("Hidden items", HgEngineHiddenItems.SourceRelPath, "sHiddenItemParam"),
            ("Icon palettes", HgEngineIconPalette.SourceFile, "gIconPalTable"),
            ("Swarms", HgEngineSwarms.RelPath, "sSwarmMapLUT"),
            ("Starters", HgEngineStarters.SourceRelPath, "sStarterChoices"),
            ("Type chart", HgEngineTypeChart.SourceRelPath, "TypeEffectivenessTable"),
            ("Overworld tags", "src/field/overworld_table.c", "gOWTagToFileNum"),
            ("Prize money", "src/trainermoney.c", "PrizeMoney"),
        };

        public static Report Build()
        {
            List<SourceRow> rows = new List<SourceRow>();
            List<(string Where, string Message)> findings = new List<(string, string)>();
            if (!HgEngineProject.IsActive) return new Report(rows, findings);

            // Dex metrics, names and descriptions have their own readers, and an item's price is the ITEM_PRICE macro.
            rows.Add(EntryRow("Species", "data/Species.c", HgEngineSpeciesPersonalFields.All.Select(f => FieldName(f.Path)), "metricsData.", "textData."));
            rows.Add(EntryRow("Moves", "data/Moves.c", HgEngineMoveSource.Fields.Select(f => FieldName(f.Path)), "names.", "description"));
            rows.Add(EntryRow("Items", "data/itemdata/itemdata.c", HgEngineItemSource.Fields.Select(f => FieldName(f.Path)), "price"));
            rows.Add(EntryRow("Trainers", "data/Trainers.c", null));
            rows.Add(EntryRow("Encounters", "data/Encounters.c", null));
            foreach ((string label, string file, string table) in Tables()) rows.Add(TableRow(label, file, table));
            rows.Add(MegaRow("Mega stones", HgEngineMegaTables.StoneTable));
            rows.Add(MegaRow("Mega moves", HgEngineMegaTables.MoveTable));

            foreach (SourceRow row in rows)
            {
                if (row.Problem != null) findings.Add((row.Label, row.Problem));
                else if (row.Read == 0) findings.Add((row.Label, $"Nothing was read from {row.Table} in {row.File}."));
                else if (row.Refused > 0) findings.Add((row.Label, $"{row.Refused} of {row.Read + row.Refused} rows in {row.Table} couldn't be read."));
            }
            FindDanglingForms(findings);
            FindPlaceholderArt(findings);
            return new Report(rows, findings);
        }

        private static string Text(string rel)
        {
            string path = Path.Combine(HgEngineProject.RepoPathUnc, rel.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(path) ? HgEngineFileCache.GetText(path) : null;
        }

        private static string FieldName(IReadOnlyList<FieldPathSegment> path) =>
            string.Join(".", path.Where(s => !s.IsIndex).Select(s => s.Name));

        private static SourceRow EntryRow(string label, string file, IEnumerable<string> modelled, params string[] editedElsewhere)
        {
            string text = Text(file);
            if (text == null) return new SourceRow { Label = label, File = file, Table = "[entries]", Problem = $"{file} is missing from the checkout." };

            CSourceFile source = CSourceFile.For(text);
            List<CEntry> entries = source.OuterEntries.ToList();
            string missing = "";
            if (modelled != null)
            {
                HashSet<string> known = modelled.ToHashSet(StringComparer.Ordinal);
                SortedSet<string> found = new SortedSet<string>(StringComparer.Ordinal);
                foreach (CEntry entry in entries)
                    if (source.ListAt(entry.Open) is CInitList list) Collect(list, "", found);
                missing = string.Join(", ", found.Where(f => !known.Contains(f) && !known.Any(k => k.StartsWith(f + ".", StringComparison.Ordinal))
                    && !editedElsewhere.Any(e => f.StartsWith(e, StringComparison.Ordinal))));
            }
            return new SourceRow { Label = label, File = file, Table = "[entries]", Read = entries.Count, NotModelled = missing };
        }

        // Dotted names of every designated field.
        private static void Collect(CInitList list, string prefix, SortedSet<string> into)
        {
            foreach (CInitItem item in list.Items)
            {
                if (item.FieldName == null) continue;
                string name = prefix + item.FieldName;
                if (item.List != null && item.List.Items.Any(i => i.FieldName != null)) Collect(item.List, name + ".", into);
                else into.Add(name);
            }
        }

        private static SourceRow TableRow(string label, string file, string table)
        {
            string text = Text(file);
            if (text == null) return new SourceRow { Label = label, File = file, Table = table, Problem = $"{file} is missing from the checkout." };
            CDeclaration declaration = CSourceFile.For(text).Find(table);
            if (declaration == null) return new SourceRow { Label = label, File = file, Table = table, Problem = $"{file} has no {table}." };
            int compiled = declaration.Init.Items.Count(i => HgEngineConfigState.Compiles(i.Conditions) != false);
            return new SourceRow { Label = label, File = file, Table = table, Read = compiled };
        }

        private static SourceRow MegaRow(string label, string table)
        {
            bool read = HgEngineMegaTables.TryRead(table, out List<HgEngineMegaTables.Row> rows, out int refused, out string error);
            return new SourceRow { Label = label, File = HgEngineMegaTables.SourceRelPath, Table = table, Read = rows.Count, Refused = refused, Problem = read ? null : error };
        }

        // MON_WITH_FORM(SPECIES_X, n) past the forms the species has: the game falls back to the base species.
        private static void FindDanglingForms(List<(string Where, string Message)> findings)
        {
            HgEngineSymbolTable species = HgEngineSymbolTable.Load("include/constants/species.h");
            if (species == null) return;
            foreach (string file in new[] { "data/Evolutions.c", "data/Trainers.c", "data/Encounters.c", "data/Headbutt.c", "data/SafariEncounters.c" })
            {
                string text = Text(file);
                if (text == null) continue;
                HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (CCall call in CSourceFile.For(text).Calls("MON_WITH_FORM"))
                {
                    if (call.Args.Count != 2) continue;
                    string name = text[call.Args[0].Start..call.Args[0].End].Trim(), form = text[call.Args[1].Start..call.Args[1].End].Trim();
                    if (!species.TryGetValue(name, out int id) || !CLexer.TryParseInt(form, out int number) || number <= 0) continue;
                    if (HgEngineFormRegistry.ResolveFormSpecies(id, number) == id && seen.Add(name + form))
                        findings.Add((file, $"MON_WITH_FORM({name}, {form}) points past the forms {name} has, so the game uses the base species."));
                }
            }
        }

        // The species' folder: the path segment where Bulbasaur's path has "bulbasaur".
        private static string SpeciesFolder(string rel, string reference)
        {
            string[] parts = rel.Split('/');
            int at = Array.FindIndex(reference.Split('/'), p => p.Equals("bulbasaur", StringComparison.OrdinalIgnoreCase));
            return at >= 0 && at < parts.Length ? parts[at] : rel;
        }

        // A species still drawn with Bulbasaur's picture.
        private static void FindPlaceholderArt(List<(string Where, string Message)> findings)
        {
            string make = Text("data/graphics/pokegra.mk");
            if (make == null) return;
            const int bulbasaur = 1, maleFront = 3;
            string root = HgEngineProject.RepoPathUnc;

            void Check(string kind, Dictionary<int, string> paths)
            {
                if (!paths.TryGetValue(bulbasaur, out string reference)) return;
                string Hash(string rel)
                {
                    string path = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
                    return File.Exists(path) ? FileHash.Sha1(path) : null;
                }
                if (Hash(reference) is not string wanted) return;
                List<string> copies = paths.Where(p => p.Key != bulbasaur && p.Value != reference && Hash(p.Value) == wanted)
                    .OrderBy(p => p.Key).Select(p => SpeciesFolder(p.Value, reference)).ToList();
                if (copies.Count > 0)
                    findings.Add(("pokegra.mk", $"{Plural.Of(copies.Count, kind)} still Bulbasaur's: {string.Join(", ", copies.Take(8))}{(copies.Count > 8 ? ", ..." : "")}"));
            }

            Check("front sprite", HgEnginePokemonBattleSprites.ParseMap(make)
                .Where(p => p.Value[maleFront] != null).ToDictionary(p => p.Key, p => p.Value[maleFront]));
            Check("icon", HgEnginePokemonIcons.ParseMap(make));
        }
    }
}
