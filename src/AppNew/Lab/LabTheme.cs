using Microsoft.FluentUI.AspNetCore.Components;

namespace DotNetLab.Lab;

public static class LabTheme
{
    public const string StorageKey = "netlab-theme";
    public const string BrandColor = "#6e4a9e";
    public const string DarkMonaco = "netlab-dark";
    public const string LightMonaco = "netlab-light";

    public static ThemeSettings CreateSettings(ThemeMode mode)
        => new(BrandColor, 0.05, 0.08, mode, true);

    public static ThemeMode ToMode(string preference)
        => preference switch
        {
            "light" => ThemeMode.Light,
            "dark" => ThemeMode.Dark,
            _ => ThemeMode.System
        };

    public static string NormalizePreference(string? preference)
        => preference is "light" or "dark" or "system" ? preference : "dark";

    public static string MonacoThemeName(bool dark)
        => dark ? DarkMonaco : LightMonaco;
}
