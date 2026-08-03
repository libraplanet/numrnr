using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Diagnostics;

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

        // <summary>
        // キーボードフック構造体
        // </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct KBDLLHOOKSTRUCT {
            public uint vkCode;      // 仮想キーコード
            public uint scanCode;    // ハードウェアスキャンコード
            public uint flags;       // イベントフラグ
            public uint time;        // タイムスタンプ
            public IntPtr dwExtraInfo; // 拡張情報
        }

        // <summary>
        // コールバックデリゲート定義
        // </summary>
        public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        // <summary>
        // フック設定関数
        // </summary>
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        // <summary>
        // フック解除関数
        // </summary>
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        // <summary>
        // 次のフック呼び出し関数
        // </summary>
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        // <summary>
        // モジュールハンドル取得関数
        // </summary>
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);
    }

    /// <summary>
    /// Main Program.
    /// </summary>
    class Program : IDisposable {
        NotifyIcon notifyIcon = new NotifyIcon();
        ContextMenuStrip contextMenuStrip = new ContextMenuStrip();
        ToolStripMenuItem toolStripMenuItemSetting = new ToolStripMenuItem();
        ToolStripMenuItem toolStripMenuItemExit = new ToolStripMenuItem();

        private Win32Api.LowLevelKeyboardProc _proc;
        private IntPtr _hookId = IntPtr.Zero;

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
                //notifyIcon
                this.notifyIcon.ContextMenuStrip = this.contextMenuStrip;
                this.notifyIcon.Visible = true;
                this.notifyIcon.Text = "Num R'n'R";
                this.notifyIcon.Icon = SystemIcons.Application;
                this.notifyIcon.DoubleClick += new EventHandler(delegate (object sender, EventArgs e) {
                });

                //toolStripMenuItemSetting
                this.toolStripMenuItemSetting.Text = "Settings (&S)";
                //this.toolStripMenuItemSetting.Font = new Font(this.toolStripMenuItemSetting.Font, FontStyle.Bold);
                this.toolStripMenuItemSetting.Enabled = false;
                this.toolStripMenuItemSetting.Click += new EventHandler(delegate (object sender, EventArgs e) {
                });

                //toolStripMenuItemExit
                this.toolStripMenuItemExit.Text = "Exit (&E)";
                this.toolStripMenuItemExit.Click += new EventHandler(delegate (object sender, EventArgs e) {
                    System.Windows.Forms.Application.Exit();
                });

                //contextMenuStrip
                this.contextMenuStrip.Items.Add(this.toolStripMenuItemSetting);
                this.contextMenuStrip.Items.Add(this.toolStripMenuItemExit);
            }
            this.contextMenuStrip.ResumeLayout(false);

            _proc = HookCallback;
            SetHook();
        }

        /// <summary>
        /// SetHook
        /// </summary>
        private void SetHook() {
            using(Process curProcess = Process.GetCurrentProcess())
            using(ProcessModule curModule = curProcess.MainModule) {
                _hookId = Win32Api.SetWindowsHookEx(
                    Win32Api.WH_KEYBOARD_LL,
                    _proc,
                    Win32Api.GetModuleHandle(curModule.ModuleName),
                    0
                );
            }
        }

        /// <summary>
        /// HookCallback
        /// </summary>
        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam) {
            if (nCode >= 0) {
                int wmMessage = wParam.ToInt32();
                if (wmMessage == Win32Api.WM_KEYDOWN || wmMessage == Win32Api.WM_SYSKEYDOWN) {
                    Win32Api.KBDLLHOOKSTRUCT hookStruct = (Win32Api.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Win32Api.KBDLLHOOKSTRUCT));
                }
            }
            return Win32Api.CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose() {
            if (_hookId != IntPtr.Zero) {
                Win32Api.UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }

            this.notifyIcon.Visible = false;
            this.notifyIcon.Dispose();
            this.contextMenuStrip.Dispose();
        }
    }
}
