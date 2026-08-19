using Advanced_Combat_Tracker;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace FoodBuffOverlay
{
    /// <summary>
    /// NetworkBuffs can be empty when this plugin is enabled midway through a session.
    /// Replaying the active ACT log and following status add/remove lines provides a
    /// second source without requiring OverlayPlugin or another dependency.
    /// </summary>
    internal sealed class FoodBuffLogTracker
    {
        private const uint WellFedStatusId = 48;
        private const int ReplayBytes = 64 * 1024 * 1024;
        private readonly object sync = new object();
        private readonly Dictionary<uint, DateTime> expiryByTarget = new Dictionary<uint, DateTime>();

        public void ReplayCurrentLog()
        {
            try
            {
                var path = ActGlobals.oFormActMain.LogFilePath;
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    var start = Math.Max(0, stream.Length - ReplayBytes);
                    stream.Seek(start, SeekOrigin.Begin);
                    using (var reader = new StreamReader(stream))
                    {
                        if (start > 0) reader.ReadLine(); // discard the first partial line
                        string line;
                        while ((line = reader.ReadLine()) != null) ProcessLine(line);
                    }
                }
            }
            catch
            {
                // The public snapshot remains available if log replay is unavailable.
            }
        }

        public bool ProcessLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line) || line.IndexOf('|') < 0) return false;
            var fields = line.Split('|');
            if (fields.Length < 9) return false;
            var type = fields[0].Trim();
            if (type != "26" && type != "30") return false;

            uint effectId;
            uint targetId;
            if (!uint.TryParse(fields[2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out effectId) ||
                effectId != WellFedStatusId ||
                !uint.TryParse(fields[7], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out targetId))
                return false;

            lock (sync)
            {
                if (type == "30")
                {
                    expiryByTarget.Remove(targetId);
                    return true;
                }

                double duration;
                DateTime timestamp;
                if (!double.TryParse(fields[4], NumberStyles.Float, CultureInfo.InvariantCulture, out duration))
                    return false;
                if (!DateTime.TryParse(fields[1], CultureInfo.InvariantCulture,
                        DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal, out timestamp))
                    timestamp = DateTime.Now;
                expiryByTarget[targetId] = timestamp.ToUniversalTime().AddSeconds(Math.Max(0, duration));
                return true;
            }
        }

        public FoodBuffState Resolve(FoodBuffState snapshot, uint playerId, int warningMinutes)
        {
            if (snapshot == null || snapshot.Condition == FoodBuffCondition.Unavailable || playerId == 0)
                return snapshot ?? FoodBuffState.Unavailable();
            if (snapshot.Condition == FoodBuffCondition.Active || snapshot.Condition == FoodBuffCondition.Expiring)
                return snapshot;

            lock (sync)
            {
                DateTime expiry;
                if (!expiryByTarget.TryGetValue(playerId, out expiry)) return snapshot;
                var remaining = expiry - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                {
                    expiryByTarget.Remove(playerId);
                    return snapshot;
                }
                return FoodBuffState.FromRemaining(remaining, warningMinutes);
            }
        }
    }
}
