using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Synthypia
{
    internal static class Program
    {
        /// <summary>
        /// Der Haupteinstiegspunkt für die Anwendung.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Splash.Open();                       // Startbildschirm, solange das Hauptfenster lädt
            Form1 form;
            try
            {
                form = new Form1();              // lädt Lied, Plugin usw. – das Fenster ist noch unsichtbar
            }
            catch
            {
                Splash.Close();
                throw;
            }
            form.Shown += (s, e) =>
            {
                // Zuerst nach vorn holen, solange der Startbildschirm den Vordergrund noch hält; erst dann schließen
                form.WindowState = form.WindowState == FormWindowState.Minimized ? FormWindowState.Normal : form.WindowState;
                form.TopMost = true;
                form.TopMost = false;
                form.Activate();
                form.BringToFront();
                Splash.Close();
            };
            Application.Run(form);
        }
    }
}
