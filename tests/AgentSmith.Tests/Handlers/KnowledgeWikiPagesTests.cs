using AgentSmith.Application.Services.Handlers;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-11-09-0620h: the knowledge compile is shown every wiki page, because each page it returns replaces the stored
/// one — shown only the index, it rewrote the other pages from nothing.
/// </summary>
public sealed class KnowledgeWikiPagesTests
{
    [Fact]
    public async Task ReadExistingWiki_SeveralPages_ShowsEachUnderItsName()
    {
        var files = new SpecBranchFiles();
        files.Seed("/w/wiki/index.md", "# Index");
        files.Seed("/w/wiki/decisions.md", "- chose SQL");
        files.Seed("/w/wiki/last-compiled.txt", "run-9");

        var wiki = await new RunDirectoryReader().ReadExistingWikiAsync(files, "/w/wiki", CancellationToken.None);

        wiki.Should().StartWith("<page name=\"index.md\">\n# Index\n</page>")
            .And.Contain("<page name=\"decisions.md\">\n- chose SQL\n</page>").And.NotContain("run-9");
    }

    [Fact]
    public async Task ReadExistingWiki_OverBudget_NamesThePagesLeftOut()
    {
        var files = new SpecBranchFiles();
        files.Seed("/w/wiki/index.md", "# Index");
        files.Seed("/w/wiki/big.md", new string('x', RunDirectoryReader.WikiBudgetChars));

        var wiki = await new RunDirectoryReader().ReadExistingWikiAsync(files, "/w/wiki", CancellationToken.None);

        wiki.Should().Contain("<page name=\"index.md\">").And.EndWith("Left out to fit: big.md.");
    }

    [Fact]
    public void KnowledgePrompt_ExistingPages_RepeatsNoSkillRule()
    {
        var prompt = new KnowledgePromptBuilder(Moq.Mock.Of<AgentSmith.Contracts.Services.IPromptCatalog>())
            .BuildUserPrompt("<page name=\"decisions.md\">\n- chose SQL\n</page>", []);

        prompt.Should().Contain("## Existing wiki pages").And.NotContain("replaces the stored page").And.NotContain("wiki_updates");
    }
}
