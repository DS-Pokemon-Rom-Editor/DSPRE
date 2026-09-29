using System.Drawing;
using Ekona.Images;
using static DSPRE.RomInfo;

namespace DSPRE
{
    /// <summary>
    /// Which files of the trainer graphics archives belong to one trainer class. Diamond and Pearl
    /// keep two files a class, a drawing and its colours; Platinum and HeartGold keep five and add
    /// cells, an animation and a second drawing.
    /// </summary>
    public static class TrainerGraphicsLayout
    {
        public static int Stride
        {
            get { return gameFamily == GameFamilies.DP ? 2 : 5; }
        }

        /// <summary>False on Diamond and Pearl, where a class is one flat picture with no cell data.</summary>
        public static bool HasCells
        {
            get { return gameFamily != GameFamilies.DP; }
        }

        /// <summary>
        /// Diamond and Pearl store the drawing XORed against a rolling key, as they do their Pokemon
        /// battle sprites. Platinum and HeartGold store trainer pixels plainly.
        /// </summary>
        public static bool PixelsAreScrambled
        {
            get { return gameFamily == GameFamilies.DP; }
        }

        public static int DrawingEntry(int trClassID)
        {
            return trClassID * Stride;
        }

        public static int ColoursEntry(int trClassID)
        {
            return trClassID * Stride + 1;
        }

        /// <summary>-1 where the class has no cells.</summary>
        public static int CellsEntry(int trClassID)
        {
            return HasCells ? trClassID * Stride + 2 : -1;
        }

        /// <summary>
        /// The whole drawing, cropped to the half it actually uses and with colour zero taken out. The
        /// file is twice as wide as the picture in it, every class of it, and the preview boxes centre
        /// what they are given at its own size, so handing over the full width cuts the left off.
        /// </summary>
        public static Image FlatPicture(ImageBase tile, PaletteBase colours)
        {
            if (tile == null || colours == null)
            {
                return null;
            }

            Bitmap whole = new Bitmap(tile.Get_Image(colours));
            if (colours.NumberOfPalettes > 0 && colours.Palette[0].Length > 0)
            {
                whole.MakeTransparent(colours.Palette[0][0]);
            }

            int drawn = whole.Width / 2;
            if (drawn <= 0 || drawn >= whole.Width)
            {
                return whole;
            }

            Bitmap cropped = whole.Clone(new Rectangle(0, 0, drawn, whole.Height), whole.PixelFormat);
            whole.Dispose();
            return cropped;
        }

        /// <summary>Undoes the rolling-key XOR in place, running backwards from the last halfword.</summary>
        public static void Unscramble(byte[] data)
        {
            if (data == null)
            {
                return;
            }

            int words = data.Length / 2;
            if (words <= 0)
            {
                return;
            }

            unchecked
            {
                uint key = (uint)(data[(words - 1) * 2] | (data[(words - 1) * 2 + 1] << 8));
                for (int i = words - 1; i >= 0; i--)
                {
                    ushort v = (ushort)(data[i * 2] | (data[i * 2 + 1] << 8));
                    v = (ushort)(v ^ (ushort)(key & 0xFFFF));
                    data[i * 2] = (byte)(v & 0xFF);
                    data[i * 2 + 1] = (byte)(v >> 8);
                    key = key * 1103515245 + 24691;
                }
            }
        }
    }
}
