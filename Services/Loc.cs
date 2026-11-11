using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace KeepAwake.Services;

public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();

    private Dictionary<string, string> Map => Current == "id" ? Indonesian : English;

    public string Current { get; private set; } = "en";

    public event PropertyChangedEventHandler PropertyChanged;

    public string this[string key] => Map.TryGetValue(key, out var value) ? value : key;

    public string F(string key, params object[] args) => string.Format(this[key], args);

    public string LanguageName => Current == "id" ? "Bahasa Indonesia" : "English";

    public void Set(string code)
    {
        code = code == "id" ? "id" : "en";
        if (Current == code) return;
        Current = code;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Current)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LanguageName)));
    }

    private static readonly Dictionary<string, string> English = new()
    {
        ["Header.Title"] = "Keep Awake",
        ["Header.Subtitle"] = "Holds sleep, screen-off and idle lock so long unattended work keeps running.",

        ["Menu.NewSession"] = "New Session",
        ["Menu.Theme"] = "Theme",
        ["Menu.Language"] = "Language",
        ["Menu.About"] = "About Me",
        ["Menu.Close"] = "Close Application",

        ["Theme.Light"] = "Light",
        ["Theme.Dark"] = "Dark",
        ["Theme.System"] = "Follow system",
        ["Lang.En"] = "English",
        ["Lang.Id"] = "Bahasa Indonesia",

        ["Status.Title"] = "Session status",
        ["Status.Active"] = "Awake",
        ["Status.Idle"] = "Not running",
        ["Status.ActiveHint"] = "Display and system power states are held.",
        ["Status.IdleHint"] = "Start a session to hold sleep and idle lock.",
        ["Status.Uptime"] = "Uptime",
        ["Status.Since"] = "Started at {0}",
        ["Status.Waiting"] = "Waiting for the first idle window",

        ["Settings.Title"] = "Guard settings",
        ["Settings.Threshold"] = "Idle threshold",
        ["Settings.ThresholdHint"] = "Send a wake signal once the machine has been idle this long.",
        ["Settings.Seconds"] = "sec",
        ["Settings.Custom"] = "Custom",
        ["Settings.Simulate"] = "Simulate input when idle (F15)",
        ["Settings.SimulateHint"] = "Needed when a policy locks by inactivity timeout.",
        ["Settings.Nudge"] = "Add a 1 px mouse move",
        ["Settings.NudgeHint"] = "Fallback for policies that ignore F15.",

        ["Control.Title"] = "Session control",
        ["Control.Start"] = "Start session",
        ["Control.Restart"] = "Restart",
        ["Control.Stop"] = "Stop",
        ["Control.Ready"] = "Ready. No admin, no service, no registry.",
        ["Control.Running"] = "Holding sleep, screen-off and idle lock.",

        ["Log.Title"] = "Activity log",
        ["Log.Clear"] = "Clear",
        ["Log.Empty"] = "No activity yet. Start a session to see events here.",
        ["Log.Count"] = "{0} events",
        ["Log.CountOne"] = "1 event",

        ["Bar.Label"] = "Protection",
        ["Bar.Display"] = "Display",
        ["Bar.System"] = "System",
        ["Bar.Power"] = "Modern Standby",
        ["Bar.Input"] = "Wake signals",
        ["Bar.Idle"] = "Idle now",
        ["Bar.Held"] = "Held",
        ["Bar.Free"] = "Free",
        ["Bar.Unavailable"] = "Unavailable",

        ["Common.On"] = "on",
        ["Common.Off"] = "off",

        ["About.Version"] = "Version 2.0",
        ["About.BackgroundTitle"] = "Background & purpose",
        ["About.Background1"] = "KeepAwake is a native Windows utility that keeps a machine awake during long, unattended work. It holds the display and system power states, and can send a quiet wake signal when the machine goes idle.",
        ["About.Background2"] = "Common uses: long renders and exports, downloads, builds, presentations, and watching a screen that must not lock.",
        ["About.TechTitle"] = "Technology",
        ["About.Tech1"] = "UI: WPF + WPF-UI (Fluent 2) on .NET 9. Runs as a normal app without admin or Developer Mode.",
        ["About.Tech2"] = "Power: SetThreadExecutionState plus PowerCreateRequest for Modern Standby.",
        ["About.Tech3"] = "Input: SendInput with F15 and an optional 1 px mouse move.",
        ["About.Tech4"] = "MVVM helpers: CommunityToolkit.Mvvm.",
        ["About.DevTitle"] = "Developer",
        ["About.DevName"] = "Bimo Gusha Triastanto",
        ["About.DevNote"] = "Designed and built as a lightweight, native, low-power desktop utility.",
        ["About.Close"] = "Close",

        ["Log.AppLoaded"] = "App loaded. No admin, no registry, no network.",
        ["Log.ReadyHint"] = "Start a session to hold sleep, screen-off and idle lock.",
        ["Log.Started"] = "Start — threshold {0} s, input simulation {1}, mouse move {2}.",
        ["Log.ExecutionStateOn"] = "Execution state held: display + system (ES_CONTINUOUS).",
        ["Log.ExecutionStateRejected"] = "Execution state refused by the system (error {0}).",
        ["Log.PowerRequestOn"] = "Power request active for Modern Standby (system + display).",
        ["Log.PowerRequestUnavailable"] = "Modern Standby power request unavailable — using execution state only.",
        ["Log.Released"] = "All requests released — power policy back to normal.",
        ["Log.Stopped"] = "Session stopped.",
        ["Log.Restarted"] = "Session restarted.",
        ["Log.Simulation"] = "Wake signal #{0} (idle {1} s): F15{2}{3}.",
        ["Log.SimulationFailed"] = " refused",
        ["Log.SimulationMouse"] = " + mouse 1 px",
        ["Log.IntervalChanged"] = "Idle threshold set to {0} s.",
        ["Log.SimulateOn"] = "Input simulation turned on.",
        ["Log.SimulateOff"] = "Input simulation turned off.",
        ["Log.NudgeOn"] = "Mouse move turned on.",
        ["Log.NudgeOff"] = "Mouse move turned off.",
        ["Log.SimulateOffNote"] = "Input simulation is off. A policy using InactivityTimeoutSecs can still lock the screen.",
        ["Log.NewSession"] = "New session — settings reset to defaults.",
        ["Log.ThemeChanged"] = "Theme set to {0}.",
        ["Log.LanguageChanged"] = "Language set to {0}.",
        ["Log.Closing"] = "Closing — stopping the guard.",
        ["Log.GuardError"] = "Guard error: {0}",
        ["Log.LogCleared"] = "Log cleared."
    };

    private static readonly Dictionary<string, string> Indonesian = new()
    {
        ["Header.Title"] = "Keep Awake",
        ["Header.Subtitle"] = "Menahan sleep, layar mati, dan idle lock agar pekerjaan panjang tanpa pengawasan tetap berjalan.",

        ["Menu.NewSession"] = "Sesi Baru",
        ["Menu.Theme"] = "Tema",
        ["Menu.Language"] = "Bahasa",
        ["Menu.About"] = "Tentang Saya",
        ["Menu.Close"] = "Tutup Aplikasi",

        ["Theme.Light"] = "Terang",
        ["Theme.Dark"] = "Gelap",
        ["Theme.System"] = "Ikuti sistem",
        ["Lang.En"] = "English",
        ["Lang.Id"] = "Bahasa Indonesia",

        ["Status.Title"] = "Status sesi",
        ["Status.Active"] = "Terjaga",
        ["Status.Idle"] = "Tidak aktif",
        ["Status.ActiveHint"] = "Power state display dan system ditahan.",
        ["Status.IdleHint"] = "Mulai sesi untuk menahan sleep dan idle lock.",
        ["Status.Uptime"] = "Durasi",
        ["Status.Since"] = "Mulai pukul {0}",
        ["Status.Waiting"] = "Menunggu jendela idle pertama",

        ["Settings.Title"] = "Pengaturan penjaga",
        ["Settings.Threshold"] = "Ambang idle",
        ["Settings.ThresholdHint"] = "Kirim sinyal bangun setelah komputer diam selama ini.",
        ["Settings.Seconds"] = "dtk",
        ["Settings.Custom"] = "Kustom",
        ["Settings.Simulate"] = "Simulasi input saat idle (F15)",
        ["Settings.SimulateHint"] = "Diperlukan bila policy mengunci berdasarkan inactivity timeout.",
        ["Settings.Nudge"] = "Tambahkan gerak mouse 1 px",
        ["Settings.NudgeHint"] = "Cadangan bila policy mengabaikan F15.",

        ["Control.Title"] = "Kontrol sesi",
        ["Control.Start"] = "Mulai sesi",
        ["Control.Restart"] = "Ulangi",
        ["Control.Stop"] = "Hentikan",
        ["Control.Ready"] = "Siap. Tanpa admin, tanpa service, tanpa registry.",
        ["Control.Running"] = "Menahan sleep, layar mati, dan idle lock.",

        ["Log.Title"] = "Log aktivitas",
        ["Log.Clear"] = "Bersihkan",
        ["Log.Empty"] = "Belum ada aktivitas. Mulai sesi untuk melihat event di sini.",
        ["Log.Count"] = "{0} kejadian",
        ["Log.CountOne"] = "1 kejadian",

        ["Bar.Label"] = "Proteksi",
        ["Bar.Display"] = "Layar",
        ["Bar.System"] = "Sistem",
        ["Bar.Power"] = "Modern Standby",
        ["Bar.Input"] = "Sinyal bangun",
        ["Bar.Idle"] = "Idle saat ini",
        ["Bar.Held"] = "Ditahan",
        ["Bar.Free"] = "Bebas",
        ["Bar.Unavailable"] = "Tidak tersedia",

        ["Common.On"] = "aktif",
        ["Common.Off"] = "nonaktif",

        ["About.Version"] = "Versi 2.0",
        ["About.BackgroundTitle"] = "Latar Belakang & Fungsi",
        ["About.Background1"] = "KeepAwake adalah utilitas Windows native yang menjaga komputer tetap terjaga saat pekerjaan panjang tanpa pengawasan. Aplikasi menahan power state display dan system, serta bisa mengirim sinyal bangun senyap saat komputer idle.",
        ["About.Background2"] = "Kegunaan umum: render dan ekspor panjang, unduhan, build, presentasi, dan memantau layar yang tidak boleh terkunci.",
        ["About.TechTitle"] = "Teknologi",
        ["About.Tech1"] = "UI: WPF + WPF-UI (Fluent 2) di .NET 9. Berjalan sebagai aplikasi normal tanpa admin atau Developer Mode.",
        ["About.Tech2"] = "Power: SetThreadExecutionState plus PowerCreateRequest untuk Modern Standby.",
        ["About.Tech3"] = "Input: SendInput dengan F15 dan gerak mouse 1 px opsional.",
        ["About.Tech4"] = "Helper MVVM: CommunityToolkit.Mvvm.",
        ["About.DevTitle"] = "Pengembang",
        ["About.DevName"] = "Bimo Gusha Triastanto",
        ["About.DevNote"] = "Dirancang dan dikembangkan sebagai utilitas desktop yang ringan, native, dan bertenaga.",
        ["About.Close"] = "Tutup",

        ["Log.AppLoaded"] = "Aplikasi dimuat. Tanpa admin, tanpa registry, tanpa jaringan.",
        ["Log.ReadyHint"] = "Mulai sesi untuk menahan sleep, layar mati, dan idle lock.",
        ["Log.Started"] = "Mulai — ambang {0} dtk, simulasi input {1}, gerak mouse {2}.",
        ["Log.ExecutionStateOn"] = "Execution state ditahan: display + system (ES_CONTINUOUS).",
        ["Log.ExecutionStateRejected"] = "Execution state ditolak sistem (error {0}).",
        ["Log.PowerRequestOn"] = "Power request aktif untuk Modern Standby (system + display).",
        ["Log.PowerRequestUnavailable"] = "Power request Modern Standby tidak tersedia — memakai execution state saja.",
        ["Log.Released"] = "Semua request dilepas — power policy kembali normal.",
        ["Log.Stopped"] = "Sesi dihentikan.",
        ["Log.Restarted"] = "Sesi diulang.",
        ["Log.Simulation"] = "Sinyal bangun #{0} (idle {1} dtk): F15{2}{3}.",
        ["Log.SimulationFailed"] = " ditolak",
        ["Log.SimulationMouse"] = " + mouse 1 px",
        ["Log.IntervalChanged"] = "Ambang idle diubah ke {0} dtk.",
        ["Log.SimulateOn"] = "Simulasi input diaktifkan.",
        ["Log.SimulateOff"] = "Simulasi input dimatikan.",
        ["Log.NudgeOn"] = "Gerak mouse diaktifkan.",
        ["Log.NudgeOff"] = "Gerak mouse dimatikan.",
        ["Log.SimulateOffNote"] = "Simulasi input nonaktif. Policy yang memakai InactivityTimeoutSecs masih bisa mengunci layar.",
        ["Log.NewSession"] = "Sesi baru — pengaturan kembali ke bawaan.",
        ["Log.ThemeChanged"] = "Tema diubah ke {0}.",
        ["Log.LanguageChanged"] = "Bahasa diubah ke {0}.",
        ["Log.Closing"] = "Menutup — menghentikan penjaga.",
        ["Log.GuardError"] = "Error penjaga: {0}",
        ["Log.LogCleared"] = "Log dibersihkan."
    };
}