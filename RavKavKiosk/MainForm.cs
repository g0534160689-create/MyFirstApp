using System.Reflection;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace RavKavKiosk;

internal sealed class MainForm : Form
{
    private readonly AppSettings _settings = AppSettings.Load();
    private readonly WebView2 _browser = new() { Dock = DockStyle.Fill, Visible = false };
    private readonly Panel _home = new() { Dock = DockStyle.Fill, BackColor = Color.FromArgb(10, 7, 5) };
    private readonly Label _status = new()
    {
        AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
        Font = new Font("Segoe UI", 9), ForeColor = Color.FromArgb(197, 184, 165),
        Text = "השירות ייפתח בתוך התוכנה"
    };
    private readonly Button _start = new();
    private readonly Button _ravKavStart = new();
    private string _activeStartUrl = "https://hopon.co.il/read";
    private string _activeServiceName = "HopOn";
    private readonly Button _back = new();
    private CoreWebView2Environment? _webViewEnvironment;
    private bool _browserInitialized;

    private static readonly Color Navy = Color.FromArgb(20, 15, 12);
    private static readonly Color Blue = Color.FromArgb(190, 137, 55);
    private static readonly Color Gold = Color.FromArgb(229, 184, 105);
    private readonly System.Windows.Forms.Timer _clockTimer = new() { Interval = 1000 };
    private readonly TextBox _searchBox = new();
    private readonly FlowLayoutPanel _cardsPanel = new();
    private readonly Label _dateClock = new();
    private string _activeFilter = "all";
    private readonly HashSet<string> _favorites = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _clickCounts = new(StringComparer.Ordinal);

