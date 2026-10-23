using LibNDSFormats.NSBMD;
using System;
using System.IO;
using System.Text;

namespace DSPRE
{
    public static class NSBUtils
    {
        public const int NsbmdDoesnthaveTexture = 0;
        public const int NsbmdHasTexture = 1;

        public static byte[] BuildNSBMDwithTextures(byte[] nsbmd, byte[] nsbtx)
        {
            byte[] wholeMDL0 = GetFirstBlock(nsbmd);
            byte[] wholeTEX0 = GetFirstBlock(nsbtx);

            MemoryStream ms = new MemoryStream();
            using (BinaryWriter msWriter = new BinaryWriter(ms))
            {
                msWriter.Write(NSBMD.NdsTypeBmd0);
                msWriter.Write(NSBMD.NdsTypeByteorder);
                msWriter.Write(NSBMD.NdsTypeUnk2);

                ushort nBlocks = 2;
                uint modelLength = (uint)(wholeMDL0.Length + NSBMD.HEADERSIZE + 4 * nBlocks);
                msWriter.Write((uint)(modelLength + wholeTEX0.Length));
                msWriter.Write(NSBMD.HEADERSIZE); //Header size, always 16
                msWriter.Write(nBlocks); //Number of blocks, now it's 2 because we are inserting textures

                msWriter.Write((uint)(msWriter.BaseStream.Position + 4 * nBlocks)); // absolute offset to the model data; the texture offset follows

                msWriter.Write(modelLength); //Copy offset to TEX0
                msWriter.Write(wholeMDL0);
                msWriter.Write(wholeTEX0);
            }
            return ms.ToArray();
        }
        public static byte[] BuildNSBTXHeader(uint texturesSize)
        {
            MemoryStream ms = new MemoryStream();

            using (BinaryWriter bw = new BinaryWriter(ms))
            {
                bw.Write(Encoding.UTF8.GetBytes("BTX0"));
                bw.Write((ushort)0xFEFF); // Byte order
                bw.Write((ushort)0x0001); // unknown, always 1
                bw.Write(texturesSize);
                bw.Write((short)0x10); //Header size 
                bw.Write((short)0x01); // sub-file count
                bw.Write((uint)0x14); // Offset to sub-file
            }
            return ms.ToArray();
        }
        public static int CheckNSBMDHeader(byte[] modelFile)
        {
            using (BinaryReader byteArrReader = new BinaryReader(new MemoryStream(modelFile)))
            {
                if (modelFile.Length < 0x10 || byteArrReader.ReadUInt32() != NSBMD.NdsTypeBmd0)
                {
                    AppMessages.Error("Pick an NSBMD file.", "Not an NSBMD file");
                    return -1;
                }

                byteArrReader.BaseStream.Position = 0xE;
                return byteArrReader.ReadInt16() >= 2 ? NsbmdHasTexture : NsbmdDoesnthaveTexture;
            }
        }

        // Length covers the block offset GetFirstBlock reads at 0x10.
        public static bool IsNSBTX(byte[] file)
        {
            return file != null && file.Length >= 0x14
                && file[0] == (byte)'B' && file[1] == (byte)'T' && file[2] == (byte)'X' && file[3] == (byte)'0';
        }

        public static byte[] GetModelWithoutTextures(byte[] modelFile)
        {
            byte[] nsbmdHeaderData;
            uint mdl0Size;
            byte[] mdl0Data;

            using (BinaryReader modelReader = new BinaryReader(new MemoryStream(modelFile)))
            {
                modelReader.BaseStream.Position = 0x0;
                nsbmdHeaderData = modelReader.ReadBytes(0x8);

                modelReader.BaseStream.Position = 0x1C;
                mdl0Size = modelReader.ReadUInt32();

                modelReader.BaseStream.Position = 0x18;
                mdl0Data = modelReader.ReadBytes((int)mdl0Size);
            }

            MemoryStream output = new MemoryStream();
            using (BinaryWriter writer = new BinaryWriter(output))
            {

                writer.Write(nsbmdHeaderData); // the same for every NSBMD
                writer.Write(mdl0Size + 0x14);
                writer.Write((short)0x10); // BMD0 header size
                writer.Write((short)0x1); // one sub-file, since the embedded textures are removed
                writer.Write((uint)0x14); // MDL0 start

                writer.Write(mdl0Data);
            }
            return output.ToArray();
        }

        public static byte[] GetTexturesFromTexturedNSBMD(byte[] modelFile)
        {
            using (BinaryReader byteArrReader = new BinaryReader(new MemoryStream(modelFile)))
            {
                byteArrReader.BaseStream.Position = 14;
                if (byteArrReader.ReadUInt16() < 2) //No textures
                    return new byte[0];

                byteArrReader.BaseStream.Position = 20;
                int texAbsoluteOffset = byteArrReader.ReadInt32();

                byteArrReader.BaseStream.Position = texAbsoluteOffset + 4;
                uint textureSize = byteArrReader.ReadUInt32();

                byte[] nsbtxHeader = NSBUtils.BuildNSBTXHeader(20 + textureSize);
                byte[] texData = DSUtils.ReadFromByteArray(modelFile, readFrom: texAbsoluteOffset);

                byte[] output = new byte[nsbtxHeader.Length + texData.Length];
                Buffer.BlockCopy(nsbtxHeader, 0, output, 0, nsbtxHeader.Length);
                Buffer.BlockCopy(texData, 0, output, nsbtxHeader.Length, texData.Length);
                return output;
            }
        }
        private static byte[] GetFirstBlock(byte[] NSBFile)
        {
            int blockSize;
            uint offsetToMainBlock;
            using (BinaryReader reader = new BinaryReader(new MemoryStream(NSBFile)))
            {
                reader.BaseStream.Position = 16;
                offsetToMainBlock = reader.ReadUInt32();

                reader.BaseStream.Position = offsetToMainBlock + 4;
                blockSize = reader.ReadInt32();
            }
            byte[] blockData = new byte[blockSize];
            Buffer.BlockCopy(NSBFile, (int)offsetToMainBlock, blockData, 0, blockSize);

            return blockData;
        }
    }
}