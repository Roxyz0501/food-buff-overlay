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
using System.Threading;
using System.Threading.Tasks;
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
            VerifyLogReplayBoundsAndTargetRetention();
            VerifySettingsPersistenceLifecycle();
            VerifyBackgroundTaskShutdown();
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

    private static void VerifyLogReplayBoundsAndTargetRetention()
    {
        var root = TemporaryDirectory();
        try
        {
            var path = Path.Combine(root, "synthetic.log");
            var old = DateTime.Now.AddMinutes(-1);
            var beforeBoundary = StatusLine(26, old, 0x100, 3600);
            var replayOnly = StatusLine(26, old.AddSeconds(1), 0x102, 3600);
            File.WriteAllText(path, beforeBoundary + "\r\n" + replayOnly + "\r\n", new UTF8Encoding(false));
            var tracker = new FoodBuffLogTracker();
            var liveRemoval = StatusLine(30, DateTime.Now, 0x100, 0);
            var appendedAfterBoundary = StatusLine(26, DateTime.Now.AddSeconds(1), 0x101, 3600);
            tracker.ReplayCurrentLogAsync(path, CancellationToken.None, FoodBuffLogTracker.MaximumReplayBytes,
                FoodBuffLogTracker.ReplayTimeLimitMilliseconds, () =>
                {
                    tracker.ProcessLine(liveRemoval);
                    File.AppendAllText(path, appendedAfterBoundary + "\r\n", new UTF8Encoding(false));
                }).GetAwaiter().GetResult();

            Assert(tracker.Resolve(FoodBuffState.Missing(), 0x100, 12).Condition == FoodBuffCondition.Missing,
                "A delayed replay event overwrote a newer live removal.");
            Assert(tracker.Resolve(FoodBuffState.Missing(), 0x101, 12).Condition == FoodBuffCondition.Missing,
                "Replay read bytes appended after its fixed boundary.");
            Assert(tracker.Resolve(FoodBuffState.Missing(), 0x102, 12).Condition == FoodBuffCondition.Active,
                "Replay did not recover a status present at the captured boundary.");

            var clippedPath = Path.Combine(root, "clipped.log");
            var clipped = StatusLine(26, old, 0x201, 3600);
            var complete = StatusLine(26, old.AddSeconds(2), 0x202, 3600);
            var content = clipped + "\r\n" + complete + "\r\n";
            File.WriteAllText(clippedPath, content, new UTF8Encoding(false));
            var limited = new FoodBuffLogTracker();
            limited.ReplayCurrentLogAsync(clippedPath, CancellationToken.None, content.Length - clipped.Length / 2,
                FoodBuffLogTracker.ReplayTimeLimitMilliseconds, null).GetAwaiter().GetResult();
            Assert(limited.Resolve(FoodBuffState.Missing(), 0x201, 12).Condition == FoodBuffCondition.Missing,
                "Replay accepted the clipped first partial line.");
            Assert(limited.Resolve(FoodBuffState.Missing(), 0x202, 12).Condition == FoodBuffCondition.Active,
                "Replay failed to process a complete line within its byte budget.");

            var timeoutTracker = new FoodBuffLogTracker();
            timeoutTracker.ReplayCurrentLogAsync(clippedPath, CancellationToken.None, FoodBuffLogTracker.MaximumReplayBytes,
                1, () => Thread.Sleep(10)).GetAwaiter().GetResult();
            Assert(timeoutTracker.TrackedTargetCount == 0, "Replay exceeded its elapsed-time budget.");

            using (var cancelled = new CancellationTokenSource())
            {
                AssertThrows<OperationCanceledException>(() => limited.ReplayCurrentLogAsync(path, cancelled.Token,
                    FoodBuffLogTracker.MaximumReplayBytes, FoodBuffLogTracker.ReplayTimeLimitMilliseconds,
                    () => cancelled.Cancel()).GetAwaiter().GetResult(), "Replay ignored cancellation at its captured boundary.");
            }

            var cap = new FoodBuffLogTracker();
            cap.Resolve(FoodBuffState.Missing(), 1, 12); // Identify and retain the player's record before the target flood.
            Assert(cap.ProcessLine(StatusLine(26, DateTime.Now, 1, 3600)), "Player status line was not parsed.");
            for (uint id = 2; id < 5002; id++)
                cap.ProcessLine(StatusLine(26, DateTime.Now.AddSeconds(id), id, 3600));
            Assert(cap.TrackedTargetCount <= 4096, "Target state exceeded its hard cap.");
            Assert(cap.Resolve(FoodBuffState.Missing(), 1, 12).Condition == FoodBuffCondition.Active,
                "Target eviction lost the player's active status.");
            cap.PruneExpiredStates(DateTime.UtcNow.AddMinutes(31));
            Assert(cap.TrackedTargetCount == 1 && cap.Resolve(FoodBuffState.Missing(), 1, 12).Condition == FoodBuffCondition.Active,
                "Retention pruning did not discard stale other targets while retaining the player.");

            Assert(!cap.ProcessLine("00|not a status event|30|name|1|source|name|00000001|target"),
                "Unrelated log lines should be rejected before full status parsing.");
        }
        finally { Directory.Delete(root, true); }
    }

    private static string StatusLine(int type, DateTime timestamp, uint targetId, double duration) =>
        type.ToString(CultureInfo.InvariantCulture) + "|" + timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) +
        "|0030|Well Fed|" + duration.ToString(CultureInfo.InvariantCulture) + "|00000001|Player|" +
        targetId.ToString("X8", CultureInfo.InvariantCulture) + "|Target";

    private static void VerifySettingsPersistenceLifecycle()
    {
        Assert(SettingsPersistence.AutosaveIntervalMilliseconds == 60000, "Settings autosave interval must be 60 seconds.");
        var root = TemporaryDirectory();
        try
        {
            var path = Path.Combine(root, "settings.xml");
            var settings = new PluginSettings { Language = "en", WarningMinutes = 12 };
            var writes = 0;
            Action<string, byte[]> writer = (target, bytes) => { writes++; AtomicSettingsFile.Write(target, bytes); };
            var persistence = new SettingsPersistence(path, settings, writer);
            Assert(!persistence.SaveIfChanged(settings) && writes == 0, "Unchanged settings must not be written.");

            settings.WarningMinutes = 15;
            Assert(persistence.IsDirty(settings), "Edited settings should be dirty.");
            settings.WarningMinutes = 16; // Continuous edits collapse into the one next interval save.
            Assert(persistence.SaveIfChanged(settings) && writes == 1, "Changed settings should write once at the interval.");
            Assert(!persistence.SaveIfChanged(settings) && writes == 1, "Unchanged interval must not write again.");

            settings.Language = "ja"; // Normal unload flushes pending edits before the next 60-second tick.
            Assert(persistence.SaveIfChanged(settings) && writes == 2, "Final save did not flush pending edits.");
            var reloaded = PluginSettings.Load(path);
            Assert(reloaded.WarningMinutes == 16 && reloaded.Language == "ja", "Saved settings did not reload.");
            var persistedBytes = File.ReadAllBytes(path);

            var failing = new SettingsPersistence(path, reloaded, (target, bytes) => { throw new IOException("simulated save failure"); });
            reloaded.WarningMinutes = 19;
            AssertThrows<IOException>(() => failing.SaveIfChanged(reloaded), "Save failure was not surfaced.");
            Assert(failing.IsDirty(reloaded), "Failed save must retain dirty state for retry.");
            Assert(File.ReadAllBytes(path).SequenceEqual(persistedBytes), "Failed save damaged the existing settings file.");
            var retry = new SettingsPersistence(path, PluginSettings.Load(path), AtomicSettingsFile.Write);
            Assert(retry.IsDirty(reloaded) && retry.SaveIfChanged(reloaded), "A later retry should persist pending edits.");
            Assert(PluginSettings.Load(path).WarningMinutes == 19, "Retry did not persist settings.");
            Assert(!Directory.GetFiles(root, "*.tmp-*", SearchOption.TopDirectoryOnly).Any(), "Atomic save left temporary files behind.");
        }
        finally { Directory.Delete(root, true); }
    }

    private static void VerifyBackgroundTaskShutdown()
    {
        var group = new BackgroundTaskGroup();
        var entered = new ManualResetEventSlim(false);
        var release = new ManualResetEventSlim(false);
        Assert(group.TryStart(token => Task.Run(() => { entered.Set(); release.Wait(); })), "Background task did not start.");
        Assert(entered.Wait(TimeSpan.FromSeconds(5)), "Background task did not enter its work section.");
        var shutdown = Task.Run(() => group.StopAndWait());
        Assert(!shutdown.Wait(TimeSpan.FromMilliseconds(100)), "Shutdown returned while tracked work was still running.");
        release.Set();
        Assert(shutdown.Wait(TimeSpan.FromSeconds(5)), "Shutdown did not finish after work completed.");
        Assert(!group.TryStart(token => Task.CompletedTask), "New work was accepted after shutdown began.");
        group.Dispose(); entered.Dispose(); release.Dispose();

        var cancellable = new BackgroundTaskGroup();
        cancellable.TryStart(token => Task.Delay(Timeout.Infinite, token));
        cancellable.StopAndWait();
        cancellable.Dispose();
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

    private static string TemporaryDirectory() { var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-temp", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path); return path; }
    private static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void AssertThrows<T>(Action action, string message) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException(message);
    }
}
