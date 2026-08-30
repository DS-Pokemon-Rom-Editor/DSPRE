using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// Easing the view to one of the alternative camera settings, the way SMLS_CamCnt_Main does.
    /// No retail script asks for this, so it is here for hacks that do.
    /// </summary>
    public class FieldCameraMoveTests
    {
        [Fact]
        public void OnlyTheRowsTheTableActuallyHasCountAsReal()
        {
            Assert.False(FieldCameraMove.Exists(0));      // a script's rows count from one
            Assert.True(FieldCameraMove.Exists(1));
            Assert.False(FieldCameraMove.Exists(2));      // this build has just the one row
            Assert.Equal(1, FieldCameraMove.Settings.Length);
        }

        [Fact]
        public void ItEasesFromWhereTheCameraWasToWhereTheRowSays()
        {
            // The one row tilts to -0x1a9e, which is about 37 degrees down, over twenty four frames.
            var move = new FieldCameraMove(1, fromPitchDegrees: 48.68f);
            Assert.Equal(24, move.TotalFrames);
            Assert.Equal(48.68f, move.PitchDegrees, 2);   // starts where the camera already was

            move.Advance(12);
            Assert.InRange(move.PitchDegrees, 37f, 48.68f);   // part way there

            move.Advance(12);
            Assert.False(move.Running);
            Assert.InRange(move.PitchDegrees, 37f, 37.5f);
        }

        [Fact]
        public void TheViewSlidesBackTheWayTheRowAsksAndStopsThere()
        {
            var move = new FieldCameraMove(1, 48.68f);
            Assert.Equal(0f, move.ShiftZInTiles, 4);      // nothing has moved yet

            move.Advance(24);
            // Shift z is -0x6c000, which is 108 game units, and a tile is sixteen of those: 6.75 tiles back.
            Assert.Equal(-6.75f, move.ShiftZInTiles, 2);
            Assert.Equal(0f, move.ShiftXInTiles, 4);

            // And it stays put rather than carrying on past.
            move.Advance(100);
            Assert.Equal(-6.75f, move.ShiftZInTiles, 2);
            Assert.False(move.Running);
        }

        [Fact]
        public void AskingForARowThatIsNotThereFallsBackRatherThanFailing()
        {
            var move = new FieldCameraMove(99, 48.68f);
            move.Advance(24);
            Assert.Equal(-6.75f, move.ShiftZInTiles, 2);
        }
    }
}
