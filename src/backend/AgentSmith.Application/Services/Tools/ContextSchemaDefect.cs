namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-08-26-167c: one thing the context schema refused, before it is worded.
/// </summary>
/// <param name="Location">JSON Pointer into the document the model sent.</param>
/// <param name="Keyword">The schema keyword that rejected it (enum, pattern, required, …).</param>
/// <param name="Message">The validator's own wording.</param>
/// <param name="SchemaPath">
/// JSON Pointer into the schema document, addressing the node that carries the
/// keyword — how the broken rule and its suggestions are quoted back. Two defects
/// sharing it broke the SAME rule, which is what lets the rule be quoted once.
/// </param>
public sealed record ContextSchemaDefect(
    string Location, string Keyword, string Message, string SchemaPath);
