using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using DSPRE.ROMFiles;

namespace DSPRE.HgEngine
{
    /// <summary>data/Headbutt.c: each map's field type holds its index (HeadbuttFile_009_Route_1) and sizes its arrays,
    /// so tree group changes rewrite the type.</summary>
    /// <remarks>The index comes from the type name, not the declaration order; __size[] follows through sizeof.</remarks>
    public static class HgEngineHeadbutt
    {
        private const string SourceRelPath = "data/Headbutt.c";
        private const string SpeciesHeaderRelPath = "include/constants/species.h";
        private const string ArchiveStruct = "HeadbuttArchiveData";

        public static bool TryGetFieldName(int headbuttFileIndex, out string fieldName)
        {
            fieldName = null;
            string text = TryReadSource(out _);
            return text != null && TryFindFieldName(text, headbuttFileIndex, out fieldName);
        }

        /// <summary>Pure index-&gt;field-name resolution over already-loaded source text, split out from
        /// <see cref="TryGetFieldName"/> so it's directly unit-testable against a synthetic snippet.</summary>
        internal static bool TryFindFieldName(string headbuttSourceText, int headbuttFileIndex, out string fieldName)
        {
            fieldName = null;
            foreach (CMember member in CSourceFile.For(headbuttSourceText).FindStruct(ArchiveStruct)?.Members ?? new List<CMember>())
            {
                string[] parts = member.Type.Split('_', 3);
                if (parts.Length == 3 && parts[0] == "HeadbuttFile" && int.TryParse(parts[1], out int idx) && idx == headbuttFileIndex)
                { fieldName = member.Name; return true; }
            }
            return false;
        }

        public static bool TryLoad(int headbuttFileIndex, out HeadbuttEncounterFile file, out string error)
        {
            file = null; error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout is linked."; return false; }
            if (!TryGetFieldName(headbuttFileIndex, out string fieldName))
            { error = $"Couldn't resolve a Headbutt.c field for file index {headbuttFileIndex}."; return false; }

            string text = TryReadSource(out string path);
            if (text == null) { error = $"{path} is missing from the checkout."; return false; }
            if (!TryFindDataBlock(text, out int open, out int close))
            { error = "Couldn't locate the Headbutt.c __data initializer."; return false; }
            if (!ElementScanner.TryLocateValueSpan(text, open, close, new[] { FieldPathSegment.Field(fieldName) }, out int vs, out int ve))
            { error = $"Couldn't locate .{fieldName} in Headbutt.c."; return false; }
            string mapBlock = text.Substring(vs, ve - vs);

            file = new HeadbuttEncounterFile { ID = (ushort)headbuttFileIndex };
            HgEngineSymbolTable species = HgEngineSymbolTable.Load(SpeciesHeaderRelPath);

            int normalTreeCount = 0, specialTreeCount = 0;
            if (HgEngineSourcePatcher.TryGetFieldValueInBlock(mapBlock, new[] { FieldPathSegment.Field("normalTreeCount") }, out string ntc))
                CLexer.TryParseInt(ntc, out normalTreeCount);
            if (HgEngineSourcePatcher.TryGetFieldValueInBlock(mapBlock, new[] { FieldPathSegment.Field("specialTreeCount") }, out string stc))
                CLexer.TryParseInt(stc, out specialTreeCount);

            string unreadable = null;
            ReadSlots(mapBlock, "normalSlots", species, file.NormalEncounters, 12, ref unreadable);
            ReadSlots(mapBlock, "specialSlots", species, file.SpecialEncounters, 6, ref unreadable);
            // Saving writes every slot back, so one read as 0 would replace the source's species.
            if (unreadable != null) { error = $"Headbutt.c has a species DSPRE can't read: {unreadable}"; file = null; return false; }
            ReadGroups(mapBlock, normalTreeCount, specialTreeCount, file.NormalTreeGroups, file.SpecialTreeGroups);

            file.NormalTreeGroupsCount = (ushort)file.NormalTreeGroups.Count;
            file.SpecialTreeGroupsCount = (ushort)file.SpecialTreeGroups.Count;
            return true;
        }

