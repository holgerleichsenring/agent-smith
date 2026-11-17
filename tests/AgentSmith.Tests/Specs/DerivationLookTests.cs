using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
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
        var look = Over(sandbox);
        var search = new RepositorySearchTool(look, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

        string last = string.Empty;
        for (var i = 0; i <= DerivationLookTerms.DerivationAllowance + 2; i++)
            last = await search.SearchRepository(Repo, $"pattern{i}");

        sandbox.Ran.Should().HaveCount(DerivationLookTerms.DerivationAllowance);
        last.Should().Be(look.Budget.Exhausted,
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

    // 2026-10-17-293bb: the .NET audit exits 0 with or without advisories; its exit 1 is an
    // unrestored project ("No assets file was found"), which once became a fact about the code.
    [Fact]
    public async Task Look_ADotNetAuditExitingOne_ProvesNothing()
    {
        var look = await AuditWith(exitCode: 1, "/work/src/Api/Api.csproj", "<Project />");

        look.Evidence.Looks.Single().Ran.Should().BeFalse();
        look.Evidence.Lines.Single().Should().Contain("exited 1 and could not run");
    }

    [Fact]
    public async Task Look_AnNpmAuditExitingOne_ReachedAVerdict()
    {
        var look = await AuditWith(exitCode: 1, "/work/package.json", "{}");

        look.Evidence.Looks.Single().Ran.Should().BeTrue("npm audit exits 1 when it finds advisories");
    }

    // 2026-11-17-5701f: a single-repository run keys its sandbox 'default'; the design turn cited the repository's name.
    [Fact]
    public async Task ReadFile_APathPrefixedWithTheKeysRepositoryName_ReadsBelowTheName()
    {
        var files = new InMemorySandboxFileReader();
        files.Files["/work/src/a.cs"] = "class A {}";
        var look = Keyed(new CountingSandbox(0), files, DerivationLookTerms.ProposalReviewWith(2));
        var read = Reader(look, files);

        var answer = await read.ReadFile(Key, $"{Repo}/src/a.cs");

        answer.Should().Contain($"] {Key}/src/a.cs:").And.Contain("class A {}");
        look.Evidence.Looks.Single().Path.Should().Be("src/a.cs", "the evidence names the path that was read");
        (await read.ReadFile(Key, "src/a.cs")).Should().Contain("class A {}", "the fallback charged one look, not two");
        (await read.ReadFile(Key, "src/a.cs")).Should().Be(look.Budget.Exhausted);
    }

    [Fact]
    public async Task ReadFile_ARepositoryHoldingAFolderOfItsOwnName_ReadsThatFolderFirst()
    {
        var files = new InMemorySandboxFileReader();
        files.Files[$"/work/{Repo}/src/a.cs"] = "inner";
        files.Files["/work/src/a.cs"] = "outer";
        var look = Keyed(new CountingSandbox(0), files);

        var answer = await Reader(look, files).ReadFile(Key, $"{Repo}/src/a.cs");

        answer.Should().Contain("inner").And.NotContain("outer");
    }

    [Fact]
    public async Task ReadFile_AReadThatDidNotRun_NeverFallsBack()
    {
        var scope = new RefusingScope();
        var look = Keyed(scope, new InMemorySandboxFileReader());

        var answer = await Reader(look, new InMemorySandboxFileReader()).ReadFile(Key, $"{Repo}/src/a.cs");

        answer.Should().Contain("could not be read");
        scope.Ran.Should().ContainSingle("a read that could not run says nothing about the path");
    }

    [Fact]
    public async Task SearchRepository_APrefixedPathFilterThatDoesNotExist_SearchesBelowTheName()
    {
        var sandbox = new PathFilterSandbox();
        var look = Keyed(sandbox, new InMemorySandboxFileReader());

        var answer = await new RepositorySearchTool(look, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance)
            .SearchRepository(Key, "class A", $"{Repo}/src");

        sandbox.Ran.Select(s => s.Args![^1]).Should().Equal($"{Repo}/src", "src");
        answer.Should().Contain("src/a.cs:1:class A");
        look.Evidence.Looks.Single().Path.Should().Be("src");
        look.Evidence.Lines.Single().Should().Contain("grep -E 'class A' src'");
    }

    [Fact]
    public async Task SearchRepository_APrefixedPathWithNoMatches_DoesNotFallBack()
    {
        var sandbox = new CountingSandbox(exitCode: 1);
        var look = Keyed(sandbox, new InMemorySandboxFileReader());

        await new RepositorySearchTool(look, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance)
            .SearchRepository(Key, "class A", $"{Repo}/src");

        sandbox.Ran.Should().ContainSingle("no match is an answer about the path searched");
    }

    [Fact]
    public void WithoutRepositoryName_OnlyAKeyThatNamesARepository_Strips()
    {
        var look = Keyed(new CountingSandbox(0), new InMemorySandboxFileReader());

        look.WithoutRepositoryName(Key, $"{Repo}/src/a.cs").Should().Be("src/a.cs");
        look.WithoutRepositoryName(Key, $"{Repo}.Tests/a.cs").Should().BeNull("the name, then a slash");
        look.WithoutRepositoryName("other", $"{Repo}/src/a.cs").Should().BeNull();
    }

    private const string Key = "default";

    private static DerivationLook Keyed(
        ISandbox sandbox, InMemorySandboxFileReader files, DerivationLookTerms? terms = null) =>
        new(new Dictionary<string, ISandbox> { [Key] = sandbox }, new FixedReaderFactory(files),
            new AgentSmith.Application.Services.Sandbox.PackageEcosystemDetector(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, terms: terms,
            names: new Dictionary<string, string> { [Key] = Repo });

    private static RepositoryFileReadTool Reader(DerivationLook look, InMemorySandboxFileReader files) =>
        new(look, new FixedReaderFactory(files), Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

    /// <summary>grep as it answers a path filter: exit 2 for one under the repository's name, a match below it.</summary>
    private sealed class PathFilterSandbox : ISandbox
    {
        public string JobId => "path-filter";
        public List<Step> Ran { get; } = [];

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct)
        {
            Ran.Add(step);
            var missing = step.Args![^1].StartsWith($"{Repo}/", StringComparison.Ordinal);
            return Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId,
                missing ? 2 : 0, false, 0.1, null, missing ? "No such file or directory" : "src/a.cs:1:class A {}"));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>A source scope that opens and then refuses every read for a reason that is not an absence.</summary>
    private sealed class RefusingScope : ISourceScopeSandbox
    {
        public List<Step> Ran { get; } = [];
        public string RepoName => Repo;
        public bool IsMaterialized => true;
        public string? ResolvedSha => "sha";
        public string JobId => "refusing";

        public Task<string> MaterializeAsync(CancellationToken ct) => Task.FromResult("sha");

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken ct)
        {
            Ran.Add(step);
            return Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, 1, false, 0.1, "the reader was refused", null));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static async Task<DerivationLook> AuditWith(int exitCode, string path, string content)
    {
        var files = new InMemorySandboxFileReader();
        files.Files[path] = content;
        var look = Over(new CountingSandbox(exitCode: exitCode, output: "{}"), files);
        await new DependencyAuditTool(
            look, new FixedReaderFactory(files), new AgentSmith.Application.Services.Sandbox.PackageEcosystemDetector(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance).AuditDependencies(Repo);
        return look;
    }
}
