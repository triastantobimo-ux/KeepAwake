using System;
using System.Runtime.InteropServices;

namespace KeepAwake.Core;

internal sealed class PowerRequest : IDisposable
{
    private const int DisplayRequired = 0;
    private const int SystemRequired = 1;
    private const uint SimpleReasonString = 0x1;

    private IntPtr _handle = IntPtr.Zero;
    private IntPtr _reason = IntPtr.Zero;

    public bool SystemHeld { get; private set; }
    public bool DisplayHeld { get; private set; }

    public bool Acquire()
    {
        try
        {
            _reason = Marshal.StringToHGlobalUni("KeepAwake: active work session");
            var context = new Native.REASON_CONTEXT
            {
                Version = 0,
                Flags = SimpleReasonString,
                SimpleReasonString = _reason
            };

            _handle = Native.PowerCreateRequest(ref context);
            if (_handle == IntPtr.Zero)
            {
                FreeReason();
                return false;
            }

            SystemHeld = Native.PowerSetRequest(_handle, SystemRequired);
            DisplayHeld = Native.PowerSetRequest(_handle, DisplayRequired);
            return SystemHeld || DisplayHeld;
        }
        catch
        {
            return false;
        }
    }

    public void Release()
    {
        try
        {
            if (_handle != IntPtr.Zero)
            {
                if (DisplayHeld) Native.PowerClearRequest(_handle, DisplayRequired);
                if (SystemHeld) Native.PowerClearRequest(_handle, SystemRequired);
                Native.CloseHandle(_handle);
            }
        }
        catch { }

        _handle = IntPtr.Zero;
        SystemHeld = false;
        DisplayHeld = false;
        FreeReason();
    }

    private void FreeReason()
    {
        if (_reason == IntPtr.Zero) return;
        try { Marshal.FreeHGlobal(_reason); }
        catch { }
        _reason = IntPtr.Zero;
    }

    public void Dispose() => Release();
}