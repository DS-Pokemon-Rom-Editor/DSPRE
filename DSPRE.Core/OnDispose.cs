using System;
using System.Threading;

namespace DSPRE
{
    /// <summary>Runs an action once when disposed, to end a using scope that changed something.</summary>
    public sealed class OnDispose : IDisposable
    {
        private Action _action;

        public OnDispose(Action action) => _action = action;

        public void Dispose() => Interlocked.Exchange(ref _action, null)?.Invoke();
    }
}
