using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace FoodBuffOverlay
{
    [Serializable]
    public sealed class PluginSettings
    {
        public bool OverlayEnabled { get; set; } = true;
        public bool Locked { get; set; }
        public bool PositionPreview { get; set; }
        public bool DutyOnly { get; set; }
        public bool PartyOverlay { get; set; }
        public bool PartyLocked { get; set; }
        public bool PartyPreview { get; set; }
        private bool? partyDutyOnly;
        private int? partyWarningMinutes;
        public bool PartyDutyOnly { get => partyDutyOnly ?? DutyOnly; set => partyDutyOnly = value; }
        public int PartyWarningMinutes { get => partyWarningMinutes ?? WarningMinutes; set => partyWarningMinutes = value; }
        public int PartyOpacityPercent { get; set; } = 96;
        public int PartyWidth { get; set; } = 300;
        public int PartyRowHeight { get; set; } = 32;
        public int PartyLeft { get; set; } = 120;
        public int PartyTop { get; set; } = 240;
        public int WarningMinutes { get; set; } = 12;
        public int OpacityPercent { get; set; } = 94;
        public int Left { get; set; } = 120;
        public int Top { get; set; } = 120;
        public int Width { get; set; } = 270;
        public int Height { get; set; } = 68;
        internal string HostedLanguage { get; set; }
        internal string DisplayLanguage => HostedLanguage ?? Language;
        public string Language { get; set; }
        public bool CheckUpdatesOnStartup { get; set; } = true;
        public string SkippedVersion { get; set; }

        public void Normalize()
        {
            WarningMinutes = Math.Max(1, Math.Min(60, WarningMinutes));
            OpacityPercent = Math.Max(35, Math.Min(100, OpacityPercent));
            PartyDutyOnly = PartyDutyOnly;
            PartyWarningMinutes = Math.Max(1, Math.Min(60, PartyWarningMinutes));
            PartyOpacityPercent = Math.Max(35, Math.Min(100, PartyOpacityPercent));
            PartyWidth = Math.Max(180, Math.Min(620, PartyWidth));
            PartyRowHeight = Math.Max(24, Math.Min(90, PartyRowHeight));
            Width = Math.Max(180, Math.Min(620, Width));
            Height = Math.Max(48, Math.Min(180, Height));
            if (!string.IsNullOrWhiteSpace(Language)) Language = Localization.NormalizeLanguage(Language);
            if (SkippedVersion == null) SkippedVersion = string.Empty;
        }

        public bool InitializeLanguageIfMissing(CultureInfo uiCulture)
        {
            if (!string.IsNullOrWhiteSpace(Language))
            {
                Language = Localization.NormalizeLanguage(Language);
                return false;
            }
            Language = Localization.MapCulture(uiCulture);
            return true;
        }

        public static PluginSettings Load(string path)
        {
            try
            {
                if (!File.Exists(path)) return new PluginSettings();
                using (var stream = File.OpenRead(path))
                {
                    var value = (PluginSettings)new XmlSerializer(typeof(PluginSettings)).Deserialize(stream);
                    value.Normalize();
                    return value;
                }
            }
            catch { return new PluginSettings(); }
        }

        public void Save(string path)
        {
            AtomicSettingsFile.Write(path, Serialize(this));
        }

        internal static byte[] Serialize(PluginSettings value)
        {
            value.Normalize();
            using (var stream = new MemoryStream())
            {
                new XmlSerializer(typeof(PluginSettings)).Serialize(stream, value);
                return stream.ToArray();
            }
        }
    }
}
