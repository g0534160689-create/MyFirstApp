using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RavKavKiosk;

internal sealed class AdminPolicy
{
    public AdminPolicy() { }
    public List<string> AllowedHosts { get; set; } = [];
    public bool ShowRavKavPanel { get; set; } = true;
    public bool ShowHopOnPanel { get; set; } = true;
    public string ThemeId { get; set; } = "midnight";
    public List<RoutePolicyRule> Routes { get; set; } = [];
    public List<ElementPolicyRule> Elements { get; set; } = [];
}
internal sealed class RoutePolicyRule
{
    public RoutePolicyRule() { }
    public RoutePolicyRule(string host, string path, bool allowed) { Host = host; Path = path; Allowed = allowed; }
    public string Host { get; set; } = "";
    public string Path { get; set; } = "/";
    public bool Allowed { get; set; }
}
internal sealed class ElementPolicyRule
{
    public ElementPolicyRule() { }
    public ElementPolicyRule(string host, string selector, bool allowed) { Host = host; Selector = selector; Allowed = allowed; }
    public string Host { get; set; } = "";
    public string Selector { get; set; } = "";
    public bool Allowed { get; set; }
    public string ReplacementText { get; set; } = "";
    public string ReplacementImagePath { get; set; } = "";
    public string ReplacementImageFit { get; set; } = "contain";
}