    public MainForm()
    {
        Text = "ישיבת דעת אהרן | כל מה שצריך בלי לצאת מהישיבה";
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(760, 560);
        BackColor = Color.White;
        Font = new Font("Segoe UI", 10);

        var header = new Panel { Dock = DockStyle.Top, Height = 68, BackColor = Color.FromArgb(18, 13, 10), Padding = new Padding(30, 0, 30, 0) };
        var brand = new Label
        {
            Text = "ישיבת דעת אהרן     ✦     המרכז הדיגיטלי", Dock = DockStyle.Right, Width = 560,
            ForeColor = Gold, Font = new Font("Segoe UI", 18, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight
        };
        _back.Text = "חזרה למסך הראשי";
        _back.Dock = DockStyle.Left;
        _back.Width = 170;
        _back.FlatStyle = FlatStyle.Flat;
        _back.FlatAppearance.BorderColor = Color.FromArgb(100, 150, 190);
        _back.ForeColor = Color.White;
        _back.BackColor = Navy;
        _back.Visible = false;
        _back.Click += (_, _) => ShowHome();
        header.Controls.Add(brand);
        header.Controls.Add(_back);

        var footer = new Panel { Dock = DockStyle.Bottom, Height = 34, BackColor = Color.FromArgb(18, 13, 10), Padding = new Padding(12, 0, 12, 0) };
        footer.Controls.Add(_status);

        Controls.Add(_home);
        Controls.Add(_browser);
        Controls.Add(footer);
        Controls.Add(header);
        BuildHome();
        _clockTimer.Tick += (_, _) => UpdateDateClock();
        _clockTimer.Start();
        UpdateDateClock();
    }

    private sealed record PortalLink(string Id, string Title, string Subtitle, string Url, string Category, string Badge);

    private static readonly PortalLink[] PortalLinks =
    [
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
        _home.BackColor = Color.FromArgb(10, 7, 5);
        var bgPath = Path.Combine(AppContext.BaseDirectory, "Assets", "bg.jpg");
        if (File.Exists(bgPath))
        {
            try { using var image = Image.FromFile(bgPath); _home.BackgroundImage = new Bitmap(image); _home.BackgroundImageLayout = ImageLayout.Stretch; }
            catch (Exception ex) { AppDiagnostics.Write("Load background image", ex); }
        }

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7,
            Padding = new Padding(34, 16, 34, 16),
            BackColor = Color.FromArgb(218, 8, 6, 5)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 164));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));

        var meta = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(2) };
        var partnerPath = Path.Combine(AppContext.BaseDirectory, "Assets", "otzar-logo.png");
        if (File.Exists(partnerPath))
        {
            try
            {
                var picture = new PictureBox { Dock = DockStyle.Right, Width = 176, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(245, 255, 255, 255), Padding = new Padding(8) };
                using var image = Image.FromFile(partnerPath);
                picture.Image = new Bitmap(image);
                meta.Controls.Add(picture);
            }
            catch (Exception ex)
            {
                AppDiagnostics.Write("Load partner logo", ex);
                meta.Controls.Add(CreatePartnerFallback());
            }
        }
        else meta.Controls.Add(CreatePartnerFallback());

        _dateClock.Dock = DockStyle.Left;
        _dateClock.Width = 330;
        _dateClock.ForeColor = Color.FromArgb(242, 231, 210);
        _dateClock.BackColor = Color.FromArgb(205, 20, 15, 12);
        _dateClock.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        _dateClock.TextAlign = ContentAlignment.MiddleCenter;
        meta.Controls.Add(_dateClock);

        var hero = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(95, 12, 9, 7), Padding = new Padding(18, 8, 18, 8) };
        var heroLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.Transparent };
        heroLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        heroLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        var heroText = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Color.Transparent };
        heroText.RowStyles.Add(new RowStyle(SizeType.Percent, 44));
        heroText.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
        heroText.RowStyles.Add(new RowStyle(SizeType.Percent, 26));
        heroText.Controls.Add(new Label
        {
            Text = "ישיבת דעת אהרן", Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(255, 224, 157), Font = new Font("Segoe UI", 29, FontStyle.Bold),
            TextAlign = ContentAlignment.BottomRight
        }, 0, 0);
        heroText.Controls.Add(new Label
        {
            Text = "כל מה שצריך בלי לצאת מהישיבה", Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(250, 246, 237), Font = new Font("Segoe UI", 16, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight
        }, 0, 1);
        heroText.Controls.Add(new Label
        {
            Text = "בשיתוף מערכת אוצר החכמה  •  שע״י ישיבתנו הקדושה", Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(207, 184, 147), Font = new Font("Segoe UI", 10),
            TextAlign = ContentAlignment.TopRight
        }, 0, 2);

        var logo = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent, Padding = new Padding(10) };
        var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "daat-logo.png");
        if (File.Exists(logoPath))
        {
            try { using var image = Image.FromFile(logoPath); logo.Image = new Bitmap(image); }
            catch (Exception ex) { AppDiagnostics.Write("Load daat logo", ex); }
        }
        else
        {
            try
            {
                using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("RavKavKiosk.Assets.daat-logo.png");
                if (stream is not null) logo.Image = new Bitmap(stream);
            }
            catch (Exception ex) { AppDiagnostics.Write("Load embedded daat logo", ex); }
        }
        heroLayout.Controls.Add(heroText, 0, 0);
        heroLayout.Controls.Add(logo, 1, 0);
        hero.Controls.Add(heroLayout);

        var sectionTitle = new Label
        {
            Text = "המרכז הדיגיטלי של הישיבה     |     כל השירותים והקישורים במקום אחד",
            Dock = DockStyle.Fill, ForeColor = Color.FromArgb(229, 201, 155),
            Font = new Font("Segoe UI", 11, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent
        };

        var searchHost = new Panel
        {
            Dock = DockStyle.Fill, BackColor = Color.FromArgb(238, 23, 18, 14),
            Padding = new Padding(18, 5, 18, 5), Margin = new Padding(0, 4, 0, 4)
        };
        var searchLabel = new Label { Text = "⌕", Dock = DockStyle.Right, Width = 42, ForeColor = Gold, Font = new Font("Segoe UI", 25, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter };
        _searchBox.Dock = DockStyle.Fill;
        _searchBox.BorderStyle = BorderStyle.None;
        _searchBox.BackColor = Color.FromArgb(32, 24, 18);
        _searchBox.ForeColor = Color.FromArgb(251, 248, 242);
        _searchBox.Font = new Font("Segoe UI", 14);
        _searchBox.TextAlign = HorizontalAlignment.Right;
        _searchBox.PlaceholderText = "חיפוש מהיר — הקלד שם שירות, מבחן או נושא...";
        _searchBox.TextChanged += (_, _) => RenderCards();
        searchHost.Controls.Add(_searchBox);
        searchHost.Controls.Add(searchLabel);

        var filters = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false,
            AutoScroll = true, BackColor = Color.Transparent, Padding = new Padding(0, 3, 0, 3)
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
        _cardsPanel.BackColor = Color.Transparent;
        _cardsPanel.Padding = new Padding(8, 9, 8, 9);

        var ravPanel = new Panel
        {
            Dock = DockStyle.Fill, BackColor = Color.FromArgb(238, 24, 18, 13),
            Padding = new Padding(16, 7, 16, 7)
        };
        var ravTitle = new Label
        {
            Text = "עמדת רב־קו  |  טעינה מאובטחת בתוך התוכנה",
            Dock = DockStyle.Top, Height = 30, ForeColor = Color.FromArgb(255, 220, 150),
            Font = new Font("Segoe UI", 12, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter
        };
        var ravButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false, BackColor = Color.Transparent, Padding = new Padding(0, 2, 0, 0)
        };
        _start.Text = "◈   טעינה באמצעות HopOn";
        StyleServiceButton(_start, Color.FromArgb(125, 83, 34));
        _start.Width = 270; _start.Height = 43; _start.Margin = new Padding(8, 2, 8, 2);
        _start.Click += async (_, _) => await OpenOfficialServiceAsync(_settings.StartUrl, "HopOn");
        _ravKavStart.Text = "◈   טעינה באמצעות רב־קו אונליין";
        StyleServiceButton(_ravKavStart, Color.FromArgb(91, 67, 37));
        _ravKavStart.Width = 300; _ravKavStart.Height = 43; _ravKavStart.Margin = new Padding(8, 2, 8, 2);
        _ravKavStart.Click += async (_, _) => await OpenOfficialServiceAsync(_settings.RavKavStartUrl, "רב־קו אונליין");
        ravButtons.Controls.Add(_start);
        ravButtons.Controls.Add(_ravKavStart);
        ravPanel.Controls.Add(ravButtons);
        ravPanel.Controls.Add(ravTitle);

        layout.Controls.Add(meta, 0, 0);
        layout.Controls.Add(hero, 0, 1);
        layout.Controls.Add(sectionTitle, 0, 2);
        layout.Controls.Add(searchHost, 0, 3);
        layout.Controls.Add(filters, 0, 4);
        layout.Controls.Add(_cardsPanel, 0, 5);
        layout.Controls.Add(ravPanel, 0, 6);
        _home.Controls.Add(layout);
        RenderCards();
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
        var button = new Button { Text = caption, Width = 145, Height = 36, Margin = new Padding(5, 2, 5, 2), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(225, 24, 18, 14), ForeColor = Color.FromArgb(197, 184, 165), Font = new Font("Segoe UI", 10, FontStyle.Bold), Cursor = Cursors.Hand, UseVisualStyleBackColor = false, Tag = filter };
        button.FlatAppearance.BorderColor = Color.FromArgb(95, 229, 184, 105);
        button.Click += (_, _) =>
        {
            _activeFilter = filter;
            foreach (Control item in host.Controls) if (item is Button b) { bool selected = Equals(b.Tag, _activeFilter); b.ForeColor = selected ? Color.FromArgb(253, 230, 138) : Color.FromArgb(197, 184, 165); b.BackColor = selected ? Color.FromArgb(95, 116, 75, 30) : Color.FromArgb(225, 24, 18, 14); }
            RenderCards();
        };
        if (filter == "all") { button.ForeColor = Color.FromArgb(253, 230, 138); button.BackColor = Color.FromArgb(95, 116, 75, 30); }
        host.Controls.Add(button);
    }

    private void RenderCards()
    {
        if (_cardsPanel.IsDisposed) return;
        _cardsPanel.SuspendLayout();
        foreach (Control old in _cardsPanel.Controls) old.Dispose();
        _cardsPanel.Controls.Clear();

        var query = _searchBox.Text.Trim();
        var filtered = PortalLinks.Where(link =>
            (string.IsNullOrEmpty(query) ||
             link.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
             link.Subtitle.Contains(query, StringComparison.CurrentCultureIgnoreCase)) &&
            (_activeFilter == "all" || (_activeFilter == "fav"
                ? _favorites.Contains(link.Id)
                : link.Category == _activeFilter)))
            .OrderByDescending(link => _favorites.Contains(link.Id))
            .ThenByDescending(link => _clickCounts.GetValueOrDefault(link.Id))
            .ToArray();

        if (filtered.Length == 0)
        {
            _cardsPanel.Controls.Add(new Label
            {
                Text = "לא נמצאו תוצאות\r\nנסה לשנות את מילות החיפוש",
                Width = 520, Height = 120, ForeColor = Color.FromArgb(231, 215, 184),
                Font = new Font("Segoe UI", 15, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent
            });
        }

        var availableWidth = Math.Max(340, _cardsPanel.ClientSize.Width - 42);
        var columns = availableWidth >= 1120 ? 3 : availableWidth >= 720 ? 2 : 1;
        var cardWidth = Math.Clamp((availableWidth - (columns * 22)) / columns, 320, 470);

        foreach (var link in filtered)
        {
            var categoryColor = link.Category switch
            {
                "tests" => Color.FromArgb(211, 164, 88),
                "services" => Color.FromArgb(111, 173, 143),
                _ => Color.FromArgb(160, 143, 203)
            };
            var card = new PremiumCardPanel
            {
                Width = cardWidth, Height = 184, Margin = new Padding(10),
                Cursor = Cursors.Hand, Tag = link, Padding = new Padding(17, 13, 17, 13),
                GradientTop = Color.FromArgb(247, 36, 29, 23),
                GradientBottom = Color.FromArgb(248, 20, 16, 13),
                BorderColor = Color.FromArgb(115, categoryColor),
                HoverBorderColor = Color.FromArgb(235, categoryColor)
            };

            var accent = new Panel
            {
                Dock = DockStyle.Right, Width = 5, BackColor = categoryColor,
                Margin = new Padding(0)
            };
            var icon = new Label
            {
                Text = link.Category switch { "tests" => "✧", "services" => "▣", _ => "▤" },
                Dock = DockStyle.Right, Width = 62,
                ForeColor = Color.FromArgb(255, 224, 164),
                Font = new Font("Segoe UI Symbol", 27, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.FromArgb(42, categoryColor)
            };
            var fav = new Button
            {
                Text = _favorites.Contains(link.Id) ? "★" : "☆",
                Dock = DockStyle.Left, Width = 42,
                FlatStyle = FlatStyle.Flat, ForeColor = Color.FromArgb(255, 218, 125),
                BackColor = Color.FromArgb(22, 255, 255, 255),
                Font = new Font("Segoe UI", 18, FontStyle.Bold), Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            fav.FlatAppearance.BorderSize = 0;
            fav.Click += (_, _) =>
            {
                if (!_favorites.Add(link.Id)) _favorites.Remove(link.Id);
                RenderCards();
            };

            var content = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(12, 0, 10, 0) };
            var topLine = new Panel { Dock = DockStyle.Top, Height = 3, BackColor = Color.Transparent };
            var badge = new Label
            {
                Text = "●  " + link.Badge.ToUpperInvariant(), Dock = DockStyle.Top, Height = 27,
                ForeColor = categoryColor, Font = new Font("Segoe UI", 9, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleRight
            };
            var title = new Label
            {
                Text = link.Title, Dock = DockStyle.Top, Height = 42,
                ForeColor = Color.FromArgb(255, 250, 239),
                Font = new Font("Segoe UI", 17, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleRight, Cursor = Cursors.Hand
            };
            var description = new Label
            {
                Text = link.Subtitle, Dock = DockStyle.Top, Height = 38,
                ForeColor = Color.FromArgb(198, 188, 171),
                Font = new Font("Segoe UI", 10),
                TextAlign = ContentAlignment.TopRight, Cursor = Cursors.Hand
            };
            var bottom = new Label
            {
                Text = "כניסה לשירות  ←      " + _clickCounts.GetValueOrDefault(link.Id) + " כניסות",
                Dock = DockStyle.Bottom, Height = 30,
                ForeColor = Color.FromArgb(245, 207, 133),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleRight, Cursor = Cursors.Hand
            };
            content.Controls.Add(bottom);
            content.Controls.Add(description);
            content.Controls.Add(title);
            content.Controls.Add(badge);
            content.Controls.Add(topLine);
            card.Controls.Add(content);
            card.Controls.Add(icon);
            card.Controls.Add(fav);
            card.Controls.Add(accent);

            void Launch()
            {
                _clickCounts[link.Id] = _clickCounts.GetValueOrDefault(link.Id) + 1;
                RenderCards();
                _ = OpenOfficialServiceAsync(link.Url, link.Title);
            }
            card.Click += (_, _) => Launch();
            content.Click += (_, _) => Launch();
            title.Click += (_, _) => Launch();
            description.Click += (_, _) => Launch();
            bottom.Click += (_, _) => Launch();
            icon.Click += (_, _) => Launch();
            _cardsPanel.Controls.Add(card);
        }
        _cardsPanel.ResumeLayout();
    }

    private sealed class PremiumCardPanel : Panel
    {
        public Color GradientTop { get; set; } = Color.FromArgb(40, 35, 28);
        public Color GradientBottom { get; set; } = Color.FromArgb(20, 17, 14);
        public Color BorderColor { get; set; } = Color.FromArgb(90, 200, 170, 110);
        public Color HoverBorderColor { get; set; } = Color.FromArgb(240, 220, 180, 120);
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
            var rect = new Rectangle(1, 1, Width - 3, Height - 3);
            using var path = RoundedRectangle(rect, 18);
            using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                rect, _hovered ? Color.FromArgb(255, 48, 38, 28) : GradientTop,
                _hovered ? Color.FromArgb(255, 27, 21, 16) : GradientBottom, 90f);
            using var pen = new Pen(_hovered ? HoverBorderColor : BorderColor, _hovered ? 2.2f : 1.2f);
            e.Graphics.FillPath(brush, path);
            e.Graphics.DrawPath(pen, path);
        }

        private static System.Drawing.Drawing2D.GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            var d = radius * 2;
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
        _start.Enabled = false;
        _ravKavStart.Enabled = false;
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
                // Keep one WebView2 environment for this app and store its profile
                // under the current user's LocalAppData (not beside the EXE).
                var userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "RavKavKiosk",
                    "WebView2");
                Directory.CreateDirectory(userDataFolder);

                _webViewEnvironment ??= await CoreWebView2Environment.CreateAsync(
                    browserExecutableFolder: null,
                    userDataFolder: userDataFolder);

                await _browser.EnsureCoreWebView2Async(_webViewEnvironment);
                var core = _browser.CoreWebView2;
                core.Settings.AreDefaultContextMenusEnabled = false;
                core.Settings.AreDevToolsEnabled = false;
                core.Settings.IsStatusBarEnabled = false;
                core.Settings.IsZoomControlEnabled = false;
                core.Settings.AreBrowserAcceleratorKeysEnabled = false;
                // Install every blocking layer before the first page navigation.
                core.NavigationStarting += OnNavigationStarting;
                core.FrameNavigationStarting += OnFrameNavigationStarting;
                core.HistoryChanged += (_, _) => EnforceCurrentPage();
                core.NewWindowRequested += OnNewWindowRequested;

                core.AddWebResourceRequestedFilter(
                    "https://hopon.co.il/*",
                    CoreWebView2WebResourceContext.Document,
                    CoreWebView2WebResourceRequestSourceKinds.All);
                core.WebResourceRequested += OnWebResourceRequested;

                await core.AddScriptToExecuteOnDocumentCreatedAsync(BuildRouteGuardScript(_settings.StartUrl));
                await core.AddScriptToExecuteOnDocumentCreatedAsync(BuildRouteGuardScript(_settings.RavKavStartUrl));
                core.DownloadStarting += (_, args) => args.Cancel = true;
                core.LaunchingExternalUriScheme += OnLaunchingExternalUriScheme;
                core.PermissionRequested += (_, args) => args.State = CoreWebView2PermissionState.Deny;
                core.SourceChanged += (_, _) =>
                {
                    EnforceCurrentPage();
                    if (_browser.CoreWebView2 is not null &&
                        Uri.TryCreate(_browser.CoreWebView2.Source, UriKind.Absolute, out var current) &&
                        _settings.IsAllowedNavigationUri(current) &&
                        !_settings.IsBlockedNavigationUri(current))
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
                };
                core.ProcessFailed += (_, args) =>
                {
                    var error = new InvalidOperationException(
                        $"WebView2 process failed: {args.ProcessFailedKind}");
                    AppDiagnostics.Write("WebView2 ProcessFailed", error);
                    _status.Text = "רכיב הדפדפן הפסיק לפעול. חזור ונסה שוב.";
                };
                _browserInitialized = true;
            }

            if (!Uri.TryCreate(startUrl, UriKind.Absolute, out var start) ||
                !_settings.IsAllowedNavigationUri(start) ||
                _settings.IsBlockedNavigationUri(start))
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
            _start.Enabled = true;
            _ravKavStart.Enabled = true;
        }
    }

    private void EnforceCurrentPage()
    {
        if (_browser.CoreWebView2 is null) return;
        if (!Uri.TryCreate(_browser.CoreWebView2.Source, UriKind.Absolute, out var current) ||
            !_settings.IsAllowedNavigationUri(current) ||
            (_settings.IsBlockedNavigationUri(current) && !_settings.IsRavKavUri(current)))
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
        var strictPath = _settings.IsRavKavUri(serviceUri);
        var strictPathJson = JsonSerializer.Serialize(strictPath);
        var allowedPathJson = JsonSerializer.Serialize(_settings.RavKavAllowedPath);
        var blockedPathsJson = JsonSerializer.Serialize(_settings.BlockedPaths);

        var script = """
        (() => {
            "use strict";
            if (window.top !== window) return;
            const allowedOrigin = __ALLOWED_ORIGIN_JSON__;
            const strictPath = __STRICT_PATH__;
            const allowedPath = __ALLOWED_PATH_JSON__;
            const blockedRoots = __BLOCKED_PATHS_JSON__.map(p =>
                String(p).replace(/\\/g, "/").replace(/\/+$/g, "").toLowerCase()
            );

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
                const path = normalizedPath(value);
                if (path === "__BLOCK_INVALID__") return true;
                if (path === null) return false;
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
            .Replace("__BLOCKED_PATHS_JSON__", blockedPathsJson, StringComparison.Ordinal);
    }

    private void OnFrameNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var target) &&
            _settings.IsHopOnUri(target) &&
            _settings.IsBlockedNavigationUri(target))
        {
            e.Cancel = true;
            _status.Text = "העמוד המבוקש חסום בעמדה זו.";
        }
    }

    private void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        if (!Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var target) ||
            !_settings.IsAllowedNavigationUri(target) ||
            !_settings.IsBlockedNavigationUri(target))
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

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var target) ||
            !_settings.IsAllowedNavigationUri(target) ||
            (_settings.IsBlockedNavigationUri(target) && !_settings.IsRavKavUri(target)))
        {
            e.Cancel = true;
            _status.Text = "העמוד המבוקש חסום בעמדה זו.";
        }
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var target) &&
            _settings.IsAllowedNavigationUri(target) &&
            (!_settings.IsBlockedNavigationUri(target) || _settings.IsRavKavUri(target)))
            _browser.CoreWebView2.Navigate(target.ToString());
        else
            _status.Text = "חלון חיצוני נחסם.";
    }

    private void OnLaunchingExternalUriScheme(object? sender, CoreWebView2LaunchingExternalUriSchemeEventArgs e)
    {
        e.Cancel = true;
        _status.Text = "קישור חיצוני אינו מורשה בעמדה זו.";
    }
}
