using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using KeepAwake.Core;
using KeepAwake.Services;

namespace KeepAwake.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private const int LogCapacity = 400;

    private readonly AwakeGuard _guard = new();
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool _syncingInterval;
    private bool _dirty;

    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _uptimeText = "00:00:00";
    [ObservableProperty] private string _startedText = "";
    [ObservableProperty] private string _idleNowText = "0 s";
    [ObservableProperty] private int _wakeSignals;
    [ObservableProperty] private int _eventCount;
    [ObservableProperty] private bool _displayHeld;
    [ObservableProperty] private bool _systemHeld;
    [ObservableProperty] private bool _powerHeld;
    [ObservableProperty] private string _intervalText = "60";
    [ObservableProperty] private string _selectedPreset = "60";
    [ObservableProperty] private bool _simulateInput = true;
    [ObservableProperty] private bool _nudgeMouse;

    public ObservableCollection<LogRow> Log { get; } = new();
    public ObservableCollection<string> Presets { get; } = new();

    private static string CustomLabel => Loc.Instance["Settings.Custom"];

    private void FillPresets()
    {
        Presets.Clear();
        foreach (var value in new[] { "15", "30", "45", "60", "90", "120", "180", "300", "600" }) Presets.Add(value);
        Presets.Add(CustomLabel);
    }

    public MainViewModel()
    {
        var config = App.Config;
        _syncingInterval = true;
        FillPresets();
        IntervalText = config.IntervalSeconds.ToString(CultureInfo.InvariantCulture);
        SelectedPreset = Presets.Contains(IntervalText) ? IntervalText : CustomLabel;
        _syncingInterval = false;

        SimulateInput = config.SimulateInput;
        NudgeMouse = config.NudgeMouse;

        _guard.IntervalSeconds = config.IntervalSeconds;
        _guard.SimulateInput = config.SimulateInput;
        _guard.NudgeMouse = config.NudgeMouse;

        _guard.Logged += OnGuardLog;
        _guard.StateChanged += () => App.Current.Dispatcher.BeginInvoke(new Action(SyncState));
        _clock.Tick += (_, _) => Tick();

        Append(new LogEntry { Kind = LogKind.AppLoaded });
        Append(new LogEntry { Kind = LogKind.ReadyHint });

        SyncState();
        _dirty = true;
        _clock.Start();
    }

    public string StateLabel => Loc.Instance[IsRunning ? "Status.Active" : "Status.Idle"];
    public string StateHint => Loc.Instance[IsRunning ? "Status.ActiveHint" : "Status.IdleHint"];
    public string ControlStatus => Loc.Instance[IsRunning ? "Control.Running" : "Control.Ready"];
    public string DisplayState => Loc.Instance[DisplayHeld ? "Bar.Held" : "Bar.Free"];
    public string SystemState => Loc.Instance[SystemHeld ? "Bar.Held" : "Bar.Free"];
    public string PowerState => !IsRunning
        ? Loc.Instance["Bar.Free"]
        : Loc.Instance[PowerHeld ? "Bar.Held" : "Bar.Unavailable"];
    public string EventCountText => EventCount == 1 ? Loc.Instance["Log.CountOne"] : Loc.Instance.F("Log.Count", EventCount);
    public bool HasLog => Log.Count > 0;
    public bool CanStart => !IsRunning;
    public bool CanStop => IsRunning;

    public void Start()
    {
        if (_guard.IsRunning) return;

        ApplySettingsToGuard();
        Append(new LogEntry
        {
            Kind = LogKind.Started,
            Number = _guard.IntervalSeconds,
            Flag = _guard.SimulateInput,
            Extra = _guard.NudgeMouse
        });
        if (!_guard.SimulateInput) Append(new LogEntry { Kind = LogKind.SimulateOffNote });

        _guard.Start();
        SyncState();
    }

    public void Stop()
    {
        if (!_guard.IsRunning) return;
        Append(new LogEntry { Kind = LogKind.Stopped });
        _guard.Stop();
        SyncState();
    }

    public void Restart()
    {
        if (!_guard.IsRunning)
        {
            Start();
            return;
        }
        Append(new LogEntry { Kind = LogKind.Restarted });
        _guard.Stop();
        Start();
    }

    public void NewSession()
    {
        if (_guard.IsRunning) _guard.Stop();

        Log.Clear();
        _syncingInterval = true;
        IntervalText = "60";
        SelectedPreset = "60";
        _syncingInterval = false;
        SimulateInput = true;
        NudgeMouse = false;
        ApplySettingsToGuard();

        Append(new LogEntry { Kind = LogKind.NewSession });
        Append(new LogEntry { Kind = LogKind.ReadyHint });
        SyncState();
        Persist();
    }

    public void ClearLog()
    {
        Log.Clear();
        Append(new LogEntry { Kind = LogKind.LogCleared });
        SyncState();
    }

    public void LogTheme(string name) => Append(new LogEntry { Kind = LogKind.ThemeChanged, Text = name });

    public void LogLanguage(string name)
    {
        Append(new LogEntry { Kind = LogKind.LanguageChanged, Text = name });
        RefreshLanguage();
    }

    public void LogClosing() => Append(new LogEntry { Kind = LogKind.Closing });

    public void RefreshLanguage()
    {
        _syncingInterval = true;
        bool wasCustom = SelectedPreset == null || !Presets.Contains(IntervalText);
        FillPresets();
        SelectedPreset = wasCustom ? CustomLabel : IntervalText;
        _syncingInterval = false;
        foreach (var row in Log) row.Refresh();
        OnPropertyChanged(nameof(StateLabel));
        OnPropertyChanged(nameof(StateHint));
        OnPropertyChanged(nameof(ControlStatus));
        OnPropertyChanged(nameof(DisplayState));
        OnPropertyChanged(nameof(SystemState));
        OnPropertyChanged(nameof(PowerState));
        OnPropertyChanged(nameof(EventCountText));
        Tick();
    }

    partial void OnSelectedPresetChanged(string value)
    {
        if (_syncingInterval || value == null || value == CustomLabel) return;
        _syncingInterval = true;
        IntervalText = value;
        _syncingInterval = false;
        CommitInterval(int.Parse(value, CultureInfo.InvariantCulture));
    }

    partial void OnIntervalTextChanged(string value)
    {
        if (_syncingInterval) return;
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seconds)) return;

        seconds = Math.Clamp(seconds, 10, 600);
        _syncingInterval = true;
        string canonical = seconds.ToString(CultureInfo.InvariantCulture);
        if (canonical != value) IntervalText = canonical;
        SelectedPreset = Presets.Contains(canonical) ? canonical : CustomLabel;
        _syncingInterval = false;
        CommitInterval(seconds);
    }

    partial void OnSimulateInputChanged(bool value)
    {
        _guard.SimulateInput = value;
        if (!value) _guard.NudgeMouse = false;
        else _guard.NudgeMouse = NudgeMouse;

        if (_dirty) Append(new LogEntry { Kind = LogKind.SimulateToggled, Flag = value });
        if (!value && IsRunning) Append(new LogEntry { Kind = LogKind.SimulateOffNote });
        Persist();
    }

    partial void OnNudgeMouseChanged(bool value)
    {
        _guard.NudgeMouse = SimulateInput && value;
        if (_dirty) Append(new LogEntry { Kind = LogKind.NudgeToggled, Flag = value });
        Persist();
    }

    private void CommitInterval(int seconds)
    {
        _guard.IntervalSeconds = seconds;
        if (_dirty) Append(new LogEntry { Kind = LogKind.IntervalChanged, Number = seconds });
        Persist();
    }

    private void ApplySettingsToGuard()
    {
        if (int.TryParse(IntervalText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seconds))
            _guard.IntervalSeconds = Math.Clamp(seconds, 10, 600);
        _guard.SimulateInput = SimulateInput;
        _guard.NudgeMouse = SimulateInput && NudgeMouse;
        _dirty = true;
    }

    private void Persist()
    {
        App.Config.IntervalSeconds = _guard.IntervalSeconds;
        App.Config.SimulateInput = SimulateInput;
        App.Config.NudgeMouse = NudgeMouse;
        App.Config.Language = Loc.Instance.Current;
        App.Config.Save();
    }

    private void OnGuardLog(LogEntry entry) =>
        App.Current.Dispatcher.BeginInvoke(new Action(() => Append(entry)));

    private void Append(LogEntry entry)
    {
        while (Log.Count >= LogCapacity) Log.RemoveAt(0);
        Log.Add(new LogRow(entry));
        EventCount = Log.Count;
        OnPropertyChanged(nameof(HasLog));
        OnPropertyChanged(nameof(EventCountText));
    }

    private void SyncState()
    {
        IsRunning = _guard.IsRunning;
        DisplayHeld = _guard.IsRunning && _guard.ExecutionStateHeld;
        SystemHeld = _guard.IsRunning && _guard.ExecutionStateHeld;
        PowerHeld = _guard.IsRunning && _guard.PowerRequestHeld;
        WakeSignals = _guard.Simulations;
        StartedText = IsRunning ? Loc.Instance.F("Status.Since", _guard.StartedAt.ToString("HH:mm:ss")) : "";

        OnPropertyChanged(nameof(StateLabel));
        OnPropertyChanged(nameof(StateHint));
        OnPropertyChanged(nameof(ControlStatus));
        OnPropertyChanged(nameof(DisplayState));
        OnPropertyChanged(nameof(SystemState));
        OnPropertyChanged(nameof(PowerState));
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanStop));
        Tick();
    }

    private void Tick()
    {
        var uptime = _guard.Uptime;
        UptimeText = $"{(int)uptime.TotalHours:00}:{uptime.Minutes:00}:{uptime.Seconds:00}";
        IdleNowText = (Native.IdleMilliseconds() / 1000).ToString(CultureInfo.InvariantCulture) + " s";
        if (IsRunning)
        {
            WakeSignals = _guard.Simulations;
            PowerHeld = _guard.PowerRequestHeld;
            OnPropertyChanged(nameof(PowerState));
        }
    }

    public void Shutdown()
    {
        _clock.Stop();
        _guard.Dispose();
    }

    public void Dispose() => Shutdown();
}