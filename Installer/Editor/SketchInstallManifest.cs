using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Sketch.Sdk.Installer
{
    /// <summary>
    /// `.sketch-install.txt`: line 1 = installed stamp (tag), then one `relative/path\tsha1` line per file the SDK owns.
    /// Paths are relative to the install root and use '/'.
    /// </summary>
    sealed class SketchInstallManifest
    {
        public string Stamp = string.Empty;
        public readonly Dictionary<string, string> Files = new Dictionary<string, string>(StringComparer.Ordinal);

        public static SketchInstallManifest Read(string path)
        {
            var manifest = new SketchInstallManifest();
            if (!File.Exists(path)) return manifest;
            string[] lines = File.ReadAllLines(path);
            if (lines.Length > 0) manifest.Stamp = lines[0].Trim();
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.Length == 0 || line[0] == '#') continue;
                int tab = line.LastIndexOf('\t');
                if (tab <= 0) manifest.Files[line.Trim()] = string.Empty;
                else manifest.Files[line.Substring(0, tab)] = line.Substring(tab + 1).Trim();
            }
            return manifest;
        }

        public void Write(string path)
        {
            var sb = new StringBuilder();
            sb.Append(Stamp).Append('\n');
            sb.Append("# Sketch SDK installer manifest. Files listed here are owned by the SDK and replaced on update.\n");
            var keys = new List<string>(Files.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (string key in keys) sb.Append(key).Append('\t').Append(Files[key]).Append('\n');
            File.WriteAllText(path, sb.ToString());
        }

        public static string Sha1(string filePath)
        {
            using (SHA1 sha = SHA1.Create())
            using (FileStream stream = File.OpenRead(filePath))
            {
                byte[] hash = sha.ComputeHash(stream);
                var sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
