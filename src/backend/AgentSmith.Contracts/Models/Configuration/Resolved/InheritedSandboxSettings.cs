namespace AgentSmith.Contracts.Models.Configuration.Resolved;

/// <summary>
/// 2026-09-22-6968: the COUNTERFACTUAL sandbox settings of one project — what it would get
/// if its own sandbox block were empty. Deliberately not
/// <see cref="ResolvedProjectSettings"/>, which answers the other question: what a run of
/// this project gets, which for a project that HAS an override is the override. A form
/// whose placeholder was that would show the operator their own value back, and the one
/// thing the form has to say is what CLEARING the field restores.
/// <para>
/// Only the five SCALAR overrides are carried. The structured three (resources, the
/// per-language image map, the pod's secrets) each inherit by a different rule and are
/// 2026-09-22-6c46.
/// </para>
/// </summary>
public sealed record InheritedSandboxSettings(
    ResolvedValue<string> ToolchainImage,
    ResolvedValue<int> StepTimeoutSeconds,
    ResolvedValue<int> RunCommandTimeoutSeconds,
    ResolvedValue<string> AgentRegistry,
    ResolvedValue<string> AgentVersion);
