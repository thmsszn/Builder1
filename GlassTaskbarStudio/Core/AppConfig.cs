using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GlassTaskbarStudio.Core
{
    /// <summary>Original Explorer registry values, captured once before we touch them, so "Restore" is exact.</summary>
    public sealed class ExplorerBackup
    {
        public bool Captured { get; set; }
        public int? TaskbarAl { get; set; }
        public int? TaskbarSi { get; set; }
    }

    /// <summary>Everything persisted to %AppData%\GlassTaskbarStudio\settings.json.</summary>
    public sealed class AppConfig
    {
        public int Version { get; set; } = 1;
        public TaskbarSettings Applied { get; set; } = Presets.Default.Settings.Clone();
        public bool EffectsEnabled { get; set; } = true;
        public bool StartWithWindows { get; set; } = true;
        public bool HideInFullscreen { get; set; } = true;
        public bool UiAnimations { get; set; } = true;
        public bool LiveApply { get; set; }
        public int RefreshMs { get; set; } = 1000;
        public bool FirstRunDone { get; set; }
        public ExplorerBackup Backup { get; set; } = new ExplorerBackup();

        static readonly JsonSerializerOptions Json = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GlassTaskbarStudio");
        static string FilePath => Path.Combine(Folder, "settings.json");

        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(FilePath), Json) ?? new AppConfig();
            }
            catch { /* corrupt file -> start fresh, never crash on startup */ }
            return new AppConfig();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(this, Json));
                File.Copy(tmp, FilePath, true);
                File.Delete(tmp);
            }
            catch { }
        }

        public static JsonSerializerOptions JsonOptions => Json;
    }
}
