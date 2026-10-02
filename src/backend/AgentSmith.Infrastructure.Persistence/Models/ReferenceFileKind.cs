namespace AgentSmith.Infrastructure.Persistence.Models;

/// <summary>2026-10-01-283da: what a stored reference file is part of.</summary>
public static class ReferenceFileKind
{
    /// <summary>A screenshot the design partner sees as an image part.</summary>
    public const string Image = "image";

    /// <summary>One file of an uploaded website, read through its set's address.</summary>
    public const string Site = "site";

    /// <summary>2026-10-02-075dd: what the design partner worked out about one set — at most one per set.</summary>
    public const string Note = "note";

    /// <summary>The column's width: the longest kind with room to grow.</summary>
    public const int MaxLength = 16;
}
