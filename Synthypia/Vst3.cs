using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Synthypia
{
    /// <summary>VST3-Instrument über die native Brücke SynthypiaVst3.dll (siehe Ordner SynthypiaVst3).</summary>
    public sealed class Vst3Output : IPluginHost
    {
        const int SampleRate = 44100;
        const int Block = 256;
        const string Dll = "SynthypiaVst3.dll";

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate void ResizeCallback(int w, int h);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        static extern IntPtr vst3_load(string path, int sampleRate, int blockSize, byte[] err, int errCap);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern void vst3_destroy(IntPtr h);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern IntPtr vst3_name(IntPtr h);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern void vst3_midi(IntPtr h, int status, int d1, int d2);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern void vst3_process(IntPtr h, float[] left, float[] right, int frames);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int vst3_has_editor(IntPtr h);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        static extern int vst3_editor_open(IntPtr h, IntPtr hwnd, ResizeCallback cb, out int w, out int hgt);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern void vst3_editor_close(IntPtr h);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern void vst3_editor_size(IntPtr h, int w, int hgt);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int vst3_get_state(IntPtr h, byte[] buf, int cap);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int vst3_set_state(IntPtr h, byte[] buf, int size);

        readonly object lk = new object();
        readonly ResizeCallback resizeCallback;
        IntPtr handle;
        AudioOut audio;
        bool disposed;
        int editorW, editorH;
        string pluginPath;

        public string Name { get; private set; }
        public bool HasEditor { get; private set; }
        public bool IsOpen { get { return handle != IntPtr.Zero && audio != null && !disposed; } }
        public event Action<int, int> EditorResized;

        /// <summary>Ist die native Brücke neben der EXE vorhanden?</summary>
        public static bool IsAvailable
        {
            get { return File.Exists(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Dll)); }
        }

        public Vst3Output(string path)
        {
            if (!Environment.Is64BitProcess)
                throw new InvalidOperationException("VST3 wird nur im 64-Bit-Prozess unterstützt.");
            if (!IsAvailable)
                throw new InvalidOperationException(Dll + " fehlt neben Synthypia.exe. Sie wird mit SynthypiaVst3\\build.cmd erzeugt.");

            resizeCallback = OnEditorResize;
            var err = new byte[512];
            handle = vst3_load(path, SampleRate, Block, err, err.Length);
            if (handle == IntPtr.Zero)
            {
                int n = Array.IndexOf(err, (byte)0);
                throw new InvalidOperationException(Encoding.UTF8.GetString(err, 0, n < 0 ? err.Length : n));
            }

            try
            {
                Name = ReadUtf8(vst3_name(handle));
                if (string.IsNullOrEmpty(Name)) Name = System.IO.Path.GetFileNameWithoutExtension(path);
                HasEditor = vst3_has_editor(handle) != 0;
                pluginPath = path;
                RestoreState();
                audio = new AudioOut(SampleRate, Block, Render);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        static string ReadUtf8(IntPtr p)
        {
            if (p == IntPtr.Zero) return "";
            var bytes = new List<byte>();
            for (int i = 0; ; i++)
            {
                byte b = Marshal.ReadByte(p, i);
                if (b == 0) break;
                bytes.Add(b);
            }
            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        void Render(float[] l, float[] r, int frames)
        {
            lock (lk)
            {
                if (handle == IntPtr.Zero) return;
                vst3_process(handle, l, r, frames);
            }
        }

        public void Send(int status, int d1, int d2)
        {
            if ((status & 0xF0) == 0xC0) return;   // Programmwechsel würden Presets umschalten
            if ((status & 0x0F) == 9) return;      // GM-Schlagzeugkanal (u. a. Metronom)
            lock (lk)
            {
                if (handle != IntPtr.Zero) vst3_midi(handle, status & 0xFF, d1 & 0x7F, d2 & 0x7F);
            }
        }

        public void SetVolume(double v)
        {
            if (audio != null) audio.Gain = (float)Math.Max(0, Math.Min(1.5, v * 1.2));
        }

        // ---------- Editor (nur im UI-Thread) ----------
        public void GetEditorSize(out int w, out int h)
        {
            w = editorW;
            h = editorH;
        }

        public void EditorOpen(IntPtr hwnd)
        {
            if (vst3_editor_open(handle, hwnd, resizeCallback, out editorW, out editorH) == 0)
                throw new InvalidOperationException("Die Plugin-Oberfläche konnte nicht geöffnet werden.");
        }

        public void EditorClose()
        {
            if (handle == IntPtr.Zero) return;
            vst3_editor_close(handle);
            SaveState();
        }

        // ---------- Zustand (gewählter Klang, geladene Instrumente) ----------
        void SaveState()
        {
            if (pluginPath == null) return;
            try
            {
                byte[] data;
                lock (lk)
                {
                    if (handle == IntPtr.Zero) return;
                    int size = vst3_get_state(handle, null, 0);
                    if (size <= 0) return;
                    // Die Größe kann zwischen zwei Aufrufen leicht schwanken, daher mit Reserve und tatsächlicher Länge
                    var buf = new byte[size + 65536];
                    int written = vst3_get_state(handle, buf, buf.Length);
                    if (written <= 0 || written > buf.Length) return;
                    data = new byte[written];
                    Array.Copy(buf, data, written);
                }
                PluginState.Save(pluginPath, data);
            }
            catch { }
        }

        void RestoreState()
        {
            byte[] data = PluginState.Load(pluginPath);
            if (data == null) return;
            try
            {
                lock (lk) vst3_set_state(handle, data, data.Length);
            }
            catch { }
        }

        public void EditorIdle() { }

        void OnEditorResize(int w, int h)
        {
            editorW = w;
            editorH = h;
            var ev = EditorResized;
            if (ev != null) ev(w, h);
        }

        public void Dispose()
        {
            if (disposed) return;
            SaveState();
            disposed = true;
            if (audio != null) { audio.Dispose(); audio = null; }
            lock (lk)
            {
                if (handle != IntPtr.Zero)
                {
                    try { vst3_destroy(handle); } catch { }
                    handle = IntPtr.Zero;
                }
            }
        }
    }
}
