using YamlDotNet.Serialization;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-10-12-3a36e: writes a draft's top-level <c>repos:</c> into its YAML TEXT — any block
/// the draft stated is taken out and the decided one appended — so the text a filing stores
/// says what the draft says, and the rest of what the model wrote, comments included, stays.
/// </summary>
public sealed class PhaseDraftReposWriter
{
    private const string ReposKey = "repos:";

    private readonly ISerializer _serializer = new SerializerBuilder().WithQuotingNecessaryStrings().Build();

    public string Write(string yaml, IReadOnlyList<string> repos)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        ArgumentNullException.ThrowIfNull(repos);
        var kept = Without(yaml.Split('\n')).ToList();
        var body = string.Join('\n', kept).TrimEnd();
        return body + "\n" + _serializer.Serialize(new Dictionary<string, object> { ["repos"] = repos });
    }

    // The stated block is its key line and every line under it that is indented, a list item or blank.
    private static IEnumerable<string> Without(IEnumerable<string> lines)
    {
        var inRepos = false;
        foreach (var line in lines)
        {
            if (line.StartsWith(ReposKey, StringComparison.Ordinal)) { inRepos = true; continue; }
            if (inRepos && (line.Length == 0 || char.IsWhiteSpace(line[0]) || line[0] == '-')) continue;
            inRepos = false;
            yield return line;
        }
    }
}
