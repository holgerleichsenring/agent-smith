using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// p0285: reads a <c>project.repos[]</c> item as either a scalar (<c>connection/name</c> or a
/// catalog name) -> <see cref="RawRepoRef"/> with only <c>Ref</c>, or a mapping
/// <c>{repo, default_branch}</c> -> <see cref="RawRepoRef"/> carrying the branch override.
/// </summary>
public sealed class RawRepoRefYamlConverter : IYamlTypeConverter
{
    private const string RepoKey = "repo";
    private const string DefaultBranchKey = "default_branch";

    public bool Accepts(Type type) => type == typeof(RawRepoRef);

    public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        if (parser.Current is Scalar scalar)
        {
            parser.MoveNext();
            return new RawRepoRef(scalar.Value);
        }

        return ReadMapping(parser);
    }

    private static RawRepoRef ReadMapping(IParser parser)
    {
        parser.Consume<MappingStart>();
        string? repo = null;
        string? defaultBranch = null;
        while (parser.Current is not MappingEnd)
        {
            var key = parser.Consume<Scalar>().Value;
            var value = parser.Consume<Scalar>().Value;
            if (string.Equals(key, RepoKey, StringComparison.OrdinalIgnoreCase)) repo = value;
            else if (string.Equals(key, DefaultBranchKey, StringComparison.OrdinalIgnoreCase)) defaultBranch = value;
        }
        parser.Consume<MappingEnd>();

        if (string.IsNullOrEmpty(repo))
            throw new YamlException("A repos[] mapping entry must set 'repo'.");
        return new RawRepoRef(repo, defaultBranch);
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        var repoRef = (RawRepoRef)value!;
        emitter.Emit(new Scalar(repoRef.Ref));
    }
}
