using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Server.Services.Pocs.Variables;

/// <summary>2026-11-23-933dg: the posted variables as a write's entries — a secret value straight into its redacting
/// holder, a plain one as text, and each name to delete.</summary>
internal static class PocVariableEntries
{
    public static IReadOnlyList<PocVariableEntry> Of(IReadOnlyList<PocVariableInput>? set, IReadOnlyList<string>? delete) =>
        [.. (set ?? []).Select(Entry), .. (delete ?? []).Select(name => new PocVariableEntry(name.Trim(), PocVariableKinds.Delete))];

    private static PocVariableEntry Entry(PocVariableInput input) => input.Secret
        ? new PocVariableEntry((input.Name ?? string.Empty).Trim(), PocVariableKinds.Secret, Secret: new SecretInput(input.Value ?? string.Empty))
        : new PocVariableEntry((input.Name ?? string.Empty).Trim(), PocVariableKinds.Plain, Plain: input.Value ?? string.Empty);
}
