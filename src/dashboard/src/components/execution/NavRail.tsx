"use client";

import type { ExecutionNodeProps } from "./ExecutionNode";
import { RailRow } from "./RailRow";
import type { NodeStatus } from "./TimingGutter";
import type { RailSelection } from "@/hooks/useRailSelection";

// p0205: the left pane of the two-pane run detail — a scannable single-line
// index. Two sections: Execution (the step/sub-agent tree from
// useRunExecutionTree) and Overview (Architecture + Result). Selection +
// expansion are owned by useRailSelection so they survive refresh via the URL
// hash. Children render nested under their parent only while it is expanded.

export interface OverviewRailItem {
  id: string;
  label: string;
  status: NodeStatus;
}

interface NavRailProps {
  nodes: ExecutionNodeProps[];
  overview: OverviewRailItem[];
  selection: RailSelection;
}

export function NavRail({ nodes, overview, selection }: NavRailProps) {
  return (
    <nav
      data-testid="nav-rail"
      className="h-full overflow-y-auto border-r border-stone-200 pb-8"
    >
      <Section label="Execution" />
      {nodes.map((n) => (
        <NodeRows key={n.id} node={n} selection={selection} />
      ))}
      <Section label="Overview" />
      {overview.map((o) => (
        <RailRow
          key={o.id}
          id={o.id}
          label={o.label}
          status={o.status}
          isSelected={selection.selected === o.id}
          isExpanded={false}
          onSelect={() => selection.select(o.id)}
          onToggle={() => {}}
        />
      ))}
    </nav>
  );
}

function NodeRows({ node, selection }: { node: ExecutionNodeProps; selection: RailSelection }) {
  const children = node.children ?? [];
  const hasChildren = children.length > 0;
  const isExpanded = selection.expanded.has(node.id);
  return (
    <>
      <RailRow
        id={node.id}
        label={node.label}
        status={node.status}
        durationLabel={node.durationLabel}
        metric={node.costBadge}
        hasChildren={hasChildren}
        isSelected={selection.selected === node.id}
        isExpanded={isExpanded}
        onSelect={() => selection.select(node.id)}
        onToggle={() => selection.toggle(node.id)}
      />
      {hasChildren &&
        isExpanded &&
        children.map((c) => (
          <RailRow
            key={c.id}
            id={c.id}
            label={c.label}
            status={c.status}
            durationLabel={c.durationLabel}
            metric={c.costBadge}
            isChild
            isSelected={selection.selected === c.id}
            isExpanded={false}
            onSelect={() => selection.select(c.id, node.id)}
            onToggle={() => {}}
          />
        ))}
    </>
  );
}

function Section({ label }: { label: string }) {
  return (
    <div className="px-4 pb-1.5 pt-3 dsh-label font-semibold uppercase tracking-wider text-stone-400">
      {label}
    </div>
  );
}
