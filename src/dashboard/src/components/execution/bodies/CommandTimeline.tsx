"use client";

import { useState } from "react";
import type { SandboxCommandEntry } from "@/hooks/execution-tree/buckets";
import type { PairedLlmCall } from "@/hooks/execution-tree/llmPairing";

// p0228/p0231: the step's actions in the order the agent ran them — ONE
// chronological list interleaving the LLM turns ("the call") with the sandbox
// commands they issued (ReadFile, Grep, find, WriteFile, git, …). Previously the
// LLM calls and the commands were two disconnected lists you couldn't correlate;
// merging by timestamp means each call is followed by what it actually did. A
// run that only ever reads + greps and never WriteFiles a source file (the
// 0-changes case) is obvious here.

interface CommandTimelineProps {
  commands: SandboxCommandEntry[];
  // p0231: the step's paired LLM turns, merged into the same timeline by time.
  llmCalls?: ReadonlyArray<PairedLlmCall>;
  runEnded?: boolean;
  defaultCap?: number;
}

// Verbs that mutate the working tree — surfaced so a real edit stands out from
// the wall of read-only exploration.
const WRITE_VERBS = new Set(["WriteFile", "DeleteFile", "MoveFile"]);

// p0229: per-repo accent colours so "which repo does this command act on" is
// answerable at a glance on a multi-repo run.
const REPO_PALETTE = [
  "text-sky-700", "text-violet-700", "text-teal-700", "text-amber-700",
  "text-rose-700", "text-cyan-700", "text-lime-700", "text-fuchsia-700",
];

interface RepoDisplay {
  label: string;
  color: string;
}

type TimelineEntry =
  | { kind: "cmd"; ts: string; cmd: SandboxCommandEntry }
  | { kind: "llm"; ts: string; call: PairedLlmCall };

// p0229: every repo in a run shares a long common prefix (e.g.
// `acme-platform-server`, `acme-platform-client`) — truncating left-to-right
// hides exactly the distinguishing suffix. Strip the common prefix (trimmed to
// a clean `-`/`/` boundary) so the suffix shows, and assign a stable colour.
function buildRepoDisplay(commands: SandboxCommandEntry[]): Map<string, RepoDisplay> {
  const repos = [...new Set(commands.map((c) => c.repo))].sort();
  const prefix = repos.length > 1 ? trimToBoundary(longestCommonPrefix(repos)) : "";
  const map = new Map<string, RepoDisplay>();
  repos.forEach((repo, i) => {
    const label = prefix && repo.length > prefix.length ? repo.slice(prefix.length) : repo;
    map.set(repo, {
      label,
      color: repos.length > 1 ? REPO_PALETTE[i % REPO_PALETTE.length] : "text-stone-400",
    });
  });
  return map;
}

function longestCommonPrefix(strs: string[]): string {
  if (strs.length === 0) return "";
  let prefix = strs[0];
  for (const s of strs.slice(1)) {
    while (!s.startsWith(prefix)) prefix = prefix.slice(0, -1);
    if (!prefix) break;
  }
  return prefix;
}

function trimToBoundary(prefix: string): string {
  const cut = Math.max(prefix.lastIndexOf("-"), prefix.lastIndexOf("/"));
  return cut >= 0 ? prefix.slice(0, cut + 1) : "";
}

// Merge commands + LLM turns into one time-ordered list. An LLM turn is keyed by
// its START time so the commands it issued (which run after it returns) sort
// directly beneath it — the list then reads "call → what it did → next call".
function mergeTimeline(
  commands: SandboxCommandEntry[], llmCalls: ReadonlyArray<PairedLlmCall>,
): TimelineEntry[] {
  const entries: TimelineEntry[] = [
    ...commands.map((cmd): TimelineEntry => ({ kind: "cmd", ts: cmd.timestamp, cmd })),
    ...llmCalls.map((call): TimelineEntry => ({ kind: "llm", ts: call.startedAt, call })),
  ];
  return entries.sort((a, b) => a.ts.localeCompare(b.ts));
}

