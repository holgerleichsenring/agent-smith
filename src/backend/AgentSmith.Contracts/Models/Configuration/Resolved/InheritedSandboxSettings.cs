namespace AgentSmith.Contracts.Models.Configuration.Resolved;

/// <summary>
/// 2026-09-22-6968: the COUNTERFACTUAL sandbox settings of one project — what it would get
/// if its own sandbox block were empty. Deliberately not
/// <see cref="ResolvedProjectSettings"/>, which answers the other question: what a run of
/// this project gets, which for a project that HAS an override is the override. A form
/// whose placeholder was that would show the operator their own value back, and the one
/// thing the form has to say is what CLEARING the field restores.
/// <para>
/// 2026-09-22-6c46 added the two structured answers that CAN be given. The resource group
/// carries the layer that produced it, because four layers cannot be told apart by a value;
/// the image map is one answer PER KEY, because the merge is per key. The pod's secrets are
/// absent on purpose: the global sandbox block has no secrets field, so there is nothing to
/// inherit and a blank here would read as an inherited empty set.
/// </para>
/// <para>
/// 2026-09-23-2446: <see cref="HoldSeconds"/> is the one field here whose answer is read
/// LIVE, because its effect is live — a reaper resolves it through the configuration loader
/// on every scan, so a value frozen at composition would stop being true the moment an
/// operator edited the process-wide one. Its provenance can be the environment.
/// </para>
/// </summary>
public sealed record InheritedSandboxSettings(
    ResolvedValue<string> ToolchainImage,
    ResolvedValue<int> StepTimeoutSeconds,
    ResolvedValue<int> RunCommandTimeoutSeconds,
    ResolvedValue<string> AgentRegistry,
    ResolvedValue<string> AgentVersion,
    ResolvedValue<int> HoldSeconds,
    InheritedSandboxResources Resources,
    IReadOnlyDictionary<string, ResolvedValue<string>> Images);
