using System;
using System.IO;
using System.Linq;

namespace FoodBuffOverlay
{
    internal sealed class SettingsPersistence
    {
        internal const int AutosaveIntervalMilliseconds = 60_000;
        private readonly object gate = new object();
        private readonly string path;
        private readonly Action<string, byte[]> write;
        private byte[] lastSaved;
        private int successfulWrites;

        public SettingsPersistence(string path, PluginSettings loadedSettings)
            : this(path, loadedSettings, AtomicSettingsFile.Write)
        {
        }

        internal SettingsPersistence(string path, PluginSettings loadedSettings, Action<string, byte[]> write)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A settings path is required.", "path");
            this.path = path;
            this.write = write ?? throw new ArgumentNullException("write");
            lastSaved = PluginSettings.Serialize(loadedSettings ?? throw new ArgumentNullException("loadedSettings"));
        }

        public int SuccessfulWrites { get { lock (gate) return successfulWrites; } }
        public bool IsDirty(PluginSettings settings)
        {
            var current = PluginSettings.Serialize(settings);
            lock (gate) return !BytesEqual(lastSaved, current);
        }

        public bool SaveIfChanged(PluginSettings settings)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            lock (gate)
            {
                var current = PluginSettings.Serialize(settings);
                if (BytesEqual(lastSaved, current)) return false;
                write(path, current);
                lastSaved = current;
                successfulWrites++;
                return true;
            }
        }

        private static bool BytesEqual(byte[] left, byte[] right) =>
            left != null && right != null && left.Length == right.Length && left.SequenceEqual(right);
    }

    internal static class AtomicSettingsFile
    {
        public static void Write(string path, byte[] contents)
        {
            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);
            Directory.CreateDirectory(directory);
            var temporary = fullPath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(contents, 0, contents.Length);
                    stream.Flush(true);
                }

                if (File.Exists(fullPath)) File.Replace(temporary, fullPath, null);
                else File.Move(temporary, fullPath);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }
}
