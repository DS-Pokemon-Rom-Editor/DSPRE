using System;
using System.IO;
using System.Linq;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Rotom keeps each project's command database in .rotom/command_database and manages it itself; DSPRE does
    /// not compare, replace or update it. The one exception: a new hg-engine checkout project gets that checkout's
    /// flag names laid over it once, right after rotom init.
    /// </summary>
    public static class RotomDatabaseUpdate
    {
        /// <summary>Gives a new hg-engine folder project's database its checkout's flag names.</summary>
        public static void OverlayHgEngineFlags()
        {
            if (!HgEngine.HgEngineProject.IsActive) return;
            try
            {
                string project = RotomTool.ProjectDatabaseFile();
                if (project == null || !File.Exists(project)) return;
                byte[] before = File.ReadAllBytes(project), after = HgEngine.HgEngineScriptDatabase.Overlay(before);
                if (!after.AsSpan().SequenceEqual(before)) PlainFiles.WriteBytes(project, after);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                AppLogger.Warn("The hg-engine flag names could not be added to the project database: " + ex.Message);
            }
        }
    }
}
