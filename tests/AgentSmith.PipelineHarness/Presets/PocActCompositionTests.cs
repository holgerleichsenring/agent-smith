using System.IO.Compression;
using System.Security.Claims;
using System.Text;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Server.Services.Pocs.Import;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-23-c81ed, real server composition: after poc-deploy put a PoC on main and it settled ready, poc-stop commits
/// replicas 0 to the deploy folder and the faked Argo (read from main) settles it stopped; poc-start commits replicas 1
/// and it settles ready again; poc-delete removes the folder, the Application goes and the PoC is deleted with its name
/// free. Each act is one commit on main; the source folder stays.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class PocActCompositionTests
{
    [Fact]
    public async Task StopThenStartThenDelete_CommitReplicasZeroThenOne_ThenRemoveTheFolder_AndEachSettles()
    {
        await using var repo = await PocHarnessRepo.CreateAsync();
        if (repo is null) return;
        await using var harness = RealCompositionHarness.Build(repo.Fixture(), repo.Override);
        await using var scope = harness.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var pocId = await DeployAsync(sp, harness.Services, repo);

        await ActAsync(harness.Services, repo, PipelinePresets.PocStopName, pocId);
        (await repo.OriginAsync("show", "main:deploy/todo/deployment.yaml")).Should().Contain("replicas: 0");
        (await RowAsync(sp, pocId)).State.Should().Be(PocStates.Stopped);

        await ActAsync(harness.Services, repo, PipelinePresets.PocStartName, pocId);
        (await repo.OriginAsync("show", "main:deploy/todo/deployment.yaml")).Should().Contain("replicas: 1");
        (await RowAsync(sp, pocId)).State.Should().Be(PocStates.Ready);

        var head = await repo.OriginAsync("rev-parse", "main");
        await ActAsync(harness.Services, repo, PipelinePresets.PocDeleteName, pocId);
        (await repo.OriginAsync("rev-list", "--count", $"{head}..main")).Trim().Should().Be("1", "a delete is one commit");
        (await repo.OriginAsync("ls-tree", "-r", "--name-only", "main")).Should().NotContain("deploy/todo/").And.Contain("app.py");
        var deleted = await RowAsync(sp, pocId);
        (deleted.State, deleted.LiveName, deleted.Revision).Should().Be((PocStates.Deleted, (string?)null, (int?)null));
    }

    private static async Task ActAsync(IServiceProvider services, PocHarnessRepo repo, string preset, long pocId)
    {
        var before = await repo.OriginAsync("rev-parse", "main");
        var runner = new PipelineRunner(services) { RepoOverride = repo.Repo };
        runner.RequestContext[ContextKeys.PocId] = pocId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        runner.RequestContext[ContextKeys.PocActor] = "lee-sub";
        var result = await runner.RunAsync(preset);
        result.IsSuccess.Should().BeTrue($"{preset}: {result.Message}");
        (await repo.OriginAsync("rev-list", "--count", $"{before}..main")).Trim().Should().Be("1", $"{preset} is one commit on main");
    }

    private static async Task<long> DeployAsync(IServiceProvider sp, IServiceProvider services, PocHarnessRepo repo)
    {
        var (upload, _) = await sp.GetRequiredService<PocUploadAdmission>().AdmitAsync(Lee, new PocUpload("todo.zip", Zip(), null), default);
        var (started, refused) = await sp.GetRequiredService<PocImportLauncher>()
            .ImportAsync(Lee, upload!.UploadId, new PocImportRequest("new", null, "todo", null, false), default);
        started.Should().NotBeNull(refused?.Reason);
        var runner = new PipelineRunner(services) { RepoOverride = repo.Repo };
        foreach (var (key, value) in repo.Queued.Single().Context!) runner.RequestContext[key] = value;
        var result = await runner.RunAsync(PipelinePresets.PocDeployName);
        result.IsSuccess.Should().BeTrue(result.Message);
        (await RowAsync(sp, started!.PocId)).State.Should().Be(PocStates.Ready);
        return started.PocId;
    }

    private static Task<Poc> RowAsync(IServiceProvider sp, long pocId) =>
        sp.GetRequiredService<AgentSmithDbContext>().Set<Poc>().AsNoTracking().SingleAsync(p => p.Id == pocId);

    private static ClaimsPrincipal Lee => new(new ClaimsIdentity(
        [new Claim("sub", "lee-sub"), new Claim(ClaimTypes.Name, "lee-sub"), new Claim("preferred_username", "lee")], "test"));

    private static byte[] Zip()
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, text) in new[] { ("todo/Dockerfile", "FROM python:3.12-slim\nCOPY . .\nEXPOSE 8000\nCMD [\"python\", \"app.py\"]\n"), ("todo/app.py", "print('hi')\n") })
            {
                using var entry = zip.CreateEntry(path).Open();
                entry.Write(Encoding.UTF8.GetBytes(text));
            }
        }
        return buffer.ToArray();
    }
}
