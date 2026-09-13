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

        public PcmVoice(short[] interleavedStereoPcm, int sampleRate, bool loop = false)
        {
            _pcm = interleavedStereoPcm;
            _loop = loop;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 2);
        }

        private readonly bool _loop;

        /// <summary>Held voices give silence without moving on, so they pick up where they stopped.</summary>
        public volatile bool Paused;

        // Samples left in a fade-out, or -1 while playing normally.
        private int _fadeLeft = -1, _fadeLength;

        public void FadeOut(int samples)
        {
            if (_fadeLeft >= 0) return;
            _fadeLength = Math.Max(2, samples);
            _fadeLeft = Math.Min(_fadeLength, _pcm.Length - _pos);
        }

        public int Read(float[] buffer, int offset, int count)
        {
            if (Paused && _fadeLeft < 0)
            {
                Array.Clear(buffer, offset, count);
                return count;
            }
            if (_loop && _fadeLeft < 0 && _pcm.Length > 0)
            {
                for (int i = 0; i < count; i++)
                {
                    if (_pos >= _pcm.Length) _pos = 0;
                    buffer[offset + i] = _pcm[_pos++] / 32768f;
                }
                return count;
            }
            int n = Math.Max(0, Math.Min(count, _pcm.Length - _pos));
            if (_fadeLeft >= 0) n = Math.Min(n, _fadeLeft);
            for (int i = 0; i < n; i++)
            {
                float gain = _fadeLeft >= 0 ? (float)(_fadeLeft - i) / _fadeLength : 1f;
                buffer[offset + i] = _pcm[_pos + i] / 32768f * gain;
            }
            _pos += n;
            if (_fadeLeft >= 0) _fadeLeft -= n;
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

        public void Play(short[] interleavedStereoPcm, int sampleRate) => Start(interleavedStereoPcm, sampleRate);

        public object Start(short[] interleavedStereoPcm, int sampleRate) => Begin(interleavedStereoPcm, sampleRate, loop: false);

        public object StartLooping(short[] interleavedStereoPcm, int sampleRate) => Begin(interleavedStereoPcm, sampleRate, loop: true);

        private object Begin(short[] interleavedStereoPcm, int sampleRate, bool loop)
        {
            if (!OperatingSystem.IsWindows()) return null;
            if (interleavedStereoPcm == null || interleavedStereoPcm.Length == 0) return null;

            lock (_gate)
            {
                EnsureStarted(sampleRate);
                var voice = new PcmVoice(interleavedStereoPcm, sampleRate, loop);
                _mixer.AddMixerInput(voice);
                return voice;
            }
        }

        public void SetPaused(object handle, bool paused)
        {
            if (handle is PcmVoice voice) voice.Paused = paused;
        }

        public void Stop()
        {
            if (!OperatingSystem.IsWindows()) return;
            lock (_gate) { _mixer?.RemoveAllMixerInputs(); }
        }

        public void Stop(object handle)
        {
            if (!OperatingSystem.IsWindows() || handle is not PcmVoice voice) return;
            // A quarter-second fade; the mixer drops the voice once it runs dry.
            lock (_gate) { voice.FadeOut(voice.WaveFormat.SampleRate / 4 * 2); }
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
