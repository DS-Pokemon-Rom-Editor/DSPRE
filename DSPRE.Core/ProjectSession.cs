using DSPRE.ROMFiles;
using DSPRE.Tracking;

namespace DSPRE
{
    public static class ProjectSession
    {
        public static void Starting() => ChangeTracker.Stop();

        public static void Loaded()
        {
            ProjectSourceWatcher.Start();
            // Whatever the last ROM needed switching on, this one has to earn for itself.
            BetaEditors.ForgetWhatWasNeeded();
        }
    }
}
