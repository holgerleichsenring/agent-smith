namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-08-31-7097: which of the derived binaries the sweep did NOT find. A tool that
/// resolves prints its own name; one that does not stays silent, so absence from the
/// output is the answer. Pure transformation over the probe's stdout.
/// </summary>
internal static class MissingBinaries
{
    public static IReadOnlyList<DeclaredStageBinary> In(
        string? probeStdout, DeclaredStageDerivation derivation)
    {
        ArgumentNullException.ThrowIfNull(derivation);
        if (derivation.Binaries.Count == 0) return [];
        var found = Reported(probeStdout);
        return [.. derivation.Binaries.Where(b => !found.Contains(b.Binary))];
    }

    private static HashSet<string> Reported(string? probeStdout)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(probeStdout)) return found;
        foreach (var line in probeStdout.Split(
                     '\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var space = line.IndexOf(' ');
            found.Add(space < 0 ? line : line[..space]);
        }
        return found;
    }
}
