using System;
using DSPRE;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>HeartGold and SoulSilver map header bits that must survive a save.</summary>
    public class HeartGoldHeaderRoundTripTests
    {
        [Fact]
        public void ACameraAboveThirtyOneIsKept()
        {
            byte[] data = new byte[MapHeader.Length];
            uint last32 = (40u << 12) | (0x7Fu << 25);
            BitConverter.GetBytes(last32).CopyTo(data, 20);

            HeaderHGSS header = (HeaderHGSS)MapHeader.LoadFromByteArray(data, 0, RomInfo.GameFamilies.HGSS);
            Assert.Equal(40, header.CameraAngleID);

            HeaderHGSS reread = (HeaderHGSS)MapHeader.LoadFromByteArray(header.ToByteArray(), 0, RomInfo.GameFamilies.HGSS);
            Assert.Equal(40, reread.CameraAngleID);
            Assert.Equal(0x7F, reread.Flags);
            Assert.Equal(data, header.ToByteArray());
        }
    }
}
