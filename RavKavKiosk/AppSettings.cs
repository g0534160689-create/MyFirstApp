using System.Text.Json;

namespace RavKavKiosk;

internal sealed class AppSettings
{
    public string StartUrl { get; set; } = "https://hopon.co.il/read";
    public string AllowedHost { get; set; } = "hopon.co.il";
    public List<string> BlockedPaths { get; set; } =
    [
        "/charging-stations-map",
        "/about",
        "/ravkav",
        "/freeMonthly",
        "/freeMonthlyOtef",
        "/accessibility",
        "/map"
    ];

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

    // Allow normal navigation anywhere on the official HopOn host.
    public bool IsAllowedNavigationUri(Uri uri)
    {
        return uri.IsAbsoluteUri &&
               uri.Scheme == Uri.UriSchemeHttps &&
               uri.Host.Equals(AllowedHost, StringComparison.OrdinalIgnoreCase) &&
               uri.IsDefaultPort &&
               string.IsNullOrEmpty(uri.UserInfo);
    }

    // Block only the explicitly forbidden pages (including their nested routes).
    public bool IsBlockedNavigationUri(Uri uri)
    {
        if (!uri.IsAbsoluteUri) return true;

        var path = uri.AbsolutePath;
        try
        {
            for (var i = 0; i < 5 && path.Contains('%'); i++)
            {
                var decoded = Uri.UnescapeDataString(path);
                if (decoded == path) break;
                path = decoded;
            }
        }
        catch { return true; }

        path = path.TrimEnd('/');
        return BlockedPaths.Any(blocked =>
        {
            var normalized = blocked.TrimEnd('/');
            return path.Equals(normalized, StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith(normalized + "/", StringComparison.OrdinalIgnoreCase);
        });
    }
}
