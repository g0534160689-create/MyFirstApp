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
            // Construct the actual app form too: this validates startup UI resources.
            using var mainWindow = new MainForm();
            using (var logoStream = typeof(MainForm).Assembly.GetManifestResourceStream(
                "RavKavKiosk.Assets.daat-logo.png"))
            {
                if (logoStream is null)
                    throw new InvalidOperationException("Embedded logo resource is missing.");
                using var logoImage = Image.FromStream(logoStream);
                if (logoImage.Width < 1 || logoImage.Height < 1)
                    throw new InvalidOperationException("Embedded logo image is invalid.");
            }

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

                var encodedUri = new Uri("https://hopon.co.il" + blockedPath + "%252fprobe");
                if (!settings.IsBlockedNavigationUri(encodedUri))
                    throw new InvalidOperationException("Encoded blocked route is not blocked: " + encodedUri);

                var upperUri = new Uri("https://hopon.co.il" + blockedPath.ToUpperInvariant());
                if (!settings.IsBlockedNavigationUri(upperUri))
                    throw new InvalidOperationException("Case-variant blocked route is not blocked: " + upperUri);
            }

            if (!Uri.TryCreate(settings.RavKavStartUrl, UriKind.Absolute, out var ravKavStart) ||
                !settings.IsAllowedNavigationUri(ravKavStart) ||
                settings.IsBlockedNavigationUri(ravKavStart))
                throw new InvalidOperationException("The configured Rav-Kav Online connect route is not allowed.");

            var ravKavBlockedPaths = new[]
            {
                "/",
                "/he",
                "/he/store",
                "/he/store/other",
                "/he/store/connect/other",
                "/he/store/connect%252fother"
            };
            foreach (var path in ravKavBlockedPaths)
            {
                var blockedUri = new Uri("https://ravkavonline.co.il" + path);
                if (!settings.IsBlockedNavigationUri(blockedUri))
                    throw new InvalidOperationException("Rav-Kav Online route must be blocked: " + blockedUri);
            }

            var ravKavQueryUri = new Uri("https://ravkavonline.co.il/he/store/connect?source=kiosk");
            if (settings.IsBlockedNavigationUri(ravKavQueryUri))
                throw new InvalidOperationException("Query strings on the allowed connect route must remain available.");

            Directory.CreateDirectory(_userDataFolder);
            var environment = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: _userDataFolder);
            await _webView.EnsureCoreWebView2Async(environment);

            AppDiagnostics.Write(
                "Smoke test",
                details: "PASS: HopOn and Rav-Kav Online entry routes validated; blocked routes denied; WebView2 runtime initialized.");
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
