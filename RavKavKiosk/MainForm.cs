using System.Reflection;
using System.Text.Json;
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
    private CoreWebView2Environment? _webViewEnvironment;
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
            Text = "אוצר החכמה | עמדת טעינה", Dock = DockStyle.Right, Width = 390,
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
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            Padding = new Padding(24, 10, 24, 12),
            BackColor = _home.BackColor
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 180));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 95));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var logo = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 4)
        };

        // The logo is embedded in the EXE, so no separate image file is needed.
        using (var logoStream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("RavKavKiosk.Assets.daat-logo.png"))
        {
            if (logoStream is not null)
            {
                using var sourceImage = Image.FromStream(logoStream);
                logo.Image = new Bitmap(sourceImage);
            }
        }

        var title = new Label
        {
            Text = "אוצר החכמה",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 25, FontStyle.Bold),
            ForeColor = Navy,
            TextAlign = ContentAlignment.MiddleCenter
        };
        var subtitle = new Label
        {
            Text = "ישיבת דעת אהרן",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 19, FontStyle.Bold),
            ForeColor = Color.FromArgb(91, 29, 53),
            TextAlign = ContentAlignment.MiddleCenter
        };
        var welcomeText = new Label
        {
            Text = "להטעין את הרב־קו בלי לצאת מהישיבה",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 17, FontStyle.Bold),
            ForeColor = Color.FromArgb(83, 101, 120),
            TextAlign = ContentAlignment.MiddleCenter
        };

        var steps = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent
        };
        for (var i = 0; i < 3; i++)
            steps.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
        steps.Controls.Add(MakeStep("1", "מחברים קורא USB"), 0, 0);
        steps.Controls.Add(MakeStep("2", "מצמידים את הכרטיס"), 1, 0);
        steps.Controls.Add(MakeStep("3", "בוחרים טעינה ומשלמים"), 2, 0);

        _start.Text = "התחלת טעינה";
        _start.Dock = DockStyle.Fill;
        _start.Margin = new Padding(120, 8, 120, 8);
        _start.FlatStyle = FlatStyle.Flat;
        _start.FlatAppearance.BorderSize = 0;
        _start.BackColor = Blue;
        _start.ForeColor = Color.White;
        _start.Font = new Font("Segoe UI", 21, FontStyle.Bold);
        _start.Cursor = Cursors.Hand;
        _start.Click += async (_, _) => await OpenOfficialServiceAsync();

        var note = new Label
        {
            Text = "הטעינה מתבצעת בשירות HopOn הרשמי",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10),
            ForeColor = Color.FromArgb(105, 119, 133),
            TextAlign = ContentAlignment.TopCenter
        };

        layout.Controls.Add(logo, 0, 0);
        layout.Controls.Add(title, 0, 1);
        layout.Controls.Add(subtitle, 0, 2);
        layout.Controls.Add(welcomeText, 0, 3);
        layout.Controls.Add(steps, 0, 4);
        layout.Controls.Add(_start, 0, 5);
        layout.Controls.Add(note, 0, 6);
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
                // Keep one WebView2 environment for this app and store its profile
                // under the current user's LocalAppData (not beside the EXE).
                var userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "RavKavKiosk",
                    "WebView2");
                Directory.CreateDirectory(userDataFolder);

                _webViewEnvironment ??= await CoreWebView2Environment.CreateAsync(
                    browserExecutableFolder: null,
                    userDataFolder: userDataFolder);

                await _browser.EnsureCoreWebView2Async(_webViewEnvironment);
                var core = _browser.CoreWebView2;
                core.Settings.AreDefaultContextMenusEnabled = false;
                core.Settings.AreDevToolsEnabled = false;
                core.Settings.IsStatusBarEnabled = false;
                core.Settings.IsZoomControlEnabled = false;
                core.Settings.AreBrowserAcceleratorKeysEnabled = false;
                // Install every blocking layer before the first page navigation.
                core.NavigationStarting += OnNavigationStarting;
                core.FrameNavigationStarting += OnFrameNavigationStarting;
                core.HistoryChanged += (_, _) => EnforceCurrentPage();
                core.NewWindowRequested += OnNewWindowRequested;

                core.AddWebResourceRequestedFilter(
                    "https://hopon.co.il/*",
                    CoreWebView2WebResourceContext.Document,
                    CoreWebView2WebResourceRequestSourceKinds.All);
                core.WebResourceRequested += OnWebResourceRequested;

                await core.AddScriptToExecuteOnDocumentCreatedAsync(BuildRouteGuardScript());
                core.DownloadStarting += (_, args) => args.Cancel = true;
                core.LaunchingExternalUriScheme += OnLaunchingExternalUriScheme;
                core.PermissionRequested += (_, args) => args.State = CoreWebView2PermissionState.Deny;
                core.SourceChanged += (_, _) =>
                {
                    EnforceCurrentPage();
                    if (_browser.CoreWebView2 is not null &&
                        Uri.TryCreate(_browser.CoreWebView2.Source, UriKind.Absolute, out var current) &&
                        _settings.IsAllowedNavigationUri(current) &&
                        !_settings.IsBlockedNavigationUri(current))
                        _status.Text = "שירות HopOn";
                };
                core.NavigationCompleted += (_, args) =>
                {
                    if (!args.IsSuccess)
                    {
                        var error = new InvalidOperationException(
                            $"טעינת HopOn נכשלה: {args.WebErrorStatus}");
                        AppDiagnostics.Write("NavigationCompleted", error);
                        _status.Text = $"הטעינה נכשלה: {args.WebErrorStatus}";
                    }
                };
                core.ProcessFailed += (_, args) =>
                {
                    var error = new InvalidOperationException(
                        $"WebView2 process failed: {args.ProcessFailedKind}");
                    AppDiagnostics.Write("WebView2 ProcessFailed", error);
                    _status.Text = "רכיב הדפדפן הפסיק לפעול. חזור ונסה שוב.";
                };
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
            AppDiagnostics.Write("OpenOfficialServiceAsync", ex);
            ShowHome();
            _status.Text = "לא ניתן לפתוח את שירות HopOn.";
            MessageBox.Show(
                this,
                "פתיחת שירות הטעינה נכשלה.\r\n" +
                ex.Message + "\r\n\r\n" +
                "פרטי התקלה נשמרו בקובץ:\r\n" + AppDiagnostics.LogFilePath,
                "שגיאה בהפעלת עמדת הטעינה",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
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

    private string BuildRouteGuardScript()
    {
        var origin = new Uri(_settings.StartUrl).GetLeftPart(UriPartial.Authority);
        var originJson = JsonSerializer.Serialize(origin);
        var blockedPathsJson = JsonSerializer.Serialize(_settings.BlockedPaths);

        var script = """
        (() => {
            "use strict";
            const allowedOrigin = __ALLOWED_ORIGIN_JSON__;
            const blockedRoots = __BLOCKED_PATHS_JSON__.map(p =>
                String(p).replace(/\\/g, "/").replace(/\/+$/g, "").toLowerCase()
            );

            function normalizedPath(value) {
                try {
                    const url = new URL(String(value), location.href);
                    if (url.origin !== allowedOrigin) return null;

                    let path = url.pathname;
                    for (let i = 0; i < 10; i++) {
                        let decoded;
                        try { decoded = decodeURIComponent(path); }
                        catch { return "__BLOCK_INVALID__"; }
                        if (decoded === path) break;
                        path = decoded;
                    }
                    if (path.indexOf("%") !== -1) return "__BLOCK_INVALID__";

                    path = path.replace(/\\/g, "/").replace(/\/{2,}/g, "/");
                    const parts = [];
                    for (const part of path.split("/")) {
                        if (!part || part === ".") continue;
                        if (part === "..") { parts.pop(); continue; }
                        parts.push(part);
                    }
                    return ("/" + parts.join("/")).toLowerCase().replace(/\/+$/g, "") || "/";
                } catch {
                    return null;
                }
            }

            function isBlocked(value) {
                const path = normalizedPath(value);
                if (path === "__BLOCK_INVALID__") return true;
                if (path === null) return false;
                return blockedRoots.some(root =>
                    path === root ||
                    path.startsWith(root + "/") ||
                    path.startsWith(root + ";") ||
                    path.startsWith(root + "?") ||
                    path.startsWith(root + "#")
                );
            }

            function notifyBlocked() {
                try { window.chrome.webview.postMessage("kiosk-blocked-route"); } catch {}
            }

            // Stop SPA routers before they can change the visible URL.
            for (const method of ["pushState", "replaceState"]) {
                const original = history[method];
                history[method] = function(state, title, url) {
                    if (url != null && isBlocked(url)) {
                        notifyBlocked();
                        return;
                    }
                    return Reflect.apply(original, this, arguments);
                };
            }

            // Stop ordinary links and form submissions in the capture phase.
            document.addEventListener("click", event => {
                const element = event.target instanceof Element ? event.target : null;
                const link = element?.closest("a[href]");
                if (link && isBlocked(link.href)) {
                    event.preventDefault();
                    event.stopImmediatePropagation();
                    event.stopPropagation();
                    notifyBlocked();
                }
            }, true);

            document.addEventListener("submit", event => {
                const form = event.target;
                if (form instanceof HTMLFormElement && isBlocked(form.action)) {
                    event.preventDefault();
                    event.stopImmediatePropagation();
                    event.stopPropagation();
                    notifyBlocked();
                }
            }, true);

            // Defensive fallback for a blocked entry already present in history.
            window.addEventListener("popstate", () => {
                if (isBlocked(location.href)) {
                    history.forward();
                    notifyBlocked();
                }
            }, true);
        })();
        """;

        return script
            .Replace("__ALLOWED_ORIGIN_JSON__", originJson, StringComparison.Ordinal)
            .Replace("__BLOCKED_PATHS_JSON__", blockedPathsJson, StringComparison.Ordinal);
    }

    private void OnFrameNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var target) &&
            _settings.IsAllowedNavigationUri(target) &&
            _settings.IsBlockedNavigationUri(target))
        {
            e.Cancel = true;
            _status.Text = "העמוד המבוקש חסום בעמדה זו.";
        }
    }

    private void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        if (!Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var target) ||
            !_settings.IsAllowedNavigationUri(target) ||
            !_settings.IsBlockedNavigationUri(target))
            return;

        // A second network-level barrier: never pass a blocked HTML document to Chromium.
        e.Response = _browser.CoreWebView2.Environment.CreateWebResourceResponse(
            new MemoryStream(Array.Empty<byte>()),
            403,
            "Blocked by kiosk policy",
            "Content-Type: text/plain; charset=utf-8\r\nCache-Control: no-store");
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
            _status.Text = "העמוד המבוקש חסום בעמדה זו.";
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
