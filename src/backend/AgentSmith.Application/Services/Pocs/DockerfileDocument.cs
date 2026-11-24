namespace AgentSmith.Application.Services.Pocs;

/// <summary>2026-11-23-933db: a Dockerfile as <see cref="DockerfileStages"/> reads it — the parser directives at its
/// top and its instructions in order.</summary>
public sealed record DockerfileDocument(
    IReadOnlyList<DockerfileInstruction> Directives, IReadOnlyList<DockerfileInstruction> Instructions)
{
    /// <summary>The instructions of the final stage — from the last FROM on — or empty when there is no FROM.</summary>
    public IReadOnlyList<DockerfileInstruction> FinalStage
    {
        get
        {
            var last = Instructions.Select((i, n) => (i, n)).LastOrDefault(x => x.i.Keyword == "FROM");
            return last.i is null ? [] : [.. Instructions.Skip(last.n)];
        }
    }

    /// <summary>The final FROM's image, or null when there is no FROM.</summary>
    public string? BaseImage => FinalStage.FirstOrDefault()?.Operands.FirstOrDefault();
}
