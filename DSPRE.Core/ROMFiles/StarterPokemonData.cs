using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The starters and what must follow them: the DP and Platinum selection scene, the rival team, tag partners, HGSS
    /// cries and the dialogue naming them. Ported from UPR FVX's Gen4RomHandler.
    /// </summary>
    /// <remarks>DP and Platinum keep the starters in an overlay word table; HGSS builds them into ARM9 code.</remarks>
    public static class StarterPokemonData
    {
        // ── Byte patterns: species bytes are placeholders, rebuilt from the current starters before use ──
        private static readonly byte[] HgssRivalScriptMagicTemplate =
            { 0xCE, 0x00, 0x0C, 0x80, 0x11, 0x00, 0x0C, 0x80, 0x98, 0x00, 0x1C, 0x00, 0x05 };
        private static readonly byte[] DpptRivalScriptMagicTemplate =
            { 0xDE, 0x00, 0x0C, 0x80, 0x11, 0x00, 0x0C, 0x80, 0x83, 0x01, 0x1C, 0x00, 0x01 };
        private static readonly byte[] DpptTagBattleScriptMagic1 =
            { 0xDE, 0x00, 0x0C, 0x80, 0x28, 0x00, 0x04, 0x80 };
        private static readonly byte[] DpptTagBattleScriptMagic2Template =
            { 0x11, 0x00, 0x0C, 0x80, 0x86, 0x01, 0x1C, 0x00, 0x01 };

        private static readonly int[] HgssFilesWithRivalScript = { 7, 23, 96, 110, 819, 850, 866 };
        private static readonly int[] DpFilesWithRivalScript = { 34, 90, 118, 180, 195, 394 };
        private static readonly int[] PtFilesWithRivalScript = { 31, 36, 112, 123, 186, 427, 429, 1096 };
        private static readonly int[] DpFilesWithTagScript = { 2, 131, 230 };
        private static readonly int[] PtFilesWithTagScript = { 2, 136, 201, 236 };

        private static readonly int[] VanillaHgssStarters = { 152, 155, 158 }; // Chikorita, Cyndaquil, Totodile

        // ── Public API ──────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Reads the 3 current starter species IDs.</summary>
        public static int[] GetStarters()
        {
            // hg-engine sets the starters from src/starters.c at run time.
            if (HgEngine.HgEngineStarters.Available)
            {
                if (HgEngine.HgEngineStarters.TryRead(out int[] species, out _, out string error)) return species;
                AppLogger.Warn("StarterPokemonData: " + error);
                return (int[])VanillaHgssStarters.Clone();
            }
            if (RomInfo.GameFamily == RomInfo.GameFamilies.HGSS)
            {
                ARM9.DecompressIfMarked();
                byte[] arm9 = ARM9.ReadBytes(0);
                int baseOffset = HgssSpeciesSite(arm9);
                if (baseOffset < 0)
                {
                    AppLogger.Warn("StarterPokemonData: the HGSS starter code isn't what this ROM shipped with, showing the " +
                        "vanilla starters. The ARM9 may already be modified by another tool.");
                    return (int[])VanillaHgssStarters.Clone();
                }
                return new[] { ReadWord(arm9, baseOffset), ReadWord(arm9, baseOffset + 4), ReadWord(arm9, baseOffset + 8) };
            }
            else
            {
                RomLocator.Spot species = RomLocator.Where(RomLocator.Thing.StarterSpecies);
                if (species == null) return new[] { 0, 0, 0 };
                if (OverlayUtils.IsCompressed(species.Overlay)) OverlayUtils.Decompress(species.Overlay);
                byte[] data = DSUtils.ReadFromFile(OverlayUtils.GetPath(species.Overlay), (uint)species.Offset, 12);
                return new[] { ReadWord(data, 0), ReadWord(data, 4), ReadWord(data, 8) };
            }
        }

        /// <summary>
        /// The script in the starter file that holds GivePokemon, found from the file header so Pearl and shuffled ROMs
        /// work; -1 when not found.
        /// </summary>
        public static int GetStarterScriptNumber()
        {
            if (RomInfo.StarterHeldItemScriptFileID < 0) return -1;
            string path = Filesystem.GetScriptPath(RomInfo.StarterHeldItemScriptFileID);
            if (!File.Exists(path)) return -1;

            byte[] d;
            try { d = File.ReadAllBytes(path); } catch (Exception ex) { AppLogger.WarnOnce("StarterPokemonData.GetStarterScriptNumber", ex); return -1; }

            // The header is a run of relative offsets, one per script, ended by the 0xFD13 marker.
            List<int> starts = new List<int>();
            int pos = 0;
            while (pos + 4 <= d.Length)
            {
                uint value = (uint)(d[pos] | (d[pos + 1] << 8) | (d[pos + 2] << 16) | (d[pos + 3] << 24));
                pos += 4;
                if ((value & 0xFFFF) == 0xFD13) break;
                starts.Add((int)value + pos);
            }
            if (starts.Count == 0) return -1;

            // The script whose range covers the byte the held item sits at.
            int target = (int)RomInfo.StarterHeldItemOffset;
            List<(int start, int i)> byStart = starts.Select((start, i) => (start, i)).OrderBy(x => x.start).ToList();
            for (int n = 0; n < byStart.Count; n++)
            {
                int from = byStart[n].start;
                int to = n + 1 < byStart.Count ? byStart[n + 1].start : d.Length;
                if (target >= from && target < to) return byStart[n].i + 1;   // shown one-based
            }
            return -1;
        }
        /// <summary>Reads the DP/Pt starter's held item (HGSS starters never carry one, returns 0).</summary>
        public static int GetHeldItem()
        {
            if (RomInfo.StarterHeldItemScriptFileID < 0) return 0;
            string path = Filesystem.GetScriptPath(RomInfo.StarterHeldItemScriptFileID);
            if (!File.Exists(path)) return 0;
            byte[] data = DSUtils.ReadFromFile(path, RomInfo.StarterHeldItemOffset, 2);
            return data == null || data.Length < 2 ? 0 : ReadWord(data, 0);
        }

        /// <summary>
        /// Writes the DP/Pt starter's held item. No-op on HGSS. The touched script file's <c>.rotom</c>
        /// source (if any) is left for the caller to refresh via <see cref="RefreshRotomSourcesAsync"/>, 
        /// see that method's remarks for why this can't happen synchronously here.
        /// </summary>
        public static void SetHeldItem(int itemId)
        {
            if (RomInfo.StarterHeldItemScriptFileID < 0) return;
            string path = Filesystem.GetScriptPath(RomInfo.StarterHeldItemScriptFileID);
            if (!File.Exists(path)) return;
            byte[] bytes = new byte[2];
            WriteWord(bytes, 0, itemId);
            DSUtils.WriteToFile(path, bytes, RomInfo.StarterHeldItemOffset);
        }

        /// <summary>
        /// Writes three new starters and everything that follows them. False, with nothing written, only when the
        /// species table couldn't be found.
        /// </summary>
        /// <param name="scriptFilesTouched">Script files whose bytes were patched; pass them to
        /// <see cref="RefreshRotomSourcesAsync"/> off the UI thread afterwards.</param>
        public static bool ApplyStarters(int[] newSpecies, out List<int> scriptFilesTouched)
        {
            scriptFilesTouched = new List<int>();
            if (newSpecies == null || newSpecies.Length != 3)
                throw new ArgumentException("Starters must be exactly 3 species.", nameof(newSpecies));

            int[] oldSpecies = GetStarters();
            // The DP/Pt selection-scene patch rewrites code, so the same three species must not touch anything.
            if (oldSpecies != null && oldSpecies.SequenceEqual(newSpecies)) return true;

            if (HgEngine.HgEngineStarters.Available)
            {
                // Its hooks also pick the cries from the species, so only the choices are written.
                if (!HgEngine.HgEngineStarters.TryWrite(newSpecies, out string error)) { AppLogger.Error("StarterPokemonData: " + error); return false; }
            }
            else if (!SetSpeciesAndGraphics(newSpecies))
                return false; // couldn't safely locate the species table, leave everything else untouched

            // The species table is written; the rest is best effort, so one failure doesn't fail the save.
            RunBestEffort("starter cries", () =>
            {
                if (RomInfo.GameFamily == RomInfo.GameFamilies.HGSS && !HgEngine.HgEngineStarters.Available) PatchStarterCries(newSpecies);
            });
            List<int> touched = scriptFilesTouched;
            RunBestEffort("rival/tag-battle scripts", () => touched.AddRange(PatchRivalAndTagBattleScripts(oldSpecies, newSpecies)));
            RunBestEffort("starter dialogue text", () => PatchStarterText(oldSpecies, newSpecies));
            return true;
        }

        private static void RunBestEffort(string stepName, Action step)
        {
            try
            {
                step();
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"StarterPokemonData: {stepName} patch failed ({ex.GetType().Name}: {ex.Message}), " +
                    "the starter species itself was still changed successfully.");
            }
        }

        // ── Species table + DP/Pt selection-scene ASM patch ────────────────────────────────────────────────

        private static bool SetSpeciesAndGraphics(int[] newSpecies)
        {
            if (RomInfo.GameFamily == RomInfo.GameFamilies.HGSS)
            {
                ARM9.DecompressIfMarked();
                byte[] arm9 = ARM9.ReadBytes(0);
                int baseOffset = HgssSpeciesSite(arm9);
                if (baseOffset < 0)
                {
                    AppLogger.Warn("StarterPokemonData: the HGSS starter code isn't what this ROM shipped with, so the " +
                        "starters were NOT changed. The ARM9 may already be modified by another tool.");
                    return false;
                }
                WriteWordToArm9(baseOffset, newSpecies[0]);
                WriteWordToArm9(baseOffset + 4, newSpecies[1]);
                WriteWordToArm9(baseOffset + 8, newSpecies[2]);
                return true;
            }
            else
            {
                RomLocator.Spot species = RomLocator.Where(RomLocator.Thing.StarterSpecies);
                if (species == null) return false;
                if (OverlayUtils.IsCompressed(species.Overlay)) OverlayUtils.Decompress(species.Overlay);
                string path = OverlayUtils.GetPath(species.Overlay);
                byte[] data = File.ReadAllBytes(path);

                int offset = species.Offset;
                WriteWord(data, offset, newSpecies[0]);
                WriteWord(data, offset + 4, newSpecies[1]);
                WriteWord(data, offset + 8, newSpecies[2]);

                PatchDpPtSelectionSceneAsm(data, newSpecies);

                DSUtils.WriteWholeFile(path, data);
                return true;
            }
        }

        /// <summary>
        /// Rewrites the DP and Platinum selection scene so it can show any species (a port of UPR-FVX setStarters);
        /// without it the scene shows the old models.
        /// </summary>
        internal static void PatchDpPtSelectionSceneAsm(byte[] starterData, int[] newSpecies)
        {
            if (RomInfo.StarterSceneSpeciesLiterals is int[] literals)
            {
                PatchSelectionSceneLiterals(starterData, literals, newSpecies);
                return;
            }
            if (string.IsNullOrEmpty(RomInfo.StarterGraphicsPrefix)) return;
            int offset = RomLocator.Where(RomLocator.Thing.StarterScene)?.Offset ?? -1;
            if (!Follows(starterData, offset, DSUtils.StringToByteArray(RomInfo.StarterGraphicsPrefix)))
            {
                AppLogger.Warn("StarterPokemonData: the selection scene's code isn't where this ROM keeps it, so the scene " +
                    "keeps its old models. The overlay may already be modified by another tool.");
                return;
            }

            // The shift below is one-shot: running it on already patched code shifts it again and breaks
            // the scene, so a patched routine only gets its species immediates rewritten.
            bool alreadyPatched = IsDpPtSelectionScenePatched(starterData, offset);

            if (!alreadyPatched)
            {
                // Move a section of instructions down to make room for the add/sub pair inserted below, and shift
                // the pointer's base address so the immediate offsets that follow can be repointed to any species.
                WriteWord(starterData, offset + 0xC, ReadWord(starterData, offset + 0xA));
                if (offset % 4 == 0)
                {
                    starterData[offset + 0xC] = (byte)(starterData[offset + 0xC] - 1);
                }
                WriteWord(starterData, offset + 0xA, ReadWord(starterData, offset + 0x8));
                starterData[offset + 0xA] = (byte)(starterData[offset + 0xA] - 1);
                WriteWord(starterData, offset + 0x8, ReadWord(starterData, offset + 0x6));
                WriteWord(starterData, offset + 0x6, ReadWord(starterData, offset + 0x4));
                WriteWord(starterData, offset + 0x4, ReadWord(starterData, offset + 0x2));
                WriteWord(starterData, offset + 0x2, 0x6828);
                WriteWord(starterData, offset, 0x182D);
            }

            offset += 0x16;
            WriteWord(starterData, offset, 0x6828);

            offset += 0xA;

            // Encode each starter's species index as a (possibly two-part) add/sub off a small fixed offset,
            // since a single Thumb immediate can't span the whole species range.
            for (int i = 0; i < 3; i++)
            {
                int starterDiff = newSpecies[i] - 4 * (i + 1);

                int instr1 = 0x3200;
                int instr2 = 0x3200;

                if (starterDiff < 0)
                {
                    instr1 |= 0x800;
                    starterDiff = Math.Abs(starterDiff);
                }
                else if (starterDiff > 255)
                {
                    instr2 |= 0xFF;
                    starterDiff -= 255;
                }

                instr1 |= starterDiff & 0xFF;

                starterData[offset] = (byte)(4 * (i + 1));
                if (!alreadyPatched)
                    WriteWord(starterData, offset + 2, ReadWord(starterData, offset + 4));
                WriteWord(starterData, offset + 4, instr1);
                WriteWord(starterData, offset + 8, instr2);

                offset += 0xE;
            }

            starterData[offset] = 1;

            int innerOffset = RomLocator.Where(RomLocator.Thing.StarterSceneInner)?.Offset ?? -1;
            if (Follows(starterData, innerOffset, SceneInnerPrefix) && innerOffset + 2 <= starterData.Length)
                starterData[innerOffset + 1] = 0x68;
        }

        /// <summary>
        /// Japanese DP passes each sprite's species to the scene's sprite loader as a pool word that follows the
        /// word holding that sprite's slot (0x204, 0x208, 0x20C), so the species are rewritten in place.
        /// </summary>
        private static void PatchSelectionSceneLiterals(byte[] data, int[] literals, int[] newSpecies)
        {
            for (int i = 0; i < literals.Length; i++)
            {
                int at = literals[i];
                if (at < 4 || at + 4 > data.Length || ReadLong(data, at - 4) != 0x204 + 4 * i)
                {
                    AppLogger.Warn("StarterPokemonData: the selection scene's sprite species weren't found where expected, " +
                        "so the scene keeps its old sprites. The overlay may already be modified by another tool.");
                    return;
                }
            }
            for (int i = 0; i < literals.Length; i++) WriteLong(data, literals[i], newSpecies[i]);
        }

        private static bool IsDpPtSelectionScenePatched(byte[] data, int offset) =>
            offset + 4 <= data.Length
            && ReadWord(data, offset) == 0x182D
            && ReadWord(data, offset + 2) == 0x6828;

        // The bytes around each site as shipped; a site with different bytes around it is left alone.
        private static readonly byte[] HgssSpeciesTail = { 0x03, 0x03, 0x1A, 0x12, 0x01, 0x23, 0x00, 0x00 };
        private static readonly byte[] SceneInnerPrefix = { 0x02, 0x90, 0x03, 0x90, 0x02, 0x20, 0x00, 0x02 };
        private static readonly byte[] CriesPrefix = DSUtils.StringToByteArray("0004000C10BD0000000000000000000000E000000000000000E0000000000200");

        /// <summary>Whether <paramref name="prefix"/> ends right where <paramref name="offset"/> starts.</summary>
        private static bool Follows(byte[] data, int offset, byte[] prefix) =>
            offset >= prefix.Length && offset <= data.Length && data.AsSpan(offset - prefix.Length, prefix.Length).SequenceEqual(prefix);

        /// <summary>Where the HGSS arm9 builds the starters' species, or -1 when its code there isn't as shipped.</summary>
        private static int HgssSpeciesSite(byte[] arm9)
        {
            int at = RomLocator.Where(RomLocator.Thing.StarterSpecies)?.Offset ?? -1;
            int tail = at + 13;
            return at >= 0 && tail + HgssSpeciesTail.Length <= arm9.Length && arm9.AsSpan(tail, HgssSpeciesTail.Length).SequenceEqual(HgssSpeciesTail) ? at : -1;
        }

        // ── HGSS starter cries table ────────────────────────────────────────────────────────────────────────

        private static void PatchStarterCries(int[] newSpecies)
        {
            RomLocator.Spot cries = RomLocator.Where(RomLocator.Thing.StarterCries);
            if (cries == null) return;
            if (OverlayUtils.IsCompressed(cries.Overlay)) OverlayUtils.Decompress(cries.Overlay);
            string path = OverlayUtils.GetPath(cries.Overlay);
            byte[] data = File.ReadAllBytes(path);

            int offset = cries.Offset;
            if (!Follows(data, offset, CriesPrefix))
            {
                AppLogger.Warn("StarterPokemonData: the starter cries aren't where this ROM keeps them, so they were left alone.");
                return;
            }
            foreach (int species in newSpecies)
            {
                if (offset + 4 > data.Length) break;
                WriteLong(data, offset, species);
                offset += 4;
            }
            DSUtils.WriteWholeFile(path, data);
        }

        // ── Rival / tag-battle scripts ──────────────────────────────────────────────────────────────────────

        /// <summary>Byte-patches the rival/tag-battle script files and returns the IDs of the ones actually
        /// changed, so the caller can refresh their <c>.rotom</c> source afterward (see
        /// <see cref="RefreshRotomSourcesAsync"/>) without spawning a process per file inline here.</summary>
        private static List<int> PatchRivalAndTagBattleScripts(int[] oldSpecies, int[] newSpecies)
        {
            List<int> touched = new List<int>();

            if (RomInfo.GameFamily == RomInfo.GameFamilies.HGSS)
            {
                byte[] magic = BuildPatternWithSpeciesAt(HgssRivalScriptMagicTemplate, 8, oldSpecies[0]);
                foreach (int fileId in HgssFilesWithRivalScript)
                {
                    string path = Filesystem.GetScriptPath(fileId);
                    if (!File.Exists(path)) continue;
                    byte[] data = File.ReadAllBytes(path);

                    List<int> offsets = DSUtils.SearchBytes(data, magic);
                    if (offsets.Count != 1) continue; // ambiguous/not found, skip this file, like UPR does

                    int baseOffset = offsets[0];
                    WriteWord(data, baseOffset + 8, newSpecies[0]);

                    int jumpAmount = ReadLong(data, baseOffset + 13);
                    int secondBase = jumpAmount + baseOffset + 17;
                    if (secondBase >= 0 && secondBase + 6 <= data.Length
                        && data[secondBase] == 0x11 && ReadWord(data, secondBase + 4) == oldSpecies[1])
                    {
                        WriteWord(data, secondBase + 4, newSpecies[1]);
                    }

                    ArchiveMembers.Put(RomInfo.DirNames.Scripts, fileId, data);
                    touched.Add(fileId);
                }
                return touched;
            }

            // DP/Pt rival scripts: a series of IfJump commands following the two starter species; the jump
            // target tells apart a genuine rival script from a coincidental byte match.
            bool isPlat = RomInfo.GameFamily == RomInfo.GameFamilies.Plat;
            int[] rivalFiles = isPlat ? PtFilesWithRivalScript : DpFilesWithRivalScript;
            byte[] rivalMagic = BuildPatternWithSpeciesAt(DpptRivalScriptMagicTemplate, 8, oldSpecies[0]);
            foreach (int fileId in rivalFiles)
            {
                string path = Filesystem.GetScriptPath(fileId);
                if (!File.Exists(path)) continue;
                byte[] data = File.ReadAllBytes(path);
                bool changed = false;

                foreach (int baseOffset in DSUtils.SearchBytes(data, rivalMagic))
                {
                    int jumpLoc = baseOffset + rivalMagic.Length;
                    if (jumpLoc + 4 > data.Length) continue;
                    int jumpTo = ReadLong(data, jumpLoc) + jumpLoc + 4;
                    if (jumpTo < 0 || jumpTo + 2 > data.Length) continue;

                    int atJump = ReadWord(data, jumpTo);
                    bool looksLikeRivalScript = atJump == 0xE5 || atJump == 0x28F || (atJump == 0x125 && isPlat);
                    if (!looksLikeRivalScript) continue;

                    WriteWord(data, baseOffset + 0x8, newSpecies[0]);
                    WriteWord(data, baseOffset + 0x15, newSpecies[1]);
                    changed = true;
                }
                if (changed)
                {
                    ArchiveMembers.Put(RomInfo.DirNames.Scripts, fileId, data);
                    touched.Add(fileId);
                }
            }

            // DP/Pt tag battles (Lucas/Dawn, Barry): magic1 anchors the match, magic2 (species-parameterized)
            // confirms it a fixed distance later.
            int[] tagFiles = isPlat ? PtFilesWithTagScript : DpFilesWithTagScript;
            byte[] tagMagic2 = BuildPatternWithSpeciesAt(DpptTagBattleScriptMagic2Template, 4, oldSpecies[1]);
            foreach (int fileId in tagFiles)
            {
                string path = Filesystem.GetScriptPath(fileId);
                if (!File.Exists(path)) continue;
                byte[] data = File.ReadAllBytes(path);
                bool changed = false;

                foreach (int baseOffset in DSUtils.SearchBytes(data, DpptTagBattleScriptMagic1))
                {
                    int secondPartStart = baseOffset + DpptTagBattleScriptMagic1.Length + 2;
                    if (secondPartStart + tagMagic2.Length > data.Length) continue;

                    bool valid = true;
                    for (int i = 0; i < tagMagic2.Length; i++)
                    {
                        if (data[secondPartStart + i] != tagMagic2[i]) { valid = false; break; }
                    }
                    if (!valid) continue;

                    int jumpLoc = secondPartStart + tagMagic2.Length;
                    if (jumpLoc + 4 > data.Length) continue;
                    int jumpTo = ReadLong(data, jumpLoc) + jumpLoc + 4;
                    if (jumpTo < 0 || jumpTo + 2 > data.Length || ReadWord(data, jumpTo) != 0x1B) continue; // not a tag battle script

                    if (baseOffset + 0x23 > data.Length) continue;
                    if (ReadWord(data, baseOffset + 0x21) == oldSpecies[0])
                        WriteWord(data, baseOffset + 0x21, newSpecies[0]);
                    else
                        WriteWord(data, baseOffset + 0x21, newSpecies[2]);
                    WriteWord(data, baseOffset + 0xE, newSpecies[1]);
                    changed = true;
                }
                if (changed)
                {
                    ArchiveMembers.Put(RomInfo.DirNames.Scripts, fileId, data);
                    touched.Add(fileId);
                }
            }

            return touched;
        }

        /// <summary>
        /// Rotom sources go stale when only the .bin changes, so this regenerates them. It runs rotom per file and must
        /// be awaited off the UI thread, or the dispatcher deadlocks.
        /// </summary>
        public static Task RefreshRotomSourcesAsync(IEnumerable<int> fileIds)
            => ScriptSourceSync.RefreshAsync(fileIds);

        // ── Dialogue text ───────────────────────────────────────────────────────────────────────────────────

        /// <summary>Swaps the old species and type names for the new ones in the dialogue, so other languages keep their text.</summary>
        private static void PatchStarterText(int[] oldSpecies, int[] newSpecies)
        {
            if (RomInfo.StarterScreenTextNumber < 0) return;

            string[] pokemonNames = RomInfo.GetPokemonNames();
            string[] typeNames = RomInfo.GetTypeNames();

            string OldName(int i) => oldSpecies[i] < pokemonNames.Length ? pokemonNames[oldSpecies[i]] : null;
            string NewName(int i) => newSpecies[i] < pokemonNames.Length ? pokemonNames[newSpecies[i]] : null;
            string OldType(int i) => TypeNameOf(oldSpecies[i], typeNames);
            string NewType(int i) => TypeNameOf(newSpecies[i], typeNames);

            TextArchive archive = new TextArchive(RomInfo.StarterScreenTextNumber);

            if (RomInfo.GameFamily == RomInfo.GameFamilies.HGSS)
            {
                for (int i = 0; i < 3; i++)
                {
                    ReplaceIn(archive.Messages, i + 1, OldName(i), NewName(i));
                    ReplaceIn(archive.Messages, i + 1, OldType(i), NewType(i));
                    ReplaceIn(archive.Messages, i + 4, OldName(i), NewName(i));
                    ReplaceIn(archive.Messages, i + 4, OldType(i), NewType(i));
                }
            }
            else
            {
                TextArchive pokedexArchive = RomInfo.StarterPokedexSpeciesTextNumber >= 0
                    ? new TextArchive(RomInfo.StarterPokedexSpeciesTextNumber) : null;

                for (int i = 0; i < 3; i++)
                {
                    ReplaceIn(archive.Messages, i + 1, OldName(i), NewName(i));
                    if (pokedexArchive != null
                        && oldSpecies[i] < pokedexArchive.Messages.Count && newSpecies[i] < pokedexArchive.Messages.Count)
                    {
                        ReplaceIn(archive.Messages, i + 1, pokedexArchive.Messages[oldSpecies[i]], pokedexArchive.Messages[newSpecies[i]]);
                    }
                }
            }

            archive.SaveToExpandedDir(RomInfo.StarterScreenTextNumber, showSuccessMessage: false);
        }

        private static string TypeNameOf(int species, string[] typeNames)
        {
            try
            {
                PokemonPersonalData personal = new PokemonPersonalData(species);
                int typeIndex = (int)personal.Type1;
                return typeIndex >= 0 && typeIndex < typeNames.Length ? typeNames[typeIndex] : null;
            }
            catch (IOException)
            {
                return null;
            }
        }

        private static void ReplaceIn(List<string> messages, int index, string oldValue, string newValue)
        {
            if (index < 0 || index >= messages.Count) return;
            if (string.IsNullOrEmpty(oldValue) || newValue == null || oldValue == newValue) return;
            if (messages[index].Contains(oldValue))
                messages[index] = messages[index].Replace(oldValue, newValue);
        }

        // ── Byte helpers ────────────────────────────────────────────────────────────────────────────────────

        private static byte[] BuildPatternWithSpeciesAt(byte[] template, int index, int species)
        {
            byte[] pattern = (byte[])template.Clone();
            pattern[index] = (byte)(species & 0xFF);
            pattern[index + 1] = (byte)((species >> 8) & 0xFF);
            return pattern;
        }

        private static void WriteWordToArm9(int offset, int value)
        {
            byte[] bytes = new byte[2];
            WriteWord(bytes, 0, value);
            ARM9.WriteBytes(bytes, (uint)offset);
        }

        private static int ReadWord(byte[] data, int offset) => data[offset] | (data[offset + 1] << 8);

        private static void WriteWord(byte[] data, int offset, int value)
        {
            data[offset] = (byte)(value & 0xFF);
            data[offset + 1] = (byte)((value >> 8) & 0xFF);
        }

        private static int ReadLong(byte[] data, int offset) =>
            data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24);

        private static void WriteLong(byte[] data, int offset, int value)
        {
            data[offset] = (byte)(value & 0xFF);
            data[offset + 1] = (byte)((value >> 8) & 0xFF);
            data[offset + 2] = (byte)((value >> 16) & 0xFF);
            data[offset + 3] = (byte)((value >> 24) & 0xFF);
        }
    }
}
