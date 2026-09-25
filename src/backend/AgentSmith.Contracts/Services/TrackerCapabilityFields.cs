using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-09-18-b4f0: which form fields a tracker of a given type has. It is the single source
/// for both the rendered form and the upsert validation in
/// <see cref="ConfigStudioCapabilities"/>, which reads this table for the descriptor it
/// builds and for the requiredness it enforces, so the two cannot drift apart.
/// </summary>
public static class TrackerCapabilityFields
{
    /// <summary>
    /// Grounded in TicketProviderFactory: ADO builds its org URL from organization +
    /// project; GitHub/Jira connect by URL; GitLab addresses the project path. Every
    /// tracker authenticates via a secret NAME.
    /// </summary>
    public static IReadOnlyList<CapabilityField> For(TrackerType type) => type switch
    {
        TrackerType.AzureDevOps =>
        [
            new CapabilityField("organization", "Organization", Required: true),
            new CapabilityField("project", "Project", Required: true),
            new CapabilityField("url", "URL", Required: false),
            new CapabilityField("authSecret", "Auth secret", Required: true),
            .. WorkflowFields,
            WorkItemKinds,
        ],
        TrackerType.GitHub =>
        [
            new CapabilityField("url", "Repository URL", Required: true),
            new CapabilityField("authSecret", "Auth secret", Required: true),
            .. WorkflowFields,
        ],
        TrackerType.GitLab =>
        [
            new CapabilityField("project", "Project path", Required: true),
            new CapabilityField("url", "Base URL", Required: false),
            new CapabilityField("authSecret", "Auth secret", Required: true),
            .. WorkflowFields,
        ],
        TrackerType.Jira =>
        [
            new CapabilityField("url", "Base URL", Required: true),
            new CapabilityField("project", "Project key", Required: false),
            new CapabilityField("authSecret", "Auth secret", Required: true),
            .. WorkflowFields,
            WorkItemKinds,
        ],
        _ => throw new ConfigurationException(
            $"Tracker type '{type}' has no capabilities descriptor — add its field set."),
    };

    // The tracker-owned workflow (p0281b) — identical for every tracker type. TrackerEntity
    // carries these and RawConfigPatch applies them; without a descriptor entry the studio
    // form never rendered them, so a failed run could not be given a native failed_status
    // from the UI and the ticket stayed claimable (observed live on 2026-07-27).
    // p0392: needs_clarification_status, undeclared, refused a boot on 2026-07-31 and could not
    // be set from the UI. CapabilityCoverageTests keeps this list level with the raw model.
    private static readonly IReadOnlyList<CapabilityField> WorkflowFields =
    [
        new CapabilityField("triggerStatuses", "Trigger statuses", Required: false, CapabilityFieldKind.List),
        new CapabilityField("openStates", "Open states", Required: false, CapabilityFieldKind.List),
        new CapabilityField("doneStatus", "Done status", Required: false),
        new CapabilityField("failedStatus", "Failed status", Required: false),
        new CapabilityField("needsClarificationStatus", "Needs-clarification status", Required: false),
        new CapabilityField("notImplementableStatus", "Not-implementable status", Required: false),
        new CapabilityField("closeTransitionName", "Close transition name", Required: false),
        new CapabilityField("extraFields", "Extra ticket fields", Required: false, CapabilityFieldKind.List),
        new CapabilityField("zeroMatchComment", "Comment when nothing matched", Required: false, CapabilityFieldKind.Bool),
        new CapabilityField(
            "pipelineFromLabel", "Pipeline by label", Required: false, CapabilityFieldKind.Map,
            Choices: Commands.PipelinePresets.Routable),
        // OPTIONAL: Required is a blocking draft finding, and would make every existing tracker unsaveable.
        new CapabilityField(
            "defaultPipeline", "Default pipeline", Required: false, Choices: Commands.PipelinePresets.Routable),
        new CapabilityField("lifecycleStatusNames", "Lifecycle status names", Required: false, CapabilityFieldKind.Map),
        new CapabilityField("parentLinkType", "Parent link type (Jira)", Required: false),
        // 2026-09-25-3c7ac: what this board calls the labels agent-smith writes onto it.
        new CapabilityField("labelNames", "Label names", Required: false, CapabilityFieldKind.Map),
    ];

    // 2026-09-18-b4f0: declared for the two tracker types whose create sends a kind, and for
    // no others — GitHub applies any status that is not open or closed as a label and GitLab
    // accepts two state events, so neither has a kind this choice would change. The coverage
    // test unions the field keys across types, so two arms is enough to count as declared.
    private static readonly CapabilityField WorkItemKinds =
        new("workItemKinds", "Work item kind by filing role", Required: false, CapabilityFieldKind.Map);
}
