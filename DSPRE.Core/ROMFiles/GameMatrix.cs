using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
   /* ---------------------- MATRIX DATA STRUCTURE (DPPtHGSS):-----------------------------

   0x0  //  byte:       Matrix width (a.k.a row length) (x)
   0x1  //  byte:       Matrix height (a.k.a number of rows per section) (y)
   0x2  //  byte:       Headers section boolean (0 = not present, 1 = present)
   0x3  //  byte:       Altitudes section flag (0 = not present, 1 = present)
   0x4  //  byte:       Length of matrix name string
   0x5  //  string:     Matrix name string (UTF-8 Encoded)
   --   //              [Header section if applicable: y blocks of length x]
   --   //              [Altitudes section if applicable: y blocks of length x]
   --   //              [Map files section: y blocks of length x]

   -------------------------------------------------------------------------------------- */

    /// <summary>
    /// Class to store map matrix data from Pok�mon NDS games
    /// </summary>
    public class GameMatrix: RomFile
    {
        #region Fields (8)
        public static readonly string DefaultFilter = "Game Matrix File (*.mtx)|*.mtx";


        public bool HasHeadersSection { get; set; }
        public bool HasHeightsSection { get; set; }
        public string Name { get; set; }
        public byte Width { get; set; }
        public byte Height { get; set; }
        public int? Id { get; } = null;

        public ushort[,] Headers;
        public byte[,] Altitudes;
        public ushort[,] Maps;

        public static readonly ushort EMPTY = 65535;
        #endregion Fields

        /// <summary>The squares this header owns, or null when the matrix has no headers section to say.</summary>
        public HashSet<(int x, int y)> CellsOfHeader(int headerId)
        {
            if (!HasHeadersSection || headerId < 0) return null;
            HashSet<(int x, int y)> set = new HashSet<(int x, int y)>();
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    if (Headers[y, x] == headerId)
                        set.Add((x, y));
            return set.Count > 0 ? set : null;
        }

        #region Constructors(1)
        public GameMatrix(Stream data)
        {
            using (BinaryReader reader = new BinaryReader(data))
            {
                /* Read matrix size and sections included */
                Width = reader.ReadByte();
                Height = reader.ReadByte();

                if (reader.ReadBoolean())
                {
                    HasHeadersSection = true;
                }

                if (reader.ReadBoolean())
                {
                    HasHeightsSection = true;
                }

                /* Read matrix's name */
                byte nameLength = reader.ReadByte();
                Name = Encoding.UTF8.GetString(reader.ReadBytes(nameLength));

                /* Initialize section arrays */
                Headers = new ushort[Height, Width];
                Altitudes = new byte[Height, Width];
                Maps = new ushort[Height, Width];

                /* Read sections */
                if (HasHeadersSection)
                {
                    for (int i = 0; i < Height; i++)
                    {
                        for (int j = 0; j < Width; j++)
                        {
                            Headers[i, j] = reader.ReadUInt16();
                        }
                    }
                }

                if (HasHeightsSection)
                {
                    for (int i = 0; i < Height; i++)
                    {
                        for (int j = 0; j < Width; j++)
                        {
                            Altitudes[i, j] = reader.ReadByte();
                        }
                    }
                }

                for (int i = 0; i < Height; i++)
                {
                    for (int j = 0; j < Width; j++)
                    {
                        Maps[i, j] = reader.ReadUInt16();
                    }
                }
            }
        }
        public GameMatrix(int ID) : this (new FileStream(Path.Combine(RomInfo.GameDirs[DirNames.Matrices].unpackedDir, ID.ToString("D4")), FileMode.Open))
        {
            this.Id = ID;
        }

        public GameMatrix(GameMatrix copy, int newID)
        { 
            this.Id = newID;
            this.Name = copy.Name;
            this.Width = copy.Width;
            this.Height = copy.Height;

            this.HasHeadersSection = copy.HasHeadersSection;
            this.HasHeightsSection = copy.HasHeightsSection;

            this.Maps = (ushort[,])copy.Maps.Clone();
            this.Altitudes = (byte[,])copy.Altitudes.Clone();
            this.Headers = (ushort[,])copy.Headers.Clone();
        }

        public GameMatrix()
        {
            this.Name = "newMatrix";
            this.HasHeadersSection = false;
            this.HasHeightsSection = false;

            this.Width = 1;
            this.Height = 1;
            this.Maps = new ushort[1, 1] { { 0 } };
        }
        #endregion

        #region Methods (6)
        public void ResizeMatrix(int newHeight, int newWidth)
        {
            /*  Initialize new arrays   */
            ushort[,] newHeaders = new ushort[newHeight, newWidth];
            byte[,] newAltitudes = new byte[newHeight, newWidth];
            ushort[,] newMaps = new ushort[newHeight, newWidth];

            /* Copy existing headers and altitudes rows into new arrays. If new matrix is greater in any dimension, new entries will be zero */
            if (HasHeadersSection)
            {
                for (int i = 0; i < Math.Min(Height, newHeight); i++)
                {
                    for (int j = 0; j < Math.Min(Width, newWidth); j++)
                    {
                        newHeaders[i, j] = Headers[i, j];
                    }
                }
            }

            if (HasHeightsSection)
            {
                for (int i = 0; i < Math.Min(Height, newHeight); i++)
                {
                    for (int j = 0; j < Math.Min(Width, newWidth); j++)
                    {
                        newAltitudes[i, j] = Altitudes[i, j];
                    }
                }
            }

            /* Copy existing map rows into new array, and fill eventual new ones with Matrix.EMPTY (FF FF) */
            for (int i = 0; i < Math.Min(Height, newHeight); i++)
            {
                for (int j = 0; j < Math.Min(Width, newWidth); j++)
                {
                    newMaps[i, j] = Maps[i, j];
                }
            }

            if (newHeight > Height)
            {
                for (int i = Height; i < newHeight; i++)
                {
                    for (int j = 0; j < newWidth; j++)
                    {
                        newMaps[i, j] = GameMatrix.EMPTY;
                    }
                }
            }

            if (newWidth > Width)
            {
                for (int j = Width; j < newWidth; j++)
                {
                    for (int i = 0; i < newHeight; i++)
                    {
                        newMaps[i, j] = GameMatrix.EMPTY;
                    }
                }
            }

            /* Substitute old arrays with new arrays */
            Headers = newHeaders;
            Altitudes = newAltitudes;
            Maps = newMaps;

            /* Set new width and height */
            Height = (byte)newHeight;
            Width = (byte)newWidth;
        }

        public override string ToString()
        {
            return (this.Id == null ? "" : Id.ToString()) + ": " + this.Name;
        }
        public override byte[] ToByteArray()
        {
            MemoryStream newData = new MemoryStream();
            using (BinaryWriter writer = new BinaryWriter(newData))
            {
                writer.Write(Width);
                writer.Write(Height);
                writer.Write(HasHeadersSection);
                writer.Write(HasHeightsSection);
                writer.Write(Name);

                if (HasHeadersSection)
                {
                    for (int i = 0; i < Height; i++)
                    {
                        for (int j = 0; j < Width; j++)
                        {
                            writer.Write(Headers[i, j]);
                        }
                    }
                }

                if (HasHeightsSection)
                {
                    for (int i = 0; i < Height; i++)
                    {
                        for (int j = 0; j < Width; j++)
                        {
                            writer.Write(Altitudes[i, j]);
                        }
                    }
                }

                for (int i = 0; i < Height; i++)
                {
                    for (int j = 0; j < Width; j++)
                    {
                        writer.Write(Maps[i, j]);
                    }
                }

            }
            return newData.ToArray();
        }
        public void SaveToFileDefaultDir(int IDtoReplace, bool showSuccessMessage = true)
        {
            SaveToFileDefaultDir(DirNames.Matrices, IDtoReplace, showSuccessMessage);
        }
        public void SaveToFileExplorePath(string suggestedFileName, bool showSuccessMessage = true)
        {
            SaveToFileExplorePath("Gen IV Matrix File", "mtx", suggestedFileName, showSuccessMessage);
        }
        #endregion
    }
}
