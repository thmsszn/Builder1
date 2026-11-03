using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GlassBar
{
    public class PinnedApp
    {
        public string Name { get; set; }
        public string Path { get; set; }

        BitmapSource _icon;
        [JsonIgnore] public BitmapSource Icon => _icon ??= IconHelper.FromPath(Path);
    }

    public class AppSettings
    {
        public bool AutoStart { get; set; } = true;
        public bool HideNativeTaskbar { get; set; } = true;
        public bool ShowStats { get; set; } = true;
        public bool Clock24h { get; set; } = true;
        public bool Top { get; set; } = false;
        public string Tint { get; set; } = "Blau";
        public int TintAlpha { get; set; } = 110;
        public List<PinnedApp> Pinned { get; set; } = new List<PinnedApp>();

        public static readonly Dictionary<string, byte[]> Tints = new Dictionary<string, byte[]>
        {
            ["Blau"] = new byte[] { 30, 90, 210 },
            ["Violett"] = new byte[] { 120, 60, 200 },
            ["Schwarz"] = new byte[] { 12, 12, 16 },
            ["Rosa"] = new byte[] { 215, 70, 150 },
            ["Grün"] = new byte[] { 20, 150, 95 },
            ["Rot"] = new byte[] { 200, 45, 55 },
        };

        public static AppSettings Current { get; private set; } = new AppSettings();

        static string FilePath
        {
            get
            {
                var dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GlassBar");
                Directory.CreateDirectory(dir);
                return System.IO.Path.Combine(dir, "settings.json");
            }
        }

        public static void Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    Current = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
                    return;
                }
            }
            catch { }
            Current = new AppSettings();
            string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string sys = Environment.SystemDirectory;
            Current.Pinned.Add(new PinnedApp { Name = "Explorer", Path = System.IO.Path.Combine(win, "explorer.exe") });
            Current.Pinned.Add(new PinnedApp { Name = "Editor", Path = System.IO.Path.Combine(sys, "notepad.exe") });
            Current.Pinned.Add(new PinnedApp { Name = "Rechner", Path = System.IO.Path.Combine(sys, "calc.exe") });
            Current.Pinned.Add(new PinnedApp { Name = "Eingabeaufforderung", Path = System.IO.Path.Combine(sys, "cmd.exe") });
            Current.Save();
        }

        public void Save()
        {
            try { File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })); }
            catch { }
        }
    }

    internal static class AutoStart
    {
        const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string ValueName = "GlassBar";

        public static void Set(bool enabled)
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(KeyPath, true))
                {
                    if (key == null) return;
                    if (enabled) key.SetValue(ValueName, "\"" + Environment.ProcessPath + "\"");
                    else key.DeleteValue(ValueName, false);
                }
            }
            catch { }
        }
    }
}
