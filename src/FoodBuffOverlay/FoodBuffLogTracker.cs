using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FoodBuffOverlay
{
    /// <summary>Tracks Well Fed status from live ACT lines and a bounded snapshot of the active log.</summary>
    internal sealed class FoodBuffLogTracker
    {
        private const uint WellFedStatusId = 48;
        internal const int MaximumReplayBytes = 64 * 1024 * 1024;
        internal const int ReplayTimeLimitMilliseconds = 3000;
        private const double MaximumStatusDurationSeconds = 7 * 24 * 60 * 60;
        private const int ReplayChunkBytes = 32 * 1024;
        private const int MaximumLineBytes = 256 * 1024;
        private const int MaximumTrackedTargets = 4096;
        private const int CleanupInterval = 128;
        private static readonly TimeSpan TargetRetention = TimeSpan.FromMinutes(30);
        private static readonly Encoding Utf8 = new UTF8Encoding(false, false);
        private readonly object sync = new object();
        private readonly Dictionary<uint, TargetState> states = new Dictionary<uint, TargetState>();
        private readonly LinkedList<uint> recency = new LinkedList<uint>();
        private uint playerId;
        private int eventsSinceCleanup;

        public void SetPlayerId(uint value)
        {
            if (value == 0) return;
            lock (sync)
            {
                playerId = value;
                TrimToLimit();
            }
        }

        public Task ReplayCurrentLogAsync(string path, CancellationToken cancellationToken) =>
            ReplayCurrentLogAsync(path, cancellationToken, MaximumReplayBytes, ReplayTimeLimitMilliseconds, null);

        internal async Task ReplayCurrentLogAsync(string path, CancellationToken cancellationToken,
            int maximumBytes, int timeLimitMilliseconds, Action afterBoundaryCaptured)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
            if (maximumBytes < 0) throw new ArgumentOutOfRangeException("maximumBytes");
            if (timeLimitMilliseconds <= 0) throw new ArgumentOutOfRangeException("timeLimitMilliseconds");

            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                deadline.CancelAfter(timeLimitMilliseconds);
                var replayToken = deadline.Token;
                try
                {
                    var timer = Stopwatch.StartNew();
                    using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete, ReplayChunkBytes, FileOptions.Asynchronous | FileOptions.SequentialScan))
                    {
                        // Snapshot the end once. Appends after this point are handled by OnLogLineRead, never read here.
                        var end = stream.Length;
                        var start = Math.Max(0L, end - maximumBytes);
                        var remaining = end - start;
                        var discardPartialLine = false;
                        if (start > 0)
                        {
                            stream.Position = start - 1;
                            discardPartialLine = stream.ReadByte() != (byte)'\n';
                        }
                        stream.Position = start;
                        afterBoundaryCaptured?.Invoke();

                        var readBuffer = new byte[ReplayChunkBytes];
                        var lineBuffer = new byte[MaximumLineBytes];
                        var lineLength = 0;
                        var discardLine = discardPartialLine;
                        while (remaining > 0 && timer.ElapsedMilliseconds < timeLimitMilliseconds)
                        {
                            replayToken.ThrowIfCancellationRequested();
                            var request = (int)Math.Min(readBuffer.Length, remaining);
                            var read = await stream.ReadAsync(readBuffer, 0, request, replayToken).ConfigureAwait(false);
                            if (read <= 0) break;
                            remaining -= read;

                            for (var i = 0; i < read; i++)
                            {
                                if ((i & 4095) == 0) replayToken.ThrowIfCancellationRequested();
                                var value = readBuffer[i];
                                if (value == (byte)'\n')
                                {
                                    if (!discardLine && lineLength > 0)
                                    {
                                        var count = lineBuffer[lineLength - 1] == (byte)'\r' ? lineLength - 1 : lineLength;
                                        if (count > 0) ProcessReplayLine(Utf8.GetString(lineBuffer, 0, count));
                                    }
                                    lineLength = 0;
                                    discardLine = false;
                                }
                                else if (!discardLine)
                                {
                                    if (lineLength < lineBuffer.Length) lineBuffer[lineLength++] = value;
                                    else { lineLength = 0; discardLine = true; }
                                }
                            }
                        }
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                }
                catch (OperationCanceledException)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!deadline.IsCancellationRequested) throw;
                    // The replay time budget elapsed; already parsed state remains usable.
                }
            }
        }

        public bool ProcessLine(string line) => ProcessLine(line, false);

        private void ProcessReplayLine(string line) => ProcessLine(line, true);

        private bool ProcessLine(string line, bool fromReplay)
        {
            StatusEvent statusEvent;
            if (!TryParseStatusLine(line, fromReplay, out statusEvent)) return false;
            lock (sync)
            {
                Apply(statusEvent);
                eventsSinceCleanup++;
                if (eventsSinceCleanup >= CleanupInterval)
                {
                    eventsSinceCleanup = 0;
                    PruneOldStates(DateTime.UtcNow);
                }
                TrimToLimit();
            }
            return true;
        }

        private void Apply(StatusEvent statusEvent)
        {
            TargetState existing;
            if (states.TryGetValue(statusEvent.TargetId, out existing))
            {
                if (statusEvent.TimestampUtc < existing.EventTimestampUtc) return;
                // Prefer a live observation over the same timestamp encountered in a delayed replay.
                if (statusEvent.TimestampUtc == existing.EventTimestampUtc && statusEvent.FromReplay && !existing.FromReplay) return;
                existing.IsActive = statusEvent.IsActive && statusEvent.ExpiryUtc > DateTime.UtcNow;
                existing.ExpiryUtc = statusEvent.ExpiryUtc;
                existing.EventTimestampUtc = statusEvent.TimestampUtc;
                existing.FromReplay = statusEvent.FromReplay;
                existing.LastSeenUtc = DateTime.UtcNow;
                recency.Remove(existing.Node);
                existing.Node = recency.AddLast(statusEvent.TargetId);
                return;
            }

            var state = new TargetState
            {
                IsActive = statusEvent.IsActive && statusEvent.ExpiryUtc > DateTime.UtcNow,
                ExpiryUtc = statusEvent.ExpiryUtc,
                EventTimestampUtc = statusEvent.TimestampUtc,
                FromReplay = statusEvent.FromReplay,
                LastSeenUtc = DateTime.UtcNow
            };
            state.Node = recency.AddLast(statusEvent.TargetId);
            states.Add(statusEvent.TargetId, state);
        }

        private void PruneOldStates(DateTime now)
        {
            var node = recency.First;
            while (node != null)
            {
                var next = node.Next;
                TargetState state;
                if (node.Value != playerId && states.TryGetValue(node.Value, out state) && now - state.LastSeenUtc > TargetRetention)
                {
                    states.Remove(node.Value);
                    recency.Remove(node);
                }
                node = next;
            }
        }

        private void TrimToLimit()
        {
            while (states.Count > MaximumTrackedTargets)
            {
                var node = recency.First;
                if (node != null && node.Value == playerId) node = node.Next;
                if (node == null) return;
                states.Remove(node.Value);
                recency.Remove(node);
            }
        }

        public FoodBuffState Resolve(FoodBuffState snapshot, uint currentPlayerId, int warningMinutes)
        {
            if (snapshot == null || snapshot.Condition == FoodBuffCondition.Unavailable || currentPlayerId == 0)
                return snapshot ?? FoodBuffState.Unavailable();

            lock (sync)
            {
                playerId = currentPlayerId;
                TrimToLimit();
                if (snapshot.Condition == FoodBuffCondition.Active || snapshot.Condition == FoodBuffCondition.Expiring)
                    return snapshot;
                TargetState state;
                if (!states.TryGetValue(currentPlayerId, out state) || !state.IsActive) return snapshot;
                var remaining = state.ExpiryUtc - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                {
                    state.IsActive = false;
                    return snapshot;
                }
                return FoodBuffState.FromRemaining(remaining, warningMinutes);
            }
        }

        internal int TrackedTargetCount { get { lock (sync) return states.Count; } }

        internal void PruneExpiredStates(DateTime nowUtc)
        {
            lock (sync) PruneOldStates(nowUtc);
        }

        private static bool TryParseStatusLine(string line, bool fromReplay, out StatusEvent statusEvent)
        {
            statusEvent = default(StatusEvent);
            if (string.IsNullOrEmpty(line)) return false;
            int typeStart, typeLength;
            if (!TryGetField(line, 0, out typeStart, out typeLength)) return false;
            if (typeStart < line.Length && line[typeStart] == '\uFEFF') { typeStart++; typeLength--; }
            var isAdd = typeLength == 2 && line[typeStart] == '2' && line[typeStart + 1] == '6';
            var isRemove = typeLength == 2 && line[typeStart] == '3' && line[typeStart + 1] == '0';
            if (!isAdd && !isRemove) return false;

            int effectStart, effectLength;
            uint effectId;
            if (!TryGetField(line, 2, out effectStart, out effectLength) ||
                !TryParseHex(line, effectStart, effectLength, out effectId) || effectId != WellFedStatusId) return false;
            int targetStart, targetLength;
            uint targetId;
            if (!TryGetField(line, 7, out targetStart, out targetLength) ||
                !TryParseHex(line, targetStart, targetLength, out targetId)) return false;

            int timestampStart, timestampLength;
            if (!TryGetField(line, 1, out timestampStart, out timestampLength)) return false;
            DateTime timestamp;
            if (!DateTime.TryParse(line.Substring(timestampStart, timestampLength), CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal, out timestamp))
                timestamp = fromReplay ? DateTime.MinValue : DateTime.Now;
            var timestampUtc = timestamp == DateTime.MinValue ? DateTime.MinValue : timestamp.ToUniversalTime();

            var expiry = DateTime.MinValue;
            if (isAdd)
            {
                int durationStart, durationLength;
                double duration;
                if (!TryGetField(line, 4, out durationStart, out durationLength) ||
                    !double.TryParse(line.Substring(durationStart, durationLength), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out duration) || double.IsNaN(duration) || double.IsInfinity(duration) ||
                    duration > MaximumStatusDurationSeconds) return false;
                try { expiry = timestampUtc.AddSeconds(Math.Max(0, duration)); }
                catch (ArgumentOutOfRangeException) { return false; }
            }

            statusEvent = new StatusEvent
            {
                TargetId = targetId,
                IsActive = isAdd,
                ExpiryUtc = expiry,
                TimestampUtc = timestampUtc,
                FromReplay = fromReplay
            };
            return true;
        }

        private static bool TryGetField(string line, int wantedField, out int start, out int length)
        {
            start = 0;
            for (var field = 0; field < wantedField; field++)
            {
                var separator = line.IndexOf('|', start);
                if (separator < 0) { length = 0; return false; }
                start = separator + 1;
            }
            var end = line.IndexOf('|', start);
            if (end < 0) end = line.Length;
            length = end - start;
            return length > 0;
        }

        private static bool TryParseHex(string value, int start, int length, out uint result)
        {
            result = 0;
            while (length > 0 && char.IsWhiteSpace(value[start])) { start++; length--; }
            while (length > 0 && char.IsWhiteSpace(value[start + length - 1])) length--;
            if (length >= 2 && value[start] == '0' && (value[start + 1] == 'x' || value[start + 1] == 'X')) { start += 2; length -= 2; }
            if (length == 0 || length > 8) return false;
            for (var i = 0; i < length; i++)
            {
                var c = value[start + i];
                var digit = c >= '0' && c <= '9' ? c - '0' : c >= 'a' && c <= 'f' ? c - 'a' + 10 : c >= 'A' && c <= 'F' ? c - 'A' + 10 : -1;
                if (digit < 0) return false;
                result = (result << 4) | (uint)digit;
            }
            return true;
        }

        private struct StatusEvent
        {
            public uint TargetId;
            public bool IsActive;
            public DateTime ExpiryUtc;
            public DateTime TimestampUtc;
            public bool FromReplay;
        }

        private sealed class TargetState
        {
            public bool IsActive;
            public DateTime ExpiryUtc;
            public DateTime EventTimestampUtc;
            public bool FromReplay;
            public DateTime LastSeenUtc;
            public LinkedListNode<uint> Node;
        }
    }
}
