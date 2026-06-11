using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace KeyboardLock
{
    static class NativeMethods
    {
        public const int WH_KEYBOARD_LL = 13;
        public const int WM_KEYDOWN = 0x0100;

        public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);
    }

    class LockForm : Form
    {
        private IntPtr _hookId = IntPtr.Zero;
        private NativeMethods.LowLevelKeyboardProc _hookProc;
        private Button _btnUnlock;
        private Timer _pulseTimer;
        private float _pulsePhase = 0f;

        public LockForm()
        {
            // Keep reference so GC doesn't collect the delegate
            _hookProc = HookCallback;

            // Form setup
            this.Text = "Keyboard Lock";
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Size = new Size(420, 210);
            this.TopMost = true;
            this.ShowInTaskbar = true;
            this.BackColor = Color.FromArgb(28, 28, 48);
            this.DoubleBuffered = true;

            // Rounded corners on Windows 11+
            try
            {
                SetRoundedCorners();
            }
            catch { }

            // Button
            _btnUnlock = new Button();
            _btnUnlock.Text = "\U0001F513  Re-enable Keyboard && Exit";
            _btnUnlock.Size = new Size(340, 48);
            _btnUnlock.Location = new Point((this.ClientSize.Width - 340) / 2, 140);
            _btnUnlock.FlatStyle = FlatStyle.Flat;
            _btnUnlock.FlatAppearance.BorderSize = 1;
            _btnUnlock.FlatAppearance.BorderColor = Color.FromArgb(80, 120, 200);
            _btnUnlock.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 80, 160);
            _btnUnlock.BackColor = Color.FromArgb(40, 50, 90);
            _btnUnlock.ForeColor = Color.FromArgb(220, 230, 255);
            _btnUnlock.Font = new Font("Segoe UI", 11f, FontStyle.Regular);
            _btnUnlock.Cursor = Cursors.Hand;
            _btnUnlock.Click += (s, e) => this.Close();
            this.Controls.Add(_btnUnlock);

            // Subtle pulse animation
            _pulseTimer = new Timer();
            _pulseTimer.Interval = 50;
            _pulseTimer.Tick += (s, e) =>
            {
                _pulsePhase += 0.08f;
                this.Invalidate(new Rectangle(0, 0, this.Width, 130));
            };
            _pulseTimer.Start();

            // Install keyboard hook
            InstallHook();
        }

        private void SetRoundedCorners()
        {
            // DWM rounded corners (Windows 11)
            int attr = 33; // DWMWA_WINDOW_CORNER_PREFERENCE
            int pref = 2;  // DWMWCP_ROUND
            DwmSetWindowAttribute(this.Handle, attr, ref pref, sizeof(int));
        }

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private void InstallHook()
        {
            using (var proc = Process.GetCurrentProcess())
            using (var mod = proc.MainModule)
            {
                _hookId = NativeMethods.SetWindowsHookEx(
                    NativeMethods.WH_KEYBOARD_LL,
                    _hookProc,
                    NativeMethods.GetModuleHandle(mod.ModuleName),
                    0);
            }

            if (_hookId == IntPtr.Zero)
            {
                MessageBox.Show(
                    "Failed to install keyboard hook.\nTry running as Administrator.",
                    "Keyboard Lock",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                Environment.Exit(1);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                // Block ALL keyboard events
                return (IntPtr)1;
            }
            return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // Gradient background
            using (var bgBrush = new LinearGradientBrush(
                this.ClientRectangle,
                Color.FromArgb(28, 28, 48),
                Color.FromArgb(18, 22, 38),
                LinearGradientMode.Vertical))
            {
                g.FillRectangle(bgBrush, this.ClientRectangle);
            }

            // Subtle glow circle behind icon (pulsing)
            float pulse = (float)(0.5 + 0.5 * Math.Sin(_pulsePhase));
            int alpha = (int)(20 + 30 * pulse);
            float radius = 38 + 8 * pulse;
            using (var glowBrush = new SolidBrush(Color.FromArgb(alpha, 80, 140, 255)))
            {
                g.FillEllipse(glowBrush, 30 - radius / 2 + 18, 28 - radius / 2 + 18, radius, radius);
            }

            // Keyboard icon
            using (var iconFont = new Font("Segoe UI Emoji", 28f))
            {
                g.DrawString("\u2328", iconFont, new SolidBrush(Color.FromArgb(140, 190, 255)), 16, 14);
            }

            // Lock icon
            using (var lockFont = new Font("Segoe UI Emoji", 14f))
            {
                g.DrawString("\U0001F512", lockFont, new SolidBrush(Color.FromArgb(255, 180, 80)), 52, 10);
            }

            // Title
            using (var titleFont = new Font("Segoe UI", 18f, FontStyle.Bold))
            {
                g.DrawString("Keyboard Locked", titleFont,
                    new SolidBrush(Color.FromArgb(235, 240, 255)), 82, 22);
            }

            // Subtitle
            using (var subFont = new Font("Segoe UI", 10f))
            {
                g.DrawString("All keys are disabled \u2014 safe to clean your keyboard.",
                    subFont, new SolidBrush(Color.FromArgb(140, 150, 180)), 84, 58);
            }

            // Thin accent line
            using (var linePen = new Pen(Color.FromArgb(50, 80, 140, 255), 1.5f))
            {
                g.DrawLine(linePen, 40, 90, this.Width - 40, 90);
            }

            // Status indicator dot
            using (var dotBrush = new SolidBrush(Color.FromArgb((int)(180 + 75 * pulse), 100, 220, 100)))
            {
                g.FillEllipse(dotBrush, 44, 106, 10, 10);
            }
            using (var statusFont = new Font("Segoe UI", 9f))
            {
                g.DrawString("Hook active  \u2022  Click button or use mouse to exit",
                    statusFont, new SolidBrush(Color.FromArgb(120, 130, 160)), 62, 104);
            }

            // Border
            using (var borderPen = new Pen(Color.FromArgb(60, 80, 130, 220), 1.5f))
            {
                g.DrawRectangle(borderPen, 0, 0, this.Width - 1, this.Height - 1);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Always allow closing
            if (_pulseTimer != null) _pulseTimer.Stop();
            if (_hookId != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
            base.OnFormClosing(e);
        }

        // Allow dragging the borderless window
        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x84;
            const int HTCAPTION = 2;

            if (m.Msg == WM_NCHITTEST)
            {
                base.WndProc(ref m);
                // Only allow drag on the top area (not the button)
                int lp = m.LParam.ToInt32();
                Point pt = this.PointToClient(new Point(lp & 0xFFFF, lp >> 16));
                if (pt.Y < 130)
                    m.Result = (IntPtr)HTCAPTION;
                return;
            }
            base.WndProc(ref m);
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new LockForm());
        }
    }
}
