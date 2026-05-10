namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// Resolves the agent-smith-skills <c>skills/</c> root for tests. Mirrors the
/// resolution in <see cref="TestPatternsDirectory"/> but returns the parent
/// directory (the one that holds <c>concept-vocabulary.yaml</c>).
/// Tests look at AGENTSMITH_TEST_SKILLS_DIR / ./test-skills /
/// adjacent agent-smith-skills checkout.
/// </summary>
internal static class TestSkillsRoot
{
    public static string? Resolve()
    {
        var env = Environment.GetEnvironmentVariable("AGENTSMITH_TEST_SKILLS_DIR");
        if (!string.IsNullOrWhiteSpace(env))
        {
            var direct = Path.Combine(env, "skills");
            if (Directory.Exists(direct)) return direct;
        }

        var repoRoot = Path.GetFullPath(Path.Combine(
            Directory.GetCurrentDirectory().Split("bin")[0], "..", ".."));

        var candidates = new[]
        {
            Path.Combine(repoRoot, "test-skills", "skills"),
            Path.GetFullPath(Path.Combine(repoRoot, "..", "agent-smith-skills", "skills")),
        };

        return candidates.FirstOrDefault(Directory.Exists);
    }

    public static bool IsAvailable() => Resolve() is not null;
}
