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
        new(files, git ?? Git(files),
            new SeriesManifestFinder(new SeriesManifest(), NullLogger<SeriesManifestFinder>.Instance),
            new SeriesSpecFileReader(new PhaseDraftReader(), NullLogger<SeriesSpecFileReader>.Instance),
            new SeriesManifest(), new SandboxTargets(), NullLogger<SeriesReader>.Instance);

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
            new PhaseRecordPublisher(events ?? EventTestStubs.Recording()), new PhaseRecordIndexLine(), recorder);
    }
}
