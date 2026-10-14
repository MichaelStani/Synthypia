using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Synthypia
{
    /// <summary>
    /// Schmale, scrollende Notenschrift (Klaviersystem). Bewusst dezent gehalten:
    /// gedämpfte Farben, kleine Höhe, nur die nächsten Sekunden.
    /// </summary>
    public sealed class SheetView : Control
    {
        static readonly Color ColR = Color.FromArgb(74, 222, 128);
        static readonly Color ColL = Color.FromArgb(56, 189, 248);
        static readonly Color ColB = Color.FromArgb(139, 147, 167);
        static readonly Color Bg = Color.FromArgb(17, 20, 31);

        // diatonische Position der Stammtöne innerhalb einer Oktave (Kreuz-Schreibweise)
        static readonly int[] Step = { 0, 0, 1, 1, 2, 3, 3, 4, 4, 5, 5, 6 };
        static readonly bool[] Sharp = { false, true, false, true, false, false, true, false, true, false, true, false };

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Player Player { get; set; }

        /// <summary>Sichtbarer Zeitraum in Sekunden.</summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double WindowSeconds { get; set; }

        public SheetView()
        {
            WindowSeconds = 6.0;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            BackColor = Bg;
        }

        static Color HandColor(char hand)
        {
            return hand == 'R' ? ColR : hand == 'L' ? ColL : ColB;
        }

        static readonly int[] FlatStep = { 0, 1, 1, 2, 2, 3, 4, 4, 5, 5, 6, 6 };
        static readonly int[] SharpAccOf = { 0, 1, 0, 1, 0, 0, 1, 0, 1, 0, 1, 0 };
        static readonly int[] FlatAccOf = { 0, -1, 0, -1, 0, 0, -1, 0, -1, 0, -1, 0 };
        static readonly int[] SharpOrder = { 3, 0, 4, 1, 5, 2, 6 };   // F C G D A E B
        static readonly int[] FlatOrder = { 6, 2, 5, 1, 4, 0, 3 };    // B E A D G C F

        // diatonische Position der Vorzeichen im Violin- bzw. Bassschlüssel
        static readonly int[] SharpTreble = { 38, 35, 39, 36, 33, 37, 34 };
        static readonly int[] SharpBass = { 24, 21, 25, 22, 19, 23, 20 };
        static readonly int[] FlatTreble = { 34, 37, 33, 36, 32, 35, 31 };
        static readonly int[] FlatBass = { 20, 23, 19, 22, 18, 21, 17 };

        static readonly int[][] KeyAccTable = BuildKeyAcc();

        static int[][] BuildKeyAcc()
        {
            var t = new int[15][];
            for (int sf = -7; sf <= 7; sf++)
            {
                var a = new int[7];
                if (sf > 0) for (int i = 0; i < sf; i++) a[SharpOrder[i]] = 1;
                else for (int i = 0; i < -sf; i++) a[FlatOrder[i]] = -1;
                t[sf + 7] = a;
            }
            return t;
        }

        /// <summary>
        /// Notenposition (diatonischer Index) und anzuzeigendes Vorzeichen für eine MIDI-Note in der
        /// gegebenen Tonart. sign: 0 keins, 1 Kreuz, -1 B, 2 Auflösungszeichen.
        /// </summary>
        static int Spell(int midi, int sf, out int sign)
        {
            int pc = midi % 12;
            bool flat = sf < 0;
            int step = flat ? FlatStep[pc] : Step[pc];
            int acc = flat ? FlatAccOf[pc] : SharpAccOf[pc];
            int keyAcc = KeyAccTable[sf + 7][step];
            sign = acc == keyAcc ? 0 : acc == 1 ? 1 : acc == -1 ? -1 : 2;
            return (midi / 12 - 1) * 7 + step;
        }

        static string SignGlyph(int sign)
        {
            return sign == 1 ? "♯" : sign == -1 ? "♭" : "♮";
        }

        double BeatLength(Song song, double t)
        {
            var b = song.Beats;
            if (b.Count < 2) return 0.5;
            int lo = 0, hi = b.Count - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (b[mid].Time <= t) lo = mid; else hi = mid - 1;
            }
            if (lo >= b.Count - 1) lo = b.Count - 2;
            double d = b[lo + 1].Time - b[lo].Time;
            return d > 0.05 ? d : 0.5;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Bg);
            int W = ClientSize.Width, H = ClientSize.Height;
            if (W < 80 || H < 60 || Player == null || Player.Song == null) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

            float s = H / 16f;                       // Linienabstand
            float yTB = 3f * s + 4f * s;             // y der unteren Violinlinie (E4, Index 30)
            Func<int, float> Y = idx => yTB - (idx - 30) * s / 2f;

            int sf = Player.Song.KeySigAt(Player.Position);
            sf = Math.Max(-7, Math.Min(7, sf));
            int sigCount = Math.Abs(sf);
            float sigX0 = s * 3.9f;
            float leftW = sigX0 + sigCount * s * 1.0f + s * 1.3f;
            float playX = leftW + (W - leftW) * 0.12f;
            double pos = Player.Position;
            double past = WindowSeconds * 0.12, future = WindowSeconds - past;
            float pxPerSec = (float)((W - leftW) / WindowSeconds);
            Func<double, float> X = t => playX + (float)((t - pos) * pxPerSec);

            using (var linePen = new Pen(Color.FromArgb(95, 104, 130), 1f))
            {
                // Notenlinien
                for (int i = 0; i < 5; i++)
                {
                    float yt = Y(38 - 2 * i), yb = Y(26 - 2 * i);
                    g.DrawLine(linePen, 0, yt, W, yt);
                    g.DrawLine(linePen, 0, yb, W, yb);
                }

                g.SetClip(new RectangleF(leftW, 0, W - leftW, H));

                // Taktstriche
                using (var barPen = new Pen(Color.FromArgb(70, 95, 104, 130), 1f))
                {
                    foreach (var b in Player.Song.Beats)
                    {
                        if (b.Time < pos - past - 0.2) continue;
                        if (b.Time > pos + future + 0.2) break;
                        if (!b.Bar) continue;
                        float x = X(b.Time);
                        g.DrawLine(barPen, x, Y(38), x, Y(18));
                    }
                }

                // Abspielposition
                using (var headPen = new Pen(Color.FromArgb(150, 251, 191, 36), 1.5f))
                    g.DrawLine(headPen, playX, Y(40), playX, Y(16));

                DrawNotes(g, pos, past, future, s, X, Y, linePen);
                g.ResetClip();
            }

            // Schlüssel (liegen über den Noten, daher nach dem Clipping)
            using (var cover = new SolidBrush(Bg))
                g.FillRectangle(cover, 0, 0, leftW - 2, H);
            using (var linePen = new Pen(Color.FromArgb(95, 104, 130), 1f))
            {
                for (int i = 0; i < 5; i++)
                {
                    float yt = Y(38 - 2 * i), yb = Y(26 - 2 * i);
                    g.DrawLine(linePen, 0, yt, leftW - 2, yt);
                    g.DrawLine(linePen, 0, yb, leftW - 2, yb);
                }
                g.DrawLine(linePen, leftW - 2, Y(38), leftW - 2, Y(18));
            }
            using (var clefFont = new Font("Segoe UI Symbol", s * 5.2f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var clefBrush = new SolidBrush(Color.FromArgb(190, 200, 220)))
            {
                g.DrawString("\U0001D11E", clefFont, clefBrush, 2f, Y(38) - s * 2.3f);
                using (var bassFont = new Font("Segoe UI Symbol", s * 3.6f, FontStyle.Regular, GraphicsUnit.Pixel))
                    g.DrawString("\U0001D122", bassFont, clefBrush, 3f, Y(26) - s * 1.0f);

                // Tonart-Vorzeichen
                if (sigCount > 0)
                {
                    int[] tr = sf > 0 ? SharpTreble : FlatTreble;
                    int[] bs = sf > 0 ? SharpBass : FlatBass;
                    string glyph = SignGlyph(sf > 0 ? 1 : -1);
                    using (var sigFont = new Font("Segoe UI Symbol", s * 1.9f, FontStyle.Regular, GraphicsUnit.Pixel))
                    using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        float dy = sf > 0 ? 0f : -s * 0.35f;
                        for (int i = 0; i < sigCount; i++)
                        {
                            float x = sigX0 + (i + 0.5f) * s * 1.0f;
                            g.DrawString(glyph, sigFont, clefBrush, x, Y(tr[i]) + dy, fmt);
                            g.DrawString(glyph, sigFont, clefBrush, x, Y(bs[i]) + dy, fmt);
                        }
                    }
                }
            }
        }

        void DrawNotes(Graphics g, double pos, double past, double future, float s,
                       Func<double, float> X, Func<int, float> Y, Pen linePen)
        {
            var song = Player.Song;
            var notes = Player.Notes;
            int i = Player.FirstVisibleIndex(pos - past);
            float headW = s * 1.3f, headH = s * 0.95f;

            using (var accFont = new Font("Segoe UI Symbol", s * 1.7f, FontStyle.Regular, GraphicsUnit.Pixel))
            {
                for (; i < notes.Count; i++)
                {
                    var n = notes[i];
                    if (n.Start > pos + future) break;
                    if (n.Drum || n.End < pos - past) continue;

                    int sign;
                    int idx = Spell(n.Midi, Math.Max(-7, Math.Min(7, song.KeySigAt(n.Start))), out sign);
                    float x = X(n.Start);
                    float y = Y(idx);

                    bool active = n.Start <= pos && n.End > pos;
                    bool passed = n.End <= pos;
                    int alpha = passed ? 70 : active ? 255 : 175;
                    Color c = HandColor(n.Hand);
                    if (n.State == 2) c = Color.FromArgb(120, 120, 130);
                    Color col = Color.FromArgb(alpha, c);
                    if (active) col = Color.FromArgb(255, ControlPaint.Light(c, 0.3f));

                    // Hilfslinien
                    using (var lp = new Pen(Color.FromArgb(Math.Min(alpha, 140), 150, 158, 180), 1f))
                    {
                        float lx0 = x - headW * 0.9f, lx1 = x + headW * 0.9f;
                        if (idx <= 16) { for (int l = 16; l >= idx; l -= 2) g.DrawLine(lp, lx0, Y(l), lx1, Y(l)); }
                        else if (idx >= 40) { for (int l = 40; l <= idx; l += 2) g.DrawLine(lp, lx0, Y(l), lx1, Y(l)); }
                        else if (idx == 28) g.DrawLine(lp, lx0, Y(28), lx1, Y(28));
                    }

                    // Vorzeichen
                    if (sign != 0)
                        using (var ab = new SolidBrush(Color.FromArgb(alpha, 200, 208, 225)))
                            g.DrawString(SignGlyph(sign), accFont, ab, x - headW * 1.75f, y - s * (sign == -1 ? 1.35f : 1.05f));

                    // Notenwert grob aus der Dauer ableiten
                    double beats = (n.End - n.Start) / BeatLength(song, n.Start);
                    bool hollow = beats >= 1.5;
                    bool stem = beats < 3.5;

                    var rect = new RectangleF(x - headW / 2, y - headH / 2, headW, headH);
                    var state = g.Save();
                    g.TranslateTransform(x, y);
                    g.RotateTransform(-20f);
                    g.TranslateTransform(-x, -y);
                    if (hollow)
                        using (var pen = new Pen(col, Math.Max(1.2f, s * 0.16f)))
                            g.DrawEllipse(pen, rect);
                    else
                        using (var br = new SolidBrush(col))
                            g.FillEllipse(br, rect);
                    g.Restore(state);

                    if (stem)
                    {
                        int mid = n.Midi >= 60 ? 34 : 22;
                        bool down = idx >= mid;
                        float sx = down ? x - headW / 2 + 0.5f : x + headW / 2 - 0.5f;
                        float sy2 = down ? y + s * 3.3f : y - s * 3.3f;
                        using (var pen = new Pen(col, 1.2f))
                            g.DrawLine(pen, sx, y, sx, sy2);
                    }
                }
            }
        }
    }
}
