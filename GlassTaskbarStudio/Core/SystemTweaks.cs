using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace GlassTaskbarStudio.Core
{
    /// <summary>
    /// Registry-backed Windows 11 taskbar options (icon alignment, size) and autostart.
    /// Everything is HKCU - no administrator rights needed.
    /// </summary>
    public static class SystemTweaks
    {
        const string AdvancedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunName = "GlassTaskbarStudio";

        static int? Read(string name)
        {
            try { using (var k = Registry.CurrentUser.OpenSubKey(AdvancedKey)) return k?.GetValue(name) as int?; }
            catch { return null; }
        }

        static void Write(string name, int? value)
        {
            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(AdvancedKey))
                {
                    if (value.HasValue) k.SetValue(name, value.Value, RegistryValueKind.DWord);
                    else k.DeleteValue(name, false);
                }
            }
            catch { }
        }

        static void Broadcast()
        {
            try { Native.SendMessageTimeout(Native.HWND_BROADCAST, 0x1A, UIntPtr.Zero, "TraySettings", 2, 2000, out _); }
            catch { }
        }

        /// <summary>Applies alignment + size. Returns true if Explorer must be restarted for the size to take effect.</summary>
        public static bool ApplyExplorerOptions(TaskbarSettings s, AppConfig cfg)
        {
            bool needsRestart = false, changed = false;
            if (!Capabilities.SupportsAlignment && !Capabilities.SupportsSizeTweak) return false;

            if (!cfg.Backup.Captured)
            {
                cfg.Backup.TaskbarAl = Read("TaskbarAl");
                cfg.Backup.TaskbarSi = Read("TaskbarSi");
                cfg.Backup.Captured = true;
                cfg.Save();
            }

            if (Capabilities.SupportsAlignment)
            {
                int al = s.Alignment == IconAlignment.Center ? 1 : 0;
                if ((Read("TaskbarAl") ?? 1) != al) { Write("TaskbarAl", al); changed = true; }
            }
            if (Capabilities.SupportsSizeTweak)
            {
                int si = (int)s.Height; // 0 small, 1 medium (default), 2 large
                if ((Read("TaskbarSi") ?? 1) != si) { Write("TaskbarSi", si); changed = true; needsRestart = true; }
            }
            if (changed) Broadcast();
            return needsRestart;
        }

        /// <summary>Puts back exactly what was there before this app touched anything.</summary>
        public static void RestoreExplorerOptions(AppConfig cfg)
        {
            if (!cfg.Backup.Captured) return;
            if (Capabilities.SupportsAlignment) Write("TaskbarAl", cfg.Backup.TaskbarAl);
            if (Capabilities.SupportsSizeTweak) Write("TaskbarSi", cfg.Backup.TaskbarSi);
            Broadcast();
        }

        public static void SetAutoStart(bool enabled)
        {
            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (enabled) k.SetValue(RunName, $"\"{Environment.ProcessPath}\" --tray");
                    else k.DeleteValue(RunName, false);
                }
            }
            catch { }
        }

        public static void RestartExplorer()
        {
            foreach (var p in Process.GetProcessesByName("explorer"))
            {
                try { p.Kill(); } catch { }
                p.Dispose();
            }
            // Windows relaunches the shell automatically; start it ourselves if it did not.
            System.Threading.Tasks.Task.Delay(2500).ContinueWith(_ =>
            {
                if (Process.GetProcessesByName("explorer").Length == 0)
                    try { Process.Start("explorer.exe"); } catch { }
            });
        }
    }
}
