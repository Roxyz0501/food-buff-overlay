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
        private readonly BackgroundTaskGroup backgroundTasks = new BackgroundTaskGroup();
        private readonly object pendingUpdateGate = new object();
        private PluginSettings settings;
        private SettingsPersistence settingsPersistence;
        private SettingsControl control;
        private OverlayForm overlay;
        private System.Windows.Forms.Timer refreshTimer;
        private System.Windows.Forms.Timer displayTimer;
        private System.Windows.Forms.Timer settingsSaveTimer;
        private FoodBuffState sampledState = FoodBuffState.Unavailable();
        private Label status;
        private string settingsPath;
        private TabPage pluginPage;
        private PendingUpdate pendingUpdate;
        private int stopping;
        private string lastSaveError;

        public void InitPlugin(TabPage pluginScreenSpace, Label pluginStatusText)
        {
            status = pluginStatusText;
            settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Advanced Combat Tracker", "Config", "FoodBuffOverlay.xml");
            settings = PluginSettings.Load(settingsPath);
            settingsPersistence = new SettingsPersistence(settingsPath, settings);
            settings.InitializeLanguageIfMissing(CultureInfo.CurrentUICulture);
            settings.Normalize();
            control = new SettingsControl(settings);
            control.SettingsChanged += OnSettingsChanged;
            control.CheckUpdatesRequested += OnCheckUpdatesRequested;
            control.InstallUpdateRequested += OnInstallUpdateRequested;
            control.SkipUpdateRequested += OnSkipUpdateRequested;
            pluginPage = pluginScreenSpace;
            pluginScreenSpace.Text = Localization.Get(settings.Language, "PluginTab");
            pluginScreenSpace.Controls.Add(control);
            overlay = new OverlayForm(settings);
            overlay.ApplySettings(settings, true);
            ActGlobals.oFormActMain.OnLogLineRead += OnLogLineRead;
            var activeLogPath = ActGlobals.oFormActMain.LogFilePath;
            refreshTimer = new System.Windows.Forms.Timer { Interval = 5000 };
            refreshTimer.Tick += PollStatus;
            refreshTimer.Start();
            displayTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            displayTimer.Tick += RefreshDisplay;
            displayTimer.Start();
            settingsSaveTimer = new System.Windows.Forms.Timer { Interval = SettingsPersistence.AutosaveIntervalMilliseconds };
            settingsSaveTimer.Tick += SaveSettings;
            settingsSaveTimer.Start();
            PollStatus(null, EventArgs.Empty);
            backgroundTasks.TryStart(token => logTracker.ReplayCurrentLogAsync(activeLogPath, token));
            status.Text = Localization.Get(settings.Language, "Started");
            if (settings.CheckUpdatesOnStartup)
                CheckForUpdates(false);
        }

        private void PollStatus(object sender, EventArgs e)
        {
            if (settings == null || Volatile.Read(ref stopping) != 0) return;
            var snapshot = dataSource.Read(settings.WarningMinutes);
            var playerId = dataSource.CurrentPlayerId;
            logTracker.SetPlayerId(playerId);
            sampledState = logTracker.Resolve(snapshot, playerId, settings.WarningMinutes);
            RefreshDisplay(null, EventArgs.Empty);
            var error = string.IsNullOrWhiteSpace(dataSource.LastErrorKey)
                ? (string.IsNullOrWhiteSpace(dataSource.LastError) ? null : Localization.Get(settings.Language, "DataReadFailed"))
                : Localization.Get(settings.Language, dataSource.LastErrorKey);
            control?.SetConnection(sampledState, error);
        }

        private void RefreshDisplay(object sender, EventArgs e)
        {
            if (settings == null || Volatile.Read(ref stopping) != 0) return;
            ApplyPendingUpdate();
            overlay?.SetState(sampledState.At(DateTime.UtcNow, settings.WarningMinutes));
        }

        private void OnLogLineRead(bool isImport, LogLineEventArgs logInfo)
        {
            if (Volatile.Read(ref stopping) != 0 || isImport || logInfo == null) return;
            logTracker.ProcessLine(logInfo.originalLogLine);
            if (!string.Equals(logInfo.logLine, logInfo.originalLogLine, StringComparison.Ordinal))
                logTracker.ProcessLine(logInfo.logLine);
            // The existing UI timers sample status. Avoid queuing a callback that could run after deinitialization.
        }

        private void OnSettingsChanged(object sender, EventArgs e)
        {
            if (Volatile.Read(ref stopping) != 0) return;
            control.ApplyTo(settings);
            overlay.ApplySettings(settings);
            if (pluginPage != null) pluginPage.Text = Localization.Get(settings.Language, "PluginTab");
            if (status != null) status.Text = Localization.Get(settings.Language, "Started");
            PollStatus(null, EventArgs.Empty);
        }

        public void DeInitPlugin()
        {
            if (Interlocked.Exchange(ref stopping, 1) != 0) return;
            ActGlobals.oFormActMain.OnLogLineRead -= OnLogLineRead;
            if (refreshTimer != null) { refreshTimer.Stop(); refreshTimer.Tick -= PollStatus; refreshTimer.Dispose(); }
            if (displayTimer != null) { displayTimer.Stop(); displayTimer.Tick -= RefreshDisplay; displayTimer.Dispose(); }
            if (settingsSaveTimer != null) { settingsSaveTimer.Stop(); settingsSaveTimer.Tick -= SaveSettings; settingsSaveTimer.Dispose(); }
            if (control != null)
            {
                control.SettingsChanged -= OnSettingsChanged;
                control.CheckUpdatesRequested -= OnCheckUpdatesRequested;
                control.InstallUpdateRequested -= OnInstallUpdateRequested;
                control.SkipUpdateRequested -= OnSkipUpdateRequested;
            }
            overlay?.StopAndFlushBounds();
            backgroundTasks.StopAndWait();
            backgroundTasks.Dispose();
            SaveSettings(null, EventArgs.Empty);
            updateService.Dispose();
            overlay?.Close(); overlay?.Dispose(); control?.Dispose();
            if (status != null) status.Text = lastSaveError ?? Localization.Get(settings == null ? Localization.English : settings.Language, "Stopped");
        }

        private void SaveSettings(object sender, EventArgs e)
        {
            if (settings == null || settingsPersistence == null) return;
            try
            {
                settingsPersistence.SaveIfChanged(settings);
                lastSaveError = null;
            }
            catch (Exception ex)
            {
                lastSaveError = Localization.Get(settings.Language, "SaveError", ex.GetBaseException().Message);
                if (status != null) status.Text = lastSaveError;
            }
        }

        private void OnCheckUpdatesRequested(object sender, EventArgs e) => CheckForUpdates(true);

        private void CheckForUpdates(bool userInitiated)
        {
            if (Volatile.Read(ref stopping) != 0 || control == null || control.IsDisposed) return;
            control.SetUpdateChecking();
            var current = Assembly.GetExecutingAssembly().GetName().Version;
            backgroundTasks.TryStart(async token =>
            {
                var result = await updateService.CheckAsync(current, token).ConfigureAwait(false);
                if (token.IsCancellationRequested || Volatile.Read(ref stopping) != 0) return;
                lock (pendingUpdateGate) pendingUpdate = PendingUpdate.Checked(result, current, userInitiated);
            });
        }

        private void OnInstallUpdateRequested(object sender, EventArgs e)
        {
            if (Volatile.Read(ref stopping) != 0) return;
            var release = control == null ? null : control.AvailableRelease;
            if (release == null) return;
            control.SetUpdatePreparing();
            backgroundTasks.TryStart(async token =>
            {
                try
                {
                    var prepared = await updateService.DownloadAndVerifyAsync(release, token).ConfigureAwait(false);
                    if (token.IsCancellationRequested || Volatile.Read(ref stopping) != 0) return;
                    lock (pendingUpdateGate) pendingUpdate = PendingUpdate.Prepared(prepared);
                }
                catch (Exception ex)
                {
                    if (token.IsCancellationRequested || Volatile.Read(ref stopping) != 0) return;
                    lock (pendingUpdateGate) pendingUpdate = PendingUpdate.Failed(ex.GetBaseException().Message);
                }
            });
        }

        private void OnSkipUpdateRequested(object sender, EventArgs e)
        {
            if (Volatile.Read(ref stopping) != 0) return;
            var release = control == null ? null : control.AvailableRelease;
            if (release == null) return;
            settings.SkippedVersion = release.Version.ToString();
            control.ShowUpdateResult(new UpdateCheckResult { Status = UpdateCheckStatus.UpdateAvailable, Release = release },
                Assembly.GetExecutingAssembly().GetName().Version, settings.SkippedVersion);
        }

        private void ApplyPendingUpdate()
        {
            if (Volatile.Read(ref stopping) != 0 || control == null || control.IsDisposed) return;
            PendingUpdate pending;
            lock (pendingUpdateGate) { pending = pendingUpdate; pendingUpdate = null; }
            if (pending == null) return;
            if (pending.CheckResult != null)
            {
                control.ShowUpdateResult(pending.CheckResult, pending.CurrentVersion, settings.SkippedVersion);
                if (pending.CheckResult.Status == UpdateCheckStatus.UpdateAvailable && pending.CheckResult.Release != null &&
                    !string.Equals(settings.SkippedVersion, pending.CheckResult.Release.Version.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    status.Text = Localization.Get(settings.Language, "UpdateAvailable", pending.CurrentVersion.ToString(3),
                        pending.CheckResult.Release.Version, pending.CheckResult.Release.Notes);
                }
                else if (pending.UserInitiated && pending.CheckResult.Status == UpdateCheckStatus.Failed)
                {
                    status.Text = Localization.Get(settings.Language, "CheckFailed", Localization.Get(settings.Language, "NetworkFailure"));
                }
            }
            else if (pending.PreparedUpdate != null)
            {
                control.SetUpdatePrepared(pending.PreparedUpdate.StagedDllPath);
                status.Text = Localization.Get(settings.Language, "UpdatePreparedStatus");
            }
            else if (!string.IsNullOrEmpty(pending.Error))
            {
                var message = LocalizeUpdateError(pending.Error);
                control.SetUpdateError(message);
                status.Text = Localization.Get(settings.Language, "UpdateFailed", message);
            }
        }

        private string LocalizeUpdateError(string code)
        {
            switch (code)
            {
                case "UpdateAssetMissing": return Localization.Get(settings.Language, "UpdateAssetMissing");
                case "HashFailed": return Localization.Get(settings.Language, "HashFailed");
                case "InvalidPackage": return Localization.Get(settings.Language, "InvalidPackage");
                default: return Localization.Get(settings.Language, "UpdateFailureGeneric");
            }
        }

        private sealed class PendingUpdate
        {
            public UpdateCheckResult CheckResult { get; private set; }
            public Version CurrentVersion { get; private set; }
            public bool UserInitiated { get; private set; }
            public PreparedUpdate PreparedUpdate { get; private set; }
            public string Error { get; private set; }

            public static PendingUpdate Checked(UpdateCheckResult result, Version version, bool initiated) =>
                new PendingUpdate { CheckResult = result, CurrentVersion = version, UserInitiated = initiated };
            public static PendingUpdate Prepared(PreparedUpdate update) => new PendingUpdate { PreparedUpdate = update };
            public static PendingUpdate Failed(string error) => new PendingUpdate { Error = error };
        }
    }
}
