namespace DSPRE.Avalonia.Data
{
    /// <summary>Plays back interleaved stereo 16-bit PCM (the format <see cref="SseqPlayer"/> renders to).
    /// Kept as a thin interface so this cross-platform layer never needs an audio-output package reference
    /// itself; a real backend is wired in by whichever shell hosts it (see <see cref="AudioOutput"/>).</summary>
    public interface IAudioOutput
    {
        void Play(short[] interleavedStereoPcm, int sampleRate);

        /// <summary>Silences whatever is playing, for stopping the music or closing a preview.</summary>
        void Stop();
    }

    /// <summary>Does nothing. The default until a shell wires in a real backend, so builds/shells that don't
    /// (the pure cross-platform Avalonia shell, for now) simply stay silent instead of failing.</summary>
    public sealed class NullAudioOutput : IAudioOutput
    {
        public void Play(short[] interleavedStereoPcm, int sampleRate) { }
        public void Stop() { }
    }

    /// <summary>The active audio backend. Defaults to a silent no-op; a shell's startup code assigns a real
    /// implementation (e.g. an NAudio-backed one) if it has one available.</summary>
    public static class AudioOutput
    {
        public static IAudioOutput Current { get; set; } = new NullAudioOutput();
    }
}
