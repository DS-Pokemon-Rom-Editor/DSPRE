using System;
using System.Runtime.InteropServices;
using System.Text;

namespace DSPRE.Interop {
    /// <summary>P/Invoke shim for the native uxie ROM-data library (uxie.dll).</summary>
    internal static class Uxie {
        private const string Dll = "uxie"; // resolves to uxie.dll on Windows

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr uxie_version();

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr uxie_last_error();

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern void uxie_free_string(IntPtr ptr);

        /// <summary>Returns the uxie library version string.</summary>
        public static string Version() => TakeOwnedString(uxie_version());

        /// <summary>Last error reported by uxie on this thread, or null.</summary>
        public static string LastError() {
            // Owned by uxie's thread-local; copy but do NOT free.
            IntPtr p = uxie_last_error();
            return p == IntPtr.Zero ? null : PtrToStringUtf8(p);
        }

        /// <summary>Marshal a uxie-owned C string into a managed string, then free it.</summary>
        private static string TakeOwnedString(IntPtr ptr) {
            if (ptr == IntPtr.Zero) return null;
            try { return PtrToStringUtf8(ptr); }
            finally { uxie_free_string(ptr); }
        }

        // .NET Framework 4.8 has no Marshal.PtrToStringUTF8, so decode manually.
        private static string PtrToStringUtf8(IntPtr ptr) {
            if (ptr == IntPtr.Zero) return null;
            int len = 0;
            while (Marshal.ReadByte(ptr, len) != 0) len++;
            byte[] buffer = new byte[len];
            Marshal.Copy(ptr, buffer, 0, len);
            return Encoding.UTF8.GetString(buffer);
        }
    }
}
