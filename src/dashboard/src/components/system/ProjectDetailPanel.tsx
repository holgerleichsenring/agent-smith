import type { ReactNode } from "react";
import { Badge } from "@/components/ui/Badge";
import { Card } from "@/components/ui/Card";
import { SectionLabel } from "@/components/ui/SectionLabel";
import { ProvenanceBadge } from "./ProvenanceBadge";
import type {
  ConfigProject,
  ResolvedValue,
  ResourceSummary,
} from "@/lib/configApi";

// p0270b: the read-only EXPLAINER for one selected project. The static catalog
// boxes are replaced by the project's RESOLVED effective settings — each value
// untruncated and stamped with a provenance badge (default vs override vs per
// run) — plus the tracker trigger semantics labelled BY ROLE (what triggers a
// run, what marks it done, what marks it failed, whether polling is on).
// Primitives only (Card / SectionLabel / Badge), dsh-* tokens, no raw px.

export function ProjectDetailPanel({ project }: { project: ConfigProject }) {
  const { resolved, trigger } = project;
  return (
    <div className="content-shell" data-testid="project-detail-panel">
      <Card className="px-5 py-4">
        <Header project={project} />
        <ResolvedSection resolved={resolved} />
        <TriggerSection trigger={trigger} />
      </Card>
    </div>
  );
}

function Header({ project }: { project: ConfigProject }) {
  return (
    <header className="border-b border-stone-200 pb-3">
      <div className="dsh-h3 font-semibold text-stone-800">{project.name}</div>
      <div className="mt-1 dsh-mono text-stone-500">{project.pipeline}</div>
      <div className="mt-2 flex flex-wrap items-center gap-1.5">
        <Badge tone="neutral">agent: {project.agentName}</Badge>
        <Badge tone="neutral">tracker: {project.trackerName}</Badge>
        {project.repoNames.map((r) => (
          <Badge key={r} tone="amber">
            repo: {r}
          </Badge>
        ))}
      </div>
    </header>
  );
}

function ResolvedSection({ resolved }: { resolved: ConfigProject["resolved"] }) {
  return (
    <section className="mt-4" data-testid="resolved-settings">
      <SectionLabel>Resolved settings</SectionLabel>
      <dl className="mt-2 divide-y divide-stone-100">
        <ResolvedRow
          testId="resolved-step-timeout"
          label="step timeout"
          rv={resolved.stepTimeoutSeconds}
          format={(v) => `${v}s`}
        />
        <ResolvedRow
          testId="resolved-run-command-timeout"
          label="run_command timeout"
          rv={resolved.runCommandTimeoutSeconds}
          format={(v) => `${v}s`}
        />
        <ResolvedRow
          testId="resolved-sandbox-resources"
          label="sandbox resources"
          rv={resolved.sandboxResources}
          format={formatResources}
        />
        <ResolvedRow
          testId="resolved-agent-image"
          label="agent image"
          rv={resolved.agentImage}
          format={(v) => v}
        />
        <ResolvedRow
          testId="resolved-orchestrator-image"
          label="orchestrator image"
          rv={resolved.orchestratorImage}
          format={(v) => v}
        />
        <ResolvedRow
          testId="resolved-toolchain-image"
          label="toolchain image"
          rv={resolved.toolchainImage}
          format={(v) => v}
        />
        <ResolvedRow
          testId="resolved-cost-cap"
          label="cost cap / run"
          rv={resolved.costCap}
          format={(v) => `$${v.usd} · ${v.tokens.toLocaleString()} tok`}
        />
      </dl>
      {resolved.resolutionError && (
        <p className="mt-3 rounded-md border border-rose-200 bg-rose-50 px-3 py-2 dsh-body text-rose-700" data-testid="resolution-error">
          {resolved.resolutionError}
        </p>
      )}
    </section>
  );
}

function ResolvedRow<T>({
  testId,
  label,
  rv,
  format,
}: {
  testId: string;
  label: string;
  rv: ResolvedValue<T>;
  format: (v: T) => ReactNode;
}) {
  // A value only knowable per run carries no config-time value — show the
  // literal "resolved per run", never a fabricated one.
  const perRun = rv.source === "run-resolved";
  return (
    <div className="flex items-baseline justify-between gap-3 py-1.5" data-testid={testId}>
      <dt className="dsh-label text-stone-400">{label}</dt>
      <dd className="flex items-baseline gap-2">
        <span className="dsh-mono text-stone-700">
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

function formatResources(r: ResourceSummary): string {
  return `${r.cpuRequest}–${r.cpuLimit} CPU · ${r.memoryRequest}–${r.memoryLimit}`;
}

function TriggerSection({ trigger }: { trigger: ConfigProject["trigger"] }) {
  return (
    <section className="mt-5 border-t border-stone-200 pt-4" data-testid="trigger-semantics">
      <SectionLabel>Trigger</SectionLabel>
      <dl className="mt-2 space-y-1.5">
        <TriggerRow role="triggers on">
          {trigger.triggerStatuses.length > 0 ? (
            <span className="flex flex-wrap gap-1.5">
              {trigger.triggerStatuses.map((s) => (
                <Badge key={s} tone="neutral">
                  {s}
                </Badge>
              ))}
            </span>
          ) : (
            <span className="dsh-mono text-stone-400">—</span>
          )}
        </TriggerRow>
        <TriggerRow role="done">
          {trigger.doneStatus ? (
            <Badge tone="green">{trigger.doneStatus}</Badge>
          ) : (
            <span className="dsh-mono text-stone-400">—</span>
          )}
        </TriggerRow>
        <TriggerRow role="failed">
          {trigger.failedStatus ? (
            <Badge tone="rose">{trigger.failedStatus}</Badge>
          ) : (
            <span className="dsh-mono text-stone-400">—</span>
          )}
        </TriggerRow>
        <TriggerRow role="polling">
          <Badge tone={trigger.pollingEnabled ? "green" : "neutral"}>
            {trigger.pollingEnabled ? "on" : "off"}
          </Badge>
        </TriggerRow>
      </dl>
    </section>
  );
}

function TriggerRow({ role, children }: { role: string; children: ReactNode }) {
  return (
    <div className="flex items-baseline justify-between gap-3">
      <dt className="dsh-label text-stone-400">{role}</dt>
      <dd className="flex items-baseline">{children}</dd>
    </div>
  );
}
