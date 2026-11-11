using System.Linq;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Screens
{
    /// <summary>The message and menu borders read through the archive members, so pending and saved edits show.</summary>
    public static class WindowFrames
    {
        /// <summary>The frames as the game's options list them, for a picker.</summary>
        public static string[] Names { get; } = Enumerable.Range(1, FieldWindowFrame.FrameCount).Select(n => $"Frame {n}").ToArray();

        /// <summary>With <paramref name="ifUnpacked"/>, null unless the archive is already unpacked.</summary>
        public static FieldWindowFrame Load(int frameIndex, bool ifUnpacked = false)
        {
            ScriptNarc frames = ScriptNarc.Open(DirNames.WindowFrames, ifUnpacked);
            return frames.Available ? FieldWindowFrame.Load(frameIndex, frames.Get, frames.Count) : null;
        }

        /// <summary>render_window.c LoadUserFrameGfx1: frame type 0 is drawing 0, any other type drawing 1, both with the same colours.</summary>
        public static FieldWindowFrame LoadStandard(int drawing = 0)
        {
            ScriptNarc frames = new ScriptNarc(DirNames.WindowFrames);
            return frames.Available ? FieldWindowFrame.LoadStandard(frames.Get, frames.Count, drawing) : null;
        }

        public static uint[] LoadSystemFontColours(bool ifUnpacked = false)
        {
            ScriptNarc fonts = ScriptNarc.Open(DirNames.Fonts, ifUnpacked);
            return fonts.Available ? FieldWindowFrame.LoadSystemFontColours(fonts.Get) : null;
        }
    }
}
