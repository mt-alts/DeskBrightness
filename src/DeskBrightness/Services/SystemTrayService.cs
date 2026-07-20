using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using DeskBrightness.Config;
using Microsoft.Extensions.DependencyInjection;

namespace DeskBrightness.Services
{
    public sealed class SystemTrayService : IDisposable
    {
        private const uint WM_TRAYICON = 0x8000;
        private const uint WM_LBUTTONDBLCLK = 0x0203;
        private const uint WM_RBUTTONUP = 0x0205;
        private const uint WM_CONTEXTMENU = 0x007B;

        private bool _disposed;
        private bool _isTrayMode;
        private Window? _targetWindow;
        private ServiceProvider? _services;
        private IntPtr _hWnd;
        private IntPtr _hIcon;
        private HwndSource? _source;
        private System.Windows.Threading.DispatcherTimer? _keepAliveTimer;
        private readonly uint _msgId;
        private readonly LocalizationService _localization;

        public SystemTrayService(LocalizationService localization)
        {
            _localization = localization;
            _msgId = WM_TRAYICON + (uint)Guid.NewGuid().GetHashCode() & 0x7FFF;
            _hWnd = IntPtr.Zero;
            _hIcon = IntPtr.Zero;
            _isTrayMode = false;
        }

        public void Attach(Window window)
        {
            _targetWindow = window;
            _targetWindow.SourceInitialized += OnSourceInitialized;
            _targetWindow.Closing += OnWindowClosing;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            RemoveIcon();
            _targetWindow = null;
            GC.SuppressFinalize(this);
        }

        private void OnSourceInitialized(object? sender, EventArgs e)
        {
            if (_targetWindow is null) return;

            _hWnd = new WindowInteropHelper(_targetWindow).Handle;
            _source = HwndSource.FromHwnd(_hWnd);
            _source?.AddHook(WndProc);

            _hIcon = LoadImage(
                IntPtr.Zero,
                Path.Combine(
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".",
                                        AppConfig.FileSystem.IconFileName
                ),
                1, 16, 16, 0x00000010
            );
        }

        public void ShowIcon()
        {
            if (_hWnd == IntPtr.Zero || _disposed) return;

            var nid = CreateNid();
            nid.uVersion = 0;
            Shell_NotifyIconW(0x00000000, ref nid);

            nid.uVersion = 4;
            Shell_NotifyIconW(0x00000004, ref nid);
        }

        private void RemoveIcon()
        {
            if (_hWnd == IntPtr.Zero) return;

            var nid = CreateNid();
            Shell_NotifyIconW(0x00000001, ref nid);

            if (_hIcon != IntPtr.Zero)
            {
                DestroyIcon(_hIcon);
                _hIcon = IntPtr.Zero;
            }
        }

        private NOTIFYICONDATAW CreateNid()
        {
            var nid = new NOTIFYICONDATAW
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
                hWnd = _hWnd,
                uID = 0,
                uFlags = 0x00000001 | 0x00000002 | 0x00000004 | 0x00000008,
                uCallbackMessage = _msgId,
                hIcon = _hIcon,
                szTip = AppConfig.Metadata.AppName,
                guidItem = Guid.Empty,
            };
            return nid;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg != (int)_msgId)
                return IntPtr.Zero;

            switch ((uint)lParam)
            {
                case WM_LBUTTONDBLCLK:
                    RestoreWindow();
                    handled = true;
                    break;

                case WM_RBUTTONUP:
                case WM_CONTEXTMENU:
                    ShowContextMenu();
                    handled = true;
                    break;
            }

            return IntPtr.Zero;
        }

        private void ShowContextMenu()
        {
            var menu = CreatePopupMenu();
            const uint showCmd = 0x0001;
            const uint exitCmd = 0x0002;

            AppendMenu(menu, 0x00000000, showCmd, _localization.Get("TrayShow"));
            AppendMenu(menu, 0x00000800, 0, null);
            AppendMenu(menu, 0x00000000, exitCmd, _localization.Get("TrayExit"));

            GetCursorPos(out var pt);
            SetForegroundWindow(_hWnd);

            var cmd = TrackPopupMenuEx(menu, 0x00000100, pt.x, pt.y, _hWnd, IntPtr.Zero);

            if (cmd == showCmd)
                RestoreWindow();
            else if (cmd == exitCmd)
                DoExit();

            DestroyMenu(menu);
        }

        private void DoExit()
        {
            if (_targetWindow is not null)
                _targetWindow.Closing -= OnWindowClosing;

            RemoveIcon();
            _source?.RemoveHook(WndProc);
            Dispose();
            Application.Current.Shutdown();
        }

        public void OnEnterTrayMode()
        {
            _isTrayMode = true;
            ShowIcon();
            _services = (Application.Current as App)?.Services;

            if (_keepAliveTimer is null)
            {
                _keepAliveTimer = new System.Windows.Threading.DispatcherTimer(
                    TimeSpan.FromSeconds(AppConfig.Timing.KeepAliveTimerSeconds),
                    System.Windows.Threading.DispatcherPriority.Background,
                    (_, _) => { },
                    System.Windows.Application.Current.Dispatcher
                );
            }
            _keepAliveTimer.Start();
        }

        public void OnLeaveTrayMode()
        {
            _isTrayMode = false;
            RemoveIcon();
            _keepAliveTimer?.Stop();
        }

        private void RestoreWindow()
        {
            if (_targetWindow is null) return;

            _targetWindow.ShowInTaskbar = true;
            _targetWindow.WindowState = WindowState.Normal;
            _targetWindow.Left = double.NaN;
            _targetWindow.Top = double.NaN;
            _targetWindow.Activate();
        }

        private void OnWindowClosing(object? sender, CancelEventArgs e)
        {
            if (_targetWindow is null) return;

            var vm = _targetWindow.DataContext as ViewModels.MainViewModel;
            if (vm is not null && vm.MinimizeToTraySetting)
            {
                e.Cancel = true;
                _targetWindow.ShowInTaskbar = false;
                _targetWindow.WindowState = WindowState.Minimized;
                OnEnterTrayMode();
            }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool Shell_NotifyIconW(uint dwMessage, ref NOTIFYICONDATAW lpdata);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadImage(IntPtr hinst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool InsertMenu(IntPtr hMenu, uint uPosition, uint uFlags, uint uIDNewItem, string? lpNewItem);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string? lpNewItem);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint TrackPopupMenuEx(IntPtr hMenu, uint uFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);

        [DllImport("user32.dll")]
        private static extern bool DestroyMenu(IntPtr hMenu);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int x; public int y; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NOTIFYICONDATAW
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public uint dwState;
            public uint dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public uint uVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public uint dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }
    }
}