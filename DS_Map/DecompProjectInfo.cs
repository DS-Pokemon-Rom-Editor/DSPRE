using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using static DSPRE.RomInfo;

namespace DSPRE
{
    /// <summary>
    /// Holds offsets parsed from a Platinum EN decomp project's xMAP file.
    ///
    /// Only PokÃ©mon Platinum English (ROM ID: CPUE) is supported.
    ///
    /// The built ROM is extracted exactly like a normal ROM load; DSPRE reads all
    /// NARCs from the standard dsrom layout.  The only values that may differ from
    /// vanilla are two binary offsets that a decomp build can relocate:
    ///
    ///   arm9.bin
    ///     HeaderTableOffset : byte offset of the map-header array
    ///     Vanilla Platinum EN: 0xE601C
    ///     Read by: SetHeaderTableOffset()  MapHeader.LoadFromARM9()
    ///
    ///   overlay_0005.bin
    ///     OWTableOffset     : byte offset of the overworld sprite-config table
    ///     Vanilla Platinum EN: 0x2BC34
    ///     Read by: SetOWtable()  ReadOWTable()
    /// </summary>
    public class DecompProjectInfo
    {
        /// <summary>
        /// Byte offset inside arm9.bin where the map-header table begins.
        /// 0 = not found in xMAP; vanilla value 0xE601C will be kept.
        /// </summary>
        public uint HeaderTableOffset { get; set; }

        /// <summary>
        /// Byte offset within overlay_0005.bin where the OW sprite-config table starts.
        /// 0 = not found in xMAP; vanilla value 0x2BC34 will be kept.
        /// </summary>
        public uint OWTableOffset { get; set; }

        // xMAP Parser  (Platinum EN only)

        // xMAP line format (whitespace-delimited columns):
        //   [leading spaces]  RAM_addr  size  section  symbol_name  (object.c.o)
        //   e.g.  020E601C 00003798 .rodata sMapHeaders (src_map_header.c.o)
        //
        // sMapHeaders  (src_map_header.c.o)
        //   HeaderTableOffset = RAM_addr - ARM9_RAM_BASE (0x02000000)
        //   Vanilla Platinum EN: RAM 0x020E601C  file offset 0xE601C
        //
        // OW table symbol  (overlay_0005)
        //   OWTableOffset = RAM_addr - OverlayUtils.OverlayTable.GetRAMAddress(5)
        //   Vanilla Platinum EN: file offset 0x2BC34
        //   Candidate symbol names are listed in OW_TABLE_SYMBOLS.

        private const string HEADER_TABLE_SYMBOL = "sMapHeaders";

        // Known OW table symbol names across Platinum decomp forks.
        // Add more if a fork uses a different name.
        private static readonly string[] OW_TABLE_SYMBOLS =
        {
            "sOvTable",
            "gOvTable",
            "sOverworldTable",
            "gOverworldTable",
        };

        // ARM9 binary is always mapped to this base address on NDS Platinum.
        private const uint ARM9_RAM_BASE = 0x02000000u;

