using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FoodBuffOverlay
{
    internal sealed class OverlayForm : Form
    {
        private const int WM_NCHITTEST = 0x84, WM_NCLBUTTONDOWN = 0x00A1;
        private const int HTCAPTION = 2, HTLEFT = 10, HTRIGHT = 11, HTTOP = 12, HTTOPLEFT = 13,
            HTTOPRIGHT = 14, HTBOTTOM = 15, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;
        private const int WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80;
        private const int ResizeBorder = 7;
        private readonly Color transparencyColor = Color.FromArgb(1, 2, 3);
        private readonly Timer boundsTimer;
        private PluginSettings settings;
        private FoodBuffState state = FoodBuffState.Unavailable();
        private bool initialized;
        private bool appliedLocked;

        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam);

        public OverlayForm(PluginSettings settings)
        {
            this.settings = settings;
            appliedLocked = settings.Locked;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = transparencyColor;
            TransparencyKey = transparencyColor;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            DoubleBuffered = true;
            MinimumSize = new Size(180, 48);
            MaximumSize = new Size(620, 180);
            Font = new Font(Localization.FontFamily(settings.Language), 9f);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            boundsTimer = new Timer { Interval = 450 };
            boundsTimer.Tick += BoundsTimerTick;
            Move += BoundsChanged;
        }

        protected override bool ShowWithoutActivation => true;
        protected override CreateParams CreateParams
        {
            get
            {
                var value = base.CreateParams;
                value.ExStyle |= WS_EX_TOOLWINDOW;
                if (settings != null && settings.Locked) value.ExStyle |= WS_EX_TRANSPARENT;
                return value;
            }
        }

        public void ApplySettings(PluginSettings value, bool restoreBounds = false)
        {
            var lockChanged = appliedLocked != value.Locked;
            if (lockChanged && initialized && !appliedLocked)
            {
                boundsTimer.Stop();
                CaptureBounds();
            }
            settings = value;
            settings.Normalize();
            if (restoreBounds)
            {
                var desired = new Rectangle(settings.Left, settings.Top, settings.Width, settings.Height);
                if (!Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(desired)))
                    desired.Location = new Point(Screen.PrimaryScreen.WorkingArea.Left + 80, Screen.PrimaryScreen.WorkingArea.Top + 80);
                Bounds = desired;
            }
            Opacity = settings.OpacityPercent / 100d;
            appliedLocked = settings.Locked;
            if (lockChanged && IsHandleCreated) RecreateHandle();
            RefreshVisibility();
        }

        public void SetState(FoodBuffState value)
        {
            state = value ?? FoodBuffState.Unavailable();
            RefreshVisibility();
        }

        private void RefreshVisibility()
        {
            var alert = state.Condition == FoodBuffCondition.Missing || state.Condition == FoodBuffCondition.Expiring;
            var show = settings.OverlayEnabled && (alert || settings.PositionPreview);
            if (show) { if (!Visible) Show(); Invalidate(); }
            else if (Visible) Hide();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (settings.Locked || e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, WM_NCLBUTTONDOWN, new IntPtr(HTCAPTION), IntPtr.Zero);
        }

        private void BoundsChanged(object sender, EventArgs e)
        {
            if (!initialized || appliedLocked) return;
            boundsTimer.Stop(); boundsTimer.Start();
        }

        private void BoundsTimerTick(object sender, EventArgs e)
        {
            boundsTimer.Stop();
            if (!initialized || appliedLocked) return;
            CaptureBounds();
        }

        private void CaptureBounds()
        {
            if (!initialized) return;
            settings.Left = Left; settings.Top = Top; settings.Width = Width; settings.Height = Height;
        }

        public void StopAndFlushBounds()
        {
            boundsTimer.Stop();
            if (initialized && !appliedLocked) CaptureBounds();
        }

        protected override void OnShown(EventArgs e) { base.OnShown(e); initialized = true; UpdateRegion(); }
        protected override void OnResize(EventArgs e) { base.OnResize(e); UpdateRegion(); BoundsChanged(this, EventArgs.Empty); }
        private void UpdateRegion()
        {
            if (Width <= 0 || Height <= 0) return;
            using (var path = RoundedPath(new Rectangle(0, 0, Width, Height), 12)) Region = new Region(path);
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == WM_NCHITTEST && !settings.Locked)
            {
                var raw = message.LParam.ToInt64();
                var point = PointToClient(new Point(unchecked((short)(raw & 0xffff)), unchecked((short)((raw >> 16) & 0xffff))));
                var left = point.X <= ResizeBorder; var right = point.X >= ClientSize.Width - ResizeBorder;
                var top = point.Y <= ResizeBorder; var bottom = point.Y >= ClientSize.Height - ResizeBorder;
                if (left && top) { message.Result = new IntPtr(HTTOPLEFT); return; }
                if (right && top) { message.Result = new IntPtr(HTTOPRIGHT); return; }
                if (left && bottom) { message.Result = new IntPtr(HTBOTTOMLEFT); return; }
                if (right && bottom) { message.Result = new IntPtr(HTBOTTOMRIGHT); return; }
                if (left) { message.Result = new IntPtr(HTLEFT); return; }
                if (right) { message.Result = new IntPtr(HTRIGHT); return; }
                if (top) { message.Result = new IntPtr(HTTOP); return; }
                if (bottom) { message.Result = new IntPtr(HTBOTTOM); return; }
            }
            base.WndProc(ref message);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var preview = settings.PositionPreview && state.Condition != FoodBuffCondition.Missing && state.Condition != FoodBuffCondition.Expiring;
            var missing = !preview && state.Condition == FoodBuffCondition.Missing;
            var accent = missing ? Color.FromArgb(255, 79, 88) : Color.FromArgb(255, 184, 72);
            var darkAccent = missing ? Color.FromArgb(112, 25, 34) : Color.FromArgb(111, 70, 14);
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var background = new LinearGradientBrush(rect, Color.FromArgb(246, 24, 18, 22), Color.FromArgb(244, 57, 28, 23), LinearGradientMode.Horizontal))
            using (var path = RoundedPath(rect, 12)) e.Graphics.FillPath(background, path);
            using (var glow = new LinearGradientBrush(new Rectangle(0, 0, Math.Max(1, Width / 3), Height), Color.FromArgb(205, darkAccent), Color.FromArgb(0, darkAccent), LinearGradientMode.Horizontal))
            using (var path = RoundedPath(rect, 12)) e.Graphics.FillPath(glow, path);
            using (var bar = new SolidBrush(accent)) e.Graphics.FillRectangle(bar, 0, 9, 5, Height - 18);

            var main = preview ? Localization.Get(settings.Language, "OverlayPreview") : missing
                ? Localization.Get(settings.Language, "OverlayMissing")
                : Localization.Get(settings.Language, "OverlayRemaining", FormatRemaining(state.Remaining));
            var detail = preview ? Localization.Get(settings.Language, "OverlayPreviewDetail") : missing
                ? Localization.Get(settings.Language, "OverlayMissingDetail")
                : Localization.Get(settings.Language, "OverlayExpiringDetail");
            var family = Localization.FontFamily(settings.Language);
            var mainSize = Math.Max(11f, Math.Min(22f, Height * 0.27f));
            using (var mainFont = new Font(family, mainSize, FontStyle.Bold))
            using (var detailFont = new Font(family, Math.Max(7f, Math.Min(10f, Height * 0.13f))))
            using (var mainBrush = new SolidBrush(Color.FromArgb(255, 250, 239)))
            using (var detailBrush = new SolidBrush(Color.FromArgb(220, 244, 192, 157)))
            {
                var y = Math.Max(4, (Height - (int)(mainFont.Height + detailFont.Height + 3)) / 2);
                e.Graphics.DrawString(main, mainFont, mainBrush, new RectangleF(17, y, Width - 28, mainFont.Height + 3));
                e.Graphics.DrawString(detail, detailFont, detailBrush, new RectangleF(19, y + mainFont.Height + 1, Width - 30, detailFont.Height + 2));
            }
            using (var border = new Pen(Color.FromArgb(210, accent), 1.5f))
            using (var path = RoundedPath(rect, 12)) e.Graphics.DrawPath(border, path);
            if (!settings.Locked)
            {
                using (var pen = new Pen(Color.FromArgb(180, 255, 226, 174), 1f))
                {
                    e.Graphics.DrawLine(pen, Width - 17, Height - 6, Width - 6, Height - 17);
                    e.Graphics.DrawLine(pen, Width - 11, Height - 6, Width - 6, Height - 11);
                }
            }
        }

        internal static string FormatRemaining(TimeSpan value)
        {
            var total = Math.Max(0, (int)Math.Ceiling(value.TotalSeconds));
            return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
        }

        private static GraphicsPath RoundedPath(Rectangle rectangle, int radius)
        {
            var path = new GraphicsPath(); var diameter = radius * 2;
            path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure(); return path;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopAndFlushBounds();
                Move -= BoundsChanged;
                boundsTimer.Tick -= BoundsTimerTick;
                boundsTimer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
