"use client";

import { Badge } from "@/components/ui/Badge";
import { useProjectBootstrap } from "@/hooks/useProjectBootstrap";
import type { ConfigRepo } from "@/lib/configApi";
import type { RepoBootstrap } from "@/lib/projectBootstrapApi";

// p0271: the project's repositories with their full URLs. 2026-10-13-0b82c: and whether each one's
// default branch is bootstrapped — a command on a pull request of a repository that is not is
// answered instead of run, and this is where a person sees why.

export function ProjectRepos({ project, repos }: { project: string; repos: ConfigRepo[] }) {
  const bootstrap = useProjectBootstrap(project);
  if (repos.length === 0) return <span className="dsh-mono text-stone-400">—</span>;
  return (
    <div className="space-y-3">
      {repos.map((r) => (
        <div key={r.name} data-testid={`repo-${r.name}`} className="rounded-md border border-stone-100 px-3 py-2">
          <div className="flex items-center gap-2">
            <span className="dsh-body font-semibold text-stone-700">{r.name}</span>
            <Badge tone="neutral">{r.type}</Badge>
            {bootstrap?.get(r.name) && <BootstrapBadge entry={bootstrap.get(r.name)!} />}
          </div>
          {r.url && <div className="mt-1 dsh-mono break-all text-stone-600">{r.url}</div>}
          <div className="mt-1 flex flex-wrap gap-x-4 dsh-label text-stone-400">
            {r.organization && <span>org: {r.organization}</span>}
            {r.project && <span>project: {r.project}</span>}
            {r.defaultBranch && <span>branch: {r.defaultBranch}</span>}
          </div>
          {bootstrap?.get(r.name) && <BootstrapDetail entry={bootstrap.get(r.name)!} />}
        </div>
      ))}
    </div>
  );
}

function BootstrapBadge({ entry }: { entry: RepoBootstrap }) {
  const [tone, label] =
    entry.state === "complete" ? (["green", "initialised"] as const)
    : entry.state === "incomplete" ? (["amber", "not initialised"] as const)
    : (["neutral", "bootstrap unknown"] as const);
  return <Badge tone={tone} testId={`repo-bootstrap-${entry.repo}`}>{label}</Badge>;
}

function BootstrapDetail({ entry }: { entry: RepoBootstrap }) {
  if (entry.state === "complete") return null;
  const where = entry.branch === "default" ? "the default branch" : `branch ${entry.branch}`;
  return (
    <div className="mt-2 dsh-body text-stone-600" data-testid={`repo-bootstrap-detail-${entry.repo}`}>
      {entry.state === "incomplete" ? (
        <>
          <div>On {where}:</div>
          <ul className="ml-4 list-disc">
            {entry.missing.map((m) => (
              <li key={m}>{m}</li>
            ))}
          </ul>
          <div className="mt-1 text-stone-500">Run Initialize and merge its pull request; until then a command on this repository is answered, not run.</div>
        </>
      ) : (
        <div>Could not read {where}: {entry.reason}</div>
      )}
    </div>
  );
}
