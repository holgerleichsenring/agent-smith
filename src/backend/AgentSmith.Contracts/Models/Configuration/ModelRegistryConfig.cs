using System.Text.Json.Serialization;

namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Configuration for task-specific model routing: each task type may name its own model.
/// <para>
/// No role carries a built-in model. A built-in id belongs to one provider, and a partial
/// <c>models:</c> block kept it for every role it did not name — an OpenAI agent answered its
/// scout calls with a Claude model id. An unset role inherits instead: primary from the
/// agent's own <c>model</c>, the others from primary, code-map generation from scout and then
/// primary. <see cref="ModelRoleChain"/> is that chain, stated once.
/// </para>
/// A null role is left out of a stored document, so what is stored is only what was chosen.
/// </summary>
public sealed class ModelRegistryConfig
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ModelAssignment? Scout { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ModelAssignment? Primary { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ModelAssignment? Planning { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ModelAssignment? Reasoning { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ModelAssignment? Summarization { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ModelAssignment? ContextGeneration { get; set; }

    /// <summary>Unset, the code-map sweep follows scout — p0374 measured it onto the cheaper
    /// model — and primary when scout is unset too.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ModelAssignment? CodeMapGeneration { get; set; }
}
