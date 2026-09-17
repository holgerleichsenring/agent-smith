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
// 2026-09-16-4b41: FIVE columns, from design/mockups/project-card.html. 942a drew the
// contexts nested inside their repository's rectangle, which leaves one of the three
// relationships the drawing exists to show — project to repository — as the only visible
// arrow. A template belongs to a context and a context belongs to a repository: three
// things, two arrows, and they need three columns. The main column is capped at a thousand
// pixels, but an SVG scales, so width was never the constraint — legibility is, which is
// why the column HEADERS are sized for the scaled result rather than for the viewBox.
//
// Hand-authored SVG, not a graph library: five columns with a computed number of rows is
// a layout a component can compute, and a layout engine would be a dependency, a bundle
// and a render loop for a picture whose shape is fixed. Colour is reserved for the thing
// that is wrong — an unfinished binding and a reference that does not resolve — because a
// drawing where everything is coloured says nothing about what needs attention.

const W = 1090;
const ROW_TOP = 44; // below the header rule
const ROW = 92; // one (repository, context) pair
const NODE_MID = 22; // a row's nodes are centred this far below the row's top
/** The agent/tracker stack is 114 units tall; its top must clear the header rule at 25. */
const STACK_MID_MIN = 91;
const STACK_BELOW = 65; // stack bottom, plus a little, measured from its middle
const COL = {
  leftX: 16, leftW: 176, leftH: 46,
  projX: 236, projW: 182, projH: 52,
  repoX: 472, repoW: 196, repoH: 44,
  ctxX: 720, ctxW: 118, ctxH: 40,
  tplX: 882, tplW: 196, tplH: 44,
};
const HEADERS: { key: string; x: number; text: string }[] = [
  { key: "agent", x: COL.leftX, text: "AGENT · TRACKER" },
  { key: "project", x: COL.projX, text: "PROJECT" },
  { key: "repositories", x: COL.repoX, text: "REPOSITORIES" },
  { key: "contexts", x: COL.ctxX, text: "CONTEXTS" },
  { key: "built-after", x: COL.tplX, text: "BUILT AFTER" },
];

interface ContextNode {
  key: string;
  context: string;
  target: string;
  unfinished: boolean;
  unresolved: boolean;
}

interface RepoGroup {
  ref: string;
  label: string;
  resolved: boolean;
  contexts: ContextNode[];
}

export function ProjectGraph({
  project,
  catalog,
}: {
  project: StudioProject;
  catalog: ConfigCatalog;
}) {
  const groups = layout(project, catalog);
  // One ROW per (repository, context) pair, one for a repository no template names, and one
  // per reason a template could not be placed. The height follows that count — 942a's was a
  // sum of block chrome — and the caption counts what the project DECLARES, not rows.
  const rows = groups.reduce((n, g) => n + Math.max(1, g.contexts.length), 0);
  // The left stack is centred on the ROWS' own middle, not on the canvas: the last row
  // carries the drawing's bottom padding, so the two are not the same line and only the
  // first fans symmetrically.
  const midY = Math.max(ROW_TOP + NODE_MID + ((rows - 1) * ROW) / 2, STACK_MID_MIN);
  const height = Math.max(ROW_TOP + rows * ROW, midY + STACK_BELOW);
  const agentY = midY - 57;
  const trackerY = midY + 11;
  const projY = midY - COL.projH / 2;
  const arrow = `arrow-${project.id}`;
  const claim =
    `How ${project.id} is wired: its agent and tracker, its ${project.repos.length} ` +
    `${project.repos.length === 1 ? "repository" : "repositories"}, the contexts its own ` +
    `templates name, and the template each of those is built after.`;

  let row = 0;
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

        {/* The headers say what each column IS. Sized for the SCALED result: the studio's
            main column caps at 1000px, so this viewBox renders at about 0.83 — at the
            reference's 10 units these land near eight pixels, which is not legible. */}
        {HEADERS.map((h) => (
          <text
            key={h.key}
            data-testid={`graph-header-${project.id}-${h.key}`}
            x={h.x}
            y={17}
            fontSize={12}
            fontWeight={600}
            letterSpacing={1.2}
            opacity={0.55}
            fill="currentColor"
          >
            {h.text}
          </text>
        ))}
        <line x1={0} y1={25} x2={W} y2={25} stroke="currentColor" strokeOpacity={0.16} />

        <Node
          x={COL.leftX} y={agentY} w={COL.leftW} h={COL.leftH} fontSize={12.5}
          label={project.agent || "no agent"} caption="agent"
          bad={!resolves(catalog, "agents", project.agent)}
          testId={`graph-node-agent-${project.id}`}
        />
        <Node
          x={COL.leftX} y={trackerY} w={COL.leftW} h={COL.leftH} fontSize={12.5}
          label={project.tracker || "no tracker"} caption="tracker"
          bad={!resolves(catalog, "trackers", project.tracker)}
          testId={`graph-node-tracker-${project.id}`}
        />
        <Node
          x={COL.projX} y={projY} w={COL.projW} h={COL.projH} fontSize={13} labelDy={37}
          accent bold label={project.id} caption="project"
          testId={`graph-node-project-${project.id}`}
        />

        <Edge from={[COL.leftX + COL.leftW, agentY + COL.leftH / 2]} to={[COL.projX, midY]} arrow={arrow} />
        <Edge from={[COL.leftX + COL.leftW, trackerY + COL.leftH / 2]} to={[COL.projX, midY]} arrow={arrow} />

        {groups.map((group) => {
          const span = Math.max(1, group.contexts.length);
          const first = ROW_TOP + row * ROW + NODE_MID;
          const repoY = first + ((span - 1) * ROW) / 2 - COL.repoH / 2;
          const placed = group.contexts.map((ctx, i) => ({ ctx, mid: first + i * ROW }));
          row += span;
          return (
            <g key={group.ref} data-testid={`graph-row-${project.id}-${group.ref}`}>
              <Edge
                from={[COL.projX + COL.projW, midY]}
                to={[COL.repoX, repoY + COL.repoH / 2]}
                arrow={arrow}
              />
              <Node
                x={COL.repoX} y={repoY} w={COL.repoW} h={COL.repoH} fontSize={12.5}
                label={group.label} caption="repository"
                bad={!group.resolved}
                testId={`graph-node-repo-${project.id}-${group.ref}`}
              />
              {placed.map(({ ctx, mid }) => (
                <g key={ctx.key}>
                  <Edge
                    from={[COL.repoX + COL.repoW, repoY + COL.repoH / 2]}
                    to={[COL.ctxX, mid]}
                    arrow={arrow}
                  />
                  <Node
                    x={COL.ctxX} y={mid - COL.ctxH / 2} w={COL.ctxW} h={COL.ctxH}
                    fontSize={12} labelDy={33} captionDy={17} rx={6} faint
                    label={ctx.context || "no context"} caption="context"
                    bad={ctx.unfinished}
                    testId={`graph-node-context-${project.id}-${ctx.key}`}
                  />
                  <Edge
                    from={[COL.ctxX + COL.ctxW, mid]}
                    to={[COL.tplX, mid]}
                    arrow={arrow}
                  />
                  <Node
                    x={COL.tplX} y={mid - COL.tplH / 2} w={COL.tplW} h={COL.tplH} fontSize={12}
                    label={ctx.target} caption="built after"
                    bad={ctx.unfinished || ctx.unresolved}
                    testId={`graph-node-template-${project.id}-${ctx.key}`}
                  />
                </g>
              ))}
            </g>
          );
        })}
      </svg>
      <figcaption className="pgraph-cap">{claim}</figcaption>
    </figure>
  );
}

