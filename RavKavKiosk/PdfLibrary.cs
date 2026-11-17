using System.Drawing.Printing;
using System.Text.Json;

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
    private readonly WebView2 _browser = new() { Dock = DockStyle.Fill };
    private readonly Label _status = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, ForeColor = Color.FromArgb(255, 222, 161) };
    private readonly Button _refresh = new() { Text = "רענון ספרייה", AutoSize = true };
    private readonly Button _back = new() { Text = "חזרה לספרייה", AutoSize = true };
    private bool _browserReady;
    private PdfBook? _selectedBook;

    public PdfLibraryViewerForm()
    {
        Text = "ספריית PDF — קורא מסמכים";
        Width = 1280; Height = 860; MinimumSize = new Size(960, 640);
        StartPosition = FormStartPosition.CenterParent;
        RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
        BackColor = Color.FromArgb(9, 18, 34); ForeColor = Color.White;
        Font = new Font("Segoe UI", 10);
        BuildUi();
        LoadBooks();
        Shown += async (_, _) => await InitializeBrowserAsync();
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 2, RowCount = 1 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 255));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var library = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, BackColor = Color.FromArgb(15, 27, 43), Padding = new Padding(8) };
        library.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        library.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        library.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        library.Controls.Add(new Label { Text = "ספרים זמינים", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, ForeColor = Color.FromArgb(255, 222, 161), Font = new Font("Segoe UI", 12, FontStyle.Bold) }, 0, 0);
        _books.BackColor = Color.FromArgb(17, 30, 48); _books.ForeColor = Color.White; _books.Font = new Font("Segoe UI", 10);
        _books.SelectedIndexChanged += (_, _) => OpenSelectedBook();
        library.Controls.Add(_books, 0, 1);
        _refresh.Dock = DockStyle.Fill; _refresh.FlatStyle = FlatStyle.Flat;
        _refresh.Click += (_, _) => LoadBooks();
        library.Controls.Add(_refresh, 0, 2);
        root.Controls.Add(library, 0, 0);

        var reader = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        reader.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        reader.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var toolbar = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(17, 30, 48), Padding = new Padding(8) };
        _status.Text = "בחר ספר מהרשימה כדי להתחיל לצפות";
        _back.Dock = DockStyle.Left; _back.FlatStyle = FlatStyle.Flat;
        _back.Click += (_, _) => { _selectedBook = null; _browser.CoreWebView2?.NavigateToString(WelcomeHtml()); _status.Text = "בחר ספר מהרשימה"; };
        toolbar.Controls.Add(_status); toolbar.Controls.Add(_back);
        _browser.DefaultBackgroundColor = Color.FromArgb(31, 42, 55);
        reader.Controls.Add(toolbar, 0, 0);
        reader.Controls.Add(_browser, 0, 1);
        root.Controls.Add(reader, 1, 0);
    }

    private async Task InitializeBrowserAsync()
    {
        try
        {
            await _browser.EnsureCoreWebView2Async();
            var core = _browser.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsZoomControlEnabled = true;
            core.DownloadStarting += (_, e) => { e.Cancel = true; };
            core.NewWindowRequested += (_, e) => { e.Handled = true; };
            core.NavigationCompleted += (_, e) =>
            {
                if (!e.IsSuccess) _status.Text = "לא ניתן להציג את הקובץ. בדוק שה-PDF תקין ונסה שוב.";
            };
            _browserReady = true;
            core.NavigateToString(WelcomeHtml());
            _status.Text = "תצוגת PDF בסגנון דפדפן מוכנה";
            if (_books.SelectedItem is BookItem) OpenSelectedBook();
        }
        catch (Exception ex)
        {
            AppDiagnostics.Write("PDF WebView2 initialization", ex);
            _status.Text = "רכיב הצפייה לא הופעל. יש לוודא ש-WebView2 Runtime מותקן.";
            MessageBox.Show(this, "לא ניתן להפעיל את קורא ה-PDF:\n" + ex.Message, "שגיאה בהפעלת הצופה", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string WelcomeHtml() => """
        <!doctype html><html lang="he" dir="rtl"><meta charset="utf-8">
        <style>html,body{height:100%;margin:0;background:#202b3a;color:#e9eef5;font:16px Segoe UI,Arial}
        body{display:grid;place-items:center}.welcome{text-align:center;padding:36px;border:1px solid #456078;border-radius:18px;background:#14253a}
        h2{color:#ffdea1;margin:0 0 12px}.muted{color:#b8c5d3}</style>
        <body><div class="welcome"><h2>ספריית הספרים הדיגיטלית</h2><div class="muted">בחר ספר מהרשימה כדי לפתוח אותו בקורא המסמכים.</div></div></body></html>
        """;

    private void LoadBooks()
    {
        var selectedId = (_books.SelectedItem as BookItem)?.Book.Id;
        _books.Items.Clear();
        foreach (var book in PdfLibraryStore.GetBooks()) _books.Items.Add(new BookItem(book));
        if (_books.Items.Count > 0)
        {
            var index = 0;
            if (selectedId is not null)
                for (var i = 0; i < _books.Items.Count; i++)
                    if (((BookItem)_books.Items[i]!).Book.Id == selectedId) { index = i; break; }
            _books.SelectedIndex = index;
        }
        else
        {
            _selectedBook = null;
            if (_browserReady) _browser.CoreWebView2.NavigateToString(WelcomeHtml());
            _status.Text = "עדיין לא הוגדרו ספרים. יש לפנות למנהל המערכת.";
        }
    }

    private void OpenSelectedBook()
    {
        if (_books.SelectedItem is not BookItem item) return;
        _selectedBook = item.Book;
        var path = PdfLibraryStore.GetBookPath(item.Book);
        if (!File.Exists(path))
        {
            _status.Text = "קובץ הספר לא נמצא במחשב.";
            return;
        }
        _status.Text = "פותח: " + item.Book.Title;
        if (_browserReady)
            _browser.CoreWebView2.Navigate(new Uri(path).AbsoluteUri);
    }

    private sealed class BookItem(PdfBook book)
    {
        public PdfBook Book { get; } = book;
        public override string ToString() => Book.Title;
    }
}
