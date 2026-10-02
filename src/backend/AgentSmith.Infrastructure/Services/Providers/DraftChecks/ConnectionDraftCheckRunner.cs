using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

namespace AgentSmith.Infrastructure.Services.Providers.DraftChecks;

/// <summary>
/// 2026-10-02-5f89b: tests an unsaved connection. The draft takes the save path —
/// <see cref="RawConfigPatch"/> then <see cref="ConnectionCatalogBuilder"/> — so the check tests
/// what saving would store; the token comes from the credential resolver; the type's check runs.
/// </summary>
public sealed class ConnectionDraftCheckRunner(
    ICredentialResolver credentials, IEnumerable<IConnectionDraftCheck> checks, DraftCheckCollector collector)
    : IConnectionDraftCheckRunner
{
    public Task<DraftCheckReport> RunAsync(ConnectionEntity draft, CancellationToken cancellationToken)
    {
        var connection = Resolve(draft);
        var secret = DraftCheckSecret.Resolve(connection.Auth, () => credentials.For(connection));
        if (secret.Token is not { } token) return Task.FromResult(new DraftCheckReport([secret.Step]));

        var check = checks.FirstOrDefault(c => c.Type == connection.Type);
        if (check is null)
            return Task.FromResult(new DraftCheckReport([secret.Step,
                DraftCheckStep.Fail(DraftCheckStep.Host, $"No check exists for a {connection.Type} connection.")]));
        return collector.CollectAsync(secret.Step, ct => check.RunAsync(connection, token, ct), cancellationToken);
    }

    private static ResolvedConnection Resolve(ConnectionEntity draft)
    {
        var id = string.IsNullOrWhiteSpace(draft.Id) ? "draft" : draft.Id;
        var raw = new Dictionary<string, RawConnectionEntry> { [id] = RawConfigPatch.Connection(draft, existing: null) };
        // The builder's findings concern the saved catalog; the secret step below says the same live.
        return new ConnectionCatalogBuilder().Build(raw, [], [])[id];
    }
}
