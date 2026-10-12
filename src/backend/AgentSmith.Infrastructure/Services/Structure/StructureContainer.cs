namespace AgentSmith.Infrastructure.Services.Structure;

/// <summary>
/// 2026-10-06-8565b: a node that names the units inside it without being a unit itself — a
/// Rust impl ('Display for Foo') or module. Absent fields are skipped, so 'impl Foo' reads 'Foo'.
/// </summary>
public sealed class StructureContainer
{
    public List<string> Fields { get; set; } = [];
    public string Separator { get; set; } = string.Empty;
}
