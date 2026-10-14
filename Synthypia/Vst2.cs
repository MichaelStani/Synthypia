using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace Synthypia
{
    /// <summary>Gemeinsame Schnittstelle für Klangausgaben (Windows-MIDI-Gerät oder VST2-Plugin).</summary>
    public interface IMidiSink : IDisposable
    {
        bool IsOpen { get; }
        void Send(int status, int d1, int d2);
        void SetVolume(double v);
    }

    /// <summary>Ein geladenes Instrument-Plugin (VST2 oder VST3) mit optionaler Oberfläche.</summary>
    public interface IPluginHost : IMidiSink
    {
        string Name { get; }
        bool HasEditor { get; }
        void GetEditorSize(out int w, out int h);
        void EditorOpen(IntPtr hwnd);
        void EditorClose();
        void EditorIdle();
        /// <summary>Das Plugin wünscht eine andere Größe der Oberfläche.</summary>
        event Action<int, int> EditorResized;
    }

    /// <summary>Minimaler VST2-Host: lädt ein Instrument-Plugin, speist es mit MIDI und gibt das Audio über waveOut aus.</summary>
    public sealed class VstOutput : IPluginHost
    {
#pragma warning disable 67
        public event Action<int, int> EditorResized;
#pragma warning restore 67

        const int SampleRate = 44100;
        const int Block = 256;

        // ---------- VST2-Strukturen und Opcodes ----------
        [StructLayout(LayoutKind.Sequential)]
        struct AEffect
        {
            public int Magic;
            public IntPtr Dispatcher, Process, SetParameter, GetParameter;
            public int NumPrograms, NumParams, NumInputs, NumOutputs, Flags;
            public IntPtr Resvd1, Resvd2;
            public int InitialDelay, RealQualities, OffQualities;
            public float IoRatio;
            public IntPtr Object, User;
            public int UniqueId, Version;
            public IntPtr ProcessReplacing, ProcessDoubleReplacing;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate IntPtr HostCallback(IntPtr effect, int opcode, int index, IntPtr value, IntPtr ptr, float opt);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate IntPtr PluginMain(HostCallback host);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate IntPtr Dispatcher(IntPtr effect, int opcode, int index, IntPtr value, IntPtr ptr, float opt);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate void ProcessReplacing(IntPtr effect, IntPtr inputs, IntPtr outputs, int frames);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate float GetParameterFn(IntPtr effect, int index);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate void SetParameterFn(IntPtr effect, int index, float value);

        const int MagicVstP = 0x56737450; // 'VstP'
        const int effOpen = 0, effClose = 1, effSetSampleRate = 10, effSetBlockSize = 11, effMainsChanged = 12,
                  effEditGetRect = 13, effEditOpen = 14, effEditClose = 15, effEditIdle = 19,
                  effProcessEvents = 25, effGetEffectName = 45, effGetVendorString = 47, effGetProductString = 48;
        const int effGetChunk = 23, effSetChunk = 24;
        const int effFlagsHasEditor = 1, effFlagsCanReplacing = 1 << 4, effFlagsProgramChunks = 1 << 5, effFlagsIsSynth = 1 << 8;

        // ---------- Windows-API ----------
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern IntPtr LoadLibraryW(string path);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)] static extern IntPtr GetProcAddress(IntPtr h, string name);
        [DllImport("kernel32.dll")] static extern bool FreeLibrary(IntPtr h);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool SetDllDirectoryW(string path);

        // ---------- Zustand ----------
        readonly object lk = new object();
        readonly HostCallback hostCallback;
        IntPtr lib, effect;
        Dispatcher dispatch;
        ProcessReplacing process;
        AEffect fx;
        IntPtr timeInfo;
        IntPtr[] inBufs, outBufs;
        IntPtr inPtrs, outPtrs;

        AudioOut audio;

        readonly Queue<int> pending = new Queue<int>();
        bool disposed, ready;

        public string Path { get; private set; }
        public string Name { get; private set; }
        public string Vendor { get; private set; }
        public bool HasEditor { get { return (fx.Flags & effFlagsHasEditor) != 0; } }
        public bool IsOpen { get { return audio != null && !disposed; } }
        public IntPtr Effect { get { return effect; } }

        /// <summary>Prüft, ob die DLL zur Prozessarchitektur passt. Gibt eine Fehlermeldung oder null zurück.</summary>
        public static string CheckArchitecture(string path)
        {
            try
            {
                using (var fs = File.OpenRead(path))
                using (var br = new BinaryReader(fs))
                {
                    fs.Position = 0x3C;
                    int pe = br.ReadInt32();
                    fs.Position = pe + 4;
                    ushort machine = br.ReadUInt16();
                    bool dll64 = machine == 0x8664;
                    bool dll32 = machine == 0x014C;
                    if (!dll64 && !dll32) return "Unbekanntes DLL-Format.";
                    if (dll64 && !Environment.Is64BitProcess)
                        return "Das Plugin ist 64-Bit, Synthypia läuft aber als 32-Bit-Prozess.";
                    if (dll32 && Environment.Is64BitProcess)
                        return "Das Plugin ist 32-Bit, Synthypia läuft aber als 64-Bit-Prozess. Bitte die 64-Bit-Version des Plugins verwenden.";
                }
            }
            catch (Exception ex)
            {
                return "Die Datei konnte nicht gelesen werden: " + ex.Message;
            }
            return null;
        }

        public VstOutput(string path)
        {
            Path = path;
            hostCallback = OnHostCallback;

            string err = CheckArchitecture(path);
            if (err != null) throw new InvalidOperationException(err);

            // Plugins laden ihre Zusatz-DLLs oft aus dem eigenen Ordner.
            SetDllDirectoryW(System.IO.Path.GetDirectoryName(path));
            lib = LoadLibraryW(path);
            if (lib == IntPtr.Zero)
                throw new InvalidOperationException("Die DLL konnte nicht geladen werden (Fehlercode " + Marshal.GetLastWin32Error() + ").");

            try
            {
                IntPtr entry = GetProcAddress(lib, "VSTPluginMain");
                if (entry == IntPtr.Zero) entry = GetProcAddress(lib, "main");
                if (entry == IntPtr.Zero) throw new InvalidOperationException("Die DLL ist kein VST2-Plugin (kein Einstiegspunkt gefunden).");

                var main = (PluginMain)Marshal.GetDelegateForFunctionPointer(entry, typeof(PluginMain));
                effect = main(hostCallback);
                if (effect == IntPtr.Zero) throw new InvalidOperationException("Das Plugin konnte nicht initialisiert werden.");

                fx = (AEffect)Marshal.PtrToStructure(effect, typeof(AEffect));
                if (fx.Magic != MagicVstP) throw new InvalidOperationException("Das ist kein gültiges VST2-Plugin.");
                if ((fx.Flags & effFlagsCanReplacing) == 0 || fx.ProcessReplacing == IntPtr.Zero)
                    throw new InvalidOperationException("Das Plugin unterstützt kein processReplacing.");
                if ((fx.Flags & effFlagsIsSynth) == 0)
                    throw new InvalidOperationException("Das Plugin ist kein Instrument (Synthesizer). Bitte ein Instrument-Plugin wählen.");

                dispatch = (Dispatcher)Marshal.GetDelegateForFunctionPointer(fx.Dispatcher, typeof(Dispatcher));
                process = (ProcessReplacing)Marshal.GetDelegateForFunctionPointer(fx.ProcessReplacing, typeof(ProcessReplacing));

                timeInfo = Marshal.AllocHGlobal(128);
                for (int i = 0; i < 128; i++) Marshal.WriteByte(timeInfo, i, 0);
                Marshal.WriteInt64(timeInfo, 8, BitConverter.DoubleToInt64Bits(SampleRate));
                Marshal.WriteInt64(timeInfo, 32, BitConverter.DoubleToInt64Bits(120.0));
                Marshal.WriteInt32(timeInfo, 64, 4);
                Marshal.WriteInt32(timeInfo, 68, 4);
                Marshal.WriteInt32(timeInfo, 84, (1 << 10) | (1 << 13)); // Tempo und Taktart gültig

                dispatch(effect, effOpen, 0, IntPtr.Zero, IntPtr.Zero, 0);
                dispatch(effect, effSetSampleRate, 0, IntPtr.Zero, IntPtr.Zero, SampleRate);
                dispatch(effect, effSetBlockSize, 0, (IntPtr)Block, IntPtr.Zero, 0);
                dispatch(effect, effMainsChanged, 0, (IntPtr)1, IntPtr.Zero, 0);
                dispatch(effect, 71, 0, IntPtr.Zero, IntPtr.Zero, 0); // effStartProcess

                Name = GetString(effGetEffectName);
                if (string.IsNullOrEmpty(Name)) Name = GetString(effGetProductString);
                if (string.IsNullOrEmpty(Name)) Name = System.IO.Path.GetFileNameWithoutExtension(path);
                Vendor = GetString(effGetVendorString);

                AllocBuffers();
                RestoreState();
                OpenAudio();
                ready = true;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        string GetString(int opcode)
        {
            IntPtr buf = Marshal.AllocHGlobal(256);
            try
            {
                for (int i = 0; i < 256; i++) Marshal.WriteByte(buf, i, 0);
                dispatch(effect, opcode, 0, IntPtr.Zero, buf, 0);
                return Marshal.PtrToStringAnsi(buf);
            }
            catch { return ""; }
            finally { Marshal.FreeHGlobal(buf); }
        }

        void AllocBuffers()
        {
            int nIn = Math.Max(0, fx.NumInputs), nOut = Math.Max(2, fx.NumOutputs);
            inBufs = new IntPtr[nIn];
            outBufs = new IntPtr[nOut];
            inPtrs = Marshal.AllocHGlobal(IntPtr.Size * Math.Max(1, nIn));
            outPtrs = Marshal.AllocHGlobal(IntPtr.Size * nOut);
            for (int i = 0; i < nIn; i++)
            {
                inBufs[i] = AllocZeroed(Block * 4);
                Marshal.WriteIntPtr(inPtrs, i * IntPtr.Size, inBufs[i]);
            }
            for (int i = 0; i < nOut; i++)
            {
                outBufs[i] = AllocZeroed(Block * 4);
                Marshal.WriteIntPtr(outPtrs, i * IntPtr.Size, outBufs[i]);
            }
        }

        static IntPtr AllocZeroed(int bytes)
        {
            IntPtr p = Marshal.AllocHGlobal(bytes);
            for (int i = 0; i < bytes; i += 4) Marshal.WriteInt32(p, i, 0);
            return p;
        }

        void OpenAudio()
        {
            audio = new AudioOut(SampleRate, Block, Render);
        }

        void Render(float[] l, float[] r, int frames)
        {
            lock (lk)
            {
                if (disposed) return;
                SendPending();
                for (int c = 0; c < outBufs.Length; c++)
                    for (int i = 0; i < Block * 4; i += 4) Marshal.WriteInt32(outBufs[c], i, 0);
                try { process(effect, inPtrs, outPtrs, Block); }
                catch { return; }
                Marshal.Copy(outBufs[0], l, 0, frames);
                Marshal.Copy(outBufs[outBufs.Length > 1 ? 1 : 0], r, 0, frames);
            }
        }

        // ---------- MIDI ----------
        public void Send(int status, int d1, int d2)
        {
            int hi = status & 0xF0;
            if (hi == 0xC0) return;            // Programmwechsel würden Presets des Plugins umschalten
            if ((status & 0x0F) == 9) return;  // GM-Schlagzeugkanal (u. a. Metronom) ergibt am Instrument keinen Sinn
            lock (lk)
            {
                if (disposed) return;
                pending.Enqueue((status & 0xFF) | ((d1 & 0x7F) << 8) | ((d2 & 0x7F) << 16));
            }
        }

        // Aufruf mit gehaltenem lk
        void SendPending()
        {
            int n = pending.Count;
            if (n == 0) return;
            if (n > 256) n = 256;

            const int evSize = 32;
            IntPtr evs = Marshal.AllocHGlobal(evSize * n);
            IntPtr list = Marshal.AllocHGlobal(16 + IntPtr.Size * n);
            try
            {
                Marshal.WriteInt32(list, 0, n);
                Marshal.WriteIntPtr(list, 8, IntPtr.Zero);
                for (int i = 0; i < n; i++)
                {
                    int msg = pending.Dequeue();
                    IntPtr e = evs + i * evSize;
                    for (int b = 0; b < evSize; b += 4) Marshal.WriteInt32(e, b, 0);
                    Marshal.WriteInt32(e, 0, 1);        // kVstMidiType
                    Marshal.WriteInt32(e, 4, evSize);   // byteSize
                    Marshal.WriteInt32(e, 16, 0);       // noteLength / offset bleiben 0
                    Marshal.WriteInt32(e, 24, msg & 0xFFFFFF);
                    Marshal.WriteIntPtr(list, 16 + i * IntPtr.Size, e);
                }
                try { dispatch(effect, effProcessEvents, 0, IntPtr.Zero, list, 0); }
                catch { }
            }
            finally
            {
                Marshal.FreeHGlobal(evs);
                Marshal.FreeHGlobal(list);
            }
        }

        public void SetVolume(double v)
        {
            if (audio != null) audio.Gain = (float)Math.Max(0, Math.Min(1.5, v * 1.2));
        }

        // ---------- Editor ----------
        /// <summary>Größe der Plugin-Oberfläche (Breite, Höhe) oder 0,0 ohne Editor.</summary>
        public void GetEditorSize(out int w, out int h)
        {
            w = h = 0;
            if (!HasEditor || disposed) return;
            IntPtr pp = Marshal.AllocHGlobal(IntPtr.Size);
            try
            {
                Marshal.WriteIntPtr(pp, IntPtr.Zero);
                dispatch(effect, effEditGetRect, 0, IntPtr.Zero, pp, 0);
                IntPtr rect = Marshal.ReadIntPtr(pp);
                if (rect != IntPtr.Zero)
                {
                    short top = Marshal.ReadInt16(rect, 0), left = Marshal.ReadInt16(rect, 2);
                    short bottom = Marshal.ReadInt16(rect, 4), right = Marshal.ReadInt16(rect, 6);
                    w = right - left;
                    h = bottom - top;
                }
            }
            finally { Marshal.FreeHGlobal(pp); }
        }

        public void EditorOpen(IntPtr hwnd)
        {
            dispatch(effect, effEditOpen, 0, IntPtr.Zero, hwnd, 0);
        }

        public void EditorClose()
        {
            if (disposed) return;
            dispatch(effect, effEditClose, 0, IntPtr.Zero, IntPtr.Zero, 0);
            SaveState();
        }

        // ---------- Zustand (gewählter Klang) ----------
        // Format: 1 = Chunk des Plugins, 2 = Liste der Parameterwerte
        void SaveState()
        {
            if (!ready) return;
            try
            {
                byte[] data = null;
                lock (lk)
                {
                    if (disposed) return;
                    if ((fx.Flags & effFlagsProgramChunks) != 0)
                    {
                        IntPtr holder = Marshal.AllocHGlobal(IntPtr.Size);
                        try
                        {
                            Marshal.WriteIntPtr(holder, IntPtr.Zero);
                            int size = (int)dispatch(effect, effGetChunk, 0, IntPtr.Zero, holder, 0);
                            IntPtr src = Marshal.ReadIntPtr(holder);
                            if (size > 0 && src != IntPtr.Zero)
                            {
                                data = new byte[size + 1];
                                data[0] = 1;
                                Marshal.Copy(src, data, 1, size);
                            }
                        }
                        finally { Marshal.FreeHGlobal(holder); }
                    }
                    else if (fx.NumParams > 0 && fx.GetParameter != IntPtr.Zero)
                    {
                        var get = (GetParameterFn)Marshal.GetDelegateForFunctionPointer(fx.GetParameter, typeof(GetParameterFn));
                        data = new byte[1 + 4 * fx.NumParams];
                        data[0] = 2;
                        for (int i = 0; i < fx.NumParams; i++)
                            Array.Copy(BitConverter.GetBytes(get(effect, i)), 0, data, 1 + 4 * i, 4);
                    }
                }
                if (data != null) PluginState.Save(Path, data);
            }
            catch { }
        }

        void RestoreState()
        {
            byte[] data = PluginState.Load(Path);
            if (data == null || data.Length < 2) return;
            try
            {
                lock (lk)
                {
                    if (data[0] == 1 && (fx.Flags & effFlagsProgramChunks) != 0)
                    {
                        IntPtr p = Marshal.AllocHGlobal(data.Length - 1);
                        try
                        {
                            Marshal.Copy(data, 1, p, data.Length - 1);
                            dispatch(effect, effSetChunk, 0, (IntPtr)(data.Length - 1), p, 0);
                        }
                        finally { Marshal.FreeHGlobal(p); }
                    }
                    else if (data[0] == 2 && fx.SetParameter != IntPtr.Zero && data.Length == 1 + 4 * fx.NumParams)
                    {
                        var set = (SetParameterFn)Marshal.GetDelegateForFunctionPointer(fx.SetParameter, typeof(SetParameterFn));
                        for (int i = 0; i < fx.NumParams; i++) set(effect, i, BitConverter.ToSingle(data, 1 + 4 * i));
                    }
                }
            }
            catch { }
        }

        public void EditorIdle()
        {
            if (!disposed) dispatch(effect, effEditIdle, 0, IntPtr.Zero, IntPtr.Zero, 0);
        }

        // ---------- Host-Rückruf ----------
        IntPtr OnHostCallback(IntPtr eff, int opcode, int index, IntPtr value, IntPtr ptr, float opt)
        {
            switch (opcode)
            {
                case 1: return (IntPtr)2400;                    // audioMasterVersion
                case 7: return timeInfo;                        // audioMasterGetTime
                case 16: return (IntPtr)SampleRate;             // audioMasterGetSampleRate
                case 17: return (IntPtr)Block;                  // audioMasterGetBlockSize
                case 23: return (IntPtr)2;                      // audioMasterGetCurrentProcessLevel: Benutzer-/Audio-Thread
                case 32: WriteAnsi(ptr, "Synthypia"); return (IntPtr)1;       // Hersteller
                case 33: WriteAnsi(ptr, "Synthypia"); return (IntPtr)1;       // Produkt
                case 34: return (IntPtr)1000;                   // Version
                case 38: return (IntPtr)1;                      // Sprache: Englisch
                case 37:                                        // audioMasterCanDo
                    {
                        string s = ptr == IntPtr.Zero ? "" : Marshal.PtrToStringAnsi(ptr);
                        return (s == "sendVstEvents" || s == "sendVstMidiEvent" || s == "sendVstTimeInfo" || s == "sizeWindow") ? (IntPtr)1 : IntPtr.Zero;
                    }
                default: return IntPtr.Zero;
            }
        }

        static void WriteAnsi(IntPtr dest, string s)
        {
            if (dest == IntPtr.Zero) return;
            for (int i = 0; i < s.Length; i++) Marshal.WriteByte(dest, i, (byte)s[i]);
            Marshal.WriteByte(dest, s.Length, 0);
        }

        // ---------- Aufräumen ----------
        public void Dispose()
        {
            if (disposed) return;
            SaveState();
            if (audio != null) { audio.Dispose(); audio = null; }

            lock (lk)
            {
                disposed = true;

                if (effect != IntPtr.Zero && dispatch != null)
                {
                    try
                    {
                        dispatch(effect, effMainsChanged, 0, IntPtr.Zero, IntPtr.Zero, 0);
                        dispatch(effect, effClose, 0, IntPtr.Zero, IntPtr.Zero, 0);
                    }
                    catch { }
                    effect = IntPtr.Zero;
                }
                if (lib != IntPtr.Zero) { FreeLibrary(lib); lib = IntPtr.Zero; }

                FreeAll(inBufs); FreeAll(outBufs);
                inBufs = outBufs = null;
                if (inPtrs != IntPtr.Zero) Marshal.FreeHGlobal(inPtrs);
                if (outPtrs != IntPtr.Zero) Marshal.FreeHGlobal(outPtrs);
                if (timeInfo != IntPtr.Zero) Marshal.FreeHGlobal(timeInfo);
                inPtrs = outPtrs = timeInfo = IntPtr.Zero;
            }
        }

        static void FreeAll(IntPtr[] a)
        {
            if (a == null) return;
            foreach (var p in a) if (p != IntPtr.Zero) Marshal.FreeHGlobal(p);
        }
    }
}
