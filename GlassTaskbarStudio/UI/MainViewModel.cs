using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using GlassTaskbarStudio.Core;
using Microsoft.Win32;

namespace GlassTaskbarStudio.UI
{
    /// <summary>
    /// Single view model for the whole window. The "draft" is what the sliders edit (and the preview shows);
    /// Apply copies it to the engine, the registry tweaks and settings.json.
    /// </summary>
    public sealed class MainViewModel : ObservableObject
    {
        readonly DispatcherTimer _liveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(70) };
        readonly DispatcherTimer _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.5) };
        TaskbarSettings _applied;
        bool _isDirty, _restartNeeded, _effects;
        string _activeTheme, _engineStatus, _toast;

        public AppConfig Config { get; }
        public TaskbarEngine Engine { get; }
        public TaskbarSettings Draft { get; } = new TaskbarSettings();
        public System.Collections.Generic.IReadOnlyList<PresetInfo> Presets => Core.Presets.All;

        // capabilities (bound by the UI to disable what Windows cannot do)
        public bool IsWindows11 => Capabilities.IsWindows11;
        public bool SupportsAlignment => Capabilities.SupportsAlignment;
        public bool SupportsSizeTweak => Capabilities.SupportsSizeTweak;
        public bool SupportsCompact => Capabilities.SupportsCompact;
        public bool SupportsAcrylic => Capabilities.SupportsAcrylic;
        public string OsName => Capabilities.OsName;

        public ICommand ApplyCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand RestoreCommand { get; }
        public ICommand SelectPresetCommand { get; }
        public ICommand RestartExplorerCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand ImportCommand { get; }
        public ICommand OpenFolderCommand { get; }

        public MainViewModel(AppConfig config, TaskbarEngine engine)
        {
            Config = config; Engine = engine;
            _applied = config.Applied.Clone();
            Draft.CopyFrom(_applied);
            _effects = config.EffectsEnabled;
            _engineStatus = engine.Status;
            engine.StatusChanged += s => EngineStatus = s;
            Draft.PropertyChanged += (s, e) => OnDraftChanged();
            _liveTimer.Tick += (s, e) => { _liveTimer.Stop(); if (LiveApply && _effects) Engine.Apply(Draft, false); };
            _toastTimer.Tick += (s, e) => { _toastTimer.Stop(); Toast = null; };
            RefreshState();

            ApplyCommand = new RelayCommand(Apply);
            ResetCommand = new RelayCommand(ResetToDefault);
            RestoreCommand = new RelayCommand(RestoreWindowsTaskbar);
            SelectPresetCommand = new RelayCommand(p => { if (p is PresetInfo pi) SelectPreset(pi); });
            RestartExplorerCommand = new RelayCommand(RestartExplorer);
            ExportCommand = new RelayCommand(Export);
            ImportCommand = new RelayCommand(Import);
            OpenFolderCommand = new RelayCommand(() => { Directory.CreateDirectory(AppConfig.Folder); Process.Start(new ProcessStartInfo(AppConfig.Folder) { UseShellExecute = true }); });
        }

        // ---------------- state ----------------

        public bool IsDirty { get => _isDirty; private set => Set(ref _isDirty, value); }
        public string ActiveTheme { get => _activeTheme; private set => Set(ref _activeTheme, value); }
        public string EngineStatus { get => _engineStatus; private set => Set(ref _engineStatus, value); }
        public string Toast { get => _toast; private set => Set(ref _toast, value); }
        public bool RestartNeeded { get => _restartNeeded; private set => Set(ref _restartNeeded, value); }

        /// <summary>Master switch: when off, the original Windows taskbar is shown untouched.</summary>
        public bool EffectsEnabled
        {
            get => _effects;
            set
            {
                if (!Set(ref _effects, value)) return;
                Config.EffectsEnabled = value;
                if (value) { Engine.Apply(_applied, false); Engine.Enable(); } else Engine.Disable();
                Config.Save();
            }
        }

        public bool LiveApply { get => Config.LiveApply; set { Config.LiveApply = value; OnPropertyChanged(); Config.Save(); if (value) OnDraftChanged(); } }
        public bool UiAnimations { get => Config.UiAnimations; set { Config.UiAnimations = value; OnPropertyChanged(); Config.Save(); } }
        public bool StartWithWindows { get => Config.StartWithWindows; set { Config.StartWithWindows = value; SystemTweaks.SetAutoStart(value); OnPropertyChanged(); Config.Save(); } }
        public bool HideInFullscreen { get => Config.HideInFullscreen; set { Config.HideInFullscreen = value; Engine.HideInFullscreen = value; OnPropertyChanged(); Config.Save(); } }
        public double RefreshMs { get => Config.RefreshMs; set { Config.RefreshMs = (int)value; Engine.RefreshMs = (int)value; OnPropertyChanged(); Config.Save(); } }

        void OnDraftChanged()
        {
            RefreshState();
            if (LiveApply && _effects) { _liveTimer.Stop(); _liveTimer.Start(); }
        }

        void RefreshState()
        {
            IsDirty = !Draft.ValueEquals(_applied);
            var match = Core.Presets.Match(Draft);
            ActiveTheme = match?.Name ?? "Custom";
            foreach (var p in Presets) p.IsActive = ReferenceEquals(p, match);
        }

        void ShowToast(string text)
        {
            Toast = text; _toastTimer.Stop(); _toastTimer.Start();
        }

        // ---------------- actions ----------------

        public void Apply()
        {
            _applied = Draft.Clone();
            Config.Applied = _applied.Clone();
            RestartNeeded = SystemTweaks.ApplyExplorerOptions(_applied, Config);
            Engine.Apply(_applied, _applied.AnimateTransitions);
            bool wasOff = !_effects;
            _effects = true; OnPropertyChanged(nameof(EffectsEnabled)); Config.EffectsEnabled = true;
            if (wasOff || !Engine.IsActive) Engine.Enable();
            Config.FirstRunDone = true;
            Config.Save();
            RefreshState();
            ShowToast(RestartNeeded ? "Applied - restart Explorer to change the taskbar size" : "Applied to your taskbar");
        }

        public void SelectPreset(PresetInfo preset)
        {
            // presets define the LOOK; system-level choices (alignment, size, monitors, motion) stay as the user set them
            var keep = (Draft.Alignment, Draft.Height, Draft.AllMonitors, Draft.AnimateTransitions, Draft.TransitionMs);
            Draft.CopyFrom(preset.Settings);
            (Draft.Alignment, Draft.Height, Draft.AllMonitors, Draft.AnimateTransitions, Draft.TransitionMs) = keep;
            Apply();
            ShowToast($"Preset \"{preset.Name}\" applied");
        }

        public void ResetToDefault()
        {
            Draft.CopyFrom(Core.Presets.Default.Settings);
            Apply();
            ShowToast("Reset to the default Liquid Glass look");
        }

        public void RestoreWindowsTaskbar()
        {
            var r = MessageBox.Show("Restore the original Windows taskbar?\n\nAll effects are removed and your original icon alignment and size are put back. " +
                                    "Your saved look stays available - press Apply to bring it back.",
                                    "Restore Windows taskbar", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (r != MessageBoxResult.OK) return;
            EffectsEnabled = false;
            SystemTweaks.RestoreExplorerOptions(Config);
            RestartNeeded = Capabilities.SupportsSizeTweak;
            ShowToast("Original Windows taskbar restored");
        }

        void RestartExplorer()
        {
            var r = MessageBox.Show("Restart Windows Explorer now?\n\nThe taskbar and desktop disappear for a moment. Unsaved work in other apps is not affected.",
                                    "Restart Explorer", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (r != MessageBoxResult.OK) return;
            SystemTweaks.RestartExplorer();
            RestartNeeded = false;
        }

        void Export()
        {
            var dlg = new SaveFileDialog { Filter = "Taskbar look (*.json)|*.json", FileName = "my-taskbar-look.json" };
            if (dlg.ShowDialog() != true) return;
            File.WriteAllText(dlg.FileName, JsonSerializer.Serialize(Draft, AppConfig.JsonOptions));
            ShowToast("Look exported");
        }

        void Import()
        {
            var dlg = new OpenFileDialog { Filter = "Taskbar look (*.json)|*.json" };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var s = JsonSerializer.Deserialize<TaskbarSettings>(File.ReadAllText(dlg.FileName), AppConfig.JsonOptions);
                if (s == null) throw new InvalidDataException();
                Draft.CopyFrom(s);
                ShowToast("Look imported - press Apply to use it");
            }
            catch { MessageBox.Show("That file is not a valid taskbar look.", "Import", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }
    }
}
