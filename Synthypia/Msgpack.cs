using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Synthypia
{
    /// <summary>Minimaler msgpack-Encoder/-Decoder (nil, bool, int, str, bin, array, map) für das ODR-Protokoll.</summary>
    static class Msgpack
    {
        public static byte[] Pack(object o)
        {
            var ms = new MemoryStream();
            Write(ms, o);
            return ms.ToArray();
        }

        static void BE(Stream s, ulong v, int n)
        {
            for (int i = n - 1; i >= 0; i--) s.WriteByte((byte)(v >> (8 * i)));
        }

        static void Write(Stream s, object o)
        {
            if (o == null) { s.WriteByte(0xc0); return; }
            if (o is bool) { s.WriteByte((bool)o ? (byte)0xc3 : (byte)0xc2); return; }

            var str = o as string;
            if (str != null)
            {
                var sb = Encoding.UTF8.GetBytes(str);
                if (sb.Length < 32) s.WriteByte((byte)(0xa0 | sb.Length));
                else if (sb.Length < 256) { s.WriteByte(0xd9); s.WriteByte((byte)sb.Length); }
                else if (sb.Length < 65536) { s.WriteByte(0xda); BE(s, (ulong)sb.Length, 2); }
                else { s.WriteByte(0xdb); BE(s, (ulong)sb.Length, 4); }
                s.Write(sb, 0, sb.Length);
                return;
            }

            var bin = o as byte[];
            if (bin != null)
            {
                if (bin.Length < 256) { s.WriteByte(0xc4); s.WriteByte((byte)bin.Length); }
                else { s.WriteByte(0xc6); BE(s, (ulong)bin.Length, 4); }
                s.Write(bin, 0, bin.Length);
                return;
            }

            if (o is int || o is long || o is short || o is byte || o is uint || o is ushort)
            {
                long v = Convert.ToInt64(o);
                if (v >= 0 && v < 128) s.WriteByte((byte)v);
                else if (v < 0 && v >= -32) s.WriteByte((byte)v);
                else if (v >= 0 && v < 256) { s.WriteByte(0xcc); s.WriteByte((byte)v); }
                else if (v >= 0 && v < 65536) { s.WriteByte(0xcd); BE(s, (ulong)v, 2); }
                else if (v >= 0 && v <= uint.MaxValue) { s.WriteByte(0xce); BE(s, (ulong)v, 4); }
                else { s.WriteByte(0xd3); BE(s, (ulong)v, 8); }
                return;
            }

            var dict = o as IDictionary;
            if (dict != null)
            {
                if (dict.Count < 16) s.WriteByte((byte)(0x80 | dict.Count));
                else { s.WriteByte(0xde); BE(s, (ulong)dict.Count, 2); }
                foreach (DictionaryEntry e in dict) { Write(s, e.Key); Write(s, e.Value); }
                return;
            }

            var en = o as IEnumerable;
            if (en != null)
            {
                var items = en.Cast<object>().ToList();
                if (items.Count < 16) s.WriteByte((byte)(0x90 | items.Count));
                else if (items.Count < 65536) { s.WriteByte(0xdc); BE(s, (ulong)items.Count, 2); }
                else { s.WriteByte(0xdd); BE(s, (ulong)items.Count, 4); }
                foreach (var i in items) Write(s, i);
                return;
            }

            throw new NotSupportedException(o.GetType().ToString());
        }

        public static object Unpack(byte[] data)
        {
            int p = 0;
            return Read(data, ref p);
        }

        static ulong U(byte[] d, ref int p, int n)
        {
            ulong v = 0;
            for (int i = 0; i < n; i++) v = (v << 8) | d[p++];
            return v;
        }

        static object Read(byte[] d, ref int p)
        {
            byte t = d[p++];
            if (t < 0x80) return (long)t;
            if (t >= 0xe0) return (long)(sbyte)t;
            if (t >= 0xa0 && t <= 0xbf) return Str(d, ref p, t & 0x1f);
            if (t >= 0x90 && t <= 0x9f) return Arr(d, ref p, t & 0x0f);
            if (t >= 0x80 && t <= 0x8f) return Map(d, ref p, t & 0x0f);
            switch (t)
            {
                case 0xc0: return null;
                case 0xc2: return false;
                case 0xc3: return true;
                case 0xc4: return Bin(d, ref p, (int)U(d, ref p, 1));
                case 0xc5: return Bin(d, ref p, (int)U(d, ref p, 2));
                case 0xc6: return Bin(d, ref p, (int)U(d, ref p, 4));
                case 0xca: return (double)BitConverter.ToSingle(BitConverter.GetBytes((int)U(d, ref p, 4)), 0);
                case 0xcb: return BitConverter.Int64BitsToDouble((long)U(d, ref p, 8));
                case 0xcc: return (long)U(d, ref p, 1);
                case 0xcd: return (long)U(d, ref p, 2);
                case 0xce: return (long)U(d, ref p, 4);
                case 0xcf: return (long)U(d, ref p, 8);
                case 0xd0: return (long)(sbyte)U(d, ref p, 1);
                case 0xd1: return (long)(short)U(d, ref p, 2);
                case 0xd2: return (long)(int)U(d, ref p, 4);
                case 0xd3: return (long)U(d, ref p, 8);
                case 0xd9: return Str(d, ref p, (int)U(d, ref p, 1));
                case 0xda: return Str(d, ref p, (int)U(d, ref p, 2));
                case 0xdb: return Str(d, ref p, (int)U(d, ref p, 4));
                case 0xdc: return Arr(d, ref p, (int)U(d, ref p, 2));
                case 0xdd: return Arr(d, ref p, (int)U(d, ref p, 4));
                case 0xde: return Map(d, ref p, (int)U(d, ref p, 2));
                case 0xdf: return Map(d, ref p, (int)U(d, ref p, 4));
                default: throw new NotSupportedException("msgpack type 0x" + t.ToString("x2"));
            }
        }

        static byte[] Bin(byte[] d, ref int p, int n)
        {
            var b = new byte[n];
            Buffer.BlockCopy(d, p, b, 0, n);
            p += n;
            return b;
        }

        static string Str(byte[] d, ref int p, int n)
        {
            var s = Encoding.UTF8.GetString(d, p, n);
            p += n;
            return s;
        }

        static List<object> Arr(byte[] d, ref int p, int n)
        {
            var l = new List<object>(n);
            for (int i = 0; i < n; i++) l.Add(Read(d, ref p));
            return l;
        }

        static Dictionary<object, object> Map(byte[] d, ref int p, int n)
        {
            var m = new Dictionary<object, object>();
            for (int i = 0; i < n; i++)
            {
                var k = Read(d, ref p);
                m[k] = Read(d, ref p);
            }
            return m;
        }
    }
}
