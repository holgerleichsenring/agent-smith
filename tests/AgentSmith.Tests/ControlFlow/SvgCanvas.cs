using System.Globalization;
using System.Text;

namespace AgentSmith.Tests.ControlFlow;

/// <summary>
/// p0408: the smallest SVG writer the control-flow generator needs. Invariant number
/// formatting and escaped text, so the generated file is byte-identical on every machine
/// — a drift test is only worth having if regeneration is deterministic.
/// </summary>
internal sealed class SvgCanvas
{
    private readonly StringBuilder _sb = new();

    public void Raw(string markup) => _sb.Append(markup).Append('\n');

    public void Rect(double x, double y, double w, double h, string cls, double rx = 8) =>
        Raw($"""  <rect x="{N(x)}" y="{N(y)}" width="{N(w)}" height="{N(h)}" rx="{N(rx)}" class="{cls}"/>""");

    public void Text(
        double x, double y, string cls, double size, string text,
        string weight = "400", string anchor = "start") =>
        Raw($"""  <text x="{N(x)}" y="{N(y)}" class="{cls}" font-size="{N(size)}" """
            + $"""font-weight="{weight}" text-anchor="{anchor}">{Escape(text)}</text>""");

    public void Path(string d, string cls) => Raw($"""  <path d="{d}" class="{cls}"/>""");

    public void Line(double x1, double y1, double x2, double y2, string cls) =>
        Raw($"""  <line x1="{N(x1)}" y1="{N(y1)}" x2="{N(x2)}" y2="{N(y2)}" class="{cls}"/>""");

    public override string ToString() => _sb.ToString();

    /// <summary>Trims a label to the width a box can actually show, with an ellipsis so
    /// truncation is visible rather than silent.</summary>
    public static string Fit(string text, int maxChars) =>
        text.Length <= maxChars ? text : text[..(maxChars - 1)].TrimEnd() + "…";

    private static string N(double value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Escape(string text) => text
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal);
}
