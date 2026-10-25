using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Tests.Handlers;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-10-25-54beb: one owner of a run's template scopes — opened once per address, borrowed by
/// the master and the diff reviewer's look, disposed only when the run ends.
/// </summary>
public sealed class RunTemplateScopesTests
{
    [Fact]
    public void TemplateScopeCache_SecondLook_ReusesScope()
    {
        var (owner, factory, pipeline) = Owned();

        var first = owner.Borrow(pipeline);
        var second = owner.Borrow(pipeline, ["default"]);

        second["template:default"].Should().BeSameAs(first["template:default"]);
        factory.Created.Should().Be(1, "a second look must not clone the template again");
    }

    [Fact]
    public async Task TemplateScopeOwner_ParallelBorrowers_MaterialiseOnce()
    {
        var (owner, factory, pipeline) = Owned();

        var lent = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() => owner.Borrow(pipeline))));

        factory.Created.Should().Be(1, "parallel reviewers share one scope, and so one clone");
        lent.Select(l => l["template:default"]).Distinct().Should().ContainSingle();
    }

    [Fact]
    public async Task DiffReviewLook_Disposed_TemplateScopeStaysOpen()
    {
        var (owner, factory, pipeline) = Owned();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes, new Dictionary<string, ISandbox>(StringComparer.Ordinal) { ["api"] = new StubSandbox() });
        pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(
            ContextKeys.SandboxDiscoveries, new Dictionary<string, RemoteContextDiscovery>());
        var look = DerivationTestLooks.Factory().OnTerms(
            pipeline, DerivationLookTerms.SpecReview, borrowed: owner.Borrow(pipeline))!;
        look.Templates.Should().ContainKey("template:default");

        await look.DisposeAsync();
        factory.Scopes.Single().Disposed.Should().BeFalse("the look borrowed it");

        await owner.DisposeAsync();
        factory.Scopes.Single().Disposed.Should().BeTrue("the run's end is where it is torn down");
    }

    [Fact]
    public async Task RunTemplateScopes_Own_IsFoundByBorrowersAndRefusesAfterDisposal()
    {
        var (owner, _, pipeline) = Owned();

        RunTemplateScopes.Of(pipeline).Should().BeSameAs(owner);
        await owner.DisposeAsync();

        owner.Invoking(o => o.Borrow(pipeline)).Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void RunTemplateScopes_NoOwner_IsNull() =>
        RunTemplateScopes.Of(new PipelineContext()).Should().BeNull();

    [Fact]
    public void TemplateDeclarations_For_KeysTheOwningDeclarationByAddress() =>
        TemplateDeclarations.For(PipelineWithTemplate())["template:default"].TemplateContext.Should().Be("server");

    [Fact]
    public void TemplateDeclarations_Of_NarrowsToTheNamedContexts() =>
        TemplateDeclarations.Of(CodingMasterTemplateTests.ProjectWithTemplate(), [], ["api"]).Should().BeEmpty();

    [Fact]
    public async Task CodingMaster_RunOwnsTheTemplates_AttachmentBorrowsWithoutDisposing()
    {
        var (owner, factory, pipeline) = Owned();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes, new Dictionary<string, ISandbox>(StringComparer.Ordinal) { ["api"] = new StubSandbox() });
        var draft = new AgentSmith.Contracts.Models.SpecDraft(
            "2026-10-25-54beb", "goal", "yaml", []) { Contexts = ["default"] };
        var master = new AgentSmith.Application.Services.Handlers.MasterTemplateScopes(
            new ProjectTemplateScopes(new CountingFactory(), NullLogger<ProjectTemplateScopes>.Instance),
            CodingMasterTemplateTests.Advice(),
            NullLogger<AgentSmith.Application.Services.Handlers.MasterTemplateScopes>.Instance);

        await (await master.OpenAsync(pipeline, draft, false, CancellationToken.None)).DisposeAsync();

        factory.Scopes.Should().ContainSingle("the master borrowed the run's scope instead of cloning its own")
            .Which.Disposed.Should().BeFalse();
        owner.Borrow(pipeline)["template:default"].Should().BeSameAs(factory.Scopes[0]);
    }

    private static (RunTemplateScopes Owner, CountingFactory Factory, PipelineContext Pipeline) Owned()
    {
        var factory = new CountingFactory();
        var pipeline = PipelineWithTemplate();
        var owner = new RunTemplateScopes(
            new ProjectTemplateScopes(factory, NullLogger<ProjectTemplateScopes>.Instance)).Own(pipeline);
        return (owner, factory, pipeline);
    }

    private static PipelineContext PipelineWithTemplate()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ProjectConfig, CodingMasterTemplateTests.ProjectWithTemplate());
        return pipeline;
    }

    private sealed class CountingFactory : ISourceScopeSandboxFactory
    {
        private int _created;
        public int Created => _created;
        public List<CodingMasterTemplateTests.RecordingScope> Scopes { get; } = [];

        public ISourceScopeSandbox Create(
            ResolvedProject project, RepoConnection repo, string? revision = null, string? conversationId = null)
        {
            Interlocked.Increment(ref _created);
            var scope = new CodingMasterTemplateTests.RecordingScope();
            lock (Scopes) Scopes.Add(scope);
            return scope;
        }
    }
}
