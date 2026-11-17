using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.SpecExecution;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Services;
using AgentSmith.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.TestSupport;

/// <summary>2026-10-06-03c7d: the series reader and writer as production composes them.</summary>
internal static class SeriesDoubles
{
    internal static SandboxGitOperations Git(ISandboxFileReaderFactory files) =>
        new(new GitBranchPusher(), TestGitCredentials.Resolver, NullLogger<SandboxGitOperations>.Instance,
            files, new SandboxGitIdentity(NullLogger<SandboxGitIdentity>.Instance));

    internal static SeriesReader Reader(ISandboxFileReaderFactory files, SandboxGitOperations? git = null) =>
        new(new SubSeriesReader(files, git ?? Git(files), SubSeries(), new SandboxTargets()), Composer());

    /// <summary>2026-11-17-a0b6a: the sub-series reading shared by the checkout and the branch read.</summary>
    internal static SubSeriesFiles SubSeries() =>
        new(new SeriesManifestFinder(new SeriesManifest(), NullLogger<SeriesManifestFinder>.Instance),
            new SeriesSpecFileReader(new SpecDraftReader(), NullLogger<SeriesSpecFileReader>.Instance),
            new SeriesManifest());

    /// <summary>2026-11-17-a0b6a: the join and composition shared by both reads.</summary>
    internal static SeriesComposer Composer() =>
        new(new SeriesJoin(new SeriesNewestManifest(), new SeriesJoinOrder()), new SeriesManifest(),
            NullLogger<SeriesComposer>.Instance);

    /// <summary>2026-11-17-a0b6a: the host-side branch read over a recording remote and a pointer store.</summary>
    internal static TicketBranchSeries BranchSeries(
        ISourceProviderFactory? sources = null, ISpecSetPointerStore? pointers = null) =>
        new(pointers ?? new AgentSmith.Application.Services.Persistence.InMemorySpecSetPointerStore(),
            sources ?? new RecordingBranchSources(), SubSeries(), Composer(), NullLogger<TicketBranchSeries>.Instance);

    /// <summary>2026-11-17-a0b6c: the server's branch read of a ticket over <paramref name="config"/>'s projects.</summary>
    internal static AgentSmith.Server.Services.Tickets.TicketBranchSpecs BranchSpecs(
        AgentSmith.Contracts.Models.Configuration.AgentSmithConfig config,
        ISourceProviderFactory? sources = null, ISpecSetPointerStore? pointers = null) =>
        new(LiveConfig.Projects(config), BranchSeries(sources, pointers));

    /// <summary>2026-10-12-f5f7d: DeriveSpec's branch read over <paramref name="reader"/>; done/ is
    /// listed through <paramref name="files"/>, empty when none is given.</summary>
    internal static SeriesBranchRead BranchRead(ISpecSetReader reader, ISandboxFileReaderFactory? files = null) =>
        new(reader, new SeriesTakenIds(files ?? EmptyFiles(), new SandboxTargets()));

    internal static ISandboxFileReaderFactory EmptyFiles()
    {
        var factory = new Moq.Mock<ISandboxFileReaderFactory>();
        factory.Setup(f => f.Create(Moq.It.IsAny<ISandbox>())).Returns(new SpecBranchFiles());
        return factory.Object;
    }

    /// <summary>2026-10-12-f5f7d: a one-repository write as tests made it before placement — every
    /// spec naming none belongs to <paramref name="repo"/>.</summary>
    internal static Task<SpecSetWriteResult> WriteAsync(
        this SeriesWriter writer, AgentSmith.Contracts.Commands.PipelineContext pipeline,
        AgentSmith.Contracts.Models.Configuration.RepoConnection repo, SpecSet set, CancellationToken ct) =>
        writer.WriteAsync(pipeline, new SeriesRepos().Place(set, new SeriesScope([repo], repo.Name, [], repo.Name)), ct);

    /// <summary>2026-10-12-f5f7d: a one-repository pointer record, as tests made it before a write
    /// could cover several repositories.</summary>
    internal static Task RecordAsync(
        this SpecSetPointerRecorder recorder, string project,
        AgentSmith.Contracts.Models.Configuration.RepoConnection repo, SpecSet set, string sha, CancellationToken ct) =>
        recorder.RecordAsync(project, repo.Name, set,
            new Dictionary<string, string>(StringComparer.Ordinal) { [repo.Name] = sha }, ct);

    internal static SeriesWriter Writer(ISandboxFileReaderFactory files, SandboxGitOperations? git = null)
    {
        var ops = git ?? Git(files);
        var member = new SeriesMemberCommit(files, ops, new SeriesFiles(new SeriesManifest()), new SeriesStaleFiles(),
            NullLogger<SeriesMemberCommit>.Instance);
        return new(member, new SeriesWriteRollback(ops, member, NullLogger<SeriesWriteRollback>.Instance),
            ops, new SandboxTargets(), NullLogger<SeriesWriter>.Instance);
    }

    /// <summary>2026-10-06-03c7e: the record step as production composes it.</summary>
    internal static WriteSpecRecordHandler RecordHandler(
        ISandboxFileReaderFactory files, ISpecSetPointerStore pointers, SandboxGitOperations? git = null,
        AgentSmith.Contracts.Events.IEventPublisher? events = null, DefaultVerdictRecord? verdicts = null)
    {
        var targets = new SandboxTargets();
        var indexWriter = new SpecIndexWriter(
            files, new ContextYamlStateDoneCodec(new ContextYamlBuilders()), targets,
            NullLogger<SpecIndexWriter>.Instance);
        var commit = new SeriesRecordCommit(files, git ?? Git(files),
            new SpecSetPointerRecorder(pointers, NullLogger<SpecSetPointerRecorder>.Instance));
        var recorder = new SpecDoneRecorder(files, targets, new SpecDoneFiles(), indexWriter,
            new SeriesFiles(new SeriesManifest()), commit, verdicts ?? NoVerdicts(),
            AgentSmith.Tests.TestHelpers.MemoryStandings.LabelWriter(), NullLogger<SpecDoneRecorder>.Instance);
        return new WriteSpecRecordHandler(
            new SpecRecordPublisher(events ?? EventTestStubs.Recording()), new SpecRecordIndexLine(), recorder,
            new SeriesRepos());
    }

    /// <summary>2026-10-13-8762p: the record's verdict copy as a composition without a database has it.</summary>
    internal static DefaultVerdictRecord NoVerdicts() =>
        new(new AgentSmith.Application.Services.Resume.NullDecidedDefaults(),
            Moq.Mock.Of<AgentSmith.Contracts.Decisions.IDefaultVerdictFile>(), NullLogger<DefaultVerdictRecord>.Instance);
}
