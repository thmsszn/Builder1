using System;

namespace GlassTaskbarStudio.Core
{
    /// <summary>What this Windows build can actually do. UI disables features that would only be simulated.</summary>
    public static class Capabilities
    {
        public static readonly int Build = Environment.OSVersion.Version.Build;
        public static bool IsWindows11 => Build >= 22000;
        public static bool SupportsAcrylic => Build >= 17134;
        /// <summary>Registry-driven icon alignment (TaskbarAl) exists only on Windows 11.</summary>
        public static bool SupportsAlignment => IsWindows11;
        /// <summary>Registry-driven taskbar size (TaskbarSi) is a Windows 11 feature.</summary>
        public static bool SupportsSizeTweak => IsWindows11;
        /// <summary>Compact "island" mode needs the centered Windows 11 icon layout.</summary>
        public static bool SupportsCompact => IsWindows11;

        public static string OsName => IsWindows11 ? $"Windows 11 (build {Build})" : $"Windows 10 (build {Build})";
    }
}
