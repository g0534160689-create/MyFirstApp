using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace RavKavKiosk;

internal sealed class MainForm : Form
{
    private readonly AppSettings _settings = AppSettings.Load();
    private readonly WebView2 _browser = new() { Dock = DockStyle.Fill };
    private readonly Label _status = new()
    {
        AutoSize = false, Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(12, 0, 0, 0),
        Text = "מתחבר לשירות רב־קו..."
    };

    public MainForm()
    {
        Text = "טעינת רב־קו";
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(900, 650);
        BackColor = Color.White;

        var header = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = Color.FromArgb(20, 52, 84) };
        var title = new Label
        {
            Text = "טעינת רב־קו", ForeColor = Color.White,
            Font = new Font("Segoe UI", 15, FontStyle.Bold),
            Dock = DockStyle.Right, Width = 220,
            TextAlign = ContentAlignment.MiddleCenter
        };
        var exit = new Button
        {
            Text = "יציאה", Dock = DockStyle.Left, Width = 90,
            FlatStyle = FlatStyle.Flat, ForeColor = Color.White,
            BackColor = Color.FromArgb(20, 52, 84)
        };
        exit.FlatAppearance.BorderSize = 0;
        exit.Click += (_, _) => Close();
        header.Controls.Add(title);
        header.Controls.Add(exit);

        var footer = new Panel { Dock = DockStyle.Bottom, Height = 32, BackColor = Color.FromArgb(242, 245, 248) };
        footer.Controls.Add(_status);
        Controls.Add(_browser);
        Controls.Add(footer);
        Controls.Add(header);
        Shown += InitializeBrowserAsync;
    }

    private async void InitializeBrowserAsync(object? sender, EventArgs e)
    {
        try
        {
            await _browser.EnsureCoreWebView2Async();
            var core = _browser.CoreWebView2;
            var options = core.Settings;
            options.AreDefaultContextMenusEnabled = false;
            options.AreDevToolsEnabled = false;
            options.IsStatusBarEnabled = false;
            options.IsZoomControlEnabled = false;
            options.AreBrowserAcceleratorKeysEnabled = false;

            core.NavigationStarting += OnNavigationStarting;
            core.NewWindowRequested += OnNewWindowRequested;
            core.DownloadStarting += (_, args) => args.Cancel = true;
            core.LaunchingExternalUriScheme += OnLaunchingExternalUriScheme;
            core.PermissionRequested += (_, args) => args.State = CoreWebView2PermissionState.Deny;
            core.SourceChanged += (_, _) => _status.Text = "שירות רב־קו";
            core.ProcessFailed += (_, _) => _status.Text = "הדפדפן נתקל בתקלה. סגור ופתח את התוכנה מחדש.";

            if (!Uri.TryCreate(_settings.StartUrl, UriKind.Absolute, out var start) ||
                start.Scheme != Uri.UriSchemeHttps || !_settings.IsAllowedHttpsHost(start.Host))
            {
                _status.Text = "הגדרת כתובת הפתיחה אינה מאושרת.";
                return;
            }
            core.Navigate(start.ToString());
        }
        catch (Exception ex)
        {
            _status.Text = "לא ניתן להפעיל את רכיב הדפדפן: " + ex.Message;
        }
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var target) ||
            target.Scheme != Uri.UriSchemeHttps || !_settings.IsAllowedHttpsHost(target.Host))
        {
            e.Cancel = true;
            _status.Text = "המעבר נחסם: הכתובת אינה מאושרת.";
        }
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var target) &&
            target.Scheme == Uri.UriSchemeHttps && _settings.IsAllowedHttpsHost(target.Host))
            _browser.CoreWebView2.Navigate(target.ToString());
        else
            _status.Text = "חלון חיצוני נחסם.";
    }

    private void OnLaunchingExternalUriScheme(object? sender, CoreWebView2LaunchingExternalUriSchemeEventArgs e)
    {
        if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var target) ||
            !_settings.IsAllowedExternalScheme(target.Scheme))
        {
            e.Cancel = true;
            _status.Text = "הפעלת תוכנה חיצונית נחסמה עד לאישור פרוטוקול קורא הכרטיסים.";
        }
    }
}
