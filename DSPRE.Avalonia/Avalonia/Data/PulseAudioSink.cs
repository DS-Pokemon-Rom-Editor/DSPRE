using System;
using System.Runtime.InteropServices;
using System.Threading;
using NAudio.Wave;

namespace DSPRE.Avalonia.Data
{
    /// <summary>Streams a sample provider to PulseAudio, or PipeWire's Pulse layer, through libpulse-simple.</summary>
    internal sealed class PulseAudioSink : IDisposable
    {
        private const string Library = "libpulse-simple.so.0";
        private const int PA_SAMPLE_FLOAT32LE = 5;
        private const int PA_STREAM_PLAYBACK = 1;

        // https://freedesktop.org/software/pulseaudio/doxygen/structpa__sample__spec.html
        [StructLayout(LayoutKind.Sequential)]
        private struct SampleSpec
        {
            public int Format;
            public uint Rate;
            public byte Channels;
        }

        // https://freedesktop.org/software/pulseaudio/doxygen/structpa__buffer__attr.html
        [StructLayout(LayoutKind.Sequential)]
        private struct BufferAttr
        {
            public uint MaxLength, TargetLength, Prebuffer, MinRequest, FragmentSize;
        }

        // https://freedesktop.org/software/pulseaudio/doxygen/simple_8h.html
        [DllImport(Library)]
        private static extern IntPtr pa_simple_new(string server, string name, int direction, string device, string streamName,
            ref SampleSpec spec, IntPtr channelMap, ref BufferAttr attr, out int error);

        [DllImport(Library)]
        private static extern int pa_simple_write(IntPtr stream, float[] data, UIntPtr bytes, out int error);

        [DllImport(Library)]
        private static extern void pa_simple_free(IntPtr stream);

        private readonly IntPtr _stream;
        private readonly ISampleProvider _source;
        private volatile bool _stopped;

        private PulseAudioSink(IntPtr stream, ISampleProvider source)
        {
            _stream = stream;
            _source = source;
        }

        /// <summary>Starts streaming, or returns null with the reason when libpulse or a sound server is missing.</summary>
        public static PulseAudioSink Open(ISampleProvider source, out string why)
        {
            why = null;
            WaveFormat format = source.WaveFormat;
            SampleSpec spec = new SampleSpec
            {
                Format = PA_SAMPLE_FLOAT32LE,
                Rate = (uint)format.SampleRate,
                Channels = (byte)format.Channels,
            };
            // uint.MaxValue lets the server pick that field; queueing 50 ms keeps latency close to WaveOut's.
            BufferAttr attr = new BufferAttr
            {
                MaxLength = uint.MaxValue,
                TargetLength = (uint)(format.AverageBytesPerSecond / 20),
                Prebuffer = uint.MaxValue,
                MinRequest = uint.MaxValue,
                FragmentSize = uint.MaxValue,
            };

            IntPtr stream;
            int error;
            try
            {
                stream = pa_simple_new(null, "DSPRE", PA_STREAM_PLAYBACK, null, "Preview", ref spec, IntPtr.Zero, ref attr, out error);
            }
            catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException)
            {
                why = "Sound needs the PulseAudio client library. On Debian or Ubuntu, install it with\n\n"
                    + "sudo apt install libpulse0\n\nand restart DSPRE.";
                return null;
            }
            if (stream == IntPtr.Zero)
            {
                why = "DSPRE couldn't reach a PulseAudio or PipeWire sound server (error " + error + "). Start one and restart DSPRE to get sound.";
                return null;
            }

            PulseAudioSink sink = new PulseAudioSink(stream, source);
            new Thread(sink.Pump) { IsBackground = true, Name = "DSPRE audio output" }.Start();
            return sink;
        }

        public void Dispose() => _stopped = true;

        private void Pump()
        {
            WaveFormat format = _source.WaveFormat;
            // Writes 10 ms at a time; pa_simple_write blocks while the server's buffer is full, so the loop needs no timer.
            float[] chunk = new float[format.SampleRate / 100 * format.Channels];
            while (!_stopped)
            {
                int read = _source.Read(chunk, 0, chunk.Length);
                if (read <= 0)
                {
                    Array.Clear(chunk);
                    read = chunk.Length;
                }
                if (pa_simple_write(_stream, chunk, (UIntPtr)(read * sizeof(float)), out _) < 0) break;
            }
            pa_simple_free(_stream);
        }
    }
}
