using System;
using System.Drawing;
using System.Windows.Forms;
using System.Linq;
using System.Runtime.InteropServices;

namespace FoodBuffOverlay
{
    internal sealed class PartyForm : Form
    {
        private readonly Label rows = new Label { AutoSize = true, UseMnemonic = false };
        private readonly PluginSettings settings;
        private bool locked;
        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hwnd, int index, int value);
        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr w, IntPtr l);

        public PartyForm(PluginSettings settings)
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
            this.settings = settings;
            locked = settings.Locked;
            Controls.Add(rows);
            var saved = new Point(settings.PartyLeft, settings.PartyTop);
            Location = Screen.AllScreens.Any(s => s.WorkingArea.Contains(saved)) ? saved : Screen.PrimaryScreen.WorkingArea.Location;
            rows.MouseDown += StartDrag;
            MouseDown += StartDrag;
            LocationChanged += (s,e) =>
            {
                if (Visible && !locked) { settings.PartyLeft = Left; settings.PartyTop = Top; }
            };
        }
        private void StartDrag(object sender, MouseEventArgs e)
        {
            if (locked || e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, 0x00A1, new IntPtr(2), IntPtr.Zero);
        }
        protected override bool ShowWithoutActivation => true;
        protected override CreateParams CreateParams
        {
            get { var p = base.CreateParams; p.ExStyle |= 0x08080080; if (locked) p.ExStyle |= 0x20; return p; }
        }
        public void RefreshRows(FfxivDataSource source, FoodBuffLogTracker tracker)
        {
            if (locked != settings.Locked)
            {
                locked = settings.Locked;
                if (IsHandleCreated)
                {
                    var style = GetWindowLong(Handle, -20);
                    SetWindowLong(Handle, -20, locked ? style | 0x20 : style & ~0x20);
                }
            }
            if (!settings.PartyOverlay || (source.Party.Count == 0 && !settings.PositionPreview) || (settings.DutyOnly && !source.InDuty))
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
            if (source.Party.Count == 0) text = Localization.Get(settings.Language,"Party") + Environment.NewLine + Localization.Get(settings.Language,"OverlayPreview");
            if (rows.Text != text) rows.Text = text;
            if (!Visible) Show();
        }
    }
}