internal sealed class AdminPolicyExport
{
    public int FormatVersion { get; set; } = 1;
    public DateTime ExportedAtUtc { get; set; } = DateTime.UtcNow;
    public AdminPolicy Policy { get; set; } = new();
    public Dictionary<string, string> ImageAssets { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

internal static class AdminPolicyStore
{
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DaatAharon", "RavKavKiosk");
    private static readonly string FilePath = Path.Combine(Folder, "admin-policy.json");
    private static readonly string BackupPath = FilePath + ".bak";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public static string StoragePath => FilePath;

    public static AdminPolicy Load()
    {
        Directory.CreateDirectory(Folder);
        foreach (var candidate in new[] { FilePath, BackupPath })
        {
            try
            {
                if (!File.Exists(candidate)) continue;
                var json = File.ReadAllText(candidate, Encoding.UTF8);
                var loaded = JsonSerializer.Deserialize<AdminPolicy>(json, JsonOptions);
                if (loaded is not null) return loaded;
            }
            catch (Exception ex)
            {
                AppDiagnostics.Write("Admin policy load failed",
                    new InvalidDataException("Unable to load " + Path.GetFileName(candidate), ex));
            }
        }
        return new AdminPolicy();
    }

    public static void Save(AdminPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        Directory.CreateDirectory(Folder);
        var json = JsonSerializer.Serialize(policy, JsonOptions);
        var temp = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, json, new UTF8Encoding(false));
            // Verify the serialized file before replacing the last known-good settings.
            var verified = JsonSerializer.Deserialize<AdminPolicy>(File.ReadAllText(temp, Encoding.UTF8), JsonOptions)
                ?? throw new InvalidDataException("קובץ ההגדרות שנשמר אינו תקין.");
            if (File.Exists(FilePath))
                File.Copy(FilePath, BackupPath, true);
            File.Move(temp, FilePath, true);
            var persisted = JsonSerializer.Deserialize<AdminPolicy>(File.ReadAllText(FilePath, Encoding.UTF8), JsonOptions)
                ?? throw new IOException("אימות קובץ ההגדרות לאחר השמירה נכשל.");
            if (persisted.Routes.Count != policy.Routes.Count ||
                persisted.Elements.Count != policy.Elements.Count ||
                persisted.AllowedHosts.Count != policy.AllowedHosts.Count)
                throw new IOException("אימות מספר הכללים לאחר השמירה נכשל.");
            AppDiagnostics.Write("Admin policy saved",
                details: "Routes=" + persisted.Routes.Count +
                         "; Elements=" + persisted.Elements.Count +
                         "; Hosts=" + persisted.AllowedHosts.Count +
                         "; Path=" + FilePath);
        }
        catch (Exception ex)
        {
            AppDiagnostics.Write("Admin policy save failed", ex);
            throw new IOException("שמירת הגדרות הניהול נכשלה. הקובץ: " + FilePath, ex);
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        }
    }
    public static void ExportBundle(AdminPolicy policy, string destination)
    {
        var bundle = new AdminPolicyExport
        {
            FormatVersion = 1,
            ExportedAtUtc = DateTime.UtcNow,
            Policy = JsonSerializer.Deserialize<AdminPolicy>(JsonSerializer.Serialize(policy)) ?? new AdminPolicy()
        };
        long totalBytes = 0;
        foreach (var element in bundle.Policy.Elements)
        {
            var path = element.ReplacementImagePath;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) { element.ReplacementImagePath = ""; continue; }
            var info = new FileInfo(path);
            if (info.Length > 8 * 1024 * 1024) throw new InvalidDataException("תמונה חורגת ממגבלת 8MB: " + Path.GetFileName(path));
            totalBytes += info.Length;
            if (totalBytes > 40 * 1024 * 1024) throw new InvalidDataException("סך התמונות לייצוא חורג מ־40MB.");
            if (!bundle.ImageAssets.ContainsKey(path))
                bundle.ImageAssets[path] = Convert.ToBase64String(File.ReadAllBytes(path));
        }
        var json = JsonSerializer.Serialize(bundle, new JsonSerializerOptions { WriteIndented = true });
        var temp = destination + ".tmp";
        File.WriteAllText(temp, json, new UTF8Encoding(false));
        File.Move(temp, destination, true);
    }

    public static AdminPolicy ImportBundle(string source)
    {
        var info = new FileInfo(source);
        if (!info.Exists || info.Length > 50 * 1024 * 1024) throw new InvalidDataException("קובץ הייבוא אינו תקין או גדול מ־50MB.");
        var bundle = JsonSerializer.Deserialize<AdminPolicyExport>(File.ReadAllText(source))
            ?? throw new InvalidDataException("קובץ הגיבוי ריק או פגום.");
        if (bundle.FormatVersion != 1 || bundle.Policy is null) throw new InvalidDataException("גרסת קובץ הגיבוי אינה נתמכת.");
        var policy = bundle.Policy;
        if (policy.Routes.Count > 5000 || policy.Elements.Count > 5000 || policy.AllowedHosts.Count > 1000)
            throw new InvalidDataException("קובץ הגיבוי מכיל מספר חריג של כללים.");
        foreach (var route in policy.Routes)
        {
            if (string.IsNullOrWhiteSpace(route.Host) || Uri.CheckHostName(route.Host) == UriHostNameType.Unknown ||
                string.IsNullOrWhiteSpace(route.Path) || !route.Path.StartsWith('/') || route.Path.Length > 2048 ||
                route.Path.Any(char.IsControl))
                throw new InvalidDataException("נמצא כלל כתובת לא תקין בקובץ.");
        }
        foreach (var host in policy.AllowedHosts)
            if (string.IsNullOrWhiteSpace(host) || Uri.CheckHostName(host) == UriHostNameType.Unknown)
                throw new InvalidDataException("נמצא דומיין לא תקין בקובץ.");
        foreach (var element in policy.Elements)
            if (string.IsNullOrWhiteSpace(element.Host) || Uri.CheckHostName(element.Host) == UriHostNameType.Unknown ||
                string.IsNullOrWhiteSpace(element.Selector) || element.Selector.Length > 2048 ||
                element.Selector.Any(char.IsControl) || (element.ReplacementText?.Length ?? 0) > 1000)
                throw new InvalidDataException("נמצא כלל רכיב לא תקין בקובץ.");

        var imageFolder = Path.Combine(Folder, "ImportedImages");
        Directory.CreateDirectory(imageFolder);
        long totalBytes = 0;
        foreach (var element in policy.Elements)
        {
            if (string.IsNullOrWhiteSpace(element.ReplacementImagePath)) continue;
            if (!bundle.ImageAssets.TryGetValue(element.ReplacementImagePath, out var encoded))
            {
                element.ReplacementImagePath = "";
                continue;
            }
            var ext = Path.GetExtension(element.ReplacementImagePath).ToLowerInvariant();
            if (ext is not (".png" or ".jpg" or ".jpeg" or ".webp" or ".gif" or ".bmp"))
                throw new InvalidDataException("קובץ תמונה בעל סיומת לא נתמכת.");
            byte[] bytes;
            try { bytes = Convert.FromBase64String(encoded); }
            catch (FormatException) { throw new InvalidDataException("נתוני תמונה פגומים בקובץ הגיבוי."); }
            if (bytes.Length == 0 || bytes.Length > 8 * 1024 * 1024)
                throw new InvalidDataException("תמונה בקובץ הגיבוי חורגת ממגבלת 8MB.");
            totalBytes += bytes.Length;
            if (totalBytes > 40 * 1024 * 1024) throw new InvalidDataException("סך התמונות בייבוא חורג מ־40MB.");
            var target = Path.Combine(imageFolder, Guid.NewGuid().ToString("N") + ext);
            File.WriteAllBytes(target, bytes);
            element.ReplacementImagePath = target;
            element.ReplacementImageFit = element.ReplacementImageFit == "cover" ? "cover" : "contain";
        }
        foreach (var route in policy.Routes) route.Host = route.Host.Trim().TrimEnd('.').ToLowerInvariant();
        foreach (var element in policy.Elements) element.Host = element.Host.Trim().TrimEnd('.').ToLowerInvariant();
        policy.AllowedHosts = policy.AllowedHosts.Select(h => h.Trim().TrimEnd('.').ToLowerInvariant()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return policy;
    }

    public static bool? RouteDecision(Uri uri)
    {
        var p = Load();
        var match = p.Routes
            .Where(r => r.Host.Equals(uri.Host, StringComparison.OrdinalIgnoreCase) && PathMatches(uri.AbsolutePath, r.Path))
            .OrderByDescending(r => r.Path.Length)
            .ThenBy(r => r.Allowed) // deny wins when two rules have equal specificity
            .FirstOrDefault();
        return match?.Allowed;
    }
    public static bool IsHostAllowed(string host) => Load().AllowedHosts.Contains(host, StringComparer.OrdinalIgnoreCase);
    public static bool IsHostBlocked(string host) => Load().Routes.Any(r => r.Host.Equals(host, StringComparison.OrdinalIgnoreCase) && r.Path == "/" && !r.Allowed);
    public static bool PathMatches(string actual, string rule)
    {
        if (string.IsNullOrWhiteSpace(rule)) return false;
        var root = rule.Trim();
        if (!root.StartsWith('/')) root = "/" + root;
        root = root.TrimEnd('/');
        if (root.Length == 0 || root == "/") return true;
        return actual.Equals(root, StringComparison.OrdinalIgnoreCase) || actual.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase);
    }
    public static IReadOnlyList<ElementPolicyRule> ElementRules(string host) =>
        Load().Elements.Where(r => r.Host.Equals(host, StringComparison.OrdinalIgnoreCase)).ToArray();
}

