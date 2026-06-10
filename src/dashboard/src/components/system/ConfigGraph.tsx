"use client";

import { useMemo } from "react";
import type { ConfigEdge, ConfigEdgeKind, ConfigProject } from "@/lib/configApi";

interface Props {
  projects: ConfigProject[];
  edges: ConfigEdge[];
}

// p0266: config-time relationship graph — the "how the system is wired" view,
// complement to the per-run TopologyGraph ("what ran where"). Root → project
// columns → each project's linked entities stacked beneath it. No graph
// library — pure SVG math, the same idiom as TopologyGraph. Palette is
// status-NEUTRAL (config is static, not running) and differentiates by edge
// kind. A shared entity is duplicated under each project that references it,
// trading node-uniqueness for a knot-free, readable layout (see decisions).

const COLUMN_WIDTH = 210;
const MARGIN = 24;
const ROOT_Y = 22;
const ROOT_W = 150;
const PROJECT_Y = 96;
const ENTITY_Y0 = 176;
const ENTITY_STEP = 42;
const NODE_W = 168;
const NODE_H = 30;

type KindStyle = { fill: string; stroke: string; text: string; label: string };

const KIND_STYLES: Record<ConfigEdgeKind, KindStyle> = {
  agent: { fill: "fill-violet-50", stroke: "stroke-violet-300", text: "fill-violet-800", label: "agent" },
  tracker: { fill: "fill-sky-50", stroke: "stroke-sky-300", text: "fill-sky-800", label: "tracker" },
  repo: { fill: "fill-amber-50", stroke: "stroke-amber-300", text: "fill-amber-800", label: "repo" },
  pipeline: { fill: "fill-stone-50", stroke: "stroke-stone-300", text: "fill-stone-700", label: "pipeline" },
};

const KIND_ORDER: ConfigEdgeKind[] = ["agent", "tracker", "repo", "pipeline"];

interface EntityNode {
  to: string;
  kind: ConfigEdgeKind;
}

function entitiesOf(project: string, edges: ConfigEdge[]): EntityNode[] {
  const mine = edges.filter((e) => e.from === project);
  return KIND_ORDER.flatMap((kind) =>
    mine.filter((e) => e.kind === kind).map((e) => ({ to: e.to, kind })),
  );
}

export function ConfigGraph({ projects, edges }: Props) {
  const columns = useMemo(
    () => projects.map((p) => ({ project: p, entities: entitiesOf(p.name, edges) })),
    [projects, edges],
  );

  if (columns.length === 0) {
    return (
      <div
        className="rounded-lg border border-stone-200 bg-white p-6 dsh-body text-stone-500"
        data-testid="config-graph-empty"
      >
        No projects configured.
      </div>
    );
  }

  const maxEntities = Math.max(...columns.map((c) => c.entities.length), 1);
  const width = columns.length * COLUMN_WIDTH + MARGIN * 2;
  const height = ENTITY_Y0 + maxEntities * ENTITY_STEP + MARGIN;
  const rootX = width / 2;
  const columnX = (i: number) => MARGIN + i * COLUMN_WIDTH + COLUMN_WIDTH / 2;

  return (
    <figure className="rounded-lg border border-stone-200 bg-white p-2" data-testid="config-graph">
      <svg viewBox={`0 0 ${width} ${height}`} className="h-auto w-full" role="img" aria-label="agent-smith config graph">
        {columns.map((c, i) => (
          <Edge key={`root-${c.project.name}`} x1={rootX} y1={ROOT_Y + NODE_H} x2={columnX(i)} y2={PROJECT_Y} />
        ))}
        {columns.map((c, i) =>
          c.entities.map((e, j) => (
            <Edge
              key={`pe-${c.project.name}-${e.kind}-${e.to}`}
              x1={columnX(i)}
              y1={PROJECT_Y + NODE_H}
              x2={columnX(i)}
              y2={ENTITY_Y0 + j * ENTITY_STEP}
            />
          )),
        )}
        <RootNode x={rootX} y={ROOT_Y} />
        {columns.map((c, i) => (
          <ProjectNode key={c.project.name} x={columnX(i)} y={PROJECT_Y} name={c.project.name} pipeline={c.project.pipeline} />
        ))}
        {columns.map((c, i) =>
          c.entities.map((e, j) => (
            <EntityNodeBox key={`n-${c.project.name}-${e.kind}-${e.to}`} x={columnX(i)} y={ENTITY_Y0 + j * ENTITY_STEP} node={e} />
          )),
        )}
      </svg>
      <Legend />
    </figure>
  );
}

