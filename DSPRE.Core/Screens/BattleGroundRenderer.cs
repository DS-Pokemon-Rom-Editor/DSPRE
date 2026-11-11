using DSPRE.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Screens
{
    /// <summary>
    /// Draws the platforms Pokémon stand on in battle, from <see cref="DirNames.BattleObj"/>: one cell layout shared by every
    /// terrain, with each terrain's tiles and palette, at the game's platform positions.
    /// </summary>
    public sealed class BattleGroundRenderer
    {
        public sealed class GroundImage
        {
            public byte[] Rgba; public int Width, Height, Left, Top;
        }

        // terrain id to label.
        private static readonly string[] DpTerrainNames =
            { "Gravel", "Sand", "Lawn", "Pool", "Rock", "Cave", "Snow", "Water", "Ice", "Floor", "Marsh" };
        private static readonly string[] PtTerrainNames =
            { "Gravel", "Sand", "Lawn", "Pool", "Rock", "Cave", "Snow", "Water", "Ice", "Floor", "Marsh",
              "Bridge", "Aaron", "Bertha", "Flint", "Lucian", "Cynthia", "Distortion World",
              "Battle Tower", "Battle Factory", "Battle Arcade", "Battle Castle", "Battle Hall", "Giratina" };
        private static readonly string[] HgssTerrainNames =
            { "Gravel", "Sand", "Lawn", "Pool", "Rock", "Cave", "Snow", "Water", "Ice", "Floor", "Marsh",
              "Unused", "Will", "Koga", "Bruno", "Karen", "Lance", "Distortion World",
              "Battle Tower", "Battle Factory", "Battle Arcade", "Battle Castle", "Battle Hall", "Giratina" };
        public static string[] TerrainNames => GameFamily switch
        {
            GameFamilies.DP => DpTerrainNames,
            GameFamilies.HGSS => HgssTerrainNames,
            _ => PtTerrainNames,
        };

        // terrain id to platform graphic set per side. Id 11 draws the player's platform from set 10, and the opponent's platform and colours from set 8.
        private static readonly int[] DpGroundGfx = { 2, 7, 0, 10, 4, 9, 5, 1, 3, 6, 8 };
        private static readonly int[] PtHgssPlayerGfx =
            { 2, 7, 0, 10, 4, 9, 5, 1, 3, 6, 8, 10, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23 };
        private static readonly int[] PtHgssOpponentGfx =
            { 2, 7, 0, 10, 4, 9, 5, 1, 3, 6, 8, 8, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23 };
        private static int[] PlayerGfx => GameFamily == GameFamilies.DP ? DpGroundGfx : PtHgssPlayerGfx;
        private static int[] OpponentGfx => GameFamily == GameFamilies.DP ? DpGroundGfx : PtHgssOpponentGfx;


        // Cell origin on screen; a 256 canvas drawn at pos - 128 puts its centre there.
        private const int PlayerX = 64, PlayerY = 128 + 8, OpponentX = 24 * 8, OpponentY = 8 * 11, Canvas = 256;

        public static int TerrainCount => TerrainNames.Length;

        /// <summary>
        /// Which files make up the ground one Pokemon stands on, found by the names the game gives
        /// them. The numbers differ per game, so a constant is right for one family and wrong for the
        /// others. Platform 0 has the layout every terrain uses.
        /// </summary>
        public static (int PlayerDrawing, int OpponentDrawing, int PlayerLayout, int OpponentLayout, int PaletteDay)?
            TerrainFiles(int terrainId)
        {
            if (terrainId < 0 || terrainId >= PlayerGfx.Length) return null;
            int playerSet = PlayerGfx[terrainId], opponent = OpponentGfx[terrainId];
            int playerDraw = BattleObjects.Find($"Platform.{playerSet}.Player", "Drawing");
            int opponentDraw = BattleObjects.Find($"Platform.{opponent}.Opponent", "Drawing");
            int playerLayout = BattleObjects.Find("Platform.0.Player", "As it appears");
            int opponentLayout = BattleObjects.Find("Platform.0.Opponent", "As it appears");
            int palDay = BattleObjects.Find($"Platform.{opponent}.Day", "Colours");
            if (playerDraw < 0 || opponentDraw < 0 || playerLayout < 0 || opponentLayout < 0 || palDay < 0) return null;
            return (playerDraw, opponentDraw, playerLayout, opponentLayout, palDay);
        }

        // A default only: backdrop and ground are set independently per zone, and platform numbering parallels backdrop numbering.
        public static int BackdropForTerrain(int terrainId)
        {
            if (terrainId < 0 || terrainId >= PlayerGfx.Length) return -1;
            // Ice platforms are drawn as set 3, the forest's number, but ice floors are indoors: Snowpoint Gym uses Indoors 1.
            if (terrainId == TerrainIce) return Math.Min(BackdropIndoors1, BattleBgRenderer.BackdropCount - 1);
            return Math.Min(PlayerGfx[terrainId], BattleBgRenderer.BackdropCount - 1);
        }

        private const int TerrainIce = 8, BackdropIndoors1 = 6;

        private readonly ScriptNarc _narc = new ScriptNarc(DirNames.BattleObj);
        public bool Available => _narc.Available;

        /// <summary>Builds the (player, opponent) ground platforms for a terrain (a terrain id), or (null,null) if the
        /// archive is unmapped/missing. <paramref name="timeZone"/> 0=day,1=evening,2=night selects the palette.</summary>
        public (GroundImage player, GroundImage opponent) Build(int terrainId, int timeZone = 0)
        {
            if (!_narc.Available) return (null, null);
            (int PlayerDrawing, int OpponentDrawing, int PlayerLayout, int OpponentLayout, int PaletteDay)? files = TerrainFiles(terrainId);
            if (files == null) return (null, null);
            int tz = Math.Clamp(timeZone, 0, 2);
            (int PlayerDrawing, int OpponentDrawing, int PlayerLayout, int OpponentLayout, int PaletteDay) f = files.Value;
            GroundImage player = Render(f.PlayerDrawing, f.PaletteDay + tz, f.PlayerLayout, PlayerX, PlayerY);
            GroundImage opponent = Render(f.OpponentDrawing, f.PaletteDay + tz, f.OpponentLayout, OpponentX, OpponentY);
            return (player, opponent);
        }

        // Only the static single-battle gauge frame; the fill and text are overlaid in the UI.
        public GroundImage BuildGauge(bool player)
        {
            if (!_narc.Available) return null;
            // Found by name because file numbers differ per game.
            string thing = player ? "HpBar.Player" : "HpBar.Opponent";
            int drawing = BattleObjects.Find(thing, "Drawing");
            int layout = BattleObjects.Find(thing, "As it appears");
            int colours = BattleObjects.Find("HpBar.Shared", "Colours");
            if (drawing < 0 || layout < 0 || colours < 0) return null;
            (int X, int Y) at = BattleGaugeComposer.CentreOf(player ? BattleGaugeComposer.Kind.PlayerSingle : BattleGaugeComposer.Kind.OpponentSingle);
            return Render(drawing, colours, layout, at.X, at.Y);
        }

        private GroundImage Render(int ncgrIdx, int nclrIdx, int ncerIdx, int posX, int posY)
        {
            try
            {
                byte[] chr = NitroBgCodec.Inflate(_narc.Get(ncgrIdx));
                byte[] pal = NitroBgCodec.Inflate(_narc.Get(nclrIdx));
                byte[] cel = NitroBgCodec.Inflate(_narc.Get(ncerIdx));
                if (chr == null || pal == null || cel == null) return null;
                RawImage raw = CellRenderer.Draw(NcerFile.Read(cel), 0, chr, NitroBgCodec.ReadColours(pal), Canvas, Canvas);
                if (raw == null || raw.IsEmpty) return null;
                return new GroundImage { Rgba = ToRgba(raw, Canvas), Width = Canvas, Height = Canvas, Left = posX - Canvas / 2, Top = posY - Canvas / 2 };
            }
            catch (Exception ex) { AppLogger.Error("BattleGroundRenderer.Render failed: " + ex.Message); return null; }
        }

        // RawImage BGRA → straight RGBA byte[s*s*4].
        private static byte[] ToRgba(DSPRE.RawImage raw, int s)
        {
            byte[] outp = new byte[s * s * 4];
            if (raw == null || raw.IsEmpty) return outp;
            int bw = Math.Min(s, raw.Width), bh = Math.Min(s, raw.Height);
            for (int y = 0; y < bh; y++)
            {
                for (int x = 0; x < bw; x++)
                {
                    int si = (y * raw.Width + x) * 4, di = (y * s + x) * 4;   // BGRA → RGBA
                    outp[di + 0] = raw.Bgra[si + 2]; outp[di + 1] = raw.Bgra[si + 1];
                    outp[di + 2] = raw.Bgra[si + 0]; outp[di + 3] = raw.Bgra[si + 3];
                }
            }
            return outp;
        }
    }
}
