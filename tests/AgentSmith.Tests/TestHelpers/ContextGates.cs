using AgentSmith.Application.Services.Tools;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Infrastructure.Services;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// 2026-08-25-c9c7: the context-document gate assembled the way DI assembles it, so a
/// test that only cares about the write path does not have to re-state the graph — and
/// so every such test judges documents against the SHIPPED schema, not a stand-in.
/// </summary>
internal static class ContextGates
{
    private static readonly ContextSchemaProvider Schema = new();

    private static readonly ContextDefectReport Report = new(new ContextSchemaPointer(Schema));

    public static ContextDocumentGate Build() =>
        new(new ContextStackImageRule(), Rule(), Report, new ContextSingleValueNormaliser(Schema));

    public static ContextSchemaRule Rule() => new(Schema, Report);

    public static ContextDefectReport DefectReport() => Report;

    public static ContextSingleValueNormaliser Normaliser() => new(Schema);

    /// <summary>
    /// 2026-08-26-364f: the writer assembled the way DI assembles it, so a test proves the
    /// REAL read-modify-write rather than a stand-in that could not delete anything.
    /// </summary>
    public static SandboxContextYamlWriter Writer() =>
        new(new ContextYamlSectionUpsert(new ContextYamlBuilders()));

    /// <summary>
    /// The real emitter. A mock returns null YAML, which the write path used to put on
    /// disk unnoticed — a fixture that cannot produce a file cannot prove one survived.
    /// </summary>
    public static AgentSmith.Contracts.Services.IContextYamlSerializer Serializer() =>
        new ContextYamlSerializer(new ContextYamlBuilders());
}
