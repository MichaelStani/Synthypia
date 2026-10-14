using System;
using System.IO;
using System.Text;

namespace Synthypia
{
    /// <summary>Legt den Zustand eines Plugins (gewählter Klang, geladene Instrumente) pro Plugin-Datei unter %APPDATA%\Synthypia\plugins ab.</summary>
    static class PluginState
    {
        static string FileFor(string pluginPath)
        {
            string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Synthypia", "plugins");
            uint hash = 2166136261;
            foreach (char c in pluginPath.ToLowerInvariant()) hash = (hash ^ c) * 16777619;
            string name = System.IO.Path.GetFileNameWithoutExtension(pluginPath);
            var sb = new StringBuilder();
            foreach (char c in name) sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            return System.IO.Path.Combine(dir, sb + "_" + hash.ToString("x8") + ".state");
        }

        static string LastFile()
        {
            return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(FileFor("x")), "last-plugin.txt");
        }

        /// <summary>Pfad des zuletzt verwendeten Plugins oder null, wenn zuletzt ein MIDI-Gerät aktiv war.</summary>
        public static string LoadLast()
        {
            try
            {
                string f = LastFile();
                if (!File.Exists(f)) return null;
                string p = File.ReadAllText(f, Encoding.UTF8).Trim();
                return p.Length > 0 && File.Exists(p) ? p : null;
            }
            catch { return null; }
        }

        public static void SaveLast(string pluginPath)
        {
            try
            {
                string f = LastFile();
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(f));
                File.WriteAllText(f, pluginPath, Encoding.UTF8);
            }
            catch { }
        }

        static string MidiInFile()
        {
            return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(FileFor("x")), "midi-input.txt");
        }

        /// <summary>Name des zuletzt gewählten MIDI-Eingangs oder null (keiner gewählt).</summary>
        public static string LoadMidiIn()
        {
            try
            {
                string f = MidiInFile();
                if (!File.Exists(f)) return null;
                string n = File.ReadAllText(f, Encoding.UTF8).Trim();
                return n.Length > 0 ? n : null;
            }
            catch { return null; }
        }

        /// <summary>Merkt den MIDI-Eingang (null oder leer = keiner).</summary>
        public static void SaveMidiIn(string name)
        {
            try
            {
                string f = MidiInFile();
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(f));
                File.WriteAllText(f, name ?? "", Encoding.UTF8);
            }
            catch { }
        }

        public static void ClearLast()
        {
            try { File.Delete(LastFile()); } catch { }
        }

        public static byte[] Load(string pluginPath)
        {
            try
            {
                string f = FileFor(pluginPath);
                return File.Exists(f) ? File.ReadAllBytes(f) : null;
            }
            catch { return null; }
        }

        public static void Save(string pluginPath, byte[] data)
        {
            try
            {
                string f = FileFor(pluginPath);
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(f));
                string tmp = f + ".tmp";
                File.WriteAllBytes(tmp, data);
                if (File.Exists(f)) File.Delete(f);
                File.Move(tmp, f);
            }
            catch { }
        }
    }
}
