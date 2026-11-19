using System;
using System.Windows.Forms;

namespace Roxyz.Act.UI
{
    internal sealed class PluginLanguagePage : TabPage
    {
        internal static readonly string[] Codes = { "en", "ja", "zh-CN", "ko" };
        internal static readonly string[] Names = { "English", "日本語", "简体中文", "한국어" };
        private static readonly string[] Titles = { "Language", "言語", "语言", "언어" };
        private readonly ComboBox selector = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
        private bool applying;
        public event EventHandler LanguageChanged;
        public string LanguageCode => Codes[selector.SelectedIndex];

        public PluginLanguagePage(string code)
        {
            BackColor = System.Drawing.Color.White;
            Padding = new Padding(24);
            AutoScroll = true;
            selector.Items.AddRange(Names);
            var row = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
            row.Controls.Add(selector);
            Controls.Add(row);
            SetLanguage(code);
            selector.SelectedIndexChanged += delegate {
                Text = selector.AccessibleName = Titles[selector.SelectedIndex];
                if (!applying) LanguageChanged?.Invoke(this, EventArgs.Empty);
            };
        }

        public void SetLanguage(string code)
        {
            var primary = (code ?? "").Split('-', '_')[0].ToLowerInvariant();
            int index = primary == "ja" ? 1 : primary == "zh" ? 2 : primary == "ko" ? 3 : 0;
            applying = true;
            try {
                selector.SelectedIndex = index;
                Text = selector.AccessibleName = Titles[index];
            } finally { applying = false; }
        }
    }
}
