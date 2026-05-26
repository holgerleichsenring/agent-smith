import type { RunMeta } from "@/lib/api";
import { StatusBadge } from "./StatusBadge";
import { RepoModeBadge } from "./RepoModeBadge";

export function TopologyHeader({ meta }: { meta: RunMeta }) {
  const summary = `${describeRepoMode(meta.repoMode, meta.repos.length)} · ${meta.sandboxCount || 0} sandboxes · ${meta.pipelineName} · ${meta.status}`;
  return (
    <header data-testid="topology-header" className="flex flex-col gap-3 border-b pb-4">
      <div className="flex items-center gap-2">
        <StatusBadge status={meta.status} />
        <RepoModeBadge mode={meta.repoMode} repos={meta.repos} />
        <span className="text-xs text-stone-500">{meta.pipelineName}</span>
      </div>
      <h1 className="font-mono text-sm">{meta.runId}</h1>
      <p data-testid="topology-summary" className="text-sm">
        {summary}
      </p>
      {meta.repos.length > 0 && (
        <ul className="flex flex-wrap gap-1 text-xs">
          {meta.repos.map((r) => (
            <li key={r} className="rounded-full border border-stone-300 px-2 py-0.5">
              {r}
            </li>
          ))}
        </ul>
      )}
    </header>
  );
}

function describeRepoMode(mode: string, count: number): string {
  if (mode === "multi") return `multi-repo (${count})`;
  if (mode === "mono") return "mono-repo";
  return mode;
}
