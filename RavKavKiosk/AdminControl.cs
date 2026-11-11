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
}

internal static class AdminPolicyStore
{
    private static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RavKavKiosk");
    private static readonly string FilePath = Path.Combine(Folder, "admin-policy.json");
    public static AdminPolicy Load()
    {
        try { return File.Exists(FilePath) ? JsonSerializer.Deserialize<AdminPolicy>(File.ReadAllText(FilePath)) ?? new() : new(); }
        catch { return new(); }
    }
    public static void Save(AdminPolicy policy)
    {
        Directory.CreateDirectory(Folder);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(policy, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        File.Move(temp, FilePath, true);
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
    private bool adminMode;
    private Button? adminModeButton;
    public AdminManagerForm(string url)
    {
        currentUrl = url; Text = "ניהול מערכת והרשאות"; Width = 790; Height = 590; MinimumSize = new Size(650, 480);
        StartPosition = FormStartPosition.CenterParent; RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
        BuildUi(); RefreshRules();
    }
    private void BuildUi()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 1, RowCount = 7 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
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
        root.Controls.Add(rules, 0, 4); root.Controls.Add(selected, 0, 5);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var pick = new Button { Text = "בחירת רכיב בלחיצה שמאלית", Width = 190 }; pick.Click += (_, _) => { Hide(); PickElementRequested?.Invoke(); };
        var blockElement = new Button { Text = "חסום רכיב שנבחר", Width = 140 }; blockElement.Click += (_, _) => SaveSelectedElement(false);
        var allowElement = new Button { Text = "אפשר רכיב שנבחר", Width = 140 }; allowElement.Click += (_, _) => SaveSelectedElement(true);
        var remove = new Button { Text = "מחק כלל מסומן", Width = 120 }; remove.Click += (_, _) => RemoveSelected();
        var change = new Button { Text = "שינוי סיסמה", Width = 110 }; change.Click += (_, _) => AdminAuthentication.ChangePassword(this);
        actions.Controls.AddRange([pick, blockElement, allowElement, remove, change]); root.Controls.Add(actions, 0, 6);
    }
    private void AddRoute(bool allowed)
    {
        var offset = policy.Routes.Count + policy.Elements.Count + policy.AllowedHosts.Count + 7 + AppSettings.Load().BlockedPaths.Count;
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
    private void SaveSelectedElement(bool allowed)
    {
        var marker = ": ";
        var split = selected.Text.LastIndexOf(marker, StringComparison.Ordinal);
        var selector = selected.Text.StartsWith("רכיב שנבחר", StringComparison.Ordinal) && split >= 0 ? selected.Text[(split + marker.Length)..] : "";
        var host = selectedElementHost;
        if (selector.Length == 0 || string.IsNullOrWhiteSpace(host)) { MessageBox.Show(this, "בחר תחילה רכיב מתוך האתר באמצעות לחיצה שמאלית או בחר כלל רכיב מהרשימה."); return; }
        policy.Elements.RemoveAll(r => r.Host.Equals(host, StringComparison.OrdinalIgnoreCase) && r.Selector == selector);
        policy.Elements.Add(new ElementPolicyRule(host, selector, allowed));
        AdminPolicyStore.Save(policy); RefreshRules(); RulesChanged?.Invoke();
    }
    private void RemoveSelected()
    {
        var index = rules.SelectedIndex; if (index < 0) return;
        var all = policy.Routes.Select(r => (object)r).Concat(policy.Elements.Select(e => (object)e)).ToList();
        if (index < all.Count)
        {
            if (all[index] is RoutePolicyRule route) policy.Routes.Remove(route);
            if (all[index] is ElementPolicyRule element) policy.Elements.Remove(element);
        }
        else if (index < all.Count + policy.AllowedHosts.Count)
        {
            var hostIndex = index - all.Count;
            if (hostIndex < 0 || hostIndex >= policy.AllowedHosts.Count) return;
            policy.AllowedHosts.RemoveAt(hostIndex);
        }
        else
        {
            MessageBox.Show(this, "השורה שנבחרה היא מידע על קישור או חסימה מובנית. כדי לשנות אותה, בחר קישור ולחץ על אפשר נתיב או חסום נתיב.");
            return;
        }
        AdminPolicyStore.Save(policy); RefreshRules(); RulesChanged?.Invoke();
    }
    private void RefreshRules()
    {
        rules.Items.Clear();
        foreach (var r in policy.Routes) rules.Items.Add($"{(r.Allowed ? "מאושר" : "חסום")} | {r.Host}{r.Path}");
        foreach (var e in policy.Elements) rules.Items.Add($"{(e.Allowed ? "מוצג" : "מוסתר")} | {e.Host} | {e.Selector}");
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
        if (elementIndex >= 0 && elementIndex < policy.Elements.Count)
        {
            var element = policy.Elements[elementIndex];
            selectedElementHost = element.Host;
            selected.Text = $"רכיב שנבחר (כלל קיים) | {element.Host}: {element.Selector}";
            return;
        }
        var offset = policy.Routes.Count + policy.Elements.Count + policy.AllowedHosts.Count + 7 + AppSettings.Load().BlockedPaths.Count;
        var scannedIndex = index - offset;
        if (scannedIndex >= 0 && scannedIndex < scannedLinks.Count)
        {
            ruleInput.Text = scannedLinks[scannedIndex].Url;
            selected.Text = $"קישור שזוהה: {(scannedLinks[scannedIndex].Blocked ? "חסום" : "פתוח")} — {scannedLinks[scannedIndex].Url}";
        }
    }
}
