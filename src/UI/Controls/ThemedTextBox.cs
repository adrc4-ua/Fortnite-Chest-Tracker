using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Windows.Forms;

namespace FCT.UI;

/// <summary>
/// What kind of numbers a text box accepts, or whether it accepts any text.
/// </summary>
public enum NumericMode
{
    /// <summary>Any character is allowed (Asian characters, Unicode glyphs, special characters).</summary>
    None,

    /// <summary>Only ASCII digits 0-9 are allowed (e.g. chest counts, match counts).</summary>
    Integer,

    /// <summary>ASCII digits 0-9 and at most one decimal separator (, or .) are allowed (e.g. averages).</summary>
    Decimal,
}

/// <summary>
/// A text box styled for FCT with:
/// <list type="bullet">
/// <item>Strict numeric filtering when <see cref="Numeric"/> is set (rejects non-numbers on typing, pasting, and text changes);</item>
/// <item>Unrestricted Unicode and special character support when <see cref="Numeric"/> is <see cref="NumericMode.None"/>;</item>
/// <item>Full copy, cut, paste and select-all support across keyboard shortcuts and a dark context menu;</item>
/// <item>A cue banner (placeholder text) helper;</item>
/// <item>Click and context-menu forwarding from the surrounding <see cref="Card"/>.</item>
/// </list>
/// </summary>
[SupportedOSPlatform("windows")]
public class ThemedTextBox : TextBox
{
    private const int WmPaste = 0x0302;
    private const int EmSetCueBanner = 0x1501;

    private string? _cueBanner;
    private bool _sanitizing;

    public ThemedTextBox()
    {
        ContextMenuStrip = BuildContextMenu();
    }

    /// <summary>Numeric input restriction: none (all characters), integer only, or decimal number.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public NumericMode Numeric { get; set; } = NumericMode.None;

    /// <summary>Placeholder text displayed when the box is empty and unfocused.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? CueBanner
    {
        get => _cueBanner;
        set
        {
            _cueBanner = value;
            ApplyCueBanner();
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyCueBanner();
    }

    private void ApplyCueBanner()
    {
        if (IsHandleCreated && !string.IsNullOrEmpty(_cueBanner))
        {
            SendMessage(Handle, EmSetCueBanner, (IntPtr)1, _cueBanner);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, string lParam);

    /// <summary>
    /// Forwards left clicks on the enclosing card to focus this box, and right clicks to show the context menu.
    /// </summary>
    public void AttachToCard(Card card)
    {
        card.Cursor = Cursors.IBeam;
        card.MouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                Focus();
            }
        };
        card.MouseUp += (_, e) =>
        {
            if (e.Button == MouseButtons.Right && ContextMenuStrip != null)
            {
                Focus();
                ContextMenuStrip.Show(card, e.Location);
            }
        };
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.A)
        {
            SelectAll();
            e.SuppressKeyPress = true;
            e.Handled = true;
            return;
        }

        if (e.Control && e.KeyCode == Keys.V)
        {
            PasteFromClipboard();
            e.SuppressKeyPress = true;
            e.Handled = true;
            return;
        }

        if (e.Shift && e.KeyCode == Keys.Insert)
        {
            PasteFromClipboard();
            e.SuppressKeyPress = true;
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        if (char.IsControl(e.KeyChar))
        {
            base.OnKeyPress(e);
            return;
        }

        switch (Numeric)
        {
            case NumericMode.Integer:
                if (e.KeyChar is < '0' or > '9')
                {
                    e.Handled = true;
                    return;
                }
                break;

            case NumericMode.Decimal:
                if (e.KeyChar is >= '0' and <= '9')
                {
                    break;
                }
                if (e.KeyChar is '.' or ',')
                {
                    var remaining = SelectionLength > 0 ? Text.Remove(SelectionStart, SelectionLength) : Text;
                    if (!remaining.Contains('.') && !remaining.Contains(','))
                    {
                        break;
                    }
                }
                e.Handled = true;
                return;

            case NumericMode.None:
            default:
                // Any character is permitted (Asian characters, Unicode glyphs, special symbols).
                break;
        }

        base.OnKeyPress(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmPaste)
        {
            PasteFromClipboard();
            return;
        }
        base.WndProc(ref m);
    }

    /// <summary>
    /// Pastes text from the clipboard, sanitizing according to <see cref="Numeric"/> and respecting <see cref="TextBox.MaxLength"/>.
    /// </summary>
    public void PasteFromClipboard()
    {
        if (ReadOnly || !Enabled)
        {
            return;
        }
        string? text;
        try
        {
            if (!Clipboard.ContainsText())
            {
                return;
            }
            text = Clipboard.GetText();
        }
        catch
        {
            return; // Clipboard may be busy/locked
        }

        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        InsertSanitizedText(text);
    }

    private void InsertSanitizedText(string raw)
    {
        switch (Numeric)
        {
            case NumericMode.Integer:
                InsertIntegerText(raw);
                break;

            case NumericMode.Decimal:
                InsertDecimalText(raw);
                break;

            case NumericMode.None:
            default:
                var clean = Multiline ? raw : raw.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
                InsertText(clean);
                break;
        }
    }

    private void InsertIntegerText(string raw)
    {
        var sb = new StringBuilder(raw.Length);
        foreach (var ch in raw)
        {
            if (ch is >= '0' and <= '9')
            {
                sb.Append(ch);
            }
        }
        if (sb.Length > 0)
        {
            InsertText(sb.ToString());
        }
    }

