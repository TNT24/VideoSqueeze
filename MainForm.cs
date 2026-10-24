using System.Runtime.InteropServices;
using System.Text.Json;

namespace VideoSqueeze;

public class MainForm : Form
{
    static readonly string[] VideoExtensions =
        { ".mp4", ".mkv", ".mov", ".avi", ".m4v", ".webm", ".wmv", ".flv", ".ts", ".m2ts", ".mts", ".mpg", ".mpeg", ".mxf", ".3gp" };
    static readonly string SettingsPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VideoSqueeze", "settings.json");

    readonly ListView list = new SmoothList
    {
        View = View.Details, FullRowSelect = true, AllowDrop = true, Dock = DockStyle.Fill, OwnerDraw = true,
        BorderStyle = BorderStyle.None, HeaderStyle = ColumnHeaderStyle.Nonclickable, ShowItemToolTips = true,
        BackColor = Theme.Card, ForeColor = Theme.Text,
    };
    readonly Label lblEmpty = new()
    {
        Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, AllowDrop = true,
        BackColor = Theme.Card, ForeColor = Theme.SubtleText,
        Text = "Drop videos or folders here\r\n\r\nor use  Add files  /  Add folder  above",
    };
    readonly ModernCombo cbPreset = new(Settings.Presets.Select(p => p.Name).Append("Custom…").ToArray());
    readonly Label lblPresetInfo = new() { AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = Theme.SubtleText, Margin = new Padding(14, 6, 3, 3) };
    readonly Card settingsBox = new();
    readonly ModernCombo cbMode = new("Visually lossless — automatic (recommended)", "Manual quality value", "Mathematically lossless (bigger files!)");
    readonly ModernCombo cbCodec = new("AV1 — smallest files (CPU, SVT-AV1)", "HEVC/H.265 — plays everywhere (CPU, x265)", "HEVC/H.265 — fast (NVIDIA GPU)");
    readonly ModernCombo cbSpeed = new("Fast", "Balanced", "Best compression (slow)");
    readonly ModernCombo cbContainer = new("MKV (keeps every track)", "MP4 (most compatible)");
    readonly NumericUpDown numVmaf = new() { Minimum = 85, Maximum = 99, DecimalPlaces = 1, Increment = 0.5m, Width = 80 };
    readonly NumericUpDown numQuality = new() { Width = 80 };
    readonly TextBox txtOut = new() { PlaceholderText = "Same folder as the original" };
    readonly ModernCheck chkDiscard = new() { Text = "Skip videos that wouldn't get smaller", AutoSize = true };
    readonly ModernCheck chkNotify = new() { Text = "Alert me when finished", AutoSize = true, Checked = true };
    readonly ModernCheck chkCompare = new() { Text = "Compare when finished", AutoSize = true };
    readonly NotifyIcon tray = new();
    readonly ModernButton btnEstimate = new("Estimate savings");
    readonly ModernButton btnStart = new("Start", primary: true);
    readonly ModernButton btnCancel = new("Stop") { Enabled = false };
    readonly Dictionary<string, (string Key, Estimate Estimate)> estimates = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, string> outputs = new(StringComparer.OrdinalIgnoreCase);
    readonly SlimProgress bar = new() { Dock = DockStyle.Fill, Margin = new Padding(0, 8, 0, 0) };
    readonly Label lblStatus = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft, Text = "Add videos to begin.", AutoEllipsis = true };
    readonly TextBox txtLog = new()
    {
        Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, BorderStyle = BorderStyle.None,
        BackColor = Theme.Card, ForeColor = Theme.SubtleText,
    };
    readonly ToolTip tips = new() { AutoPopDelay = 20000 };
    readonly Card updateBar = new() { Dock = DockStyle.Fill, BackColor = Theme.Selection, Visible = false };
    readonly Label lblUpdate = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
    readonly ModernButton btnUpdate = new("Update", primary: true);
    readonly ModernButton btnUpdateLater = new("Later");
    readonly System.Windows.Forms.Timer updateTimer = new() { Interval = 6 * 60 * 60 * 1000 };
    readonly LinkLabel lnkCheckUpdates = new()
    {
        Text = "Check for updates", AutoSize = true, Margin = Padding.Empty, LinkBehavior = LinkBehavior.HoverUnderline,
        LinkColor = Theme.Accent, ActiveLinkColor = Theme.AccentPressed, VisitedLinkColor = Theme.Accent, DisabledLinkColor = Theme.SubtleText,
    };
    Update? update;
    bool checkingUpdate;
    string? updateInstaller; // downloaded, waiting for the current run to finish

    CancellationTokenSource? cts;
    List<ListViewItem>? queue; // the running batch; Retry appends to it
    bool lastRunEstimate;
    bool loading;
    bool closeWhenStopped; // the window was closed mid-run; finish stopping first

    int S(int px) => (int)Math.Round(px * DeviceDpi / 96f);

    public MainForm(string[]? startupPaths = null)
    {
        if (startupPaths is { Length: > 0 }) Shown += (_, _) => OpenPaths(startupPaths.Select(Path.GetFullPath));
        Text = "VideoSqueeze";
        Font = Theme.Body();
        BackColor = Theme.Window;
        ForeColor = Theme.Text;
        Size = new Size(S(1080), S(860));
        MinimumSize = new Size(S(880), S(680));
        StartPosition = FormStartPosition.CenterScreen;
        Icon = LoadIcon(256);
        HandleCreated += (_, _) => Theme.StyleTitleBar(this);
        txtLog.Font = new Font(FontFamily.Families.Any(f => f.Name == "Cascadia Mono") ? "Cascadia Mono" : "Consolas", 9f);
        lblEmpty.Font = Theme.Body(11f);
        lblStatus.Font = Theme.Body(9.75f, FontStyle.Bold);
        foreach (Control c in new Control[] { txtOut, numVmaf, numQuality })
        {
            c.BackColor = Theme.CardAlt;
            c.ForeColor = Theme.Text;
        }
        txtOut.BorderStyle = BorderStyle.FixedSingle;
        numVmaf.BorderStyle = numQuality.BorderStyle = BorderStyle.FixedSingle;
        chkDiscard.ForeColor = Theme.Text;

        // ----- header -----
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2, AutoSize = true, Margin = new Padding(0, 0, 0, S(14)) };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var logo = new PictureBox { Image = LoadIcon(64).ToBitmap(), SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(S(44), S(44)), Margin = new Padding(0, 0, S(12), 0) };
        header.Controls.Add(logo, 0, 0);
        header.SetRowSpan(logo, 2);
        header.Controls.Add(new Label { Text = "VideoSqueeze", AutoSize = true, Font = Theme.Display(17f), Margin = new Padding(0, 0, 0, 0) }, 1, 0);
        header.Controls.Add(new Label { Text = "Shrink videos without visible quality loss", AutoSize = true, ForeColor = Theme.SubtleText, Margin = new Padding(2, 0, 0, 0) }, 1, 1);
        var fileButtons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Anchor = AnchorStyles.Right, Margin = Padding.Empty };
        fileButtons.Controls.Add(Btn("Add files…", AddFilesDialog));
        fileButtons.Controls.Add(Btn("Add folder…", AddFolderDialog));
        fileButtons.Controls.Add(Btn("Remove", RemoveSelected));
        fileButtons.Controls.Add(Btn("Clear", ClearList));
        var compare = Btn("Compare", CompareSelected);
        compare.Primary = true;
        tips.SetToolTip(compare, "Spot-check a finished video: original vs compressed, side by side (or double-click it in the list).");
        fileButtons.Controls.Add(compare);
        header.Controls.Add(fileButtons, 2, 0);
        header.SetRowSpan(fileButtons, 2);

        // ----- file list -----
        list.Columns.Add("Video", 300);
        list.Columns.Add("Size", S(90), HorizontalAlignment.Right);
        list.Columns.Add("Status", S(240));
        list.Columns.Add("Est. saving", S(160), HorizontalAlignment.Right);
        list.Columns.Add("New size", S(95), HorizontalAlignment.Right);
        list.SmallImageList = new ImageList { ImageSize = new Size(1, S(34)) }; // row height
        list.DrawColumnHeader += DrawHeader;
        list.DrawItem += (_, e) => e.DrawDefault = false;
        list.DrawSubItem += DrawCell;
        list.Resize += (_, _) => { if (list.ClientSize.Width > 0 && list.ClientSize.Height > 0) FitColumns(); };
        list.DragEnter += OnDragEnter;
        list.DragDrop += OnDragDrop;
        lblEmpty.DragEnter += OnDragEnter;
        lblEmpty.DragDrop += OnDragDrop;
        list.KeyDown += (_, e) => { if (e.KeyCode == Keys.Delete) RemoveSelected(); };
        list.DoubleClick += (_, _) => CompareSelected();
        list.MouseClick += (_, e) => { if (RetryHit(e.Location) is { } item) Retry(item); };
        list.MouseMove += (_, e) => list.Cursor = RetryHit(e.Location) != null ? Cursors.Hand : Cursors.Default;
        Theme.StyleNative(list);
        list.HandleCreated += (_, _) => headerFiller.AssignHandle(SendMessage(list.Handle, LVM_GETHEADER, IntPtr.Zero, IntPtr.Zero));
        Theme.StyleNative(txtLog);
        var listCard = new Card { Dock = DockStyle.Fill, Padding = new Padding(S(6)) };
        listCard.Controls.Add(list);
        listCard.Controls.Add(lblEmpty);
        lblEmpty.BringToFront();

        // ----- preset card -----
        var presetCard = new Card { Dock = DockStyle.Fill };
        var presetGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 3, BackColor = Theme.Card };
        presetGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        presetGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(380)));
        presetGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        presetGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 33));
        presetGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 33));
        presetGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 34));
        cbPreset.Dock = DockStyle.Fill;
        cbPreset.Font = Theme.Body(10.5f, FontStyle.Bold);
        cbPreset.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        presetGrid.Controls.Add(Caption("PRESET"), 0, 0);
        presetGrid.Controls.Add(cbPreset, 1, 0);
        presetGrid.Controls.Add(lblPresetInfo, 2, 0);
        var outPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty, BackColor = Theme.Card };
        outPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        outPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        txtOut.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        outPanel.Controls.Add(txtOut, 0, 0);
        var browse = Btn("Browse…", BrowseOutput);
        browse.Height = S(30);
        outPanel.Controls.Add(browse, 1, 0);
        presetGrid.Controls.Add(Caption("SAVE TO"), 0, 1);
        presetGrid.Controls.Add(outPanel, 1, 1);
        var checks = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Anchor = AnchorStyles.Left, BackColor = Theme.Card, Margin = Padding.Empty };
        foreach (var c in new[] { chkDiscard, chkNotify, chkCompare })
        {
            c.ForeColor = Theme.Text;
            c.Margin = new Padding(0, 3, S(18), 3);
            checks.Controls.Add(c);
        }
        presetGrid.Controls.Add(checks, 1, 2);
        presetGrid.SetColumnSpan(checks, 2);
        tips.SetToolTip(chkCompare, "When a run finishes, opens the side-by-side comparison for the last video it compressed.");
        tips.SetToolTip(chkNotify,"Plays a sound, shows a Windows notification and flashes the taskbar button when a run finishes.");
        tray.Icon = Icon;
        tray.Text = "VideoSqueeze";
        tray.BalloonTipClicked += (_, _) => BringToFrontNow();
        tray.BalloonTipClosed += (_, _) => tray.Visible = false;
        tray.Click += (_, _) => BringToFrontNow();
        presetCard.Controls.Add(presetGrid);

        // ----- custom settings card -----
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 3, BackColor = Theme.Card };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (int i = 0; i < 3; i++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 33.3f));
        AddRow(grid, 0, "MODE", cbMode, "CODEC", cbCodec);
        AddRow(grid, 1, "TARGET VMAF", numVmaf, "SPEED", cbSpeed);
        AddRow(grid, 2, "QUALITY (CRF/CQ)", numQuality, "CONTAINER", cbContainer);
        settingsBox.Dock = DockStyle.Fill;
        settingsBox.Controls.Add(grid);

        tips.SetToolTip(cbMode,
            "Visually lossless: test-encodes samples and measures them with Netflix's VMAF metric,\n" +
            "then picks the smallest file that still looks identical to the original.\n\n" +
            "Mathematically lossless: bit-exact, but usually LARGER than camera/downloaded files.\n" +
            "Only useful for raw/ProRes/screen recordings.");
        tips.SetToolTip(numVmaf, "VMAF 0–100. 95 = indistinguishable for practically everyone (default).\n93 = very close. 97+ = extra safety margin, bigger files.");
        tips.SetToolTip(cbCodec, "AV1: best compression, slowest.\nHEVC x265: very good, plays on almost all TVs/phones.\nNVIDIA GPU: many times faster, files ~15–30% bigger for the same quality.");
        tips.SetToolTip(numQuality, "Lower number = better quality, bigger file.");
        tips.SetToolTip(chkDiscard, "If compressing a video wouldn't make it smaller, don't save a compressed copy.\nYour original videos are never changed or deleted either way.");
        tips.SetToolTip(btnEstimate, "Checks a few samples of each video (a few minutes per file, no full encode)\nand shows how much space you'd save. Start then reuses this result.");

        // ----- action card -----
        var actionCard = new Card { Dock = DockStyle.Fill };
        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, BackColor = Theme.Card };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 3; i++) actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        actions.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var progressStack = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Margin = new Padding(0, 0, S(16), 0), BackColor = Theme.Card };
        progressStack.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        progressStack.RowStyles.Add(new RowStyle(SizeType.Absolute, S(16)));
        progressStack.Controls.Add(lblStatus, 0, 0);
        progressStack.Controls.Add(bar, 0, 1);
        actions.Controls.Add(progressStack, 0, 0);
        foreach (var (b, col) in new[] { (btnCancel, 1), (btnEstimate, 2), (btnStart, 3) })
        {
            b.Anchor = AnchorStyles.None;
            b.Height = S(40);
            b.Margin = new Padding(S(8), 0, 0, 0);
            actions.Controls.Add(b, col, 0);
        }
        btnStart.Width = Math.Max(btnStart.Width, S(120));
        btnStart.Click += async (_, _) => await StartAsync(estimateOnly: false);
        btnEstimate.Click += async (_, _) => await StartAsync(estimateOnly: true);
        btnCancel.Click += (_, _) => cts?.Cancel();
        actionCard.Controls.Add(actions);

        // ----- activity log card -----
        var logCard = new Card { Dock = DockStyle.Fill, Margin = Padding.Empty };
        logCard.Controls.Add(txtLog);
        var logTitle = Caption("ACTIVITY");
        logTitle.Dock = DockStyle.Left;
        // Version and update link at the right of the title bar (the header has no room left at the minimum width).
        var versionBar = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false, BackColor = Theme.Card, Margin = Padding.Empty };
        versionBar.Controls.Add(new Label { Text = $"v{Updater.Current}  ·", AutoSize = true, ForeColor = Theme.SubtleText, Margin = Padding.Empty });
        lnkCheckUpdates.Margin = Padding.Empty;
        lnkCheckUpdates.Click += async (_, _) => await CheckForUpdateAsync(manual: true);
        versionBar.Controls.Add(lnkCheckUpdates);
        var logHeader = new Panel { Dock = DockStyle.Top, Height = S(24), BackColor = Theme.Card };
        logHeader.Controls.Add(versionBar);
        logHeader.Controls.Add(logTitle);
        logCard.Controls.Add(logHeader);

        // ----- update bar (hidden until a newer release is found) -----
        var updateRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = Theme.Selection };
        updateRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        updateRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        updateRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        updateRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        lblUpdate.ForeColor = Theme.Text;
        lblUpdate.Font = Theme.Body(9.75f, FontStyle.Bold);
        updateRow.Controls.Add(lblUpdate, 0, 0);
        foreach (var (b, col) in new[] { (btnUpdateLater, 1), (btnUpdate, 2) })
        {
            b.Anchor = AnchorStyles.None;
            b.Height = S(32);
            b.Margin = new Padding(S(8), 0, 0, 0);
            updateRow.Controls.Add(b, col, 0);
        }
        btnUpdate.Click += async (_, _) => await UpdateNowAsync();
        btnUpdateLater.Click += (_, _) => ShowUpdateBar(false);
        updateBar.Padding = new Padding(S(16), S(6), S(10), S(6));
        updateBar.Controls.Add(updateRow);
        updateTimer.Tick += async (_, _) => await CheckForUpdateAsync();
        Shown += async (_, _) =>
        {
            await Task.Delay(3000); // let the window settle first; this is a background check
            await CheckForUpdateAsync();
            updateTimer.Start();
        };

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 7, ColumnCount = 1, Padding = new Padding(S(20), S(16), S(20), S(18)) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, S(150)));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, S(150)));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, S(84)));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
        root.Controls.Add(updateBar, 0, 0);
        root.Controls.Add(header, 0, 1);
        root.Controls.Add(listCard, 0, 2);
        root.Controls.Add(presetCard, 0, 3);
        root.Controls.Add(settingsBox, 0, 4);
        root.Controls.Add(actionCard, 0, 5);
        root.Controls.Add(logCard, 0, 6);
        Controls.Add(root);

        cbPreset.SelectedIndexChanged += (_, _) => { OnPresetChanged(); RefreshEstimates(); };
        cbMode.SelectedIndexChanged += (_, _) => { UpdateEnabled(); RefreshEstimates(); };
        cbSpeed.SelectedIndexChanged += (_, _) => RefreshEstimates();
        numVmaf.ValueChanged += (_, _) => RefreshEstimates();
        cbCodec.SelectedIndexChanged += (_, _) => { OnCodecChanged(); RefreshEstimates(); };
        LoadSettings();
        UpdateEnabled();

        if (Tools.Locate())
            Log($"Using FFmpeg: {Tools.Ffmpeg}");
        else
        {
            Log("FFmpeg not found! Install Shutter Encoder, put ffmpeg.exe + ffprobe.exe next to VideoSqueeze.exe, or add them to PATH.");
            btnStart.Enabled = btnEstimate.Enabled = false;
        }
        Log("Tip: hover over the settings for explanations. Originals are never modified or deleted.");
    }

    static Icon LoadIcon(int size)
    {
        using var s = typeof(MainForm).Assembly.GetManifestResourceStream("app.ico")!;
        return new Icon(s, new Size(size, size));
    }

    // ---------- list drawing ----------

    void FitColumns()
    {
        int others = 0;
        for (int i = 1; i < list.Columns.Count; i++) others += list.Columns[i].Width;
        int scrollbar = SystemInformation.VerticalScrollBarWidth + 2;
        list.Columns[0].Width = Math.Max(S(160), list.ClientSize.Width - others - scrollbar);
    }

    const int LVM_GETHEADER = 0x101F;
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    readonly HeaderFiller headerFiller = new();

    /// <summary>Owner-draw only paints the columns; Windows fills the header past the last column in white. Paint it to match.</summary>
    sealed class HeaderFiller : NativeWindow
    {
        const int WM_PAINT = 0x000F;
        const int HDM_GETITEMCOUNT = 0x1200, HDM_GETITEMRECT = 0x1207;
        [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref RECT lParam);
        [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr hWnd, out RECT rect);

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg != WM_PAINT) return;
            int count = (int)MainForm.SendMessage(Handle, HDM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);
            var last = new RECT();
            if (count > 0) SendMessage(Handle, HDM_GETITEMRECT, count - 1, ref last);
            GetClientRect(Handle, out var client);
            if (last.Right >= client.Right) return;
            using var g = Graphics.FromHwnd(Handle);
            using (var b = new SolidBrush(Theme.Card)) g.FillRectangle(b, last.Right, 0, client.Right - last.Right, client.Bottom);
            using var p = new Pen(Theme.Border);
            g.DrawLine(p, last.Right, client.Bottom - 1, client.Right, client.Bottom - 1);
        }
    }

    void DrawHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
    {
        using (var b = new SolidBrush(Theme.Card)) e.Graphics.FillRectangle(b, e.Bounds);
        using (var p = new Pen(Theme.Border))
        {
            e.Graphics.DrawLine(p, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            if (e.ColumnIndex < list.Columns.Count - 1) e.Graphics.DrawLine(p, e.Bounds.Right - 1, e.Bounds.Top, e.Bounds.Right - 1, e.Bounds.Bottom - 1);
        }
        var r = Rectangle.Inflate(e.Bounds, -S(8), 0);
        var align = e.Header?.TextAlign == HorizontalAlignment.Right ? TextFormatFlags.Right : TextFormatFlags.Left;
        using var f = Theme.Body(8.5f, FontStyle.Bold);
        TextRenderer.DrawText(e.Graphics, e.Header?.Text.ToUpperInvariant(), f, r, Theme.SubtleText, align | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    void DrawCell(object? sender, DrawListViewSubItemEventArgs e)
    {
        if (e.Item == null || e.SubItem == null) return;
        bool selected = e.Item.Selected;
        // Each cell paints its own borders: a cell repainted on its own (e.g. a status update) would otherwise erase
        // lines drawn by its neighbours. The last column also covers the strip left free for the scrollbar.
        bool lastCol = e.ColumnIndex == list.Columns.Count - 1;
        var cell = lastCol ? Rectangle.FromLTRB(e.Bounds.Left, e.Bounds.Top, Math.Max(e.Bounds.Right, list.ClientSize.Width), e.Bounds.Bottom) : e.Bounds;
        using (var b = new SolidBrush(selected ? Theme.Selection : Theme.Card)) e.Graphics.FillRectangle(b, cell);
        using (var p = new Pen(Theme.Border))
        {
            e.Graphics.DrawLine(p, cell.Left, cell.Bottom - 1, cell.Right, cell.Bottom - 1);
            if (!lastCol) e.Graphics.DrawLine(p, cell.Right - 1, cell.Top, cell.Right - 1, cell.Bottom - 1);
        }

        string text = e.SubItem.Text;
        Color color = Theme.Text;
        Font font = list.Font;
        if (e.ColumnIndex == 2)
        {
            color = text.StartsWith("Done") ? Theme.Success
                : text.StartsWith("Failed") ? Theme.Error
                : text.StartsWith("Skipped") || text.Contains("will be skipped") || text == "Stopped" ? Theme.Warning
                : text == "Queued" ? Theme.SubtleText
                : text.StartsWith("Estimated") ? Theme.Text
                : Theme.Accent;
        }
        else if (e.ColumnIndex == 3 && text.StartsWith("−")) color = Theme.Success;
        else if (e.ColumnIndex is 1 or 4) color = Theme.SubtleText;

        var r = Rectangle.Inflate(e.Bounds, -S(8), 0);
        if (e.ColumnIndex == 2 && IsFailed(e.Item))
        {
            var pill = RetryRect(e.Bounds);
            r.Width = pill.Left - r.Left - S(6);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var path = Theme.Rounded(pill, pill.Height / 2f))
            using (var b = new SolidBrush(Theme.Accent))
                e.Graphics.FillPath(b, path);
            using var pf = Theme.Body(8.25f, FontStyle.Bold);
            TextRenderer.DrawText(e.Graphics, "↻ Retry", pf, pill, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
        var align = list.Columns[e.ColumnIndex].TextAlign == HorizontalAlignment.Right ? TextFormatFlags.Right : TextFormatFlags.Left;
        TextRenderer.DrawText(e.Graphics, text, font, r, color, align | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
    }

    // ---------- retry ----------

    static bool IsFailed(ListViewItem item) => item.SubItems[2].Text.StartsWith("Failed");

    Rectangle RetryRect(Rectangle cell)
    {
        int w = S(64), h = S(22);
        return new Rectangle(cell.Right - S(8) - w, cell.Top + (cell.Height - h) / 2, w, h);
    }

    ListViewItem? RetryHit(Point p)
    {
        var hit = list.HitTest(p);
        if (hit.Item == null || hit.SubItem == null || !IsFailed(hit.Item)) return null;
        if (hit.Item.SubItems.IndexOf(hit.SubItem) != 2) return null;
        return RetryRect(hit.SubItem.Bounds).Contains(p) ? hit.Item : null;
    }

    /// <summary>Re-runs one failed file: straight away when idle, or at the end of the batch that's running.</summary>
    async void Retry(ListViewItem item)
    {
        item.SubItems[2].Text = "Queued";
        if (queue != null)
        {
            queue.Add(item); // it already failed this run, so it's behind the current position
            Log($"Retry: {item.Text} added to the end of the queue.");
            return;
        }
        Log($"Retrying {item.Text}…");
        await StartAsync(lastRunEstimate, new[] { item });
    }

    void UpdateEmptyState() => lblEmpty.Visible = list.Items.Count == 0;

    void OnDragEnter(object? sender, DragEventArgs e) =>
        e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Copy : DragDropEffects.None;

    void OnDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths) AddPaths(paths);
    }

    // ---------- settings ----------

    bool IsCustom => cbPreset.SelectedIndex >= Settings.Presets.Length;

    Settings ReadSettings()
    {
        var s = ReadControls();
        s.Preset = cbPreset.SelectedIndex;
        if (!IsCustom)
        {
            var p = Settings.Presets[cbPreset.SelectedIndex];
            s.Mode = p.Mode;
            s.Codec = p.Codec;
            s.Speed = p.Speed;
            s.TargetVmaf = p.TargetVmaf;
            s.Container = "mkv";
        }
        return s;
    }

    void OnPresetChanged()
    {
        settingsBox.Visible = IsCustom;
        // Collapse the row too, otherwise the hidden box still reserves its space.
        if (settingsBox.Parent is TableLayoutPanel root)
            root.RowStyles[root.GetRow(settingsBox)] = IsCustom ? new RowStyle(SizeType.Absolute, S(150)) : new RowStyle(SizeType.Absolute, 0);
        lblPresetInfo.Text = IsCustom ? "Choose every setting yourself." : Settings.Presets[cbPreset.SelectedIndex].Description;
        if (loading || IsCustom) return;
        // Mirror the preset into the custom controls, so "Custom…" starts from the last preset used.
        var p = Settings.Presets[cbPreset.SelectedIndex];
        loading = true;
        cbMode.SelectedIndex = (int)p.Mode;
        cbCodec.SelectedIndex = (int)p.Codec;
        ApplyQualityRange(p.Codec);
        numQuality.Value = Settings.QualityRange(p.Codec).Default;
        cbSpeed.SelectedIndex = (int)p.Speed;
        numVmaf.Value = (decimal)p.TargetVmaf;
        cbContainer.SelectedIndex = 0;
        loading = false;
        UpdateEnabled();
    }

    Settings ReadControls() => new()
    {
        Mode = (Mode)cbMode.SelectedIndex,
        Codec = (Codec)cbCodec.SelectedIndex,
        Speed = (Speed)cbSpeed.SelectedIndex,
        TargetVmaf = (double)numVmaf.Value,
        ManualQuality = (int)numQuality.Value,
        Container = cbContainer.SelectedIndex == 1 ? "mp4" : "mkv",
        OutputFolder = txtOut.Text.Trim(),
        DiscardIfLarger = chkDiscard.Checked,
        NotifyWhenDone = chkNotify.Checked,
        CompareWhenDone = chkCompare.Checked,
    };

    void LoadSettings()
    {
        var s = new Settings();
        try { if (File.Exists(SettingsPath)) s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath)) ?? s; } catch { }
        loading = true;
        cbMode.SelectedIndex = (int)s.Mode;
        cbCodec.SelectedIndex = (int)s.Codec;
        cbSpeed.SelectedIndex = (int)s.Speed;
        numVmaf.Value = Math.Clamp((decimal)s.TargetVmaf, numVmaf.Minimum, numVmaf.Maximum);
        ApplyQualityRange(s.Codec);
        numQuality.Value = Math.Clamp(s.ManualQuality, numQuality.Minimum, numQuality.Maximum);
        cbContainer.SelectedIndex = s.Container == "mp4" ? 1 : 0;
        txtOut.Text = s.OutputFolder;
        chkDiscard.Checked = s.DiscardIfLarger;
        chkNotify.Checked = s.NotifyWhenDone;
        chkCompare.Checked = s.CompareWhenDone;
        cbPreset.SelectedIndex = Math.Clamp(s.Preset, 0, Settings.Presets.Length);
        loading = false;
        OnPresetChanged();
    }

    void SaveSettings(Settings s)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(s));
        }
        catch { }
    }

    void OnCodecChanged()
    {
        if (loading) return;
        ApplyQualityRange((Codec)cbCodec.SelectedIndex);
        numQuality.Value = Settings.QualityRange((Codec)cbCodec.SelectedIndex).Default;
    }

    void ApplyQualityRange(Codec c)
    {
        var (min, max, _) = Settings.QualityRange(c);
        numQuality.Minimum = min;
        numQuality.Maximum = max;
    }

    void UpdateEnabled()
    {
        var mode = (Mode)Math.Max(0, cbMode.SelectedIndex);
        numVmaf.Enabled = mode == Mode.Auto;
        numQuality.Enabled = mode == Mode.Manual;
        cbCodec.Enabled = cbSpeed.Enabled = mode != Mode.Lossless;
    }

    // ---------- file list ----------

    void AddPaths(IEnumerable<string> paths)
    {
        var existing = list.Items.Cast<ListViewItem>().Select(i => (string)i.Tag!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        int already = 0;
        foreach (var p in paths)
        {
            IEnumerable<string> files = Directory.Exists(p)
                // Skip folders Windows won't let us read (e.g. "System Volume Information" on a drive root) instead of crashing.
                ? Directory.EnumerateFiles(p, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true })
                : new[] { p };
            foreach (var f in files.Where(f => VideoExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())).OrderBy(f => f))
            {
                if (!existing.Add(f) || Path.GetFileNameWithoutExtension(f).Contains("_squeezed")) continue;
                var item = new ListViewItem(new[] { Path.GetFileName(f), Format.Bytes(new FileInfo(f).Length), "Queued", "", "" })
                {
                    Tag = f,
                    ToolTipText = f,
                };
                // Compressed before (e.g. the folder was added again): don't make a second copy. Remove the
                // _squeezed file to compress it again.
                if (FindOutput(f) is { } done)
                {
                    outputs[f] = done;
                    item.SubItems[2].Text = "Done (already squeezed)";
                    item.SubItems[4].Text = Format.Bytes(new FileInfo(done).Length);
                    already++;
                }
                list.Items.Add(item);
            }
        }
        UpdateEmptyState();
        if (already > 0) Log($"{already} video(s) already have a _squeezed copy — marked Done and skipped. Delete the copy to compress again.");
        long total = list.Items.Cast<ListViewItem>().Sum(i => File.Exists((string)i.Tag!) ? new FileInfo((string)i.Tag!).Length : 0);
        lblStatus.Text = $"{list.Items.Count} video(s) — {Format.Bytes(total)} in total.";
    }

    void AddFilesDialog()
    {
        using var dlg = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "Videos|" + string.Join(";", VideoExtensions.Select(e => "*" + e)) + "|All files|*.*",
        };
        if (dlg.ShowDialog(this) == DialogResult.OK) AddPaths(dlg.FileNames);
    }

    void AddFolderDialog()
    {
        using var dlg = new FolderBrowserDialog { Description = "Add every video in this folder (and subfolders)" };
        if (dlg.ShowDialog(this) == DialogResult.OK) AddPaths(new[] { dlg.SelectedPath });
    }

    void BrowseOutput()
    {
        using var dlg = new FolderBrowserDialog { Description = "Where to save compressed videos" };
        if (dlg.ShowDialog(this) == DialogResult.OK) txtOut.Text = dlg.SelectedPath;
    }

    void RemoveSelected()
    {
        if (cts != null) return;
        if (list.SelectedItems.Count > 0 && list.SelectedItems.Count == list.Items.Count) { ClearList(); return; }
        foreach (ListViewItem i in list.SelectedItems) list.Items.Remove(i);
        UpdateEmptyState();
    }

    void ClearList()
    {
        if (cts != null) return;
        // Scroll back to the top first: clearing a scrolled ListView keeps its old scroll
        // offset, so the next files added would start partway down the list.
        if (list.Items.Count > 0) list.EnsureVisible(0);
        list.Items.Clear();
        UpdateEmptyState();
        lblStatus.Text = "Add videos to begin.";
    }

    // ---------- spot-check ----------

    void CompareSelected()
    {
        if (list.SelectedItems.Count == 0)
        {
            MessageBox.Show(this, "Select a video in the list first (one that has been compressed).", Text);
            return;
        }
        string src = (string)list.SelectedItems[0].Tag!;
        var output = FindOutput(src);
        if (output == null)
        {
            MessageBox.Show(this, $"No compressed version of \"{Path.GetFileName(src)}\" was found yet.\r\nCompress it first, then compare.", Text);
            return;
        }
        using var f = new CompareForm(src, output);
        f.ShowDialog(this);
    }

    /// <summary>The compressed file for a source: from this session, or one on disk that provably came from it.</summary>
    string? FindOutput(string src)
    {
        if (outputs.TryGetValue(src, out var known) && File.Exists(known)) return known;
        string outFolder = txtOut.Text;
        // Off the UI thread: blocking on it directly would deadlock on the WinForms synchronization context.
        try { return Task.Run(() => Squeezer.FindOutputAsync(src, outFolder, default)).GetAwaiter().GetResult(); }
        catch { return null; } // e.g. ffprobe missing
    }

    // ---------- running ----------

    /// <summary>Estimates are only reusable for the same quality-affecting settings.</summary>
    static string EstimateKey(Settings s) => $"{s.Mode}|{s.Codec}|{s.Speed}|{s.TargetVmaf}";

    async Task StartAsync(bool estimateOnly, IList<ListViewItem>? only = null)
    {
        var settings = ReadSettings();
        if (estimateOnly && settings.Mode != Mode.Auto)
        {
            MessageBox.Show(this, "Estimates are available for the automatic (visually lossless) mode — all presets use it.", Text);
            return;
        }
        string key = EstimateKey(settings);
        var pending = (only ?? list.Items.Cast<ListViewItem>().ToList()).Where(i =>
        {
            var status = i.SubItems[2].Text;
            if (status.StartsWith("Done") || status.StartsWith("Skipped")) return false;
            return !estimateOnly || !(estimates.TryGetValue((string)i.Tag!, out var e) && e.Key == key);
        }).ToList();
        if (pending.Count == 0)
        {
            MessageBox.Show(this, estimateOnly ? "Every file already has an estimate for this preset." : "Add some videos first (or they're all done already).", Text);
            return;
        }
        if (settings.OutputFolder != "" && !Path.IsPathFullyQualified(settings.OutputFolder))
        {
            MessageBox.Show(this, "The 'Save to' folder must be a full path.", Text);
            return;
        }
        SaveSettings(settings);

        cts = new CancellationTokenSource();
        queue = pending;
        lastRunEstimate = estimateOnly;
        SetRunning(true);
        KeepAwake(true);
        long before = 0, after = 0;
        string? lastCompressed = null;
        int n = 0, failed = 0;
        bool stopped = false;
        try
        {
            for (int qi = 0; qi < pending.Count; qi++) // index loop: Retry can append while running
            {
                var item = pending[qi];
                if (!list.Items.Contains(item)) continue;
                n++;
                string path = (string)item.Tag!;
                item.SubItems[2].Text = "Working…";
                item.EnsureVisible();
                var squeezer = new Squeezer(settings,
                    msg => BeginInvoke(() => Log(msg)),
                    (phase, frac) => BeginInvoke(() =>
                    {
                        bar.Value = (int)(Math.Clamp(frac, 0, 1) * 1000);
                        lblStatus.Text = $"File {n} of {pending.Count}  ·  {phase}";
                        item.SubItems[2].Text = phase;
                    }));
                try
                {
                    if (!File.Exists(path)) throw new FileNotFoundException("File no longer exists.");
                    if (estimateOnly)
                    {
                        var e = await Task.Run(() => squeezer.EstimateAsync(path, cts.Token));
                        estimates[path] = (key, e);
                        ShowEstimate(item, e);
                        item.SubItems[2].Text = e.WorthIt ? "Estimated — ready" : "Estimated — will be skipped";
                        before += e.OriginalSize;
                        after += e.WorthIt ? e.PredictedSize : e.OriginalSize;
                    }
                    else
                    {
                        var known = estimates.TryGetValue(path, out var k) && k.Key == key ? k.Estimate : null;
                        var r = await Task.Run(() => squeezer.RunAsync(path, cts.Token, known));
                        item.SubItems[2].Text = r.Status;
                        if (r.Saved)
                        {
                            if (r.OutputPath != null) { outputs[path] = r.OutputPath; lastCompressed = path; }
                            item.SubItems[4].Text = Format.Bytes(r.OutputSize);
                            before += new FileInfo(path).Length;
                            after += r.OutputSize;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    item.SubItems[2].Text = "Stopped";
                    Log("Stopped by user.");
                    stopped = true;
                    break;
                }
                catch (Exception ex)
                {
                    failed++;
                    item.SubItems[2].Text = "Failed — see log";
                    Log($"  ✖ {Path.GetFileName(path)}: {ex.Message}");
                }
            }
        }
        finally
        {
            KeepAwake(false);
            cts.Dispose();
            cts = null;
            queue = null;
            SetRunning(false);
        }
        if (closeWhenStopped) { Close(); return; }
        bar.Value = 0;
        if (estimateOnly)
        {
            // Total over every file that has an estimate for this preset, not just the ones checked this time.
            var all = list.Items.Cast<ListViewItem>()
                .Select(i => estimates.TryGetValue((string)i.Tag!, out var e) && e.Key == key ? e.Estimate : null)
                .Where(e => e != null).ToList();
            long orig = all.Sum(e => e!.OriginalSize), est = all.Sum(e => e!.WorthIt ? e.PredictedSize : e.OriginalSize);
            lblStatus.Text = orig > 0
                ? $"Estimate for {all.Count} file(s): {Format.Bytes(orig)} → ≈{Format.Bytes(est)}  ·  saves ≈{Format.Bytes(orig - est)} ({100.0 * (orig - est) / orig:0}%). Press Start to compress."
                : "Estimate finished.";
        }
        else
            lblStatus.Text = before > 0
                ? $"Finished. {Format.Bytes(before)} → {Format.Bytes(after)}  ·  saved {Format.Bytes(before - after)} ({100.0 * (before - after) / before:0}%)."
                : "Finished.";
        Log(lblStatus.Text);

        if (!stopped && chkNotify.Checked)
        {
            string title = estimateOnly ? "Estimate finished" : failed > 0 ? $"Finished — {failed} video(s) failed" : "Compression finished";
            Alert(title, lblStatus.Text.Replace("  ·  ", " — "), failed > 0);
        }
        if (!stopped && chkCompare.Checked && lastCompressed != null && outputs.TryGetValue(lastCompressed, out var compressed))
            new CompareForm(lastCompressed, compressed).Show(this); // modeless: the list stays usable
    }

    // ---------- updates ----------

    /// <param name="manual">From the "Check for updates" link: report every outcome, and re-show a bar hidden with Later.</param>
    async Task CheckForUpdateAsync(bool manual = false)
    {
        if (update != null) // already offered (or downloading)
        {
            if (manual) ShowUpdateBar(true);
            return;
        }
        if (checkingUpdate) return;
        checkingUpdate = true;
        // Only a manual check changes the link; the automatic ones stay invisible unless they find something.
        if (manual)
        {
            lnkCheckUpdates.Enabled = false;
            lnkCheckUpdates.Text = "Checking…";
        }
        string result;
        try
        {
            update = await Updater.CheckAsync(default);
            result = update != null ? "Check for updates" : "Up to date";
            if (manual && update == null) Log($"Checked for updates: {Updater.Current} is the latest version.");
        }
        catch (Exception ex)
        {
            // Usually offline or GitHub unreachable: try again at the next check. Logged so a real fault isn't invisible.
            Log($"Couldn't check for updates ({ex.GetBaseException().Message}).");
            result = "Couldn't check — see Activity log";
        }
        finally { checkingUpdate = false; }
        if (manual)
        {
            lnkCheckUpdates.Text = result;
            lnkCheckUpdates.Enabled = true;
            if (update == null)
            {
                await Task.Delay(5000); // then put the link back
                if (lnkCheckUpdates.Text == result) lnkCheckUpdates.Text = "Check for updates";
            }
        }
        if (update == null) return;
        Log($"VideoSqueeze {update.Version} is available (you have {Updater.Current}).");
        lblUpdate.Text = $"VideoSqueeze {update.Version} is available — you have {Updater.Current}.";
        btnUpdate.Text = "Update";
        ShowUpdateBar(true);
    }

    void ShowUpdateBar(bool show)
    {
        updateBar.Visible = show;
        if (updateBar.Parent is TableLayoutPanel root)
            root.RowStyles[root.GetRow(updateBar)] = new RowStyle(SizeType.Absolute, show ? S(56) + updateBar.Margin.Bottom : 0);
    }

    async Task UpdateNowAsync()
    {
        if (update == null) return;
        if (updateInstaller == null)
        {
            btnUpdate.Enabled = btnUpdateLater.Enabled = false;
            try
            {
                updateInstaller = await Task.Run(() => Updater.DownloadAsync(update,
                    f => BeginInvoke(() => lblUpdate.Text = $"Downloading VideoSqueeze {update.Version}… {f:P0}"), default));
            }
            catch (Exception ex)
            {
                Log($"Update download failed: {ex.Message}");
                lblUpdate.Text = "The update couldn't be downloaded. Try again later.";
                btnUpdate.Text = "Retry";
                btnUpdate.Enabled = btnUpdateLater.Enabled = true;
                return;
            }
            btnUpdateLater.Enabled = true;
        }
        if (cts != null)
        {
            // Never interrupt an encode: offer the restart once the run is over (see SetRunning).
            lblUpdate.Text = $"VideoSqueeze {update.Version} is ready — you can restart to install it when this run finishes.";
            btnUpdate.Text = "Restart to update";
            btnUpdate.Enabled = false;
            return;
        }
        lblUpdate.Text = $"Installing VideoSqueeze {update.Version}…";
        Log(lblUpdate.Text);
        try { Updater.Install(updateInstaller); }
        catch (Exception ex)
        {
            Log($"Couldn't start the installer: {ex.Message}");
            lblUpdate.Text = "The installer couldn't start.";
            btnUpdate.Enabled = true;
            return;
        }
        Close();
    }

    // ---------- completion alert ----------

    [StructLayout(LayoutKind.Sequential)]
    struct FLASHWINFO { public uint cbSize; public IntPtr hwnd; public uint dwFlags; public uint uCount; public uint dwTimeout; }

    [DllImport("user32.dll")]
    static extern bool FlashWindowEx(ref FLASHWINFO info);

    /// <summary>Sound + Windows notification + taskbar flash (until the window is focused).</summary>
    void Alert(string title, string message, bool problem)
    {
        try
        {
            var wav = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Media",
                problem ? "Windows Critical Stop.wav" : "Windows Notify System Generic.wav");
            if (File.Exists(wav)) new System.Media.SoundPlayer(wav).Play();
            else (problem ? System.Media.SystemSounds.Hand : System.Media.SystemSounds.Asterisk).Play();
        }
        catch { }

        try
        {
            tray.Visible = true;
            tray.ShowBalloonTip(10000, title, message, problem ? ToolTipIcon.Warning : ToolTipIcon.Info);
        }
        catch { }

        if (!ContainsFocus)
        {
            var info = new FLASHWINFO
            {
                cbSize = (uint)Marshal.SizeOf<FLASHWINFO>(), hwnd = Handle,
                dwFlags = 0x3 | 0xC, // flash caption + taskbar, until the window comes to the foreground
            };
            FlashWindowEx(ref info);
        }
    }

    /// <summary>Paths passed by Explorer's "Squeeze with VideoSqueeze" (also from a second launch).</summary>
    public void OpenPaths(IEnumerable<string> paths)
    {
        var existing = paths.Where(p => File.Exists(p) || Directory.Exists(p)).ToList();
        if (existing.Count == 0) return;
        AddPaths(existing);
        BringToFrontNow();
    }

    void BringToFrontNow()
    {
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
        tray.Visible = false;
    }

    void RefreshEstimates()
    {
        if (loading || cbPreset.SelectedIndex < 0) return;
        string key = EstimateKey(ReadSettings());
        foreach (ListViewItem i in list.Items)
        {
            if (estimates.TryGetValue((string)i.Tag!, out var e) && e.Key == key) ShowEstimate(i, e.Estimate);
            else i.SubItems[3].Text = "";
        }
    }

    static void ShowEstimate(ListViewItem item, Estimate e) =>
        item.SubItems[3].Text = e.WorthIt ? $"−{e.SavedPercent:0}% (≈{Format.Bytes(e.PredictedSize)})" : "none";

    void SetRunning(bool running)
    {
        btnStart.Enabled = btnEstimate.Enabled = !running;
        btnCancel.Enabled = running;
        if (updateInstaller != null) btnUpdate.Enabled = !running;
        foreach (Control c in new Control[] { cbPreset, cbMode, cbCodec, cbSpeed, cbContainer, numVmaf, numQuality, txtOut, chkDiscard })
            c.Enabled = !running;
        if (!running) UpdateEnabled();
    }

    void Log(string msg)
    {
        txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\r\n");
    }

    // Alt-tabbing into a fullscreen game minimizes/shrinks the window (and may change the resolution). The ListView
    // then keeps a stale scroll offset and skips repainting, so rows vanish or start partway down. Re-anchor on return.
    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        RepairList();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (WindowState != FormWindowState.Minimized) RepairList();
    }

    void RepairList()
    {
        if (list.Items.Count == 0 || list.ClientSize.Height <= 0) return;
        int top = list.TopItem?.Index ?? 0;
        list.EnsureVisible(0);
        if (top > 0) list.EnsureVisible(Math.Min(top + list.ClientSize.Height / list.GetItemRect(0).Height - 1, list.Items.Count - 1));
        FitColumns();
        list.Invalidate();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (cts != null)
        {
            // Close only once the run has stopped, so its clean-up (deleting the half-written .part file) gets to run.
            e.Cancel = true;
            if (closeWhenStopped) return;
            if (MessageBox.Show(this, "Encoding is in progress. Stop and exit?", Text, MessageBoxButtons.YesNo) == DialogResult.No) return;
            closeWhenStopped = true;
            lblStatus.Text = "Stopping…";
            cts.Cancel();
            return;
        }
        SaveSettings(ReadSettings());
        tray.Visible = false;
        tray.Dispose();
        base.OnFormClosing(e);
    }

    // Prevents Windows from sleeping in the middle of a long encode.
    [DllImport("kernel32.dll")]
    static extern uint SetThreadExecutionState(uint flags);

    static void KeepAwake(bool on) => SetThreadExecutionState(on ? 0x80000001u : 0x80000000u);

    // ---------- small UI helpers ----------

    ModernButton Btn(string text, Action onClick)
    {
        var b = new ModernButton(text) { Margin = new Padding(S(6), 0, 0, 0) };
        b.Click += (_, _) => onClick();
        return b;
    }

    Label Caption(string text) => new()
    {
        Text = text, AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = Theme.SubtleText,
        Font = Theme.Body(8.25f, FontStyle.Bold), Margin = new Padding(0, 3, S(14), 3),
    };

    void AddRow(TableLayoutPanel grid, int row, string l1, Control c1, string l2, Control c2)
    {
        c1.Anchor = c2.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        if (c1 is NumericUpDown) c1.Anchor = AnchorStyles.Left;
        if (c2 is NumericUpDown) c2.Anchor = AnchorStyles.Left;
        grid.Controls.Add(Caption(l1), 0, row);
        grid.Controls.Add(c1, 1, row);
        var cap2 = Caption(l2);
        cap2.Margin = new Padding(S(20), 3, S(14), 3);
        grid.Controls.Add(cap2, 2, row);
        grid.Controls.Add(c2, 3, row);
    }
}
