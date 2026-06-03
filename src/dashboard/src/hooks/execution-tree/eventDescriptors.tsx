"use client";

import { EventType, type RunEvent } from "@/types/hub-events";
import type { DrawerEvent, EventKind } from "@/components/execution/EventDrawer";

// p0203: pure helpers — turn typed RunEvent into the short string the
// LiveTail + EventDrawer rows render. Lifted out of useRunExecutionTree
// so the hook stays focused on orchestration. LLM start/finish strings
// stay for unpaired fallback rendering; the paired-row path bypasses
// these.

export function describeEvent(e: RunEvent): string {
  switch (e.type) {
    case EventType.L1StepDetail:
      return e.detail;
    case EventType.DecisionLogged:
      return `decision · ${e.chose}`;
    case EventType.GateChecked:
      return `gate · ${e.gate} · ${e.passed ? "pass" : "fail"}`;
    case EventType.ToolCall:
      return e.summary ?? `tool · ${e.tool}`;
    case EventType.ToolResult:
      return `${e.tool} · ${e.ok ? "ok" : "fail"}${e.errorMessage ? " · " + e.errorMessage : ""}`;
    case EventType.LlmCallStarted:
      return `LLM start · ${e.model} (${e.role})`;
    case EventType.LlmCallFinished:
      return `LLM · ${e.tokensIn} in / ${e.tokensOut} out · ${(e.durationMs / 1000).toFixed(1)}s`;
    case EventType.SubAgentObservation:
      return e.text;
    case EventType.SubAgentFinding:
      return `${capitalise(e.severity)} · ${e.title}`;
    case EventType.SubAgentFileWritten:
      return `wrote ${e.path}`;
    case EventType.SubAgentToolCall:
      return e.argsSummary ?? `tool · ${e.toolName}`;
    case EventType.TicketFetched:
      return `#${e.ticketId} — ${e.title}`;
    default:
      return EventType[e.type] ?? "event";
  }
}

export function mapToDrawerEvents(es: RunEvent[]): DrawerEvent[] {
  const out: DrawerEvent[] = [];
  for (const e of es) {
    const kind = kindOf(e);
    if (!kind) continue;
    out.push({
      id: `${e.type}-${e.timestamp}-${out.length}`,
      timestamp: shortTime(e.timestamp),
      kind,
      severity: severityOf(e),
      body: <span>{describeEvent(e)}</span>,
      searchText: describeEvent(e),
    });
  }
  return out;
}

export function kindOf(e: RunEvent): EventKind | null {
  switch (e.type) {
    case EventType.L1StepDetail:
    case EventType.SubAgentObservation:
      return "obs";
    case EventType.SubAgentFinding:
      return "find";
    case EventType.ToolCall:
    case EventType.ToolResult:
    case EventType.SubAgentToolCall:
      return "tool";
    case EventType.LlmCallStarted:
    case EventType.LlmCallFinished:
      return "llm";
    case EventType.SubAgentFileWritten:
      return "file";
    case EventType.DecisionLogged:
    case EventType.GateChecked:
      return "dec";
    default:
      return null;
  }
}

export function severityOf(e: RunEvent): "high" | "med" | "info" | undefined {
  if (e.type === EventType.SubAgentFinding) {
    const s = e.severity.toLowerCase();
    if (s === "high" || s === "critical") return "high";
    if (s === "med" || s === "medium" || s === "moderate") return "med";
    return "info";
  }
  return undefined;
}

export function shortTime(iso: string): string {
  const d = new Date(iso);
  return d.toISOString().slice(11, 19);
}

export function formatHms(ms: number): string {
  return new Date(ms).toISOString().slice(11, 19);
}

export function formatDuration(seconds: number): string {
  if (!isFinite(seconds) || seconds <= 0) return "—";
  if (seconds < 1) return `${Math.round(seconds * 1000)}ms`;
  if (seconds < 60) return `${seconds.toFixed(1)}s`;
  const m = Math.floor(seconds / 60);
  const rem = Math.round(seconds - m * 60);
  return rem === 0 ? `${m}m` : `${m}m${rem}s`;
}

function capitalise(s: string): string {
  return s.length === 0 ? s : s[0].toUpperCase() + s.slice(1);
}
