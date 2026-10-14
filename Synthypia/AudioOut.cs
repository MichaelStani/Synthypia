using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Synthypia
{
    /// <summary>Stereo-Float-Ausgabe über waveOut mit mehreren Puffern. Die Klangerzeugung liefert die Samples per Rückruf.</summary>
    public sealed class AudioOut : IDisposable
    {
        /// <summary>Füllt <paramref name="frames"/> Samples je Kanal.</summary>
        public delegate void RenderFn(float[] left, float[] right, int frames);

        const int BufferCount = 6;

        [StructLayout(LayoutKind.Sequential)]
        struct WaveFormatEx
        {
            public ushort FormatTag, Channels;
            public uint SamplesPerSec, AvgBytesPerSec;
            public ushort BlockAlign, BitsPerSample, Size;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct WaveHdr
        {
            public IntPtr Data;
            public uint BufferLength, BytesRecorded;
            public IntPtr User;
            public uint Flags, Loops;
            public IntPtr Next, Reserved;
        }

        [DllImport("winmm.dll")] static extern uint waveOutOpen(out IntPtr h, uint dev, ref WaveFormatEx fmt, IntPtr cb, IntPtr inst, uint flags);
        [DllImport("winmm.dll")] static extern uint waveOutPrepareHeader(IntPtr h, IntPtr hdr, uint size);
        [DllImport("winmm.dll")] static extern uint waveOutUnprepareHeader(IntPtr h, IntPtr hdr, uint size);
        [DllImport("winmm.dll")] static extern uint waveOutWrite(IntPtr h, IntPtr hdr, uint size);
        [DllImport("winmm.dll")] static extern uint waveOutReset(IntPtr h);
        [DllImport("winmm.dll")] static extern uint waveOutClose(IntPtr h);
        const uint CALLBACK_EVENT = 0x00050000, WAVE_MAPPER = 0xFFFFFFFF, WHDR_DONE = 1;

        readonly int block;
        readonly RenderFn render;
        readonly IntPtr[] headers = new IntPtr[BufferCount];
        readonly IntPtr[] buffers = new IntPtr[BufferCount];
        readonly AutoResetEvent waveEvent = new AutoResetEvent(false);
        readonly float[] l, r, mix;
        IntPtr waveOut;
        Thread thread;
        volatile bool quit;
        bool disposed;

        /// <summary>Lautstärkefaktor, wird auf das gerenderte Signal angewendet.</summary>
        public volatile float Gain = 0.8f;

        public AudioOut(int sampleRate, int block, RenderFn render)
        {
            this.block = block;
            this.render = render;
            l = new float[block];
            r = new float[block];
            mix = new float[block * 2];

            var fmt = new WaveFormatEx
            {
                FormatTag = 3, // IEEE-Float
                Channels = 2,
                SamplesPerSec = (uint)sampleRate,
                BitsPerSample = 32,
                BlockAlign = 8,
                AvgBytesPerSec = (uint)sampleRate * 8,
                Size = 0
            };
            if (waveOutOpen(out waveOut, WAVE_MAPPER, ref fmt, waveEvent.SafeWaitHandle.DangerousGetHandle(), IntPtr.Zero, CALLBACK_EVENT) != 0)
            {
                waveOut = IntPtr.Zero;
                throw new InvalidOperationException("Das Audiogerät konnte nicht geöffnet werden.");
            }

            int hdrSize = Marshal.SizeOf(typeof(WaveHdr));
            for (int i = 0; i < BufferCount; i++)
            {
                buffers[i] = Marshal.AllocHGlobal(block * 8);
                for (int b = 0; b < block * 8; b += 4) Marshal.WriteInt32(buffers[i], b, 0);
                headers[i] = Marshal.AllocHGlobal(hdrSize);
                Marshal.StructureToPtr(new WaveHdr { Data = buffers[i], BufferLength = (uint)(block * 8) }, headers[i], false);
                waveOutPrepareHeader(waveOut, headers[i], (uint)hdrSize);
                waveOutWrite(waveOut, headers[i], (uint)hdrSize);
            }

            thread = new Thread(Loop) { IsBackground = true, Name = "Synthypia-Audio", Priority = ThreadPriority.Highest };
            thread.Start();
        }

        void Loop()
        {
            int hdrSize = Marshal.SizeOf(typeof(WaveHdr));
            int next = 0; // Puffer müssen in genau der Reihenfolge neu eingereiht werden, in der sie abgespielt wurden
            while (!quit)
            {
                waveEvent.WaitOne(50);
                while (!quit)
                {
                    var h = (WaveHdr)Marshal.PtrToStructure(headers[next], typeof(WaveHdr));
                    if ((h.Flags & WHDR_DONE) == 0) break;
                    Fill(buffers[next]);
                    waveOutWrite(waveOut, headers[next], (uint)hdrSize);
                    next = (next + 1) % BufferCount;
                }
            }
        }

        void Fill(IntPtr dest)
        {
            Array.Clear(l, 0, block);
            Array.Clear(r, 0, block);
            try { render(l, r, block); }
            catch { Array.Clear(l, 0, block); Array.Clear(r, 0, block); }

            float g = Gain;
            for (int i = 0; i < block; i++)
            {
                mix[2 * i] = Clip(l[i] * g);
                mix[2 * i + 1] = Clip(r[i] * g);
            }
            Marshal.Copy(mix, 0, dest, mix.Length);
        }

        static float Clip(float x)
        {
            return x > 1f ? 1f : x < -1f ? -1f : x;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            quit = true;
            if (thread != null) thread.Join(500);

            if (waveOut != IntPtr.Zero)
            {
                waveOutReset(waveOut);
                int hdrSize = Marshal.SizeOf(typeof(WaveHdr));
                for (int i = 0; i < BufferCount; i++)
                    if (headers[i] != IntPtr.Zero) waveOutUnprepareHeader(waveOut, headers[i], (uint)hdrSize);
                waveOutClose(waveOut);
                waveOut = IntPtr.Zero;
            }
            for (int i = 0; i < BufferCount; i++)
            {
                if (headers[i] != IntPtr.Zero) Marshal.FreeHGlobal(headers[i]);
                if (buffers[i] != IntPtr.Zero) Marshal.FreeHGlobal(buffers[i]);
                headers[i] = buffers[i] = IntPtr.Zero;
            }
            waveEvent.Dispose();
        }
    }
}
