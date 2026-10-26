using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace YGuardClient;

internal sealed class MainForm : Form
{
    // Override with env YGUARD_CLIENT_URL if needed (staging).
    private static readonly string StartUrl =
        Environment.GetEnvironmentVariable("YGUARD_CLIENT_URL")
        ?? "https://yguard.ir/launcher?desktop=1";

    private readonly WebView2 _web = new()
    {
        Dock = DockStyle.Fill,
        DefaultBackgroundColor = Color.Transparent,
    };

    private bool _dragFromWeb;

    public MainForm()
    {
        Text = "YGuard Client";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1100, 700);
        Size = new Size(1280, 800);
        BackColor = Color.FromArgb(12, 13, 16);
        FormBorderStyle = FormBorderStyle.None;
        DoubleBuffered = true;

        try
        {
            if (File.Exists(Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico")))
                Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico"));
        }
        catch
        {
            /* ignore missing icon */
        }

        Controls.Add(_web);
        HandleCreated += (_, _) => ApplyRoundedCorners();
        Resize += (_, _) => ApplyRoundedCorners();
        Load += async (_, _) => await InitWebAsync();
    }

    private void ApplyRoundedCorners()
    {
        if (!IsHandleCreated) return;
        try
        {
            // Win11+: soft round corners on the borderless window.
            const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
            const int DWMWCP_ROUND = 2;
            var pref = DWMWCP_ROUND;
            _ = DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));
        }
        catch
        {
            /* older Windows — CSS radius still softens the web chrome */
        }
    }

    private async Task InitWebAsync()
    {
        try
        {
            var userData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "YGuardClient",
                "WebView2");
            Directory.CreateDirectory(userData);

            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: userData);
            await _web.EnsureCoreWebView2Async(env);

            var core = _web.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.AreDevToolsEnabled =
                string.Equals(
                    Environment.GetEnvironmentVariable("YGUARD_CLIENT_DEVTOOLS"),
                    "1",
                    StringComparison.Ordinal);

            core.Settings.UserAgent =
                $"{core.Settings.UserAgent} YGuardClient/0.2.2";

            core.NavigationStarting += OnNavigationStarting;
            core.NewWindowRequested += OnNewWindowRequested;
            core.WebMessageReceived += OnWebMessage;
            core.DOMContentLoaded += (_, _) =>
            {
                // Tell the page it is hosted in the desktop client.
                _ = core.ExecuteScriptAsync(
                    "document.documentElement.dataset.yguardDesktop='1';" +
                    "window.yguardDesktop=true;");
            };

            // Prefer the Steam account already logged in on this PC.
            var autoUrl = await SteamAutoLogin.TryGetDesktopLoginUrlAsync();
            core.Navigate(autoUrl ?? StartUrl);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "WebView2 failed to start. Install the Microsoft Edge WebView2 Runtime, then try again.\n\n" +
                ex.Message,
                "YGuard Client",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            Close();
        }
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (TryLaunchExternalProtocol(e.Uri))
            e.Cancel = true;
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        if (TryLaunchExternalProtocol(e.Uri))
        {
            e.Handled = true;
            return;
        }

        // Keep Steam / OAuth popups inside this window.
        e.Handled = true;
        _web.CoreWebView2?.Navigate(e.Uri);
    }

    private static bool TryLaunchExternalProtocol(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri)) return false;
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var u)) return false;

        var scheme = u.Scheme.ToLowerInvariant();
        if (scheme is not ("steam" or "ts3server" or "teamspeak" or "discord" or "mailto"))
            return false;

        try
        {
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
            return true;
        }
        catch
        {
            return true; // cancel navigation either way
        }
    }

    private void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        string msg;
        try
        {
            msg = e.TryGetWebMessageAsString();
        }
        catch
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(msg)) return;

        BeginInvoke(() =>
        {
            switch (msg.Trim().ToLowerInvariant())
            {
                case "minimize":
                    WindowState = FormWindowState.Minimized;
                    break;
                case "maximize":
                    WindowState = WindowState == FormWindowState.Maximized
                        ? FormWindowState.Normal
                        : FormWindowState.Maximized;
                    break;
                case "close":
                    Close();
                    break;
                case "drag-start":
                    _dragFromWeb = true;
                    ReleaseCapture();
                    SendMessage(Handle, 0xA1 /* WM_NCLBUTTONDOWN */, 2 /* HTCAPTION */, 0);
                    _dragFromWeb = false;
                    break;
            }
        });
    }

    protected override void WndProc(ref Message m)
    {
        const int WM_NCHITTEST = 0x84;
        const int HTCLIENT = 1;
        const int HTLEFT = 10;
        const int HTRIGHT = 11;
        const int HTTOP = 12;
        const int HTTOPLEFT = 13;
        const int HTTOPRIGHT = 14;
        const int HTBOTTOM = 15;
        const int HTBOTTOMLEFT = 16;
        const int HTBOTTOMRIGHT = 17;

        if (m.Msg == WM_NCHITTEST && WindowState == FormWindowState.Normal && !_dragFromWeb)
        {
            base.WndProc(ref m);
            if ((int)m.Result == HTCLIENT)
            {
                var pt = PointToClient(new Point(m.LParam.ToInt32()));
                const int grip = 6;
                var left = pt.X <= grip;
                var right = pt.X >= ClientSize.Width - grip;
                var top = pt.Y <= grip;
                var bottom = pt.Y >= ClientSize.Height - grip;
                if (top && left) m.Result = (IntPtr)HTTOPLEFT;
                else if (top && right) m.Result = (IntPtr)HTTOPRIGHT;
                else if (bottom && left) m.Result = (IntPtr)HTBOTTOMLEFT;
                else if (bottom && right) m.Result = (IntPtr)HTBOTTOMRIGHT;
                else if (left) m.Result = (IntPtr)HTLEFT;
                else if (right) m.Result = (IntPtr)HTRIGHT;
                else if (top) m.Result = (IntPtr)HTTOP;
                else if (bottom) m.Result = (IntPtr)HTBOTTOM;
            }
            return;
        }

        base.WndProc(ref m);
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int dwAttribute,
        ref int pvAttribute,
        int cbAttribute);
}
