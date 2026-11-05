using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace RavKavKiosk;

internal sealed class MainForm : Form
{
    private readonly AppSettings _settings = AppSettings.Load();
    private readonly WebView2 _browser = new() { Dock = DockStyle.Fill, Visible = false };
    private readonly Panel _home = new() { Dock = DockStyle.Fill, BackColor = Color.FromArgb(246, 249, 252) };
    private readonly Label _status = new()
    {
        AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
        Font = new Font("Segoe UI", 9), ForeColor = Color.FromArgb(91, 107, 123),
        Text = "השירות ייפתח בתוך התוכנה"
    };
    private readonly Button _start = new();
    private readonly Button _back = new();
    private bool _browserInitialized;

    private static readonly Color Navy = Color.FromArgb(18, 48, 78);
    private static readonly Color Blue = Color.FromArgb(0, 112, 192);

    public MainForm()
    {
        Text = "עמדת טעינה HopOn";
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(760, 560);
        BackColor = Color.White;
        Font = new Font("Segoe UI", 10);

        var header = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = Navy, Padding = new Padding(24, 0, 24, 0) };
        var brand = new Label
        {
            Text = "HopOn | עמדת טעינה", Dock = DockStyle.Right, Width = 330,
            ForeColor = Color.White, Font = new Font("Segoe UI", 19, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight
        };
        _back.Text = "חזרה למסך הראשי";
        _back.Dock = DockStyle.Left;
        _back.Width = 170;
        _back.FlatStyle = FlatStyle.Flat;
        _back.FlatAppearance.BorderColor = Color.FromArgb(100, 150, 190);
        _back.ForeColor = Color.White;
        _back.BackColor = Navy;
        _back.Visible = false;
        _back.Click += (_, _) => ShowHome();
        header.Controls.Add(brand);
        header.Controls.Add(_back);

        var footer = new Panel { Dock = DockStyle.Bottom, Height = 42, BackColor = Color.FromArgb(235, 241, 247), Padding = new Padding(12, 0, 12, 0) };
        footer.Controls.Add(_status);

        Controls.Add(_home);
        Controls.Add(_browser);
        Controls.Add(footer);
        Controls.Add(header);
        BuildHome();
    }

