using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace Roxyz.Act.UI
{
    internal enum UpdatePageState { Ready, Checking, Available, Preparing, Prepared, PrepareFailed }

    // Layout source: Food Buff Overlay v1.6.0, SettingsControl.BuildUpdatePanel.
    internal sealed class PluginUpdatePage : TabPage
    {
        private readonly Label updateTitle = NewLabel(), updateStatus = NewLabel(), authorVersion = NewLabel();
        private readonly CheckBox checkStartup = Check();
        private readonly Button checkNow = new Button(), updateNow = new Button(), later = new Button();
        private readonly string repositoryName, installedVersion;
        // WinForms may keep an already assigned, equal Font instance. Keep all
        // owned fonts alive until the controls are disposed (at most four pairs).
        private readonly Font[] bodyFonts = new Font[4], titleFonts = new Font[4];
        private int language;
        private bool settingStartup;
        public event EventHandler CheckRequested, InstallRequested, LaterRequested, StartupChanged;
        public bool CheckOnStartup { get { return checkStartup.Checked; } }

        public PluginUpdatePage(string repositoryName, string installedVersion, bool checkOnStartup, string languageCode)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(repositoryName ?? "", @"\A[A-Za-z0-9][A-Za-z0-9_.-]*\z"))
                throw new ArgumentException("Invalid repository name", "repositoryName");
            this.repositoryName = repositoryName;
            this.installedVersion = installedVersion;
            BackColor = Color.White;
            Padding = new Padding(4);
            Controls.Add(BuildUpdatePanel());
            checkStartup.Checked = checkOnStartup;
            checkStartup.CheckedChanged += delegate { if (!settingStartup) StartupChanged?.Invoke(this, EventArgs.Empty); };
            checkNow.Click += delegate { CheckRequested?.Invoke(this, EventArgs.Empty); };
            updateNow.Click += delegate { InstallRequested?.Invoke(this, EventArgs.Empty); };
            later.Click += delegate { LaterRequested?.Invoke(this, EventArgs.Empty); };
            SetLanguage(languageCode);
        }

        public void SetCheckOnStartup(bool value)
        {
            settingStartup = true;
            try { checkStartup.Checked = value; } finally { settingStartup = false; }
        }

        // The host supplies the localized status, including versions and release notes.
        public void ShowState(UpdatePageState state, string message)
        {
            bool offer = state == UpdatePageState.Available || state == UpdatePageState.PrepareFailed;
            updateNow.Visible = later.Visible = offer || state == UpdatePageState.Preparing;
            updateNow.Enabled = later.Enabled = offer;
            checkNow.Enabled = state != UpdatePageState.Preparing && state != UpdatePageState.Checking && state != UpdatePageState.Prepared;
            updateStatus.Text = message ?? "";
        }

        public void SetLanguage(string code)
        {
            string lang = (code ?? "").Split('-', '_')[0].ToLowerInvariant();
            language = lang == "ja" ? 1 : lang == "zh" ? 2 : lang == "ko" ? 3 : 0;
            string family = T("Segoe UI", "Yu Gothic UI", "Microsoft YaHei UI", "Malgun Gothic");
            if (bodyFonts[language] == null)
                bodyFonts[language] = new Font(family, Control.DefaultFont.Size, FontStyle.Regular);
            if (titleFonts[language] == null)
                titleFonts[language] = new Font(family, 11f, FontStyle.Bold);
            Font = bodyFonts[language];
            updateTitle.Font = titleFonts[language];
            Text = T("Updates", "更新", "更新", "업데이트");
            updateTitle.Text = T("Updates", "更新", "更新", "업데이트");
            checkStartup.Text = T("Check for updates when the plugin starts", "プラグイン起動時に更新を確認する", "插件启动时检查更新", "플러그인 시작 시 업데이트 확인");
            checkNow.Text = T("Check now", "今すぐ確認", "立即检查", "지금 확인");
            updateNow.Text = T("Update", "更新する", "更新", "업데이트");
            later.Text = T("Later", "後で", "稍后", "나중에");
            authorVersion.Text = string.Format(T("Author: Roxyz0501   •   v{0}", "作者: Roxyz0501   •   v{0}", "作者: Roxyz0501   •   v{0}", "제작자: Roxyz0501   •   v{0}"), installedVersion);
        }

        private string T(string en, string ja, string zh, string ko)
        {
            return language == 1 ? ja : language == 2 ? zh : language == 3 ? ko : en;
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
            layout.Controls.Add(authorVersion, 0, 4);
            var repository = new LinkLabel { AutoSize = true, Text = "GitHub · Roxyz0501/" + repositoryName };
            repository.LinkClicked += delegate
            {
                try { Process.Start(new ProcessStartInfo("https://github.com/Roxyz0501/" + repositoryName) { UseShellExecute = true }); }
                catch { updateStatus.Text = T("Could not open GitHub.", "GitHub を開けませんでした。", "无法打开 GitHub。", "GitHub를 열 수 없습니다."); }
            };
            layout.Controls.Add(repository, 0, 5);
            panel.Controls.Add(layout);
            return panel;
        }

        private static void StylePrimary(Button button) { button.AutoSize = true; button.FlatStyle = FlatStyle.Flat; button.BackColor = Color.FromArgb(202, 101, 20); button.ForeColor = Color.White; button.FlatAppearance.BorderColor = Color.FromArgb(164, 79, 12); button.FlatAppearance.MouseOverBackColor = Color.FromArgb(224, 126, 31); button.FlatAppearance.MouseDownBackColor = Color.FromArgb(174, 83, 13); button.Padding = new Padding(10, 3, 10, 3); button.Visible = false; }
        private static void StyleSecondary(Button button) { button.AutoSize = true; button.FlatStyle = FlatStyle.Flat; button.BackColor = Color.White; button.ForeColor = Color.FromArgb(63, 76, 95); button.FlatAppearance.BorderColor = Color.FromArgb(171, 181, 196); button.Padding = new Padding(10, 3, 10, 3); }
        private static CheckBox Check() => new CheckBox { AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = Color.FromArgb(48, 61, 80) };
        private static Label NewLabel() => new Label { AutoSize = true, Anchor = AnchorStyles.Left };
        private static FlowLayoutPanel Flow() => new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
                for (int i = 0; i < bodyFonts.Length; i++)
                {
                    bodyFonts[i]?.Dispose(); bodyFonts[i] = null;
                    titleFonts[i]?.Dispose(); titleFonts[i] = null;
                }
        }
    }
}
