import { callActivity, pairLlmCalls } from "@/hooks/execution-tree/llmPairing";
import { pullRequestStatusLabel } from "@/lib/prStatus";
import { extractUrls, stripUrls } from "@/lib/prUrls";
import { EventType, type PullRequestStatus, type RunEvent } from "@/types/hub-events";
import { clockOf } from "./runNowLines";

// 2026-11-17-6d9ec: one step's events as terminal lines, what the graph's step panel drew as an event drawer, a command
// timeline, LLM rows, per-repository sandbox boxes, PR outcomes and the ticket/catalog bodies. Every line carries a time,
// a kind, its text, a right-hand meta and the filter it answers to; the summary counts what the command timeline did.

export type DetailFilter = "find" | "obs" | "tool" | "llm" | "file" | "dec";
export type DetailTone = "text" | "dim" | "ok" | "run" | "bad";

export interface DetailLine {
  key: string;
  /** ISO time the line sorts by. */
  ts: string;
  at: string;
  kind: string;
  text: string;
  meta: string;
  /** Null for a line only "All" shows: a pull request, the ticket, the catalog. */
  filter: DetailFilter | null;
  tone: DetailTone;
  href?: string;
}

export interface RepoOutcome {
  repo: string;
  summary: string;
  /** null while the command runs; -1 when it could not run. */
  exitCode: number | null;
  durationMs: number | null;
}

export interface StepDetailView {
  lines: DetailLine[];
  actions: number;
  writes: number;
  llm: number;
  costUsd: number;
  repos: RepoOutcome[];
}

const WRITE_VERBS = new Set(["WriteFile", "DeleteFile", "MoveFile"]);

export function shortMs(ms: number | null | undefined): string {
  if (ms == null) return "";
  if (ms < 1000) return `${ms}ms`;
  const s = ms / 1000;
  if (s < 60) return `${s.toFixed(1)}s`;
  const m = Math.floor(s / 60);
  return `${m}m ${Math.round(s - m * 60)}s`;
}

function tokens(n: number | null): string {
  return n === null ? "?" : n.toLocaleString("en-US");
}

interface Command {
  ts: string;
  repo: string;
  verb: string;
  summary: string | null;
  write: boolean;
  exitCode: number | null;
  durationMs: number | null;
  timedOut: boolean;
  limit: number | null;
}

/** Each SandboxCommand with the SandboxResult that closed it (the newest open command of the same repository). */
function commandsOf(events: RunEvent[]): Command[] {
  const out: Command[] = [];
  for (const e of events) {
    if (e.type === EventType.SandboxCommand) {
      out.push({ ts: e.timestamp, repo: e.repo, verb: e.command, summary: e.summary, write: e.isWrite === true || WRITE_VERBS.has(e.command),
        exitCode: null, durationMs: null, timedOut: false, limit: null });
    } else if (e.type === EventType.SandboxResult) {
      for (let i = out.length - 1; i >= 0; i--) {
        const c = out[i];
        if (c.repo === e.repo && c.exitCode === null && c.durationMs === null) {
          c.exitCode = e.exitCode;
          c.durationMs = e.durationMs;
          c.timedOut = e.timedOut === true;
          c.limit = e.timeoutSeconds ?? null;
          break;
        }
      }
    }
  }
  return out;
}

function commandOutcome(c: Command): { meta: string; tone: DetailTone } {
  const took = c.durationMs !== null ? ` · ${shortMs(c.durationMs)}` : "";
  if (c.exitCode === null) return { meta: "running…", tone: "run" };
  if (c.timedOut) return { meta: `timed out${took}${c.limit ? ` (limit ${c.limit}s)` : ""}`, tone: "bad" };
  if (c.exitCode === 0) return { meta: `exit 0${took}`, tone: c.write ? "ok" : "text" };
  if (c.exitCode === -1) return { meta: `not run${took}`, tone: "dim" };
  return { meta: `exit ${c.exitCode}${took}`, tone: "bad" };
}

