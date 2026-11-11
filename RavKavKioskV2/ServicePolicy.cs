namespace RavKavKioskV2;

internal enum ServiceKind { HopOn, RavKavOnline }

internal static class ServicePolicy
{
    public static string DisplayName(ServiceKind service) =>
        service == ServiceKind.HopOn ? "HopOn" : "רב־קו אונליין";

    public static string StartUrl(ServiceKind service) =>
        service == ServiceKind.HopOn
            ? "https://hopon.co.il/read"
            : "https://ravkavonline.co.il/he/store/connect";

    public static string ProfileFolder(ServiceKind service) =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RavKavKioskV2", "WebView2",
            service == ServiceKind.HopOn ? "HopOn" : "RavKavOnline");

    // Fresh, explicit deny-list: only the unwanted HopOn information pages are
    // blocked. Checkout, identity, CAPTCHA, payment, reader and confirmation routes
    // are deliberately not enumerated here.
    private static readonly string[] HopOnBlockedRoots =
    [
        "/charging-stations-map",
        "/about",
        "/ravkav",
        "/freemonthly",
        "/freemonthlyotef",
        "/accessibility",
        "/map",
        "/student"
    ];

    public static bool IsReaderProtocol(Uri uri) =>
        uri.Scheme.Equals("ravkavonline", StringComparison.OrdinalIgnoreCase) ||
        uri.Scheme.Equals("ravkavonline.exe", StringComparison.OrdinalIgnoreCase);

    public static bool IsTrustedReaderOrigin(string? source, ServiceKind service)
    {
        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri)) return false;
        if (service == ServiceKind.RavKavOnline)
            return uri.Scheme == Uri.UriSchemeHttps &&
                   (uri.Host.Equals("ravkavonline.co.il", StringComparison.OrdinalIgnoreCase) ||
                    uri.Host.Equals("www.ravkavonline.co.il", StringComparison.OrdinalIgnoreCase));
        return uri.Scheme == Uri.UriSchemeHttps &&
               (uri.Host.Equals("hopon.co.il", StringComparison.OrdinalIgnoreCase) ||
                uri.Host.Equals("www.hopon.co.il", StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsAllowedWebUri(Uri uri, ServiceKind service)
    {
        if (!uri.IsAbsoluteUri ||
            !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo))
            return false;

        if (service != ServiceKind.HopOn ||
            !(uri.Host.Equals("hopon.co.il", StringComparison.OrdinalIgnoreCase) ||
              uri.Host.Equals("www.hopon.co.il", StringComparison.OrdinalIgnoreCase)))
            return true;

        var path = NormalizePath(uri.AbsolutePath);
        if (path is null) return false;
        return !HopOnBlockedRoots.Any(root =>
            path.Equals(root, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(root + ";", StringComparison.OrdinalIgnoreCase));
    }

    private static string? NormalizePath(string path)
    {
        try
        {
            for (var i = 0; i < 8; i++)
            {
                var decoded = Uri.UnescapeDataString(path);
                if (decoded == path) break;
                path = decoded;
            }
            if (path.Contains('%')) return null;
            path = path.Replace('\\', '/');
            var parts = new List<string>();
            foreach (var part in path.Split('/'))
            {
                if (part.Length == 0 || part == ".") continue;
                if (part == "..") { if (parts.Count > 0) parts.RemoveAt(parts.Count - 1); }
                else parts.Add(part);
            }
            return "/" + string.Join("/", parts).ToLowerInvariant();
        }
        catch { return null; }
    }
}
