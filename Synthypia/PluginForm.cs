using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Synthypia
{
    /// <summary>Fenster für die Oberfläche eines VST-Plugins.</summary>
    public sealed class PluginForm : Form
    {
        readonly IPluginHost plugin;
        readonly Timer idle = new Timer { Interval = 40 };
        readonly TitleBar titleBar;
        readonly Panel host;
        bool editorOpen;

        const int WM_NCCALCSIZE = 0x83, WM_NCHITTEST = 0x84, HTCLIENT = 1, HTCAPTION = 2;

        public PluginForm(IPluginHost plugin)
        {
            this.plugin = plugin;
            Text = plugin.Name;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            BackColor = Color.FromArgb(32, 36, 50);

            // Gleiche eigene Titelleiste wie im Hauptfenster (hier nur mit Schließen-Schaltfläche);
            // die Plugin-Oberfläche sitzt in einem eigenen Bereich darunter.
            host = new Panel { Dock = DockStyle.Fill, BackColor = BackColor };
            titleBar = new TitleBar(this) { CloseOnly = true };
            Controls.Add(host);
            Controls.Add(titleBar);
            ClientSize = new Size(400, 300 + titleBar.Height);

            idle.Tick += (s, e) => { if (editorOpen) plugin.EditorIdle(); };
            plugin.EditorResized += OnPluginResized;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Chrome.Apply(this);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCCALCSIZE && m.WParam != IntPtr.Zero)
            {
                // Standard-Titelleiste entfernen: Der Client-Bereich beginnt ganz oben
                var r = (Chrome.RECT)Marshal.PtrToStructure(m.LParam, typeof(Chrome.RECT));
                int origTop = r.top;
                base.WndProc(ref m);
                r = (Chrome.RECT)Marshal.PtrToStructure(m.LParam, typeof(Chrome.RECT));
                r.top = origTop;
                Marshal.StructureToPtr(r, m.LParam, false);
                m.Result = IntPtr.Zero;
                return;
            }

            if (m.Msg == WM_NCHITTEST)
            {
                base.WndProc(ref m);
                if ((int)m.Result == HTCLIENT)
                {
                    long l = m.LParam.ToInt64();
                    var p = PointToClient(new Point((short)(l & 0xFFFF), (short)((l >> 16) & 0xFFFF)));
                    if (p.Y >= 0 && p.Y < titleBar.Height) m.Result = (IntPtr)HTCAPTION;   // Titelleiste: Fenster ziehen
                }
                return;
            }

            base.WndProc(ref m);
        }

        void OnPluginResized(int w, int h)
        {
            if (IsDisposed) return;
            if (InvokeRequired) BeginInvoke((Action)(() => ApplySize(w, h)));
            else ApplySize(w, h);
        }

        void ApplySize(int w, int h)
        {
            if (w > 0 && h > 0) ClientSize = new Size(w, h + titleBar.Height);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            try
            {
                plugin.EditorOpen(host.Handle);
                editorOpen = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Synthypia", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Close();
                return;
            }
            int w, h;
            plugin.GetEditorSize(out w, out h);
            ApplySize(w, h);
            idle.Start();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            idle.Stop();
            plugin.EditorResized -= OnPluginResized;
            if (editorOpen)
            {
                editorOpen = false;
                try { plugin.EditorClose(); } catch { }
            }
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) idle.Dispose();
            base.Dispose(disposing);
        }
    }
}
