using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.SpecExecution;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Infrastructure.Services;
using AgentSmith.Infrastructure.Services.Sandbox;
using AgentSmith.Tests.Architecture;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-08-26-31e5: a spec that finished in a target repository leaves an index line in the context it changed.
/// 2026-11-26-462aa: the line goes into the context's done.md, and a repository whose context.yaml files still carry
/// state.done is migrated by that first write.
/// <para>
/// Every case runs the real index writer against a REAL sandbox on a real temp directory: "left byte-identical" and
/// "no pointer without its line" are only checkable on a disk.
/// </para>
/// </summary>
[Trait("TestProcess", "serial")]
[Collection(ExternalProcessCollection.Name)]
public sealed class SpecIndexLineTests : IDisposable
{
    private const string SpecId = "2026-08-26-31e5";
    private const string Goal = "A finished spec is recorded in its context";
    private const string Pointer =
        ".agentsmith/specs/done/2026-08-26-31e5-a-finished-spec-is-recorded-in-its-context.yaml";

    private const string Seeded = """
        # yaml-language-server: $schema=../../context.schema.json
        meta:
          workdir: "."          # the repo root
        integrations:
          Redis:  { type: bidirectional, does: "Job queue" }
        state:
          done:
            p0002: "shipped the \"second\" thing -> .agentsmith/specs/done/p0002.yaml"
            # a comment inside the block
            p0001: "shipped the first thing -> .agentsmith/specs/done/p0001.yaml"
          active: {}

        # the next section's comment
        verify:
          - label: test
            command: dotnet test
        """;

    private readonly List<string> _dirs = [];

