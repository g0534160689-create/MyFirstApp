using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace RavKavKiosk;

internal sealed class ServicePopupForm : Form
{
    private readonly WebView2 view = new() { Dock = DockStyle.Fill };

    public CoreWebView2 Core => view.CoreWebView2;

    public ServicePopupForm()
    {
        Text = "חלון שירות מאובטח";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(640, 420);
        Size = new Size(980, 720);
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        BackColor = Color.FromArgb(12, 22, 38);
        Controls.Add(view);
    }

    public async Task InitializeAsync(CoreWebView2Environment environment)
    {
        await view.EnsureCoreWebView2Async(environment);
        view.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        view.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
        view.CoreWebView2.Settings.AreDevToolsEnabled = false;
        view.CoreWebView2.Settings.IsStatusBarEnabled = false;
        view.CoreWebView2.Settings.IsZoomControlEnabled = false;
        view.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
    }
}
