using System.Windows;
using System.Windows.Controls;

namespace TimeNotice;

public partial class SettingsWindow : Window
{
    private readonly Settings _settings;
    private readonly Action _onSaved;

    public SettingsWindow(Settings settings, Action onSaved)
    {
        InitializeComponent();
        Icon = AppIcon.CreateWindowIcon();
        _settings = settings;
        _onSaved = onSaved;
        GeneralEnabledBox.IsChecked = _settings.GeneralEnabled;
        IntervalBox.Text = _settings.GeneralMinutes.ToString();
        WindowOnlyBox.IsChecked = _settings.WindowOnly;
        WindowStartBox.Text = _settings.WindowStart;
        WindowEndBox.Text = _settings.WindowEnd;
        GameEnabledBox.IsChecked = _settings.GameEnabled;
        GameIntervalBox.Text = _settings.GameMinutes.ToString();
        CountBox.IsChecked = _settings.CountWhenRunning;
        GameBox.Text = string.Join("\n", _settings.GameExes);
        IgnoreBox.Text = string.Join("\n", _settings.IgnoreExes);
        GameWindowOnlyBox.IsChecked = _settings.GameWindowOnly;
        GameWindowStartBox.Text = _settings.GameWindowStart;
        GameWindowEndBox.Text = _settings.GameWindowEnd;
        DisplayBox.Text = _settings.DisplaySeconds.ToString();
        ShowNextBox.IsChecked = _settings.ShowSession;
        MainTemplateBox.Text = _settings.MainTemplate;
        SubTemplateBox.Text = _settings.SubTemplate;
        QuietBox.Text = string.Join("\n", _settings.QuietModeExes);
        GameMainBox.Text = _settings.GameMainTemplate ?? "";
        GameSubBox.Text = _settings.GameSubTemplate ?? "";
        GameNotifyBox.IsChecked = _settings.GameNotifyOnDetect;
        GameDetectTemplateBox.Text = _settings.GameDetectTemplate;
        GameDetectSubBox.Text = _settings.GameDetectSubTemplate ?? "";
        HeurEnabledBox.IsChecked = _settings.HeuristicEnabled;
        HeurStartBox.Text = _settings.HeuristicStartSeconds.ToString();
        HeurSuspectBox.Text = _settings.HeuristicSuspectSeconds.ToString();
        HeurInactiveBox.Text = _settings.HeuristicInactiveMinutes.ToString();
        HeurGpuBox.Text = _settings.HeuristicGpuPercent.ToString();
        SelectTag(PositionBox, _settings.Position, "BottomRight");
        SelectTag(ThemeBox, _settings.ThemeMode, "Dark");
        BgBox.Text = _settings.CustomBackground;
        TextColorBox.Text = _settings.CustomText;
        SubColorBox.Text = _settings.CustomSubText;
        OpacitySlider.Value = Math.Clamp(_settings.Opacity * 100, 20, 100);
        OpacityLabel.Text = $"{(int)OpacitySlider.Value}%";
        OpacitySlider.ValueChanged += (_, e) => { OpacityLabel.Text = $"{(int)e.NewValue}%"; };
        ThemeBox.SelectionChanged += (_, _) => { CustomColorPanel.Visibility = SelectedTag(ThemeBox) == "Custom" ? Visibility.Visible : Visibility.Collapsed; };
        CustomColorPanel.Visibility = SelectedTag(ThemeBox) == "Custom" ? Visibility.Visible : Visibility.Collapsed;
        IntervalBox.TextChanged += (_, _) => UpdateWarning();
        GameIntervalBox.TextChanged += (_, _) => UpdateWarning();
        DisplayBox.TextChanged += (_, _) => UpdateWarning();
        GeneralEnabledBox.Checked += (_, _) => UpdateWarning();
        GeneralEnabledBox.Unchecked += (_, _) => UpdateWarning();
        GameEnabledBox.Checked += (_, _) => UpdateWarning();
        GameEnabledBox.Unchecked += (_, _) => UpdateWarning();
        WindowOnlyBox.Checked += (_, _) => { GeneralTimePanel.Visibility = Visibility.Visible; };
        WindowOnlyBox.Unchecked += (_, _) => { GeneralTimePanel.Visibility = Visibility.Collapsed; };
        GameWindowOnlyBox.Checked += (_, _) => { GameTimePanel.Visibility = Visibility.Visible; };
        GameWindowOnlyBox.Unchecked += (_, _) => { GameTimePanel.Visibility = Visibility.Collapsed; };
        GeneralTimePanel.Visibility = WindowOnlyBox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        GameTimePanel.Visibility = GameWindowOnlyBox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        UpdateWarning();
    }

