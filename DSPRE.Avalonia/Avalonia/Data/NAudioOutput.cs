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

    /// <summary>
    /// Plays <see cref="SseqPlayer"/>'s PCM through NAudio; silent off Windows. Every trigger goes through one mixer, as the
    /// hardware mixes channels, so a sound played several times on one frame layers.
    /// </summary>
    public sealed class NAudioOutput : IAudioOutput
    {
        private readonly object _gate = new object();
        private object _output;
        private MixingSampleProvider _mixer;
        private bool _unavailable;

        private static bool Supported => OperatingSystem.IsWindows() || OperatingSystem.IsLinux();

        public void Play(short[] interleavedStereoPcm, int sampleRate) => Start(interleavedStereoPcm, sampleRate);

        public object Start(short[] interleavedStereoPcm, int sampleRate) => Begin(interleavedStereoPcm, sampleRate, loop: false);

        public object StartLooping(short[] interleavedStereoPcm, int sampleRate) => Begin(interleavedStereoPcm, sampleRate, loop: true);

        private object Begin(short[] interleavedStereoPcm, int sampleRate, bool loop)
        {
            if (!Supported) return null;
            if (interleavedStereoPcm == null || interleavedStereoPcm.Length == 0) return null;

            lock (_gate)
            {
                if (!EnsureStarted()) return null;
                PcmVoice voice = new PcmVoice(interleavedStereoPcm, sampleRate, loop);
                // Raw samples at their own rate join the mix through a resampler; rendered sounds are already at the mix rate.
                _mixer.AddMixerInput(sampleRate == AudioOutput.RenderRate ? voice : new WdlResamplingSampleProvider(voice, AudioOutput.RenderRate));
                return voice;
            }
        }

        public void SetPaused(object handle, bool paused)
        {
            if (handle is PcmVoice voice) voice.Paused = paused;
        }

        public void Stop()
        {
            if (!Supported) return;
            lock (_gate) { _mixer?.RemoveAllMixerInputs(); }
        }

        public void Stop(object handle)
        {
            if (!Supported || handle is not PcmVoice voice) return;
            // A quarter-second fade; the mixer drops the voice once it runs dry.
            lock (_gate) { voice.FadeOut(voice.WaveFormat.SampleRate / 4 * 2); }
        }

        private bool EnsureStarted()
        {
            if (_mixer != null) return true;
            if (_unavailable) return false;
            MixingSampleProvider mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(AudioOutput.RenderRate, 2))
            {
                ReadFully = true,   // keep producing silence between sounds instead of ending the output stream
            };
            // TODO: maybe this needs to be refactored to work in Mac
            _output = OperatingSystem.IsWindows() ? StartWaveOut(mixer) : PulseAudioSink.Open(mixer);
            if (_output == null)
            {
                _unavailable = true;
                return false;
            }
            _mixer = mixer;
            return true;
        }

        private static WaveOutEvent StartWaveOut(ISampleProvider mixer)
        {
            // NAudio's default is 300 ms in 2 buffers, 9 game frames behind the picture; 60 ms in 3 keeps sounds on their frame.
            WaveOutEvent output = new WaveOutEvent { DesiredLatency = 60, NumberOfBuffers = 3 };
            output.Init(mixer);
            output.Play();
            return output;
        }
    }
}
