namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// Resolves the agent-smith-skills <c>skills/</c> root for tests. Mirrors the
/// resolution in <see cref="TestPatternsDirectory"/> but returns the parent
/// directory (the one that holds <c>concept-vocabulary.yaml</c>).
/// Tests look at AGENTSMITH_TEST_SKILLS_DIR / ./test-skills /
/// adjacent agent-smith-skills checkout.
///
/// p0125c-followup: the resolver REQUIRES <c>concept-vocabulary.yaml</c> to
/// exist at the candidate path, not just the directory itself. A bare
/// <c>skills/</c> directory without the vocab file would have the loader
/// return <c>ConceptVocabulary.Empty</c> — that's the silent-empty trap
/// that hid the bug in the first place. Better to return null and let
/// callers fall back to the hand-rolled minimal vocab.
/// </summary>
internal static class TestSkillsRoot
{
    private const string VocabFileName = "concept-vocabulary.yaml";

    public static string? Resolve()
    {
        var env = Environment.GetEnvironmentVariable("AGENTSMITH_TEST_SKILLS_DIR");
        if (!string.IsNullOrWhiteSpace(env))
        {
            var direct = Path.Combine(env, "skills");
            if (HasVocab(direct)) return direct;
        }

        var repoRoot = Path.GetFullPath(Path.Combine(
            Directory.GetCurrentDirectory().Split("bin")[0], "..", ".."));

        var candidates = new[]
        {
            Path.Combine(repoRoot, "test-skills", "skills"),
            Path.GetFullPath(Path.Combine(repoRoot, "..", "agent-smith-skills", "skills")),
        };

        return candidates.FirstOrDefault(HasVocab);
    }

    public static bool IsAvailable() => Resolve() is not null;

    private static bool HasVocab(string skillsDirectory) =>
        Directory.Exists(skillsDirectory)
        && File.Exists(Path.Combine(skillsDirectory, VocabFileName));
}