    private void InsertDecimalText(string raw)
    {
        var remaining = SelectionLength > 0 ? Text.Remove(SelectionStart, SelectionLength) : Text;
        var hasSep = remaining.Contains('.') || remaining.Contains(',');
        var sb = new StringBuilder(raw.Length);

        foreach (var ch in raw)
        {
            if (ch is >= '0' and <= '9')
            {
                sb.Append(ch);
            }
            else if (ch is '.' or ',' && !hasSep)
            {
                sb.Append(ch);
                hasSep = true;
            }
        }

        if (sb.Length > 0)
        {
            InsertText(sb.ToString());
        }
    }

    private void InsertText(string insert)
    {
        if (MaxLength > 0)
        {
            var remainingRoom = MaxLength - (Text.Length - SelectionLength);
            if (remainingRoom <= 0)
            {
                return;
            }
            if (insert.Length > remainingRoom)
            {
                insert = insert[..remainingRoom];
                if (insert.Length > 0 && char.IsHighSurrogate(insert[^1]))
                {
                    insert = insert[..^1];
                }
            }
        }
        SelectedText = insert;
    }

    protected override void OnTextChanged(EventArgs e)
    {
        if (_sanitizing)
        {
            base.OnTextChanged(e);
            return;
        }

        if (Numeric == NumericMode.Integer)
        {
            SanitizeInteger();
        }
        else if (Numeric == NumericMode.Decimal)
        {
            SanitizeDecimal();
        }

        base.OnTextChanged(e);
    }

    private void SanitizeInteger()
    {
        var text = Text;
        var sb = new StringBuilder(text.Length);
        var changed = false;
        foreach (var c in text)
        {
            if (c is >= '0' and <= '9')
            {
                sb.Append(c);
            }
            else
            {
                changed = true;
            }
        }
        if (MaxLength > 0 && sb.Length > MaxLength)
        {
            sb.Length = MaxLength;
            changed = true;
        }
        if (changed)
        {
            _sanitizing = true;
            var sel = Math.Min(SelectionStart, sb.Length);
            Text = sb.ToString();
            SelectionStart = sel;
            _sanitizing = false;
        }
    }

    private void SanitizeDecimal()
    {
        var text = Text;
        var sb = new StringBuilder(text.Length);
        var changed = false;
        var hasSep = false;
        foreach (var c in text)
        {
            if (c is >= '0' and <= '9')
            {
                sb.Append(c);
            }
            else if (c is '.' or ',' && !hasSep)
            {
                sb.Append(c);
                hasSep = true;
            }
            else
            {
                changed = true;
            }
        }
        if (MaxLength > 0 && sb.Length > MaxLength)
        {
            sb.Length = MaxLength;
            changed = true;
        }
        if (changed)
        {
            _sanitizing = true;
            var sel = Math.Min(SelectionStart, sb.Length);
            Text = sb.ToString();
            SelectionStart = sel;
            _sanitizing = false;
        }
    }

    private ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip
        {
            Renderer = new DarkMenuRenderer(),
            ForeColor = Theme.Ink,
            Font = Theme.Text(9f),
            ShowImageMargin = false,
        };

        var undo = new ToolStripMenuItem(L.T("Deshacer", "Undo"), null, (_, _) => Undo()) { ShortcutKeyDisplayString = "Ctrl+Z" };
        var cut = new ToolStripMenuItem(L.T("Cortar", "Cut"), null, (_, _) => Cut()) { ShortcutKeyDisplayString = "Ctrl+X" };
        var copy = new ToolStripMenuItem(L.T("Copiar", "Copy"), null, (_, _) => Copy()) { ShortcutKeyDisplayString = "Ctrl+C" };
        var paste = new ToolStripMenuItem(L.T("Pegar", "Paste"), null, (_, _) => PasteFromClipboard()) { ShortcutKeyDisplayString = "Ctrl+V" };
        var delete = new ToolStripMenuItem(L.T("Eliminar", "Delete"), null, (_, _) => { if (!ReadOnly) SelectedText = ""; }) { ShortcutKeyDisplayString = "Del" };
        var selectAll = new ToolStripMenuItem(L.T("Seleccionar todo", "Select All"), null, (_, _) => SelectAll()) { ShortcutKeyDisplayString = "Ctrl+A" };

        menu.Items.AddRange(new ToolStripItem[]
        {
            undo,
            new ToolStripSeparator(),
            cut,
            copy,
            paste,
            delete,
            new ToolStripSeparator(),
            selectAll,
        });

        menu.Opening += (_, _) =>
        {
            undo.Text = L.T("Deshacer", "Undo");
            cut.Text = L.T("Cortar", "Cut");
            copy.Text = L.T("Copiar", "Copy");
            paste.Text = L.T("Pegar", "Paste");
            delete.Text = L.T("Eliminar", "Delete");
            selectAll.Text = L.T("Seleccionar todo", "Select All");

            var hasSelection = SelectionLength > 0;
            var canWrite = !ReadOnly && Enabled;
            var hasText = Text.Length > 0;
            var hasClipboard = false;
            try
            {
                hasClipboard = Clipboard.ContainsText();
            }
            catch
            {
            }

            undo.Enabled = canWrite && CanUndo;
            cut.Enabled = canWrite && hasSelection;
            copy.Enabled = hasSelection;
            paste.Enabled = canWrite && hasClipboard;
            delete.Enabled = canWrite && hasSelection;
            selectAll.Enabled = hasText && SelectionLength < Text.Length;
        };

        return menu;
    }
}
