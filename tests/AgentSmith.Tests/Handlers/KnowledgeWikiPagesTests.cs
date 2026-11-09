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

        wiki.Should().Contain("### decisions.md\n- chose SQL").And.Contain("### index.md\n# Index").And.NotContain("run-9");
    }

    [Fact]
    public void KnowledgePrompt_ExistingPages_SaysAReturnedPageReplacesTheStoredOne()
    {
        var prompt = new KnowledgePromptBuilder(Moq.Mock.Of<AgentSmith.Contracts.Services.IPromptCatalog>())
            .BuildUserPrompt("### decisions.md\n- chose SQL", []);

        prompt.Should().Contain("A page you return replaces the stored page entirely")
            .And.Contain("Return the pages this run data changes, each whole");
    }
}
