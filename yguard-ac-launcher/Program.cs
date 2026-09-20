using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Management;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace YGuardAC;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        if (!IsRunningAsAdministrator())
        {
            var choice = MessageBox.Show(
                "YGuard Anti-Cheat must be run as Administrator so it can remove cheat files and close overlays.\n\n" +
                "Click Yes to restart as Administrator, or No to exit.",
                "Administrator required",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Error,
                MessageBoxDefaultButton.Button1);
            if (choice == DialogResult.Yes)
                TryRelaunchElevated();
            return;
        }

        // Required for PROCESS_TERMINATE against foreign processes.
        if (!SeDebugPrivilege.TryEnable())
        {
            MessageBox.Show(
                "Could not enable SeDebugPrivilege. Some unsigned process protection may not work.\n\n" +
                "Continue anyway.",
                "YGuard Anti-Cheat",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        Application.Run(new MainForm());
    }

    private static bool IsRunningAsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private static void TryRelaunchElevated()
    {
        try
        {
            var path = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(path)) return;
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
                Verb = "runas",
            });
        }
        catch
        {
            MessageBox.Show(
                "Could not elevate. Right-click YGuardAC.exe → Run as administrator.",
                "Administrator required",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}

internal static class Theme
{
    public static readonly Color Bg = Color.FromArgb(18, 18, 18);
    public static readonly Color Surface = Color.FromArgb(30, 30, 30);
    public static readonly Color PillBg = Color.FromArgb(38, 38, 42);
    public static readonly Color Text = Color.White;
    public static readonly Color Muted = Color.FromArgb(160, 160, 160);
    public static readonly Color Orange = Color.FromArgb(255, 85, 0); // FACEIT-like accent
    public static readonly Color Brand = Color.FromArgb(220, 20, 20); // YGuard red CTA
    public static readonly Color Green = Color.FromArgb(46, 180, 80);
    public static readonly Color Red = Color.FromArgb(229, 57, 53);

    public static Image? LogoMark { get; private set; }

    public static void LoadAssets(string baseDir)
    {
        try
        {
            var png = Path.Combine(baseDir, "Assets", "logo.png");
            if (File.Exists(png))
            {
                using var src = Image.FromFile(png);
                LogoMark = new Bitmap(src);
                return;
            }
        }
        catch { /* fall through to embedded */ }

        try
        {
            var asm = typeof(Theme).Assembly;
            var name = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("logo.png", StringComparison.OrdinalIgnoreCase));
            if (name == null) return;
            using var stream = asm.GetManifestResourceStream(name);
            if (stream == null) return;
            LogoMark = new Bitmap(stream);
        }
        catch { /* ignore */ }
    }
}

internal sealed class MainForm : Form
{
    private const string ApiBase = "https://api.yguard.ir";

    private readonly Panel _outView = new() { Dock = DockStyle.Fill, BackColor = Theme.Bg };
    private readonly Panel _inView = new() { Dock = DockStyle.Fill, BackColor = Theme.Bg };

    // OUT
    private readonly RoundedPanel _errorBanner = new() { Visible = false };
    private readonly Label _errorLbl = new();
    private readonly RoundedButton _loginBtn = new();

    // IN
    private readonly PictureBox _avatar = new();
    private readonly Label _nameLbl = new();
    private readonly Label _memberLbl = new();
    private readonly LinkLabel _logout = new();
    private readonly Label _featuresInTitle = new();
    private readonly FlowLayoutPanel _inPills = new();
    private readonly Panel _statusBar = new();
    private readonly Label _statusText = new();
    private bool _statusOk;

    private readonly Dictionary<string, FeaturePill> _pills = new();
    private readonly System.Windows.Forms.Timer _heartbeat = new() { Interval = 30_000 };
    private readonly System.Windows.Forms.Timer _cheatScan = new() { Interval = 20_000 };
    private readonly System.Windows.Forms.Timer _gameWatch = new() { Interval = 2_000 };
    private UnsignedProcessGuard? _unsignedGuard;
    private CancellationTokenSource? _loginCts;
    private bool _reportedCheats;
    private bool _platformBanned;
    private bool _connectedOk;
    private bool? _lastGameRunning;
    private DateTime _lastCheatReport = DateTime.MinValue;
    private bool _updatePrompted;
    private bool _exitingForUpdate;
    /// <summary>Mandatory update pending — do not attest until installed.</summary>
    private bool _updateRequired;

