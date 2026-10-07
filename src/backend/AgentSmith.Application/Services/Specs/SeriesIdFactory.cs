using System.Globalization;
using System.Security.Cryptography;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-06-03c7c: mints the base id of a SERIES — the specs cut from one piece of work —
/// in code, from the clock: today's UTC date plus four random hex digits, the shape this
/// repository mints its own phases in. Its members are the base plus a letter, by position.
/// <para>
/// It replaces ids derived from a ticket number: two trackers number their tickets
/// independently, so equal numbers collided, and a model never mints one either.
/// </para>
/// </summary>
public sealed class SeriesIdFactory(TimeProvider time)
{
    private const string DateFormat = "yyyy-MM-dd";
    private const int SuffixSpace = 0x10000;
    private const string SuffixFormat = "x4";

    /// <summary>A fresh base id, <c>{yyyy-MM-dd}-{xxxx}</c>.</summary>
    public string Mint()
    {
        var date = time.GetUtcNow().UtcDateTime.ToString(DateFormat, CultureInfo.InvariantCulture);
        var suffix = RandomNumberGenerator.GetInt32(SuffixSpace)
            .ToString(SuffixFormat, CultureInfo.InvariantCulture);
        return $"{date}-{suffix}";
    }

    /// <summary>The id of the series' member at <paramref name="index"/> — a, b, c, ….</summary>
    public static string Member(string series, int index) => $"{series}{(char)('a' + index)}";
}