        /// <summary>
        /// Parses an xMAP file produced by the Platinum EN decomp toolchain.
        /// Must be called after <see cref="RomInfo"/> is initialised so the overlay
        /// table is accessible for converting the OW table RAM address to a file offset.
        /// </summary>
        /// <param name="xmapPath">Absolute path to the xMAP file.</param>
        /// <returns>Populated <see cref="DecompProjectInfo"/> on success, or <c>null</c> on failure.</returns>
        public static DecompProjectInfo ParseXMAP(string xmapPath)
        {
            if (!File.Exists(xmapPath))
            {
                MessageBox.Show($"xMAP file not found:\n{xmapPath}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }

            try
            {
                var info = new DecompProjectInfo();

                uint? headerTableRAM = null;
                uint? owTableRAM     = null;

                foreach (string raw in File.ReadLines(xmapPath))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("//") || line.StartsWith("#"))
                        continue;

                    // Columns: [0] RAM_addr  [1] size  [2] section  [3] symbol_name
                    string[] tok = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (tok.Length < 4) continue;

                    if (!uint.TryParse(tok[0], System.Globalization.NumberStyles.HexNumber, null, out uint ramAddr))
                        continue;

                    string symbol = tok[3];

                    if (headerTableRAM == null && symbol == HEADER_TABLE_SYMBOL)
                    {
                        headerTableRAM = ramAddr;
                        AppLogger.Info($"[Decomp] xMAP: '{HEADER_TABLE_SYMBOL}' at RAM 0x{ramAddr:X8}");
                    }

                    if (owTableRAM == null)
                    {
                        foreach (string owSym in OW_TABLE_SYMBOLS)
                        {
                            if (symbol == owSym)
                            {
                                owTableRAM = ramAddr;
                                AppLogger.Info($"[Decomp] xMAP: OW table symbol '{owSym}' at RAM 0x{ramAddr:X8}");
                                break;
                            }
                        }
                    }

                    if (headerTableRAM != null && owTableRAM != null)
                        break;
                }

                // HeaderTableOffset = RAM_addr - ARM9_RAM_BASE
                if (headerTableRAM != null)
                {
                    info.HeaderTableOffset = headerTableRAM.Value - ARM9_RAM_BASE;
                    AppLogger.Info($"[Decomp] HeaderTableOffset = 0x{info.HeaderTableOffset:X}" +
                                   $" (RAM 0x{headerTableRAM.Value:X8} - base 0x{ARM9_RAM_BASE:X8})");
                }
                else
                {
                    AppLogger.Warn($"[Decomp] '{HEADER_TABLE_SYMBOL}' not found in xMAP;" +
                                    " HeaderTableOffset will keep vanilla value 0xE601C.");
                }

                // OWTableOffset = RAM_addr - overlay_0005_RAMbase
                if (owTableRAM != null)
                {
                    uint ov5Base = OverlayUtils.OverlayTable.GetRAMAddress(5);
                    if (ov5Base == 0)
                    {
                        AppLogger.Warn("[Decomp] Could not read overlay 5 RAM base from overlay table;" +
                                       " OWTableOffset will keep vanilla value 0x2BC34.");
                    }
                    else
                    {
                        info.OWTableOffset = owTableRAM.Value - ov5Base;
                        AppLogger.Info($"[Decomp] OWTableOffset = 0x{info.OWTableOffset:X}" +
                                       $" (RAM 0x{owTableRAM.Value:X8} - ov5 base 0x{ov5Base:X8})");
                    }
                }
                else
                {
                    AppLogger.Warn($"[Decomp] No OW table symbol found in xMAP" +
                                   $" (tried: {string.Join(", ", OW_TABLE_SYMBOLS)});" +
                                    " OWTableOffset will keep vanilla value 0x2BC34.");
                }

                return info;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to parse xMAP file:\n{ex.Message}", "Parse Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
        }

        /// <summary>
        /// Applies the two parsed offsets into the live <see cref="RomInfo"/> static state.
        /// Must be called after <see cref="RomInfo"/> is initialised (so the overlay table
        /// and vanilla offsets are already set) and after <see cref="ParseXMAP"/>.
        /// NARC directories are NOT overridden; the standard extracted layout is used.
        /// Text-archive index numbers are NOT overridden; vanilla Platinum EN values are correct.
        /// </summary>
        public void ApplyToRomInfo()
        {
            if (HeaderTableOffset != 0)
            {
                RomInfo.headerTableOffset = HeaderTableOffset;
                AppLogger.Info($"[Decomp] headerTableOffset overridden with 0x{HeaderTableOffset:X}");
            }
            else
            {
                AppLogger.Info("[Decomp] headerTableOffset not overridden; vanilla value 0xE601C kept.");
            }

            if (OWTableOffset != 0)
            {
                RomInfo.OWTableOffset = OWTableOffset;
                AppLogger.Info($"[Decomp] OWTableOffset overridden with 0x{OWTableOffset:X}");
            }
            else
            {
                AppLogger.Info("[Decomp] OWTableOffset not overridden; vanilla value 0x2BC34 kept.");
            }
        }
    }
}