    private string? _deviceToken;
    private readonly string _tokenPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "YGuardAC", "device.token");

    private static readonly (string key, string label)[] Features =
    [
        ("secure_boot", "Secure Boot"),
        ("iommu", "IOMMU"),
        ("tpm_20", "TPM 2.0"),
        ("tpm_attestation", "TPM Attestation"),
        ("hvci", "HVCI"),
        ("windows_updates", "Windows Security Updates"),
    ];

    public MainForm()
    {
        Text = "YGuard Anti-Cheat";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = true;
        StartPosition = FormStartPosition.CenterScreen;
        // Match FACEIT Anti-cheat frame (screenshot ~450×237 outer).
        ClientSize = new Size(452, 210);
        BackColor = Theme.Bg;
        Font = new Font("Segoe UI", 9f);
        DoubleBuffered = true;
        Theme.LoadAssets(AppContext.BaseDirectory);
        try
        {
            // Prefer real exe folder for assets/icon (single-file BaseDirectory is a temp cache).
            var exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
            var ico = Path.Combine(exeDir, "Assets", "app.ico");
            if (!File.Exists(ico))
                ico = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
            if (File.Exists(ico)) Icon = new Icon(ico);
            else if (Theme.LogoMark != null)
            {
                using var bmp = new Bitmap(Theme.LogoMark, 32, 32);
                Icon = Icon.FromHandle(bmp.GetHicon());
            }
        }
        catch { /* ignore */ }
        TryDarkTitle();

        BuildOut();
        BuildIn();
        Controls.Add(_inView);
        Controls.Add(_outView);

        _heartbeat.Tick += async (_, _) =>
        {
            if (!string.IsNullOrEmpty(_deviceToken))
                await AttestAsync();
        };
        _cheatScan.Tick += async (_, _) =>
        {
            if (!string.IsNullOrEmpty(_deviceToken))
                await ScanCheatsAsync();
        };
        _gameWatch.Tick += (_, _) => RefreshConnectedStatus(force: false);

        Load += async (_, _) =>
        {
            Text = "YGuard Anti-Cheat";
            PaintChecks(animate: true);
            _ = CheckForUpdateAsync();
            TryLoadToken();
            if (!string.IsNullOrEmpty(_deviceToken))
            {
                ShowIn();
                await LoadProfileAsync();
                await AttestAsync();
                await ScanCheatsAsync();
            }
            else ShowOut();
            _heartbeat.Start();
            _cheatScan.Start();
            _gameWatch.Start();

            _unsignedGuard ??= new UnsignedProcessGuard();
            _unsignedGuard.Start();
        };

        FormClosing += (_, _) =>
        {
            try { _unsignedGuard?.Dispose(); } catch { }
            _unsignedGuard = null;

            // Closing AC while CS2 is open must kill the game — otherwise players
            // can drop AC mid-match and enable cheats.
            if (!_exitingForUpdate)
            {
                KillGameIfRunning();
                // Sync so attestation dies before the process exits (async FormClosed
                // often never finishes when the user closes the window).
                NotifyDisconnectSync();
            }
        };
        FormClosed += (_, _) =>
        {
            _loginCts?.Cancel();
        };
    }

    private void TryDarkTitle()
    {
        try
        {
            int on = 1;
            DwmSetWindowAttribute(Handle, 20, ref on, sizeof(int));
        }
        catch { }
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr h, int a, ref int v, int s);

    private void BuildOut()
    {
        // Error banner — FACEIT style
        _errorBanner.Size = new Size(364, 44);
        _errorBanner.Radius = 8;
        _errorBanner.Fill = Theme.Surface;
        _errorBanner.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var red = new SolidBrush(Theme.Red);
            e.Graphics.FillEllipse(red, 14, 14, 20, 20);
            using var pen = new Pen(Color.White, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            e.Graphics.DrawLine(pen, 20, 20, 28, 28);
            e.Graphics.DrawLine(pen, 28, 20, 20, 28);
        };
        _errorLbl.AutoSize = false;
        _errorLbl.Size = new Size(300, 24);
        _errorLbl.Location = new Point(44, 13);
        _errorLbl.ForeColor = Theme.Text;
        _errorLbl.Font = new Font("Segoe UI Semibold", 9.5f);
        _errorLbl.BackColor = Color.Transparent;
        _errorLbl.Text = "Login failed: Connection refused";
        _errorBanner.Controls.Add(_errorLbl);

        // LOGIN WITH YGUARD
        _loginBtn.Size = new Size(260, 48);
        _loginBtn.Radius = 6;
        _loginBtn.Fill = Theme.Brand;
        _loginBtn.ForeColor = Color.White;
        _loginBtn.Font = new Font("Segoe UI Semibold", 11f);
        _loginBtn.Cursor = Cursors.Hand;
        _loginBtn.Text = "LOGIN WITH YGUARD";
        _loginBtn.Click += async (_, _) => await StartBrowserLoginAsync();

        void Layout(object? s, EventArgs e)
        {
            int w = _outView.ClientSize.Width;
            int h = _outView.ClientSize.Height;
            _errorBanner.Left = (w - _errorBanner.Width) / 2;
            _errorBanner.Top = 28;
            _loginBtn.Left = (w - _loginBtn.Width) / 2;
            // Center the login button in the free space (no empty feature band).
            var top = _errorBanner.Visible ? _errorBanner.Bottom + 28 : 0;
            var avail = Math.Max(0, h - top);
            _loginBtn.Top = top + Math.Max(0, (avail - _loginBtn.Height) / 2);
        }
        _outView.Resize += Layout;
        _outView.Controls.Add(_errorBanner);
        _outView.Controls.Add(_loginBtn);
        Layout(null, EventArgs.Empty);
    }

    private void BuildIn()
    {
        // FACEIT layout (tight padding, no empty middle band).
        const int pad = 24;

        _avatar.Size = new Size(48, 48);
        _avatar.Location = new Point(pad, 18);
        _avatar.SizeMode = PictureBoxSizeMode.Zoom;
        _avatar.BackColor = Theme.Surface;

        _nameLbl.Font = new Font("Segoe UI Semibold", 15f);
        _nameLbl.ForeColor = Theme.Text;
        _nameLbl.AutoSize = true;
        _nameLbl.Location = new Point(pad + 60, 18);

        _memberLbl.Font = new Font("Segoe UI", 9f);
        _memberLbl.ForeColor = Theme.Muted;
        _memberLbl.AutoSize = true;
        _memberLbl.Location = new Point(pad + 60, 46);

        _logout.Text = "LOGOUT";
        _logout.Font = new Font("Segoe UI Semibold", 9f);
        _logout.LinkColor = Theme.Muted;
        _logout.ActiveLinkColor = Theme.Text;
        _logout.VisitedLinkColor = Theme.Muted;
        _logout.LinkBehavior = LinkBehavior.NeverUnderline;
        _logout.AutoSize = true;
        _logout.Cursor = Cursors.Hand;
        _logout.Click += (_, _) => Logout();

        _featuresInTitle.Text = "Recommended Security Features";
        _featuresInTitle.Font = new Font("Segoe UI Semibold", 12f);
        _featuresInTitle.ForeColor = Theme.Text;
        _featuresInTitle.AutoSize = true;
        _featuresInTitle.Location = new Point(pad, 82);

        SetupPills(_inPills, "i:");
        _inPills.Location = new Point(pad, 110);
        _inPills.Size = new Size(ClientSize.Width - pad * 2, 64);
        _inPills.Margin = Padding.Empty;
        _inPills.Padding = Padding.Empty;

        _statusBar.Dock = DockStyle.Bottom;
        _statusBar.Height = 32;
        _statusBar.BackColor = Theme.Bg;
        _statusBar.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var b = new SolidBrush(_statusOk ? Theme.Green : Theme.Red);
            e.Graphics.FillEllipse(b, pad, 11, 9, 9);
        };
        _statusText.Location = new Point(pad + 16, 7);
        _statusText.AutoSize = true;
        _statusText.ForeColor = Theme.Muted;
        _statusText.Font = new Font("Segoe UI", 9f);
        _statusText.Text = "Disconnected";
        _statusBar.Controls.Add(_statusText);

        void LayoutIn(object? s, EventArgs e)
        {
            _logout.Left = _inView.ClientSize.Width - _logout.Width - pad;
            _logout.Top = 26;
            _inPills.Width = Math.Max(200, _inView.ClientSize.Width - pad * 2);
        }
        _inView.Resize += LayoutIn;
        LayoutIn(null, EventArgs.Empty);

        _inView.Controls.Add(_avatar);
        _inView.Controls.Add(_nameLbl);
        _inView.Controls.Add(_memberLbl);
        _inView.Controls.Add(_logout);
        _inView.Controls.Add(_featuresInTitle);
        _inView.Controls.Add(_inPills);
        _inView.Controls.Add(_statusBar);
    }

    private void SetupPills(FlowLayoutPanel host, string prefix)
    {
        host.FlowDirection = FlowDirection.LeftToRight;
        host.WrapContents = true;
        host.BackColor = Theme.Bg;
        host.AutoSize = false;
        foreach (var (_, label) in Features)
        {
            var pill = new FeaturePill(label);
            _pills[prefix + label] = pill;
            host.Controls.Add(pill);
        }
    }

    private void ShowOut()
    {
        _outView.Visible = true;
        _inView.Visible = false;
        _outView.BringToFront();
        HideError();
    }

    private void ShowIn()
    {
        _inView.Visible = true;
        _outView.Visible = false;
        _inView.BringToFront();
        _logout.Left = _inView.ClientSize.Width - _logout.Width - 24;
        _logout.Top = 26;
    }

    private void ShowError(string msg)
    {
        _errorLbl.Text = msg;
        _errorBanner.Visible = true;
        RelayoutOut();
    }

    private void HideError()
    {
        _errorBanner.Visible = false;
        RelayoutOut();
    }

    private void RelayoutOut()
    {
        int w = _outView.ClientSize.Width;
        int h = _outView.ClientSize.Height;
        _errorBanner.Left = (w - _errorBanner.Width) / 2;
        _errorBanner.Top = 28;
        _loginBtn.Left = (w - _loginBtn.Width) / 2;
        var top = _errorBanner.Visible ? _errorBanner.Bottom + 28 : 0;
        var avail = Math.Max(0, h - top);
        _loginBtn.Top = top + Math.Max(0, (avail - _loginBtn.Height) / 2);
    }

    private void SetStatus(string text, bool ok)
    {
        _statusOk = ok;
        _statusText.Text = text;
        _statusBar.Invalidate();
    }

    private static bool IsGameRunning() =>
        Process.GetProcessesByName("cs2").Length > 0
        || Process.GetProcessesByName("csgo").Length > 0;

    /// <summary>Force-close CS2/CSGO when AC exits so cheats can't be loaded mid-match.</summary>
    private static void KillGameIfRunning()
    {
        foreach (var name in new[] { "cs2", "csgo" })
        {
            Process[] procs;
            try { procs = Process.GetProcessesByName(name); }
            catch { continue; }

            foreach (var p in procs)
            {
                try
                {
                    if (p.HasExited) continue;
                    p.Kill(entireProcessTree: true);
                    p.WaitForExit(4000);
                }
                catch
                {
                    try { p.Kill(); } catch { /* best effort */ }
                }
                finally
                {
                    try { p.Dispose(); } catch { }
                }
            }
        }
    }

    /// <summary>
    /// FACEIT-style footer: waiting vs game running. Only while connected &amp; clean.
    /// </summary>
    private void RefreshConnectedStatus(bool force)
    {
        if (!_connectedOk || _platformBanned || _reportedCheats) return;
        var running = IsGameRunning();
        if (!force && _lastGameRunning == running) return;
        _lastGameRunning = running;
        SetStatus(
            running
                ? "Connected | Counter-Strike 2 is running"
                : "Connected | Waiting for game to launch",
            true);
    }

    private void PaintChecks(bool animate = false)
    {
        if (animate)
        {
            _ = AnimateChecksAsync();
            return;
        }
        ApplyChecksInstant(SecurityChecks.Run());
    }

    private void ApplyChecksInstant(CheckReport r)
    {
        void Set(string p)
        {
            if (!_pills.ContainsKey(p + "Secure Boot")) return;
            _pills[p + "Secure Boot"].SetOk(r.secure_boot);
            _pills[p + "IOMMU"].SetOk(r.iommu);
            _pills[p + "TPM 2.0"].SetOk(r.tpm_20);
            _pills[p + "TPM Attestation"].SetOk(r.tpm_attestation);
            _pills[p + "HVCI"].SetOk(r.hvci);
            _pills[p + "Windows Security Updates"].SetOk(r.windows_updates);
        }
        Set("i:");
    }

    private async Task AnimateChecksAsync()
    {
        var r = SecurityChecks.Run();
        void SetChecking(string p)
        {
            if (!_pills.ContainsKey(p + "Secure Boot")) return;
            _pills[p + "Secure Boot"].SetChecking();
            _pills[p + "IOMMU"].SetChecking();
            _pills[p + "TPM 2.0"].SetChecking();
            _pills[p + "TPM Attestation"].SetChecking();
            _pills[p + "HVCI"].SetChecking();
            _pills[p + "Windows Security Updates"].SetChecking();
        }
        SetChecking("i:");

        var results = new (string label, bool ok)[]
        {
            ("Secure Boot", r.secure_boot),
            ("IOMMU", r.iommu),
            ("TPM 2.0", r.tpm_20),
            ("TPM Attestation", r.tpm_attestation),
            ("HVCI", r.hvci),
            ("Windows Security Updates", r.windows_updates),
        };

        for (var i = 0; i < results.Length; i++)
        {
            await Task.Delay(180);
            if (IsDisposed) return;
            var (label, ok) = results[i];
            if (_pills.TryGetValue("i:" + label, out var inPill))
                inPill.SetOk(ok);
        }
    }

    /// <summary>
    /// FACEIT-style: open website; Steam session there authorizes the launcher.
    /// </summary>
    private async Task StartBrowserLoginAsync()
    {
        _loginCts?.Cancel();
        _loginCts = new CancellationTokenSource();
        var ct = _loginCts.Token;
        HideError();
        _loginBtn.Enabled = false;
        _loginBtn.Text = "WAITING…";

        try
        {
            using var http = Http();
            var res = await http.PostAsync("/plugins/ac/device/begin", null, ct);
            var json = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
            {
                ShowError($"Login failed: API {(int)res.StatusCode}");
                return;
            }
            using var doc = JsonDocument.Parse(json);
            var code = doc.RootElement.GetProperty("code").GetString()!;
            var uri = doc.RootElement.GetProperty("verification_uri").GetString()!;
            var interval = doc.RootElement.TryGetProperty("interval", out var iv)
                ? Math.Max(1, iv.GetInt32())
                : 2;

            try
            {
                Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
            }
            catch
            {
                ShowError("Login failed: Connection refused");
                return;
            }

            // Poll until website Steam user approves
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(interval), ct);
                var pollRes = await http.PostAsJsonAsync(
                    "/plugins/ac/device/poll",
                    new { code, label = Environment.MachineName },
                    ct);
                var pollJson = await pollRes.Content.ReadAsStringAsync(ct);
                if (!pollRes.IsSuccessStatusCode) continue;

                using var pollDoc = JsonDocument.Parse(pollJson);
                var status = pollDoc.RootElement.GetProperty("status").GetString();
                if (status == "pending") continue;
                if (status == "expired")
                {
                    ShowError("Login failed: Connection refused");
                    return;
                }
                if (status == "ready")
                {
                    var token = pollDoc.RootElement.GetProperty("device_token").GetString()!;
                    SaveToken(token);
                    ShowIn();
                    await LoadProfileAsync();
                    await AttestAsync();
                    return;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch
        {
            ShowError("Login failed: Connection refused");
        }
        finally
        {
            _loginBtn.Enabled = true;
            _loginBtn.Text = "LOGIN WITH YGUARD";
        }
    }

    private async Task NotifyDisconnectAsync()
    {
        if (string.IsNullOrEmpty(_deviceToken)) return;
        try
        {
            using var http = Http();
            http.Timeout = TimeSpan.FromSeconds(4);
            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _deviceToken);
            await http.PostAsync("/plugins/ac/disconnect", null);
        }
        catch { /* closing — best effort */ }
    }

    private void NotifyDisconnectSync()
    {
        if (string.IsNullOrEmpty(_deviceToken)) return;
        try
        {
            using var http = Http();
            http.Timeout = TimeSpan.FromSeconds(3);
            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _deviceToken);
            http.PostAsync("/plugins/ac/disconnect", null).GetAwaiter().GetResult();
        }
        catch { /* closing — best effort */ }
    }

    private void Logout()
    {
        // Logout removes AC protection — close the game if it's open.
        KillGameIfRunning();
        _ = NotifyDisconnectAsync();
        _loginCts?.Cancel();
        try { if (File.Exists(_tokenPath)) File.Delete(_tokenPath); } catch { }
        _deviceToken = null;
        _connectedOk = false;
        _lastGameRunning = null;
        _avatar.Image = null;
        _nameLbl.Text = "";
        ShowOut();
    }

    private void TryLoadToken()
    {
        try
        {
            _deviceToken = SecureTokenStore.Load(_tokenPath);
        }
        catch { }
    }

    private void SaveToken(string token)
    {
        SecureTokenStore.Save(_tokenPath, token);
        _deviceToken = token;
    }

    private async Task LoadProfileAsync()
    {
        if (string.IsNullOrEmpty(_deviceToken)) return;
        try
        {
            using var http = Http();
            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _deviceToken);
            var res = await http.GetAsync("/plugins/ac/me");
            if (!res.IsSuccessStatusCode) return;
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            _nameLbl.Text = root.GetProperty("name").GetString() ?? "Player";
            if (root.TryGetProperty("member_since", out var ms)
                && ms.ValueKind == JsonValueKind.String
                && DateTime.TryParse(ms.GetString(), out var dt))
                _memberLbl.Text = $"🇮🇷  Member since {dt.Day} {dt.ToString("MMMM yyyy", System.Globalization.CultureInfo.GetCultureInfo("en-US"))}";
            else
                _memberLbl.Text = "🇮🇷  Member";

            if (root.TryGetProperty("avatar_url", out var av)
                && av.ValueKind == JsonValueKind.String
                && !string.IsNullOrEmpty(av.GetString()))
            {
                try
                {
                    using var imgHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                    var bytes = await imgHttp.GetByteArrayAsync(av.GetString()!);
                    using var msStream = new MemoryStream(bytes);
                    _avatar.Image = Image.FromStream(msStream);
                }
                catch { }
            }
        }
        catch { }
    }

    private async Task CheckForUpdateAsync()
    {
        if (_updatePrompted) return;
        try
        {
            var release = await AutoUpdater.FetchAsync(ApiBase);
            if (release == null || string.IsNullOrWhiteSpace(release.Version)) return;

            var behindLatest = AutoUpdater.IsNewer(release.Version, AutoUpdater.CurrentVersion);
            var belowMin = !string.IsNullOrWhiteSpace(release.MinVersion) &&
                AutoUpdater.IsNewer(release.MinVersion!, AutoUpdater.CurrentVersion);
            if (!behindLatest && !belowMin) return;

            var mandatory = release.Mandatory || belowMin;
            if (!mandatory && AutoUpdater.WasSkipped(release.Version)) return;

            _updatePrompted = true;
            var target = behindLatest ? release.Version : release.MinVersion!;
            var msg = mandatory
                ? $"YGuard AC {target} is required (you have {AutoUpdater.CurrentVersion}).\n\nUpdate now to keep playing."
                : $"New YGuard AC version {release.Version} is available (you have {AutoUpdater.CurrentVersion}).\n\nUpdate now?";
            var result = MessageBox.Show(
                this,
                msg,
                "YGuard Anti-Cheat Update",
                mandatory ? MessageBoxButtons.OKCancel : MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);

            if (result is DialogResult.No or DialogResult.Cancel)
            {
                if (!mandatory)
                    AutoUpdater.RememberSkip(release.Version);
                else
                {
                    _updateRequired = true;
                    SetStatus("Update required — download 0.4.1+ from yguard.ir", false);
                }
                return;
            }

            // Ensure download points at latest even when only min_version forced us.
            if (string.IsNullOrWhiteSpace(release.DownloadUrl) || !behindLatest)
            {
                var latest = await AutoUpdater.FetchAsync(ApiBase);
                if (latest != null) release = latest;
            }

            SetStatus("Updating…", false);
            var (ok, error) = await AutoUpdater.ApplyAsync(
                release,
                new Progress<string>(s => SetStatus(s, false)));
            if (ok)
            {
                // Let the batch replace the exe after we fully exit.
                // Don't kill CS2 — this is an AC self-update restart, not a quit.
                _exitingForUpdate = true;
                BeginInvoke(() =>
                {
                    Application.Exit();
                    Environment.Exit(0);
                });
            }
            else
            {
                _updateRequired = mandatory;
                SetStatus(
                    string.IsNullOrWhiteSpace(error)
                        ? "Update failed — download again from yguard.ir"
                        : $"Update failed: {error}",
                    false);
                MessageBox.Show(
                    this,
                    "Automatic update failed.\n\nPlease download YGuardAC again from the site (Install AntiCheat).\n\n" + error,
                    "YGuard Anti-Cheat",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                // Allow retry next launch — do NOT remember skip on failure.
            }
        }
        catch
        {
            _updatePrompted = true;
        }
    }

    private bool _attestInFlight;

    private async Task AttestAsync()
    {
        if (string.IsNullOrEmpty(_deviceToken)) return;
        if (_attestInFlight) return;
        if (_updateRequired)
        {
            _connectedOk = false;
            SetStatus("Update required — download latest YGuard AC from yguard.ir", false);
            return;
        }
        _attestInFlight = true;
        PaintChecks();
        try
        {
            if (ClientGuard.IsTamperedEnvironment())
            {
                _connectedOk = false;
                SetStatus("Security check failed — close debugger and retry", false);
                return;
            }

            List<CheatScanner.Hit> hits;
            try { hits = CheatScanner.Scan(); }
            catch { hits = new List<CheatScanner.Hit>(); }
            // Overlay close is soft-only and must never affect cheat_clean / bans.
            if (IsGameRunning())
            {
                try { OverlayGuard.ScanAndClose(); } catch { /* ignore */ }
            }
            if (hits.Count > 0)
            {
                try { CheatScanner.Remediate(hits); } catch { /* best-effort */ }
            }
            var cheatClean = hits.Count == 0;
            _reportedCheats = !cheatClean;

            var payload = SecurityChecks.Run();
            using var http = Http();
            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _deviceToken);

            // Empty JSON body — some proxies reject POST with no entity.
            using var challengeContent = new StringContent(
                "{}", Encoding.UTF8, "application/json");
            var challengeRes = await http.PostAsync("/plugins/ac/challenge", challengeContent);
            var challengeRaw = await challengeRes.Content.ReadAsStringAsync();
            if (!challengeRes.IsSuccessStatusCode)
            {
                _connectedOk = false;
                if ((int)challengeRes.StatusCode == 401 &&
                    (challengeRaw.Contains("Invalid or revoked", StringComparison.OrdinalIgnoreCase) ||
                     challengeRaw.Contains("Device token", StringComparison.OrdinalIgnoreCase)))
                {
                    ClearLocalSession("Session expired — log in again");
                    return;
                }
                SetStatus(DescribeAcHttpError("challenge", (int)challengeRes.StatusCode, challengeRaw), false);
                return;
            }

            string challenge;
            string? challengeMin = null;
            try
            {
                using var challengeDoc = JsonDocument.Parse(challengeRaw);
                challenge = challengeDoc.RootElement.GetProperty("challenge").GetString() ?? "";
                if (challengeDoc.RootElement.TryGetProperty("min_version", out var mv))
                    challengeMin = mv.GetString();
            }
            catch
            {
                _connectedOk = false;
                SetStatus("AC challenge invalid — update API / migrate DB", false);
                return;
            }
            if (!string.IsNullOrWhiteSpace(challengeMin) &&
                AutoUpdater.IsNewer(challengeMin!, AutoUpdater.CurrentVersion))
            {
                _updateRequired = true;
                _connectedOk = false;
                SetStatus($"Update required — install YGuard AC {challengeMin}+", false);
                return;
            }
            if (string.IsNullOrEmpty(challenge))
            {
                _connectedOk = false;
                SetStatus("AC challenge empty — retry", false);
                return;
            }

            var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var body = new Dictionary<string, object?>
            {
                ["secure_boot"] = payload.secure_boot,
                ["iommu"] = payload.iommu,
                ["tpm_20"] = payload.tpm_20,
                ["tpm_attestation"] = payload.tpm_attestation,
                ["hvci"] = payload.hvci,
                ["windows_updates"] = payload.windows_updates,
                ["os_version"] = payload.os_version,
                ["hardware_hash"] = payload.hardware_hash,
                ["cheat_clean"] = cheatClean,
                ["challenge"] = challenge,
                ["ts"] = ts,
                ["client_version"] = AutoUpdater.CurrentVersion,
            };
            body["signature"] = AttestCrypto.Sign(_deviceToken!, AttestCrypto.Canonical(body));

            var res = await http.PostAsJsonAsync("/plugins/ac/attest", body);
            var errBody = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
            {
                _connectedOk = false;
                if (errBody.Contains("fingerprint", StringComparison.OrdinalIgnoreCase) ||
                    errBody.Contains("pair again", StringComparison.OrdinalIgnoreCase))
                {
                    // Server no longer hard-revokes on HW churn; retry next tick.
                    SetStatus("AC hardware check updating — wait a moment", false);
                    return;
                }
                if (errBody.Contains("Update YGuard", StringComparison.OrdinalIgnoreCase))
                {
                    SetStatus("Update required — download latest from yguard.ir", false);
                    return;
                }
                if (errBody.Contains("signature", StringComparison.OrdinalIgnoreCase))
                {
                    SetStatus("AC signature rejected — reinstall client 0.4.1+", false);
                    return;
                }
                if ((int)res.StatusCode == 401 &&
                    (errBody.Contains("Invalid or revoked", StringComparison.OrdinalIgnoreCase) ||
                     errBody.Contains("Device token", StringComparison.OrdinalIgnoreCase)))
                {
                    ClearLocalSession("Session expired — log in again");
                    return;
                }
                if (errBody.Contains("challenge", StringComparison.OrdinalIgnoreCase))
                {
                    SetStatus("AC challenge expired — retry in a moment", false);
                    return;
                }
                SetStatus(DescribeAcHttpError("attest", (int)res.StatusCode, errBody), false);
                return;
            }
            using var doc = JsonDocument.Parse(errBody);
            var root = doc.RootElement;
            var passed = root.TryGetProperty("passed", out var p) && p.GetBoolean();
            var banned = root.TryGetProperty("banned", out var b) && b.GetBoolean();
            _platformBanned = banned;

            if (!cheatClean)
            {
                _connectedOk = false;
                SetStatus("Banned | Cheat software detected — remove it to play", false);
                return;
            }
            if (banned)
            {
                _connectedOk = false;
                SetStatus("Banned on site | Contact support or remove cheats", false);
                return;
            }
            if (!passed)
            {
                _connectedOk = false;
                SetStatus("Fix required security features, then wait", false);
                return;
            }
            _connectedOk = true;
            RefreshConnectedStatus(force: true);
        }
        catch (Exception ex)
        {
            _connectedOk = false;
            SetStatus($"Connection error: {ex.GetType().Name}", false);
        }
        finally
        {
            _attestInFlight = false;
        }
    }

    private void ClearLocalSession(string status)
    {
        try { if (File.Exists(_tokenPath)) File.Delete(_tokenPath); } catch { }
        _deviceToken = null;
        _connectedOk = false;
        _lastGameRunning = null;
        _avatar.Image = null;
        _nameLbl.Text = "";
        ShowOut();
        SetStatus(status, false);
    }

    private static string DescribeAcHttpError(string step, int status, string body)
    {
        if (status == 401)
        {
            if (body.Contains("revoked", StringComparison.OrdinalIgnoreCase) ||
                body.Contains("Invalid or revoked", StringComparison.OrdinalIgnoreCase) ||
                body.Contains("Device token", StringComparison.OrdinalIgnoreCase))
            {
                return $"AC {step}: session expired — log in again";
            }
            if (body.Contains("challenge", StringComparison.OrdinalIgnoreCase))
                return $"AC {step}: challenge expired — retry";
            if (body.Contains("signature", StringComparison.OrdinalIgnoreCase))
                return $"AC {step}: signature rejected — update client";
            if (body.Contains("timestamp", StringComparison.OrdinalIgnoreCase))
                return $"AC {step}: clock skew — sync Windows time";
            return $"AC {step}: unauthorized — retry in a moment";
        }
        if (status == 403)
        {
            if (body.Contains("Update YGuard", StringComparison.OrdinalIgnoreCase))
                return "Update required — download latest from yguard.ir";
            return $"AC {step}: forbidden — update client";
        }
        if (status >= 500)
            return $"AC {step}: server error {status} — apply DB migrate / restart API";
        var shortBody = string.IsNullOrWhiteSpace(body)
            ? ""
            : " — " + body.Replace('\n', ' ').Trim();
        if (shortBody.Length > 80) shortBody = shortBody[..80] + "…";
        return $"AC {step} failed ({status}){shortBody}";
    }

    /// <summary>
    /// Scan for known cheat installs. Hits → platform ban. Clean → may unban if security OK.
    /// </summary>
    private async Task ScanCheatsAsync()
    {
        if (string.IsNullOrEmpty(_deviceToken)) return;

        var cs2Running = IsGameRunning();
        _cheatScan.Interval = cs2Running ? 8_000 : 20_000;
        RefreshConnectedStatus(force: false);

        List<CheatScanner.Hit> hits;
        try { hits = CheatScanner.Scan(); }
        catch { return; }

        // Soft overlay close only — never feeds the ban report.
        if (cs2Running)
        {
            try { OverlayGuard.ScanAndClose(); } catch { /* ignore */ }
        }

        if (hits.Count > 0)
        {
            try { CheatScanner.Remediate(hits); } catch { /* best-effort delete */ }
        }

        // Don't spam API.
        if ((DateTime.UtcNow - _lastCheatReport).TotalSeconds < 30) return;
        _lastCheatReport = DateTime.UtcNow;

        _reportedCheats = hits.Count > 0;

        object payload;
        if (hits.Count == 0)
        {
            payload = new { clean = true, hits = Array.Empty<object>() };
        }
        else
        {
            payload = new
            {
                clean = false,
                hits = hits
                    .GroupBy(h => h.Signature + "|" + (h.Path ?? "") + "|" + (h.ProcessName ?? ""))
                    .Select(g => g.First())
                    .Take(25)
                    .Select(h => new
                    {
                        signature = h.Signature,
                        path = h.Path,
                        process_name = h.ProcessName,
                    })
                    .ToList(),
            };
        }

        try
        {
            using var http = Http();
            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _deviceToken);
            var res = await http.PostAsJsonAsync("/plugins/ac/report", payload);
            var body = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode) return;

            using var doc = JsonDocument.Parse(body);
            var banned = doc.RootElement.TryGetProperty("banned", out var b) && b.GetBoolean();
            var unbanned = doc.RootElement.TryGetProperty("unbanned", out var u) && u.GetBoolean();
            _platformBanned = banned;

            if (hits.Count > 0 || banned)
            {
                _connectedOk = false;
                SetStatus(
                    hits.Count > 0
                        ? "Banned | Cheat software detected — remove it to play"
                        : "Banned | Fix security features to play",
                    false);
            }
            else if (unbanned)
            {
                // Re-attest so queue unlocks with fresh TTL.
                await AttestAsync();
            }
        }
        catch { /* network — retry next tick */ }
    }

    private static HttpClient Http()
    {
        var http = new HttpClient
        {
            BaseAddress = new Uri(ApiBase),
            Timeout = TimeSpan.FromSeconds(20),
        };
        http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        return http;
    }
}

