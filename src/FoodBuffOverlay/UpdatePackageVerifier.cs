using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;

namespace FoodBuffOverlay
{
    internal static class UpdatePackageVerifier
    {
        public static string ComputeSha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        public static bool VerifySha256(string path, string expected) =>
            !string.IsNullOrWhiteSpace(expected) && string.Equals(ComputeSha256(path), expected.Trim(), StringComparison.OrdinalIgnoreCase);

        public static string FindManifestHash(string manifest, string assetName)
        {
            foreach (var raw in (manifest ?? string.Empty).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var line = raw.Trim();
                if (line.Length < 64) continue;
                var hash = line.Substring(0, 64);
                if (!hash.All(IsHex)) continue;
                var name = line.Substring(64).Trim().TrimStart('*');
                if (string.Equals(name, assetName, StringComparison.OrdinalIgnoreCase)) return hash.ToLowerInvariant();
            }
            return null;
        }

        public static string SafeDestination(string root, string entryName)
        {
            if (string.IsNullOrWhiteSpace(entryName) || Path.IsPathRooted(entryName)) throw new InvalidDataException("Invalid ZIP entry path.");
            var normalized = entryName.Replace('/', Path.DirectorySeparatorChar);
            var rootPath = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var destination = Path.GetFullPath(Path.Combine(rootPath, normalized));
            if (!destination.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("ZIP entry escapes the staging directory.");
            return destination;
        }

        public static void ExtractValidated(string zipPath, string destinationRoot)
        {
            Directory.CreateDirectory(destinationRoot);
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                if (archive.Entries.Count > 64) throw new InvalidDataException("Update package contains too many files.");
                foreach (var entry in archive.Entries)
                {
                    if (entry.Length > 50L * 1024 * 1024) throw new InvalidDataException("Update package entry is too large.");
                    var destination = SafeDestination(destinationRoot, entry.FullName);
                    if (string.IsNullOrEmpty(entry.Name)) { Directory.CreateDirectory(destination); continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    entry.ExtractToFile(destination, true);
                }
            }
        }

        public static void ValidatePluginAssembly(string dllPath, SemVersion releaseVersion)
        {
            var name = AssemblyName.GetAssemblyName(dllPath);
            if (!string.Equals(name.Name, UpdateConfiguration.PluginAssemblyName, StringComparison.Ordinal))
                throw new InvalidDataException("InvalidPackage");
            var actual = name.Version;
            if (releaseVersion == null || actual.Major != releaseVersion.Major || actual.Minor != releaseVersion.Minor || actual.Build != releaseVersion.Patch)
                throw new InvalidDataException("InvalidPackage");
        }

        private static bool IsHex(char value) => (value >= '0' && value <= '9') || (value >= 'a' && value <= 'f') || (value >= 'A' && value <= 'F');
    }
}
