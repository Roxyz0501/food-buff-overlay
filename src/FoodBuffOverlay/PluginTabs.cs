using System;
using System.Drawing;
using System.Windows.Forms;

namespace Roxyz.Act.UI
{
    internal sealed class PluginTabs : TabControl
    {
        private int hoveredTabIndex = -1;
        private readonly TabPage supportTab;

        public PluginTabs(TabPage supportTab)
        {
            this.supportTab = supportTab;
            Dock = DockStyle.Fill;
            DrawMode = TabDrawMode.OwnerDrawFixed;
            SizeMode = TabSizeMode.Normal;
            Padding = new Point(18, 6);
            DrawItem += DrawTab;
            MouseMove += OnTabsMouseMove;
            MouseLeave += delegate { hoveredTabIndex = -1; Invalidate(); };
        }

        public void SetLanguageFont(string family)
        {
            if (!fonts.TryGetValue(family, out var font))
                fonts.Add(family, font = new Font(family, 9f, FontStyle.Bold));
            Font = font;
        }

        private readonly System.Collections.Generic.Dictionary<string, Font> fonts =
            new System.Collections.Generic.Dictionary<string, Font>(StringComparer.OrdinalIgnoreCase);

        // Append all functional pages through this entry point, including dynamically loaded pages.
        public void AddContentPage(TabPage page)
        {
            if (page == supportTab || !TabPages.Contains(supportTab)) TabPages.Add(page);
            else InsertContentPage(TabPages.IndexOf(supportTab), page);
        }

        public void InsertContentPage(int index, TabPage page)
        {
            // .NET Framework TabPages.Insert needs the native control even in a hidden ACT pane.
            var handle = Handle;
            TabPages.Insert(index, page);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (!disposing) return;
            foreach (var font in fonts.Values) font.Dispose();
            fonts.Clear();
        }

        private void OnTabsMouseMove(object sender, MouseEventArgs e)
        {
            var next = -1;
            for (var i = 0; i < this.TabCount; i++) if (this.GetTabRect(i).Contains(e.Location)) { next = i; break; }
            if (next == hoveredTabIndex) return; hoveredTabIndex = next; this.Invalidate();
        }

        private void DrawTab(object sender, DrawItemEventArgs e)
        {
            var support = this.TabPages[e.Index] == supportTab; var selected = e.Index == this.SelectedIndex; var hovered = e.Index == hoveredTabIndex;
            var background = selected ? Color.White : Color.FromArgb(240, 242, 245);
            var foreground = Color.FromArgb(42, 53, 69);
            if (support) { background = selected ? Color.FromArgb(255, 245, 226) : hovered ? Color.FromArgb(255, 238, 207) : Color.FromArgb(250, 231, 197); foreground = selected ? Color.FromArgb(137, 62, 7) : hovered ? Color.FromArgb(157, 72, 8) : Color.FromArgb(174, 84, 13); }
            else if (hovered && !selected) background = Color.FromArgb(247, 248, 250);
            using (var brush = new SolidBrush(background)) e.Graphics.FillRectangle(brush, e.Bounds);
            TextRenderer.DrawText(e.Graphics, this.TabPages[e.Index].Text, this.Font, e.Bounds, foreground, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            if (support && selected) using (var pen = new Pen(Color.FromArgb(213, 137, 32), 2f)) e.Graphics.DrawLine(pen, e.Bounds.Left + 4, e.Bounds.Bottom - 2, e.Bounds.Right - 4, e.Bounds.Bottom - 2);
        }

    }
}
