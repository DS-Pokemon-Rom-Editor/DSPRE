using DSPRE.Avalonia.ViewModels.World;
using Xunit;

namespace DSPRE.Tests.Editors
{
    public class CameraEditorRowTests
    {
        // Platinum's normal camera, HeartGold's Violet Gym (shifted) and a made-up flat one with turn and roll.
        public static TheoryData<GameCamera> Cameras => new()
        {
            new GameCamera(0x29aec1, -0x29fe, 0, 0, 0, GameCamera.PERSPECTIVE, 0, 0x5c1, 150u << 12, 900u << 12),
            new GameCamera(0x19465c, -0x1c7d, 0, 0, 0, GameCamera.PERSPECTIVE, 0, 0x981, 134u << 12, 1200u << 12, 0, 0x25000, -0xf000),
            new GameCamera(0x61b89b, -0x237e, 0x123, -0x45, 7, GameCamera.ORTHO, 3, 0x281, 150u << 12, 1735u << 12),
        };

        [Theory]
        [MemberData(nameof(Cameras))]
        public void Writing_back_the_shown_values_keeps_every_byte(GameCamera cam)
        {
            bool hgss = cam.xOffset != null;
            var row = new CameraRowVM(0);
            row.LoadFrom(cam);

            row.Distance = row.Distance;
            row.Tilt = row.Tilt;
            row.Turn = row.Turn;
            row.Roll = row.Roll;
            row.ViewIndex = row.ViewIndex;
            row.FieldOfView = row.FieldOfView;
            row.NearClip = row.NearClip;
            row.FarClip = row.FarClip;
            row.ShiftX = row.ShiftX;
            row.ShiftY = row.ShiftY;
            row.ShiftZ = row.ShiftZ;

            Assert.Equal(cam.ToByteArray(), row.ToGameCamera(hgss).ToByteArray());
        }

        [Fact]
        public void Shown_values_are_tiles_and_degrees()
        {
            var row = new CameraRowVM(0);
            row.LoadFrom(new GameCamera(0x29aec1, -0x29fe, 0x4000, 0x2000, 0, GameCamera.PERSPECTIVE, 0, 0x5c1, 150u << 12, 900u << 12,
                                        0, 0x25000, -0xf000));

            Assert.Equal(41.683m, row.Distance);
            Assert.Equal(59.05m, row.Tilt);
            Assert.Equal(90m, row.Turn);
            Assert.Equal(22.5m, row.Roll);
            Assert.Equal(16.18m, row.FieldOfView);
            Assert.Equal(9.375m, row.NearClip);
            Assert.Equal(56.25m, row.FarClip);
            Assert.Equal(2.313m, row.ShiftY);
            Assert.Equal(-0.938m, row.ShiftZ);
        }

        [Fact]
        public void An_edit_lands_in_the_raw_field()
        {
            var row = new CameraRowVM(0);
            row.LoadFrom(new GameCamera(0x29aec1, -0x29fe, 0, 0, 0, GameCamera.PERSPECTIVE, 0, 0x5c1, 150u << 12, 900u << 12));

            row.Tilt = 45m;
            row.FieldOfView = 20m;
            row.Distance = 40m;
            row.ViewIndex = 1;
            var cam = row.ToGameCamera(false);

            Assert.Equal(-0x2000, cam.vertRot);
            Assert.Equal(0x71c, cam.fov);   // half of 20 degrees
            Assert.Equal(40u * 16 * 4096, cam.distance);
            Assert.Equal(GameCamera.ORTHO, cam.perspMode);
        }
    }
}
