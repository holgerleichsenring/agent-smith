"use client";

import { useState, type ReactNode } from "react";
import type {
  ConfigCapabilities,
  ConfigFinding,
  StudioProject,
  StudioTracker,
} from "@/lib/configApi";
import { cn } from "@/lib/utils";
import { RefSelect, MultiRefSelect, SelectField, TextField } from "./formFields";
import { RepoPicker } from "./RepoPicker";
import { TemplateBindings } from "./TemplateBindings";
import { TrackerRouting } from "./TrackerRouting";
import { camelCase } from "./capabilityFields";
import { projectIntegrity, unfinishedTemplates } from "./integrity";
import type { ConfigCatalog } from "./useConfigCatalog";

// 2026-09-16-74a2: the project form was one scroll of eleven controls, three of which
// were about pipelines and none of which said what it decided. It is five tabbed
// sections now, and the pipeline section keeps ONE control — the tracker's label map is
// rendered read-only beside it, because that is what actually routes a ticket.
//
// The wiring PREVIEW is gone from the drawer (it cannot breathe in 560px; 2026-09-16-942a
// draws it on the card, where the page gives it about 900). The drawer keeps the
// one-line verdict, which is the half that gates Save.

const TABS = ["identity", "repos", "pipeline", "templates", "routing"] as const;
export type ProjectTab = (typeof TABS)[number];

// Per-strategy value hints for the routing section. The STRATEGY LIST itself comes from
// capabilities; these are only human placeholders for the strategies the product ships
// (an unknown strategy falls back to a generic hint and stays selectable).
const RESOLUTION_HINTS: Record<string, { placeholder: string; help: string }> = {
  tag: { placeholder: "e.g. checkout", help: "ticket tag/label that routes to this project" },
  area_path: { placeholder: "e.g. Product\\Team\\Component", help: "the ticket's area path" },
  repo: { placeholder: "e.g. Sample.Api", help: "a repo name mentioned on the ticket" },
  to_address: { placeholder: "e.g. team@example.com", help: "the inbound email address" },
};

export function ProjectForm({
  project,
  onChange,
  catalog,
  capabilities,
  findings,
  idField,
}: {
  project: StudioProject;
  onChange: (next: StudioProject) => void;
  catalog: ConfigCatalog;
  capabilities: ConfigCapabilities | null;
  findings: ConfigFinding[];
  idField: ReactNode;
}) {
  const [tab, setTab] = useState<ProjectTab>("identity");
  // p0345b: a project's repo refs come in two forms — plain catalog ids (toggled from
  // the repos catalog) and connection-scoped discovery refs "conn/RepoName" (managed by
  // the RepoPicker). Both live in the one `repos` array; the split here is presentational.
  const plainRefs = project.repos.filter((r) => !r.includes("/"));
  const connRefs = project.repos.filter((r) => r.includes("/"));
  const tracker = catalog.trackers.find((t) => t.id === project.tracker) as StudioTracker | undefined;
  const marked = markedTabs(findings, project);

  return (
    <div className="flex flex-col gap-4">
      <div className="dtabs" role="tablist" aria-label="Project sections">
        {TABS.map((key) => (
          <button
            key={key}
            type="button"
            role="tab"
            aria-selected={tab === key}
            className={cn("dtab", tab === key && "on")}
            data-marked={marked.has(key) ? "true" : "false"}
            data-testid={`form-tab-${key}`}
            onClick={() => setTab(key)}
          >
            {key}
            {marked.has(key) && <span className="dtab-mark" aria-label="needs attention">▲</span>}
          </button>
        ))}
      </div>

      <div className="flex flex-col gap-4" data-testid={`form-section-${tab}`} role="tabpanel">
        {tab === "identity" && (
          <>
            {idField}
            <RefSelect
              label="agent"
              value={project.agent}
              options={catalog.agents}
              testId="form-ref-agent"
              onChange={(v) => onChange({ ...project, agent: v })}
            />
            <RefSelect
              label="tracker"
              value={project.tracker}
              options={catalog.trackers}
              testId="form-ref-tracker"
              onChange={(v) => onChange({ ...project, tracker: v })}
            />
          </>
        )}

        {tab === "repos" && (
          <>
            {/* A picker with nothing to offer is not drawn: "pick from the catalog /
                no entries in catalog" rendered even when every repository came from a
                connection, which is the normal shape. It returns with the first entry. */}
            {catalog.repos.length > 0 && (
              <MultiRefSelect
                label="repos"
                values={plainRefs}
                options={catalog.repos}
                testId="form-ref-repos"
                onChange={(v) => onChange({ ...project, repos: [...v, ...connRefs] })}
              />
            )}
            <RepoPicker
              label="connection-scoped repos"
              values={connRefs}
              connections={catalog.connections}
              testId="form-connref"
              onChange={(v) => onChange({ ...project, repos: [...plainRefs, ...v] })}
            />
          </>
        )}

        {tab === "pipeline" && (
          <>
            <SelectField
              label="default pipeline"
              value={project.defaultPipeline ?? ""}
              options={capabilities?.pipelines ?? []}
              placeholder="— none —"
              help={pipelineHelp(project.defaultPipeline ?? "", capabilities)}
              testId="form-field-defaultPipeline"
              onChange={(v) =>
                onChange({ ...project, defaultPipeline: v === "" ? undefined : v })
              }
            />
            <TrackerRouting tracker={tracker} trackerId={project.tracker} />
          </>
        )}

        {tab === "templates" && (
          /* 2026-09-14-620e: templates are PICKED, not typed. The field writes
             `templates` only when the operator touches it — absent means "nothing to
             say", which is what keeps a client that never renders this field from
             wiping a stored declaration. */
          <TemplateBindings
            project={project}
            catalog={catalog}
            onChange={(templates) => onChange({ ...project, templates })}
          />
        )}

        {tab === "routing" && <RoutingSection project={project} onChange={onChange} capabilities={capabilities} />}
      </div>

      <DraftFindings findings={findings} />
      <ProjectVerdict project={project} catalog={catalog} />
    </div>
  );
}

