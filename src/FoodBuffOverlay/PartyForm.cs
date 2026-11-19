using System;
using System.Drawing;
using System.Windows.Forms;
using System.Linq;

namespace FoodBuffOverlay
{
    internal sealed class PartyForm : Form
    {
        private readonly Label rows = new Label { AutoSize = true, UseMnemonic = false };
        public PartyForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(45,32,27);
            ForeColor = Color.Wheat;
            Padding = new Padding(10,6,10,6);
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            ShowInTaskbar = false;
            TopMost = true;
            Controls.Add(rows);
        }
        protected override bool ShowWithoutActivation => true;
        protected override CreateParams CreateParams
        {
            get { var p = base.CreateParams; p.ExStyle |= 0x080800A0; return p; }
        }
        public void RefreshRows(PluginSettings settings, FfxivDataSource source, FoodBuffLogTracker tracker, Rectangle anchor)
        {
            if (!settings.PartyOverlay || source.Party.Count == 0 || (settings.DutyOnly && !source.InDuty))
            {
                if (Visible) Hide();
                return;
            }
            var text = string.Join(Environment.NewLine, source.Party.Select(member =>
            {
                var state = tracker.ResolveMember(member.Item3.At(DateTime.UtcNow, settings.WarningMinutes), member.Item1, settings.WarningMinutes);
                return member.Item2 + "    " + (state.Condition == FoodBuffCondition.Missing
                    ? Localization.Get(settings.Language,"OverlayMissing") : OverlayForm.FormatRemaining(state.Remaining));
            }));
            if (rows.Text != text) rows.Text = text;
            var area = Screen.FromRectangle(anchor).WorkingArea;
            Location = new Point(Math.Max(area.Left, Math.Min(anchor.Left,area.Right-Width)), anchor.Bottom+4+Height <= area.Bottom ? anchor.Bottom+4 : Math.Max(area.Top,anchor.Top-Height-4));
            if (!Visible) Show();
        }
    }
}
