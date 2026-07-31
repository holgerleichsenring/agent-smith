using System.Reflection;
using AgentSmith.Contracts.WorkSpecs;

namespace AgentSmith.Application.Services.WorkSpecs;

/// <summary>
/// p0390: the work-spec JSON schema, shipped INTO the target repo next to the
/// specs it describes. Without it the yaml-language-server header on every
/// spec.yaml would dangle in a repo that has never seen this system's source.
/// </summary>
public static class WorkSpecSchemaResource
{
    private const string ResourceName =
        "AgentSmith.Application.Services.Validation.Schemas.work-spec.schema.json";

    /// <summary>Repo-relative path the schema copy is written to.</summary>
    public const string RepoPath = WorkSpecKey.Root + "/work-spec.schema.json";

    private static readonly Lazy<string> Cached = new(Read);

    public static string Text => Cached.Value;

    private static string Read()
    {
        using var stream = typeof(WorkSpecSchemaResource).Assembly
            .GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded work-spec schema '{ResourceName}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