function RoutingSection({
  project,
  onChange,
  capabilities,
}: {
  project: StudioProject;
  onChange: (next: StudioProject) => void;
  capabilities: ConfigCapabilities | null;
}) {
  const hint = project.resolution ? RESOLUTION_HINTS[project.resolution.strategy] : undefined;
  return (
    <>
      <SelectField
        label="resolution strategy"
        value={project.resolution?.strategy ?? ""}
        options={capabilities?.resolutionStrategies ?? []}
        placeholder="— none —"
        help="how a ticket resolves to this project"
        testId="form-field-resolution-strategy"
        onChange={(v) =>
          onChange({
            ...project,
            resolution: v ? { strategy: v, value: project.resolution?.value ?? "" } : null,
          })
        }
      />
      {project.resolution && (
        <TextField
          label="resolution value"
          value={project.resolution.value}
          mono
          placeholder={hint?.placeholder ?? "match value for this strategy"}
          help={hint?.help ?? `value the ${project.resolution.strategy} strategy matches on`}
          testId="form-field-resolution-value"
          onChange={(v) => onChange({ ...project, resolution: { ...project.resolution!, value: v } })}
        />
      )}
    </>
  );
}

/**
 * Which tab a finding sends the operator to. Only two can be marked today and the spec
 * says which: a project draft's findings name default_pipeline, templates, and six
 * TRACKER-owned status fields this form renders no input for — marking a tab for one of
 * those would point at a section that cannot fix it. An unfinished template binding is
 * added because it blocks the save, and it lives on the same tab a finding would mark.
 */
function markedTabs(findings: ConfigFinding[], project: StudioProject): Set<ProjectTab> {
  const marked = new Set<ProjectTab>();
  for (const f of findings) {
    if (!f.field) continue;
    const key = camelCase(f.field);
    if (key === "defaultPipeline") marked.add("pipeline");
    if (key === "templates") marked.add("templates");
  }
  if (unfinishedTemplates(project).length > 0) marked.add("templates");
  return marked;
}

// p0393/p0392: `pipelines` is what the studio may OFFER; a stored configuration may carry
// a retired alias. SelectField keeps an unlisted current value selectable, and this
// labels it for what it is rather than silently rewriting it.
function pipelineHelp(value: string, capabilities: ConfigCapabilities | null): string {
  if (!capabilities) return "capabilities unavailable";
  if (value && !capabilities.pipelines.includes(value))
    return `'${value}' is a retired name — it still runs, but pick a current one to replace it`;
  return "which pipeline definition this project runs; what a TICKET routes to is the tracker's, below";
}

// p0392: what the server would say about this project, before it is saved. The rules run
// on the server (ConfigDraftRules); this only renders the answer. A finding that names a
// field is ALSO shown on that field — the summary here is for the ones that name the unit.
function DraftFindings({ findings }: { findings: ConfigFinding[] }) {
  if (findings.length === 0) return null;
  return (
    <div className="field" data-testid="form-draft-findings">
      <label>
        what the server would report <span className="help">before you save</span>
      </label>
      <ul style={{ margin: 0, paddingLeft: "1.1rem" }}>
        {findings.map((f, i) => (
          <li
            key={`${f.field ?? "unit"}-${i}`}
            data-testid={f.field ? `form-draft-finding-${f.field}` : "form-draft-finding"}
            data-severity={f.severity}
            className="help"
            style={{ color: f.severity === "blocking" ? "var(--bad)" : undefined }}
          >
            {f.reason}
          </li>
        ))}
      </ul>
    </div>
  );
}

/**
 * The drawer's one-line verdict — what the five-node preview's integrity bar always was.
 * It stays outside the tabs because it is about the unit, not a section, and because it
 * is the thing that gates Save.
 */
function ProjectVerdict({ project, catalog }: { project: StudioProject; catalog: ConfigCatalog }) {
  const integrity = projectIntegrity(catalog, project);
  const broken = integrity.repoResults.filter((r) => !r.ok).map((r) => r.id);
  const missing: string[] = [];
  if (!integrity.agentOk) missing.push("agent");
  if (!integrity.trackerOk) missing.push("tracker");
  if (!integrity.reposOk) missing.push("at least one repo");
  if (broken.length) missing.push(`unknown repo/connection ${broken.join(", ")}`);
  return (
    <div
      data-testid="project-integrity"
      data-ok={integrity.ok ? "true" : "false"}
      className={cn("integrity", !integrity.ok && "warn")}
    >
      <span className="ii">{integrity.ok ? "✓" : "▲"}</span>
      <div>
        {integrity.ok
          ? "Every reference resolves to a catalog entry — this project will pass config validation."
          : `Unresolved: ${missing.join(", ")}`}
      </div>
    </div>
  );
}
