using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// Tests that open a ROM share one of these, because RomInfo keeps what it reads in static fields.
    /// </summary>
    [CollectionDefinition("rom")]
    public class RomInfoCollection { }
}