export function stepDetailLines(events: RunEvent[], runEnded: boolean): StepDetailView {
  const lines: DetailLine[] = [];
  const names = new Map<string, string>();
  for (const e of events) if (e.type === EventType.SubAgentSpawned) names.set(e.subAgentId, e.name);
  const who = (id: string) => (names.has(id) ? `${names.get(id)}: ` : "");
  // 2026-11-18-c8e2d: a child's own tool calls carry its name as their role, so its work reads apart from the master's.
  const children = new Set(names.values());
  const by = (role: string | null) => (role && children.has(role) ? `${role}: ` : "");
  const add = (ts: string, kind: string, text: string, filter: DetailFilter | null, tone: DetailTone = "text", meta = "", href?: string) =>
    lines.push({ key: `${lines.length}`, ts, at: clockOf(ts) ?? "", kind, text, meta, filter, tone, href });

  const llm = pairLlmCalls(events);
  for (const call of llm.pairs) {
    const cache = call.cachedTokensIn ? ` · cache ${tokens(call.cachedTokensIn)}` : "";
    const meta = call.finishedAt === null
      ? runEnded ? "ended" : "in flight"
      : [call.costUsd !== null ? `$${call.costUsd.toFixed(3)}` : "", shortMs(call.durationMs)].filter(Boolean).join(" · ");
    add(call.startedAt, "llm", `${callActivity(call)} · ${call.model} · ${tokens(call.tokensIn)} in / ${tokens(call.tokensOut)} out${cache}`,
      "llm", call.finishedAt === null && !runEnded ? "run" : "dim", meta);
  }

  const commands = commandsOf(events);
  for (const c of commands) {
    const outcome = commandOutcome(c);
    add(c.ts, c.write ? "write" : "tool", `${c.repo} ${c.verb}${c.summary ? `  ${c.summary}` : ""}`, c.write ? "file" : "tool", outcome.tone, outcome.meta);
  }

  for (const e of events) {
    switch (e.type) {
      case EventType.ToolCall:
        add(e.timestamp, "tool", `${by(e.role)}${e.tool}${e.summary ? `  ${e.summary}` : ""}`, "tool", "text", e.repeat && e.repeat > 1 ? `repeat ${e.repeat}` : "");
        break;
      case EventType.ToolResult:
        if (!e.ok) add(e.timestamp, "tool", `${e.tool} failed${e.errorMessage ? `: ${e.errorMessage}` : ""}`, "tool", "bad");
        break;
      case EventType.SubAgentToolCall:
        add(e.timestamp, "tool", `${who(e.subAgentId)}${e.argsSummary ?? e.toolName}`, "tool");
        break;
      case EventType.SubAgentFileWritten:
        add(e.timestamp, "write", `${who(e.subAgentId)}${e.path}`, "file", "ok", `${e.bytes} bytes`);
        break;
      case EventType.DecisionLogged:
        add(e.timestamp, "decision", `${e.chose}${e.over ? ` over ${e.over}` : ""}${e.reason ? ` — ${e.reason}` : ""}`, "dec", "text", e.category);
        break;
      case EventType.GateChecked:
        add(e.timestamp, "gate", `${e.gate} ${e.passed ? "passed" : "failed"}${e.reason ? `: ${e.reason}` : ""}`, "dec", e.passed ? "dim" : "bad");
        break;
      case EventType.TicketInstructionIgnored:
        add(e.timestamp, "decision", `ignored a ticket instruction "${e.quote}" — ${e.reason}`, "dec", "run");
        break;
      case EventType.SubAgentFinding: {
        const high = /^(high|critical)$/i.test(e.severity);
        add(e.timestamp, "finding", `${who(e.subAgentId)}${e.severity} · ${e.title}${e.detail ? ` — ${e.detail}` : ""}`, "find", high ? "bad" : "run");
        break;
      }
      case EventType.SubAgentObservation:
        add(e.timestamp, "obs", `${who(e.subAgentId)}${e.text}`, "obs", "dim");
        break;
      case EventType.L1StepDetail:
        add(e.timestamp, "obs", e.detail, "obs", "dim");
        break;
      case EventType.SubAgentSpawned:
        add(e.timestamp, "sub-agent", `${e.name} started: ${e.activity}`, "obs");
        break;
      case EventType.SubAgentCompleted:
        add(e.timestamp, "sub-agent", `${names.get(e.subAgentId) ?? e.subAgentId} ${childEnd(e.status)} after ${e.toolCalls} tool calls`, "obs",
          e.status === "Succeeded" ? "text" : e.status === "Stopped" ? "dim" : "bad");
        break;
      case EventType.SubAgentMessaged:
        add(e.timestamp, "sub-agent", `${e.continued ? "sent back to" : "to"} ${names.get(e.subAgentId) ?? e.subAgentId}: ${e.text}`, "obs",
          e.continued ? "run" : "text");
        break;
      case EventType.PullRequestOutcome: {
        const url = e.url ?? extractUrls(e.reason)[0];
        const reason = e.reason ? ` — ${stripUrls(e.reason)}` : "";
        add(e.timestamp, "pr", `${e.repo} ${pullRequestStatusLabel(e.status as PullRequestStatus)}${reason}`, null,
          e.status === "failed" ? "bad" : e.status === "no_changes" ? "dim" : "ok", "", url ?? undefined);
        break;
      }
      case EventType.TicketFetched:
        add(e.timestamp, "ticket", `#${e.ticketId} — ${e.title}`, null, "text", e.state, e.webUrl ?? undefined);
        break;
      case EventType.CatalogLoaded:
        add(e.timestamp, "catalog", `${e.version} from ${e.source.toLowerCase()} · ${e.conceptCount} concepts · ${e.skillsLoaded} skills · ${e.mastersCount} masters${e.fromCache ? " · warm cache" : ""}`,
          null, "text", `${e.durationMs}ms`, "/system/catalog");
        break;
      case EventType.CatalogIssue:
        add(e.timestamp, "catalog", `${e.severity}: ${e.message}`, null, /error/i.test(e.severity) ? "bad" : "run");
        break;
      default:
        break;
    }
  }

  lines.sort((l, r) => r.ts.localeCompare(l.ts) || Number(r.key) - Number(l.key));
  const repos = new Map<string, RepoOutcome>();
  for (const c of commands) repos.set(c.repo, { repo: c.repo, summary: c.summary ?? c.verb, exitCode: c.exitCode, durationMs: c.durationMs });
  const fileWrites = events.filter((e) => e.type === EventType.SubAgentFileWritten).length;
  return {
    lines,
    actions: commands.length,
    writes: commands.filter((c) => c.write).length + fileWrites,
    llm: llm.callCount,
    costUsd: llm.totalCostUsd,
    repos: [...repos.values()],
  };
}

