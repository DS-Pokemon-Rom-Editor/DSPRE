using System.IO;
using DSPRE;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>A personal data record keeps both abilities in the layout its game was built with.</summary>
    [Collection("rom")]
    public class PersonalDataLayoutTests
    {
        private static void SetHge(bool value) =>
            typeof(RomInfo).GetProperty(nameof(RomInfo.IsHGE)).SetValue(null, value);

        private static byte[] Record(bool hge)
        {
            byte[] data = new byte[44];
            data[0x16] = 0x01; data[0x17] = 0x01;   // hg-engine: ability 1 is 257 (u16); vanilla: abilities 1 and 1
            data[0x18] = 30;                         // flee rate
            data[0x19] = 0x83;                       // colour 3, flipped
            if (hge) { data[0x1A] = 0x2F; data[0x1B] = 0x00; }   // ability 2 is 47
            data[0x1C] = 0x05;                       // first TM bits
            return data;
        }

        [Fact]
        public void HgEngineRecordsReadBothSixteenBitAbilitiesAndWriteThemBack()
        {
            bool previous = RomInfo.IsHGE;
            try
            {
                SetHge(true);
                byte[] bytes = Record(hge: true);
                PokemonPersonalData mon = new PokemonPersonalData(new MemoryStream(bytes));

                Assert.Equal(257, mon.FirstAbility);
                Assert.Equal(47, mon.SecondAbility);
                Assert.Equal(30, mon.EscapeRate);
                Assert.True(mon.Flip);
                Assert.Equal(bytes, mon.ToByteArray());
            }
            finally { SetHge(previous); }
        }

        [Fact]
        public void VanillaRecordsKeepOneByteAbilities()
        {
            bool previous = RomInfo.IsHGE;
            try
            {
                SetHge(false);
                byte[] bytes = Record(hge: false);
                PokemonPersonalData mon = new PokemonPersonalData(new MemoryStream(bytes));

                Assert.Equal(1, mon.FirstAbility);
                Assert.Equal(1, mon.SecondAbility);
                Assert.Equal(30, mon.EscapeRate);
                Assert.Equal(bytes, mon.ToByteArray());
            }
            finally { SetHge(previous); }
        }
    }
}
