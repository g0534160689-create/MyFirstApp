namespace RavKavKioskV2;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Diagnostics.Write("UI exception", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex) Diagnostics.Write("Fatal exception", ex);
        };
        Application.Run(new LauncherForm());
    }
}

internal static class Diagnostics
{
    public static string LogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RavKavKioskV2", "Logs", "kiosk-v2.log");

    public static void Write(string area, Exception? error = null, string? detail = null)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {area}\r\n" +
                (detail is null ? "" : detail + "\r\n") +
                (error is null ? "" : error + "\r\n") + "\r\n");
        }
        catch { }
    }
}
