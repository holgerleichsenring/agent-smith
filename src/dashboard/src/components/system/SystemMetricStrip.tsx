import type { ReactNode } from "react";
import { cn } from "@/lib/utils";

// p0343d: the system/rollup pages' KPI strip — the mock's .health/.metric
// field-block strip re-scoped under .mock-system (auto-fit columns because
// each page carries a different, honest number of KPIs). Prop-driven so the
// rollup tests can assert real values without the live hubs.

export interface MetricCell {
  label: string;
  value: ReactNode;
  small?: ReactNode;
  hot?: boolean;
  testId?: string;
}

export function SystemMetricStrip({ cells, testId }: { cells: MetricCell[]; testId?: string }) {
  return (
    <div className="health" data-testid={testId}>
      {cells.map((cell) => (
        <div
          key={cell.label}
          data-testid={cell.testId}
          className={cn("metric", cell.hot && "hot")}
        >
          <span className="k">{cell.label}</span>
          <span className="v">
            {cell.value}
            {cell.small && <small>{cell.small}</small>}
          </span>
        </div>
      ))}
    </div>
  );
}
