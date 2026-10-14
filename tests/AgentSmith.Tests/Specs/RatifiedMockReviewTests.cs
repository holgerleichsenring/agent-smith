using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Browser;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.Browser;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-10-13-8762s: the run's master is told which mock a person ratified, and the diff review compares a
/// static page the spec built with it — render and compare tools, three more iterations and one more look —
/// or the step result says why it did not: no static page, no browser, not beside the spec, or offered and
/// not compared. Without a ratified mock nothing changes.
/// </summary>
public sealed class RatifiedMockReviewTests
{
    private const string Key = "api";
    private const string SpecId = "2026-10-13-8762s";
    private static readonly string Mock = $"{SeriesPaths.Planned}/{SpecId}-mock-v2.html";
    private readonly BrowserRenderFixture _browser = new();

    [Fact]
    public void SpecPromptSection_RatifiedMock_IsNamedAsTheTarget()
    {
        var other = $"{SeriesPaths.Planned}/{SpecId}-mock-v1.html";
        var spec = Spec([other, Mock]);

        var text = SpecPromptSection.Build(Set(spec), spec);

        text.Should().Contain($"- `{Mock}` — ratified by a person — build against this one")
            .And.Contain($"- `{other}`\n").And.NotContain($"`{other}` — ratified");
    }

    [Fact]
    public async Task ReviewSpecDiff_RatifiedMockAndStaticPageInDiff_ReviewerGetsCompareAndCapPlusThree()
    {
        var capture = new Capture();
        var pipeline = Pipeline(browser: true);

        var result = await Handle(capture, pipeline);

        capture.MaxIterations.Should().Be(SpecDiffReviewer.MaxIterations + RatifiedMockOffer.ExtraIterations);
        capture.Tools.Should().Contain(_browser.Factory().Tools(pipeline, false).Select(t => t.Name))
            .And.Contain("compare_reference");
        capture.Prompt.Should().Contain("## The ratified design mock").And.Contain($"`{Mock}`")
            .And.Contain("- `api/web/index.html`").And.Contain("YOUR READ");
        result.Message.Should().Contain("Ratified mock v2 offered, not compared.");
    }

    [Fact]
    public async Task ReviewSpecDiff_RatifiedMock_LookAllowanceGrowsByOne()
    {
        var offered = new Capture();
        var plain = new Capture();

        await Handle(offered, Pipeline(browser: true));
        await Handle(plain, Pipeline(browser: true, ratified: false));

        offered.Prompt.Should().Contain($"up to {DerivationLookTerms.SpecReviewAllowance + 1} looks");
        plain.Prompt.Should().Contain($"up to {DerivationLookTerms.SpecReviewAllowance} looks");
    }

    [Fact]
    public async Task ReviewSpecDiff_HtmlUnderAgentsmith_IsNoCandidate()
    {
        var capture = new Capture();

        var result = await Handle(capture, Pipeline(browser: true, pages: [Mock, ".agentsmith/specs/planned/x.html"]));

        capture.Tools.Should().NotContain("compare_reference");
        result.Message.Should().Contain("Ratified mock v2 not compared: the spec built no static page.");
    }

    [Fact]
    public async Task ReviewSpecDiff_NoStaticPageInDiff_SaysNotComparedAndAddsNoTools()
    {
        var capture = new Capture();

        var result = await Handle(capture, Pipeline(browser: true, pages: ["src/Api/Handler.cs"]));

        capture.Tools.Should().NotContain("compare_reference").And.NotContain("render_reference");
        capture.MaxIterations.Should().Be(SpecDiffReviewer.MaxIterations);
        result.Message.Should().Contain("not compared: the spec built no static page");
    }

    [Fact]
    public async Task ReviewSpecDiff_ToolsOfferedNoComparisonRecorded_SaysOfferedNotCompared()
    {
        var compared = new Capture { Compares = true };
        var silent = new Capture();

        var made = await Handle(compared, Pipeline(browser: true));
        var notMade = await Handle(silent, Pipeline(browser: true));

        made.Message.Should().Contain("Ratified mock v2 compared with the built page.");
        notMade.Message.Should().Contain("Ratified mock v2 offered, not compared.");
    }

    [Fact]
    public async Task ReviewSpecDiff_RatifiedMockWithoutBrowser_SaysNotComparedAndStillReviews()
    {
        var capture = new Capture();

        var result = await Handle(capture, Pipeline(browser: false));

        capture.Prompt.Should().NotBeNull("the review is still asked");
        capture.Tools.Should().NotContain("compare_reference");
        result.Message.Should().Contain("kept no finding").And.Contain("Ratified mock v2 not compared: the run has no browser.");
    }

