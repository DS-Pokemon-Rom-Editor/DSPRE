// Loader for NSBTX files/data.
// Code adapted from kiwi.ds' NSBMD Model Viewer.

using System;
using System.Collections.Generic;
using System.IO;
using LibNDSFormats.NSBMD;
using System.Linq;
namespace LibNDSFormats.NSBTX
{
    /// <summary>
    /// Loader for NSBTX files & data.
    /// </summary>
    public static class NSBTXLoader
    {
        public static object MessageBoxIcons { get; private set; }
        #region Methods (2)

        // Public Methods (2) 

        /// <summary>
        /// Load NSBTX from stream.
        /// </summary>
        /// <param name="stream">Stream to use.</param>
        /// <returns>Material definitions.</returns>
        public static IEnumerable<NSBMDMaterial> LoadNsbtx(Stream stream, out List<NSBMDTexture> texs, out List<NSBMDPalette> pals)
        {
            texs = new List<NSBMDTexture>();
            pals = new List<NSBMDPalette>();
            List<NSBMDMaterial> materials = new List<NSBMDMaterial>();
            BinaryReader reader = new BinaryReader(stream);
            int id = reader.ReadInt32();
            if (id != NSBMD.NSBMD.NdsTypeBtx0)
            {
                //Console.WriteLine("The header of this texture file is null!!!");
                return null;
            }

            reader.BaseStream.Position += 2;
            int i = reader.ReadUInt16();
            if (i == NSBMD.NSBMD.NdsTypeUnk1)
            {
                i = reader.ReadInt32();

                if (i == stream.Length)
                {
                    int numblock = reader.ReadInt32();
                    numblock >>= 16;
                    int r = reader.ReadInt32();
                    id = reader.ReadInt32();

                    if (numblock == 1 && r == 0x14)
                    {
                        int texnum;
                        int palnum;
                        materials.AddRange(ReadTex0(stream, 0x14, out texnum, out palnum, out texs, out pals));
                    }
                }
            }
            return materials;
        }

        /// <summary>
        /// Load NSBTX from file.
        /// </summary>
        /// <param name="stream">File to use.</param>
        /// <returns>Material definitions.</returns>
        public static IEnumerable<NSBMDMaterial> LoadNsbtx(FileInfo fileInfo, out List<NSBMDTexture> texs, out List<NSBMDPalette> pals)
        {
            texs = new List<NSBMDTexture>();
            pals = new List<NSBMDPalette>();
            IEnumerable<NSBMDMaterial> result = null;
            using (FileStream fileStream = new FileStream(fileInfo.FullName, FileMode.Open))
            {
                result = LoadNsbtx(fileStream, out texs, out pals);
            }
            return result;
        }

        /// <summary>
        /// Load materials in stream.
        /// </summary>
        /// <param name="stream">Stream to use.</param>
        /// <returns>Material definitions.</returns>
        public static IEnumerable<NSBMDMaterial> ReadTex0(Stream stream, int blockoffset, out int ptexnum, out int ppalnum, out List<NSBMDTexture> texs, out List<NSBMDPalette> pals)
        {
            byte[] data;
            long position = stream.Position;
            using (MemoryStream copy = new MemoryStream())
            {
                stream.Position = 0;
                stream.CopyTo(copy);
                data = copy.ToArray();
            }
            stream.Position = position;

            DSPRE.ROMFiles.Tex0Block block = DSPRE.ROMFiles.Tex0Block.Read(data, blockoffset, out string error);
            if (block == null) throw new InvalidDataException(error);

            texs = new List<NSBMDTexture>();
            pals = new List<NSBMDPalette>();
            int texnum = block.Textures.Count, palnum = block.Palettes.Count;
            // One material per texture and palette slot, paired by index, as the model viewers expect.
            NSBMDMaterial[] material = new NSBMDMaterial[Math.Max(texnum, palnum)];
            for (int i = 0; i < material.Length; i++)
                material[i] = new NSBMDMaterial();

            for (int i = 0; i < texnum; i++)
            {
                DSPRE.ROMFiles.Tex0Block.Texture t = block.Textures[i];
                NSBMDMaterial mat = material[i];
                mat.Texname = t.Name;
                mat.Format = t.Format;
                mat.Width = t.Width;
                mat.Height = t.Height;
                mat.Color0 = t.Color0 ? 1 : 0;
                mat.Texoffset = (uint)t.DataAt;
                mat.Texsize = (uint)t.DataSize;
                mat.Texdata = Slice(data, t.DataAt, t.DataSize);
                if (t.Format == 5) mat.Spdata = Slice(data, t.IndexAt, t.IndexSize);
                texs.Add(new NSBMDTexture
                {
                    Texname = t.Name, Format = t.Format, Width = t.Width, Height = t.Height, Color0 = mat.Color0,
                    Texsize = mat.Texsize, Texdata = mat.Texdata, Spdata = mat.Spdata,
                });
            }

            for (int i = 0; i < palnum; i++)
            {
                DSPRE.ROMFiles.Tex0Block.Palette p = block.Palettes[i];
                NSBMDMaterial mat = material[i];
                mat.Palname = p.Name;
                mat.Paloffset = (uint)p.DataAt;
                mat.Palsize = (uint)p.DataSize;
                RGBA[] colours = new RGBA[p.DataSize / 2];
                for (int j = 0; j < colours.Length && p.DataAt + j * 2 + 2 <= data.Length; j++)
                {
                    ushort c = (ushort)(data[p.DataAt + j * 2] | data[p.DataAt + j * 2 + 1] << 8);
                    colours[j].R = (byte)((c & 0x1F) << 3);
                    colours[j].G = (byte)(((c >> 5) & 0x1F) << 3);
                    colours[j].B = (byte)(((c >> 10) & 0x1F) << 3);
                    colours[j].A = (c & 0x8000) == 0 ? (byte)0xFF : (byte)0;
                }
                mat.Paldata = colours;
                pals.Add(new NSBMDPalette { Palname = p.Name, Paloffset = mat.Paloffset, Palsize = mat.Palsize, Paldata = colours });
            }

            ptexnum = texnum;
            ppalnum = palnum;
            return material;
        }

        private static byte[] Slice(byte[] data, int at, int length) =>
            at >= 0 && at <= data.Length ? data.AsSpan(at, Math.Min(length, data.Length - at)).ToArray() : Array.Empty<byte>();

        #endregion Methods
    }
}