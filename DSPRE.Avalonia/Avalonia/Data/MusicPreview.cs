using System;
using System.Threading;
using System.Threading.Tasks;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// Plays one of the game's music sequences as a preview, looping, for editors that pick music by number (the
    /// Header editor's day and night music). One plays at a time: starting another stops the first.
    /// </summary>
    public static class MusicPreview
    {
        // Long enough to hear a theme's loop come round without rendering the whole thing.
        private const double Seconds = 90;
        private const int SampleRate = 32000;

        private static readonly object Gate = new();
        private static object _handle;
        private static int _playing = -1;
        private static int _starting;

        /// <summary>The sequence playing, or -1.</summary>
        public static int Playing { get { lock (Gate) return _playing; } }

        private static object _tag;
        /// <summary>Whoever started the playing sequence, so the button that did can show stop while its twin shows play.</summary>
        public static bool StartedBy(object tag) { lock (Gate) return _playing >= 0 && Equals(_tag, tag); }

        /// <summary>Raised when playback starts or stops, on whatever thread did it.</summary>
        public static event Action Changed;

        /// <summary>Plays a sequence, or stops it if the same button already started it.</summary>
        public static void Toggle(int seqId, object tag = null)
        {
            if (Playing == seqId)
            {
                if (StartedBy(tag)) { Stop(); return; }
                // The same sequence from the other button: keep playing, only the owner changes.
                lock (Gate) _tag = tag;
                Changed?.Invoke();
                return;
            }
            Start(seqId, tag);
        }

        public static void Start(int seqId, object tag = null)
        {
            Stop();
            if (seqId < 0) return;
            int ticket = Interlocked.Increment(ref _starting);
            lock (Gate) { _playing = seqId; _tag = tag; }
            Changed?.Invoke();
            Task.Run(() =>
            {
                short[] pcm = null;
                try
                {
                    SdatArchive sdat = SoundArchive.Load();
                    if (sdat != null) pcm = SseqPlayer.Render(sdat, seqId, SampleRate, Seconds);
                }
                catch (Exception ex) { AppLogger.Error($"Music preview of sequence {seqId} failed: {ex.Message}"); }
                lock (Gate)
                {
                    // Stopped, or another started, while this one rendered.
                    if (ticket != _starting || _playing != seqId) return;
                    if (pcm == null || pcm.Length == 0) { _playing = -1; }
                    else
                    {
                        try { _handle = AudioOutput.Current.StartLooping(pcm, SampleRate); }
                        catch (Exception ex) { AppLogger.Error("Music preview could not play: " + ex.Message); _playing = -1; }
                    }
                }
                Changed?.Invoke();
            });
        }

        public static void Stop()
        {
            object handle;
            bool was;
            lock (Gate)
            {
                handle = _handle;
                was = _playing >= 0;
                _handle = null;
                _playing = -1;
                _tag = null;
                Interlocked.Increment(ref _starting);
            }
            if (handle != null)
            {
                try { AudioOutput.Current.Stop(handle); } catch { }
            }
            if (was) Changed?.Invoke();
        }
    }
}