function Edge({ x1, y1, x2, y2 }: { x1: number; y1: number; x2: number; y2: number }) {
  const cy = y1 + (y2 - y1) / 2;
  return (
    <path
      d={`M ${x1} ${y1} C ${x1} ${cy}, ${x2} ${cy}, ${x2} ${y2}`}
      fill="none"
      strokeWidth={1.5}
      className="stroke-stone-300"
      data-testid="config-edge"
    />
  );
}

function RootNode({ x, y }: { x: number; y: number }) {
  return (
    <g data-testid="config-node-root">
      <rect x={x - ROOT_W / 2} y={y} width={ROOT_W} height={NODE_H} rx={8} className="fill-stone-800 stroke-stone-900" strokeWidth={1.5} />
      <text x={x} y={y + 20} textAnchor="middle" className="dsh-body font-medium fill-white">
        agent-smith
      </text>
    </g>
  );
}

function ProjectNode({ x, y, name, pipeline }: { x: number; y: number; name: string; pipeline: string }) {
  return (
    <g data-testid={`config-node-project-${name}`}>
      <rect x={x - NODE_W / 2} y={y} width={NODE_W} height={NODE_H} rx={6} className="fill-emerald-50 stroke-emerald-300" strokeWidth={1.5} />
      <text x={x} y={y + 14} textAnchor="middle" className="dsh-mono font-semibold fill-emerald-900">
        {truncate(name, 18)}
      </text>
      <text x={x} y={y + 25} textAnchor="middle" className="dsh-label fill-emerald-700">
        {truncate(pipeline, 22)}
      </text>
    </g>
  );
}

function EntityNodeBox({ x, y, node }: { x: number; y: number; node: EntityNode }) {
  const s = KIND_STYLES[node.kind];
  return (
    <g data-testid={`config-node-${node.kind}-${node.to}`} data-kind={node.kind}>
      <rect x={x - NODE_W / 2} y={y} width={NODE_W} height={NODE_H} rx={6} className={`${s.fill} ${s.stroke}`} strokeWidth={1.5} />
      <text x={x - NODE_W / 2 + 10} y={y + 19} className={`dsh-mono ${s.text}`}>
        {truncate(node.to, 16)}
      </text>
      <text x={x + NODE_W / 2 - 10} y={y + 19} textAnchor="end" className="dsh-label fill-stone-400">
        {s.label}
      </text>
    </g>
  );
}

function Legend() {
  return (
    <figcaption className="mt-2 flex flex-wrap items-center gap-x-4 gap-y-1 px-2 dsh-label text-stone-500" data-testid="config-graph-legend">
      {KIND_ORDER.map((kind) => (
        <span key={kind} className="inline-flex items-center gap-1.5">
          <span className={`inline-block h-2.5 w-2.5 rounded-sm border ${swatch(kind)}`} aria-hidden />
          {KIND_STYLES[kind].label}
        </span>
      ))}
    </figcaption>
  );
}

function swatch(kind: ConfigEdgeKind): string {
  return {
    agent: "bg-violet-50 border-violet-300",
    tracker: "bg-sky-50 border-sky-300",
    repo: "bg-amber-50 border-amber-300",
    pipeline: "bg-stone-50 border-stone-300",
  }[kind];
}

function truncate(s: string, max: number): string {
  return s.length <= max ? s : s.slice(0, max - 1) + "…";
}
