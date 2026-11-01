using System.Text.Json;
using System.IO;

namespace TimeNotice;

public sealed class Settings
{
    public int ReminderMinutes { get; set; } = 30; // legacy Phase 2 field, migrates to GeneralMinutes on load
    public bool GeneralEnabled { get; set; } = true;
    public int GeneralMinutes { get; set; } = 30;
    public bool WindowOnly { get; set; } = false;
    public string WindowStart { get; set; } = "09:00"; // ponytail: HH:mm text, TimePicker upgrade if asked
    public string WindowEnd { get; set; } = "23:00";
    public bool GameEnabled { get; set; } = false;
    public int GameMinutes { get; set; } = 60;
    public List<string> GameExes { get; set; } = new();
    public bool GameWindowOnly { get; set; } = false;
    public string GameWindowStart { get; set; } = "09:00"; // ponytail: HH:mm text, TimePicker upgrade if asked
    public string GameWindowEnd { get; set; } = "23:00";
    public int DisplaySeconds { get; set; } = 5;
    public bool ShowSession { get; set; } = true;
    public string MainTemplate { get; set; } = "It is now {time}"; // ponytail: {time} {next} tokens only, preview in Settings window later if asked
    public string SubTemplate { get; set; } = "Next: {next}";
    public string Position { get; set; } = "BottomRight"; // ponytail: 4 corners only, free-form drag later if asked
    public double Opacity { get; set; } = 0.92;
    public string ThemeMode { get; set; } = "Dark"; // Dark | Light | MatchHost | Custom
    public string CustomBackground { get; set; } = "#CC1E1E1E"; // ponytail: hex #RRGGBB or #AARRGGBB, color picker later if asked
    public string CustomText { get; set; } = "#FFFFFFFF";
    public string CustomSubText { get; set; } = "#FFBBBBBB";
    public string GameMainTemplate { get; set; } = ""; // empty = reuse MainTemplate
    public string GameSubTemplate { get; set; } = ""; // empty = reuse SubTemplate
    public bool GameNotifyOnDetect { get; set; } = false;
    public string GameDetectTemplate { get; set; } = "{game} detected"; // ponytail: {game} {interval} {time} tokens, custom line later if asked
    public string GameDetectSubTemplate { get; set; } = "Monitoring every {interval} min"; // blank = no sub line
    public bool PauseAll { get; set; } = false;
    public List<string> QuietModeExes { get; set; } = new() { "example-game.exe" };
    public List<string> LauncherExes { get; set; } = new();
    public bool HeuristicEnabled { get; set; } = true;
    public int HeuristicStartSeconds { get; set; } = 60; // sustained active before a session is found
    public int HeuristicSuspectSeconds { get; set; } = 30; // fg loss/idle before SUSPECT
    public int HeuristicInactiveMinutes { get; set; } = 5; // SUSPECT age before INACTIVE/close
    public int HeuristicGpuPercent { get; set; } = 5; // per-process GPU Engine % counts as active

    public static string Dir => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TimeNotice");

    public static string Path => System.IO.Path.Combine(Dir, "settings.json");

    public static Settings Load()
    {
        Settings s;
        try
        {
            if (File.Exists(Path))
                s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(Path)) ?? new Settings();
            else
                return new Settings();
        }
        catch { /* ponytail: corrupt config falls back to defaults, repair on save */ return new Settings(); }
        if (s.GeneralMinutes <= 0) s.GeneralMinutes = s.ReminderMinutes > 0 ? s.ReminderMinutes : 30; // migrate Phase 2 file
        s.ReminderMinutes = s.GeneralMinutes; // keep legacy in sync
        if (s.GameMinutes <= 0) s.GameMinutes = 60;
        if (string.IsNullOrWhiteSpace(s.GameWindowStart)) s.GameWindowStart = "09:00";
        if (string.IsNullOrWhiteSpace(s.GameWindowEnd)) s.GameWindowEnd = "23:00";
        if (string.IsNullOrWhiteSpace(s.MainTemplate)) s.MainTemplate = "It is now {time}";
        if (string.IsNullOrWhiteSpace(s.SubTemplate)) s.SubTemplate = "Next: {next}";
        if (s.GameMainTemplate is null) s.GameMainTemplate = "";
        if (s.GameSubTemplate is null) s.GameSubTemplate = "";
        if (string.IsNullOrWhiteSpace(s.GameDetectTemplate)) s.GameDetectTemplate = "{game} detected";
        if (s.GameDetectSubTemplate is null) s.GameDetectSubTemplate = "Monitoring every {interval} min";
        var validPositions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "TopLeft", "TopRight", "BottomLeft", "BottomRight" };
        if (!validPositions.Contains(s.Position ?? "")) s.Position = "BottomRight";
        var validThemes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Dark", "Light", "MatchHost", "Custom" };
        if (!validThemes.Contains(s.ThemeMode ?? "")) s.ThemeMode = "Dark";
        if (s.Opacity < 0.2) s.Opacity = 0.2;
        if (s.Opacity > 1.0) s.Opacity = 1.0;
        if (string.IsNullOrWhiteSpace(s.CustomBackground)) s.CustomBackground = "#CC1E1E1E";
        if (string.IsNullOrWhiteSpace(s.CustomText)) s.CustomText = "#FFFFFFFF";
        if (string.IsNullOrWhiteSpace(s.CustomSubText)) s.CustomSubText = "#FFBBBBBB";
        if (s.LauncherExes is null || s.LauncherExes.Count == 0)
            s.LauncherExes = GameCatalog.DefaultLaunchers(); // seed Phase 3 launcher-ignore on old configs
        s.LauncherExes = s.LauncherExes
            .Select(x => (x ?? "").Trim()).Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (s.HeuristicStartSeconds < 10 || s.HeuristicStartSeconds > 600) s.HeuristicStartSeconds = 60;
        if (s.HeuristicSuspectSeconds < 5 || s.HeuristicSuspectSeconds > 300) s.HeuristicSuspectSeconds = 30;
        if (s.HeuristicInactiveMinutes < 1 || s.HeuristicInactiveMinutes > 60) s.HeuristicInactiveMinutes = 5;
        if (s.HeuristicGpuPercent < 1 || s.HeuristicGpuPercent > 100) s.HeuristicGpuPercent = 5;
        return s;
    }

    public void Save()
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(Path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
