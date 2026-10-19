﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using static DSPRE.RomInfo;

namespace DSPRE {
    public static class ARM9 {
        private const int MAX_SIZE = 0xBC000;
        public static readonly uint address = 0x02000000;
        public class Reader : DSUtils.EasyReader {
            public Reader(long pos = 0) : base(arm9Path, pos) {
                this.BaseStream.Position = pos;
            }
        }
        /// <summary>
        /// Edits a copy of arm9; closing it writes the result once through <see cref="DSUtils.WriteWholeFile"/>, so the
        /// hg-engine guard sees the change and a failed write never leaves half a file.
        /// </summary>
        public class Writer : BinaryWriter {
            private bool _written;

            public Writer(long pos = 0) : base(new MemoryStream()) {
                byte[] now = File.ReadAllBytes(arm9Path);
                this.BaseStream.Write(now, 0, now.Length);
                this.BaseStream.Position = pos;
            }

            public void EditSize(int increment) {
                this.BaseStream.SetLength(this.BaseStream.Length + increment);
            }

            protected override void Dispose(bool disposing) {
                if (disposing && !_written) {
                    _written = true;
                    DSUtils.WriteWholeFile(arm9Path, ((MemoryStream)this.BaseStream).ToArray());
                }
                base.Dispose(disposing);
            }
        }
        public static void EditSize(int increment) {
            using (Writer w = new Writer()) {
                w.EditSize(increment);
            }
        }
        public static bool Decompress(string path) {

            Process decompress = DSUtils.CreateDecompressProcess(path);
            if (decompress == null) return false;
            decompress.Start();
            decompress.WaitForExit();

            return new FileInfo(path).Length > MAX_SIZE;
        }

        public static bool Compress(string path) {
            Process compress = new Process();
            compress.StartInfo.Arguments = @" -en9 " + '"' + path + '"';
            if (!DSUtils.ConfigureToolStartInfo(compress.StartInfo, "blz"))
            {
                DSUtils.ReportToolUnavailable("blz");
                compress.Dispose();
                return false;
            }
            compress.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            compress.StartInfo.CreateNoWindow = true;
            compress.Start();
            compress.WaitForExit();

            return new FileInfo(path).Length <= MAX_SIZE;
        }
        public static bool CheckCompressionMark() {
            return BitConverter.ToInt32(ReadBytes(RomInfo.Arm9CompressionMarkOffset, 4), 0) != 0;
        }

        /// <summary>Decompresses arm9.bin in place if it is marked compressed. True when it is flat afterwards.</summary>
        public static bool DecompressIfMarked() {
            // The mark stays set until Save ROM clears it, so a flat file must not be decompressed or trimmed again.
            if (!CheckCompressionMark() || IsFlatButMarked()) return true;
            // An ndstool extraction keeps the 12-byte footer after the code, which blz can't read past.
            if (!RomInfo.IsDsRomProject) EditSize(-12);
            return Decompress(arm9Path);
        }

        /// <summary>Flat on disk but still marked compressed: the game would decompress it again at boot.</summary>
        public static bool IsFlatButMarked() {
            return CheckCompressionMark() && new FileInfo(arm9Path).Length > MAX_SIZE;
        }

        public static void ClearCompressionMark() {
            WriteBytes(new byte[4], RomInfo.Arm9CompressionMarkOffset);
        }

        public static byte[] ReadBytes(uint startOffset, long numberOfBytes = 0) {
            return DSUtils.ReadFromFile(RomInfo.arm9Path, startOffset, numberOfBytes);
        }

        /// <summary>Returns every file offset in arm9.bin where <paramref name="needle"/> occurs. Caller must
        /// ensure arm9 is decompressed first (see <see cref="CheckCompressionMark"/>/<see cref="Decompress(string)"/>).</summary>
        public static List<int> SearchBytes(byte[] needle) {
            return DSUtils.SearchBytes(ReadBytes(0), needle);
        }
        public static void WriteBytes(byte[] bytesToWrite, uint destOffset, int firstByte = 0, int? byteCount = null) {
            DSUtils.WriteToFile(RomInfo.arm9Path, bytesToWrite, destOffset, firstByte, byteCount);
        }

        public static ushort ReadWordLE(uint startOffset)
        {
            byte[] bytes = ReadBytes(startOffset, 2);
            ushort word = (ushort)(bytes[0] | (bytes[1] << 8));
            return word;
        }

        public static ushort ReadWordBE(uint startOffset)
        {
            byte[] bytes = ReadBytes(startOffset, 2);
            ushort word = (ushort)((bytes[0] << 8) | bytes[1]);
            return word;
        }

        public static byte ReadByte(uint startOffset) {
            return DSUtils.ReadFromFile(RomInfo.arm9Path, startOffset, 1)[0];
        }
        public static void WriteByte(byte value, uint destOffset) {
            DSUtils.WriteToFile(RomInfo.arm9Path, new byte[] { value }, destOffset, 0);
        }
    }
}
