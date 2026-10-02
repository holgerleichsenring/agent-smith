using AgentSmith.Application.Services.Browser;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.PipelineHarness.Presets;
using AgentSmith.Server.Services.Sandbox;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit.Abstractions;

namespace AgentSmith.PipelineHarness.Browser;

/// <summary>
/// 2026-10-01-283di docker tier: compare_reference through the PRODUCTION Docker spawner — two
/// uploaded sets (the browser fixture's 900 px reference and 1400 px candidate) load in one run of
/// the script; the h1's font-size is named with both values and the pad counts as mismatch.
/// </summary>
[Trait("Category", "PipelineHarness")]
[Trait("Tier", "Docker")]
public sealed class CompareReferenceDockerTests(ITestOutputHelper output) : IReferenceSetReader, IToolImageDeposit
{
    private const string Conversation = "conv-283di-docker";
    private readonly List<ToolImage> _deposited = [];

    [Fact]
    public async Task CompareReference_TwoSetsOnDocker_NamesTheFontSizeAndCountsThePad()
    {
        if (!DockerAvailability.IsAvailable(out var detail))
        {
            output.WriteLine(DockerAvailability.CoverageNotExercised + " (" + detail + ")");
            return;
        }
        var backend = await DockerBackend.BuildAsync();
        var holds = new HeldSandboxRegister(new AliveSandboxHeartbeat(), NullLogger<HeldSandboxRegister>.Instance);
        try
        {
            var host = new CompareReferenceToolHost(Services(backend.Factory, holds), new RenderReferenceScope(
                new ResolvedProject { Name = "p" }, Conversation, new Dictionary<string, ISandbox>
                {
                    ["reference:reference"] = Address("reference"), ["reference:candidate"] = Address("candidate"),
                }, []));

            var text = await host.CompareReference("reference:reference", "reference:candidate", ["h1"], ct: CancellationToken.None);
            output.WriteLine(text);

            text.Should().Contain("h1 ⇄ h1: font-size — reference 16px, candidate 15px")
                .And.Contain("reference 900 px, candidate 1400 px; the shorter padded with magenta to 1400 px")
                .And.MatchRegex(@"desktop: 3[5-9]\.\d\d% of pixels differ");
            _deposited.Should().ContainSingle().Which.Bytes.Length.Should().BeLessThan(700 * 1024);
        }
        finally
        {
            await holds.EvictAsync(CancellationToken.None);
        }
    }

    private CompareReferenceServices Services(ISandboxFactory factory, IHeldSandboxRegister holds)
    {
        var specBuilder = new SandboxSpecBuilder(Mock.Of<ISandboxResourceResolver>(),
            Mock.Of<IAgentImageResolver>(r => r.Resolve(It.IsAny<ResolvedProject>()) == DockerBackend.AgentImage));
        var opener = new BrowserSandboxOpener(new SandboxContainerRuntime(true), factory, specBuilder,
            Mock.Of<IBrowserImageResolver>(r => r.Resolve(It.IsAny<ResolvedProject>()) == DockerBackend.BrowserImage),
            Options.Create(new SandboxGlobalConfig()), Mock.Of<IRunContextAccessor>(), holds,
            NullLogger<BrowserSandboxOpener>.Instance);
        var files = new SandboxFileReaderFactory();
        var stager = new RenderSourceStager(new ReferenceSetMaterialiser(this, files), new RepoRenderSource(files, new RepoTreeListing()));
        var render = new RenderReferenceServices(new RenderSourceParser(),
            new RenderUrlGuard(new DnsHostAddressResolver(), new PublicAddressRule()),
            new ReferenceRenderer(opener, stager, new BrowserRenderInvocation(files)), this, new RenderResultText());
        return new CompareReferenceServices(render, new ReferenceComparer(opener, stager, new BrowserRenderInvocation(files)),
            new StyleDifferenceComparer(), new VisualComparisonRecorder(files, NullLogger<VisualComparisonRecorder>.Instance));
    }

    // The address the turn's map carries; comparing reads only its set id and never opens it.
    private static ReferenceSetSandbox Address(string setId) => new(new ResolvedProject { Name = "p" }, Conversation,
        $"reference:{setId}", setId, null!, null!, null!, NullLogger.Instance);

    public Task<IReadOnlyList<ReferenceSetFile>> FilesAsync(string sessionId, string setId, CancellationToken cancellationToken)
    {
        var page = Path.Combine(DockerBackend.RepositoryRoot, "src", "sandbox-images", "browser", "fixture", "compare", $"{setId}.html");
        return Task.FromResult<IReadOnlyList<ReferenceSetFile>>([new ReferenceSetFile("index.html", File.ReadAllBytes(page))]);
    }

    public Task<IReadOnlyList<string>> SetIdsAsync(string sessionId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>(["reference", "candidate"]);

    public ToolImageDepositResult Deposit(ToolImage image)
    {
        _deposited.Add(image);
        return ToolImageDepositResult.Accepted;
    }
}
