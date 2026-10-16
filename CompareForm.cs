using System.Drawing.Drawing2D;
using System.Globalization;

namespace VideoSqueeze;

/// <summary>Spot-check window: the same frame from the original and the compressed file, with a wipe slider.</summary>
class CompareForm : Form
{
    readonly string original, compressed;
    readonly ComparePanel view = new() { Dock = DockStyle.Fill };
    readonly TrackBar timeline = new() { Dock = DockStyle.Fill, TickStyle = TickStyle.None, BackColor = Theme.Card };
    readonly Label lblTime = new() { AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = Theme.Text };
    readonly Label lblQuality = new() { AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(16, 3, 3, 3) };
    readonly System.Windows.Forms.Timer seekDelay = new() { Interval = 250 };
    readonly Random random = new();
    MediaInfo? origInfo, compInfo;
    CancellationTokenSource? loadCts;

    int S(int px) => (int)Math.Round(px * DeviceDpi / 96f);

    public CompareForm(string original, string compressed)
    {
        this.original = original;
        this.compressed = compressed;
        Text = "Compare — " + Path.GetFileName(original);
        Font = Theme.Body();
        BackColor = Theme.Window;
        ForeColor = Theme.Text;
        Size = new Size(S(1100), S(780));
        MinimumSize = new Size(S(700), S(500));
        StartPosition = FormStartPosition.CenterParent;
        KeyPreview = true;
        HandleCreated += (_, _) => Theme.StyleTitleBar(this);
        lblTime.Font = Theme.Body(10f, FontStyle.Bold);
        lblQuality.Font = Theme.Body(10f, FontStyle.Bold);

        var viewCard = new Card { Dock = DockStyle.Fill, Padding = new Padding(S(6)), Margin = new Padding(0, 0, 0, S(12)) };
        view.BackColor = Theme.Card;
        viewCard.Controls.Add(view);

        var controlsCard = new Card { Dock = DockStyle.Fill, Margin = Padding.Empty };
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 2, BackColor = Theme.Card };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 3; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, S(36)));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var info = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Anchor = AnchorStyles.Left, BackColor = Theme.Card, Margin = Padding.Empty };
        info.Controls.Add(lblTime);
        info.Controls.Add(lblQuality);
        grid.Controls.Add(info, 0, 0);
        var back = Btn("◀  10 s", () => Step(-10));
        var rnd = Btn("Random spot", RandomSpot);
        var fwd = Btn("10 s  ▶", () => Step(10));
        grid.Controls.Add(back, 1, 0);
        grid.Controls.Add(rnd, 2, 0);
        grid.Controls.Add(fwd, 3, 0);
        grid.Controls.Add(timeline, 0, 1);
        grid.SetColumnSpan(timeline, 4);
        controlsCard.Controls.Add(grid);

        var hint = new Label
        {
            Dock = DockStyle.Fill, ForeColor = Theme.SubtleText, TextAlign = ContentAlignment.MiddleLeft,
            Text = "Drag the slider to wipe between versions  ·  hold Space to see the original  ·  mouse wheel to zoom  ·  ← / → to step",
        };

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(S(16), S(12), S(16), S(12)) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, S(110)));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, S(28)));
        root.Controls.Add(viewCard, 0, 0);
        root.Controls.Add(controlsCard, 0, 1);
        root.Controls.Add(hint, 0, 2);
        Controls.Add(root);

        timeline.ValueChanged += (_, _) => { seekDelay.Stop(); seekDelay.Start(); lblTime.Text = Time(timeline.Value); };
        seekDelay.Tick += async (_, _) => { seekDelay.Stop(); await ShowAtAsync(timeline.Value); };
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Space) { view.ShowOriginalOnly = true; e.Handled = e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Left) { Step(-10); e.Handled = true; }
            else if (e.KeyCode == Keys.Right) { Step(10); e.Handled = true; }
        };
        KeyUp += (_, e) => { if (e.KeyCode == Keys.Space) view.ShowOriginalOnly = false; };
        Shown += async (_, _) => await OpenAsync();
        FormClosed += (_, _) => { loadCts?.Cancel(); view.SetImages(null, null); };
    }

    async Task OpenAsync()
    {
        try
        {
            lblQuality.Text = "Loading…";
            origInfo = await MediaInfo.ProbeAsync(original, CancellationToken.None);
            compInfo = await MediaInfo.ProbeAsync(compressed, CancellationToken.None);
            timeline.Maximum = Math.Max(1, (int)Math.Min(origInfo.Duration, compInfo.Duration) - 1);
            timeline.LargeChange = Math.Max(1, timeline.Maximum / 20);
            RandomSpot();
        }
        catch (Exception ex)
        {
            lblQuality.ForeColor = Theme.Error;
            lblQuality.Text = "Couldn't open: " + ex.Message;
        }
    }

    void Step(int seconds) => timeline.Value = Math.Clamp(timeline.Value + seconds, timeline.Minimum, timeline.Maximum);

    void RandomSpot() => timeline.Value = Math.Clamp((int)(timeline.Maximum * (0.03 + random.NextDouble() * 0.94)), 0, timeline.Maximum);

    async Task ShowAtAsync(double t)
    {
        if (origInfo == null || compInfo == null) return;
        loadCts?.Cancel();
        loadCts = new CancellationTokenSource();
        var ct = loadCts.Token;
        lblQuality.ForeColor = Theme.SubtleText;
        lblQuality.Text = "Loading frame…";
        try
        {
            var a = GrabAsync(original, origInfo.VideoIndex, t, ct);
            var b = GrabAsync(compressed, compInfo.VideoIndex, t, ct);
            await Task.WhenAll(a, b);
            view.SetImages(a.Result, b.Result);

            lblQuality.Text = "Measuring quality…";
            double vmaf = await Task.Run(() => Squeezer.VmafAsync(compressed, original, origInfo.Height > 1440, ct, t, 1), ct);
            (lblQuality.ForeColor, string verdict) = vmaf switch
            {
                >= 93 => (Theme.Success, "looks identical"),
                >= 85 => (Theme.Warning, "minor differences possible"),
                _ => (Theme.Error, "visible differences likely"),
            };
            lblQuality.Text = $"Quality here: VMAF {vmaf:0.0} — {verdict}";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (ct.IsCancellationRequested) return;
            lblQuality.ForeColor = Theme.Error;
            lblQuality.Text = "Couldn't read this spot: " + ex.Message.Split('\n')[0];
        }
    }

    /// <summary>Decodes one frame at <paramref name="t"/>, corrected to square pixels (e.g. anamorphic DVDs).</summary>
    static async Task<Bitmap> GrabAsync(string file, int streamIndex, double t, CancellationToken ct)
    {
        var bytes = await Tools.FfmpegBytesAsync(new[]
        {
            "-hide_banner", "-nostdin", "-loglevel", "error",
            "-ss", t.ToString("0.###", CultureInfo.InvariantCulture), "-i", file, "-map", $"0:{streamIndex}", "-frames:v", "1",
            "-vf", "scale=trunc(iw*sar/2)*2:ih,format=bgr24", "-f", "image2pipe", "-c:v", "bmp", "-",
        }, ct);
        using var ms = new MemoryStream(bytes);
        return new Bitmap(ms);
    }

    static string Time(double s) => TimeSpan.FromSeconds(s).ToString(@"h\:mm\:ss");

    ModernButton Btn(string text, Action onClick)
    {
        var b = new ModernButton(text) { Margin = new Padding(S(8), 0, 0, 0), Anchor = AnchorStyles.Right };
        b.Click += (_, _) => onClick();
        return b;
    }
}

