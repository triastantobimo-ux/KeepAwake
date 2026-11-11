using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace KeepAwake.Core;

internal sealed class AwakeGuard : IDisposable
{
    private const uint HoldFlags = Native.ES_CONTINUOUS | Native.ES_SYSTEM_REQUIRED | Native.ES_DISPLAY_REQUIRED;

    private readonly PowerRequest _power = new();
    private Thread _worker;
    private ManualResetEvent _stopSignal;
    private int _intervalSeconds = 60;
    private volatile bool _simulateInput = true;
    private volatile bool _nudgeMouse;
    private int _simulations;
    private int _executionStateOk;

    public event Action<LogEntry> Logged;
    public event Action StateChanged;

    public bool IsRunning => _worker != null;
    public DateTime StartedAt { get; private set; }
    public int Simulations => Volatile.Read(ref _simulations);
    public bool ExecutionStateHeld => Volatile.Read(ref _executionStateOk) == 1;
    public bool PowerRequestHeld { get; private set; }

    public int IntervalSeconds
    {
        get => Volatile.Read(ref _intervalSeconds);
        set { if (value > 0) Volatile.Write(ref _intervalSeconds, value); }
    }

    public bool SimulateInput
    {
        get => _simulateInput;
        set => _simulateInput = value;
    }

    public bool NudgeMouse
    {
        get => _nudgeMouse;
        set => _nudgeMouse = value;
    }

    public TimeSpan Uptime => IsRunning ? DateTime.Now - StartedAt : TimeSpan.Zero;

    public void Start()
    {
        if (_worker != null) return;

        var stop = new ManualResetEvent(false);
        _stopSignal = stop;
        Volatile.Write(ref _simulations, 0);
        Volatile.Write(ref _executionStateOk, 0);
        StartedAt = DateTime.Now;

        _worker = new Thread(() => Run(stop)) { IsBackground = true, Name = "KeepAwakeGuard" };
        _worker.Start();
        StateChanged?.Invoke();
    }

    public void Stop()
    {
        var worker = _worker;
        var stop = _stopSignal;
        if (worker == null || stop == null) return;

        _worker = null;
        _stopSignal = null;
        stop.Set();

        // The ES_CONTINUOUS request belongs to the worker thread, so it must die before we report release.
        worker.Join(3000);
        stop.Dispose();
        PowerRequestHeld = false;
        Emit(new LogEntry { Kind = LogKind.Released });
        StateChanged?.Invoke();
    }

    private void Run(ManualResetEvent stop)
    {
        uint previous = Native.SetThreadExecutionState(HoldFlags);
        if (previous == 0)
        {
            Emit(new LogEntry
            {
                Kind = LogKind.ExecutionStateRejected,
                Number = Marshal.GetLastWin32Error()
            });
        }
        else
        {
            Volatile.Write(ref _executionStateOk, 1);
            Emit(new LogEntry { Kind = LogKind.ExecutionStateOn });
        }

        PowerRequestHeld = _power.Acquire();
        Emit(new LogEntry { Kind = PowerRequestHeld ? LogKind.PowerRequestOn : LogKind.PowerRequestUnavailable });
        StateChanged?.Invoke();

        int elapsed = 0;
        try
        {
            while (!stop.WaitOne(1000))
            {
                elapsed++;
                if (elapsed % 30 == 0) Native.SetThreadExecutionState(HoldFlags);
                if (!_simulateInput) continue;

                uint idle = Native.IdleMilliseconds();
                if (idle < (uint)IntervalSeconds * 1000) continue;

                bool key = Native.PressKey(Native.VK_F15);
                bool mouse = _nudgeMouse && Native.NudgeMouse();
                Interlocked.Increment(ref _simulations);

                Emit(new LogEntry
                {
                    Kind = LogKind.Simulation,
                    Number = Simulations,
                    Second = (int)(idle / 1000),
                    Flag = key,
                    Extra = mouse
                });
            }
        }
        catch (Exception ex)
        {
            Emit(new LogEntry { Kind = LogKind.GuardError, Text = ex.Message });
        }
        finally
        {
            Native.SetThreadExecutionState(Native.ES_CONTINUOUS);
            _power.Release();
            Volatile.Write(ref _executionStateOk, 0);
        }
    }

    public void Emit(LogEntry entry) => Logged?.Invoke(entry);

    public void Dispose()
    {
        Stop();
        _power.Dispose();
    }
}