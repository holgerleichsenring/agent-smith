"use client";

import {
  EventType,
  type CatalogIssueEvent,
  type DecisionLoggedEvent,
  type GateCheckedEvent,
  type LlmCallFinishedEvent,
  type LlmCallStartedEvent,
  type PipelineStepsPlannedEvent,
  type RunEvent,
  type StepFinishedEvent,
  type StepStartedEvent,
  type ToolCallEvent,
  type ToolResultEvent,
  type TriageRouteEvent,
} from "@/types/hub-events";
import { useDateLocale, useT } from "@/hooks/useT";
import type { T } from "@/lib/i18n/translate";

interface Props {
  event: RunEvent;
  expanded: boolean;
  onToggle: () => void;
}

type Severity = "info" | "ok" | "warn" | "error";

interface RowView {
  icon: string;
  label: string;
  detail: string;
  reason: string | null;
  severity: Severity;
}

export function ActivityRow({ event, expanded, onToggle }: Props) {
  const t = useT();
  const locale = useDateLocale();
  const view = projectEvent(t, locale, event);
  const containerClass = severityClass(view.severity);
  return (
    <div
      className={`flex flex-col gap-1 rounded border px-3 py-2 text-sm ${containerClass}`}
      data-testid={`activity-row-${event.type}`}
      data-severity={view.severity}
    >
      <button
        type="button"
        onClick={onToggle}
        className="flex items-baseline gap-3 text-left focus:outline-none"
        aria-expanded={expanded}
      >
        <span className="w-20 shrink-0 font-mono text-xs text-stone-500">
          {formatTime(event.timestamp)}
        </span>
        <span className="w-5 shrink-0 text-center" aria-hidden>
          {view.icon}
        </span>
        <span className="w-28 shrink-0 text-xs font-medium uppercase tracking-wide">
          {view.label}
        </span>
        <span className="flex-1 truncate text-stone-800">{view.detail}</span>
      </button>
      {view.reason !== null ? (
        <p className="ml-[136px] text-xs italic text-stone-700">{view.reason}</p>
      ) : null}
      {expanded ? <PayloadJson value={event} /> : null}
    </div>
  );
}

// The planned tail is transported as JSON so the event stays one flat row; the
// count is all this row needs, and a malformed payload must not throw here.
function countPlannedSteps(stepsJson: string): number | null {
  try {
    const parsed: unknown = JSON.parse(stepsJson);
    return Array.isArray(parsed) ? parsed.length : null;
  } catch {
    return null;
  }
}

