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
    private readonly Button _ravKavStart = new();
    private string _activeStartUrl = "https://hopon.co.il/read";
    private string _activeServiceName = "HopOn";
    private readonly Button _back = new();
    private CoreWebView2Environment? _webViewEnvironment;
    private bool _browserInitialized;

    private static readonly Color Navy = Color.FromArgb(18, 48, 78);
    private static readonly Color Blue = Color.FromArgb(0, 112, 192);

    public MainForm()
    {
        Text = "פורטל אוצר החכמה";
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
            Text = "אוצר החכמה | פורטל הישיבה", Dock = DockStyle.Right, Width = 430,
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

    private sealed record PortalLink(string Id, string Title, string Subtitle, string Url, string Category, string Badge);

    private static readonly PortalLink[] PortalLinks =
    [
        new("torah-dileyh", "תורה דיליה", "סיכומי סוגיות וכתיבת חבורות", "https://www.matara.pro/nedarimplus/online/?mosad=7005288", "tests", "נפוץ"),
        new("print-charge", "הטענת הדפסות", "טעינת יתרת צילום והדפסה מהירה", "https://www.matara.pro/nedarimplus/Forms/4488.html?MasofId=&ClientId=&Zeout=&Version=1", "services", "שירות"),
        new("dirshu", "מבחני דרשו", "ציונים, מועדי בחינות והרשמה", "https://matara.pro/nedarimplus/online/?mosad=7005954", "tests", "דרשו"),
        new("yeshiva-kivro", "ישיבה על קברו", "מפעל הלימוד וההנצחה העולמי", "https://www.matara.pro/nedarimplus/v6/Mokirim14.html?Version=1", "learning", "מוקירים"),
        new("ladaat", "מבחני לדעת", "מגוון מבחנים", "https://www.matara.pro/nedarimplus/Forms/976.html?MasofId=&ClientId=&Zeout=&Version=8", "tests", "מבחנים"),
        new("siach-hatorah", "שיח התורה", "מבחנים בכל סגנונות התורה", "https://rishumon.net/SiachAtorah.html", "learning", "אוצר"),
        new("torah-shebichtav", "תורה שבכתב", "עיון בטקסטים, מפרשים ודקדוק", "https://tora.irom.co.il/?Version=17", "learning", "מאגר")
    ];

    private void BuildHome()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6,
            Padding = new Padding(28, 10, 28, 12), BackColor = _home.BackColor
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));

        var logo = new PictureBox
        {
            Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, 2)
        };
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
            Text = "פורטל אוצר החכמה", Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 25, FontStyle.Bold),
            ForeColor = Navy, TextAlign = ContentAlignment.MiddleCenter
        };
        var subtitle = new Label
        {
            Text = "ישיבת דעת אהרן", Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 17, FontStyle.Bold),
            ForeColor = Color.FromArgb(91, 29, 53),
            TextAlign = ContentAlignment.MiddleCenter
        };
        var intro = new Label
        {
            Text = "כל הקישורים והשירותים במקום אחד", Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 12),
            ForeColor = Color.FromArgb(83, 101, 120),
            TextAlign = ContentAlignment.MiddleCenter
        };

        var cards = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoScroll = true, WrapContents = true,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = Color.Transparent, Padding = new Padding(8, 5, 8, 5)
        };
        foreach (var link in PortalLinks)
        {
            var card = new Button
            {
                Width = 330, Height = 88, Margin = new Padding(9),
                Text = $"{link.Title}\r\n{link.Subtitle}   •   {link.Badge}",
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = Navy,
                BackColor = link.Category switch
                {
                    "tests" => Color.FromArgb(255, 248, 225),
                    "services" => Color.FromArgb(225, 247, 239),
                    _ => Color.FromArgb(237, 244, 252)
                },
                FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false, Tag = link
            };
            card.FlatAppearance.BorderColor = Color.FromArgb(210, 222, 234);
            card.FlatAppearance.BorderSize = 1;
            card.Click += async (_, _) =>
            {
                if (card.Tag is PortalLink selected)
                    await OpenOfficialServiceAsync(selected.Url, selected.Title);
            };
            cards.Controls.Add(card);
        }

        var services = new Panel
        {
            Dock = DockStyle.Fill, BackColor = Color.FromArgb(235, 241, 247),
            Padding = new Padding(12, 7, 12, 7)
        };
        var serviceTitle = new Label
        {
            Text = "שירותים נוספים — הטענת רב־קו", Dock = DockStyle.Top,
            Height = 28, Font = new Font("Segoe UI", 11, FontStyle.Bold),
            ForeColor = Navy, TextAlign = ContentAlignment.MiddleCenter
        };
        var serviceButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false, BackColor = Color.Transparent
        };

        _start.Text = "טעינה באמצעות HopOn";
        StyleServiceButton(_start, Blue);
        _start.Width = 250; _start.Height = 42;
        _start.Margin = new Padding(8, 2, 8, 2);
        _start.Click += async (_, _) => await OpenOfficialServiceAsync(_settings.StartUrl, "HopOn");

        _ravKavStart.Text = "טעינה באמצעות רב-קו אונליין";
        StyleServiceButton(_ravKavStart, Color.FromArgb(35, 125, 88));
        _ravKavStart.Width = 270; _ravKavStart.Height = 42;
        _ravKavStart.Margin = new Padding(8, 2, 8, 2);
        _ravKavStart.Click += async (_, _) => await OpenOfficialServiceAsync(_settings.RavKavStartUrl, "רב-קו אונליין");

        serviceButtons.Controls.Add(_start);
        serviceButtons.Controls.Add(_ravKavStart);
        services.Controls.Add(serviceButtons);
        services.Controls.Add(serviceTitle);

        layout.Controls.Add(logo, 0, 0);
        layout.Controls.Add(title, 0, 1);
        layout.Controls.Add(subtitle, 0, 2);
        layout.Controls.Add(intro, 0, 3);
        layout.Controls.Add(cards, 0, 4);
        layout.Controls.Add(services, 0, 5);
        _home.Controls.Add(layout);
    }

    private static void StyleServiceButton(Button button, Color background)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = background;
        button.ForeColor = Color.White;
        button.Font = new Font("Segoe UI", 11, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
        button.UseVisualStyleBackColor = false;
    }

    private async Task OpenOfficialServiceAsync(string startUrl, string serviceName)
    {
        _start.Enabled = false;
        _ravKavStart.Enabled = false;
        _activeStartUrl = startUrl;
        _activeServiceName = serviceName;
        _status.Text = $"מתחבר לשירות {serviceName}...";
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

                await core.AddScriptToExecuteOnDocumentCreatedAsync(BuildRouteGuardScript(_settings.StartUrl));
                await core.AddScriptToExecuteOnDocumentCreatedAsync(BuildRouteGuardScript(_settings.RavKavStartUrl));
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
                        _status.Text = $"שירות {_activeServiceName}";
                };
                core.NavigationCompleted += (_, args) =>
                {
                    if (!args.IsSuccess)
                    {
                        var error = new InvalidOperationException(
                            $"טעינת {_activeServiceName} נכשלה: {args.WebErrorStatus}");
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

            if (!Uri.TryCreate(startUrl, UriKind.Absolute, out var start) ||
                !_settings.IsAllowedNavigationUri(start) ||
                _settings.IsBlockedNavigationUri(start))
                throw new InvalidOperationException("כתובת השירות אינה מאושרת בהגדרות.");

            _browser.CoreWebView2.Navigate(start.ToString());
        }
        catch (Exception ex)
        {
            AppDiagnostics.Write("OpenOfficialServiceAsync", ex);
            ShowHome();
            _status.Text = $"לא ניתן לפתוח את שירות {serviceName}.";
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
            _ravKavStart.Enabled = true;
        }
    }

    private void EnforceCurrentPage()
    {
        if (_browser.CoreWebView2 is null) return;
        if (!Uri.TryCreate(_browser.CoreWebView2.Source, UriKind.Absolute, out var current) ||
            !_settings.IsAllowedNavigationUri(current) ||
            (_settings.IsBlockedNavigationUri(current) && !_settings.IsRavKavUri(current)))
        {
            _status.Text = "העמוד הזה אינו זמין בעמדת הטעינה.";
            _browser.CoreWebView2.Navigate(_activeStartUrl);
        }
    }

    private string BuildRouteGuardScript(string serviceStartUrl)
    {
        var serviceUri = new Uri(serviceStartUrl);
        var origin = serviceUri.GetLeftPart(UriPartial.Authority);
        var originJson = JsonSerializer.Serialize(origin);
        var strictPath = _settings.IsRavKavUri(serviceUri);
        var strictPathJson = JsonSerializer.Serialize(strictPath);
        var allowedPathJson = JsonSerializer.Serialize(_settings.RavKavAllowedPath);
        var blockedPathsJson = JsonSerializer.Serialize(_settings.BlockedPaths);

        var script = """
        (() => {
            "use strict";
            if (window.top !== window) return;
            const allowedOrigin = __ALLOWED_ORIGIN_JSON__;
            const strictPath = __STRICT_PATH__;
            const allowedPath = __ALLOWED_PATH_JSON__;
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
                if (strictPath) return path !== allowedPath.toLowerCase();
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
                    if (!strictPath && url != null && isBlocked(url)) {
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
                if (!strictPath && form instanceof HTMLFormElement && isBlocked(form.action)) {
                    event.preventDefault();
                    event.stopImmediatePropagation();
                    event.stopPropagation();
                    notifyBlocked();
                }
            }, true);

            // Defensive fallback for a blocked entry already present in history.
            window.addEventListener("popstate", () => {
                if (!strictPath && isBlocked(location.href)) {
                    history.forward();
                    notifyBlocked();
                }
            }, true);
        })();
        """;

        return script
            .Replace("__ALLOWED_ORIGIN_JSON__", originJson, StringComparison.Ordinal)
            .Replace("__STRICT_PATH__", strictPathJson, StringComparison.Ordinal)
            .Replace("__ALLOWED_PATH_JSON__", allowedPathJson, StringComparison.Ordinal)
            .Replace("__BLOCKED_PATHS_JSON__", blockedPathsJson, StringComparison.Ordinal);
    }

    private void OnFrameNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var target) &&
            _settings.IsHopOnUri(target) &&
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
            (_settings.IsBlockedNavigationUri(target) && !_settings.IsRavKavUri(target)))
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
            (!_settings.IsBlockedNavigationUri(target) || _settings.IsRavKavUri(target)))
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