/** "3/4 repositories ok, failed: client" for a step that ran in more than one repository; null otherwise. */
export function repoSummary(repos: RepoOutcome[]): string | null {
  if (repos.length < 2) return null;
  const finished = repos.filter((r) => r.exitCode !== null);
  const failed = finished.filter((r) => r.exitCode !== 0 && r.exitCode !== -1).map((r) => r.repo);
  const ok = finished.filter((r) => r.exitCode === 0).length;
  return `${ok}/${repos.length} repositories ok${failed.length > 0 ? `, failed: ${failed.join(", ")}` : ""}`;
}

/** "passed · 41.0s", "failed (exit 2)", "not run", "running…". */
export function repoOutcomeWords(r: RepoOutcome): string {
  if (r.exitCode === null) return "running…";
  if (r.exitCode === -1) return "not run";
  const took = r.durationMs !== null ? ` · ${shortMs(r.durationMs)}` : "";
  return r.exitCode === 0 ? `passed${took}` : `failed (exit ${r.exitCode})${took}`;
}

/** 2026-11-18-c8e2d: how a child's pass ended, in the run log's words. */
function childEnd(status: string): string {
  switch (status) {
    case "Succeeded": return "finished";
    case "Failed": return "failed";
    case "Empty": return "finished with no answer";
    case "Stopped": return "stopped with its master's loop";
    default: return status.toLowerCase();
  }
}
