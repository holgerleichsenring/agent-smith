import type { ReactNode } from "react";
import { Badge } from "@/components/ui/Badge";
import { Card } from "@/components/ui/Card";
import { SectionLabel } from "@/components/ui/SectionLabel";
import { ProvenanceBadge } from "./ProvenanceBadge";
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
        <ReposSection repos={projectRepos} />
        <TrackerSection name={project.trackerName} tracker={tracker} trigger={project.trigger} />
        <ResolvedSection resolved={project.resolved} />
      </Card>
    </div>
  );
}

function Header({ project }: { project: ConfigProject }) {
  return (
    <header className="border-b border-stone-200 pb-3">
      <div className="dsh-h3 font-semibold text-stone-800">{project.name}</div>
      <div className="mt-1 flex flex-wrap gap-1.5">
        {project.pipelines.map((p) => (
          <Badge key={p} tone="neutral">
            {p}
          </Badge>
        ))}
      </div>
    </header>
  );
}

function AgentSection({ name, agent }: { name: string; agent: ConfigAgent | null }) {
  return (
    <Section title="Agent" testId="agent-section">
      <KvRow label="name" value={name} />
      {agent && (
        <>
          <KvRow label="model" value={agent.model} />
          <KvRow label="type" value={agent.type} />
          <KvRow label="network timeout" value={`${agent.networkTimeoutSeconds}s`} />
          <KvRow label="max fix iterations" value={agent.maxFixIterations} />
          {agent.requestsPerMinute != null && (
            <KvRow label="rate limit" value={`${agent.requestsPerMinute} req/min`} />
          )}
        </>
      )}
    </Section>
  );
}

function ReposSection({ repos }: { repos: ConfigRepo[] }) {
  return (
    <Section title="Repositories" testId="repos-section">
      {repos.length === 0 && <span className="dsh-mono text-stone-400">—</span>}
      <div className="space-y-3">
        {repos.map((r) => (
          <div key={r.name} data-testid={`repo-${r.name}`} className="rounded-md border border-stone-100 px-3 py-2">
            <div className="flex items-center gap-2">
              <span className="dsh-body font-semibold text-stone-700">{r.name}</span>
              <Badge tone="neutral">{r.type}</Badge>
            </div>
            {r.url && <div className="mt-1 dsh-mono break-all text-stone-600">{r.url}</div>}
            <div className="mt-1 flex flex-wrap gap-x-4 dsh-label text-stone-400">
              {r.organization && <span>org: {r.organization}</span>}
              {r.project && <span>project: {r.project}</span>}
              {r.defaultBranch && <span>branch: {r.defaultBranch}</span>}
            </div>
          </div>
        ))}
      </div>
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
  return (
    <Section title="Tracker" testId="trigger-semantics">
      <KvRow label="name" value={name} />
      {tracker && (
        <>
          <KvRow label="type" value={tracker.type} />
          {tracker.url && <KvRow label="url" value={tracker.url} mono />}
          {tracker.project && <KvRow label="project" value={tracker.project} />}
        </>
      )}
      <div className="mt-2 border-t border-stone-100 pt-2">
        <TriggerRow role="triggers on">
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
        <TriggerRow role="done">
          {trigger.doneStatus ? <Badge tone="green">{trigger.doneStatus}</Badge> : <Dash />}
        </TriggerRow>
        <TriggerRow role="failed">
          {trigger.failedStatus ? <Badge tone="rose">{trigger.failedStatus}</Badge> : <Dash />}
        </TriggerRow>
        <TriggerRow role="polling">
          <span className="dsh-mono text-stone-700">
            {trigger.pollingEnabled ? `every ${trigger.pollingIntervalSeconds}s` : "off"}
          </span>
        </TriggerRow>
        <TriggerRow role="comment trigger">
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
  return (
    <Section title="Resolved settings" testId="resolved-settings">
      <dl className="divide-y divide-stone-100">
        <ResolvedRow testId="resolved-step-timeout" label="step timeout" rv={resolved.stepTimeoutSeconds} format={(v) => `${v}s`} />
        <ResolvedRow testId="resolved-run-command-timeout" label="run_command timeout" rv={resolved.runCommandTimeoutSeconds} format={(v) => `${v}s`} />
        <ResolvedRow testId="resolved-sandbox-resources" label="sandbox resources" rv={resolved.sandboxResources} format={formatResources} />
        <ResolvedRow testId="resolved-agent-image" label="agent image" rv={resolved.agentImage} format={(v) => v} />
        <ResolvedRow testId="resolved-orchestrator-image" label="orchestrator image" rv={resolved.orchestratorImage} format={(v) => v} />
        <ResolvedRow testId="resolved-toolchain-image" label="toolchain image" rv={resolved.toolchainImage} format={(v) => v} />
        <ResolvedRow testId="resolved-cost-cap" label="cost cap / run" rv={resolved.costCap} format={(v) => `$${v.usd} · ${v.tokens.toLocaleString()} tok`} />
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
  const perRun = rv.source === "run-resolved";
  return (
    <div className="flex items-baseline justify-between gap-3 py-1.5" data-testid={testId}>
      <dt className="dsh-label text-stone-400">{label}</dt>
      <dd className="flex items-baseline gap-2 text-right">
        <span className="dsh-mono break-all text-stone-700">
          {perRun || rv.value === null ? (
            <span className="text-stone-400">resolved per run</span>
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
