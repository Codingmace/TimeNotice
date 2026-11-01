using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace TimeNotice;

public partial class OverlayWindow : Window
{
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    private readonly Settings _settings;
    private readonly DispatcherTimer _hideTimer;

    public event Action? Hidden;
    public bool IsShowing => Visibility == Visibility.Visible;

    public OverlayWindow(Settings settings)
    {
        InitializeComponent();
        Icon = AppIcon.CreateWindowIcon();
        _settings = settings;
        ShowActivated = false;
        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(settings.DisplaySeconds) };
        _hideTimer.Tick += (_, _) => FadeOut();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var h = new WindowInteropHelper(this).Handle;
        int ex = GetWindowLong(h, -20);
        SetWindowLong(h, -20, ex | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW);
    }

    public void ShowReminder(string timeText, string? subText = null)
    {
        BeginAnimation(OpacityProperty, null); // clear fade hold so re-show is visible
        TimeText.Text = timeText;
        SubText.Text = subText ?? (_settings.ShowSession ? "TimeNotice test — no focus steal" : "");
        ApplyTheme();
        if (Visibility != Visibility.Visible) Show();
        UpdateLayout();
        Position();
        _hideTimer.Interval = TimeSpan.FromSeconds(Math.Max(1, _settings.DisplaySeconds));
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    // Sticky confirm (test): shows without auto-hide so a 15s poll can own
    // the countdown. Still NOACTIVATE + TRANSPARENT: no focus steal, click-through.
    public void ShowSticky(string main, string? sub)
    {
        BeginAnimation(OpacityProperty, null);
        TimeText.Text = main;
        SubText.Text = sub ?? "";
        ApplyTheme();
        if (Visibility != Visibility.Visible) Show();
        UpdateLayout();
        Position();
        _hideTimer.Stop();
    }

    public void UpdateSticky(string main, string? sub)
    {
        if (Visibility != Visibility.Visible) return;
        TimeText.Text = main;
        SubText.Text = sub ?? "";
        UpdateLayout();
        Position();
    }

    public void HideNow()
    {
        _hideTimer.Stop();
        BeginAnimation(OpacityProperty, null);
        Hide();
        try { Hidden?.Invoke(); } catch { }
    }

    private void ApplyTheme()
    {
        var mode = (_settings.ThemeMode ?? "Dark").Trim();
        if (mode.Equals("MatchHost", StringComparison.OrdinalIgnoreCase))
            mode = IsHostLight() ? "Light" : "Dark";
        string bg = "#CC1E1E1E", fg = "#FFFFFFFF", sub = "#FFBBBBBB";
        if (mode.Equals("Light", StringComparison.OrdinalIgnoreCase))
        {
            bg = "#FFFFFFFF"; fg = "#FF111116"; sub = "#FF5B5B66";
        }
        else if (mode.Equals("Custom", StringComparison.OrdinalIgnoreCase))
        {
            bg = _settings.CustomBackground; fg = _settings.CustomText; sub = _settings.CustomSubText;
        }
        PopupBorder.Background = new System.Windows.Media.SolidColorBrush(ParseColor(bg, "#CC1E1E1E"));
        TimeText.Foreground = new System.Windows.Media.SolidColorBrush(ParseColor(fg, "#FFFFFFFF"));
        SubText.Foreground = new System.Windows.Media.SolidColorBrush(ParseColor(sub, "#FFBBBBBB"));
        var op = _settings.Opacity;
        if (op < 0.2) op = 0.2;
        if (op > 1.0) op = 1.0;
        Opacity = op;
    }

    private static System.Windows.Media.Color ParseColor(string? text, string fallback)
    {
        foreach (var candidate in new[] { text ?? "", fallback })
        {
            try
            {
                var c = System.Windows.Media.ColorConverter.ConvertFromString(candidate.Trim());
                if (c is System.Windows.Media.Color color) return color;
            }
            catch { }
        }
        return System.Windows.Media.Colors.Black;
    }

    private static bool IsHostLight()
    {
        try
        {
            var v = Microsoft.Win32.Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme", 0);
            if (v is int i) return i != 0;
        }
        catch { }
        return false; // fail-dark when unknown
    }

    private void Position()
    {
        var area = SystemParameters.WorkArea;
        var w = ActualWidth > 0 ? ActualWidth : Width; // ponytail: SizeToContent makes Height auto, so anchor on rendered size
        var h = ActualHeight > 0 ? ActualHeight : 90;
        var pos = (_settings.Position ?? "BottomRight").Trim();
        if (pos.Equals("TopLeft", StringComparison.OrdinalIgnoreCase))
        {
            Left = area.Left + 24;
            Top = area.Top + 24;
        }
        else if (pos.Equals("TopRight", StringComparison.OrdinalIgnoreCase))
        {
            Left = area.Right - w - 24;
            Top = area.Top + 24;
        }
        else if (pos.Equals("BottomLeft", StringComparison.OrdinalIgnoreCase))
        {
            Left = area.Left + 24;
            Top = area.Bottom - h - 24;
        }
        else // BottomRight
        {
            Left = area.Right - w - 24;
            Top = area.Bottom - h - 24;
        }
    }

    private void FadeOut()
    {
        _hideTimer.Stop();
        var anim = new DoubleAnimation(0, TimeSpan.FromMilliseconds(400));
        anim.Completed += (_, _) => { Hide(); try { Hidden?.Invoke(); } catch { } };
        BeginAnimation(OpacityProperty, anim);
    }

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}
