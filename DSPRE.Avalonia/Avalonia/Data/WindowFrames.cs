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

        public static FieldWindowFrame LoadStandard()
        {
            ScriptNarc frames = new ScriptNarc(DirNames.WindowFrames);
            return frames.Available ? FieldWindowFrame.LoadStandard(frames.Get, frames.Count) : null;
        }

        public static uint[] LoadSystemFontColours()
        {
            ScriptNarc fonts = new ScriptNarc(DirNames.Fonts);
            return fonts.Available ? FieldWindowFrame.LoadSystemFontColours(fonts.Get) : null;
        }
    }
}