/**
 * A repository group per declared repo ref, with the contexts THIS project's templates
 * name in it. A repository no template names still gets a row — that is a fact about the
 * configuration, not a gap in the drawing.
 *
 * The picker stores the repository on every pick, so a template whose contextRepo is unset
 * was stored before it did. With a single repo it is unambiguous and lands there. With
 * several it keeps a row that says the repository is not named until it is opened and saved
 * — uncoloured, because an unstated repo is allowed. A contextRepo naming a repository this
 * project does not declare is a different reason, gets its own row, and IS coloured.
 */
function layout(project: StudioProject, catalog: ConfigCatalog): RepoGroup[] {
  const templates = project.templates ?? [];
  const sole = project.repos.length === 1 ? project.repos[0] : null;
  const home = (t: TemplateReference) => t.contextRepo || sole || "";
  const declared = new Set(project.repos);

  const groups: RepoGroup[] = project.repos.map((ref) => ({
    ref,
    label: ref,
    resolved: resolveRepoRef(catalog, ref).ok,
    contexts: templates.filter((t) => home(t) === ref).map((t, i) => node(t, i, catalog)),
  }));

  const strays = templates.filter((t) => !declared.has(home(t)));
  const unnamed = strays.filter((t) => !t.contextRepo);
  const undeclared = strays.filter((t) => !!t.contextRepo);
  const reasons = [
    { ref: "—", label: "repository not named", resolved: true, of: unnamed },
    { ref: "—undeclared", label: "repository not declared here", resolved: false, of: undeclared },
  ];
  for (const { of, ...group } of reasons)
    if (of.length > 0) groups.push({ ...group, contexts: of.map((t, i) => node(t, i, catalog)) });
  return groups;
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

/**
 * SVG text does not wrap and a real repository ref is three times the width of any name a
 * mock uses, so a long label ran out of its box and across the next column. These labels
 * are monospaced — an advance of 0.6em holds for the stack they use — so the budget is
 * computable without measuring, and a narrower face only over-truncates, which is the safe
 * direction. Stretching the glyphs gives no ellipsis and squashes the name; a clip path
 * cuts mid-glyph and needs an id per node. The full string stays on the title.
 */
export function fitLabel(label: string, boxWidth: number, fontSize: number): string {
  const budget = Math.floor((boxWidth - 18) / (0.6 * fontSize));
  if (budget < 1) return "…";
  return label.length <= budget ? label : `${label.slice(0, budget - 1)}…`;
}

function Node({
  x, y, w, h, label, caption, bad, testId,
  fontSize, captionDy = 18, labelDy = 35, rx = 8, accent, bold, faint,
}: {
  x: number; y: number; w: number; h: number;
  label: string; caption: string; bad?: boolean; testId: string;
  fontSize: number; captionDy?: number; labelDy?: number; rx?: number;
  accent?: boolean; bold?: boolean; faint?: boolean;
}) {
  const stroke = bad ? "var(--bad)" : accent ? "var(--accent)" : "currentColor";
  const opacity = bad ? 1 : accent ? 1 : faint ? 0.35 : 0.45;
  return (
    <g data-testid={testId} data-coloured={bad ? "true" : "false"}>
      <title>{`${caption}: ${label}`}</title>
      <rect
        x={x} y={y} width={w} height={h} rx={rx}
        fill="none" stroke={stroke} strokeOpacity={opacity} strokeWidth={accent ? 2 : 1}
      />
      <text x={x + 14} y={y + captionDy} fontSize={10} fill={stroke} opacity={0.55}>
        {caption}
      </text>
      <text
        x={x + 14} y={y + labelDy} fontSize={fontSize} fill={stroke}
        fontWeight={bold ? 600 : undefined} fontFamily="var(--mono)"
      >
        {fitLabel(label, w, fontSize)}
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
