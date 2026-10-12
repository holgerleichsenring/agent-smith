namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0422: splits a diff into windows that fit one call, at FILE boundaries.
/// <para>
/// Truncating was the wrong answer to a big diff — it silently dropped whole
/// repositories, and a criterion about a file past the cut came back "not delivered"
/// over work that was there. Splitting is the right one because evidence is MONOTONE:
/// what a window shows, it shows, whatever the other windows contain. So each window is
/// asked which criteria IT satisfies and the answers are unioned.
/// </para>
/// <para>
/// The budget is grounded rather than guessed: 245 real worker calls in one day ranged
/// from 3.7k to 5.2M characters with a median of 205k, so a window of 400k sits inside
/// what a 200k-token context takes while leaving room for the criteria and the
/// instructions. A single file bigger than a window is the one case that must still be
/// cut, and it says so in the text.
/// </para>
/// </summary>
public static class DiffWindows
{
    public const int DefaultBudgetChars = 400_000;

    public static IReadOnlyList<string> Split(string diff, int budgetChars = DefaultBudgetChars)
    {
        if (string.IsNullOrEmpty(diff)) return [];
        if (diff.Length <= budgetChars) return [diff];

        var windows = new List<string>();
        var current = new System.Text.StringBuilder();
        foreach (var file in Files(diff))
        {
            var piece = file.Length <= budgetChars
                ? file
                : file[..budgetChars] + "\n… this file's diff is longer than one window and is cut here\n";
            if (current.Length > 0 && current.Length + piece.Length > budgetChars)
            {
                windows.Add(current.ToString());
                current.Clear();
            }
            current.Append(piece);
        }
        if (current.Length > 0) windows.Add(current.ToString());
        return windows;
    }

    /// <summary>Each file's diff, header included — never split mid-file. A header is found
    /// only where git writes one, at a line start (2026-10-12-fbbdd).</summary>
    private static IEnumerable<string> Files(string diff)
    {
        var starts = SpecDiffText.Boundaries(diff);
        if (starts.Count == 0)
        {
            yield return diff;
            yield break;
        }
        if (starts[0] > 0) yield return diff[..starts[0]];
        for (var i = 0; i < starts.Count; i++)
            yield return diff[starts[i]..(i + 1 < starts.Count ? starts[i + 1] : diff.Length)];
    }
}
