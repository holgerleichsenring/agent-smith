namespace AgentSmith.Contracts.Commands;

public static partial class PipelinePresets
{
    /// <summary>Preset name of the spec-dialog pipeline (p0315b) — the key the
    /// AgenticMaster step and the master context builder branch on.</summary>
    public const string SpecDialogName = "spec-dialog";

    // p0315b: one design-conversation TURN. The server's SpecDialogTurnRunner
    // seeds the transcript, the lazy read-only source sandboxes, and the reply
    // slot via PipelineRequest.Context; the preset assembles the cheap tier-1
    // grounding (cached code map), runs design-partner-master in one agentic
    // loop (tier-2 source reads materialise a sandbox lazily inside the loop),
    // and hands the reply back. Deliberately NOT in CodeChangingPresets /
    // GreenTestPresets: a conversation ships no code and emits no verdict.
    // No PipelineNameInitializer: spec-dialog runs no skill activation, and the
    // pipeline_name concept enum lives in the (operator-pinned) skills catalog —
    // requiring a new enum value would break spec-dialog on every existing pin.
    public static readonly IReadOnlyList<string> SpecDialog =
    [
        CommandNames.LoadCatalog,
        CommandNames.LoadCachedCodeMap,
        CommandNames.AgenticMaster,         // loads design-partner-master
        CommandNames.CollectSpecDialogReply,
    ];
}
