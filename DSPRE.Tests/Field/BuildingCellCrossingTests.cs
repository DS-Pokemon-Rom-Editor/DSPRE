using DSPRE.Avalonia.ViewModels.World;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// Building positions are tiles from the map's centre, so a map spans -16 to 16 and only a building
    /// beyond that belongs to a neighbouring map.
    /// </summary>
    public class BuildingCellCrossingTests
    {
        [Theory]
        [InlineData(0, 0, 0)]
        [InlineData(-1, 0, 0)]
        [InlineData(-16, 0, 0)]
        [InlineData(15, 32768, 0)]
        [InlineData(16, 0, 0)]
        [InlineData(-16, 32768, 0)]
        [InlineData(-17, 32768, -1)]
        [InlineData(16, 1, 1)]
        [InlineData(17, 0, 1)]
        [InlineData(48, 0, 1)]
        [InlineData(49, 0, 2)]
        [InlineData(-17, 0, -1)]
        [InlineData(-48, 0, -1)]
        [InlineData(-49, 0, -2)]
        public void OnlyPositionsBeyondTheEdgeCross(short position, ushort fraction, int expected)
        {
            Assert.Equal(expected, MapEditorViewModel.CellsCrossed(position, fraction));
        }
    }
}
