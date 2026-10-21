using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace VideoSqueeze;

/// <summary>Colours and fonts; follows the Windows light/dark app setting.</summary>
static class Theme
{
    public static readonly bool Dark = IsWindowsDark();

    public static readonly Color Window = Dark ? Color.FromArgb(24, 25, 31) : Color.FromArgb(243, 244, 248);
    public static readonly Color Card = Dark ? Color.FromArgb(34, 35, 43) : Color.White;
    public static readonly Color CardAlt = Dark ? Color.FromArgb(28, 29, 36) : Color.FromArgb(248, 249, 252);
    public static readonly Color Border = Dark ? Color.FromArgb(52, 54, 66) : Color.FromArgb(225, 227, 234);
    public static readonly Color Text = Dark ? Color.FromArgb(234, 235, 242) : Color.FromArgb(28, 30, 38);
    public static readonly Color SubtleText = Dark ? Color.FromArgb(150, 154, 170) : Color.FromArgb(105, 110, 125);
    public static readonly Color Accent = Color.FromArgb(108, 92, 255);
    public static readonly Color AccentHover = Color.FromArgb(128, 114, 255);
    public static readonly Color AccentPressed = Color.FromArgb(88, 72, 230);
    public static readonly Color Secondary = Dark ? Color.FromArgb(46, 48, 59) : Color.FromArgb(236, 237, 243);
    public static readonly Color SecondaryHover = Dark ? Color.FromArgb(58, 60, 73) : Color.FromArgb(225, 227, 236);
    public static readonly Color Selection = Dark ? Color.FromArgb(52, 47, 92) : Color.FromArgb(233, 230, 255);
    public static readonly Color Success = Dark ? Color.FromArgb(87, 204, 140) : Color.FromArgb(22, 150, 85);
    public static readonly Color Warning = Dark ? Color.FromArgb(240, 180, 80) : Color.FromArgb(190, 120, 0);
    public static readonly Color Error = Dark ? Color.FromArgb(255, 110, 110) : Color.FromArgb(205, 45, 45);

    static readonly string TextFamily = Has("Segoe UI Variable Text") ? "Segoe UI Variable Text" : "Segoe UI";
    static readonly string DisplayFamily = Has("Segoe UI Variable Display") ? "Segoe UI Variable Display" : "Segoe UI";

    public static Font Body(float size = 9.75f, FontStyle style = FontStyle.Regular) => new(TextFamily, size, style);
    public static Font Display(float size, FontStyle style = FontStyle.Bold) => new(DisplayFamily, size, style);

    static bool Has(string family) => FontFamily.Families.Any(f => f.Name == family);

    static bool IsWindowsDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch { return false; }
    }

    public static GraphicsPath Rounded(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d <= 0) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    // ---- native styling (title bar, scrollbars) ----

    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr hwnd, string? app, string? idList);

    /// <summary>Dark title bar when in dark mode, and a caption colour matching the window (Windows 11).</summary>
    public static void StyleTitleBar(Form f)
    {
        try
        {
            int dark = Dark ? 1 : 0;
            DwmSetWindowAttribute(f.Handle, 20, ref dark, 4);
            int caption = ColorTranslator.ToWin32(Window);
            DwmSetWindowAttribute(f.Handle, 35, ref caption, 4);
            int text = ColorTranslator.ToWin32(Text);
            DwmSetWindowAttribute(f.Handle, 36, ref text, 4);
        }
        catch { /* older Windows */ }
    }

    /// <summary>Dark scrollbars etc. for native controls.</summary>
    public static void StyleNative(Control c)
    {
        if (!Dark) return;
        // Combo boxes use the "common file dialog" dark theme; everything else the Explorer one.
        string theme = c is ComboBox ? "DarkMode_CFD" : "DarkMode_Explorer";
        void Apply() { try { SetWindowTheme(c.Handle, theme, null); } catch { } }
        if (c.IsHandleCreated) Apply(); else c.HandleCreated += (_, _) => Apply();
    }
}

/// <summary>A rounded "card" panel.</summary>
class Card : Panel
{
    public Card()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Theme.Card;
        ForeColor = Theme.Text;
        Padding = new Padding(16, 12, 16, 12);
        Margin = new Padding(0, 0, 0, 12);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? Theme.Window);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Theme.Rounded(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), 10);
        using var fill = new SolidBrush(BackColor);
        using var pen = new Pen(Theme.Border);
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(pen, path);
    }
}

/// <summary>Flat rounded button with hover/pressed states.</summary>
class ModernButton : Button
{
    bool hover, pressed;
    public bool Primary { get; set; }

