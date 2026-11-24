using System.Runtime.CompilerServices;
using AgentSmith.Contracts.Exceptions;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Services.Providers.DraftChecks;

/// <summary>
/// 2026-11-23-933de: the Test action of a PoC target's registry section. The draft's own problems refuse it before any
/// request; pull_auth is read from the secrets catalog ONLY — never environment first as a PoC run reads it — so a
/// draft can never send a server environment variable to a login server it names. Then the probe answers.
/// </summary>
public sealed class PocRegistryDraftCheck(
    ISecretValues secrets, IContainerRegistryFactory registries, DraftCheckCollector collector) : IPocRegistryDraftCheck
{
    public Task<DraftCheckReport> RunAsync(PocRegistryConfig draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var problems = draft.Problems().ToList();
        if (problems.Count > 0)
            return Task.FromResult(new DraftCheckReport([DraftCheckStep.Fail(DraftCheckStep.Values, string.Join("; ", problems))]));
        var (step, token) = Secret(draft.PullAuth);
        return token is null
            ? Task.FromResult(new DraftCheckReport([step]))
            : collector.CollectAsync(step, ct => ProbeAsync(draft, token, ct), cancellationToken);
    }

    private (DraftCheckStep Step, string? Token) Secret(string name)
    {
        if (secrets.Resolve(name) is { } token)
            return (DraftCheckStep.Pass(DraftCheckStep.Secret, $"Secret '{name}' has a value."), token);
        var detail = secrets.Unreadable(name) is { } reason
            ? $"The stored value of secret '{name}' cannot be read: {reason}"
            : $"{PocRegistryConfig.Key}.pull_auth names '{name}', which is not a secret of the secrets catalog with a value.";
        return (DraftCheckStep.Fail(DraftCheckStep.Secret, detail), null);
    }

    private async IAsyncEnumerable<DraftCheckStep> ProbeAsync(
        PocRegistryConfig draft, string token, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        RegistryProbe? probe = null;
        string? refusal = null;
        try
        {
            probe = await registries.Create(draft, token).ProbeAsync(cancellationToken);
        }
        catch (ContainerRegistryException ex)
        {
            refusal = ex.Message;
        }
        yield return probe is null
            ? DraftCheckStep.Fail(DraftCheckStep.Host, refusal!)
            : probe.Reached
                ? DraftCheckStep.Pass(DraftCheckStep.Scope, probe.Sentence)
                : DraftCheckStep.Fail(DraftCheckStep.Scope, probe.Sentence);
    }
}
