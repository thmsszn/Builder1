using System;
using System.Windows;
using Forms = System.Windows.Forms;
using GlassTaskbarStudio.Core;

namespace GlassTaskbarStudio.UI
{
    /// <summary>Tray icon: open the window, switch looks, toggle effects, restore, exit.</summary>
    internal sealed class TrayService : IDisposable
    {
        readonly Forms.NotifyIcon _icon;
        readonly Forms.ToolStripMenuItem _effects;

        public TrayService(MainViewModel vm, Action showWindow, Action exit)
        {
            var menu = new Forms.ContextMenuStrip { ShowImageMargin = false };
            menu.Items.Add("Open Glass Taskbar Studio", null, (s, e) => showWindow());
            menu.Items.Add(new Forms.ToolStripSeparator());

            var looks = new Forms.ToolStripMenuItem("Looks");
            foreach (var p in vm.Presets)
            {
                var preset = p;
                looks.DropDownItems.Add(preset.Name, null, (s, e) => vm.SelectPreset(preset));
            }
            menu.Items.Add(looks);

            _effects = new Forms.ToolStripMenuItem("Taskbar effects") { CheckOnClick = false };
            _effects.Click += (s, e) => vm.EffectsEnabled = !vm.EffectsEnabled;
            menu.Items.Add(_effects);
            menu.Opening += (s, e) => _effects.Checked = vm.EffectsEnabled;

            menu.Items.Add("Restore Windows taskbar", null, (s, e) => vm.RestoreWindowsTaskbar());
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("Exit", null, (s, e) => exit());

            _icon = new Forms.NotifyIcon { Icon = IconFactory.CreateTrayIcon(), Text = "Glass Taskbar Studio", Visible = true, ContextMenuStrip = menu };
            _icon.MouseClick += (s, e) => { if (e.Button == Forms.MouseButtons.Left) showWindow(); };
        }

        public void Dispose() { _icon.Visible = false; _icon.Dispose(); }
    }
}
