using System;
using System.Windows;
using KeepAwake.Services;
using Microsoft.Win32;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace KeepAwake;

public partial class App : Application
{
    private const int PaletteIndex = 2;

    public static AppConfig Config { get; private set; } = new();
    public static string ThemePreference { get; private set; } = "System";
    public static bool IsDark { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        Config = AppConfig.Load();
        Loc.Instance.Set(Config.Language);
        ApplyTheme(Config.Theme);

        base.OnStartup(e);

        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        new MainWindow().Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        Config.Save();
        base.OnExit(e);
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category != UserPreferenceCategory.General || ThemePreference != "System") return;
        Dispatcher.BeginInvoke(new Action(() => ApplyTheme("System")));
    }

    public static void ApplyTheme(string mode)
    {
        ThemePreference = mode is "Light" or "Dark" ? mode : "System";
        IsDark = ThemePreference == "Dark" || (ThemePreference == "System" && SystemUsesDark());

        var palette = new ResourceDictionary
        {
            Source = new Uri(IsDark ? "Themes/Palette.Dark.xaml" : "Themes/Palette.Light.xaml", UriKind.Relative)
        };

        var dictionaries = Current.Resources.MergedDictionaries;
        if (dictionaries.Count > PaletteIndex) dictionaries[PaletteIndex] = palette;
        else dictionaries.Add(palette);

        ApplicationThemeManager.Apply(
            IsDark ? ApplicationTheme.Dark : ApplicationTheme.Light,
            WindowBackdropType.None,
            false);

        Config.Theme = ThemePreference;
        Config.Save();
    }

    private static bool SystemUsesDark()
    {
        try
        {
            var value = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme", 1);
            return value is int flag && flag == 0;
        }
        catch
        {
            return false;
        }
    }
}