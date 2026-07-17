namespace AgentSmith.Contracts.Models.ConfigStudio;

/// <summary>
/// Who is making a config change. Every mutation is attributed; the audit row
/// carries this so the Changes view can answer "who changed what, when".
/// </summary>
public sealed record ChangeAttribution(string Actor)
{
    public static ChangeAttribution System { get; } = new("system");
}
