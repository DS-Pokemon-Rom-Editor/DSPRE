using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using static DSPRE.DSUtils;
using static DSPRE.RomInfo;

namespace DSPRE
{
    public static class LegacyOverlayUtils
    {
        public static class OverlayTable
        {
            private const int ENTRY_LEN = 32;

            /**
            * Only checks if the overlay is CONFIGURED as compressed
            **/
            public static bool IsDefaultCompressed(int ovNumber)
            {
                if (DSUtils.legacyMode)
                {
                    using (DSUtils.EasyReader f = new EasyReader(RomInfo.overlayTablePath, ovNumber * ENTRY_LEN + 31))
                    {
                        return (f.ReadByte() & 1) == 1;
                    }
                }
                return OverlayUtils.OverlayTable.GetRecompress(ovNumber);
            }

            public static void SetDefaultCompressed(int ovNumber, bool compressStatus)
            {
                DSUtils.WriteToFile(RomInfo.overlayTablePath, new byte[] { compressStatus ? (byte)1 : (byte)0 }, (uint)(ovNumber * ENTRY_LEN + 31)); //overlayNumber * size of entry + offset
            }

            public static uint GetRAMAddress(int ovNumber)
            {
                if (DSUtils.legacyMode)
                {
                    using (DSUtils.EasyReader f = new EasyReader(RomInfo.overlayTablePath, ovNumber * ENTRY_LEN + 4))
                    {
                        return f.ReadUInt32();
                    }
                }
                return OverlayUtils.OverlayTable.GetRAMAddress(ovNumber);

            }
            public static uint GetUncompressedSize(int ovNumber)
            {
                if (DSUtils.legacyMode)
                {
                    using (DSUtils.EasyReader f = new EasyReader(RomInfo.overlayTablePath, ovNumber * ENTRY_LEN + 8))
                    {
                        return f.ReadUInt32();
                    }
                }
                return OverlayUtils.OverlayTable.GetCodeSize(ovNumber) + OverlayUtils.OverlayTable.GetBSSSize(ovNumber);
            }

            /**
            * Gets number of overlays
            **/
            public static int GetNumberOfOverlays()
            {
                if (DSUtils.legacyMode)
                {
                    using (FileStream fileStream = File.OpenRead(RomInfo.overlayTablePath))
                    {
                        // Get the length of the file in bytes
                        return (int)(fileStream.Length / ENTRY_LEN);
                    }
                }
                return OverlayUtils.OverlayTable.GetNumberOfOverlays();
            }
        }


        public static string GetPath(int overlayNumber)
        {

            if (DSUtils.legacyMode)
            {
                return $"{overlayPath}\\overlay_{overlayNumber:D4}.bin";
            }
            else
            {
                return $"{overlayPath}\\ov{overlayNumber:D3}.bin";
            }

        }

        /**
         * Checks the actual size of the overlay file
         **/
        public static bool IsCompressed(int ovNumber)
        {
            if (DSUtils.legacyMode)
            {
                return (new FileInfo(GetPath(ovNumber)).Length < OverlayTable.GetUncompressedSize(ovNumber));
            }
            else
            {
                return false; // ds-rom automatically uncompresses overlays
            }
        }

        public static void RestoreFromCompressedBackup(int overlayNumber, bool eventEditorIsReady)
        {
            String overlayFilePath = GetPath(overlayNumber);

            if (File.Exists(overlayFilePath + DSUtils.backupSuffix))
            {
                if (new FileInfo(overlayFilePath).Length <= new FileInfo(overlayFilePath + DSUtils.backupSuffix).Length)
                { //if overlay is bigger than its backup
                    Console.WriteLine($"Overlay {overlayNumber} is already compressed.");
                    return;
                }
                else
                {
                    File.Delete(overlayFilePath);
                    File.Move(overlayFilePath + DSUtils.backupSuffix, overlayFilePath);
                }
            }
            else
            {
                string msg = $"Overlay File {overlayFilePath}{DSUtils.backupSuffix} couldn't be found and restored.";
                Console.WriteLine(msg);

                if (eventEditorIsReady)
                {
                    MessageBox.Show(msg, "Can't restore overlay from backup", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
        public static int Compress(int overlayNumber)
        {
            if (!DSUtils.legacyMode)
            {
                // Show message only when NOT in legacy mode (shouldn't happen, but just in case)
                MessageBox.Show("Overlay compression has been deprecated.\n" +
                    "Please use the new folder structure instead.\n" +
                    "dsrom automatically handles overlay compression during ROM build.\n" +
                    "To fix this error you can configure Overlay " + overlayNumber + " as uncompressed.",
                    "Overlay Compression Deprecated", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 0;
            }

            // Legacy mode: actually compress the overlay using blz.exe
            string overlayFilePath = GetPath(overlayNumber);

            if (!File.Exists(overlayFilePath))
            {
                MessageBox.Show("Overlay to compress #" + overlayNumber + " doesn't exist",
                    "Overlay not found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return DSUtils.ERR_OVERLAY_NOTFOUND;
            }

            Process compress = new Process();
            compress.StartInfo.FileName = @"Tools\blz.exe";
            compress.StartInfo.Arguments = "-en " + '"' + overlayFilePath + '"';
            compress.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            compress.StartInfo.CreateNoWindow = true;
            compress.Start();
            compress.WaitForExit();
            return compress.ExitCode;
        }

        public static int Decompress(string overlayFilePath, bool makeBackup = true)
        {
            if (!File.Exists(overlayFilePath))
            {
                MessageBox.Show($"File to decompress \"{overlayFilePath}\" doesn't exist",
                    "Overlay not found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return ERR_OVERLAY_NOTFOUND;
            }

            if (makeBackup)
            {
                if (File.Exists(overlayFilePath + backupSuffix))
                {
                    File.Delete(overlayFilePath + backupSuffix);
                }
                File.Copy(overlayFilePath, overlayFilePath + backupSuffix);
            }

            Process decompress = DSUtils.CreateDecompressProcess(overlayFilePath);
            decompress.Start();
            decompress.WaitForExit();
            return decompress.ExitCode;
        }
        public static int Decompress(int overlayNumber, bool makeBackup = true)
        {
            return Decompress(GetPath(overlayNumber), makeBackup);
        }

    }
}
