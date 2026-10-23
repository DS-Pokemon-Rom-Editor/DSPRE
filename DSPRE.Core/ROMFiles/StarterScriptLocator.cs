using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Finds the starter's GivePokemon in the binaries, for projects without sources: a give whose species comes from a
    /// variable. Untouched Platinum has two, so the user confirms the pick.
    /// </summary>
    public static class StarterScriptLocator
    {
        /// <summary>Anything at or above this is a variable.</summary>
        public const int FirstVariable = 0x4000;

        /// <summary>Anything at or above this is one of the script's own slots.</summary>
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
            Result result = new Result();
            if (RomInfo.StarterHeldItemScriptFileID < 0)
            {
                result.Outcome = Outcome.NotApplicable;
                return result;
            }

            Candidate vanilla = ReadVanillaSlot();
            if (vanilla != null && string.IsNullOrEmpty(rememberedKey))
            {
                result.Outcome = Outcome.Vanilla;
                result.Chosen = vanilla;
                result.Candidates.Add(vanilla);
                return result;
            }

            List<Candidate> all = FindCandidates();
            result.Candidates = all;

            if (!string.IsNullOrEmpty(rememberedKey))
            {
                Candidate kept = all.FirstOrDefault(c => c.Key == rememberedKey);
                if (kept != null)
                {
                    result.Chosen = kept;
                    bool grew = !string.IsNullOrEmpty(knownFingerprint)
                             && knownFingerprint != result.Fingerprint;
                    result.Outcome = grew ? Outcome.NewOneAppeared : Outcome.Remembered;
                    return result;
                }
                // The saved choice is gone: ask instead of writing somewhere else.
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
            if (RomInfo.StarterCommandScriptNumber < 0 || RomInfo.StarterCommandIndex < 0) return null;

            ScriptFile f = TryParse(RomInfo.StarterHeldItemScriptFileID);
            if (f?.AllScripts == null) return null;

            ScriptCommandContainer container = f.AllScripts.FirstOrDefault(c => c.ManualUserID == (uint)RomInfo.StarterCommandScriptNumber);
            List<ScriptCommand> cmds = container?.Commands;
            if (cmds == null || RomInfo.StarterCommandIndex >= cmds.Count) return null;

            Candidate made = Describe(RomInfo.StarterHeldItemScriptFileID, false,
                                RomInfo.StarterCommandScriptNumber, RomInfo.StarterCommandIndex,
                                cmds[RomInfo.StarterCommandIndex]);
            return made != null && made.FromVariable ? made : null;
        }

        /// <summary>Every give-a-Pokemon command in the game, best guess first.</summary>
        public static List<Candidate> FindCandidates()
        {
            List<Candidate> found = new List<Candidate>();
            if (!RomInfo.GameDirs.ContainsKey(RomInfo.DirNames.Scripts)) return found;

            string dir = RomInfo.GameDirs[RomInfo.DirNames.Scripts].unpackedDir;
            if (!Directory.Exists(dir)) return found;

            int count = NarcAPI.Narc.MemberCount(dir);
            for (int id = 0; id < count; id++)
            {
                ScriptFile f = TryParse(id);
                if (f == null) continue;
                Collect(found, id, f.AllScripts, false);
                Collect(found, id, f.AllFunctions, true);
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
            foreach (ScriptCommandContainer c in containers)
            {
                List<ScriptCommand> cmds = c.Commands;
                if (cmds == null) continue;
                for (int i = 0; i < cmds.Count; i++)
                {
                    Candidate made = Describe(fileId, functions, (int)c.ManualUserID, i, cmds[i]);
                    if (made != null) into.Add(made);
                }
            }
        }

        /// <summary>A GivePokemon command, matched by name: Diamond calls it 0x0089 and Platinum 0x0096.</summary>
        private static Candidate Describe(int fileId, bool inFunction, int containerId, int index, ScriptCommand cmd)
        {
            string name = cmd?.Name;
            if (string.IsNullOrEmpty(name)) return null;
            if (!name.StartsWith("GivePokemon", StringComparison.OrdinalIgnoreCase)) return null;
            if (name.StartsWith("GivePokemonEgg", StringComparison.OrdinalIgnoreCase)) return null;

            List<byte[]> ps = cmd.CmdParams;
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
            catch (Exception ex) { AppLogger.WarnOnce("StarterScriptLocator.TryParse", ex); return null; }
        }
    }
}
