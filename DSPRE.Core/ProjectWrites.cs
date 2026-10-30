using System;
using System.Threading;

namespace DSPRE
{
    /// <summary>
    /// Raised after DSPRE writes, moves or deletes a project file, with the file's path. Code writes also say which
    /// bytes changed and what wrote them, as named by the innermost <see cref="By"/> scope.
    /// </summary>
    public static class ProjectWrites
    {
        public static event Action<string> Written;
        public static event Action<string, long, int, string> CodeWritten;

        private static readonly AsyncLocal<string> _by = new AsyncLocal<string>();

        /// <summary>Names what is writing until the returned scope is disposed, like "Patch toolbox: Expand trainer classes".</summary>
        public static IDisposable By(string what)
        {
            string outer = _by.Value;
            _by.Value = what;
            return new Scope(() => _by.Value = outer);
        }

        public static void Raise(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try { Written?.Invoke(path); }
            catch (Exception ex) { AppLogger.WarnOnce("ProjectWrites.Raise", ex); }
        }

        public static void RaiseCode(string path, long offset, int length)
        {
            if (string.IsNullOrEmpty(path)) return;
            try { CodeWritten?.Invoke(path, offset, length, _by.Value); }
            catch (Exception ex) { AppLogger.WarnOnce("ProjectWrites.RaiseCode", ex); }
            Raise(path);
        }

        private sealed class Scope : IDisposable
        {
            private Action _end;
            public Scope(Action end) { _end = end; }
            public void Dispose()
            {
                _end?.Invoke();
                _end = null;
            }
        }
    }
}
