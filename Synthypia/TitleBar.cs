using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Synthypia
{
    /// <summary>
    /// Eigene Titelleiste im Windows-11-Stil: Symbol, Titel und flache Fenster-Schaltflächen in der Farbe der Oberfläche.
    /// Ersetzt die Standardleiste; Rahmen, Größenänderung, Andocken und Schatten bleiben beim System (siehe Form1.WndProc).
    /// </summary>
    sealed class TitleBar : Control
    {
        public static readonly Color BarColor = Color.FromArgb(32, 36, 50);
        static readonly Color Accent = Color.FromArgb(0, 120, 212);
        static readonly Color TextColor = Color.FromArgb(234, 237, 245);
        static readonly Color Muted = Color.FromArgb(130, 138, 160);

        const int WM_NCHITTEST = 0x84, HTCLIENT = 1, HTTRANSPARENT = -1;
        const int Min = 0, Max = 1, Close = 2;

        readonly Form form;
        int hot = -1, pressed = -1;
        string iconFamily;

        public TitleBar(Form form)
        {
            this.form = form;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Dock = DockStyle.Top;
            BackColor = BarColor;
            TabStop = false;
            Height = LogicalToDeviceUnits(34);

            form.TextChanged += (s, e) => Invalidate();
            form.Activated += (s, e) => Invalidate();
            form.Deactivate += (s, e) => Invalidate();
            form.Resize += (s, e) => Invalidate();
        }

        /// <summary>Nur die Schließen-Schaltfläche anzeigen (für Fenster fester Größe, z. B. das Plugin-Fenster).</summary>
        public bool CloseOnly { get; set; }

        int ButtonWidth { get { return LogicalToDeviceUnits(46); } }

        int FirstButton { get { return CloseOnly ? Close : Min; } }

        Rectangle ButtonRect(int i)
        {
            int w = ButtonWidth;
            return new Rectangle(Width - (3 - i) * w, 0, w, Height);
        }

        int HitButton(Point p)
        {
            for (int i = FirstButton; i < 3; i++) if (ButtonRect(i).Contains(p)) return i;
            return -1;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCHITTEST)
            {
                long l = m.LParam.ToInt64();
                var screen = new Point((short)(l & 0xFFFF), (short)((l >> 16) & 0xFFFF));
                // Auf den Schaltflächen selbst auswerten, sonst dem Fenster überlassen (Ziehen, Größe, Doppelklick)
                m.Result = (IntPtr)(HitButton(PointToClient(screen)) >= 0 ? HTCLIENT : HTTRANSPARENT);
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = HitButton(e.Location);
            if (h != hot) { hot = h; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hot = -1;
            pressed = -1;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { pressed = HitButton(e.Location); Invalidate(); }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            int h = HitButton(e.Location);
            int p = pressed;
            pressed = -1;
            Invalidate();
            if (e.Button == MouseButtons.Left && h >= 0 && h == p)
            {
                switch (h)
                {
                    case Min: form.WindowState = FormWindowState.Minimized; break;
                    case Max: form.WindowState = form.WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized; break;
                    case Close: form.Close(); break;
                }
            }
            base.OnMouseUp(e);
        }

        Font IconFont(float size)
        {
            if (iconFamily == null)
            {
                using (var fc = new InstalledFontCollection())
                    iconFamily = fc.Families.Any(f => f.Name == "Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";
            }
            return new Font(iconFamily, size, GraphicsUnit.Point);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BarColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            bool active = form.ContainsFocus || Form.ActiveForm == form;

            // Symbol: kleine Klaviatur in der Akzentfarbe
            int s = LogicalToDeviceUnits(20), ix = LogicalToDeviceUnits(12), iy = (Height - s) / 2;
            using (var br = new SolidBrush(Accent)) g.FillRectangle(br, ix, iy, s, s);
            float u = s / 20f;
            using (var white = new SolidBrush(Color.White))
                for (int k = 0; k < 3; k++) g.FillRectangle(white, ix + (3 + k * 5) * u, iy + 6 * u, 4 * u, 11 * u);
            using (var black = new SolidBrush(Color.FromArgb(24, 26, 36)))
                for (int k = 0; k < 2; k++) g.FillRectangle(black, ix + (6.5f + k * 5) * u, iy + 6 * u, 3 * u, 7 * u);

            // Titel
            int tx = ix + s + LogicalToDeviceUnits(10);
            var flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
            using (var f = new Font(Theme.UiFont.FontFamily, 9.5f))
                TextRenderer.DrawText(g, form.Text, f, new Rectangle(tx, 0, Math.Max(10, Width - (3 - FirstButton) * ButtonWidth - tx - 8), Height), active ? TextColor : Muted, flags);

            // Fenster-Schaltflächen
            string[] glyphs = { "", form.WindowState == FormWindowState.Maximized ? "" : "", "" };
            using (var icon = IconFont(9.5f))
            {
                for (int i = FirstButton; i < 3; i++)
                {
                    var r = ButtonRect(i);
                    bool isHot = hot == i, isDown = pressed == i && isHot;
                    Color fg = active ? TextColor : Muted;
                    if (isHot || isDown)
                    {
                        Color bg = i == Close ? (isDown ? Color.FromArgb(160, 20, 30) : Color.FromArgb(232, 17, 35))
                                              : Color.FromArgb(isDown ? 40 : 26, 255, 255, 255);
                        using (var br = new SolidBrush(bg)) g.FillRectangle(br, r);
                        if (i == Close) fg = Color.White;
                    }
                    TextRenderer.DrawText(g, glyphs[i], icon, r, fg, TextFormatFlags.NoPadding | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }
        }
    }

    /// <summary>Win32-Hilfen für das eigene Fenster-Design.</summary>
    static class Chrome
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int left, top, right, bottom; }

        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] static extern int GetSystemMetrics(int index);
        [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr hwnd);

        /// <summary>Ob das Fenster maximiert ist, direkt vom Fensterstil (Form.WindowState wird erst nach WM_SIZE aktualisiert).</summary>
        public static bool IsMaximized(Form f) { return f.IsHandleCreated && IsZoomed(f.Handle); }

        const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20, DWMWA_BORDER_COLOR = 34, DWMWA_CAPTION_COLOR = 35;
        const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOZORDER = 0x4, SWP_NOACTIVATE = 0x10, SWP_FRAMECHANGED = 0x20;

        static int Bgr(Color c) { return c.R | (c.G << 8) | (c.B << 16); }

        /// <summary>Dunkles Fenster-Design (Rahmenfarbe wirkt ab Windows 11) und Neuberechnung des Rahmens.</summary>
        public static void Apply(Form f)
        {
            try
            {
                int on = 1, border = Bgr(Color.FromArgb(58, 64, 88)), cap = Bgr(TitleBar.BarColor);
                DwmSetWindowAttribute(f.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, 4);
                DwmSetWindowAttribute(f.Handle, DWMWA_BORDER_COLOR, ref border, 4);
                DwmSetWindowAttribute(f.Handle, DWMWA_CAPTION_COLOR, ref cap, 4);
            }
            catch { }
            SetWindowPos(f.Handle, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
        }

        /// <summary>Dicke des Größenrahmens (für maximierte Fenster, die über den Bildschirmrand hinausragen).</summary>
        public static int FrameThickness()
        {
            return GetSystemMetrics(33) + GetSystemMetrics(92);   // SM_CYFRAME + SM_CXPADDEDBORDER
        }
    }
}
