using System;
using System.Text;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection;

namespace numrnr {
    /// <summary>
    /// Win32 API
    /// </summary>
    static class Win32Api {
        public const int WH_KEYBOARD_LL = 13;
        public const int WM_KEYDOWN = 0x0100;
        public const int WM_KEYUP = 0x0101;
        public const int WM_SYSKEYDOWN = 0x0104;
        public const int WM_SYSKEYUP = 0x0105;

        public const int VK_NUMLOCK = 0x90;

        // Raw Input 用定義
        public const int WM_INPUT = 0x00FF;
        public const int RIDEV_INPUTSINK = 0x00000100;
        public const uint RID_INPUT = 0x10000003;

        // SendInput 用定義
        public const uint INPUT_KEYBOARD = 1;
        public const uint KEYEVENTF_KEYUP = 0x0002;
        public static readonly IntPtr INSIGNIA_REINJECT = new IntPtr(0x4E554D52); // "NUMR" マーク

        [StructLayout(LayoutKind.Sequential)]
        public struct KBDLLHOOKSTRUCT {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RAWINPUTDEVICE {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RAWINPUTHEADER {
            public uint dwType;
            public uint dwSize;
            public IntPtr hDevice;
            public IntPtr wParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct INPUT {
            [FieldOffset(0)]
            public uint type;
            [FieldOffset(4)]
            public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RAWKEYBOARD {
            public ushort MakeCode;
            public ushort Flags;
            public ushort Reserved;
            public ushort VKey;
            public uint Message;
            public IntPtr ExtraInformation;
        }

        public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        public static extern short GetKeyState(int keyCode);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
    }

    static class Const {
        public const string APP_NAME = "Num R'n'R";
    }

    /// <summary>
    /// RawInputReceiver.
    /// </summary>
    class RawInputReceiver : NativeWindow, IDisposable {
        public event Action<IntPtr, ushort, uint, IntPtr>? OnDeviceInput;

        public RawInputReceiver() {
            this.CreateHandle(new CreateParams());

            // キーボードの Raw Input 登録
            Win32Api.RAWINPUTDEVICE[] rid = new Win32Api.RAWINPUTDEVICE[1];
            rid[0].usUsagePage = 0x01; // Generic Desktop Controls
            rid[0].usUsage = 0x06;     // Keyboard
            rid[0].dwFlags = Win32Api.RIDEV_INPUTSINK; // バックグラウンドでも受信
            rid[0].hwndTarget = this.Handle;

            Win32Api.RegisterRawInputDevices(rid, (uint)rid.Length, (uint)Marshal.SizeOf(typeof(Win32Api.RAWINPUTDEVICE)));
        }
        protected override void WndProc(ref Message m) {
            if (m.Msg == Win32Api.WM_INPUT) {
                uint dwSize = 0;
                Win32Api.GetRawInputData(m.LParam, Win32Api.RID_INPUT, IntPtr.Zero, ref dwSize, (uint)Marshal.SizeOf(typeof(Win32Api.RAWINPUTHEADER)));
                if (dwSize > 0) {
                    IntPtr buffer = Marshal.AllocHGlobal((int)dwSize);
                    try {
                        if (Win32Api.GetRawInputData(m.LParam, Win32Api.RID_INPUT, buffer, ref dwSize, (uint)Marshal.SizeOf(typeof(Win32Api.RAWINPUTHEADER))) == dwSize) {
                            Win32Api.RAWINPUTHEADER header = (Win32Api.RAWINPUTHEADER)Marshal.PtrToStructure(buffer, typeof(Win32Api.RAWINPUTHEADER));
                            // keyboard (1)
                            if (header.dwType == 1) {
                                IntPtr keyboardPtr = buffer + Marshal.SizeOf(typeof(Win32Api.RAWINPUTHEADER));
                                Win32Api.RAWKEYBOARD rawKeyboard = (Win32Api.RAWKEYBOARD)Marshal.PtrToStructure(keyboardPtr, typeof(Win32Api.RAWKEYBOARD));
                                OnDeviceInput?.Invoke(header.hDevice, rawKeyboard.VKey, rawKeyboard.Message, rawKeyboard.ExtraInformation);
                            }
                        }
                    } finally {
                        Marshal.FreeHGlobal(buffer);
                    }
                }
            }
            base.WndProc(ref m);
        }
        public void Dispose() {
            this.DestroyHandle();
        }
    }

