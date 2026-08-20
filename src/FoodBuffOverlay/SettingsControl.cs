using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace FoodBuffOverlay
{
    internal sealed class SettingsControl : UserControl
    {
        internal const string SupportUrl = "https://ko-fi.com/roxyz0501";
        private readonly PluginSettings settings;
        private readonly CheckBox enabled = Check();
        private readonly CheckBox locked = Check();
        private readonly CheckBox preview = Check();
        private readonly CheckBox checkStartup = Check();
        private readonly NumericUpDown minutes = new NumericUpDown { Minimum = 1, Maximum = 60, Width = 74 };
        private readonly TrackBar opacity = new TrackBar { Minimum = 35, Maximum = 100, TickFrequency = 5, Width = 220 };
        private readonly Label opacityValue = NewLabel();
        private readonly Label connection = NewLabel();
        private readonly ComboBox language = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
        private readonly Label languageLabel = NewLabel();
        private readonly Label languageHint = NewLabel();
        private readonly Label warningLabel = NewLabel();
        private readonly Label minuteSuffix = NewLabel();
        private readonly Label opacityLabel = NewLabel();
        private readonly Label hint = NewLabel();
        private readonly Label authorVersion = NewLabel();
        private readonly Label headerTitle = NewLabel();
        private readonly Label headerSubtitle = NewLabel();
        private readonly Label updateTitle = NewLabel();
        private readonly Label updateStatus = NewLabel();
        private readonly Button checkNow = new Button();
        private readonly Button updateNow = new Button();
        private readonly Button later = new Button();
        private readonly Label supportHeading = NewLabel();
        private readonly Label supportBody = NewLabel();
        private readonly Label supportOptional = NewLabel();
        private readonly Button supportButton = new Button();
        private readonly Label supportStatus = NewLabel();
        private readonly TabControl tabs = new TabControl();
        private readonly TabPage settingsTab = new TabPage();
        private readonly TabPage updateTab = new TabPage();
        private readonly TabPage supportTab = new TabPage();
        private int hoveredTabIndex = -1;
        private bool loading = true;
        private string updateMessageKey = "RepositoryNotConfigured";
        private object[] updateMessageArgs = new object[0];

        public event EventHandler SettingsChanged;
        public event EventHandler CheckUpdatesRequested;
        public event EventHandler InstallUpdateRequested;
        public event EventHandler SkipUpdateRequested;
        public ReleaseInfo AvailableRelease { get; private set; }

        public SettingsControl(PluginSettings value)
        {
            settings = value;
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(244, 247, 251);
            Padding = new Padding(14);

            enabled.Checked = settings.OverlayEnabled;
            locked.Checked = settings.Locked;
            preview.Checked = settings.PositionPreview;
            checkStartup.Checked = settings.CheckUpdatesOnStartup;
            minutes.Value = settings.WarningMinutes;
            opacity.Value = settings.OpacityPercent;
            opacityValue.Text = opacity.Value + "%";
            language.Items.AddRange(new object[]
            {
                new LanguageItem("English", Localization.English),
                new LanguageItem("日本語", Localization.Japanese),
                new LanguageItem("简体中文", Localization.Chinese),
                new LanguageItem("한국어", Localization.Korean)
            });
            language.SelectedIndex = LanguageIndex(settings.Language);

            var header = BuildHeader();
            tabs.Dock = DockStyle.Fill;
            tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabs.Padding = new Point(18, 6);
            settingsTab.BackColor = Color.White;
            settingsTab.Padding = new Padding(4);
            updateTab.BackColor = Color.White;
            updateTab.Padding = new Padding(4);
            supportTab.BackColor = Color.White;
            supportTab.Padding = new Padding(4);
            settingsTab.Controls.Add(BuildSettingsPanel());
            updateTab.Controls.Add(BuildUpdatePanel());
            supportTab.Controls.Add(BuildSupportPanel());
            tabs.TabPages.Add(settingsTab);
            tabs.TabPages.Add(updateTab);
            tabs.TabPages.Add(supportTab);
            tabs.DrawItem += DrawTab;
            tabs.MouseMove += OnTabsMouseMove;
            tabs.MouseLeave += delegate { hoveredTabIndex = -1; tabs.Invalidate(); };

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.Controls.Add(header, 0, 0);
            root.Controls.Add(tabs, 0, 1);
            Controls.Add(root);

            enabled.CheckedChanged += Changed;
            locked.CheckedChanged += Changed;
            preview.CheckedChanged += Changed;
            checkStartup.CheckedChanged += Changed;
            minutes.ValueChanged += Changed;
            opacity.ValueChanged += Changed;
            language.SelectedIndexChanged += LanguageChanged;
            checkNow.Click += delegate { CheckUpdatesRequested?.Invoke(this, EventArgs.Empty); };
            updateNow.Click += delegate { InstallUpdateRequested?.Invoke(this, EventArgs.Empty); };
            later.Click += delegate { SkipUpdateRequested?.Invoke(this, EventArgs.Empty); };
            supportButton.Click += OpenSupportLink;
            ApplyLanguage();
            loading = false;
        }

        private Control BuildHeader()
        {
            var header = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            header.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (var pen = new Pen(Color.FromArgb(202, 165, 87), 2f)) e.Graphics.DrawLine(pen, 0, header.Height - 2, header.Width, header.Height - 2);
            };
            headerTitle.AutoSize = true; headerTitle.Location = new Point(18, 9); headerTitle.ForeColor = Color.FromArgb(34, 44, 61);
            headerSubtitle.AutoSize = true; headerSubtitle.Location = new Point(20, 43); headerSubtitle.ForeColor = Color.FromArgb(103, 116, 137);
            header.Controls.Add(headerTitle); header.Controls.Add(headerSubtitle);
            return header;
        }

        private Control BuildSettingsPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(20), AutoScroll = true };
            var layout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 9 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 270));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            AddWide(layout, enabled, 0); AddWide(layout, locked, 1); AddWide(layout, preview, 2);
            layout.Controls.Add(warningLabel, 0, 3);
            var minutePanel = Flow(); minutePanel.Controls.Add(minutes); minutePanel.Controls.Add(minuteSuffix); layout.Controls.Add(minutePanel, 1, 3);
            layout.Controls.Add(opacityLabel, 0, 4);
            var opacityPanel = Flow(); opacityPanel.Controls.Add(opacity); opacityValue.Margin = new Padding(5, 10, 0, 0); opacityPanel.Controls.Add(opacityValue); layout.Controls.Add(opacityPanel, 1, 4);
            layout.Controls.Add(languageLabel, 0, 5);
            var languagePanel = Flow(); languagePanel.Controls.Add(language); languageHint.Margin = new Padding(8, 6, 0, 0); languagePanel.Controls.Add(languageHint); layout.Controls.Add(languagePanel, 1, 5);
            hint.ForeColor = Color.FromArgb(80, 80, 88); hint.Padding = new Padding(0, 9, 0, 4); AddWide(layout, hint, 6);
            connection.ForeColor = Color.FromArgb(42, 109, 158); connection.AutoSize = false; connection.Height = 30; AddWide(layout, connection, 7);
            authorVersion.ForeColor = Color.FromArgb(113, 126, 146); AddWide(layout, authorVersion, 8);
            for (var i = 0; i < 3; i++) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            panel.Controls.Add(layout);
            return panel;
        }

        private Control BuildUpdatePanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(24), AutoScroll = true };
            var layout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 4, Padding = new Padding(4) };
            updateTitle.ForeColor = Color.FromArgb(34, 44, 61); updateTitle.Margin = new Padding(0, 0, 0, 14); layout.Controls.Add(updateTitle, 0, 0);
            checkStartup.Margin = new Padding(0, 0, 0, 14); layout.Controls.Add(checkStartup, 0, 1);
            var updateButtons = Flow(); StyleSecondary(checkNow); StylePrimary(updateNow); StyleSecondary(later); later.Visible = false;
            updateButtons.Margin = new Padding(0, 0, 0, 12); updateButtons.Controls.Add(checkNow); updateButtons.Controls.Add(updateNow); updateButtons.Controls.Add(later); layout.Controls.Add(updateButtons, 0, 2);
            updateStatus.AutoSize = false; updateStatus.Width = 700; updateStatus.Height = 150; updateStatus.ForeColor = Color.FromArgb(73, 83, 98); layout.Controls.Add(updateStatus, 0, 3);
            panel.Controls.Add(layout);
            return panel;
        }

        private Control BuildSupportPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(255, 249, 239), Padding = new Padding(28), AutoScroll = true };
            var card = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 6, BackColor = Color.White, Padding = new Padding(28, 23, 28, 24) };
            supportHeading.ForeColor = Color.FromArgb(166, 83, 15); supportHeading.Margin = new Padding(0, 0, 0, 10); card.Controls.Add(supportHeading, 0, 0);
            supportBody.MaximumSize = new Size(700, 0); supportBody.ForeColor = Color.FromArgb(61, 68, 79); supportBody.Margin = new Padding(0, 0, 0, 8); card.Controls.Add(supportBody, 0, 1);
            supportOptional.MaximumSize = new Size(700, 0); supportOptional.ForeColor = Color.FromArgb(117, 76, 24); supportOptional.Margin = new Padding(0, 0, 0, 18); card.Controls.Add(supportOptional, 0, 2);
            supportButton.AutoSize = false; supportButton.Size = new Size(360, 50); supportButton.FlatStyle = FlatStyle.Flat; supportButton.BackColor = Color.FromArgb(202, 101, 20); supportButton.ForeColor = Color.White; supportButton.Cursor = Cursors.Hand; supportButton.Margin = new Padding(0, 0, 0, 10);
            supportButton.FlatAppearance.BorderColor = Color.FromArgb(164, 79, 12); supportButton.FlatAppearance.BorderSize = 1;
            supportButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(224, 126, 31); supportButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(174, 83, 13);
            card.Controls.Add(supportButton, 0, 3);
            var url = NewLabel(); url.Text = SupportUrl; url.ForeColor = Color.FromArgb(126, 104, 78); url.Margin = new Padding(1, 0, 0, 8); card.Controls.Add(url, 0, 4);
            supportStatus.ForeColor = Color.FromArgb(178, 62, 47); supportStatus.Margin = new Padding(0, 2, 0, 0); card.Controls.Add(supportStatus, 0, 5);
            panel.Controls.Add(card);
            return panel;
        }

        public void ApplyTo(PluginSettings value)
        {
            value.OverlayEnabled = enabled.Checked; value.Locked = locked.Checked; value.PositionPreview = preview.Checked;
            value.WarningMinutes = (int)minutes.Value; value.OpacityPercent = opacity.Value;
            value.Language = SelectedLanguage; value.CheckUpdatesOnStartup = checkStartup.Checked; value.Normalize();
        }

        public void SetConnection(FoodBuffState state, string localizedError)
        {
            if (IsDisposed) return;
            var unavailable = state.Condition == FoodBuffCondition.Unavailable;
            connection.Text = unavailable ? T("Connecting") + (string.IsNullOrWhiteSpace(localizedError) ? "" : " — " + localizedError) : T("Connected");
            connection.ForeColor = unavailable ? Color.FromArgb(184, 84, 62) : Color.FromArgb(42, 109, 158);
        }

        public void SetUpdateChecking()
        {
            AvailableRelease = null; updateNow.Visible = false; later.Visible = false; checkNow.Enabled = false;
            SetUpdateMessage("Checking");
        }

        public void ShowUpdateResult(UpdateCheckResult result, Version currentVersion, string skippedVersion)
        {
            checkNow.Enabled = true; updateNow.Visible = false; later.Visible = false; AvailableRelease = result == null ? null : result.Release;
            if (result == null) { SetUpdateMessage("CheckFailed", "Unknown result"); return; }
            switch (result.Status)
            {
                case UpdateCheckStatus.RepositoryNotConfigured: SetUpdateMessage("RepositoryNotConfigured"); break;
                case UpdateCheckStatus.NoStableRelease: SetUpdateMessage("NoStableRelease"); break;
                case UpdateCheckStatus.UpToDate: SetUpdateMessage("UpToDate", currentVersion.ToString(3)); break;
                case UpdateCheckStatus.Failed: SetUpdateMessage("CheckFailed", T("NetworkFailure")); break;
                case UpdateCheckStatus.UpdateAvailable:
                    if (result.Release != null && string.Equals(skippedVersion, result.Release.Version.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        SetUpdateMessage("UpdateSkipped", result.Release.Version); break;
                    }
                    SetUpdateMessage("UpdateAvailable", currentVersion.ToString(3), result.Release.Version, result.Release.Notes);
                    updateNow.Visible = true; later.Visible = true; break;
            }
        }

        public void SetUpdatePreparing() { updateNow.Enabled = false; later.Enabled = false; checkNow.Enabled = false; SetUpdateMessage("PreparingUpdate"); }
        public void SetUpdatePrepared() { updateNow.Visible = false; later.Visible = false; checkNow.Enabled = true; SetUpdateMessage("UpdatePrepared"); }
        public void SetUpdateError(string error) { updateNow.Enabled = true; later.Enabled = true; checkNow.Enabled = true; SetUpdateMessage("UpdateFailed", error); }

        private void LanguageChanged(object sender, EventArgs e)
        {
            if (loading) return;
            ApplyLanguage();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Changed(object sender, EventArgs e)
        {
            opacityValue.Text = opacity.Value + "%";
            if (!loading) SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ApplyLanguage()
        {
            var family = Localization.FontFamily(SelectedLanguage);
            ApplyFontFamily(this, family);
            headerTitle.Text = T("PluginTab"); headerSubtitle.Text = T("HeaderSubtitle");
            headerTitle.Font = new Font(family, 17f, FontStyle.Bold); headerSubtitle.Font = new Font(family, 8.5f);
            tabs.Font = new Font(family, 9f, FontStyle.Bold); settingsTab.Text = T("TabSettings"); updateTab.Text = T("TabUpdate"); supportTab.Text = T("TabSupport");
            enabled.Text = T("Enabled"); locked.Text = T("Locked"); preview.Text = T("Preview");
            warningLabel.Text = T("WarningThreshold"); minuteSuffix.Text = T("MinutesSuffix"); opacityLabel.Text = T("Opacity");
            languageLabel.Text = T("Language"); languageHint.Text = T("LanguageHint"); hint.Text = T("Hint");
            authorVersion.Text = T("AuthorVersion", Assembly.GetExecutingAssembly().GetName().Version.ToString(3));
            updateTitle.Text = T("UpdateSection"); updateTitle.Font = new Font(family, 11f, FontStyle.Bold);
            checkStartup.Text = T("CheckStartup"); checkNow.Text = T("CheckNow"); updateNow.Text = T("UpdateNow"); later.Text = T("Later");
            supportHeading.Text = T("SupportHeading"); supportHeading.Font = new Font(family, 18f, FontStyle.Bold);
            supportBody.Text = T("SupportBody"); supportBody.Font = new Font(family, 10f);
            supportOptional.Text = T("SupportOptional"); supportOptional.Font = new Font(family, 9.5f, FontStyle.Bold);
            supportButton.Text = T("SupportButton"); supportButton.Font = new Font(family, 11f, FontStyle.Bold);
            RefreshUpdateMessage(); tabs.Invalidate(); Invalidate(true);
        }

        private void SetUpdateMessage(string key, params object[] args) { updateMessageKey = key; updateMessageArgs = args ?? new object[0]; RefreshUpdateMessage(); }
        private void RefreshUpdateMessage() { updateStatus.Text = T(updateMessageKey, updateMessageArgs); }
        private string T(string key, params object[] args) => Localization.Get(SelectedLanguage, key, args);
        private string SelectedLanguage => language.SelectedItem is LanguageItem ? ((LanguageItem)language.SelectedItem).Code : Localization.English;

        private void OnTabsMouseMove(object sender, MouseEventArgs e)
        {
            var next = -1;
            for (var i = 0; i < tabs.TabCount; i++) if (tabs.GetTabRect(i).Contains(e.Location)) { next = i; break; }
            if (next == hoveredTabIndex) return; hoveredTabIndex = next; tabs.Invalidate();
        }

        private void DrawTab(object sender, DrawItemEventArgs e)
        {
            var support = tabs.TabPages[e.Index] == supportTab; var selected = e.Index == tabs.SelectedIndex; var hovered = e.Index == hoveredTabIndex;
            var background = selected ? Color.White : Color.FromArgb(240, 242, 245);
            var foreground = Color.FromArgb(42, 53, 69);
            if (support) { background = selected ? Color.FromArgb(255, 245, 226) : hovered ? Color.FromArgb(255, 238, 207) : Color.FromArgb(250, 231, 197); foreground = selected ? Color.FromArgb(137, 62, 7) : hovered ? Color.FromArgb(157, 72, 8) : Color.FromArgb(174, 84, 13); }
            else if (hovered && !selected) background = Color.FromArgb(247, 248, 250);
            using (var brush = new SolidBrush(background)) e.Graphics.FillRectangle(brush, e.Bounds);
            TextRenderer.DrawText(e.Graphics, tabs.TabPages[e.Index].Text, tabs.Font, e.Bounds, foreground, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            if (support && selected) using (var pen = new Pen(Color.FromArgb(213, 137, 32), 2f)) e.Graphics.DrawLine(pen, e.Bounds.Left + 4, e.Bounds.Bottom - 2, e.Bounds.Right - 4, e.Bounds.Bottom - 2);
        }

        private void OpenSupportLink(object sender, EventArgs e)
        {
            string error;
            if (TryOpenSupportLink(info => Process.Start(info), out error))
            {
                supportStatus.ForeColor = Color.FromArgb(78, 125, 65); supportStatus.Text = T("BrowserOpened");
            }
            else { supportStatus.ForeColor = Color.FromArgb(178, 62, 47); supportStatus.Text = T("LinkFailed", T("BrowserFailure")); }
        }

        internal static bool TryOpenSupportLink(Action<ProcessStartInfo> start, out string error)
        {
            try { if (start == null) throw new ArgumentNullException("start"); start(new ProcessStartInfo(SupportUrl) { UseShellExecute = true }); error = null; return true; }
            catch (Exception ex) { error = ex.GetBaseException().Message; return false; }
        }

        private static void ApplyFontFamily(Control root, string family)
        {
            root.Font = new Font(family, root.Font.Size, root.Font.Style);
            foreach (Control child in root.Controls) ApplyFontFamily(child, family);
        }

        private static int LanguageIndex(string code)
        {
            switch (Localization.NormalizeLanguage(code)) { case Localization.Japanese: return 1; case Localization.Chinese: return 2; case Localization.Korean: return 3; default: return 0; }
        }

        private static void StylePrimary(Button button) { button.AutoSize = true; button.FlatStyle = FlatStyle.Flat; button.BackColor = Color.FromArgb(202, 101, 20); button.ForeColor = Color.White; button.FlatAppearance.BorderColor = Color.FromArgb(164, 79, 12); button.FlatAppearance.MouseOverBackColor = Color.FromArgb(224, 126, 31); button.FlatAppearance.MouseDownBackColor = Color.FromArgb(174, 83, 13); button.Padding = new Padding(10, 3, 10, 3); button.Visible = false; }
        private static void StyleSecondary(Button button) { button.AutoSize = true; button.FlatStyle = FlatStyle.Flat; button.BackColor = Color.White; button.ForeColor = Color.FromArgb(63, 76, 95); button.FlatAppearance.BorderColor = Color.FromArgb(171, 181, 196); button.Padding = new Padding(10, 3, 10, 3); }
        private static CheckBox Check() => new CheckBox { AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = Color.FromArgb(48, 61, 80) };
        private static Label NewLabel() => new Label { AutoSize = true, Anchor = AnchorStyles.Left };
        private static FlowLayoutPanel Flow() => new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
        private static void AddWide(TableLayoutPanel layout, Control control, int row) { layout.Controls.Add(control, 0, row); layout.SetColumnSpan(control, 2); }

        private sealed class LanguageItem
        {
            public LanguageItem(string label, string code) { Label = label; Code = code; }
            public string Label { get; private set; }
            public string Code { get; private set; }
            public override string ToString() => Label;
        }
    }
}
