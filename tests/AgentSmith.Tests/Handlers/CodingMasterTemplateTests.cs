using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// 2026-09-13-6f35: the coding master can read the template its phase is built after. The
/// entry is addressable by name, refused on write and on command BY THE SANDBOX, and absent
/// from the sandbox map every downstream handler iterates.
/// </summary>
public sealed class CodingMasterTemplateTests
{
    private const string Target = "Sample.Server";
    private const string Address = "template:default";

    [Fact]
    public async Task CodingMaster_DeclaredTemplate_IsAddressableByName()
    {
        var (host, template) = await AttachedHostAsync();

        var result = await host.ReadFile($"{Address}/src/Api/Order.cs");

        result.Should().NotStartWith("Error");
        template.Reads.Should().ContainSingle().Which.Should().Be("src/Api/Order.cs",
            "the prefix identifies the entry and is stripped before the read reaches it");
    }

    [Fact]
    public async Task CodingMaster_TemplateEntry_NotInPipelineSandboxes()
    {
        var pipeline = PipelineWithTemplate(out var scope);

        await using var attachment = await Scopes(scope).OpenAsync(
            pipeline, Draft(), readOnlySurface: false, CancellationToken.None);

        attachment.Names.Should().Equal(Address);
        // CommitAndPR, the toolchain probe and the preflight checks all iterate this map.
        pipeline.Get<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes).Keys
            .Should().ContainSingle().Which.Should().Be(Target);
        pipeline.Get<IReadOnlyList<string>>(ContextKeys.TemplateAddresses).Should().Equal(Address);
    }

    [Fact]
    public async Task CodingMaster_WriteIntoTemplate_IsRefusedBySandbox()
    {
        var host = HostWithRealScope(out var spawns);

        var result = await host.WriteFile($"{Address}/src/Api/Order.cs", "// mine now");

        result.Should().StartWith("Error").And.Contain("read-only");
        spawns.Spawned.Should().BeEmpty("a step refused by KIND never reaches a container");
    }

    [Fact]
    public async Task CodingMaster_RunCommandInTemplate_IsRefusedBySandbox()
    {
        var host = HostWithRealScope(out var spawns);

        var result = await host.RunCommand("rm -rf .", repo: Address);

        result.Should().Contain("read-only");
        spawns.Spawned.Should().BeEmpty();
    }

    [Fact]
    public async Task CodingMaster_RefusedTemplateWrite_IsNotRecordedAsAChange()
    {
        var host = HostWithRealScope(out _);

        await host.WriteFile($"{Address}/src/Api/Order.cs", "// mine now");

        host.GetChanges().Should().BeEmpty(
            "a refused write that still counted as a change would put a foreign path "
            + "in the commit comment and the run result");
    }

    [Fact]
    public async Task CodingMaster_NoTemplateDeclared_AddressMapUnchanged()
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes, new Dictionary<string, ISandbox>(StringComparer.Ordinal)
            { [Target] = new Mock<ISandbox>().Object });

        await using var attachment = await Scopes(new RecordingScope()).OpenAsync(
            pipeline, Draft(), readOnlySurface: false, CancellationToken.None);

        attachment.Names.Should().BeEmpty();
        pipeline.Has(ContextKeys.TemplateAddresses).Should().BeFalse(
            "a project with no template addresses exactly what it addressed before");
    }

    [Fact]
    public void CodingMaster_SingleRepoPlusTemplate_RepositorySectionAppears()
    {
        // The section is empty at one name or fewer, so the commonest project shape — one
        // repository — flips from no section to a section the moment a template is declared,
        // and the master must prefix the TARGET's paths too.
        MasterPromptSections.BuildRepoNamesSection([Target]).Should().BeEmpty();

        var section = MasterPromptSections.BuildRepoNamesSection([Target, Address]);

        section.Should().Contain("Repositories in this run");
        section.Should().Contain($"`{Target}`");
        section.Should().Contain($"`{Address}`");
    }

    [Fact]
    public async Task CodingMaster_TemplateUnreachable_NamesTheTemplateAndRevision()
    {
        var pipeline = PipelineWithTemplate(out _);
        var sut = Scopes(new UnreachableScope());

        var act = () => sut.OpenAsync(pipeline, Draft(), readOnlySurface: false, CancellationToken.None);

        var failure = await act.Should().ThrowAsync<InvalidOperationException>();
        failure.Which.Message.Should().Contain("reference-project").And.Contain("v4.2.0",
            "which template and which revision are different repairs");
        pipeline.Has(ContextKeys.TemplateAddresses).Should().BeFalse();
    }

    [Fact]
    public async Task CodingMaster_TemplateUnreachable_DisposesWhatItOpened()
    {
        var pipeline = PipelineWithTemplate(out _);
        var scope = new UnreachableScope();

        try { await Scopes(scope).OpenAsync(pipeline, Draft(), false, CancellationToken.None); }
        catch (InvalidOperationException) { /* the phase fails; the clone must not leak */ }

        scope.Disposed.Should().BeTrue();
    }

    [Fact]
    public async Task CodingMaster_ReadOnlySurface_OpensNoTemplate()
    {
        var pipeline = PipelineWithTemplate(out var scope);

        await using var attachment = await Scopes(scope).OpenAsync(
            pipeline, Draft(), readOnlySurface: true, CancellationToken.None);

        attachment.Names.Should().BeEmpty("a scan and a dialog turn write no code");
        scope.Materialized.Should().BeFalse();
    }

    [Fact]
    public async Task CodingMaster_PhaseChangesAnotherContext_TemplateIsNotOpened()
    {
        var pipeline = PipelineWithTemplate(out var scope);

        await using var attachment = await Scopes(scope).OpenAsync(
            pipeline, Draft("api"), readOnlySurface: false, CancellationToken.None);

        attachment.Names.Should().BeEmpty(
            "a template declared for a context this phase never touches is a clone nobody asked for");
    }

    [Fact]
    public async Task Attachment_Disposed_TearsDownTheScopesItOwns()
    {
        var pipeline = PipelineWithTemplate(out var scope);
        var attachment = await Scopes(scope).OpenAsync(
            pipeline, Draft(), false, CancellationToken.None);

        await attachment.DisposeAsync();

        scope.Disposed.Should().BeTrue();
    }

    [Fact]
    public void TemplateSection_NoTemplate_RendersNothing() =>
        TemplatePromptSection.Build([]).Should().BeEmpty();

    [Fact]
    public void TemplateSection_Template_SaysItAnswersHowAndIsReadOnly()
    {
        var section = TemplatePromptSection.Build([Address]);

        section.Should().Contain(Address);
        section.Should().Contain("READ-ONLY");
        section.Should().Contain("HOW");
        section.Should().Contain("phase spec");
    }

    // ---- through the handler -----------------------------------------------

    [Fact]
    public async Task CodingMaster_TemplateUnreachable_FailsBeforeFirstToken()
    {
        var loop = new RecordingLoop();
        var handler = MasterHandlerFixture.Build(
            loop, new MasterHandlerFixture.StubPromptCatalog("coding-agent-master", "body"),
            templateScopes: new FixedScopeFactory(new UnreachableScope()));
        var context = MasterHandlerFixture.BuildContext("coding-agent-master");
        context.Pipeline.Set(ContextKeys.ProjectConfig, ProjectWithTemplate());

        var act = () => handler.ExecuteAsync(context, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        loop.SeenRequests.Should().BeEmpty("the phase fails before the master spends a token");
    }

    [Fact]
    public async Task CodingMaster_DeclaredTemplate_ReachesTheSystemPrompt()
    {
        var loop = new RecordingLoop();
        var handler = MasterHandlerFixture.Build(
            loop, new MasterHandlerFixture.StubPromptCatalog("coding-agent-master", "body"),
            templateScopes: new FixedScopeFactory(new RecordingScope()));
        var context = MasterHandlerFixture.BuildContext("coding-agent-master");
        context.Pipeline.Set(ContextKeys.ProjectConfig, ProjectWithTemplate());

        await handler.ExecuteAsync(context, CancellationToken.None);

        loop.SeenRequests.Should().ContainSingle();
        loop.SeenRequests[0].SystemPrompt.Should().Contain(Address)
            .And.Contain("READ-ONLY");
    }

    private sealed class RecordingLoop : IAgenticLoopRunner
    {
        private readonly List<AgenticLoopRequest> _seen = [];
        public IReadOnlyList<AgenticLoopRequest> SeenRequests => _seen;

        public Task<AgenticLoopResult> RunAsync(AgenticLoopRequest request, CancellationToken ct)
        {
            _seen.Add(request);
            var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok"))
            {
                Usage = new UsageDetails { InputTokenCount = 10, OutputTokenCount = 5 },
            };
            return Task.FromResult(new AgenticLoopResult(response, TimeSpan.FromSeconds(1)));
        }
    }

    // ---- composition -------------------------------------------------------

    internal static MasterTemplateScopes Scopes(ISourceScopeSandbox scope) =>
        new(new ProjectTemplateScopes(
                new FixedScopeFactory(scope), NullLogger<ProjectTemplateScopes>.Instance),
            NullLogger<MasterTemplateScopes>.Instance);

    private static PhaseDraft Draft(string context = "default") =>
        new("2026-09-13-6f35", "goal", "yaml", []) { Contexts = [context] };

    private static PipelineContext PipelineWithTemplate(out RecordingScope scope)
    {
        scope = new RecordingScope();
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes, new Dictionary<string, ISandbox>(StringComparer.Ordinal)
            { [Target] = new Mock<ISandbox>().Object });
        pipeline.Set(ContextKeys.ProjectConfig, ProjectWithTemplate());
        return pipeline;
    }

    internal static ResolvedProject ProjectWithTemplate() => new()
    {
        Name = "sample",
        Templates =
        [
            new ProjectTemplate("default", "server", "v4.2.0", new RepoConnection
            {
                Name = "reference-project", Type = RepoType.GitHub,
                Url = "https://stub.test/reference-project",
            }),
        ],
    };

    private static async Task<(FilesystemToolHost Host, RecordingScope Template)> AttachedHostAsync()
    {
        var pipeline = PipelineWithTemplate(out var scope);
        var attachment = await Scopes(scope).OpenAsync(
            pipeline, Draft(), false, CancellationToken.None);
        var host = TargetHost();
        attachment.AttachTo(host);
        return (host, scope);
    }

    // The real read-only sandbox, so the refusal under test is the product's and not a stub's.
    private static FilesystemToolHost HostWithRealScope(out StubSandboxFactory spawns)
    {
        spawns = new StubSandboxFactory();
        var runContext = new Mock<IRunContextAccessor>();
        runContext.SetupGet(r => r.CurrentRunId).Returns("run-1");
        var scope = new SourceScopeSandbox(
            ProjectWithTemplate(), ProjectWithTemplate().Templates[0].Repo, "v4.2.0",
            hold: null,
            new SourceScopeOpener(
                new SourceScopeMaterialiser(new SourceScopeRefresh()), spawns,
                new SandboxSpecBuilder(
                    new AgentSmith.Tests.Sandbox.StubSandboxResourceResolver(),
                    Mock.Of<IAgentImageResolver>(
                        r => r.Resolve(It.IsAny<ResolvedProject>()) == "agent:test")),
                runContext.Object),
            new AsyncLocalSourceScopeObserverAccessor(), NullLogger<SourceScopeSandbox>.Instance);
        var host = TargetHost();
        new MasterTemplateAttachment(
            new Dictionary<string, ISourceScopeSandbox>(StringComparer.Ordinal) { [Address] = scope })
            .AttachTo(host);
        return host;
    }

    private static FilesystemToolHost TargetHost()
    {
        var sandbox = new Mock<ISandbox>();
        sandbox.Setup(s => s.RunStepAsync(
                It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
                Task.FromResult(new StepResult(
                    StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null, "ok")));
        return new FilesystemToolHost(
            new Dictionary<string, ISandbox>(StringComparer.Ordinal) { [Target] = sandbox.Object },
            defaultRepo: Target);
    }

    internal sealed class FixedScopeFactory(ISourceScopeSandbox scope) : ISourceScopeSandboxFactory
    {
        public ISourceScopeSandbox Create(
            ResolvedProject project, RepoConnection repo, string? revision = null,
            string? conversationId = null) => scope;
    }

    internal sealed class RecordingScope : ISourceScopeSandbox
    {
        public bool Disposed { get; private set; }
        public bool Materialized { get; private set; }
        public List<string> Reads { get; } = [];
        public List<StepKind> Ran { get; } = [];
        public string RepoName => "reference-project";
        public bool IsMaterialized => Materialized;
        public string? ResolvedSha => Materialized ? "abc123" : null;
        public string JobId => "template-scope";

        public Task<string> MaterializeAsync(CancellationToken ct)
        {
            Materialized = true;
            return Task.FromResult("abc123");
        }

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? p, CancellationToken ct)
        {
            Ran.Add(step.Kind);
            if (step.Path is not null) Reads.Add(step.Path);
            return Task.FromResult(new StepResult(
                StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null, "// template"));
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class UnreachableScope : ISourceScopeSandbox
    {
        public bool Disposed { get; private set; }
        public string RepoName => "reference-project";
        public bool IsMaterialized => false;
        public string? ResolvedSha => null;
        public string JobId => "template-scope";

        public Task<string> MaterializeAsync(CancellationToken ct) =>
            throw new SourceScopeUnavailableException(
                SourceScopeFailureKind.NoSuchRevision, "reference-project", "v4.2.0",
                "no such revision 'v4.2.0'");

        public Task<StepResult> RunStepAsync(Step step, IProgress<StepEvent>? p, CancellationToken ct) =>
            throw new InvalidOperationException("never reached");

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
