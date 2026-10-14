using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Synthypia
{
    public sealed class Note
    {
        public int Midi;
        public double Start, End;
        public int Vel;
        public int Channel;
        public int Track;
        public long StartTick, EndTick;
        public char Hand = 'B';      // R, L, B (Begleitung)
        public bool Practice;        // vom Spieler zu spielen
        public bool Drum;
        public int State;            // 0 offen, 1 getroffen, 2 verpasst
        public bool Sounding;
    }

    public sealed class MidiEvt
    {
        public double Time;
        public byte Status, D1, D2;
        public Note Note;            // != null bei Note-On/Off
        public bool IsOn;
        public int Prio;             // Sortierung bei gleicher Zeit: Off < Steuerung < On
        public int Seq;
        public int Track;
        public long Tick;
    }

    public sealed class TrackInfo
    {
        public int Index;
        public string Name = "";
        public int Program = -1;
        public bool Drums;
        public char Hand = 'B';      // R, L, A (Auto-Teilung bei C4), B, X (aus)
        public double MeanPitch;
        public List<Note> Notes = new List<Note>();
        public string DisplayName
        {
            get
            {
                string n = string.IsNullOrWhiteSpace(Name) ? "Spur " + (Index + 1) : Name.Trim();
                return n + " (" + Notes.Count + ")";
            }
        }
    }

    public sealed class KeyPt
    {
        public long Tick;
        public double Time;
        public int Sf;           // Vorzeichen: >0 Kreuze, <0 Bs
        public bool Minor;
    }

    public sealed class TempoInfo
    {
        public double Time;
        public double Bpm;
    }

    public sealed class Beat
    {
        public double Time;
        public bool Bar;
    }

    public sealed class Song
    {
        public string Title = "";
        public List<TrackInfo> Tracks = new List<TrackInfo>();
        public List<MidiEvt> Controls = new List<MidiEvt>();
        public List<Beat> Beats = new List<Beat>();
        public List<KeyPt> Keys = new List<KeyPt>();
        public List<TempoInfo> Tempos = new List<TempoInfo>();
        public double Duration;
        public double Bpm = 120;

        sealed class TempoPt { public long Tick; public int Uspq; public double Sec; }
        sealed class SigPt { public long Tick; public int Num; public int Den; }

        public static Song Load(string path)
        {
            return Parse(File.ReadAllBytes(path), Path.GetFileNameWithoutExtension(path));
        }

        static uint U32(byte[] d, ref int p)
        {
            uint v = ((uint)d[p] << 24) | ((uint)d[p + 1] << 16) | ((uint)d[p + 2] << 8) | d[p + 3];
            p += 4;
            return v;
        }

        static int U16(byte[] d, ref int p)
        {
            int v = (d[p] << 8) | d[p + 1];
            p += 2;
            return v;
        }

        static int Vlq(byte[] d, ref int p, int end)
        {
            int v = 0;
            for (int i = 0; i < 4 && p < end; i++)
            {
                byte b = d[p++];
                v = (v << 7) | (b & 0x7F);
                if ((b & 0x80) == 0) break;
            }
            return v;
        }

        public static Song Parse(byte[] data, string title)
        {
            int p = -1;
            for (int i = 0; i < Math.Min(data.Length - 4, 256); i++)
            {
                if (data[i] == 'M' && data[i + 1] == 'T' && data[i + 2] == 'h' && data[i + 3] == 'd') { p = i; break; }
            }
            if (p < 0) throw new InvalidDataException("Keine gültige MIDI-Datei (Header 'MThd' fehlt).");

            int hdr = p;
            p += 4;
            int hlen = (int)U32(data, ref p);
            int format = U16(data, ref p);
            int ntrk = U16(data, ref p);
            int div = U16(data, ref p);
            p = hdr + 8 + Math.Max(6, hlen);
            if (div == 0) throw new InvalidDataException("Ungültige Zeitauflösung.");

            var tempos = new List<TempoPt>();
            var sigs = new List<SigPt>();
            var song = new Song { Title = title };
            var keyRaw = new List<KeyPt>();

            for (int t = 0; t < ntrk && p + 8 <= data.Length; t++)
            {
                string id = Encoding.ASCII.GetString(data, p, 4);
                p += 4;
                int len = (int)Math.Min(U32(data, ref p), (uint)(data.Length - p));
                int end = p + len;
                if (id != "MTrk") { p = end; t--; continue; }

                var tr = new TrackInfo { Index = song.Tracks.Count };
                long tick = 0;
                int running = 0;
                var open = new Dictionary<int, Queue<Note>>();

                while (p < end)
                {
                    tick += Vlq(data, ref p, end);
                    if (p >= end) break;
                    int st = data[p];
                    if (st < 0x80)
                    {
                        if (running == 0) { p++; continue; }
                        st = running;
                    }
                    else
                    {
                        p++;
                        if (st < 0xF0) running = st;
                    }

                    if (st == 0xFF)
                    {
                        if (p >= end) break;
                        int type = data[p++];
                        int l = Vlq(data, ref p, end);
                        if (p + l > end) l = end - p;
                        if (type == 0x03 && tr.Name.Length == 0)
                            tr.Name = Encoding.UTF8.GetString(data, p, l);
                        else if (type == 0x51 && l == 3)
                            tempos.Add(new TempoPt { Tick = tick, Uspq = (data[p] << 16) | (data[p + 1] << 8) | data[p + 2] });
                        else if (type == 0x58 && l >= 2)
                            sigs.Add(new SigPt { Tick = tick, Num = Math.Max(1, (int)data[p]), Den = 1 << Math.Min((int)data[p + 1], 6) });
                        else if (type == 0x59 && l >= 2)
                            keyRaw.Add(new KeyPt { Tick = tick, Sf = Math.Max(-7, Math.Min(7, (int)(sbyte)data[p])), Minor = data[p + 1] != 0 });
                        p += l;
                    }
                    else if (st == 0xF0 || st == 0xF7)
                    {
                        int l = Vlq(data, ref p, end);
                        p += l;
                    }
                    else
                    {
                        int hi = st & 0xF0, ch = st & 0x0F;
                        if (p >= end) break;
                        int d1 = data[p++];
                        int d2 = 0;
                        if (hi != 0xC0 && hi != 0xD0)
                        {
                            if (p >= end) break;
                            d2 = data[p++];
                        }

                        if (hi == 0x90 && d2 > 0)
                        {
                            var n = new Note { Midi = d1, Vel = d2, Channel = ch, StartTick = tick, EndTick = tick, Track = tr.Index };
                            tr.Notes.Add(n);
                            int key = (ch << 7) | d1;
                            Queue<Note> q;
                            if (!open.TryGetValue(key, out q)) { q = new Queue<Note>(); open[key] = q; }
                            q.Enqueue(n);
                        }
                        else if (hi == 0x80 || hi == 0x90)
                        {
                            Queue<Note> q;
                            if (open.TryGetValue((ch << 7) | d1, out q) && q.Count > 0)
                                q.Dequeue().EndTick = tick;
                        }
                        else if (hi == 0xB0 || hi == 0xC0 || hi == 0xD0 || hi == 0xE0)
                        {
                            if (hi == 0xC0 && ch != 9 && tr.Program < 0) tr.Program = d1;
                            song.Controls.Add(new MidiEvt
                            {
                                Tick = tick, Status = (byte)st, D1 = (byte)d1, D2 = (byte)d2, Track = tr.Index, Prio = 1
                            });
                        }
                    }
                }
                foreach (var q in open.Values)
                    foreach (var n in q) n.EndTick = Math.Max(n.EndTick, tick);

                p = end;
                tr.Drums = tr.Notes.Count > 0 && tr.Notes.All(n => n.Channel == 9);
                song.Tracks.Add(tr);
            }

            // Tempo-Karte
            tempos.Sort((a, b) => a.Tick.CompareTo(b.Tick));
            if (tempos.Count == 0 || tempos[0].Tick > 0) tempos.Insert(0, new TempoPt { Tick = 0, Uspq = 500000 });
            bool smpte = (div & 0x8000) != 0;
            double tps = 0;
            if (smpte) tps = (256 - (div >> 8)) * (double)(div & 0xFF);
            double sec = 0;
            for (int i = 0; i < tempos.Count; i++)
            {
                if (i > 0) sec += (tempos[i].Tick - tempos[i - 1].Tick) * (tempos[i - 1].Uspq / 1e6 / div);
                tempos[i].Sec = sec;
            }
            Func<long, double> toSec = tick =>
            {
                if (smpte) return tick / tps;
                int lo = 0, hi = tempos.Count - 1;
                while (lo < hi)
                {
                    int mid = (lo + hi + 1) / 2;
                    if (tempos[mid].Tick <= tick) lo = mid; else hi = mid - 1;
                }
                var tp = tempos[lo];
                return tp.Sec + (tick - tp.Tick) * (tp.Uspq / 1e6 / div);
            };

            song.Bpm = 60e6 / tempos[0].Uspq;
            long totalTicks = 0;
            foreach (var tr in song.Tracks)
            {
                foreach (var n in tr.Notes)
                {
                    n.Start = toSec(n.StartTick);
                    n.End = Math.Max(toSec(n.EndTick), n.Start + 0.04);
                    n.Drum = tr.Drums;
                    totalTicks = Math.Max(totalTicks, n.EndTick);
                }
                if (tr.Notes.Count > 0) tr.MeanPitch = tr.Notes.Average(n => (double)n.Midi);
            }
            foreach (var c in song.Controls) c.Time = toSec(c.Tick);

            foreach (var k in keyRaw.OrderBy(k => k.Tick))
            {
                k.Time = toSec(k.Tick);
                song.Keys.Add(k);
            }
            if (!smpte)
                foreach (var tp in tempos)
                    song.Tempos.Add(new TempoInfo { Time = tp.Sec, Bpm = 60e6 / tp.Uspq });

            if (!song.Tracks.Any(t => t.Notes.Count > 0))
                throw new InvalidDataException("Die MIDI-Datei enthält keine Noten.");

            // Takt- und Schlaglinien
            sigs.Sort((a, b) => a.Tick.CompareTo(b.Tick));
            if (sigs.Count == 0 || sigs[0].Tick > 0) sigs.Insert(0, new SigPt { Tick = 0, Num = 4, Den = 4 });
            double beatsDiv = smpte ? 480 : div;
            for (int i = 0; i < sigs.Count; i++)
            {
                long segEnd = i + 1 < sigs.Count ? sigs[i + 1].Tick : totalTicks + 1;
                double tpb = beatsDiv * 4.0 / sigs[i].Den;
                int k = 0;
                for (double tk = sigs[i].Tick; tk < segEnd && song.Beats.Count < 200000; tk += tpb, k++)
                    song.Beats.Add(new Beat { Time = toSec((long)Math.Round(tk)), Bar = k % sigs[i].Num == 0 });
            }

            song.Finish();
            if (song.Keys.Count == 0) song.DetectKey();
            song.AutoAssignHands();
            return song;
        }

        /// <summary>Tempo (Viertel pro Minute) an der Zeit t, ohne Wiedergabe-Geschwindigkeit.</summary>
        public double BpmAt(double t)
        {
            double bpm = Bpm;
            foreach (var tp in Tempos)
            {
                if (tp.Time <= t + 1e-9) bpm = tp.Bpm; else break;
            }
            return bpm;
        }

        /// <summary>Vorzeichenanzahl (Kreuze &gt; 0, Bs &lt; 0) der Tonart an der Zeit t.</summary>
        public int KeySigAt(double t)
        {
            if (Keys.Count == 0) return 0;
            int sf = Keys[0].Sf;
            foreach (var k in Keys)
            {
                if (k.Time <= t + 1e-9) sf = k.Sf; else break;
            }
            return sf;
        }

        static readonly double[] ProfMajor = { 6.35, 2.23, 3.48, 2.33, 4.38, 4.09, 2.52, 5.19, 2.39, 3.66, 2.29, 2.88 };
        static readonly double[] ProfMinor = { 6.33, 2.68, 3.52, 5.38, 2.60, 3.53, 2.54, 4.75, 3.98, 2.69, 3.34, 3.17 };
        static readonly int[] MajorSf = { 0, -5, 2, -3, 4, -1, 6, 1, -4, 3, -2, 5 };

        static double Corr(double[] a, double[] b)
        {
            double ma = a.Average(), mb = b.Average(), num = 0, da = 0, db = 0;
            for (int i = 0; i < a.Length; i++)
            {
                num += (a[i] - ma) * (b[i] - mb);
                da += (a[i] - ma) * (a[i] - ma);
                db += (b[i] - mb) * (b[i] - mb);
            }
            return da <= 0 || db <= 0 ? 0 : num / Math.Sqrt(da * db);
        }

        /// <summary>Schätzt die Tonart aus den Noten (Krumhansl-Schmuckler), wenn die Datei keine angibt.</summary>
        public void DetectKey()
        {
            var h = new double[12];
            foreach (var tr in Tracks)
                foreach (var n in tr.Notes)
                    if (!n.Drum) h[n.Midi % 12] += Math.Min(n.End - n.Start, 2.0);
            if (h.Sum() <= 0) return;

            double best = double.MinValue;
            int bestSf = 0;
            bool bestMinor = false;
            for (int tonic = 0; tonic < 12; tonic++)
            {
                var rot = new double[12];
                for (int pc = 0; pc < 12; pc++) rot[pc] = h[(pc + tonic) % 12];
                double cMaj = Corr(rot, ProfMajor), cMin = Corr(rot, ProfMinor);
                if (cMaj > best) { best = cMaj; bestSf = MajorSf[tonic]; bestMinor = false; }
                if (cMin > best) { best = cMin; bestSf = MajorSf[(tonic + 3) % 12]; bestMinor = true; }
            }
            Keys.Clear();
            Keys.Add(new KeyPt { Time = 0, Sf = bestSf, Minor = bestMinor });
        }

        void Finish()
        {
            Duration = 0;
            foreach (var tr in Tracks)
                foreach (var n in tr.Notes)
                    if (n.End > Duration) Duration = n.End;
        }

        public void AutoAssignHands()
        {
            var playable = Tracks.Where(t => t.Notes.Count > 0 && !t.Drums).OrderByDescending(t => t.MeanPitch).ToList();
            foreach (var t in Tracks) t.Hand = 'B';
            if (playable.Count == 1) playable[0].Hand = 'A';
            else if (playable.Count >= 2)
            {
                playable[0].Hand = 'R';
                playable[1].Hand = 'L';
            }
        }

        /// <summary>Eingebautes Demo-Stück (Ode an die Freude), falls keine Datei geladen ist.</summary>
        public static Song CreateDemo()
        {
            var s = new Song { Title = "Demo: Ode an die Freude", Bpm = 100 };
            double q = 0.6;
            var right = new TrackInfo { Index = 0, Name = "Rechte Hand", Hand = 'R' };
            var left = new TrackInfo { Index = 1, Name = "Linke Hand", Hand = 'L' };
            double[][] mel =
            {
                new double[]{64,1},new double[]{64,1},new double[]{65,1},new double[]{67,1},
                new double[]{67,1},new double[]{65,1},new double[]{64,1},new double[]{62,1},
                new double[]{60,1},new double[]{60,1},new double[]{62,1},new double[]{64,1},
                new double[]{64,1.5},new double[]{62,0.5},new double[]{62,2},
                new double[]{64,1},new double[]{64,1},new double[]{65,1},new double[]{67,1},
                new double[]{67,1},new double[]{65,1},new double[]{64,1},new double[]{62,1},
                new double[]{60,1},new double[]{60,1},new double[]{62,1},new double[]{64,1},
                new double[]{62,1.5},new double[]{60,0.5},new double[]{60,2},
            };
            double t = 0;
            foreach (var m in mel)
            {
                right.Notes.Add(new Note { Midi = (int)m[0], Start = t * q, End = (t + m[1]) * q - 0.03, Vel = 90, Channel = 0, Track = 0 });
                t += m[1];
            }
            int[][] bass = { new[]{48,55}, new[]{48,55}, new[]{48,55}, new[]{43,50}, new[]{48,55}, new[]{48,55}, new[]{43,50}, new[]{48,55} };
            for (int bar = 0; bar < bass.Length; bar++)
            {
                left.Notes.Add(new Note { Midi = bass[bar][0], Start = bar * 4 * q, End = (bar * 4 + 2) * q - 0.03, Vel = 70, Channel = 0, Track = 1 });
                left.Notes.Add(new Note { Midi = bass[bar][1], Start = (bar * 4 + 2) * q, End = (bar * 4 + 4) * q - 0.03, Vel = 70, Channel = 0, Track = 1 });
            }
            s.Tracks.Add(right);
            s.Tracks.Add(left);
            for (int i = 0; i < 33; i++) s.Beats.Add(new Beat { Time = i * q, Bar = i % 4 == 0 });
            s.Tempos.Add(new TempoInfo { Time = 0, Bpm = 100 });
            s.Keys.Add(new KeyPt { Time = 0, Sf = 0 });
            s.Finish();
            return s;
        }
    }
}
