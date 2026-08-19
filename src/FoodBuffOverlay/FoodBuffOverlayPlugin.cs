using Advanced_Combat_Tracker;
using System;
using System.IO;
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace FoodBuffOverlay
{
    public sealed class FoodBuffOverlayPlugin : IActPluginV1
    {
        private readonly FfxivDataSource dataSource = new FfxivDataSource();
        private readonly FoodBuffLogTracker logTracker = new FoodBuffLogTracker();
        private readonly UpdateService updateService = new UpdateService();
        private readonly CancellationTokenSource updateCancellation = new CancellationTokenSource();
        private PluginSettings settings;
        private SettingsControl control;
        private OverlayForm overlay;
        private System.Windows.Forms.Timer refreshTimer;
        private System.Windows.Forms.Timer displayTimer;
        private FoodBuffState sampledState = FoodBuffState.Unavailable();
        private Label status;
        private string settingsPath;
        private TabPage pluginPage;

        public void InitPlugin(TabPage pluginScreenSpace, Label pluginStatusText)
        {
            status = pluginStatusText;
            settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Advanced Combat Tracker", "Config", "FoodBuffOverlay.xml");
            settings = PluginSettings.Load(settingsPath);
            var initializedLanguage = settings.InitializeLanguageIfMissing(CultureInfo.CurrentUICulture);
            settings.Normalize();
            if (initializedLanguage) SaveSettings();
            control = new SettingsControl(settings);
            control.SettingsChanged += OnSettingsChanged;
            control.CheckUpdatesRequested += OnCheckUpdatesRequested;
            control.InstallUpdateRequested += OnInstallUpdateRequested;
            control.SkipUpdateRequested += OnSkipUpdateRequested;
            pluginPage = pluginScreenSpace;
            pluginScreenSpace.Text = Localization.Get(settings.Language, "PluginTab");
            pluginScreenSpace.Controls.Add(control);
            overlay = new OverlayForm(settings);
            overlay.BoundsChangedByUser += delegate { SaveSettings(); };
            overlay.ApplySettings(settings, true);
            ActGlobals.oFormActMain.OnLogLineRead += OnLogLineRead;
            logTracker.ReplayCurrentLog();
            refreshTimer = new System.Windows.Forms.Timer { Interval = 5000 };
            refreshTimer.Tick += PollStatus;
            refreshTimer.Start();
            displayTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            displayTimer.Tick += RefreshDisplay;
            displayTimer.Start();
            PollStatus(null, EventArgs.Empty);
            status.Text = Localization.Get(settings.Language, "Started");
            if (settings.CheckUpdatesOnStartup)
                CheckForUpdates(false);
        }

        private void PollStatus(object sender, EventArgs e)
        {
            if (settings == null) return;
            var snapshot = dataSource.Read(settings.WarningMinutes);
            sampledState = logTracker.Resolve(snapshot, dataSource.CurrentPlayerId, settings.WarningMinutes);
            RefreshDisplay(null, EventArgs.Empty);
            var error = string.IsNullOrWhiteSpace(dataSource.LastErrorKey)
                ? (string.IsNullOrWhiteSpace(dataSource.LastError) ? null : Localization.Get(settings.Language, "DataReadFailed"))
                : Localization.Get(settings.Language, dataSource.LastErrorKey);
            control?.SetConnection(sampledState, error);
        }

        private void RefreshDisplay(object sender, EventArgs e)
        {
            if (settings == null) return;
            overlay?.SetState(sampledState.At(DateTime.UtcNow, settings.WarningMinutes));
        }

        private void OnLogLineRead(bool isImport, LogLineEventArgs logInfo)
        {
            if (isImport || logInfo == null) return;
            var changed = logTracker.ProcessLine(logInfo.originalLogLine);
            if (!string.Equals(logInfo.logLine, logInfo.originalLogLine, StringComparison.Ordinal))
                changed |= logTracker.ProcessLine(logInfo.logLine);
            if (!changed || control == null) return;
            if (control.InvokeRequired) control.BeginInvoke(new Action(() => PollStatus(null, EventArgs.Empty)));
            else PollStatus(null, EventArgs.Empty);
        }

        private void OnSettingsChanged(object sender, EventArgs e)
        {
            control.ApplyTo(settings);
            overlay.ApplySettings(settings);
            if (pluginPage != null) pluginPage.Text = Localization.Get(settings.Language, "PluginTab");
            if (status != null) status.Text = Localization.Get(settings.Language, "Started");
            SaveSettings();
            PollStatus(null, EventArgs.Empty);
        }

        public void DeInitPlugin()
        {
            ActGlobals.oFormActMain.OnLogLineRead -= OnLogLineRead;
            updateCancellation.Cancel();
            if (refreshTimer != null) { refreshTimer.Stop(); refreshTimer.Tick -= PollStatus; refreshTimer.Dispose(); }
            if (displayTimer != null) { displayTimer.Stop(); displayTimer.Tick -= RefreshDisplay; displayTimer.Dispose(); }
            SaveSettings();
            if (control != null)
            {
                control.SettingsChanged -= OnSettingsChanged;
                control.CheckUpdatesRequested -= OnCheckUpdatesRequested;
                control.InstallUpdateRequested -= OnInstallUpdateRequested;
                control.SkipUpdateRequested -= OnSkipUpdateRequested;
            }
            overlay?.Close(); overlay?.Dispose(); control?.Dispose();
            updateService.Dispose(); updateCancellation.Dispose();
            if (status != null) status.Text = Localization.Get(settings == null ? Localization.English : settings.Language, "Stopped");
        }

        private void SaveSettings()
        {
            try { settings?.Save(settingsPath); }
            catch (Exception ex) { if (status != null) status.Text = Localization.Get(settings == null ? Localization.English : settings.Language, "SaveError", ex.Message); }
        }

        private void OnCheckUpdatesRequested(object sender, EventArgs e) => CheckForUpdates(true);

        private async void CheckForUpdates(bool userInitiated)
        {
            if (control == null || control.IsDisposed) return;
            control.SetUpdateChecking();
            var current = Assembly.GetExecutingAssembly().GetName().Version;
            var result = await updateService.CheckAsync(current, updateCancellation.Token);
            if (control == null || control.IsDisposed || updateCancellation.IsCancellationRequested) return;
            control.ShowUpdateResult(result, current, settings.SkippedVersion);
            if (result.Status == UpdateCheckStatus.UpdateAvailable && result.Release != null &&
                !string.Equals(settings.SkippedVersion, result.Release.Version.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                status.Text = Localization.Get(settings.Language, "UpdateAvailable", current.ToString(3), result.Release.Version, result.Release.Notes);
            }
            else if (userInitiated && result.Status == UpdateCheckStatus.Failed)
            {
                status.Text = Localization.Get(settings.Language, "CheckFailed", Localization.Get(settings.Language, "NetworkFailure"));
            }
        }

        private async void OnInstallUpdateRequested(object sender, EventArgs e)
        {
            var release = control == null ? null : control.AvailableRelease;
            if (release == null) return;
            control.SetUpdatePreparing();
            try
            {
                var prepared = await updateService.DownloadAndVerifyAsync(release, updateCancellation.Token);
                if (updateCancellation.IsCancellationRequested) return;
                updateService.LaunchUpdater(prepared, Assembly.GetExecutingAssembly().Location);
                control.SetUpdatePrepared();
                status.Text = Localization.Get(settings.Language, "RestartAct");
            }
            catch (Exception ex)
            {
                var message = LocalizeUpdateError(ex.GetBaseException().Message);
                control.SetUpdateError(message);
                status.Text = Localization.Get(settings.Language, "UpdateFailed", message);
            }
        }

        private void OnSkipUpdateRequested(object sender, EventArgs e)
        {
            var release = control == null ? null : control.AvailableRelease;
            if (release == null) return;
            settings.SkippedVersion = release.Version.ToString();
            SaveSettings();
            control.ShowUpdateResult(new UpdateCheckResult { Status = UpdateCheckStatus.UpdateAvailable, Release = release },
                Assembly.GetExecutingAssembly().GetName().Version, settings.SkippedVersion);
        }

        private string LocalizeUpdateError(string code)
        {
            switch (code)
            {
                case "UpdateAssetMissing": return Localization.Get(settings.Language, "UpdateAssetMissing");
                case "HashFailed": return Localization.Get(settings.Language, "HashFailed");
                case "UpdaterMissing": return Localization.Get(settings.Language, "UpdaterMissing");
                case "InvalidPackage": return Localization.Get(settings.Language, "InvalidPackage");
                default: return Localization.Get(settings.Language, "UpdateFailureGeneric");
            }
        }
    }
}