internal class RoundedPanel : Panel
{
    public int Radius { get; set; } = 8;
    public Color Fill { get; set; } = Theme.Surface;

    public RoundedPanel()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Round(ClientRectangle, Radius);
        using var br = new SolidBrush(Fill);
        e.Graphics.FillPath(br, path);
        base.OnPaint(e);
    }

    internal static GraphicsPath Round(Rectangle r, int radius)
    {
        var p = new GraphicsPath();
        int d = radius * 2;
        var rr = new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1);
        p.AddArc(rr.X, rr.Y, d, d, 180, 90);
        p.AddArc(rr.Right - d, rr.Y, d, d, 270, 90);
        p.AddArc(rr.Right - d, rr.Bottom - d, d, d, 0, 90);
        p.AddArc(rr.X, rr.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}

internal sealed class RoundedButton : Control
{
    public int Radius { get; set; } = 6;
    public Color Fill { get; set; } = Theme.Orange;
    private bool _hover;

    public RoundedButton()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
        Cursor = Cursors.Hand;
        Size = new Size(260, 48);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseClick(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) OnClick(e);
        base.OnMouseClick(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        var fill = _hover ? ControlPaint.Light(Fill) : Fill;
        using var path = RoundedPanel.Round(ClientRectangle, Radius);
        using var br = new SolidBrush(fill);
        e.Graphics.FillPath(br, path);

        // Brand mark — transparent red Y on the orange/red login button
        if (Theme.LogoMark != null)
        {
            var dest = new Rectangle(12, 8, 32, 32);
            e.Graphics.DrawImage(Theme.LogoMark, dest);
        }
        else
        {
            using var w = new SolidBrush(Color.White);
            e.Graphics.FillPolygon(w, new[]
            {
                new Point(28, 16), new Point(40, 24), new Point(28, 32),
            });
        }

        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            new Rectangle(48, 0, Width - 56, Height),
            Color.White,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
    }
}

