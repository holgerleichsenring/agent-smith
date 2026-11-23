"use client";

import { Badge } from "@/components/ui/Badge";
import { useProjectBootstrap } from "@/hooks/useProjectBootstrap";
import type { ConfigRepo } from "@/lib/configApi";
import type { RepoBootstrap } from "@/lib/projectBootstrapApi";
import { useT } from "@/hooks/useT";

// p0271: the project's repositories with their full URLs. 2026-10-13-0b82c: and whether each one's
// default branch is bootstrapped — a command on a pull request of a repository that is not is
// answered instead of run, and this is where a person sees why.

export function ProjectRepos({ project, repos }: { project: string; repos: ConfigRepo[] }) {
  const t = useT();
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
            {r.organization && <span>{t("system.repos.org", { value: r.organization })}</span>}
            {r.project && <span>{t("system.repos.project", { value: r.project })}</span>}
            {r.defaultBranch && <span>{t("system.repos.branch", { value: r.defaultBranch })}</span>}
          </div>
          {bootstrap?.get(r.name) && <BootstrapDetail entry={bootstrap.get(r.name)!} />}
        </div>
      ))}
    </div>
  );
}

function BootstrapBadge({ entry }: { entry: RepoBootstrap }) {
  const t = useT();
  const [tone, label] =
    entry.state === "complete" ? (["green", "system.repos.initialised"] as const)
    : entry.state === "incomplete" ? (["amber", "system.repos.notInitialised"] as const)
    : (["neutral", "system.repos.bootstrapUnknown"] as const);
  return <Badge tone={tone} testId={`repo-bootstrap-${entry.repo}`}>{t(label)}</Badge>;
}

function BootstrapDetail({ entry }: { entry: RepoBootstrap }) {
  const t = useT();
  if (entry.state === "complete") return null;
  const where = entry.branch === "default" ? t("system.repos.defaultBranch") : t("system.repos.namedBranch", { branch: entry.branch });
  return (
    <div className="mt-2 dsh-body text-stone-600" data-testid={`repo-bootstrap-detail-${entry.repo}`}>
      {entry.state === "incomplete" ? (
        <>
          <div>{t("system.repos.on", { where })}</div>
          <ul className="ml-4 list-disc">
            {entry.missing.map((m) => (
              <li key={m}>{m}</li>
            ))}
          </ul>
          <div className="mt-1 text-stone-500">{t("system.repos.runInitialize")}</div>
        </>
      ) : (
        <div>{t("system.repos.couldNotRead", { where, reason: entry.reason ?? "" })}</div>
      )}
    </div>
  );
}
