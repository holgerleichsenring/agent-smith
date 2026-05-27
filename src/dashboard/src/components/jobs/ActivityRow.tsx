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
      return {
        icon: "•",
        label: "Step start",
        detail: `${e.stepIndex}/${e.totalSteps} ${e.stepName}`,
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
