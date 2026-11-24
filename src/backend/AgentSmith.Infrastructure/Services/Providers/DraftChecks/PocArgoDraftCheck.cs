using System.Runtime.CompilerServices;
using AgentSmith.Contracts.Exceptions;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.DraftChecks;

/// <summary>
/// 2026-11-23-c81ec: the Test action of a PoC target's Argo CD section. The draft's own problems refuse it before any
/// request; token_secret is read from the secrets catalog ONLY (never environment first, as a run reads it), so a draft
/// can never send a server environment variable to a URL it names. Then the token's expiry, the probe, and the
/// ApplicationSet's presence — the state the last poc-target-install left.
/// </summary>
public sealed class PocArgoDraftCheck(
    ISecretValues secrets, IArgoCdClientFactory argos, DraftCheckCollector collector, TimeProvider clock, ILogger<PocArgoDraftCheck> logger)
    : IPocArgoDraftCheck
{
    public Task<DraftCheckReport> RunAsync(PocArgoCheckRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var problems = request.Argo.Problems(request.Target).ToList();
        if (problems.Count > 0)
            return Task.FromResult(new DraftCheckReport([DraftCheckStep.Fail(DraftCheckStep.Values, string.Join("; ", problems))]));
        var (step, token) = Secret(request.Argo.TokenSecret);
        return token is null
            ? Task.FromResult(new DraftCheckReport([step]))
            : collector.CollectAsync(step, ct => StepsAsync(request, token, ct), cancellationToken);
    }

    private (DraftCheckStep Step, string? Token) Secret(string name)
    {
        if (secrets.Resolve(name) is { } token)
            return (DraftCheckStep.Pass(DraftCheckStep.Secret, $"Secret '{name}' has a value."), token);
        var detail = secrets.Unreadable(name) is { } reason
            ? $"The stored value of secret '{name}' cannot be read: {reason}"
            : $"{PocArgoConfig.Key}.token_secret names '{name}', which is not a secret of the secrets catalog with a value.";
        return (DraftCheckStep.Fail(DraftCheckStep.Secret, detail), null);
    }

    private async IAsyncEnumerable<DraftCheckStep> StepsAsync(
        PocArgoCheckRequest request, string token, [EnumeratorCancellation] CancellationToken ct)
    {
        yield return TokenStep(request.Argo, token);
        var client = argos.Create(request.Argo, token);
        var (probe, refusal) = await ProbeAsync(client, ct);
        yield return probe is null ? DraftCheckStep.Fail(DraftCheckStep.Host, refusal!)
            : probe.Reached ? DraftCheckStep.Pass(DraftCheckStep.Scope, probe.Sentence) : DraftCheckStep.Fail(DraftCheckStep.Scope, probe.Sentence);
        yield return await ApplicationSetStepAsync(client, request.Argo.ApplicationSetName(request.Target), ct);
    }

    private DraftCheckStep TokenStep(PocArgoConfig argo, string token)
    {
        var (expiry, refusal) = ArgoTokenExpiry.Read(token);
        if (expiry is null) return DraftCheckStep.Fail(DraftCheckStep.Token, refusal!);
        var now = clock.GetUtcNow();
        var said = expiry.Sentence(now, argo.TokenWarnDays);
        return expiry.ExpiresAt is { } at && at <= now ? DraftCheckStep.Fail(DraftCheckStep.Token, said) : DraftCheckStep.Pass(DraftCheckStep.Token, said);
    }

    private async Task<(ArgoProbe? Probe, string? Refusal)> ProbeAsync(IArgoCdClient client, CancellationToken ct)
    {
        try
        {
            return (await client.ProbeAsync(ct), null);
        }
        catch (ArgoCdException ex)
        {
            logger.LogInformation(ex, "The Argo CD draft probe was refused");
            return (null, ex.Message);
        }
    }

    private async Task<DraftCheckStep> ApplicationSetStepAsync(IArgoCdClient client, string name, CancellationToken ct)
    {
        try
        {
            return await client.GetApplicationSetAsync(name, ct) is not null
                ? DraftCheckStep.Pass(DraftCheckStep.ApplicationSet, $"The ApplicationSet {name} exists; poc-target-install keeps it as rendered.")
                : DraftCheckStep.Fail(DraftCheckStep.ApplicationSet, $"The ApplicationSet {name} does not exist yet; run Install for this target.");
        }
        catch (ArgoCdException ex)
        {
            logger.LogInformation(ex, "The ApplicationSet {Name} could not be read", name);
            return DraftCheckStep.Fail(DraftCheckStep.ApplicationSet, ex.Message);
        }
    }
}
