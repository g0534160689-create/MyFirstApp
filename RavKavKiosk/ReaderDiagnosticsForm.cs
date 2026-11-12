using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using Microsoft.Web.WebView2.Core;

namespace RavKavKiosk;

internal sealed class ReaderDiagnosticsForm : Form
{
    private readonly TextBox report = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        WordWrap = false,
        Font = new Font("Consolas", 9.5f),
        BackColor = Color.FromArgb(14, 25, 41),
        ForeColor = Color.FromArgb(222, 235, 247),
        BorderStyle = BorderStyle.FixedSingle
    };

    public ReaderDiagnosticsForm()
    {
        Text = "אבחון קורא כרטיסים — רב-קו אונליין";
        StartPosition = FormStartPosition.CenterParent;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        BackColor = Color.FromArgb(10, 20, 34);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9.5f);
        MinimumSize = new Size(720, 520);
        Size = new Size(900, 650);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), RowCount = 3, ColumnCount = 1 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        Controls.Add(root);

        root.Controls.Add(new Label
        {
            Text = "בדיקת סביבת ההפעלה של קורא הכרטיסים",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 15, FontStyle.Bold),
            ForeColor = Color.FromArgb(255, 222, 161),
            TextAlign = ContentAlignment.MiddleRight
        }, 0, 0);
        root.Controls.Add(report, 0, 1);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var refresh = new Button { Text = "הרץ בדיקה מחדש", Width = 150, Height = 34 };
        refresh.Click += (_, _) => RunDiagnostics();
        var copy = new Button { Text = "העתק דוח", Width = 110, Height = 34 };
        copy.Click += (_, _) => { if (!string.IsNullOrWhiteSpace(report.Text)) Clipboard.SetText(report.Text); };
        var close = new Button { Text = "סגור", Width = 90, Height = 34, DialogResult = DialogResult.OK };
        actions.Controls.AddRange([refresh, copy, close]);
        root.Controls.Add(actions, 0, 2);
        AcceptButton = close;
        RunDiagnostics();
    }

    private void RunDiagnostics()
    {
        var lines = new List<string>
        {
            "RavKavKiosk — Reader diagnostics",
            "זמן בדיקה: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            "Windows: " + Environment.OSVersion.VersionString,
            "ארכיטקטורת מערכת: " + (Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit"),
            "ארכיטקטורת התוכנה: " + (Environment.Is64BitProcess ? "64-bit" : "32-bit")
        };

        try
        {
            var version = CoreWebView2Environment.GetAvailableBrowserVersionString();
            lines.Add("WebView2 Runtime: " + (string.IsNullOrWhiteSpace(version) ? "לא נמצא" : version));
        }
        catch (Exception ex) { lines.Add("WebView2 Runtime: שגיאה — " + ex.Message); }

        lines.Add("");
        lines.Add("=== רישום פרוטוקול ההפעלה של רב-קו ===");
        CheckProtocol(lines, "ravkavonline");
        CheckProtocol(lines, "ravkavonline.exe");

        lines.Add("");
        lines.Add("=== שירות Windows Smart Card ===");
        lines.Add(RunServiceQuery());

        lines.Add("");
        lines.Add("=== קוראי כרטיסים דרך PC/SC ===");
        lines.AddRange(ListSmartCardReaders());

        lines.Add("");
        lines.Add("=== מה נדרש כדי שהאתר יפעיל את הקורא ===");
        lines.Add("1. לפתוח את רב-קו אונליין מתוך התוכנה.");
        lines.Add("2. להתחיל את תהליך חיבור הכרטיס באתר וללחוץ על 'סיימתי להתקין' כאשר האתר מציג זאת.");
        lines.Add("3. לאשר את בקשת הפעלת התוכנה החיצונית, אם מופיעה.");
        lines.Add("4. אם לא מופיעה בקשה: לבדוק את רישום הפרוטוקול למעלה ולוודא שתוכנת הקורא הותקנה דרך תהליך ההרשמה באתר.");
        lines.Add("");
        lines.Add("הערה: בדיקה זו מאבחנת את Windows ואת שכבת הקורא. היא אינה מבצעת טעינה או עסקה בכרטיס.");

        report.Text = string.Join(Environment.NewLine, lines);
        report.SelectionStart = 0;
        report.SelectionLength = 0;
    }

    private static void CheckProtocol(List<string> lines, string scheme)
    {
        try
        {
            using var key = Registry.ClassesRoot.OpenSubKey(scheme);
            if (key is null)
            {
                lines.Add(scheme + ": לא נמצא רישום פרוטוקול ב-HKCR");
                return;
            }

            var description = key.GetValue(null)?.ToString();
            var urlProtocol = key.GetValue("URL Protocol")?.ToString();
            using var commandKey = key.OpenSubKey(@"shell\open\command");
            var command = commandKey?.GetValue(null)?.ToString();

            lines.Add(scheme + ": נמצא רישום");
            lines.Add("  תיאור: " + (string.IsNullOrWhiteSpace(description) ? "(לא הוגדר)" : description));
            lines.Add("  URL Protocol: " + (urlProtocol is null ? "לא מוגדר" : "מוגדר"));
            lines.Add("  פקודת הפעלה: " + (string.IsNullOrWhiteSpace(command) ? "(לא נמצאה פקודת shell\\open\\command)" : command));
        }
        catch (Exception ex) { lines.Add(scheme + ": שגיאת קריאת Registry — " + ex.Message); }
    }

    private static string RunServiceQuery()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = "query SCardSvr",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(5000))
            {
                process.Kill(true);
                return "בדיקת השירות חרגה מזמן ההמתנה.";
            }
            return string.IsNullOrWhiteSpace(output) ? "לא התקבל פלט: " + error : output.Trim();
        }
        catch (Exception ex) { return "לא ניתן לבדוק את השירות: " + ex.Message; }
    }

    private static IEnumerable<string> ListSmartCardReaders()
    {
        IntPtr context = IntPtr.Zero;
        try
        {
            var result = SCardEstablishContext(2, IntPtr.Zero, IntPtr.Zero, out context);
            if (result != 0)
                return ["לא ניתן להתחבר לשירות PC/SC. קוד שגיאה: 0x" + result.ToString("X8")];

            uint length = 0;
            result = SCardListReaders(context, null, null, ref length);
            if (result == unchecked((int)0x8010002E))
                return ["Windows מדווח שאין קוראי כרטיסים זמינים. חבר את הקורא ל-USB ובדוק את מנהל ההתקנים."];
            if (result != 0 && result != unchecked((int)0x8010002E))
                return ["SCardListReaders נכשל. קוד שגיאה: 0x" + result.ToString("X8")];
            if (length == 0)
                return ["לא נמצאו קוראי כרטיסים." ];

            var buffer = new StringBuilder(checked((int)length));
            result = SCardListReaders(context, null, buffer, ref length);
            if (result != 0)
                return ["קריאת רשימת הקוראים נכשלה. קוד שגיאה: 0x" + result.ToString("X8")];

            var names = buffer.ToString().Split('\0', StringSplitOptions.RemoveEmptyEntries);
            return names.Length == 0 ? ["לא נמצאו קוראי כרטיסים."] : names.Select((name, index) => $"{index + 1}. {name}").ToArray();
        }
        catch (DllNotFoundException) { return ["רכיב Windows winscard.dll לא נמצא."]; }
        catch (Exception ex) { return ["שגיאה בזיהוי קוראי כרטיסים: " + ex.Message]; }
        finally
        {
            if (context != IntPtr.Zero) _ = SCardReleaseContext(context);
        }
    }

    [DllImport("winscard.dll", EntryPoint = "SCardEstablishContext")]
    private static extern int SCardEstablishContext(uint scope, IntPtr reserved1, IntPtr reserved2, out IntPtr context);

    [DllImport("winscard.dll", EntryPoint = "SCardListReadersW", CharSet = CharSet.Unicode)]
    private static extern int SCardListReaders(IntPtr context, string? groups, StringBuilder? readers, ref uint readersLength);

    [DllImport("winscard.dll", EntryPoint = "SCardReleaseContext")]
    private static extern int SCardReleaseContext(IntPtr context);
}
