using System;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace DSPRE.Avalonia.Data
{
    /// <summary>One in-flight sound: a short[] PCM buffer read out as floats. Returns fewer samples than asked
    /// for once its own data runs out (down to 0), which is exactly the signal <see cref="MixingSampleProvider"/>
    /// uses to drop a finished source, so no separate "are we done" bookkeeping is needed.</summary>
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

    /// <summary>Plays back <see cref="SseqPlayer"/>'s rendered PCM through NAudio's WASAPI output. The playback
    /// device is Windows-only, so every entry point checks <see cref="OperatingSystem.IsWindows"/> and no-ops
    /// elsewhere; a Linux build degrades to silent instead of throwing.
    ///
    /// A move animation can trigger its sound effect more than once, including several times on the same frame
    /// as a deliberate layering technique for a punchier hit, and the real hardware mixes every simultaneous
    /// channel into one output stream. To match that, every active trigger runs through one persistent
    /// <see cref="MixingSampleProvider"/>/<see cref="WaveOutEvent"/> pair rather than a separate OS output
    /// device per trigger, and a voice is only removed once it has actually finished playing.</summary>
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

        public void Stop()
        {
            if (!OperatingSystem.IsWindows()) return;
            lock (_gate) { _mixer?.RemoveAllMixerInputs(); }
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