    public void Dispose()
    {
        foreach (var dir in _dirs.Where(Directory.Exists))
            Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public async Task Record_ASpecFinished_WritesItsLineFirstInDoneMd()
    {
        var repo = Repo("app", "default", "meta:\n  workdir: \".\"\n");

        await Run([repo]);

        var done = Done(repo);
        done.Should().StartWith(ContextDoneIndex.Header);
        ContextDoneIndex.Entries(done).Should().ContainSingle().Which.Should().Be(new KeyValuePair<string, string>(SpecId, $"{Goal} -> {Pointer}"));
        Text(repo).Should().Be("meta:\n  workdir: \".\"\n", "a context.yaml without state.done is not touched");
    }

    [Fact]
    public async Task Record_AContextWithStateDone_MovesEveryEntryInOrderBelowTheNewLine()
    {
        var repo = Repo("app", "default", Seeded);

        await Run([repo]);

        ContextDoneIndex.Entries(Done(repo)).Select(e => e.Key).Should().Equal(SpecId, "p0002", "p0001");
        ContextDoneIndex.Entries(Done(repo))[1].Value.Should().Be(
            "shipped the \"second\" thing -> .agentsmith/specs/done/p0002.yaml", "the value is read by a YAML parser");
    }

    [Fact]
    public async Task Record_AMigratedContext_KeepsEveryOtherLineByteIdentical()
    {
        var repo = Repo("app", "default", Seeded);

        await Run([repo]);

        Text(repo).Should().Be("""
            # yaml-language-server: $schema=../../context.schema.json
            meta:
              workdir: "."          # the repo root
            integrations:
              Redis:  { type: bidirectional, does: "Job queue" }
            state:
              active: {}

            # the next section's comment
            verify:
              - label: test
                command: dotnet test
            """);
        ContextSchemaFile.Validate(Text(repo)).Should().BeEmpty("state without done is a valid state");
    }

    [Fact]
    public async Task Record_AStateWithOnlyDone_LosesTheEmptiedState()
    {
        var repo = Repo("app", "default", "meta:\n  workdir: \".\"\nstate:\n  done:\n    p0001: \"x\"\n\n# verify next\nverify: []\n");

        await Run([repo]);

        Text(repo).Should().Be("meta:\n  workdir: \".\"\n\n# verify next\nverify: []\n");
    }

    [Fact]
    public async Task Record_TheSameSpecTwice_WritesOneLine()
    {
        var repo = Repo("app", "default", Seeded);

        await Run([repo]);
        await Run([repo]);

        ContextDoneIndex.Entries(Done(repo)).Count(e => e.Key == SpecId)
            .Should().Be(1, "a re-run is a designed path");
    }

    [Fact]
    public async Task Record_AnotherContextWithStateDone_IsMigratedInTheSameWrite()
    {
        var repo = Repo("app", "api", Seeded);
        Seed(repo.WorkDir, "default", Seeded);

        await Run([repo]);

        ContextDoneIndex.Entries(Done(repo)).Select(e => e.Key).Should().Equal(SpecId, "p0002", "p0001");
        ContextDoneIndex.Entries(File.ReadAllText(DonePath(repo.WorkDir, "default"))).Select(e => e.Key)
            .Should().Equal(["p0002", "p0001"], "the line goes to the sandbox's context, the migration to every context");
        Text(repo.WorkDir, "default").Should().NotContain("p0001");
    }

    [Fact]
    public async Task Record_GitAttributes_GainTheUnionLinesOnceAndKeepTheirOwn()
    {
        var repo = Repo("app", "default", Seeded);
        File.WriteAllText(Path.Combine(repo.WorkDir, ".gitattributes"), "*.png binary");

        await Run([repo]);
        await Run([repo]);

        File.ReadAllText(Path.Combine(repo.WorkDir, ".gitattributes")).Should().Be(
            "*.png binary\n.agentsmith/contexts/*/done.md merge=union\n.agentsmith/memory/MEMORY.md merge=union\n");
    }

    [Fact]
    public async Task Record_AnOverLongGoal_IsCutAtAWordBoundaryAndStillFits()
    {
        var goal = string.Join(' ', Enumerable.Repeat("preserving what the typed document never modelled", 20));
        var repo = Repo("app", "default", Seeded);

        await Run([repo], goal);

        var entry = ContextDoneIndex.Entries(Done(repo)).Single(e => e.Key == SpecId).Value;
        entry.Length.Should().BeLessThanOrEqualTo(SpecRecordIndexLine.MaxChars,
            "the line is composed to fit — the record step runs AFTER the work is committed, "
            + "so refusing it would fail a run nobody could go back and shorten");
        entry.Should().EndWith($" -> {Pointer}");
        var head = entry[..entry.IndexOf('…')];
        goal.Should().StartWith(head);
        goal[head.Length].Should().Be(' ', "the goal is cut at a word boundary, not mid-word");
    }

    [Fact]
    public async Task Record_AContextYamlThatDoesNotParse_IsLeftAsItIs()
    {
        const string broken = "meta:\n  workdir: \".\"\nstate:\n  done:\n    p0001: \"unterminated\n";
        var repo = Repo("app", "default", broken);

        var written = await Run([repo]);

        Text(repo).Should().Be(broken);
        written.Should().NotContain(".agentsmith/contexts/default/context.yaml");
        ContextDoneIndex.Entries(Done(repo)).Select(e => e.Key).Should().Equal(SpecId);
    }

    private sealed record Target(string Name, string ContextName, string WorkDir);

    private Target Repo(string name, string contextName, string yaml)
    {
        var dir = Directory.CreateTempSubdirectory("spec-index-").FullName;
        _dirs.Add(dir);
        Seed(dir, contextName, yaml);
        return new Target(name, contextName, dir);
    }

    private static void Seed(string workDir, string contextName, string yaml)
    {
        var path = ContextPath(workDir, contextName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, yaml);
    }

    private static string ContextPath(string workDir, string contextName) =>
        Path.Combine(workDir, ".agentsmith", "contexts", contextName, "context.yaml");

    private static string DonePath(string workDir, string contextName) =>
        Path.Combine(workDir, ".agentsmith", "contexts", contextName, "done.md");

    private static string Text(Target repo) => Text(repo.WorkDir, repo.ContextName);

    private static string Text(string workDir, string contextName) =>
        File.ReadAllText(ContextPath(workDir, contextName));

    private static string Done(Target repo) => File.ReadAllText(DonePath(repo.WorkDir, repo.ContextName));

    // 2026-10-06-03c7e: the record step writes the line into the carrying repository only, so
    // the line's shape is pinned at the writer the step calls.
    private static async Task<IReadOnlyList<string>> Run(IReadOnlyList<Target> targets, string goal = Goal)
    {
        var sandboxes = new Dictionary<string, ISandbox>(StringComparer.Ordinal);
        var discoveries = new Dictionary<string, RemoteContextDiscovery>(StringComparer.Ordinal);
        foreach (var target in targets)
        {
            sandboxes[target.Name] = new InProcessSandbox(
                target.Name, target.WorkDir, ownsWorkDir: false, InProcessStepEnvironment.Inherit, NullLogger.Instance);
            discoveries[target.Name] = new RemoteContextDiscovery(target.ContextName, ".", "C#");
        }

        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, sandboxes);
        pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(
            ContextKeys.SandboxDiscoveries, discoveries);
        var line = new SpecRecordIndexLine().Compose(goal, Pointer)!;
        IReadOnlyList<string> written = [];
        foreach (var (key, sandbox) in sandboxes)
        {
            written = await Writer().WriteAsync(
                pipeline, sandbox, key, Repository.WorkspaceRoot, SpecId, line, CancellationToken.None);
            written.Should().NotBeEmpty();
            await sandbox.DisposeAsync();
        }
        return written;
    }

    private static SpecIndexWriter Writer() => new(
        new AgentSmith.Application.Services.Sandbox.SandboxFileReaderFactory(),
        new ContextDoneMigration(new ContextYamlStateDoneCodec(new ContextYamlBuilders()),
            NullLogger<ContextDoneMigration>.Instance),
        new SandboxTargets(),
        NullLogger<SpecIndexWriter>.Instance);
}
