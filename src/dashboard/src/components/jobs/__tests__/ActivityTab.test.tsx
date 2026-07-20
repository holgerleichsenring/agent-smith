import { render, screen, fireEvent } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { EventFilterProvider } from "@/lib/EventFilterContext";
import {
  EventType,
  type CatalogIssueEvent,
  type RunEvent,
  type StepFinishedEvent,
  type StepStartedEvent,
} from "@/types/hub-events";

const replaceMock = vi.fn();
let mockSearchParams = new URLSearchParams();
let mockEvents: RunEvent[] = [];

vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace: replaceMock }),
  usePathname: () => "/jobs/run-1",
  useSearchParams: () => mockSearchParams,
}));

vi.mock("@/hooks/useRunEvents", () => ({
  useRunEvents: () => mockEvents,
}));

import { ActivityTab } from "../ActivityTab";

const RUN_ID = "2026-05-27T18-00-00-dddd";
const TS = "2026-05-27T18:00:00.000Z";

function stepStart(idx: number, name: string): StepStartedEvent {
  return {
    runId: RUN_ID,
    type: EventType.StepStarted,
    timestamp: TS,
    stepIndex: idx,
    stepName: name,
    totalSteps: 10,
  };
}

function stepFail(idx: number, reason: string): StepFinishedEvent {
  return {
    runId: RUN_ID,
    type: EventType.StepFinished,
    timestamp: TS,
    stepIndex: idx,
    status: "failed",
    durationMs: 250,
    reason,
  };
}

function catalogIssue(message: string, severity: string = "warning"): CatalogIssueEvent {
  return {
    runId: RUN_ID,
    type: EventType.CatalogIssue,
    timestamp: TS,
    severity,
    source: "skills/project-discovery/SKILL.md",
    category: "skill-validation",
    message,
  };
}

beforeEach(() => {
  replaceMock.mockClear();
  mockSearchParams = new URLSearchParams();
  mockEvents = [];
});

describe("ActivityTab", () => {
  it("default load — all six pills active, all event categories visible", () => {
    mockEvents = [
      stepStart(1, "Bootstrap"),
      catalogIssue("description must be at most 200 chars"),
      stepFail(1, "no skill with output_schema=discovery available"),
    ];
    render(<EventFilterProvider><ActivityTab runId={RUN_ID} /></EventFilterProvider>);
    for (const pill of ["decisions", "tools", "llm", "sandbox", "gates", "issues"]) {
      expect(screen.getByTestId(`activity-pill-${pill}`)).toHaveAttribute("aria-pressed", "true");
    }
    expect(screen.getByText(/description must be at most 200 chars/)).toBeInTheDocument();
    expect(screen.getByText(/no skill with output_schema=discovery available/)).toBeInTheDocument();
  });

  it("the project-discovery skill-drop scenario — both rows visible with reasons inline", () => {
    mockEvents = [
      stepStart(1, "Bootstrap"),
      catalogIssue("description must be at most 200 chars; got 229"),
      stepFail(1, "BootstrapDiscover: no skill with output_schema=discovery available"),
    ];
    render(<EventFilterProvider><ActivityTab runId={RUN_ID} /></EventFilterProvider>);
    const catalogRow = screen.getByTestId(`activity-row-${EventType.CatalogIssue}`);
    const failRow = screen.getByTestId(`activity-row-${EventType.StepFinished}`);
    expect(catalogRow).toHaveAttribute("data-severity", "warn");
    expect(failRow).toHaveAttribute("data-severity", "error");
  });

  it("toggling Issues pill off hides CatalogIssue rows", () => {
    mockEvents = [stepStart(1, "Bootstrap"), catalogIssue("validation failed")];
    render(<EventFilterProvider><ActivityTab runId={RUN_ID} /></EventFilterProvider>);
    expect(screen.queryByTestId(`activity-row-${EventType.CatalogIssue}`)).toBeInTheDocument();
    fireEvent.click(screen.getByTestId("activity-pill-issues"));
    expect(replaceMock).toHaveBeenCalled();
    const url = replaceMock.mock.calls[0][0] as string;
    expect(url).toContain("activity=");
    expect(url).not.toContain("issues");
  });

  it("StepStarted/StepFinished remain visible even with all pills off (lifecycle scaffold)", () => {
    mockSearchParams = new URLSearchParams("activity=");
    mockEvents = [stepStart(1, "Bootstrap"), stepFail(1, "x")];
    render(<EventFilterProvider><ActivityTab runId={RUN_ID} /></EventFilterProvider>);
    expect(screen.getByTestId(`activity-row-${EventType.StepStarted}`)).toBeInTheDocument();
    expect(screen.getByTestId(`activity-row-${EventType.StepFinished}`)).toBeInTheDocument();
  });

  it("URL ?activity=… restores pill state on refresh", () => {
    mockSearchParams = new URLSearchParams("activity=decisions,tools");
    mockEvents = [];
    render(<EventFilterProvider><ActivityTab runId={RUN_ID} /></EventFilterProvider>);
    expect(screen.getByTestId("activity-pill-decisions")).toHaveAttribute("aria-pressed", "true");
    expect(screen.getByTestId("activity-pill-tools")).toHaveAttribute("aria-pressed", "true");
    expect(screen.getByTestId("activity-pill-issues")).toHaveAttribute("aria-pressed", "false");
    expect(screen.getByTestId("activity-pill-gates")).toHaveAttribute("aria-pressed", "false");
  });

  it("windows a long feed to the last 200 rows with a 'show earlier' fold (p0355)", () => {
    mockEvents = Array.from({ length: 250 }, (_, i) => stepStart(i, `Step ${i}`));
    render(<EventFilterProvider><ActivityTab runId={RUN_ID} /></EventFilterProvider>);
    expect(screen.getAllByTestId(`activity-row-${EventType.StepStarted}`)).toHaveLength(200);
    const fold = screen.getByTestId("activity-show-earlier");
    expect(fold).toHaveTextContent("Show 50 earlier events");
    fireEvent.click(fold);
    expect(screen.getAllByTestId(`activity-row-${EventType.StepStarted}`)).toHaveLength(250);
    expect(screen.queryByTestId("activity-show-earlier")).not.toBeInTheDocument();
  });

  it("power-user 'Show event types' toggle reveals the legacy FilterRail", () => {
    mockEvents = [];
    render(<EventFilterProvider><ActivityTab runId={RUN_ID} /></EventFilterProvider>);
    expect(screen.queryByTestId("activity-debug-rail")).not.toBeInTheDocument();
    fireEvent.click(screen.getByTestId("activity-debug-toggle"));
    expect(screen.getByTestId("activity-debug-rail")).toBeInTheDocument();
  });
});