internal static class AdminAuthentication
{
    private static readonly string PasswordFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RavKavKiosk", "admin-password.json");
    private sealed class PasswordRecord
    {
        public PasswordRecord() { }
        public PasswordRecord(string salt, string hash) { Salt = salt; Hash = hash; }
        public string Salt { get; set; } = "";
        public string Hash { get; set; } = "";
    }
    public static bool Authenticate(IWin32Window owner)
    {
        if (!File.Exists(PasswordFile))
        {
            using var setup = new PasswordDialog(true);
            if (setup.ShowDialog(owner) != DialogResult.OK) return false;
            SetPassword(setup.Password);
            return true;
        }
        using var login = new PasswordDialog(false);
        if (login.ShowDialog(owner) != DialogResult.OK) return false;
        if (Verify(login.Password)) return true;
        MessageBox.Show(owner, "הסיסמה שגויה.", "אימות מנהל", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
    }
    public static bool ChangePassword(IWin32Window owner)
    {
        using var old = new PasswordDialog(false, "אימות הסיסמה הנוכחית");
        if (old.ShowDialog(owner) != DialogResult.OK || !Verify(old.Password))
        {
            MessageBox.Show(owner, "האימות נכשל. הסיסמה לא שונתה.", "שינוי סיסמה", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        using var next = new PasswordDialog(true, "הגדרת סיסמה חדשה");
        if (next.ShowDialog(owner) != DialogResult.OK) return false;
        SetPassword(next.Password);
        MessageBox.Show(owner, "הסיסמה עודכנה בהצלחה.", "שינוי סיסמה", MessageBoxButtons.OK, MessageBoxIcon.Information);
        return true;
    }
    private static void SetPassword(string password)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PasswordFile)!);
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 310_000, HashAlgorithmName.SHA256, 32);
        var temp = PasswordFile + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(new PasswordRecord(Convert.ToBase64String(salt), Convert.ToBase64String(hash))));
        File.Move(temp, PasswordFile, true);
    }
    private static bool Verify(string password)
    {
        try
        {
            var record = JsonSerializer.Deserialize<PasswordRecord>(File.ReadAllText(PasswordFile));
            if (record is null) return false;
            var salt = Convert.FromBase64String(record.Salt);
            var expected = Convert.FromBase64String(record.Hash);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, 310_000, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch { return false; }
    }
}

internal sealed class PasswordDialog : Form
{
    private readonly TextBox first = new() { UseSystemPasswordChar = true, Width = 250 };
    private readonly TextBox second = new() { UseSystemPasswordChar = true, Width = 250 };
    public string Password => first.Text;
    public PasswordDialog(bool confirm, string title = "")
    {
        Text = title.Length > 0 ? title : confirm ? "הגדרת סיסמת מנהל" : "אימות מנהל";
        FormBorderStyle = FormBorderStyle.FixedDialog; StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = MaximizeBox = false; ShowInTaskbar = false;
        RightToLeft = RightToLeft.Yes; RightToLeftLayout = true; ClientSize = new Size(390, confirm ? 190 : 135);
        Controls.Add(new Label { Text = confirm ? "בחר סיסמה (לפחות 8 תווים):" : "הזן סיסמת מנהל:", AutoSize = true, Location = new Point(250, 18) });
        first.Location = new Point(25, 48); Controls.Add(first);
        if (confirm)
        {
            Controls.Add(new Label { Text = "הקלד שוב לאימות:", AutoSize = true, Location = new Point(250, 88) });
            second.Location = new Point(25, 115); Controls.Add(second);
        }
        var ok = new Button { Text = "אישור", Location = new Point(215, confirm ? 150 : 95), Width = 90 };
        ok.Click += (_, _) =>
        {
            if (first.Text.Length == 0 || (confirm && first.Text.Length < 8)) { MessageBox.Show(this, "הסיסמה חייבת להכיל לפחות 8 תווים."); return; }
            if (confirm && first.Text != second.Text) { MessageBox.Show(this, "הסיסמאות אינן זהות. הקלד שוב."); second.Clear(); second.Focus(); return; }
            DialogResult = DialogResult.OK; Close();
        };
        var cancel = new Button { Text = "ביטול", DialogResult = DialogResult.Cancel, Location = new Point(115, confirm ? 150 : 95), Width = 90 };
        Controls.Add(ok); Controls.Add(cancel); AcceptButton = ok; CancelButton = cancel;
    }
}

