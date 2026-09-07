using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using static AgentSmith.Tests.Specs.DerivationTestLooks;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-07-b7e2: the derivation is offered a fixed, named set of read-only tools and
/// nothing else — what CAN be called is the framework's, which is what makes the
/// budget a cost bound rather than a safety promise.
/// </summary>
public sealed class DerivationLookTests
{
    [Fact]
    public void Look_ADerivationCall_IsOfferedOnlyTheNamedReadOnlyTools()
    {
        var look = Over(new CountingSandbox(exitCode: 1));

        var offered = DerivationTools.For(look)!.Select(t => t.Name).ToList();

        offered.Should().BeEquivalentTo(
            [RepositorySearchTool.Name, RepositoryFileReadTool.Name, DependencyAuditTool.Name],
            "a search, a file read and the ecosystem's audit — no shell, no probe command");
    }

    [Fact]
    public void Look_ARunWithoutSandboxes_OffersNoTools()
    {
        var pipeline = new PipelineContext();

        var look = Factory().Create(pipeline);

        look.Should().BeNull("a derivation over nothing checked out writes from the ticket alone");
        DerivationTools.For(look).Should().BeNull(
            "null and an empty Tools list are different calls; null is the one with no offer");
    }

    [Fact]
    public void Look_ARunWithSandboxes_ResolvesEveryOneOfThem()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Sandboxes, (IReadOnlyDictionary<string, ISandbox>)
            new Dictionary<string, ISandbox> { ["api"] = new CountingSandbox(0), ["worker"] = new CountingSandbox(0) });
        pipeline.Set(ContextKeys.SandboxDiscoveries,
            (IReadOnlyDictionary<string, RemoteContextDiscovery>)new Dictionary<string, RemoteContextDiscovery>());

        var look = Factory().Create(pipeline);

        look!.Repositories.Should().BeEquivalentTo(["api", "worker"],
            "the derivation looks over every sandbox of the run, not the carrying repo's one");
    }

    [Fact]
    public async Task Look_ACallOverTheBudget_IsRefusedAndTheDerivationCompletesWithoutIt()
    {
        var sandbox = new CountingSandbox(exitCode: 1);
        var search = new RepositorySearchTool(Over(sandbox), Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

        string last = string.Empty;
        for (var i = 0; i <= DerivationLookBudget.Allowance + 2; i++)
            last = await search.SearchRepository(Repo, $"pattern{i}");

        sandbox.Ran.Should().HaveCount(DerivationLookBudget.Allowance);
        last.Should().Be(DerivationLookBudget.Exhausted,
            "the refusal is answered in text, so the loop ends on the model's reply, not on a cap");
    }

    [Theory]
    [InlineData("/root/.npmrc")]
    [InlineData("../../root/.nuget/NuGet/NuGet.Config")]
    [InlineData("src/../../etc/passwd")]
    [InlineData("~/.npmrc")]
    public async Task Look_APathOutsideTheRepository_IsRefusedWithoutRunningAnything(string path)
    {
        var sandbox = new CountingSandbox(exitCode: 0, output: "secret");
        var files = new InMemorySandboxFileReader();
        files.Files["/root/.npmrc"] = "//registry/:_authToken=secret";
        var look = Over(sandbox, files);
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

        var searched = await new RepositorySearchTool(look, logger).SearchRepository(Repo, "token", path);
        var read = await new RepositoryFileReadTool(look, new FixedReaderFactory(files), logger).ReadFile(Repo, path);

        searched.Should().Be(ContainedPath.Refusal);
        read.Should().Be(ContainedPath.Refusal);
        sandbox.Ran.Should().BeEmpty("nothing ran");
        look.Evidence.Lines.Should().BeEmpty("nothing was looked at, so nothing is citable");
    }

    [Fact]
    public async Task Look_EveryToolCall_MintsAnEvidenceLineWithAnId()
    {
        var sandbox = new CountingSandbox(exitCode: 0, output: "{\"vulnerabilities\":{}}");
        var files = new InMemorySandboxFileReader();
        files.Files["/work/package.json"] = "{}";
        var look = Over(sandbox, files);
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

        var searched = await new RepositorySearchTool(look, logger).SearchRepository(Repo, "lodash", "src");
        var read = await new RepositoryFileReadTool(look, new FixedReaderFactory(files), logger).ReadFile(Repo, "package.json");
        var audited = await new DependencyAuditTool(
            look, new FixedReaderFactory(files), new AgentSmith.Application.Services.Sandbox.PackageEcosystemDetector(), logger)
            .AuditDependencies(Repo);

        searched.Should().StartWith("[L1] ");
        read.Should().StartWith("[L2] ").And.Contain("{}");
        audited.Should().StartWith("[L3] ").And.Contain("npm audit --json");
        look.Evidence.Lines.Should().HaveCount(3);
        look.Evidence.Lines[0].Should().StartWith($"[L1] {Repo}: the derivation ran 'grep -E 'lodash' src' exited 0");
        look.Evidence.Lines[2].Should().Be($"[L3] {Repo}: the derivation ran 'npm audit --json' exited 0");
        sandbox.Ran.Select(s => s.Command).Should().Equal("grep", "npm");
        sandbox.Ran[1].Args.Should().Equal("audit", "--json");
    }

    [Fact]
    public async Task Look_AnAuditThatCouldNotRun_SaysItProvesNothing()
    {
        var sandbox = new CountingSandbox(exitCode: 127, output: string.Empty);
        var files = new InMemorySandboxFileReader();
        files.Files["/work/src/Api/Api.csproj"] = "<Project />";
        var look = Over(sandbox, files);

        var audited = await new DependencyAuditTool(
            look, new FixedReaderFactory(files), new AgentSmith.Application.Services.Sandbox.PackageEcosystemDetector(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance).AuditDependencies(Repo);

        audited.Should().Contain("proves nothing");
        look.Evidence.Lines.Single().Should().Contain("could not run, so it proves nothing");
        sandbox.Ran.Single().Command.Should().Be("dotnet");
    }
}
