using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ThemeToggleWidget
{
    internal static class Program
    {
        internal const string MutexName = @"Local\ThemeToggleWidget.SingleInstance";
        internal const string ToggleEventName = @"Local\ThemeToggleWidget.Toggle";

        [STAThread]
        private static void Main()
        {
            bool createdNew;
            using (Mutex mutex = new Mutex(true, MutexName, out createdNew))
            {
                if (!createdNew)
                {
                    SignalRunningInstance();
                    return;
                }

                Native.EnableDpiAwareness();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayAppContext());
            }
        }

        private static void SignalRunningInstance()
        {
            try
            {
                using (EventWaitHandle signal = EventWaitHandle.OpenExisting(ToggleEventName))
                {
                    signal.Set();
                }
            }
            catch
            {
            }
        }
    }

    internal static class ThemeManager
    {
        private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

        public static bool IsDark()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(PersonalizeKey))
                {
                    if (key == null)
                    {
                        return false;
                    }

                    object value = key.GetValue("AppsUseLightTheme");
                    return value is int && (int)value == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        public static void SetDark(bool dark)
        {
            int value = dark ? 0 : 1;

            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(PersonalizeKey))
            {
                if (key == null)
                {
                    throw new InvalidOperationException("无法打开 Windows 个性化注册表项。");
                }

                key.SetValue("AppsUseLightTheme", value, RegistryValueKind.DWord);
                key.SetValue("SystemUsesLightTheme", value, RegistryValueKind.DWord);
            }

            Native.BroadcastColorSchemeChange();
        }
    }

    internal sealed class HotkeyOption
    {
        public readonly string Name;
        public readonly uint Modifiers;
        public readonly uint VirtualKey;

        public HotkeyOption(string name, uint modifiers, uint virtualKey)
        {
            Name = name;
            Modifiers = modifiers;
            VirtualKey = virtualKey;
        }

        public bool IsOff
        {
            get { return VirtualKey == 0; }
        }

        public static readonly HotkeyOption[] Presets = new HotkeyOption[]
        {
            new HotkeyOption("Ctrl+Alt+T", Native.MOD_CONTROL | Native.MOD_ALT, 0x54),
            new HotkeyOption("Ctrl+Alt+D", Native.MOD_CONTROL | Native.MOD_ALT, 0x44),
            new HotkeyOption("Alt+Shift+T", Native.MOD_ALT | Native.MOD_SHIFT, 0x54)
        };

        public static readonly HotkeyOption Off = new HotkeyOption("关闭快捷键", 0, 0);

        public static HotkeyOption Find(string name)
        {
            foreach (HotkeyOption option in Presets)
            {
                if (string.Equals(option.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return option;
                }
            }

            if (name != null && string.Equals(name, Off.Name, StringComparison.Ordinal))
            {
                return Off;
            }

            return null;
        }
    }

    internal static class AppSettings
    {
        private const string SettingsKey = @"Software\ThemeToggleWidget";
        private const string DefaultHotkey = "Ctrl+Alt+T";

        public static string LoadHotkeyName()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(SettingsKey))
                {
                    if (key != null)
                    {
                        object value = key.GetValue("Hotkey");
                        if (value is string)
                        {
                            return (string)value;
                        }
                    }
                }
            }
            catch
            {
            }

            return DefaultHotkey;
        }

        public static void SaveHotkeyName(string name)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(SettingsKey))
                {
                    if (key != null)
                    {
                        key.SetValue("Hotkey", name, RegistryValueKind.String);
                    }
                }
            }
            catch
            {
            }
        }
    }

    internal static class StartupManager
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "ThemeToggleWidget";

        public static bool IsEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey))
                {
                    if (key == null)
                    {
                        return false;
                    }

                    object value = key.GetValue(ValueName);
                    return value is string && !string.IsNullOrEmpty((string)value);
                }
            }
            catch
            {
                return false;
            }
        }

        public static void SetEnabled(bool enabled)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, true))
            {
                if (key == null)
                {
                    throw new InvalidOperationException("无法打开 Windows 启动项注册表。");
                }

                if (enabled)
                {
                    key.SetValue(ValueName, "\"" + Application.ExecutablePath + "\"", RegistryValueKind.String);
                }
                else
                {
                    key.DeleteValue(ValueName, false);
                }
            }
        }
    }

    internal static class Native
    {
        public const uint WM_HOTKEY = 0x0312;
        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_NOREPEAT = 0x4000;

        private const uint WM_SETTINGCHANGE = 0x001A;
        private const uint SMTO_ABORTIFHUNG = 0x0002;
        private static readonly IntPtr HWND_BROADCAST = new IntPtr(0xFFFF);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessageTimeout(
            IntPtr hWnd,
            uint msg,
            IntPtr wParam,
            string lParam,
            uint flags,
            uint timeout,
            out IntPtr result);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr context);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        public static void EnableDpiAwareness()
        {
            try
            {
                // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = -4
                if (SetProcessDpiAwarenessContext(new IntPtr(-4)))
                {
                    return;
                }
            }
            catch
            {
            }

            try
            {
                SetProcessDPIAware();
            }
            catch
            {
            }
        }

        public static void BroadcastColorSchemeChange()
        {
            IntPtr result;
            SendMessageTimeout(
                HWND_BROADCAST,
                WM_SETTINGCHANGE,
                IntPtr.Zero,
                "ImmersiveColorSet",
                SMTO_ABORTIFHUNG,
                1000,
                out result);
        }
    }

    internal sealed class MessageWindow : NativeWindow, IDisposable
    {
        public const int WmAppToggle = 0x8001;
        public const int WmAppSync = 0x8002;

        public event EventHandler HotkeyPressed;
        public event EventHandler ToggleRequested;
        public event EventHandler SyncRequested;

        public MessageWindow()
        {
            // Parent = HWND_MESSAGE: message-only window, never shows on screen.
            CreateHandle(new CreateParams
            {
                Caption = "ThemeToggleTray.Messages",
                Parent = new IntPtr(-3)
            });
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == (int)Native.WM_HOTKEY)
            {
                EventHandler handler = HotkeyPressed;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }

                return;
            }

            if (m.Msg == WmAppToggle)
            {
                EventHandler handler = ToggleRequested;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }

                return;
            }

            if (m.Msg == WmAppSync)
            {
                EventHandler handler = SyncRequested;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }

                return;
            }

            base.WndProc(ref m);
        }

        public void Dispose()
        {
            if (Handle != IntPtr.Zero)
            {
                DestroyHandle();
            }
        }
    }

    internal sealed class TrayAppContext : ApplicationContext
    {
        private const int HotkeyId = 0x5457;
        private const int ClickGuardMilliseconds = 400;

        private readonly NotifyIcon _tray;
        private readonly ContextMenuStrip _menu;
        private readonly ToolStripMenuItem _toggleMenuItem;
        private readonly ToolStripMenuItem _startupMenuItem;
        private readonly List<ToolStripMenuItem> _hotkeyItems = new List<ToolStripMenuItem>();
        private readonly MessageWindow _window;
        private readonly EventWaitHandle _toggleSignal;
        private readonly Thread _signalThread;
        private readonly Icon _lightIcon;
        private readonly Icon _darkIcon;

        private HotkeyOption _hotkey;
        private bool _hotkeyRegistered;
        private bool _dark;
        private int _lastClickTick;
        private volatile bool _stopping;

        public TrayAppContext()
        {
            _dark = ThemeManager.IsDark();
            _hotkey = HotkeyOption.Find(AppSettings.LoadHotkeyName()) ?? HotkeyOption.Presets[0];

            _lightIcon = TrayIconFactory.Create(false);
            _darkIcon = TrayIconFactory.Create(true);

            _menu = new ContextMenuStrip();
            _toggleMenuItem = new ToolStripMenuItem("切换主题", null, delegate { ToggleTheme(); });
            _menu.Items.Add(_toggleMenuItem);

            ToolStripMenuItem hotkeyMenu = new ToolStripMenuItem("快捷键");
            foreach (HotkeyOption option in HotkeyOption.Presets)
            {
                HotkeyOption captured = option;
                ToolStripMenuItem item = new ToolStripMenuItem(captured.Name, null, delegate { SelectHotkey(captured); });
                _hotkeyItems.Add(item);
                hotkeyMenu.DropDownItems.Add(item);
            }

            hotkeyMenu.DropDownItems.Add(new ToolStripSeparator());
            ToolStripMenuItem offItem = new ToolStripMenuItem(HotkeyOption.Off.Name, null, delegate { SelectHotkey(HotkeyOption.Off); });
            _hotkeyItems.Add(offItem);
            hotkeyMenu.DropDownItems.Add(offItem);
            _menu.Items.Add(hotkeyMenu);

            _startupMenuItem = new ToolStripMenuItem("开机自动启动", null, delegate { ToggleStartup(); });
            _startupMenuItem.Checked = StartupManager.IsEnabled();
            _menu.Items.Add(_startupMenuItem);

            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(new ToolStripMenuItem("退出", null, delegate { ExitApp(); }));
            _menu.Opening += delegate { UpdateUi(); };

            _tray = new NotifyIcon();
            _tray.Icon = _darkIcon;
            _tray.Text = "深浅色主题切换";
            _tray.ContextMenuStrip = _menu;
            _tray.MouseClick += OnTrayMouseClick;
            _tray.Visible = true;

            _window = new MessageWindow();
            _window.HotkeyPressed += delegate { ToggleTheme(); };
            _window.ToggleRequested += delegate { ToggleTheme(); };
            _window.SyncRequested += delegate { SyncFromSystem(); };

            _toggleSignal = new EventWaitHandle(false, EventResetMode.AutoReset, Program.ToggleEventName);
            _signalThread = new Thread(SignalLoop);
            _signalThread.IsBackground = true;
            _signalThread.Start();

            ApplyHotkey();
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            UpdateUi();
        }

        private void OnTrayMouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            // Double-clicking the tray icon (or a fast second click) must not toggle twice.
            int now = Environment.TickCount;
            int elapsed = unchecked(now - _lastClickTick);
            if (elapsed >= 0 && elapsed < ClickGuardMilliseconds)
            {
                return;
            }

            _lastClickTick = now;
            ToggleTheme();
        }

        private void ToggleTheme()
        {
            if (_stopping)
            {
                return;
            }

            bool target = !ThemeManager.IsDark();

            try
            {
                ThemeManager.SetDark(target);
            }
            catch (Exception ex)
            {
                ShowBalloon("切换失败", ex.Message, ToolTipIcon.Error);
                return;
            }

            _dark = target;
            UpdateUi();
        }

        private void SyncFromSystem()
        {
            if (_stopping)
            {
                return;
            }

            _dark = ThemeManager.IsDark();
            UpdateUi();
        }

        private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (_stopping || _window.Handle == IntPtr.Zero)
            {
                return;
            }

            Native.PostMessage(_window.Handle, MessageWindow.WmAppSync, IntPtr.Zero, IntPtr.Zero);
        }

        private void ApplyHotkey()
        {
            if (_hotkeyRegistered)
            {
                Native.UnregisterHotKey(_window.Handle, HotkeyId);
                _hotkeyRegistered = false;
            }

            if (_hotkey == null || _hotkey.IsOff)
            {
                UpdateUi();
                return;
            }

            _hotkeyRegistered = Native.RegisterHotKey(
                _window.Handle,
                HotkeyId,
                _hotkey.Modifiers | Native.MOD_NOREPEAT,
                _hotkey.VirtualKey);

            if (!_hotkeyRegistered)
            {
                ShowBalloon(
                    "快捷键注册失败",
                    "组合 " + _hotkey.Name + " 可能已被其它程序占用，请在右键菜单中更换。",
                    ToolTipIcon.Warning);
            }

            UpdateUi();
        }

        private void SelectHotkey(HotkeyOption option)
        {
            _hotkey = option;
            AppSettings.SaveHotkeyName(option.Name);
            ApplyHotkey();
        }

        private void ToggleStartup()
        {
            try
            {
                StartupManager.SetEnabled(!_startupMenuItem.Checked);
                _startupMenuItem.Checked = StartupManager.IsEnabled();
            }
            catch (Exception ex)
            {
                ShowBalloon("设置失败", ex.Message, ToolTipIcon.Error);
            }
        }

        private void UpdateUi()
        {
            _dark = ThemeManager.IsDark();
            _tray.Icon = _dark ? _darkIcon : _lightIcon;
            _tray.Text = _dark ? "当前：深色模式 · 左键切换" : "当前：浅色模式 · 左键切换";
            _toggleMenuItem.Text = _dark ? "切换为浅色主题" : "切换为深色主题";

            string active = _hotkeyRegistered ? _hotkey.Name : null;
            foreach (ToolStripMenuItem item in _hotkeyItems)
            {
                item.Checked = string.Equals(item.Text, active, StringComparison.Ordinal);
            }
        }

        private void ShowBalloon(string title, string text, ToolTipIcon icon)
        {
            _tray.ShowBalloonTip(3000, title, text, icon);
        }

        private void SignalLoop()
        {
            while (!_stopping)
            {
                try
                {
                    _toggleSignal.WaitOne();
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                if (_stopping)
                {
                    return;
                }

                if (_window.Handle != IntPtr.Zero)
                {
                    Native.PostMessage(_window.Handle, MessageWindow.WmAppToggle, IntPtr.Zero, IntPtr.Zero);
                }
            }
        }

        private void ExitApp()
        {
            if (_stopping)
            {
                return;
            }

            _stopping = true;

            if (_hotkeyRegistered && _window.Handle != IntPtr.Zero)
            {
                Native.UnregisterHotKey(_window.Handle, HotkeyId);
                _hotkeyRegistered = false;
            }

            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;

            try
            {
                _toggleSignal.Set();
            }
            catch
            {
            }

            _tray.Visible = false;
            _tray.Dispose();
            _menu.Dispose();
            _window.Dispose();

            _lightIcon.Dispose();
            _darkIcon.Dispose();

            ExitThread();
        }
    }

    internal static class TrayIconFactory
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr handle);

        public static Icon Create(bool dark)
        {
            using (Bitmap bitmap = new Bitmap(32, 32))
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);

                    Color accent = Color.FromArgb(255, 0, 120, 212);
                    RectangleF rect = new RectangleF(1f, 1f, 30f, 30f);

                    using (GraphicsPath path = CreateRounded(rect, 8f))
                    using (SolidBrush brush = new SolidBrush(accent))
                    {
                        g.FillPath(brush, path);
                    }

                    if (dark)
                    {
                        DrawMoon(g, accent);
                    }
                    else
                    {
                        DrawSun(g);
                    }
                }

                IntPtr handle = bitmap.GetHicon();
                try
                {
                    using (Icon icon = Icon.FromHandle(handle))
                    {
                        return (Icon)icon.Clone();
                    }
                }
                finally
                {
                    DestroyIcon(handle);
                }
            }
        }

        private static void DrawMoon(Graphics g, Color accent)
        {
            using (SolidBrush white = new SolidBrush(Color.White))
            {
                g.FillEllipse(white, 7.5f, 6.5f, 15f, 15f);
            }

            using (SolidBrush cut = new SolidBrush(accent))
            {
                g.FillEllipse(cut, 12.2f, 5.4f, 15f, 15f);
            }
        }

        private static void DrawSun(Graphics g)
        {
            using (SolidBrush white = new SolidBrush(Color.White))
            {
                g.FillEllipse(white, 10f, 10f, 12f, 12f);
            }

            using (Pen ray = new Pen(Color.White, 2f))
            {
                ray.StartCap = LineCap.Round;
                ray.EndCap = LineCap.Round;

                g.DrawLine(ray, 16f, 3.5f, 16f, 7f);
                g.DrawLine(ray, 16f, 25f, 16f, 28.5f);
                g.DrawLine(ray, 3.5f, 16f, 7f, 16f);
                g.DrawLine(ray, 25f, 16f, 28.5f, 16f);
                g.DrawLine(ray, 7.2f, 7.2f, 9.6f, 9.6f);
                g.DrawLine(ray, 22.4f, 22.4f, 24.8f, 24.8f);
                g.DrawLine(ray, 7.2f, 24.8f, 9.6f, 22.4f);
                g.DrawLine(ray, 22.4f, 9.6f, 24.8f, 7.2f);
            }
        }

        private static GraphicsPath CreateRounded(RectangleF rect, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float d = radius * 2f;

            path.AddArc(rect.Left, rect.Top, d, d, 180f, 90f);
            path.AddArc(rect.Right - d, rect.Top, d, d, 270f, 90f);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0f, 90f);
            path.AddArc(rect.Left, rect.Bottom - d, d, d, 90f, 90f);
            path.CloseFigure();
            return path;
        }
    }
}
