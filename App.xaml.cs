using System;
using System.Diagnostics;
using System.Windows;

namespace InternetSpeedMeter
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Automatically close any previously running instance to prevent file locks
            try
            {
                var current = Process.GetCurrentProcess();
                foreach (var p in Process.GetProcessesByName(current.ProcessName))
                {
                    if (p.Id != current.Id)
                    {
                        try
                        {
                            p.Kill();
                            p.WaitForExit(1500);
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }
    }
}
