using AgentSmith.Application.Services.Admin;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Pocs.Variables;

/// <summary>
/// 2026-11-23-933dg: the writes of one poc-variables run, entry by entry — a secret through SecretWrite in the PoC's
/// scope, a plain value into the PoC's plain list; a name written as one kind loses the other, a delete removes both.
/// Answers the plain list to store, or the first refusal SecretWrite gave.
/// </summary>
public sealed class PocVariableWriter(SecretWrite secrets, ISecretVault vault, TimeProvider clock)
{
    public async Task<(List<PocPlainVariable> Plain, string? Refusal)> WriteAsync(
        PocVariableWrite write, IReadOnlyList<PocPlainVariable> plain, string runId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(write);
        var scope = SecretScope.Poc(write.PocId);
        var list = plain.ToList();
        foreach (var entry in write.Entries)
        {
            list.RemoveAll(v => v.Name == entry.Name);
            if (entry.Kind == PocVariableKinds.Plain)
                list.Add(new PocPlainVariable(entry.Name, entry.Plain!, write.Person, clock.GetUtcNow()));
            var report = entry.Kind == PocVariableKinds.Secret
                ? await secrets.RunAsync(new SecretWriteRequest(entry.Name, SecretWriteRequest.Set, entry.Secret, write.Person, scope), runId, ct)
                : await DeleteSecretAsync(scope, entry.Name, write.Person, runId, ct);
            if (report is { Written: false }) return (list, report.Line);
        }
        return (list, null);
    }

    // A plain write or a delete removes a secret of that name when one is stored; without a master key none can be.
    private async Task<SecretWriteReport?> DeleteSecretAsync(SecretScope scope, string name, string person, string runId, CancellationToken ct)
    {
        if (vault.Unavailable is not null) return null;
        var stored = await vault.StatesAsync(scope, ct);
        if (!stored.Any(s => s.Name == name)) return null;
        return await secrets.RunAsync(new SecretWriteRequest(name, SecretWriteRequest.Delete, null, person, scope), runId, ct);
    }
}
