namespace AgentSmith.Contracts.Reviews;

/// <summary>2026-11-26-49d3a: one failed test as a host reported it — its outcome, its name, the error message and the stack.</summary>
public sealed record CiFailedTest(string Outcome, string Name, string? Message, string? Stack);

/// <summary>
/// 2026-11-26-49d3a: failed tests as an excerpt's lines — every test's outcome, name and message first, then each stack under
/// its test's name. The excerpt keeps its start within merge.ci_excerpt_chars, so a long stack cuts stacks, never the name or
/// message of a later test.
/// </summary>
public static class CiTestLines
{
    public static IReadOnlyList<string> Of(IReadOnlyList<CiFailedTest> tests)
    {
        ArgumentNullException.ThrowIfNull(tests);
        var lines = new List<string>();
        foreach (var test in tests)
        {
            lines.Add($"test {test.Outcome}: {test.Name}");
            if (!string.IsNullOrWhiteSpace(test.Message)) lines.Add(test.Message.Trim());
        }
        foreach (var test in tests.Where(t => !string.IsNullOrWhiteSpace(t.Stack)))
            lines.Add($"stack of {test.Name}:\n{test.Stack!.Trim()}");
        return lines;
    }
}
