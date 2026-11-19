using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.SpecExecution;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-08-5cd2, run a109's shape through the real reader: the branch carries a set
/// whose first spec executed (the marker's commit is the last one on the spec path, so
/// the pointer's sha no longer matches), and the ticket comes back re-triggered. When
/// the ticket text changed since the set was cut, the unbuilt tail is kept and the edit is
/// handed to its coding master (2026-11-19-6b0ba); when it did not — or the set predates the
/// fingerprint — nothing is recorded as new input.
/// </summary>
public sealed class DeriveSpecTicketEditTests
{
    private const string Key = "azdo-19106";
    private const string PointerSha = "spec-sha-1";
    private const string MarkerSha = "marker-sha-2";
    private const string EditedTicket = """
        Migrate the client.

        Ping me if unclear.
        """;

    [Fact]
    public async Task DeriveSpec_AnEditedTicketWithAnExecutedSpec_KeepsTheTailAndHandsTheEditToItsMaster()
    {
        var files = SeededFiles(fingerprint: "a-fingerprint-of-the-text-before-the-edit");
        var published = new CapturingPublisher();

        var context = Context();
        var result = await Handler(files, published).ExecuteAsync(context, default);

        result.IsSuccess.Should().BeTrue();
        context.Pipeline.Get<string>(ContextKeys.SpecKeptInput).Should().Be(SpecRevisionCause.TicketEdit,
            "the unbuilt spec is kept and its coding master amends it for the edit (2026-11-19-6b0ba)");
        published.Set!.StandIns.Should().BeEmpty();
        published.Set.Specs.Select(p => p.SpecId).Should().Equal("p19106a", "p19106b");
        published.Set.Executed.Should().Equal(["p19106a"], "the executed spec is carried, never edited");
        published.Set.Current.Cause.Should().Be(SpecRevisionCause.TicketEdit);
    }

    [Fact]
    public async Task DeriveSpec_ASetWithoutAFingerprint_IsNotRederived()
    {
        var files = SeededFiles(fingerprint: null);
        var published = new CapturingPublisher();

        await Handler(files, published).ExecuteAsync(Context(), default);

        published.Set!.StandIns.Should().BeEmpty("a set cut before the fingerprint existed compares as unchanged");
        published.Set!.Specs.Select(p => p.Draft.Goal).Should().Equal("Goal p19106a", "Goal p19106b");
    }

    [Fact]
    public async Task DeriveSpec_AnUnchangedTicketWithAnExecutedSpec_IsNotRederived()
    {
        var files = SeededFiles(fingerprint: TicketTextFingerprint.Of(Ticket()));
        var published = new CapturingPublisher();

        await Handler(files, published).ExecuteAsync(Context(), default);

        published.Set!.Current.Cause.Should().Be(SpecRevisionCause.ReviewerEdit,
            "the pointer here still names the derivation commit, so the last commit on the spec path "
            + "is a foreign one — a reviewer's edit after the spec ran; the marker itself moves the "
            + "pointer since 2026-09-08-4aa9");
        published.Set.TicketFingerprint.Should().Be(TicketTextFingerprint.Of(Ticket()));
    }

    [Fact]
    public async Task DeriveSpec_ARevisionOverAnEdit_CarriesTheCurrentFingerprint()
    {
        var files = SeededFiles(fingerprint: "a-fingerprint-of-the-text-before-the-edit");
        var published = new CapturingPublisher();

        await Handler(files, published).ExecuteAsync(Context(), default);

        published.Set!.TicketFingerprint.Should().Be(TicketTextFingerprint.Of(Ticket()),
            "the next run compares against the text this run handed its master");
    }

