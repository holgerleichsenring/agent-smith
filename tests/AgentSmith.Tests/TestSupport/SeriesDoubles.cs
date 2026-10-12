using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.PhaseExecution;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
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
        new(new SubSeriesReader(files, git ?? Git(files),
                new SeriesManifestFinder(new SeriesManifest(), NullLogger<SeriesManifestFinder>.Instance),
                new SeriesSpecFileReader(new PhaseDraftReader(), NullLogger<SeriesSpecFileReader>.Instance),
                new SeriesManifest(), new SandboxTargets()),
            new SeriesJoin(new SeriesNewestManifest(), new SeriesJoinOrder()),
            new SeriesManifest(), NullLogger<SeriesReader>.Instance);

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

    internal static SeriesWriter Writer(ISandboxFileReaderFactory files, SandboxGitOperations? git = null) =>
        new(files, git ?? Git(files), new SeriesFiles(new SeriesManifest()), new SeriesStaleFiles(),
            new SandboxTargets(), NullLogger<SeriesWriter>.Instance);

    /// <summary>2026-10-06-03c7e: the record step as production composes it.</summary>
    internal static WritePhaseRecordHandler RecordHandler(
        ISandboxFileReaderFactory files, ISpecSetPointerStore pointers, SandboxGitOperations? git = null,
        AgentSmith.Contracts.Events.IEventPublisher? events = null)
    {
        var targets = new SandboxTargets();
        var indexWriter = new PhaseIndexWriter(
            files, new ContextYamlStateDoneCodec(new ContextYamlBuilders()), targets,
            NullLogger<PhaseIndexWriter>.Instance);
        var commit = new SeriesRecordCommit(files, git ?? Git(files),
            new SpecSetPointerRecorder(pointers, NullLogger<SpecSetPointerRecorder>.Instance));
        var recorder = new SpecDoneRecorder(files, targets, new SpecDoneFiles(), indexWriter,
            new SeriesFiles(new SeriesManifest()), commit, NullLogger<SpecDoneRecorder>.Instance);
        return new WritePhaseRecordHandler(
            new PhaseRecordPublisher(events ?? EventTestStubs.Recording()), new PhaseRecordIndexLine(), recorder,
            new SeriesRepos());
    }
}
