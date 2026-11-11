using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KeepAwake.Services;

public sealed class AppConfig
{
    public string Theme { get; set; } = "System";
    public string Language { get; set; } = "en";
    public int IntervalSeconds { get; set; } = 60;
    public bool SimulateInput { get; set; } = true;
    public bool NudgeMouse { get; set; }

    private static string FilePath => Path.Combine(AppContext.BaseDirectory, "keepawake.config.json");

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public static AppConfig Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new AppConfig();
            var config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(FilePath), Options) ?? new AppConfig();
            config.IntervalSeconds = Math.Clamp(config.IntervalSeconds, 10, 600);
            if (config.Theme is not ("Light" or "Dark" or "System")) config.Theme = "System";
            if (config.Language is not ("en" or "id")) config.Language = "en";
            return config;
        }
        catch
        {
            return new AppConfig();
        }
    }

    public void Save()
    {
        try { File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Options)); }
        catch { }
    }
}