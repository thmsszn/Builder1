using System;
using System.Linq;
using System.Threading;
using System.Windows;

namespace GlassBar
{
    public partial class App : Application
    {
        Mutex _mutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Notfall-Parameter: GlassBar.exe --restore  → Windows-Taskleiste wieder anzeigen
            if (e.Args.Contains("--restore"))
            {
                NativeTaskbar.Restore();
                Shutdown();
                return;
            }

            _mutex = new Mutex(true, "GlassBar_SingleInstance", out bool created);
            if (!created) { Shutdown(); return; }

            AppSettings.Load();
            AutoStart.Set(AppSettings.Current.AutoStart);

            DispatcherUnhandledException += (s, a) => { NativeTaskbar.Restore(); };
            AppDomain.CurrentDomain.ProcessExit += (s, a) => { if (NativeTaskbar.IsHidden) NativeTaskbar.Restore(); };

            new MainWindow().Show();
        }
    }
}
