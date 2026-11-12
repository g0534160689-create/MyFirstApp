using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace RavKavKiosk;

internal sealed class MainForm : Form
{
    private readonly AppSettings _settings = AppSettings.Load();
    private WebView2 _browser = new() { Dock = DockStyle.Fill, Visible = false };
    private readonly BufferedPanel _home = new() { Dock = DockStyle.Fill, BackColor = Color.FromArgb(9, 18, 34) };
    private readonly Label _status = new()
    {
        AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
        Font = new Font("Segoe UI", 8), ForeColor = Color.FromArgb(159, 178, 198),
        Text = "השירות ייפתח בתוך התוכנה"
    };
    private string _activeStartUrl = "https://hopon.co.il/read";
    private string _activeServiceName = "HopOn";
    private readonly Button _back = new();
    private AdminManagerForm? _adminManager;
    private bool _elementPicking;
    private readonly Dictionary<string, CoreWebView2Environment> _webViewEnvironments = new(StringComparer.OrdinalIgnoreCase);
    private string? _initializedServiceProfile;
    private bool _browserInitialized;
    private readonly string? _backgroundImagePath = ResolveBackgroundImagePath();
    private BufferedPanel? _heroPanel;
    private PictureBox? _daatLogoBox;
    private PictureBox? _partnerLogoBox;
    private PremiumCardPanel? _ravKavPanel;
    private PremiumCardPanel? _hopOnPanel;
    private PremiumCardPanel? _heroCard;
    private int _adminHotspotClicks;
    private DateTime _lastAdminHotspotClickUtc;

    private static readonly Color Navy = Color.FromArgb(14, 27, 48);
    private static readonly Color Blue = Color.FromArgb(49, 91, 133);
    private static readonly Color Gold = Color.FromArgb(225, 190, 112);
    private readonly System.Windows.Forms.Timer _clockTimer = new() { Interval = 1000 };
    private readonly TextBox _searchBox = new();
    private readonly BufferedFlowLayoutPanel _cardsPanel = new();
    private readonly System.Windows.Forms.Timer _searchTimer = new() { Interval = 180 };
    private readonly Label _dateClock = new();
    private string _activeFilter = "all";
    private readonly HashSet<string> _favorites = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _clickCounts = new(StringComparer.Ordinal);

    public MainForm()
    {
        Text = "ישיבת דעת אהרן | כל מה שצריך בלי לצאת מהישיבה";
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(760, 560);
        BackColor = Color.FromArgb(9, 18, 34);
        Font = new Font("Segoe UI", 10);

        var header = new BufferedPanel { Dock = DockStyle.Top, Height = 70, BackColor = Color.FromArgb(8, 16, 29), Padding = new Padding(30, 0, 30, 0) };
        var brand = new Label
        {
            Text = "ישיבת דעת אהרן     ✦     המרכז הדיגיטלי", Dock = DockStyle.Right, Width = 560,
            ForeColor = Color.FromArgb(255, 222, 161), Font = new Font("Segoe UI", 15, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight, BackColor = Color.Transparent
        };
        _back.Text = "חזרה למסך הראשי";
        _back.Dock = DockStyle.Left;
        _back.Width = 170;
        _back.FlatStyle = FlatStyle.Flat;
        _back.FlatAppearance.BorderColor = Color.FromArgb(100, 150, 190);
        _back.FlatAppearance.BorderSize = 1;
        _back.FlatAppearance.MouseOverBackColor = Color.FromArgb(35, 62, 91);
        _back.FlatAppearance.MouseDownBackColor = Color.FromArgb(49, 79, 109);
        _back.Cursor = Cursors.Hand;
        _back.Font = new Font("Segoe UI", 9, FontStyle.Bold);
        _back.AccessibleName = "חזרה למסך הראשי";
        _back.ForeColor = Color.White;
        _back.BackColor = Navy;
        _back.Visible = false;
        _back.Click += (_, _) => ShowHome();
        header.Controls.Add(brand);
        // Invisible administrator hotspot in the upper-left corner: no button or visible hint.
        var adminHotspot = new Panel
        {
            Name = "AdminHotspot",
            Location = new Point(0, 0),
            Size = new Size(54, 64),
            BackColor = Color.Transparent,
            Cursor = Cursors.Default,
            TabStop = false
        };
        adminHotspot.Click += (_, _) =>
        {
            var now = DateTime.UtcNow;
            if ((now - _lastAdminHotspotClickUtc).TotalSeconds > 4) _adminHotspotClicks = 0;
            _lastAdminHotspotClickUtc = now;
            _adminHotspotClicks++;
            if (_adminHotspotClicks >= 5)
            {
                _adminHotspotClicks = 0;
                OpenAdminManager();
            }
        };
        header.Controls.Add(adminHotspot);
        adminHotspot.BringToFront();

        header.Controls.Add(_back);

        var footer = new BufferedPanel { Dock = DockStyle.Bottom, Height = 34, BackColor = Color.FromArgb(10, 19, 33), Padding = new Padding(18, 0, 18, 0) };
        footer.Controls.Add(_status);

        Controls.Add(_home);
        Controls.Add(_browser);
        Controls.Add(footer);
        Controls.Add(header);
        BuildHome();
        ApplyTheme(AdminPolicyStore.Load().ThemeId);
        Shown += async (_, _) => await LoadVisualAssetsAsync();
        _clockTimer.Tick += (_, _) => UpdateDateClock();
        _searchTimer.Tick += (_, _) => { _searchTimer.Stop(); RenderCards(); };
        _clockTimer.Start();
        UpdateDateClock();
    }

    private sealed record PortalLink(string Id, string Title, string Subtitle, string Url, string Category, string Badge);

    private static readonly PortalLink[] PortalLinks =
    [
        new("nedarim-personal", "נדרים פלוס – אזור אישי", "כניסה לאזור האישי של נדרים פלוס", "https://donors.matara.pro/login", "services", "אזור אישי"),
        new("torah-dileyh", "תורה דיליה", "סיכומי סוגיות וכתיבת חבורות", "https://www.matara.pro/nedarimplus/online/?mosad=7005288", "tests", "נפוץ"),
        new("print-charge", "הטענת הדפסות", "טעינת יתרת צילום והדפסה מהירה", "https://www.matara.pro/nedarimplus/Forms/4488.html?MasofId=&ClientId=&Zeout=&Version=1", "services", "שירות"),
        new("dirshu", "מבחני דרשו", "ציונים, מועדי בחינות והרשמה", "https://matara.pro/nedarimplus/online/?mosad=7005954", "tests", "דרשו"),
        new("yeshiva-kivro", "ישיבה על קברו", "מפעל הלימוד וההנצחה העולמי", "https://www.matara.pro/nedarimplus/v6/Mokirim14.html?Version=1", "learning", "מוקירים"),
        new("ladaat", "מבחני לדעת", "מגוון מבחנים", "https://www.matara.pro/nedarimplus/Forms/976.html?MasofId=&ClientId=&Zeout=&Version=8", "tests", "מבחנים"),
        new("siach-hatorah", "שיח התורה", "מבחנים בכל סגנונות התורה", "https://rishumon.net/SiachAtorah.html", "learning", "אוצר"),
        new("torah-shebichtav", "תורה שבכתב", "עיון בטקסטים, מפרשים ודקדוק", "https://tora.irom.co.il/?Version=17", "learning", "מאגר")
    ];

    private void BuildHome()
    {
        _home.BackColor = Color.FromArgb(12, 22, 38);
        _home.Padding = new Padding(0);
        _home.TabStop = false;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7,
            Padding = new Padding(34, 18, 34, 20),
            BackColor = Color.Transparent
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));

        // Top utility strip
        var meta = new BufferedPanel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        var partnerHost = new BufferedPanel { Dock = DockStyle.Right, Width = 156, BackColor = Color.FromArgb(244, 240, 231), Padding = new Padding(9) };
        partnerHost.Controls.Add(new Label { Text = "אוצר החכמה", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(64, 53, 37), Font = new Font("Segoe UI", 11, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent });
        _partnerLogoBox = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent, Visible = false };
        partnerHost.Controls.Add(_partnerLogoBox);
        meta.Controls.Add(partnerHost);

        _dateClock.Dock = DockStyle.Left;
        _dateClock.Width = 330;
        _dateClock.ForeColor = Color.FromArgb(179, 196, 215);
        _dateClock.BackColor = Color.Transparent;
        _dateClock.Font = new Font("Segoe UI", 9, FontStyle.Bold);
        _dateClock.AccessibleName = "תאריך ושעה";
        _dateClock.Padding = new Padding(6, 0, 0, 0);
        _dateClock.TextAlign = ContentAlignment.MiddleLeft;
        meta.Controls.Add(_dateClock);