        public static bool TrySave(int headbuttFileIndex, HeadbuttEncounterFile file, out string error)
        {
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout is linked."; return false; }
            if (!TryGetFieldName(headbuttFileIndex, out string fieldName))
            { error = $"Couldn't resolve a Headbutt.c field for file index {headbuttFileIndex}."; return false; }

            string text = TryReadSource(out string path);
            if (text == null) { error = $"{path} is missing from the checkout."; return false; }

            HgEngineSymbolTable species = HgEngineSymbolTable.Load(SpeciesHeaderRelPath);
            List<string> failed = new List<string>();

            // As the game's own files: a map with no trees and no Pokemon set is only the counts.
            int totalGroups = file.NormalTreeGroups.Count + file.SpecialTreeGroups.Count;
            bool slots = totalGroups > 0 || file.NormalEncounters.Concat(file.SpecialEncounters).Any(e => e.PokemonID != 0 || e.MinLevel != 0 || e.MaxLevel != 0);
            if (!TryShapeMap(ref text, fieldName, slots, totalGroups, out error)) return false;

            List<string> SlotItems(IReadOnlyList<HeadbuttEncounter> list, int count)
            {
                List<string> items = new List<string>(count);
                for (int i = 0; i < count; i++)
                {
                    HeadbuttEncounter e = i < list.Count ? list[i] : new HeadbuttEncounter();
                    int id = e.PokemonID & HgEngineTrainerSource.SpeciesMask, form = e.PokemonID >> HgEngineTrainerSource.FormShift;
                    string sp = HgEngineTrainerSource.FormatSpecies(species != null && species.TryGetNameWithPrefix(id, "SPECIES_", out string sn) ? sn : id.ToString(), form);
                    items.Add($"{{ {sp}, {e.MinLevel}, {e.MaxLevel} }}");
                }
                return items;
            }

            // Written in the layout the file already has, so a save only changes the slots that changed.
            if (slots && !TryReplaceMapField(ref text, fieldName, "normalSlots", old => HgEngineSafariEncounters.ListLiteral(old, SlotItems(file.NormalEncounters, 12)))) failed.Add("normalSlots");
            if (slots && !TryReplaceMapField(ref text, fieldName, "specialSlots", old => HgEngineSafariEncounters.ListLiteral(old, SlotItems(file.SpecialEncounters, 6)))) failed.Add("specialSlots");
            if (!TryReplaceMapField(ref text, fieldName, "normalTreeCount", file.NormalTreeGroups.Count.ToString())) failed.Add("normalTreeCount");
            if (!TryReplaceMapField(ref text, fieldName, "specialTreeCount", file.SpecialTreeGroups.Count.ToString())) failed.Add("specialTreeCount");

            if (totalGroups > 0)
            {
                string GroupLiteral(HeadbuttTreeGroup g)
                {
                    List<string> coords = new List<string>(6);
                    foreach (HeadbuttTree t in g.Trees)
                    {
                        int x = t.IsUnused ? -1 : t.GlobalX, y = t.IsUnused ? -1 : t.GlobalY;
                        coords.Add($"{{ {x}, {y} }}");
                    }
                    return "{ " + string.Join(", ", coords) + " }";
                }
                List<string> groups = new List<string>(totalGroups);
                foreach (HeadbuttTreeGroup g in file.NormalTreeGroups) groups.Add(GroupLiteral(g));
                foreach (HeadbuttTreeGroup g in file.SpecialTreeGroups) groups.Add(GroupLiteral(g));
                string treeCoordsLiteral = "{\n            " + string.Join(",\n            ", groups) + ",\n        }";
                if (!TryReplaceMapField(ref text, fieldName, "treeCoords", treeCoordsLiteral)) failed.Add("treeCoords");
            }

            if (failed.Count > 0)
            { error = $"Headbutt.c has no {string.Join(", ", failed)} for .{fieldName}, so nothing was written."; return false; }
            try { HgEngineFileCache.WriteText(path, text); }
            catch (System.Exception ex) when (ex is IOException || ex is System.UnauthorizedAccessException)
            { error = $"Headbutt.c couldn't be written: {ex.Message}"; return false; }
            return true;
        }

        /// <summary>
        /// Makes the map's struct type and initializer hold exactly the arrays it needs: the two slot arrays when
        /// <paramref name="slots"/>, and treeCoords[<paramref name="groups"/>][6] when there are groups.
        /// </summary>
        private static bool TryShapeMap(ref string text, string fieldName, bool slots, int groups, out string error)
        {
            error = null;
            CSourceFile source = CSourceFile.For(text);
            string type = source.FindStruct(ArchiveStruct)?.Members.FirstOrDefault(m => m.Name == fieldName).Type;
            if (type == null) { error = $"Headbutt.c declares no type for .{fieldName}."; return false; }
            CStruct decl = source.FindStruct(type);
            if (decl == null) { error = $"Headbutt.c has no typedef for {type}."; return false; }

            string body = "\n    u16 normalTreeCount;\n    u16 specialTreeCount;\n"
                + (slots ? "    HeadbuttEncounterSlot normalSlots[12];\n    HeadbuttEncounterSlot specialSlots[6];\n" : "")
                + (groups > 0 ? $"    HeadbuttTreeCoord treeCoords[{groups}][6];\n" : "");
            string current = text.Substring(decl.Open + 1, decl.Close - decl.Open - 1);
            if (CSourceFile.Normalize(current) != CSourceFile.Normalize(body))
                text = text.Substring(0, decl.Open + 1) + body + text.Substring(decl.Close);

            foreach ((string name, bool wanted, string placeholder) in new[]
            {
                ("treeCoords", groups > 0, "{\n        }"),
                ("specialSlots", slots, "{ }"),
                ("normalSlots", slots, "{ }"),
            })
            {
                if (!TryFindDataBlock(text, out int open, out _)) { error = "Couldn't locate the Headbutt.c __data initializer."; return false; }
                CInitList map = CSourceFile.For(text).ListAt(open)?.Field(fieldName)?.List;
                CInitItem member = map?.Field(name);
                if (member != null && !wanted)
                    text = HgEngineSourcePatcher.RemoveElement(text, member.Start, member.End);
                else if (member == null && wanted)
                {
                    if (map == null) { error = $"Couldn't locate .{fieldName} in Headbutt.c."; return false; }
                    // Placed before the map's closing brace, the counts come first and the arrays follow in order.
                    int lineStart = HgEngineSourcePatcher.LineStart(text, map.Close);
                    string indent = text.Substring(lineStart, map.Close - lineStart) + "    ";
                    CInitItem before = name == "treeCoords" ? null : name == "normalSlots" ? map.Field("specialSlots") ?? map.Field("treeCoords") : map.Field("treeCoords");
                    int at = before != null ? HgEngineSourcePatcher.LineStart(text, before.Start) : lineStart;
                    text = text.Insert(at, $"{indent}.{name} = {placeholder.Replace("\n", "\n" + indent.Substring(4))},\n");
                }
            }
            return true;
        }

