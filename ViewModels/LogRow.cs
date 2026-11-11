using System.ComponentModel;
using KeepAwake.Core;
using KeepAwake.Services;

namespace KeepAwake.ViewModels;

public sealed class LogRow : INotifyPropertyChanged
{
    public LogRow(LogEntry entry) => Entry = entry;

    public LogEntry Entry { get; }
    public string Clock => Entry.Clock;
    public string Tone => Entry.Severity.ToString();
    public string Text => Render(Entry);

    public event PropertyChangedEventHandler PropertyChanged;

    public void Refresh() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));

    private static string Render(LogEntry e)
    {
        var loc = Loc.Instance;
        return e.Kind switch
        {
            LogKind.AppLoaded => loc["Log.AppLoaded"],
            LogKind.ReadyHint => loc["Log.ReadyHint"],
            LogKind.Started => loc.F("Log.Started", e.Number,
                loc[e.Flag ? "Common.On" : "Common.Off"],
                loc[e.Extra ? "Common.On" : "Common.Off"]),
            LogKind.ExecutionStateOn => loc["Log.ExecutionStateOn"],
            LogKind.ExecutionStateRejected => loc.F("Log.ExecutionStateRejected", e.Number),
            LogKind.PowerRequestOn => loc["Log.PowerRequestOn"],
            LogKind.PowerRequestUnavailable => loc["Log.PowerRequestUnavailable"],
            LogKind.Released => loc["Log.Released"],
            LogKind.Stopped => loc["Log.Stopped"],
            LogKind.Restarted => loc["Log.Restarted"],
            LogKind.Simulation => loc.F("Log.Simulation", e.Number, e.Second,
                e.Flag ? "" : loc["Log.SimulationFailed"],
                e.Extra ? loc["Log.SimulationMouse"] : ""),
            LogKind.IntervalChanged => loc.F("Log.IntervalChanged", e.Number),
            LogKind.SimulateToggled => loc[e.Flag ? "Log.SimulateOn" : "Log.SimulateOff"],
            LogKind.NudgeToggled => loc[e.Flag ? "Log.NudgeOn" : "Log.NudgeOff"],
            LogKind.SimulateOffNote => loc["Log.SimulateOffNote"],
            LogKind.NewSession => loc["Log.NewSession"],
            LogKind.ThemeChanged => loc.F("Log.ThemeChanged", e.Text),
            LogKind.LanguageChanged => loc.F("Log.LanguageChanged", e.Text),
            LogKind.Closing => loc["Log.Closing"],
            LogKind.GuardError => loc.F("Log.GuardError", e.Text),
            LogKind.LogCleared => loc["Log.LogCleared"],
            _ => e.Text
        };
    }
}