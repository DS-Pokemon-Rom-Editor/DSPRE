using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace DSPRE.Interop {
    /// <summary>Thrown when a uxie FFI call fails. Carries uxie's last-error message.</summary>
    public sealed class UxieException : Exception {
        public UxieException(string message) : base(message) { }
        public UxieException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Semantic identity of a ROM as reported by uxie, mapped onto DSPRE's enums.
    /// uxie owns the detection vocabulary; this is the bridge into DSPRE's types.
    /// </summary>
    public sealed class RomIdentity {
        public string GameCode;
        public RomInfo.GameVersions Version;
        public RomInfo.GameFamilies Family;
        public RomInfo.GameLanguages Language;
        public string Region;
        public byte RomVersion;
    }

    /// <summary>P/Invoke shim for the native uxie ROM-data library (uxie.dll).</summary>
    internal static class Uxie {
        private const string Dll = "uxie"; // resolves to uxie.dll on Windows

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr uxie_version();

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr uxie_last_error();

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern void uxie_free_string(IntPtr ptr);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr uxie_identify_rom([MarshalAs(UnmanagedType.LPUTF8Str)] string path);

        /// <summary>Returns the uxie library version string.</summary>
        public static string Version() => TakeOwnedString(uxie_version());

        /// <summary>
        /// Identify a ROM from its header file (header.yaml or header.bin).
        /// No fallback: throws <see cref="UxieException"/> if uxie cannot identify it.
        /// </summary>
        public static RomIdentity Identify(string headerPath) {
            string json = TakeOwnedString(uxie_identify_rom(headerPath));
            if (json == null) {
                // Read last_error immediately — no intervening FFI call has cleared it.
                throw new UxieException(LastError() ?? "uxie_identify_rom returned null without an error message");
            }

            try {
                using (JsonDocument doc = JsonDocument.Parse(json)) {
                    JsonElement root = doc.RootElement;
                    return new RomIdentity {
                        GameCode = root.GetProperty("game_code").GetString(),
                        Version = MapEnum<RomInfo.GameVersions>(root.GetProperty("game").GetString()),
                        Family = MapEnum<RomInfo.GameFamilies>(root.GetProperty("family").GetString()),
                        Language = MapEnum<RomInfo.GameLanguages>(root.GetProperty("language").GetString()),
                        Region = root.TryGetProperty("region", out JsonElement region) ? region.GetString() : null,
                        RomVersion = root.GetProperty("rom_version").GetByte(),
                    };
                }
            } catch (UxieException) {
                throw; // unknown game/family/language from the mappers — propagate as-is
            } catch (Exception ex) {
                throw new UxieException($"Could not parse uxie identity JSON: {ex.Message}\nJSON: {json}", ex);
            }
        }

        // uxie owns the detection vocabulary; its JSON strings match DSPRE's enum member
        // names 1:1 (GameFamilies.Plat was renamed to Platinum to converge). Parse by name
        // and fail loud on any drift — uxie is authoritative, there is no fallback.
        private static T MapEnum<T>(string value) where T : struct {
            if (Enum.TryParse(value, out T result) && Enum.IsDefined(typeof(T), result)) {
                return result;
            }
            throw new UxieException($"uxie reported '{value}', which has no matching {typeof(T).Name}");
        }

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
