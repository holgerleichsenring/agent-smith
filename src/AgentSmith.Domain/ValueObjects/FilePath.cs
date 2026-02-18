namespace AgentSmith.Domain.ValueObjects;

/// <summary>
/// Relative file path within a repository. Must not be absolute or contain parent traversal.
/// </summary>
public sealed record FilePath
{
    public string Value { get; }

    public FilePath(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ValidatePath(value);
        Value = value;
    }

    public override string ToString() => Value;

    private static void ValidatePath(string value)
    {
        if (Path.IsPathRooted(value))
            throw new ArgumentException("File path must be relative.", nameof(value));

        if (value.Contains("..", StringComparison.Ordinal))
            throw new ArgumentException("File path must not contain parent traversal.", nameof(value));
    }
}
