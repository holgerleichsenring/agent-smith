using System.IO.Compression;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Pocs.Import;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-23-933db, real server composition: a person without a project uploads a PoC ZIP and starts it as a new PoC —
/// the server writes their binding and their poc- project through the real configuration store, the Poc row and its
/// revision, and queues poc-deploy; the run, built from the queued seeds, takes the archive in to a validated revision
/// with its port and files; 2026-11-23-933dc: then scans it and commits it to the source repo; 2026-11-23-933dd: and the
/// host's pipeline (faked) builds, scans and pushes it.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class PocDeployCompositionTests
{
    private const string LeesProject = "poc-lee";
    private const string Dockerfile = "FROM python:3.12-slim\nCOPY . .\nEXPOSE 8000\nCMD [\"python\", \"app.py\"]\n";

    [Fact]
    public async Task FirstImport_ProvisionsTheProject_AndPocDeployValidatesTheRevision()
    {
        await using var repo = await PocHarnessRepo.CreateAsync(); // 2026-11-23-933dc: the run now scans and commits
        if (repo is null) return;
        var queued = repo.Queued;
        await using var harness = RealCompositionHarness.Build(repo.Fixture(), repo.Override);
        await using var scope = harness.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        var (upload, refused) = await sp.GetRequiredService<PocUploadAdmission>().AdmitAsync(Lee, new PocUpload("todo.zip", Zip(), null), default);
        upload.Should().NotBeNull(refused?.Reason);
        var (started, notStarted) = await sp.GetRequiredService<PocImportLauncher>()
            .ImportAsync(Lee, upload!.UploadId, new PocImportRequest("new", null, "todo", null, false), default);
        started.Should().NotBeNull(notStarted?.Reason);

        sp.GetRequiredService<IConfigStore>().GetProjects().Should().ContainSingle(p => p.Id == "poc-lee")
            .Which.Repos.Should().Equal("poc-src");
        sp.GetRequiredService<RoleMappingSource>().Current().Mapping.ProjectBindings.Should().ContainSingle(b => b.Project == "poc-lee");
        var request = JsonSerializer.Deserialize<PipelineRequest>(JsonSerializer.Serialize(queued.Single()))!;
        (request.ProjectName, request.PipelineName).Should().Be(("poc-lee", PipelinePresets.PocDeployName));

        var runner = new PipelineRunner(harness.Services) { RepoOverride = repo.Repo };
        foreach (var (key, value) in request.Context!) runner.RequestContext[key] = value;
        var result = await runner.RunAsync(PipelinePresets.PocDeployName);

        result.IsSuccess.Should().BeTrue(result.Message);
        var revision = await sp.GetRequiredService<AgentSmithDbContext>().Set<PocRevision>().AsNoTracking().SingleAsync(r => r.PocId == started!.PocId);
        (revision.State, revision.Port).Should().Be((PocRevisionStates.Built, 8000), "933dd: the run ends after the image is pushed");
        revision.ManifestJson.Should().Contain("app.py");
        revision.CommitSha.Should().Be(await repo.OriginAsync("rev-parse", "main"), "933dc: a clean revision is committed");
        revision.Digest.Should().Be(PocHarnessBuildHost.Digest, "933dd: the pushed digest the registry confirmed");
        repo.Build.Starts.Single().Inputs["sha"].Should().Be(revision.CommitSha);
    }

    [Fact]
    public async Task Import_ACriticalInTheImage_EndsBlockedAtTheImageStage()
    {
        await using var repo = await PocHarnessRepo.CreateAsync();
        if (repo is null) return;
        repo.Build.CriticalInImage = true;
        await using var harness = RealCompositionHarness.Build(repo.Fixture(), repo.Override);
        await using var scope = harness.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var (upload, _) = await sp.GetRequiredService<PocUploadAdmission>().AdmitAsync(Lee, new PocUpload("todo.zip", Zip(), null), default);
        var (started, _) = await sp.GetRequiredService<PocImportLauncher>()
            .ImportAsync(Lee, upload!.UploadId, new PocImportRequest("new", null, "todo", null, false), default);
        var runner = new PipelineRunner(harness.Services) { RepoOverride = repo.Repo };
        foreach (var (key, value) in repo.Queued.Single().Context!) runner.RequestContext[key] = value;

        var result = await runner.RunAsync(PipelinePresets.PocDeployName);

        result.IsSuccess.Should().BeFalse();
        var revision = await sp.GetRequiredService<AgentSmithDbContext>().Set<PocRevision>().AsNoTracking().SingleAsync(r => r.PocId == started!.PocId);
        (revision.State, revision.BlockedStage).Should().Be((PocRevisionStates.Blocked, PocScanStages.Image));
        PocScanVerdict.FromJson(revision.ImageVerdictJson)!.Blocks.Should().ContainSingle(b => b.Id == "trivy:CVE-2024-933|openssl|3.0.1");
        (await sp.GetRequiredService<AgentSmithDbContext>().Set<Poc>().AsNoTracking().SingleAsync(p => p.Id == started!.PocId)).State
            .Should().Be(PocStates.Blocked);
    }

    [Fact]
    public async Task ActRun_AGivenRunId_IsTheRunsId_AndAnotherPresetIsRefusedInThePocProject()
    {
        await using var repo = await PocHarnessRepo.CreateAsync(); // 2026-11-23-933dc: the run now scans and commits
        if (repo is null) return;
        var queued = repo.Queued;
        await using var harness = RealCompositionHarness.Build(repo.Fixture(), repo.Override);
        await using var scope = harness.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var (upload, _) = await sp.GetRequiredService<PocUploadAdmission>().AdmitAsync(Lee, new PocUpload("todo.zip", Zip(), null), default);
        var (started, _) = await sp.GetRequiredService<PocImportLauncher>()
            .ImportAsync(Lee, upload!.UploadId, new PocImportRequest("new", null, "todo", null, false), default);
        var acts = sp.GetRequiredService<AgentSmith.Server.Services.Acts.IActRun>();

        var deployed = await acts.RunAsync(LeesProject, PipelinePresets.PocDeployName, queued.Single().Context!, runId: started!.RunId);
        var code = await acts.RunAsync(LeesProject, PipelinePresets.CodeName, new Dictionary<string, object>());

        deployed.IsSuccess.Should().BeTrue(deployed.Message);
        var revision = await sp.GetRequiredService<AgentSmithDbContext>().Set<PocRevision>().AsNoTracking().SingleAsync(r => r.PocId == started.PocId);
        revision.RunId.Should().Be(started.RunId, "the run ran under the id the gate claimed");
        code.IsSuccess.Should().BeFalse();
        code.Message.Should().Contain("poc- projects run only the PoC presets");
    }

    private static ClaimsPrincipal Lee => new(new ClaimsIdentity(
        [new Claim("sub", "lee-sub"), new Claim(ClaimTypes.Name, "lee-sub"), new Claim("preferred_username", "lee")], "test"));

    private static byte[] Zip()
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, text) in new[] { ("todo/Dockerfile", Dockerfile), ("todo/app.py", "print('hi')\n") })
            {
                using var entry = zip.CreateEntry(path).Open();
                entry.Write(Encoding.UTF8.GetBytes(text));
            }
        }
        return buffer.ToArray();
    }
}
