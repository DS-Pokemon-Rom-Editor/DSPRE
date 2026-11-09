using System.Linq;
using System.Reflection;

namespace DSPRE
{
    /// <summary>App identity info usable from any layer (no UI dependency).</summary>
    public static class AppInfo
    {
        /// <summary>DSPRE's 4-part version, from the DSPRE.Avalonia assembly even when another program hosts it.</summary>
        public static string GetDSPREVersion()
        {
            Assembly app = System.AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "DSPRE.Avalonia");
            System.Version v = (app ?? Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()).GetName().Version;
            return v == null ? "?" : $"{v.Major}.{v.Minor}.{v.Build}.{v.Revision}";
        }
    }
}
