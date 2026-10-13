"use client";

import type { KeyboardEvent } from "react";
import { FLOW_WIDTH, type AttemptFlowLayout, type FlowIcon, type FlowNode } from "./attemptFlowLayout";

// 2026-10-13-9a59q: paints a layout — the mock's SVG on card-terminal-panel. Nodes are focusable
// buttons (Enter, Space); the live dash and halo stop under reduced motion (CSS).

const ICON: Record<FlowIcon, string> = {
  ticket: "M-5-6 L6 0 L-5 6 Z",
  derive: "M-6-6 h8 l4 4 v8 h-12 Z M-3-1 h6 M-3 2 h6",
  gate: "M-6 0 L-2 5 L6-6",
  hand: "M-5 2 v-6 a1.6 1.6 0 013 0 v-1 a1.6 1.6 0 013 0 v1 a1.6 1.6 0 013 0 v5 a5 5 0 01-9 3 Z",
  spec: "M-5-7 h7 l4 4 v10 h-11 Z M2-7 v4 h4",
  build: "M-5-5 a2 2 0 100 .1 M5-5 a2 2 0 100 .1 M5 5 a2 2 0 100 .1 M-4-3 v4 a2 2 0 002 2 h5 M3-4 L-2 0",
  verify: "M-6 0 L-2 5 L6-6",
  ask: "M-3-3 a3 3 0 116 0 c0 2-3 2-3 4 M0 5 v.5",
  pr: "M-5-6 a2 2 0 100 .1 M-5-4 v9 M-5 7 a2 2 0 100 .1 M5 1 a2 2 0 100 .1 M5 3 v3 M5-1 v-4 a2 2 0 00-2-2 h-4 M-1-8 L-4-5 L-1-2",
  stop: "M-5-5 h10 v10 h-10 Z",
};

interface Props {
  layout: AttemptFlowLayout;
  selected: string | null;
  onSelect: (id: string) => void;
}

export function FlowCanvas({ layout, selected, onSelect }: Props) {
  return (
    <div className="canvas">
      <svg viewBox={`0 0 ${FLOW_WIDTH} ${layout.height}`} role="img" aria-label="Flow of the selected attempt" data-testid="attempt-flow">
        {layout.lanes.map((lane, i) => (
          <rect key={`lane-${i}`} className="lane" x={lane.x} y={lane.y} width={lane.width} height={lane.height} rx={14} />
        ))}
        {layout.edges.map((edge, i) => <path key={`edge-${i}`} className={`edge ${edge.cls}`} d={edge.d} data-edge={edge.cls} />)}
        {layout.texts.map((text, i) => (
          <text key={`text-${i}`} className={text.cls} x={text.x} y={text.y} textAnchor={text.middle ? "middle" : undefined}>
            {text.text}
          </text>
        ))}
        {layout.nodes.map((node) => (
          <FlowNodeMark key={node.id} node={node} selected={node.id === selected} onSelect={onSelect} />
        ))}
      </svg>
    </div>
  );
}

function FlowNodeMark({ node, selected, onSelect }: { node: FlowNode; selected: boolean; onSelect: (id: string) => void }) {
  const { x, y, r } = node;
  const onKeyDown = (event: KeyboardEvent<SVGGElement>) => {
    if (event.key !== "Enter" && event.key !== " ") return;
    event.preventDefault();
    onSelect(node.id);
  };
  return (
    <g
      className={`node ${node.state}${selected ? " sel" : ""}`}
      tabIndex={0}
      role="button"
      aria-label={[node.label, node.metric].filter(Boolean).join(", ")}
      aria-pressed={selected}
      data-node={node.id}
      onClick={() => onSelect(node.id)}
      onKeyDown={onKeyDown}
    >
      <circle className="halo" cx={x} cy={y} r={r + 7} />
      <circle className="disc" cx={x} cy={y} r={r} />
      <path className="ic" transform={`translate(${x},${y})${r < 22 ? " scale(.75)" : ""}`} d={ICON[node.icon]} />
      {node.badge && <text className="badge" x={x + r + 4} y={y - r + 4}>{node.badge}</text>}
      <text className="lab" x={x} y={y + r + 18} textAnchor="middle">{node.label}</text>
      <text className="met" x={x} y={y + r + 32} textAnchor="middle">{node.metric}</text>
    </g>
  );
}
