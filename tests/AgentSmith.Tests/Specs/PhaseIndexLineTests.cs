using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.PhaseExecution;
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
/// 2026-08-26-31e5: a phase that finished in a target repository leaves an index line in the
/// context it changed. The pointer has shipped since p0315d and the index never did, so the
/// chronicle existed as loose files nobody assembled.
/// <para>
/// Every case runs the real index writer against a REAL sandbox on a real temp directory: "left
/// byte-identical" and "no pointer without its line" are only checkable on a disk.
/// </para>
/// </summary>
[Collection(ExternalProcessCollection.Name)]
public sealed class PhaseIndexLineTests : IDisposable
{
    private const string PhaseId = "2026-08-26-31e5";
    private const string Goal = "A finished phase is recorded in its context";
    private const string Pointer =
        ".agentsmith/specs/done/2026-08-26-31e5-a-finished-phase-is-recorded-in-its-context.yaml";

    private const string Seeded = """
        # yaml-language-server: $schema=../../context.schema.json
        meta:
          workdir: "."          # the repo root
        integrations:
          Redis:  { type: bidirectional, does: "Job queue" }
        state:
          done:
            p0001: "shipped the first thing -> .agentsmith/specs/done/p0001.yaml"
          active: {}
        """;

    private readonly List<string> _dirs = [];