        private static void ReadSlots(string mapBlock, string fieldName, HgEngineSymbolTable species, List<HeadbuttEncounter> dest, int fixedCount,
            ref string unreadable)
        {
            if (HgEngineSourcePatcher.TryGetFieldValueInBlock(mapBlock, new[] { FieldPathSegment.Field(fieldName) }, out string raw))
            {
                foreach (string el in HgEngineSourcePatcher.SplitArrayValue(raw))
                {
                    List<string> parts = HgEngineSourcePatcher.SplitArrayValue(el.Trim());
                    if (parts.Count < 3) continue;
                    dest.Add(new HeadbuttEncounter
                    {
                        PokemonID = (ushort)HgEngineTrainerSource.SpeciesWord(parts[0], species, ref unreadable),
                        MinLevel = (byte)HgEngineSymbolTable.ValueOrZero(parts[1], null),
                        MaxLevel = (byte)HgEngineSymbolTable.ValueOrZero(parts[2], null),
                    });
                }
            }
            while (dest.Count < fixedCount) dest.Add(new HeadbuttEncounter());
        }

        private static void ReadGroups(string mapBlock, int normalCount, int specialCount,
            System.ComponentModel.BindingList<HeadbuttTreeGroup> normalDest, System.ComponentModel.BindingList<HeadbuttTreeGroup> specialDest)
        {
            if (!HgEngineSourcePatcher.TryGetFieldValueInBlock(mapBlock, new[] { FieldPathSegment.Field("treeCoords") }, out string raw)) return;
            List<string> groups = HgEngineSourcePatcher.SplitArrayValue(raw);
            for (int i = 0; i < groups.Count; i++)
            {
                HeadbuttTreeGroup group = new HeadbuttTreeGroup();
                List<string> coords = HgEngineSourcePatcher.SplitArrayValue(groups[i].Trim());
                for (int j = 0; j < coords.Count && j < group.Trees.Count; j++)
                {
                    List<string> xy = HgEngineSourcePatcher.SplitArrayValue(coords[j].Trim());
                    if (xy.Count < 2) continue;
                    group.Trees[j].GlobalX = unchecked((ushort)HgEngineSymbolTable.ValueOrZero(xy[0], null));
                    group.Trees[j].GlobalY = unchecked((ushort)HgEngineSymbolTable.ValueOrZero(xy[1], null));
                }
                if (i < normalCount) normalDest.Add(group);
                else specialDest.Add(group);
            }
        }

        private static bool TryReplaceMapField(ref string text, string mapFieldName, string subFieldName, string newLiteral)
            => TryReplaceMapField(ref text, mapFieldName, subFieldName, _ => newLiteral);

        private static bool TryReplaceMapField(ref string text, string mapFieldName, string subFieldName, System.Func<string, string> make)
        {
            if (!TryFindDataBlock(text, out int open, out int close)) return false;
            FieldPathSegment[] path = new[] { FieldPathSegment.Field(mapFieldName), FieldPathSegment.Field(subFieldName) };
            if (!ElementScanner.TryLocateValueSpan(text, open, close, path, out int vs, out int ve)) return false;
            string newLiteral = make(text.Substring(vs, ve - vs));
            if (HgEngineSourcePatcher.SameTokens(text.Substring(vs, ve - vs), newLiteral)) return true;
            text = text.Substring(0, vs) + newLiteral + text.Substring(ve);
            return true;
        }

        private static bool TryFindDataBlock(string text, out int open, out int close)
        {
            open = close = -1;
            CDeclaration data = CSourceFile.For(text).Find("__data");
            if (data == null) return false;
            open = data.Init.Open;
            close = data.Init.Close;
            return true;
        }


        private static string TryReadSource(out string path)
        {
            path = Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(path) ? HgEngineFileCache.GetText(path) : null;
        }
    }
}
