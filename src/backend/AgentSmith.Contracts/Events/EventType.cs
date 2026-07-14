namespace AgentSmith.Contracts.Events;

public enum EventType
{
    RunStarted = 0,
    RunFinished = 1,
    SandboxCreated = 2,
    SandboxDisposed = 3,
    StepStarted = 4,
    StepFinished = 5,
    DecisionLogged = 10,
    GateChecked = 11,
    TriageRoute = 12,
    LlmCallStarted = 13,
    LlmCallFinished = 14,
    SandboxCommand = 20,
    SandboxOutput = 21,
    SandboxResult = 22,
    ToolCall = 23,
    ToolResult = 24,
    L1StepDetail = 25,
    // p0184: ticket detail event (L1Run tier). Carries title, description,
    // state, labels, attachment count — fills the empty Fetch-ticket step
    // body and feeds RunSnapshot.TicketTitle for the runs-page card.
    TicketFetched = 26,
    // p0205: per-run catalog binding (version/source/url/counts/from-cache),
    // emitted by the visible LoadCatalog step. Distinct from the system-scoped
    // SkillCatalogLoadedEvent on the System page.
    CatalogLoaded = 27,
    // p0223: per-repo outcome of the commit/PR step — opened (with URL),
    // no-changes (benign, no PR needed), or a genuine failure (with reason).
    // Lets the run detail render a meaningful outcome instead of a raw red
    // "git commit · exit 1" for repos that simply had nothing to commit.
    PullRequestOutcome = 28,
    CatalogIssue = 30,
    // p0316: the master refused a ticket-embedded instruction (out-of-scope /
    // destructive / prompt-injection). One event per ignored instruction (quote +
    // reason) for the dashboard + audit trail; also persisted in result.md.
    TicketInstructionIgnored = 31,
    // p0177: L2SubAgent events (60-range — separated from L2Orchestrator
    // so sub-agent specific records stay visually grouped on the wire).
    SubAgentSpawned = 60,
    SubAgentObservation = 61,
    SubAgentFinding = 62,
    SubAgentFileWritten = 63,
    SubAgentToolCall = 64,
    SubAgentCompleted = 65,
    // p0200: operator-initiated or watchdog-initiated cancel signal.
    // Landed on the run stream so late-subscribing dashboards see the
    // cancellation request was made; RunFinished follows once the
    // executor returns.
    RunCancelRequested = 70,
    // p0201: sandbox container died unexpectedly (docker daemon lost it,
    // OOM kill, manual rm, network partition). SandboxLivenessWatcher
    // publishes after the docker-inspect probe confirms the container is
    // truly gone, just before signalling the per-run CTS. Trail shows
    // ContainerState so the operator distinguishes "Exited(137)" from
    // "container missing".
    SandboxVanished = 71,
    // p0327: a run parked on a DialogQuestion past the hot-wait threshold.
    // Carries the serialized checkpoint (pending question + step cursor +
    // pipeline context) so the server-side projector can persist it — the
    // event stream is the only DB channel a spawned orchestrator has.
    RunCheckpointed = 72,
    // p0328: the ratification outcome of the expectation negotiation
    // (verbatim/edited/rejected/unratified + edit distance). Carries the
    // draft + ratified ExpectationDraft JSON so the server-side projector
    // can persist the RunExpectation row — the event stream is the only
    // DB channel a spawned orchestrator has.
    ExpectationRatified = 73,
}
