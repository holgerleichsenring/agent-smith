namespace AgentSmith.Application.Services.Specs;

/// <summary>2026-10-02-3f06b: why a fact's evidence does not stand, and the path it concerns when one does.</summary>
public sealed record EvidenceProblem(string Reason, string? Path = null)
{
    public override string ToString() => Path is null ? Reason : $"{Reason}: {Path}";
}
