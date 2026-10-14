using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Synthypia
{
    static class NativeMidi
    {
        public delegate void MidiInProc(IntPtr hMidiIn, uint wMsg, IntPtr dwInstance, IntPtr dwParam1, IntPtr dwParam2);

        public const uint MIDI_MAPPER = 0xFFFFFFFF;
        public const uint CALLBACK_FUNCTION = 0x00030000;
        public const uint MIM_DATA = 0x3C3;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct MIDIOUTCAPS
        {
            public ushort wMid, wPid;
            public uint vDriverVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szPname;
            public ushort wTechnology, wVoices, wNotes, wChannelMask;
            public uint dwSupport;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct MIDIINCAPS
        {
            public ushort wMid, wPid;
            public uint vDriverVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szPname;
            public uint dwSupport;
        }

        [DllImport("winmm.dll")] public static extern uint midiOutGetNumDevs();
        [DllImport("winmm.dll", CharSet = CharSet.Unicode)] public static extern uint midiOutGetDevCaps(UIntPtr id, ref MIDIOUTCAPS caps, uint size);
        [DllImport("winmm.dll")] public static extern uint midiOutOpen(out IntPtr h, uint id, IntPtr cb, IntPtr inst, uint flags);
        [DllImport("winmm.dll")] public static extern uint midiOutShortMsg(IntPtr h, uint msg);
        [DllImport("winmm.dll")] public static extern uint midiOutReset(IntPtr h);
        [DllImport("winmm.dll")] public static extern uint midiOutClose(IntPtr h);
        [DllImport("winmm.dll")] public static extern uint midiOutSetVolume(IntPtr h, uint vol);

        [DllImport("winmm.dll")] public static extern uint midiInGetNumDevs();
        [DllImport("winmm.dll", CharSet = CharSet.Unicode)] public static extern uint midiInGetDevCaps(UIntPtr id, ref MIDIINCAPS caps, uint size);
        [DllImport("winmm.dll")] public static extern uint midiInOpen(out IntPtr h, uint id, MidiInProc cb, IntPtr inst, uint flags);
        [DllImport("winmm.dll")] public static extern uint midiInStart(IntPtr h);
        [DllImport("winmm.dll")] public static extern uint midiInStop(IntPtr h);
        [DllImport("winmm.dll")] public static extern uint midiInReset(IntPtr h);
        [DllImport("winmm.dll")] public static extern uint midiInClose(IntPtr h);

        [DllImport("winmm.dll")] public static extern uint timeBeginPeriod(uint ms);
        [DllImport("winmm.dll")] public static extern uint timeEndPeriod(uint ms);
    }

    /// <summary>MIDI-Ausgabe (standardmäßig Microsoft GS Wavetable Synth).</summary>
    public sealed class MidiOutDevice : IMidiSink
    {
        IntPtr handle;
        readonly object lk = new object();

        public static List<string> ListDevices()
        {
            var list = new List<string>();
            uint n = NativeMidi.midiOutGetNumDevs();
            for (uint i = 0; i < n; i++)
            {
                var caps = new NativeMidi.MIDIOUTCAPS();
                NativeMidi.midiOutGetDevCaps((UIntPtr)i, ref caps, (uint)Marshal.SizeOf(typeof(NativeMidi.MIDIOUTCAPS)));
                list.Add(caps.szPname);
            }
            return list;
        }

        /// <param name="index">-1 = Windows-MIDI-Mapper, sonst Geräteindex</param>
        public MidiOutDevice(int index)
        {
            uint id = index < 0 ? NativeMidi.MIDI_MAPPER : (uint)index;
            if (NativeMidi.midiOutOpen(out handle, id, IntPtr.Zero, IntPtr.Zero, 0) != 0)
                handle = IntPtr.Zero;
        }

        public bool IsOpen { get { return handle != IntPtr.Zero; } }

        public void Send(int status, int d1, int d2)
        {
            lock (lk)
            {
                if (handle == IntPtr.Zero) return;
                NativeMidi.midiOutShortMsg(handle, (uint)((status & 0xFF) | ((d1 & 0x7F) << 8) | ((d2 & 0x7F) << 16)));
            }
        }

        public void SetVolume(double v)
        {
            lock (lk)
            {
                if (handle == IntPtr.Zero) return;
                uint x = (uint)(Math.Max(0, Math.Min(1, v)) * 0xFFFF);
                NativeMidi.midiOutSetVolume(handle, (x << 16) | x);
            }
        }

        public void Dispose()
        {
            lock (lk)
            {
                if (handle == IntPtr.Zero) return;
                NativeMidi.midiOutReset(handle);
                NativeMidi.midiOutClose(handle);
                handle = IntPtr.Zero;
            }
        }
    }

    /// <summary>MIDI-Eingabe (z. B. USB-Keyboard).</summary>
    public sealed class MidiInDevice : IDisposable
    {
        IntPtr handle;
        readonly NativeMidi.MidiInProc proc;
        public event Action<int, int, int> Message;

        public static List<string> ListDevices()
        {
            var list = new List<string>();
            uint n = NativeMidi.midiInGetNumDevs();
            for (uint i = 0; i < n; i++)
            {
                var caps = new NativeMidi.MIDIINCAPS();
                NativeMidi.midiInGetDevCaps((UIntPtr)i, ref caps, (uint)Marshal.SizeOf(typeof(NativeMidi.MIDIINCAPS)));
                list.Add(caps.szPname);
            }
            return list;
        }

        public MidiInDevice(int index)
        {
            proc = OnMidi;
            if (NativeMidi.midiInOpen(out handle, (uint)index, proc, IntPtr.Zero, NativeMidi.CALLBACK_FUNCTION) != 0)
            {
                handle = IntPtr.Zero;
                throw new InvalidOperationException("MIDI-Eingabegerät konnte nicht geöffnet werden (evtl. von einem anderen Programm belegt).");
            }
            NativeMidi.midiInStart(handle);
        }

        void OnMidi(IntPtr h, uint msg, IntPtr inst, IntPtr p1, IntPtr p2)
        {
            if (msg != NativeMidi.MIM_DATA) return;
            long v = p1.ToInt64();
            int status = (int)(v & 0xFF);
            if (status >= 0xF0) return;
            var handler = Message;
            if (handler != null) handler(status, (int)((v >> 8) & 0xFF), (int)((v >> 16) & 0xFF));
        }

        public void Dispose()
        {
            if (handle == IntPtr.Zero) return;
            NativeMidi.midiInStop(handle);
            NativeMidi.midiInReset(handle);
            NativeMidi.midiInClose(handle);
            handle = IntPtr.Zero;
        }
    }
}
