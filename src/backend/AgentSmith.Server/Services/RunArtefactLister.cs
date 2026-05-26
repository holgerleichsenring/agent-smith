using AgentSmith.Server.Api.Models;

namespace AgentSmith.Server.Services;

/// <summary>
/// p0169a: enumerates files inside a run directory and surfaces them as
/// <see cref="RunArtefactResponse"/> entries for the dashboard sidebar.
/// MIME type guess is intentionally coarse — the dashboard's
/// react-markdown render path consumes raw bytes.
/// </summary>
public sealed class RunArtefactLister
{
    public IReadOnlyList<RunArtefactResponse> List(string runDir)
    {
        if (!Directory.Exists(runDir)) return [];
        var list = new List<RunArtefactResponse>();
        foreach (var file in Directory.EnumerateFiles(runDir))
        {
            var info = new FileInfo(file);
            list.Add(new RunArtefactResponse(info.Name, info.Length, GuessContentType(info.Name)));
        }
        list.Sort((a, b) => string.Compare(a.Filename, b.Filename, StringComparison.Ordinal));
        return list;
    }

    private static string GuessContentType(string filename) =>
        Path.GetExtension(filename).ToLowerInvariant() switch
        {
            ".md" => "text/markdown",
            ".json" => "application/json",
            ".yaml" or ".yml" => "application/yaml",
            ".txt" => "text/plain",
            _ => "application/octet-stream",
        };
}
