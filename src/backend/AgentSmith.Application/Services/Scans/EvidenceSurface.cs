namespace AgentSmith.Application.Services.Scans;

/// <summary>p0429a: what the refuter is looking at when it is asked to refute a finding.</summary>
public enum EvidenceSurface
{
    /// <summary>The lines around the cited one, from a file the scan can read.</summary>
    Source,

    /// <summary>The endpoint the specification declares and the exchange it produced.</summary>
    LiveTarget,
}
