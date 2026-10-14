namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// Which paths two sandboxes of one repository changed differently, and what every patch
/// between them leaves out. A conflict is one path with two DIFFERENT post-images; the same
/// bytes in two clones disagree about nothing. A receiver never gets a path of its own
/// change, and a path several senders carry with one post-image is carried by the first
/// sender in the given order only.
/// </summary>
public sealed class SandboxTreeConflicts
{
    private readonly IReadOnlyList<(string Key, IReadOnlyDictionary<string, string> Own)> _sandboxes;

    private SandboxTreeConflicts(IReadOnlyList<(string Key, IReadOnlyDictionary<string, string> Own)> sandboxes)
    {
        _sandboxes = sandboxes;
        Conflicts = [.. sandboxes
            .SelectMany(s => s.Own.Select(change => (Path: change.Key, s.Key, Image: change.Value)))
            .GroupBy(c => c.Path, StringComparer.Ordinal)
            .Where(g => g.Select(c => c.Image).Distinct(StringComparer.Ordinal).Count() > 1)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => (g.Key, (IReadOnlyList<string>)[.. g.Select(c => c.Key)]))];
    }

    /// <param name="sandboxes">Each sandbox's own change, path to post-image, primary first.</param>
    public static SandboxTreeConflicts Of(
        IReadOnlyList<(string Key, IReadOnlyDictionary<string, string> Own)> sandboxes) =>
        new(sandboxes ?? throw new ArgumentNullException(nameof(sandboxes)));

    /// <summary>Every path changed to different post-images, with the keys that changed it, in ordinal order.</summary>
    public IReadOnlyList<(string Path, IReadOnlyList<string> Keys)> Conflicts { get; }

    /// <summary>Every conflicting path and its sandboxes, or null when there is none.</summary>
    public string? Describe() => Conflicts.Count == 0
        ? null
        : string.Join("; ", Conflicts.Select(c => $"{c.Path} was changed in both {string.Join(" and ", c.Keys)}"));

    /// <summary>The paths the patch from <paramref name="sender"/> to <paramref name="receiver"/> leaves out.</summary>
    public IReadOnlyList<string> Excluded(string receiver, string sender)
    {
        var excluded = new SortedSet<string>(Own(receiver).Keys, StringComparer.Ordinal);
        var earlier = _sandboxes.TakeWhile(s => s.Key != sender).Where(s => s.Key != receiver).ToList();
        foreach (var path in Own(sender).Keys)
            if (earlier.Any(s => s.Own.ContainsKey(path)))
                excluded.Add(path);
        return [.. excluded];
    }

    private IReadOnlyDictionary<string, string> Own(string key) =>
        _sandboxes.First(s => s.Key == key).Own;
}
