namespace RavKavKioskV2;

internal sealed class LauncherForm : Form
{
    private readonly Label _status = new();
    private readonly Button _hopOn = new();
    private readonly Button _ravKav = new();

    public LauncherForm()
    {
        Text = "עמדת טעינת רב־קו — מערכת חדשה";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(820, 560);
        Size = new Size(1080, 700);
        BackColor = Color.FromArgb(12, 25, 42);
        Font = new Font("Segoe UI", 10);
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4,
            Padding = new Padding(42), BackColor = BackColor
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 28));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 28));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 19));
        Controls.Add(root);

        var title = new Label
        {
            Dock = DockStyle.Fill, Text = "עמדת טעינת רב־קו",
            ForeColor = Color.White, Font = new Font("Segoe UI", 30, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        };
        root.Controls.Add(title, 0, 0);

        StyleServiceButton(_hopOn, "HopOn", "טעינה דרך HopOn", Color.FromArgb(21, 93, 105));
        StyleServiceButton(_ravKav, "Rav-Kav Online", "טעינה דרך רב־קו אונליין", Color.FromArgb(43, 76, 130));
        _hopOn.Click += (_, _) => OpenService(ServiceKind.HopOn);
        _ravKav.Click += (_, _) => OpenService(ServiceKind.RavKavOnline);
        root.Controls.Add(_hopOn, 0, 1);
        root.Controls.Add(_ravKav, 0, 2);

        _status.Dock = DockStyle.Fill;
        _status.ForeColor = Color.FromArgb(183, 203, 222);
        _status.TextAlign = ContentAlignment.MiddleCenter;
        _status.Text = "בחר שירות. לכל אתר סביבת דפדפן נפרדת.";
        root.Controls.Add(_status, 0, 3);
    }

    private static void StyleServiceButton(Button button, string accessibleName, string caption, Color color)
    {
        button.Text = caption;
        button.AccessibleName = accessibleName;
        button.Dock = DockStyle.Fill;
        button.Margin = new Padding(20, 12, 20, 12);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Color.FromArgb(115, 164, 195);
        button.FlatAppearance.BorderSize = 2;
        button.BackColor = color;
        button.ForeColor = Color.White;
        button.Font = new Font("Segoe UI", 22, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
    }

    private void OpenService(ServiceKind service)
    {
        try
        {
            var form = new ServiceBrowserForm(service);
            form.FormClosed += (_, _) => _status.Text = "בחר שירות. לכל אתר סביבת דפדפן נפרדת.";
            form.Show(this);
            _status.Text = "נפתח שירות " + ServicePolicy.DisplayName(service);
        }
        catch (Exception ex)
        {
            Diagnostics.Write("Open service window", ex, service.ToString());
            MessageBox.Show(this, ex.Message, "לא ניתן לפתוח את השירות", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
