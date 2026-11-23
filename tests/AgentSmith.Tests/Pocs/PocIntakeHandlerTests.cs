using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Pocs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Pocs;

/// <summary>
/// 2026-11-23-933db: the poc-deploy step reads the rows the import wrote, checks the stored archive by every rule on the
/// inflated data and records the revision once — validated with its manifest, or refused with the rule, which turns a
/// never-ready PoC refused. It never creates a Poc row and commits nothing.
/// </summary>
public sealed class PocIntakeHandlerTests
{
    private readonly FakeIntakeStore _store = new();
    private readonly Mock<IEventPublisher> _events = new();

    [Fact]
    public async Task Intake_SeedsNamingNoPoc_FailsWithoutWriting()
    {
        var result = await Handler().ExecuteAsync(Context(99, 1), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("No PoC 99");
        _store.Outcomes.Should().BeEmpty();
    }

    [Fact]
    public async Task Intake_MissingSeeds_Fails()
    {
        var result = await Handler().ExecuteAsync(Context(null, null), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        _store.Outcomes.Should().BeEmpty();
    }

    [Fact]
    public async Task Intake_AValidArchive_WritesAValidatedRevisionWithItsManifest_AndPublishesImported()
    {
        _store.Archive = PocZips.App();

        var result = await Handler().ExecuteAsync(Context(7, 1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Message.Should().Contain("The code scan comes next");
        var outcome = _store.Outcomes.Should().ContainSingle().Subject;
        outcome.State.Should().Be(PocRevisionStates.Validated);
        outcome.Port.Should().Be(8000);
        outcome.RunId.Should().Be("run-1");
        PocManifest.FromJson(outcome.ManifestJson!).Files.Keys.Should().BeEquivalentTo(["Dockerfile", "app.py"]);
        _events.Verify(e => e.PublishAsync(It.Is<PocImportedEvent>(x => x.PocId == 7 && x.Revision == 1 && x.Actor == "ann-sub"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Intake_ARefusedArchive_WritesARefusedRevision_AndFailsNamingTheRule()
    {
        _store.Archive = PocZips.App(("app/.env", PocZips.Text("KEY=1")));

        var result = await Handler().ExecuteAsync(Context(7, 1), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain(".env");
        _store.Outcomes.Should().ContainSingle().Which.State.Should().Be(PocRevisionStates.Refused);
        _events.Verify(e => e.PublishAsync(It.IsAny<PocImportedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Intake_OverTheBinaryBound_IsRefusedNamingTheKey()
    {
        var binary = new byte[2 * 1024 * 1024];
        new Random(4).NextBytes(binary);
        binary[0] = 0;
        _store.Archive = PocZips.App(("app/model.bin", binary));
        _store.Limits = new PocArchiveLimits { MaxBinaryMb = 1 };

        var result = await Handler().ExecuteAsync(Context(7, 1), CancellationToken.None);

        result.Message.Should().Contain("pocs.archive.max_binary_mb");
    }

    [Fact]
    public async Task Intake_ARevisionAlreadyEvaluated_IsNotCheckedAgain()
    {
        _store.RevisionState = PocRevisionStates.Validated;

        var result = await Handler().ExecuteAsync(Context(7, 1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _store.Outcomes.Should().BeEmpty();
    }

    [Theory]
    [InlineData(PocRevisionStates.Blocked)]
    [InlineData(PocRevisionStates.Overruled)]
    public async Task Intake_ABlockedOrOverruledRevision_IsNotEvaluatedAgain(string state)
    {
        _store.RevisionState = state;

        var result = await Handler().ExecuteAsync(Context(7, 1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _store.Outcomes.Should().BeEmpty("2026-11-23-933dc: an overrule run or a retry never rewrites it validated");
    }

    [Fact]
    public async Task Intake_TheUploadIsGone_Fails()
    {
        _store.Archive = null;

        (await Handler().ExecuteAsync(Context(7, 1), CancellationToken.None)).Message.Should().Contain("upload the ZIP again");
    }

    private PocIntakeHandler Handler()
    {
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(() => new AgentSmithConfig { Pocs = new PocsConfig { Archive = _store.Limits } });
        return new PocIntakeHandler(_store, PocZips.Reader(), new PocArchiveLimitsSource(loader.Object, new ServerContext("c.yml")),
            new PocImportedPublisher(_events.Object, TimeProvider.System), NullLogger<PocIntakeHandler>.Instance);
    }

    private static PocIntakeContext Context(long? pocId, int? revision)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "run-1");
        return new PocIntakeContext(pipeline, new ResolvedProject { Name = "poc-ann" }, pocId, revision, "ann-sub");
    }

    private sealed class FakeIntakeStore : IPocIntakeStore
    {
        public byte[]? Archive { get; set; } = PocZips.App();
        public string RevisionState { get; set; } = PocRevisionStates.Admitted;
        public PocArchiveLimits Limits { get; set; } = new();
        public List<PocIntakeOutcome> Outcomes { get; } = [];

        public Task<PocIntakeSubject?> ReadAsync(long pocId, int revision, CancellationToken ct) => Task.FromResult(pocId == 7
            ? new PocIntakeSubject(7, "todo", PocStates.Importing, revision, RevisionState, "poc-upload:x", "u1")
            : null);

        public Task<byte[]?> ArchiveAsync(string uploadSession, string uploadId, CancellationToken ct) => Task.FromResult(Archive);

        public Task RecordAsync(PocIntakeOutcome outcome, CancellationToken ct)
        {
            Outcomes.Add(outcome);
            return Task.CompletedTask;
        }
    }
}
