using System;
using System.Linq;

namespace DSPRE.Screens
{
    /// <summary>Several preview scopes as one, ended in reverse order. Null scopes are skipped.</summary>
    public sealed class DrawScopes : IDisposable
    {
        private readonly IDisposable[] _all;

        public DrawScopes(params IDisposable[] all) { _all = all.Where(d => d != null).ToArray(); }

        public void Dispose() { foreach (IDisposable d in _all.Reverse()) d.Dispose(); }
    }
}
