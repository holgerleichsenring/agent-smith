"use client";

import type { ReactNode } from "react";
import { useDateLocale, useT } from "@/hooks/useT";
import { Badge } from "@/components/ui/Badge";
import { Card } from "@/components/ui/Card";
import { SectionLabel } from "@/components/ui/SectionLabel";
import { ProvenanceBadge } from "./ProvenanceBadge";
import { ProjectInitAction } from "./ProjectInitAction";
import { ProjectMemorySection } from "./ProjectMemorySection";
import { ProjectRepos } from "./ProjectRepos";
import type {
  ConfigAgent,
  ConfigProject,
  ConfigRepo,
  ConfigTracker,
  ResolvedValue,
  ResourceSummary,
} from "@/lib/configApi";

// p0271: the dense, hierarchical detail SHEET for one selected project — the
// replacement for the topology graph. Everything the operator needs about THIS
// project in labelled sections: agent, repositories (with full URLs), tracker
// (with how-it-tracks config), and the resolved effective settings (each with a
// quiet provenance hint). Primitives only (Card/SectionLabel/Badge), dsh-* tokens.

interface Props {
  project: ConfigProject;
  repos: ConfigRepo[];
  trackers: ConfigTracker[];
  agents: ConfigAgent[];
}

export function ProjectDetailPanel({ project, repos, trackers, agents }: Props) {
  const agent = agents.find((a) => a.name === project.agentName) ?? null;
  const tracker = trackers.find((t) => t.name === project.trackerName) ?? null;
  const projectRepos = project.repoNames
    .map((n) => repos.find((r) => r.name === n))
    .filter((r): r is ConfigRepo => r !== undefined);

  return (
    // p0343d: sits inside the parity page's .main — no extra shell padding.
    <div data-testid="project-detail-panel">
      <Card className="px-5 py-4">
        <Header project={project} />
        <AgentSection name={project.agentName} agent={agent} />
        <ReposSection project={project.name} repos={projectRepos} />
        <TrackerSection name={project.trackerName} tracker={tracker} trigger={project.trigger} />
        <ResolvedSection resolved={project.resolved} />
        <ProjectMemorySection project={project.name} />
      </Card>
    </div>
  );
}

function Header({ project }: { project: ConfigProject }) {
  return (
    <header className="border-b border-stone-200 pb-3">
      <div className="flex items-start justify-between gap-3">
        <div>
          <div className="dsh-h3 font-semibold text-stone-800">{project.name}</div>
          <div className="mt-1 flex flex-wrap gap-1.5">
            {project.pipelines.map((p) => (
              <Badge key={p} tone="neutral">
                {p}
              </Badge>
            ))}
          </div>
        </div>
        {/* p0489: start init-project for this project — no ticket anywhere. */}
        <ProjectInitAction project={project.name} />
      </div>
    </header>
  );
}

function AgentSection({ name, agent }: { name: string; agent: ConfigAgent | null }) {
  const t = useT();
  return (
    <Section title={t("system.project.agent")} testId="agent-section">
      <KvRow label={t("system.project.name")} value={name} />
      {agent && (
        <>
          <KvRow label={t("system.project.model")} value={agent.model} />
          <KvRow label={t("system.project.type")} value={agent.type} />
          <KvRow label={t("system.project.networkTimeout")} value={`${agent.networkTimeoutSeconds}s`} />
          <KvRow label={t("system.project.maxFixIterations")} value={agent.maxFixIterations} />
          <KvRow label={t("system.project.runsAtATime")} value={agent.maxConcurrentRuns ?? t("system.project.noLimit")} />
          {agent.requestsPerMinute != null && (
            <KvRow label={t("system.project.rateLimit")} value={t("system.project.perMinute", { count: agent.requestsPerMinute })} />
          )}
        </>
      )}
    </Section>
  );
}

function ReposSection({ project, repos }: { project: string; repos: ConfigRepo[] }) {
  const t = useT();
  return (
    <Section title={t("system.project.repositories")} testId="repos-section">
      <ProjectRepos project={project} repos={repos} />
    </Section>
  );
}

