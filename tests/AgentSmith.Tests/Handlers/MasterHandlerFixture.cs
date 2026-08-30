using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Net.Http;

namespace AgentSmith.Tests.Handlers;

/// <summary>
/// The real <see cref="AgenticMasterHandler"/>, wired from one place.
/// <para>
/// Extracted so a suite that exercises the handler's LOOP (p0341f) builds the same
/// object as the suite that exercises its prompt — a second hand-rolled copy of a
/// forty-argument constructor is a second thing to keep in step, and the first one to
/// fall behind.
/// </para>
/// </summary>
internal static class MasterHandlerFixture
{
    internal static AgenticMasterHandler Build(
        IAgenticLoopRunner loop, IPromptCatalog prompts, string? masterSchema = null,
        int maxSubAgents = 0) =>
        new(loop, prompts, new NoOpDecisionLogger(), AgentSmithConfig.Empty(),
            new AgentSmith.Infrastructure.Services.ContextYamlSerializer(
                new AgentSmith.Infrastructure.Services.ContextYamlBuilders()),
            AgentSmith.Tests.TestHelpers.ContextGates.Build(),
            AgentSmith.Tests.TestHelpers.ContextGates.Writer(),
            new StubSchemaResolver(masterSchema),
            new AgentSmith.Application.Services.ScanMasterPromptFactory(),
            new AgentSmith.Application.Services.SpecDialogPromptFactory(),
            new AgentSmith.Application.Services.PhaseExecutionPromptFactory(),
            BuildOutcomeResolver(),
            new NoOpTicketDocumentMaterializer(),
            WebTool,
            new AgentSmith.Application.Services.Events.NoOpEventPublisher(),
            new AgentSmith.Application.Services.Resume.NullPriorRunLedgerReader(),
            new AgentSmith.Application.Services.Sandbox.SandboxToolchainProbe(
                NullLogger<AgentSmith.Application.Services.Sandbox.SandboxToolchainProbe>.Instance),
            new SandboxWorkingTreeReader(NullLogger<SandboxWorkingTreeReader>.Instance),
            new AgentSmith.Application.Services.RunWorkCheckpointer(
                new AgentSmith.Application.Services.RepoWorkPusher(
                    new AgentSmith.Application.Services.SandboxGitOperations(
                        new AgentSmith.Application.Services.GitBranchPusher(),
                        NullLogger<AgentSmith.Application.Services.SandboxGitOperations>.Instance,
                        Mock.Of<AgentSmith.Contracts.Sandbox.ISandboxFileReaderFactory>(),
                        new SandboxGitIdentity(NullLogger<SandboxGitIdentity>.Instance)),
                    Mock.Of<AgentSmith.Contracts.Services.ISecretPatternScanner>(),
                    new AgentSmith.Application.Services.Handlers.SandboxTargets(),
                    NullLogger<AgentSmith.Application.Services.RepoWorkPusher>.Instance),
                NullLogger<AgentSmith.Application.Services.RunWorkCheckpointer>.Instance),
            new AgentSmith.Tests.TestHelpers.StubSandboxFileReaderFactory(),
            dialogueTransport: null,
            BuildToolComposer(maxSubAgents),
            NullLogger<AgenticMasterHandler>.Instance);

    /// <summary>2026-08-30-03e1: the master's tool surface, built the way the composition
    /// root builds it — with the real verification lens, so the entries a scan master can
    /// look up here are the entries the shipped binary answers with.</summary>
    private static AgentSmith.Application.Services.Tools.MasterToolComposer BuildToolComposer(
        int maxSubAgents) =>
        new(new AgentSmith.Application.Services.Tools.AgenticToolSurface(),
            new AgentSmith.Application.Services.Tools.ScanStationToolFactory(),
            new AgentSmith.Application.Services.Tools.ScanRequirementToolFactory(
                Lens, new AgentSmith.Application.Services.Tools.CitedFindingRecorder(Lens)),
            new AgentSmith.Application.Services.Tools.EnsureRepoSandboxToolFactory(
                new AgentSmith.Application.Services.Sandbox.UnboundedCapacityProbe(),
                new AgentSmith.Tests.Sandbox.StubSandboxResourceResolver(),
                new SandboxRepoCloner(
                    Mock.Of<AgentSmith.Contracts.Providers.ISourceProviderFactory>(),
                    new SandboxGitIdentity(NullLogger<SandboxGitIdentity>.Instance),
                    AgentSmith.Tests.TestHelpers.TestGit.WorkBranchCheckout,
                    NullLogger<SandboxRepoCloner>.Instance),
                new SandboxTargets()),
            new StubSubAgentRunner(),
            new SubAgentBudget(20),
            new SubAgentNameValidator(),
            new InMemoryChildAnswerStore(),
            new NoOpDecisionLogger(),
            new LoopLimitsConfig { MaxSubAgentsPerRun = maxSubAgents },
            NullLogger<AgentSmith.Application.Services.Tools.MasterToolComposer>.Instance);

