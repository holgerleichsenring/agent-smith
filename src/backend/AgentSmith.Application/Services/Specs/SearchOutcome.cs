using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0484: what a search's exit status MEANS, split from running it.
/// <para>
/// grep exits 1 BECAUSE it found nothing, and that is the proof an absence criterion asks
/// for — the same inversion p0469 had to amend the account's prompt for. A reader that folds
/// "found nothing" together with "could not run" has the proof backwards, so the two are
/// worded as far apart as they are meant.
/// </para>
/// </summary>
internal static class SearchOutcome
{
    /// <summary>Enough of a match list to show what is there, and no more: the account reads
    /// this inside a call that already carries the whole diff.</summary>
    private const int MaxOutputChars = 4_000;

    internal static string Report(StepResult result, string repository, string? path, string pattern)
    {
        ArgumentNullException.ThrowIfNull(result);
        var where = $"{repository}{(string.IsNullOrWhiteSpace(path) ? string.Empty : "/" + path)}";
        var output = (result.OutputContent ?? string.Empty).Trim();
        return result.ExitCode switch
        {
            0 => $"'{pattern}' found in {where}:\n"
                 + (output.Length <= MaxOutputChars
                     ? output
                     : output[..MaxOutputChars] + "\n… more matches follow"),
            1 => $"'{pattern}' does not occur anywhere in {where}.",
            _ => $"The search of {where} could not run (exit {result.ExitCode}) and proves nothing: {output}",
        };
    }
}
