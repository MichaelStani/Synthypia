using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Synthypia
{
    /// <summary>
    /// Client für den NIHardwareConnectionService (ODR-Agent) von Native Instruments.
    /// Der Agent lauscht auf einem AF_UNIX-Socket; .NET Framework kennt diesen Typ nicht, daher direkt über Winsock.
    /// Transport: 16 Roh-Bytes Client-UUID, danach Frames aus uint32-LE-Länge + msgpack.
    /// </summary>
    sealed class OdrClient : IDisposable
    {
        const int AF_UNIX = 1, SOCK_STREAM = 1, SOL_SOCKET = 0xffff, SO_RCVTIMEO = 0x1006, SO_SNDTIMEO = 0x1005;
        const uint FIONREAD = 0x4004667F;
        static readonly IntPtr InvalidSocket = new IntPtr(-1);

        [DllImport("ws2_32.dll")] static extern int WSAStartup(ushort version, byte[] data);
        [DllImport("ws2_32.dll")] static extern IntPtr socket(int af, int type, int protocol);
        [DllImport("ws2_32.dll")] static extern int connect(IntPtr s, byte[] addr, int len);
        [DllImport("ws2_32.dll")] static extern int send(IntPtr s, byte[] buf, int len, int flags);
        [DllImport("ws2_32.dll")] static extern int recv(IntPtr s, byte[] buf, int len, int flags);
        [DllImport("ws2_32.dll")] static extern int closesocket(IntPtr s);
        [DllImport("ws2_32.dll")] static extern int setsockopt(IntPtr s, int level, int name, ref int val, int len);
        [DllImport("ws2_32.dll")] static extern int ioctlsocket(IntPtr s, uint cmd, ref uint arg);

        static bool wsaReady;

        public static string SocketPath
        {
            get
            {
                string docs = Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments);
                return Path.Combine(docs, "Native Instruments", "com.native-instruments.odr_agent.kks");
            }
        }

        IntPtr sock = InvalidSocket;
        readonly Guid id = Guid.NewGuid();
        int msgId;
        List<object> registry = new List<object>();
        Dictionary<object, object> hello = new Dictionary<object, object>();

        public string Serial = "";
        public string Product = "";
        public string AgentVersion = "";

        public OdrClient()
        {
            if (!wsaReady)
            {
                WSAStartup(0x0202, new byte[512]);
                wsaReady = true;
            }
            string path = SocketPath;
            if (!File.Exists(path)) throw new IOException("NI Hardware Connection Service nicht gefunden (Native Access / Komplete Kontrol installiert?)");

            sock = socket(AF_UNIX, SOCK_STREAM, 0);
            if (sock == InvalidSocket) throw new IOException("AF_UNIX-Socket nicht verfügbar (Windows 10 1803 oder neuer nötig)");
            int timeout = 3000;
            setsockopt(sock, SOL_SOCKET, SO_RCVTIMEO, ref timeout, 4);
            setsockopt(sock, SOL_SOCKET, SO_SNDTIMEO, ref timeout, 4);

            var addr = new byte[110];                       // sockaddr_un: ushort family + char[108] path
            addr[0] = AF_UNIX;
            var pb = Encoding.UTF8.GetBytes(path);
            if (pb.Length > 107) throw new IOException("Socket-Pfad zu lang");
            Buffer.BlockCopy(pb, 0, addr, 2, pb.Length);
            if (connect(sock, addr, addr.Length) != 0)
            {
                Dispose();
                throw new IOException("Verbindung zum NI-Agent fehlgeschlagen (Dienst läuft?)");
            }
            SendAll(UuidBytes(id));                          // Roh-Bytes, ungerahmt, vor allem anderen
        }

        /// <summary>UUID in RFC-Reihenfolge (Big Endian), wie sie der Agent erwartet.</summary>
        static byte[] UuidBytes(Guid g)
        {
            var b = g.ToByteArray();                         // .NET: erste drei Gruppen little-endian
            Array.Reverse(b, 0, 4);
            Array.Reverse(b, 4, 2);
            Array.Reverse(b, 6, 2);
            return b;
        }

        void SendAll(byte[] data)
        {
            int off = 0;
            while (off < data.Length)
            {
                var chunk = off == 0 ? data : Slice(data, off);
                int n = send(sock, chunk, chunk.Length, 0);
                if (n <= 0) throw new IOException("Senden an den NI-Agent fehlgeschlagen");
                off += n;
            }
        }

        static byte[] Slice(byte[] d, int off)
        {
            var r = new byte[d.Length - off];
            Buffer.BlockCopy(d, off, r, 0, r.Length);
            return r;
        }

        void SendFrame(object[] msg)
        {
            var body = Msgpack.Pack(msg);
            var frame = new byte[4 + body.Length];
            BitConverter.GetBytes((uint)body.Length).CopyTo(frame, 0);
            Buffer.BlockCopy(body, 0, frame, 4, body.Length);
            SendAll(frame);
        }

        byte[] ReadExact(int n)
        {
            var buf = new byte[n];
            int off = 0;
            while (off < n)
            {
                var tmp = new byte[n - off];
                int r = recv(sock, tmp, tmp.Length, 0);
                if (r <= 0) throw new IOException("Keine Antwort vom NI-Agent");
                Buffer.BlockCopy(tmp, 0, buf, off, r);
                off += r;
            }
            return buf;
        }

        object ReadMessage()
        {
            uint len = BitConverter.ToUInt32(ReadExact(4), 0);
            if (len > 64u * 1024 * 1024) throw new IOException("Ungültiger Frame vom NI-Agent");
            return Msgpack.Unpack(ReadExact((int)len));
        }

        /// <summary>Wird mit Fehlerantworten des Agents aufgerufen (z. B. "Arguments cannot be parsed").</summary>
        public Action<string> OnAgentError;

        /// <summary>Liest eingehende Nachrichten, damit der Socket nie volläuft; meldet Fehlerantworten. Blockiert nicht, solange nichts ansteht.</summary>
        public void Drain()
        {
            uint avail = 0;
            while (ioctlsocket(sock, FIONREAD, ref avail) == 0 && avail >= 4)
            {
                var m = ReadMessage() as List<object>;
                if (m != null && m.Count >= 3 && Convert.ToInt64(m[0]) == 1 && m[2] != null && OnAgentError != null)
                {
                    string s = Convert.ToString(m[2]);
                    OnAgentError(s.Length > 160 ? s.Substring(0, 160) + " …" : s);
                }
                avail = 0;
            }
        }

        List<object> Request(object method, object[] args)
        {
            int mid = ++msgId;
            SendFrame(new object[] { 0, mid, method, args });
            for (int guard = 0; guard < 200; guard++)
            {
                var m = ReadMessage() as List<object>;
                if (m != null && m.Count >= 4 && Convert.ToInt64(m[0]) == 1 && Convert.ToInt64(m[1]) == mid) return m;
            }
            throw new IOException("Keine Antwort auf Anfrage");
        }

        void Notify(object method, object[] args)
        {
            SendFrame(new object[] { 2, method, args });
        }

        int Sym(string name)
        {
            int i = registry.IndexOf(name);
            if (i < 0) throw new KeyNotFoundException("Methode " + name + " fehlt in der Registry des NI-Agents");
            return i;
        }

        /// <summary>Hello, Gerät verbinden und Light-Guide-Fokus anfordern.</summary>
        public void Connect()
        {
            var info = new Dictionary<string, object>
            {
                { "client_info", new Dictionary<string, object> { { "name", "Synthypia" }, { "version", "1.0" }, { "type", "standalone" } } },
                { "ipc_protocol_version", "2.1.0" }
            };
            var r = Request("instance_hello", new object[] { id.ToString(), info });
            if (r[2] != null) throw new IOException("NI-Agent lehnt die Anmeldung ab");
            hello = (Dictionary<object, object>)r[3];

            object o;
            if (hello.TryGetValue("agent_version", out o) && o != null) AgentVersion = o.ToString();
            registry = hello.TryGetValue("symbol_registry", out o) ? o as List<object> : null;
            if (registry == null) throw new IOException("NI-Agent liefert keine Methoden-Registry (unbekannte Version)");

            var devs = hello.TryGetValue("available_devices", out o) ? o as List<object> : null;
            if (devs == null || devs.Count == 0) throw new IOException("Kein NI-Keyboard angeschlossen");
            var dev = (Dictionary<object, object>)devs[0];
            Serial = Convert.ToString(dev["serialnumber"]);
            Product = dev.TryGetValue("product", out o) ? Convert.ToString(o) : "";

            var c = Request(Sym("connect_device"), new object[] { id.ToString(), Serial });
            if (c[2] != null) throw new IOException("Gerät konnte nicht verbunden werden");
            Notify(Sym("client_request_focus"), new object[] { id.ToString(), Serial });
            Thread.Sleep(300);
        }

        /// <summary>colors[note] für MIDI-Note 0..127; 0 = aus.</summary>
        public void SetLeds(byte[] colors)
        {
            var arr = new List<object>(128);
            for (int n = 0; n < 128; n++) arr.Add(new object[] { n, (int)colors[n] });
            var map = new Dictionary<object, object> { { Sym("client_midi_addressing"), arr } };
            Notify(Sym("client_lightguide_set_leds"), new object[] { id.ToString(), Serial, map });
        }

        public void Dispose()
        {
            if (sock != InvalidSocket) { closesocket(sock); sock = InvalidSocket; }
        }
    }
}
