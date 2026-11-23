using System.IO.Compression;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Server.Services.Pocs.Import;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-23-933dc, real server composition with real git: poc-deploy scans the upload before anything reaches git. A
/// ZIP whose src/config.py holds an AWS access key id ends blocked with a masked finding and no commit on the source
/// repo; a clean ZIP ends with one commit under its folder.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class PocScanCompositionTests
{
    private const string Dockerfile = "FROM python:3.12-slim\nCOPY . .\nEXPOSE 8000\nUSER 10001\nCMD [\"python\", \"app.py\"]\n";

    [Fact]
    public async Task AnAwsKey_EndsBlocked_WithNoCommit_ACleanZip_IsCommittedUnderItsFolder()
    {
        await using var repo = await PocHarnessRepo.CreateAsync();
        if (repo is null) return;

        var (blocked, blockedRevision) = await ImportAndRunAsync(repo, "leaky", ("todo/src/config.py", "KEY = \"AKIAABCDEFGHIJKLMNOP\"\n"));
        (await repo.OriginAsync("rev-list", "--count", "main")).Should().Be("1", "a blocked upload never reaches git");
        var (clean, cleanRevision) = await ImportAndRunAsync(repo, "clean", ("todo/src/config.py", "KEY = 'set at runtime'\n"));

        blocked.IsSuccess.Should().BeFalse();
        blocked.Message.Should().Contain("blocked by 1 finding(s)");
        blockedRevision.State.Should().Be(PocRevisionStates.Blocked);
        PocScanVerdict.FromJson(blockedRevision.VerdictJson)!.Blocks.Should().ContainSingle()
            .Which.MaskedSnippet.Should().Be("KEY = \"<secret at src/config.py:1>\"");
        blockedRevision.VerdictJson.Should().NotContain("AKIAABCDEFGHIJKLMNOP");
        clean.IsSuccess.Should().BeTrue(clean.Message);
        (await repo.OriginAsync("rev-parse", "main")).Should().Be(cleanRevision.CommitSha);
        (await repo.OriginAsync("ls-tree", "-r", "--name-only", "main")).Split('\n')
            .Should().Contain("pocs/poc-lee/clean/src/config.py").And.NotContain(p => p.Contains("leaky"));
    }

    private static async Task<(CommandResult Result, PocRevision Revision)> ImportAndRunAsync(
        PocHarnessRepo repo, string name, (string Path, string Text) file)
    {
        repo.Queued.Clear();
        await using var harness = RealCompositionHarness.Build(repo.Fixture(), repo.Override);
        await using var scope = harness.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var (upload, refused) = await sp.GetRequiredService<PocUploadAdmission>().AdmitAsync(Lee, new PocUpload($"{name}.zip", Zip(file), null), default);
        upload.Should().NotBeNull(refused?.Reason);
        var (started, notStarted) = await sp.GetRequiredService<PocImportLauncher>()
            .ImportAsync(Lee, upload!.UploadId, new PocImportRequest("new", null, name, null, false), default);
        started.Should().NotBeNull(notStarted?.Reason);
        var request = JsonSerializer.Deserialize<PipelineRequest>(JsonSerializer.Serialize(repo.Queued.Single()))!;
        var runner = new PipelineRunner(harness.Services) { RepoOverride = repo.Repo };
        foreach (var (key, value) in request.Context!) runner.RequestContext[key] = value;
        var result = await runner.RunAsync(PipelinePresets.PocDeployName);
        var revision = await sp.GetRequiredService<AgentSmithDbContext>().Set<PocRevision>().AsNoTracking().SingleAsync(r => r.PocId == started!.PocId);
        return (result, revision);
    }

    private static ClaimsPrincipal Lee => new(new ClaimsIdentity(
        [new Claim("sub", "lee-sub"), new Claim(ClaimTypes.Name, "lee-sub"), new Claim("preferred_username", "lee")], "test"));

    private static byte[] Zip((string Path, string Text) extra)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (path, text) in new[] { ("todo/Dockerfile", Dockerfile), ("todo/app.py", "print('hi')\n"), extra })
            {
                using var entry = zip.CreateEntry(path).Open();
                entry.Write(Encoding.UTF8.GetBytes(text));
            }
        return buffer.ToArray();
    }
}
