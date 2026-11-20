using System;
using System.Text.RegularExpressions;

namespace Sketch.Sdk.Installer
{
    readonly struct SketchSemVer : IComparable<SketchSemVer>
    {
        static readonly Regex Pattern = new Regex(@"^v?(\d+)\.(\d+)\.(\d+)$", RegexOptions.CultureInvariant);

        public readonly int Major;
        public readonly int Minor;
        public readonly int Patch;

        SketchSemVer(int major, int minor, int patch)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
        }

        public string Tag => "v" + this;

        public static bool TryParse(string text, out SketchSemVer version)
        {
            version = default;
            if (string.IsNullOrEmpty(text)) return false;
            Match m = Pattern.Match(text.Trim());
            if (!m.Success) return false;
            version = new SketchSemVer(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value));
            return true;
        }

        public int CompareTo(SketchSemVer other)
        {
            if (Major != other.Major) return Major.CompareTo(other.Major);
            if (Minor != other.Minor) return Minor.CompareTo(other.Minor);
            return Patch.CompareTo(other.Patch);
        }

        public override string ToString() => Major + "." + Minor + "." + Patch;
    }
}