export function CommandTimeline({
  commands, llmCalls = [], runEnded = false, defaultCap = 12,
}: CommandTimelineProps) {
  const [showAll, setShowAll] = useState(false);
  const merged = mergeTimeline(commands, llmCalls);
  if (merged.length === 0) return null;

  const writes = commands.filter((c) => WRITE_VERBS.has(c.verb)).length;
  const totalCost = llmCalls.reduce((sum, c) => sum + (c.costUsd ?? 0), 0);
  const repoDisplay = buildRepoDisplay(commands);
  const shown = showAll ? merged : merged.slice(0, defaultCap);
  const overCap = merged.length > defaultCap;

  return (
    <div data-testid="command-timeline" className="space-y-1">
      <div className="flex items-center gap-2 dsh-body text-stone-500">
        <span data-testid="command-timeline-summary">
          {commands.length} action{commands.length === 1 ? "" : "s"}
          {" · "}
          <span className={writes > 0 ? "text-emerald-700" : "text-amber-700"}>
            {writes} write{writes === 1 ? "" : "s"}
          </span>
          {llmCalls.length > 0 && (
            <>
              {" · "}
              <span className="text-indigo-700">{llmCalls.length} llm</span>
              {" · "}${totalCost.toFixed(4)}
            </>
          )}
        </span>
      </div>
      <div data-testid="command-timeline-list">
        {shown.map((e, i) =>
          e.kind === "cmd" ? (
            <CommandRow key={`c-${e.ts}-${i}`} entry={e.cmd} repo={repoDisplay.get(e.cmd.repo)} />
          ) : (
            <LlmRow key={`l-${e.ts}-${i}`} call={e.call} runEnded={runEnded} />
          ),
        )}
      </div>
      {overCap && (
        <button
          type="button"
          data-testid="command-timeline-show-all"
          onClick={() => setShowAll((s) => !s)}
          className="inline-flex items-center gap-1.5 py-1 dsh-body font-medium text-emerald-700 hover:underline"
        >
          {showAll ? "▴ show fewer" : `▾ show all ${merged.length} steps`}
        </button>
      )}
    </div>
  );
}

// p0231: the LLM turn rendered inline in the timeline — the "call" itself, with
// the commands it issued sorting directly beneath it. Tinted + indigo role so it
// reads as the decision point, not another command.
function LlmRow({ call, runEnded }: { call: PairedLlmCall; runEnded: boolean }) {
  const inFlight = call.finishedAt === null;
  return (
    <div
      data-testid="timeline-llm-row"
      data-role={call.role}
      className="flex items-baseline gap-2 border-b border-stone-100 bg-indigo-50/40 py-1 font-mono dsh-body last:border-b-0"
    >
      <span className="w-40 flex-none truncate font-semibold text-indigo-700" title={call.role}>
        {roleLabel(call)}
      </span>
      <span className="w-24 flex-none truncate text-stone-500">{call.model}</span>
      <span className="flex-1 truncate text-stone-500">
        {formatTokens(call.tokensIn, call.tokensOut)}
      </span>
      <span className="flex-none text-stone-700">
        {formatCost(call.costUsd)}
        {call.durationMs !== null ? ` · ${formatMs(call.durationMs)}` : ""}
        {inFlight && (
          <span
            className={`ml-1.5 rounded px-1.5 py-0.5 dsh-label ${
              runEnded ? "bg-stone-100 text-stone-500" : "bg-amber-100 text-amber-800"
            }`}
          >
            {runEnded ? "ended" : "in flight"}
          </span>
        )}
      </span>
    </div>
  );
}

function roleLabel(call: PairedLlmCall): string {
  if (call.roleIsUnknown) return call.phase && call.phase.length > 0 ? call.phase : "llm call";
  return call.role;
}

function CommandRow({ entry, repo }: { entry: SandboxCommandEntry; repo?: RepoDisplay }) {
  const isWrite = WRITE_VERBS.has(entry.verb);
  const failed = entry.exitCode !== null && entry.exitCode !== 0 && entry.exitCode !== -1;
  return (
    <div
      data-testid={`command-row-${entry.verb}`}
      data-write={isWrite}
      data-repo={entry.repo}
      className="flex items-baseline gap-2 border-b border-stone-100 py-1 font-mono dsh-body last:border-b-0"
    >
      <span className={`w-40 flex-none truncate font-medium ${repo?.color ?? "text-stone-400"}`} title={entry.repo}>
        {repo?.label ?? entry.repo}
      </span>
      <span
        className={`w-24 flex-none font-semibold ${isWrite ? "text-emerald-700" : "text-stone-700"}`}
      >
        {entry.verb}
      </span>
      <span className="flex-1 truncate text-stone-600" title={entry.summary ?? ""}>
        {entry.summary ?? "—"}
      </span>
      <span className={`flex-none ${failed ? "text-rose-600" : "text-stone-400"}`}>
        {outcomeLabel(entry)}
      </span>
    </div>
  );
}

function outcomeLabel(entry: SandboxCommandEntry): string {
  if (entry.exitCode === null) return "running…";
  const dur = entry.durationMs !== null ? ` · ${formatMs(entry.durationMs)}` : "";
  if (entry.exitCode === 0) return `ok${dur}`;
  if (entry.exitCode === -1) return `not run${dur}`;
  return `exit ${entry.exitCode}${dur}`;
}

function formatTokens(input: number | null, output: number | null): string {
  if (input === null || output === null) return "—";
  return `${input}in/${output}out`;
}

function formatCost(usd: number | null): string {
  if (usd === null) return "—";
  return `$${usd.toFixed(4)}`;
}

function formatMs(ms: number): string {
  if (ms < 1000) return `${ms}ms`;
  return `${(ms / 1000).toFixed(1)}s`;
}