    public ModernButton(string text, bool primary = false)
    {
        Text = text;
        Primary = primary;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Font = Theme.Body(9.75f, primary ? FontStyle.Bold : FontStyle.Regular);
        Height = 36;
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        Width = TextRenderer.MeasureText(text, Font).Width + 36;
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Window);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Color bg = Primary
            ? (!Enabled ? Color.FromArgb(110, Theme.Accent) : pressed ? Theme.AccentPressed : hover ? Theme.AccentHover : Theme.Accent)
            : (pressed || hover) && Enabled ? Theme.SecondaryHover : Theme.Secondary;
        Color fg = Primary ? Color.White : Enabled ? Theme.Text : Theme.SubtleText;
        using (var path = Theme.Rounded(new RectangleF(0, 0, Width - 1, Height - 1), 8))
        using (var brush = new SolidBrush(bg))
            g.FillPath(brush, path);
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, fg,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }
}

/// <summary>Thin rounded progress bar (0..1000).</summary>
class SlimProgress : Control
{
    int value;
    public int Value { get => value; set { this.value = Math.Clamp(value, 0, 1000); Invalidate(); } }

    public SlimProgress()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Height = 6;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Card);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var track = new RectangleF(0, 0, Width - 1, Height - 1);
        using (var p = Theme.Rounded(track, Height / 2f)) using (var b = new SolidBrush(Theme.Secondary)) g.FillPath(b, p);
        if (value <= 0) return;
        var fill = new RectangleF(0, 0, Math.Max(Height, (Width - 1) * value / 1000f), Height - 1);
        using (var p = Theme.Rounded(fill, Height / 2f))
        using (var b = new LinearGradientBrush(new RectangleF(0, 0, Width, Height), Theme.Accent, Color.FromArgb(32, 178, 255), 0f))
            g.FillPath(b, p);
    }
}

/// <summary>Drop-down list with roomier owner-drawn items that respects the theme.</summary>
class ModernCombo : ComboBox
{
    public ModernCombo(params string[] items)
    {
        DropDownStyle = ComboBoxStyle.DropDownList;
        DrawMode = DrawMode.OwnerDrawFixed;
        FlatStyle = FlatStyle.Flat;
        ItemHeight = 26;
        BackColor = Theme.CardAlt;
        ForeColor = Theme.Text;
        Font = Theme.Body();
        Items.AddRange(items);
        Theme.StyleNative(this);
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        bool selected = (e.State & DrawItemState.Selected) != 0 && (e.State & DrawItemState.ComboBoxEdit) == 0;
        using (var b = new SolidBrush(selected ? Theme.Accent : BackColor)) e.Graphics.FillRectangle(b, e.Bounds);
        var r = e.Bounds;
        r.X += 6;
        TextRenderer.DrawText(e.Graphics, Items[e.Index]?.ToString(), Font, r, selected ? Color.White : (Enabled ? ForeColor : Theme.SubtleText),
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
    }
}

/// <summary>Check box that keeps theme colours when disabled (Windows draws disabled text near-black on dark backgrounds).</summary>
class ModernCheck : CheckBox
{
    public ModernCheck()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        ForeColor = Theme.Text;
    }

    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Card);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        int size = (int)Math.Round(14 * DeviceDpi / 96f);
        var box = new RectangleF(0.5f, (Height - size) / 2f, size, size);
        Color accent = Enabled ? Theme.Accent : Theme.Border;
        using (var p = Theme.Rounded(box, size / 4f))
        {
            if (Checked) { using var b = new SolidBrush(accent); g.FillPath(b, p); }
            else { using var b = new SolidBrush(Theme.CardAlt); g.FillPath(b, p); using var pen = new Pen(Enabled ? Theme.SubtleText : Theme.Border); g.DrawPath(pen, p); }
        }
        if (Checked)
        {
            using var pen = new Pen(Enabled ? Color.White : Theme.SubtleText, size / 7f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            g.DrawLines(pen, new[]
            {
                new PointF(box.Left + size * 0.24f, box.Top + size * 0.52f),
                new PointF(box.Left + size * 0.43f, box.Top + size * 0.70f),
                new PointF(box.Left + size * 0.76f, box.Top + size * 0.32f),
            });
        }
        var text = new Rectangle((int)box.Right + size / 2, 0, Width - (int)box.Right - size / 2, Height);
        TextRenderer.DrawText(g, Text, Font, text, Enabled ? ForeColor : Theme.SubtleText,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g, text);
    }
}
