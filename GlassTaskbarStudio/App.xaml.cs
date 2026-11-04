using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using GlassTaskbarStudio.Core;
using GlassTaskbarStudio.UI;

namespace GlassTaskbarStudio
{
    public partial class App : Application
    {
        Mutex _mutex;
        EventWaitHandle _showEvent;
        TaskbarEngine _engine;
        TrayService _tray;
        MainWindow _window;
        MainViewModel _vm;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Emergency path: remove every effect and restore registry values, then quit.
            if (e.Args.Contains("--reset"))
            {
                // stop a running instance first, otherwise it would re-apply the effects a second later
                int self = System.Diagnostics.Process.GetCurrentProcess().Id;
                foreach (var proc in System.Diagnostics.Process.GetProcessesByName(System.Diagnostics.Process.GetCurrentProcess().ProcessName))
                {
                    if (proc.Id == self) continue;
                    try { proc.Kill(); proc.WaitForExit(2000); } catch { }
                    proc.Dispose();
                }
                TaskbarEngine.HardRestoreAll();
                var cfg = AppConfig.Load();
                SystemTweaks.RestoreExplorerOptions(cfg);
                cfg.EffectsEnabled = false; cfg.Save();
                Shutdown();
                return;
            }

            _mutex = new Mutex(true, @"Local\GlassTaskbarStudio.Single", out bool first);
            _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\GlassTaskbarStudio.Show");
            if (!first) { _showEvent.Set(); Shutdown(); return; }

            DispatcherUnhandledException += (s, a) => { Log(a.Exception); a.Handled = true; };
            AppDomain.CurrentDomain.UnhandledException += (s, a) => { Log(a.ExceptionObject as Exception); try { TaskbarEngine.HardRestoreAll(); } catch { } };
            SessionEnding += (s, a) => Cleanup();

            var config = AppConfig.Load();
            _engine = new TaskbarEngine { HideInFullscreen = config.HideInFullscreen, RefreshMs = config.RefreshMs };
            _vm = new MainViewModel(config, _engine);
            _window = new MainWindow(_vm);
            new System.Windows.Interop.WindowInteropHelper(_window).EnsureHandle(); // so the TaskbarCreated hook exists even while hidden

            _tray = new TrayService(_vm, ShowWindow, ExitApp);

            // keep autostart entry in sync with the saved preference (also repairs a moved exe)
            SystemTweaks.SetAutoStart(config.StartWithWindows);

            // Apply the saved look right away
            _engine.Apply(config.Applied, false);
            if (!config.EffectsEnabled) _engine.Disable();
            else
            {
                SystemTweaks.ApplyExplorerOptions(config.Applied, config);
                _engine.Enable();
            }

            new Thread(() =>
            {
                while (_showEvent.WaitOne()) { if (_window == null) break; Dispatcher.BeginInvoke(new Action(ShowWindow)); }
            }) { IsBackground = true }.Start();

            bool silent = e.Args.Contains("--tray") && config.FirstRunDone;
            if (!silent) ShowWindow();
            if (!config.FirstRunDone) { config.FirstRunDone = true; config.Save(); } // later autostarts stay in the tray
        }

        void ShowWindow()
        {
            if (_window == null) return;
            if (!_window.IsVisible) _window.Show();
            if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
            _window.Activate();
        }

        void ExitApp()
        {
            if (_window != null) _window.AllowClose = true;
            Shutdown();
        }

        void Cleanup()
        {
            try { _vm?.Config.Save(); } catch { }
            try { _engine?.Dispose(); } catch { }   // gives the taskbar back to Windows
            try { _tray?.Dispose(); } catch { }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Cleanup();
            try { _mutex?.ReleaseMutex(); } catch { }
            base.OnExit(e);
        }

        static void Log(Exception ex)
        {
            try
            {
                Directory.CreateDirectory(AppConfig.Folder);
                File.AppendAllText(Path.Combine(AppConfig.Folder, "error.log"), $"[{DateTime.Now:s}] {ex}\n\n");
            }
            catch { }
        }
    }
}
