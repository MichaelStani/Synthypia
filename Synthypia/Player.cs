using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Synthypia
{
    public enum PlayMode { Watch, Wait, Score }
    public enum PracticeHands { Both, Right, Left }

    /// <summary>Wiedergabe-Engine: Zeitgeber, MIDI-Ausgabe, Übungsmodi und Wertung.</summary>
    public sealed class Player : IDisposable
    {
        public const double LeadIn = 2.0;
        const double HitWindow = 0.28;

        readonly object sync = new object();
        IMidiSink outDev;
        Thread thread;
        volatile bool quit;

        Song song;
        List<Note> notes = new List<Note>();
        List<MidiEvt> events = new List<MidiEvt>();
        int evIdx, waitIdx, missIdx, beatIdx;
        double pos = -LeadIn;
        bool playing;
        double speed = 1.0;
        double volume = 0.8;
        int userChannel = 15;
        int userProgram;
        readonly bool[] userDown = new bool[128];

        public PlayMode Mode = PlayMode.Watch;
        public PracticeHands Hands = PracticeHands.Both;
        public bool Metronome;
        /// <summary>0 = an das Stück angepasst, sonst 49, 61 oder 88 Tasten.</summary>
        public int KeyboardKeys;
        int octaveShift;
        public double LoopA = -1, LoopB = -1;
        public bool Loop;
        public int Lo = 21, Hi = 108;
        public double MaxDur = 1;
        public bool Waiting;
        public int Hits, Misses, Wrong, Streak, BestStreak;

        public Player(int outputDevice)
        {
            NativeMidi.timeBeginPeriod(1);
            outDev = new MidiOutDevice(outputDevice);
            outDev.SetVolume(volume);
            thread = new Thread(Run) { IsBackground = true, Name = "Synthypia-Player", Priority = ThreadPriority.AboveNormal };
            thread.Start();
        }

        // ---------- Zustand für die Oberfläche ----------
        public Song Song { get { return song; } }
        public List<Note> Notes { get { return notes; } }
        public double Position { get { return pos; } }
        public bool Playing { get { return playing; } }
        public double Speed { get { return speed; } }
        public double Duration { get { return song == null ? 0 : song.Duration; } }
        public bool IsUserDown(int midi) { return midi >= 0 && midi < 128 && userDown[midi]; }
        public bool OutputOpen { get { return outDev != null && outDev.IsOpen; } }
        public int UserChannel { get { return userChannel; } }

        public double Accuracy
        {
            get
            {
                int total = Hits + Misses;
                return total == 0 ? 100.0 : 100.0 * Hits / total;
            }
        }

        // ---------- Steuerung ----------
        public void LoadSong(Song s)
        {
            lock (sync)
            {
                playing = false;
                AllNotesOff();
                song = s;
                octaveShift = 0;
                Rebuild();
                SeekInternal(-LeadIn);
                LoopA = LoopB = -1;
                Loop = false;
            }
        }

        public void Play()
        {
            lock (sync)
            {
                if (song == null) return;
                if (pos >= song.Duration + 0.9) SeekInternal(-LeadIn);
                playing = true;
            }
        }

        public void Pause()
        {
            lock (sync)
            {
                playing = false;
                AllNotesOff();
            }
        }

        public void TogglePlay()
        {
            if (playing) Pause(); else Play();
        }

        public void Stop()
        {
            lock (sync)
            {
                playing = false;
                SeekInternal(-LeadIn);
            }
        }

        public void Seek(double t)
        {
            lock (sync) SeekInternal(t);
        }

        public void SetSpeed(double s)
        {
            lock (sync) speed = Math.Max(0.1, Math.Min(3.0, s));
        }

        public void SetVolume(double v)
        {
            lock (sync)
            {
                volume = v;
                if (outDev != null) outDev.SetVolume(v);
            }
        }

        public void SetUserProgram(int program)
        {
            lock (sync)
            {
                userProgram = program;
                if (outDev != null) outDev.Send(0xC0 | userChannel, program, 0);
            }
        }

        public void SetOutputDevice(int index)
        {
            // Das alte Gerät zuerst schließen: der GS-Wavetable-Synth lässt sich nur einmal öffnen, sonst scheitert das neue Öffnen
            IMidiSink old;
            lock (sync)
            {
                AllNotesOff();
                old = outDev;
                outDev = null;
            }
            if (old != null) old.Dispose();
            SetOutput(new MidiOutDevice(index), false);
        }

        /// <summary>Wechselt die Klangausgabe. Die alte Ausgabe wird beendet, die neue gehört danach dem Player.</summary>
        public void SetOutput(IMidiSink sink, bool disposeOld)
        {
            lock (sync)
            {
                AllNotesOff();
                if (outDev != null && disposeOld) outDev.Dispose();
                outDev = sink;
                outDev.SetVolume(volume);
                outDev.Send(0xC0 | userChannel, userProgram, 0);
                if (song != null) SendSetup(evIdx);
            }
        }

        public void SetMode(PlayMode m)
        {
            lock (sync)
            {
                if (Mode == m) return;
                Mode = m;
                AllNotesOff();
                SeekInternal(pos);
                ResetStats();
            }
        }

        public void SetHands(PracticeHands h)
        {
            lock (sync)
            {
                Hands = h;
                Rebuild();
            }
        }

        public void SetLoopPoint(bool isA)
        {
            lock (sync)
            {
                double t = Math.Max(0, pos);
                if (isA) { LoopA = t; if (LoopB <= LoopA) LoopB = -1; }
                else { LoopB = t; if (LoopA < 0 || LoopA >= LoopB) LoopA = 0; }
            }
        }

        public void ClearLoop()
        {
            lock (sync) { LoopA = LoopB = -1; Loop = false; }
        }

        /// <summary>Muss nach Änderung von Hand-Zuordnungen aufgerufen werden.</summary>
        public void Rebuild()
        {
            lock (sync)
            {
                AllNotesOff();
                if (song == null) return;

                var allNotes = new List<Note>();
                var evs = new List<MidiEvt>();
                int seq = 0;
                var usedCh = new HashSet<int>();

                foreach (var tr in song.Tracks)
                {
                    if (tr.Hand == 'X') continue;
                    foreach (var n in tr.Notes)
                    {
                        char h = tr.Drums ? 'B' : tr.Hand == 'A' ? (n.Midi < 60 ? 'L' : 'R') : tr.Hand;
                        n.Hand = h;
                        n.Practice = !tr.Drums &&
                            ((h == 'R' && (Hands == PracticeHands.Both || Hands == PracticeHands.Right)) ||
                             (h == 'L' && (Hands == PracticeHands.Both || Hands == PracticeHands.Left)));
                        n.State = 0;
                        n.Sounding = false;
                        allNotes.Add(n);
                        usedCh.Add(n.Channel);
                        evs.Add(new MidiEvt { Time = n.Start, Status = (byte)(0x90 | n.Channel), D1 = (byte)n.Midi, D2 = (byte)n.Vel, Note = n, IsOn = true, Prio = 2, Seq = seq++ });
                        evs.Add(new MidiEvt { Time = n.End, Status = (byte)(0x80 | n.Channel), D1 = (byte)n.Midi, D2 = 0, Note = n, IsOn = false, Prio = 0, Seq = seq++ });
                    }
                }
                foreach (var c in song.Controls)
                {
                    if (song.Tracks[c.Track].Hand == 'X') continue;
                    c.Seq = seq++;
                    evs.Add(c);
                    usedCh.Add(c.Status & 0x0F);
                }

                allNotes.Sort((a, b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : a.Midi.CompareTo(b.Midi));
                evs.Sort((a, b) =>
                {
                    int c = a.Time.CompareTo(b.Time);
                    if (c != 0) return c;
                    c = a.Prio.CompareTo(b.Prio);
                    return c != 0 ? c : a.Seq.CompareTo(b.Seq);
                });

                notes = allNotes;
                events = evs;
                MaxDur = allNotes.Count == 0 ? 1 : Math.Min(60, allNotes.Max(n => n.End - n.Start));

                // freien Kanal für den Spieler wählen
                userChannel = 15;
                for (int ch = 15; ch >= 0; ch--)
                    if (ch != 9 && !usedCh.Contains(ch)) { userChannel = ch; break; }
                if (outDev != null) outDev.Send(0xC0 | userChannel, userProgram, 0);

                ComputeRange();
                SeekInternal(pos);
                ResetStats();
            }
        }

        void ComputeRange()
        {
            ComputeRangeCore();
            // Noten außerhalb der Tastatur kann der Spieler nicht treffen: sie gehören dann zur Begleitung und erklingen automatisch
            foreach (var n in notes)
            {
                bool hand = (n.Hand == 'R' && (Hands == PracticeHands.Both || Hands == PracticeHands.Right)) ||
                            (n.Hand == 'L' && (Hands == PracticeHands.Both || Hands == PracticeHands.Left));
                n.Practice = !n.Drum && hand && n.Midi >= Lo && n.Midi <= Hi;
            }
        }

        void ComputeRangeCore()
        {
            var vis = notes.Where(n => !n.Drum).ToList();
            if (KeyboardKeys == 88) { Lo = 21; Hi = 108; return; }
            if (KeyboardKeys == 49 || KeyboardKeys == 61)
            {
                // Tastaturen mit 49/61 Tasten beginnen und enden auf C: die Oktavlage mit den meisten Noten des Stücks wählen
                int span = KeyboardKeys - 1;
                int bestStart = 36, bestCount = -1, bestDist = int.MaxValue;
                int median = vis.Count == 0 ? 60 : vis.OrderBy(n => n.Midi).ElementAt(vis.Count / 2).Midi;
                for (int s = 24; s + span <= 108; s += 12)
                {
                    int cnt = vis.Count(n => n.Midi >= s && n.Midi <= s + span);
                    int dist = Math.Abs(s + span / 2 - median);
                    if (cnt > bestCount || (cnt == bestCount && dist < bestDist)) { bestStart = s; bestCount = cnt; bestDist = dist; }
                }
                int start = Math.Max(24, Math.Min(108 - span, bestStart + 12 * octaveShift));
                octaveShift = (start - bestStart) / 12;
                Lo = start; Hi = start + span;
                return;
            }
            if (vis.Count == 0) { Lo = 21; Hi = 108; return; }
            int lo = Math.Max(21, vis.Min(n => n.Midi) - 1);
            int hi = Math.Min(108, vis.Max(n => n.Midi) + 1);
            while (lo > 21 && IsBlack(lo)) lo--;
            while (hi < 108 && IsBlack(hi)) hi++;
            while (Whites(lo, hi) < 29 && (lo > 21 || hi < 108))
            {
                if (lo > 21) { lo--; while (lo > 21 && IsBlack(lo)) lo--; }
                if (Whites(lo, hi) < 29 && hi < 108) { hi++; while (hi < 108 && IsBlack(hi)) hi++; }
            }
            Lo = lo; Hi = hi;
        }

        public static bool IsBlack(int midi)
        {
            int m = midi % 12;
            return m == 1 || m == 3 || m == 6 || m == 8 || m == 10;
        }

        static int Whites(int lo, int hi)
        {
            int c = 0;
            for (int p = lo; p <= hi; p++) if (!IsBlack(p)) c++;
            return c;
        }

        public void RefreshRange()
        {
            lock (sync) ComputeRange();
        }

        /// <summary>Stellt die Tastaturgröße um (0 = an Stück angepasst) und setzt die Oktav-Verschiebung zurück.</summary>
        public void SetKeyboardKeys(int keys)
        {
            lock (sync)
            {
                KeyboardKeys = keys;
                octaveShift = 0;
                ComputeRange();
            }
        }

        /// <summary>Ob die Tastatur (nur bei 49/61 Tasten) um eine Oktave nach unten (-1) oder oben (+1) verschoben werden kann.</summary>
        public bool CanShiftOctave(int dir)
        {
            lock (sync)
            {
                if (KeyboardKeys != 49 && KeyboardKeys != 61) return false;
                int span = KeyboardKeys - 1;
                int target = Lo + 12 * dir;
                return target >= 24 && target + span <= 108;
            }
        }

        public void ShiftOctave(int dir)
        {
            lock (sync)
            {
                if (!CanShiftOctave(dir)) return;
                octaveShift += dir;
                AllNotesOff();
                ComputeRange();
            }
        }

        void ResetStats()
        {
            Hits = Misses = Wrong = Streak = BestStreak = 0;
        }

        // ---------- Spieler-Eingabe ----------
        public void UserNoteOn(int midi, int vel)
        {
            lock (sync)
            {
                if (outDev != null) outDev.Send(0x90 | userChannel, midi, Math.Max(1, vel));
                userDown[midi & 0x7F] = true;
                if (song == null || Mode == PlayMode.Watch) return;

                Note best = null;
                double bd = double.MaxValue;
                int i = LowerBound(pos - HitWindow);
                for (; i < notes.Count && notes[i].Start <= pos + HitWindow; i++)
                {
                    var n = notes[i];
                    if (n.Practice && n.State == 0 && n.Midi == midi)
                    {
                        double d = Math.Abs(n.Start - pos);
                        if (d < bd) { bd = d; best = n; }
                    }
                }
                if (best != null)
                {
                    best.State = 1;
                    Hits++;
                    Streak++;
                    if (Streak > BestStreak) BestStreak = Streak;
                }
                else if (Mode == PlayMode.Score && playing)
                {
                    Wrong++;
                    Streak = 0;
                }
            }
        }

        public void UserNoteOff(int midi)
        {
            lock (sync)
            {
                if (outDev != null) outDev.Send(0x80 | userChannel, midi, 0);
                userDown[midi & 0x7F] = false;
            }
        }

        /// <summary>Pedal, Pitch-Bend usw. vom Keyboard (Status mit beliebigem Kanal).</summary>
        public void UserControl(int status, int d1, int d2)
        {
            lock (sync)
            {
                if (outDev != null) outDev.Send((status & 0xF0) | userChannel, d1, d2);
            }
        }

        // ---------- interne Logik ----------
        int LowerBound(double t)
        {
            int lo = 0, hi = notes.Count;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (notes[mid].Start < t) lo = mid + 1; else hi = mid;
            }
            return lo;
        }

        /// <summary>Index der ersten Note, die im sichtbaren Bereich ab t beginnt (für die Darstellung).</summary>
        public int FirstVisibleIndex(double t)
        {
            return LowerBound(t - MaxDur);
        }

        void Send(int status, int d1, int d2)
        {
            if (outDev != null) outDev.Send(status, d1, d2);
        }

        void AllNotesOff()
        {
            for (int ch = 0; ch < 16; ch++)
            {
                Send(0xB0 | ch, 64, 0);   // Pedal loslassen
                Send(0xB0 | ch, 123, 0);  // All Notes Off
            }
            foreach (var n in notes) n.Sounding = false;
            Array.Clear(userDown, 0, userDown.Length);
        }

        bool Audible(Note n)
        {
            return Mode == PlayMode.Watch || !n.Practice;
        }

        void SeekInternal(double t)
        {
            AllNotesOff();
            pos = t;
            Waiting = false;
            if (song == null) return;

            int lo = 0, hi = events.Count;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (events[mid].Time < t) lo = mid + 1; else hi = mid;
            }
            evIdx = lo;
            SendSetup(evIdx);

            waitIdx = LowerBound(t - 1e-6);
            missIdx = LowerBound(t - HitWindow);

            var beats = song.Beats;
            int bl = 0, bh = beats.Count;
            while (bl < bh)
            {
                int mid = (bl + bh) / 2;
                if (beats[mid].Time < t) bl = mid + 1; else bh = mid;
            }
            beatIdx = bl;

            for (int i = LowerBound(t - HitWindow - 1e-6); i < notes.Count; i++) notes[i].State = 0;
            if (t <= 0) ResetStats();
        }

        /// <summary>Programmwechsel, Lautstärke, Pan, Pitch-Bend usw. vor der Position wiederherstellen.</summary>
        void SendSetup(int upTo)
        {
            var last = new SortedDictionary<int, MidiEvt>();
            for (int i = 0; i < upTo && i < events.Count; i++)
            {
                var e = events[i];
                if (e.Note != null) continue;
                int hi = e.Status & 0xF0;
                if (hi == 0xB0 && (e.D1 == 64 || e.D1 == 66 || e.D1 == 67 || e.D1 >= 120)) continue;
                if (hi == 0xD0) continue;
                int key = ((e.Status & 0x0F) << 24) | (hi << 8) | (hi == 0xB0 ? e.D1 : 0);
                last[key] = e;
            }
            foreach (var kv in last)
            {
                var e = kv.Value;
                Send(e.Status, e.D1, e.D2);
            }
        }

        void Dispatch(MidiEvt e)
        {
            var n = e.Note;
            if (n == null) { Send(e.Status, e.D1, e.D2); return; }
            if (e.IsOn)
            {
                if (!Audible(n)) return;
                Send(e.Status, e.D1, e.D2);
                n.Sounding = true;
            }
            else if (n.Sounding)
            {
                Send(e.Status, e.D1, 0);
                n.Sounding = false;
            }
        }

        void Run()
        {
            var sw = Stopwatch.StartNew();
            double last = sw.Elapsed.TotalSeconds;
            while (!quit)
            {
                double now = sw.Elapsed.TotalSeconds;
                double dt = Math.Min(0.1, now - last);
                last = now;
                lock (sync)
                {
                    if (playing && song != null) Step(dt);
                }
                Thread.Sleep(1);
            }
        }

        void Step(double dt)
        {
            double np = pos + dt * speed;
            Waiting = false;

            if (Mode == PlayMode.Wait)
            {
                while (waitIdx < notes.Count && (!notes[waitIdx].Practice || notes[waitIdx].State != 0)) waitIdx++;
                if (waitIdx < notes.Count && notes[waitIdx].Start <= np)
                {
                    np = Math.Max(pos, notes[waitIdx].Start);
                    Waiting = true;
                }
            }

            if (Loop && LoopA >= 0 && LoopB > LoopA && np >= LoopB)
            {
                SeekInternal(LoopA);
                return;
            }

            while (evIdx < events.Count && events[evIdx].Time <= np) Dispatch(events[evIdx++]);

            var beats = song.Beats;
            while (beatIdx < beats.Count && beats[beatIdx].Time <= np)
            {
                if (Metronome) Send(0x99, beats[beatIdx].Bar ? 76 : 77, 100);
                beatIdx++;
            }

            while (missIdx < notes.Count && notes[missIdx].Start + HitWindow < np)
            {
                var n = notes[missIdx];
                if (Mode == PlayMode.Score && n.Practice && n.State == 0)
                {
                    n.State = 2;
                    Misses++;
                    Streak = 0;
                }
                missIdx++;
            }

            if (np >= song.Duration + 1.0)
            {
                np = song.Duration + 1.0;
                playing = false;
                AllNotesOff();
            }
            pos = np;
        }

        public void Dispose()
        {
            quit = true;
            if (thread != null) thread.Join(500);
            lock (sync)
            {
                AllNotesOff();
                if (outDev != null) outDev.Dispose();
                outDev = null;
            }
            NativeMidi.timeEndPeriod(1);
        }
    }
}
