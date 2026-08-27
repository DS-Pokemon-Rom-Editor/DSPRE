using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Reads and writes the starter Pokémon (Turtwig/Chimchar/Piplup on DP/Pt, Chikorita/Cyndaquil/Totodile
    /// on HGSS) plus everything that has to stay in sync with them: the DP/Pt starter-selection-scene ASM
    /// routine, the rival's early-game team, tag-battle partners, the starter-cries table (HGSS), and the
    /// professor/rival dialogue that names the chosen species.
    ///
    /// Starters aren't a NARC table in Gen 4 — DP/Pt keep them as a fixed word-table in an overlay, HGSS bakes
    /// them straight into compiled ARM9 code. This is a from-scratch port of Universal Pokémon Randomizer
    /// FVX's <c>Gen4RomHandler.getStarters</c>/<c>setStarters</c> (byte offsets/patterns verified against its
    /// gen4_offsets.ini and Gen4Constants.java), adapted to two DSPRE-specific improvements: dialogue text is
    /// patched with a surgical old-name/old-type → new-name/new-type substring swap instead of a hardcoded
    /// English rewrite (so localized ROMs keep their own language), and the rival/tag-battle script search
    /// patterns are built from the *current* starter species rather than a hardcoded vanilla ID, so editing
    /// starters more than once in the same project keeps working instead of silently no-op'ing after the first
    /// edit.
    /// </summary>
    public static class StarterPokemonData
    {
        // ── Byte patterns (species bytes are placeholders — always rebuilt from the CURRENT starters via
        //    BuildPatternWithSpeciesAt before use, per the repeat-edit-safe design above). ────────────────────
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
            if (RomInfo.gameFamily == RomInfo.GameFamilies.HGSS)
            {
                if (ARM9.CheckCompressionMark()) ARM9.Decompress(RomInfo.arm9Path);
                byte[] arm9 = ARM9.ReadBytes(0);
                var matches = DSUtils.SearchBytes(arm9, RomInfo.starterArm9SearchSuffix);
                if (matches.Count != 1)
                {
                    AppLogger.Warn("StarterPokemonData: HGSS starter ARM9 signature not found (or matched more " +
                        "than once) — showing the vanilla starters. The ARM9 may already be modified by another tool.");
                    return (int[])VanillaHgssStarters.Clone();
                }
                int baseOffset = matches[0] - 13;
                return new[] { ReadWord(arm9, baseOffset), ReadWord(arm9, baseOffset + 4), ReadWord(arm9, baseOffset + 8) };
            }
            else
            {
                if (RomInfo.starterOverlayNumber < 0) return new[] { 0, 0, 0 };
                if (OverlayUtils.IsCompressed(RomInfo.starterOverlayNumber)) OverlayUtils.Decompress(RomInfo.starterOverlayNumber);
                byte[] data = DSUtils.ReadFromFile(OverlayUtils.GetPath(RomInfo.starterOverlayNumber), RomInfo.starterSpeciesOffset, 12);
                return new[] { ReadWord(data, 0), ReadWord(data, 4), ReadWord(data, 8) };
            }
        }

        /// <summary>Reads the DP/Pt starter's held item (HGSS starters never carry one — returns 0).</summary>
        public static int GetHeldItem()
        {
            if (RomInfo.starterHeldItemScriptFileID < 0) return 0;
            string path = Filesystem.GetScriptPath(RomInfo.starterHeldItemScriptFileID);
            if (!File.Exists(path)) return 0;
            byte[] data = DSUtils.ReadFromFile(path, RomInfo.starterHeldItemOffset, 2);
            return data == null || data.Length < 2 ? 0 : ReadWord(data, 0);
        }

        /// <summary>
        /// Writes the DP/Pt starter's held item. No-op on HGSS. The touched script file's <c>.rotom</c>
        /// source (if any) is left for the caller to refresh via <see cref="RefreshRotomSourcesAsync"/> —
        /// see that method's remarks for why this can't happen synchronously here.
        /// </summary>
        public static void SetHeldItem(int itemId)
        {
            if (RomInfo.starterHeldItemScriptFileID < 0) return;
            string path = Filesystem.GetScriptPath(RomInfo.starterHeldItemScriptFileID);
            if (!File.Exists(path)) return;
            var bytes = new byte[2];
            WriteWord(bytes, 0, itemId);
            DSUtils.WriteToFile(path, bytes, RomInfo.starterHeldItemOffset);
        }

        /// <summary>
        /// Applies a new set of 3 starter species: species table (+ DP/Pt selection-scene ASM patch, HGSS
        /// starter cries), the rival's early-game team and tag-battle partners, and the professor/rival
        /// dialogue that names the species. Returns false (nothing was written) only if the species table
        /// itself couldn't be safely located/written — see <see cref="GetStarters"/>'s HGSS ARM9-signature
        /// guard.
        /// </summary>
        /// <param name="scriptFilesTouched">Script file IDs whose raw bytes were patched (rival/tag-battle
        /// scripts) — pass these to <see cref="RefreshRotomSourcesAsync"/> afterward (not done here; that
        /// call shells out to an external process per file and must not block the caller's UI thread).</param>
        public static bool ApplyStarters(int[] newSpecies, out List<int> scriptFilesTouched)
        {
            scriptFilesTouched = new List<int>();
            if (newSpecies == null || newSpecies.Length != 3)
                throw new ArgumentException("Starters must be exactly 3 species.", nameof(newSpecies));

            int[] oldSpecies = GetStarters();

            if (!SetSpeciesAndGraphics(newSpecies))
                return false; // couldn't safely locate the species table — leave everything else untouched

            // The species table (the part that actually matters for gameplay) is already written at this
            // point. Everything below is best-effort follow-up — a failure in one (a malformed script file,
            // a text archive that fails to decode, ...) must not make it look like the save itself failed.
            RunBestEffort("starter cries", () =>
            {
                if (RomInfo.gameFamily == RomInfo.GameFamilies.HGSS) PatchStarterCries(newSpecies);
            });
            var touched = scriptFilesTouched;
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
                AppLogger.Warn($"StarterPokemonData: {stepName} patch failed ({ex.GetType().Name}: {ex.Message}) — " +
                    "the starter species itself was still changed successfully.");
            }
        }

        // ── Species table + DP/Pt selection-scene ASM patch ────────────────────────────────────────────────

        private static bool SetSpeciesAndGraphics(int[] newSpecies)
        {
            if (RomInfo.gameFamily == RomInfo.GameFamilies.HGSS)
            {
                if (ARM9.CheckCompressionMark()) ARM9.Decompress(RomInfo.arm9Path);
                byte[] arm9 = ARM9.ReadBytes(0);
                var matches = DSUtils.SearchBytes(arm9, RomInfo.starterArm9SearchSuffix);
                if (matches.Count != 1)
                {
                    AppLogger.Warn("StarterPokemonData: HGSS starter ARM9 signature not found (or matched more " +
                        "than once) — starters were NOT changed. The ARM9 may already be modified by another tool.");
                    return false;
                }
                int baseOffset = matches[0] - 13;
                WriteWordToArm9(baseOffset, newSpecies[0]);
                WriteWordToArm9(baseOffset + 4, newSpecies[1]);
                WriteWordToArm9(baseOffset + 8, newSpecies[2]);
                return true;
            }
            else
            {
                if (RomInfo.starterOverlayNumber < 0) return false;
                if (OverlayUtils.IsCompressed(RomInfo.starterOverlayNumber)) OverlayUtils.Decompress(RomInfo.starterOverlayNumber);
                string path = OverlayUtils.GetPath(RomInfo.starterOverlayNumber);
                byte[] data = File.ReadAllBytes(path);

                int offset = (int)RomInfo.starterSpeciesOffset;
                WriteWord(data, offset, newSpecies[0]);
                WriteWord(data, offset + 4, newSpecies[1]);
                WriteWord(data, offset + 8, newSpecies[2]);

                PatchDpPtSelectionSceneAsm(data, newSpecies);

                File.WriteAllBytes(path, data);
                return true;
            }
        }

        /// <summary>
        /// Rewrites the Thumb instructions the DP/Pt starter-selection 3D minigame uses to pick which model to
        /// render, so it can show any species instead of only the 3 vanilla ones (their original layout only
        /// supported a narrow, contiguous species range via a single fixed pointer offset). Byte-for-byte port
        /// of UPR-FVX's <c>Gen4RomHandler.setStarters</c> DP/Pt graphics block — without this, the minigame
        /// keeps rendering the vanilla starter model even though the player ends up receiving the right species.
        /// </summary>
        private static void PatchDpPtSelectionSceneAsm(byte[] starterData, int[] newSpecies)
        {
            if (string.IsNullOrEmpty(RomInfo.starterGraphicsPrefix)) return;
            byte[] prefix = DSUtils.StringToByteArray(RomInfo.starterGraphicsPrefix);
            var matches = DSUtils.SearchBytes(starterData, prefix);
            if (matches.Count == 0 || matches[0] <= 0) return;
            int offset = matches[0] + prefix.Length;

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
                WriteWord(starterData, offset + 2, ReadWord(starterData, offset + 4));
                WriteWord(starterData, offset + 4, instr1);
                WriteWord(starterData, offset + 8, instr2);

                offset += 0xE;
            }

            starterData[offset] = 1;

            if (!string.IsNullOrEmpty(RomInfo.starterGraphicsPrefixInner))
            {
                byte[] innerPrefix = DSUtils.StringToByteArray(RomInfo.starterGraphicsPrefixInner);
                var innerMatches = DSUtils.SearchBytes(starterData, innerPrefix);
                if (innerMatches.Count > 0 && innerMatches[0] > 0)
                {
                    int innerOffset = innerMatches[0] + innerPrefix.Length;
                    starterData[innerOffset + 1] = 0x68;
                }
            }
        }

        // ── HGSS starter cries table ────────────────────────────────────────────────────────────────────────

        private static void PatchStarterCries(int[] newSpecies)
        {
            if (RomInfo.starterOverlayNumber < 0 || string.IsNullOrEmpty(RomInfo.starterCriesPrefix)) return;
            if (OverlayUtils.IsCompressed(RomInfo.starterOverlayNumber)) OverlayUtils.Decompress(RomInfo.starterOverlayNumber);
            string path = OverlayUtils.GetPath(RomInfo.starterOverlayNumber);
            byte[] data = File.ReadAllBytes(path);

            byte[] prefix = DSUtils.StringToByteArray(RomInfo.starterCriesPrefix);
            var matches = DSUtils.SearchBytes(data, prefix);
            if (matches.Count == 0 || matches[0] <= 0) return;

            int offset = matches[0] + prefix.Length;
            foreach (int species in newSpecies)
            {
                if (offset + 4 > data.Length) break;
                WriteLong(data, offset, species);
                offset += 4;
            }
            File.WriteAllBytes(path, data);
        }

        // ── Rival / tag-battle scripts ──────────────────────────────────────────────────────────────────────

        /// <summary>Byte-patches the rival/tag-battle script files and returns the IDs of the ones actually
        /// changed, so the caller can refresh their <c>.rotom</c> source afterward (see
        /// <see cref="RefreshRotomSourcesAsync"/>) without spawning a process per file inline here.</summary>
        private static List<int> PatchRivalAndTagBattleScripts(int[] oldSpecies, int[] newSpecies)
        {
            var touched = new List<int>();

            if (RomInfo.gameFamily == RomInfo.GameFamilies.HGSS)
            {
                byte[] magic = BuildPatternWithSpeciesAt(HgssRivalScriptMagicTemplate, 8, oldSpecies[0]);
                foreach (int fileId in HgssFilesWithRivalScript)
                {
                    string path = Filesystem.GetScriptPath(fileId);
                    if (!File.Exists(path)) continue;
                    byte[] data = File.ReadAllBytes(path);

                    var offsets = DSUtils.SearchBytes(data, magic);
                    if (offsets.Count != 1) continue; // ambiguous/not found — skip this file, like UPR does

                    int baseOffset = offsets[0];
                    WriteWord(data, baseOffset + 8, newSpecies[0]);

                    int jumpAmount = ReadLong(data, baseOffset + 13);
                    int secondBase = jumpAmount + baseOffset + 17;
                    if (secondBase >= 0 && secondBase + 6 <= data.Length
                        && data[secondBase] == 0x11 && ReadWord(data, secondBase + 4) == oldSpecies[1])
                    {
                        WriteWord(data, secondBase + 4, newSpecies[1]);
                    }

                    File.WriteAllBytes(path, data);
                    touched.Add(fileId);
                }
                return touched;
            }

            // DP/Pt rival scripts: a series of IfJump commands following the two starter species; the jump
            // target tells apart a genuine rival script from a coincidental byte match.
            bool isPlat = RomInfo.gameFamily == RomInfo.GameFamilies.Plat;
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
                    File.WriteAllBytes(path, data);
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
                    File.WriteAllBytes(path, data);
                    touched.Add(fileId);
                }
            }

            return touched;
        }

        /// <summary>
        /// Rotom-format projects (<see cref="RomInfo.hasRotomProject"/>) keep a decompiled <c>.rotom</c> text
        /// source per script file (<c>expanded/scripts/&lt;id&gt;.rotom</c>) that the Script Editor reads
        /// directly off disk — it is only ever decompiled from the binary ONCE, the very first time the
        /// project has no <c>.rotom</c> files at all, and never reconciled against the binary again after
        /// that. Since <see cref="PatchRivalAndTagBattleScripts"/>/<see cref="SetHeldItem"/> only touch the
        /// raw <c>.bin</c>, an existing <c>.rotom</c> source would otherwise go stale — showing the OLD
        /// species literal in the editor, and silently reverting this fix the next time anyone hits
        /// "Compile" for any unrelated script edit (project-wide recompile from the stale text).
        ///
        /// This is genuinely async (properly awaited, never <c>.GetAwaiter().GetResult()</c>'d) — each file
        /// spawns a real <c>rotom.exe</c> process, and calling this synchronously from a UI-thread call chain
        /// (as the first version of this fix did) deadlocks Avalonia's dispatcher: the awaited
        /// <c>Process.WaitForExitAsync</c> needs the UI thread to resume on, but the UI thread would be
        /// blocked waiting for this call to return. Callers must `await` this from an async context (or fire
        /// it in the background) rather than block on it — see <see cref="StarterEditorViewModel"/>'s
        /// SaveChanges, which runs it after the synchronous save returns so the UI never freezes.
        /// Best-effort throughout: a failure here only affects the Script Editor's display / future
        /// recompiles, not the ROM data itself (already correctly patched by the time this runs).
        /// </summary>
        public static async Task RefreshRotomSourcesAsync(IEnumerable<int> fileIds)
        {
            if (!RomInfo.hasRotomProject || !RotomTool.IsAvailable || fileIds == null) return;

            // rotom's single-file decompile mode (-i/-o) doesn't auto-discover the command database from
            // rotom.toml the way whole-project mode does — it must be passed explicitly.
            string databaseDir = Path.Combine(RomInfo.workDir, ".rotom", "command_database");
            string databasePath = Directory.Exists(databaseDir) ? Directory.GetFiles(databaseDir, "*.json").FirstOrDefault() : null;
            if (databasePath == null)
            {
                AppLogger.Warn($"StarterPokemonData: no rotom command database found under {databaseDir} — .rotom sources left stale.");
                return;
            }

            foreach (int fileId in fileIds)
            {
                try
                {
                    string binPath = Filesystem.GetScriptPath(fileId);
                    string rotomPath = Path.Combine(RomInfo.workDir, "expanded", "scripts", fileId.ToString("D4") + ".rotom");
                    if (!File.Exists(binPath) || !File.Exists(rotomPath)) continue; // no existing source to keep in sync

                    var result = await RotomTool.RunAsync("decompile", "-i", binPath, "-o", rotomPath, "--database", databasePath);
                    if (!result.Success)
                        AppLogger.Warn($"StarterPokemonData: failed to refresh .rotom source for script {fileId}: {RotomTool.FormatResult(result)}");
                }
                catch (Exception ex)
                {
                    AppLogger.Warn($"StarterPokemonData: failed to refresh .rotom source for script {fileId}: {ex.Message}");
                }
            }
        }

        // ── Dialogue text ───────────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Surgically swaps the old starters' species name (and, where the template embeds it, primary-type
        /// name) for the new ones inside the professor/rival dialogue — never a hardcoded English rewrite, so
        /// localized ROMs keep their own language for everything except the species/type words themselves.
        /// </summary>
        private static void PatchStarterText(int[] oldSpecies, int[] newSpecies)
        {
            if (RomInfo.starterScreenTextNumber < 0) return;

            string[] pokemonNames = RomInfo.GetPokemonNames();
            string[] typeNames = RomInfo.GetTypeNames();

            string OldName(int i) => oldSpecies[i] < pokemonNames.Length ? pokemonNames[oldSpecies[i]] : null;
            string NewName(int i) => newSpecies[i] < pokemonNames.Length ? pokemonNames[newSpecies[i]] : null;
            string OldType(int i) => TypeNameOf(oldSpecies[i], typeNames);
            string NewType(int i) => TypeNameOf(newSpecies[i], typeNames);

            var archive = new TextArchive(RomInfo.starterScreenTextNumber);

            if (RomInfo.gameFamily == RomInfo.GameFamilies.HGSS)
            {
                for (int i = 0; i < 3; i++)
                {
                    ReplaceIn(archive.messages, i + 1, OldName(i), NewName(i));
                    ReplaceIn(archive.messages, i + 1, OldType(i), NewType(i));
                    ReplaceIn(archive.messages, i + 4, OldName(i), NewName(i));
                    ReplaceIn(archive.messages, i + 4, OldType(i), NewType(i));
                }
            }
            else
            {
                TextArchive pokedexArchive = RomInfo.starterPokedexSpeciesTextNumber >= 0
                    ? new TextArchive(RomInfo.starterPokedexSpeciesTextNumber) : null;

                for (int i = 0; i < 3; i++)
                {
                    ReplaceIn(archive.messages, i + 1, OldName(i), NewName(i));
                    if (pokedexArchive != null
                        && oldSpecies[i] < pokedexArchive.messages.Count && newSpecies[i] < pokedexArchive.messages.Count)
                    {
                        ReplaceIn(archive.messages, i + 1, pokedexArchive.messages[oldSpecies[i]], pokedexArchive.messages[newSpecies[i]]);
                    }
                }
            }

            archive.SaveToExpandedDir(RomInfo.starterScreenTextNumber, showSuccessMessage: false);
        }

        private static string TypeNameOf(int species, string[] typeNames)
        {
            try
            {
                var personal = new PokemonPersonalData(species);
                int typeIndex = (int)personal.type1;
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
            var bytes = new byte[2];
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
