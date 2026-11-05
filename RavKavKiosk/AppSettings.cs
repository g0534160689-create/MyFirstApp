using System.Text.Json;

namespace RavKavKiosk;

internal sealed class AppSettings
{
    public string StartUrl { get; set; } = "https://ravkavonline.co.il/he/store/welcome";
    public List<string> AllowedHttpsHosts { get; set; } = ["ravkavonline.co.il"];
    public List<string> AllowedExternalSchemes { get; set; } = [];

    public static AppSettings Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(path)) return new AppSettings();
        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new AppSettings();
        }
        catch { return new AppSettings(); }
    }

    public bool IsAllowedHttpsHost(string host) =>
        AllowedHttpsHosts.Any(allowed =>
            host.Equals(allowed, StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith("." + allowed, StringComparison.OrdinalIgnoreCase));

    public bool IsAllowedExternalScheme(string scheme) =>
        AllowedExternalSchemes.Contains(scheme, StringComparer.OrdinalIgnoreCase);
}
