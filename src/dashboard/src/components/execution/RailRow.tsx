"use client";

import type { NodeStatus } from "./TimingGutter";

// p0205: one single-line row of the master/detail nav rail. chevron (only when
// the node has children) · status dot · label · optional metric · duration.
// Clicking the row selects it; clicking the chevron toggles its children
// without changing selection. Mirrors the calm single-line index in the p0205
// redesign mockup.

export interface RailRowProps {
  id: string;
  label: string;
  status: NodeStatus;
  durationLabel?: string;
  metric?: string | null;
  hasChildren?: boolean;
  isChild?: boolean;
  isSelected: boolean;
  isExpanded: boolean;
  onSelect: () => void;
  onToggle: () => void;
}

export function RailRow(props: RailRowProps) {
  const selectedCls = props.isSelected ? "bg-emerald-50 border-l-emerald-500" : "border-l-transparent";
  const labelTone =
    props.status === "fail"
      ? "text-rose-700"
      : props.isSelected
      ? "font-semibold text-emerald-700"
      : "text-stone-700";
  return (
    <div
      data-testid={`rail-row-${props.id}`}
      data-selected={props.isSelected ? "true" : "false"}
      onClick={props.onSelect}
      role="button"
      tabIndex={0}
      onKeyDown={(e) => (e.key === "Enter" || e.key === " ") && props.onSelect()}
      className={`flex min-h-[34px] cursor-pointer select-none items-center gap-2.5 border-l-[3px] py-1.5 hover:bg-stone-50 ${selectedCls} ${
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
      <span
        data-testid={`rail-row-${props.id}-label`}
        className={`flex-1 truncate dsh-body ${props.isChild ? "font-mono dsh-mono" : "font-medium"} ${labelTone}`}
      >
        {props.label}
      </span>
      {props.metric && (
        <span className="flex-none font-mono dsh-label text-stone-400">{props.metric}</span>
      )}
      <span className="w-12 flex-none text-right font-mono dsh-label text-stone-400">
        {props.durationLabel ?? ""}
      </span>
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
      className={`w-3 flex-none text-center dsh-label text-stone-400 transition-transform ${
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
      className={`h-2 w-2 flex-none rounded-full ${dotClass(status)}`}
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
  }
}
