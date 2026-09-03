using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Finds the command that hands the player their starter, so the editor can change its held item and
    /// level without writing to a byte offset.
    ///
    /// This reads the binaries, for projects with no decompiled sources to search. Where a project has
    /// them, StarterRotomSource is better: the sources name the starter outright. This looks for the
    /// shape instead, a give-a-Pokemon command whose species comes from a variable rather than being
    /// written into the script, which is weaker and matches twice in an untouched Platinum.
    ///
    /// Two candidates already exist in an untouched Platinum, so a single match is never assumed: the
    /// editor picks the best one and lets the user say otherwise.
    /// </summary>
    public static class StarterScriptLocator
    {
        /// <summary>Anything at or above this is a variable rather than a literal (SVWK_START).</summary>
        public const int FirstVariable = 0x4000;

        /// <summary>Anything at or above this is one of the script's own slots (SCWK_START).</summary>
        public const int FirstScriptSlot = 0x8000;

        /// <summary>Where a give-a-Pokemon command is, and what it is currently set to.</summary>
        public sealed class Candidate
        {
            public int FileId;
            public bool InFunction;          // Script when false
            public int ContainerId;
            public int CommandIndex;

            public string CommandName;
            public int Species;              // a variable number when FromVariable
            public int Level;
            public int HeldItem;

            public bool FromVariable => Species >= FirstVariable;
            public bool FromScriptSlot => Species >= FirstScriptSlot;

            /// <summary>What the picker shows, so two candidates can be told apart.</summary>
            public string Where => InFunction
                ? $"file {FileId}, function {ContainerId}"
                : $"file {FileId}, script {ContainerId}";

            public string Summary => FromVariable
                ? $"{CommandName}, species from variable 0x{Species:X}, level {Level}"
                : $"{CommandName}, species {Species} written into the script, level {Level}";

            /// <summary>Stable enough to remember, and cheap to compare.</summary>
            public string Key => $"{FileId}/{(InFunction ? "F" : "S")}{ContainerId}/{CommandIndex}";
        }

        /// <summary>Why the editor is showing what it is showing.</summary>
        public enum Outcome
        {
            Vanilla,          // the untouched slot held what it should, nothing was scanned
            Remembered,       // the user's saved choice still holds up
            OnlyOne,          // one candidate in the whole game, taken without asking
            NeedsChoosing,    // several, or the saved one is gone
            NewOneAppeared,   // the saved choice still works but the game grew another candidate
            NotFound,         // nothing that looks like a starter anywhere
            NotApplicable,    // this game does not keep its starter in a script
        }

        public sealed class Result
        {
            public Outcome Outcome;
            public Candidate Chosen;
            public List<Candidate> Candidates = new List<Candidate>();
            public string Fingerprint => string.Join(",", Candidates.Select(c => c.Key).OrderBy(k => k, StringComparer.Ordinal));
        }

        /// <summary>
        /// The cheap check first: parse the one file the untouched game keeps this in and look at the one
        /// slot. Only when that does not hold up does anything else get read.
        /// </summary>
        public static Result Locate(string rememberedKey, string knownFingerprint)
        {
            var result = new Result();
            if (RomInfo.starterHeldItemScriptFileID < 0)
            {
                result.Outcome = Outcome.NotApplicable;
                return result;
            }

            var vanilla = ReadVanillaSlot();
            if (vanilla != null && string.IsNullOrEmpty(rememberedKey))
            {
                result.Outcome = Outcome.Vanilla;
                result.Chosen = vanilla;
                result.Candidates.Add(vanilla);
                return result;
            }

            var all = FindCandidates();
            result.Candidates = all;

            if (!string.IsNullOrEmpty(rememberedKey))
            {
                var kept = all.FirstOrDefault(c => c.Key == rememberedKey);
                if (kept != null)
                {
                    result.Chosen = kept;
                    bool grew = !string.IsNullOrEmpty(knownFingerprint)
                             && knownFingerprint != result.Fingerprint;
                    result.Outcome = grew ? Outcome.NewOneAppeared : Outcome.Remembered;
                    return result;
                }
                // The saved choice is gone: fall through and ask rather than write somewhere else.
                result.Outcome = all.Count == 0 ? Outcome.NotFound : Outcome.NeedsChoosing;
                return result;
            }

            if (all.Count == 0) { result.Outcome = Outcome.NotFound; return result; }
            if (all.Count == 1) { result.Outcome = Outcome.OnlyOne; result.Chosen = all[0]; return result; }

            result.Outcome = Outcome.NeedsChoosing;
            result.Chosen = all[0];         // the best guess, still shown for confirmation
            return result;
        }

        /// <summary>The known slot in an untouched ROM, or null when it does not hold what it should.</summary>
        public static Candidate ReadVanillaSlot()
        {
            if (RomInfo.starterCommandScriptNumber < 0 || RomInfo.starterCommandIndex < 0) return null;

            ScriptFile f = TryParse(RomInfo.starterHeldItemScriptFileID);
            if (f?.allScripts == null) return null;

            var container = f.allScripts.FirstOrDefault(c => c.manualUserID == (uint)RomInfo.starterCommandScriptNumber);
            var cmds = container?.commands;
            if (cmds == null || RomInfo.starterCommandIndex >= cmds.Count) return null;

            var made = Describe(RomInfo.starterHeldItemScriptFileID, false,
                                RomInfo.starterCommandScriptNumber, RomInfo.starterCommandIndex,
                                cmds[RomInfo.starterCommandIndex]);
            return made != null && made.FromVariable ? made : null;
        }

        /// <summary>Every give-a-Pokemon command in the game, best guess first.</summary>
        public static List<Candidate> FindCandidates()
        {
            var found = new List<Candidate>();
            if (!RomInfo.gameDirs.ContainsKey(RomInfo.DirNames.scripts)) return found;

            string dir = RomInfo.gameDirs[RomInfo.DirNames.scripts].unpackedDir;
            if (!Directory.Exists(dir)) return found;

            int count = Directory.GetFiles(dir).Length;
            for (int id = 0; id < count; id++)
            {
                var f = TryParse(id);
                if (f == null) continue;
                Collect(found, id, f.allScripts, false);
                Collect(found, id, f.allFunctions, true);
            }

            // The script's own slot is how the starter arrives, so that ranks above a saved variable,
            // which ranks above a species written straight into the script.
            return found
                .OrderByDescending(c => c.FromScriptSlot)
                .ThenByDescending(c => c.FromVariable)
                .ThenBy(c => c.FileId)
                .ToList();
        }

        private static void Collect(List<Candidate> into, int fileId,
                                    List<ScriptCommandContainer> containers, bool functions)
        {
            if (containers == null) return;
            foreach (var c in containers)
            {
                var cmds = c.commands;
                if (cmds == null) continue;
                for (int i = 0; i < cmds.Count; i++)
                {
                    var made = Describe(fileId, functions, (int)c.manualUserID, i, cmds[i]);
                    if (made != null) into.Add(made);
                }
            }
        }

        /// <summary>
        /// A give-a-Pokemon command, read by NAME rather than by opcode: Diamond calls it 0x0089 and
        /// Platinum 0x0096, so an opcode written down here would be wrong for one of them.
        /// </summary>
        private static Candidate Describe(int fileId, bool inFunction, int containerId, int index, ScriptCommand cmd)
        {
            string name = cmd?.name;
            if (string.IsNullOrEmpty(name)) return null;
            if (!name.StartsWith("GivePokemon", StringComparison.OrdinalIgnoreCase)) return null;
            if (name.StartsWith("GivePokemonEgg", StringComparison.OrdinalIgnoreCase)) return null;

            var ps = cmd.cmdParams;
            if (ps == null || ps.Count < 3) return null;

            return new Candidate
            {
                FileId = fileId,
                InFunction = inFunction,
                ContainerId = containerId,
                CommandIndex = index,
                CommandName = name.Split(' ')[0],
                Species = ReadParam(ps, 0),
                Level = ReadParam(ps, 1),
                HeldItem = ReadParam(ps, 2),
            };
        }

        private static int ReadParam(List<byte[]> ps, int i)
        {
            if (i >= ps.Count) return -1;
            byte[] b = ps[i];
            if (b == null || b.Length == 0) return -1;
            if (b.Length == 1) return b[0];
            return b[0] | (b[1] << 8);
        }
        private static ScriptFile TryParse(int id)
        {
            try { return new ScriptFile(id); }
            catch { return null; }
        }
    }
}