    class LogForm : Form {
        private TextBox textBox = new TextBox();
        public LogForm() {
            this.SuspendLayout();

            this.Size = new Size(600, 400);
            this.Text = $"{Const.APP_NAME} - Log";
            this.textBox.Multiline = true;
            this.textBox.ReadOnly = true;
            this.textBox.ForeColor = Color.Black;
            this.textBox.ScrollBars = ScrollBars.Both;
            this.textBox.WordWrap = false;
            this.textBox.Dock = DockStyle.Fill;
            this.Controls.Add(this.textBox);
            this.FormClosing += delegate(object sender, FormClosingEventArgs e) {
                if (e.CloseReason == CloseReason.UserClosing) {
                    e.Cancel = true;
                    this.Hide();
                }
            };

            this.ResumeLayout(false);
        }
        public void AppendLog(string message) {
#if DEBUG
            if (this.InvokeRequired) {
                this.Invoke(new Action<string>(AppendLog), message);
            } else {
                const int MAX_LINES = 500;
                string logLine = $"[{DateTime.Now:yyyy/MM/dd HH:mm:ss.fff}] {message}";
                this.textBox.Lines = this.textBox.Lines.Prepend(logLine).Take(MAX_LINES).ToArray();
            }
#endif
        }
    }

    /// <summary>
    /// Main Program.
    /// </summary>
    class Program : IDisposable {
        NotifyIcon notifyIcon = new NotifyIcon();
        ContextMenuStrip contextMenuStrip = new ContextMenuStrip();
        ToolStripMenuItem toolStripMenuItemLog = new ToolStripMenuItem();
        ToolStripMenuItem toolStripMenuItemInfo = new ToolStripMenuItem();
        ToolStripMenuItem toolStripMenuItemExit = new ToolStripMenuItem();

        LogForm logForm;

        private Win32Api.LowLevelKeyboardProc _proc;
        private IntPtr _hookId = IntPtr.Zero;
        private RawInputReceiver? _rawInputReceiver = null;
        private IntPtr _lastDeviceHandle = IntPtr.Zero;
        private bool _isChagedDevice = false;
        private Dictionary<IntPtr, bool> _deviceNumlockMap = new Dictionary<IntPtr, bool>();

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main() {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using(new numrnr.Program()) {
                System.Windows.Forms.Application.Run();
            }
        }

        /// <summary>
        /// constructor
        /// </summary>
        private Program() {
            this.contextMenuStrip.SuspendLayout();
            //design
            {
                // LogForm
                this.logForm = new LogForm();
                this.logForm.Hide();

                //notifyIcon
                this.notifyIcon.ContextMenuStrip = this.contextMenuStrip;
                this.notifyIcon.Visible = true;
                this.notifyIcon.Text = Const.APP_NAME;
                this.notifyIcon.Icon = SystemIcons.Application;
                this.notifyIcon.DoubleClick += new EventHandler(delegate (object sender, EventArgs e) {
                });

                //toolStripMenuItemLog
                this.toolStripMenuItemLog.Text = "Log (&L)";
                //this.toolStripMenuItemLog.Font = new Font(this.toolStripMenuItemLog.Font, FontStyle.Bold);
                this.toolStripMenuItemLog.Click += new EventHandler(delegate (object sender, EventArgs e) {
                    if(this.logForm.Visible) {
                        this.logForm.Hide();
                    } else {
                        this.logForm.Show();
                        this.logForm.Activate();
                    }
                });
#if DEBUG
                this.toolStripMenuItemLog.Enabled = true;
#else
                this.toolStripMenuItemLog.Enabled = false;
#endif

                //toolStripMenuItemInfo
                this.toolStripMenuItemInfo.Text = "Info (&I)";
                this.toolStripMenuItemInfo.Click += new EventHandler(delegate (object sender, EventArgs e) {
                    AssemblyConfigurationAttribute? configAttr = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyConfigurationAttribute>();
                    string buildConfig = configAttr?.Configuration ?? "Unknown";
                    string appName = "Num R'n'R";
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine($"-- {appName} --");
                    sb.AppendLine("Independent NumLock state manager for multiple keyboards.");
                    sb.AppendLine("");
                    sb.AppendLine("version: v.0.0.0.0.0.0.0.0.0.1");
                    sb.AppendLine("auther: libraplanet");
                    sb.AppendLine("license: MIT License");
                    sb.AppendLine($"build: {buildConfig}");

                    MessageBox.Show(
                        sb.ToString(),
                        appName,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                });

                //toolStripMenuItemExit
                this.toolStripMenuItemExit.Text = "Exit (&E)";
                this.toolStripMenuItemExit.Click += new EventHandler(delegate (object sender, EventArgs e) {
                    System.Windows.Forms.Application.Exit();
                });

                //contextMenuStrip
                this.contextMenuStrip.Items.Add(this.toolStripMenuItemLog);
                this.contextMenuStrip.Items.Add(this.toolStripMenuItemInfo);
                this.contextMenuStrip.Items.Add(this.toolStripMenuItemExit);
            }
            this.contextMenuStrip.ResumeLayout(false);

            this._rawInputReceiver = new RawInputReceiver();
            this._rawInputReceiver.OnDeviceInput += delegate(IntPtr hDevice, ushort vkey, uint message, IntPtr extraInfo) {
                if (extraInfo != Win32Api.INSIGNIA_REINJECT) {
                    bool isChagedDevice = this._isChagedDevice = (_lastDeviceHandle != IntPtr.Zero) && (_lastDeviceHandle != hDevice);
                    bool curIsNumLockOn = (Win32Api.GetKeyState(Win32Api.VK_NUMLOCK) & 0x0001) != 0;
                    if(vkey == Win32Api.VK_NUMLOCK) {
                        if (message == Win32Api.WM_KEYDOWN) {
                            // NUM LOCKが押下されたので、先読み
                            this._deviceNumlockMap[hDevice] = !curIsNumLockOn;
                            this._lastDeviceHandle = hDevice;
                        }
                    } else {
                        this.logForm.AppendLog($"[WM_INPUT] Dev: 0x{hDevice.ToInt64():X8}, numLock: {curIsNumLockOn}");
 
                        if(isChagedDevice && this._deviceNumlockMap.ContainsKey(hDevice)) {
                            bool expectedIsNumLockOn = this._deviceNumlockMap[hDevice];
                            if(curIsNumLockOn != expectedIsNumLockOn) {
                                ToggleNumLock();
                                curIsNumLockOn = expectedIsNumLockOn;
                            }
                        }
                        this._deviceNumlockMap[hDevice] = curIsNumLockOn;
                        this._lastDeviceHandle = hDevice;
                    }
                }
            };

            this._proc = HookCallback;
            // SetHook();
        }

        /// <summary>
        /// SetHook
        /// </summary>
        private void SetHook() {
            using(Process curProcess = Process.GetCurrentProcess())
            using(ProcessModule curModule = curProcess.MainModule) {
                this._hookId = Win32Api.SetWindowsHookEx(
                    Win32Api.WH_KEYBOARD_LL,
                    this._proc,
                    Win32Api.GetModuleHandle(curModule.ModuleName),
                    0
                );
            }
        }
        
        /// <summary>
        /// ToggleNumLock
        /// </summary>
        private void ToggleNumLock() {
            Win32Api.INPUT[] inputs = new Win32Api.INPUT[2];
            // Key Down
            inputs[0].type = Win32Api.INPUT_KEYBOARD;
            inputs[0].ki.wVk = (ushort)Win32Api.VK_NUMLOCK;
            inputs[0].ki.wScan = 0;
            inputs[0].ki.dwFlags = 0;
            inputs[0].ki.time = 0;
            inputs[0].ki.dwExtraInfo = Win32Api.INSIGNIA_REINJECT;

            // Key Up
            inputs[1].type = Win32Api.INPUT_KEYBOARD;
            inputs[1].ki.wVk = (ushort)Win32Api.VK_NUMLOCK;
            inputs[1].ki.wScan = 0;
            inputs[1].ki.dwFlags = Win32Api.KEYEVENTF_KEYUP;
            inputs[1].ki.time = 0;
            inputs[1].ki.dwExtraInfo = Win32Api.INSIGNIA_REINJECT;

            Win32Api.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(Win32Api.INPUT)));
        }

