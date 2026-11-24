namespace AgentSmith.Application.Services.Pocs;

/// <summary>2026-11-23-933db: one logical Dockerfile instruction — its line continuations joined — with the line it
/// starts on, its keyword in upper case and its arguments. A parser directive is one too, its key as the keyword.</summary>
public sealed record DockerfileInstruction(int Line, string Keyword, string Arguments)
{
    /// <summary>The arguments split on whitespace.</summary>
    public IReadOnlyList<string> Tokens => Arguments.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>The leading '--name=value' flags of the arguments.</summary>
    public IReadOnlyList<string> Flags => [.. Tokens.TakeWhile(t => t.StartsWith("--", StringComparison.Ordinal))];

    /// <summary>The arguments after the leading flags.</summary>
    public IReadOnlyList<string> Operands => [.. Tokens.SkipWhile(t => t.StartsWith("--", StringComparison.Ordinal))];
}
