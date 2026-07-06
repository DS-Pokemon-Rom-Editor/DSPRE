using System;
using System.Collections.Generic;
using System.IO;
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
            ValidateTrainers(issues);
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

        /// <summary>Flags trainer party members whose species, held item, or a move points past the end of
        /// its list (e.g. a party mon still referencing a species removed by a ROM hack). Each trainer and each
        /// slot is read defensively so one unreadable file never aborts the whole scan.</summary>
        private static void ValidateTrainers(List<ValidationIssue> issues)
        {
            try
            {
                int mons = GetPokemonNames().Length;
                int items = GetItemNames().Length;
                int moves = GetAttackNames().Length;

                string propDir = gameDirs[DirNames.trainerProperties].unpackedDir;
                string partyDir = gameDirs[DirNames.trainerParty].unpackedDir;
                if (!Directory.Exists(propDir) || !Directory.Exists(partyDir)) return;

                int count = Directory.GetFiles(propDir).Length;
                string[] names; try { names = GetSimpleTrainerNames(); } catch { names = Array.Empty<string>(); }

                for (int id = 0; id < count; id++)
                {
                    string suffix = Path.DirectorySeparatorChar + id.ToString("D4");
                    TrainerFile tf;
                    try
                    {
                        using var propStream = new FileStream(propDir + suffix, FileMode.Open, FileAccess.Read);
                        using var partyStream = new FileStream(partyDir + suffix, FileMode.Open, FileAccess.Read);
                        tf = new TrainerFile(new TrainerProperties((ushort)id, propStream), partyStream,
                            id < names.Length ? names[id] : "");
                    }
                    catch { continue; }

                    string who = id < names.Length && !string.IsNullOrWhiteSpace(names[id])
                        ? $"{names[id]} (#{id})" : $"Trainer #{id}";
                    void Add(string msg) => issues.Add(new ValidationIssue { Category = "Trainer", Where = who, Message = msg });

                    int pc = tf.trp?.partyCount ?? 0;
                    for (int i = 0; i < pc; i++)
                    {
                        PartyPokemon p;
                        try { p = tf.party[i]; } catch { continue; }
                        if (p == null || p.CheckEmpty()) continue;

                        int sp = p.pokeID ?? 0;
                        if (sp >= mons) Add($"party slot {i + 1} species {sp} is out of range (only {mons} exist)");
                        if (p.heldItem is ushort it && it >= items)
                            Add($"party slot {i + 1} held item {it} is out of range (only {items} exist)");
                        if (p.moves != null)
                            foreach (ushort mv in p.moves)
                                if (mv != 0 && mv >= moves)
                                    Add($"party slot {i + 1} move {mv} is out of range (only {moves} exist)");
                    }
                }
            }
            catch (Exception ex)
            {
                issues.Add(new ValidationIssue { Category = "Trainer", Where = "(scan)", Message = "scan failed: " + ex.Message });
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
