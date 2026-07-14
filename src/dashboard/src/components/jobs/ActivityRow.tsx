"use client";

import {
  EventType,
  type CatalogIssueEvent,
  type DecisionLoggedEvent,
  type GateCheckedEvent,
  type LlmCallFinishedEvent,
  type LlmCallStartedEvent,
  type RunEvent,
  type StepFinishedEvent,
  type StepStartedEvent,
  type ToolCallEvent,
  type ToolResultEvent,
  type TriageRouteEvent,
} from "@/types/hub-events";

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
  const view = projectEvent(event);
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

function projectEvent(event: RunEvent): RowView {
  switch (event.type) {
    case EventType.RunStarted: {
      return {
        icon: "▶",
        label: "Run",
        detail: "started",
        reason: null,
        severity: "info",
      };
    }
    case EventType.RunFinished: {
      const e = event as Extract<RunEvent, { type: EventType.RunFinished }>;
      return {
        icon: e.status === "success" ? "✓" : "✕",
        label: "Run",
        detail: `${e.status} — ${e.summary}`,
        reason: null,
        severity: e.status === "success" ? "ok" : "error",
      };
    }
    case EventType.StepStarted: {
      const e = event as StepStartedEvent;
      // p0203: prefer the operator-facing displayName when present; fall
      // back to the raw stepName for pre-p0203 producers.
      const label = e.displayName ?? e.stepName;
      return {
        icon: "•",
        label: "Step start",
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
        label: ok ? "Step done" : "Step failed",
        detail: `${e.stepIndex} — ${e.status} (${e.durationMs}ms)`,
        reason: !ok ? e.reason : null,
        severity: ok ? "ok" : "error",
      };
    }
    case EventType.SandboxCreated: {
      const e = event as Extract<RunEvent, { type: EventType.SandboxCreated }>;
      return {
        icon: "□",
        label: "Sandbox",
        detail: `created ${e.repo} — ${e.image}`,
        reason: null,
        severity: "info",
      };
    }
    case EventType.SandboxDisposed: {
      const e = event as Extract<RunEvent, { type: EventType.SandboxDisposed }>;
      return {
        icon: "□",
        label: "Sandbox",
        detail: `disposed ${e.repo}${e.exitCode !== null ? ` (exit ${e.exitCode})` : ""}`,
        reason: null,
        severity: "info",
      };
    }
    case EventType.SandboxCommand: {
      const e = event as Extract<RunEvent, { type: EventType.SandboxCommand }>;
      return {
        icon: "⟫",
        label: "Sandbox cmd",
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
        label: "Sandbox out",
        detail: `${e.repo}: ${e.command} (exit ${e.exitCode}, ${e.durationMs}ms)`,
        reason: null,
        severity: ok ? "info" : "warn",
      };
    }
    case EventType.DecisionLogged: {
      const e = event as DecisionLoggedEvent;
      return {
        icon: "◆",
        label: "Decision",
        detail: `${e.category}: ${e.chose}${e.over ? ` over ${e.over}` : ""}`,
        reason: e.reason,
        severity: "info",
      };
    }
    case EventType.TriageRoute: {
      const e = event as TriageRouteEvent;
      return {
        icon: "◇",
        label: "Triage",
        detail: `${e.skill} → ${e.role} (${e.confidence})`,
        reason: null,
        severity: "info",
      };
    }
    case EventType.GateChecked: {
      const e = event as GateCheckedEvent;
      return {
        icon: e.passed ? "✓" : "✕",
        label: "Gate",
        detail: `${e.gate} — ${e.passed ? "passed" : "failed"}`,
        reason: !e.passed ? e.reason : null,
        severity: e.passed ? "info" : "error",
      };
    }
    case EventType.LlmCallStarted: {
      const e = event as LlmCallStartedEvent;
      return {
        icon: "↗",
        label: "LLM start",
        detail: `${e.model} · ${e.role}`,
        reason: null,
        severity: "info",
      };
    }
    case EventType.LlmCallFinished: {
      const e = event as LlmCallFinishedEvent;
      return {
        icon: "↘",
        label: "LLM done",
        detail: `${e.tokensIn}in/${e.tokensOut}out · $${e.costUsd.toFixed(4)} · ${e.durationMs}ms`,
        reason: null,
        severity: "info",
      };
    }
    case EventType.ToolCall: {
      const e = event as ToolCallEvent;
      return {
        icon: "→",
        label: "Tool call",
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
        label: "Tool result",
        detail: `${e.tool} — ${e.ok ? `ok (${e.resultLength}B)` : "failed"}`,
        reason: !e.ok ? e.errorMessage : null,
        severity: e.ok ? "info" : "warn",
      };
    }
    case EventType.CatalogLoaded: {
      const e = event as Extract<RunEvent, { type: EventType.CatalogLoaded }>;
      return {
        icon: "▦",
        label: "Catalog",
        detail: `${e.version} — ${e.conceptCount} concepts · ${e.skillsLoaded} skills · ${e.mastersCount} masters`,
        reason: e.fromCache ? "warm cache" : "fresh pull",
        severity: "info",
      };
    }
    case EventType.CatalogIssue: {
      const e = event as CatalogIssueEvent;
      return {
        icon: e.severity === "error" ? "✕" : "⚠",
        label: "Catalog",
        detail: `${e.category} in ${e.source}`,
        reason: e.message,
        severity: e.severity === "error" ? "error" : "warn",
      };
    }
    case EventType.PullRequestOutcome: {
      // p0223: a no-changes repo is a normal outcome (neutral), not a failure.
      const e = event as Extract<RunEvent, { type: EventType.PullRequestOutcome }>;
      const detail = e.status === "no_changes"
        ? "no changes — no PR needed"
        : e.status === "opened"
          ? `PR opened — ${e.url}`
          : "failed";
      return {
        icon: e.status === "opened" ? "✓" : e.status === "failed" ? "✕" : "○",
        label: "PR",
        detail: `${e.repo}: ${detail}`,
        reason: e.status === "failed" ? e.reason : null,
        severity: e.status === "failed" ? "error" : e.status === "opened" ? "ok" : "info",
      };
    }
    case EventType.TicketInstructionIgnored: {
      // p0316: the master refused a ticket-embedded instruction (out-of-scope /
      // destructive / injection). Surfaced as a warning with the verbatim quote.
      const e = event as Extract<RunEvent, { type: EventType.TicketInstructionIgnored }>;
      return {
        icon: "⚠",
        label: "Ignored instruction",
        detail: `"${e.quote}"`,
        reason: e.reason,
        severity: "warn",
      };
    }
    case EventType.L1StepDetail: {
      const e = event as Extract<RunEvent, { type: EventType.L1StepDetail }>;
      return {
        icon: "·",
        label: "Step detail",
        detail: `${e.stepIndex} ${e.origin}: ${e.detail}`,
        reason: null,
        severity: "info",
      };
    }
    case EventType.TicketFetched: {
      const e = event as Extract<RunEvent, { type: EventType.TicketFetched }>;
      return {
        icon: "✉",
        label: "Ticket",
        detail: `#${e.ticketId} — ${e.title}`,
        reason: null,
        severity: "info",
      };
    }
    case EventType.SubAgentSpawned: {
      const e = event as Extract<RunEvent, { type: EventType.SubAgentSpawned }>;
      return {
        icon: "⤴",
        label: "Sub-agent spawn",
        detail: `${e.name} — ${e.activity}`,
        reason: null,
        severity: "info",
      };
    }
    case EventType.SubAgentObservation: {
      const e = event as Extract<RunEvent, { type: EventType.SubAgentObservation }>;
      return {
        icon: "·",
        label: "Sub-agent obs",
        detail: `${e.subAgentId}: ${e.text}`,
        reason: null,
        severity: "info",
      };
    }
    case EventType.SubAgentFinding: {
      const e = event as Extract<RunEvent, { type: EventType.SubAgentFinding }>;
      return {
        icon: "◆",
        label: "Sub-agent finding",
        detail: `${e.severity}: ${e.title}`,
        reason: e.detail,
        severity: e.severity === "high" || e.severity === "critical" ? "warn" : "info",
      };
    }
    case EventType.SubAgentFileWritten: {
      const e = event as Extract<RunEvent, { type: EventType.SubAgentFileWritten }>;
      return {
        icon: "✏",
        label: "Sub-agent write",
        detail: `${e.path} (${e.bytes}B)`,
        reason: null,
        severity: "info",
      };
    }
    case EventType.SubAgentToolCall: {
      const e = event as Extract<RunEvent, { type: EventType.SubAgentToolCall }>;
      return {
        icon: "→",
        label: "Sub-agent tool",
        detail: e.argsSummary ? `${e.toolName} ${e.argsSummary}` : e.toolName,
        reason: null,
        severity: "info",
      };
    }
    case EventType.SubAgentCompleted: {
      const e = event as Extract<RunEvent, { type: EventType.SubAgentCompleted }>;
      return {
        icon: e.status === "Succeeded" ? "✓" : "✕",
        label: "Sub-agent done",
        detail: `${e.subAgentId}: ${e.status} — ${e.observationsCount} obs, ${e.findingsCount} findings, $${e.costUsd.toFixed(4)}`,
        reason: null,
        severity: e.status === "Succeeded" ? "info" : "warn",
      };
    }
    case EventType.RunCancelRequested: {
      const e = event as Extract<RunEvent, { type: EventType.RunCancelRequested }>;
      return {
        icon: "⊘",
        label: "Run cancel",
        detail: `requested (${e.reason})`,
        reason: null,
        severity: "warn",
      };
    }
    case EventType.SandboxVanished: {
      const e = event as Extract<RunEvent, { type: EventType.SandboxVanished }>;
      return {
        icon: "✕",
        label: "Sandbox vanished",
        detail: `${e.repo}: ${e.containerState} (${e.reason})`,
        reason: e.lastHeartbeatAt
          ? `last heartbeat: ${formatTime(e.lastHeartbeatAt)}`
          : null,
        severity: "error",
      };
    }
    case EventType.RunCheckpointed: {
      // p0327: the run parked on a question — compute released, resumes on answer.
      const e = event as Extract<RunEvent, { type: EventType.RunCheckpointed }>;
      return {
        icon: "?",
        label: "Checkpointed",
        detail: `waiting for an operator answer (question ${e.questionId})`,
        reason: `answer deadline: ${formatTime(e.answerDeadlineAt)}`,
        severity: "info",
      };
    }
    case EventType.ExpectationRatified: {
      // p0328: the negotiated Soll block got its ratification outcome.
      const e = event as Extract<RunEvent, { type: EventType.ExpectationRatified }>;
      return {
        icon: "☑",
        label: "Expectation",
        detail: `ratified ${e.outcome} by ${e.ratifiedBy}`,
        reason: e.editDistance > 0 ? `edited (distance ${e.editDistance})` : null,
        severity: "info",
      };
    }
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