    public void Dispose()
    {
        foreach (var dir in _dirs.Where(Directory.Exists))
            Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public async Task Record_APhaseFinished_WritesAnIndexLineNamingItsFile()
    {
        var repo = Repo("app", "default", Seeded);

        await Run([repo]);

        Done(repo)[PhaseId].Should().Be($"{Goal} -> {Pointer}");
    }

    [Fact]
    public async Task Record_APhaseFinished_LeavesTheRestOfTheContextByteIdentical()
    {
        var repo = Repo("app", "default", Seeded);

        await Run([repo]);

        var before = Seeded.Split('\n');
        var after = Text(repo).Split('\n').Where(l => !l.Contains(PhaseId, StringComparison.Ordinal));
        after.Should().Equal(before,
            "the chronicle is SPLICED — the yaml-language-server header, the comment and the "
            + "flow-style integrations line are all still exactly what they were");
    }

    [Fact]
    public async Task Record_AContextWithNoStateSection_GetsOneWithTheEntry()
    {
        var repo = Repo("app", "default", "meta:\n  workdir: \".\"\n");

        await Run([repo]);

        Done(repo).Should().ContainKey(PhaseId);
        Section(repo, "state").Should().ContainKey("active",
            "/state declares done AND active required, and refuses anything else");
    }

    [Fact]
    public async Task Record_ASecondPhase_JoinsTheFirstNewestFirst()
    {
        var repo = Repo("app", "default", Seeded);

        await Run([repo]);

        var lines = Text(repo).Split('\n').ToList();
        lines.FindIndex(l => l.Contains(PhaseId, StringComparison.Ordinal))
            .Should().BeLessThan(lines.FindIndex(l => l.Contains("p0001", StringComparison.Ordinal)),
                "newest first, the order this repository already writes");
    }

    [Fact]
    public async Task Record_TheSamePhaseTwice_WritesOneLine()
    {
        var repo = Repo("app", "default", Seeded);

        await Run([repo]);
        await Run([repo]);

        Text(repo).Split('\n').Count(l => l.Contains(PhaseId + ":", StringComparison.Ordinal))
            .Should().Be(1, "a re-run is a designed path, and a duplicate key is unparseable");
        Done(repo).Should().ContainKey(PhaseId);
    }

    [Fact]
    public async Task Record_AnOverLongGoal_IsCutAtAWordBoundaryAndStillFits()
    {
        var goal = string.Join(' ', Enumerable.Repeat("preserving what the typed document never modelled", 20));
        var repo = Repo("app", "default", Seeded);

        await Run([repo], goal);

        var entry = (string)Done(repo)[PhaseId]!;
        entry.Length.Should().BeLessThanOrEqualTo(PhaseRecordIndexLine.MaxChars,
            "the line is composed to fit — the record step runs AFTER the work is committed, "
            + "so refusing it would fail a run nobody could go back and shorten");
        entry.Should().EndWith($" -> {Pointer}");
        var head = entry[..entry.IndexOf('…')];
        goal.Should().StartWith(head);
        goal[head.Length].Should().Be(' ', "the goal is cut at a word boundary, not mid-word");
    }

    [Fact]
    public async Task Record_ACreatedStateSection_ValidatesAgainstTheSchema()
    {
        var repo = Repo("app", "default", "meta:\n  workdir: \".\"\n");

        await Run([repo]);

        ContextSchemaFile.Validate(Text(repo)).Should().BeEmpty(
            "a section this step creates in a customer's repository has to be a valid one");
    }

    [Fact]
    public async Task Record_TheContext_IsTheOneTheSandboxCarried()
    {
        var repo = Repo("app", "api", Seeded);
        Seed(repo.WorkDir, "default", Seeded);

        await Run([repo]);

        Done(repo).Should().ContainKey(PhaseId, "the sandbox carried the 'api' context");
        Text(repo.WorkDir, "default").Should().Be(Seeded,
            "applies_to names an area and cannot route; the sandbox names the context");
    }

    private sealed record Target(string Name, string ContextName, string WorkDir);

    private Target Repo(string name, string contextName, string yaml)
    {
        var dir = Directory.CreateTempSubdirectory("phase-index-").FullName;
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

    private static string Text(Target repo) => Text(repo.WorkDir, repo.ContextName);

    private static string Text(string workDir, string contextName) =>
        File.ReadAllText(ContextPath(workDir, contextName));

    private static Dictionary<object, object?> Section(Target repo, params string[] keys)
    {
        var node = new ContextYamlBuilders().Deserializer
            .Deserialize<Dictionary<object, object?>>(Text(repo))!;
        foreach (var key in keys) node = (Dictionary<object, object?>)node[key]!;
        return node;
    }

    private static Dictionary<object, object?> Done(Target repo) => Section(repo, "state", "done");

    // 2026-10-06-03c7e: the record step writes the line into the carrying repository only, so
    // the line's shape is pinned at the writer the step calls.
    private static async Task Run(IReadOnlyList<Target> targets, string goal = Goal)
    {
        var sandboxes = new Dictionary<string, ISandbox>(StringComparer.Ordinal);
        var discoveries = new Dictionary<string, RemoteContextDiscovery>(StringComparer.Ordinal);
        foreach (var target in targets)
        {
            sandboxes[target.Name] = new InProcessSandbox(
                target.Name, target.WorkDir, ownsWorkDir: false, NullLogger.Instance);
            discoveries[target.Name] = new RemoteContextDiscovery(target.ContextName, ".", "C#");
        }

        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(ContextKeys.Sandboxes, sandboxes);
        pipeline.Set<IReadOnlyDictionary<string, RemoteContextDiscovery>>(
            ContextKeys.SandboxDiscoveries, discoveries);
        var line = new PhaseRecordIndexLine().Compose(goal, Pointer)!;
        foreach (var (key, sandbox) in sandboxes)
        {
            var written = await Writer().WriteAsync(
                pipeline, sandbox, key, Repository.SandboxWorkPath, PhaseId, line, CancellationToken.None);
            written.Should().NotBeNull();
            await sandbox.DisposeAsync();
        }
    }

    private static PhaseIndexWriter Writer() => new(
        new AgentSmith.Application.Services.Sandbox.SandboxFileReaderFactory(),
        new ContextYamlStateDoneCodec(new ContextYamlBuilders()),
        new SandboxTargets(),
        NullLogger<PhaseIndexWriter>.Instance);
}
