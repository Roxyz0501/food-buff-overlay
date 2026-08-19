using System;
using System.Text.RegularExpressions;

namespace FoodBuffOverlay
{
    internal sealed class SemVersion : IComparable<SemVersion>
    {
        private static readonly Regex Pattern = new Regex(
            @"^[vV]?(\d+)\.(\d+)\.(\d+)(?:-([0-9A-Za-z.-]+))?(?:\+[0-9A-Za-z.-]+)?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public int Major { get; private set; }
        public int Minor { get; private set; }
        public int Patch { get; private set; }
        public string PreRelease { get; private set; }
        public bool IsStable => string.IsNullOrEmpty(PreRelease);

        public static bool TryParse(string value, out SemVersion version)
        {
            version = null;
            var match = Pattern.Match((value ?? string.Empty).Trim());
            int major, minor, patch;
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out major) ||
                !int.TryParse(match.Groups[2].Value, out minor) || !int.TryParse(match.Groups[3].Value, out patch))
                return false;
            version = new SemVersion { Major = major, Minor = minor, Patch = patch, PreRelease = match.Groups[4].Value };
            return true;
        }

        public int CompareTo(SemVersion other)
        {
            if (other == null) return 1;
            var result = Major.CompareTo(other.Major);
            if (result == 0) result = Minor.CompareTo(other.Minor);
            if (result == 0) result = Patch.CompareTo(other.Patch);
            if (result != 0) return result;
            if (IsStable && !other.IsStable) return 1;
            if (!IsStable && other.IsStable) return -1;
            return ComparePreRelease(PreRelease, other.PreRelease);
        }

        private static int ComparePreRelease(string left, string right)
        {
            if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase)) return 0;
            var a = (left ?? string.Empty).Split('.');
            var b = (right ?? string.Empty).Split('.');
            for (var i = 0; i < Math.Max(a.Length, b.Length); i++)
            {
                if (i >= a.Length) return -1;
                if (i >= b.Length) return 1;
                int ai, bi;
                var an = int.TryParse(a[i], out ai);
                var bn = int.TryParse(b[i], out bi);
                int result;
                if (an && bn) result = ai.CompareTo(bi);
                else if (an != bn) result = an ? -1 : 1;
                else result = string.Compare(a[i], b[i], StringComparison.OrdinalIgnoreCase);
                if (result != 0) return result;
            }
            return 0;
        }

        public override string ToString() => Major + "." + Minor + "." + Patch + (IsStable ? "" : "-" + PreRelease);
    }
}
