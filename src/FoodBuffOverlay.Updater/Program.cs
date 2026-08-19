using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace FoodBuffOverlay.Updater
{
    internal static class Program
    {
        private const string PluginFileName = "FoodBuffOverlay.dll";

        private static int Main(string[] args)
        {
            try
            {
                var values = Parse(args);
                var processId = int.Parse(Required(values, "--wait-pid"));
                var source = Path.GetFullPath(Required(values, "--source"));
                var target = Path.GetFullPath(Required(values, "--target"));
                var expectedHash = Required(values, "--sha256");
                var version = Required(values, "--version");
                ValidatePaths(source, target);
                if (!string.Equals(Hash(source), expectedHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Staged DLL hash mismatch.");
                WaitForExit(processId);
                ReplaceWithBackup(source, target, expectedHash, version);
                return 0;
            }
            catch (Exception ex)
            {
                try
                {
                    var log = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FoodBuffOverlay.update-error.log");
                    File.AppendAllText(log, DateTime.Now.ToString("O") + " " + ex + Environment.NewLine);
                }
                catch { }
                return 1;
            }
        }

        internal static void ReplaceWithBackup(string source, string target, string expectedHash, string version)
        {
            ReplaceWithBackup(source, target, expectedHash, version, (incoming, destination) => File.Copy(incoming, destination, true));
        }

        internal static void ReplaceWithBackup(string source, string target, string expectedHash, string version, Action<string, string> installCopy)
        {
            var directory = Path.GetDirectoryName(target);
            Directory.CreateDirectory(directory);
            var backup = target + ".backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            if (File.Exists(backup)) backup += "-" + Guid.NewGuid().ToString("N");
            var incoming = target + ".update-new";
            if (File.Exists(incoming)) File.Delete(incoming);
            File.Copy(source, incoming, true);
            if (!string.Equals(Hash(incoming), expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Copied DLL hash mismatch.");
            var hadOriginal = File.Exists(target);
            if (hadOriginal) File.Copy(target, backup, false);
            try
            {
                installCopy(incoming, target);
                if (!string.Equals(Hash(target), expectedHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Installed DLL hash mismatch.");
                File.WriteAllText(Path.Combine(directory, "FoodBuffOverlay.update-result.txt"),
                    "Updated to " + version + " at " + DateTime.Now.ToString("O") + Environment.NewLine +
                    (hadOriginal ? "Backup: " + backup : "No previous DLL was present."));
            }
            catch
            {
                if (hadOriginal && File.Exists(backup)) File.Copy(backup, target, true);
                throw;
            }
            finally
            {
                if (File.Exists(incoming)) File.Delete(incoming);
            }
        }

        private static void ValidatePaths(string source, string target)
        {
            if (!File.Exists(source)) throw new FileNotFoundException("Staged DLL was not found.", source);
            if (!string.Equals(Path.GetFileName(source), PluginFileName, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetFileName(target), PluginFileName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Updater only accepts FoodBuffOverlay.dll.");
            if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Source and target must be different files.");
        }

        private static void WaitForExit(int processId)
        {
            try
            {
                using (var process = Process.GetProcessById(processId))
                {
                    process.WaitForExit();
                }
            }
            catch (ArgumentException) { }
        }

        private static Dictionary<string, string> Parse(string[] args)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i + 1 < args.Length; i += 2) result[args[i]] = args[i + 1];
            return result;
        }

        private static string Required(Dictionary<string, string> values, string key)
        {
            string value;
            if (!values.TryGetValue(key, out value) || string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Missing argument " + key);
            return value;
        }

        private static string Hash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}
