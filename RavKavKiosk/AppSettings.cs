using System.Text.Json;

namespace RavKavKiosk;

internal sealed class AppSettings
{
    public string StartUrl { get; set; } = "https://hopon.co.il/read";
    public string AllowedHost { get; set; } = "hopon.co.il";
    public string AllowedPath { get; set; } = "/read";

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

    public bool IsAllowedNavigationUri(Uri uri)
    {
        if (!uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps ||
            !uri.Host.Equals(AllowedHost, StringComparison.OrdinalIgnoreCase) ||
            !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo))
            return false;

        // Allow only the card-reading/charging page. Any other path on this host is denied.
        var path = uri.AbsolutePath;
        try
        {
            for (var i = 0; i < 3 && path.Contains('%'); i++)
            {
                var decoded = Uri.UnescapeDataString(path);
                if (decoded == path) break;
                path = decoded;
            }
        }
        catch { return false; }

        return path.Equals(AllowedPath, StringComparison.OrdinalIgnoreCase) ||
               path.Equals(AllowedPath + "/", StringComparison.OrdinalIgnoreCase);
    }
}
