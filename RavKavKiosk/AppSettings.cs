using System.Text.Json;

namespace RavKavKiosk;

internal sealed class AppSettings
{
    public string StartUrl { get; set; } = "https://hopon.co.il/read";
    public string AllowedHost { get; set; } = "hopon.co.il";
    public string RavKavStartUrl { get; set; } = "https://ravkavonline.co.il/he/store/connect";
    public string RavKavHost { get; set; } = "ravkavonline.co.il";
    public string RavKavAllowedPath { get; set; } = "/he/store/connect";
    public List<string> HopOnPaymentPaths { get; set; } = ["/pay"];
    public List<string> PortalAllowedHosts { get; set; } =
    [
        "donors.matara.pro",
        "www.matara.pro",
        "matara.pro",
        "rishumon.net",
        "tora.irom.co.il"
    ];

    public List<string> BlockedPaths { get; set; } =
    [
        "/charging-stations-map",
        "/about",
        "/ravkav",
        "/freeMonthly",
        "/freeMonthlyOtef",
        "/accessibility",
        "/map",
        "/student"
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

    // Both official services may be opened in the embedded browser. HopOn permits
    // its normal internal flow; Rav-Kav Online permits only its connect entry route.
    public bool IsAllowedNavigationUri(Uri uri)
    {
        if (!IsSecureHttpUri(uri)) return false;

        var adminDecision = AdminPolicyStore.RouteDecision(uri);
        if (adminDecision.HasValue) return adminDecision.Value;
        return (uri.Host.Equals(AllowedHost, StringComparison.OrdinalIgnoreCase) ||
                uri.Host.Equals("www.hopon.co.il", StringComparison.OrdinalIgnoreCase)) ||
               uri.Host.Equals(RavKavHost, StringComparison.OrdinalIgnoreCase) ||
               (PortalAllowedHosts ?? []).Any(host =>
                   uri.Host.Equals(host, StringComparison.OrdinalIgnoreCase)) ||
               AdminPolicyStore.IsHostAllowed(uri.Host);
    }

    public bool IsHopOnUri(Uri uri) =>
        uri.IsAbsoluteUri &&
        (uri.Host.Equals(AllowedHost, StringComparison.OrdinalIgnoreCase) ||
         uri.Host.Equals("www.hopon.co.il", StringComparison.OrdinalIgnoreCase));

    public bool IsRavKavUri(Uri uri) =>
        uri.IsAbsoluteUri &&
        uri.Host.Equals(RavKavHost, StringComparison.OrdinalIgnoreCase);

    // For Rav-Kav Online, only /he/store/connect is a permitted top-level route.
    // For HopOn, retain the existing blocked-route list and its nested/encoded checks.
    public bool IsBlockedNavigationUri(Uri uri)
    {
        if (!IsSecureHttpUri(uri)) return true;
        var adminDecision = AdminPolicyStore.RouteDecision(uri);
        if (adminDecision.HasValue) return !adminDecision.Value;
        if (AdminPolicyStore.IsHostAllowed(uri.Host)) return false;

        if (IsRavKavUri(uri))
        {
            var path = NormalizePath(uri.AbsolutePath);
            return path is null ||
                   !path.Equals(NormalizePath(RavKavAllowedPath), StringComparison.OrdinalIgnoreCase);
        }

        if (!IsHopOnUri(uri))
            return !(PortalAllowedHosts ?? []).Any(host =>
                uri.Host.Equals(host, StringComparison.OrdinalIgnoreCase));

        var normalizedPath = NormalizePath(uri.AbsolutePath);
        if (normalizedPath is null) return true;

        // Keep HopOn checkout and payment pages available throughout the purchase flow.
        if ((HopOnPaymentPaths ?? ["/pay"]).Any(root =>
        {
            var paymentRoot = NormalizePath(root);
            return !string.IsNullOrEmpty(paymentRoot) &&
                   (normalizedPath.Equals(paymentRoot, StringComparison.OrdinalIgnoreCase) ||
                    normalizedPath.StartsWith(paymentRoot.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase));
        })) return false;

        return (BlockedPaths ?? []).Any(blocked =>
        {
            var root = NormalizePath(blocked);
            if (string.IsNullOrEmpty(root) || root == "/") return false;
            if (!normalizedPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return false;

            if (normalizedPath.Length == root.Length) return true;
            var next = normalizedPath[root.Length];
            return next == '/' || next == ';' || next == '?' || next == '#';
        });
    }

    private static bool IsSecureHttpUri(Uri uri) =>
        uri.IsAbsoluteUri &&
        uri.Scheme == Uri.UriSchemeHttps &&
        uri.IsDefaultPort &&
        string.IsNullOrEmpty(uri.UserInfo);

    private static string? NormalizePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var path = value;
        try
        {
            for (var i = 0; i < 10 && path.Contains('%'); i++)
            {
                var decoded = Uri.UnescapeDataString(path);
                if (decoded == path) break;
                path = decoded;
            }
        }
        catch { return null; }

        if (path.Contains('%')) return null;
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
        return "/" + string.Join("/", segments);
    }
}
