using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FlipY
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }

    public class MainForm : Form
    {
        private const int WH_MOUSE_LL = 14;
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID = 0x1000;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_ALT = 0x0001;

        private readonly NotifyIcon trayIcon;
        private readonly CheckBox invertToggle;
        private readonly NativeMethods.LowLevelMouseProc hookProc;
        private IntPtr hookHandle = IntPtr.Zero;
        private bool isInverted;
        private bool processingSynthetic;
        private Point lastPoint;
        private bool hasLastPoint;
        private Point? syntheticExpected;
        private int syntheticSuppressUntil;

        public MainForm()
        {
            Text = "FlipY";
            ClientSize = new Size(360, 180);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;

            invertToggle = new CheckBox
            {
                Text = "Invert Y: OFF",
                Appearance = Appearance.Button,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font(FontFamily.GenericSansSerif, 18F, FontStyle.Bold),
                Size = new Size(320, 100),
                Location = new Point(20, 30),
                BackColor = Color.LightGray,
                FlatStyle = FlatStyle.Flat,
            };
            invertToggle.Click += (sender, args) => ToggleInversion();
            Controls.Add(invertToggle);

            trayIcon = new NotifyIcon
            {
                Text = "FlipY: OFF",
                Icon = SystemIcons.Application,
                Visible = true,
                ContextMenuStrip = new ContextMenuStrip(),
            };

            trayIcon.ContextMenuStrip.Items.Add("Toggle Invert Y", null, (s, e) => ToggleInversion());
            trayIcon.ContextMenuStrip.Items.Add("Exit", null, (s, e) => Close());
            trayIcon.Click += TrayIcon_Click;

            hookProc = MouseHookCallback;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            RegisterHotKey();
            SetMouseHook();
            RegisterRawMouse();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            trayIcon.Visible = false;
            UnregisterHotKey();
            ReleaseMouseHook();
            UnregisterRawMouse();
            trayIcon.Dispose();
            base.OnFormClosing(e);
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_INPUT = 0x00FF;
            if (m.Msg == WM_HOTKEY && m.WParam == (IntPtr)HOTKEY_ID)
            {
                ToggleInversion();
            }

            if (m.Msg == WM_INPUT)
            {
                ProcessRawInput(m.LParam);
            }

            base.WndProc(ref m);
        }

        private void TrayIcon_Click(object? sender, EventArgs e)
        {
            ToggleInversion();
        }

        private void ToggleInversion()
        {
            SetInversion(!isInverted);
        }

        private void SetInversion(bool enabled)
        {
            isInverted = enabled;
            invertToggle.Checked = enabled;
            invertToggle.Text = enabled ? "Invert Y: ON" : "Invert Y: OFF";
            invertToggle.BackColor = enabled ? Color.LightGreen : Color.LightGray;
            trayIcon.Text = enabled ? "FlipY: ON" : "FlipY: OFF";
            trayIcon.BalloonTipTitle = "FlipY";
            trayIcon.BalloonTipText = enabled ? "Y-axis inversion is enabled." : "Y-axis inversion is disabled.";
            trayIcon.ShowBalloonTip(500);
            if (!enabled)
            {
                hasLastPoint = false;
            }
        }

        private void SetMouseHook()
        {
            if (hookHandle != IntPtr.Zero)
                return;

            using var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            using var currentModule = currentProcess.MainModule;
            var moduleHandle = NativeMethods.GetModuleHandle(currentModule?.ModuleName ?? string.Empty);
            hookHandle = NativeMethods.SetWindowsHookEx(WH_MOUSE_LL, hookProc, moduleHandle, 0);

            if (hookHandle == IntPtr.Zero)
            {
                MessageBox.Show("Unable to install the mouse hook.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ReleaseMouseHook()
        {
            if (hookHandle != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(hookHandle);
                hookHandle = IntPtr.Zero;
            }
        }

        private void RegisterHotKey()
        {
            if (!NativeMethods.RegisterHotKey(Handle, HOTKEY_ID, MOD_CONTROL | MOD_ALT, (uint)Keys.Y))
            {
                MessageBox.Show("Unable to register Ctrl+Alt+Y global hotkey. It may already be in use.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void UnregisterHotKey()
        {
            NativeMethods.UnregisterHotKey(Handle, HOTKEY_ID);
        }

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_MOUSEMOVE && !processingSynthetic)
            {
                var mouseInfo = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                var current = new Point(mouseInfo.pt.x, mouseInfo.pt.y);

                // If we recently set the cursor to a synthetic position, ignore the resulting event
                if (syntheticExpected.HasValue && current == syntheticExpected.Value)
                {
                    syntheticExpected = null;
                    hasLastPoint = true;
                    lastPoint = current;
                    return NativeMethods.CallNextHookEx(hookHandle, nCode, wParam, lParam);
                }

                if (nCode >= 0 && isInverted && !IsCursorVisible() && hasLastPoint)
                {
                    var dy = current.Y - lastPoint.Y;

                    if (dy != 0)
                    {
                        // Only invert Y; keep the X position reported by the system to avoid drift
                        var newX = current.X;
                        var newY = lastPoint.Y - dy;

                        // Clamp to virtual screen bounds
                        var vs = SystemInformation.VirtualScreen;
                        newX = Math.Max(vs.Left, Math.Min(vs.Right - 1, newX));
                        newY = Math.Max(vs.Top, Math.Min(vs.Bottom - 1, newY));

                        processingSynthetic = true;
                        NativeMethods.SetCursorPos(newX, newY);
                        processingSynthetic = false;

                        // Expect the synthetic event and update tracking
                        syntheticExpected = new Point(newX, newY);
                        syntheticSuppressUntil = Environment.TickCount + 100;
                        lastPoint = new Point(newX, newY);
                        hasLastPoint = true;

                        return (IntPtr)1;
                    }
                }
                lastPoint = current;
                hasLastPoint = true;
            }

            return NativeMethods.CallNextHookEx(hookHandle, nCode, wParam, lParam);
        }

        private void RegisterRawMouse()
        {
            var rid = new NativeMethods.RAWINPUTDEVICE[1];
            rid[0].usUsagePage = 0x01; // Generic desktop controls
            rid[0].usUsage = 0x02; // Mouse
            rid[0].dwFlags = NativeMethods.RIDEV_INPUTSINK;
            rid[0].hwndTarget = Handle;
            if (!NativeMethods.RegisterRawInputDevices(rid, (uint)rid.Length, (uint)Marshal.SizeOf(typeof(NativeMethods.RAWINPUTDEVICE))))
            {
                // Non-fatal; continue without raw input
            }
        }

        private bool IsCursorVisible()
        {
            if (NativeMethods.GetCursorInfo(out var ci))
            {
                return (ci.flags & NativeMethods.CURSOR_SHOWING) != 0;
            }

            // If we can't query, assume visible to be safe
            return true;
        }

        private void UnregisterRawMouse()
        {
            var rid = new NativeMethods.RAWINPUTDEVICE[1];
            rid[0].usUsagePage = 0x01;
            rid[0].usUsage = 0x02;
            rid[0].dwFlags = NativeMethods.RIDEV_REMOVE;
            rid[0].hwndTarget = IntPtr.Zero;
            NativeMethods.RegisterRawInputDevices(rid, (uint)rid.Length, (uint)Marshal.SizeOf(typeof(NativeMethods.RAWINPUTDEVICE)));
        }

        private void ProcessRawInput(IntPtr lParam)
        {
            uint dwSize = 0;
            const uint RID_INPUT = 0x10000003;
            if (NativeMethods.GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref dwSize, (uint)Marshal.SizeOf(typeof(NativeMethods.RAWINPUTHEADER))) == 0 && dwSize > 0)
            {
                var buffer = Marshal.AllocHGlobal((int)dwSize);
                try
                {
                    if (NativeMethods.GetRawInputData(lParam, RID_INPUT, buffer, ref dwSize, (uint)Marshal.SizeOf(typeof(NativeMethods.RAWINPUTHEADER))) == dwSize)
                    {
                        var raw = Marshal.PtrToStructure<NativeMethods.RAWINPUT>(buffer);
                        if (raw.header.dwType == NativeMethods.RIM_TYPEMOUSE)
                        {
                            var dx = raw.data.mouse.lLastX;
                            var dy = raw.data.mouse.lLastY;
                            if (isInverted && !IsCursorVisible() && dy != 0)
                            {
                                NativeMethods.POINT cur;
                                NativeMethods.GetCursorPos(out cur);
                                var newX = cur.x; // keep current X
                                var newY = cur.y - dy; // invert Y delta

                                var vs = SystemInformation.VirtualScreen;
                                newX = Math.Max(vs.Left, Math.Min(vs.Right - 1, newX));
                                newY = Math.Max(vs.Top, Math.Min(vs.Bottom - 1, newY));

                                processingSynthetic = true;
                                NativeMethods.SetCursorPos(newX, newY);
                                processingSynthetic = false;

                                syntheticExpected = new Point(newX, newY);
                                syntheticSuppressUntil = Environment.TickCount + 100;
                                lastPoint = new Point(newX, newY);
                                hasLastPoint = true;
                            }
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
        }
    }

    internal static class NativeMethods
    {
        public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        // Cursor info
        public const uint CURSOR_SHOWING = 0x00000001;

        [StructLayout(LayoutKind.Sequential)]
        public struct CURSORINFO
        {
            public uint cbSize;
            public uint flags;
            public IntPtr hCursor;
            public POINT ptScreenPos;
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCursorInfo(out CURSORINFO pci);

        // Raw Input structures and functions
        public const uint RIDEV_INPUTSINK = 0x00000100;
        public const uint RIDEV_REMOVE = 0x00000001;
        public const uint RIM_TYPEMOUSE = 0x00000000;

        [StructLayout(LayoutKind.Sequential)]
        public struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool RegisterRawInputDevices([MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

        [StructLayout(LayoutKind.Sequential)]
        public struct RAWINPUTHEADER
        {
            public uint dwType;
            public uint dwSize;
            public IntPtr hDevice;
            public IntPtr wParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RAWMOUSE
        {
            public ushort usFlags;
            public uint ulButtons;
            public ushort usButtonFlags;
            public ushort usButtonData;
            public uint ulRawButtons;
            public int lLastX;
            public int lLastY;
            public uint ulExtraInformation;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct RAWDATA
        {
            [FieldOffset(0)] public RAWMOUSE mouse;
            // We only need mouse for this app
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RAWINPUT
        {
            public RAWINPUTHEADER header;
            public RAWDATA data;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCursorPos(out POINT lpPoint);
    }
}
