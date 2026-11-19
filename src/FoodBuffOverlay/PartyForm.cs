using System;
using System.Drawing;
using System.Windows.Forms;
using System.Linq;
using System.Runtime.InteropServices;

namespace FoodBuffOverlay
{
    internal sealed class PartyForm : Form
    {
        private Tuple<string, string, FoodBuffCondition>[] rows = Array.Empty<Tuple<string, string, FoodBuffCondition>>();
        private string language;
        private bool fitting;
        private const int VerticalMargin = 12;
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
            BackColor = Color.FromArgb(1, 2, 3);
            TransparencyKey = BackColor;
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw, true);
            ShowInTaskbar = false;
            Opacity = settings.PartyOpacityPercent / 100d;
            TopMost = true;
            this.settings = settings;
            locked = settings.PartyLocked;
            SetRosterSize(1);
            var saved = new Point(settings.PartyLeft, settings.PartyTop);
            Location = Screen.AllScreens.Any(s => s.WorkingArea.Contains(saved)) ? saved : Screen.PrimaryScreen.WorkingArea.Location;
            SizeChanged += (s, e) =>
            {
                if (fitting || locked || !Visible || rows.Length == 0) return;
                settings.PartyWidth = Width;
                settings.PartyRowHeight = Math.Max(24, Math.Min(90, (Height - VerticalMargin) / rows.Length));
            };
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
            get { var p = base.CreateParams; p.ExStyle |= 0x08000080; if (locked) p.ExStyle |= 0x20; return p; }
        }
        private void SetRosterSize(int count)
        {
            fitting = true;
            try
            {
                MinimumSize = new Size(180, VerticalMargin + count * 24);
                MaximumSize = new Size(620, VerticalMargin + count * 90);
                Size = new Size(settings.PartyWidth, VerticalMargin + count * settings.PartyRowHeight);
            }
            finally { fitting = false; }
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x84 && !locked)
            {
                var raw = message.LParam.ToInt64();
                var point = PointToClient(new Point((short)raw, (short)(raw >> 16)));
                int hit = OverlayForm.ResizeHit(point, ClientSize);
                if (hit != 0) { message.Result = new IntPtr(hit); return; }
            }
            base.WndProc(ref message);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (rows.Length == 0) return;
            OverlayForm.PaintFrame(e.Graphics, ClientSize, rows.Any(row => row.Item3 == FoodBuffCondition.Missing), locked);
            int height = (ClientSize.Height - VerticalMargin) / rows.Length;
            using (var font = new Font(Localization.FontFamily(language), Math.Max(9f, Math.Min(18f, height * .37f)), FontStyle.Bold))
            {
                const TextFormatFlags flags = TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
                for (int i = 0; i < rows.Length; i++)
                {
                    var row = rows[i];
                    int right = Math.Min(ClientSize.Width / 2, TextRenderer.MeasureText(e.Graphics, row.Item2, font).Width + 4);
                    int y = VerticalMargin / 2 + i * height;
                    var color = row.Item3 == FoodBuffCondition.Missing ? Color.FromArgb(255, 100, 106)
                        : row.Item3 == FoodBuffCondition.Expiring ? Color.FromArgb(255, 184, 72) : Color.Wheat;
                    TextRenderer.DrawText(e.Graphics, row.Item1, font, new Rectangle(15, y, Width - right - 31, height), Color.FloralWhite, flags);
                    TextRenderer.DrawText(e.Graphics, row.Item2, font, new Rectangle(Width - right - 12, y, right, height), color, flags | TextFormatFlags.Right);
                }
            }
        }

        public void RefreshRows(FfxivDataSource source, FoodBuffLogTracker tracker)
        {
            var opacity = settings.PartyOpacityPercent / 100d;
            if (Opacity != opacity) Opacity = opacity;
            bool changed = locked != settings.PartyLocked || language != settings.DisplayLanguage;
            language = settings.DisplayLanguage;
            if (locked != settings.PartyLocked)
            {
                locked = settings.PartyLocked;
                if (IsHandleCreated)
                {
                    var style = GetWindowLong(Handle, -20);
                    SetWindowLong(Handle, -20, locked ? style | 0x20 : style & ~0x20);
                }
            }
            if (!settings.PartyPreview && (!settings.PartyOverlay || source.Party.Count == 0 || (settings.PartyDutyOnly && !source.InDuty)))
            {
                if (Visible) Hide();
                return;
            }
            var now = DateTime.UtcNow;
            var next = source.Party.Select(member =>
            {
                var state = tracker.ResolveMember(member.Item3.At(now, settings.PartyWarningMinutes), member.Item1, settings.PartyWarningMinutes);
                var text = state.Condition == FoodBuffCondition.Unavailable ? "—" : state.Condition == FoodBuffCondition.Missing
                    ? Localization.Get(language, "OverlayMissing") : OverlayForm.FormatRemaining(state.Remaining);
                return Tuple.Create(member.Item2, text, state.Condition);
            }).ToArray();
            if (next.Length == 0)
                next = new[] { Tuple.Create("Player A", "11:59", settings.PartyWarningMinutes >= 12 ? FoodBuffCondition.Expiring : FoodBuffCondition.Active),
                    Tuple.Create("Player B", Localization.Get(language, "OverlayMissing"), FoodBuffCondition.Missing) };
            if (next.Length != rows.Length) SetRosterSize(next.Length);
            if (changed || !rows.SequenceEqual(next))
            {
                rows = next;
                AccessibleDescription = string.Join(Environment.NewLine, rows.Select(row => row.Item1 + " " + row.Item2));
                Invalidate();
            }
            if (!Visible) Show();
        }
    }
}
