"use client";

import type { StudioProject, TemplateReference } from "@/lib/configApi";
import { isTemplateUnfinished, resolveRepoRef, resolves } from "./integrity";
import type { ConfigCatalog } from "./useConfigCatalog";

// 2026-09-16-942a: what a project is wired to, drawn from what is STORED.
//
// A template does not belong to the project: it belongs to a CONTEXT, and a context
// belongs to a repository. As one row of chips, a project with three repositories and
// three templates is nine equal-looking names — and two repositories declaring the same
// context name are indistinguishable, which is the configuration that silently dropped a
// template before 2026-09-16-4df5 separated them.
//
// The drawing READS NOTHING. Every context it shows comes from the project's own
// templates: a template names its context and, since 4df5, the repository that context
// belongs to. Asking the contexts endpoint would be one authenticated call per repository
// per card, each re-assembling the whole configuration, on a page that lists every
// project — and that hook mounts only inside the open drawer and caches nothing.
//
// Hand-authored SVG, not a graph library: four columns with a known number of rows is a
// layout a component can compute, and a layout engine would be a dependency, a bundle and
// a render loop for a picture whose shape is fixed. Colour is reserved for the thing that
// is wrong — an unfinished binding and a reference that does not resolve — because a
// drawing where everything is coloured says nothing about what needs attention.

const W = 900;
const NH = 30; // a node
const CH = 24; // a context chip nested in its repository
const GAP = 4;
const COL = { leftX: 6, leftW: 168, projX: 206, projW: 140, repoX: 386, repoW: 216, tplX: 646, tplW: 248 };

interface ContextNode {
  key: string;
  context: string;
  target: string;
  unfinished: boolean;
  unresolved: boolean;
}

interface RepoBlock {
  ref: string;
  label: string;
  resolved: boolean;
  contexts: ContextNode[];
  y: number;
  h: number;
}

export function ProjectGraph({
  project,
  catalog,
}: {
  project: StudioProject;
  catalog: ConfigCatalog;
}) {
  const blocks = layout(project, catalog);
  const height = Math.max(
    blocks.length > 0 ? blocks[blocks.length - 1].y + blocks[blocks.length - 1].h + 10 : 0,
    2 * NH + 3 * 10,
  );
  const midY = height / 2;
  const agentY = midY - NH - 6;
  const trackerY = midY + 6;
  const projY = midY - NH / 2;
  const arrow = `arrow-${project.id}`;
  const claim =
    `How ${project.id} is wired: its agent and tracker, its ${blocks.length} ` +
    `${blocks.length === 1 ? "repository" : "repositories"}, the contexts its own templates ` +
    `name inside each one, and the template each of those contexts is built after.`;

  return (
    <figure className="pgraph" data-testid={`config-card-graph-${project.id}`}>
      <svg
        role="img"
        aria-label={claim}
        viewBox={`0 0 ${W} ${height}`}
        style={{ maxWidth: "100%", height: "auto" }}
      >
        <defs>
          <marker id={arrow} viewBox="0 0 8 8" refX="7" refY="4" markerWidth="6" markerHeight="6" orient="auto">
            <path d="M 0 0 L 8 4 L 0 8 z" fill="currentColor" />
          </marker>
        </defs>

        <Node
          x={COL.leftX} y={agentY} w={COL.leftW} h={NH}
          label={project.agent || "no agent"} caption="agent"
          bad={!resolves(catalog, "agents", project.agent)}
          testId={`graph-node-agent-${project.id}`}
        />
        <Node
          x={COL.leftX} y={trackerY} w={COL.leftW} h={NH}
          label={project.tracker || "no tracker"} caption="tracker"
          bad={!resolves(catalog, "trackers", project.tracker)}
          testId={`graph-node-tracker-${project.id}`}
        />
        <Node
          x={COL.projX} y={projY} w={COL.projW} h={NH}
          label={project.id} caption="project"
          testId={`graph-node-project-${project.id}`}
        />

        <Edge from={[COL.leftX + COL.leftW, agentY + NH / 2]} to={[COL.projX, projY + NH / 2]} arrow={arrow} />
        <Edge from={[COL.leftX + COL.leftW, trackerY + NH / 2]} to={[COL.projX, projY + NH / 2]} arrow={arrow} />

        {blocks.map((block) => (
          <g key={block.ref} data-testid={`graph-block-${project.id}-${block.ref}`}>
            <Edge
              from={[COL.projX + COL.projW, projY + NH / 2]}
              to={[COL.repoX, block.y + 8 + NH / 2]}
              arrow={arrow}
            />
            <rect
              x={COL.repoX} y={block.y} width={COL.repoW} height={block.h} rx={9}
              fill="none" stroke="currentColor" strokeOpacity={0.35}
            />
            <Node
              x={COL.repoX + 8} y={block.y + 8} w={COL.repoW - 16} h={NH}
              label={block.label} caption="repository"
              bad={!block.resolved}
              testId={`graph-node-repo-${project.id}-${block.ref}`}
            />
            {block.contexts.map((ctx, i) => {
              const y = block.y + 8 + NH + GAP + i * (CH + GAP);
              return (
                <g key={ctx.key}>
                  <Node
                    x={COL.repoX + 20} y={y} w={COL.repoW - 28} h={CH}
                    label={ctx.context || "no context"} caption="context"
                    bad={ctx.unfinished}
                    testId={`graph-node-context-${project.id}-${ctx.key}`}
                  />
                  <Edge
                    from={[COL.repoX + COL.repoW, y + CH / 2]}
                    to={[COL.tplX, y + CH / 2]}
                    arrow={arrow}
                  />
                  <Node
                    x={COL.tplX} y={y} w={COL.tplW} h={CH}
                    label={ctx.target} caption="built after"
                    bad={ctx.unfinished || ctx.unresolved}
                    testId={`graph-node-template-${project.id}-${ctx.key}`}
                  />
                </g>
              );
            })}
          </g>
        ))}
      </svg>
      <figcaption className="pgraph-cap">{claim}</figcaption>
    </figure>
  );
}

