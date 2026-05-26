import { render, screen } from "@testing-library/react";
import { JobListTable } from "../JobListTable";
import type { RunMeta } from "@/lib/api";

function meta(i: number, overrides: Partial<RunMeta> = {}): RunMeta {
  return {
    runId: `2026-05-20T22-27-${(40 + i).toString().padStart(2, "0")}-abc${i}`,
    pipelineName: "fix-bug",
    status: "done",
    startedAt: "2026-05-20T22:27:43Z",
    durationSeconds: 120 + i,
    repoMode: "mono",
    sandboxCount: 1,
    repos: ["primary"],
    ticket: null,
    type: "fix",
    ...overrides,
  };
}

describe("JobListTable", () => {
  it("renders empty-state when no jobs", () => {
    render(<JobListTable jobs={[]} />);
    expect(screen.getByTestId("empty-state")).toBeInTheDocument();
  });

  it("renders ten rows with status badges when given ten jobs", () => {
    const jobs = Array.from({ length: 10 }, (_, i) => meta(i));
    render(<JobListTable jobs={jobs} />);
    expect(screen.getAllByTestId("job-row")).toHaveLength(10);
    expect(screen.getAllByTestId("status-badge")).toHaveLength(10);
  });
});
