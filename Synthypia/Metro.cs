using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Synthypia
{
    /// <summary>Farben und Schriften des flachen, modernen Designs (Windows-11-Stil, dunkel).</summary>
    static class Theme
    {
        public static readonly Color Panel = Color.FromArgb(32, 36, 50);
        public static readonly Color Control = Color.FromArgb(44, 49, 68);
        public static readonly Color ControlHover = Color.FromArgb(58, 64, 88);
        public static readonly Color ControlDown = Color.FromArgb(72, 79, 106);
        public static readonly Color ControlDisabled = Color.FromArgb(36, 40, 56);
        public static readonly Color Text = Color.FromArgb(234, 237, 245);
        public static readonly Color Muted = Color.FromArgb(150, 158, 180);
        public static readonly Color Accent = Color.FromArgb(0, 120, 212);
        public static readonly Color AccentHover = Color.FromArgb(28, 140, 230);
        public static readonly Color AccentDown = Color.FromArgb(0, 96, 172);

        static string iconFamily;
        static Font uiFont;

        /// <summary>Oberflächenschrift: Segoe UI Semibold (auf dunklem Grund wirkt normale Schrift dünner), sonst Segoe UI.</summary>
        public static Font UiFont
        {
            get
            {
                if (uiFont == null)
                {
                    string family = "Segoe UI";
                    using (var fc = new InstalledFontCollection())
                        if (fc.Families.Any(f => f.Name == "Segoe UI Semibold")) family = "Segoe UI Semibold";
                    uiFont = new Font(family, 9.5f, FontStyle.Regular, GraphicsUnit.Point);
                }
                return uiFont;
            }
        }

        /// <summary>Setzt die Oberflächenschrift auf Buttons, Listen, Schalter und normale Beschriftungen (Abschnittsüberschriften bleiben fett).</summary>
        public static void ApplyFonts(Control root)
        {
            foreach (Control c in root.Controls)
            {
                if (c is MetroButton || c is MetroComboBox || c is MetroCheckBox || (c is Label && !c.Font.Bold)) c.Font = UiFont;
                if (c.HasChildren) ApplyFonts(c);
            }
        }

        /// <summary>Symbolschrift: Segoe Fluent Icons (Windows 11) oder Segoe MDL2 Assets (Windows 10).</summary>
        public static Font Icon(float size)
        {
            if (iconFamily == null)
            {
                using (var fc = new InstalledFontCollection())
                    iconFamily = fc.Families.Any(f => f.Name == "Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";
            }
            return new Font(iconFamily, size, GraphicsUnit.Point);
        }

        public static GraphicsPath Round(RectangleF r, float radius)
        {
            float d = radius * 2;
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr hwnd, string subApp, string subIdList);

        /// <summary>Dunkles Windows-Theme (Bildlaufleisten, Listen) für ein natives Fenster.</summary>
        public static void DarkWindow(IntPtr h)
        {
            try { SetWindowTheme(h, "DarkMode_Explorer", null); } catch { }
        }

        public static void DarkScrollBars(Control c)
        {
            EventHandler apply = (s, e) => DarkWindow(c.Handle);
            if (c.IsHandleCreated) apply(c, EventArgs.Empty);
            c.HandleCreated += apply;
        }
    }

    /// <summary>Flacher Button mit Hover-/Klickzustand, optionalem Symbol und Akzentfarbe.</summary>
    sealed class MetroButton : Button
    {
        bool hover, down;

        /// <summary>Zeichen der Symbolschrift (z. B. ""), wird links vom Text gezeichnet.</summary>
        [DefaultValue("")] public string IconGlyph { get; set; }
        /// <summary>Hervorgehobene Schaltfläche in der Akzentfarbe.</summary>
        [DefaultValue(false)] public bool Accent { get; set; }

        public MetroButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            IconGlyph = "";
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { down = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            Color bg, fg;
            if (!Enabled) { bg = Theme.ControlDisabled; fg = Color.FromArgb(100, 106, 128); }
            else if (Accent) { bg = down ? Theme.AccentDown : hover ? Theme.AccentHover : Theme.Accent; fg = Color.White; }
            else { bg = down ? Theme.ControlDown : hover ? Theme.ControlHover : Theme.Control; fg = Theme.Text; }

            g.Clear(Parent != null ? Parent.BackColor : Theme.Panel);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Theme.Round(r, 4))
            using (var br = new SolidBrush(bg))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillPath(br, path);
                if (!Accent && Enabled)
                    using (var pen = new Pen(Color.FromArgb(hover ? 110 : 60, 255, 255, 255), 1f) { Alignment = PenAlignment.Inset })
                        g.DrawPath(pen, path);
            }

            string glyph = IconGlyph ?? "";
            bool hasGlyph = glyph.Length > 0, hasText = !string.IsNullOrEmpty(Text);
            var flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.Left | TextFormatFlags.VerticalCenter;
            using (var icon = Theme.Icon(Font.Size + 1.5f))
            {
                Size gs = hasGlyph ? TextRenderer.MeasureText(g, glyph, icon, new Size(1000, 100), flags) : Size.Empty;
                Size ts = hasText ? TextRenderer.MeasureText(g, Text, Font, new Size(1000, 100), flags) : Size.Empty;
                int gap = hasGlyph && hasText ? 8 : 0;
                int x = (Width - (gs.Width + gap + ts.Width)) / 2;
                if (hasGlyph)
                {
                    TextRenderer.DrawText(g, glyph, icon, new Rectangle(x, 0, gs.Width + 2, Height), fg, flags);
                    x += gs.Width + gap;
                }
                if (hasText) TextRenderer.DrawText(g, Text, Font, new Rectangle(x, 0, ts.Width + 4, Height), fg, flags);
            }
        }
    }

    /// <summary>Kippschalter statt Kontrollkästchen.</summary>
    sealed class MetroCheckBox : CheckBox
    {
        bool hover;

        public MetroCheckBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnCheckedChanged(EventArgs e) { Invalidate(); base.OnCheckedChanged(e); }

        public override Size GetPreferredSize(Size proposedSize)
        {
            Size ts = TextRenderer.MeasureText(Text ?? "", Font, new Size(1000, 100), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            return new Size(48 + ts.Width + 4, Math.Max(26, ts.Height + 8));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Panel);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            const int tw = 38, th = 18;
            var track = new RectangleF(1, (Height - th) / 2f, tw, th);
            using (var path = Theme.Round(track, th / 2f))
            {
                if (Checked)
                {
                    using (var br = new SolidBrush(hover ? Theme.AccentHover : Theme.Accent)) g.FillPath(br, path);
                }
                else
                {
                    using (var br = new SolidBrush(hover ? Theme.ControlHover : Theme.Control)) g.FillPath(br, path);
                    using (var pen = new Pen(Theme.Muted, 1.2f) { Alignment = PenAlignment.Inset }) g.DrawPath(pen, path);
                }
            }
            float kd = Checked ? 12 : 10;
            float kx = Checked ? track.Right - kd - 4 : track.X + 4;
            using (var br = new SolidBrush(Checked ? Color.White : Theme.Muted))
                g.FillEllipse(br, kx, track.Y + (th - kd) / 2f, kd, kd);

            TextRenderer.DrawText(g, Text, Font, new Rectangle(48, 0, Width - 48, Height), Enabled ? Theme.Text : Theme.Muted,
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }
    }

    /// <summary>Flache Auswahlliste mit Chevron und dunkler Dropdown-Liste.</summary>
    sealed class MetroComboBox : ComboBox
    {
        bool hover;

        [StructLayout(LayoutKind.Sequential)]
        struct COMBOBOXINFO { public int cbSize; public Rectangle rcItem, rcButton; public int stateButton; public IntPtr hwndCombo, hwndItem, hwndList; }
        [DllImport("user32.dll")] static extern bool GetComboBoxInfo(IntPtr hwnd, ref COMBOBOXINFO info);

        public MetroComboBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            DropDownStyle = ComboBoxStyle.DropDownList;
            DrawMode = DrawMode.OwnerDrawFixed;
            ItemHeight = 19;   // ergibt eine Ausklappbox von ca. 26 px Höhe, damit die Seitenleiste ohne Scrollbalken passt
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnSelectedIndexChanged(EventArgs e) { Invalidate(); base.OnSelectedIndexChanged(e); }
        protected override void OnDropDownClosed(EventArgs e) { Invalidate(); base.OnDropDownClosed(e); }

        protected override void OnDropDown(EventArgs e)
        {
            base.OnDropDown(e);
            Invalidate();
            var info = new COMBOBOXINFO { cbSize = Marshal.SizeOf(typeof(COMBOBOXINFO)) };
            if (GetComboBoxInfo(Handle, ref info) && info.hwndList != IntPtr.Zero) Theme.DarkWindow(info.hwndList);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Panel);
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color bg = !Enabled ? Theme.ControlDisabled : DroppedDown ? Theme.ControlDown : hover ? Theme.ControlHover : Theme.Control;
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Theme.Round(r, 4))
            {
                using (var br = new SolidBrush(bg)) g.FillPath(br, path);
                Color bc = DroppedDown || (Focused && ShowFocusCues) ? Theme.Accent : Color.FromArgb(hover ? 110 : 60, 255, 255, 255);
                using (var pen = new Pen(bc, 1f) { Alignment = PenAlignment.Inset }) g.DrawPath(pen, path);
            }

            string text = SelectedItem == null ? "" : GetItemText(SelectedItem);
            var flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
            TextRenderer.DrawText(g, text, Font, new Rectangle(10, 0, Width - 38, Height), Enabled ? Theme.Text : Theme.Muted, flags);

            using (var icon = Theme.Icon(8f))
                TextRenderer.DrawText(g, "", icon, new Rectangle(Width - 28, 0, 20, Height), Enabled ? Theme.Muted : Color.FromArgb(100, 106, 128),
                    TextFormatFlags.NoPadding | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool hot = (e.State & DrawItemState.Selected) != 0 && (e.State & DrawItemState.ComboBoxEdit) == 0;
            using (var br = new SolidBrush(hot ? Theme.Accent : Color.FromArgb(38, 42, 58))) e.Graphics.FillRectangle(br, e.Bounds);
            TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font, new Rectangle(e.Bounds.X + 10, e.Bounds.Y, e.Bounds.Width - 12, e.Bounds.Height),
                hot ? Color.White : Theme.Text,
                TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>Flacher Schieberegler (Ersatz für TrackBar mit gleicher Schnittstelle: Value, Minimum, Maximum, Scroll).</summary>
    [DefaultEvent("Scroll")]
    sealed class MetroSlider : Control, ISupportInitialize
    {
        int min, max = 10, val;
        bool hover, drag;

        public event EventHandler Scroll;

        /// <summary>Nur zur Kompatibilität mit der TrackBar vorhanden, ohne Wirkung.</summary>
        [DefaultValue(TickStyle.None)] public TickStyle TickStyle { get; set; }

        public MetroSlider()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            TabStop = false;
            Cursor = Cursors.Hand;
            Height = 28;
        }

        public void BeginInit() { }
        public void EndInit() { }

        [DefaultValue(0)]
        public int Minimum { get { return min; } set { min = value; if (max < min) max = min; val = Clamp(val); Invalidate(); } }
        [DefaultValue(10)]
        public int Maximum { get { return max; } set { max = Math.Max(value, min); val = Clamp(val); Invalidate(); } }
        [DefaultValue(0)]
        public int Value { get { return val; } set { int v = Clamp(value); if (v != val) { val = v; Invalidate(); } } }

        int Clamp(int v) { return Math.Max(min, Math.Min(max, v)); }

        const int Pad = 9;   // halber Daumen

        float Frac { get { return max == min ? 0f : (val - min) / (float)(max - min); } }

        void Raise()
        {
            var h = Scroll;
            if (h != null) h(this, EventArgs.Empty);
        }

        void SetFromX(int x)
        {
            double f = (x - Pad) / (double)Math.Max(1, Width - 2 * Pad);
            int v = Clamp(min + (int)Math.Round(f * (max - min)));
            if (v == val) return;
            val = v;
            Invalidate();
            Raise();
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { drag = true; Capture = true; SetFromX(e.X); Invalidate(); }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (drag) SetFromX(e.X);
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            drag = false;
            Capture = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            int step = Math.Max(1, (max - min) / 20);
            int v = Clamp(val + (e.Delta > 0 ? step : -step));
            if (v != val) { val = v; Invalidate(); Raise(); }
            base.OnMouseWheel(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Panel);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float cy = Height / 2f;
            float x0 = Pad, x1 = Width - Pad, tx = x0 + (x1 - x0) * Frac;
            using (var pen = new Pen(Color.FromArgb(70, 77, 104), 4f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawLine(pen, x0, cy, x1, cy);
            if (tx > x0)
                using (var pen = new Pen(drag || hover ? Theme.AccentHover : Theme.Accent, 4f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawLine(pen, x0, cy, tx, cy);

            float d = drag ? 14 : hover ? 18 : 16;
            using (var br = new SolidBrush(Color.White))
                g.FillEllipse(br, tx - d / 2, cy - d / 2, d, d);
            using (var pen = new Pen(Theme.Accent, 2f))
                g.DrawEllipse(pen, tx - d / 2 + 1, cy - d / 2 + 1, d - 2, d - 2);
        }
    }
}
