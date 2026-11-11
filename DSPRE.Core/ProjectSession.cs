using System;
using System.Collections.Generic;
using DSPRE.ROMFiles;
using DSPRE.Tracking;

namespace DSPRE
{
    public static class ProjectSession
    {
        private static int _loads;
        private static readonly List<Action> ClearedOnLoad = new List<Action>();

        // Changes with every load, so caches clear when the same folder is reopened.
        public static string LoadId { get; private set; } = "";

        public static void ClearOnLoad(Action clear)
        {
            lock (ClearedOnLoad) ClearedOnLoad.Add(clear);
        }

        public static void Began()
        {
            LoadId = (++_loads).ToString();
            Action[] clears;
            lock (ClearedOnLoad) clears = ClearedOnLoad.ToArray();
            foreach (Action clear in clears)
            {
                try { clear(); }
                catch (Exception ex) when (AppLogger.Noted("ProjectSession.Began", ex)) { }
            }
        }

        public static void Starting() => ChangeTracker.Stop();

        public static void Loaded()
        {
            ProjectSourceWatcher.Start();
            // Whatever the last ROM needed switching on, this one has to earn for itself.
            BetaEditors.ForgetWhatWasNeeded();
        }
    }
}
