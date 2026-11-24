using System.Globalization;
using System.Text.RegularExpressions;

namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>2026-11-23-c81eb: a Kubernetes resource quantity ('500m', '1', '0.5', '128Mi', '1G') read as its value in
/// base units, so two quantities compare; null when the text is not one.</summary>
public static partial class PocQuantity
{
    private static readonly Dictionary<string, decimal> Suffixes = new(StringComparer.Ordinal)
    {
        [""] = 1m, ["m"] = 0.001m, ["k"] = 1e3m, ["M"] = 1e6m, ["G"] = 1e9m, ["T"] = 1e12m, ["P"] = 1e15m,
        ["Ki"] = 1024m, ["Mi"] = 1024m * 1024, ["Gi"] = 1024m * 1024 * 1024, ["Ti"] = 1024m * 1024 * 1024 * 1024,
    };

    public static decimal? Parse(string? text)
    {
        var match = Shape().Match(text ?? string.Empty);
        if (!match.Success) return null;
        var number = decimal.Parse(match.Groups["number"].Value, CultureInfo.InvariantCulture);
        return number * Suffixes[match.Groups["suffix"].Value];
    }

    [GeneratedRegex(@"^(?<number>[0-9]+(\.[0-9]+)?)(?<suffix>m|k|M|G|T|P|Ki|Mi|Gi|Ti)?$")]
    private static partial Regex Shape();
}
