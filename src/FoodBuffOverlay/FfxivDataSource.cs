using Advanced_Combat_Tracker;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;

namespace FoodBuffOverlay
{
    internal sealed class FfxivDataSource
    {
        private const ushort WellFedStatusId = 48;
        private object repository;
        private MethodInfo getPlayerId;
        private MethodInfo getCombatants;
        private MethodInfo getServerTimestamp;

        public string LastError { get; private set; }
        public string LastErrorKey { get; private set; }
        public uint CurrentPlayerId { get; private set; }

        public FoodBuffState Read(int warningMinutes)
        {
            if (repository == null && !TryConnect()) return FoodBuffState.Unavailable();
            try
            {
                var playerId = Convert.ToUInt32(getPlayerId.Invoke(repository, null));
                CurrentPlayerId = playerId;
                var combatants = getCombatants.Invoke(repository, null) as IEnumerable;
                if (playerId == 0 || combatants == null) return FoodBuffState.Unavailable();

                object player = null;
                foreach (var combatant in combatants)
                {
                    if (combatant != null && ReadUInt(combatant, "ID") == playerId)
                    {
                        player = combatant;
                        break;
                    }
                }
                if (player == null) return FoodBuffState.Unavailable();

                var buffs = ReadMember(player, "NetworkBuffs") as IEnumerable;
                if (buffs == null) throw new MissingMemberException(player.GetType().FullName, "NetworkBuffs");
                foreach (var buff in buffs)
                {
                    if (buff == null || ReadUInt(buff, "BuffID") != WellFedStatusId) continue;
                    var duration = Convert.ToDouble(ReadMember(buff, "Duration"));
                    var timestampValue = ReadMember(buff, "Timestamp");
                    var timestamp = timestampValue is DateTime ? (DateTime)timestampValue : DateTime.Now;
                    var now = ServerNow();
                    var elapsed = now - timestamp;
                    // Some plugin builds expose an unspecified DateTime kind. Large skew means the
                    // duration is already the best available remaining-time value.
                    var remaining = Math.Abs(elapsed.TotalHours) > 24
                        ? TimeSpan.FromSeconds(duration)
                        : TimeSpan.FromSeconds(duration) - elapsed;
                    LastError = null;
                    LastErrorKey = null;
                    return remaining > TimeSpan.Zero
                        ? FoodBuffState.FromRemaining(remaining, warningMinutes)
                        : FoodBuffState.Missing();
                }
                LastError = null;
                LastErrorKey = null;
                return FoodBuffState.Missing();
            }
            catch (Exception ex)
            {
                LastError = ex.GetBaseException().Message;
                LastErrorKey = null;
                repository = null;
                CurrentPlayerId = 0;
                return FoodBuffState.Unavailable();
            }
        }

        private bool TryConnect()
        {
            try
            {
                var plugin = ActGlobals.oFormActMain.ActPlugins.Select(p => p.pluginObj)
                    .FirstOrDefault(p => p != null && p.GetType().FullName == "FFXIV_ACT_Plugin.FFXIV_ACT_Plugin");
                if (plugin == null) { LastError = null; LastErrorKey = "FfxivPluginMissing"; return false; }
                repository = plugin.GetType().GetProperty("DataRepository")?.GetValue(plugin, null);
                if (repository == null) { LastError = null; LastErrorKey = "RepositoryNotReady"; return false; }
                var type = repository.GetType().GetInterfaces()
                    .FirstOrDefault(x => x.FullName == "FFXIV_ACT_Plugin.Common.IDataRepository") ?? repository.GetType();
                getPlayerId = type.GetMethod("GetCurrentPlayerID");
                getCombatants = type.GetMethod("GetCombatantList");
                getServerTimestamp = type.GetMethod("GetServerTimestamp");
                if (getPlayerId == null || getCombatants == null)
                {
                    LastError = null; LastErrorKey = "DataApiMissing"; repository = null; return false;
                }
                LastError = null;
                LastErrorKey = null;
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.GetBaseException().Message;
                LastErrorKey = null;
                repository = null;
                CurrentPlayerId = 0;
                return false;
            }
        }

        private DateTime ServerNow()
        {
            if (getServerTimestamp == null) return DateTime.Now;
            try
            {
                var value = getServerTimestamp.Invoke(repository, null);
                var timestamp = value is DateTime ? (DateTime)value : DateTime.Now;
                return timestamp == default(DateTime) ? DateTime.Now : timestamp;
            }
            catch { return DateTime.Now; }
        }

        private static uint ReadUInt(object value, string name) => Convert.ToUInt32(ReadMember(value, name));
        private static object ReadMember(object value, string name)
        {
            var type = value.GetType();
            var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (property != null) return property.GetValue(value, null);
            var field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (field != null) return field.GetValue(value);
            throw new MissingMemberException(type.FullName, name);
        }
    }
}
