namespace AgentSmith.Contracts.Models.Skills;

/// <summary>
/// A keyed activation criterion. Key is referenced by the triage rationale token grammar
/// '&lt;role&gt;=&lt;skill&gt;:&lt;key&gt;;'. Desc is the natural-language description the LLM
/// uses to evaluate whether the criterion applies to the current ticket.
/// </summary>
public sealed record ActivationKey(string Key, string Desc);
