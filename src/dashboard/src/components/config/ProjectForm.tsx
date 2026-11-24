"use client";

import { useState, type ReactNode } from "react";
import type {
  ConfigCapabilities,
  ConfigFinding,
  InheritedSandboxProjection,
  StudioProject,
  StudioTracker,
} from "@/lib/configApi";
import { cn } from "@/lib/utils";
import { RefSelect, MultiRefSelect, SelectField, TextField } from "./formFields";
import { RepoPicker } from "./RepoPicker";
import { TemplateBindings } from "./TemplateBindings";
import { TrackerRouting } from "./TrackerRouting";
import { ProjectSandboxSection } from "./ProjectSandboxSection";
import { ProjectMergeSection } from "./ProjectMergeSection";
import { camelCase } from "./capabilityFields";
import { projectIntegrity, unfinishedTemplates } from "./integrity";
import type { ConfigCatalog } from "./useConfigCatalog";
import { useT } from "@/hooks/useT";
import type { T, TextKey } from "@/lib/i18n/translate";

// 2026-09-16-74a2: the project form was one scroll of eleven controls, three of which
// were about pipelines and none of which said what it decided. It is five tabbed
// sections now, and the pipeline section keeps ONE control — the tracker's label map is
// rendered read-only beside it, because that is what actually routes a ticket.
//
// The wiring PREVIEW is gone from the drawer (it cannot breathe in 560px; 2026-09-16-942a
// draws it on the card, where the page gives it about 900). The drawer keeps the
// one-line verdict, which is the half that gates Save.

// 2026-09-22-6968: the sixth tab. Six sandbox controls appended to an existing section
// would rebuild the one-scroll shape 2026-09-16-74a2 removed, and they belong to none of
// identity, repos, pipeline, templates or routing.
// 2026-11-17-b34eb: the seventh, "merge" — when a ticket starts behind another and what happens to a green run's
// pull requests. Both are about delivery, which none of the six sections is.
const TABS = ["identity", "repos", "pipeline", "templates", "routing", "sandbox", "merge"] as const;
export type ProjectTab = (typeof TABS)[number];

const TAB_TITLE = {
  identity: "config.project.tab.identity",
  repos: "config.project.tab.repos",
  pipeline: "config.project.tab.pipeline",
  templates: "config.project.tab.templates",
  routing: "config.project.tab.routing",
  sandbox: "config.project.tab.sandbox",
  merge: "config.project.tab.merge",
} as const satisfies Record<ProjectTab, TextKey>;

// Per-strategy value hints for the routing section. The STRATEGY LIST itself comes from
// capabilities; these are only human placeholders for the strategies the product ships
// (an unknown strategy falls back to a generic hint and stays selectable).
const RESOLUTION_HINTS: Record<string, { placeholder: TextKey; help: TextKey }> = {
  tag: { placeholder: "config.resolution.tag.placeholder", help: "config.resolution.tag.help" },
  area_path: { placeholder: "config.resolution.areaPath.placeholder", help: "config.resolution.areaPath.help" },
  repo: { placeholder: "config.resolution.repo.placeholder", help: "config.resolution.repo.help" },
  to_address: { placeholder: "config.resolution.toAddress.placeholder", help: "config.resolution.toAddress.help" },
};

