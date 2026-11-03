using System;
using System.Threading;
using System.Threading.Tasks;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Saves and the previews that read project files on a worker thread take turns, so a save never finds a file a
    /// preview has open.
    /// </summary>
    public static class PreviewFileGate
    {
        private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);

        /// <summary>Runs a preview's reads on a worker thread, after any save in progress. A save on the UI thread waits for
        /// the read, so the read must never wait on the UI thread.</summary>
        public static Task<T> RunAsync<T>(Func<T> read) => Task.Run(async () =>
        {
            await Gate.WaitAsync().ConfigureAwait(false);
            try { return read(); }
            finally { Gate.Release(); }
        });

        /// <summary>Holds previews back until disposed. Waits for a preview that is reading.</summary>
        public static IDisposable Writing()
        {
            Gate.Wait();
            return new Release();
        }

        private sealed class Release : IDisposable
        {
            private int _done;
            public void Dispose()
            {
                if (Interlocked.Exchange(ref _done, 1) == 0) Gate.Release();
            }
        }
    }
}
