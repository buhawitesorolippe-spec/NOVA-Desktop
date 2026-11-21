using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Nova.Desktop;

internal static class ThemeManager
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeLegacy = 19;

    public static void Apply(string? theme)
    {
        var application = Application.Current;
        var isDark = !string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase);
        application.Resources["CurrentTheme"] = isDark ? "Dark" : "Light";

        var colors = isDark
            ? new Dictionary<string, Color>
            {
                ["BackgroundBrush"] = Color.FromRgb(10, 10, 10),
                ["SidebarBrush"] = Color.FromRgb(16, 16, 16),
                ["SurfaceBrush"] = Color.FromRgb(27, 27, 27),
                ["InputBrush"] = Color.FromRgb(23, 23, 23),
                ["RaisedBrush"] = Color.FromRgb(32, 32, 32),
                ["BorderBrush"] = Color.FromRgb(41, 41, 41),
                ["TextBrush"] = Color.FromRgb(236, 236, 236),
                ["MutedBrush"] = Color.FromRgb(146, 146, 146),
                ["AccentBrush"] = Color.FromRgb(140, 183, 255),
                ["AccentTextBrush"] = Color.FromRgb(16, 16, 16),
                ["HoverBrush"] = Color.FromRgb(36, 36, 36),
                ["SelectedBrush"] = Color.FromRgb(52, 68, 90),
                ["SelectedTextBrush"] = Color.FromRgb(255, 255, 255),
                ["AssistantBubbleBrush"] = Color.FromRgb(29, 29, 29),
                ["UserBubbleBrush"] = Color.FromRgb(37, 42, 50),
                ["BrandIconBrush"] = Color.FromRgb(23, 27, 33),
                ["BrandIconBorderBrush"] = Color.FromRgb(83, 104, 134),
                ["ComposerBorderBrush"] = Color.FromRgb(48, 48, 48),
                ["ControlGlyphBrush"] = Color.FromRgb(184, 184, 184),
                ["SubtleTextBrush"] = Color.FromRgb(182, 201, 228),
                ["ErrorBrush"] = Color.FromRgb(255, 143, 136)
            }
            : new Dictionary<string, Color>
            {
                ["BackgroundBrush"] = Color.FromRgb(247, 248, 250),
                ["SidebarBrush"] = Color.FromRgb(238, 240, 244),
                ["SurfaceBrush"] = Color.FromRgb(255, 255, 255),
                ["InputBrush"] = Color.FromRgb(255, 255, 255),
                ["RaisedBrush"] = Color.FromRgb(243, 245, 248),
                ["BorderBrush"] = Color.FromRgb(211, 216, 224),
                ["TextBrush"] = Color.FromRgb(26, 29, 35),
                ["MutedBrush"] = Color.FromRgb(95, 103, 115),
                ["AccentBrush"] = Color.FromRgb(47, 103, 219),
                ["AccentTextBrush"] = Color.FromRgb(255, 255, 255),
                ["HoverBrush"] = Color.FromRgb(231, 235, 241),
                ["SelectedBrush"] = Color.FromRgb(220, 231, 250),
                ["SelectedTextBrush"] = Color.FromRgb(23, 32, 51),
                ["AssistantBubbleBrush"] = Color.FromRgb(255, 255, 255),
                ["UserBubbleBrush"] = Color.FromRgb(228, 235, 247),
                ["BrandIconBrush"] = Color.FromRgb(229, 236, 247),
                ["BrandIconBorderBrush"] = Color.FromRgb(184, 197, 216),
                ["ComposerBorderBrush"] = Color.FromRgb(201, 205, 212),
                ["ControlGlyphBrush"] = Color.FromRgb(83, 91, 103),
                ["SubtleTextBrush"] = Color.FromRgb(53, 66, 83),
                ["ErrorBrush"] = Color.FromRgb(180, 35, 24)
            };

        foreach (var (key, color) in colors)
            application.Resources[key] = new SolidColorBrush(color);

        foreach (Window window in application.Windows)
        {
            window.SetResourceReference(Window.BackgroundProperty, "BackgroundBrush");
            window.SetResourceReference(Window.ForegroundProperty, "TextBrush");
            if (new WindowInteropHelper(window).Handle != IntPtr.Zero)
                ApplyTitleBarTheme(window, isDark);
        }
    }

    public static void TrackWindow(Window window)
    {
        window.SetResourceReference(Window.BackgroundProperty, "BackgroundBrush");
        window.SetResourceReference(Window.ForegroundProperty, "TextBrush");
        window.SourceInitialized += (_, _) =>
        {
            var isDark = string.Equals(
                Application.Current.Resources["CurrentTheme"] as string,
                "Dark",
                StringComparison.Ordinal);
            ApplyTitleBarTheme(window, isDark);
        };
    }

    private static void ApplyTitleBarTheme(Window window, bool isDark)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
            return;

        var enabled = isDark ? 1 : 0;
        var result = DwmSetWindowAttribute(
            handle, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int));
        if (result != 0)
            DwmSetWindowAttribute(
                handle, DwmwaUseImmersiveDarkModeLegacy, ref enabled, sizeof(int));
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr windowHandle, int attribute, ref int value, int valueSize);
}
