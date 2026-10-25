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
        int maxSubAgents = 0, ISourceScopeSandboxFactory? templateScopes = null,
        ISubAgentRunner? subAgents = null, LoopLimitsConfig? limits = null,
        AgentSmith.Contracts.Providers.IFigmaClient? figma = null,
        AgentSmith.Application.Services.Tools.RenderReferenceToolFactory? render = null,
        AgentSmith.Contracts.Events.IEventPublisher? passEvents = null,
        AgentSmith.Contracts.Events.IRunContextAccessor? runContext = null,
        IDecisionLogger? decisions = null,
        AgentSmith.Contracts.Events.IEventPublisher? recordEvents = null) =>
        new(loop, prompts, RecordHosts(decisions ?? new NoOpDecisionLogger(), recordEvents), AgentSmithConfig.Empty(),
            new AgentSmith.Infrastructure.Services.ContextYamlSerializer(
                new AgentSmith.Infrastructure.Services.ContextYamlBuilders()),
            AgentSmith.Tests.TestHelpers.ContextGates.Build(),
            AgentSmith.Tests.TestHelpers.ContextGates.Writer(),
            AgentSmith.Tests.TestHelpers.ContextGates.DerivationStamp(), AgentSmith.Tests.TestHelpers.ContextGates.ImageReview(),
            new StubSchemaResolver(masterSchema),
            new AgentSmith.Application.Services.ScanMasterPromptFactory(),
            BuildScanPasses(loop),
            new AgentSmith.Application.Services.SpecExecutionPromptFactory(),
            TurnFlow(loop),
            new NoOpTicketDocumentMaterializer(),
            // 2026-09-13-6f35: no template declared unless a test hands one in — the default
            // factory throws, so a run that reaches it by accident says so instead of passing.
            new AgentSmith.Application.Services.Handlers.MasterTemplateScopes(
                new AgentSmith.Application.Services.Specs.ProjectTemplateScopes(
                    templateScopes ?? new NoTemplateScopes(),
                    NullLogger<AgentSmith.Application.Services.Specs.ProjectTemplateScopes>.Instance),
                NullLogger<AgentSmith.Application.Services.Handlers.MasterTemplateScopes>.Instance),
            WebTool,
            new AgentSmith.Application.Services.Events.NoOpEventPublisher(),
            new AgentSmith.Application.Services.Resume.PriorRunSeedSource(
                new AgentSmith.Application.Services.Resume.NullPriorRunLedgerReader(),
                NullLogger<AgentSmith.Application.Services.Resume.PriorRunSeedSource>.Instance),
            new AgentSmith.Application.Services.Sandbox.SandboxToolchainProbe(
                new ContextVerifyStagesResolver(),
                new AgentSmith.Application.Services.Sandbox.ToolchainFindingReporter(
                    NullLogger<AgentSmith.Application.Services.Sandbox.ToolchainFindingReporter>.Instance),
                NullLogger<AgentSmith.Application.Services.Sandbox.SandboxToolchainProbe>.Instance),
            Reengagement(loop, Recorder(passEvents, runContext)), Recorder(passEvents, runContext),
            new AgentSmith.Application.Services.RunWorkCheckpointer(
                new AgentSmith.Application.Services.RepoWorkPusher(
                    new AgentSmith.Application.Services.SandboxGitOperations(
                        new AgentSmith.Application.Services.GitBranchPusher(), AgentSmith.Tests.TestSupport.TestGitCredentials.Resolver,
                        NullLogger<AgentSmith.Application.Services.SandboxGitOperations>.Instance,
                        Mock.Of<AgentSmith.Contracts.Sandbox.ISandboxFileReaderFactory>(),
                        new SandboxGitIdentity(NullLogger<SandboxGitIdentity>.Instance)),
                    Mock.Of<AgentSmith.Contracts.Services.ISecretPatternScanner>(),
                    new AgentSmith.Application.Services.Handlers.SandboxTargets(),
                    NullLogger<AgentSmith.Application.Services.RepoWorkPusher>.Instance),
                NullLogger<AgentSmith.Application.Services.RunWorkCheckpointer>.Instance),
            AgentSmith.Tests.TestHelpers.MemoryStandings.IndexReader(Repositories()),
            dialogueTransport: null,
            Composition(maxSubAgents, subAgents, limits, figma, render),
            AgentSmith.Tests.TestHelpers.TurnActivityRecorder.Tools(),
            NullLogger<AgenticMasterHandler>.Instance);

    /// <summary>The dialog turn's prompt and reply handling: both prompt contracts, the outcome gate and the
    /// proposal review over the same loop — no reviewer call unless a test proposes.</summary>
    internal static AgentSmith.Application.Services.SpecDialog.SpecDialogTurnFlow TurnFlow(IAgenticLoopRunner loop) =>
        new(new AgentSmith.Application.Services.SpecDialogPromptFactory(),
            new AgentSmith.Application.Services.SpecDialog.TicketStewardPromptFactory(),
            OutcomeGate(loop),
            new AgentSmith.Application.Services.SpecDialog.SpecDialogProposalReview(
                Mock.Of<AgentSmith.Application.Services.Specs.ISpecCutReviewer>(),
                AgentSmith.Tests.Specs.DerivationTestLooks.Factory(),
                AgentSmith.Tests.SpecDialog.ProposalEvidenceTestReview.Create(),
                // 2026-10-13-8762i: rounds 0 — the single review; the rounds have their own rig.
                AgentSmith.Tests.SpecDialog.ProposalReview.ProposalReviewRig.Reader(
                    () => AgentSmith.Tests.SpecDialog.ProposalReview.ProposalReviewRig.Of(rounds: 0)),
                new AgentSmith.Tests.SpecDialog.ProposalReview.ProposalReviewRig().Loop(),
                AgentSmith.Tests.SpecDialog.ProposalReview.TicketPlanReviewRig.Default(),
                AgentSmith.Tests.TestHelpers.TurnActivityRecorder.Silent(),
                NullLogger<AgentSmith.Application.Services.SpecDialog.SpecDialogProposalReview>.Instance));

    /// <summary>The design turn's outcome gate over the given loop, with the real resolver and refusal.</summary>
    internal static AgentSmith.Application.Services.SpecDialog.SpecDialogOutcomeGate OutcomeGate(IAgenticLoopRunner loop) =>
        new(loop, new AgentSmith.Application.Services.SpecDialogPromptFactory(), BuildOutcomeResolver(),
            new AgentSmith.Application.Services.SpecDialog.SpecDialogProposalRefusal(
                loop, new AgentSmith.Application.Services.SpecDialogPromptFactory(),
                AgentSmith.Tests.TestHelpers.TurnActivityRecorder.Silent(),
                NullLogger<AgentSmith.Application.Services.SpecDialog.SpecDialogProposalRefusal>.Instance),
            AgentSmith.Tests.TestHelpers.TurnActivityRecorder.Silent(),
            NullLogger<AgentSmith.Application.Services.SpecDialog.SpecDialogOutcomeGate>.Instance);

    /// <summary>2026-10-13-8762m: the record tools of a master start, over the stub repositories.</summary>
    internal static AgentSmith.Application.Services.Tools.MasterRecordToolHosts RecordHosts(
        IDecisionLogger decisions, AgentSmith.Contracts.Events.IEventPublisher? events = null) =>
        new(decisions, Repositories(), events ?? new AgentSmith.Application.Services.Events.NoOpEventPublisher(),
            new AgentSmith.Application.Services.Memory.MemoryCitationResolver(new()),
            NullLogger<AgentSmith.Application.Services.Tools.MasterRecordToolHosts>.Instance);

    private static AgentSmith.Application.Services.Repositories.RunRepositoriesFactory Repositories() =>
        new(new SandboxTargets(), new AgentSmith.Tests.TestHelpers.StubSandboxFileReaderFactory(),
            new SandboxWorkingTreeReader(NullLogger<SandboxWorkingTreeReader>.Instance));

    /// <summary>2026-10-13-9a59f: the open loop the handler drives, over the same loop runner.</summary>
    internal static MasterReengagementLoop Reengagement(
        IAgenticLoopRunner loop, AgentSmith.Application.Services.Passes.MasterPassRecorder recorder) =>
        new(new ReengagePass(loop, new SandboxWorkingTreeReader(NullLogger<SandboxWorkingTreeReader>.Instance),
                new ReengageStopLog(NullLogger<ReengageStopLog>.Instance), NullLogger<ReengagePass>.Instance),
            recorder, new ReengageStopLog(NullLogger<ReengageStopLog>.Instance), Objection());

    /// <summary>2026-10-17-a196c: the real claim objection over the real collaborators.</summary>
    internal static AcceptanceClaimObjection Objection() =>
        new(new AcceptanceClaimEvidence(new SpecSandboxes(new SandboxTargets()), AgentSmith.Tests.TestHelpers.TestGit.Delivery,
                new SandboxWorkingTreeReader(NullLogger<SandboxWorkingTreeReader>.Instance),
                NullLogger<AcceptanceClaimEvidence>.Instance),
            new AcceptanceClaimCheck());

    /// <summary>2026-10-13-9a59f: no spec frame by default, so a handler test records no pass.</summary>
    internal static AgentSmith.Application.Services.Passes.MasterPassRecorder Recorder(
        AgentSmith.Contracts.Events.IEventPublisher? events = null,
        AgentSmith.Contracts.Events.IRunContextAccessor? runContext = null) =>
        new(events ?? new AgentSmith.Application.Services.Events.NoOpEventPublisher(),
            runContext ?? new AgentSmith.Application.Services.Events.AsyncLocalRunContextAccessor(),
            TimeProvider.System, NullLogger<AgentSmith.Application.Services.Passes.MasterPassRecorder>.Instance);

    /// <summary>
    /// The real <see cref="MasterToolComposition"/> the handler holds — the sub-agent
    /// collaborators and the escalation factory live here since the composition moved out.
    /// </summary>
    private static MasterToolComposition Composition(
        int maxSubAgents, ISubAgentRunner? subAgents, LoopLimitsConfig? limits,
        AgentSmith.Contracts.Providers.IFigmaClient? figma,
        AgentSmith.Application.Services.Tools.RenderReferenceToolFactory? render) =>
        new(new AgentSmith.Application.Services.Tools.AgenticToolSurface(),
            new AgentSmith.Application.Services.Tools.EnsureRepoSandboxToolFactory(
                new AgentSmith.Application.Services.Sandbox.UnboundedCapacityProbe(),
                new AgentSmith.Tests.Sandbox.StubSandboxResourceResolver(),
                new SandboxRepoCloner(
                    Mock.Of<AgentSmith.Contracts.Providers.ISourceProviderFactory>(), AgentSmith.Tests.TestSupport.TestGitCredentials.Resolver,
                    new SandboxGitIdentity(NullLogger<SandboxGitIdentity>.Instance),
                    AgentSmith.Tests.TestHelpers.TestGit.WorkBranchCheckout, NullLogger<SandboxRepoCloner>.Instance),
                new SandboxTargets(),
                AgentSmith.Tests.Spawning.CapacityTestDoubles.NoHolds()),
            limits ?? new LoopLimitsConfig { MaxSubAgentsPerRun = maxSubAgents },
            new SubAgentBudget(20),
            subAgents ?? new StubSubAgentRunner(),
            new SubAgentNameValidator(),
            new NoOpDecisionLogger(),
            new InMemoryChildAnswerStore(),
            // 2026-10-01-7f7ab: a project with no figma source never reaches the client.
            new AgentSmith.Application.Services.Tools.DesignReadToolHostFactory(
                figma ?? Mock.Of<AgentSmith.Contracts.Providers.IFigmaClient>(MockBehavior.Strict),
                AgentSmith.Tests.DesignSources.DesignImageFakes.NoLoopDeposit(),
                new AgentSmith.Application.Services.Events.NoOpEventPublisher(),
                NullLogger<AgentSmith.Application.Services.Tools.DesignReadToolHostFactory>.Instance),
            // 2026-10-01-283de: a turn that seeds no project never builds the host.
            render ?? new AgentSmith.Tests.Browser.BrowserRenderFixture(spawnsContainers: false).Factory(),
            new AgentSmith.Application.Services.Tools.TicketActToolsFactory(Verification()),
            AgentSmith.Tests.TestHelpers.TurnActivityRecorder.Tools(),
            AgentSmith.Tests.TestHelpers.TestTreeSync.GateStage(),
            AgentSmith.Tests.TestHelpers.TestSanity.Wrapper(),
            NullLogger<MasterToolComposition>.Instance);

    /// <summary>The series verification a design master's verify_series runs — no scope opened, no review taken,
    /// unless a test hands in its own.</summary>
    internal static AgentSmith.Application.Services.SpecDialog.SeriesVerification Verification(
        ISourceScopeSandboxFactory? scopes = null, AgentSmith.Application.Services.Specs.ISpecCutReviewer? reviewer = null) =>
        new(scopes ?? new NoTemplateScopes(), AgentSmith.Tests.SpecDialog.ProposalEvidenceTestReview.Create(),
            reviewer ?? Mock.Of<AgentSmith.Application.Services.Specs.ISpecCutReviewer>(),
            AgentSmith.Tests.Specs.DerivationTestLooks.Factory(),
            NullLogger<AgentSmith.Application.Services.SpecDialog.SeriesVerification>.Instance);

    /// <summary>2026-09-13-6f35: the run that declares no template spawns nothing.</summary>
    private sealed class NoTemplateScopes : ISourceScopeSandboxFactory
    {
        public ISourceScopeSandbox Create(
            ResolvedProject project, RepoConnection repo, string? revision = null,
            string? conversationId = null) =>
            throw new InvalidOperationException("no template was declared in this test");
    }

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

    // 2026-09-01-7df4/0e80: the scan's follow-up passes run on the SAME loop runner the
    // handler was given, so a test that counts requests still sees every pass.
    internal static ScanMasterPasses BuildScanPasses(IAgenticLoopRunner loop)
    {
        var prompts = new AgentSmith.Application.Services.ScanMasterPromptFactory();
        var tolerant = AgentSmith.Tests.TestHelpers.TolerantJsonParserFactory.CreateTolerant();
        return new ScanMasterPasses(
            new ScanCoverageRedrive(loop, prompts, NullLogger<ScanCoverageRedrive>.Instance),
            new ScanReconciliationDrive(loop, prompts, NullLogger<ScanReconciliationDrive>.Instance),
            new MasterAnswerUnion(tolerant), tolerant, NullLogger<ScanMasterPasses>.Instance);
    }

    // p0353: the master takes the web_fetch tool host by DI; a real instance (its
    // HttpClient is never called in these tests) keeps the ctor happy.
    private static readonly AgentSmith.Application.Services.Tools.WebToolHost WebTool =
        new(new HttpClient());

    // p0315e: the real resolver chain over the real schema — the handler gate
    // resolves a typed outcome instead of validating only the draft.
    internal static AgentSmith.Application.Services.SpecDialog.OutcomeProposalResolver BuildOutcomeResolver()
    {
        var validator = new AgentSmith.Application.Services.SpecDialog.SpecDraftValidator(
            new AgentSmith.Application.Services.Validation.SpecSchemaProvider());
        var reader = new AgentSmith.Application.Services.SpecDialog.SpecDraftReader();
        return new AgentSmith.Application.Services.SpecDialog.OutcomeProposalResolver(
            validator, reader,
            new AgentSmith.Application.Services.SpecDialog.BugOutcomeParser(),
            new AgentSmith.Application.Services.SpecDialog.EpicOutcomeParser(
                validator, reader,
                new AgentSmith.Application.Services.SpecDialog.RequiresEdgeChecker(),
                AgentSmith.Tests.SpecDialog.DialogTestRepos.Real(), AgentSmith.Tests.TestSupport.SeriesContracts.Reader(), AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Reader()),
            new AgentSmith.Application.Services.SpecDialog.TicketGraphOutcomeParser(new AgentSmith.Application.Services.SpecDialog.TicketGraphChecker(
                new AgentSmith.Application.Services.SpecDialog.TicketGraphOrderer())),
            AgentSmith.Tests.SpecDialog.DialogTestRepos.Real());
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

    /// <summary>
    /// 2026-09-22-5891: keeps the context it is handed. Discarding it left a child's tool
    /// list, its budget and its iteration ceiling unobservable — the master grants all three
    /// through this one record, so a test that cannot see it cannot see what a child gets.
    /// </summary>
    internal sealed class StubSubAgentRunner : ISubAgentRunner
    {
        private readonly List<SubAgentContext> _contexts = [];
        private readonly List<IReadOnlyList<SubAgentSpec>> _waves = [];

        /// <summary>The context handed to each spawn call, in call order.</summary>
        public IReadOnlyList<SubAgentContext> SeenContexts
        {
            get { lock (_contexts) return [.. _contexts]; }
        }

        /// <summary>The specs of each spawn call — what the budget actually granted.</summary>
        public IReadOnlyList<IReadOnlyList<SubAgentSpec>> SeenWaves
        {
            get { lock (_contexts) return [.. _waves]; }
        }

        public Task<IReadOnlyList<SubAgentResult>> RunAsync(
            IReadOnlyList<SubAgentSpec> specs, SubAgentContext context, CancellationToken cancellationToken)
        {
            lock (_contexts)
            {
                _contexts.Add(context);
                _waves.Add(specs);
            }
            return Task.FromResult<IReadOnlyList<SubAgentResult>>(
                [.. specs.Select((spec, index) => new SubAgentResult(
                    TaskIndex: index, Status: SubAgentStatus.Succeeded,
                    SubAgentId: $"sa-{index}", Name: spec.Name,
                    ObservationsCount: 1, FindingsCount: 0, FilesWrittenCount: 0,
                    ToolCalls: 0, CostUsd: 0m, OccurredAt: DateTimeOffset.UtcNow))]);
        }
    }

    internal sealed class NoOpDecisionLogger : IDecisionLogger
    {
        public Task<DecisionLogOutcome> LogAsync(
            ISandboxFileReader? repositoryFiles, DecisionCategory category, string decision,
            CancellationToken cancellationToken = default, string? sourceLabel = null)
            => Task.FromResult(DecisionLogOutcome.Recorded);
    }
}
