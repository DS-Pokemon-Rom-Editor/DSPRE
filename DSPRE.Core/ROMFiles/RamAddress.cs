namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Turns a RAM address a pointer holds into a file offset: arm9, an overlay, or the synthetic overlay a patch moved a
    /// table into. Callers list the files they accept in the order to try them.
    /// </summary>
    public static class RamAddress
    {
        /// <summary>A file loaded at <see cref="Base"/>, <see cref="Size"/> bytes long.</summary>
        public readonly record struct Region(string Name, uint Base, long Size);

        public static Region Arm9(long size) => new Region("arm9", ARM9.Address, size);
        public static Region Overlay(int overlay, long size) => new Region($"overlay {overlay}", OverlayUtils.OverlayTable.GetRAMAddress(overlay), size);
        public static Region Synthetic(long size) => new Region("synthetic overlay", RomInfo.SynthOverlayLoadAddress, size);

        /// <summary>
        /// Which of <paramref name="regions"/> holds <paramref name="length"/> bytes from <paramref name="ram"/>, the first that
        /// does, and the offset there; -1 when none does.
        /// </summary>
        public static (int Region, int Offset) Find(uint ram, long length, params Region[] regions)
        {
            for (int i = 0; i < regions.Length; i++)
            {
                Region r = regions[i];
                if (r.Size <= 0 || ram < r.Base) continue;
                long offset = (long)ram - r.Base;
                if (offset + length <= r.Size) return (i, (int)offset);
            }
            return (-1, -1);
        }
    }
}
