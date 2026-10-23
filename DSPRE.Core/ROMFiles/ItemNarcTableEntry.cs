namespace DSPRE.Editors
{
    /// <summary>One row of the ARM9 item NARC table: data, icon, palette and AGB file indices.</summary>
    public struct ItemNarcTableEntry
    {
        public uint ItemData;
        public uint ItemIcon;
        public uint ItemPalette;
        public uint ItemAGB;
    };
}
