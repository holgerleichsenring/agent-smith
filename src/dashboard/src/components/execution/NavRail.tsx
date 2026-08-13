"use client";

import { Fragment, useState } from "react";
import type { ExecutionNodeProps } from "./ExecutionNode";
import { RailRow } from "./RailRow";
import type { NodeStatus } from "./TimingGutter";
import type { RailSelection } from "@/hooks/useRailSelection";
import { isStoryRow } from "@/lib/runStepRail";

// p0205: the left pane of the two-pane run detail — a scannable single-line
// index. Two sections: Execution (the step/sub-agent tree from
// useRunExecutionTree) and Overview (Architecture + Result). Selection +
// expansion are owned by useRailSelection so they survive refresh via the URL
// hash. Children render nested under their parent only while it is expanded.
//
// p0395: spliced phase steps (p0393a) carry their phase as a GROUP HEADER above
// the phase's first row instead of a per-row label prefix — the prefix ate the
// rail width and truncated the real step name on every row.
//
// p0398: the default view is the run's STORY — milestones plus gates that have
// something to say. Internals and silent gates collapse into one "mechanics"
// row per segment (pre-phase, per phase) which expands in place to the full
// step list; nothing is hidden from the data, only from the default view. A
// segment holding the selected step renders expanded so deep links keep
// resolving to a visible row.

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

interface RailSegment {
  key: string;
  phaseId: string | null;
  nodes: ExecutionNodeProps[];
}

// Consecutive rows of the same phase (or of no phase) form one segment — the
// unit the mechanics row collapses over.
function toSegments(nodes: ExecutionNodeProps[]): RailSegment[] {
  const segments: RailSegment[] = [];
  for (const node of nodes) {
    const phaseId = node.phaseId ?? null;
    const last = segments[segments.length - 1];
    if (last && last.phaseId === phaseId) last.nodes.push(node);
    else segments.push({ key: `${phaseId ?? "pre"}:${node.id}`, phaseId, nodes: [node] });
  }
  return segments;
}

export function NavRail({ nodes, overview, selection }: NavRailProps) {
  const [openMechanics, setOpenMechanics] = useState<Set<string>>(new Set());
  const toggleMechanics = (key: string) =>
    setOpenMechanics((prev) => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });

  return (
    <nav
      data-testid="nav-rail"
      className="h-full overflow-y-auto border-r border-stone-200 pb-8"
    >
      <Section label="Execution" />
      {toSegments(nodes).map((segment) => (
        <Fragment key={segment.key}>
          {segment.phaseId && <PhaseHeader phaseId={segment.phaseId} />}
          <SegmentRows
            segment={segment}
            selection={selection}
            isOpen={openMechanics.has(segment.key)}
            onToggleMechanics={() => toggleMechanics(segment.key)}
          />
        </Fragment>
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

// One segment: story rows always render; the rest sit behind one mechanics row
// placed where the first collapsed step lives. Expanded, every step renders in
// its original order — the mechanics row stays as the collapse toggle.
function SegmentRows(props: {
  segment: RailSegment;
  selection: RailSelection;
  isOpen: boolean;
  onToggleMechanics: () => void;
}) {
  const hidden = props.segment.nodes.filter((n) => !isStoryRow(n));
  const selectedHidden = hidden.some((n) => n.id === props.selection.selected);
  const expanded = props.isOpen || selectedHidden;
  const firstHiddenId = hidden[0]?.id;
  return (
    <>
      {props.segment.nodes.map((n) => (
        <Fragment key={n.id}>
          {n.id === firstHiddenId && (
            <MechanicsRow
              segmentKey={props.segment.key}
              count={hidden.length}
              isOpen={expanded}
              onToggle={props.onToggleMechanics}
            />
          )}
          {(expanded || isStoryRow(n)) && <NodeRows node={n} selection={props.selection} />}
        </Fragment>
      ))}
    </>
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
        isPlanned={node.planned}
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

// p0398: the collapsed stand-in for a segment's mechanics — loaders, splice
// bookkeeping, silent gates. One row, a count, and a chevron; expanding reveals
// the full step list in place.
function MechanicsRow(props: {
  segmentKey: string;
  count: number;
  isOpen: boolean;
  onToggle: () => void;
}) {
  return (
    <div
      data-testid={`rail-mechanics-${props.segmentKey}`}
      onClick={props.onToggle}
      role="button"
      tabIndex={0}
      onKeyDown={(e) => (e.key === "Enter" || e.key === " ") && props.onToggle()}
      className="flex min-h-[28px] cursor-pointer select-none items-center gap-2.5 border-l-[3px] border-l-transparent px-4 py-1 hover:bg-stone-50"
    >
      <span
        className={`w-3 flex-none text-center dsh-label text-stone-400 transition-transform ${
          props.isOpen ? "rotate-90" : ""
        }`}
        aria-hidden="true"
      >
        ▶
      </span>
      <span className="dsh-label text-stone-400">
        {props.isOpen ? "Hide mechanics" : `${props.count} mechanics step${props.count === 1 ? "" : "s"}`}
      </span>
    </div>
  );
}

// One header per run of same-phase rows: the phase id in mono (it IS an id),
// visually between the section eyebrow and the rows it groups.
function PhaseHeader({ phaseId }: { phaseId: string }) {
  return (
    <div
      data-testid={`rail-phase-${phaseId}`}
      className="border-t border-stone-100 px-4 pb-1 pt-2.5 font-mono dsh-label font-semibold text-stone-500"
    >
      {phaseId}
    </div>
  );
}

function Section({ label }: { label: string }) {
  return (
    <div className="px-4 pb-1.5 pt-3 dsh-label font-semibold uppercase tracking-wider text-stone-400">
      {label}
    </div>
  );
}