        /// <summary>
        /// HookCallback
        /// </summary>
        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam) {
            if (nCode >= 0) {
                int wmMessage = wParam.ToInt32();
                if (wmMessage == Win32Api.WM_KEYDOWN || wmMessage == Win32Api.WM_SYSKEYDOWN) {
                    Win32Api.KBDLLHOOKSTRUCT hookStruct = (Win32Api.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Win32Api.KBDLLHOOKSTRUCT));

                    if (hookStruct.dwExtraInfo != Win32Api.INSIGNIA_REINJECT) {
                        Keys key = (Keys)hookStruct.vkCode;
                        if (key == Keys.NumLock) {
                            // 何もしない
                            this.logForm.AppendLog($"[HOOK] NumLock Pressed | vkCode: 0x{hookStruct.vkCode:X2}, Device: 0x{_lastDeviceHandle.ToInt64():X8}");
                        } else {
                            this.logForm.AppendLog($"[HOOK] Key: {key} (0x{hookStruct.vkCode:X2}), Device: 0x{_lastDeviceHandle.ToInt64():X8} isChagedDevice: {_isChagedDevice}");
                            if(this._isChagedDevice && this._deviceNumlockMap.ContainsKey(_lastDeviceHandle)) {
                                bool curIsNumLockOn = (Win32Api.GetKeyState(Win32Api.VK_NUMLOCK) & 0x0001) != 0;
                                bool expectedIsNumLockOn = this._deviceNumlockMap[_lastDeviceHandle];
                                if(curIsNumLockOn != expectedIsNumLockOn) {
                                    ToggleNumLock();
                                }
                            }
                        }
                    }
                }
            }
            return Win32Api.CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose() {
            if (this._hookId != IntPtr.Zero) {
                Win32Api.UnhookWindowsHookEx(this._hookId);
                this._hookId = IntPtr.Zero;
            }

            _rawInputReceiver?.Dispose();

            this.notifyIcon.Visible = false;
            this.notifyIcon.Dispose();
            this.contextMenuStrip.Dispose();
        }
    }
}
