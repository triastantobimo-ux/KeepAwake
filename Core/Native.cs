using System;
using System.Runtime.InteropServices;

namespace KeepAwake.Core;

internal static class Native
{
    public const uint ES_SYSTEM_REQUIRED = 0x00000001;
    public const uint ES_DISPLAY_REQUIRED = 0x00000002;
    public const uint ES_CONTINUOUS = 0x80000000;

    public const ushort VK_F15 = 0x7E;

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint SetThreadExecutionState(uint esFlags);

    [DllImport("kernel32.dll")]
    private static extern uint GetTickCount();

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO info);

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUTUNION
    {
        [FieldOffset(0)] public MOUSEINPUT Mouse;
        [FieldOffset(0)] public KEYBDINPUT Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint Type;
        public INPUTUNION Data;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, INPUT[] inputs, int size);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr PowerCreateRequest(ref REASON_CONTEXT context);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool PowerSetRequest(IntPtr handle, int type);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool PowerClearRequest(IntPtr handle, int type);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr handle);

    [StructLayout(LayoutKind.Sequential)]
    public struct REASON_CONTEXT
    {
        public uint Version;
        public uint Flags;
        public IntPtr SimpleReasonString;
    }

    public static uint IdleMilliseconds()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO)) };
        if (!GetLastInputInfo(ref info)) return 0;
        return unchecked(GetTickCount() - info.dwTime);
    }

    public static bool PressKey(ushort vk)
    {
        var inputs = new INPUT[2];
        inputs[0].Type = 1;
        inputs[0].Data.Keyboard = new KEYBDINPUT { wVk = vk, dwFlags = 0 };
        inputs[1].Type = 1;
        inputs[1].Data.Keyboard = new KEYBDINPUT { wVk = vk, dwFlags = 2 };
        return SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT))) == 2;
    }

    public static bool NudgeMouse()
    {
        var inputs = new INPUT[2];
        inputs[0].Type = 0;
        inputs[0].Data.Mouse = new MOUSEINPUT { dx = 1, dy = 0, dwFlags = 0x0001 };
        inputs[1].Type = 0;
        inputs[1].Data.Mouse = new MOUSEINPUT { dx = -1, dy = 0, dwFlags = 0x0001 };
        return SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT))) == 2;
    }
}