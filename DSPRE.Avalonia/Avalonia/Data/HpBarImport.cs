using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Puts a PNG into an HP bar with the battle fitted to it (<see cref="HpBarFit"/>), and shows the result
    /// before anything is written.
    /// </summary>
    public static class HpBarImport
    {
        public static readonly PendingArchiveWrites.Target SheetTarget =
            new("hp-bar-pieces", BattleGaugePieces.ReadSheet, b => BattleGaugePieces.WriteSheet(b));

        public static HpBarFit.Plan Plan(BattleGaugeComposer.Kind kind, string path, out string whynot)
        {
            whynot = null;
            byte[] file;
            try { file = File.ReadAllBytes(path); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { whynot = ex.Message; return null; }
            if (!IndexedPng.TryRead(file, out byte[] indices, out uint[] palette, out int w, out int h))
            {
                whynot = "That PNG isn't indexed. Save it as an indexed PNG.";
                return null;
            }
            HpBarFit.Plan plan = HpBarFit.Make(kind, GraphicAssets.Flatten(indices, palette, w, h), w, h);
            if (plan.CannotBecause != null) { whynot = plan.CannotBecause; return null; }
            return plan;
        }

        /// <summary>
        /// Writes the colours, the picture and the fitted pieces. Archive writes go wherever the caller's
        /// staging sends them; the pieces go to <paramref name="putSheet"/>.
        /// </summary>
        public static string Apply(GraphicAssets.Archive archive, int index, string path, HpBarFit.Plan plan,
                                   Func<byte[]> sheet, Action<byte[]> putSheet, out string note)
        {
            note = null;
            string trouble = HpBarFit.WriteColours(archive, plan);
            if (trouble != null) return trouble;
            BattleGaugeTextRenderer.Reset();
            trouble = GraphicAssets.ImportPng(archive, index, path, out note);
            if (trouble != null) return trouble;
            List<string> notes = new List<string>();
            byte[] fitted = HpBarFit.FitPieces(plan.Kind, sheet(), notes);
            foreach (string n in notes) if (!plan.Notes.Contains(n)) plan.Notes.Add(n);
            if (fitted != null) putSheet(fitted);
            return null;
        }

        /// <summary>
        /// Draws the battle with the current bar and with the imported one, or returns null.
        /// A double battle bar of yours is drawn twice: with its gauge, and with the numbers Start swaps in.
        /// </summary>
        public static List<(string Label, byte[] Now, byte[] After)> Preview(GraphicAssets.Archive archive, int index, string path, HpBarFit.Plan plan, out string trouble)
        {
            trouble = null;
            bool doubles = plan.Kind is not (BattleGaugeComposer.Kind.PlayerSingle or BattleGaugeComposer.Kind.OpponentSingle);
            bool numbers = plan.Kind is BattleGaugeComposer.Kind.PlayerNear or BattleGaugeComposer.Kind.PlayerFar;
            // The current bar has to be drawn before the import is staged.
            List<byte[]> now = new List<byte[]>();
            try
            {
                BattleGaugeTextRenderer.Reset();
                foreach (bool asNumbers in numbers ? new[] { false, true } : new[] { false }) now.Add(Draw(doubles, asNumbers));
            }
            catch (Exception ex) { trouble = "The preview couldn't be drawn: " + ex.Message; return null; }
            PendingArchiveWrites dry = new PendingArchiveWrites();
            byte[] sheet = null;
            string said = null;
            trouble = dry.Import(() => Apply(archive, index, path, plan, BattleGaugePieces.ReadSheet, s => sheet = s, out said));
            if (trouble != null) return null;
            try
            {
                using (dry.Reading())
                using (BattleGaugePieces.Staged(sheet))
                {
                    BattleGaugeTextRenderer.Reset();
                    List<(string, byte[], byte[])> shown = new List<(string, byte[], byte[])>();
                    foreach (bool asNumbers in numbers ? new[] { false, true } : new[] { false })
                        shown.Add((asNumbers ? "After Start" : "In battle", now[shown.Count], Draw(doubles, asNumbers)));
                    return shown;
                }
            }
            catch (Exception ex) { trouble = "The preview couldn't be drawn: " + ex.Message; return null; }
            finally { BattleGaugeTextRenderer.Reset(); }
        }

        private static byte[] Draw(bool doubles, bool asNumbers) => BattleScreenRenderer.Flatten(new BattleScreenRenderer().Build(new BattleScreenRenderer.Options
        {
            TerrainId = 2, PokemonName = "PIDGEY", Level = 42, Gender = BattleGaugeText.Gender.Male, HealthLeft = 0.45,
            Doubles = doubles, NumbersInsteadOfBar = asNumbers,
        }), touch: false);
    }
}
