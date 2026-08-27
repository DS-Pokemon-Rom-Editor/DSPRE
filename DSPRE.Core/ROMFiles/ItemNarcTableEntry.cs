namespace DSPRE.Editors
{
    /// <summary>
    /// One row of the ARM9 item NARC table (data/icon/palette/AGB file indices).
    /// Extracted from the WinForms ItemEditor so both UI layers share it.
    /// </summary>
    public struct ItemNarcTableEntry
    {
        public uint itemData;
        public uint itemIcon;
        public uint itemPalette;
        public uint itemAGB;
    };
}