export function ProjectForm({
  project,
  onChange,
  catalog,
  capabilities,
  inheritedSandbox = null,
  findings,
  idField,
}: {
  project: StudioProject;
  onChange: (next: StudioProject) => void;
  catalog: ConfigCatalog;
  capabilities: ConfigCapabilities | null;
  /** 2026-09-22-6968: what each sandbox control would inherit if this project said
   *  nothing. Null when the projection could not be read. */
  inheritedSandbox?: InheritedSandboxProjection | null;
  findings: ConfigFinding[];
  idField: ReactNode;
}) {
  const t = useT();
  const [tab, setTab] = useState<ProjectTab>("identity");
  // p0345b: a project's repo refs come in two forms — plain catalog ids (toggled from
  // the repos catalog) and connection-scoped discovery refs "conn/RepoName" (managed by
  // the RepoPicker). Both live in the one `repos` array; the split here is presentational.
  const plainRefs = project.repos.filter((r) => !r.includes("/"));
  const connRefs = project.repos.filter((r) => r.includes("/"));
  const tracker = catalog.trackers.find((entry) => entry.id === project.tracker) as StudioTracker | undefined;
  const marked = markedTabs(findings, project);

  return (
    <div className="flex flex-col gap-4">
      <div className="dtabs" role="tablist" aria-label={t("config.project.sections")}>
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
            {t(TAB_TITLE[key])}
            {marked.has(key) && <span className="dtab-mark" aria-label={t("config.needsAttention")}>▲</span>}
          </button>
        ))}
      </div>

      <div className="flex flex-col gap-4" data-testid={`form-section-${tab}`} role="tabpanel">
        {tab === "identity" && (
          <>
            {idField}
            <RefSelect
              label={t("config.project.agent")}
              value={project.agent}
              options={catalog.agents}
              testId="form-ref-agent"
              onChange={(v) => onChange({ ...project, agent: v })}
            />
            <RefSelect
              label={t("config.project.tracker")}
              value={project.tracker}
              options={catalog.trackers}
              testId="form-ref-tracker"
              onChange={(v) => onChange({ ...project, tracker: v })}
            />
            {/* 2026-10-01-7f7aa: drawn only when the catalog has a design source to offer. */}
            {catalog["design-sources"].length > 0 && (
              <MultiRefSelect
                label={t("config.project.designSources")}
                values={project.designSources ?? []}
                options={catalog["design-sources"]}
                testId="form-ref-designSources"
                onChange={(v) => onChange({ ...project, designSources: v })}
              />
            )}
          </>
        )}

        {tab === "repos" && (
          <>
            {/* A picker with nothing to offer is not drawn: "pick from the catalog /
                no entries in catalog" rendered even when every repository came from a
                connection, which is the normal shape. It returns with the first entry. */}
            {catalog.repos.length > 0 && (
              <MultiRefSelect
                label={t("config.project.repos")}
                values={plainRefs}
                options={catalog.repos}
                testId="form-ref-repos"
                onChange={(v) => onChange({ ...project, repos: [...v, ...connRefs] })}
              />
            )}
            <RepoPicker
              label={t("config.project.connectionRepos")}
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
              label={t("config.project.defaultPipeline")}
              value={project.defaultPipeline ?? ""}
              options={capabilities?.pipelines ?? []}
              placeholder={t("config.none")}
              help={pipelineHelp(t, project.defaultPipeline ?? "", capabilities)}
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

        {tab === "sandbox" && (
          <ProjectSandboxSection
            project={project}
            onChange={onChange}
            inheritedSandbox={inheritedSandbox}
          />
        )}

        {tab === "merge" && <ProjectMergeSection project={project} onChange={onChange} capabilities={capabilities} secrets={catalog.secrets} />}
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
  const t = useT();
  const hint = project.resolution ? RESOLUTION_HINTS[project.resolution.strategy] : undefined;
  return (
    <>
      <SelectField
        label={t("config.resolution.strategy.label")}
        value={project.resolution?.strategy ?? ""}
        options={capabilities?.resolutionStrategies ?? []}
        placeholder={t("config.none")}
        help={t("config.resolution.strategy.help")}
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
          label={t("config.resolution.value.label")}
          value={project.resolution.value}
          mono
          placeholder={hint ? t(hint.placeholder) : t("config.resolution.value.placeholder")}
          help={hint ? t(hint.help) : t("config.resolution.value.help", { strategy: project.resolution.strategy })}
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

// p0393/p0392: `pipelines` is what the studio may OFFER; a stored configuration may still
// carry a retired name. SelectField keeps an unlisted current value selectable, and this
// labels it for what it is rather than silently rewriting it. 2026-09-25-e5b1: such a name
// no longer runs — the alias map that made it resolve is gone — so the sentence says so.
function pipelineHelp(t: T, value: string, capabilities: ConfigCapabilities | null): string {
  if (!capabilities) return t("config.capabilitiesUnavailable");
  if (value && !capabilities.pipelines.includes(value)) return t("config.project.pipeline.retired", { value });
  return t("config.project.pipeline.help");
}

// p0392: what the server would say about this project, before it is saved. The rules run
// on the server (ConfigDraftRules); this only renders the answer. A finding that names a
// field is ALSO shown on that field — the summary here is for the ones that name the unit.
function DraftFindings({ findings }: { findings: ConfigFinding[] }) {
  const t = useT();
  if (findings.length === 0) return null;
  return (
    <div className="field" data-testid="form-draft-findings">
      <label>
        {t("config.project.findings.label")} <span className="help">{t("config.project.findings.help")}</span>
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
  const t = useT();
  const integrity = projectIntegrity(catalog, project);
  const broken = integrity.repoResults.filter((r) => !r.ok).map((r) => r.id);
  const missing: string[] = [];
  if (!integrity.agentOk) missing.push(t("config.project.agent"));
  if (!integrity.trackerOk) missing.push(t("config.project.tracker"));
  if (!integrity.reposOk) missing.push(t("config.project.verdict.oneRepo"));
  if (broken.length) missing.push(t("config.project.verdict.unknownRepo", { refs: broken.join(", ") }));
  return (
    <div
      data-testid="project-integrity"
      data-ok={integrity.ok ? "true" : "false"}
      className={cn("integrity", !integrity.ok && "warn")}
    >
      <span className="ii">{integrity.ok ? "✓" : "▲"}</span>
      <div>
        {integrity.ok
          ? t("config.project.verdict.ok")
          : t("config.project.verdict.unresolved", { missing: missing.join(", ") })}
      </div>
    </div>
  );
}