function projectEvent(t: T, locale: string, event: RunEvent): RowView {
  switch (event.type) {
    case EventType.PipelineStepsPlanned: {
      const e = event as PipelineStepsPlannedEvent;
      const count = countPlannedSteps(e.stepsJson);
      return {
        icon: "▤",
        label: t("runs.activity.label.pipeline"),
        detail: count === null
          ? t("runs.activity.plannedUnknown", { first: e.firstStepIndex })
          : t("runs.activity.planned", { count, first: e.firstStepIndex }),
        reason: null,
        severity: "info",
      };
    }
    case EventType.RunStarted: {
      return {
        icon: "▶",
        label: t("runs.activity.label.run"),
        detail: t("runs.activity.started"),
        reason: null,
        severity: "info",
      };
    }
    case EventType.RunFinished: {
      const e = event as Extract<RunEvent, { type: EventType.RunFinished }>;
      return {
        icon: e.status === "success" || e.status === "shortfall" ? "✓" : "✕",
        label: t("runs.activity.label.run"),
        detail: `${e.status} — ${e.summary}`,
        reason: null,
        severity: e.status === "success" || e.status === "shortfall" ? "ok" : "error",
      };
    }
    case EventType.StepStarted: {
      const e = event as StepStartedEvent;
      // p0203: prefer the operator-facing displayName when present; fall
      // back to the raw stepName for pre-p0203 producers.
      const label = e.displayName ?? e.stepName;
      return {
        icon: "•",
        label: t("runs.activity.label.stepStart"),
        detail: `${e.stepIndex}/${e.totalSteps} ${label}`,
        reason: null,
        severity: "info",
      };
    }
    case EventType.StepFinished: {
      const e = event as StepFinishedEvent;
      const ok = e.status === "success";
      return {
        icon: ok ? "✓" : "✕",
        label: ok ? t("runs.activity.label.stepDone") : t("runs.activity.label.stepFailed"),
        detail: `${e.stepIndex} — ${e.status} (${e.durationMs}ms)`,
        reason: !ok ? e.reason : null,
        severity: ok ? "ok" : "error",
      };
    }
    case EventType.SandboxCreated: {
      const e = event as Extract<RunEvent, { type: EventType.SandboxCreated }>;
      return {
        icon: "□",
        label: t("runs.activity.label.sandbox"),
        detail: t("runs.activity.sandboxCreated", { repo: e.repo, image: e.image }),
        reason: null,
        severity: "info",
      };
    }
    case EventType.SandboxDisposed: {
      const e = event as Extract<RunEvent, { type: EventType.SandboxDisposed }>;
      return {
        icon: "□",
        label: t("runs.activity.label.sandbox"),
        detail: `${t("runs.activity.sandboxDisposed", { repo: e.repo })}${e.exitCode !== null ? t("runs.activity.exit", { code: e.exitCode }) : ""}`,
        reason: null,
        severity: "info",
      };
    }
    case EventType.SandboxCommand: {
      const e = event as Extract<RunEvent, { type: EventType.SandboxCommand }>;
      return {
        icon: "⟫",
        label: t("runs.activity.label.sandboxCmd"),
        detail: e.summary
          ? `${e.repo}: ${e.command} ${e.summary}`
          : `${e.repo}: ${e.command}`,
        reason: null,
        severity: "info",
      };
    }
    case EventType.SandboxOutput: {
      const e = event as Extract<RunEvent, { type: EventType.SandboxOutput }>;
      return {
        icon: "·",
        label: e.stream,
        detail: `${e.repo}: ${e.line}`,
        reason: null,
        severity: e.stream === "stderr" ? "warn" : "info",
      };
    }
    case EventType.SandboxResult: {
      const e = event as Extract<RunEvent, { type: EventType.SandboxResult }>;
      const ok = e.exitCode === 0;
      return {
        icon: ok ? "✓" : "✕",
        label: t("runs.activity.label.sandboxOut"),
        detail: t("runs.activity.sandboxResult", { repo: e.repo, command: e.command, code: e.exitCode, ms: e.durationMs }),
        reason: null,
        severity: ok ? "info" : "warn",
      };
    }
    case EventType.DecisionLogged: {
      const e = event as DecisionLoggedEvent;
      return {
        icon: "◆",
        label: t("runs.activity.label.decision"),
        detail: `${e.category}: ${e.chose}${e.over ? t("runs.activity.over", { over: e.over }) : ""}`,
        reason: e.reason,
        severity: "info",
      };
    }
    case EventType.TriageRoute: {
      const e = event as TriageRouteEvent;
      return {
        icon: "◇",
        label: t("runs.activity.label.triage"),
        detail: `${e.skill} → ${e.role} (${e.confidence})`,
        reason: null,
        severity: "info",
      };
    }
    case EventType.GateChecked: {
      const e = event as GateCheckedEvent;
      return {
        icon: e.passed ? "✓" : "✕",
        label: t("runs.activity.label.gate"),
        detail: `${e.gate} — ${e.passed ? t("runs.activity.passed") : t("runs.status.failed")}`,
        reason: !e.passed ? e.reason : null,
        severity: e.passed ? "info" : "error",
      };
    }
    case EventType.LlmCallStarted: {
      const e = event as LlmCallStartedEvent;
      return {
        icon: "↗",
        label: t("runs.activity.label.llmStart"),
        detail: `${e.model} · ${e.role}`,
        reason: null,
        severity: "info",
      };
    }
    case EventType.LlmCallFinished: {
      const e = event as LlmCallFinishedEvent;
      return {
        icon: "↘",
        label: t("runs.activity.label.llmDone"),
        detail: t("runs.activity.llmDetail", { tokensIn: e.tokensIn, tokensOut: e.tokensOut, cost: e.costUsd.toFixed(4), ms: e.durationMs }),
        reason: null,
        severity: "info",
      };
    }
    case EventType.ToolCall: {
      const e = event as ToolCallEvent;
      return {
        icon: "→",
        label: t("runs.activity.label.toolCall"),
        detail: e.summary
          ? `${e.tool} ${e.summary}`
          : `${e.tool} (${e.argsLength}B)`,
        reason: null,
        severity: "info",
      };
    }
    case EventType.ToolResult: {
      const e = event as ToolResultEvent;
      return {
        icon: e.ok ? "←" : "✕",
        label: t("runs.activity.label.toolResult"),
        detail: `${e.tool} — ${e.ok ? t("runs.activity.toolOk", { bytes: e.resultLength }) : t("runs.status.failed")}`,
        reason: !e.ok ? e.errorMessage : null,
        severity: e.ok ? "info" : "warn",
      };
    }
    case EventType.CatalogLoaded: {
      const e = event as Extract<RunEvent, { type: EventType.CatalogLoaded }>;
      return {
        icon: "▦",
        label: t("runs.activity.label.catalog"),
        detail: t("runs.activity.catalogLoaded", { version: e.version, concepts: e.conceptCount, skills: e.skillsLoaded, masters: e.mastersCount }),
        reason: e.fromCache ? t("runs.activity.warmCache") : t("runs.activity.freshPull"),
        severity: "info",
      };
    }
    case EventType.CatalogIssue: {
      const e = event as CatalogIssueEvent;
      return {
        icon: e.severity === "error" ? "✕" : "⚠",
        label: t("runs.activity.label.catalog"),
        detail: t("runs.activity.catalogIssue", { category: e.category, source: e.source }),
        reason: e.message,
        severity: e.severity === "error" ? "error" : "warn",
      };
    }
    case EventType.PullRequestOutcome: {
      // p0223: a no-changes repo is a normal outcome (neutral), not a failure.
      const e = event as Extract<RunEvent, { type: EventType.PullRequestOutcome }>;
      const detail = e.status === "no_changes"
        ? t("runs.activity.prNoChanges")
        : e.status === "opened"
          ? t("runs.activity.prOpened", { url: String(e.url) })
          : e.status === "awaiting_ci" // 3435b: merges once CI is green
            ? t("runs.activity.prAwaitingCi", { url: String(e.url) })
            : t("runs.status.failed");
      return {
        icon: e.status === "opened" || e.status === "awaiting_ci" ? "✓" : e.status === "failed" ? "✕" : "○",
        label: t("runs.activity.label.pr"),
        detail: `${e.repo}: ${detail}`,
        reason: e.status === "failed" ? e.reason : null,
        severity: e.status === "failed" ? "error" : e.status === "opened" || e.status === "awaiting_ci" ? "ok" : "info",
      };
    }
    case EventType.TicketInstructionIgnored: {
      // p0316: the master refused a ticket-embedded instruction (out-of-scope /
      // destructive / injection). Surfaced as a warning with the verbatim quote.
      const e = event as Extract<RunEvent, { type: EventType.TicketInstructionIgnored }>;
      return {
        icon: "⚠",
        label: t("runs.activity.label.ignored"),
        detail: `"${e.quote}"`,
        reason: e.reason,
        severity: "warn",
      };
    }
    case EventType.L1StepDetail: {
      const e = event as Extract<RunEvent, { type: EventType.L1StepDetail }>;
      return {
        icon: "·",
        label: t("runs.activity.label.stepDetail"),
        detail: `${e.stepIndex} ${e.origin}: ${e.detail}`,
        reason: null,
        severity: "info",
      };
    }
    case EventType.TicketFetched: {
      const e = event as Extract<RunEvent, { type: EventType.TicketFetched }>;
      return {
        icon: "✉",
        label: t("runs.activity.label.ticket"),
        detail: `#${e.ticketId} — ${e.title}`,
        reason: null,
        severity: "info",
      };
    }
    case EventType.SubAgentSpawned: {
      const e = event as Extract<RunEvent, { type: EventType.SubAgentSpawned }>;
      return {
        icon: "⤴",
        label: t("runs.activity.label.subSpawn"),
        detail: `${e.name} — ${e.activity}`,
        reason: null,
        severity: "info",
      };
    }
    case EventType.SubAgentObservation: {
      const e = event as Extract<RunEvent, { type: EventType.SubAgentObservation }>;
      return {
        icon: "·",
        label: t("runs.activity.label.subObs"),
        detail: `${e.subAgentId}: ${e.text}`,
        reason: null,
        severity: "info",
      };
    }
    case EventType.SubAgentFinding: {
      const e = event as Extract<RunEvent, { type: EventType.SubAgentFinding }>;
      return {
        icon: "◆",
        label: t("runs.activity.label.subFinding"),
        detail: `${e.severity}: ${e.title}`,
        reason: e.detail,
        severity: e.severity === "high" || e.severity === "critical" ? "warn" : "info",
      };
    }
    case EventType.SubAgentFileWritten: {
      const e = event as Extract<RunEvent, { type: EventType.SubAgentFileWritten }>;
      return {
        icon: "✏",
        label: t("runs.activity.label.subWrite"),
        detail: `${e.path} (${e.bytes}B)`,
        reason: null,
        severity: "info",
      };
    }
    case EventType.SubAgentToolCall: {
      const e = event as Extract<RunEvent, { type: EventType.SubAgentToolCall }>;
      return {
        icon: "→",
        label: t("runs.activity.label.subTool"),
        detail: e.argsSummary ? `${e.toolName} ${e.argsSummary}` : e.toolName,
        reason: null,
        severity: "info",
      };
    }
    case EventType.SubAgentMessaged: {
      const e = event as Extract<RunEvent, { type: EventType.SubAgentMessaged }>;
      return {
        icon: e.continued ? "↩" : "→",
        label: e.continued ? t("runs.activity.label.sentBack") : t("runs.activity.label.toSub"),
        detail: `${e.subAgentId}: ${e.text}`,
        reason: null,
        severity: "info",
      };
    }
    case EventType.SubAgentCompleted: {
      const e = event as Extract<RunEvent, { type: EventType.SubAgentCompleted }>;
      return {
        icon: e.status === "Succeeded" ? "✓" : "✕",
        label: t("runs.activity.label.subDone"),
        detail: t("runs.activity.subDone", { id: e.subAgentId, status: e.status, obs: e.observationsCount, findings: e.findingsCount, cost: e.costUsd.toFixed(4) }),
        reason: null,
        severity: e.status === "Succeeded" ? "info" : "warn",
      };
    }
    case EventType.RunCancelRequested: {
      const e = event as Extract<RunEvent, { type: EventType.RunCancelRequested }>;
      return {
        icon: "⊘",
        label: t("runs.activity.label.runCancel"),
        detail: t("runs.activity.cancelRequested", { reason: e.reason }),
        reason: null,
        severity: "warn",
      };
    }
    case EventType.SandboxVanished: {
      const e = event as Extract<RunEvent, { type: EventType.SandboxVanished }>;
      return {
        icon: "✕",
        label: t("runs.activity.label.vanished"),
        detail: `${e.repo}: ${e.containerState} (${e.reason})`,
        reason: e.lastHeartbeatAt
          ? t("runs.activity.lastHeartbeat", { at: formatTime(e.lastHeartbeatAt) })
          : null,
        severity: "error",
      };
    }
    case EventType.RunCheckpointed: {
      // p0327: the run parked on a question — compute released, resumes on answer.
      const e = event as Extract<RunEvent, { type: EventType.RunCheckpointed }>;
      return {
        icon: "?",
        label: t("runs.activity.label.checkpointed"),
        detail: t("runs.activity.checkpointed", { id: e.questionId }),
        reason: t("runs.activity.deadline", { at: formatTime(e.answerDeadlineAt) }),
        severity: "info",
      };
    }
    case EventType.ExpectationRatified: {
      // p0328: the negotiated Soll block got its ratification outcome.
      const e = event as Extract<RunEvent, { type: EventType.ExpectationRatified }>;
      return {
        icon: "☑",
        label: t("runs.activity.label.expectation"),
        detail: t("runs.activity.ratified", { outcome: e.outcome, by: e.ratifiedBy }),
        reason: e.editDistance > 0 ? t("runs.activity.edited", { distance: e.editDistance }) : null,
        severity: "info",
      };
    }
    case EventType.RunStoryRecorded: {
      // p0344b: the run's story artifacts (ledger + acceptance) were persisted.
      const e = event as Extract<RunEvent, { type: EventType.RunStoryRecorded }>;
      const parts = [
        e.progressLedgerJson !== null ? t("runs.activity.ledger") : null,
        e.acceptanceJson !== null ? t("runs.activity.acceptance") : null,
      ].filter((p): p is string => p !== null);
      return {
        icon: "☰",
        label: t("runs.activity.label.story"),
        detail: parts.length > 0 ? t("runs.activity.recorded", { parts: parts.join(" + ") }) : t("runs.activity.recordedEmpty"),
        reason: null,
        severity: "info",
      };
    }
    case EventType.RunBudgetResolved: {
      // p0357: the run's cost budget was sized from the complexity tier.
      const e = event as Extract<RunEvent, { type: EventType.RunBudgetResolved }>;
      return {
        icon: "◔",
        label: t("runs.activity.label.budget"),
        detail: t("runs.activity.budget", { tier: e.tier, cap: e.capUsd, tokens: e.capTokens.toLocaleString(locale) }),
        reason: null,
        severity: "info",
      };
    }
    case EventType.RunWorkShapeResolved: {
      // p0413: the shape that decided how the ticket was cut into specs.
      const e = event as Extract<RunEvent, { type: EventType.RunWorkShapeResolved }>;
      return {
        icon: "◇",
        label: t("runs.activity.label.shape"),
        detail: e.reason ? `${e.shape} — ${e.reason}` : e.shape,
        reason: null,
        severity: "info",
      };
    }
    case EventType.LedgerTransitionsRecorded: {
      // p0374a: what one update_progress call actually changed. The Story row
      // above shows the ledger's current SHAPE, which every flush overwrites;
      // this is the history that shape came from. A refused rewrite is a warning:
      // the model tried to take completed work back without the reopen token.
      const e = event as Extract<RunEvent, { type: EventType.LedgerTransitionsRecorded }>;
      const transitions = parseTransitions(e.transitionsJson);
      const refused = transitions.filter(
        (x) => x.cause === "regression_refused" || x.cause === "omission_refused",
      ).length;
      return {
        icon: refused > 0 ? "⤺" : "☑",
        label: t("runs.activity.label.ledger"),
        detail:
          transitions.length === 0
            ? t("runs.activity.noChange")
            : t("runs.activity.transitions", { count: transitions.length, pass: transitions[0].pass }),
        reason:
          refused > 0
            ? t("runs.activity.refused", { count: refused })
            : null,
        severity: refused > 0 ? "warn" : "info",
      };
    }
    // p0410: an event this build has never heard of is not a reason to take the
    // page down. A newer server may send one at any time; render what every event
    // carries and let the payload toggle show the rest.
    default: {
      return {
        icon: "•",
        label: t("runs.activity.label.event"),
        detail: t("runs.activity.type", { type: (event as RunEvent).type }),
        reason: null,
        severity: "info",
      };
    }
  }
}

