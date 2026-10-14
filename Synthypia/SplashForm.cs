using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace Synthypia
{
    /// <summary>
    /// Startbildschirm, der in einem eigenen Thread mit eigener Nachrichtenschleife läuft,
    /// damit die Fortschrittsanzeige auch dann weiterläuft, wenn der UI-Thread ein Plugin lädt.
    /// </summary>
    static class Splash
    {
        static SplashForm form;
        static Thread thread;
        static readonly ManualResetEvent ready = new ManualResetEvent(false);

        public static void Open()
        {
            if (thread != null) return;
            thread = new Thread(() =>
            {
                form = new SplashForm();
                form.Shown += (s, e) => ready.Set();
                Application.Run(form);
            }) { IsBackground = true, Name = "Synthypia-Splash" };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            ready.WaitOne(3000);
        }

        public static void SetStatus(string text)
        {
            var f = form;
            if (f == null || f.IsDisposed || !f.IsHandleCreated) return;
            try { f.BeginInvoke((Action)(() => f.SetStatus(text))); } catch { }
        }

        public static void Close()
        {
            var f = form;
            form = null;
            if (f == null || f.IsDisposed || !f.IsHandleCreated) return;
            try { f.BeginInvoke((Action)f.Close); } catch { }
        }
    }

    sealed class SplashForm : Form
    {
        readonly Label status;

        public SplashForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = true;
            Text = "Synthypia";
            BackColor = Color.FromArgb(11, 13, 20);
            ClientSize = new Size(440, 170);
            DoubleBuffered = true;

            var title = new Label
            {
                Text = "Synthypia",
                Font = new Font("Segoe UI", 24F, FontStyle.Bold),
                ForeColor = Color.FromArgb(226, 230, 240),
                AutoSize = true,
                Location = new Point(28, 24)
            };
            status = new Label
            {
                Text = "Wird gestartet …",
                Font = new Font("Segoe UI", 10F),
                ForeColor = Color.FromArgb(150, 158, 180),
                AutoSize = false,
                Size = new Size(384, 22),
                Location = new Point(30, 92)
            };
            var bar = new ProgressBar
            {
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 30,
                Size = new Size(384, 6),
                Location = new Point(30, 124)
            };
            Controls.Add(title);
            Controls.Add(status);
            Controls.Add(bar);
        }

        public void SetStatus(string text)
        {
            if (status.Text != text) status.Text = text;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(Color.FromArgb(52, 58, 80)))
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            using (var br = new SolidBrush(Color.FromArgb(120, 190, 255)))
                e.Graphics.FillRectangle(br, 0, 0, 5, Height);
        }
    }
}
