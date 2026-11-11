using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using KeepAwake.Services;
using KeepAwake.ViewModels;
using Wpf.Ui.Controls;

namespace KeepAwake;

public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel _vm;
    private Storyboard _pulse;

    public MainWindow()
    {
        InitializeComponent();

        try { Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Assets/AppIcon.ico")); }
        catch { }

        _vm = new MainViewModel();
        DataContext = _vm;

        BuildPulse();
        _vm.PropertyChanged += OnViewModelChanged;
        _vm.Log.CollectionChanged += OnLogChanged;

        Loaded += (_, _) => { UpdatePulse(); UpdateMenuChecks(); };
        Closing += OnClosing;
    }

    private void OnViewModelChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsRunning)) UpdatePulse();
    }

    private void OnLogChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add) return;
        Dispatcher.BeginInvoke(new Action(() => LogScroll.ScrollToEnd()), DispatcherPriority.Loaded);
    }

    // ---- pulse ----------------------------------------------------------

    private void BuildPulse()
    {
        _pulse = new Storyboard();
        AddRing("PulseOuter", "PulseOuterScale", TimeSpan.Zero);
        AddRing("PulseInner", "PulseInnerScale", TimeSpan.FromSeconds(1.2));
    }

    private void AddRing(string ringName, string scaleName, TimeSpan delay)
    {
        var duration = new Duration(TimeSpan.FromSeconds(2.4));
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        var opacity = new DoubleAnimation(0.5, 0, duration)
        {
            BeginTime = delay,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = ease
        };
        Storyboard.SetTargetName(opacity, ringName);
        Storyboard.SetTargetProperty(opacity, new PropertyPath(OpacityProperty));
        _pulse.Children.Add(opacity);

        foreach (var property in new[] { ScaleTransform.ScaleXProperty, ScaleTransform.ScaleYProperty })
        {
            var grow = new DoubleAnimation(1, 2.5, duration)
            {
                BeginTime = delay,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = ease
            };
            Storyboard.SetTargetName(grow, scaleName);
            Storyboard.SetTargetProperty(grow, new PropertyPath(property));
            _pulse.Children.Add(grow);
        }
    }

    private void UpdatePulse()
    {
        if (_pulse == null) return;

        bool running = _vm.IsRunning;
        PulseCore.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, running ? "Accent" : "TextMuted");

        if (running && SystemParameters.ClientAreaAnimation)
        {
            _pulse.Begin(this, true);
        }
        else
        {
            _pulse.Stop(this);
            PulseOuter.BeginAnimation(OpacityProperty, null);
            PulseInner.BeginAnimation(OpacityProperty, null);
            PulseOuter.Opacity = 0;
            PulseInner.Opacity = 0;
        }
    }

    // ---- session --------------------------------------------------------

    private void OnStart(object sender, RoutedEventArgs e) => _vm.Start();
    private void OnStop(object sender, RoutedEventArgs e) => _vm.Stop();
    private void OnRestart(object sender, RoutedEventArgs e) => _vm.Restart();
    private void OnClearLog(object sender, RoutedEventArgs e) => _vm.ClearLog();

    private void OnNewSession(object sender, RoutedEventArgs e)
    {
        _vm.NewSession();
        UpdatePulse();
    }

    private void OnCloseApplication(object sender, RoutedEventArgs e) => Close();

    private void OnClosing(object sender, CancelEventArgs e)
    {
        _vm.LogClosing();
        _vm.Shutdown();
        _pulse?.Stop(this);
    }

    // ---- theme ----------------------------------------------------------

    private void OnThemeMenu(object sender, RoutedEventArgs e)
    {
        UpdateMenuChecks();
        OpenMenu((System.Windows.Controls.Button)sender);
    }

    private void OnThemeLight(object sender, RoutedEventArgs e) => SetTheme("Light", "Theme.Light");
    private void OnThemeDark(object sender, RoutedEventArgs e) => SetTheme("Dark", "Theme.Dark");
    private void OnThemeSystem(object sender, RoutedEventArgs e) => SetTheme("System", "Theme.System");

    private void SetTheme(string mode, string labelKey)
    {
        App.ApplyTheme(mode);
        _vm.LogTheme(Loc.Instance[labelKey]);
        UpdateMenuChecks();
        UpdatePulse();
    }

    // ---- language -------------------------------------------------------

    private void OnLanguageMenu(object sender, RoutedEventArgs e)
    {
        UpdateMenuChecks();
        OpenMenu((System.Windows.Controls.Button)sender);
    }

    private void OnLanguageEnglish(object sender, RoutedEventArgs e) => SetLanguage("en");
    private void OnLanguageIndonesian(object sender, RoutedEventArgs e) => SetLanguage("id");

    private void SetLanguage(string code)
    {
        if (Loc.Instance.Current == code) return;
        Loc.Instance.Set(code);
        _vm.LogLanguage(Loc.Instance.LanguageName);
        UpdateMenuChecks();
    }

    private void UpdateMenuChecks()
    {
        ThemeLightItem.IsChecked = App.ThemePreference == "Light";
        ThemeDarkItem.IsChecked = App.ThemePreference == "Dark";
        ThemeSystemItem.IsChecked = App.ThemePreference == "System";
        LangEnItem.IsChecked = Loc.Instance.Current == "en";
        LangIdItem.IsChecked = Loc.Instance.Current == "id";
    }

    private static void OpenMenu(System.Windows.Controls.Button button)
    {
        if (button.ContextMenu == null) return;
        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.Placement = PlacementMode.Bottom;
        button.ContextMenu.IsOpen = true;
    }

    // ---- about ----------------------------------------------------------

    private void OnAbout(object sender, RoutedEventArgs e)
    {
        var about = new AboutWindow { Owner = this };
        about.ShowDialog();
    }
}