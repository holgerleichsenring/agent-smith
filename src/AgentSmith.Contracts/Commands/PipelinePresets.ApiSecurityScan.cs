namespace AgentSmith.Contracts.Commands;

public static partial class PipelinePresets
{
    // Live-API security scan: TryCheckoutSource is fail-soft (p0102a) so the scan
    // runs even when no source is available; the OpenAPI spec, Nuclei, Spectral, and
    // ZAP scanners drive the finding stream; CompressApiScanFindings (p67) bundles
    // category slices for the api-security-* skills, then the standard triage +
    // discussion + delivery chain runs over the compressed findings.
    public static readonly IReadOnlyList<string> ApiSecurityScan =
    [
        CommandNames.PipelineNameInitializer,
        CommandNames.TryCheckoutSource,     // p0102a: fail-soft source resolution (CLI flag, local config, or remote clone)
        CommandNames.BootstrapCheck, CommandNames.BootstrapGate, // p0130a conditional gate (skips when source_available=false)
        CommandNames.LoadContext,           // p0104: target's .agentsmith/context.yaml — soft-fail if absent
        CommandNames.LoadCodingPrinciples,  // p0104: target's .agentsmith/coding-principles.md — soft-fail if absent
        CommandNames.LoadSwagger,
        CommandNames.SessionSetup,          // p79: authenticate personas before scan
        CommandNames.SpawnNuclei,
        CommandNames.SpawnSpectral,
        CommandNames.SpawnZap,              // p60: DAST via OWASP ZAP (skips if dast not enabled)
        CommandNames.CompressApiScanFindings, // p67: category slices for skill-specific findings
        CommandNames.LoadSkills,
        CommandNames.Triage,                // p0111c: unified triage replaces ApiSecurityTriage
        CommandNames.RunReviewPhase,
        CommandNames.RunFinalPhase,
        CommandNames.ConvergenceCheck,
        CommandNames.CompileFindings,
        CommandNames.DeliverFindings,
    ];
}
