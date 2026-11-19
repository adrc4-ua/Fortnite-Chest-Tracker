using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace FCT.UI;

/// <summary>The building blocks of the settings window: cards, their titles, switch rows, read-only fields and labels.</summary>
[SupportedOSPlatform("windows")]
internal static class SettingsParts
{
    public static Card Card(Control parent, int x, int y, int width, int height, int radius)
    {
        var card = new Card { Location = new Point(x, y), Size = new Size(width, height), Radius = radius, BackColor = Theme.Card };
        parent.Controls.Add(card);
        return card;
    }

    /// <summary>A card title like the page's (gold diamond, bold text); <paramref name="reserve"/> leaves room on the right.</summary>
    public static void Title(Card card, string text, int reserve = 0) =>
        card.Controls.Add(new SectionTitle { Text = text, Font = Theme.Text(8.5f, FontStyle.Bold), Location = new Point(11, 6), Size = new Size(card.Width - 22 - reserve, 20) });

    /// <summary>
    /// A title (and an optional grey line) on the left and a switch on the right, centred on what is beside it.
    /// Clicking the text flips the switch too.
    /// </summary>
    public static ToggleSwitch SwitchRow(Card card, string text, string? hint, int top, bool on)
    {
        var toggle = new ToggleSwitch { Location = new Point(card.Width - 13 - 40, hint != null ? top + 5 : top - 2), Checked = on };
        var textWidth = card.Width - 26 - 48;
        Flips(Text(card, text, 9.5f, FontStyle.Bold, Theme.Ink, new Point(13, top), new Size(textWidth, 18)), toggle);
        if (hint != null)
        {
            Flips(Text(card, hint, 7.5f, FontStyle.Regular, Theme.Muted, new Point(13, top + 18), new Size(textWidth, 14)), toggle);
        }
        card.Controls.Add(toggle);
        return toggle;
    }

    private static void Flips(Label label, ToggleSwitch toggle)
    {
        label.Cursor = Cursors.Hand;
        label.Click += (_, _) => toggle.Checked = !toggle.Checked;
    }

    /// <summary>A borderless read-only text box on a rounded background.</summary>
    public static ThemedTextBox Field(Card card, Point at, int width, string text)
    {
        var back = new Card { Location = at, Size = new Size(width, 32), Radius = 10, BackColor = Theme.Bg2, Edge = Theme.Line };
        var box = new ThemedTextBox { BorderStyle = BorderStyle.None, BackColor = Theme.Bg2, ForeColor = Theme.Ink, Font = Theme.Text(9f), ReadOnly = true, Text = text, Width = width - 20 };
        box.Location = new Point(10, (back.Height - box.Height) / 2);
        box.AttachToCard(back);
        back.Controls.Add(box);
        card.Controls.Add(back);
        return box;
    }

    /// <summary>A see-through label that ends in "…" when too long (and that <see cref="ShrinkToFit"/> may shrink).</summary>
    public static Label Text(Control parent, string text, float points, FontStyle style, Color color, Point at, Size size, ContentAlignment align = ContentAlignment.TopLeft)
    {
        var label = new Label
        {
            Text = text, Font = Theme.Text(points, style), ForeColor = color, BackColor = Color.Transparent,
            Location = at, Size = size, TextAlign = align, AutoEllipsis = true, UseMnemonic = false,
        };
        parent.Controls.Add(label);
        return label;
    }

    /// <summary>
    /// Text that does not fit its place (long languages) gets a slightly smaller font, down to 6.5 pt, instead of being
    /// cut. Only one-line labels made by <see cref="Text"/> and buttons.
    /// </summary>
    public static void ShrinkToFit(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            ShrinkToFit(control);
            if (control is not (Label { AutoEllipsis: true } or PillButton) || string.IsNullOrEmpty(control.Text))
            {
                continue;
            }
            var font = control.Font;
            if (control is Label && control.Height > font.Height * 1.8)
            {
                continue; // room for several lines: it wraps
            }
            var room = control.Width - (control is PillButton ? 12 : 2);
            while (font.Size > 6.5f && TextRenderer.MeasureText(control.Text, font).Width > room)
            {
                font = new Font(font.FontFamily, font.Size - 0.25f, font.Style);
            }
            if (!ReferenceEquals(font, control.Font))
            {
                control.Font = font;
            }
        }
    }

    /// <summary>A rounded button of the settings window.</summary>
    public static PillButton Button(string text, Point at, Size size, int radius, float points = 8.5f) =>
        new() { Text = text, Location = at, Size = size, Radius = radius, Font = Theme.Text(points, FontStyle.Bold) };
}
