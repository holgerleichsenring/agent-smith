using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.Architecture;
using FluentAssertions;

namespace AgentSmith.Tests.Specs.Review;

/// <summary>2026-10-13-8762n: the entered spec's path:line citations against the tree they were written against — real git.</summary>
[Trait("TestProcess", "serial")]
[Collection(ExternalProcessCollection.Name)]
public sealed class SpecCitationRegrounderTests
{
    private const string Code = "src/Api/Orders.cs";

    /// <summary>Orders.cs written, the spec citing <paramref name="evidence"/> committed after it.</summary>
    private static async Task<string> CitedAsync(CitationRepo repo, params string[] evidence)
    {
        await repo.WriteAsync(Code, CitationRepo.Numbered("order", 8));
        await repo.CommitAsync("code", CitationRepo.At(1));
        var yaml = CitationRepo.SpecYaml(CitationRepo.SpecId, evidence);
        await repo.WriteAsync(CitationRepo.SpecPath(), yaml);
        await repo.CommitAsync("spec", CitationRepo.At(2));
        return yaml;
    }

    private static Task<SpecReground> RegroundAsync(string yaml, params CitationRepo[] repos)
    {
        var spec = CitationRepo.Spec(yaml);
        var set = CitationRepo.Set(null, spec);
        var pipeline = CitationRepo.Pipeline(repos);
        pipeline.Set(ContextKeys.SpecSet, set);
        return CitationRepo.Regrounder().RegroundAsync(pipeline, set, spec, CancellationToken.None);
    }

    [Fact]
    public async Task SpecCitationRegrounder_ChangedLinesFoundNowhere_IsAStaleCitation()
    {
        await using var repo = await CitationRepo.CreateAsync();
        if (repo is null) return;
        var yaml = await CitedAsync(repo, Code + ":2-3");
        await repo.WriteAsync(Code, CitationRepo.Numbered("rewritten", 8));
        await repo.CommitAsync("rewritten", CitationRepo.At(3));

        var found = await RegroundAsync(yaml, repo);

        found.Moves.Should().BeEmpty();
        found.Yaml.Should().Be(yaml.Trim(), "a stale citation is told, never rewritten");
        found.Stale.Should().ContainSingle().Which.Should().StartWith(Code + ":2-3 (shop): its lines changed")
            .And.EndWith("their text was found nowhere");
    }

    [Fact]
    public async Task SpecCitationRegrounder_TokenWithContinuation_MovedToOtherFile_IsStaleNotSpliced()
    {
        await using var repo = await CitationRepo.CreateAsync();
        if (repo is null) return;
        var yaml = await CitedAsync(repo, Code + ":2-3, :6");
        var lines = CitationRepo.Numbered("order", 8).Split('\n');
        await repo.WriteAsync("src/Api/Billing.cs", "// billing\n" + lines[1] + "\n" + lines[2] + "\n");
        await repo.WriteAsync(Code, string.Join('\n', lines.Where((_, i) => i is not (1 or 2))));
        await repo.CommitAsync("moved to billing", CitationRepo.At(3));

        var found = await RegroundAsync(yaml, repo);

        found.Moves.Should().BeEmpty("re-pointing it would make ':6' read as Billing.cs:6");
        found.Yaml.Should().Be(yaml.Trim());
        found.Stale.Should().ContainSingle().Which.Should().Contain("src/Api/Billing.cs:2-3")
            .And.Contain("a ':' citation continues it");
    }

    [Fact]
    public async Task SpecCitationRegrounder_MultiRepo_BareFileMovedAway_IsRegrounded()
    {
        await using var shop = await CitationRepo.CreateAsync();
        await using var web = await CitationRepo.CreateAsync("web");
        if (shop is null || web is null) return;
        var yaml = await CitedAsync(shop, Code + ":2-3");
        await shop.GitAsync(null, "mv", Code, "src/Api/Billing.cs");
        await shop.CommitAsync("a predecessor moved the file", CitationRepo.At(3));

        var found = await RegroundAsync(yaml, shop, web);

        found.Moves.Should().ContainSingle().Which.New.Should().Be("src/Api/Billing.cs:2-3",
            "no working tree holds the cited file any more; the base of shop does");
        found.Yaml.Should().Contain("evidence: \"src/Api/Billing.cs:2-3\"");
    }

    [Fact]
    public async Task SpecCitationRegrounder_RepositoryPrefixedToken_NewTokenKeepsThePrefix()
    {
        await using var repo = await CitationRepo.CreateAsync();
        if (repo is null) return;
        var yaml = await CitedAsync(repo, "shop/" + Code + ":2-3");
        await repo.WriteAsync(Code, "// one\n// two\n" + CitationRepo.Numbered("order", 8));
        await repo.CommitAsync("two lines on top", CitationRepo.At(3));

        var found = await RegroundAsync(yaml, repo);

        found.Moves.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new SpecCitationMove("shop/" + Code + ":2-3", "shop/" + Code + ":4-5", [0]));
    }

    [Fact]
    public async Task SpecCitationRegrounder_MintedLookLines_AreUntouched()
    {
        var sandbox = new AgentSmith.Tests.TestHelpers.StubSandbox();
        var yaml = CitationRepo.SpecYaml(CitationRepo.SpecId, "[L1] shop: read " + Code + ":2-3 ran, exited 0");
        var spec = CitationRepo.Spec(yaml);
        var set = CitationRepo.Set(null, spec);
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, new Dictionary<string, ISandbox> { ["shop"] = sandbox });
        pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos, new Dictionary<string, string> { ["shop"] = "shop" });
        pipeline.Set(ContextKeys.SpecSet, set);

        var found = await CitationRepo.Regrounder().RegroundAsync(pipeline, set, spec, CancellationToken.None);

        found.Should().BeEquivalentTo(SpecReground.Unchanged(yaml.Trim()));
        sandbox.RanSteps.Should().BeEmpty("a minted look line is out of reach by design; nothing is asked of git");
    }

    [Fact]
    public async Task SpecCitationRegrounder_OverPairBudget_CountsUnchecked()
    {
        await using var repo = await CitationRepo.CreateAsync();
        if (repo is null) return;
        var cited = Enumerable.Range(1, SpecRegroundSession.MaxPairs + 1).Select(i => $"src/F{i}.cs:1").ToArray();
        foreach (var token in cited) await repo.WriteAsync(token[..^2], "class F {}\n");
        await repo.CommitAsync("files", CitationRepo.At(1));
        var yaml = CitationRepo.SpecYaml(CitationRepo.SpecId, cited);
        await repo.WriteAsync(CitationRepo.SpecPath(), yaml);
        await repo.CommitAsync("spec", CitationRepo.At(2));

        var found = await RegroundAsync(yaml, repo);

        found.Unchecked.Should().Be(1, "the 25th distinct file is past the budget of 24");
        found.Moves.Should().BeEmpty();
        found.Stale.Should().BeEmpty();
    }
}
