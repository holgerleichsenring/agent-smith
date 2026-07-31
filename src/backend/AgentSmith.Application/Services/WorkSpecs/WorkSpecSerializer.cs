using AgentSmith.Application.Services.WorkSpecs.Yaml;
using AgentSmith.Contracts.WorkSpecs;
using YamlDotNet.Core;

namespace AgentSmith.Application.Services.WorkSpecs;

/// <summary>
/// p0390: spec.yaml emit + consume. The emitted document carries the
/// yaml-language-server schema header, so a reviewer editing the spec in the PR
/// gets the same validation the writer applies — the artifact reviews itself.
/// </summary>
public sealed class WorkSpecSerializer : IWorkSpecSerializer
{
    /// <summary>Schema header prepended to every emitted spec.yaml. Relative to
    /// <c>.agentsmith/specs/tickets/&lt;key&gt;/</c>, where the schema copy the writer
    /// ships sits at <c>.agentsmith/specs/work-spec.schema.json</c>.</summary>
    public const string SchemaHeader =
        "# yaml-language-server: $schema=../../work-spec.schema.json";

    public string Serialize(WorkSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return SchemaHeader + "\n" + WorkSpecYamlBuilders.Serializer.Serialize(ToDocument(spec));
    }

    public WorkSpec? Parse(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml)) return null;
        WorkSpecDocument? doc;
        try { doc = WorkSpecYamlBuilders.Deserializer.Deserialize<WorkSpecDocument>(yaml); }
        catch (YamlException) { return null; }
        catch (InvalidCastException) { return null; }
        return doc?.Goal is null ? null : WorkSpecDocumentMapper.ToSpec(doc);
    }

    private static WorkSpecDocument ToDocument(WorkSpec spec) => new()
    {
        Key = spec.Key,
        Goal = spec.Goal,
        Requirements = [.. spec.Requirements],
        Constraints = [.. spec.Constraints.Select(
            c => new WorkSpecConstraintEntry { Rule = c.Rule, SampleAnchor = c.SampleAnchor })],
        Done = [.. spec.Done],
        DoneIsReadOnly = spec.DoneIsReadOnly,
        Assumptions = [.. spec.Assumptions],
        Revisions = [.. spec.Revisions.Select(r => new WorkSpecRevisionEntry
        {
            Number = r.Number,
            Cause = r.Cause,
            At = r.At.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
        })],
        HandbackCase = spec.Handback?.Case.ToString(),
        HandbackReason = spec.Handback?.Reason,
    };
}
