using System;
using System.Collections.Generic;

namespace DSPRE.ROMFiles
{
    /// <summary>The DS's LZ77 (type 0x10) codec. The compressor is Nintenlord's.</summary>
    public static class Lz77
    {
        static private unsafe int[] Search(byte* source, int position, int lenght) //Function taken from Nintenlord's compressor. All credits for this code goes to Nintenlord!
        {
            int SlidingWindowSize = 4096;
            int ReadAheadBufferSize = 18;

            if (position >= lenght)
                return new int[2] { -1, 0 };
            if ((position < 2) || ((lenght - position) < 2))
                return new int[2] { 0, 0 };

            List<int> results = new List<int>();

            for (int i = 1; (i < SlidingWindowSize) && (i < position); i++)
            {
                if (source[position - (i + 1)] == source[position])
                {
                    results.Add(i + 1);
                }
            }
            if (results.Count == 0)
                return new int[2] { 0, 0 };

            int amountOfBytes = 0;

            // A match may not run past the end of the data: the games' decompressor finishes every copy it starts,
            // so an overlong last match writes past the buffer the declared size allocated.
            int most = Math.Min(ReadAheadBufferSize, lenght - position);
            bool Continue = true;
            while (amountOfBytes < most && Continue)
            {
                amountOfBytes++;
                if (amountOfBytes == most) break;
                for (int i = results.Count - 1; i >= 0; i--)
                {
                    if (source[position + amountOfBytes] != source[position - results[i] + (amountOfBytes % results[i])])
                    {
                        if (results.Count > 1)
                            results.RemoveAt(i);
                        else
                            Continue = false;
                    }
                }
            }
            return new int[2] { amountOfBytes, results[0] }; //lenght of data is first, then position
        }

        static public unsafe byte[] Compress(byte[] source, bool header = false)//Function taken from Nintenlord's compressor. All credits for this code goes to Nintenlord!
        {
            fixed (byte* pointer = &source[0])
            {
                return Compress(pointer, source.Length, header);
            }
        }

        static public unsafe byte[] Compress(byte* source, int lenght, bool header = false) //Function taken from Nintenlord's compressor. All credits for this code goes to Nintenlord!
        {
            int position = 0;
            int BlockSize = 8;

            List<byte> CompressedData = new List<byte>();
            if (header) //0x37375A4C
            {
                CompressedData.Add(0x4C);
                CompressedData.Add(0x5A);
                CompressedData.Add(0x37);
                CompressedData.Add(0x37);
            }
            CompressedData.Add(0x10);

            {
                byte* pointer = (byte*)&lenght;
                for (int i = 0; i < 3; i++)
                    CompressedData.Add(*(pointer++));
            }

            while (position < lenght)
            {
                byte isCompressed = 0;
                List<byte> tempList = new List<byte>();

                for (int i = 0; i < BlockSize; i++)
                {
                    int[] searchResult = Search(source, position, lenght);

                    if (searchResult[0] > 2)
                    {
                        byte add = (byte)((((searchResult[0] - 3) & 0xF) << 4) + (((searchResult[1] - 1) >> 8) & 0xF));
                        tempList.Add(add);
                        add = (byte)((searchResult[1] - 1) & 0xFF);
                        tempList.Add(add);
                        position += searchResult[0];
                        isCompressed |= (byte)(1 << (BlockSize - (i + 1)));
                    }
                    else if (searchResult[0] >= 0)
                        tempList.Add(source[position++]);
                    else
                        break;
                }
                CompressedData.Add(isCompressed);
                CompressedData.AddRange(tempList);
            }
            while (CompressedData.Count % 4 != 0)
                CompressedData.Add(0);

            return CompressedData.ToArray();
        }

        public static byte[] FastCompress(byte[] source)
        {
            int DataLen = 4;
            DataLen += source.Length;
            DataLen += (int)Math.Ceiling((double)source.Length / 8);
            byte[] dest = new byte[DataLen];

            dest[0] = 0;
            dest[1] = (byte)(source.Length & 0xFF);
            dest[2] = (byte)((source.Length >> 8) & 0xFF);
            dest[3] = (byte)((source.Length >> 16) & 0xFF);

            int FilePos = 4;
            int UntilNext = 0;

            for (int SrcPos = 0; SrcPos < source.Length; SrcPos++)
            {
                if (UntilNext == 0)
                {
                    dest[FilePos] = 0;
                    FilePos++;
                    UntilNext = 8;
                }
                dest[FilePos] = source[SrcPos];
                FilePos++;
                UntilNext -= 1;
            }

            return dest;
        }

