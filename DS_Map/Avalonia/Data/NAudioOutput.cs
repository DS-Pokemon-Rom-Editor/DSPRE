using System;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace DSPRE.Avalonia.Data
{
    /// <summary>One in-flight sound: a short[] PCM buffer read out as floats. Returns fewer samples than asked
    /// for once its own data runs out (down to 0), which is exactly the signal <see cref="MixingSampleProvider"/>
    /// uses to drop a finished source — no separate "are we done" bookkeeping needed.</summary>
    internal sealed class PcmVoice : ISampleProvider
    {
        private readonly short[] _pcm;
        private int _pos;
        public WaveFormat WaveFormat { get; }

        public PcmVoice(short[] interleavedStereoPcm, int sampleRate)
        {
            _pcm = interleavedStereoPcm;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 2);
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int n = Math.Max(0, Math.Min(count, _pcm.Length - _pos));
            for (int i = 0; i < n; i++) buffer[offset + i] = _pcm[_pos + i] / 32768f;
            _pos += n;
            return n;
        }
    }

    /// <summary>Plays back <see cref="SseqPlayer"/>'s rendered PCM through NAudio's WASAPI output. NAudio's
    /// actual playback device (unlike the pure parsing/rendering code around it) is Windows-only, so every entry
    /// point here checks <see cref="OperatingSystem.IsWindows"/> and no-ops elsewhere — the package is referenced
    /// (so both the WinForms-hybrid shell and the pure Avalonia shell get real sound on Windows), but a Linux
    /// build/run degrades to silent instead of throwing.
    ///
    /// A single move animation routinely triggers its sound effect more than once — verified directly against
    /// real move scripts, not assumed: some fire the same sound twice a few frames apart, others fire it two to
    /// four times on the SAME frame (a deliberate layering technique for a punchier hit, not a parsing quirk).
    /// The real hardware mixes every simultaneously-playing channel down to one output stream; spawning a
    /// separate OS-level output device per trigger (the previous approach here) doesn't match that and risks
    /// audio-driver contention/glitches when several land at once. This mixes every active trigger through one
    /// persistent <see cref="MixingSampleProvider"/> and one <see cref="WaveOutEvent"/> instead, matching the
    /// real single-output-stream shape. Confirmed against the real sound engine's own documented behaviour that
    /// starting a new sequence must not stop whatever else is already playing (see snd_command.c commentary in
    /// project memory) — this mixer never removes a voice early, only once it has genuinely finished.</summary>
    public sealed class NAudioOutput : IAudioOutput
    {
        private readonly object _gate = new object();
        private WaveOutEvent _output;
        private MixingSampleProvider _mixer;

        public void Play(short[] interleavedStereoPcm, int sampleRate)
        {
            if (!OperatingSystem.IsWindows()) return;
            if (interleavedStereoPcm == null || interleavedStereoPcm.Length == 0) return;

            lock (_gate)
            {
                EnsureStarted(sampleRate);
                _mixer.AddMixerInput(new PcmVoice(interleavedStereoPcm, sampleRate));
            }
        }

        private void EnsureStarted(int sampleRate)
        {
            if (_mixer != null) return;
            _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 2))
            {
                ReadFully = true,   // keep producing silence between sounds instead of ending the output stream
            };
            _output = new WaveOutEvent();
            _output.Init(_mixer);
            _output.Play();
        }
    }
}
