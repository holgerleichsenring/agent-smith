using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Pocs;

/// <summary>2026-11-23-933dg: one variable of a write — its name, its kind (<see cref="PocVariableKinds"/>), and the value:
/// a secret in a redacting holder, a plain one as text (it is committed in plain text). Says only name and kind.</summary>
public sealed record PocVariableEntry(string Name, string Kind, SecretInput? Secret = null, string? Plain = null)
{
    /// <summary>The value's length in UTF-8 bytes; 0 for a delete.</summary>
    public int ValueBytes => System.Text.Encoding.UTF8.GetByteCount(Secret?.Reveal() ?? Plain ?? string.Empty);

    public override string ToString() => $"{Name} ({Kind})";
}
