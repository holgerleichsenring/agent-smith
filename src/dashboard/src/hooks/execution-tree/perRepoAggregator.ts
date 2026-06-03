// p0203: groups consecutive per-repo step buckets (same base command, e.g.
// AnalyzeCode/repo-a, AnalyzeCode/repo-b, AnalyzeCode/repo-c) into a single
// synthetic parent bucket carrying the N/M summary + failed-repo names.
// The per-repo blocks become children, collapsed by default in the
// renderer. Only the whitelisted multi-repo command names trigger the
// rollup; everything else passes through untouched.

import type { NodeStatus } from "@/components/execution/TimingGutter";

const MULTI_REPO_COMMANDS: ReadonlySet<string> = new Set([
  "BootstrapCheckCommand",
  "BootstrapGateCommand",
  "BootstrapDispatchCommand",
  "BootstrapDiscoverCommand",
  "BootstrapRoundCommand",
  "LoadCodingPrinciplesCommand",
  "LoadContextCommand",
  "LoadCodeMapCommand",
  "AnalyzeCodeCommand",
  "EnsurePrerequisitesCommand",
  "TestCommand",
  "PersistWorkBranchCommand",
  "SetupRegistryAuthCommand",
]);

export interface BaseStepLike {
  stepName: string;
  status: NodeStatus;
}

export interface RepoRollup {
  baseCommand: string;
  baseDisplay: string;
  okCount: number;
  failCount: number;
  total: number;
  failedRepos: string[];
  summaryText: string;
  tone: "ok" | "warn" | "fail";
}

export function isMultiRepoCommand(baseCommand: string): boolean {
  return MULTI_REPO_COMMANDS.has(baseCommand);
}

export function extractBaseCommand(stepName: string): string {
  const idx = stepName.indexOf(" (");
  return idx === -1 ? stepName : stepName.slice(0, idx).trim();
}

export function extractRepoSuffix(stepName: string): string | null {
  const open = stepName.indexOf(" (");
  const close = stepName.lastIndexOf(")");
  if (open === -1 || close === -1 || close < open) return null;
  const inside = stepName.slice(open + 2, close);
  const comma = inside.indexOf(",");
  return (comma === -1 ? inside : inside.slice(0, comma)).trim() || null;
}

export function buildRepoRollup(
  baseCommand: string,
  baseDisplay: string,
  buckets: ReadonlyArray<{ stepName: string; status: NodeStatus }>,
): RepoRollup {
  let okCount = 0;
  let failCount = 0;
  const failedRepos: string[] = [];
  for (const b of buckets) {
    if (b.status === "ok") okCount += 1;
    if (b.status === "fail") {
      failCount += 1;
      const repo = extractRepoSuffix(b.stepName);
      if (repo) failedRepos.push(repo);
    }
  }
  const total = buckets.length;
  const tone: RepoRollup["tone"] = failCount > 0 ? "fail" : okCount === total ? "ok" : "warn";
  const summaryText = composeSummary(okCount, failCount, total, failedRepos);
  return { baseCommand, baseDisplay, okCount, failCount, total, failedRepos, summaryText, tone };
}

function composeSummary(
  ok: number,
  fail: number,
  total: number,
  failedRepos: ReadonlyArray<string>,
): string {
  if (fail === 0) return `${ok}/${total} repos`;
  const repos = failedRepos.length > 0 ? ` — see repos: ${failedRepos.join(", ")}` : "";
  return `${ok}/${total} ok, ${fail}/${total} failed${repos}`;
}