        /// <summary>
        /// Whether an LZ10 stream produces exactly its declared size. A copy that runs past it decodes fine here but
        /// overruns the game's buffer.
        /// </summary>
        public static bool EndsAtDeclaredSize(byte[] source)
        {
            if (source == null || source.Length < 4 || source[0] != 0x10) return false;
            int size = source[1] | (source[2] << 8) | (source[3] << 16);
            int xin = 4, made = 0;
            while (made < size)
            {
                if (xin >= source.Length) return false;
                byte flags = source[xin++];
                for (int bit = 0; bit < 8 && made < size; bit++)
                {
                    if ((flags & (0x80 >> bit)) == 0) { if (xin >= source.Length) return false; xin++; made++; continue; }
                    if (xin + 1 >= source.Length) return false;
                    int data = (source[xin] << 8) | source[xin + 1];
                    xin += 2;
                    if ((data & 0xFFF) + 1 > made) return false;
                    made += (data >> 12) + 3;
                }
            }
            return made == size;
        }

        public static byte[] Decompress(byte[] source)
        {
            // This code converted from Elitemap 
            int DataLen;
            DataLen = source[1] | (source[2] << 8) | (source[3] << 16);
            byte[] dest = new byte[DataLen];
            int i, j, xin, xout;
            xin = 4;
            xout = 0;
            int length, offset, windowOffset, data;
            byte d;
            while (DataLen > 0)
            {
                d = source[xin++];
                if (d != 0)
                {
                    for (i = 0; i < 8; i++)
                    {
                        if ((d & 0x80) != 0)
                        {
                            data = ((source[xin] << 8) | source[xin + 1]);
                            xin += 2;
                            length = (data >> 12) + 3;
                            offset = data & 0xFFF;
                            windowOffset = xout - offset - 1;
                            for (j = 0; j < length; j++)
                            {
                                dest[xout++] = dest[windowOffset++];
                                DataLen--;
                                if (DataLen == 0)
                                {
                                    return dest;
                                }
                            }
                        }
                        else
                        {
                            dest[xout++] = source[xin++];
                            DataLen--;
                            if (DataLen == 0)
                            {
                                return dest;
                            }
                        }
                        d <<= 1;
                    }
                }
                else
                {
                    for (i = 0; i < 8; i++)
                    {
                        dest[xout++] = source[xin++];
                        DataLen--;
                        if (DataLen == 0)
                        {
                            return dest;
                        }
                    }
                }
            }
            return dest;
        }

        public static byte[] DecompressWithHeader(byte[] source)
        {
            // This code converted from Elitemap 
            int DataLen;
            DataLen = source[5] | (source[6] << 8) | (source[7] << 16);
            byte[] dest = new byte[DataLen];
            int i, j, xin, xout;
            xin = 8;
            xout = 0;
            int length, offset, windowOffset, data;
            byte d;
            while (DataLen > 0)
            {
                d = source[xin++];
                if (d != 0)
                {
                    for (i = 0; i < 8; i++)
                    {
                        if ((d & 0x80) != 0)
                        {
                            data = ((source[xin] << 8) | source[xin + 1]);
                            xin += 2;
                            length = (data >> 12) + 3;
                            offset = data & 0xFFF;
                            windowOffset = xout - offset - 1;
                            for (j = 0; j < length; j++)
                            {
                                dest[xout++] = dest[windowOffset++];
                                DataLen--;
                                if (DataLen == 0)
                                {
                                    return dest;
                                }
                            }
                        }
                        else
                        {
                            dest[xout++] = source[xin++];
                            DataLen--;
                            if (DataLen == 0)
                            {
                                return dest;
                            }
                        }
                        d <<= 1;
                    }
                }
                else
                {
                    for (i = 0; i < 8; i++)
                    {
                        dest[xout++] = source[xin++];
                        DataLen--;
                        if (DataLen == 0)
                        {
                            return dest;
                        }
                    }
                }
            }
            return dest;
        }
    }
}
