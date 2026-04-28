using FluentAssertions;

namespace AgentSmith.Tests.Skills;

/// <summary>
/// Smoke tests on SKILL.md content for the three code-aware skills.
/// They are LLM skills — full behavior is exercised by integration runs;
/// these tests ensure the prompts mention the specific guidance the spec calls out.
/// Skills now live in the external agentsmith-skills repo. The catalog is
/// resolved either from the AGENTSMITH_TEST_SKILLS_DIR env var, the
/// ./test-skills directory populated by scripts/fetch-skills.sh, or an
/// adjacent agentsmith-skills checkout — whichever is available.
/// </summary>
public sealed class CodeAwareSkillContentTests
{
    private static string ResolveSkillsRoot()
    {
        var env = Environment.GetEnvironmentVariable("AGENTSMITH_TEST_SKILLS_DIR");
        if (!string.IsNullOrWhiteSpace(env) && Directory.Exists(Path.Combine(env, "skills")))
            return Path.Combine(env, "skills", "api-security");

        var repoRoot = Path.GetFullPath(Path.Combine(
            Directory.GetCurrentDirectory().Split("bin")[0], "..", ".."));

        var candidates = new[]
        {
            Path.Combine(repoRoot, "test-skills", "skills", "api-security"),
            Path.GetFullPath(Path.Combine(repoRoot, "..", "agent-smith-skills", "skills", "api-security")),
        };

        return candidates.FirstOrDefault(Directory.Exists) ?? candidates[0];
    }

    private static string ReadSkill(string name)
    {
        var root = ResolveSkillsRoot();
        var path = Path.Combine(root, name, "SKILL.md");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Skill '{name}' not available — run scripts/fetch-skills.sh or set " +
                $"AGENTSMITH_TEST_SKILLS_DIR. Tried: {path}");
        }
        return File.ReadAllText(path);
    }

    private static bool SkillsAvailable() =>
        Directory.Exists(ResolveSkillsRoot());

    [Fact]
    public void AuthConfigReviewer_DeadAuthorizationMiddleware_Critical()
    {
        if (!SkillsAvailable()) return; // catalog not present in this environment
        var skill = ReadSkill("auth-config-reviewer");
        skill.Should().Contain("UseAuthentication").And.Contain("middleware");
        skill.Should().Contain("analyzed_from_source");
    }

    [Fact]
    public void OwnershipChecker_DbSetWithoutUserPredicate_FlagsIdor()
    {
        if (!SkillsAvailable()) return; // catalog not present in this environment
        var skill = ReadSkill("ownership-checker");
        skill.Should().Contain("UserId").And.Contain("ownership");
        skill.Should().Contain("critical");
    }

    [Fact]
    public void UploadValidatorReviewer_HeaderOnlyMime_Medium()
    {
        if (!SkillsAvailable()) return; // catalog not present in this environment
        var skill = ReadSkill("upload-validator-reviewer");
        skill.Should().Contain("Header-only MIME").And.Contain("medium");
        skill.Should().Contain("magic");
    }
}
