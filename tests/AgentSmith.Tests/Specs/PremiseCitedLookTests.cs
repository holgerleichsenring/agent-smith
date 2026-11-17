using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using static AgentSmith.Tests.Specs.DerivationTestLooks;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-11-04-6768a: what the series-base replay needs from a look — the pattern a search ran, and the
/// one look the admission chose for a false finding.
/// </summary>
public sealed class PremiseCitedLookTests
{
    [Fact]
    public async Task RepositorySearchTool_Search_RecordsItsPattern()
    {
        var look = Over(new CountingSandbox(exitCode: 1));

        await new RepositorySearchTool(look, NullLogger.Instance).SearchRepository(Repo, "Empty|Payload", "src");

        look.Evidence.Looks.Single().Pattern.Should().Be("Empty|Payload", "never read back out of the prose 'what'");
    }

    // 2026-11-17-5701f: the replay's run read '<repo>/<path>' inside the repository, found nothing and stopped.
    [Fact]
    public async Task PremiseCheckLook_APrefixedPathInARunKeyedDefault_ReadsTheRealFile()
    {
        var files = new InMemorySandboxFileReader();
        files.Files["/work/src/Data/AppDbContext.cs"] = "class AppDbContext {}";
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Sandboxes,
            (IReadOnlyDictionary<string, ISandbox>)
            new Dictionary<string, ISandbox> { ["default"] = new CountingSandbox(0) });
        pipeline.Set(ContextKeys.SandboxDiscoveries,
            (IReadOnlyDictionary<string, RemoteContextDiscovery>)
            new Dictionary<string, RemoteContextDiscovery>());
        pipeline.Set(ContextKeys.SandboxRepos,
            (IReadOnlyDictionary<string, string>)new Dictionary<string, string> { ["default"] = Repo });
        var look = Factory(new FixedReaderFactory(files)).ForPremiseCheck(pipeline)!;

        var answer = await new RepositoryFileReadTool(look, new FixedReaderFactory(files), NullLogger.Instance)
            .ReadFile("default", $"{Repo}/src/Data/AppDbContext.cs");

        answer.Should().Contain("class AppDbContext {}").And.NotContain("has nothing at");
        look.Evidence.Looks.Single().ExitCode.Should().Be(0, "a read that found the file is no evidence of an absence");
    }

    [Fact]
    public async Task PremiseCheckAdmission_ProvenFinding_CarriesTheCitedLook()
    {
        var look = Over(new CountingSandbox(exitCode: 0));
        await new RepositorySearchTool(look, NullLogger.Instance).SearchRepository(Repo, "first");
        await new RepositorySearchTool(look, NullLogger.Instance).SearchRepository(Repo, "second");
        var draft = PremiseCheckTestDoubles.Draft();

        var check = new PremiseCheckAdmission(NullLogger.Instance).Admit(draft, SpecPremises.For(draft),
            [new PremiseFinding("OrderHandler validates the payload before dispatch", "no-longer-holds", "gone", "X9, L2, L1")],
            look);

        check.False.Single().CitedId.Should().Be("L2", "the first cited look that ran, the one whose line it carries");
        check.False.Single().Evidence.Should().StartWith("[L2]");
    }
}
