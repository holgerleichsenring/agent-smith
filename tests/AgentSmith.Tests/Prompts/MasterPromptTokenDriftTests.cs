using System.Text.RegularExpressions;
using AgentSmith.Application.Prompts;
using FluentAssertions;

namespace AgentSmith.Tests.Prompts;

/// <summary>
/// p0313: MasterPromptTokens.All is what makes an unbound placeholder fail loud
/// instead of reaching the model as the literal text "{Token}". It is a hand-kept
/// list, so it rots exactly the way a second source of truth always does: when
/// {WorkSpecSection} (p0390) and {ProgressLedgerSection} (p0341) were added to
/// the coding master, nobody extended the list — and because the check only looks
/// for KNOWN tokens, it stayed silent while GenerateTests and GenerateDocs sent
/// both strings verbatim to the model on every add-feature run.
///
/// This test closes that loop: every PascalCase placeholder any master in the
/// checked-in catalog references must be declared. The next person to add a token
/// finds out here rather than in a prompt.
/// </summary>
public sealed class MasterPromptTokenDriftTests
{
    [Fact]
    public void EveryPlaceholderUsedByAMaster_IsDeclaredInMasterPromptTokens()
    {
        var masters = MasterSkillFiles().ToList();
        masters.Should().NotBeEmpty("the catalog fixture must ship master bodies to scan");

        var used = masters
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), @"\{(?<token>[A-Z][A-Za-z]*)\}"))
            .Select(m => m.Groups["token"].Value)
            .Distinct()
            .ToList();

        used.Should().BeSubsetOf(MasterPromptTokens.All,
            "an undeclared placeholder is invisible to the strict-render check and "
            + "reaches the model as literal text");
    }

    private static IEnumerable<string> MasterSkillFiles()
    {
        var root = Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "SkillsCatalog", "skills", "_masters");
        if (!Directory.Exists(root))
            root = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "AgentSmith.PipelineHarness",
                "Fixtures", "SkillsCatalog", "skills", "_masters"));
        return Directory.Exists(root)
            ? Directory.EnumerateFiles(root, "SKILL.md", SearchOption.AllDirectories)
            : [];
    }
}