    private void BuildHome()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5,
            Padding = new Padding(32, 26, 32, 24), BackColor = _home.BackColor
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 22));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 22));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 56));

        var title = new Label
        {
            Text = "טעינת כרטיס רב־קו", Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 30, FontStyle.Bold), ForeColor = Navy,
            TextAlign = ContentAlignment.BottomCenter
        };
        var subtitle = new Label
        {
            Text = "טוענים בקלות, בכמה צעדים פשוטים", Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 16), ForeColor = Color.FromArgb(83, 101, 120),
            TextAlign = ContentAlignment.TopCenter
        };
        var steps = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = Color.Transparent };
        for (var i = 0; i < 3; i++) steps.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
        steps.Controls.Add(MakeStep("1", "מחברים קורא USB"), 0, 0);
        steps.Controls.Add(MakeStep("2", "מצמידים את הכרטיס"), 1, 0);
        steps.Controls.Add(MakeStep("3", "בוחרים טעינה ומשלמים"), 2, 0);

        _start.Text = "התחלת טעינה";
        _start.Dock = DockStyle.Fill;
        _start.Margin = new Padding(120, 12, 120, 12);
        _start.FlatStyle = FlatStyle.Flat;
        _start.FlatAppearance.BorderSize = 0;
        _start.BackColor = Blue;
        _start.ForeColor = Color.White;
        _start.Font = new Font("Segoe UI", 23, FontStyle.Bold);
        _start.Cursor = Cursors.Hand;
        _start.Click += async (_, _) => await OpenOfficialServiceAsync();

        var note = new Label
        {
            Text = "הטעינה מתבצעת בשירות HopOn הרשמי.\r\nהתוכנה מאפשרת גישה לעמוד הטעינה בלבד.",
            Dock = DockStyle.Fill, Font = new Font("Segoe UI", 11),
            ForeColor = Color.FromArgb(105, 119, 133), TextAlign = ContentAlignment.TopCenter
        };

        layout.Controls.Add(title, 0, 0);
        layout.Controls.Add(subtitle, 0, 1);
        layout.Controls.Add(steps, 0, 2);
        layout.Controls.Add(_start, 0, 3);
        layout.Controls.Add(note, 0, 4);
        _home.Controls.Add(layout);
    }

    private static Control MakeStep(string number, string caption)
    {
        var panel = new Panel { Dock = DockStyle.Fill, Margin = new Padding(10), BackColor = Color.White, Padding = new Padding(12) };
        var numberLabel = new Label
        {
            Text = number, Dock = DockStyle.Top, Height = 46, ForeColor = Blue,
            Font = new Font("Segoe UI", 24, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter
        };
        var textLabel = new Label
        {
            Text = caption, Dock = DockStyle.Fill, ForeColor = Navy,
            Font = new Font("Segoe UI", 13, FontStyle.Bold), TextAlign = ContentAlignment.TopCenter
        };
        panel.Controls.Add(textLabel);
        panel.Controls.Add(numberLabel);
        return panel;
    }

    private async Task OpenOfficialServiceAsync()
    {
        _start.Enabled = false;
        _status.Text = "מתחבר לשירות HopOn...";
        _home.Visible = false;
        _browser.Visible = true;
        _back.Visible = true;
        try
        {
            if (!_browserInitialized)
            {
                await _browser.EnsureCoreWebView2Async();
                var core = _browser.CoreWebView2;
                core.Settings.AreDefaultContextMenusEnabled = false;
                core.Settings.AreDevToolsEnabled = false;
                core.Settings.IsStatusBarEnabled = false;
                core.Settings.IsZoomControlEnabled = false;
                core.Settings.AreBrowserAcceleratorKeysEnabled = false;
                core.NavigationStarting += OnNavigationStarting;
                core.HistoryChanged += (_, _) => EnforceCurrentPage();
                core.NewWindowRequested += OnNewWindowRequested;
                core.DownloadStarting += (_, args) => args.Cancel = true;
                core.LaunchingExternalUriScheme += OnLaunchingExternalUriScheme;
                core.PermissionRequested += (_, args) => args.State = CoreWebView2PermissionState.Deny;
                core.SourceChanged += (_, _) => _status.Text = "שירות HopOn";
                core.ProcessFailed += (_, _) => _status.Text = "אירעה תקלה. חזור ונסה שוב.";
                _browserInitialized = true;
            }

            if (!Uri.TryCreate(_settings.StartUrl, UriKind.Absolute, out var start) ||
                !_settings.IsAllowedNavigationUri(start) ||
                _settings.IsBlockedNavigationUri(start))
                throw new InvalidOperationException("כתובת השירות אינה מאושרת בהגדרות.");

            _browser.CoreWebView2.Navigate(start.ToString());
        }
        catch (Exception ex)
        {
            _status.Text = "לא ניתן לפתוח את השירות: " + ex.Message;
            ShowHome();
        }
        finally
        {
            _start.Enabled = true;
        }
    }

    private void EnforceCurrentPage()
    {
        if (_browser.CoreWebView2 is null) return;
        if (!Uri.TryCreate(_browser.CoreWebView2.Source, UriKind.Absolute, out var current) ||
            !_settings.IsAllowedNavigationUri(current) ||
            _settings.IsBlockedNavigationUri(current))
        {
            _status.Text = "העמוד הזה אינו זמין בעמדת הטעינה.";
            _browser.CoreWebView2.Navigate(_settings.StartUrl);
        }
    }

    private void ShowHome()
    {
        _browser.Visible = false;
        _home.Visible = true;
        _back.Visible = false;
        _status.Text = "מוכן לטעינה";
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var target) ||
            !_settings.IsAllowedNavigationUri(target) ||
            _settings.IsBlockedNavigationUri(target))
        {
            e.Cancel = true;
            _status.Text = "העמוד המבוקש אינו זמין בעמדה זו.";
        }
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var target) &&
            _settings.IsAllowedNavigationUri(target) &&
            !_settings.IsBlockedNavigationUri(target))
            _browser.CoreWebView2.Navigate(target.ToString());
        else
            _status.Text = "חלון חיצוני נחסם.";
    }

    private void OnLaunchingExternalUriScheme(object? sender, CoreWebView2LaunchingExternalUriSchemeEventArgs e)
    {
        e.Cancel = true;
        _status.Text = "קישור חיצוני אינו מורשה בעמדה זו.";
    }
}
