namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-11-23-933dg: how many variables one PoC holds and how large each value may be, from <c>pocs.variables:</c>; an
/// absent block reads as these defaults. Both keep a PoC's Secret and ConfigMap under Kubernetes' 1 MiB per object
/// once sealed and base64-encoded (64 x 4 KB).
/// </summary>
public sealed class PocVariablesConfig
{
    public const string MaxNamesKey = "pocs.variables.max_names";
    public const string MaxValueKbKey = "pocs.variables.max_value_kb";
    public const string Reason = "a Kubernetes Secret or ConfigMap holds at most 1 MiB";
    private const int MebibyteKb = 1024; // not a limit: the 1 MiB a Kubernetes object holds, in KB

    /// <summary>The most variables, secret and plain together, one PoC holds.</summary>
    public int MaxNames { get; set; } = 64;

    /// <summary>The largest value in KB (UTF-8).</summary>
    public int MaxValueKb { get; set; } = 4;

    /// <summary>What is wrong with these values, one sentence each naming the key, the value and the reason.</summary>
    public IEnumerable<string> Problems()
    {
        if (MaxNames < 1)
            yield return $"{MaxNamesKey} is {MaxNames}; it must be 1 or more, because 0 would refuse every variable";
        if (MaxValueKb < 1)
            yield return $"{MaxValueKbKey} is {MaxValueKb}; it must be 1 or more, because 0 would refuse every value";
        else if ((long)MaxNames * MaxValueKb > MebibyteKb / 2)
            yield return $"{MaxNamesKey} x {MaxValueKbKey} is {(long)MaxNames * MaxValueKb} KB; it must stay at or below "
                + $"{MebibyteKb / 2} KB, because {Reason} and sealing and base64 roughly double a value";
    }
}
