using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace FCT.UI;

/// <summary>
/// Chests per day of the week, drawn like the page's chest curve: a smooth gold line over a fading fill, guide lines
/// with the scale on the left, the average of the days played as a dashed line, a dot per day (today in white, with
/// its figure above) and the day names below (today's in gold). Days still to come get no dot.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DailyChart : Control
{
    private const TextFormatFlags Plain = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
    private IReadOnlyList<DayChests> _days = Array.Empty<DayChests>();

    public DailyChart()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Card;
    }

    public void SetDays(IReadOnlyList<DayChests> days)
    {
        _days = days;
        Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs e) => Backdrop.Fill(this, e.Graphics);

    protected override void OnPaint(PaintEventArgs e)
    {
        if (_days.Count >= 2)
        {
            using var painter = new Painter(this, e.Graphics);
            painter.Paint();
        }
    }

    /// <summary>A round top for the scale: 1, 2, 2.5 or 5 times a power of ten, at least the value (10 when empty).</summary>
    private static int RoundTop(int value)
    {
        if (value <= 0)
        {
            return 10;
        }
        var power = Math.Pow(10, Math.Floor(Math.Log10(value)));
        foreach (var step in new[] { 1, 2, 2.5, 5, 10 })
        {
            if (step * power >= value)
            {
                return (int)Math.Ceiling(step * power);
            }
        }
        return value;
    }

    /// <summary>
    /// One paint of the chart. Text goes through TextRenderer (GDI ClearType, like the window's labels): GDI+ made the
    /// small figures blurry. The fonts follow the control's, which scales with the window.
    /// </summary>
    private sealed class Painter : IDisposable
    {
        private readonly DailyChart _chart;
        private readonly Graphics _g;
        private readonly IReadOnlyList<DayChests> _days;
        private readonly Font _axisFont, _dayFont, _todayFont, _figureFont;
        private readonly float _dayHeight, _dotRadius;
        private readonly int _top;
        private RectangleF _plot;

        public Painter(DailyChart chart, Graphics g)
        {
            (_chart, _g, _days) = (chart, g, chart._days);
            var font = chart.Font;
            _axisFont = new Font(font.FontFamily, font.Size * 0.88f, FontStyle.Regular);
            _dayFont = new Font(font.FontFamily, font.Size, FontStyle.Regular);
            _todayFont = new Font(font.FontFamily, font.Size, FontStyle.Bold);
            _figureFont = new Font(font.FontFamily, font.Size * 0.88f, FontStyle.Bold);
            _dayHeight = _dayFont.GetHeight(g.DpiY);
            _dotRadius = Math.Max(3f, _dayHeight * 0.28f);
            _top = RoundTop(_days.Max(d => d.Chests));
        }

        public void Paint()
        {
            _g.SmoothingMode = SmoothingMode.AntiAlias;
            // the scale's figures on the left and the days below; on top, room for the figure of a dot at the very top
            var axisWidth = Measure(L.N(_top), _axisFont).Width + 4f;
            var plotTop = Measure("0", _figureFont).Height + _dotRadius + 3f;
            _plot = new RectangleF(axisWidth + 6f, plotTop, _chart.Width - axisWidth - 6f - 8f, _chart.Height - plotTop - _dayHeight - 7f);
            if (_plot.Width <= 10 || _plot.Height <= 10)
            {
                return;
            }
            Grid(axisWidth);
            Curve();
            Average();
            Dots();
            DayNames();
        }

        private float X(int day) => _plot.Left + day * _plot.Width / (_days.Count - 1);
        private float Y(double chests) => _plot.Bottom - (float)(chests / _top) * _plot.Height;
        private static bool IsToday(DayChests day) => day.Day == DateTime.Now.Date;

        private Size Measure(string text, Font font) => TextRenderer.MeasureText(_g, text, font, Size.Empty, Plain);

        private void Write(string text, Font font, Color color, float x, float y) =>
            TextRenderer.DrawText(_g, text, font, new Point((int)Math.Round(x), (int)Math.Round(y)), color, Plain);

        /// <summary>Guide lines at 0, half and the top, each with its figure on the left.</summary>
        private void Grid(float axisWidth)
        {
            using var guide = new Pen(Theme.Line, 1f);
            foreach (var level in new[] { 0, _top / 2, _top })
            {
                var y = (float)Math.Round(Y(level)) + 0.5f;
                _g.DrawLine(guide, _plot.Left, y, _plot.Right, y);
                var size = Measure(L.N(level), _axisFont);
                Write(L.N(level), _axisFont, Theme.Muted, axisWidth - size.Width, y - size.Height / 2f);
            }
        }

        /// <summary>A smooth curve (Catmull-Rom as Béziers, never under zero) through the days so far, with its fill and glow.</summary>
        private void Curve()
        {
            var points = _days.Select((day, i) => (day, point: new PointF(X(i), Y(day.Chests)))).Where(d => !d.day.Future).Select(d => d.point).ToList();
            if (points.Count < 2)
            {
                return;
            }
            using var line = new GraphicsPath();
            for (var i = 0; i < points.Count - 1; i++)
            {
                var before = points[Math.Max(0, i - 1)];
                var from = points[i];
                var to = points[i + 1];
                var after = points[Math.Min(points.Count - 1, i + 2)];
                var control1 = new PointF(from.X + (to.X - before.X) / 6f, Math.Min(_plot.Bottom, from.Y + (to.Y - before.Y) / 6f));
                var control2 = new PointF(to.X - (after.X - from.X) / 6f, Math.Min(_plot.Bottom, to.Y - (after.Y - from.Y) / 6f));
                line.AddBezier(from, control1, control2, to);
            }

            using (var area = (GraphicsPath)line.Clone())
            {
                var last = points[^1];
                area.AddLine(last, new PointF(last.X, _plot.Bottom));
                area.AddLine(new PointF(last.X, _plot.Bottom), new PointF(points[0].X, _plot.Bottom));
                area.CloseFigure();
                using var fade = new LinearGradientBrush(new PointF(0, _plot.Top - 1), new PointF(0, _plot.Bottom + 1), Color.FromArgb(90, Theme.GoldBase), Color.FromArgb(0, Theme.GoldBase));
                _g.FillPath(fade, area);
            }
            // the page's gold drop-shadow, then the line itself
            using (var glow = RoundPen(Color.FromArgb(55, Theme.GoldBase), 6f))
            {
                _g.DrawPath(glow, line);
            }
            using (var gold = RoundPen(Theme.GoldBase, 2.2f))
            {
                _g.DrawPath(gold, line);
            }
        }

        private static Pen RoundPen(Color color, float width) =>
            new(color, width) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };

        /// <summary>The average of the days played, dashed (its figure is in the card's header, so it never sits on the curve).</summary>
        private void Average()
        {
            var played = _days.Where(d => d.Chests > 0).ToList();
            if (played.Count < 2)
            {
                return;
            }
            var average = played.Average(d => d.Chests);
            var y = Y(average);
            using var dashes = new Pen(Color.FromArgb(115, Theme.GoldHi), 1f) { DashPattern = new[] { 3f, 3f } };
            _g.DrawLine(dashes, _plot.Left, y, _plot.Right, y);
        }

        /// <summary>A light gold dot per day so far (today white) ringed in the card's colour, its figure always above it.</summary>
        private void Dots()
        {
            var r = _dotRadius;
            using var ring = new Pen(Theme.Card, 2f);
            using var dot = new SolidBrush(Theme.GoldHi);
            using var today = new SolidBrush(Color.White);
            for (var i = 0; i < _days.Count; i++)
            {
                var day = _days[i];
                if (day.Future)
                {
                    continue;
                }
                var at = new PointF(X(i), Y(day.Chests));
                var isToday = IsToday(day);
                _g.FillEllipse(isToday ? today : dot, at.X - r, at.Y - r, 2 * r, 2 * r);
                _g.DrawEllipse(ring, at.X - r, at.Y - r, 2 * r, 2 * r);
                if (day.Chests > 0)
                {
                    var figure = L.N(day.Chests);
                    var size = Measure(figure, _figureFont);
                    var x = Math.Clamp(at.X - size.Width / 2f, _plot.Left, _chart.Width - size.Width);
                    Write(figure, _figureFont, isToday ? Color.White : Theme.Champ, x, at.Y - r - 2f - size.Height);
                }
            }
        }

        /// <summary>Short day names under the plot (today's in gold); the first and last kept inside the edges.</summary>
        private void DayNames()
        {
            for (var i = 0; i < _days.Count; i++)
            {
                var day = _days[i];
                var isToday = IsToday(day);
                var name = day.Day.ToString("ddd", L.Culture);
                if (name.Length > 3)
                {
                    name = name[..3];
                }
                var font = isToday ? _todayFont : _dayFont;
                var width = Measure(name, font).Width;
                Write(name, font, isToday ? Theme.GoldBase : Theme.Muted, Math.Clamp(X(i) - width / 2f, 0f, _chart.Width - width), _plot.Bottom + 5f);
            }
        }

        public void Dispose()
        {
            _axisFont.Dispose();
            _dayFont.Dispose();
            _todayFont.Dispose();
            _figureFont.Dispose();
        }
    }
}
