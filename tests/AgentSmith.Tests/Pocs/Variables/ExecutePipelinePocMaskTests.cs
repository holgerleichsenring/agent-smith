using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Application.Services.Pocs;
using AgentSmith.Application.Services.Resume;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.Sandbox;
using AgentSmith.Tests.TestHelpers;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Pocs.Variables;

/// <summary>
/// 2026-11-23-933dg: the run opens its mask frame at its start — a run of poc-ann masks its PoC's secret in what its
/// steps hand the masker (the trace writer and the masking chat client both call it on the run's flow), while a run of
/// another project, at the same time, writes a file holding that word unmasked.
/// </summary>
public sealed class ExecutePipelinePocMaskTests
{
    private const string PocValue = "Production-key-933dg";
    private readonly Mock<IPipelineExecutor> _pipeline = new();
    private readonly Mock<IConfigurationLoader> _config = new();
    private readonly RunMaskFrame _frame = new();
    private readonly SecretMasker _masker;
    private readonly Dictionary<string, string> _written = new();

    public ExecutePipelinePocMaskTests()
    {
        _masker = new SecretMasker(new LoadedSecretValues(new AgentSmithConfig()), _frame);
        _config.Setup(c => c.LoadConfig("config.yml")).Returns(new AgentSmithConfig
        {
            Projects =
            {
                ["poc-ann"] = new ResolvedProject { Pipeline = "code", Repos = [new RepoConnection { Name = "poc-ann" }] },
                ["payments"] = new ResolvedProject { Pipeline = "code", Repos = [new RepoConnection { Name = "payments" }] },
            },
        });
        _pipeline.Setup(p => p.ExecuteAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<ResolvedProject>(), It.IsAny<PipelineContext>(), It.IsAny<CancellationToken>()))
            .Returns<IReadOnlyList<string>, ResolvedProject, PipelineContext, CancellationToken>(async (_, _, ctx, _) =>
            {
                await Task.Yield();
                lock (_written) _written[ctx.Get<string>(ContextKeys.RunId)] = _masker.Apply($"config: key={PocValue}");
                return CommandResult.Ok("done");
            });
    }

    [Fact]
    public async Task ARunOfThePocProject_MasksItsSecret_AnotherProjectsRunDoesNot()
    {
        var values = new Mock<IPocMaskValues>();
        values.Setup(v => v.ForProjectAsync("poc-ann", It.IsAny<CancellationToken>())).ReturnsAsync([PocValue]);
        values.Setup(v => v.ForProjectAsync("payments", It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var masking = new PocRunMasking(_frame, values.Object);

        await Task.WhenAll(Run("poc-ann", "run-a", masking), Run("payments", "run-b", masking));

        _written["run-a"].Should().Be("config: key=***");
        _written["run-b"].Should().Be($"config: key={PocValue}");
        _frame.Current.Should().BeEmpty("the frame ends with the run");
    }

    private Task<CommandResult> Run(string project, string runId, PocRunMasking masking) =>
        Task.Run(() => UseCase(masking).ExecuteAsync(new PipelineRequest(project, "code", RunId: runId), "config.yml", CancellationToken.None));

    private ExecutePipelineUseCase UseCase(PocRunMasking masking)
    {
        var skills = new Mock<ISkillLoader>();
        skills.Setup(s => s.LoadVocabulary(It.IsAny<string>())).Returns(ConceptVocabulary.Empty);
        var lease = new Mock<IActiveRunLease>();
        var events = new RecordingEventPublisher();
        return new ExecutePipelineUseCase(
            _config.Object, Mock.Of<IIntentParser>(), _pipeline.Object,
            new ResumeRequestReader(new PipelineContextSerializer(NullLogger<PipelineContextSerializer>.Instance),
                Mock.Of<ICommandContextFactory>(f => f.Knows(It.IsAny<string>())), NullLogger<ResumeRequestReader>.Instance),
            new ResumedCapRecompute(new StubConfigResolver(), events, NullLogger<ResumedCapRecompute>.Instance),
            Mock.Of<ISourceConfigOverrider>(), new StubSkillsCatalogResolver(),
            new ConceptVocabularyLoader(new StubSkillsCatalogPath(), skills.Object, NullLogger<ConceptVocabularyLoader>.Instance),
            new PipelineConfigResolver(), events, new AgentSmith.Application.Services.Events.AsyncLocalRunContextAccessor(), new ModelPricingResolver(),
            new RunCancellationRegistry(NullLogger<RunCancellationRegistry>.Instance), lease.Object,
            new RunHeartbeatPump(lease.Object, new NoOpRunHeartbeat(), TimeProvider.System, NullLogger<RunHeartbeatPump>.Instance),
            new StubConfigResolver(), Mock.Of<IProgressReporter>(), Mock.Of<IPipelineErrorHandler>(),
            NullLogger<ExecutePipelineUseCase>.Instance, pocMasks: masking);
    }
}
