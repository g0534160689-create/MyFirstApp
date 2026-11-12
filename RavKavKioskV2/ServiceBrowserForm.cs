using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Diagnostics;

namespace RavKavKioskV2;

internal sealed class ServiceBrowserForm : Form
{
    private readonly ServiceKind _service;
    private readonly WebView2 _web = new() { Dock = DockStyle.Fill };
    private readonly Label _status = new();
    private readonly Button _back = new();
    private readonly Button _home = new();
    private bool _initialized;
    private bool _closing;

    public ServiceBrowserForm(ServiceKind service)
    {
        _service = service;
        Text = "טעינת רב־קו — " + ServicePolicy.DisplayName(service);
        StartPosition = FormStartPosition.CenterParent;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(900, 620);
        BackColor = Color.FromArgb(15, 28, 45);
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;

        var toolbar = new Panel { Dock = DockStyle.Top, Height = 54, BackColor = Color.FromArgb(20, 39, 60) };
        _back.Text = "חזרה";
        _back.Width = 100;
        _back.Dock = DockStyle.Right;
        _back.Click += (_, _) =>
        {
            if (_initialized && _web.CoreWebView2.CanGoBack) _web.CoreWebView2.GoBack();
            else Close();
        };
        _home.Text = "רענון";
        _home.Width = 100;
        _home.Dock = DockStyle.Right;
        _home.Click += (_, _) => { if (_initialized) _web.CoreWebView2.Reload(); };
        _status.Dock = DockStyle.Fill;
        _status.TextAlign = ContentAlignment.MiddleRight;
        _status.ForeColor = Color.White;
        _status.Padding = new Padding(12, 0, 12, 0);
        _status.Text = "מתחבר ל-" + ServicePolicy.DisplayName(service) + "...";
        toolbar.Controls.Add(_status);
        toolbar.Controls.Add(_home);
        toolbar.Controls.Add(_back);
        Controls.Add(_web);
        Controls.Add(toolbar);
        Shown += async (_, _) => await InitializeBrowserAsync();
        FormClosing += (_, _) => _closing = true;
    }

    private async Task InitializeBrowserAsync()
    {
        try
        {
            var folder = ServicePolicy.ProfileFolder(_service);
            Directory.CreateDirectory(folder);
            var environment = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null, userDataFolder: folder);
            await _web.EnsureCoreWebView2Async(environment);

            var core = _web.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.Settings.AreDefaultScriptDialogsEnabled = true;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsZoomControlEnabled = true;
            core.Settings.AreBrowserAcceleratorKeysEnabled = true;
            core.Settings.IsPasswordAutosaveEnabled = false;
            core.Settings.IsGeneralAutofillEnabled = true;

            // Deliberately no injected JavaScript, no blanket resource filter,
            // and no route interception in the document's own history/form layer.
            core.NavigationStarting += OnNavigationStarting;
            core.FrameNavigationStarting += OnFrameNavigationStarting;
            core.NewWindowRequested += OnNewWindowRequested;
            core.LaunchingExternalUriScheme += OnLaunchingExternalUriScheme;
            core.NavigationCompleted += (_, e) =>
            {
                if (e.IsSuccess)
                    _status.Text = "מחובר ל-" + ServicePolicy.DisplayName(_service);
                else
                {
                    _status.Text = "טעינת האתר נכשלה: " + e.WebErrorStatus;
                    Diagnostics.Write("Navigation failed", details: _service + " | " + e.WebErrorStatus + " | " + core.Source);
                }
            };
            core.ProcessFailed += (_, e) =>
            {
                _status.Text = "רכיב הדפדפן נעצר. סגור ופתח מחדש.";
                Diagnostics.Write("WebView2 process failed", details: _service + " | " + e.ProcessFailedKind);
            };
            core.PermissionRequested += (_, e) =>
            {
                e.State = CoreWebView2PermissionState.Deny;
                Diagnostics.Write("Web permission denied", details: _service + " | " + e.PermissionKind + " | " + e.Uri);
            };
            core.DownloadStarting += (_, e) =>
            {
                // Keep the standard WebView2 download UI. Downloads are not silently discarded.
                _status.Text = "מתבצעת הורדה...";
            };

            _initialized = true;
            Diagnostics.Write("Browser ready", details: _service + " | profile=" + folder);
            core.Navigate(ServicePolicy.StartUrl(_service));
        }
        catch (Exception ex)
        {
            Diagnostics.Write("Initialize browser", ex, _service.ToString());
            _status.Text = "לא ניתן להפעיל את הדפדפן: " + ex.Message;
            MessageBox.Show(this,
                "הפעלת הדפדפן נכשלה. פרטי התקלה נשמרו כאן:\r\n" + Diagnostics.LogPath +
                "\r\n\r\n" + ex.Message,
                "שגיאה", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var protocol) && ServicePolicy.IsReaderProtocol(protocol))
        {
            _status.Text = "ממתין לאישור הפעלת קורא הכרטיסים...";
            Diagnostics.Write("Reader protocol requested", details: _service + " | " + protocol.Scheme);
            return; // LaunchingExternalUriScheme performs trusted-origin validation and consent.
        }

