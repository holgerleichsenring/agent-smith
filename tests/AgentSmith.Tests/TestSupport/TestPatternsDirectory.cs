namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// Resolves the patterns directory for tests. Patterns now live in the
/// external agentsmith-skills repo (extracted alongside skills/). Tests look
/// at AGENTSMITH_TEST_SKILLS_DIR / ./test-skills / adjacent agentsmith-skills
/// checkout.
/// </summary>
internal static class TestPatternsDirectory
{
    public static string? Resolve()
    {
        var env = Environment.GetEnvironmentVariable("AGENTSMITH_TEST_SKILLS_DIR");
        if (!string.IsNullOrWhiteSpace(env))
        {
            var direct = Path.Combine(env, "patterns");
            if (Directory.Exists(direct)) return direct;
        }

        var repoRoot = Path.GetFullPath(Path.Combine(
            Directory.GetCurrentDirectory().Split("bin")[0], "..", ".."));

        var candidates = new[]
        {
            Path.Combine(repoRoot, "test-skills", "patterns"),
            Path.GetFullPath(Path.Combine(repoRoot, "..", "agent-smith-skills", "patterns")),
        };

        return candidates.FirstOrDefault(Directory.Exists);
    }

    public static bool IsAvailable() => Resolve() is not null;
}