function TrackerSection({
  name, tracker, trigger,
}: {
  name: string;
  tracker: ConfigTracker | null;
  trigger: ConfigProject["trigger"];
}) {
  const t = useT();
  return (
    <Section title={t("system.project.tracker")} testId="trigger-semantics">
      <KvRow label={t("system.project.name")} value={name} />
      {tracker && (
        <>
          <KvRow label={t("system.project.type")} value={tracker.type} />
          {tracker.url && <KvRow label={t("system.project.url")} value={tracker.url} mono />}
          {tracker.project && <KvRow label={t("system.project.project")} value={tracker.project} />}
        </>
      )}
      <div className="mt-2 border-t border-stone-100 pt-2">
        <TriggerRow role={t("system.project.triggersOn")}>
          {trigger.triggerStatuses.length > 0 ? (
            <span className="flex flex-wrap justify-end gap-1.5">
              {trigger.triggerStatuses.map((s) => (
                <Badge key={s} tone="neutral">{s}</Badge>
              ))}
            </span>
          ) : (
            <Dash />
          )}
        </TriggerRow>
        <TriggerRow role={t("system.project.done")}>
          {trigger.doneStatus ? <Badge tone="green">{trigger.doneStatus}</Badge> : <Dash />}
        </TriggerRow>
        <TriggerRow role={t("system.project.failed")}>
          {trigger.failedStatus ? <Badge tone="rose">{trigger.failedStatus}</Badge> : <Dash />}
        </TriggerRow>
        {/* 2026-10-08-101b: a tracker entry polls or takes webhooks, never both. */}
        <TriggerRow role={t("system.project.mode")}>
          <span className="dsh-mono text-stone-700">
            {trigger.pollingEnabled
              ? t("system.project.polling", { seconds: trigger.pollingIntervalSeconds })
              : t("system.project.webhooks")}
          </span>
        </TriggerRow>
        <TriggerRow role={t("system.project.commentTrigger")}>
          {trigger.commentKeyword ? (
            <span className="dsh-mono text-stone-700">{trigger.commentKeyword}</span>
          ) : (
            <Dash />
          )}
        </TriggerRow>
      </div>
    </Section>
  );
}

function ResolvedSection({ resolved }: { resolved: ConfigProject["resolved"] }) {
  const t = useT();
  const locale = useDateLocale();
  return (
    <Section title={t("system.project.resolvedSettings")} testId="resolved-settings">
      <dl className="divide-y divide-stone-100">
        <ResolvedRow testId="resolved-step-timeout" label={t("system.project.stepTimeout")} rv={resolved.stepTimeoutSeconds} format={(v) => `${v}s`} />
        <ResolvedRow testId="resolved-run-command-timeout" label={t("system.project.runCommandTimeout")} rv={resolved.runCommandTimeoutSeconds} format={(v) => `${v}s`} />
        <ResolvedRow testId="resolved-sandbox-resources" label={t("system.project.sandboxResources")} rv={resolved.sandboxResources} format={formatResources} />
        <ResolvedRow testId="resolved-agent-image" label={t("system.project.agentImage")} rv={resolved.agentImage} format={(v) => v} />
        <ResolvedRow testId="resolved-toolchain-image" label={t("system.project.toolchainImage")} rv={resolved.toolchainImage} format={(v) => v} />
        <ResolvedRow testId="resolved-cost-cap" label={t("system.project.costCap")} rv={resolved.costCap} format={(v) => `$${v.usd} · ${t("system.project.tokens", { tokens: v.tokens.toLocaleString(locale) })}`} />
      </dl>
      {resolved.resolutionError && (
        <p className="mt-3 rounded-md border border-rose-200 bg-rose-50 px-3 py-2 dsh-body text-rose-700" data-testid="resolution-error">
          {resolved.resolutionError}
        </p>
      )}
    </Section>
  );
}

function ResolvedRow<T>({
  testId, label, rv, format,
}: {
  testId: string;
  label: string;
  rv: ResolvedValue<T>;
  format: (v: T) => ReactNode;
}) {
  const t = useT();
  const perRun = rv.source === "run-resolved";
  return (
    <div className="flex items-baseline justify-between gap-3 py-1.5" data-testid={testId}>
      <dt className="dsh-label text-stone-400">{label}</dt>
      <dd className="flex items-baseline gap-2 text-right">
        <span className="dsh-mono break-all text-stone-700">
          {perRun || rv.value === null ? (
            <span className="text-stone-400">{t("system.project.perRunResolved")}</span>
          ) : (
            format(rv.value)
          )}
        </span>
        <ProvenanceBadge source={rv.source} />
      </dd>
    </div>
  );
}

function Section({ title, testId, children }: { title: string; testId: string; children: ReactNode }) {
  return (
    <section className="mt-4 border-t border-stone-100 pt-4 first:border-t-0" data-testid={testId}>
      <SectionLabel>{title}</SectionLabel>
      <div className="mt-2">{children}</div>
    </section>
  );
}

function KvRow({ label, value, mono }: { label: string; value: ReactNode; mono?: boolean }) {
  return (
    <div className="flex items-baseline justify-between gap-3 py-1">
      <dt className="dsh-label text-stone-400">{label}</dt>
      <dd className={`${mono ? "dsh-mono break-all" : "dsh-body"} text-right text-stone-700`}>{value}</dd>
    </div>
  );
}

function TriggerRow({ role, children }: { role: string; children: ReactNode }) {
  return (
    <div className="flex items-baseline justify-between gap-3 py-1">
      <dt className="dsh-label text-stone-400">{role}</dt>
      <dd className="flex items-baseline">{children}</dd>
    </div>
  );
}

function Dash() {
  return <span className="dsh-mono text-stone-400">—</span>;
}

function formatResources(r: ResourceSummary): string {
  return `${r.cpuRequest}–${r.cpuLimit} CPU · ${r.memoryRequest}–${r.memoryLimit}`;
}
