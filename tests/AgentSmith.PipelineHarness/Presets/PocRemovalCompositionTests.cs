using System.IO.Compression;
using System.Security.Claims;
using System.Text;
using AgentSmith.Application.Services.Pocs.Lifetime;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Server.Services.Pocs.Acts;
using AgentSmith.Server.Services.Pocs.Import;
using AgentSmith.Server.Services.Pocs.Lifetime;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-23-c81ee, real server composition on SQLite with the faked git host and Argo: a PoC blocked before it ever
/// deployed keeps its upload pocs.blocked_retention_days; the purge run deletes the 8-day-old upload, the lifetime plan
/// then finds it due for removal, and the poc-delete it starts commits nothing (no deploy folder exists), settles the
/// PoC deleted with its name free — so its owner, at a per_person_limit of 1, imports a new PoC of that name.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class PocRemovalCompositionTests
{
    [Fact]
    public async Task Removal_AfterThePurge_DeletesTheBlockedPoc_FreeingItsNameAndItsSlot()
    {
        await using var repo = await PocHarnessRepo.CreateAsync();
        if (repo is null) return;
        var fixture = repo.Fixture();
        File.WriteAllText(fixture, File.ReadAllText(fixture).Replace("    - name: main\n", "    - name: main\n      per_person_limit: 1\n", StringComparison.Ordinal));
        var events = new ActRunEvents();
        await using var harness = RealCompositionHarness.Build(fixture, services =>
        {
            repo.Override(services);
            services.RemoveAll<IEventPublisher>().AddSingleton<IEventPublisher>(events);
        });
        await using var scope = harness.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var pocId = await ImportBlockedAsync(sp);
        var head = await repo.OriginAsync("rev-parse", "main");

        var purge = await new PipelineRunner(harness.Services) { RepoOverride = repo.Repo }.RunAsync(PipelinePresets.PocUploadPurgeName);
        purge.IsSuccess.Should().BeTrue(purge.Message);
        await DueAsync(sp, pocId);
        await LaunchAndRunAsync(sp, harness.Services, repo, pocId);

        var row = await RowAsync(sp, pocId);
        (row.State, row.LiveName).Should().Be((PocStates.Deleted, (string?)null));
        await events.ListedRunsAsync(harness.Services); // the server's drain: the act projection writes the audit entry
        var audit = await sp.GetRequiredService<AgentSmithDbContext>().Set<PocAuditEntry>().AsNoTracking()
            .Where(e => e.PocId == pocId && e.Act == PocActs.Deleted).ToListAsync();
        audit.Should().ContainSingle().Which.Reason.Should().Contain(PocsConfig.BlockedRetentionDaysKey).And.Contain("never deployed");
        (await repo.OriginAsync("rev-parse", "main")).Should().Be(head, "a PoC that never deployed has no deploy folder to remove");
        var (again, refused) = await ImportAsync(sp);
        again.Should().NotBeNull(refused?.Reason);
        again!.PocId.Should().NotBe(pocId);
    }

    private static async Task<long> ImportBlockedAsync(IServiceProvider sp)
    {
        var (started, refused) = await ImportAsync(sp);
        started.Should().NotBeNull(refused?.Reason);
        var db = sp.GetRequiredService<AgentSmithDbContext>();
        await db.Set<Poc>().Where(p => p.Id == started!.PocId).ExecuteUpdateAsync(s => s.SetProperty(p => p.State, PocStates.Blocked));
        var revision = await db.Set<PocRevision>().AsNoTracking().SingleAsync(r => r.PocId == started!.PocId);
        await db.Set<PocRevision>().Where(r => r.Id == revision.Id).ExecuteUpdateAsync(s => s.SetProperty(r => r.State, PocRevisionStates.Blocked));
        var run = await db.Set<Run>().SingleAsync(r => r.Id == started!.RunId); // the blocked poc-deploy run ended
        (run.FinishedAt, run.Status) = (DateTimeOffset.UtcNow, AgentSmith.Contracts.Runs.RunStatuses.Failed);
        await db.SaveChangesAsync();
        var created = DateTimeOffset.UtcNow.AddDays(-8);
        await db.Set<ReferenceFile>().Where(f => f.SessionId == revision.UploadSession && f.SetId == revision.UploadId)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.CreatedAt, created));
        return started!.PocId;
    }

    private static async Task<(PocImportAnswer? Answer, PocRefusal? Refusal)> ImportAsync(IServiceProvider sp)
    {
        var (upload, _) = await sp.GetRequiredService<PocUploadAdmission>().AdmitAsync(Lee, new PocUpload("todo.zip", Zip(), null), default);
        return await sp.GetRequiredService<PocImportLauncher>().ImportAsync(Lee, upload!.UploadId, new PocImportRequest("new", null, "todo", null, false), default);
    }

    private static async Task DueAsync(IServiceProvider sp, long pocId)
    {
        var subject = await sp.GetRequiredService<IPocLifetimeStore>().ReadAsync(pocId, default);
        subject!.UploadGone.Should().BeTrue("the purge deleted the 8-day-old upload");
        sp.GetRequiredService<PocLifetimePlan>().CaseOf(subject, null, new PocLifetimeConfig(), DateTimeOffset.UtcNow).Should().Be(PocLifetimeCases.Removal);
    }

    private static async Task LaunchAndRunAsync(IServiceProvider sp, IServiceProvider services, PocHarnessRepo repo, long pocId)
    {
        var poc = await RowAsync(sp, pocId);
        var (answer, refusal) = await sp.GetRequiredService<PocActLauncher>().LaunchAsync(poc, PocActKinds.Delete,
            new PocActRequest(ConfirmName: poc.Name), PocLifetimePass.Actor, default, lifetimeCase: PocLifetimeCases.Removal);
        answer.Should().NotBeNull(refusal?.Reason);
        var runner = new PipelineRunner(services) { RepoOverride = repo.Repo };
        foreach (var (key, value) in repo.Queued.Single(q => q.RunId == answer!.RunId).Context!) runner.RequestContext[key] = value;
        var result = await runner.RunAsync(PipelinePresets.PocDeleteName);
        result.IsSuccess.Should().BeTrue(result.Message);
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
