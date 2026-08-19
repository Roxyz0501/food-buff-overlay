using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;

namespace FoodBuffOverlay
{
    internal static class ReleaseParser
    {
        public static List<ReleaseInfo> ParseStableReleases(string json)
        {
            var serializer = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 };
            var root = serializer.DeserializeObject(json) as object[];
            if (root == null) throw new FormatException("GitHub release response is not an array.");
            var releases = new List<ReleaseInfo>();
            foreach (var raw in root)
            {
                var item = raw as Dictionary<string, object>;
                if (item == null || Bool(item, "draft") || Bool(item, "prerelease")) continue;
                var tag = Text(item, "tag_name");
                SemVersion version;
                if (!SemVersion.TryParse(tag, out version) || !version.IsStable) continue;
                var release = new ReleaseInfo
                {
                    Tag = tag,
                    Version = version,
                    Name = Text(item, "name"),
                    Notes = Summarize(Text(item, "body"))
                };
                var assets = Value(item, "assets") as object[];
                if (assets != null)
                {
                    foreach (var rawAsset in assets)
                    {
                        var asset = rawAsset as Dictionary<string, object>;
                        if (asset == null) continue;
                        release.Assets.Add(new ReleaseAsset
                        {
                            Name = Text(asset, "name"),
                            DownloadUrl = Text(asset, "browser_download_url"),
                            Size = Long(asset, "size")
                        });
                    }
                }
                releases.Add(release);
            }
            return releases.OrderByDescending(x => x.Version).ToList();
        }

        internal static string Summarize(string body)
        {
            var value = (body ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            while (value.Contains("  ")) value = value.Replace("  ", " ");
            return value.Length <= 280 ? value : value.Substring(0, 277) + "…";
        }

        private static object Value(Dictionary<string, object> item, string key) { object value; return item.TryGetValue(key, out value) ? value : null; }
        private static string Text(Dictionary<string, object> item, string key) => Convert.ToString(Value(item, key)) ?? string.Empty;
        private static bool Bool(Dictionary<string, object> item, string key) { var value = Value(item, key); return value != null && Convert.ToBoolean(value); }
        private static long Long(Dictionary<string, object> item, string key) { var value = Value(item, key); return value == null ? 0 : Convert.ToInt64(value); }
    }
}
