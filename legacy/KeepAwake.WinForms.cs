using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace KeepAwake
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            try { Native.MakeDpiAware(); }
            catch { }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    internal static class Native
    {
        public const uint ES_SYSTEM_REQUIRED = 0x00000001;
        public const uint ES_DISPLAY_REQUIRED = 0x00000002;
        public const uint ES_CONTINUOUS = 0x80000000;

        public const ushort VK_F15 = 0x7E;

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint SetThreadExecutionState(uint esFlags);

        [DllImport("kernel32.dll")]
        private static extern uint GetTickCount();

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO info);

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct INPUTUNION
        {
            [FieldOffset(0)] public MOUSEINPUT Mouse;
            [FieldOffset(0)] public KEYBDINPUT Keyboard;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint Type;
            public INPUTUNION Data;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint count, INPUT[] inputs, int size);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr PowerCreateRequest(ref REASON_CONTEXT context);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool PowerSetRequest(IntPtr handle, int type);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool PowerClearRequest(IntPtr handle, int type);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr handle);

        [StructLayout(LayoutKind.Sequential)]
        public struct REASON_CONTEXT
        {
            public uint Version;
            public uint Flags;
            public IntPtr SimpleReasonString;
        }

        public static void MakeDpiAware()
        {
            SetProcessDPIAware();
        }

        public static uint IdleMilliseconds()
        {
            var info = new LASTINPUTINFO();
            info.cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO));
            if (!GetLastInputInfo(ref info)) return 0;
            return unchecked(GetTickCount() - info.dwTime);
        }

        public static bool PressKey(ushort vk)
        {
            var inputs = new INPUT[2];
            inputs[0].Type = 1;
            inputs[0].Data.Keyboard = new KEYBDINPUT { wVk = vk, dwFlags = 0 };
            inputs[1].Type = 1;
            inputs[1].Data.Keyboard = new KEYBDINPUT { wVk = vk, dwFlags = 2 };
            return SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT))) == 2;
        }

        public static bool NudgeMouse()
        {
            var inputs = new INPUT[2];
            inputs[0].Type = 0;
            inputs[0].Data.Mouse = new MOUSEINPUT { dx = 1, dy = 0, dwFlags = 0x0001 };
            inputs[1].Type = 0;
            inputs[1].Data.Mouse = new MOUSEINPUT { dx = -1, dy = 0, dwFlags = 0x0001 };
            return SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT))) == 2;
        }
    }

    internal sealed class PowerRequest : IDisposable
    {
        private const int DisplayRequired = 0;
        private const int SystemRequired = 1;
        private const uint SimpleReasonString = 0x1;

        private IntPtr _handle = IntPtr.Zero;
        private IntPtr _reason = IntPtr.Zero;
        private bool _system;
        private bool _display;

        public bool Acquire()
        {
            try
            {
                _reason = Marshal.StringToHGlobalUni("KeepAwake: sesi kerja aktif");
                var context = new Native.REASON_CONTEXT();
                context.Version = 0;
                context.Flags = SimpleReasonString;
                context.SimpleReasonString = _reason;

                _handle = Native.PowerCreateRequest(ref context);
                if (_handle == IntPtr.Zero) { FreeReason(); return false; }

                _system = Native.PowerSetRequest(_handle, SystemRequired);
                _display = Native.PowerSetRequest(_handle, DisplayRequired);
                return _system || _display;
            }
            catch
            {
                return false;
            }
        }

        public void Release()
        {
            try
            {
                if (_handle != IntPtr.Zero)
                {
                    if (_display) Native.PowerClearRequest(_handle, DisplayRequired);
                    if (_system) Native.PowerClearRequest(_handle, SystemRequired);
                    Native.CloseHandle(_handle);
                }
            }
            catch { }

            _handle = IntPtr.Zero;
            _system = false;
            _display = false;
            FreeReason();
        }

        private void FreeReason()
        {
            if (_reason == IntPtr.Zero) return;
            try { Marshal.FreeHGlobal(_reason); }
            catch { }
            _reason = IntPtr.Zero;
        }

        public void Dispose()
        {
            Release();
        }
    }

    internal sealed class AwakeGuard : IDisposable
    {
        private const uint Flags = Native.ES_CONTINUOUS | Native.ES_SYSTEM_REQUIRED | Native.ES_DISPLAY_REQUIRED;

        private readonly PowerRequest _power = new PowerRequest();
        private Thread _worker;
        private ManualResetEvent _stopSignal;
        private volatile int _intervalSeconds = 60;
        private volatile bool _simulateInput = true;
        private volatile bool _nudgeMouse;
        private int _simulations;

        public event Action<string> Log;

        public bool IsRunning { get { return _worker != null; } }
        public DateTime StartedAt { get; private set; }
        public int Simulations { get { return Volatile.Read(ref _simulations); } }

        public int IntervalSeconds
        {
            get { return _intervalSeconds; }
            set { if (value > 0) _intervalSeconds = value; }
        }

        public bool SimulateInput
        {
            get { return _simulateInput; }
            set { _simulateInput = value; }
        }

        public bool NudgeMouse
        {
            get { return _nudgeMouse; }
            set { _nudgeMouse = value; }
        }

        public void Start()
        {
            if (_worker != null) return;

            var stop = new ManualResetEvent(false);
            _stopSignal = stop;
            _simulations = 0;
            StartedAt = DateTime.Now;

            _worker = new Thread(delegate () { Run(stop); });
            _worker.IsBackground = true;
            _worker.Name = "KeepAwakeGuard";
            _worker.Start();
        }

        public void Stop()
        {
            var worker = _worker;
            var stop = _stopSignal;
            if (worker == null || stop == null) return;

            _worker = null;
            _stopSignal = null;
            stop.Set();

            if (worker.Join(3000))
            {
                stop.Dispose();
                Report("Semua request dilepas - power policy kembali normal.");
            }
        }

        private void Run(ManualResetEvent stop)
        {
            uint previous = Native.SetThreadExecutionState(Flags);
            if (previous == 0)
                Report("PERINGATAN: execution state ditolak sistem (error " + Marshal.GetLastWin32Error() + ").");
            else
                Report("Execution state aktif: display + system ditahan (ES_CONTINUOUS).");

            if (_power.Acquire())
                Report("Power request aktif untuk Modern Standby (system + display).");
            else
                Report("Power request Modern Standby tidak tersedia - memakai execution state saja.");

            int elapsed = 0;
            try
            {
                while (!stop.WaitOne(1000))
                {
                    elapsed++;
                    if (elapsed % 30 == 0) Native.SetThreadExecutionState(Flags);
                    if (!_simulateInput) continue;

                    uint idle = Native.IdleMilliseconds();
                    if (idle < (uint)_intervalSeconds * 1000) continue;

                    bool key = Native.PressKey(Native.VK_F15);
                    bool mouse = _nudgeMouse && Native.NudgeMouse();
                    Interlocked.Increment(ref _simulations);

                    Report(string.Format(CultureInfo.InvariantCulture,
                        "Simulasi input #{0} (idle {1} detik): F15{2}{3}.",
                        Simulations, idle / 1000, key ? "" : " GAGAL", mouse ? " + mouse 1px" : ""));
                }
            }
            catch (Exception ex)
            {
                Report("ERROR guard: " + ex.Message);
            }
            finally
            {
                Native.SetThreadExecutionState(Native.ES_CONTINUOUS);
                _power.Release();
            }
        }

        private void Report(string message)
        {
            var handler = Log;
            if (handler == null) return;
            try { handler(message); }
            catch { }
        }

        public void Dispose()
        {
            Stop();
            _power.Dispose();
        }
    }

    internal sealed class MainForm : Form
    {
        private static readonly Color Accent = Color.FromArgb(22, 163, 74);
        private static readonly Color Neutral = Color.FromArgb(51, 65, 85);
        private static readonly Color Danger = Color.FromArgb(220, 38, 38);
        private static readonly Color DisabledBack = Color.FromArgb(226, 232, 240);
        private static readonly Color DisabledFore = Color.FromArgb(148, 163, 184);
        private static readonly Color Muted = Color.FromArgb(100, 116, 139);

        private readonly AwakeGuard _guard = new AwakeGuard();
        private readonly System.Windows.Forms.Timer _tick = new System.Windows.Forms.Timer { Interval = 1000 };
        private float _scale = 1f;

        private Label _title;
        private Label _subtitle;
        private Panel _card;
        private Label _dot;
        private Label _state;
        private Label _meta;
        private Label _intervalLabel;
        private NumericUpDown _interval;
        private Label _intervalSuffix;
        private CheckBox _simulate;
        private CheckBox _nudge;
        private Button _start;
        private Button _restart;
        private Button _stop;
        private Label _logLabel;
        private Label _logHint;
        private TextBox _log;
        private Label _footer;

        public MainForm()
        {
            Text = "KeepAwake - Penahan Sleep & Lock Screen";
            Font = new Font("Segoe UI", 9f);
            BackColor = Color.White;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None;

            using (var screen = Graphics.FromHwnd(IntPtr.Zero))
                _scale = screen.DpiX / 96f;

            ClientSize = new Size(S(620), S(566));

            try { Icon = BuildIcon(); }
            catch { }

            BuildUi();

            _guard.Log += AppendLog;
            _tick.Tick += delegate { RefreshStatus(); };
            _tick.Start();
            FormClosing += OnClosing;

            RefreshStatus();
            AppendLog("Aplikasi dimuat. Tanpa admin, tanpa registry, tanpa jaringan.");
            AppendLog("Tekan START untuk menahan sleep, screen-off, dan idle lock.");
        }

        private int S(int value)
        {
            return (int)Math.Round(value * _scale);
        }

        private void BuildUi()
        {
            _title = new Label();
            _title.Text = "KeepAwake";
            _title.Font = new Font("Segoe UI", 15f, FontStyle.Bold);
            _title.ForeColor = Color.FromArgb(15, 23, 42);
            _title.UseMnemonic = false;
            _title.SetBounds(S(18), S(12), S(584), S(32));

            _subtitle = new Label();
            _subtitle.Text = "Menahan sleep, layar mati, dan idle lock tanpa beban CPU/GPU/RAM.";
            _subtitle.ForeColor = Muted;
            _subtitle.UseMnemonic = false;
            _subtitle.SetBounds(S(18), S(46), S(584), S(18));

            _card = new Panel();
            _card.BackColor = Color.FromArgb(241, 245, 249);
            _card.SetBounds(S(18), S(74), S(584), S(78));

            _dot = new Label();
            _dot.Text = "\u25CF";
            _dot.Font = new Font("Segoe UI", 14f);
            _dot.BackColor = Color.Transparent;
            _dot.UseMnemonic = false;
            _dot.SetBounds(S(14), S(22), S(24), S(26));

            _state = new Label();
            _state.Text = "NONAKTIF";
            _state.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            _state.BackColor = Color.Transparent;
            _state.UseMnemonic = false;
            _state.SetBounds(S(46), S(12), S(520), S(28));

            _meta = new Label();
            _meta.Text = "";
            _meta.Font = new Font("Segoe UI", 8.5f);
            _meta.ForeColor = Muted;
            _meta.BackColor = Color.Transparent;
            _meta.UseMnemonic = false;
            _meta.SetBounds(S(46), S(44), S(524), S(20));

            _card.Controls.Add(_dot);
            _card.Controls.Add(_state);
            _card.Controls.Add(_meta);

            _intervalLabel = new Label();
            _intervalLabel.Text = "Interval simulasi";
            _intervalLabel.UseMnemonic = false;
            _intervalLabel.SetBounds(S(18), S(170), S(150), S(24));
            _intervalLabel.TextAlign = ContentAlignment.MiddleLeft;

            _interval = new NumericUpDown();
            _interval.Minimum = 10;
            _interval.Maximum = 600;
            _interval.Increment = 5;
            _interval.Value = 60;
            _interval.TextAlign = HorizontalAlignment.Left;
            _interval.SetBounds(S(172), S(170), S(84), S(24));
            _interval.ValueChanged += delegate { OnIntervalChanged(); };

            _intervalSuffix = new Label();
            _intervalSuffix.Text = "detik  (10 - 600)";
            _intervalSuffix.ForeColor = Muted;
            _intervalSuffix.UseMnemonic = false;
            _intervalSuffix.SetBounds(S(262), S(170), S(140), S(24));
            _intervalSuffix.TextAlign = ContentAlignment.MiddleLeft;

            _simulate = new CheckBox();
            _simulate.Text = "Simulasi input saat idle (F15) - untuk policy inactivity lock";
            _simulate.Checked = true;
            _simulate.SetBounds(S(18), S(202), S(584), S(22));
            _simulate.CheckedChanged += delegate { OnSimulateChanged(); };

            _nudge = new CheckBox();
            _nudge.Text = "Tambahkan gerak mouse 1px (cadangan bila F15 diabaikan)";
            _nudge.SetBounds(S(40), S(226), S(562), S(22));
            _nudge.CheckedChanged += delegate { _guard.NudgeMouse = _nudge.Checked; };

            _start = new Button();
            _start.Text = "START";
            StyleButton(_start, Accent);
            _start.SetBounds(S(18), S(260), S(186), S(44));
            _start.Click += delegate { StartGuard(); };

            _restart = new Button();
            _restart.Text = "RESTART";
            StyleButton(_restart, Neutral);
            _restart.SetBounds(S(216), S(260), S(186), S(44));
            _restart.Click += delegate { RestartGuard(); };

            _stop = new Button();
            _stop.Text = "STOP";
            StyleButton(_stop, Danger);
            _stop.SetBounds(S(414), S(260), S(188), S(44));
            _stop.Click += delegate { StopGuard(); };

            _logLabel = new Label();
            _logLabel.Text = "Log Aktivitas";
            _logLabel.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _logLabel.UseMnemonic = false;
            _logLabel.SetBounds(S(18), S(320), S(200), S(20));

            _logHint = new Label();
            _logHint.Text = "riwayat otomatis dibatasi";
            _logHint.Font = new Font("Segoe UI", 8f);
            _logHint.ForeColor = Muted;
            _logHint.TextAlign = ContentAlignment.MiddleRight;
            _logHint.UseMnemonic = false;
            _logHint.SetBounds(S(402), S(322), S(200), S(18));

            _log = new TextBox();
            _log.Multiline = true;
            _log.ReadOnly = true;
            _log.ScrollBars = ScrollBars.Vertical;
            _log.WordWrap = true;
            _log.BorderStyle = BorderStyle.FixedSingle;
            _log.BackColor = Color.FromArgb(17, 24, 39);
            _log.ForeColor = Color.FromArgb(226, 232, 240);
            _log.Font = new Font("Consolas", 9f);
            _log.SetBounds(S(18), S(344), S(584), S(172));

            _footer = new Label();
            _footer.Text = "Tanpa admin  |  tanpa service  |  tanpa registry  |  request dilepas otomatis saat aplikasi ditutup";
            _footer.Font = new Font("Segoe UI", 8f);
            _footer.ForeColor = Muted;
            _footer.UseMnemonic = false;
            _footer.SetBounds(S(18), S(528), S(584), S(18));

            Controls.Add(_card);
            Controls.Add(_title);
            Controls.Add(_subtitle);
            Controls.Add(_intervalLabel);
            Controls.Add(_interval);
            Controls.Add(_intervalSuffix);
            Controls.Add(_simulate);
            Controls.Add(_nudge);
            Controls.Add(_start);
            Controls.Add(_restart);
            Controls.Add(_stop);
            Controls.Add(_logLabel);
            Controls.Add(_logHint);
            Controls.Add(_log);
            Controls.Add(_footer);
        }

        private static void StyleButton(Button button, Color back)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = back;
            button.ForeColor = Color.White;
            button.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            button.UseVisualStyleBackColor = false;
        }

        private void StartGuard()
        {
            if (_guard.IsRunning) return;

            _guard.IntervalSeconds = (int)_interval.Value;
            _guard.SimulateInput = _simulate.Checked;
            _guard.NudgeMouse = _simulate.Checked && _nudge.Checked;
            _nudge.Enabled = _simulate.Checked;

            AppendLog(string.Format(CultureInfo.InvariantCulture,
                "START - interval {0} detik, simulasi input {1}, mouse jiggle {2}.",
                _guard.IntervalSeconds, _guard.SimulateInput ? "ON" : "OFF", _guard.NudgeMouse ? "ON" : "OFF"));

            if (!_guard.SimulateInput)
                AppendLog("Catatan: simulasi input OFF. Bila policy memakai InactivityTimeoutSecs, layar masih bisa terkunci.");

            _guard.Start();
            RefreshStatus();
        }

        private void StopGuard()
        {
            if (!_guard.IsRunning) return;
            AppendLog("STOP - melepas semua request.");
            _guard.Stop();
            RefreshStatus();
        }

        private void RestartGuard()
        {
            if (!_guard.IsRunning)
            {
                StartGuard();
                return;
            }

            AppendLog("RESTART - guard dihentikan lalu dimulai ulang.");
            _guard.Stop();
            StartGuard();
            RefreshStatus();
        }

        private void OnIntervalChanged()
        {
            _guard.IntervalSeconds = (int)_interval.Value;
            if (_guard.IsRunning)
                AppendLog("Interval simulasi diubah ke " + _interval.Value.ToString(CultureInfo.InvariantCulture) + " detik.");
        }

        private void OnSimulateChanged()
        {
            _guard.SimulateInput = _simulate.Checked;
            _guard.NudgeMouse = _simulate.Checked && _nudge.Checked;
            _nudge.Enabled = _simulate.Checked;
            if (_guard.IsRunning)
                AppendLog("Simulasi input " + (_simulate.Checked ? "diaktifkan." : "dimatikan."));
        }

        private void RefreshStatus()
        {
            bool running = _guard.IsRunning;

            _dot.ForeColor = running ? Accent : Color.FromArgb(148, 163, 184);
            _state.Text = running ? "AKTIF - display & system ditahan" : "NONAKTIF";
            _state.ForeColor = running ? Color.FromArgb(21, 128, 61) : Color.FromArgb(71, 85, 105);

            if (running)
            {
                _meta.Text = string.Format(CultureInfo.InvariantCulture,
                    "Uptime {0}   |   simulasi input {1}x   |   interval {2} detik",
                    (DateTime.Now - _guard.StartedAt).ToString(@"hh\:mm\:ss"), _guard.Simulations, _guard.IntervalSeconds);
            }
            else
            {
                _meta.Text = "Siap memulai - tekan START.";
            }

            SetButtons(running);
        }

        private void SetButtons(bool running)
        {
            _start.Enabled = !running;
            _restart.Enabled = running;
            _stop.Enabled = running;

            _start.BackColor = running ? DisabledBack : Accent;
            _start.ForeColor = running ? DisabledFore : Color.White;
            _restart.BackColor = running ? Neutral : DisabledBack;
            _restart.ForeColor = running ? Color.White : DisabledFore;
            _stop.BackColor = running ? Danger : DisabledBack;
            _stop.ForeColor = running ? Color.White : DisabledFore;
        }

        private void AppendLog(string message)
        {
            try
            {
                if (IsDisposed || _log.IsDisposed) return;

                if (InvokeRequired)
                {
                    BeginInvoke(new Action<string>(AppendLog), message);
                    return;
                }

                if (_log.TextLength > 80000)
                {
                    string text = _log.Text;
                    int cut = text.Length - 50000;
                    int newline = text.IndexOf('\n', cut);
                    _log.Text = newline >= 0 ? text.Substring(newline + 1) : text.Substring(cut);
                }

                _log.AppendText(DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "  " + message + Environment.NewLine);
                _log.SelectionStart = _log.TextLength;
                _log.ScrollToCaret();
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }

        private void OnClosing(object sender, FormClosingEventArgs e)
        {
            _tick.Stop();
            _tick.Dispose();

            if (_guard.IsRunning)
            {
                AppendLog("Aplikasi ditutup - menghentikan guard.");
                _guard.Stop();
            }

            _guard.Dispose();
        }

        private static Icon BuildIcon()
        {
            using (var bitmap = new Bitmap(32, 32))
            {
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);

                    using (var brush = new SolidBrush(Accent))
                        g.FillEllipse(brush, 1f, 1f, 30f, 30f);

                    using (var pen = new Pen(Color.White, 3.5f))
                    {
                        pen.StartCap = LineCap.Round;
                        pen.EndCap = LineCap.Round;
                        g.DrawArc(pen, 8f, 8f, 16f, 16f, 300f, 300f);
                        g.DrawLine(pen, 16f, 5f, 16f, 15f);
                    }
                }

                return Icon.FromHandle(bitmap.GetHicon());
            }
        }
    }
}
