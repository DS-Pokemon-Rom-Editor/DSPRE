using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>The message and menu borders read through the archive members, so pending and saved edits show.</summary>
    public static class WindowFrames
    {
        public static FieldWindowFrame Load(int frameIndex)
        {
            ScriptNarc frames = new ScriptNarc(DirNames.WindowFrames);
            return frames.Available ? FieldWindowFrame.Load(frameIndex, frames.Get, frames.Count) : null;
        }

        /// <summary>render_window.c LoadUserFrameGfx1: frame type 0 is drawing 0, any other type drawing 1, both with the same colours.</summary>
        public static FieldWindowFrame LoadStandard(int drawing = 0)
        {
            ScriptNarc frames = new ScriptNarc(DirNames.WindowFrames);
            return frames.Available ? FieldWindowFrame.LoadStandard(frames.Get, frames.Count, drawing) : null;
        }

        public static uint[] LoadSystemFontColours()
        {
            ScriptNarc fonts = new ScriptNarc(DirNames.Fonts);
            return fonts.Available ? FieldWindowFrame.LoadSystemFontColours(fonts.Get) : null;
        }
    }
}