        // Hero area
        var hero = new PremiumCardPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(32, 15, 32, 15),
            GradientTop = Color.FromArgb(29, 54, 82),
            GradientBottom = Color.FromArgb(17, 34, 55),
            BorderColor = Color.FromArgb(65, 91, 119),
            HoverBorderColor = Color.FromArgb(65, 91, 119)
        };
        _heroCard = hero;
        _heroPanel = new BufferedPanel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        var heroLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent };
        heroLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        heroLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        var heroText = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Color.Transparent };
        heroText.RowStyles.Add(new RowStyle(SizeType.Percent, 43));
        heroText.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
        heroText.RowStyles.Add(new RowStyle(SizeType.Percent, 27));
        heroText.Controls.Add(new Label
        {
            Text = "המרכז הדיגיטלי", Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(255, 222, 161), Font = new Font("Segoe UI", 27, FontStyle.Bold),
            TextAlign = ContentAlignment.BottomRight, BackColor = Color.Transparent
        }, 0, 0);
        heroText.Controls.Add(new Label
        {
            Text = "ישיבת דעת אהרן  ·  כל השירותים במקום אחד", Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(245, 248, 252), Font = new Font("Segoe UI", 13, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight, BackColor = Color.Transparent
        }, 0, 1);
        heroText.Controls.Add(new Label
        {
            Text = "בשיתוף מערכת אוצר החכמה  •  שע״י ישיבתנו הקדושה", Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(178, 195, 213), Font = new Font("Segoe UI", 9),
            TextAlign = ContentAlignment.TopRight, BackColor = Color.Transparent
        }, 0, 2);

        var logo = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(24, 47, 72), Padding = new Padding(14), BorderStyle = BorderStyle.None };
        logo.Controls.Add(new Label { Text = "דעת אהרן", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(255, 222, 161), Font = new Font("Segoe UI", 16, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent });
        _daatLogoBox = logo;
        heroLayout.Controls.Add(heroText, 0, 0);
        heroLayout.Controls.Add(logo, 1, 0);

        var heroHost = new BufferedPanel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(0) };
        heroHost.Controls.Add(heroLayout);
        hero.Controls.Add(heroHost);

        var searchHost = new Panel
        {
            Dock = DockStyle.Fill, BackColor = Color.FromArgb(22, 39, 59),
            Padding = new Padding(18, 8, 18, 8), Margin = new Padding(0, 5, 0, 5)
        };
        var searchLabel = new Label { Text = "⌕", Dock = DockStyle.Right, Width = 42, ForeColor = Color.FromArgb(255, 213, 143), Font = new Font("Segoe UI", 23, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent };
        _searchBox.Dock = DockStyle.Fill;
        _searchBox.BorderStyle = BorderStyle.None;
        _searchBox.BackColor = Color.FromArgb(22, 39, 59);
        _searchBox.ForeColor = Color.FromArgb(246, 248, 251);
        _searchBox.Font = new Font("Segoe UI", 13, FontStyle.Regular);
        _searchBox.Margin = new Padding(4, 5, 4, 5);
        _searchBox.TextAlign = HorizontalAlignment.Right;
        _searchBox.PlaceholderText = "חיפוש מהיר: שירות, מבחן, מקור לימוד...";
        _searchBox.AccessibleName = "חיפוש שירותים";
        _searchBox.TabIndex = 1;
        _searchBox.MaxLength = 120;
        _searchBox.TextChanged += (_, _) => { _searchTimer.Stop(); _searchTimer.Start(); };
        searchLabel.AccessibleName = "סמל חיפוש";
        searchLabel.Padding = new Padding(0, 0, 2, 0);
        searchHost.Paint += (_, e) =>
        {
            using var border = new Pen(Color.FromArgb(67, 101, 132), 1.2f);
            e.Graphics.DrawRectangle(border, 0, 0, Math.Max(1, searchHost.Width - 1), Math.Max(1, searchHost.Height - 1));
        };
        searchHost.Controls.Add(_searchBox);
        searchHost.Controls.Add(searchLabel);

        var filters = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false,
            AutoScroll = true, BackColor = Color.Transparent, Padding = new Padding(0, 4, 0, 4), Margin = new Padding(0, 2, 0, 2)
        };
        AddFilter(filters, "הכל", "all");
        AddFilter(filters, "★ מועדפים", "fav");
        AddFilter(filters, "מבחנים וציונים", "tests");
        AddFilter(filters, "מקורות לימוד", "learning");
        AddFilter(filters, "שירותי ישיבה", "services");

        _cardsPanel.Dock = DockStyle.Fill;
        _cardsPanel.AutoScroll = true;
        _cardsPanel.WrapContents = true;
        _cardsPanel.FlowDirection = FlowDirection.RightToLeft;
        _cardsPanel.FlowDirection = FlowDirection.RightToLeft;
        _cardsPanel.BackColor = Color.Transparent;
        _cardsPanel.Padding = new Padding(12, 12, 12, 16);
        _cardsPanel.Margin = new Padding(0, 2, 0, 0);
        _cardsPanel.TabStop = true;

        var sectionHeader = new BufferedPanel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(4, 0, 4, 0) };
        sectionHeader.Controls.Add(new Label
        {
            Text = "השירותים שלך, במקום אחד", Dock = DockStyle.Right, Width = 330,
            ForeColor = Color.FromArgb(246, 248, 252), Font = new Font("Segoe UI", 15, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight, BackColor = Color.Transparent
        });
        sectionHeader.Controls.Add(new Label
        {
            Text = "בחר כרטיס לפתיחת שירות • חפש וסנן לפי קטגוריה",
            Dock = DockStyle.Fill, ForeColor = Color.FromArgb(159, 180, 202),
            Font = new Font("Segoe UI", 9), TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.Transparent
        });
        sectionHeader.Controls.Add(new Label
        {
            Text = $"{PortalLinks.Length:00} שירותים זמינים", Dock = DockStyle.Left, Width = 150,
            ForeColor = Color.FromArgb(128, 210, 185), Font = new Font("Segoe UI", 9, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent
        });

        layout.Controls.Add(meta, 0, 0);
        layout.Controls.Add(hero, 0, 1);
        layout.Controls.Add(searchHost, 0, 2);
        layout.Controls.Add(filters, 0, 3);
        layout.Controls.Add(sectionHeader, 0, 4);
        var ravKavOptions = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
            BackColor = Color.Transparent, Margin = new Padding(0)
        };
        ravKavOptions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        ravKavOptions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        var savedPolicy = AdminPolicyStore.Load();
        _ravKavPanel = BuildRavKavPanel();
        _ravKavPanel.Visible = savedPolicy.ShowRavKavPanel;
        _hopOnPanel = BuildHopOnPanel();
        _hopOnPanel.Visible = savedPolicy.ShowHopOnPanel;
        ravKavOptions.Controls.Add(_ravKavPanel, 0, 0);
        ravKavOptions.Controls.Add(_hopOnPanel, 1, 0);
        layout.Controls.Add(_cardsPanel, 0, 5);
        layout.Controls.Add(ravKavOptions, 0, 6);
        _home.Controls.Add(layout);
        _cardsPanel.Resize += (_, _) => ResizeExistingCards();
        RenderCards();
    }    private PremiumCardPanel BuildRavKavPanel()
    {
        var panel = new PremiumCardPanel
        {
            Dock = DockStyle.Fill, Margin = new Padding(3, 5, 3, 5),
            Padding = new Padding(20, 10, 20, 10),
            GradientTop = Color.FromArgb(34, 67, 72),
            GradientBottom = Color.FromArgb(17, 42, 51),
            BorderColor = Color.FromArgb(81, 139, 128),
            HoverBorderColor = Color.FromArgb(119, 213, 178),
            Cursor = Cursors.Hand,
            AccessibleRole = AccessibleRole.Link,
            AccessibleName = "פתיחת שירות רב-קו אונליין"
        };
        var open = new Button
        {
            Text = "פתיחת רב-קו אונליין  ←",
            Dock = DockStyle.Left,
            Width = 230,
            Height = 48,
            Margin = new Padding(5),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(75, 139, 112),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        open.FlatAppearance.BorderColor = Color.FromArgb(125, 190, 157);
        open.FlatAppearance.BorderSize = 1;
        var description = new Label
        {
            Text = "טעינת כרטיס, בדיקת יתרה וניהול נסיעות באמצעות קורא כרטיסים",
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(198, 218, 215),
            Font = new Font("Segoe UI", 9),
            TextAlign = ContentAlignment.MiddleRight,
            BackColor = Color.Transparent
        };
        var title = new Label
        {
            Text = "רב-קו אונליין",
            Dock = DockStyle.Right,
            Width = 210,
            ForeColor = Color.FromArgb(255, 224, 164),
            Font = new Font("Segoe UI", 17, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight,
            BackColor = Color.Transparent
        };
        async void OpenRavKav(object? sender, EventArgs e) =>
            await OpenOfficialServiceAsync(_settings.RavKavStartUrl, "רב-קו אונליין");
        open.Click += OpenRavKav;
        title.Click += OpenRavKav;
        description.Click += OpenRavKav;
        panel.Click += OpenRavKav;
        panel.Controls.Add(description);
        panel.Controls.Add(title);
        panel.Controls.Add(open);
        return panel;
    }

    private void ApplyTheme(string themeId)
    {
        var palette = themeId switch
        {
            "blue" => (Back: Color.FromArgb(9, 22, 43), Top: Color.FromArgb(32, 76, 119), Bottom: Color.FromArgb(15, 42, 75), Border: Color.FromArgb(77, 137, 190), Accent: Color.FromArgb(116, 190, 244)),
            "green" => (Back: Color.FromArgb(9, 29, 28), Top: Color.FromArgb(31, 83, 69), Bottom: Color.FromArgb(14, 47, 40), Border: Color.FromArgb(76, 151, 122), Accent: Color.FromArgb(125, 218, 174)),
            "purple" => (Back: Color.FromArgb(24, 17, 42), Top: Color.FromArgb(72, 48, 103), Bottom: Color.FromArgb(37, 24, 62), Border: Color.FromArgb(126, 96, 164), Accent: Color.FromArgb(196, 157, 245)),
            "graphite" => (Back: Color.FromArgb(24, 28, 34), Top: Color.FromArgb(62, 70, 82), Bottom: Color.FromArgb(34, 40, 49), Border: Color.FromArgb(105, 117, 132), Accent: Color.FromArgb(184, 196, 210)),
            _ => (Back: Color.FromArgb(12, 22, 38), Top: Color.FromArgb(29, 54, 82), Bottom: Color.FromArgb(17, 34, 55), Border: Color.FromArgb(65, 91, 119), Accent: Color.FromArgb(225, 190, 112))
        };
        _home.BackColor = palette.Back;
        if (_heroCard is not null)
        {
            _heroCard.GradientTop = palette.Top;
            _heroCard.GradientBottom = palette.Bottom;
            _heroCard.BorderColor = palette.Border;
            _heroCard.HoverBorderColor = palette.Accent;
            _heroCard.Invalidate();
        }
        foreach (Control control in _cardsPanel.Controls)
        {
            if (control is not PremiumCardPanel card || card.Tag is not PortalLink) continue;
            card.GradientTop = palette.Top;
            card.GradientBottom = palette.Bottom;
            card.BorderColor = palette.Border;
            card.HoverBorderColor = palette.Accent;
            card.Invalidate();
        }
        _home.Invalidate(true);
    }

    private PremiumCardPanel BuildHopOnPanel()
    {
        var panel = new PremiumCardPanel
        {
            Dock = DockStyle.Fill, Margin = new Padding(5), Padding = new Padding(18, 10, 18, 10),
            GradientTop = Color.FromArgb(35, 57, 91), GradientBottom = Color.FromArgb(17, 32, 57),
            BorderColor = Color.FromArgb(83, 119, 164), HoverBorderColor = Color.FromArgb(120, 177, 231),
            Cursor = Cursors.Hand, AccessibleRole = AccessibleRole.Link,
            AccessibleName = "פתיחת שירות טעינת רב-קו HopOn"
        };
        var open = new Button
        {
            Text = "פתיחת HopOn  ←", Dock = DockStyle.Left, Width = 190,
            FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(57, 104, 160),
            ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Cursor = Cursors.Hand, UseVisualStyleBackColor = false
        };
        open.FlatAppearance.BorderColor = Color.FromArgb(120, 164, 210);
        open.FlatAppearance.BorderSize = 1;
        var details = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.Transparent };
        details.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        details.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        details.Controls.Add(new Label
        {
            Text = "טעינת רב-קו · HopOn", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(255, 222, 161),
            Font = new Font("Segoe UI", 15, FontStyle.Bold), TextAlign = ContentAlignment.BottomRight,
            BackColor = Color.Transparent
        }, 0, 0);
        details.Controls.Add(new Label
        {
            Text = "מעבר לשירות הטעינה והמשך לתשלום", Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(195, 211, 229), Font = new Font("Segoe UI", 9),
            TextAlign = ContentAlignment.TopRight, BackColor = Color.Transparent
        }, 0, 1);
        async void OpenHopOn(object? sender, EventArgs e) =>
            await OpenOfficialServiceAsync(_settings.StartUrl, "HopOn – טעינת רב-קו");
        open.Click += OpenHopOn;
        details.Click += OpenHopOn;
        panel.Click += OpenHopOn;
        panel.Controls.Add(details);
        panel.Controls.Add(open);
        return panel;
    }

    private void SetRavKavPanelVisibility(bool visible)
    {
        if (_ravKavPanel is null || _ravKavPanel.IsDisposed) return;
        _ravKavPanel.Visible = visible;
        _ravKavPanel.Parent?.PerformLayout();
    }

    private void SetHopOnPanelVisibility(bool visible)
    {
        if (_hopOnPanel is null || _hopOnPanel.IsDisposed) return;
        _hopOnPanel.Visible = visible;
        _hopOnPanel.Parent?.PerformLayout();
    }

    private static string? ResolveBackgroundImagePath()
    {
        // Support both the packaged Assets folder and a manually copied image
        // beside the EXE or in the user's local application-data folder.
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Assets", "bg.jpg"),
            Path.Combine(AppContext.BaseDirectory, "bg.jpg"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RavKavKiosk", "Assets", "bg.jpg")
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private async Task LoadVisualAssetsAsync()
    {
        var jobs = new List<Task<(string kind, Image? image)>>();
        if (File.Exists(_backgroundImagePath))
            jobs.Add(Task.Run(() => ("background", LoadOptimizedImage(_backgroundImagePath, 1920, 1080))));
        var partnerPath = Path.Combine(AppContext.BaseDirectory, "Assets", "otzar-logo.png");
        if (File.Exists(partnerPath))
            jobs.Add(Task.Run(() => ("partner", LoadOptimizedImage(partnerPath, 480, 260))));
        var daatPath = Path.Combine(AppContext.BaseDirectory, "Assets", "daat-logo.png");
        if (File.Exists(daatPath))
            jobs.Add(Task.Run(() => ("daat", LoadOptimizedImage(daatPath, 640, 640))));
        else
            jobs.Add(Task.Run(() => ("daat", LoadEmbeddedOptimizedImage("RavKavKiosk.Assets.daat-logo.png", 640, 640))));

        foreach (var job in jobs)
        {
            try
            {
                var (kind, image) = await job;
                if (image is null) continue;
                if (IsDisposed || _home.IsDisposed) { image.Dispose(); continue; }

                PictureBox? target = kind switch { "partner" => _partnerLogoBox, "daat" => _daatLogoBox, _ => null };
                if (kind == "background")
                {
                    // The home panel fills the form, so a Form.BackgroundImage is hidden
                    // behind it. Paint the image on the actual surface users see.
                    var previous = _home.BackgroundImage;
                    _home.BackgroundImage = image;
                    _home.BackgroundImageLayout = ImageLayout.Stretch;
                    previous?.Dispose();

                    // Keep the form background in sync when the browser view is shown.
                    BackgroundImage = image;
                    BackgroundImageLayout = ImageLayout.Stretch;
                }
                else if (target is not null)
                {
                    var previous = target.Image;
                    target.Image = image;
                    if (kind == "daat")
                        foreach (Control child in target.Controls) child.Visible = false;
                    else if (target.Parent is not null)
                        foreach (Control sibling in target.Parent.Controls)
                            if (sibling is Label) sibling.Visible = false;
                    target.Visible = true;
                    previous?.Dispose();
                }
            }
            catch (Exception ex) { AppDiagnostics.Write("Load visual asset", ex); }
        }
    }

    private static Image LoadEmbeddedOptimizedImage(string resourceName, int maxWidth, int maxHeight)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException("Embedded image resource not found.", resourceName);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return CreateOptimizedImage(buffer.ToArray(), maxWidth, maxHeight);
    }

    private static Image LoadOptimizedImage(string path, int maxWidth, int maxHeight)
    {
        // Read bytes first so the user's file is never kept locked by the running app.
        return CreateOptimizedImage(File.ReadAllBytes(path), maxWidth, maxHeight);
    }

    private static Image CreateOptimizedImage(byte[] bytes, int maxWidth, int maxHeight)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var source = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: true);
        var scale = Math.Min(1d, Math.Min((double)maxWidth / source.Width, (double)maxHeight / source.Height));
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));
        var bitmap = new Bitmap(width, height);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            graphics.DrawImage(source, new Rectangle(0, 0, width, height));
        }
        return bitmap;
    }

    private static Control CreatePartnerFallback() =>
        new Label
        {
            Text = "אוצר החכמה", Dock = DockStyle.Right, Width = 176,
            BackColor = Color.FromArgb(245, 255, 255, 255),
            ForeColor = Color.FromArgb(70, 48, 20), Font = new Font("Segoe UI", 13, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter, BorderStyle = BorderStyle.FixedSingle
        };

    private void AddFilter(FlowLayoutPanel host, string caption, string filter)
    {
        var button = new Button { Text = caption, Width = 154, Height = 38, Margin = new Padding(5, 2, 5, 2), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(23, 43, 68), ForeColor = Color.FromArgb(197, 184, 165), Font = new Font("Segoe UI", 10, FontStyle.Bold), Cursor = Cursors.Hand, UseVisualStyleBackColor = false, Tag = filter };
        button.FlatAppearance.BorderColor = Color.FromArgb(78, 116, 151);
        button.FlatAppearance.BorderSize = 1;
        button.Padding = new Padding(8, 2, 8, 2);
        button.TextAlign = ContentAlignment.MiddleCenter;
        button.AccessibleName = caption;
        button.TabStop = true;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(41, 65, 91);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(55, 78, 98);
        button.Click += (_, _) =>
        {
            _activeFilter = filter;
            foreach (Control item in host.Controls) if (item is Button b) { bool selected = Equals(b.Tag, _activeFilter); b.ForeColor = selected ? Color.FromArgb(253, 230, 138) : Color.FromArgb(197, 184, 165); b.BackColor = selected ? Color.FromArgb(76, 91, 62) : Color.FromArgb(23, 43, 68); }
            RenderCards();
        };
        if (filter == "all") { button.ForeColor = Color.FromArgb(253, 230, 138); button.BackColor = Color.FromArgb(76, 91, 62); }
        host.Controls.Add(button);
    }

    private bool _cardsCreated;
    private Label? _emptyState;

    private void RenderCards()
    {
        if (_cardsPanel.IsDisposed) return;
        if (_cardsCreated)
        {
            ApplyCardFilter();
            return;
        }

        _cardsCreated = true;
        _cardsPanel.SuspendLayout();
        var availableWidth = Math.Max(340, _cardsPanel.ClientSize.Width - 38);
        var columns = availableWidth >= 1160 ? 3 : availableWidth >= 720 ? 2 : 1;
        var cardWidth = Math.Clamp((availableWidth - (columns * 24)) / columns, 320, 490);

        foreach (var link in PortalLinks)
        {
            var featured = link.Id == "nedarim-personal";
            var categoryColor = featured ? Color.FromArgb(90, 205, 181) : link.Category switch
            {
                "tests" => Color.FromArgb(230, 190, 119),
                "services" => Color.FromArgb(91, 190, 174),
                _ => Color.FromArgb(142, 170, 220)
            };
            var card = new PremiumCardPanel
            {
                Width = cardWidth, Height = 194, Margin = new Padding(9),
                Cursor = Cursors.Hand, Tag = link, Padding = new Padding(16, 13, 16, 13),
                GradientTop = featured ? Color.FromArgb(31, 76, 78) : Color.FromArgb(29, 49, 72),
                GradientBottom = featured ? Color.FromArgb(18, 48, 55) : Color.FromArgb(17, 32, 51),
                BorderColor = featured ? Color.FromArgb(100, 151, 137) : Color.FromArgb(64, 91, 120),
                HoverBorderColor = categoryColor
            };

            var accent = new Panel { Dock = DockStyle.Right, Width = 5, BackColor = categoryColor, Margin = new Padding(0) };
            var icon = new Label
            {
                Text = featured ? "↗" : link.Category switch { "tests" => "✧", "services" => "▣", _ => "▤" },
                Dock = DockStyle.Right, Width = 62, ForeColor = categoryColor,
                Font = new Font("Segoe UI Symbol", 26, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.FromArgb(34, 57, 79),
                AccessibleName = "סמל קטגוריה: " + link.Category, Padding = new Padding(2)
            };
            var fav = new Button
            {
                Name = "favoriteButton",
                Text = _favorites.Contains(link.Id) ? "★" : "☆",
                Dock = DockStyle.Left, Width = 42,
                FlatStyle = FlatStyle.Flat, ForeColor = Color.FromArgb(255, 218, 125),
                BackColor = Color.FromArgb(30, 51, 74),
                Font = new Font("Segoe UI", 16, FontStyle.Bold), Cursor = Cursors.Hand,
                AccessibleName = "הוסף למועדפים",
                UseVisualStyleBackColor = false
            };
            fav.FlatAppearance.BorderSize = 0;
            fav.FlatAppearance.MouseOverBackColor = Color.FromArgb(56, 68, 77);
            fav.FlatAppearance.MouseDownBackColor = Color.FromArgb(75, 81, 83);
            fav.FlatAppearance.BorderColor = Color.FromArgb(69, 88, 107);
            fav.FlatAppearance.BorderSize = 1;
            fav.TabStop = true;
            fav.Click += (_, _) =>
            {
                if (!_favorites.Add(link.Id)) _favorites.Remove(link.Id);
                fav.Text = _favorites.Contains(link.Id) ? "★" : "☆";
                ApplyCardFilter();
            };

            var content = new BufferedPanel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(23, 42, 62), Padding = new Padding(13, 1, 11, 1) };
            var badge = new Label
            {
                Text = (featured ? "●  מומלץ עבורך" : "●  " + link.Badge), Dock = DockStyle.Top, Height = 27,
                ForeColor = categoryColor, Font = new Font("Segoe UI", 9, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleRight, BackColor = Color.Transparent,
                Padding = new Padding(0, 1, 0, 1), AccessibleName = "תגית " + link.Badge
            };
            var title = new Label
            {
                Text = link.Title, Dock = DockStyle.Top, Height = 42,
                ForeColor = Color.FromArgb(249, 250, 252), Font = new Font("Segoe UI", 16, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleRight, Cursor = Cursors.Hand, BackColor = Color.Transparent,
                AutoEllipsis = true, AccessibleName = link.Title
            };
            var description = new Label
            {
                Text = link.Subtitle, Dock = DockStyle.Top, Height = 38,
                ForeColor = Color.FromArgb(181, 197, 213), Font = new Font("Segoe UI", 9),
                TextAlign = ContentAlignment.TopRight, Cursor = Cursors.Hand, BackColor = Color.Transparent,
                AutoEllipsis = true, AccessibleName = "תיאור " + link.Title
            };
            var bottom = new Label
            {
                Name = "entryCount",
                Text = "נפתח " + _clickCounts.GetValueOrDefault(link.Id) + " פעמים",
                Dock = DockStyle.Bottom, Height = 22,
                ForeColor = categoryColor, Font = new Font("Segoe UI", 9, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleRight, Cursor = Cursors.Hand, BackColor = Color.Transparent,
                AccessibleName = "מונה פתיחות " + link.Title
            };
            var openButton = new Button
            {
                Name = "openServiceButton", Text = "פתיחת השירות  ←",
                Dock = DockStyle.Bottom, Height = 31, FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(34, 57, 79), ForeColor = categoryColor,
                Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false, Margin = new Padding(0, 3, 0, 0)
            };
            openButton.FlatAppearance.BorderColor = Color.FromArgb(74, 100, 127);
            openButton.FlatAppearance.BorderSize = 1;
            openButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(57, 82, 107);
            openButton.Padding = new Padding(8, 2, 8, 2);
            openButton.AccessibleName = "פתיחת השירות " + link.Title;
            openButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(47, 72, 97);
            openButton.Click += (_, _) => Launch();
            var separator = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Color.FromArgb(53, 75, 98), Margin = new Padding(0, 2, 0, 2) };
            content.Controls.Add(openButton);
            content.Controls.Add(bottom);
            content.Controls.Add(separator);
            content.Controls.Add(description);
            content.Controls.Add(title);
            content.Controls.Add(badge);
            card.Controls.Add(content);
            card.Controls.Add(icon);
            card.Controls.Add(fav);
            card.Controls.Add(accent);

            void Launch()
            {
                _clickCounts[link.Id] = _clickCounts.GetValueOrDefault(link.Id) + 1;
                bottom.Text = "כניסה לשירות  ←      " + _clickCounts[link.Id] + " כניסות";
                _ = OpenOfficialServiceAsync(link.Url, link.Title);
            }
            var tooltip = new ToolTip { AutomaticDelay = 250, AutoPopDelay = 5000, InitialDelay = 350, ReshowDelay = 100 };
            tooltip.SetToolTip(card, "לחץ לפתיחת " + link.Title);
            tooltip.SetToolTip(icon, "פתיחת " + link.Title);
            tooltip.SetToolTip(fav, "הוספה או הסרה מהמועדפים");
            card.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) { e.Handled = true; Launch(); }
            };
            card.TabStop = true;
            card.AccessibleRole = AccessibleRole.Link;
            card.AccessibleName = "פתיחת " + link.Title;
            openButton.TabStop = true;
            card.Click += (_, _) => Launch();
            content.Click += (_, _) => Launch();
            title.Click += (_, _) => Launch();
            description.Click += (_, _) => Launch();
            bottom.Click += (_, _) => Launch();
            icon.Click += (_, _) => Launch();
            _cardsPanel.Controls.Add(card);
        }

        _emptyState = new Label
        {
            Text = "לא נמצאו שירותים מתאימים\r\nאפשר לנסות חיפוש אחר",
            Width = 520, Height = 130, ForeColor = Color.FromArgb(210, 222, 235),
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent,
            Visible = false
        };
        _cardsPanel.Controls.Add(_emptyState);
        _cardsPanel.ResumeLayout();
        ApplyCardFilter();
    }    private void ApplyCardFilter()
    {
        if (_cardsPanel.IsDisposed) return;
        var query = _searchBox.Text.Trim();
        var visibleCount = 0;
        _cardsPanel.SuspendLayout();
        foreach (Control control in _cardsPanel.Controls)
        {
            if (control is not PremiumCardPanel card || card.Tag is not PortalLink link) continue;
            var matchesText = string.IsNullOrEmpty(query) ||
                link.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                link.Subtitle.Contains(query, StringComparison.CurrentCultureIgnoreCase);
            var matchesFilter = _activeFilter == "all" ||
                (_activeFilter == "fav" ? _favorites.Contains(link.Id) : link.Category == _activeFilter);
            card.Visible = matchesText && matchesFilter;
            if (card.Visible) visibleCount++;
        }
        if (_emptyState is not null) _emptyState.Visible = visibleCount == 0;
        _cardsPanel.ResumeLayout();
    }

    private void ResizeExistingCards()
    {
        if (_cardsPanel.IsDisposed || _cardsPanel.ClientSize.Width <= 0) return;
        var availableWidth = Math.Max(340, _cardsPanel.ClientSize.Width - 46);
        var columns = availableWidth >= 1120 ? 3 : availableWidth >= 720 ? 2 : 1;
        var cardWidth = Math.Clamp((availableWidth - (columns * 24)) / columns, 320, 490);
        foreach (Control control in _cardsPanel.Controls)
            if (control is PremiumCardPanel) control.Width = cardWidth;
    }

    private sealed class BufferedPanel : Panel
    {
        public BufferedPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
        }
    }

    private sealed class BufferedFlowLayoutPanel : FlowLayoutPanel
    {
        public BufferedFlowLayoutPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }
    }

    private sealed class PremiumCardPanel : Panel
    {
        public Color GradientTop = Color.FromArgb(28, 53, 82);
        public Color GradientBottom = Color.FromArgb(14, 28, 47);
        public Color BorderColor = Color.FromArgb(78, 115, 151);
        public Color HoverBorderColor = Color.FromArgb(225, 190, 112);
        private bool _hovered;

        public PremiumCardPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            MouseEnter += (_, _) => { _hovered = true; Invalidate(); };
            MouseLeave += (_, _) => { _hovered = false; Invalidate(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var rect = new Rectangle(1, 1, Math.Max(1, Width - 3), Math.Max(1, Height - 3));
            using var path = RoundedRectangle(rect, 18);
            using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                rect, _hovered ? Color.FromArgb(36, 67, 100) : GradientTop,
                _hovered ? Color.FromArgb(19, 39, 66) : GradientBottom, 90f);
            using var pen = new Pen(_hovered ? HoverBorderColor : BorderColor, _hovered ? 2.5f : 1.35f);
            e.Graphics.FillPath(brush, path);
            e.Graphics.DrawPath(pen, path);
        }

        private static System.Drawing.Drawing2D.GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            var d = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    private void UpdateDateClock()
    {
        try
        {
            var now = DateTime.Now; var hebrew = new System.Globalization.HebrewCalendar(); var culture = new System.Globalization.CultureInfo("he-IL"); culture.DateTimeFormat.Calendar = hebrew;
            _dateClock.Text = "●  " + now.ToString("dd MMMM yyyy", culture) + "     •     " + now.ToString("HH:mm:ss");
        }
        catch { _dateClock.Text = DateTime.Now.ToString("dd/MM/yyyy  •  HH:mm:ss"); }
    }

    private static void StyleServiceButton(Button button, Color background)
    {
        button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderColor = Color.FromArgb(140, 229, 184, 105); button.FlatAppearance.BorderSize = 1;
        button.BackColor = background; button.ForeColor = Color.FromArgb(255, 248, 235); button.Font = new Font("Segoe UI", 11, FontStyle.Bold); button.Cursor = Cursors.Hand; button.UseVisualStyleBackColor = false;
    }

    private async Task OpenOfficialServiceAsync(string startUrl, string serviceName)
    {
        var profileKey = serviceName.Contains("HopOn", StringComparison.OrdinalIgnoreCase) ? "HopOn" : "RavKavOnline";
        if (_browserInitialized && !string.Equals(_initializedServiceProfile, profileKey, StringComparison.OrdinalIgnoreCase))
        {
            // Give each official service its own WebView2 instance and persistent browser profile.
            // This prevents cookies, cache, storage and service-specific navigation state from mixing.
            Controls.Remove(_browser);
            _browser.Dispose();
            _browser = new WebView2 { Dock = DockStyle.Fill, Visible = false };
            Controls.Add(_browser);
            Controls.SetChildIndex(_browser, Math.Min(1, Controls.Count - 1));
            _browserInitialized = false;
            _elementPicking = false;
            AppDiagnostics.Write("Browser profile switched", details: "New isolated profile: " + profileKey);
        }
        _activeServiceProfile = profileKey;
        _activeStartUrl = startUrl;
        _activeServiceName = serviceName;
        _status.Text = $"מתחבר לשירות {serviceName}...";
        _home.Visible = false;
        _browser.Visible = true;
        _back.Visible = true;
        try
        {
            if (!_browserInitialized)
            {
                // Independent persistent WebView2 data directories for HopOn and Rav-Kav Online.
                var userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "RavKavKiosk",
                    "WebView2",
                    profileKey);
                Directory.CreateDirectory(userDataFolder);

                if (!_webViewEnvironments.TryGetValue(profileKey, out var environment))
                {
                    environment = await CoreWebView2Environment.CreateAsync(
                        browserExecutableFolder: null,
                        userDataFolder: userDataFolder);
                    _webViewEnvironments[profileKey] = environment;
                }

                await _browser.EnsureCoreWebView2Async(environment);
                var core = _browser.CoreWebView2;
                core.Settings.AreDefaultContextMenusEnabled = false;
                core.Settings.AreDefaultScriptDialogsEnabled = true;
                core.Settings.AreDevToolsEnabled = false;
                core.Settings.IsStatusBarEnabled = false;
                core.Settings.IsZoomControlEnabled = false;
                core.Settings.AreBrowserAcceleratorKeysEnabled = false;
                // Install every blocking layer before the first page navigation.
                core.NavigationStarting += OnNavigationStarting;
                core.WebMessageReceived += OnWebMessageReceived;
                core.FrameNavigationStarting += OnFrameNavigationStarting;
                core.HistoryChanged += (_, _) => EnforceCurrentPage();
                core.NewWindowRequested += OnNewWindowRequested;

                core.AddWebResourceRequestedFilter(
                    "https://www.hopon.co.il/*",
                    CoreWebView2WebResourceContext.Document,
                    CoreWebView2WebResourceRequestSourceKinds.All);
                core.AddWebResourceRequestedFilter(
                    "https://hopon.co.il/*",
                    CoreWebView2WebResourceContext.Document,
                    CoreWebView2WebResourceRequestSourceKinds.All);
                core.AddWebResourceRequestedFilter(
                    "https://ravkavonline.co.il/*",
                    CoreWebView2WebResourceContext.Document,
                    CoreWebView2WebResourceRequestSourceKinds.All);
                core.WebResourceRequested += OnWebResourceRequested;

                // Rav-Kav Online owns its complete multi-step workflow and reader bridge.
                // Do not inject kiosk route wrappers into this site: even a harmless
                // history/link hook can interfere with its reader bootstrap and SPA flow.
                if (profileKey == "HopOn")
                    await core.AddScriptToExecuteOnDocumentCreatedAsync(BuildRouteGuardScript(_settings.StartUrl));
                core.DownloadStarting += (_, args) => args.Cancel = true;
                core.LaunchingExternalUriScheme += OnLaunchingExternalUriScheme;
                core.PermissionRequested += (_, args) =>
                {
                    // Do not silently grant camera/microphone/location permissions. The CAPTCHA
                    // checkbox and image challenge work without weakening the user's permissions.
                    args.State = CoreWebView2PermissionState.Deny;
                    AppDiagnostics.Write("Web permission denied", details: args.PermissionKind + " from " + SafeOriginForLog(args.Uri));
                };
                core.SourceChanged += (_, _) =>
                {
                    EnforceCurrentPage();
                    if (_browser.CoreWebView2 is not null &&
                        Uri.TryCreate(_browser.CoreWebView2.Source, UriKind.Absolute, out var current) &&
                        IsAllowedNavigationUri(current) &&
                        !IsBlockedNavigationUri(current))
                        _status.Text = $"שירות {_activeServiceName}";
                };
                core.NavigationCompleted += (_, args) =>
                {
                    if (!args.IsSuccess)
                    {
                        var error = new InvalidOperationException(
                            $"טעינת {_activeServiceName} נכשלה: {args.WebErrorStatus}");
                        AppDiagnostics.Write("NavigationCompleted", error);
                        _status.Text = $"הטעינה נכשלה: {args.WebErrorStatus}";
                    }
                    else
                    {
                        // Keep Rav-Kav pages pristine. Admin DOM replacements and
                        // injected scripts can hide or mutate the reader bootstrap.
                        if (_activeServiceProfile != "RavKavOnline")
                        {
                            _ = ApplyElementRulesAsync();
                            if (_adminMode) _ = ApplyAdminModeToPageAsync();
                        }
                    }
                };
                core.ProcessFailed += (_, args) =>
                {
                    var error = new InvalidOperationException(
                        $"WebView2 process failed: {args.ProcessFailedKind}");
                    AppDiagnostics.Write("WebView2 ProcessFailed", error);
                    _status.Text = "רכיב הדפדפן הפסיק לפעול. חזור ונסה שוב.";
                };
                _browserInitialized = true;
                _initializedServiceProfile = profileKey;
            }

            if (!Uri.TryCreate(startUrl, UriKind.Absolute, out var start) ||
                !IsAllowedNavigationUri(start) ||
                IsBlockedNavigationUri(start))
                throw new InvalidOperationException("כתובת השירות אינה מאושרת בהגדרות.");

            _browser.CoreWebView2.Navigate(start.ToString());
        }
        catch (Exception ex)
        {
            AppDiagnostics.Write("OpenOfficialServiceAsync", ex);
            ShowHome();
            _status.Text = $"לא ניתן לפתוח את שירות {serviceName}.";
            MessageBox.Show(
                this,
                "פתיחת שירות הטעינה נכשלה.\r\n" +
                ex.Message + "\r\n\r\n" +
                "פרטי התקלה נשמרו בקובץ:\r\n" + AppDiagnostics.LogFilePath,
                "שגיאה בהפעלת עמדת הטעינה",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
        }
    }

    // Temporary, session-only override for inspecting routes blocked by normal kiosk policy.
    private bool _adminMode;
    private string _activeServiceProfile = "HopOn";
    private bool IsAllowedNavigationUri(Uri uri)
    {
        // During a user-initiated Rav-Kav/HopOn purchase flow, payment processors,
        // identity checks and 3-D Secure may use separate HTTPS hosts. Keep the
        // browser inside secure HTTPS and never permit insecure or credentialed URLs.
        if (!IsSecureWebNavigationUri(uri)) return false;
        var adminDecision = AdminPolicyStore.RouteDecision(uri);
        return !adminDecision.HasValue || adminDecision.Value;
    }

    private bool IsBlockedNavigationUri(Uri uri)
    {
        if (!IsSecureWebNavigationUri(uri)) return true;

        // The Rav-Kav flow is multi-domain and multi-stage. The built-in blacklist
        // must never interrupt card identification, contract selection, payment,
        // 3-D Secure, reader communication or the final write/confirmation page.
        if (_activeServiceProfile == "RavKavOnline")
            return false;

        // External HTTPS hosts are part of the active checkout/authentication flow.
        // Apply the kiosk blacklist only to the HopOn site itself.
        if (_activeServiceProfile == "HopOn" && !_settings.IsHopOnUri(uri))
            return false;

        // Payment paths are protected even if an older administrator rule denied them.
        if (_settings.IsHopOnUri(uri) && (_settings.HopOnPaymentPaths ?? ["/pay"]).Any(root =>
        {
            var normalized = root.TrimEnd('/');
            return uri.AbsolutePath.Equals(normalized, StringComparison.OrdinalIgnoreCase) ||
                   uri.AbsolutePath.StartsWith(normalized + "/", StringComparison.OrdinalIgnoreCase);
        }))
            return false;

        var adminDecision = AdminPolicyStore.RouteDecision(uri);
        if (adminDecision.HasValue) return !adminDecision.Value;

        return _activeServiceProfile == "HopOn" && _settings.IsHopOnUri(uri) &&
               _settings.IsBlockedNavigationUri(uri);
    }

    private static bool IsSecureWebNavigationUri(Uri uri) =>
        uri.IsAbsoluteUri && uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
        uri.IsDefaultPort && string.IsNullOrEmpty(uri.UserInfo);
    private void SetAdminMode(bool enabled)
    {
        _adminMode = enabled;
        _status.Text = enabled ? "מצב מנהל פעיל — ניווט HTTPS פתוח זמנית" : "מצב מנהל כבוי — החסימות הרגילות פעילות";
        _ = ApplyAdminModeToPageAsync();
        if (!enabled) EnforceCurrentPage();
    }
    private async Task ApplyAdminModeToPageAsync()
    {
        if (!_browserInitialized || _browser.CoreWebView2 is null) return;
        try
        {
            await _browser.CoreWebView2.ExecuteScriptAsync("window.__kioskAdminMode = " + (_adminMode ? "true" : "false") + ";");
            await ApplyRouteRulesAsync();
        }
        catch (Exception ex) { AppDiagnostics.Write("Toggle administrator mode", ex); }
    }

    private void OpenAdminManager()
    {
        if (!AdminAuthentication.Authenticate(this)) return;
        if (_adminManager is not null && !_adminManager.IsDisposed) { _adminManager.Show(); _adminManager.Activate(); return; }
        var currentUrl = _browserInitialized && Uri.TryCreate(_browser.CoreWebView2?.Source, UriKind.Absolute, out var current)
            ? current.ToString() : "https://hopon.co.il/read";
        _adminManager = new AdminManagerForm(currentUrl);
        _adminManager.PickElementRequested += StartElementPicker;
        _adminManager.ScanLinksRequested += ScanCurrentPageLinks;
        _adminManager.BlockedElementsRefreshRequested += ReportBlockedElements;
        _adminManager.RulesChanged += () => { _ = ApplyElementRulesAsync(); _ = ApplyRouteRulesAsync(); };
        _adminManager.AdminModeChanged += SetAdminMode;
        _adminManager.RavKavPanelVisibilityChanged += SetRavKavPanelVisibility;
        _adminManager.HopOnPanelVisibilityChanged += SetHopOnPanelVisibility;
        _adminManager.ThemeChanged += ApplyTheme;
        _adminManager.ReaderDiagnosticsRequested += () => { using var diagnostics = new ReaderDiagnosticsForm(); diagnostics.ShowDialog(this); };
        ApplyTheme(AdminPolicyStore.Load().ThemeId);
        _adminManager.FormClosed += (_, _) => { _adminManager = null; SetAdminMode(false); };
        _adminManager.Show(this);
    }

    private async void StartElementPicker()
    {
        if (!_browserInitialized || !_browser.Visible)
        { MessageBox.Show(this, "כדי לבחור רכיב, פתח תחילה אתר בתוך התוכנה."); _adminManager?.Show(); return; }
        _elementPicking = true;
        const string script = """
        (() => {
          if (window.__kioskPickerInstalled) return;
          window.__kioskPickerInstalled = true;
          let previous = null;
          const selectorFor = el => {
            if (el.id) return "#" + CSS.escape(el.id);
            const parts = [];
            while (el && el.nodeType === 1 && el !== document.documentElement) {
              let part = el.tagName.toLowerCase();
              if (el.classList.length) part += "." + [...el.classList].slice(0,2).map(c => CSS.escape(c)).join(".");
              else {
                const siblings = el.parentElement ? [...el.parentElement.children].filter(x => x.tagName === el.tagName) : [];
                if (siblings.length > 1) part += ":nth-of-type(" + (siblings.indexOf(el)+1) + ")";
              }
              parts.unshift(part); if (parts.length >= 5) break; el = el.parentElement;
            }
            return parts.join(" > ");
          };
          const move = e => {
            if (previous) previous.style.outline = previous.__kioskOldOutline || "";
            previous = e.target instanceof Element ? e.target : null;
            if (previous) { previous.__kioskOldOutline = previous.style.outline; previous.style.outline = "3px solid #f0b84b"; }
          };
          const pick = e => {
            if (e.button !== 0) return;
            const el = e.target instanceof Element ? e.target : null; if (!el) return;
            e.preventDefault(); e.stopImmediatePropagation(); e.stopPropagation();
            const selector = selectorFor(el);
            const tag = el.tagName.toLowerCase();
            const href = el.closest("a[href]")?.href || "";
            const label = (el.innerText || el.getAttribute("aria-label") || el.getAttribute("title") || "").trim().slice(0,120);
            if (previous) previous.style.outline = previous.__kioskOldOutline || "";
            document.removeEventListener("mousemove", move, true); document.removeEventListener("click", pick, true);
            window.__kioskPickerInstalled = false; document.body.style.cursor = "";
            chrome.webview.postMessage({type:"element-picked", selector, tag, href, label});
          };
          document.body.style.cursor = "crosshair";
          document.addEventListener("mousemove", move, true); document.addEventListener("click", pick, true);
        })();
        """;
        try { await _browser.CoreWebView2.ExecuteScriptAsync(script); }
        catch (Exception ex) { AppDiagnostics.Write("Start element picker", ex); _elementPicking = false; }
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var doc = JsonDocument.Parse(e.WebMessageAsJson);
            var messageType = doc.RootElement.GetProperty("type").GetString();
            if (messageType == "blocked-elements-report")
            {
                var items = new List<(string Selector, string Tag, string Text, int Count)>();
                if (doc.RootElement.TryGetProperty("items", out var reportItems) && reportItems.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in reportItems.EnumerateArray())
                    {
                        var selector = item.TryGetProperty("selector", out var selectorValue) ? selectorValue.GetString() ?? "" : "";
                        var tag = item.TryGetProperty("tag", out var tagValue) ? tagValue.GetString() ?? "element" : "element";
                        var label = item.TryGetProperty("text", out var textValue) ? textValue.GetString() ?? "" : "";
                        var count = item.TryGetProperty("count", out var countValue) && countValue.TryGetInt32(out var parsedCount) ? parsedCount : 1;
                        items.Add((selector, tag, label, count));
                    }
                }
                _adminManager?.ReceiveBlockedElementReport(items);
                return;
            }
            if (messageType == "link-scan")
            {
                var links = new List<(string Url, string Text, bool Blocked)>();
                foreach (var item in doc.RootElement.GetProperty("links").EnumerateArray())
                {
                    var urlText = item.GetProperty("url").GetString() ?? "";
                    if (!Uri.TryCreate(urlText, UriKind.Absolute, out var linkUri) || linkUri.Scheme != Uri.UriSchemeHttps) continue;
                    var labelText = item.TryGetProperty("text", out var textValue) ? textValue.GetString() ?? "" : "";
                    var blocked = !IsAllowedNavigationUri(linkUri) || IsBlockedNavigationUri(linkUri);
                    links.Add((urlText, string.IsNullOrWhiteSpace(labelText) ? "(ללא כותרת)" : labelText, blocked));
                }
                _adminManager?.ReceiveScannedLinks(links);
                return;
            }
            if (messageType != "element-picked" || !_elementPicking) return;
            var selector = doc.RootElement.GetProperty("selector").GetString();
            if (string.IsNullOrWhiteSpace(selector) || selector.Length > 500 ||
                !System.Text.RegularExpressions.Regex.IsMatch(selector, @"^[A-Za-z0-9#._\\: >()\-]+$")) return;
            var tag = doc.RootElement.TryGetProperty("tag", out var tagValue) ? tagValue.GetString() : "";
            var href = doc.RootElement.TryGetProperty("href", out var hrefValue) ? hrefValue.GetString() : "";
            var label = doc.RootElement.TryGetProperty("label", out var labelValue) ? labelValue.GetString() : "";
            var pickedHost = Uri.TryCreate(_browser.CoreWebView2?.Source, UriKind.Absolute, out var pickedUri) ? pickedUri.Host : "";
            _elementPicking = false; _adminManager?.ReceivePickedSelector(selector, tag ?? "", href ?? "", label ?? "", pickedHost);
        }
        catch (Exception ex) { AppDiagnostics.Write("Read selected page element", ex); }
    }

    private async void ReportBlockedElements()
    {
        if (!_browserInitialized || _browser.CoreWebView2 is null)
        {
            MessageBox.Show(this, "אין עמוד פתוח לסריקת רכיבים.");
            return;
        }
        try
        {
            await _browser.CoreWebView2.ExecuteScriptAsync("window.__kioskReportBlockedElements?.();");
        }
        catch (Exception ex)
        {
            AppDiagnostics.Write("Report blocked elements", ex);
            MessageBox.Show(this, "לא ניתן לקבל כרגע את רשימת הרכיבים שנחסמו.");
        }
    }

    private async void ScanCurrentPageLinks()
    {
        if (!_browserInitialized || _browser.CoreWebView2 is null)
        {
            MessageBox.Show(this, "אין עמוד פתוח לסריקה.");
            return;
        }
        const string script = """
        (() => {
          const seen = new Set();
          const links = [...document.querySelectorAll("a[href]")].map(a => ({
            url: a.href,
            text: (a.innerText || a.getAttribute("aria-label") || a.title || "").trim().replace(/\s+/g, " ").slice(0,100)
          })).filter(x => x.url.startsWith("https://") && !seen.has(x.url) && seen.add(x.url)).slice(0,300);
          chrome.webview.postMessage({type:"link-scan", links});
        })();
        """;
        try { await _browser.CoreWebView2.ExecuteScriptAsync(script); }
        catch (Exception ex) { AppDiagnostics.Write("Scan page links", ex); MessageBox.Show(this, "סריקת הקישורים נכשלה."); }
    }

    private async Task ApplyRouteRulesAsync()
    {
        if (!_browserInitialized || _browser.CoreWebView2 is null ||
            !Uri.TryCreate(_browser.CoreWebView2.Source, UriKind.Absolute, out var uri)) return;
        var rules = AdminPolicyStore.Load().Routes
            .Where(r => r.Host.Equals(uri.Host, StringComparison.OrdinalIgnoreCase))
            .Select(r => new { path = r.Path == "/" ? "/" : r.Path.TrimEnd('/').ToLowerInvariant(), allowed = r.Allowed });
        var json = JsonSerializer.Serialize(rules);
        try { await _browser.CoreWebView2.ExecuteScriptAsync("window.__kioskAdminRules = " + json + ";"); }
        catch (Exception ex) { AppDiagnostics.Write("Apply admin route rules", ex); }
    }

    private async Task ApplyElementRulesAsync()
    {
        if (!_browserInitialized || _browser.CoreWebView2 is null) return;
        if (!Uri.TryCreate(_browser.CoreWebView2.Source, UriKind.Absolute, out var uri)) return;
        var rules = AdminPolicyStore.ElementRules(uri.Host)
            .Where(r => !r.Allowed)
            .Select(r => new { selector = r.Selector, replacementText = r.ReplacementText ?? "", replacementImageDataUri = TryLoadReplacementImageDataUri(r.ReplacementImagePath), imageFit = r.ReplacementImageFit == "cover" ? "cover" : "contain" })
            .ToArray();
        var json = JsonSerializer.Serialize(rules);
        var script = """
        (() => {
          const rules = __RULES__;
          const marker = "data-kiosk-replacement-for";
          // Remove the previous pass cleanly when an administrator edits the rules.
          document.querySelectorAll("[data-kiosk-replacement]").forEach(node => node.remove());
          document.querySelectorAll("[data-kiosk-hidden]").forEach(node => {
            const oldDisplay = node.getAttribute("data-kiosk-old-display") || "";
            const oldPriority = node.getAttribute("data-kiosk-old-display-priority") || "";
            if (oldDisplay) node.style.setProperty("display", oldDisplay, oldPriority);
            else node.style.removeProperty("display");
            node.removeAttribute("data-kiosk-hidden");
            node.removeAttribute("data-kiosk-old-display");
            node.removeAttribute("data-kiosk-old-display-priority");
          });
          const apply = () => {
            for (const rule of rules) {
              let elements;
              try { elements = document.querySelectorAll(rule.selector); } catch { continue; }
              for (const el of elements) {
                if (!(el instanceof HTMLElement) || el.hasAttribute("data-kiosk-replacement")) continue;
                if ((rule.replacementText || rule.replacementImageDataUri) && el.previousElementSibling?.getAttribute(marker) === rule.selector) continue;
                if (rule.replacementText || rule.replacementImageDataUri) {
                  const replacement = document.createElement("div");
                  replacement.setAttribute("data-kiosk-replacement", "true");
                  replacement.setAttribute(marker, rule.selector);
                  const computed = getComputedStyle(el);
                  replacement.style.boxSizing = computed.boxSizing;
                  replacement.style.display = computed.display === "none" ? "block" : computed.display;
                  replacement.style.width = computed.width;
                  replacement.style.minHeight = computed.height === "auto" ? "" : computed.height;
                  replacement.style.margin = computed.margin;
                  replacement.style.padding = computed.padding;
                  replacement.style.textAlign = computed.textAlign;
                  replacement.style.font = computed.font;
                  replacement.style.color = computed.color;
                  replacement.style.background = computed.backgroundColor;
                  replacement.style.border = computed.border;
                  replacement.style.borderRadius = computed.borderRadius;
                  replacement.style.overflow = "hidden";
                  if (rule.replacementImageDataUri) {
                    const image = document.createElement("img"); image.src = rule.replacementImageDataUri; image.alt = "תמונה חלופית"; image.draggable = false;
                    image.style.display = "block"; image.style.width = "100%"; image.style.height = "100%"; image.style.maxWidth = "100%"; image.style.maxHeight = "100%"; image.style.objectFit = rule.imageFit === "cover" ? "cover" : "contain"; image.style.objectPosition = "center";
                    replacement.appendChild(image);
                  } else { replacement.textContent = rule.replacementText; }
                  el.parentNode?.insertBefore(replacement, el);
                }
                if (!el.hasAttribute("data-kiosk-hidden")) {
                  el.setAttribute("data-kiosk-old-display", el.style.getPropertyValue("display"));
                  el.setAttribute("data-kiosk-old-display-priority", el.style.getPropertyPriority("display"));
                }
                el.style.setProperty("display", "none", "important");
                el.setAttribute("data-kiosk-hidden", "true");
              }
            }
          };
          apply();
          if (!window.__kioskElementObserver) {
            window.__kioskElementObserver = new MutationObserver(() => {
              if (window.__kioskElementApplying) return;
              window.__kioskElementApplying = true;
              try { apply(); } finally { window.__kioskElementApplying = false; }
            });
            window.__kioskElementObserver.observe(document.documentElement, { childList: true, subtree: true });
          }
          // Report real DOM matches on demand; keep this separate from the observer
          // to avoid flooding the host while modern pages continuously update.
          window.__kioskReportBlockedElements = () => {
            const found = new Map();
            for (const rule of rules) {
              let nodes;
              try { nodes = document.querySelectorAll(rule.selector); } catch { continue; }
              let count = 0;
              let sample = "";
              for (const el of nodes) {
                if (!(el instanceof HTMLElement) || !el.hasAttribute("data-kiosk-hidden")) continue;
                count++;
                if (!sample) sample = (el.innerText || el.getAttribute("aria-label") || el.getAttribute("title") || "").trim().replace(/\s+/g, " ").slice(0, 100);
              }
              if (count) found.set(rule.selector, {
                selector: rule.selector,
                tag: (() => { try { return document.querySelector(rule.selector)?.tagName?.toLowerCase() || "element"; } catch { return "element"; } })(),
                text: sample,
                count
              });
            }
            chrome.webview.postMessage({ type: "blocked-elements-report", items: [...found.values()].slice(0, 200) });
          };
        })();
        """.Replace("__RULES__", json, StringComparison.Ordinal);
        try { await _browser.CoreWebView2.ExecuteScriptAsync(script); }
        catch (Exception ex) { AppDiagnostics.Write("Apply admin element rules", ex); }
    }

    private static string? TryLoadReplacementImageDataUri(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            var mime = ext switch { ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".webp" => "image/webp", ".gif" => "image/gif", ".bmp" => "image/bmp", _ => null };
            if (mime is null || !File.Exists(path)) return null;
            var info = new FileInfo(path); if (info.Length <= 0 || info.Length > 8 * 1024 * 1024) return null;
            return $"data:{mime};base64,{Convert.ToBase64String(File.ReadAllBytes(path))}";
        }
        catch (Exception ex) { AppDiagnostics.Write("Load replacement image", ex); return null; }
    }

    private void EnforceCurrentPage()
    {
        if (_browser.CoreWebView2 is null) return;
        if (!Uri.TryCreate(_browser.CoreWebView2.Source, UriKind.Absolute, out var current) ||
            !IsAllowedNavigationUri(current) ||
            IsBlockedNavigationUri(current))
        {
            _status.Text = "העמוד הזה אינו זמין בעמדת הטעינה.";
            _browser.CoreWebView2.Navigate(_activeStartUrl);
        }
    }

    private string BuildRouteGuardScript(string serviceStartUrl)
    {
        var serviceUri = new Uri(serviceStartUrl);
        var origin = serviceUri.GetLeftPart(UriPartial.Authority);
        var originJson = JsonSerializer.Serialize(origin);
        // Both services need multi-step workflows. Rav-Kav Online must move beyond
        // /he/store/connect; HopOn checkout may move between /read, /pay and payment.
        var strictPath = false;
        var strictPathJson = JsonSerializer.Serialize(strictPath);
        var allowedPathJson = JsonSerializer.Serialize(_settings.RavKavAllowedPath);
        var blockedPaths = _settings.IsRavKavUri(serviceUri) ? Array.Empty<string>() : _settings.BlockedPaths.ToArray();
        var blockedPathsJson = JsonSerializer.Serialize(blockedPaths);
        var paymentPathsJson = JsonSerializer.Serialize(_settings.HopOnPaymentPaths ?? ["/pay"]);
        // Rav-Kav transaction routes must not inherit stale administrator deny rules.
        var adminRulesJson = JsonSerializer.Serialize(_activeServiceProfile == "RavKavOnline"
            ? Array.Empty<object>()
            : AdminPolicyStore.Load().Routes
                .Where(r => r.Host.Equals(serviceUri.Host, StringComparison.OrdinalIgnoreCase))
                .Select(r => new { path = r.Path == "/" ? "/" : r.Path.TrimEnd('/').ToLowerInvariant(), allowed = r.Allowed }).ToArray());

        var script = """
        (() => {
            "use strict";
            if (window.top !== window) return;
            const allowedOrigin = __ALLOWED_ORIGIN_JSON__;
            const strictPath = __STRICT_PATH__;
            const allowedPath = __ALLOWED_PATH_JSON__;
            const paymentRoots = __PAYMENT_PATHS_JSON__.map(p =>
                String(p).replace(/\\/g, "/").replace(/\/+$/g, "").toLowerCase()
            );
            const blockedRoots = __BLOCKED_PATHS_JSON__.map(p =>
                String(p).replace(/\\/g, "/").replace(/\/+$/g, "").toLowerCase()
            );
            window.__kioskAdminRules = __ADMIN_RULES_JSON__;
            window.__kioskAdminMode = false;

            function normalizedPath(value) {
                try {
                    const url = new URL(String(value), location.href);
                    if (url.origin !== allowedOrigin) return null;

                    let path = url.pathname;
                    for (let i = 0; i < 10; i++) {
                        let decoded;
                        try { decoded = decodeURIComponent(path); }
                        catch { return "__BLOCK_INVALID__"; }
                        if (decoded === path) break;
                        path = decoded;
                    }
                    if (path.indexOf("%") !== -1) return "__BLOCK_INVALID__";

                    path = path.replace(/\\/g, "/").replace(/\/{2,}/g, "/");
                    const parts = [];
                    for (const part of path.split("/")) {
                        if (!part || part === ".") continue;
                        if (part === "..") { parts.pop(); continue; }
                        parts.push(part);
                    }
                    return ("/" + parts.join("/")).toLowerCase().replace(/\/+$/g, "") || "/";
                } catch {
                    return null;
                }
            }

            function isBlocked(value) {
                if (window.__kioskAdminMode === true) return false;
                const path = normalizedPath(value);
                if (path === "__BLOCK_INVALID__") return true;
                if (path === null) return false;
                if (!strictPath && paymentRoots.some(root => path === root || path.startsWith(root + "/"))) return false;
                const matchingRules = (window.__kioskAdminRules || [])
                    .filter(rule => rule.path === "/" || path === rule.path || path.startsWith(rule.path + "/"))
                    .sort((a, b) => b.path.length - a.path.length);
                if (matchingRules.length) return !matchingRules[0].allowed;
                if (strictPath) return path !== allowedPath.toLowerCase();
                return blockedRoots.some(root =>
                    path === root ||
                    path.startsWith(root + "/") ||
                    path.startsWith(root + ";") ||
                    path.startsWith(root + "?") ||
                    path.startsWith(root + "#")
                );
            }

            function notifyBlocked() {
                try { window.chrome.webview.postMessage("kiosk-blocked-route"); } catch {}
            }

            // Stop SPA routers before they can change the visible URL.
            for (const method of ["pushState", "replaceState"]) {
                const original = history[method];
                history[method] = function(state, title, url) {
                    if (!strictPath && url != null && isBlocked(url)) {
                        notifyBlocked();
                        return;
                    }
                    return Reflect.apply(original, this, arguments);
                };
            }

            // Stop ordinary links and form submissions in the capture phase.
            document.addEventListener("click", event => {
                const element = event.target instanceof Element ? event.target : null;
                const link = element?.closest("a[href]");
                if (link && isBlocked(link.href)) {
                    event.preventDefault();
                    event.stopImmediatePropagation();
                    event.stopPropagation();
                    notifyBlocked();
                }
            }, true);

            document.addEventListener("submit", event => {
                const form = event.target;
                if (!strictPath && form instanceof HTMLFormElement && isBlocked(form.action)) {
                    event.preventDefault();
                    event.stopImmediatePropagation();
                    event.stopPropagation();
                    notifyBlocked();
                }
            }, true);

            // Defensive fallback for a blocked entry already present in history.
            window.addEventListener("popstate", () => {
                if (!strictPath && isBlocked(location.href)) {
                    history.forward();
                    notifyBlocked();
                }
            }, true);
        })();
        """;

        return script
            .Replace("__ALLOWED_ORIGIN_JSON__", originJson, StringComparison.Ordinal)
            .Replace("__STRICT_PATH__", strictPathJson, StringComparison.Ordinal)
            .Replace("__ALLOWED_PATH_JSON__", allowedPathJson, StringComparison.Ordinal)
            .Replace("__BLOCKED_PATHS_JSON__", blockedPathsJson, StringComparison.Ordinal)
            .Replace("__PAYMENT_PATHS_JSON__", paymentPathsJson, StringComparison.Ordinal)
            .Replace("__ADMIN_RULES_JSON__", adminRulesJson, StringComparison.Ordinal);
    }

    private void OnFrameNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var target))
        {
            e.Cancel = true;
            AppDiagnostics.Write("Frame navigation blocked", details: "Invalid frame URI");
            return;
        }

        // Rav-Kav Online starts its locally installed card-reader helper through a
        // custom URI scheme. Some site versions trigger that scheme from a hidden
        // iframe rather than the top-level document. Do not cancel it here: allow
        // WebView2 to raise LaunchingExternalUriScheme, where the explicit approval
        // dialog and trusted-origin check are enforced.
        if (_activeServiceProfile == "RavKavOnline" && IsRavKavReaderProtocol(target))
        {
            AppDiagnostics.Write("Rav-Kav reader frame protocol allowed",
                details: "Scheme: " + target.Scheme + "; host: " + target.Host);
            return;
        }

        // CAPTCHA providers run in cross-origin iframes. Permit only their dedicated
        // challenge endpoints while HopOn is active.
        if (_activeServiceProfile == "HopOn" && IsHopOnVerificationUri(target))
        {
            AppDiagnostics.Write("HopOn verification frame allowed", details: SafeOriginForLog(target.GetLeftPart(UriPartial.Authority)));
            return;
        }

        if (!IsAllowedNavigationUri(target) || IsBlockedNavigationUri(target))
        {
            e.Cancel = true;
            _status.Text = "העמוד המבוקש חסום בעמדה זו.";
            AppDiagnostics.Write("Frame navigation blocked",
                details: target.GetLeftPart(UriPartial.Authority) + " | scheme: " + target.Scheme);
        }
    }

    private void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        if (!Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var target) ||
            !IsAllowedNavigationUri(target) ||
            !IsBlockedNavigationUri(target))
            return;

        // A second network-level barrier: never pass a blocked HTML document to Chromium.
        e.Response = _browser.CoreWebView2.Environment.CreateWebResourceResponse(
            new MemoryStream(Array.Empty<byte>()),
            403,
            "Blocked by kiosk policy",
            "Content-Type: text/plain; charset=utf-8\r\nCache-Control: no-store");
    }

    private void ShowHome()
    {
        _browser.Visible = false;
        _home.Visible = true;
        _back.Visible = false;
        _status.Text = "מוכן לטעינה";
    }

    private static bool IsRavKavReaderProtocol(Uri uri) =>
        // Rav-Kav uses ravkav:wss://proxy.ravkavonline.co.il/<session-id>
        uri.Scheme.Equals("ravkav", StringComparison.OrdinalIgnoreCase) ||
        uri.Scheme.Equals("ravkavonline", StringComparison.OrdinalIgnoreCase) ||
        uri.Scheme.Equals("ravkavonline.exe", StringComparison.OrdinalIgnoreCase);

    private static bool IsHopOnVerificationUri(Uri uri)
    {
        if (!uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort ||
            !string.IsNullOrEmpty(uri.UserInfo)) return false;

        var host = uri.IdnHost.ToLowerInvariant();
        var path = uri.AbsolutePath;
        return host switch
        {
            "google.com" or "www.google.com" => path.StartsWith("/recaptcha/", StringComparison.OrdinalIgnoreCase),
            "recaptcha.net" or "www.recaptcha.net" => path.StartsWith("/recaptcha/", StringComparison.OrdinalIgnoreCase),
            "gstatic.com" or "www.gstatic.com" => path.StartsWith("/recaptcha/", StringComparison.OrdinalIgnoreCase),
            "challenges.cloudflare.com" => path.StartsWith("/cdn-cgi/challenge-platform/", StringComparison.OrdinalIgnoreCase) ||
                                            path.StartsWith("/turnstile/", StringComparison.OrdinalIgnoreCase),
            "hcaptcha.com" or "www.hcaptcha.com" => path.StartsWith("/1/", StringComparison.OrdinalIgnoreCase),
            "newassets.hcaptcha.com" or "js.hcaptcha.com" => path.StartsWith("/captcha/", StringComparison.OrdinalIgnoreCase) ||
                                                               path.StartsWith("/1/", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var external) && IsRavKavReaderProtocol(external))
        {
            // Let WebView2 raise LaunchingExternalUriScheme and show its native
            // confirmation prompt. Do not cancel this navigation here.
            _status.Text = "ממתין לאישור הפעלת קורא הכרטיסים...";
            AppDiagnostics.Write("Reader protocol navigation requested", new InvalidOperationException("Scheme: " + external.Scheme));
            return;
        }

        if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var target) ||
            !IsAllowedNavigationUri(target) ||
            IsBlockedNavigationUri(target))
        {
            e.Cancel = true;
            _status.Text = "העמוד המבוקש חסום בעמדה זו.";
            AppDiagnostics.Write("Navigation blocked", new InvalidOperationException("Blocked URI: " + e.Uri));
        }
    }

    private static bool IsGooglePayUri(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return false;

        var host = uri.IdnHost.TrimEnd('.').ToLowerInvariant();
        return host.Equals("pay.google.com", StringComparison.Ordinal) ||
               host.EndsWith(".pay.google.com", StringComparison.Ordinal) ||
               host.Equals("payments.google.com", StringComparison.Ordinal) ||
               host.EndsWith(".payments.google.com", StringComparison.Ordinal) ||
               host.Equals("wallet.google.com", StringComparison.Ordinal) ||
               host.EndsWith(".wallet.google.com", StringComparison.Ordinal);
    }

    private async void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        if (IsGooglePayUri(e.Uri))
        {
            e.Handled = true;
            _status.Text = "חלון Google Pay נחסם בהתאם להגדרות המערכת.";
            AppDiagnostics.Write("Google Pay popup blocked",
                details: "Target: " + SafeOriginForLog(e.Uri));
            return;
        }

        if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var target) && IsRavKavReaderProtocol(target))
        {
            // Custom reader URI is handled by LaunchingExternalUriScheme, not an HTTPS popup.
            e.Handled = false;
            AppDiagnostics.Write("Reader protocol popup forwarded",
                details: "Scheme: " + target.Scheme + "; top-level: " + SafeOriginForLog(_browser.CoreWebView2?.Source ?? ""));
            return;
        }

        var isBlankBootstrap = Uri.TryCreate(e.Uri, UriKind.Absolute, out target) &&
            target.Equals(new Uri("about:blank")) && e.IsUserInitiated;
        if (!isBlankBootstrap &&
            (!Uri.TryCreate(e.Uri, UriKind.Absolute, out target) ||
             !IsAllowedNavigationUri(target) || IsBlockedNavigationUri(target) ||
             !target.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            e.Handled = true;
            _status.Text = "חלון קופץ נחסם: הכתובת אינה מאובטחת או אינה מאושרת.";
            AppDiagnostics.Write("Popup blocked", details: "Invalid or blocked popup URI");
            return;
        }

        // Create a real in-app WebView2 popup using the exact same environment/profile.
        // This preserves window.opener/postMessage for authentication and reader flows.
        var deferral = e.GetDeferral();
        ServicePopupForm? popup = null;
        try
        {
            var environment = _browser.CoreWebView2?.Environment
                ?? throw new InvalidOperationException("WebView2 environment is not ready.");
            popup = new ServicePopupForm();
            await popup.InitializeAsync(environment);

            popup.Core.NavigationStarting += (_, navigation) =>
            {
                if (Uri.TryCreate(navigation.Uri, UriKind.Absolute, out var next) &&
                    next.Equals(new Uri("about:blank")))
                    return;
                if (IsGooglePayUri(navigation.Uri))
                {
                    navigation.Cancel = true;
                    _status.Text = "חלון Google Pay נחסם בהתאם להגדרות המערכת.";
                    AppDiagnostics.Write("Google Pay popup redirect blocked",
                        details: "Target: " + SafeOriginForLog(navigation.Uri));
                    return;
                }
                if (!Uri.TryCreate(navigation.Uri, UriKind.Absolute, out next) ||
                    !IsAllowedNavigationUri(next) || IsBlockedNavigationUri(next) ||
                    !next.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                {
                    navigation.Cancel = true;
                    AppDiagnostics.Write("Popup navigation blocked", details: "Unsafe popup redirect");
                }
            };

            e.NewWindow = popup.Core;
            e.Handled = false;
            popup.FormClosed += (_, _) => popup.Dispose();
            popup.Show(this);
            _status.Text = "נפתח חלון מאובטח לאישור/השלמת הפעולה...";
            AppDiagnostics.Write("Secure in-app popup opened",
                details: SafeOriginForLog(target.GetLeftPart(UriPartial.Authority)) +
                         "; user initiated: " + e.IsUserInitiated);
        }
        catch (Exception ex)
        {
            popup?.Dispose();
            e.Handled = true;
            _status.Text = "לא ניתן לפתוח חלון קופץ מאובטח.";
            AppDiagnostics.Write("Secure popup creation failed", ex);
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void OnLaunchingExternalUriScheme(object? sender, CoreWebView2LaunchingExternalUriSchemeEventArgs e)
    {
        if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var target) ||
            !IsRavKavReaderProtocol(target) ||
            !IsValidRavKavReaderUri(e.Uri))
        {
            e.Cancel = true;
            _status.Text = "בקשת הפעלת קורא לא מזוהה נחסמה.";
            AppDiagnostics.Write("Rav-Kav external protocol rejected",
                new InvalidOperationException("Unrecognized reader protocol request."));
            return;
        }

        var origin = e.InitiatingOrigin ?? string.Empty;
        var trustedOrigin = IsTrustedReaderOrigin(origin);
        if (!trustedOrigin && string.IsNullOrWhiteSpace(origin) &&
            Uri.TryCreate(_browser.CoreWebView2?.Source, UriKind.Absolute, out var topLevel))
        {
            origin = topLevel.GetLeftPart(UriPartial.Authority);
            trustedOrigin = IsTrustedReaderOrigin(origin);
        }

        if (!trustedOrigin || _activeServiceProfile != "RavKavOnline")
        {
            e.Cancel = true;
            _status.Text = "בקשת קורא הכרטיסים נדחתה: מקור לא מאושר.";
            AppDiagnostics.Write("Rav-Kav external protocol rejected",
                new InvalidOperationException("Origin: " + SafeOriginForLog(e.InitiatingOrigin ?? "") +
                                               "; top-level: " + SafeOriginForLog(_browser.CoreWebView2?.Source ?? "")));
            return;
        }

        // Chrome does not require a separate in-app confirmation for this trusted
        // reader agent. Dispatch the exact registered URI through Windows ShellExecute.
        // WebView2's own prompt is suppressed so the kiosk flow remains in one window.
        e.Cancel = true;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri,
                UseShellExecute = true,
                Verb = "open",
                WindowStyle = ProcessWindowStyle.Hidden
            });
            _status.Text = "בקשת ההפעלה נשלחה לתוכנת קורא הכרטיסים...";
            AppDiagnostics.Write("Rav-Kav reader agent dispatched",
                details: "Scheme: " + target.Scheme + "; origin: " + SafeOriginForLog(origin) +
                         "; uri length: " + e.Uri.Length);
        }
        catch (Exception ex)
        {
            _status.Text = "לא נמצאה תוכנת קורא רשומה ב-Windows. יש להתקין מחדש את רכיב הקורא.";
            AppDiagnostics.Write("Rav-Kav reader agent dispatch failed",
                new InvalidOperationException("Windows could not dispatch the registered reader protocol.", ex));
        }
    }

    private static bool IsValidRavKavReaderUri(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 4096 ||
            value.Any(char.IsControl)) return false;
        return value.StartsWith("ravkav:wss://proxy.ravkavonline.co.il/",
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTrustedReaderOrigin(string origin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
            !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return false;

        return uri.Host.Equals("hopon.co.il", StringComparison.OrdinalIgnoreCase) ||
               uri.Host.Equals("www.hopon.co.il", StringComparison.OrdinalIgnoreCase) ||
               uri.Host.Equals("ravkavonline.co.il", StringComparison.OrdinalIgnoreCase) ||
               uri.Host.Equals("www.ravkavonline.co.il", StringComparison.OrdinalIgnoreCase);
    }

    private static string SafeOriginForLog(string origin) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri) ? uri.GetLeftPart(UriPartial.Authority) : "(unknown origin)";

    private sealed class ReaderLaunchConfirmationForm : Form
    {
        public ReaderLaunchConfirmationForm(string origin, string scheme)
        {
            Text = "אישור הפעלת קורא הכרטיסים";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            ClientSize = new Size(520, 270);
            BackColor = Color.FromArgb(13, 25, 43);
            Font = new Font("Segoe UI", 10);
            Icon = SystemIcons.Shield;

            var header = new Panel { Dock = DockStyle.Top, Height = 66, BackColor = Color.FromArgb(25, 55, 78) };
            header.Controls.Add(new Label
            {
                Text = "אישור פתיחת תוכנה חיצונית",
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(255, 222, 161),
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            });

            var details = new Label
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(28, 22, 28, 12),
                ForeColor = Color.FromArgb(235, 241, 248),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 11),
                Text = "האתר מבקש להפעיל את תוכנת קורא הכרטיסים של רב־קו.\r\n\r\n" +
                       "מקור הבקשה: " + origin + "\r\n" +
                       "סוג הפעלה: " + scheme + "\r\n\r\n" +
                       "האם לאשר ל-Windows לפתוח את התוכנה המותקנת במחשב?"
            };

            var footer = new Panel { Dock = DockStyle.Bottom, Height = 72, BackColor = Color.FromArgb(9, 18, 32), Padding = new Padding(18, 12, 18, 12) };
            var cancel = new Button
            {
                Text = "לא עכשיו",
                DialogResult = DialogResult.No,
                Dock = DockStyle.Left,
                Width = 130,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(38, 52, 70),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                UseVisualStyleBackColor = false
            };
            cancel.FlatAppearance.BorderColor = Color.FromArgb(84, 104, 128);
            var approve = new Button
            {
                Text = "אישור והפעלת הקורא",
                DialogResult = DialogResult.Yes,
                Dock = DockStyle.Right,
                Width = 210,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(56, 125, 99),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                UseVisualStyleBackColor = false
            };
            approve.FlatAppearance.BorderColor = Color.FromArgb(122, 190, 153);
            footer.Controls.Add(cancel);
            footer.Controls.Add(approve);
            Controls.Add(details);
            Controls.Add(footer);
            Controls.Add(header);
            AcceptButton = approve;
            CancelButton = cancel;
        }
    }
}