    private static DeriveSpecHandler Handler(
        SeededFileReader files, ISpecSetPublisher publisher)
    {
        var factory = new Mock<ISandboxFileReaderFactory>();
        factory.Setup(f => f.Create(It.IsAny<ISandbox>())).Returns(files);
        var gitOps = new SandboxGitOperations(
            new GitBranchPusher(), AgentSmith.Tests.TestSupport.TestGitCredentials.Resolver, NullLogger<SandboxGitOperations>.Instance, factory.Object,
            new SandboxGitIdentity(NullLogger<SandboxGitIdentity>.Instance));
        var draftReader = new SpecDraftReader();
        var reader = AgentSmith.Tests.TestSupport.SeriesDoubles.Reader(factory.Object, gitOps);
        var pointers = new InMemorySpecSetPointerStore();
        pointers.SaveAsync(string.Empty, new SpecSetPointer(Key, "primary", PointerSha, 1), default)
            .GetAwaiter().GetResult();
        var validator = new SpecDraftValidator(new SpecSchemaProvider());
        var tickets = new Mock<ITicketProviderFactory>();
        return new DeriveSpecHandler(
            SeriesDoubles.BranchRead(reader), publisher, pointers,
            new ApprovedSpecSetResolver(
                new InMemorySpecApprovalStore(), NullLogger<ApprovedSpecSetResolver>.Instance),
            new SpecSourceResolver(
                new ApprovedSetHandoff(NullLogger<ApprovedSetHandoff>.Instance), AgentSmith.Tests.TestSupport.ApprovedSetDoubles.Continuation(),
                new FiledTicketSpecGate(NullLogger<FiledTicketSpecGate>.Instance),
                AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Reader(), NullLogger<SpecSourceResolver>.Instance),
            new SpecStandIns(validator, draftReader, new DerivedSpecYamlRenderer()),
            new SpecSetTicketCommenter(tickets.Object, Moq.Mock.Of<AgentSmith.Contracts.Reviews.IFullSetPrNotice>(), AgentSmith.Tests.TestSupport.SeriesSettingsDoubles.Reader(), NullLogger<SpecSetTicketCommenter>.Instance),
            ApprovedSetDoubles.KeptNotice(),
            new UnansweredQuestionPin(NullLogger<UnansweredQuestionPin>.Instance),
            new UnansweredQuestionNotice(tickets.Object, NullLogger<UnansweredQuestionNotice>.Instance),
            new SeriesResolver(new SeriesIdFactory(TimeProvider.System)),
            AgentSmith.Tests.TestSupport.HandbackTestDoubles.Probe(),
            new SpecSetUnchanged(new SeriesFiles(new SeriesManifest())));
    }

    // The marker's commit is the last one on the spec path: every git call answers it.
    private static DeriveSpecContext Context()
    {
        var sandbox = new Mock<ISandbox>();
        sandbox.Setup(s => s.RunStepAsync(It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StepResult(1, Guid.Empty, 0, false, 0, null, MarkerSha));
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "run-2");
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes, new Dictionary<string, ISandbox> { ["primary"] = sandbox.Object });
        pipeline.Set(ContextKeys.Ticket, Ticket());
        return new DeriveSpecContext(
            Ticket(), null, [new RepoConnection { Name = "primary" }], new AgentConfig(), pipeline);
    }

    private static Ticket Ticket() =>
        new(new TicketId("19106"), "Migrate the client", EditedTicket, null, "open", "azdo", []);

    private static SeededFileReader SeededFiles(string? fingerprint)
    {
        var files = new SeededFileReader();
        files.Seed($".agentsmith/series/2026-10-06-0a0a.yaml", SetYaml(fingerprint));
        files.Seed($".agentsmith/specs/done/p19106a-first.yaml", SpecYaml("p19106a"));
        files.Seed($".agentsmith/specs/planned/p19106b-second.yaml", SpecYaml("p19106b"));
        return files;
    }

    private static string SetYaml(string? fingerprint) => $"""
        ticket: {Key}
        specs:
        - p19106a
        - p19106b
        revisions:
        - number: 1
          cause: initial derivation
          at: 2026-09-08T10:00:00.0000000+00:00
        carried:
        - segment: 1
          phase: p19106a
        - segment: 2
          phase: p19106b
        {(fingerprint is null ? string.Empty : "ticket_fingerprint: " + fingerprint)}
        """;

    private static string SpecYaml(string id) => $"""
        spec: {id}
        goal: "Goal {id}"
        done:
          - "Done {id}."
        """;

    private sealed class CapturingPublisher : ISpecSetPublisher
    {
        public SpecSet? Set { get; private set; }

        public Task<CommandResult> PublishAsync(
            PipelineContext pipeline, string project, SeriesScope scope, SpecSet set,
            IReadOnlyList<IgnoredInstruction> ignoredInstructions, CancellationToken ct)
        {
            Set = set;
            return Task.FromResult(CommandResult.Ok("published"));
        }

        public bool Kept { get; private set; }

        public Task<CommandResult> KeepAsync(
            PipelineContext pipeline, SeriesScope scope, SpecSet set, string revisionSha, CancellationToken ct)
        {
            (Set, Kept) = (set, true);
            return Task.FromResult(CommandResult.Ok("kept"));
        }
    }

    private sealed class SeededFileReader : ISandboxFileReader
    {
        private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);

        public void Seed(string path, string content) => _files[path] = content;

        public Task<bool> ExistsAsync(string path, CancellationToken ct) => Task.FromResult(_files.ContainsKey(path));
        public Task<string?> TryReadAsync(string path, CancellationToken ct) =>
            Task.FromResult(_files.TryGetValue(path, out var c) ? c : null);
        public Task<string> ReadRequiredAsync(string path, CancellationToken ct) => Task.FromResult(_files[path]);
        public Task<IReadOnlyList<string>> ListAsync(string path, int? maxDepth, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>([.. _files.Keys.Where(k => k.StartsWith(path, StringComparison.Ordinal))]);
        public Task WriteAsync(string path, string content, CancellationToken ct)
        {
            _files[path] = content;
            return Task.CompletedTask;
        }
    }
}
