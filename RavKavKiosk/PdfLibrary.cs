using System.Drawing.Printing;
using System.Text.Json;
using PdfiumViewer;

namespace RavKavKiosk;

internal sealed record PdfBook(string Id, string Title, string FileName);

internal static class PdfLibraryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static string Root => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RavKavKiosk", "PdfLibrary");
    private static string CatalogPath => Path.Combine(Root, "catalog.json");

    public static string GetBookPath(PdfBook book) => Path.Combine(Root, book.FileName);

    public static List<PdfBook> GetBooks()
    {
        Directory.CreateDirectory(Root);
        try
        {
            if (!File.Exists(CatalogPath)) return [];
            var books = JsonSerializer.Deserialize<List<PdfBook>>(File.ReadAllText(CatalogPath)) ?? [];
            return books.Where(b => Guid.TryParseExact(b.Id, "N", out _) &&
                                    string.Equals(b.FileName, b.Id + ".pdf", StringComparison.OrdinalIgnoreCase) &&
                                    File.Exists(GetBookPath(b)))
                        .OrderBy(b => b.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
        }
        catch (Exception ex)
        {
            AppDiagnostics.Write("PDF catalog read", ex);
            return [];
        }
    }

    public static void Add(string sourcePath, string title)
    {
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("קובץ ה-PDF לא נמצא.", sourcePath);
        using (var input = File.OpenRead(sourcePath))
        {
            Span<byte> signature = stackalloc byte[5];
            if (input.Read(signature) != 5 || !signature.SequenceEqual("%PDF-"u8))
                throw new InvalidDataException("הקובץ שנבחר אינו קובץ PDF תקין.");
        }

        Directory.CreateDirectory(Root);
        var books = GetBooks();
        var id = Guid.NewGuid().ToString("N");
        var fileName = id + ".pdf";
        File.Copy(sourcePath, Path.Combine(Root, fileName), overwrite: false);
        books.Add(new PdfBook(id, CleanTitle(title), fileName));
        Save(books);
    }

    public static void Rename(string id, string title)
    {
        var books = GetBooks();
        var index = books.FindIndex(b => b.Id == id);
        if (index < 0) throw new InvalidOperationException("הספר לא נמצא בספרייה.");
        books[index] = books[index] with { Title = CleanTitle(title) };
        Save(books);
    }

    public static void Remove(string id)
    {
        var books = GetBooks();
        var book = books.FirstOrDefault(b => b.Id == id);
        if (book is null) return;
        books.Remove(book);
        Save(books);
        var path = GetBookPath(book);
        if (File.Exists(path)) File.Delete(path);
    }

    private static string CleanTitle(string title)
    {
        title = (title ?? "").Trim();
        if (title.Length == 0) throw new ArgumentException("יש להזין שם לספר.");
        return title.Length > 120 ? title[..120] : title;
    }

    private static void Save(List<PdfBook> books)
    {
        Directory.CreateDirectory(Root);
        var temp = CatalogPath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(books, JsonOptions));
        File.Move(temp, CatalogPath, overwrite: true);
    }
}

internal sealed class PdfLibraryManagerForm : Form
{
    private readonly ListBox _books = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly TextBox _title = new() { Width = 280, PlaceholderText = "שם הספר שיוצג בספרייה" };

