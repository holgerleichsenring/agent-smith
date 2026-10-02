using AgentSmith.Contracts.Exceptions;
using AgentSmith.Contracts.Models.ConfigStudio;

namespace AgentSmith.Infrastructure.Services.Providers.DraftChecks;

/// <summary>
/// 2026-10-02-5f89b: the first step — the picked secret has a value, read through the same
/// <c>ICredentialResolver</c> a saved entity's calls use. The step names the secret, never the value.
/// </summary>
public sealed record DraftCheckSecret(DraftCheckStep Step, string? Token)
{
    public static DraftCheckSecret Resolve(string secretName, Func<string> resolve)
    {
        try
        {
            var token = resolve();
            return new(DraftCheckStep.Pass(DraftCheckStep.Secret, $"Secret '{secretName}' has a value."), token);
        }
        catch (MissingCredentialException ex)
        {
            return new(DraftCheckStep.Fail(DraftCheckStep.Secret, ex.Message), null);
        }
    }
}