internal sealed class AdminManagerForm : Form
{
    private readonly AdminPolicy policy = AdminPolicyStore.Load();
    private readonly TextBox ruleInput = new() { Width = 330 };
    private readonly TextBox hostInput = new() { Width = 190 };
    private readonly ListBox rules = new() { Dock = DockStyle.Fill, HorizontalScrollbar = true, SelectionMode = SelectionMode.MultiExtended };
    private readonly List<(string Url, string Text, bool Blocked)> scannedLinks = [];
    private readonly List<ElementPolicyRule> displayedElements = [];
    private ComboBox? elementFilter;
    private ComboBox? elementSort;
    private TextBox? replacementInput;
    private TextBox? imagePathInput;
    private ComboBox? imageFitInput;
    private PictureBox? replacementPreview;
    private readonly Label selected = new() { AutoSize = false, Height = 38, Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleRight };
    private readonly string currentUrl;
    private string selectedElementHost = "";
    public event Action? PickElementRequested;
    public event Action? ScanLinksRequested;
    public event Action? RulesChanged;
    public event Action<bool>? AdminModeChanged;
    public event Action<bool>? RavKavPanelVisibilityChanged;
    public event Action<bool>? HopOnPanelVisibilityChanged;
    public event Action<string>? ThemeChanged;
    public event Action? ReaderDiagnosticsRequested;
    private bool adminMode;
    private Button? adminModeButton;
    public AdminManagerForm(string url)
    {
        currentUrl = url; Text = "ניהול מערכת והרשאות"; Width = 790; Height = 590; MinimumSize = new Size(650, 480);
        StartPosition = FormStartPosition.CenterParent; RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
        BackColor = Color.FromArgb(11, 20, 34);
        ForeColor = Color.FromArgb(232, 239, 248);
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        BuildUi();
        ApplyPremiumStyle(this);
        RefreshRules();
    }
    private void BuildUi()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 1, RowCount = 8 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
        Controls.Add(root);
        root.Controls.Add(new Label { Text = "ניהול הרשאות — כתובות, נתיבים ורכיבים בעמוד", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 13, FontStyle.Bold), TextAlign = ContentAlignment.MiddleRight }, 0, 0);
        var adminStrip = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        adminModeButton = new Button { Text = "הפעל מצב מנהל — פתח את כל הנתיבים", Width = 300, Height = 32, BackColor = Color.FromArgb(28, 92, 67), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        adminModeButton.Click += (_, _) =>
        {
            adminMode = !adminMode;
            adminModeButton.Text = adminMode ? "כבה מצב מנהל — החזר חסימות" : "הפעל מצב מנהל — פתח את כל הנתיבים";
            adminModeButton.BackColor = adminMode ? Color.FromArgb(145, 73, 42) : Color.FromArgb(28, 92, 67);
            AdminModeChanged?.Invoke(adminMode);
        };
        adminStrip.Controls.Add(adminModeButton);
        var ravKavVisible = new CheckBox
        {
            Text = "הצג חלונית רב-קו במסך הראשי",
            AutoSize = true,
            Checked = policy.ShowRavKavPanel,
            Padding = new Padding(8, 8, 0, 0)
        };
        ravKavVisible.CheckedChanged += (_, _) =>
        {
            policy.ShowRavKavPanel = ravKavVisible.Checked;
            AdminPolicyStore.Save(policy);
            RavKavPanelVisibilityChanged?.Invoke(ravKavVisible.Checked);
        };
        adminStrip.Controls.Add(ravKavVisible);
        var hopOnVisible = new CheckBox
        {
            Text = "הצג חלונית HopOn",
            AutoSize = true,
            Checked = policy.ShowHopOnPanel,
            Padding = new Padding(8, 8, 0, 0)
        };
        hopOnVisible.CheckedChanged += (_, _) =>
        {
            policy.ShowHopOnPanel = hopOnVisible.Checked;
            AdminPolicyStore.Save(policy);
            HopOnPanelVisibilityChanged?.Invoke(hopOnVisible.Checked);
        };
        adminStrip.Controls.Add(hopOnVisible);
        var themePicker = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 155,
            Height = 30,
            Font = new Font("Segoe UI", 9),
            Margin = new Padding(6, 3, 6, 0)
        };
        themePicker.Items.AddRange(new object[]
        {
            "לילה יוקרתי", "כחול מודרני", "ירוק מרענן", "סגול אלגנטי", "אפור גרפיט"
        });
        themePicker.SelectedIndex = policy.ThemeId switch
        {
            "blue" => 1, "green" => 2, "purple" => 3, "graphite" => 4, _ => 0
        };
        themePicker.SelectedIndexChanged += (_, _) =>
        {
            policy.ThemeId = themePicker.SelectedIndex switch
            {
                1 => "blue", 2 => "green", 3 => "purple", 4 => "graphite", _ => "midnight"
            };
            AdminPolicyStore.Save(policy);
            ThemeChanged?.Invoke(policy.ThemeId);
        };
        adminStrip.Controls.Add(new Label { Text = "ערכת נושא:", AutoSize = true, Padding = new Padding(8, 8, 0, 0) });
        adminStrip.Controls.Add(themePicker);
        adminStrip.Controls.Add(new Label { Text = "המצב פתוח רק כל עוד חלון הניהול פעיל", AutoSize = true, Padding = new Padding(8, 8, 0, 0) });
        root.Controls.Add(adminStrip, 0, 1);
        var route = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = true };
        route.Controls.Add(new Label { Text = "כתובת או נתיב:", AutoSize = true, Padding = new Padding(0, 8, 0, 0) });
        ruleInput.Text = currentUrl; route.Controls.Add(ruleInput);
        var block = new Button { Text = "חסום נתיב", Width = 100 }; block.Click += (_, _) => AddRoute(false);
        var allow = new Button { Text = "אפשר נתיב", Width = 100 }; allow.Click += (_, _) => AddRoute(true);
        var scan = new Button { Text = "סרוק קישורים בעמוד", Width = 150 }; scan.Click += (_, _) => ScanLinksRequested?.Invoke();
        route.Controls.Add(block); route.Controls.Add(allow); route.Controls.Add(scan); root.Controls.Add(route, 0, 2);
        var host = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        host.Controls.Add(new Label { Text = "דומיין מורשה:", AutoSize = true, Padding = new Padding(0, 8, 0, 0) });
        host.Controls.Add(hostInput);
        var addHost = new Button { Text = "הוסף לאתרים המורשים", Width = 170 }; addHost.Click += (_, _) => AddHost();
        host.Controls.Add(addHost); root.Controls.Add(host, 0, 3);
        root.Controls.Add(rules, 0, 4);
        var selectionBar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        selected.Width = 300;
        selectionBar.Controls.Add(selected);
        elementFilter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130, Height = 30 };
        elementFilter.Items.AddRange(new object[] { "כל הרכיבים", "מוסתרים", "מוסתרים עם תמונה", "מוסתרים עם טקסט", "מוצגים" });
        elementFilter.SelectedIndex = 0;
        elementFilter.SelectedIndexChanged += (_, _) => RefreshRules();
        elementSort = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140, Height = 30 };
        elementSort.Items.AddRange(new object[] { "סדר רגיל", "מוסתרים תחילה", "תמונות תחילה", "לפי אתר" });
        elementSort.SelectedIndex = 1;
        elementSort.SelectedIndexChanged += (_, _) => RefreshRules();
        selectionBar.Controls.Add(new Label { Text = "סינון:", AutoSize = true, Padding = new Padding(4, 8, 0, 0) });
        selectionBar.Controls.Add(elementFilter);
        selectionBar.Controls.Add(new Label { Text = "מיון:", AutoSize = true, Padding = new Padding(4, 8, 0, 0) });
        selectionBar.Controls.Add(elementSort);
        root.Controls.Add(selectionBar, 0, 5);
        var replacementRow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = true, AutoScroll = true };
        replacementRow.Controls.Add(new Label { Text = "טקסט חלופי:", AutoSize = true, Padding = new Padding(0, 8, 0, 0) });
        replacementInput = new TextBox { Width = 245, Height = 48, Multiline = true, ScrollBars = ScrollBars.Vertical, MaxLength = 1000 };
        replacementInput.PlaceholderText = "טקסט להצגה במקום הרכיב"; replacementRow.Controls.Add(replacementInput);
        imagePathInput = new TextBox { Width = 220, Height = 28, ReadOnly = true, PlaceholderText = "בחר תמונה מהמחשב..." };
        imageFitInput = new ComboBox { Width = 105, DropDownStyle = ComboBoxStyle.DropDownList };
        imageFitInput.Items.AddRange(new object[] { "התאם בשלמות", "מלא את האזור" });
        imageFitInput.SelectedIndex = 0;
        replacementPreview = new PictureBox { Width = 76, Height = 76, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(17, 30, 48), BorderStyle = BorderStyle.FixedSingle };
        var browseImage = new Button { Text = "בחר תמונה", Width = 100, Height = 30 };
        browseImage.Click += (_, _) =>
        {
            using var dialog = new OpenFileDialog { Title = "בחירת תמונה חלופית", Filter = "קובצי תמונה|*.png;*.jpg;*.jpeg;*.webp;*.gif;*.bmp", CheckFileExists = true };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                try
                {
                    using var source = Image.FromFile(dialog.FileName);
                    var thumbnail = source.GetThumbnailImage(240, 240, () => false, IntPtr.Zero);
                    var oldPreview = replacementPreview?.Image;
                    if (replacementPreview is not null) replacementPreview.Image = thumbnail;
                    oldPreview?.Dispose();
                    imagePathInput.Text = dialog.FileName;
                }
                catch (Exception ex) { MessageBox.Show(this, "לא ניתן לטעון תצוגה מקדימה: " + ex.Message); }
            }
        };
        replacementRow.Controls.Add(imagePathInput); replacementRow.Controls.Add(browseImage);
        replacementRow.Controls.Add(new Label { Text = "התאמת תמונה:", AutoSize = true, Padding = new Padding(4, 8, 0, 0) });
        replacementRow.Controls.Add(imageFitInput); replacementRow.Controls.Add(replacementPreview);
        var replaceElement = new Button { Text = "הסתר + טקסט", Width = 115, Height = 34 };
        replaceElement.Click += (_, _) => SaveSelectedElement(false, replacementInput.Text.Trim()); replacementRow.Controls.Add(replaceElement);
        var replaceWithImage = new Button { Text = "הסתר + תמונה", Width = 120, Height = 34 };
        replaceWithImage.Click += (_, _) => SaveSelectedElement(false, replacementInput.Text.Trim(), imagePathInput.Text.Trim()); replacementRow.Controls.Add(replaceWithImage);
        root.Controls.Add(replacementRow, 0, 6);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = true, AutoScroll = true };
        var pick = new Button { Text = "בחירת רכיב בלחיצה שמאלית", Width = 190 }; pick.Click += (_, _) => { Hide(); PickElementRequested?.Invoke(); };
        var blockElement = new Button { Text = "הסתר רכיב שנבחר", Width = 140 }; blockElement.Click += (_, _) => SaveSelectedElement(false);
        var allowElement = new Button { Text = "הצג רכיב שנבחר", Width = 140 }; allowElement.Click += (_, _) => SaveSelectedElement(true);
        var remove = new Button { Text = "מחק כלל מסומן", Width = 120 }; remove.Click += (_, _) => RemoveSelected();
        var change = new Button { Text = "שינוי סיסמה", Width = 110 }; change.Click += (_, _) => AdminAuthentication.ChangePassword(this);
        var readerDiagnostics = new Button { Text = "אבחון קורא כרטיסים", Width = 150 };
        readerDiagnostics.Click += (_, _) => ReaderDiagnosticsRequested?.Invoke();
        var resetBlacklist = new Button { Text = "אפס רשימה שחורה", Width = 135 };
        resetBlacklist.Click += (_, _) => ResetBlacklist();
        var resetElements = new Button { Text = "אפס כללי רכיבים", Width = 125 };
        resetElements.Click += (_, _) => ResetElementRules();
        var resetAll = new Button { Text = "אפס כללי מנהל", Width = 120 };
        resetAll.Click += (_, _) => ResetAllCustomRules();
        var exportRules = new Button { Text = "ייצוא כללי חסימה", Width = 145 };
        exportRules.Click += (_, _) => ExportRules();
        var importRules = new Button { Text = "ייבוא כללי חסימה", Width = 145 };
        importRules.Click += (_, _) => ImportRules();
        actions.Controls.AddRange([pick, blockElement, allowElement, remove, resetBlacklist, resetElements, resetAll, exportRules, importRules, readerDiagnostics, change]); root.Controls.Add(actions, 0, 7);
    }
    private void ApplyPremiumStyle(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            switch (control)
            {
                case TableLayoutPanel:
                case FlowLayoutPanel:
                    control.BackColor = Color.Transparent;
                    break;
                case ListBox list:
                    list.BackColor = Color.FromArgb(17, 30, 48);
                    list.ForeColor = Color.FromArgb(225, 235, 247);
                    list.BorderStyle = BorderStyle.FixedSingle;
                    list.Font = new Font("Segoe UI", 9.5f);
                    break;
                case TextBox input:
                    input.BackColor = Color.FromArgb(20, 35, 54);
                    input.ForeColor = Color.FromArgb(244, 247, 252);
                    input.BorderStyle = BorderStyle.FixedSingle;
                    input.Font = new Font("Segoe UI", 9.5f);
                    break;
                case ComboBox combo:
                    combo.BackColor = Color.FromArgb(20, 35, 54);
                    combo.ForeColor = Color.FromArgb(244, 247, 252);
                    combo.FlatStyle = FlatStyle.Flat;
                    break;
                case Button button when button == adminModeButton:
                    button.FlatStyle = FlatStyle.Flat;
                    button.UseVisualStyleBackColor = false;
                    button.ForeColor = Color.White;
                    button.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
                    button.Cursor = Cursors.Hand;
                    break;
                case Button button:
                    button.FlatStyle = FlatStyle.Flat;
                    button.UseVisualStyleBackColor = false;
                    button.BackColor = Color.FromArgb(31, 49, 72);
                    button.ForeColor = Color.FromArgb(242, 246, 252);
                    button.FlatAppearance.BorderColor = Color.FromArgb(67, 94, 124);
                    button.FlatAppearance.BorderSize = 1;
                    button.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
                    button.Cursor = Cursors.Hand;
                    button.MouseEnter += (_, _) => button.BackColor = Color.FromArgb(43, 68, 96);
                    button.MouseLeave += (_, _) =>
                    {
                        if (button.IsDisposed) return;
                        button.BackColor = Color.FromArgb(31, 49, 72);
                    };
                    break;
                case Label label:
                    label.ForeColor = Color.FromArgb(207, 220, 237);
                    break;
                case CheckBox check:
                    check.ForeColor = Color.FromArgb(216, 228, 243);
                    check.BackColor = Color.Transparent;
                    break;
            }
            if (control.HasChildren) ApplyPremiumStyle(control);
        }
    }

    private void ExportRules()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "ייצוא גיבוי כללי חסימה",
            Filter = "גיבוי מערכת JSON (*.json)|*.json",
            FileName = "RavKavKiosk-Policy-Backup.json",
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            AdminPolicyStore.ExportBundle(policy, dialog.FileName);
            MessageBox.Show(this, "הייצוא הושלם. הגיבוי כולל כללי נתיבים, רכיבים ותמונות חלופיות.", "ייצוא הושלם", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, "הייצוא נכשל: " + ex.Message, "שגיאת ייצוא", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void ImportRules()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "ייבוא גיבוי כללי חסימה",
            Filter = "גיבוי מערכת JSON (*.json)|*.json",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        if (MessageBox.Show(this, "ייבוא הגיבוי יחליף את כללי הנתיבים, הרכיבים והדומיינים הידניים הקיימים. להמשיך?",
            "אישור החלפת כללים", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        try
        {
            var imported = AdminPolicyStore.ImportBundle(dialog.FileName);
            policy.Routes = imported.Routes;
            policy.Elements = imported.Elements;
            policy.AllowedHosts = imported.AllowedHosts;
            policy.ThemeId = imported.ThemeId;
            policy.ShowRavKavPanel = imported.ShowRavKavPanel;
            policy.ShowHopOnPanel = imported.ShowHopOnPanel;
            AdminPolicyStore.Save(policy);
            RefreshRules();
            RulesChanged?.Invoke();
            ThemeChanged?.Invoke(policy.ThemeId);
            RavKavPanelVisibilityChanged?.Invoke(policy.ShowRavKavPanel);
            HopOnPanelVisibilityChanged?.Invoke(policy.ShowHopOnPanel);
            selected.Text = "הייבוא הושלם. הכללים והתמונות שוחזרו.";
            MessageBox.Show(this, "הייבוא הושלם בהצלחה.", "ייבוא הושלם", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, "הייבוא נכשל ולא הוחלפו הכללים: " + ex.Message, "שגיאת ייבוא", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void ResetBlacklist()
    {
        if (MessageBox.Show(this, "לאפס את כל החסימות הידניות? כל הנתיבים שאושרו יישארו ללא שינוי.", "איפוס רשימה שחורה", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        policy.Routes.RemoveAll(r => !r.Allowed);
        AdminPolicyStore.Save(policy); RefreshRules(); RulesChanged?.Invoke();
        selected.Text = "הרשימה השחורה הידנית אופסה.";
    }

    private void ResetElementRules()
    {
        if (policy.Elements.Count == 0) return;
        if (MessageBox.Show(this, $"למחוק את כל {policy.Elements.Count} כללי הרכיבים ולהציג מחדש את הרכיבים שהוסתרו?", "איפוס כללי רכיבים", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        policy.Elements.Clear();
        AdminPolicyStore.Save(policy); RefreshRules(); RulesChanged?.Invoke();
        selected.Text = "כל כללי הרכיבים אופסו.";
    }

    private void ResetAllCustomRules()
    {
        if (MessageBox.Show(this, "לאפס את כל כללי הנתיבים, הרכיבים והדומיינים הידניים? הגדרות תצוגה, סיסמה והחסימות המובנות לא ישתנו.", "איפוס כללי מנהל", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        policy.Routes.Clear(); policy.Elements.Clear(); policy.AllowedHosts.Clear();
        AdminPolicyStore.Save(policy); RefreshRules(); RulesChanged?.Invoke();
        selected.Text = "כל הכללים הידניים אופסו.";
    }

    private void AddRoute(bool allowed)
    {
        var offset = policy.Routes.Count + displayedElements.Count + policy.AllowedHosts.Count + 7 + AppSettings.Load().BlockedPaths.Count;
        var selectedLinks = rules.SelectedIndices.Cast<int>()
            .Select(index => index - offset)
            .Where(index => index >= 0 && index < scannedLinks.Count)
            .Distinct().Select(index => scannedLinks[index]).ToArray();
        var targets = selectedLinks.Length > 0
            ? selectedLinks.Select(link => link.Url).ToArray()
            : new[] { ruleInput.Text.Trim() };
        var added = 0;
        foreach (var target in targets)
        {
            if (!Uri.TryCreate(target, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) continue;
            var path = uri.AbsolutePath;
            policy.Routes.RemoveAll(r => r.Host.Equals(uri.Host, StringComparison.OrdinalIgnoreCase) && r.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
            policy.Routes.Add(new RoutePolicyRule(uri.Host, path, allowed));
            added++;
        }
        if (added == 0) { MessageBox.Show(this, "בחר קישור מהרשימה או הזן כתובת HTTPS מלאה."); return; }
        AdminPolicyStore.Save(policy); RefreshRules(); RulesChanged?.Invoke();
        selected.Text = allowed ? $"נפתחו {added} נתיבים שנבחרו." : $"נחסמו {added} נתיבים שנבחרו.";
    }
    private void AddHost()
    {
        var host = hostInput.Text.Trim().TrimEnd('.').ToLowerInvariant();
        if (host.Length == 0 || host.Contains('/') || host.Contains(':') || host.Contains(' ')) { MessageBox.Show(this, "הזן שם דומיין תקין בלבד."); return; }
        if (!policy.AllowedHosts.Contains(host, StringComparer.OrdinalIgnoreCase)) policy.AllowedHosts.Add(host);
        AdminPolicyStore.Save(policy); hostInput.Clear(); RefreshRules(); RulesChanged?.Invoke();
    }
    public void ReceivePickedSelector(string selector, string tag = "", string href = "", string label = "", string host = "")
    {
        selectedElementHost = string.IsNullOrWhiteSpace(host) && Uri.TryCreate(currentUrl, UriKind.Absolute, out var pageUri) ? pageUri.Host : host;
        var details = string.IsNullOrWhiteSpace(href) ? "" : " | קישור: " + href;
        var caption = string.IsNullOrWhiteSpace(label) ? "" : " | " + label;
        selected.Text = $"רכיב שנבחר ({tag}){caption}{details}: {selector}";
        if (replacementInput is not null) replacementInput.Clear();
        if (imagePathInput is not null) imagePathInput.Clear();
        Show(); Activate();
    }
    public void ReceiveScannedLinks(IEnumerable<(string Url, string Text, bool Blocked)> links)
    {
        scannedLinks.Clear();
        scannedLinks.AddRange(links.Take(300));
        RefreshRules();
        selected.Text = $"נסרקו {scannedLinks.Count} קישורים בעמוד. בחר קישור מהרשימה כדי לטעון אותו לשדה הנתיב, ואז לחץ אפשר או חסום.";
        Show(); Activate();
    }
    private void SaveSelectedElement(bool allowed, string replacementText = "", string replacementImagePath = "")
    {
        var marker = ": ";
        var split = selected.Text.LastIndexOf(marker, StringComparison.Ordinal);
        var selector = selected.Text.StartsWith("רכיב שנבחר", StringComparison.Ordinal) && split >= 0 ? selected.Text[(split + marker.Length)..] : "";
        var host = selectedElementHost;
        if (selector.Length == 0 || string.IsNullOrWhiteSpace(host)) { MessageBox.Show(this, "בחר תחילה רכיב מתוך האתר באמצעות לחיצה שמאלית או בחר כלל רכיב מהרשימה."); return; }
        policy.Elements.RemoveAll(r => r.Host.Equals(host, StringComparison.OrdinalIgnoreCase) && r.Selector == selector);
        var imagePath = allowed ? "" : replacementImagePath.Trim();
        if (imagePath.Length > 0 && (!File.Exists(imagePath) || Path.GetExtension(imagePath).ToLowerInvariant() is not (".png" or ".jpg" or ".jpeg" or ".webp" or ".gif" or ".bmp"))) { MessageBox.Show(this, "בחר קובץ תמונה תקין מסוג PNG, JPG, WEBP, GIF או BMP."); return; }
        if (imagePath.Length > 0 && new FileInfo(imagePath).Length > 8 * 1024 * 1024) { MessageBox.Show(this, "גודל התמונה מוגבל ל־8MB."); return; }
        var rule = new ElementPolicyRule(host, selector, allowed)
        {
            ReplacementText = allowed ? "" : replacementText.Trim(),
            ReplacementImagePath = imagePath,
            ReplacementImageFit = imageFitInput?.SelectedIndex == 1 ? "cover" : "contain"
        };
        policy.Elements.Add(rule);
        AdminPolicyStore.Save(policy); RefreshRules(); RulesChanged?.Invoke();
        selected.Text = allowed ? "הרכיב יוצג מחדש." :
            rule.ReplacementImagePath.Length > 0 ? "הרכיב הוסתר ותמונה חלופית תוצג במקומו." :
            rule.ReplacementText.Length > 0 ? "הרכיב הוסתר ותוכן חלופי יוצג במקומו." : "הרכיב הוסתר.";
    }
    private void RemoveSelected()
    {
        var index = rules.SelectedIndex; if (index < 0) return;
        if (index < policy.Routes.Count)
        {
            policy.Routes.RemoveAt(index);
        }
        else
        {
            var elementIndex = index - policy.Routes.Count;
            if (elementIndex >= 0 && elementIndex < displayedElements.Count)
                policy.Elements.Remove(displayedElements[elementIndex]);
            else
            {
                var hostOffset = policy.Routes.Count + displayedElements.Count;
                if (index >= hostOffset && index < hostOffset + policy.AllowedHosts.Count)
                    policy.AllowedHosts.RemoveAt(index - hostOffset);
                else
                {
                    MessageBox.Show(this, "השורה שנבחרה היא מידע מובנה או קישור שנסרק. בחר כלל מותאם כדי למחוק אותו.");
                    return;
                }
            }
        }
        AdminPolicyStore.Save(policy); RefreshRules(); RulesChanged?.Invoke();
    }

    private void RefreshRules()
    {
        rules.Items.Clear();
        foreach (var r in policy.Routes) rules.Items.Add($"{(r.Allowed ? "מאושר" : "חסום")} | {r.Host}{r.Path}");
        IEnumerable<ElementPolicyRule> elements = policy.Elements;
        var filter = elementFilter?.SelectedIndex ?? 0;
        elements = filter switch
        {
            1 => elements.Where(e => !e.Allowed),
            2 => elements.Where(e => !e.Allowed && !string.IsNullOrWhiteSpace(e.ReplacementImagePath)),
            3 => elements.Where(e => !e.Allowed && !string.IsNullOrWhiteSpace(e.ReplacementText)),
            4 => elements.Where(e => e.Allowed),
            _ => elements
        };
        elements = (elementSort?.SelectedIndex ?? 1) switch
        {
            1 => elements.OrderBy(e => e.Allowed).ThenBy(e => e.Host, StringComparer.OrdinalIgnoreCase),
            2 => elements.OrderByDescending(e => !string.IsNullOrWhiteSpace(e.ReplacementImagePath)).ThenBy(e => e.Host, StringComparer.OrdinalIgnoreCase),
            3 => elements.OrderBy(e => e.Host, StringComparer.OrdinalIgnoreCase).ThenBy(e => e.Selector, StringComparer.OrdinalIgnoreCase),
            _ => elements
        };
        displayedElements.Clear(); displayedElements.AddRange(elements);
        foreach (var e in displayedElements)
            rules.Items.Add($"{(e.Allowed ? "מוצג" : !string.IsNullOrWhiteSpace(e.ReplacementImagePath) ? "מוסתר + תמונה" : e.ReplacementText.Length > 0 ? "מוסתר + חלופי" : "מוסתר")} | {e.Host} | {e.Selector}");
        foreach (var h in policy.AllowedHosts) rules.Items.Add($"דומיין מורשה | {h}");
        foreach (var h in new[] { "hopon.co.il", "ravkavonline.co.il", "donors.matara.pro", "www.matara.pro", "matara.pro", "rishumon.net", "tora.irom.co.il" })
            rules.Items.Add($"דומיין מורשה מובנה | {h}");
        foreach (var path in AppSettings.Load().BlockedPaths)
            rules.Items.Add($"חסום כברירת מחדל | https://hopon.co.il{path}");
        for (var i = 0; i < scannedLinks.Count; i++)
        {
            var link = scannedLinks[i];
            var blocked = !Uri.TryCreate(link.Url, UriKind.Absolute, out var linkUri) ||
                !AppSettings.Load().IsAllowedNavigationUri(linkUri) || AppSettings.Load().IsBlockedNavigationUri(linkUri);
            scannedLinks[i] = (link.Url, link.Text, blocked);
            rules.Items.Add($"{(blocked ? "חסום בעמוד" : "פתוח בעמוד")} | {link.Text} | {link.Url}");
        }
        rules.SelectedIndexChanged -= OnRuleSelectionChanged;
        rules.SelectedIndexChanged += OnRuleSelectionChanged;
    }
    private void OnRuleSelectionChanged(object? sender, EventArgs e)
    {
        var index = rules.SelectedIndex;
        if (index < 0) return;
        if (index < policy.Routes.Count)
        {
            var route = policy.Routes[index];
            ruleInput.Text = $"https://{route.Host}{route.Path}";
            selected.Text = $"נתיב שנבחר: {(route.Allowed ? "מאושר" : "חסום")} — {ruleInput.Text}";
            return;
        }
        var elementIndex = index - policy.Routes.Count;
        if (elementIndex >= 0 && elementIndex < displayedElements.Count)
        {
            var element = displayedElements[elementIndex];
            selectedElementHost = element.Host;
            selected.Text = $"רכיב שנבחר (כלל קיים) | {element.Host}: {element.Selector}";
            if (replacementInput is not null) replacementInput.Text = element.ReplacementText ?? "";
            if (imagePathInput is not null) imagePathInput.Text = element.ReplacementImagePath ?? "";
            if (imageFitInput is not null) imageFitInput.SelectedIndex = element.ReplacementImageFit == "cover" ? 1 : 0;
            if (replacementPreview is not null)
            {
                var oldPreview = replacementPreview.Image;
                replacementPreview.Image = null;
                oldPreview?.Dispose();
                if (!string.IsNullOrWhiteSpace(element.ReplacementImagePath) && File.Exists(element.ReplacementImagePath))
                {
                    try { using var source = Image.FromFile(element.ReplacementImagePath); replacementPreview.Image = source.GetThumbnailImage(240, 240, () => false, IntPtr.Zero); }
                    catch { }
                }
            }
            return;
        }
        var offset = policy.Routes.Count + displayedElements.Count + policy.AllowedHosts.Count + 7 + AppSettings.Load().BlockedPaths.Count;
        var scannedIndex = index - offset;
        if (scannedIndex >= 0 && scannedIndex < scannedLinks.Count)
        {
            ruleInput.Text = scannedLinks[scannedIndex].Url;
            selected.Text = $"קישור שזוהה: {(scannedLinks[scannedIndex].Blocked ? "חסום" : "פתוח")} — {scannedLinks[scannedIndex].Url}";
        }
    }
}