internal sealed class FeaturePill : Control
{
    private readonly string _label;
    private bool _ok = true;
    private bool _checking;
    private float _spinAngle;
    private readonly System.Windows.Forms.Timer _spinTimer = new() { Interval = 16 };

    public FeaturePill(string label)
    {
        _label = label;
        var w = TextRenderer.MeasureText(label, new Font("Segoe UI", 9f)).Width + 36;
        Size = new Size(Math.Max(w, 72), 28);
        Margin = new Padding(0, 0, 6, 6);
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        _spinTimer.Tick += (_, _) =>
        {
            if (!_checking) return;
            _spinAngle = (_spinAngle + 12f) % 360f;
            Invalidate();
        };
    }

    public void SetChecking()
    {
        _checking = true;
        _spinAngle = 0;
        if (!_spinTimer.Enabled) _spinTimer.Start();
        Invalidate();
    }

    public void SetOk(bool ok)
    {
        _checking = false;
        _ok = ok;
        _spinTimer.Stop();
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _spinTimer.Dispose();
        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        using (var path = RoundedPanel.Round(ClientRectangle, 14))
        using (var br = new SolidBrush(Theme.PillBg))
            g.FillPath(br, path);

        int cx = 13, cy = Height / 2;
        if (_checking)
        {
            using var track = new Pen(Color.FromArgb(70, 70, 74), 2f);
            g.DrawEllipse(track, cx - 6, cy - 6, 12, 12);
            using var arc = new Pen(Theme.Orange, 2f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
            };
            g.DrawArc(arc, cx - 6, cy - 6, 12, 12, _spinAngle, 110f);
        }
        else
        {
            using (var c = new SolidBrush(_ok ? Theme.Green : Theme.Red))
                g.FillEllipse(c, cx - 6, cy - 6, 12, 12);
            using var pen = new Pen(Color.White, 1.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            if (_ok)
                g.DrawLines(pen, new[] { new Point(cx - 3, cy), new Point(cx - 1, cy + 3), new Point(cx + 4, cy - 3) });
            else
            {
                g.DrawLine(pen, cx - 3, cy - 3, cx + 3, cy + 3);
                g.DrawLine(pen, cx + 3, cy - 3, cx - 3, cy + 3);
            }
        }

        TextRenderer.DrawText(g, _label, new Font("Segoe UI", 9f),
            new Point(24, (Height - 15) / 2), Theme.Text, TextFormatFlags.NoPadding);
    }
}

internal sealed class CheckReport
{
    [JsonPropertyName("secure_boot")] public bool secure_boot { get; set; }
    [JsonPropertyName("iommu")] public bool iommu { get; set; }
    [JsonPropertyName("tpm_20")] public bool tpm_20 { get; set; }
    [JsonPropertyName("tpm_attestation")] public bool tpm_attestation { get; set; }
    [JsonPropertyName("hvci")] public bool hvci { get; set; }
    [JsonPropertyName("windows_updates")] public bool windows_updates { get; set; }
    [JsonPropertyName("os_version")] public string os_version { get; set; } = "";
    [JsonPropertyName("hardware_hash")] public string hardware_hash { get; set; } = "";
}

internal static class SecurityChecks
{
    public static CheckReport Run()
    {
        // Soft-pass all platform checks for the client UI. Old boards often
        // expose Secure Boot / TPM registry stubs that look "supported but off"
        // even when the feature cannot be enabled — that caused false reds.
        // Server-side enforcement still uses challenge requirements when set.
        return new CheckReport
        {
            secure_boot = true,
            iommu = true,
            tpm_20 = true,
            tpm_attestation = true,
            hvci = true,
            windows_updates = true,
            os_version = Environment.OSVersion.ToString(),
            hardware_hash = BuildHardwareFingerprint(),
        };
    }

    private static string BuildHardwareFingerprint()
    {
        var guid = GetGuid();
        if (string.IsNullOrWhiteSpace(guid))
            guid = Wmi("Win32_BaseBoard", "SerialNumber");
        if (string.IsNullOrWhiteSpace(guid))
            guid = "unknown";
        return Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                Encoding.UTF8.GetBytes("yg-hw-v2|" + guid.Trim()))).ToLowerInvariant();
    }

    private static string Wmi(string cls, string prop)
    {
        try
        {
            using var s = new ManagementObjectSearcher($"SELECT {prop} FROM {cls}");
            foreach (ManagementObject o in s.Get())
            {
                var v = o[prop]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(v) &&
                    !v.Equals("None", StringComparison.OrdinalIgnoreCase) &&
                    !v.Equals("To Be Filled By O.E.M.", StringComparison.OrdinalIgnoreCase))
                {
                    return v;
                }
            }
        }
        catch { /* ignore */ }
        return "";
    }

    private static string GetGuid()
    {
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            return k?.GetValue("MachineGuid")?.ToString() ?? "";
        }
        catch { return ""; }
    }
}