/**
 * A repository block per declared repo ref, with the contexts THIS project's templates
 * name inside it. A repository no template names is drawn without any — that is a fact
 * about the configuration, not a gap in the drawing.
 *
 * A template whose contextRepo is unset names "wherever that name is" (4df5 asks for it
 * only when two repos declare one context name), so with a single repo it is unambiguous
 * and lands there. With several it lands in a block of its own that says the repository is
 * not named — uncoloured, because an unstated repo is allowed; what IS coloured is a
 * contextRepo naming a repository this project does not declare.
 */
function layout(project: StudioProject, catalog: ConfigCatalog): RepoBlock[] {
  const templates = project.templates ?? [];
  const sole = project.repos.length === 1 ? project.repos[0] : null;
  const home = (t: TemplateReference) => t.contextRepo || sole || "";
  const declared = new Set(project.repos);

  const blocks: Omit<RepoBlock, "y" | "h">[] = project.repos.map((ref) => ({
    ref,
    label: ref,
    resolved: resolveRepoRef(catalog, ref).ok,
    contexts: templates.filter((t) => home(t) === ref).map((t, i) => node(t, i, catalog)),
  }));

  const strays = templates.filter((t) => !declared.has(home(t)));
  if (strays.length > 0) {
    const named = strays.some((t) => !!t.contextRepo);
    blocks.push({
      ref: "—",
      label: named ? "repository not declared here" : "repository not named",
      resolved: !named,
      contexts: strays.map((t, i) => node(t, i, catalog)),
    });
  }

  let y = 10;
  return blocks.map((b) => {
    const h = 16 + NH + b.contexts.length * (CH + GAP);
    const placed = { ...b, y, h };
    y += h + 12;
    return placed;
  });
}

function node(template: TemplateReference, index: number, catalog: ConfigCatalog): ContextNode {
  const unfinished = isTemplateUnfinished(template);
  return {
    key: `${template.contextRepo ?? ""}-${template.context || "unnamed"}-${index}`,
    context: template.context,
    target: `${template.project || "?"} / ${template.repo || "?"} · ${template.templateContext || "?"}`,
    unfinished,
    unresolved: !!template.project && !resolves(catalog, "projects", template.project),
  };
}

function Node({
  x, y, w, h, label, caption, bad, testId,
}: {
  x: number; y: number; w: number; h: number;
  label: string; caption: string; bad?: boolean; testId: string;
}) {
  const stroke = bad ? "var(--bad)" : "currentColor";
  return (
    <g data-testid={testId} data-coloured={bad ? "true" : "false"}>
      <title>{`${caption}: ${label}`}</title>
      <rect x={x} y={y} width={w} height={h} rx={7} fill="none" stroke={stroke} strokeOpacity={bad ? 1 : 0.55} />
      <text x={x + 9} y={y + h / 2 + 1} fontSize={9} fill={stroke} opacity={0.65}>
        {caption}
      </text>
      <text x={x + 9} y={y + h / 2 + 11} fontSize={11} fill={stroke} fontFamily="var(--mono)">
        {label}
      </text>
    </g>
  );
}

/** Orthogonal, dashed, with a small rounded corner where it turns. */
function Edge({
  from, to, arrow,
}: {
  from: [number, number]; to: [number, number]; arrow: string;
}) {
  const [x1, y1] = from;
  const [x2, y2] = to;
  const mid = x1 + (x2 - x1) / 2;
  const r = Math.min(8, Math.abs(y2 - y1) / 2);
  const d =
    Math.abs(y2 - y1) < 1
      ? `M ${x1} ${y1} H ${x2}`
      : `M ${x1} ${y1} H ${mid - r} Q ${mid} ${y1} ${mid} ${y1 + (y2 > y1 ? r : -r)} ` +
        `V ${y2 - (y2 > y1 ? r : -r)} Q ${mid} ${y2} ${mid + r} ${y2} H ${x2}`;
  return (
    <path
      d={d}
      fill="none"
      stroke="currentColor"
      strokeOpacity={0.45}
      strokeDasharray="4 3"
      markerEnd={`url(#${arrow})`}
    />
  );
}
