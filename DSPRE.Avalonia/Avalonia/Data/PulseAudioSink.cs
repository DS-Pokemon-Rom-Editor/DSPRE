using System;
using System.Runtime.InteropServices;
using System.Threading;
using NAudio.Wave;

// https://freedesktop.org/software/pulseaudio/doxygen/structpa__sample__spec.html
[StructLayout(LayoutKind.Sequential)]
struct SampleSpec
{
    public int Format;
    public uint Rate;
    public byte Channels;
}

// https://freedesktop.org/software/pulseaudio/doxygen/structpa__buffer__attr.html
[StructLayout(LayoutKind.Sequential)]
struct BufferAttr
{
    public uint MaxLength, TargetLength, Prebuffer, MinRequest, FragmentSize;
}

namespace DSPRE.Avalonia.Data
{
    internal sealed class PulseAudioSink
    {

        // https://freedesktop.org/software/pulseaudio/doxygen/simple_8h.html
        [DllImport("libpulse-simple.so.0")]
        private static extern IntPtr pa_simple_new(string server, string name, int direction, string device, string streamName,
            ref SampleSpec spec, IntPtr channelMap, ref BufferAttr attr, out int error);

        [DllImport("libpulse-simple.so.0")]
        private static extern int pa_simple_write(IntPtr stream, float[] data, UIntPtr bytes, out int error);

        [DllImport("libpulse-simple.so.0")]
        private static extern void pa_simple_free(IntPtr stream);

        private readonly IntPtr _stream;
        private readonly ISampleProvider _source;

        private PulseAudioSink(IntPtr stream, ISampleProvider source)
        {
            _stream = stream;
            _source = source;
        }

        public static PulseAudioSink Open(ISampleProvider source)
        {
            WaveFormat format = source.WaveFormat;
            SampleSpec spec = new SampleSpec { 
                Format = 5,
                Rate = (uint) format.SampleRate,
                Channels = (byte) format.Channels
            };

            BufferAttr attr = new BufferAttr {
                MaxLength = uint.MaxValue,
                TargetLength = (uint) (format.AverageBytesPerSecond / 20),
                Prebuffer = uint.MaxValue,
                MinRequest = uint.MaxValue,
                FragmentSize = uint.MaxValue,
            };

            IntPtr stream;
            try 
            { 
                stream = pa_simple_new(null, "DSPRE", 1, null, "Preview", ref spec, IntPtr.Zero, ref attr, out _);
            }
            catch (DllNotFoundException) 
            { 
                return null;
            }
            catch (EntryPointNotFoundException)
            { 
                return null;
            }

            if (stream == IntPtr.Zero)
            {
                return null;
            }

            PulseAudioSink sink = new PulseAudioSink(stream, source);
            new Thread(sink.Pump) { IsBackground = true, Name = "DSPRE audio output" }.Start();

            return sink;
        }

        private void Pump()
        {
            WaveFormat format = _source.WaveFormat;
            float[] chunk = new float[format.SampleRate / 100 * format.Channels];

            while (true)
            {
                int read = _source.Read(chunk, 0, chunk.Length);
                if (read <= 0)
                {
                    Array.Clear(chunk);
                    read = chunk.Length;
                }
                if (pa_simple_write(_stream, chunk, (UIntPtr)(read * sizeof(float)), out _) < 0)
                {
                    break;
                }
            }
            pa_simple_free(_stream);
        }
    }
}
