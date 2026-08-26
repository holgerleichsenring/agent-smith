using AgentSmith.Application.Services.Tools;
using AgentSmith.Application.Services.Validation;

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
}
