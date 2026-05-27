"use client";

import { useMemo } from "react";
import { EventType, type RunEvent } from "@/types/hub-events";
import {
  paletteFor,
  sandboxStatusColor,
  type SandboxStatus,
} from "@/lib/sandboxStatus";

interface Props {
  pipeline: string | null;
  runId: string;
  events: readonly RunEvent[];
  selected: string | null;
  onSelect: (repo: string) => void;
}

const CANVAS_WIDTH = 800;
const CANVAS_HEIGHT = 320;
const ROOT_Y = 50;
const ROW_1_Y = 200;
const ROW_2_Y = 280;
const NODES_PER_ROW = 8;
const NODE_WIDTH = 88;
const NODE_HEIGHT = 56;
const RUN_NODE_WIDTH = 200;
const RUN_NODE_HEIGHT = 56;

// p0169j-d: ArgoCD-style SVG topology. Root run node up top, sandbox
// children below in a star layout. No graph library — pure math.
// Status colours from DESIGN.md tokens; green RESERVED for done,
// running is amber-pulsing.

export function TopologyGraph({ pipeline, runId, events, selected, onSelect }: Props) {
  const repos = useMemo(() => extractRepos(events), [events]);

  const rootStatus = useMemo(() => runRootStatus(events), [events]);
  const rootPalette = paletteFor(rootStatus);

  if (repos.length === 0) {
    return (
      <div
        className="rounded-lg border border-stone-200 bg-white p-6 text-sm text-stone-500"
        data-testid="topology-graph-empty"
      >
        No sandboxes spawned yet.
      </div>
    );
  }

  const positions = repos.map((_, i) => nodePosition(i, repos.length));
  const runNodeX = CANVAS_WIDTH / 2;

  return (
    <figure
      className="rounded-lg border border-stone-200 bg-white p-2"
      data-testid="topology-graph"
    >
      <svg
        viewBox={`0 0 ${CANVAS_WIDTH} ${CANVAS_HEIGHT}`}
        className="h-auto w-full"
        role="img"
        aria-label={`Topology for run ${runId}`}
      >
        {positions.map(({ x, y }, i) => (
          <TopologyEdge
            key={`edge-${repos[i]}`}
            x1={runNodeX}
            y1={ROOT_Y + RUN_NODE_HEIGHT / 2}
            x2={x}
            y2={y - NODE_HEIGHT / 2}
          />
        ))}
        <TopologyNodeRun
          x={runNodeX}
          y={ROOT_Y}
          pipeline={pipeline ?? "run"}
          runId={runId}
          palette={rootPalette}
        />
        {repos.map((repo, i) => {
          const status = sandboxStatusColor(events, repo);
          const palette = paletteFor(status);
          const pos = positions[i];
          return (
            <TopologyNodeSandbox
              key={repo}
              x={pos.x}
              y={pos.y}
              repo={repo}
              status={status}
              palette={palette}
              selected={selected === repo}
              onClick={() => onSelect(repo)}
            />
          );
        })}
      </svg>
    </figure>
  );
}

function TopologyEdge({ x1, y1, x2, y2 }: { x1: number; y1: number; x2: number; y2: number }) {
  // Bezier curve down — control points pull vertically to soften the bend.
  const cx1 = x1;
  const cy1 = y1 + 60;
  const cx2 = x2;
  const cy2 = y2 - 60;
  return (
    <path
      d={`M ${x1} ${y1} C ${cx1} ${cy1}, ${cx2} ${cy2}, ${x2} ${y2}`}
      fill="none"
      strokeWidth={1.5}
      className="stroke-stone-300"
      data-testid="topology-edge"
    />
  );
}

function TopologyNodeRun({
  x, y, pipeline, runId, palette,
}: {
  x: number;
  y: number;
  pipeline: string;
  runId: string;
  palette: ReturnType<typeof paletteFor>;
}) {
  const left = x - RUN_NODE_WIDTH / 2;
  return (
    <g data-testid="topology-node-run">
      <rect
        x={left}
        y={y}
        width={RUN_NODE_WIDTH}
        height={RUN_NODE_HEIGHT}
        rx={8}
        ry={8}
        className={`${palette.fill} ${palette.stroke}`}
        strokeWidth={1.5}
      />
      <text
        x={x}
        y={y + 22}
        textAnchor="middle"
        className={`text-[13px] font-medium ${palette.text}`}
      >
        {pipeline}
      </text>
      <text
        x={x}
        y={y + 42}
        textAnchor="middle"
        className="fill-stone-500 font-mono text-[10px]"
      >
        {runId}
      </text>
    </g>
  );
}

function TopologyNodeSandbox({
  x, y, repo, status, palette, selected, onClick,
}: {
  x: number;
  y: number;
  repo: string;
  status: SandboxStatus;
  palette: ReturnType<typeof paletteFor>;
  selected: boolean;
  onClick: () => void;
}) {
  const left = x - NODE_WIDTH / 2;
  const top = y - NODE_HEIGHT / 2;
  const selectionRing = selected ? "stroke-emerald-600" : palette.stroke;
  const selectionWidth = selected ? 2.5 : 1.5;
  return (
    <g
      data-testid={`topology-node-sandbox-${repo}`}
      data-status={status}
      data-selected={selected ? "true" : "false"}
      onClick={onClick}
      className="cursor-pointer"
    >
      <rect
        x={left}
        y={top}
        width={NODE_WIDTH}
        height={NODE_HEIGHT}
        rx={6}
        ry={6}
        className={`${palette.fill} ${selectionRing} ${palette.pulse ? "motion-safe:animate-topology-pulse" : ""}`}
        strokeWidth={selectionWidth}
      />
      <text
        x={x}
        y={top + 22}
        textAnchor="middle"
        className={`text-[12px] font-medium ${palette.text}`}
      >
        {truncate(repo, 12)}
      </text>
      <text
        x={x}
        y={top + 40}
        textAnchor="middle"
        className="fill-stone-500 font-mono text-[10px]"
      >
        {status}
      </text>
    </g>
  );
}

function nodePosition(index: number, total: number): { x: number; y: number } {
  const row = Math.floor(index / NODES_PER_ROW);
  const inRow = index % NODES_PER_ROW;
  const cap = Math.min(total - row * NODES_PER_ROW, NODES_PER_ROW);
  const x = (CANVAS_WIDTH / (cap + 1)) * (inRow + 1);
  const y = row === 0 ? ROW_1_Y : ROW_2_Y;
  return { x, y };
}

function extractRepos(events: readonly RunEvent[]): string[] {
  const repos = new Set<string>();
  for (const e of events) {
    if (e.type === EventType.SandboxCreated) repos.add(e.repo);
  }
  return [...repos].sort();
}

function runRootStatus(events: readonly RunEvent[]): SandboxStatus {
  let stepFailed = false;
  let finished = false;
  let succeeded = false;
  for (const e of events) {
    if (e.type === EventType.StepFinished && e.status === "failed") stepFailed = true;
    if (e.type === EventType.RunFinished) {
      finished = true;
      if (e.status === "success") succeeded = true;
    }
  }
  if (stepFailed) return "failed";
  if (finished && succeeded) return "success";
  if (finished) return "disposed";
  return "running";
}

function truncate(s: string, max: number): string {
  return s.length <= max ? s : s.slice(0, max - 1) + "…";
}