    public PdfLibraryManagerForm()
    {
        Text = "ניהול ספריית PDF";
        Width = 760; Height = 560; MinimumSize = new Size(620, 420);
        StartPosition = FormStartPosition.CenterParent;
        RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
        BackColor = Color.FromArgb(11, 20, 34); ForeColor = Color.FromArgb(232, 239, 248);
        Font = new Font("Segoe UI", 10);
        BuildUi();
        RefreshBooks();
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), RowCount = 3, ColumnCount = 1 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        Controls.Add(root);
        root.Controls.Add(new Label
        {
            Text = "הוספה, שינוי שם והסרה של ספרים. הקבצים נשמרים במחשב המקומי.",
            Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight,
            ForeColor = Color.FromArgb(255, 222, 161), Font = new Font("Segoe UI", 11, FontStyle.Bold)
        }, 0, 0);
        _books.BackColor = Color.FromArgb(17, 30, 48);
        _books.ForeColor = Color.FromArgb(235, 242, 250);
        _books.Font = new Font("Segoe UI", 11);
        root.Controls.Add(_books, 0, 1);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        var add = MakeButton("הוסף קובץ PDF");
        add.Click += (_, _) => AddBook();
        var rename = MakeButton("שנה שם");
        rename.Click += (_, _) => RenameBook();
        var remove = MakeButton("הסר ספר");
        remove.Click += (_, _) => RemoveBook();
        var close = MakeButton("סגירה");
        close.Click += (_, _) => Close();
        actions.Controls.AddRange([add, rename, remove, close]);
        root.Controls.Add(actions, 0, 2);
    }

    private static Button MakeButton(string text) => new()
    {
        Text = text, AutoSize = true, Height = 34, FlatStyle = FlatStyle.Flat,
        BackColor = Color.FromArgb(31, 49, 72), ForeColor = Color.White,
        Font = new Font("Segoe UI", 9, FontStyle.Bold), Cursor = Cursors.Hand,
        Margin = new Padding(5)
    };

    private void RefreshBooks(string? selectId = null)
    {
        _books.Items.Clear();
        foreach (var book in PdfLibraryStore.GetBooks()) _books.Items.Add(new BookItem(book));
        if (selectId is not null)
            for (var i = 0; i < _books.Items.Count; i++)
                if (((BookItem)_books.Items[i]!).Book.Id == selectId) _books.SelectedIndex = i;
    }

    private void AddBook()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "בחירת ספר PDF להוספה",
            Filter = "קובצי PDF (*.pdf)|*.pdf",
            Multiselect = true, CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        foreach (var path in dialog.FileNames)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            using var prompt = new PdfTitlePrompt(name);
            if (prompt.ShowDialog(this) != DialogResult.OK) continue;
            try { PdfLibraryStore.Add(path, prompt.TitleValue); }
            catch (Exception ex) { MessageBox.Show(this, $"לא ניתן להוסיף את {Path.GetFileName(path)}:\n{ex.Message}", "שגיאה", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
        RefreshBooks();
    }

    private void RenameBook()
    {
        if (_books.SelectedItem is not BookItem selected) return;
        using var prompt = new PdfTitlePrompt(selected.Book.Title);
        if (prompt.ShowDialog(this) != DialogResult.OK) return;
        try { PdfLibraryStore.Rename(selected.Book.Id, prompt.TitleValue); RefreshBooks(selected.Book.Id); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "שגיאה", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void RemoveBook()
    {
        if (_books.SelectedItem is not BookItem selected) return;
        if (MessageBox.Show(this, $"להסיר את הספר ״{selected.Book.Title}״ מהספרייה?", "אישור הסרה",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        try { PdfLibraryStore.Remove(selected.Book.Id); RefreshBooks(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "שגיאה", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private sealed class BookItem(PdfBook book)
    {
        public PdfBook Book { get; } = book;
        public override string ToString() => Book.Title;
    }
}

internal sealed class PdfTitlePrompt : Form
{
    private readonly TextBox _input = new() { Dock = DockStyle.Top, Height = 34 };
    public string TitleValue => _input.Text.Trim();

    public PdfTitlePrompt(string initial)
    {
        Text = "שם הספר"; Width = 420; Height = 155; StartPosition = FormStartPosition.CenterParent;
        RightToLeft = RightToLeft.Yes; RightToLeftLayout = true; FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false; _input.Text = initial;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), RowCount = 2 };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.Controls.Add(_input, 0, 0);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        var ok = new Button { Text = "אישור", DialogResult = DialogResult.OK, Width = 90 };
        var cancel = new Button { Text = "ביטול", DialogResult = DialogResult.Cancel, Width = 90 };
        buttons.Controls.AddRange([ok, cancel]); root.Controls.Add(buttons, 0, 1);
        Controls.Add(root); AcceptButton = ok; CancelButton = cancel;
    }
}

internal sealed class PdfLibraryViewerForm : Form
{
    private readonly ListBox _books = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly PictureBox _pageImage = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(31, 42, 55) };
    private readonly Label _pageLabel = new() { AutoSize = true, ForeColor = Color.White, Padding = new Padding(10, 8, 10, 0) };
    private readonly Button _previous = new() { Text = "הקודם" };
    private readonly Button _next = new() { Text = "הבא" };
    private readonly Button _zoomIn = new() { Text = "הגדלה +" };
    private readonly Button _zoomOut = new() { Text = "הקטנה −" };
    private readonly ComboBox _printLayout = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly TextBox _search = new() { Width = 220, PlaceholderText = "חיפוש טקסט בספר..." };
    private PdfDocument? _document;
    private PdfBook? _selectedBook;
    private int _pageIndex;
    private float _zoom = 1f;

    public PdfLibraryViewerForm()
    {
        Text = "ספריית PDF — צפייה והדפסה";
        Width = 1180; Height = 820; MinimumSize = new Size(900, 620);
        StartPosition = FormStartPosition.CenterParent;
        RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
        BackColor = Color.FromArgb(9, 18, 34); ForeColor = Color.White;
        Font = new Font("Segoe UI", 10);
        BuildUi(); LoadBooks();
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 2, RowCount = 1 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(root);
        var libraryPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        libraryPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        libraryPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        libraryPanel.Controls.Add(new Label { Text = "ספרים זמינים", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, ForeColor = Color.FromArgb(255, 222, 161), Font = new Font("Segoe UI", 12, FontStyle.Bold) }, 0, 0);
        _books.BackColor = Color.FromArgb(17, 30, 48); _books.ForeColor = Color.White; _books.Font = new Font("Segoe UI", 10);
        _books.SelectedIndexChanged += (_, _) => SelectBook();
        libraryPanel.Controls.Add(_books, 0, 1);
        root.Controls.Add(libraryPanel, 0, 0);

        var reader = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
        reader.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        reader.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        reader.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = true };
        _previous.Click += (_, _) => ChangePage(-1);
        _next.Click += (_, _) => ChangePage(1);
        _zoomIn.Click += (_, _) => { _zoom = Math.Min(2.5f, _zoom + .15f); RenderPage(); };
        _zoomOut.Click += (_, _) => { _zoom = Math.Max(.5f, _zoom - .15f); RenderPage(); };
        _printLayout.Items.AddRange(["עמוד אחד בדף", "שני עמודים בדף (ימין ← שמאל)"]);
        _printLayout.SelectedIndex = 0;
        var find = new Button { Text = "חפש", AutoSize = true };
        find.Click += (_, _) => FindText();
        toolbar.Controls.AddRange([_previous, _next, _zoomIn, _zoomOut, new Label { Text = "פריסת הדפסה:", AutoSize = true, Padding = new Padding(6, 8, 0, 0) }, _printLayout, _search, find]);
        var print = new Button { Text = "הדפס למדפסת ברירת המחדל", Dock = DockStyle.Left, Width = 230, Height = 34 };
        print.Click += (_, _) => PrintBook();
        var footer = new Panel { Dock = DockStyle.Fill };
        footer.Controls.Add(_pageLabel); footer.Controls.Add(print);
        reader.Controls.Add(toolbar, 0, 0); reader.Controls.Add(_pageImage, 0, 1); reader.Controls.Add(footer, 0, 2);
        root.Controls.Add(reader, 1, 0);
        _pageImage.ContextMenuStrip = new ContextMenuStrip();
    }

    private void LoadBooks()
    {
        _books.Items.Clear();
        foreach (var book in PdfLibraryStore.GetBooks()) _books.Items.Add(new BookItem(book));
        if (_books.Items.Count > 0) _books.SelectedIndex = 0;
        else _pageLabel.Text = "עדיין לא הוגדרו ספרים. יש לפנות למנהל המערכת.";
    }

    private void SelectBook()
    {
        if (_books.SelectedItem is not BookItem item) return;
        try
        {
            _document?.Dispose();
            _selectedBook = item.Book;
            _document = PdfDocument.Load(PdfLibraryStore.GetBookPath(item.Book));
            _pageIndex = 0; _zoom = 1f;
            RenderPage();
        }
        catch (Exception ex)
        {
            _document?.Dispose(); _document = null;
            _pageImage.Image = null;
            MessageBox.Show(this, "לא ניתן לפתוח את הספר: " + ex.Message, "שגיאה בפתיחת PDF", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ChangePage(int delta)
    {
        if (_document is null) return;
        _pageIndex = Math.Clamp(_pageIndex + delta, 0, _document.PageCount - 1);
        RenderPage();
    }

    private void RenderPage()
    {
        if (_document is null) return;
        try
        {
            var size = _document.PageSizes[_pageIndex];
            var ratio = size.Width / Math.Max(1, size.Height);
            var height = Math.Max(500, (int)(850 * _zoom));
            var width = Math.Max(300, (int)(height * ratio));
            var rendered = _document.Render(_pageIndex, width, height, 96, 96, true);
            var old = _pageImage.Image; _pageImage.Image = rendered; old?.Dispose();
            _pageLabel.Text = $"{_selectedBook?.Title}  |  עמוד {_pageIndex + 1} מתוך {_document.PageCount}";
            _previous.Enabled = _pageIndex > 0; _next.Enabled = _pageIndex < _document.PageCount - 1;
        }
        catch (Exception ex) { AppDiagnostics.Write("PDF page render", ex); }
    }

    private void FindText()
    {
        if (_document is null || string.IsNullOrWhiteSpace(_search.Text)) return;
        var term = _search.Text.Trim();
        for (var page = _pageIndex + 1; page < _document.PageCount; page++)
        {
            if (_document.GetPdfText(page).Contains(term, StringComparison.CurrentCultureIgnoreCase))
            {
                _pageIndex = page; RenderPage(); return;
            }
        }
        for (var page = 0; page <= _pageIndex; page++)
        {
            if (_document.GetPdfText(page).Contains(term, StringComparison.CurrentCultureIgnoreCase))
            {
                _pageIndex = page; RenderPage(); return;
            }
        }
        MessageBox.Show(this, "הטקסט לא נמצא בספר. בספרים סרוקים ללא שכבת טקסט החיפוש אינו זמין.", "חיפוש בספר", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void PrintBook()
    {
        if (_document is null || _selectedBook is null) return;
        try
        {
            using var printDocument = new PrintDocument();
            var settings = new PrinterSettings();
            if (!settings.IsValid || string.IsNullOrWhiteSpace(settings.PrinterName))
            {
                MessageBox.Show(this, "לא הוגדרה מדפסת ברירת מחדל ב-Windows.", "אין מדפסת", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            printDocument.PrinterSettings = settings;
            var twoUp = _printLayout.SelectedIndex == 1;
            printDocument.DefaultPageSettings.Landscape = twoUp;
            var nextPdfPage = 0;
            printDocument.BeginPrint += (_, _) => nextPdfPage = 0;
            printDocument.PrintPage += (_, e) =>
            {
                var bounds = e.MarginBounds;
                if (!twoUp)
                {
                    DrawPdfPage(e.Graphics, nextPdfPage++, bounds);
                }
                else
                {
                    var half = bounds.Width / 2;
                    var rightCell = new Rectangle(bounds.Left + half, bounds.Top, bounds.Width - half, bounds.Height);
                    var leftCell = new Rectangle(bounds.Left, bounds.Top, half, bounds.Height);
                    DrawPdfPage(e.Graphics, nextPdfPage++, rightCell);
                    if (nextPdfPage < _document.PageCount) DrawPdfPage(e.Graphics, nextPdfPage++, leftCell);
                }
                e.HasMorePages = nextPdfPage < _document.PageCount;
            };
            printDocument.Print();
        }
        catch (Exception ex)
        {
            AppDiagnostics.Write("PDF printing", ex);
            MessageBox.Show(this, "ההדפסה נכשלה: " + ex.Message, "שגיאת הדפסה", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DrawPdfPage(Graphics graphics, int page, Rectangle target)
    {
        if (_document is null || page >= _document.PageCount) return;
        var size = _document.PageSizes[page];
        var ratio = size.Width / Math.Max(1, size.Height);
        var width = Math.Max(400, (int)(target.Width * 2.5));
        var height = Math.Max(400, (int)(width / Math.Max(.1f, ratio)));
        if (height > target.Height * 2.5) { height = (int)(target.Height * 2.5); width = (int)(height * ratio); }
        using var image = _document.Render(page, width, height, 300, 300, true);
        var fitted = FitRectangle(target, image.Width, image.Height);
        graphics.DrawImage(image, fitted);
    }

    private static Rectangle FitRectangle(Rectangle bounds, int imageWidth, int imageHeight)
    {
        var scale = Math.Min((float)bounds.Width / imageWidth, (float)bounds.Height / imageHeight);
        var width = (int)(imageWidth * scale); var height = (int)(imageHeight * scale);
        return new Rectangle(bounds.Left + (bounds.Width - width) / 2, bounds.Top + (bounds.Height - height) / 2, width, height);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _document?.Dispose();
        var image = _pageImage.Image; _pageImage.Image = null; image?.Dispose();
        base.OnFormClosed(e);
    }

    private sealed class BookItem(PdfBook book)
    {
        public PdfBook Book { get; } = book;
        public override string ToString() => Book.Title;
    }
}
