using System;
using System.Collections.Generic;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>The kinds of file a map header cross-references.</summary>
    public enum RefKind { Matrix, AreaData, Event, Script, LevelScript, Text, Wild }

    /// <summary>One validation finding (a header pointing at a file that doesn't exist).</summary>
    public sealed class ValidationIssue
    {
        public string Category { get; init; }
        public string Where { get; init; }
        public string Message { get; init; }
    }

    /// <summary>
    /// Scans every map header once and exposes two project-health views: VALIDATION (references that point
    /// past the end of their NARC — e.g. a header still pointing at a deleted matrix/event) and WHERE-USED
    /// (reverse lookup: which headers reference a given matrix / event / script / … id). Cheap: one read per
    /// header, all the cross-references live on the header.
    /// </summary>
    public static class ProjectIndex
    {
        private sealed class HeaderRefs
        {
            public ushort Id;
            public int Matrix, Area, Event, Script, LevelScript, Text, Wild;
        }

        private const int U16None = 0xFFFF;
        private const int U8None = 0xFF;

        private static List<HeaderRefs> ScanHeaders()
        {
            var list = new List<HeaderRefs>();
            int count = GetHeaderCount();
            for (ushort i = 0; i < count; i++)
            {
                try
                {
                    var h = MapHeader.GetMapHeader(i);
                    if (h == null) continue;
                    list.Add(new HeaderRefs
                    {
                        Id = h.ID,
                        Matrix = h.matrixID, Area = h.areaDataID, Event = h.eventFileID,
                        Script = h.scriptFileID, LevelScript = h.levelScriptID,
                        Text = h.textArchiveID, Wild = h.wildPokemon,
                    });
                }
                catch { /* skip unreadable header */ }
            }
            return list;
        }

        private static int RefValue(HeaderRefs h, RefKind kind) => kind switch
        {
            RefKind.Matrix => h.Matrix, RefKind.AreaData => h.Area, RefKind.Event => h.Event,
            RefKind.Script => h.Script, RefKind.LevelScript => h.LevelScript, RefKind.Text => h.Text,
            _ => h.Wild,
        };

        private static int CountFor(RefKind kind) => kind switch
        {
            RefKind.Matrix => Filesystem.GetMatrixCount(),
            RefKind.AreaData => Filesystem.GetAreaDataCount(),
            RefKind.Event => Filesystem.GetEventFileCount(),
            RefKind.Script => Filesystem.GetScriptCount(),
            RefKind.LevelScript => Filesystem.GetScriptCount(),
            RefKind.Text => Filesystem.GetTextArchivesCount(),
            _ => Filesystem.GetEncountersCount(),
        };

        /// <summary>"none" sentinel for a ref kind (areaData is a byte → 0xFF; the rest are ushort → 0xFFFF).</summary>
        private static int NoneFor(RefKind kind) => kind == RefKind.AreaData ? U8None : U16None;

        public static List<ValidationIssue> Validate()
        {
            var issues = new List<ValidationIssue>();
            ValidateHeaders(issues);
            ValidateEvolutions(issues);
            return issues;
        }

        private static void ValidateHeaders(List<ValidationIssue> issues)
        {
            var headers = ScanHeaders();
            foreach (RefKind kind in Enum.GetValues<RefKind>())
            {
                int count = CountFor(kind);
                if (count <= 0) continue;
                int none = NoneFor(kind);
                foreach (var h in headers)
                {
                    int v = RefValue(h, kind);
                    if (v == none) continue;        // unset / not used
                    if (v >= count)
                        issues.Add(new ValidationIssue
                        {
                            Category = kind.ToString(),
                            Where = $"Header {h.Id}",
                            Message = $"{kind} id {v} is out of range (only {count} exist)",
                        });
                }
            }
        }

        /// <summary>Flags evolutions whose target species, or item/move/species parameter, points past the
        /// end of its list. The parameter's MEANING comes from the customisable LabelStore (so a method set
        /// to "CustomNumber" is treated as a raw value and not range-checked).</summary>
        private static void ValidateEvolutions(List<ValidationIssue> issues)
        {
            try
            {
                int mons = GetPokemonNames().Length;
                int items = GetItemNames().Length;
                int moves = GetAttackNames().Length;
                for (int species = 0; species < mons; species++)
                {
                    EvolutionFile ef;
                    try { ef = new EvolutionFile(species); } catch { continue; }
                    if (ef.data == null) continue;
                    for (int slot = 0; slot < ef.data.Length; slot++)
                    {
                        var d = ef.data[slot];
                        if (!d.isValid()) continue;
                        void Add(string msg) => issues.Add(new ValidationIssue
                        { Category = "Evolution", Where = $"{Name(species, mons)} (slot {slot + 1})", Message = msg });

                        if (d.target < 0 || d.target >= mons)
                            Add($"target species {d.target} is out of range (only {mons} exist)");

                        var meaning = (EvolutionParamMeaning)LabelStore.GetAttr("evolution_methods", (int)d.method);
                        int p = d.param;
                        if (meaning == EvolutionParamMeaning.PokemonName && (p < 0 || p >= mons))
                            Add($"parameter species {p} is out of range (only {mons} exist)");
                        else if (meaning == EvolutionParamMeaning.ItemName && (p < 0 || p >= items))
                            Add($"parameter item {p} is out of range (only {items} exist)");
                        else if (meaning == EvolutionParamMeaning.MoveName && (p < 0 || p >= moves))
                            Add($"parameter move {p} is out of range (only {moves} exist)");
                    }
                }
            }
            catch (Exception ex)
            {
                issues.Add(new ValidationIssue { Category = "Evolution", Where = "(scan)", Message = "scan failed: " + ex.Message });
            }
        }

        private static string Name(int species, int mons)
        {
            try { var n = GetPokemonNames(); return species < n.Length ? $"{n[species]} (#{species})" : $"Pokémon {species}"; }
            catch { return $"Pokémon {species}"; }
        }

        /// <summary>Header ids that reference the given file id of the given kind.</summary>
        public static List<ushort> HeadersUsing(RefKind kind, int id)
        {
            var result = new List<ushort>();
            foreach (var h in ScanHeaders())
                if (RefValue(h, kind) == id) result.Add(h.Id);
            return result;
        }
    }
}
