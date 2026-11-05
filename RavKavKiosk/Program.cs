using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace RavKavKiosk;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        if (args.Any(arg => string.Equals(arg, "--smoke-test", StringComparison.OrdinalIgnoreCase)))
        {
            using var smokeTest = new WebViewSmokeTestForm();
            Application.Run(smokeTest);
            return smokeTest.ExitCode;
        }

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, eventArgs) =>
        {
            AppDiagnostics.Write("Unhandled UI exception", eventArgs.Exception);
            MessageBox.Show(
                "אירעה תקלה בתוכנה. פרטי התקלה נשמרו כאן:\r\n" + AppDiagnostics.LogFilePath,
                "עמדת טעינה - תקלה",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            if (eventArgs.ExceptionObject is Exception exception)
                AppDiagnostics.Write("Unhandled application exception", exception);
        };

        try
        {
            Application.Run(new MainForm());
            return 0;
        }
        catch (Exception ex)
        {
            AppDiagnostics.Write("Fatal startup exception", ex);
            MessageBox.Show(
                "התוכנה לא הצליחה לעלות. פרטי התקלה נשמרו כאן:\r\n" + AppDiagnostics.LogFilePath +
                "\r\n\r\n" + ex.Message,
                "עמדת טעינה - תקלה",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 1;
        }
    }
}

internal static class AppDiagnostics
{
    public static string LogFilePath
    {
        get
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RavKavKiosk",
                "Logs");
            return Path.Combine(folder, "RavKavKiosk.log");
        }
    }

    public static void Write(string area, Exception? exception = null, string? details = null)
    {
        try
        {
            var folder = Path.GetDirectoryName(LogFilePath)!;
            Directory.CreateDirectory(folder);
            var content = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {area}\r\n";
            if (!string.IsNullOrWhiteSpace(details))
                content += details + "\r\n";
            if (exception is not null)
                content += exception + "\r\n";
            File.AppendAllText(LogFilePath, content + "\r\n");
        }
        catch
        {
            // Logging must never prevent the kiosk from opening.
        }
    }
}

internal sealed class WebViewSmokeTestForm : Form
{
    private readonly WebView2 _webView = new() { Dock = DockStyle.Fill };
    private readonly string _userDataFolder = Path.Combine(
        Path.GetTempPath(), "RavKavKiosk-SmokeTest-" + Guid.NewGuid().ToString("N"));

    public int ExitCode { get; private set; } = 1;

    public WebViewSmokeTestForm()
    {
        Text = "RavKavKiosk smoke test";
        Width = 640;
        Height = 480;
        ShowInTaskbar = false;
        WindowState = FormWindowState.Minimized;
        Controls.Add(_webView);
        Shown += async (_, _) => await RunSmokeTestAsync();
    }

    private async Task RunSmokeTestAsync()
    {
        try
        {
            var settings = AppSettings.Load();
            if (!Uri.TryCreate(settings.StartUrl, UriKind.Absolute, out var start) ||
                !settings.IsAllowedNavigationUri(start) ||
                settings.IsBlockedNavigationUri(start))
                throw new InvalidOperationException("The configured StartUrl is not allowed.");

            foreach (var blockedPath in settings.BlockedPaths)
            {
                var blockedUri = new Uri("https://hopon.co.il" + blockedPath);
                if (!settings.IsBlockedNavigationUri(blockedUri))
                    throw new InvalidOperationException("Blocked route is not blocked: " + blockedPath);

                var nestedUri = new Uri("https://hopon.co.il" + blockedPath.TrimEnd('/') + "/smoke-test");
                if (!settings.IsBlockedNavigationUri(nestedUri))
                    throw new InvalidOperationException("Nested blocked route is not blocked: " + nestedUri);
            }

            Directory.CreateDirectory(_userDataFolder);
            var environment = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: _userDataFolder);
            await _webView.EnsureCoreWebView2Async(environment);

            AppDiagnostics.Write(
                "Smoke test",
                details: "PASS: appsettings loaded; StartUrl allowed; all blocked routes and nested routes denied; WebView2 runtime initialized.");
            ExitCode = 0;
        }
        catch (Exception ex)
        {
            AppDiagnostics.Write("Smoke test FAILED", ex);
            ExitCode = 1;
        }
        finally
        {
            Close();
        }
    }
}