/// <summary>Draws the original left of a draggable divider and the compressed version right of it; wheel zooms.</summary>
class ComparePanel : Control
{
    Bitmap? orig, comp;
    float split = 0.5f;          // divider position, 0..1 of the control width
    float zoom = 1f;
    PointF focus = new(0.5f, 0.5f); // image point (0..1) shown at the control centre
    bool dragging, showOriginalOnly;

    public bool ShowOriginalOnly { get => showOriginalOnly; set { showOriginalOnly = value; Invalidate(); } }

    public ComparePanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.SizeWE;
    }

    public void SetImages(Bitmap? original, Bitmap? compressed)
    {
        orig?.Dispose();
        comp?.Dispose();
        orig = original;
        comp = compressed;
        Invalidate();
    }

    RectangleF ImageRect()
    {
        if (orig == null) return RectangleF.Empty;
        float fit = Math.Min((float)Width / orig.Width, (float)Height / orig.Height) * zoom;
        float w = orig.Width * fit, h = orig.Height * fit;
        return new RectangleF(Width / 2f - focus.X * w, Height / 2f - focus.Y * h, w, h);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        if (orig == null || comp == null)
        {
            TextRenderer.DrawText(g, "Loading…", Font, ClientRectangle, Theme.SubtleText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }
        // Show real pixels when zoomed in, smooth scaling otherwise.
        g.InterpolationMode = zoom >= 2 ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        var r = ImageRect();
        float splitX = showOriginalOnly ? Width : Width * split;

        g.DrawImage(comp, r);
        g.SetClip(new RectangleF(0, 0, splitX, Height));
        g.DrawImage(orig, r);
        g.ResetClip();

        g.SmoothingMode = SmoothingMode.AntiAlias;
        if (!showOriginalOnly)
        {
            using var pen = new Pen(Color.White, 2);
            g.DrawLine(pen, splitX, 0, splitX, Height);
            var knob = new RectangleF(splitX - 14, Height / 2f - 14, 28, 28);
            using (var b = new SolidBrush(Theme.Accent)) g.FillEllipse(b, knob);
            g.DrawEllipse(pen, knob);
            TextRenderer.DrawText(g, "◀▶", Theme.Body(7f, FontStyle.Bold), Rectangle.Round(knob), Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        DrawPill(g, "ORIGINAL", 12, false);
        if (!showOriginalOnly) DrawPill(g, "COMPRESSED", Width - 12, true);
        if (zoom > 1.01f) DrawPill(g, $"{zoom:0.#}×", Width / 2, null, bottom: true);
    }

    void DrawPill(Graphics g, string text, int x, bool? alignRight, bool bottom = false)
    {
        using var f = Theme.Body(8.5f, FontStyle.Bold);
        var size = TextRenderer.MeasureText(text, f);
        float w = size.Width + 16, h = size.Height + 8;
        float left = alignRight == true ? x - w : alignRight == false ? x : x - w / 2;
        var rect = new RectangleF(left, bottom ? Height - h - 12 : 12, w, h);
        using (var path = Theme.Rounded(rect, h / 2))
        using (var b = new SolidBrush(Color.FromArgb(170, 0, 0, 0)))
            g.FillPath(b, path);
        TextRenderer.DrawText(g, text, f, Rectangle.Round(rect), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        dragging = e.Button == MouseButtons.Left;
        MoveSplit(e.X);
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (dragging) MoveSplit(e.X);
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e) { dragging = false; base.OnMouseUp(e); }

    void MoveSplit(int x)
    {
        if (!dragging) return;
        split = Math.Clamp(x / (float)Math.Max(1, Width), 0f, 1f);
        Invalidate();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (orig == null) return;
        // Keep the image point under the cursor fixed while zooming.
        var before = ImageRect();
        var under = new PointF((e.X - before.X) / before.Width, (e.Y - before.Y) / before.Height);
        zoom = Math.Clamp(zoom * (e.Delta > 0 ? 1.25f : 0.8f), 1f, 8f);
        float fit = Math.Min((float)Width / orig.Width, (float)Height / orig.Height) * zoom;
        float w = orig.Width * fit, h = orig.Height * fit;
        focus = zoom <= 1.001f
            ? new PointF(0.5f, 0.5f)
            : new PointF(under.X - (e.X - Width / 2f) / w, under.Y - (e.Y - Height / 2f) / h);
        Invalidate();
        base.OnMouseWheel(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) SetImages(null, null);
        base.Dispose(disposing);
    }
}
