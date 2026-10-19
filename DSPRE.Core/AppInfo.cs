using System.Reflection;

namespace DSPRE
{
    /// <summary>App identity info usable from any layer (no UI dependency).</summary>
    public static class AppInfo
    {
        /// <summary>The running application's 4-part version (from the entry assembly).</summary>
        public static string GetDSPREVersion()
        {
            System.Version v =(Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()).GetName().Version;
            return v == null ? "?" : $"{v.Major}.{v.Minor}.{v.Build}.{v.Revision}";
        }
    }
}
