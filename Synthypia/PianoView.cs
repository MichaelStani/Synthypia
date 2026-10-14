using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Synthypia
{
    /// <summary>Fallende Noten über einer Klaviatur (Synthesia-Stil).</summary>
    public sealed class PianoView : Control
    {
        static readonly string[] Names = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Player Player { get; set; }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double FallSeconds { get; set; }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ShowNames { get; set; }

        public event Action<int> KeyPressed;
        public event Action<int> KeyReleased;

        readonly float[] kx = new float[128];
        readonly float[] kw = new float[128];
        int mouseKey = -1;
        int keyboardTop, keyboardHeight;

        static readonly Color ColR = Color.FromArgb(74, 222, 128);
        static readonly Color ColL = Color.FromArgb(56, 189, 248);
        static readonly Color ColB = Color.FromArgb(139, 147, 167);

        public PianoView()
        {
            FallSeconds = 4.0;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop = true;
            BackColor = Color.FromArgb(11, 13, 20);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            return true;
        }

        public static string NoteName(int midi)
        {
            return Names[midi % 12] + (midi / 12 - 1);
        }

        static Color HandColor(char hand)
        {
            return hand == 'R' ? ColR : hand == 'L' ? ColL : ColB;
        }

        static Color Scale(Color c, double f)
        {
            return Color.FromArgb(c.A, (int)(c.R * f), (int)(c.G * f), (int)(c.B * f));
        }

        void LayoutKeys(int lo, int hi)
        {
            int whites = 0;
            for (int p = lo; p <= hi; p++) if (!Player.IsBlack(p)) whites++;
            float ww = (float)ClientSize.Width / Math.Max(1, whites);
            float bw = ww * 0.6f;
            int wi = 0;
            for (int p = lo; p <= hi; p++)
            {
                if (Player.IsBlack(p)) { kx[p] = wi * ww - bw / 2f; kw[p] = bw; }
                else { kx[p] = wi * ww; kw[p] = ww; wi++; }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int W = ClientSize.Width, H = ClientSize.Height;
            if (W < 10 || H < 10 || Player == null) return;

            keyboardHeight = Math.Max(80, Math.Min(170, (int)(H * 0.2)));
            keyboardTop = H - keyboardHeight;
            int top = keyboardTop;
            int lo = Player.Lo, hi = Player.Hi;
            LayoutKeys(lo, hi);

            double pos = Player.Position;
            double fall = FallSeconds;
            float pps = (float)(top / fall);

            using (var bg = new LinearGradientBrush(new Rectangle(0, 0, W, Math.Max(1, top)), Color.FromArgb(10, 12, 20), Color.FromArgb(22, 28, 46), 90f))
                g.FillRectangle(bg, 0, 0, W, top);

            // Oktav-Hilfslinien
            using (var br = new SolidBrush(Color.FromArgb(18, 255, 255, 255)))
                for (int p = lo; p <= hi; p++)
                    if (p % 12 == 0) g.FillRectangle(br, kx[p], 0, 1, top);

            var song = Player.Song;
            if (song != null)
            {
                // Taktlinien
                using (var barPen = new Pen(Color.FromArgb(70, 255, 255, 255)))
                using (var beatPen = new Pen(Color.FromArgb(24, 255, 255, 255)))
                {
                    var beats = song.Beats;
                    int bl = 0, bh = beats.Count;
                    while (bl < bh) { int m = (bl + bh) / 2; if (beats[m].Time < pos) bl = m + 1; else bh = m; }
                    for (int i = bl; i < beats.Count && beats[i].Time <= pos + fall; i++)
                    {
                        float y = top - (float)((beats[i].Time - pos) * pps);
                        g.DrawLine(beats[i].Bar ? barPen : beatPen, 0, y, W, y);
                    }
                }

                // Loop-Marken
                DrawLoopMark(g, Player.LoopA, "A", pos, pps, top, W);
                DrawLoopMark(g, Player.LoopB, "B", pos, pps, top, W);

                DrawNotes(g, pos, fall, pps, top);
            }

            DrawKeyboard(g, lo, hi, top, W);
            DrawHud(g, W, top, pos);
        }

        void DrawLoopMark(Graphics g, double t, string label, double pos, float pps, int top, int W)
        {
            if (t < 0 || t < pos || t > pos + FallSeconds) return;
            float y = top - (float)((t - pos) * pps);
            using (var pen = new Pen(Color.FromArgb(220, 251, 191, 36), 2f) { DashStyle = DashStyle.Dash })
                g.DrawLine(pen, 0, y, W, y);
            using (var br = new SolidBrush(Color.FromArgb(251, 191, 36)))
                g.DrawString(label, Font, br, 4, y - 16);
        }

        void DrawNotes(Graphics g, double pos, double fall, float pps, int top)
        {
            var notes = Player.Notes;
            int count = notes.Count;
            int i = Player.FirstVisibleIndex(pos);
            using (var path = new GraphicsPath())
            using (var small = new Font("Segoe UI", 8f))
            using (var textBr = new SolidBrush(Color.FromArgb(230, 10, 12, 20)))
            {
                for (; i < count; i++)
                {
                    var n = notes[i];
                    if (n.Start > pos + fall) break;
                    if (n.Drum || n.End < pos) continue;
                    if (n.Midi < Player.Lo || n.Midi > Player.Hi) continue;

                    float yb = Math.Min(top, top - (float)((n.Start - pos) * pps));
                    float yt = top - (float)((n.End - pos) * pps);
                    float h = yb - yt;
                    if (h < 3) { yt = yb - 3; h = 3; }
                    float x = kx[n.Midi] + 1.5f, w = kw[n.Midi] - 3f;
                    if (w < 2) continue;

                    Color c = HandColor(n.Hand);
                    if (Player.IsBlack(n.Midi)) c = Scale(c, 0.72);
                    if (n.State == 2) c = Color.FromArgb(120, 90, 90, 100);
                    bool active = n.Start <= pos && n.End > pos;

                    var r = new RectangleF(x, yt, w, h);
                    float rad = Math.Min(5f, Math.Min(w, h) / 2f);
                    path.Reset();
                    path.AddArc(r.X, r.Y, rad * 2, rad * 2, 180, 90);
                    path.AddArc(r.Right - rad * 2, r.Y, rad * 2, rad * 2, 270, 90);
                    path.AddArc(r.Right - rad * 2, r.Bottom - rad * 2, rad * 2, rad * 2, 0, 90);
                    path.AddArc(r.X, r.Bottom - rad * 2, rad * 2, rad * 2, 90, 90);
                    path.CloseFigure();

                    using (var br = new SolidBrush(active ? ControlPaint.Light(c, 0.35f) : c))
                        g.FillPath(br, path);
                    using (var pen = new Pen(n.State == 1 ? Color.White : Scale(c, 0.55), n.State == 1 ? 1.6f : 1f))
                        g.DrawPath(pen, path);

                    if (ShowNames && h > 16 && w > 14)
                        g.DrawString(NoteName(n.Midi), small, textBr, x + 2, yb - 15);
                }
            }
        }

        void DrawKeyboard(Graphics g, int lo, int hi, int top, int W)
        {
            int kh = keyboardHeight;
            var lit = new Color[128];
            var isLit = new bool[128];
            var notes = Player.Notes;
            double pos = Player.Position;
            if (Player.Song != null)
            {
                int i = Player.FirstVisibleIndex(pos);
                for (; i < notes.Count && notes[i].Start <= pos; i++)
                {
                    var n = notes[i];
                    if (n.Drum || n.End <= pos) continue;
                    isLit[n.Midi] = true;
                    lit[n.Midi] = HandColor(n.Hand);
                }
            }
            for (int p = 0; p < 128; p++)
                if (Player.IsUserDown(p) && !isLit[p]) { isLit[p] = true; lit[p] = Color.FromArgb(251, 191, 36); }

            using (var linePen = new Pen(Color.FromArgb(120, 255, 255, 255), 2f))
                g.DrawLine(linePen, 0, top - 1, W, top - 1);

            using (var outline = new Pen(Color.FromArgb(70, 70, 80)))
            using (var small = new Font("Segoe UI", 7.5f))
            using (var gray = new SolidBrush(Color.FromArgb(130, 130, 140)))
            {
                for (int p = lo; p <= hi; p++)
                {
                    if (Player.IsBlack(p)) continue;
                    var r = new RectangleF(kx[p], top, kw[p], kh);
                    Color fill = isLit[p] ? lit[p] : Color.FromArgb(244, 245, 250);
                    using (var br = new SolidBrush(fill)) g.FillRectangle(br, r);
                    g.DrawRectangle(outline, r.X, r.Y, r.Width, r.Height);
                    if (p % 12 == 0 && kw[p] > 14)
                        g.DrawString(NoteName(p), small, gray, r.X + 2, r.Bottom - 16);
                }
                for (int p = lo; p <= hi; p++)
                {
                    if (!Player.IsBlack(p)) continue;
                    var r = new RectangleF(kx[p], top, kw[p], kh * 0.62f);
                    Color fill = isLit[p] ? Scale(lit[p], 0.8) : Color.FromArgb(22, 23, 30);
                    using (var br = new SolidBrush(fill)) g.FillRectangle(br, r);
                    g.DrawRectangle(outline, r.X, r.Y, r.Width, r.Height);
                }
            }
        }

        void DrawHud(Graphics g, int W, int top, double pos)
        {
            using (var f = new Font("Segoe UI", 10f, FontStyle.Bold))
            using (var big = new Font("Segoe UI", 54f, FontStyle.Bold))
            using (var white = new SolidBrush(Color.FromArgb(235, 238, 245)))
            using (var dim = new SolidBrush(Color.FromArgb(160, 168, 185)))
            {
                string line = null;
                if (Player.Mode == PlayMode.Wait)
                    line = Player.Waiting ? "Warte auf deine Tasten …" : "Modus: Warten";
                else if (Player.Mode == PlayMode.Score)
                    line = string.Format("Treffer {0}   Verpasst {1}   Falsch {2}   Serie {3} (Best {4})   Genauigkeit {5:0}%",
                        Player.Hits, Player.Misses, Player.Wrong, Player.Streak, Player.BestStreak, Player.Accuracy);
                if (line != null) g.DrawString(line, f, Player.Waiting ? white : dim, 12, 10);

                if (Player.Song == null)
                {
                    g.DrawString("MIDI-Datei öffnen oder hierher ziehen", f, white, 12, 10);
                }
                else if (pos < 0 && Player.Playing)
                {
                    string s = ((int)Math.Ceiling(-pos)).ToString();
                    var sz = g.MeasureString(s, big);
                    g.DrawString(s, big, white, (W - sz.Width) / 2, top / 3f);
                }
            }
        }

        // ---------- Maus ----------
        int KeyAt(int x, int y)
        {
            if (Player == null || y < keyboardTop) return -1;
            for (int p = Player.Lo; p <= Player.Hi; p++)
                if (Player.IsBlack(p) && y < keyboardTop + keyboardHeight * 0.62f && x >= kx[p] && x < kx[p] + kw[p]) return p;
            for (int p = Player.Lo; p <= Player.Hi; p++)
                if (!Player.IsBlack(p) && x >= kx[p] && x < kx[p] + kw[p]) return p;
            return -1;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            int k = KeyAt(e.X, e.Y);
            if (k >= 0 && e.Button == MouseButtons.Left)
            {
                mouseKey = k;
                Capture = true;
                if (KeyPressed != null) KeyPressed(k);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (mouseKey < 0) return;
            int k = KeyAt(e.X, e.Y);
            if (k >= 0 && k != mouseKey)
            {
                if (KeyReleased != null) KeyReleased(mouseKey);
                mouseKey = k;
                if (KeyPressed != null) KeyPressed(k);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (mouseKey >= 0)
            {
                if (KeyReleased != null) KeyReleased(mouseKey);
                mouseKey = -1;
                Capture = false;
            }
        }
    }
}