    private static string SelectedTag(System.Windows.Controls.ComboBox box)
    {
        return (box.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
    }

    private static void SelectTag(System.Windows.Controls.ComboBox box, string? value, string fallback)
    {
        var want = (value ?? fallback).Trim();
        foreach (var obj in box.Items)
        {
            if (obj is ComboBoxItem item && string.Equals(item.Tag?.ToString(), want, StringComparison.OrdinalIgnoreCase))
            {
                box.SelectedItem = item;
                return;
            }
        }
        foreach (var obj in box.Items)
        {
            if (obj is ComboBoxItem item && string.Equals(item.Tag?.ToString(), fallback, StringComparison.OrdinalIgnoreCase))
            {
                box.SelectedItem = item;
                return;
            }
        }
    }

    private static bool IsColor(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        try { System.Windows.Media.ColorConverter.ConvertFromString(text.Trim()); return true; }
        catch { return false; }
    }

    private void UpdateWarning()
    {
        WarnText.Text = "";
        var okSecs = int.TryParse(DisplayBox.Text.Trim(), out var secs) ? secs : 5;
        var need = okSecs + 2; // box time + queue gap, seconds
        if (GeneralEnabledBox.IsChecked == true
            && int.TryParse(IntervalBox.Text.Trim(), out var mins)
            && mins * 60 <= need)
        {
            WarnText.Text += $"General: every {mins} min with a {okSecs}s box will overlap constantly. ";
        }
        if (GameEnabledBox.IsChecked == true
            && int.TryParse(GameIntervalBox.Text.Trim(), out var gmin)
            && gmin * 60 <= need)
        {
            WarnText.Text += $"Game: every {gmin} min with a {okSecs}s box will overlap constantly. ";
        }
        if (WarnText.Text.Length > 0)
        {
            WarnText.Text += "Popups will queue back-to-back (see debug.log).";
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";
        if (!int.TryParse(IntervalBox.Text.Trim(), out var mins) || mins < 1 || mins > 480)
        {
            ErrorText.Text = "General minutes must be 1 to 480.";
            return;
        }
        if (!int.TryParse(GameIntervalBox.Text.Trim(), out var gmin) || gmin < 1 || gmin > 480)
        {
            ErrorText.Text = "Game minutes must be 1 to 480.";
            return;
        }
        if (!int.TryParse(DisplayBox.Text.Trim(), out var secs) || secs < 1 || secs > 60)
        {
            ErrorText.Text = "Seconds must be 1 to 60.";
            return;
        }
        if (!TimeSpan.TryParse(WindowStartBox.Text.Trim(), out _))
        {
            ErrorText.Text = "From time must look like 09:00.";
            return;
        }
        if (!TimeSpan.TryParse(WindowEndBox.Text.Trim(), out _))
        {
            ErrorText.Text = "To time must look like 23:00.";
            return;
        }
        if (GameWindowOnlyBox.IsChecked == true)
        {
            if (!TimeSpan.TryParse(GameWindowStartBox.Text.Trim(), out _))
            {
                ErrorText.Text = "Game from time must look like 09:00.";
                return;
            }
            if (!TimeSpan.TryParse(GameWindowEndBox.Text.Trim(), out _))
            {
                ErrorText.Text = "Game to time must look like 23:00.";
                return;
            }
        }
        if (string.IsNullOrWhiteSpace(MainTemplateBox.Text))
        {
            ErrorText.Text = "Main line cannot be empty. Use {time}.";
            return;
        }
        foreach (var rawLine in GameBox.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;
            var eq = line.IndexOf('=');
            if ((eq < 0 ? line : line[..eq]).Trim().Length == 0)
            {
                ErrorText.Text = "Each game line needs an exe, e.g. spotify.exe.";
                return;
            }
        }
        if (GameNotifyBox.IsChecked == true && string.IsNullOrWhiteSpace(GameDetectTemplateBox.Text))
        {
            ErrorText.Text = "Detection line cannot be empty when detection pops are on.";
            return;
        }
        if (!int.TryParse(HeurStartBox.Text.Trim(), out var heurStart) || heurStart < 10 || heurStart > 600)
        {
            ErrorText.Text = "Heuristic start must be 10 to 600 seconds.";
            return;
        }
        if (!int.TryParse(HeurSuspectBox.Text.Trim(), out var heurSuspect) || heurSuspect < 5 || heurSuspect > 300)
        {
            ErrorText.Text = "Heuristic suspect must be 5 to 300 seconds.";
            return;
        }
        if (!int.TryParse(HeurInactiveBox.Text.Trim(), out var heurInactive) || heurInactive < 1 || heurInactive > 60)
        {
            ErrorText.Text = "Heuristic close must be 1 to 60 minutes.";
            return;
        }
        if (!int.TryParse(HeurGpuBox.Text.Trim(), out var heurGpu) || heurGpu < 1 || heurGpu > 100)
        {
            ErrorText.Text = "Heuristic GPU % must be 1 to 100.";
            return;
        }
        var position = SelectedTag(PositionBox);
        if (position is not ("TopLeft" or "TopRight" or "BottomLeft" or "BottomRight"))
        {
            ErrorText.Text = "Pick a popup corner.";
            return;
        }
        var theme = SelectedTag(ThemeBox);
        if (theme is not ("Dark" or "Light" or "MatchHost" or "Custom"))
        {
            ErrorText.Text = "Pick a theme.";
            return;
        }
        if (theme == "Custom")
        {
            if (!IsColor(BgBox.Text)) { ErrorText.Text = "Background color must look like #CC1E1E1E."; return; }
            if (!IsColor(TextColorBox.Text)) { ErrorText.Text = "Main text color must look like #FFFFFFFF."; return; }
            if (!IsColor(SubColorBox.Text)) { ErrorText.Text = "Sub text color must look like #FFBBBBBB."; return; }
        }
        _settings.GeneralEnabled = GeneralEnabledBox.IsChecked == true;
        _settings.GeneralMinutes = mins;
        _settings.ReminderMinutes = mins; // keep legacy in sync
        _settings.WindowOnly = WindowOnlyBox.IsChecked == true;
        _settings.WindowStart = WindowStartBox.Text.Trim();
        _settings.WindowEnd = WindowEndBox.Text.Trim();
        _settings.GameEnabled = GameEnabledBox.IsChecked == true;
        _settings.GameMinutes = gmin;
        _settings.CountWhenRunning = CountBox.IsChecked == true;
        _settings.GameExes = GameBox.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        _settings.IgnoreExes = IgnoreBox.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        _settings.GameWindowOnly = GameWindowOnlyBox.IsChecked == true;
        _settings.GameWindowStart = GameWindowStartBox.Text.Trim();
        _settings.GameWindowEnd = GameWindowEndBox.Text.Trim();
        _settings.DisplaySeconds = secs;
        _settings.ShowSession = ShowNextBox.IsChecked == true;
        _settings.MainTemplate = MainTemplateBox.Text.Trim();
        _settings.SubTemplate = SubTemplateBox.Text.Trim();
        _settings.GameMainTemplate = GameMainBox.Text.Trim();
        _settings.GameSubTemplate = GameSubBox.Text.Trim();
        _settings.GameNotifyOnDetect = GameNotifyBox.IsChecked == true;
        _settings.GameDetectTemplate = GameDetectTemplateBox.Text.Trim();
        _settings.GameDetectSubTemplate = GameDetectSubBox.Text.Trim();
        _settings.HeuristicEnabled = HeurEnabledBox.IsChecked == true;
        _settings.HeuristicStartSeconds = heurStart;
        _settings.HeuristicSuspectSeconds = heurSuspect;
        _settings.HeuristicInactiveMinutes = heurInactive;
        _settings.HeuristicGpuPercent = heurGpu;
        _settings.Position = position;
        _settings.ThemeMode = theme;
        _settings.CustomBackground = BgBox.Text.Trim();
        _settings.CustomText = TextColorBox.Text.Trim();
        _settings.CustomSubText = SubColorBox.Text.Trim();
        _settings.Opacity = Math.Clamp(OpacitySlider.Value / 100.0, 0.2, 1.0);
        _settings.QuietModeExes = QuietBox.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        _settings.Save();
        _onSaved();
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
