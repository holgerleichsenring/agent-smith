using System.Reflection;
using System.Text.Json;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Services;
using AgentSmith.Infrastructure.Services.Sandbox;
using AgentSmith.Tests.Architecture;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

/// <summary>
/// 2026-09-01-e14d: the gate a repository declares is ADOPTED from the pipeline it already
/// runs, records what it was adopted from, and a later run says when that source has moved.
/// <para>
/// Every case runs against a REAL temp directory through the REAL write path and the REAL
/// parser. The hash is the whole mechanism, and a hash asserted against a stub is a hash
/// asserted against itself.
/// </para>
/// </summary>
[Collection(ExternalProcessCollection.Name)]
public sealed class VerifyDerivationTests : IDisposable
{
    private const string ContextPath = ".agentsmith/contexts/default/context.yaml";

    private const string PipelineFile = "azure-pipelines.yml";

    private const string Pipeline = """
        pool: { vmImage: ubuntu-latest }
        steps:
          - script: pip install -r requirements.txt
          - script: pytest -q
        """;

    /// <summary>What a master proposes after reading the pipeline above.</summary>
    private const string Derived = """
        {
          "meta": { "workdir": "." },
          "stack": { "lang": "python", "image": "python:3.10-bookworm" },
          "verify": [
            { "label": "install", "command": "pip install -r requirements.txt" },
            { "label": "test", "command": "pytest -q" }
          ],
          "verify_derived_from": { "files": ["azure-pipelines.yml"] }
        }
        """;

    private readonly string _workDir = Directory.CreateTempSubdirectory("verify-derived-").FullName;
    private readonly ContextYamlBuilders _builders = new();

    public void Dispose()
    {
        if (Directory.Exists(_workDir)) Directory.Delete(_workDir, recursive: true);
    }

    [Fact]
    public async Task Bootstrap_ARepositoryWithAPipeline_ProposesStagesFromIt()
    {
        Seed(PipelineFile, Pipeline);

        (await Write(Derived)).Should().StartWith("context.yaml written:");

        var summary = Parsed();
        summary.Verify!.Select(stage => stage.Command).Should().Equal(
            ["pip install -r requirements.txt", "pytest -q"],
            "the pipeline states the commands and their order; the declaration adopts both");
        summary.VerifyDerivedFrom!.Files.Should().Equal([PipelineFile],
            "a derivation whose source nobody named is a guess nobody can check");
        summary.VerifyDerivedFrom.Hash.Should().Be(await Digest(),
            "the framework stamps the hash over what is actually on disk — a model cannot "
            + "compute one, and a hash it invented would report drift on the next run");

        Description().Should().Contain("DERIVE those commands").And.Contain("verify_derived_from.files",
            "the master is ASKED for the derivation; without that this block never appears");
    }

    [Fact]
    public async Task Bootstrap_ARepositoryWithNoPipeline_ProposesNothingRatherThanGuessing()
    {
        await Write("""{ "meta": { "workdir": "." }, "stack": { "lang": "python", "image": "python:3.10" } }""");

        var summary = Parsed();
        summary.Verify.Should().BeNull("an invented gate disagrees with the one the estate runs");
        summary.VerifyDerivedFrom.Should().BeNull();

        // The other half of "nothing rather than a guess": a source pointer attached to no
        // stages is a claim about work that was never done, so the write path drops it.
        await Write("""
            { "meta": { "workdir": "." }, "verify_derived_from": { "files": ["Makefile"] } }
            """);
        Parsed().VerifyDerivedFrom.Should().BeNull();

        Description().Should().Contain("could not find gets NO verify block",
            "the rule the master is held to is stated to the master");
    }

    [Fact]
    public async Task Run_TheDerivationSourceMoved_IsReportedStale()
    {
        Seed(PipelineFile, Pipeline);
        await Write(Derived);
        Seed(PipelineFile, Pipeline.Replace("pytest -q", "pytest -q --cov", StringComparison.Ordinal));

        var (stages, notes) = await ReportAsync();

        notes.Stale.Should().ContainSingle().Which.Should()
            .Contain(PipelineFile).And.Contain("no longer hash to what was recorded");
        notes.Findings.Should().BeEmpty("a moved source is a report, never a refusal");
        stages.Single().Stages.Select(stage => stage.Label).Should().Equal(["install", "test"],
            "the declaration runs exactly as written — this phase reports drift, it does not "
            + "re-derive, and re-deriving is the operator's call");
    }

    [Fact]
    public async Task Run_TheDerivationSourceUnchanged_SaysNothing()
    {
        Seed(PipelineFile, Pipeline);
        await Write(Derived);

        var (stages, notes) = await ReportAsync();

        notes.Stale.Should().BeEmpty("an estate changes slowly, and a report every run is noise");
        stages.Should().ContainSingle().Which.DerivedFrom!.Files.Should().Equal([PipelineFile]);
    }

    private async Task<(IReadOnlyList<ContextVerifyStages> Stages, VerifyResolutionNotes Notes)> ReportAsync()
    {
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, IReadOnlyList<RemoteContextDiscovery>>>(
            ContextKeys.SandboxContexts,
            new Dictionary<string, IReadOnlyList<RemoteContextDiscovery>>(StringComparer.Ordinal)
            {
                ["repo"] = [RemoteContextDiscovery.From("default", Parsed())],
            });
        var stages = new ContextVerifyStagesResolver().For(pipeline, "repo");
        var notes = new VerifyResolutionNotes();
        await using var sandbox = NewSandbox();
        await new VerifyDerivationDrift(NewDigest(), NullLogger<VerifyDerivationDrift>.Instance)
            .ReportAsync("repo", sandbox, stages, notes, CancellationToken.None);
        return (stages, notes);
    }

    private async Task<string> Digest()
    {
        await using var sandbox = NewSandbox();
        return await NewDigest().ComputeAsync(
            sandbox, [PipelineFile], CancellationToken.None);
    }

    private static VerifyDerivationDigest NewDigest() => new(new SandboxFileReaderFactory());

    private InProcessSandbox NewSandbox() =>
        new("job", _workDir, ownsWorkDir: false, NullLogger.Instance);

    private async Task<string> Write(string document)
    {
        await using var sandbox = NewSandbox();
        var host = new WriteContextYamlToolHost(
            new Dictionary<string, ISandbox>(StringComparer.Ordinal) { ["repo"] = sandbox },
            defaultRepo: "repo",
            new ContextYamlSerializer(_builders),
            ContextGates.Build(),
            ContextGates.Writer(),
            ContextGates.DerivationStamp());
        return await host.WriteContextYaml("repo", "default", JsonDocument.Parse(document).RootElement);
    }

    private Contracts.Models.Configuration.ContextYamlSummary Parsed() =>
        new ContextYamlSerializer(_builders)
            .Parse(File.ReadAllText(System.IO.Path.Combine(_workDir, ContextPath))).Summary!;

    private void Seed(string relative, string content)
    {
        var full = System.IO.Path.Combine(_workDir, relative);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    private static string Description() =>
        typeof(WriteContextYamlToolHost)
            .GetMethod(nameof(WriteContextYamlToolHost.WriteContextYaml))!
            .GetParameters().Single(p => p.Name == "document")
            .GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()!.Description;
}
