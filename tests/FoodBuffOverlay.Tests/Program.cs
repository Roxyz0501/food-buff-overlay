using FoodBuffOverlay;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        try
        {
            VerifyCultureMappingAndSettingsCompatibility();
            VerifyLocalizationFallback();
            VerifySemVersion();
            VerifyReleaseParser();
            VerifyHashAndZipSafety();
            VerifyUpdaterBackupAndRollback();
            VerifySupportLink();
            VerifyLocalizedUi();
            VerifyRepositoryIsConfigured();
            Console.WriteLine("FoodBuffOverlay tests: PASS");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FoodBuffOverlay tests: FAIL");
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void VerifyCultureMappingAndSettingsCompatibility()
    {
        Assert(Localization.MapCulture(new CultureInfo("ja-JP")) == "ja", "ja mapping failed.");
        Assert(Localization.MapCulture(new CultureInfo("zh-TW")) == "zh-CN", "zh mapping failed.");
        Assert(Localization.MapCulture(new CultureInfo("ko-KR")) == "ko", "ko mapping failed.");
        Assert(Localization.MapCulture(new CultureInfo("fr-FR")) == "en", "unsupported culture must use English.");

        var first = new PluginSettings();
        Assert(first.InitializeLanguageIfMissing(new CultureInfo("ja-JP")), "First-run language should initialize.");
        Assert(first.Language == "ja", "First-run OS language was not applied.");
        Assert(!first.InitializeLanguageIfMissing(new CultureInfo("ko-KR")), "Saved language must not be overwritten.");
        Assert(first.Language == "ja", "Saved language changed unexpectedly.");

        var root = TemporaryDirectory();
        try
        {
            var path = Path.Combine(root, "old.xml");
            File.WriteAllText(path, "<?xml version=\"1.0\"?><PluginSettings><OverlayEnabled>true</OverlayEnabled><WarningMinutes>12</WarningMinutes></PluginSettings>", Encoding.UTF8);
            var loaded = PluginSettings.Load(path);
            Assert(loaded.Language == null, "Old config must remain distinguishable as first-run language.");
            Assert(loaded.CheckUpdatesOnStartup, "New update setting must have a safe default.");
        }
        finally { Directory.Delete(root, true); }
    }

    private static void VerifyLocalizationFallback()
    {
        Assert(Localization.Get("unsupported", "TabSupport") == "Support", "Unsupported language fallback failed.");
        Assert(Localization.Get("ko", "FallbackProbe") == "English fallback", "Missing translation-key fallback failed.");
        Assert(Localization.Get("ja", "TabSupport") == "支援", "Japanese support tab is incorrect.");
        Assert(Localization.Get("zh-CN", "TabSupport") == "支持", "Chinese support tab is incorrect.");
        Assert(Localization.Get("ko", "TabSupport") == "후원", "Korean support tab is incorrect.");
        Assert(!Localization.MissingUserKeys("ja").Any(), "Japanese resources have missing user-facing keys.");
        Assert(!Localization.MissingUserKeys("zh-CN").Any(), "Chinese resources have missing user-facing keys.");
        Assert(!Localization.MissingUserKeys("ko").Any(), "Korean resources have missing user-facing keys.");
    }

    private static void VerifySemVersion()
    {
        SemVersion stable, older, prerelease;
        Assert(SemVersion.TryParse("v1.2.3", out stable), "v-prefixed SemVer failed.");
        Assert(SemVersion.TryParse("1.2.2", out older), "SemVer failed.");
        Assert(SemVersion.TryParse("1.2.3-beta.1", out prerelease), "Prerelease SemVer failed.");
        Assert(stable.CompareTo(older) > 0, "SemVer numeric comparison failed.");
        Assert(stable.CompareTo(prerelease) > 0, "Stable release must sort after prerelease.");
        Assert(!SemVersion.TryParse("release-one", out older), "Invalid SemVer was accepted.");
    }

    private static void VerifyReleaseParser()
    {
        var json = "[" +
            "{\"tag_name\":\"v1.3.0-beta.1\",\"draft\":false,\"prerelease\":true,\"assets\":[]}," +
            "{\"tag_name\":\"v1.2.1\",\"name\":\"Stable\",\"body\":\"Fixes\\nMore\",\"draft\":false,\"prerelease\":false," +
            "\"assets\":[{\"name\":\"FoodBuffOverlay-v1.2.1.zip\",\"browser_download_url\":\"https://github.com/o/r/releases/x.zip\",\"size\":123}]}," +
            "{\"tag_name\":\"v9.0.0\",\"draft\":true,\"prerelease\":false,\"assets\":[]}]";
        var releases = ReleaseParser.ParseStableReleases(json);
        Assert(releases.Count == 1 && releases[0].Version.ToString() == "1.2.1", "Stable release filtering failed.");
        Assert(releases[0].Notes == "Fixes More", "Release notes summary failed.");
        Assert(releases[0].Assets.Count == 1, "Release assets parsing failed.");
        Assert(UpdateService.EvaluateResponse(json, new Version(1, 2, 0)).Status == UpdateCheckStatus.UpdateAvailable, "Update-available evaluation failed.");
        Assert(UpdateService.EvaluateResponse(json, new Version(1, 2, 1)).Status == UpdateCheckStatus.UpToDate, "Up-to-date evaluation failed.");
        Assert(UpdateService.EvaluateResponse("not json", new Version(1, 2, 0)).Status == UpdateCheckStatus.Failed, "Malformed response was not contained.");
        Assert(UpdateService.EvaluateResponse("[]", new Version(1, 2, 0)).Status == UpdateCheckStatus.NoStableRelease, "No-release evaluation failed.");
        var networkFailure = UpdateService.CheckFromFetcherAsync(
            () => System.Threading.Tasks.Task.FromException<string>(new HttpRequestException("offline")), new Version(1, 2, 0)).GetAwaiter().GetResult();
        Assert(networkFailure.Status == UpdateCheckStatus.Failed, "Network failure was not contained.");
    }

    private static void VerifyHashAndZipSafety()
    {
        var root = TemporaryDirectory();
        try
        {
            var file = Path.Combine(root, "asset.zip");
            File.WriteAllText(file, "known content", Encoding.UTF8);
            var hash = UpdatePackageVerifier.ComputeSha256(file);
            Assert(UpdatePackageVerifier.VerifySha256(file, hash), "SHA-256 success path failed.");
            Assert(!UpdatePackageVerifier.VerifySha256(file, new string('0', 64)), "Corrupt asset hash was accepted.");
            Assert(UpdatePackageVerifier.FindManifestHash(hash + "  asset.zip", "asset.zip") == hash, "Manifest parsing failed.");
            AssertThrows<InvalidDataException>(() => UpdatePackageVerifier.SafeDestination(root, "../escape.dll"), "Zip Slip path was accepted.");
            Assert(UpdatePackageVerifier.SafeDestination(root, "folder/file.txt").StartsWith(Path.GetFullPath(root)), "Safe ZIP path failed.");

            var zip = Path.Combine(root, "bad.zip");
            using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create)) archive.CreateEntry("../escape.dll");
            AssertThrows<InvalidDataException>(() => UpdatePackageVerifier.ExtractValidated(zip, Path.Combine(root, "extract")), "Malicious ZIP extracted.");
        }
        finally { Directory.Delete(root, true); }
    }

    private static void VerifyUpdaterBackupAndRollback()
    {
        var root = TemporaryDirectory();
        try
        {
            var sourceDir = Path.Combine(root, "stage"); var targetDir = Path.Combine(root, "plugin");
            Directory.CreateDirectory(sourceDir); Directory.CreateDirectory(targetDir);
            var source = Path.Combine(sourceDir, "FoodBuffOverlay.dll"); var target = Path.Combine(targetDir, "FoodBuffOverlay.dll");
            File.WriteAllText(source, "new plugin", Encoding.UTF8); File.WriteAllText(target, "old plugin", Encoding.UTF8);
            var hash = UpdatePackageVerifier.ComputeSha256(source);
            AssertThrows<InvalidOperationException>(() => FoodBuffOverlay.Updater.Program.ReplaceWithBackup(
                source, target, hash, "1.2.0", (incoming, destination) =>
                {
                    File.WriteAllText(destination, "broken", Encoding.UTF8);
                    throw new InvalidOperationException("simulated install failure");
                }), "Simulated updater failure did not propagate.");
            Assert(File.ReadAllText(target, Encoding.UTF8) == "old plugin", "Rollback did not restore the previous DLL.");
            Assert(Directory.GetFiles(targetDir, "*.backup-*").Length == 1, "Backup was not retained.");
            FoodBuffOverlay.Updater.Program.ReplaceWithBackup(source, target, hash, "1.2.0");
            Assert(File.ReadAllText(target, Encoding.UTF8) == "new plugin", "Updater success path failed.");
        }
        finally { Directory.Delete(root, true); }
    }

    private static void VerifySupportLink()
    {
        ProcessStartInfo captured = null; string error;
        var result = SettingsControl.TryOpenSupportLink(info => captured = info, out error);
        Assert(result && error == null, "Injected link opener should succeed.");
        Assert(captured.FileName == "https://ko-fi.com/roxyz0501" && captured.UseShellExecute, "Support URL/default-browser setting is incorrect.");
        result = SettingsControl.TryOpenSupportLink(info => { throw new InvalidOperationException("browser failure"); }, out error);
        Assert(!result && error.Contains("browser failure"), "Browser launch exception was not contained.");
    }

    private static void VerifyLocalizedUi()
    {
        var settings = new PluginSettings { Language = "en" };
        using (var control = new SettingsControl(settings))
        {
            var all = Descendants(control).ToList();
            var tabs = all.OfType<TabControl>().Single();
            var combo = all.OfType<ComboBox>().Single();
            Assert(tabs.TabPages.Count == 3, "Settings, Updates, and Support must be separate tabs.");
            Assert(tabs.TabPages[1].Text == "Updates" && tabs.TabPages[2].Text == "Support", "English update/support tabs are incorrect.");
            combo.SelectedIndex = 1; Assert(tabs.TabPages[1].Text == "更新" && tabs.TabPages[2].Text == "支援", "Immediate Japanese switch failed.");
            combo.SelectedIndex = 2; Assert(tabs.TabPages[1].Text == "更新" && tabs.TabPages[2].Text == "支持", "Immediate Chinese switch failed.");
            combo.SelectedIndex = 3; Assert(tabs.TabPages[1].Text == "업데이트" && tabs.TabPages[2].Text == "후원", "Immediate Korean switch failed.");
            var supportText = string.Join("\n", Descendants(tabs.TabPages[2]).Select(x => x.Text));
            Assert(supportText.Contains("Roxyz0501") && supportText.Contains(SettingsControl.SupportUrl), "Localized support content is incomplete.");
        }
    }

    private static void VerifyRepositoryIsConfigured()
    {
        Assert(UpdateConfiguration.IsConfigured, "The published update repository must be configured.");
        Assert(UpdateConfiguration.ReleasesApiUrl == "https://api.github.com/repos/Roxyz0501/food-buff-overlay/releases?per_page=10",
            "The update API must point to the plugin's real repository.");
        Assert(UpdateService.CheckFromFetcherAsync(() => throw new HttpRequestException("offline"), new Version(1, 2, 0)).Result.Status == UpdateCheckStatus.Failed,
            "Network failures must be contained after repository configuration.");
        UpdateService.EnsureAllowedUri(new Uri("https://api.github.com/repos/o/r/releases"), true);
        UpdateService.EnsureAllowedUri(new Uri("https://github.com/o/r/releases/download/v/a.zip"), false);
        AssertThrows<InvalidDataException>(() => UpdateService.EnsureAllowedUri(new Uri("http://github.com/o/r/a.zip"), false), "HTTP URL was accepted.");
        AssertThrows<InvalidDataException>(() => UpdateService.EnsureAllowedUri(new Uri("https://example.com/a.zip"), false), "Arbitrary host was accepted.");
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls) { yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }

    private static string TemporaryDirectory() { var path = Path.Combine(Path.GetTempPath(), "FoodBuffOverlayTests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path); return path; }
    private static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void AssertThrows<T>(Action action, string message) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException(message);
    }
}
