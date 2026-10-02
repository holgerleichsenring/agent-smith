using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services;
using AgentSmith.Tests.Handlers;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.DesignSources;

/// <summary>
/// 2026-10-01-283dg: a design turn reads the root DESIGN.md of each scoped repository remotely,
/// keeps absent and unreadable apart, and the master prompt carries it.
/// </summary>
public sealed class DesignSystemGroundingTests
{
    private const string Design = "---\ncolors:\n  primary: \"#22c55e\"\n---\n\nThe green is the only accent.\n";

    [Fact]
    public async Task DialogGroundingReader_DesignMdPresent_IsCarriedVerbatim()
    {
        var grounding = await Reader(Provider(design: Design)).ReadAsync(Repo(), CancellationToken.None);

        grounding.Design.Documents.Should().ContainSingle().Which.Should().Be(
            new ContextDocument("repo-a", null, null, "DESIGN.md", Design));
        grounding.Design.Unreadable.Should().BeEmpty();
    }

    [Fact]
    public async Task DialogGroundingReader_DesignMdUnreadable_IsReportedNotAbsent()
    {
        var pipeline = Pipeline();

        var result = await Ground(pipeline, Provider(design: null, refused: ["DESIGN.md"]));

        result.IsSuccess.Should().BeTrue("a refused read leaves a conversation that can still answer");
        result.Message.Should().Contain("could not be read: repo-a/DESIGN.md")
            .And.Contain("0 DESIGN.md");
        pipeline.TryGet<IReadOnlyList<ContextDocument>>(ContextKeys.DesignSystem, out _).Should().BeFalse();
    }

    [Fact]
    public async Task DesignTurn_ThisRepositorysDesignMd_ItsColoursReachTheMasterPrompt()
    {
        var design = await File.ReadAllTextAsync(Path.Combine(RepoRoot(), "DESIGN.md"));
        var context = MasterHandlerFixture.BuildContext("design-partner-master", includeTicket: false);
        var pipeline = context.Pipeline;
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, [Repo()]);
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox>(StringComparer.Ordinal) { ["repo-a"] = new Mock<ISandbox>().Object });
        pipeline.Set(ContextKeys.PipelineName, PipelinePresets.SpecDialogName);
        pipeline.Set<IReadOnlyList<SpecDialogTurn>>(ContextKeys.SpecDialogTranscript,
            [new SpecDialogTurn(SpecDialogTurn.UserRole, "which green is our accent?")]);
        await Ground(pipeline, Provider(design: design));
        var loop = new PromptLoop();

        await MasterHandlerFixture.Build(loop, new MasterHandlerFixture.StubPromptCatalog("design-partner-master", "body"),
                subAgents: new MasterHandlerFixture.StubSubAgentRunner(), limits: new LoopLimitsConfig())
            .ExecuteAsync(context, CancellationToken.None);

        loop.SystemPrompt.Should().Contain("## Design system — repo-a")
            .And.Contain("primary: \"#22c55e\"")
            .And.Contain("canvas: \"#fffefb\"");
    }

    [Fact]
    public async Task RemoteFileRead_SingleAsync_AbsentFile_IsAbsentNotUnreadable()
    {
        var read = await Files().SingleAsync(Provider(design: null), "repo-a", "DESIGN.md", CancellationToken.None);

        read.IsAbsent.Should().BeTrue();
    }

    [Fact]
    public async Task RemoteFileRead_TryAsync_RefusedRead_IsRecordedAsUnreadable()
    {
        var unreadable = new List<string>();

        var content = await Files().TryAsync(
            Provider(design: Design, refused: ["DESIGN.md"]), "repo-a", "DESIGN.md", unreadable, CancellationToken.None);

        content.Should().BeNull();
        unreadable.Should().ContainSingle().Which.Should().StartWith("DESIGN.md (");
    }

    private static RemoteFileRead Files() => new(NullLogger<RemoteFileRead>.Instance);

    private static Task<CommandResult> Ground(PipelineContext pipeline, ScriptedSourceProvider provider) =>
        new GroundSpecDialogHandler(Reader(provider), NullLogger<GroundSpecDialogHandler>.Instance)
            .ExecuteAsync(new GroundSpecDialogContext(pipeline), CancellationToken.None);

    private static DialogGroundingReader Reader(ScriptedSourceProvider provider) =>
        new(new ScriptedSourceProviderFactory(
                new Dictionary<string, ScriptedSourceProvider>(StringComparer.Ordinal) { ["repo-a"] = provider }),
            new ContextYamlParser(new ContextYamlSerializer(new ContextYamlBuilders())),
            Files(),
            NullLogger<DialogGroundingReader>.Instance);

    private static ScriptedSourceProvider Provider(string? design, string[]? refused = null)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [".agentsmith/contexts/default/principles.md"] = "# Principles\n",
        };
        if (design is not null) files["DESIGN.md"] = design;
        return new ScriptedSourceProvider(["default"], files, new HashSet<string>(refused ?? [], StringComparer.Ordinal));
    }

    private static RepoConnection Repo() => new() { Name = "repo-a", Url = "https://example.invalid/repo-a.git" };

    private static PipelineContext Pipeline()
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyList<RepoConnection>>(ContextKeys.Repos, [Repo()]);
        return pipeline;
    }

    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "DESIGN.md")))
            dir = Directory.GetParent(dir)?.FullName ?? throw new InvalidOperationException("no DESIGN.md above the tests");
        return dir;
    }

    private sealed class PromptLoop : IAgenticLoopRunner
    {
        public string SystemPrompt { get; private set; } = string.Empty;

        public Task<AgenticLoopResult> RunAsync(AgenticLoopRequest request, CancellationToken ct)
        {
            if (SystemPrompt.Length == 0) SystemPrompt = request.SystemPrompt;
            var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, "The accent is #22c55e."))
            {
                Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5 },
            };
            return Task.FromResult(new AgenticLoopResult(response, TimeSpan.FromSeconds(1)));
        }
    }
}
