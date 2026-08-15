"use client";

import type { NodeStatus } from "./TimingGutter";

// p0205: one row of the master/detail nav rail. chevron (only when the node
// has children) · status dot · label · meta. Clicking the row selects it;
// clicking the chevron toggles its children without changing selection.
//
// p0395b: label and meta share ONE line — meta right-aligned, label first.
// The label clamps to at most two lines, which only engages when the rail is
// too narrow for one; the row height flexes only in that case. The resizable
// rail (p0395/p0395a fractions) is what makes the one-line default workable.

export interface RailRowProps {
  id: string;
  label: string;
  status: NodeStatus;
  durationLabel?: string;
  metric?: string | null;
  hasChildren?: boolean;
  isChild?: boolean;
  /** p0405: an announced-but-unreached step. Muted, so the boundary between what
   *  ran and what is still coming is visible without reading a single label. */
  isPlanned?: boolean;
  isSelected: boolean;
  isExpanded: boolean;
  onSelect: () => void;
  onToggle: () => void;
}

export function RailRow(props: RailRowProps) {
  const selectedCls = props.isSelected ? "bg-emerald-50 border-l-emerald-500" : "border-l-transparent";
  const labelTone = props.isPlanned
    ? "text-stone-400"
    : props.status === "fail"
      ? "text-rose-700"
      : props.isSelected
      ? "font-semibold text-emerald-700"
      : "text-stone-700";
  return (
    <div
      data-testid={`rail-row-${props.id}`}
      data-planned={props.isPlanned ? "true" : "false"}
      data-selected={props.isSelected ? "true" : "false"}
      onClick={props.onSelect}
      role="button"
      tabIndex={0}
      onKeyDown={(e) => (e.key === "Enter" || e.key === " ") && props.onSelect()}
      className={`flex min-h-[34px] cursor-pointer select-none items-start gap-2.5 border-l-[3px] py-1.5 hover:bg-stone-50 ${selectedCls} ${
        props.isChild ? "pl-10 pr-4" : "px-4"
      }`}
    >
      <Chevron
        show={!!props.hasChildren}
        isExpanded={props.isExpanded}
        onClick={(e) => {
          e.stopPropagation();
          props.onToggle();
        }}
        testId={`rail-chevron-${props.id}`}
      />
      <StatusDot status={props.status} />
      <div className="flex min-w-0 flex-1 items-baseline gap-3">
        <span
          data-testid={`rail-row-${props.id}-label`}
          className={`min-w-0 flex-1 line-clamp-2 dsh-body ${props.isChild ? "font-mono dsh-mono" : "font-medium"} ${labelTone}`}
        >
          {props.label}
        </span>
        {(props.metric || props.durationLabel) && (
          <span
            data-testid={`rail-row-${props.id}-meta`}
            className="ml-auto flex flex-none gap-3 whitespace-nowrap font-mono dsh-label text-stone-400"
          >
            {props.metric && <span>{props.metric}</span>}
            {props.durationLabel && <span>{props.durationLabel}</span>}
          </span>
        )}
      </div>
    </div>
  );
}

function Chevron(props: {
  show: boolean;
  isExpanded: boolean;
  onClick: (e: React.MouseEvent) => void;
  testId: string;
}) {
  if (!props.show) return <span className="w-3 flex-none" aria-hidden="true" />;
  return (
    <span
      data-testid={props.testId}
      onClick={props.onClick}
      className={`mt-[3px] w-3 flex-none text-center dsh-label text-stone-400 transition-transform ${
        props.isExpanded ? "rotate-90" : ""
      }`}
      aria-hidden="true"
    >
      ▶
    </span>
  );
}

function StatusDot({ status }: { status: NodeStatus }) {
  return (
    <span
      data-testid={`rail-dot-${status}`}
      className={`mt-1.5 h-2 w-2 flex-none rounded-full ${dotClass(status)}`}
      aria-label={status}
    />
  );
}

function dotClass(status: NodeStatus): string {
  switch (status) {
    case "ok":
      return "bg-emerald-500";
    case "fail":
      return "bg-rose-500";
    case "run":
      return "bg-amber-500 animate-pulse";
    case "wait":
      return "bg-stone-300";
    case "cancel":
      return "bg-slate-400";
    // p0320d: queued = amber but static — waiting for capacity, not executing.
    case "queued":
      return "bg-amber-400";
    // p0327: waiting for the operator's answer — violet, static.
    case "input":
      return "bg-violet-400";
  }
}
