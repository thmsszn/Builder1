using System.Collections.Generic;
using System.Linq;

namespace GlassTaskbarStudio.Core
{
    public sealed class PresetInfo : ObservableObject
    {
        bool _isActive;
        public string Name { get; set; }
        public string Tagline { get; set; }
        public TaskbarSettings Settings { get; set; }
        /// <summary>True while the draft equals this preset exactly.</summary>
        public bool IsActive { get => _isActive; set => Set(ref _isActive, value); }
    }

    /// <summary>The six built-in looks. "Liquid Glass" is the default and equals new TaskbarSettings().</summary>
    public static class Presets
    {
        public static readonly IReadOnlyList<PresetInfo> All = new List<PresetInfo>
        {
            new PresetInfo
            {
                Name = "Liquid Glass", Tagline = "Glossy, translucent, floating - the signature look",
                Settings = new TaskbarSettings() // defaults
            },
            new PresetInfo
            {
                Name = "Windows 11 Pro", Tagline = "Clean, flush and minimal - Fluent, refined",
                Settings = new TaskbarSettings
                {
                    Material = TaskbarMaterial.Acrylic, TintHex = "#1C1C20", Transparency = 40, GlossIntensity = 10,
                    CornerRadius = 12, Floating = false, FloatingGap = 0, SideGap = 0, Compact = false,
                    ShadowIntensity = 0, BorderOpacity = 18, BorderThickness = 1
                }
            },
            new PresetInfo
            {
                Name = "Midnight", Tagline = "Deep dark glass with subtle reflections",
                Settings = new TaskbarSettings
                {
                    Material = TaskbarMaterial.Acrylic, TintHex = "#05060B", Transparency = 30, GlossIntensity = 35,
                    CornerRadius = 20, FloatingGap = 6, SideGap = 10, ShadowIntensity = 65, BorderOpacity = 32
                }
            },
            new PresetInfo
            {
                Name = "Frosted Glass", Tagline = "Soft blue frost with a strong blur",
                Settings = new TaskbarSettings
                {
                    Material = TaskbarMaterial.Blur, TintHex = "#6F95C4", Transparency = 62, GlossIntensity = 45,
                    CornerRadius = 22, FloatingGap = 8, SideGap = 14, ShadowIntensity = 35, BorderOpacity = 55
                }
            },
            new PresetInfo
            {
                Name = "Minimal", Tagline = "Completely clear - icons only",
                Settings = new TaskbarSettings
                {
                    Material = TaskbarMaterial.Clear, Transparency = 100, GlossIntensity = 0, CornerRadius = 0,
                    Floating = false, FloatingGap = 0, SideGap = 0, Compact = false, ShadowIntensity = 0, BorderOpacity = 0, BorderThickness = 0
                }
            },
            new PresetInfo
            {
                Name = "Neon", Tagline = "Dark glass with a cyan light edge and glow",
                Settings = new TaskbarSettings
                {
                    Material = TaskbarMaterial.Acrylic, TintHex = "#0B0620", Transparency = 42, GlossIntensity = 30,
                    CornerRadius = 20, FloatingGap = 8, SideGap = 12, ShadowIntensity = 80, ShadowHex = "#00E5FF",
                    BorderHex = "#00E5FF", BorderOpacity = 85, BorderThickness = 1.5
                }
            }
        };

        public static PresetInfo Default => All[0];

        public static PresetInfo Match(TaskbarSettings s) => All.FirstOrDefault(p => p.Settings.ValueEquals(s));
    }
}
