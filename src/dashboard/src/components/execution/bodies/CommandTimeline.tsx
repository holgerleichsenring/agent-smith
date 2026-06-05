"use client";

import { useState } from "react";
import type { SandboxCommandEntry } from "@/hooks/execution-tree/buckets";

// p0228: the step's actions in the order the agent ran them — one row per
// command (ReadFile, Grep, find, ListFiles, WriteFile, git, …) with its
// target and outcome. Makes "what did the LLM actually do / what did it search
// for" answerable at a glance. A run that only ever reads + greps and never
// WriteFiles a source file (the 0-changes case) is obvious here.

interface CommandTimelineProps {
  commands: SandboxCommandEntry[];
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

export function CommandTimeline({ commands, defaultCap = 12 }: CommandTimelineProps) {
  const [showAll, setShowAll] = useState(false);
  if (commands.length === 0) return null;

  const writes = commands.filter((c) => WRITE_VERBS.has(c.verb)).length;
  const repoDisplay = buildRepoDisplay(commands);
  const shown = showAll ? commands : commands.slice(0, defaultCap);
  const overCap = commands.length > defaultCap;

  return (
    <div data-testid="command-timeline" className="space-y-1">
      <div className="flex items-center gap-2 dsh-body text-stone-500">
        <span data-testid="command-timeline-summary">
          {commands.length} action{commands.length === 1 ? "" : "s"}
          {" · "}
          <span className={writes > 0 ? "text-emerald-700" : "text-amber-700"}>
            {writes} write{writes === 1 ? "" : "s"}
          </span>
        </span>
      </div>
      <div data-testid="command-timeline-list">
        {shown.map((c, i) => (
          <CommandRow key={`${c.timestamp}-${i}`} entry={c} repo={repoDisplay.get(c.repo)} />
        ))}
      </div>
      {overCap && (
        <button
          type="button"
          data-testid="command-timeline-show-all"
          onClick={() => setShowAll((s) => !s)}
          className="inline-flex items-center gap-1.5 py-1 dsh-body font-medium text-emerald-700 hover:underline"
        >
          {showAll ? "▴ show fewer" : `▾ show all ${commands.length} actions`}
        </button>
      )}
    </div>
  );
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

function formatMs(ms: number): string {
  if (ms < 1000) return `${ms}ms`;
  return `${(ms / 1000).toFixed(1)}s`;
}
