using System;

namespace KeepAwake.Core;

public enum LogKind
{
    AppLoaded,
    ReadyHint,
    Started,
    ExecutionStateOn,
    ExecutionStateRejected,
    PowerRequestOn,
    PowerRequestUnavailable,
    Released,
    Stopped,
    Restarted,
    Simulation,
    IntervalChanged,
    SimulateToggled,
    NudgeToggled,
    SimulateOffNote,
    NewSession,
    ThemeChanged,
    LanguageChanged,
    Closing,
    GuardError,
    LogCleared
}

public enum LogSeverity { Neutral, Success, Warning, Danger }

public sealed class LogEntry
{
    public DateTime Time { get; init; } = DateTime.Now;
    public LogKind Kind { get; init; }
    public int Number { get; init; }
    public int Second { get; init; }
    public bool Flag { get; init; }
    public bool Extra { get; init; }
    public string Text { get; init; }

    public string Clock => Time.ToString("HH:mm:ss");

    public LogSeverity Severity => Kind switch
    {
        LogKind.Started or LogKind.ExecutionStateOn or LogKind.PowerRequestOn or LogKind.Simulation => LogSeverity.Success,
        LogKind.ExecutionStateRejected or LogKind.SimulateOffNote or LogKind.PowerRequestUnavailable => LogSeverity.Warning,
        LogKind.GuardError or LogKind.Stopped => LogSeverity.Danger,
        _ => LogSeverity.Neutral
    };
}