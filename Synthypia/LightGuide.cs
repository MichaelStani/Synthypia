using System;
using System.Threading;

namespace Synthypia
{
    /// <summary>
    /// Zeigt die zu spielenden Noten auf dem Light Guide eines Native-Instruments-Keyboards (Kontrol S-Serie MK3).
    /// Läuft in einem eigenen Thread und liest nur den Zustand des Players; das MIDI des Keyboards bleibt voll nutzbar.
    /// </summary>
    public sealed class LightGuide : IDisposable
    {
        // Farbbytes des NI-Agents: obere sechs Bit = Palettenindex, untere zwei Bit = Helligkeit
        // 0x44 lehnt der Agent samt der ganzen Nachricht ab ("Arguments cannot be parsed"), 0x7F ist Weiß
        const byte Green = 0x1C, Blue = 0x2C, White = 0x7F;

        /// <summary>So viele Sekunden (Songzeit) vor dem Einsatz leuchtet eine Taste schon.</summary>
        const double Ahead = 0.35;

        readonly Player player;
        readonly Thread thread;
        volatile bool quit;
        volatile string status = "Verbinde …";

        public LightGuide(Player player)
        {
            this.player = player;
            thread = new Thread(Run) { IsBackground = true, Name = "Synthypia-LightGuide" };
            thread.Start();
        }

        /// <summary>Kurzer Statustext für die Oberfläche.</summary>
        public string Status { get { return status; } }

        static readonly string LogFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Synthypia_LightGuide.log");

        static void Log(string msg)
        {
            try { System.IO.File.AppendAllText(LogFile, DateTime.Now.ToString("HH:mm:ss.fff") + "  " + msg + Environment.NewLine); }
            catch { }
        }

        void Run()
        {
            Log("--- Light Guide gestartet");
            OdrClient odr = null;
            int sent = 0, beats = 0;
            var lastBeat = DateTime.MinValue;
            var last = new byte[128];
            bool force = true;
            var lastSend = DateTime.MinValue;

            while (!quit)
            {
                try
                {
                    if (odr == null)
                    {
                        odr = new OdrClient();
                        odr.OnAgentError = e => { status = "Agent: " + e; Log("AGENT-FEHLER " + e); };
                        odr.Connect();
                        status = "Verbunden: " + odr.Product;
                        Log("verbunden: " + odr.Product + " " + odr.Serial + ", Agent " + odr.AgentVersion);
                        force = true;
                    }

                    var frame = Compute();
                    var now = DateTime.UtcNow;
                    if (beats < 20 && (now - lastBeat).TotalSeconds >= 3)
                    {
                        lastBeat = now;
                        beats++;
                        int pr = 0, all = 0;
                        var nl = player.Notes;
                        if (nl != null) foreach (var x in nl) { all++; if (x.Practice) pr++; }
                        Log("Zustand: pos=" + player.Position.ToString("0.0") + " spielt=" + player.Playing + " modus=" + player.Mode +
                            " Lo=" + player.Lo + " Hi=" + player.Hi + " noten=" + all + " zuSpielen=" + pr + " Song=" + (player.Song != null));
                    }
                    // bei Änderung sofort senden, sonst alle 2 s auffrischen (falls der Agent zwischendurch verworfen hat)
                    if (force || !Same(frame, last) || (now - lastSend).TotalSeconds > 2)
                    {
                        odr.SetLeds(frame);
                        if (!Same(frame, last))
                        {
                            int lit = 0; var keys = "";
                            for (int k = 0; k < 128; k++) if (frame[k] != 0) { lit++; if (lit <= 8) keys += " " + k + ":" + frame[k].ToString("X2"); }
                            if (++sent <= 200) Log("LEDs " + lit + " an, pos=" + player.Position.ToString("0.00") + " modus=" + player.Mode + " Lo=" + player.Lo + " Hi=" + player.Hi + keys);
                        }
                        Buffer.BlockCopy(frame, 0, last, 0, 128);
                        lastSend = now;
                        force = false;
                    }
                    odr.Drain();
                    Thread.Sleep(15);
                }
                catch (Exception ex)
                {
                    status = "Nicht verbunden: " + ex.Message;
                    Log("FEHLER " + ex.GetType().Name + ": " + ex.Message);
                    if (odr != null) { odr.Dispose(); odr = null; }
                    for (int i = 0; i < 20 && !quit; i++) Thread.Sleep(100);   // 2 s warten, dann neu versuchen
                }
            }

            if (odr != null)
            {
                try { odr.SetLeds(new byte[128]); } catch { }
                odr.Dispose();
            }
        }

        static bool Same(byte[] a, byte[] b)
        {
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        /// <summary>Welche Tasten sollen jetzt leuchten? Gleiche Auswahl wie die Tastatur im PianoView, aber nur zu spielende, noch offene Noten.</summary>
        byte[] Compute()
        {
            var f = new byte[128];
            var song = player.Song;
            var notes = player.Notes;
            if (song == null || notes == null) return f;

            double pos = player.Position;
            int i = player.FirstVisibleIndex(pos);
            for (; i >= 0 && i < notes.Count && notes[i].Start <= pos + Ahead; i++)
            {
                var n = notes[i];
                if (n.Drum || n.End <= pos) continue;
                if (n.Midi < 0 || n.Midi > 127) continue;
                if (player.Mode == PlayMode.Watch)
                {
                    if (n.Midi < player.Lo || n.Midi > player.Hi) continue;   // Zuhören: alles zeigen, was auf die Tastatur passt
                }
                else if (!n.Practice || n.State != 0) continue;               // Üben: nur offene, zu spielende Noten
                f[n.Midi] = n.Hand == 'R' ? Green : n.Hand == 'L' ? Blue : White;
            }
            return f;
        }

        public void Dispose()
        {
            quit = true;
            thread.Join(2000);
        }
    }
}
