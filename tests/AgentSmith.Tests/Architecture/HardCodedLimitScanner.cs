using System.Text.RegularExpressions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// 2026-11-17-274cc: counts the lines of a C# source that hard-code a bound — a numeric or
/// TimeSpan const/static readonly whose initializer holds a digit, or a TimeSpan.From*, Take,
/// TakeLast, Task.Delay or CancelAfter called with a numeric literal. One line counts once, so a
/// declaration whose literal sits on its next line counts once. A comment line, a settable or
/// init config property's default, and a line that says why it is no limit
/// ('// not a limit: &lt;why&gt;', the reason required) count zero.
/// </summary>
internal static class HardCodedLimitScanner
{
    private static readonly Regex[] Patterns =
    [
        new(@"\b(const|static\s+readonly)\s+(int|long|double|float|decimal|TimeSpan)\s+\w+\s*=[^;]*\d", RegexOptions.Compiled),
        new(@"TimeSpan\.From(Milliseconds|Seconds|Minutes|Hours|Days)\(\s*\d", RegexOptions.Compiled),
        new(@"\.(Take|TakeLast)\(\s*\d+\s*\)", RegexOptions.Compiled),
        new(@"(Task\.Delay|CancelAfter)\(\s*\d", RegexOptions.Compiled),
    ];

    private static readonly Regex ConfigProperty = new(@"\{\s*get;\s*(set|init);\s*\}", RegexOptions.Compiled);
    private static readonly Regex NotALimit = new(@"//\s*not a limit:\s*\S", RegexOptions.Compiled);

    public static int Count(string text) =>
        text.Split('\n').Count(line => Counts(line.Trim()));

    private static bool Counts(string line) =>
        !line.StartsWith("//", StringComparison.Ordinal)
        && !line.StartsWith("*", StringComparison.Ordinal)
        && !ConfigProperty.IsMatch(line)
        && !NotALimit.IsMatch(line)
        && Patterns.Any(p => p.IsMatch(line));
}
