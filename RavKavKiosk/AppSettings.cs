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

    // Block the exact route and any nested/encoded variant of that route.
    public bool IsBlockedNavigationUri(Uri uri)
    {
        if (!uri.IsAbsoluteUri) return true;

        var path = uri.AbsolutePath;
        try
        {
            // Repeated decoding catches ordinary and double-encoded path tricks.
            for (var i = 0; i < 10 && path.Contains('%'); i++)
            {
                var decoded = Uri.UnescapeDataString(path);
                if (decoded == path) break;
                path = decoded;
            }
        }
        catch { return true; }

        // Fail closed if a suspiciously encoded path remains after repeated decoding.
        if (path.Contains('%')) return true;

        path = path.Replace('\\', '/');
        var segments = new List<string>();
        foreach (var segment in path.Split('/'))
        {
            if (segment.Length == 0 || segment == ".") continue;
            if (segment == "..")
            {
                if (segments.Count > 0) segments.RemoveAt(segments.Count - 1);
                continue;
            }
            segments.Add(segment);
        }

        var normalizedPath = "/" + string.Join("/", segments);
        return (BlockedPaths ?? []).Any(blocked =>
        {
            var root = blocked.TrimEnd('/');
            if (!normalizedPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return false;

            if (normalizedPath.Length == root.Length) return true;
            var next = normalizedPath[root.Length];
            return next == '/' || next == ';' || next == '?' || next == '#';
        });
    }
}}