    internal static IVerificationLens Lens { get; } =
        new AgentSmith.Infrastructure.Core.Services.Verification.AsvsVerificationLens(
            new AgentSmith.Infrastructure.Core.Services.Verification.EmbeddedVerificationCatalogue(
                new AgentSmith.Infrastructure.Core.Services.Verification.AsvsFlatExportParser()),
            new AgentSmith.Infrastructure.Core.Services.Verification.VerificationLensTableParser());

    internal static AgenticMasterContext BuildContext(
        string masterSkillName,
        bool includeTicket = true,
        string codingPrinciples = "principles",
        int scanMinSourceReads = 6,
        bool supportsVision = true)
    {
        var pipeline = new PipelineContext();
        var sandboxes = new Dictionary<string, ISandbox>(StringComparer.Ordinal)
        {
            ["default"] = new Mock<ISandbox>().Object,
        };
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, sandboxes);
        if (includeTicket)
            pipeline.Set(ContextKeys.Ticket,
                new Ticket(
                    id: new TicketId("TKT-1"), title: "Test ticket", description: "Do the thing",
                    acceptanceCriteria: null, status: "open", source: "test"));
        return new AgenticMasterContext(
            MasterSkillName: masterSkillName,
            Repository: new Repository(new BranchName("feature/x"), "https://example.test/repo.git"),
            CodingPrinciples: codingPrinciples,
            AgentConfig: new AgentConfig
            {
                ScanMinSourceReads = scanMinSourceReads,
                SupportsVision = supportsVision,
            },
            Pipeline: pipeline);
    }

    // p0353: the master takes the web_fetch tool host by DI; a real instance (its
    // HttpClient is never called in these tests) keeps the ctor happy.
    private static readonly AgentSmith.Application.Services.Tools.WebToolHost WebTool =
        new(new HttpClient());

    // p0315e: the real resolver chain over the real schema — the handler gate
    // resolves a typed outcome instead of validating only the draft.
    private static AgentSmith.Application.Services.SpecDialog.OutcomeProposalResolver BuildOutcomeResolver()
    {
        var validator = new AgentSmith.Application.Services.SpecDialog.SpecDraftValidator(
            new AgentSmith.Application.Services.Validation.PhaseSpecSchemaProvider());
        var reader = new AgentSmith.Application.Services.SpecDialog.PhaseDraftReader();
        return new AgentSmith.Application.Services.SpecDialog.OutcomeProposalResolver(
            validator, reader,
            new AgentSmith.Application.Services.SpecDialog.BugOutcomeParser(),
            new AgentSmith.Application.Services.SpecDialog.EpicOutcomeParser(
                validator, reader,
                new AgentSmith.Application.Services.SpecDialog.RequiresEdgeChecker()));
    }

    internal sealed class StubPromptCatalog(string name, string body) : IPromptCatalog
    {
        public string Get(string n) =>
            n == name ? body : throw new InvalidOperationException($"unexpected name {n}");

        public string Render(string n, IReadOnlyDictionary<string, string> tokens)
        {
            var c = Get(n);
            foreach (var (k, v) in tokens) c = c.Replace("{" + k + "}", v);
            return c;
        }
    }

    private sealed class NoOpTicketDocumentMaterializer : ITicketDocumentMaterializer
    {
        public Task<IReadOnlyList<MaterializedTicketDocument>> MaterializeAsync(
            ISandbox sandbox, string runRecordDir,
            IReadOnlyList<TicketDocumentAttachment> documents,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<MaterializedTicketDocument>>([]);
    }

    private sealed class StubSchemaResolver(string? schema) : IMasterOutputSchemaResolver
    {
        public string? Resolve(string masterSkillName) => schema;
    }

    private sealed class StubSubAgentRunner : ISubAgentRunner
    {
        public Task<IReadOnlyList<SubAgentResult>> RunAsync(
            IReadOnlyList<SubAgentSpec> specs, SubAgentContext context, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<SubAgentResult>>([]);
    }

    internal sealed class NoOpDecisionLogger : IDecisionLogger
    {
        public Task LogAsync(string? repoPath, DecisionCategory category, string decision,
            CancellationToken cancellationToken = default, string? sourceLabel = null) => Task.CompletedTask;
    }
}
