namespace AgentSmith.Contracts.Providers;

/// <summary>
/// 2026-11-23-933dd: a file read out of a run's artifact — its text, or, when it is larger than the read allowed,
/// <see cref="IsTooLarge"/> with its size and no text. An absent artifact or file is no answer at all (null).
/// </summary>
public sealed record PipelineArtifactText(string? Text, long SizeBytes)
{
    public bool IsTooLarge => Text is null;

    public static PipelineArtifactText Of(string text, long sizeBytes) => new(text, sizeBytes);

    public static PipelineArtifactText TooLarge(long sizeBytes) => new(null, sizeBytes);
}
