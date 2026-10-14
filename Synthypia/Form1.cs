using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Synthypia
{
    public partial class Form1 : Form
    {
        static readonly Color Fore = Color.FromArgb(226, 230, 240);
        static readonly int[] SoundPrograms = { 0, 4, 19, 48, 80 };

        readonly Player player;
        MidiInDevice midiIn;
        LightGuide light;
        IPluginHost vst;
        PluginForm pluginForm;
        int vstIndex = -1;
        bool changingOutput;
        bool restoringIn;
        readonly Dictionary<Keys, int> pcKeys = new Dictionary<Keys, int>();
        readonly Dictionary<Keys, int> pcDown = new Dictionary<Keys, int>();

        const int WM_NCCALCSIZE = 0x83, WM_NCHITTEST = 0x84;
        const int HTCLIENT = 1, HTCAPTION = 2, HTTOP = 12, HTTOPLEFT = 13, HTTOPRIGHT = 14;

        readonly TitleBar titleBar;

        public Form1()
        {
            InitializeComponent();

            // Eigene Titelleiste; zuletzt hinzugefügt, damit sie ganz oben über der Werkzeugleiste andockt
            titleBar = new TitleBar(this);
            Controls.Add(titleBar);

            Theme.ApplyFonts(this);
            // Etwas engere Abschnittsabstände, damit die Seitenleiste auch mit der größeren Schrift ohne Scrollbalken passt
            foreach (Control c in panelSide.Controls)
                if (c is Label && c.Font.Bold) c.Margin = new Padding(0, 8, 0, 3);
            volume.Height = fall.Height = 28;

            // Symbole auf den Werkzeugleisten-Buttons, "Start" hervorgehoben
            btnOpen.IconGlyph = "";
            btnPlay.IconGlyph = "";
            btnPlay.Accent = true;
            btnStop.IconGlyph = "";
            btnStop.Text = "Stopp";
            volume.Height = fall.Height = 32;   // etwas flacher, damit die Seitenleiste ohne Scrollbalken passt
            btnOctDown.Text = ""; btnOctUp.Text = "";   // Chevron links/rechts in der Symbolschrift
            btnOctDown.Font = btnOctUp.Font = Theme.Icon(10f);
            Theme.DarkScrollBars(panelSide);

            // Seltener gebrauchte Bereiche der Seitenleiste einklappbar, zunächst eingeklappt (dann braucht die Leiste keinen Scrollbalken)
            MakeCollapsible(lblSecOptions, "OPTIONEN", false, chkMetro, chkNames, chkSheet, chkPcKeys);
            MakeCollapsible(lblSecTracks, "SPUREN / HÄNDE", false, trackPanel);

            player = new Player(-1);
            view.Player = player;
            sheet.Player = player;

            cbOut.Items.Add("Windows-Standard (GS Wavetable)");
            cbOut.Items.AddRange(MidiOutDevice.ListDevices().Cast<object>().ToArray());
            cbIn.Items.Add("Keines");
            cbIn.Items.AddRange(MidiInDevice.ListDevices().Cast<object>().ToArray());
            cbMode.SelectedIndex = 0;
            cbHands.SelectedIndex = 0;
            cbSound.SelectedIndex = 0;
            cbKeys.SelectedIndex = 0;
            cbLight.SelectedIndex = 0;
            cbOut.SelectedIndex = 0;

            // zuletzt gewählten MIDI-Eingang wiederherstellen (ohne die gespeicherte Wahl zu überschreiben)
            string savedIn = PluginState.LoadMidiIn();
            restoringIn = true;
            cbIn.SelectedIndex = 0;
            int savedIdx = savedIn == null ? -1 : cbIn.Items.IndexOf(savedIn);
            if (savedIdx > 0) cbIn.SelectedIndex = savedIdx;
            restoringIn = false;

            BuildPcKeyMap();
            uiTimer.Start();
            Shown += (s, e) => view.Focus();

            Splash.SetStatus("Lade Musikstück …");
            var args = Environment.GetCommandLineArgs();
            if (args.Length > 1 && File.Exists(args[1])) OpenFile(args[1]);
            else LoadSong(Song.CreateDemo());

            // Alles Langsame (VST-Plugin) vor dem ersten Anzeigen laden: das Fenster erscheint erst, wenn es fertig ist
            AutoLoadPlugin();
            UpdateUi();

            Splash.SetStatus("Fast fertig …");
            if (!player.OutputOpen)
                Splash.Close();
            if (!player.OutputOpen)
                MessageBox.Show(this, "Das MIDI-Ausgabegerät konnte nicht geöffnet werden. Bitte rechts ein anderes Gerät wählen.", "Synthypia");
            view.Focus();
        }

        /// <summary>Macht eine Überschrift der Seitenleiste zum Ein-/Ausklapp-Schalter für die zugehörigen Steuerelemente.</summary>
        void MakeCollapsible(Label header, string title, bool expanded, params Control[] body)
        {
            bool open = expanded;
            Action apply = () =>
            {
                header.Text = (open ? "▾  " : "▸  ") + title;
                foreach (var c in body) c.Visible = open;
            };
            header.Cursor = Cursors.Hand;
            header.Click += (s, e) => { open = !open; apply(); view.Focus(); };
            apply();
        }

        // ---------------------------------------------------------------- Fenster-Design

        Rectangle normalBounds;
        FormWindowState prevState = FormWindowState.Normal;
        bool fixingBounds;

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Chrome.Apply(this);
        }

        // WinForms rechnet die Fenstergröße beim Wiederherstellen aus dem Client-Bereich mit Standard-Titelleiste zurück:
        // ohne Gegenmaßnahme wüchse das Fenster bei jedem Maximieren/Wiederherstellen um die Leistenhöhe. Normalgröße merken und zurücksetzen.
        int restoredTick = int.MinValue / 2;

        void TrackNormalBounds()
        {
            if (fixingBounds) return;
            var state = WindowState;
            if (state == FormWindowState.Normal)
            {
                if (prevState == FormWindowState.Maximized)
                {
                    restoredTick = Environment.TickCount;   // ab jetzt hält SetBoundsCore kurz die gemerkte Größe
                    if (!normalBounds.IsEmpty && Bounds != normalBounds)
                    {
                        fixingBounds = true;
                        Bounds = normalBounds;
                        fixingBounds = false;
                    }
                }
                normalBounds = Bounds;
            }
            prevState = state;
        }

        protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
        {
            // Unmittelbar nach dem Wiederherstellen rechnet WinForms die Größe noch einmal mit Standard-Titelleiste aus dem
            // Client-Bereich zurück und würde das Fenster um die Leistenhöhe vergrößern: die gemerkte Größe beibehalten.
            if (IsHandleCreated && !fixingBounds && !normalBounds.IsEmpty && WindowState == FormWindowState.Normal &&
                unchecked(Environment.TickCount - restoredTick) < 400 &&
                (specified & BoundsSpecified.Size) != 0 && (width != normalBounds.Width || height != normalBounds.Height))
            {
                width = normalBounds.Width;
                height = normalBounds.Height;
            }
            base.SetBoundsCore(x, y, width, height, specified);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (IsHandleCreated) TrackNormalBounds();
        }

        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);
            if (IsHandleCreated && WindowState == FormWindowState.Normal && !fixingBounds) normalBounds = Bounds;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCCALCSIZE && m.WParam != IntPtr.Zero)
            {
                // Standard-Titelleiste entfernen: Der Client-Bereich beginnt ganz oben, Rahmen links/rechts/unten bleiben beim System
                var r = (Chrome.RECT)Marshal.PtrToStructure(m.LParam, typeof(Chrome.RECT));
                int origTop = r.top;
                base.WndProc(ref m);
                r = (Chrome.RECT)Marshal.PtrToStructure(m.LParam, typeof(Chrome.RECT));
                r.top = origTop + (Chrome.IsMaximized(this) ? Chrome.FrameThickness() : 0);
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
                    int edge = LogicalToDeviceUnits(6);
                    if (WindowState == FormWindowState.Normal && p.Y < edge)
                        m.Result = (IntPtr)(p.X < edge ? HTTOPLEFT : p.X >= ClientSize.Width - edge ? HTTOPRIGHT : HTTOP);
                    else if (p.Y >= 0 && p.Y < titleBar.Height)
                        m.Result = (IntPtr)HTCAPTION;   // Titelleiste: Fenster ziehen, Doppelklick = maximieren
                }
                return;
            }

            base.WndProc(ref m);
        }

        // ---------------------------------------------------------------- Ereignisse der Oberfläche

        void UiTimer_Tick(object sender, EventArgs e) { UpdateUi(); }

        void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            uiTimer.Stop();
            if (pluginForm != null) pluginForm.Close();
            if (midiIn != null) midiIn.Dispose();
            StopLightGuide();
            player.Dispose();
        }

        void Form1_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
        }

        void Form1_DragDrop(object sender, DragEventArgs e)
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null && files.Length > 0) OpenFile(files[0]);
        }

        void BtnOpen_Click(object sender, EventArgs e) { BrowseFile(); }
        void BtnPlay_Click(object sender, EventArgs e) { player.TogglePlay(); view.Focus(); }
        void BtnStop_Click(object sender, EventArgs e) { player.Stop(); view.Focus(); }

        void Seek_Scroll(object sender, EventArgs e) { player.Seek(seek.Value / 1000.0 * player.Duration); }

        void Tempo_Scroll(object sender, EventArgs e)
        {
            player.SetSpeed(tempo.Value / 100.0);
        }

        void Volume_Scroll(object sender, EventArgs e) { player.SetVolume(volume.Value / 100.0); }
        void Fall_Scroll(object sender, EventArgs e) { view.FallSeconds = fall.Value / 2.0; }
        void Slider_MouseUp(object sender, MouseEventArgs e) { view.Focus(); }

        void Combo_DropDownClosed(object sender, EventArgs e) { BeginInvoke((Action)(() => view.Focus())); }
        void CbMode_SelectedIndexChanged(object sender, EventArgs e) { player.SetMode((PlayMode)cbMode.SelectedIndex); }
        void CbHands_SelectedIndexChanged(object sender, EventArgs e) { player.SetHands((PracticeHands)cbHands.SelectedIndex); }
        void CbSound_SelectedIndexChanged(object sender, EventArgs e) { player.SetUserProgram(SoundPrograms[cbSound.SelectedIndex]); }
        void CbOut_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (changingOutput) return;
            if (vst != null)
            {
                if (cbOut.SelectedIndex == vstIndex) return;
                int sel = cbOut.SelectedIndex;
                DropVstItem();
                PluginState.ClearLast(); // bewusst auf ein MIDI-Gerät gewechselt: nicht mehr automatisch laden
                changingOutput = true;
                cbOut.SelectedIndex = sel;
                changingOutput = false;
            }
            player.SetOutputDevice(cbOut.SelectedIndex - 1); // gibt ein bisheriges Plugin frei
        }

        // ---------------------------------------------------------------- VST2-Plugin

        void BtnVst_Click(object sender, EventArgs e)
        {
            string path;
            using (var dlg = new OpenFileDialog
            {
                Title = "VST-Instrument wählen",
                Filter = "VST-Plugins (*.vst3;*.dll)|*.vst3;*.dll|VST3 (*.vst3)|*.vst3|VST2 (*.dll)|*.dll",
                InitialDirectory = Directory.Exists(@"C:\Program Files\Common Files\VST3") ? @"C:\Program Files\Common Files\VST3" : ""
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) { view.Focus(); return; }
                path = dlg.FileName;
            }
            LoadPlugin(path, true);
            view.Focus();
        }

        /// <summary>Lädt das zuletzt verwendete Plugin, falls beim Beenden eines aktiv war.</summary>
        void AutoLoadPlugin()
        {
            string last = PluginState.LoadLast();
            if (last == null) return;
            Splash.SetStatus("Lade Klang-Plug-in " + Path.GetFileNameWithoutExtension(last) + " …");
            LoadPlugin(last, false);
        }

        bool LoadPlugin(string path, bool showErrors)
        {
            IPluginHost loaded;
            try
            {
                Cursor = Cursors.WaitCursor;
                if (path.EndsWith(".vst3", StringComparison.OrdinalIgnoreCase)) loaded = new Vst3Output(path);
                else loaded = new VstOutput(path);
            }
            catch (Exception ex)
            {
                if (showErrors)
                    MessageBox.Show(this, "Das Plugin konnte nicht geladen werden:\n" + ex.Message, "Synthypia", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            finally { Cursor = Cursors.Default; }

            DropVstItem();
            vst = loaded;
            changingOutput = true;
            vstIndex = cbOut.Items.Add("VST: " + vst.Name);
            cbOut.SelectedIndex = vstIndex;
            changingOutput = false;
            btnVstEditor.Enabled = vst.HasEditor;
            player.SetOutput(vst, true);
            PluginState.SaveLast(path);
            return true;
        }

        void BtnVstEditor_Click(object sender, EventArgs e)
        {
            if (vst == null || !vst.HasEditor) return;
            if (pluginForm != null) { pluginForm.Activate(); return; }
            pluginForm = new PluginForm(vst);
            pluginForm.FormClosed += (s, a) => pluginForm = null;
            pluginForm.Show(this);
        }

        /// <summary>Schließt das Plugin-Fenster und entfernt den Listeneintrag (das Plugin selbst gibt der Player frei).</summary>
        void DropVstItem()
        {
            if (pluginForm != null) pluginForm.Close();
            if (vstIndex >= 0)
            {
                changingOutput = true;
                cbOut.Items.RemoveAt(vstIndex);
                changingOutput = false;
            }
            vst = null;
            vstIndex = -1;
            btnVstEditor.Enabled = false;
        }
        void CbIn_SelectedIndexChanged(object sender, EventArgs e)
        {
            OpenMidiInput(cbIn.SelectedIndex - 1);
            if (!restoringIn) PluginState.SaveMidiIn(cbIn.SelectedIndex > 0 ? cbIn.SelectedItem.ToString() : null);
        }

        void ChkMetro_CheckedChanged(object sender, EventArgs e) { player.Metronome = chkMetro.Checked; view.Focus(); }
        void ChkNames_CheckedChanged(object sender, EventArgs e) { view.ShowNames = chkNames.Checked; view.Focus(); }
        void ChkSheet_CheckedChanged(object sender, EventArgs e) { sheet.Visible = chkSheet.Checked; view.Focus(); }
        void CbKeys_SelectedIndexChanged(object sender, EventArgs e)
        {
            player.SetKeyboardKeys(new[] { 0, 49, 61, 88 }[cbKeys.SelectedIndex]);
        }

        void CbLight_SelectedIndexChanged(object sender, EventArgs e)
        {
            StopLightGuide();
            if (cbLight.SelectedIndex == 1) light = new LightGuide(player);
            lblLightStatus.Text = "";
        }

        void StopLightGuide()
        {
            if (light == null) return;
            light.Dispose();   // löscht die LEDs
            light = null;
        }

        void BtnOctDown_Click(object sender, EventArgs e) { player.ShiftOctave(-1); view.Focus(); }
        void BtnOctUp_Click(object sender, EventArgs e) { player.ShiftOctave(1); view.Focus(); }
        void ChkPcKeys_CheckedChanged(object sender, EventArgs e) { view.Focus(); }
        void ChkLoop_CheckedChanged(object sender, EventArgs e) { player.Loop = chkLoop.Checked; view.Focus(); }

        void BtnLoopA_Click(object sender, EventArgs e) { player.SetLoopPoint(true); UpdateLoopLabel(); view.Focus(); }
        void BtnLoopB_Click(object sender, EventArgs e) { player.SetLoopPoint(false); UpdateLoopLabel(); view.Focus(); }
        void BtnLoopClear_Click(object sender, EventArgs e) { player.ClearLoop(); chkLoop.Checked = false; UpdateLoopLabel(); view.Focus(); }

        void View_KeyPressed(int key) { player.UserNoteOn(key, 95); }
        void View_KeyReleased(int key) { player.UserNoteOff(key); }

        void BuildTrackList()
        {
            trackPanel.SuspendLayout();
            trackPanel.Controls.Clear();
            var song = player.Song;
            if (song != null)
            {
                foreach (var tr in song.Tracks.Where(t => t.Notes.Count > 0))
                {
                    var t = tr;
                    var row = new Panel { Width = 270, Height = 30, Margin = new Padding(0, 0, 0, 2) };
                    var lbl = new Label
                    {
                        Text = t.DisplayName, Left = 0, Top = 6, Width = 140, AutoEllipsis = true, Font = Theme.UiFont
                    };
                    var cb = new MetroComboBox
                    {
                        DropDownStyle = ComboBoxStyle.DropDownList, Left = 145, Top = 2, Width = 122, Font = Theme.UiFont,
                        TabStop = false, BackColor = Color.FromArgb(44, 49, 68), ForeColor = Fore
                    };
                    char[] map;
                    if (t.Drums)
                    {
                        cb.Items.AddRange(new object[] { "Begleitung", "Aus" });
                        map = new[] { 'B', 'X' };
                    }
                    else
                    {
                        cb.Items.AddRange(new object[] { "Rechte Hand", "Linke Hand", "Auto (Teilung C4)", "Begleitung", "Aus" });
                        map = new[] { 'R', 'L', 'A', 'B', 'X' };
                    }
                    cb.SelectedIndex = Math.Max(0, Array.IndexOf(map, t.Hand));
                    cb.SelectedIndexChanged += (s, e) =>
                    {
                        t.Hand = map[cb.SelectedIndex];
                        player.Rebuild();
                    };
                    cb.DropDownClosed += (s, e) => BeginInvoke((Action)(() => view.Focus()));
                    row.Controls.Add(lbl);
                    row.Controls.Add(cb);
                    trackPanel.Controls.Add(row);
                }
            }
            trackPanel.ResumeLayout();
        }

        void UpdateLoopLabel()
        {
            if (player.LoopA >= 0 && player.LoopB > player.LoopA)
                lblLoop.Text = "A " + Fmt(player.LoopA) + "   B " + Fmt(player.LoopB);
            else if (player.LoopA >= 0)
                lblLoop.Text = "A " + Fmt(player.LoopA) + "   (B fehlt)";
            else
                lblLoop.Text = "Kein Abschnitt gesetzt";
        }

        static string Fmt(double t)
        {
            if (t < 0) t = 0;
            int s = (int)t;
            return (s / 60) + ":" + (s % 60).ToString("00");
        }

        void UpdateUi()
        {
            double d = player.Duration;
            double p = Math.Max(0, player.Position);
            lblTime.Text = Fmt(p) + " / " + Fmt(d);
            if (!seek.Capture && d > 0)
                seek.Value = (int)Math.Max(0, Math.Min(1000, p / d * 1000));
            var song = player.Song;
            double bpm = (song == null ? 0 : song.BpmAt(p)) * player.Speed;
            string tt = "Tempo " + tempo.Value + " %" + (bpm > 0 ? "  ·  " + bpm.ToString("0") + " BPM" : "");
            if (lblTempo.Text != tt) lblTempo.Text = tt;
            string txt = player.Playing ? "Pause" : "Start";
            if (btnPlay.Text != txt) { btnPlay.Text = txt; btnPlay.IconGlyph = player.Playing ? "" : ""; }
            bool down = player.CanShiftOctave(-1), up = player.CanShiftOctave(1);
            if (btnOctDown.Enabled != down) btnOctDown.Enabled = down;
            if (btnOctUp.Enabled != up) btnOctUp.Enabled = up;
            string oct = "C" + (player.Lo / 12 - 1) + "–C" + (player.Hi / 12 - 1);
            if (player.KeyboardKeys == 49 || player.KeyboardKeys == 61) { if (lblOct.Text != oct) lblOct.Text = oct; }
            else if (lblOct.Text != "Oktave") lblOct.Text = "Oktave";
            string ls = light == null ? "" : light.Status;
            if (lblLightStatus.Text != ls) lblLightStatus.Text = ls;
            view.Invalidate();
            if (sheet.Visible) sheet.Invalidate();
        }

        // ---------------------------------------------------------------- Dateien

        void BrowseFile()
        {
            using (var dlg = new OpenFileDialog { Filter = "MIDI-Dateien (*.mid;*.midi;*.kar)|*.mid;*.midi;*.kar|Alle Dateien|*.*" })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK) OpenFile(dlg.FileName);
            }
            view.Focus();
        }

        void OpenFile(string path)
        {
            try
            {
                LoadSong(Song.Load(path));
            }
            catch (Exception ex)
            {
                Splash.Close();
                MessageBox.Show(this, "Die Datei konnte nicht geladen werden:\n" + ex.Message, "Synthypia", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void LoadSong(Song s)
        {
            player.LoadSong(s);
            Text = "Synthypia – " + s.Title;
            chkLoop.Checked = false;
            UpdateLoopLabel();
            BuildTrackList();
        }

        // ---------------------------------------------------------------- Eingabe

        void OpenMidiInput(int index)
        {
            if (midiIn != null) { midiIn.Dispose(); midiIn = null; }
            if (index < 0) return;
            try
            {
                midiIn = new MidiInDevice(index);
                midiIn.Message += OnMidiMessage;
            }
            catch (Exception ex)
            {
                Splash.Close();
                MessageBox.Show(this, ex.Message, "Synthypia");
                cbIn.SelectedIndex = 0;
            }
        }

        void OnMidiMessage(int status, int d1, int d2)
        {
            int hi = status & 0xF0;
            if (hi == 0x90 && d2 > 0) player.UserNoteOn(d1, d2);
            else if (hi == 0x80 || hi == 0x90) player.UserNoteOff(d1);
            else if (hi == 0xB0 || hi == 0xE0) player.UserControl(status, d1, d2);
        }

        void BuildPcKeyMap()
        {
            // Liegt Y/Z je nach Layout (QWERTZ vs. QWERTY) an anderer Stelle
            bool qwertz = CultureInfo.CurrentCulture.TwoLetterISOLanguageName == "de" ||
                          InputLanguage.CurrentInputLanguage.Culture.TwoLetterISOLanguageName == "de";
            Keys lowFirst = qwertz ? Keys.Y : Keys.Z;
            Keys upperA = qwertz ? Keys.Z : Keys.Y;
            Keys[] lower = { lowFirst, Keys.S, Keys.X, Keys.D, Keys.C, Keys.V, Keys.G, Keys.B, Keys.H, Keys.N, Keys.J, Keys.M };
            Keys[] upper = { Keys.Q, Keys.D2, Keys.W, Keys.D3, Keys.E, Keys.R, Keys.D5, Keys.T, Keys.D6, upperA, Keys.D7, Keys.U, Keys.I };
            for (int i = 0; i < lower.Length; i++) pcKeys[lower[i]] = i;
            for (int i = 0; i < upper.Length; i++) pcKeys[upper[i]] = 12 + i;
        }

        int PcBase()
        {
            double center = (player.Lo + player.Hi) / 2.0;
            return Math.Max(0, 12 * (int)Math.Round(center / 12.0) - 12);
        }

        void OnFormKeyDown(object sender, KeyEventArgs e)
        {
            var combo = ActiveControl as ComboBox;
            if (combo != null && combo.DroppedDown) return;
            if (e.Control || e.Alt) return;

            bool handled = true;
            switch (e.KeyCode)
            {
                case Keys.Space: player.TogglePlay(); break;
                case Keys.Left: player.Seek(Math.Max(-Player.LeadIn, player.Position - (e.Shift ? 1 : 5))); break;
                case Keys.Right: player.Seek(Math.Min(player.Duration, player.Position + (e.Shift ? 1 : 5))); break;
                case Keys.Home: player.Stop(); break;
                case Keys.F2: player.SetLoopPoint(true); UpdateLoopLabel(); break;
                case Keys.F3: player.SetLoopPoint(false); UpdateLoopLabel(); break;
                case Keys.F5: chkSheet.Checked = !chkSheet.Checked; break;
                case Keys.F4: chkLoop.Checked = !chkLoop.Checked; break;
                default:
                    handled = false;
                    int off;
                    if (chkPcKeys.Checked && pcKeys.TryGetValue(e.KeyCode, out off))
                    {
                        handled = true;
                        if (!pcDown.ContainsKey(e.KeyCode))
                        {
                            int midi = PcBase() + off;
                            if (midi <= 127)
                            {
                                pcDown[e.KeyCode] = midi;
                                player.UserNoteOn(midi, 95);
                            }
                        }
                    }
                    break;
            }
            if (handled) { e.Handled = true; e.SuppressKeyPress = true; }
        }

        void OnFormKeyUp(object sender, KeyEventArgs e)
        {
            int midi;
            if (pcDown.TryGetValue(e.KeyCode, out midi))
            {
                pcDown.Remove(e.KeyCode);
                player.UserNoteOff(midi);
                e.Handled = true;
            }
        }
    }
}