/** p0374a: the trail carries transitions as JSON; a corrupt payload renders as
 * "no change" rather than taking the row down. */
function parseTransitions(json: string): { cause: string; pass: number }[] {
  try {
    const parsed: unknown = JSON.parse(json);
    return Array.isArray(parsed) ? (parsed as { cause: string; pass: number }[]) : [];
  } catch {
    return [];
  }
}

function severityClass(severity: Severity): string {
  switch (severity) {
    case "ok":
      return "border-stone-200 bg-white";
    case "warn":
      return "border-amber-300 bg-amber-50";
    case "error":
      return "border-rose-300 bg-rose-50";
    case "info":
    default:
      return "border-stone-200 bg-white";
  }
}

function formatTime(timestamp: string): string {
  const d = new Date(timestamp);
  if (Number.isNaN(d.getTime())) return timestamp;
  const hh = String(d.getHours()).padStart(2, "0");
  const mm = String(d.getMinutes()).padStart(2, "0");
  const ss = String(d.getSeconds()).padStart(2, "0");
  return `${hh}:${mm}:${ss}`;
}

function PayloadJson({ value }: { value: unknown }) {
  return (
    <pre className="ml-[136px] overflow-auto rounded bg-stone-50 p-2 text-xs text-stone-700">
      {JSON.stringify(value, null, 2)}
    </pre>
  );
}
