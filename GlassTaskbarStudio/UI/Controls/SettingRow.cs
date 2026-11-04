using System.Windows;
using System.Windows.Controls;

namespace GlassTaskbarStudio.UI.Controls
{
    /// <summary>A labelled settings row: title + description + technique badge on the left, the editor control on the right.</summary>
    public class SettingRow : ContentControl
    {
        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(SettingRow));
        public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(string), typeof(SettingRow));
        /// <summary>How the setting reaches Windows: NATIVE, REGION, OVERLAY, REGISTRY or APP.</summary>
        public static readonly DependencyProperty BadgeProperty = DependencyProperty.Register(nameof(Badge), typeof(string), typeof(SettingRow));
        public static readonly DependencyProperty NoteProperty = DependencyProperty.Register(nameof(Note), typeof(string), typeof(SettingRow));

        public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
        public string Description { get => (string)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
        public string Badge { get => (string)GetValue(BadgeProperty); set => SetValue(BadgeProperty, value); }
        public string Note { get => (string)GetValue(NoteProperty); set => SetValue(NoteProperty, value); }
    }
}
