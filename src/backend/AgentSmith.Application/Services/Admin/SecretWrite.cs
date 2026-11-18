using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Admin;

/// <summary>
/// 2026-11-17-f3a8d: one secret write as the secret run does it. A value is stored under a catalog secret, so setting
/// one for a name the catalog lacks adds the name first; a value shorter than the masker masks is refused, because a
/// run would otherwise print it unmasked. Every line names the secret and the operation, never the value.
/// </summary>
public sealed class SecretWrite(ISecretVault vault, IConfigStore store)
{
    public async Task<SecretWriteReport> RunAsync(SecretWriteRequest request, string runId, CancellationToken ct)
    {
        if (vault.Unavailable is { } unavailable) return new SecretWriteReport(false, unavailable);
        return request.Operation switch
        {
            SecretWriteRequest.Set => await SetAsync(request, runId, ct),
            SecretWriteRequest.Delete => await vault.DeleteAsync(request.Name, ct)
                ? new SecretWriteReport(true, $"The stored value of '{request.Name}' was deleted in run {runId}.")
                : new SecretWriteReport(false, $"'{request.Name}' has no stored value."),
            SecretWriteRequest.Rewrap => new SecretWriteReport(true,
                $"{await vault.RewrapAsync(ct)} stored value(s) re-wrapped under the current master key in run {runId}."),
            _ => new SecretWriteReport(false, $"'{request.Operation}' is not a secret operation (set, delete, rewrap)."),
        };
    }

    private async Task<SecretWriteReport> SetAsync(SecretWriteRequest request, string runId, CancellationToken ct)
    {
        if (request.Value is not { } value || value.Length < SecretMasker.MinimumMaskedLength)
            return new SecretWriteReport(false, $"A value for '{request.Name}' must be at least "
                + $"{SecretMasker.MinimumMaskedLength} characters: a shorter one would not be masked in what runs record.");
        if (!store.GetSecrets().Any(s => string.Equals(s.Id, request.Name, StringComparison.OrdinalIgnoreCase)))
            store.UpsertSecret(new SecretEntity(request.Name), new ChangeAttribution($"{request.Person} (run {runId})"));
        await vault.SetAsync(request.Name, value, request.Person, ct);
        return new SecretWriteReport(true, $"The value of '{request.Name}' was stored encrypted by {request.Person} in run {runId}.");
    }
}
