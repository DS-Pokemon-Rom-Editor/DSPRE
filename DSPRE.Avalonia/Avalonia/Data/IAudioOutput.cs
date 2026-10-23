namespace DSPRE.Avalonia.Data
{
    /// <summary>Plays interleaved stereo 16-bit PCM from <see cref="SseqPlayer"/>.</summary>
    public interface IAudioOutput
    {
        void Play(short[] interleavedStereoPcm, int sampleRate);

        /// <summary>Plays like <see cref="Play"/> and returns a handle that <see cref="Stop(object)"/> takes.</summary>
        object Start(short[] interleavedStereoPcm, int sampleRate);

        /// <summary>Silences whatever is playing, for stopping the music or closing a preview.</summary>
        void Stop();

        /// <summary>Fades out one sound started with <see cref="Start"/>, leaving the rest playing.</summary>
        void Stop(object handle);

        /// <summary>Plays from the start again each time it runs out, for music, until stopped.</summary>
        object StartLooping(short[] interleavedStereoPcm, int sampleRate) => Start(interleavedStereoPcm, sampleRate);

        /// <summary>Holds a sound where it is, or lets it carry on, the way a fanfare holds the music.</summary>
        void SetPaused(object handle, bool paused) { }
    }

    /// <summary>A silent backend, used until startup sets a real one.</summary>
    public sealed class NullAudioOutput : IAudioOutput
    {
        public void Play(short[] interleavedStereoPcm, int sampleRate) { }
        public object Start(short[] interleavedStereoPcm, int sampleRate) => null;
        public void Stop() { }
        public void Stop(object handle) { }
    }

    /// <summary>The audio backend; startup sets NAudioOutput.</summary>
    public static class AudioOutput
    {
        public static IAudioOutput Current { get; set; } = new NullAudioOutput();
    }
}
