using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Reflection;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

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
        public const uint RIDI_DEVICENAME = 0x20000007;

        // SendInput 用定義
        public const uint INPUT_KEYBOARD = 1;
        public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        public const uint KEYEVENTF_KEYUP = 0x0002;
        public static readonly IntPtr INSIGNIA_REINJECT = new IntPtr(0x4E554D52); // "NUMR" マーク

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

        [StructLayout(LayoutKind.Explicit, Size = 28)]
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

        public const int ATTACH_PARENT_PROCESS = -1;


        [DllImport("kernel32.dll")]
        public static extern bool AttachConsole(int dwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        public static extern short GetKeyState(int keyCode);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern uint GetRawInputDeviceInfo(IntPtr hDevice, uint uiCommand, IntPtr pData, ref uint pcbSize);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        public static extern uint MapVirtualKey(uint uCode, uint uMapType);

    }

    /// <summary>
    /// ConsoleLogger.
    /// コンソールへのロギング用クラス.
    ///
    /// USAGE:
    ///   private static readonly ConsoleLogger Log = new ConsoleLogger(typeof(MyClass));
    ///   Log.Info("hoge");
    /// </summary>
    public class ConsoleLogger {
        private readonly string _className;

        /// <summary>
        /// constractor.
        /// </summary>
        public ConsoleLogger(Type type) {
            this._className = type.Name;
        }

        /// <summary>
        /// Info.
        /// </summary>
        public void Info(string message, [CallerMemberName] string memberName = "") {
            if((Console.IsOutputRedirected) || (Console.Out != TextWriter.Null)) {
                Console.WriteLine($"[{DateTime.Now:yyyy/MM/dd HH:mm:ss.fff} {this._className}.{memberName}()] {message}");
            }
        }

        /// <summary>
        /// Info.
        /// </summary>
        public void Info(string message, Exception ex, [CallerMemberName] string memberName = "") {
            if((Console.IsOutputRedirected) || (Console.Out != TextWriter.Null)) {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"[{DateTime.Now:yyyy/MM/dd HH:mm:ss.fff} {this._className}.{memberName}()] {message}");
                sb.AppendLine("");
                sb.AppendLine("==== EXCEPTION DETAILS ==========");
                {
                    int depth = 0;
                    Exception? currentEx = ex;
                    while(currentEx != null) {
                        string indent = new string(' ', depth * 2);
                        sb.AppendLine($"{indent}[Type] {currentEx.GetType().FullName}");
                        sb.AppendLine($"{indent}[Message] {currentEx.Message}");
                        sb.AppendLine($"{indent}[HResult] 0x{currentEx.HResult:X8}");
                        if(currentEx.TargetSite != null) {
                            sb.AppendLine($"{indent}[TargetSite] {currentEx.TargetSite.DeclaringType?.FullName}.{currentEx.TargetSite.Name}");
                        }
                        sb.AppendLine($"{indent}[StackTrace]");
                        sb.AppendLine($"{currentEx.StackTrace}");

                        if(currentEx.InnerException!= null) {
                            sb.AppendLine($"{indent} +---> InnerException");
                        }

                        currentEx = currentEx.InnerException;
                        depth++;
                    }
                }
                sb.AppendLine("=================================");
                Console.WriteLine(sb.ToString());
            }
        }
    }

    /// <summary>
    /// Constants.
    /// </summary>
    static class Constants {
        public const string APP_NAME = "Num R'n'R";
    }

    /// <summary>
    /// Common.
    /// </summary>
    static class Common {
        private static readonly ConsoleLogger Log = new ConsoleLogger(typeof(Common));
        /// <summary>
        /// ToggleNumLock
        /// </summary>
        public static uint ToggleNumLock() {
            Win32Api.INPUT[] inputs = new Win32Api.INPUT[2];

            // VK_NUMLOCK から Hardware ScanCode を取得 (通常は 0x45)
            ushort scanCode = (ushort)Win32Api.MapVirtualKey((uint)Win32Api.VK_NUMLOCK, 0);

            // Key Down
            inputs[0].type = Win32Api.INPUT_KEYBOARD;
            inputs[0].ki.wVk = (ushort)Win32Api.VK_NUMLOCK;
            inputs[0].ki.wScan = scanCode;
            inputs[0].ki.dwFlags = Win32Api.KEYEVENTF_EXTENDEDKEY;
            inputs[0].ki.time = 0;
            inputs[0].ki.dwExtraInfo = Win32Api.INSIGNIA_REINJECT;

            // Key Up
            inputs[1].type = Win32Api.INPUT_KEYBOARD;
            inputs[1].ki.wVk = (ushort)Win32Api.VK_NUMLOCK;
            inputs[1].ki.wScan = scanCode;
            inputs[1].ki.dwFlags = Win32Api.KEYEVENTF_KEYUP | Win32Api.KEYEVENTF_EXTENDEDKEY;
            inputs[1].ki.time = 0;
            inputs[1].ki.dwExtraInfo = Win32Api.INSIGNIA_REINJECT;

            return Win32Api.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(Win32Api.INPUT)));
        }

        /// <summary>
        /// SafeToggleNumLock
        /// </summary>
        public static int SafeToggleNumLock() {
            try {
                uint result = ToggleNumLock();
                if(result == 0) {
                    int errorCode = Marshal.GetLastWin32Error();
                    if(errorCode == 0) {
                        return 1;
                    } else {
                        return errorCode;
                    }
                } else {
                    return 0;
                }
            } catch(Exception ex) {
                Log.Info($"[EX] SafeToggleNumLock", ex);
                int hr = Marshal.GetHRForException(ex);
                if(hr < 0) {
                    return hr;
                } else {
                    return -1;
                }
            }
        }
    }

    /// <summary>
    /// DeviceManager.
    /// </summary>
    static class DeviceManager {
        private static readonly ConsoleLogger Log = new ConsoleLogger(typeof(DeviceManager));
        private static readonly Dictionary<IntPtr, string> _deviceHardwareDict = new Dictionary<IntPtr, string>();
        private static readonly Regex _regexVidPid = new Regex(@"VID_[0-9A-Fa-f]{4}&PID_[0-9A-Fa-f]{4}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// getHardwareId (private module).
        /// </summary>
        private static string? _GetHardwareId(IntPtr hDevice) {
            uint pcbSize = 0;
            Win32Api.GetRawInputDeviceInfo(hDevice, Win32Api.RIDI_DEVICENAME, IntPtr.Zero, ref pcbSize);
            if(pcbSize  > 0) {
                int bytes = (int)pcbSize * Marshal.SystemDefaultCharSize;
                IntPtr pData = Marshal.AllocHGlobal(bytes);
                try {
                    if (Win32Api.GetRawInputDeviceInfo(hDevice, Win32Api.RIDI_DEVICENAME, pData, ref pcbSize) != uint.MaxValue) {
                        string devicePath = Marshal.PtrToStringAuto(pData) ?? "";
                        Match match = _regexVidPid.Match(devicePath);
                        if (match.Success) {
                            return match.Value.ToUpper();
                        } else {
                            return devicePath;
                        }
                    } else {
                        return null;
                    }
                } finally {
                    Marshal.FreeHGlobal(pData);
                }
            } else {
                return null;
            }
        }

        /// <summary>
        /// GetHardwareIdFix.
        /// </summary>
        public static string GetHardwareIdFix(IntPtr hDevice) {
            if(_deviceHardwareDict.ContainsKey(hDevice)) {
                return _deviceHardwareDict[hDevice];
            } else {
                string hexHandleInstance = $"0x{hDevice.ToInt64():X8}";
                try {
                    string hardwareId = _GetHardwareId(hDevice) ?? hexHandleInstance;
                    _deviceHardwareDict[hDevice] = hardwareId;
                    return hardwareId;
                } catch(Exception ex) {
                    Log.Info($"[EX] GetHardwareIdFix", ex);
                    return hexHandleInstance;
                }
            }
        }
    }

    /// <summary>
    /// RawInputReceiver.
    /// </summary>
    class RawInputReceiver : NativeWindow, IDisposable {
        private static readonly ConsoleLogger Log = new ConsoleLogger(typeof(RawInputReceiver));
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
                                try {
                                    IntPtr keyboardPtr = buffer + Marshal.SizeOf(typeof(Win32Api.RAWINPUTHEADER));
                                    Win32Api.RAWKEYBOARD rawKeyboard = (Win32Api.RAWKEYBOARD)Marshal.PtrToStructure(keyboardPtr, typeof(Win32Api.RAWKEYBOARD));
                                    OnDeviceInput?.Invoke(header.hDevice, rawKeyboard.VKey, rawKeyboard.Message, rawKeyboard.ExtraInformation);
                                } catch(Exception ex) {
                                    Log.Info($"[EX] RawInput OnDeviceInput", ex);
                                }
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

    class ControlPanelForm : Form {
        private static readonly ConsoleLogger Log = new ConsoleLogger(typeof(ControlPanelForm));
        public static readonly int MAX_MEMORIES = 8;
        private Button buttonToggle;
        private TextBox[] textBoxMemoryDevices = new TextBox[MAX_MEMORIES];
        private TextBox[] textBoxMemoryStatus = new TextBox[MAX_MEMORIES];
        private TextBox textBoxLog;
        private GroupBox groupBoxQuickActions;
        private GroupBox groupBoxDeviceStateMemory;
        private GroupBox groupBoxActivityLogs;


        private System.Windows.Forms.Timer _uiUpdateTimer;

        private readonly object _lockObj = new object();
        private readonly Dictionary<string, bool> _cacheNumlockStateDict = new Dictionary<string, bool>();
        private readonly List<string> _cacheLogLineList = new List<string>();

        public ControlPanelForm() {
            this.SuspendLayout();

            this.Size = new Size(800, 600);
            this.Text = $"{Constants.APP_NAME} - Control Panel";

            this.groupBoxQuickActions = new GroupBox();
            this.groupBoxQuickActions.Text = "Quick Actions";
            this.groupBoxQuickActions.Location = new Point(10, 10);
            this.groupBoxQuickActions.Size = new Size(760, 50);
            this.groupBoxQuickActions.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            this.Controls.Add(this.groupBoxQuickActions);

            this.buttonToggle = new Button();
            this.buttonToggle.Text = "toggle num lock.";
            this.buttonToggle.Location = new Point(10, 15);
            this.buttonToggle.Click += delegate(object sender, EventArgs e){
                Log.Info("buttonToggle.Click() => toggle numlock.");
                try {
                    int errorCode = Common.SafeToggleNumLock();
                    this.AppendLog($"toggle num lock! errorCode({errorCode})");
                } catch(Exception ex) {
                    Log.Info($"[EX] buttonToggle Click", ex);
                }
            };
            this.groupBoxQuickActions.Controls.Add(this.buttonToggle);

            this.groupBoxDeviceStateMemory = new GroupBox();
            this.groupBoxDeviceStateMemory.Text = "Device State Memory";
            this.groupBoxDeviceStateMemory.Location = new Point(10, 70);
            this.groupBoxDeviceStateMemory.Size = new Size(760, 100);
            this.groupBoxDeviceStateMemory.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            this.Controls.Add(this.groupBoxDeviceStateMemory);

            this.groupBoxActivityLogs = new GroupBox();
            this.groupBoxActivityLogs.Text = "Activity Logs";
            this.groupBoxActivityLogs.Location = new Point(10, 180);
            this.groupBoxActivityLogs.Size = new Size(760, 370);
            this.groupBoxActivityLogs.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            this.Controls.Add(this.groupBoxActivityLogs);

            for(int i = 0; i < MAX_MEMORIES; i++) {
                Label label = new Label();
                label.Text = $"[{i}]:";
                label.Location = new Point(10 + ((i % 2) * 270), 17 + ((i / 2) * 20));
                label.Size = new Size(22, 12);
                this.groupBoxDeviceStateMemory.Controls.Add(label);

                TextBox textDevices = textBoxMemoryDevices[i] = new TextBox();
                textDevices.Location = new Point(32 + ((i % 2) * 270), 14 + ((i / 2) * 20));
                textDevices.Size = new Size(200, 16);
                //textDevices.Text = $"[{i}]";
                textDevices.ForeColor = Color.Black;
                textDevices.ReadOnly = true;
                this.groupBoxDeviceStateMemory.Controls.Add(textDevices);

                TextBox textStatus = textBoxMemoryStatus[i] = new TextBox();
                textStatus.Location = new Point(236 + ((i % 2) * 270), 14 + ((i / 2) * 20));
                textStatus.Size = new Size(40, 16);
                //textStatus.Text = $"[{i}]";
                textStatus.ForeColor = Color.Black;
                textStatus.ReadOnly = true;
                this.groupBoxDeviceStateMemory.Controls.Add(textStatus);
            }

            this.textBoxLog = new TextBox();
            this.textBoxLog.Multiline = true;
            this.textBoxLog.ReadOnly = true;
            this.textBoxLog.ForeColor = Color.Black;
            this.textBoxLog.ScrollBars = ScrollBars.Both;
            this.textBoxLog.WordWrap = false;
            this.textBoxLog.Dock = DockStyle.Fill;
            this.groupBoxActivityLogs.Controls.Add(this.textBoxLog);

            this._uiUpdateTimer = new System.Windows.Forms.Timer();
            this._uiUpdateTimer.Interval = 100;
            this._uiUpdateTimer.Tick += delegate(object sender, EventArgs e) {
                UpdateControls();
            };

            this.Shown += delegate(object sender, EventArgs e) {
                UpdateControls();
                this._uiUpdateTimer.Start();
            };

            this.VisibleChanged += delegate(object sender, EventArgs e) {
                if(this.Visible) {
                    UpdateControls();
                    this._uiUpdateTimer.Start();
                } else {
                    this._uiUpdateTimer.Stop();
                }
            };

            this.FormClosing += delegate(object sender, FormClosingEventArgs e) {
                if (e.CloseReason == CloseReason.UserClosing) {
                    e.Cancel = true;
                    this.Hide();
                }
            };

            this.ResumeLayout(false);
        }

        /// <summary>
        /// スレッド セーフな履歴表示更新
        /// </summary>
        public void UpdateHistories(Dictionary<string, bool> numlockStateDict) {
            lock(this._lockObj) {
                this._cacheNumlockStateDict.Clear();
                foreach (KeyValuePair<string, bool> item in numlockStateDict) {
                    this._cacheNumlockStateDict[item.Key] = item.Value;
                }
            }
        }

        public void AppendLog(string message) {
            lock(this._lockObj) {
                const int MAX_LINES = 500;
                string logLine = $"[{DateTime.Now:yyyy/MM/dd HH:mm:ss.fff}] {message}";
                this._cacheLogLineList.Insert(0, logLine);
                if (this._cacheLogLineList.Count > MAX_LINES) {
                    int removeCount = this._cacheLogLineList.Count - MAX_LINES;
                    this._cacheLogLineList.RemoveRange(MAX_LINES, removeCount);
                }
            }
        }

        private void UpdateControls() {
            try {
                lock(this._lockObj) {
                    {
                        int i = 0;
                        foreach(KeyValuePair<string, bool> item in _cacheNumlockStateDict) {
                            if(i < MAX_MEMORIES) {
                                string hardwareId = item.Key;
                                bool isNumlockOn = item.Value;
                                this.textBoxMemoryDevices[i].Text = $"{hardwareId}";
                                this.textBoxMemoryStatus[i].Text = $"{isNumlockOn}";
                                i++;
                            } else {
                                break;
                            }
                        }
                        for(; i < MAX_MEMORIES; i++) {
                            this.textBoxMemoryDevices[i].Text = "";
                            this.textBoxMemoryStatus[i].Text = "";
                        }
                    }
                    //
                    this.textBoxLog.Lines = this._cacheLogLineList.ToArray();
                }
            } catch(Exception ex) {
                Log.Info($"[EX] UpdateControls", ex);
            }
        }
    }

    /// <summary>
    /// Config
    /// </summary>
    [XmlRoot("NumrnrConfig")]
    public class Config {
        private static readonly ConsoleLogger Log = new ConsoleLogger(typeof(Config));
        public bool IsUpdate{get; set;}

        public Config () {
            this.IsUpdate = true;
        }

        public static Config Load(string path) {
            using(FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using(StreamReader reader = new StreamReader(stream, Encoding.UTF8)) {
                XmlSerializer serializer = new XmlSerializer(typeof(Config));
                return (Config)serializer.Deserialize(reader);
            }
        }

        public static Config SafeLoad(string path) {
            try {
                if(File.Exists(path)) {
                    Log.Info($"\"{path}\" is found!");
                    Config config = Load(path);
                    Log.Info($"loaded.");
                    return config;
                } else {
                    Log.Info($"\"{path}\" is not found...");
                    return new Config();
                }
            } catch(Exception ex) {
                Log.Info($"[EX] Settings.SafeLoad", ex);
                return new Config();
            }
        }

        public static void Save(string path, Config config) {
            using(FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            using(StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false))) {
                XmlSerializer serializer = new XmlSerializer(typeof(Config));
                serializer.Serialize(writer, config);
                // for POSIX Text File Rule.
                writer.WriteLine();
            }
        }

        public static void SafeSave(string path, Config config) {
            try {
                Save(path, config);
                Log.Info($"saved.");
            } catch(Exception ex) {
                Log.Info($"[EX] Settings.SafeSave", ex);
            }
        }

    }

    /// <summary>
    /// Main Program.
    /// </summary>
    class Program : IDisposable {
        private static readonly ConsoleLogger Log = new ConsoleLogger(typeof(Program));

        // Controls
        private NotifyIcon notifyIcon = new NotifyIcon();
        private ContextMenuStrip contextMenuStrip = new ContextMenuStrip();
        private ToolStripMenuItem toolStripMenuItemControlPanel = new ToolStripMenuItem();
        private ToolStripMenuItem toolStripMenuItemVersion= new ToolStripMenuItem();
        private ToolStripMenuItem toolStripMenuItemExit = new ToolStripMenuItem();

        // Forms
        private ControlPanelForm controlPanelForm;

        // Process Member(1)
        private RawInputReceiver? _rawInputReceiver = null;
        private string? _lastDeviceHardwareId = null;
        private bool _isChagedDevice = false;

        // Process Member(2)
        private Config _config;
        private Dictionary<string, bool> _numlockStateDict = new Dictionary<string, bool>();

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main() {
            // UIスレッド上の未処理例外をキャッチ
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate (object sender, System.Threading.ThreadExceptionEventArgs e) {
                Log.Info($"[EX] ThreadException", e.Exception);
            };

            // バックグラウンドスレッド等での未処理例外をキャッチ
            AppDomain.CurrentDomain.UnhandledException += delegate (object sender, UnhandledExceptionEventArgs e) {
                Log.Info($"[EX] UnhandledException: {e.ExceptionObject}");
            };

            if(Win32Api.AttachConsole(Win32Api.ATTACH_PARENT_PROCESS)) {
                StreamWriter writer = new StreamWriter(Console.OpenStandardOutput(), Console.OutputEncoding);
                writer.AutoFlush = true;
                Console.SetOut(writer);
                Log.Info("start.");
            }

            try {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                using(new numrnr.Program()) {
                    System.Windows.Forms.Application.Run();
                }
            } catch (Exception ex) {
                Log.Info($"[EX] Fatal Main", ex);
            }
        }

        /// <summary>
        /// constructor
        /// </summary>
        private Program() {
            this.contextMenuStrip.SuspendLayout();
            //design
            {
                // ControlPanelForm
                this.controlPanelForm = new ControlPanelForm();
                this.controlPanelForm.Hide();

                //notifyIcon
                this.notifyIcon.ContextMenuStrip = this.contextMenuStrip;
                this.notifyIcon.Visible = true;
                this.notifyIcon.Text = Constants.APP_NAME;
                this.notifyIcon.Icon = SystemIcons.Application;
                this.notifyIcon.DoubleClick += delegate(object sender, EventArgs e) {
                    Log.Info("notifyIcon.DoubleClick() => show/close control panel.");
                    ToggleControlPanelForm();
                };

                //toolStripMenuItemControlPanel
                this.toolStripMenuItemControlPanel.Text = "Control Panel (&C)";
                //this.toolStripMenuItemControlPanel.Font = new Font(this.toolStripMenuItemControlPanel.Font, FontStyle.Bold);
                this.toolStripMenuItemControlPanel.Click += delegate(object sender, EventArgs e) {
                    Log.Info("toolStripMenuItemControlPanel.Click() => show/close control panel.");
                    ToggleControlPanelForm();
                };
                this.toolStripMenuItemControlPanel.Enabled = true;
                this.toolStripMenuItemControlPanel.Font = new Font(this.toolStripMenuItemControlPanel.Font, FontStyle.Bold);

                //toolStripMenuItemVersion
                this.toolStripMenuItemVersion.Text = "Version (&V)";
                this.toolStripMenuItemVersion.Click += new EventHandler(delegate (object sender, EventArgs e) {
                    Log.Info("toolStripMenuItemVersion.Click() => show version info.");
                    try {
                        AssemblyConfigurationAttribute? configAttr = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyConfigurationAttribute>();
                        string buildConfig = configAttr?.Configuration ?? "Unknown";
                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine($"-- {Constants.APP_NAME} --");
                        sb.AppendLine("Independent NumLock state manager for multiple keyboards.");
                        sb.AppendLine("");
                        sb.AppendLine("version: v.0.0.0.0.0.0.0.0.0.1");
                        sb.AppendLine("auther: libraplanet");
                        sb.AppendLine("license: MIT License");
                        sb.AppendLine($"build: {buildConfig}");

                        MessageBox.Show(
                            sb.ToString(),
                            $"{Constants.APP_NAME} - Version",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information
                        );
                    } catch (Exception ex) {
                        Log.Info($"[EX] Info Click", ex);
                    }
                });

                //toolStripMenuItemExit
                this.toolStripMenuItemExit.Text = "Exit (&E)";
                this.toolStripMenuItemExit.Click += delegate (object sender, EventArgs e) {
                    Log.Info("toolStripMenuItemExit.Click() => exit process.");
                    try {
                        System.Windows.Forms.Application.Exit();
                    } catch (Exception ex) {
                        Log.Info($"[EX] Exit Click", ex);
                    }
                };

                //contextMenuStrip
                this.contextMenuStrip.Items.Add(this.toolStripMenuItemControlPanel);
                this.contextMenuStrip.Items.Add(this.toolStripMenuItemVersion);
                this.contextMenuStrip.Items.Add(this.toolStripMenuItemExit);
            }
            this.contextMenuStrip.ResumeLayout(false);

            this._rawInputReceiver = new RawInputReceiver();
            this._rawInputReceiver.OnDeviceInput += delegate(IntPtr hDevice, ushort vkey, uint message, IntPtr extraInfo) {
                try {
                    if (extraInfo != Win32Api.INSIGNIA_REINJECT) {
                        string hardwareId = DeviceManager.GetHardwareIdFix(hDevice);
                        bool isChagedDevice = this._isChagedDevice = (_lastDeviceHardwareId != null) && (_lastDeviceHardwareId != hardwareId);
                        bool curIsNumLockOn = (Win32Api.GetKeyState(Win32Api.VK_NUMLOCK) & 0x0001) != 0;
                        if(vkey == Win32Api.VK_NUMLOCK) {
                            if (message == Win32Api.WM_KEYDOWN) {
                                // NUM LOCKが押下されたので、先読み
                                this._numlockStateDict[hardwareId] = !curIsNumLockOn;
                                this._lastDeviceHardwareId = hardwareId;
                                this.controlPanelForm.AppendLog($"[WM_INPUT] Dev: {hardwareId}, numLock: {curIsNumLockOn} => {!curIsNumLockOn}, hasContains:{this._numlockStateDict.ContainsKey(hardwareId)}");
                            }
                        } else {
                            this.controlPanelForm.AppendLog($"[WM_INPUT] Dev: {hardwareId}, numLock: {curIsNumLockOn}, isChagedDevice: {isChagedDevice}, hasContains:{this._numlockStateDict.ContainsKey(hardwareId)}");
                            if(isChagedDevice) {
                                this.controlPanelForm.AppendLog($"change keyboard!");
                                if(this._numlockStateDict.ContainsKey(hardwareId)) {
                                    bool expectedIsNumLockOn = this._numlockStateDict[hardwareId];
                                    this.controlPanelForm.AppendLog($"contained in dict => check! (curIsNumLockOn: {curIsNumLockOn}, expectedIsNumLockOn: {expectedIsNumLockOn})");
                                    if(curIsNumLockOn != expectedIsNumLockOn) {
                                        this.controlPanelForm.AppendLog($"restore!");

                                        int errorCode = Common.SafeToggleNumLock();
                                        this.controlPanelForm.AppendLog($"toggle num lock! errorCode({errorCode})");

                                        curIsNumLockOn = expectedIsNumLockOn;
                                    } else {
                                        this.controlPanelForm.AppendLog($"nop (samed)");
                                    }
                                } else {
                                    this.controlPanelForm.AppendLog($"nop (no contained...)");
                                }
                            }
                            this._numlockStateDict[hardwareId] = curIsNumLockOn;
                            this._lastDeviceHardwareId = hardwareId;
                        }
                    }
                    // update
                    this.controlPanelForm.UpdateHistories(this._numlockStateDict);
                } catch (Exception ex) {
                    Log.Info($"[EX] OnDeviceInput", ex);
                }
            };

            this._config = Config.SafeLoad(GetConfigFileFullpath());

            // Propertyの読み込み
            try {
                LoadToDictionary(GetSettingFileFullpath(), this._numlockStateDict);
            } catch (Exception ex) {
                Log.Info($"[EX] LoadToDictionary", ex);
            } finally {
                this.controlPanelForm.UpdateHistories(this._numlockStateDict);
            }
        }

        /// <summary>
        /// 実行ファイルのフルパスの取得.
        /// </summary>
        static string GetApplicationFullpath() {
            return Application.ExecutablePath;
        }

        /// <summary>
        /// Configのフルパスの取得.
        /// (実行ファイルから取得).
        /// </summary>
        static string GetConfigFileFullpath() {
            return Path.ChangeExtension(Application.ExecutablePath, ".config");
        }

        /// <summary>
        /// Propertyのフルパスの取得.
        /// (実行ファイルから取得).
        /// </summary>
        static string GetSettingFileFullpath() {
            return Path.ChangeExtension(Application.ExecutablePath, ".properties");
        }

        /// <summary>
        /// PropertyからDictionaryへ読み込み.
        /// </summary>
        static void LoadToDictionary(string path, Dictionary<string, bool> dict){
            try {
                using(FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using(StreamReader reader = new StreamReader(stream, Encoding.UTF8)) {
                    string? line;
                    while((line = reader.ReadLine()) != null) {
                        string buf = line.Trim();
                        if(string.IsNullOrEmpty(buf) || buf.StartsWith("#") || buf.StartsWith(";")) {
                            continue;
                        } else {
                            try {
                                string[] parts = buf.Split(new char[] { '=' }, 2);
                                if (parts.Length == 2) {
                                    string strKey = parts[0].Trim();
                                    string strValue = parts[1].Trim();
                                    if(!string.IsNullOrEmpty(strKey)) {
                                        if(bool.TryParse(strValue, out bool isNumLockOn)) {
                                            dict[strKey] = isNumLockOn;
                                        }
                                    }
                                }
                            } catch {
                                continue;
                            }
                        }
                    }
                }
            } catch {
            }
        }

        /// <summary>
        /// Dictionaryの内容をPropertyに書き出し.
        /// </summary>
        static void SaveDictionary(string path, Dictionary<string, bool> dict){
            List<string> lines = new List<string>();

            // (1) 現在の全行を読み込み
            if(File.Exists(path)) {
                // 既存
                using(FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using(StreamReader reader = new StreamReader(stream, Encoding.UTF8)) {
                    string? line;
                    while((line = reader.ReadLine()) != null) {
                        lines.Add(line);
                    }
                }
            } else {
                // 新規
                lines.Add("# Num R'n'R Device Settings");
                lines.Add($"# Created: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            }

            // (2) Dictionaryを元に修正
            {
                // $1: インデント
                // $2: 既存の Key
                // $3: '=' とその前後の空白
                // $4: 既存の Value
                // $5: 行末のコメントや空白
                Regex parserRegex = new Regex(@"^(\s*)([^\s=#;]+)(\s*=\s*)([^\s#;]+)(.*)$", RegexOptions.IgnoreCase);
                foreach(KeyValuePair<string, bool> item in dict) {
                    string key = item.Key;
                    bool val = item.Value;
                    bool isMatched = false;
                    string strValue;
                    if(val) {
                        strValue = "true";
                    } else {
                        strValue = "false";
                    }
                    for(int i = 0; i < lines.Count; i++) {
                        string buf = lines[i].Trim();
                        if(string.IsNullOrEmpty(buf) || buf.StartsWith("#") || buf.StartsWith(";")) {
                            continue;
                        } else {
                            Match match = parserRegex.Match(lines[i]);
                            if(match.Success) {
                                string existingKey = match.Groups[2].Value;
                                if(string.Equals(key, existingKey, StringComparison.OrdinalIgnoreCase)) {
                                    lines[i] = match.Result($"$1$2$3{strValue}$5");
                                    isMatched = true;
                                    break;
                                }
                            }
                        }
                    }
                    if(!isMatched) {
                        lines.Add($"{key}={strValue}");
                    }
                }
            }

            // (3) 書き出し
            using(FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            using(StreamWriter writer = new StreamWriter (stream, new UTF8Encoding(false))) {
                foreach(string line in lines) {
                    writer.WriteLine(line);
                }
            }
        }

        /// <summary>
        /// ToggleControlPanelForm
        /// </summary>
        private void ToggleControlPanelForm() {
            if(this.controlPanelForm.Visible) {
                this.controlPanelForm.Hide();
            } else {
                this.controlPanelForm.Show();
                this.controlPanelForm.Activate();
            }
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose() {

            // Configの書き出し
            Config.SafeSave(GetConfigFileFullpath(), this._config);

            // Propertyの書き出し
            try {
                SaveDictionary(GetSettingFileFullpath(), this._numlockStateDict);
            } catch (Exception ex) {
                Log.Info($"[EX] Dispose SaveDictionary", ex);
            }

            try {
                this._rawInputReceiver?.Dispose();

                this.notifyIcon.Visible = false;
                this.notifyIcon.Dispose();
                this.contextMenuStrip.Dispose();
            } catch (Exception ex) {
                Log.Info($"[EX] Dispose CleanUp", ex);
            }
        }
    }
}
