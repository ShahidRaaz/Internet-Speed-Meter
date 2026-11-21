using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace InternetSpeedMeter
{
    public partial class SettingsWindow : Window
    {
        private const string RUN_REG_KEY = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string APP_NAME = "InternetSpeedMeter";
        private bool _initialStartupChecked = false;

        public SettingsWindow(bool isLightTheme, long totalDownBytes, long totalUpBytes, int alignment)
        {
            InitializeComponent();
            ApplyTheme(isLightTheme);

            // Populate session data
            TotalDownText.Text = FormatBytes(totalDownBytes);
            TotalUpText.Text = FormatBytes(totalUpBytes);
            AlignmentText.Text = alignment == 1 ? "Center" : "Left";
            ThemeText.Text = isLightTheme ? "Light" : "Dark";

            // Check startup status
            CheckStartupStatus();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Close dialog automatically if user clicks elsewhere
            this.Deactivated += (s, args) =>
            {
                try { this.Close(); } catch { }
            };

            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        }

        private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.General || e.Category == UserPreferenceCategory.Color)
            {
                Dispatcher.Invoke(() =>
                {
                    bool isLight = CheckSystemUsesLightTheme();
                    ApplyTheme(isLight);
                    ThemeText.Text = isLight ? "Light" : "Dark";
                });
            }
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

        private void ApplyTheme(bool isLight)
        {
            if (isLight)
            {
                // ==================== WINDOWS 11 OFFICIAL LIGHT THEME ====================
                DialogBorder.Background = new SolidColorBrush(Color.FromRgb(0xF9, 0xF9, 0xF9)); // Win11 Mica Base Light
                DialogBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xE5, 0xE5, 0xE5)); // Win11 SurfaceStrokeColorDefault
                TitleText.Foreground = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));    // Win11 TextFillColorPrimary
                CloseIcon.Foreground = new SolidColorBrush(Color.FromRgb(0x5C, 0x5C, 0x5C));    // Win11 TextFillColorSecondary
                HeaderIcon.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0x67, 0xC0));   // Win11 AccentFillColorDefault

                // Win11 Solid White Card over Mica backdrop
                var cardBg = Brushes.White;
                var cardBorder = new SolidColorBrush(Color.FromRgb(0xE5, 0xE5, 0xE5));

                Card1.Background = cardBg; Card1.BorderBrush = cardBorder;
                Card2.Background = cardBg; Card2.BorderBrush = cardBorder;
                Card3.Background = cardBg; Card3.BorderBrush = cardBorder;

                LabelSession.Foreground = new SolidColorBrush(Color.FromRgb(0x76, 0x76, 0x76)); // Win11 TextFillColorTertiary
                LabelTaskbar.Foreground = new SolidColorBrush(Color.FromRgb(0x76, 0x76, 0x76));

                LabelDl.Foreground = new SolidColorBrush(Color.FromRgb(0x5C, 0x5C, 0x5C));     // Win11 TextFillColorSecondary
                LabelUl.Foreground = new SolidColorBrush(Color.FromRgb(0x5C, 0x5C, 0x5C));
                LabelMode.Foreground = new SolidColorBrush(Color.FromRgb(0x5C, 0x5C, 0x5C));
                LabelTheme.Foreground = new SolidColorBrush(Color.FromRgb(0x5C, 0x5C, 0x5C));

                AlignmentText.Foreground = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
                ThemeText.Foreground = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
                StartupCheckBox.Foreground = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));

                // Download & Upload Icons and Values: Full 100% Opacity vibrant solid colors
                DlIconText.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0x67, 0xC0)); // Win11 Accent Blue
                DlIconText.Opacity = 1.0;
                TotalDownText.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0x67, 0xC0));

                UlIconText.Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0x7C, 0x10)); // Win11 Success Green
                UlIconText.Opacity = 1.0;
                TotalUpText.Foreground = new SolidColorBrush(Color.FromRgb(0x10, 0x7C, 0x10));

                // Win11 Accent Button
                DoneButton.Background = new SolidColorBrush(Color.FromRgb(0x00, 0x67, 0xC0));
                DoneButton.Foreground = Brushes.White;
                Resources["DoneButtonHoverBgBrush"] = new SolidColorBrush(Color.FromRgb(0xE5, 0xF1, 0xFB));
                Resources["DoneButtonHoverFgBrush"] = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
            }
            else
            {
                // ==================== WINDOWS 11 OFFICIAL DARK THEME ====================
                DialogBorder.Background = new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2C)); // Win11 Flyout Dark Surface
                DialogBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x38, 0x38, 0x38)); // Win11 SurfaceStrokeColorDefault Dark
                TitleText.Foreground = Brushes.White;
                CloseIcon.Foreground = new SolidColorBrush(Color.FromRgb(0xC8, 0xC8, 0xC8));
                HeaderIcon.Foreground = new SolidColorBrush(Color.FromRgb(0x4C, 0xC2, 0xFF));   // Win11 Vibrant Cyan

                // Win11 Translucent Card over Dark backdrop
                var cardBg = new SolidColorBrush(Color.FromArgb(0x0E, 0xFF, 0xFF, 0xFF)); // CardBackgroundFillColorDefault
                var cardBorder = new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)); // CardStrokeColorDefault

                Card1.Background = cardBg; Card1.BorderBrush = cardBorder;
                Card2.Background = cardBg; Card2.BorderBrush = cardBorder;
                Card3.Background = cardBg; Card3.BorderBrush = cardBorder;

                LabelSession.Foreground = new SolidColorBrush(Color.FromRgb(0x8B, 0x8B, 0x8B)); // Win11 TextFillColorTertiary
                LabelTaskbar.Foreground = new SolidColorBrush(Color.FromRgb(0x8B, 0x8B, 0x8B));

                LabelDl.Foreground = new SolidColorBrush(Color.FromRgb(0xC8, 0xC8, 0xC8));     // Win11 TextFillColorSecondary
                LabelUl.Foreground = new SolidColorBrush(Color.FromRgb(0xC8, 0xC8, 0xC8));
                LabelMode.Foreground = new SolidColorBrush(Color.FromRgb(0xC8, 0xC8, 0xC8));
                LabelTheme.Foreground = new SolidColorBrush(Color.FromRgb(0xC8, 0xC8, 0xC8));

                AlignmentText.Foreground = Brushes.White;
                ThemeText.Foreground = Brushes.White;
                StartupCheckBox.Foreground = Brushes.White;

                // Download & Upload Icons and Values: Full 100% Opacity vibrant solid colors
                DlIconText.Foreground = new SolidColorBrush(Color.FromRgb(0x4C, 0xC2, 0xFF)); // Win11 Vibrant Cyan
                DlIconText.Opacity = 1.0;
                TotalDownText.Foreground = new SolidColorBrush(Color.FromRgb(0x4C, 0xC2, 0xFF));

                UlIconText.Foreground = new SolidColorBrush(Color.FromRgb(0x6C, 0xCB, 0x5F)); // Win11 Success Green
                UlIconText.Opacity = 1.0;
                TotalUpText.Foreground = new SolidColorBrush(Color.FromRgb(0x6C, 0xCB, 0x5F));

                // Win11 Accent Button
                DoneButton.Background = new SolidColorBrush(Color.FromRgb(0x4C, 0xC2, 0xFF));
                DoneButton.Foreground = Brushes.Black;
                Resources["DoneButtonHoverBgBrush"] = new SolidColorBrush(Color.FromRgb(0x60, 0xCD, 0xFF));
                Resources["DoneButtonHoverFgBrush"] = Brushes.Black;
            }
        }

        private static string FormatBytes(long bytes)
        {
            double b = bytes;
            if (b < 1024) return $"{b:0} B";
            if (b < 1024 * 1024) return $"{b / 1024.0:0.0} KB";
            if (b < 1024 * 1024 * 1024) return $"{b / (1024.0 * 1024.0):0.0} MB";
            return $"{b / (1024.0 * 1024.0 * 1024.0):0.00} GB";
        }

        private void CheckStartupStatus()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RUN_REG_KEY, false);
                _initialStartupChecked = key?.GetValue(APP_NAME) != null;
                StartupCheckBox.IsChecked = _initialStartupChecked;
            }
            catch { }
            UpdateDoneButtonState();
        }

        private void StartupCheckBox_Click(object sender, RoutedEventArgs e)
        {
            UpdateDoneButtonState();
        }

        private void UpdateDoneButtonState()
        {
            bool isModified = (StartupCheckBox.IsChecked ?? false) != _initialStartupChecked;
            DoneButton.IsEnabled = isModified;
        }

        private void DoneButton_Click(object sender, RoutedEventArgs e)
        {
            bool isModified = (StartupCheckBox.IsChecked ?? false) != _initialStartupChecked;
            if (!isModified) return;

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RUN_REG_KEY, true);
                if (key != null)
                {
                    if (StartupCheckBox.IsChecked == true)
                    {
                        string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                        if (!string.IsNullOrEmpty(exePath))
                        {
                            key.SetValue(APP_NAME, $"\"{exePath}\"");
                        }
                    }
                    else
                    {
                        key.DeleteValue(APP_NAME, false);
                    }
                    _initialStartupChecked = StartupCheckBox.IsChecked ?? false;
                    UpdateDoneButtonState();
                }
            }
            catch { }

            this.Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
