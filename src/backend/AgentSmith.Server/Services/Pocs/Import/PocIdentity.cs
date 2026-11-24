using AgentSmith.Application.Services.Pocs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Persistence.Repositories;

namespace AgentSmith.Server.Services.Pocs.Import;

/// <summary>
/// 2026-11-23-933db: compares an admitted archive with a PoC's latest validated revision — the comparison base, because
/// agent smith is the folder's only writer. A PoC with no validated revision has nothing to protect: the verdict is
/// same. Asked at admission for the answer and again at the import, against the PoC actually chosen.
/// </summary>
internal sealed class PocIdentity(PocImportRepository imports)
{
    public async Task<PocIdentityAnswer> CompareAsync(long pocId, PocArchiveAdmission admitted, PocArchiveLimits limits, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(admitted);
        var latest = await imports.LatestValidatedAsync(pocId, ct);
        var manifest = latest?.ManifestJson is { } json ? PocManifest.FromJson(json) : null;
        var match = PocIdentityGuard.Compare(manifest, latest?.Port, latest?.Entrypoint, admitted.Entries, admitted.Dockerfile, limits);
        return new PocIdentityAnswer(match, latest?.Number, latest?.Port);
    }
}
