using DSPRE.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests.Models
{
    public class MaterialLookTests
    {
        private readonly ITestOutputHelper _out;
        public MaterialLookTests(ITestOutputHelper o) { _out = o; }

        private static MaterialLook Default() => MaterialLook.Plain;

        [Fact]
        public void MapStudiosNewMaterialIsTheOneTheGamesUseMost()
        {
            var look = Default();
            Assert.Equal(unchecked((int)0x7FFFE739), look.DiffuseAmbient);
            Assert.Equal(0x001F8081, look.PolygonAttr);
            Assert.Equal(0x00030000, look.ImageParam);
            Assert.Equal(MaterialLook.PlainSize, look.Record.Length);
            Assert.Equal(0x6739, look.CornerColour);

            var sized = look.WithPictureSize(64, 32);
            Assert.Equal(64, BitConverter.ToUInt16(sized.Record, 32));
            Assert.Equal(32, BitConverter.ToUInt16(sized.Record, 34));
            Assert.Equal(0, BitConverter.ToUInt16(look.Record, 32));
        }

        [Fact]
        public void SeeThroughOutlinedAndFlippedMaterialsSayWhatMapStudioSays()
        {
            var look = MaterialLook.Made(new[] { false, true, false, true }, bothSides: true, alpha: 12,
                fog: false, outlined: false, MaterialLook.Tiling.Flip, MaterialLook.Tiling.Clamp, 1,
                (1, 2, 3), (4, 5, 6), (7, 8, 9), (10, 11, 12));

            Assert.Equal(0b1010, look.Lights);
            Assert.True(look.BothSides);
            Assert.False(look.Fog);
            Assert.Equal(12, look.Alpha);
            Assert.Equal(3, (look.PolygonAttr >> 24) & 63);
            Assert.Equal((1 << 16) | (1 << 18) | (1 << 30), look.ImageParam);

            var outlined = MaterialLook.Made(new[] { true, false, false, false }, false, 31, true, true,
                MaterialLook.Tiling.Repeat, MaterialLook.Tiling.Repeat, 0, (0, 0, 0), (0, 0, 0), (0, 0, 0), (0, 0, 0));
            Assert.Equal(8, (outlined.PolygonAttr >> 24) & 63);
        }

        [Fact]
        public void ChangingWhatMapStudioLetsAMaterialChangeKeepsTheRest()
        {
            var sized = Default().WithPictureSize(32, 16);
            var glass = sized.With(alpha: 10, bothSides: true, fog: false, lights: 0b0110);

            Assert.Equal(10, glass.Alpha);
            Assert.Equal(3, (glass.PolygonAttr >> 24) & 63);
            Assert.True(glass.BothSides);
            Assert.False(glass.Fog);
            Assert.Equal(0b0110, glass.Lights);

            Assert.Equal(sized.DiffuseAmbient, glass.DiffuseAmbient);
            Assert.Equal(sized.ImageParam, glass.ImageParam);
            Assert.Equal(32, BitConverter.ToUInt16(glass.Record, 32));

            Assert.Equal(0, (glass.With(alpha: 31).PolygonAttr >> 24) & 63);
        }

        [SkippableTheory]
        [InlineData("Platinum")]
        [InlineData("HeartGold")]
        public void EveryMapMaterialRecordIsReadWhole(string game)
        {
            string project = game == "Platinum" ? TestRoms.Platinum : TestRoms.HeartGold;
            Skip.If(!Directory.Exists(project), $"{game} test project not configured");

            int records = 0, longer = 0, commonest = 0;
            string defaultKey = Default().Key;
            foreach (var (name, bytes) in MapModels.Of(project, game == "HeartGold"))
            {
                var file = NsbmdFile.Read(bytes, out _);
                if (file == null) continue;
                for (int m = 0; m < file.MaterialCount; m++)
                {
                    var record = file.MaterialRecord(m);
                    Assert.True(record != null, $"{name}: material {m} could not be read.");
                    records++;
                    if (record.Length > MaterialLook.PlainSize) longer++;
                    if (MaterialLook.FromRecord(record).WithPictureSize(0, 0).Key == defaultKey) commonest++;
                }
            }

            _out.WriteLine($"{game}: {records} material records, {longer} with a picture placing after them, "
                         + $"{commonest} what Map Studio makes by default, but for the size of the picture.");
            Assert.True(records > 0, "No material was read, so this proved nothing.");
            Assert.True(commonest > 0, "Not one map material is the record Map Studio makes by default.");
        }
    }
}
