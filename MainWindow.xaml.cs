using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;

namespace InternetSpeedMeter
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer? _speedTimer;
        private DispatcherTimer? _taskbarTrackTimer;
        private long _lastBytesReceived;
        private long _lastBytesSent;
        private DateTime _lastCheckTime;
        private bool _lastDownActive = false;
        private bool _lastUpActive = false;
        private bool _isLightTheme = false;

        // Taskbar alignment, Widgets status, System Tray width, and Auto-Hide tracking
        private int _currentAlignment = -1;
        private bool _lastWidgetsEnabled = true;
        private double _cachedTrayWidth = -1;
        private DateTime _lastTrayMeasureTime = DateTime.MinValue;
        private bool _isCustomPositioned = false;
        private Point _dragStartPoint;
        private bool _isDragging = false;
        private bool _hasDragged = false;

        // Session cumulative bytes
        private long _totalDownBytes = 0;
        private long _totalUpBytes = 0;

        private SettingsWindow? _settingsWindow;

        private IntPtr _hwnd = IntPtr.Zero;
        private IntPtr _taskbarHwnd = IntPtr.Zero;
        private IntPtr _tooltipHwnd = IntPtr.Zero;
        private IntPtr _winEventHook = IntPtr.Zero;
        private WinEventDelegate? _winEventDelegate;

        // Theme colors
        private Color _downColor;
        private Color _upColor;
        private Brush _downTextBrush = Brushes.White;
        private Brush _upTextBrush = Brushes.LightGray;

        // Activity threshold: 1 KB/s
        private const double ACTIVE_THRESHOLD_BYTES = 1024.0;

        // Win32 constants & structs
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        private const int GWL_EXSTYLE = -20;
        private const int GWLP_HWNDPARENT = -8;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int SM_CYSCREEN = 1;
        private const uint MONITOR_DEFAULTTOPRIMARY = 1;

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;

        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        private const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
        private const uint WINEVENT_OUTOFCONTEXT = 0;

        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string? lpszClass, string? lpszWindow);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            return IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : (IntPtr)GetWindowLong32(hWnd, nIndex);
        }

        private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            return IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong) : (IntPtr)SetWindowLong32(hWnd, nIndex, (int)dwNewLong);
        }

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _hwnd = new WindowInteropHelper(this).Handle;

            // Apply Win32 ToolWindow style (hide from Alt+Tab)
            ApplyToolWindowStyle();

            // Attach to Taskbar so it stays permanently in front of the taskbar
            AttachToTaskbar();

            // Detect and apply initial Windows theme (Light / Dark)
            ApplyThemeColors();

            // Auto-position based on taskbar alignment and auto-hide status
            UpdateTaskbarPositionAndVisibility();

            // Start 16ms (60 FPS) taskbar tracking timer for instant Auto-Hide & Alignment updates
            _taskbarTrackTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _taskbarTrackTimer.Tick += (s, args) => UpdateTaskbarPositionAndVisibility();
            _taskbarTrackTimer.Start();

            // Listen for Windows theme & taskbar preference changes live
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

            // Initialize baseline network stats
            InitNetworkCounters();

            // Start 1-second refresh timer for real speed
            _speedTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _speedTimer.Tick += SpeedTimer_Tick;
            _speedTimer.Start();
        }

        private void Window_Closed(object? sender, EventArgs e)
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;

            _taskbarTrackTimer?.Stop();
            _speedTimer?.Stop();

            if (_winEventHook != IntPtr.Zero)
            {
                UnhookWinEvent(_winEventHook);
                _winEventHook = IntPtr.Zero;
            }

            Environment.Exit(0);
        }

        private void AttachToTaskbar()
        {
            _taskbarHwnd = FindWindow("Shell_TrayWnd", null);
            if (_taskbarHwnd != IntPtr.Zero)
            {
                // Setting taskbar as owner ensures Windows keeps our window above the taskbar in Z-order
                SetWindowLongPtr(_hwnd, GWLP_HWNDPARENT, _taskbarHwnd);
            }

            EnsureTopmost();

            // Hook foreground events so whenever taskbar is clicked, we immediately stay on top
            _winEventDelegate = WinEventProc;
            _winEventHook = SetWinEventHook(
                EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero, _winEventDelegate, 0, 0, WINEVENT_OUTOFCONTEXT);
        }

        private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            UpdateTaskbarPositionAndVisibility();
            EnsureTopmost();
        }

        private void EnsureTopmost()
        {
            if (_hwnd != IntPtr.Zero && this.Visibility == Visibility.Visible)
            {
                SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                if (_tooltipHwnd != IntPtr.Zero && IsWindow(_tooltipHwnd))
                {
                    SetWindowPos(_tooltipHwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                }
            }
        }

        private void BadgeToolTip_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ToolTip tt)
            {
                var hwndSource = PresentationSource.FromVisual(tt) as HwndSource;
                if (hwndSource != null && hwndSource.Handle != IntPtr.Zero)
                {
                    _tooltipHwnd = hwndSource.Handle;
                    SetWindowPos(_tooltipHwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                }
            }
        }

        private void BadgeToolTip_Closed(object sender, RoutedEventArgs e)
        {
            _tooltipHwnd = IntPtr.Zero;
        }

        private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                ApplyThemeColors();
                UpdateTaskbarPositionAndVisibility();
            });
        }

        private static bool CheckSystemUsesLightTheme()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key?.GetValue("SystemUsesLightTheme") is int val)
                {
                    return val == 1;
                }
            }
            catch { }
            return false;
        }

        private static int GetTaskbarAlignment()
        {
            try
            {
                // TaskbarAl: 0 = Left Aligned, 1 = Center Aligned (Default Win 11)
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
                if (key?.GetValue("TaskbarAl") is int val)
                {
                    return val;
                }
            }
            catch { }
            return 1; // Default to Center
        }

        private static bool IsWidgetsEnabled()
        {
            try
            {
                // TaskbarDa: 0 = Widgets hidden/disabled, 1 = Widgets enabled/visible
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
                if (key?.GetValue("TaskbarDa") is int val)
                {
                    return val == 1;
                }
            }
            catch { }
            return true; // Default to enabled in Windows 11
        }

        private double GetSystemTrayWidth(double dpiScaleX)
        {
            // 1. Try Win32 direct child search on Shell_TrayWnd for classic TrayNotifyWnd
            if (_taskbarHwnd != IntPtr.Zero)
            {
                IntPtr hTray = FindWindowEx(_taskbarHwnd, IntPtr.Zero, "TrayNotifyWnd", null);
                if (hTray != IntPtr.Zero && GetWindowRect(hTray, out RECT tr))
                {
                    double w = (tr.Right - tr.Left) / dpiScaleX;
                    if (w >= 60 && w <= 650) return w;
                }

                // Check for any child window docked at the right edge of Shell_TrayWnd
                if (GetWindowRect(_taskbarHwnd, out RECT tbRect))
                {
                    IntPtr hChild = IntPtr.Zero;
                    double maxRightWidth = 0;
                    while ((hChild = FindWindowEx(_taskbarHwnd, hChild, null, null)) != IntPtr.Zero)
                    {
                        if (GetWindowRect(hChild, out RECT cr))
                        {
                            if (Math.Abs(cr.Right - tbRect.Right) <= 8 && cr.Right > cr.Left)
                            {
                                double cw = (cr.Right - cr.Left) / dpiScaleX;
                                if (cw > maxRightWidth && cw <= 650)
                                {
                                    maxRightWidth = cw;
                                }
                            }
                        }
                    }
                    if (maxRightWidth >= 60) return maxRightWidth;
                }
            }

            // 2. Trigger async background UIA measurement if needed
            RefreshTrayWidthAsync();

            // 3. Return cached UIA measurement if valid
            if (_cachedTrayWidth >= 60)
            {
                return _cachedTrayWidth;
            }

            // 4. Fallback: Standard Windows 11 system tray width (~220 DIP)
            return 220.0;
        }

        private void RefreshTrayWidthAsync()
        {
            if ((DateTime.UtcNow - _lastTrayMeasureTime).TotalSeconds < 5) return;
            _lastTrayMeasureTime = DateTime.UtcNow;

            Task.Run(() =>
            {
                try
                {
                    var root = AutomationElement.RootElement;
                    var taskbar = root.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ClassNameProperty, "Shell_TrayWnd"));
                    if (taskbar != null)
                    {
                        var tray = taskbar.FindFirst(TreeScope.Descendants, new OrCondition(
                            new PropertyCondition(AutomationElement.AutomationIdProperty, "SystemTrayFrame"),
                            new PropertyCondition(AutomationElement.ClassNameProperty, "TrayNotifyWnd"),
                            new PropertyCondition(AutomationElement.LocalizedControlTypeProperty, "system tray")
                        ));

                        if (tray != null)
                        {
                            var rect = tray.Current.BoundingRectangle;
                            if (rect.Width >= 60 && rect.Width <= 650)
                            {
                                _cachedTrayWidth = rect.Width;
                            }
                        }
                    }
                }
                catch { }
            });
        }

        private void ApplyAlignmentPosition(double screenWidthDIP, double dpiScaleX)
        {
            double widgetWidth = this.ActualWidth > 0 ? this.ActualWidth : 110.0;
            bool widgetsEnabled = IsWidgetsEnabled();
            double trayWidth = GetSystemTrayWidth(dpiScaleX);

            if (_currentAlignment == 1)
            {
                // ==================== CENTER-ALIGNED TASKBAR ====================
                if (widgetsEnabled)
                {
                    // Win 11 Widgets board is at the far-left (~160px).
                    // Place Internet Speed Meter immediately to its right.
                    this.Left = 165;
                }
                else
                {
                    // Win 11 Widgets board is DISABLED -> far-left edge is open!
                    // Place Internet Speed Meter at the clean left edge of taskbar.
                    this.Left = 12;
                }
            }
            else
            {
                // ==================== LEFT-ALIGNED TASKBAR ====================
                // In Left alignment, Start and app icons are on the left.
                // On the right: System Tray occupies `trayWidth` from the right edge.
                double trayLeft = screenWidthDIP - trayWidth;

                if (widgetsEnabled)
                {
                    // In Windows 11 Left alignment, the Widgets board container sits
                    // immediately to the left of the System Tray (~150px wide).
                    // Place Internet Speed Meter immediately to the LEFT of the Win 11 Widgets board container:
                    double widgetsContainerWidth = 150.0;
                    this.Left = Math.Max(200, trayLeft - widgetsContainerWidth - widgetWidth - 20);
                }
                else
                {
                    // Widgets board is DISABLED -> place immediately to the left of System Tray.
                    this.Left = Math.Max(200, trayLeft - widgetWidth - 10);
                }
            }
        }

        private void UpdateTaskbarPositionAndVisibility()
        {
            if (_isDragging) return;

            if (_taskbarHwnd == IntPtr.Zero || !IsWindow(_taskbarHwnd))
            {
                _taskbarHwnd = FindWindow("Shell_TrayWnd", null);
                if (_taskbarHwnd != IntPtr.Zero)
                {
                    SetWindowLongPtr(_hwnd, GWLP_HWNDPARENT, _taskbarHwnd);
                }
            }

            var dpi = VisualTreeHelper.GetDpi(this);
            double dpiScaleX = dpi.DpiScaleX;
            double dpiScaleY = dpi.DpiScaleY;
            double screenHeightDIP = SystemParameters.PrimaryScreenHeight;
            double screenWidthDIP = SystemParameters.PrimaryScreenWidth;

            RECT taskbarRect;
            if (_taskbarHwnd != IntPtr.Zero && GetWindowRect(_taskbarHwnd, out taskbarRect))
            {
                int taskbarHeight = taskbarRect.Bottom - taskbarRect.Top;
                int monitorBottom = (int)Math.Round(screenHeightDIP * dpiScaleY);

                try
                {
                    MONITORINFO mi = new MONITORINFO();
                    mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
                    IntPtr hMon = MonitorFromWindow(_taskbarHwnd, MONITOR_DEFAULTTOPRIMARY);
                    if (hMon != IntPtr.Zero && GetMonitorInfo(hMon, ref mi))
                    {
                        monitorBottom = mi.rcMonitor.Bottom;
                    }
                }
                catch { }

                // Windows 11 taskbar resting height is 48 DIP (60px at 125% DPI, 48px at 100% DPI).
                double defaultTaskbarHeightDIP = 48.0;
                double expectedTaskbarHeight = defaultTaskbarHeightDIP * dpiScaleY;
                int restingPhysicalTop = (int)Math.Round(monitorBottom - expectedTaskbarHeight);

                // Fixed vertical position: Always stay at the centered taskbar resting position.
                // Do NOT move or slide the container down when the taskbar hides!
                double restingTopDIP = screenHeightDIP - defaultTaskbarHeightDIP;
                this.Top = restingTopDIP + (defaultTaskbarHeightDIP - this.Height) / 2;

                // Instant Auto-Hide:
                // The moment the taskbar moves even 3px down from its resting position,
                // or its height starts collapsing, INSTANTLY make our widget invisible on the spot!
                bool isTaskbarHidden = (taskbarRect.Top > restingPhysicalTop + 3) || (taskbarHeight < (int)expectedTaskbarHeight - 3);

                if (isTaskbarHidden)
                {
                    if (this.Visibility != Visibility.Hidden)
                    {
                        this.Visibility = Visibility.Hidden;
                        this.Opacity = 0.0;
                    }

                    if (MainBadge.ToolTip is ToolTip tt && tt.IsOpen)
                    {
                        tt.IsOpen = false;
                    }

                    // Auto-close settings if open when taskbar hides
                    if (_settingsWindow != null && _settingsWindow.IsLoaded)
                    {
                        _settingsWindow.Close();
                        _settingsWindow = null;
                    }
                    return;
                }
                else
                {
                    if (this.Visibility != Visibility.Visible)
                    {
                        this.Visibility = Visibility.Visible;
                        this.Opacity = 1.0;
                    }
                }
            }
            else
            {
                // Fallback positioning
                double workBottom = SystemParameters.WorkArea.Bottom;
                double taskbarHeight = Math.Max(40, screenHeightDIP - workBottom);
                this.Top = workBottom + (taskbarHeight - this.Height) / 2;

                if (this.Visibility != Visibility.Visible)
                {
                    this.Visibility = Visibility.Visible;
                    this.Opacity = 1.0;
                }
            }

            // Taskbar alignment & Widgets status live tracking
            int alignment = GetTaskbarAlignment();
            bool widgetsEnabled = IsWidgetsEnabled();

            if (alignment != _currentAlignment || widgetsEnabled != _lastWidgetsEnabled)
            {
                _currentAlignment = alignment;
                _lastWidgetsEnabled = widgetsEnabled;
                _isCustomPositioned = false; // Reset custom drag position on alignment or widget switch
            }

            if (!_isCustomPositioned)
            {
                ApplyAlignmentPosition(screenWidthDIP, dpiScaleX);
            }

            EnsureTopmost();
        }

        private void ApplyThemeColors()
        {
            _isLightTheme = CheckSystemUsesLightTheme();

            if (_isLightTheme)
            {
                // ==================== WINDOWS 11 OFFICIAL LIGHT THEME ====================
                _downColor = Color.FromRgb(0x00, 0x67, 0xC0);       // Win11 AccentFillColorDefault (Fluent Blue)
                _upColor = Color.FromRgb(0x10, 0x7C, 0x10);         // Win11 Success Green
                _downTextBrush = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)); // Win11 TextFillColorPrimary
                _upTextBrush = new SolidColorBrush(Color.FromRgb(0x5C, 0x5C, 0x5C));   // Win11 TextFillColorSecondary

                // Win11 SubtleFillColorSecondary hover tints & divider (~5.9% black hover)
                Resources["TaskbarHoverBrush"] = new SolidColorBrush(Color.FromArgb(0x75, 0xFF, 0xFF, 0xFF));
                Resources["MenuItemHoverBrush"] = new SolidColorBrush(Color.FromArgb(0x0D, 0x00, 0x00, 0x00));
                Resources["MenuSeparatorBrush"] = new SolidColorBrush(Color.FromRgb(0xE5, 0xE5, 0xE5));

                if (FindResource("MeterContextMenu") is ContextMenu cm)
                {
                    cm.Background = new SolidColorBrush(Color.FromRgb(0xF9, 0xF9, 0xF9)); // Win11 Mica Base Light
                    cm.Foreground = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
                    cm.BorderBrush = new SolidColorBrush(Color.FromRgb(0xE5, 0xE5, 0xE5)); // Win11 SurfaceStrokeColorDefault
                }

                if (MainBadge?.ToolTip is ToolTip ttLight)
                {
                    ttLight.Background = new SolidColorBrush(Color.FromRgb(0xF9, 0xF9, 0xF9));
                    ttLight.Foreground = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
                    ttLight.BorderBrush = new SolidColorBrush(Color.FromRgb(0xE5, 0xE5, 0xE5));
                }
            }
            else
            {
                // ==================== WINDOWS 11 OFFICIAL DARK THEME ====================
                _downColor = Color.FromRgb(0x4C, 0xC2, 0xFF);       // Win11 Vibrant Cyan
                _upColor = Color.FromRgb(0x6C, 0xCB, 0x5F);         // Win11 Success Green Dark
                _downTextBrush = Brushes.White;                      // Win11 TextFillColorPrimary
                _upTextBrush = new SolidColorBrush(Color.FromRgb(0xC8, 0xC8, 0xC8));   // Win11 TextFillColorSecondary

                // Win11 SubtleFillColorSecondary hover tints & divider (~8.2% white hover)
                Resources["TaskbarHoverBrush"] = new SolidColorBrush(Color.FromArgb(0x0F, 0x00, 0x00, 0x00));
                Resources["MenuItemHoverBrush"] = new SolidColorBrush(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF));
                Resources["MenuSeparatorBrush"] = new SolidColorBrush(Color.FromRgb(0x38, 0x38, 0x38));

                if (FindResource("MeterContextMenu") is ContextMenu cm)
                {
                    cm.Background = new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2C)); // Win11 Taskbar Flyout Dark
                    cm.Foreground = Brushes.White;
                    cm.BorderBrush = new SolidColorBrush(Color.FromRgb(0x38, 0x38, 0x38)); // Win11 SurfaceStrokeColorDefault Dark
                }

                if (MainBadge?.ToolTip is ToolTip ttDark)
                {
                    ttDark.Background = new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2C));
                    ttDark.Foreground = Brushes.White;
                    ttDark.BorderBrush = new SolidColorBrush(Color.FromRgb(0x38, 0x38, 0x38));
                }
            }

            // Apply Brushes
            var downBrush = new SolidColorBrush(_downColor);
            var upBrush = new SolidColorBrush(_upColor);

            DownArrowPath.Stroke = downBrush;
            DownSymbolText.Foreground = downBrush;
            DownSpeedText.Foreground = _downTextBrush;

            UpArrowPath.Stroke = upBrush;
            UpSymbolText.Foreground = upBrush;
            UpSpeedText.Foreground = _upTextBrush;

            // Re-apply current active opacity states with new theme
            UpdateArrowActive(DownArrowPath, _lastDownActive, immediate: true);
            UpdateArrowActive(UpArrowPath, _lastUpActive, immediate: true);
        }

        private void ApplyToolWindowStyle()
        {
            long exStyle = GetWindowLongPtr(_hwnd, GWL_EXSTYLE).ToInt64();
            exStyle |= WS_EX_TOOLWINDOW;
            SetWindowLongPtr(_hwnd, GWL_EXSTYLE, (IntPtr)exStyle);
        }

        private void InitNetworkCounters()
        {
            _lastBytesReceived = 0;
            _lastBytesSent = 0;

            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                                 ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                                 ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel);

                foreach (var ni in interfaces)
                {
                    var stats = ni.GetIPv4Statistics();
                    _lastBytesReceived += stats.BytesReceived;
                    _lastBytesSent += stats.BytesSent;
                }
            }
            catch { }

            _lastCheckTime = DateTime.UtcNow;
        }

        private void SpeedTimer_Tick(object? sender, EventArgs e)
        {
            EnsureTopmost();

            long currentBytesRecv = 0;
            long currentBytesSent = 0;

            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                                 ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                                 ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel);

                foreach (var ni in interfaces)
                {
                    var stats = ni.GetIPv4Statistics();
                    currentBytesRecv += stats.BytesReceived;
                    currentBytesSent += stats.BytesSent;
                }
            }
            catch
            {
                return;
            }

            DateTime now = DateTime.UtcNow;
            double elapsedSeconds = (now - _lastCheckTime).TotalSeconds;
            if (elapsedSeconds <= 0.2) return;

            long diffRecv = Math.Max(0, currentBytesRecv - _lastBytesReceived);
            long diffSent = Math.Max(0, currentBytesSent - _lastBytesSent);

            _totalDownBytes += diffRecv;
            _totalUpBytes += diffSent;

            double downBytesPerSec = diffRecv / elapsedSeconds;
            double upBytesPerSec = diffSent / elapsedSeconds;

            _lastBytesReceived = currentBytesRecv;
            _lastBytesSent = currentBytesSent;
            _lastCheckTime = now;

            // Update Speed Texts
            DownSpeedText.Text = FormatSpeed(downBytesPerSec);
            UpSpeedText.Text = FormatSpeed(upBytesPerSec);

            // Active Opacity: Full opacity when active, dimmed when idle (Zero Drop Shadow)
            bool isDownActive = downBytesPerSec >= ACTIVE_THRESHOLD_BYTES;
            bool isUpActive = upBytesPerSec >= ACTIVE_THRESHOLD_BYTES;

            if (isDownActive != _lastDownActive)
            {
                _lastDownActive = isDownActive;
                UpdateArrowActive(DownArrowPath, isDownActive);
            }

            if (isUpActive != _lastUpActive)
            {
                _lastUpActive = isUpActive;
                UpdateArrowActive(UpArrowPath, isUpActive);
            }
        }

        private void UpdateArrowActive(UIElement path, bool isActive, bool immediate = false)
        {
            double idleOpacity = _isLightTheme ? 0.30 : 0.25;
            double targetOpacity = isActive ? 1.0 : idleOpacity;

            if (immediate)
            {
                path.BeginAnimation(UIElement.OpacityProperty, null);
                path.Opacity = targetOpacity;
                return;
            }

            TimeSpan duration = TimeSpan.FromMilliseconds(200);
            var anim = new DoubleAnimation(targetOpacity, duration);
            path.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        private static string FormatSpeed(double bytesPerSec)
        {
            if (bytesPerSec < 1024)
            {
                return $"{bytesPerSec:0} B/s";
            }
            if (bytesPerSec < 1024 * 1024)
            {
                return $"{bytesPerSec / 1024.0:0.0} KB/s";
            }
            if (bytesPerSec < 1024 * 1024 * 1024)
            {
                return $"{bytesPerSec / (1024.0 * 1024.0):0.0} MB/s";
            }
            return $"{bytesPerSec / (1024.0 * 1024.0 * 1024.0):0.00} GB/s";
        }

        private void MainBadge_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(this);
            _isDragging = false;
            _hasDragged = false;
        }

        private void MainBadge_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && !_isDragging)
            {
                Point current = e.GetPosition(this);
                if (Math.Abs(current.X - _dragStartPoint.X) > 4 || Math.Abs(current.Y - _dragStartPoint.Y) > 4)
                {
                    _isDragging = true;
                    _hasDragged = true;
                    _isCustomPositioned = true;
                    try
                    {
                        this.DragMove();
                    }
                    catch { }
                    finally
                    {
                        _isDragging = false;
                    }
                }
            }
        }

        private void MainBadge_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_hasDragged)
            {
                _hasDragged = false;
                return;
            }

            OpenOrToggleSettings();
        }

        private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            OpenOrToggleSettings();
        }

        private void OpenOrToggleSettings()
        {
            if (_settingsWindow != null && _settingsWindow.IsLoaded)
            {
                _settingsWindow.Close();
                _settingsWindow = null;
                return;
            }

            int alignment = _currentAlignment >= 0 ? _currentAlignment : GetTaskbarAlignment();
            _settingsWindow = new SettingsWindow(_isLightTheme, _totalDownBytes, _totalUpBytes, alignment);

            // Position directly above the taskbar widget
            double targetLeft = this.Left;
            double targetTop = this.Top - _settingsWindow.Height - 8;

            double screenWidth = SystemParameters.PrimaryScreenWidth;
            if (targetLeft + _settingsWindow.Width > screenWidth - 10)
            {
                targetLeft = screenWidth - _settingsWindow.Width - 10;
            }
            if (targetLeft < 10) targetLeft = 10;

            _settingsWindow.Left = targetLeft;
            _settingsWindow.Top = targetTop;
            _settingsWindow.Closed += (s, args) => _settingsWindow = null;
            _settingsWindow.Show();
            _settingsWindow.Activate();
        }

        private void ResetPosMenuItem_Click(object sender, RoutedEventArgs e)
        {
            _isCustomPositioned = false;
            _currentAlignment = -1; // Force immediate recalculation
            UpdateTaskbarPositionAndVisibility();
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Environment.Exit(0);
        }
    }
}