    [Fact]
    public async Task ReviewSpecDiff_NoRatifiedMock_ToolsUnchanged()
    {
        var capture = new Capture();
        var pipeline = Pipeline(browser: true, ratified: false);

        var result = await Handle(capture, pipeline);

        capture.Tools.Should().NotContain(_browser.Factory().Tools(pipeline, false).Select(t => t.Name));
        capture.MaxIterations.Should().Be(SpecDiffReviewer.MaxIterations);
        capture.Prompt.Should().NotContain("ratified design mock");
        result.Message.Should().NotContain("Ratified mock");
    }

    private Task<CommandResult> Handle(Capture capture, PipelineContext pipeline)
    {
        capture.Pipeline = pipeline;
        return ReviewSpecDiffHandlerTests.Handler(capture, new RatifiedMockReview(_browser.Factory()))
            .ExecuteAsync(new ReviewSpecDiffContext(new AgentConfig(), pipeline), CancellationToken.None);
    }

    private static DerivedSpec Spec(IReadOnlyList<string> mocks, bool ratified = true) =>
        new(new SpecDraft(SpecId, "goal", "spec: x\n", []) { RatifiedMock = ratified ? 2 : null }, "page", string.Empty, [], mocks);

    private static SpecSet Set(DerivedSpec spec) =>
        new("api-1", [spec], SpecAccounting.Empty, [new SpecRevision(1, "approval", DateTimeOffset.UtcNow)], SpecSource.Approved);

    private static PipelineContext Pipeline(bool browser, bool ratified = true, IReadOnlyList<string>? pages = null)
    {
        var pipeline = new PipelineContext();
        var spec = Spec([Mock], ratified);
        pipeline.Set(ContextKeys.CurrentSpec, spec.Draft);
        pipeline.Set(ContextKeys.SpecSet, Set(spec));
        pipeline.Set(ContextKeys.ProjectConfig, new ResolvedProject
        {
            Name = "p", Sandbox = new SandboxConfig { Browser = new ProjectBrowserConfig { Enabled = browser } },
        });
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes,
            new Dictionary<string, ISandbox> { [Key] = new DiffSandbox(pages ?? ["web/index.html", Mock]) });
        pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos, new Dictionary<string, string> { [Key] = "api" });
        pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(ContextKeys.SandboxDiscoveries, new Dictionary<string, RemoteContextDiscovery>());
        pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.SpecStartHeads, new Dictionary<string, string> { [Key] = "spec-start-sha" });
        return pipeline;
    }

    /// <summary>A chat client that keeps the prompt, the tool names and the cap it was built with; when
    /// <see cref="Compares"/> it records one comparison the way compare_reference does.</summary>
    private sealed class Capture : Contracts.Services.IChatClientFactory, IChatClient
    {
        public string? Prompt { get; private set; }
        public List<string> Tools { get; } = [];
        public int? MaxIterations { get; private set; }
        public bool Compares { get; init; }
        public PipelineContext? Pipeline { get; set; }

        public IChatClient Create(AgentConfig agent, Contracts.Providers.TaskType task, int? maxIterations = null,
            Contracts.Providers.MasterLoopHooks? masterLoopHooks = null)
        {
            MaxIterations = maxIterations;
            return this;
        }

        public int GetMaxOutputTokens(AgentConfig agent, Contracts.Providers.TaskType task) => 4096;

        public string GetModel(AgentConfig agent, Contracts.Providers.TaskType task) => "stub-model";

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Prompt = string.Join("\n", messages.Select(m => m.Text));
            Tools.AddRange(options?.Tools?.Select(t => t.Name) ?? []);
            if (Compares) Pipeline?.Set<IReadOnlyList<VisualComparison>>(ContextKeys.VisualComparisons,
                [new VisualComparison("r", "c", [], [])]);
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "[]")));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    private sealed class DiffSandbox(IReadOnlyList<string> paths) : ISandbox
    {
        public string JobId => "diff-sandbox";

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? progress, CancellationToken cancellationToken) =>
            Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, ExitCode: 0, TimedOut: false,
                DurationSeconds: 0.01, ErrorMessage: null, OutputContent: string.Concat(paths.Select(p =>
                    $"diff --git a/{p} b/{p}\n--- a/{p}\n+++ b/{p}\n@@ -1 +1 @@\n+<p>x</p>\n"))));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