        if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) ||
            !ServicePolicy.IsAllowedWebUri(uri, _service))
        {
            e.Cancel = true;
            _status.Text = "הניווט נחסם לפי הרשימה השחורה.";
            Diagnostics.Write("Navigation denied", details: _service + " | " + e.Uri);
        }
        else
        {
            Diagnostics.Write("Navigation", details: _service + " | " + uri.GetLeftPart(UriPartial.Authority) + uri.AbsolutePath);
        }
    }

    private void OnFrameNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var protocol) && ServicePolicy.IsReaderProtocol(protocol))
        {
            // Do not cancel the reader's custom protocol when it is launched from a hidden iframe.
            Diagnostics.Write("Reader protocol requested from frame", details: _service + " | " + protocol.Scheme);
            return;
        }
        if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) ||
            !ServicePolicy.IsAllowedWebUri(uri, _service))
        {
            e.Cancel = true;
            Diagnostics.Write("Frame navigation denied", details: _service + " | " + e.Uri);
        }
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var protocol) && ServicePolicy.IsReaderProtocol(protocol))
        {
            e.Handled = true;
            _web.CoreWebView2.Navigate(e.Uri);
            return;
        }
        if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) &&
            ServicePolicy.IsAllowedWebUri(uri, _service))
        {
            // Leave the request unhandled: WebView2 creates a real popup so that
            // opener/postMessage flows for CAPTCHA and 3-D Secure keep working.
            Diagnostics.Write("Popup allowed", details: _service + " | " + uri.GetLeftPart(UriPartial.Authority));
            return;
        }
        e.Handled = true;
        Diagnostics.Write("Popup denied", details: _service + " | " + e.Uri);
    }

    private void OnLaunchingExternalUriScheme(object? sender, CoreWebView2LaunchingExternalUriSchemeEventArgs e)
    {
        e.Cancel = true;
        if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) ||
            !ServicePolicy.IsReaderProtocol(uri) ||
            !ServicePolicy.IsTrustedReaderOrigin(_web.CoreWebView2.Source, _service))
        {
            _status.Text = "בקשת הפעלת תוכנה חיצונית נדחתה.";
            Diagnostics.Write("External protocol denied", details: _service + " | " + e.Uri + " | source=" + _web.CoreWebView2.Source);
            return;
        }

        var answer = MessageBox.Show(this,
            "האתר מבקש להפעיל את תוכנת קורא הכרטיסים המותקנת במחשב.\r\n\r\n" +
            "יש לאשר רק אם חיברת קורא רב־קו ואתה נמצא בתהליך הטענה.\r\n\r\n" +
            "להפעיל את קורא הכרטיסים?",
            "אישור הפעלת קורא הכרטיסים",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes)
        {
            _status.Text = "הפעלת הקורא בוטלה.";
            Diagnostics.Write("Reader launch declined", details: _service);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(e.Uri) { UseShellExecute = true });
            _status.Text = "בקשת הפעלת הקורא נשלחה ל-Windows.";
            Diagnostics.Write("Reader launch approved", details: _service + " | " + uri.Scheme);
        }
        catch (Exception ex)
        {
            _status.Text = "לא נמצאה תוכנת קורא מתאימה במחשב.";
            Diagnostics.Write("Reader launch failed", ex, _service + " | " + e.Uri);
            MessageBox.Show(this,
                "Windows לא הצליח להפעיל את תוכנת הקורא. בדוק שהיא מותקנת ורשומה במערכת.\r\n\r\n" + ex.Message,
                "תוכנת הקורא לא הופעלה", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        if (!_closing) _closing = true;
        _web.Dispose();
        base.OnFormClosed(e);
    }
}
