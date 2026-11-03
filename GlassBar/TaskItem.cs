using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Media;

namespace GlassBar
{
    public class TaskItem : INotifyPropertyChanged
    {
        public IntPtr Hwnd { get; set; }
        public string ExePath { get; set; }
        string _title; ImageSource _icon; bool _active; double _itemWidth = 46;

        public string Title { get => _title; set { if (_title != value) { _title = value; Raise(nameof(Title)); } } }
        public ImageSource Icon { get => _icon; set { _icon = value; Raise(nameof(Icon)); } }
        public bool IsActive { get => _active; set { if (_active != value) { _active = value; Raise(nameof(IsActive)); } } }
        public double ItemWidth { get => _itemWidth; set { if (Math.Abs(_itemWidth - value) > 0.5) { _itemWidth = value; Raise(nameof(ItemWidth)); } } }

        public event PropertyChangedEventHandler PropertyChanged;
        void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    internal static class WindowTracker
    {
        public static List<IntPtr> GetTaskWindows(IntPtr self)
        {
            var list = new List<IntPtr>();
            Native.EnumWindows((h, _) =>
            {
                if (h == self || !Native.IsWindowVisible(h)) return true;
                long ex = Native.GetWindowLongPtr(h, Native.GWL_EXSTYLE).ToInt64();
                bool tool = (ex & Native.WS_EX_TOOLWINDOW) != 0;
                bool app = (ex & Native.WS_EX_APPWINDOW) != 0;
                if (tool && !app) return true;
                if (Native.GetWindow(h, 4) != IntPtr.Zero && !app) return true; // hat Owner
                if (Native.GetWindowTextLength(h) == 0) return true;
                if (Native.DwmGetWindowAttribute(h, 14, out int cloaked, 4) == 0 && cloaked != 0) return true;
                string cls = Native.GetClass(h);
                if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd") return true;
                list.Add(h);
                return true;
            }, IntPtr.Zero);
            return list;
        }

        public static void Activate(IntPtr hwnd)
        {
            IntPtr fg = Native.GetAncestor(Native.GetForegroundWindow(), 3);
            if (Native.IsIconic(hwnd)) { Native.ShowWindow(hwnd, Native.SW_RESTORE); Native.SetForegroundWindow(hwnd); }
            else if (fg == hwnd) Native.ShowWindow(hwnd, Native.SW_MINIMIZE);
            else Native.SetForegroundWindow(hwnd);
        }
    }
}
