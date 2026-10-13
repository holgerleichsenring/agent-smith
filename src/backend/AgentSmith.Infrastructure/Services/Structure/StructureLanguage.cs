namespace AgentSmith.Infrastructure.Services.Structure;

/// <summary>
/// 2026-10-06-8565b: one language as the structure engine reads it — an embedded data file,
/// so a new language is a resource and a keep-list entry, never a branch in code. The query's
/// @type, @function and @test captures map the grammar onto the neutral vocabulary; @name and
/// @params feed the unit key.
/// </summary>
public sealed class StructureLanguage
{
    public string Name { get; set; } = string.Empty;
    public List<string> Extensions { get; set; } = [];
    public string Library { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public List<string> AttributeKinds { get; set; } = [];
    public Dictionary<string, StructureContainer> Containers { get; set; } = [];
    public List<string> Exclude { get; set; } = [];
    public string Query { get; set; } = string.Empty;
}
