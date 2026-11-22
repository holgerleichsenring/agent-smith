import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { attemptStatus } from "@/components/jobs/runlog/runStatusLine";
import { modelWaitHead, waitClock } from "@/components/jobs/runlog/modelWaitWords";
import { currentModelWait } from "@/components/jobs/runlog/runNowLines";
import { translator } from "@/lib/i18n/translate";
import { EventType, type RunEvent } from "@/types/hub-events";
import { ticketState } from "../ticketState";
import { yourMove } from "../yourMove";
import { YourMoveBox } from "../YourMoveBox";
import { attempt, detail } from "./detailFixtures";

// 2026-11-22-50d7a: a run waiting for its model place is not "Working" — the ticket page reads the wait off the run's
// events the way the run log does, and the Your move box, the run's status line and the run page's head say it.

const LINE = "Waiting for the model: gpt41 runs 1 at a time (agent.max_concurrent_runs)";
const SINCE = "2026-11-22T19:37:04Z";
const waiting = { type: EventType.L1StepDetail, runId: "r-1", timestamp: SINCE, stepIndex: 3, origin: "model-slot", detail: LINE } as unknown as RunEvent;
const started = { type: EventType.StepStarted, runId: "r-1", timestamp: "2026-11-22T19:36:00Z", stepIndex: 3, stepName: "AnalyzeCode",
  totalSteps: 20, displayName: "Analyze codebase" } as unknown as RunEvent;
const callStarted = { type: EventType.LlmCallStarted, runId: "r-1", timestamp: "2026-11-22T20:17:00Z" } as unknown as RunEvent;
const en = translator("en");

function workingDetail() {
  const d = detail([attempt({ status: "running", finishedAt: null })]);
  d.summary = { ...d.summary, bucket: "working" };
  return d;
}

vi.mock("../useYourMove", () => ({ useYourMove: () => current }));
vi.mock("../useTicketTabGo", () => ({ useTicketTabGo: () => ({ go: () => {} }) }));
let current: ReturnType<typeof read> | null = null;
function read(events: RunEvent[]) {
  const view = ticketState({ detail: workingDetail(), liveEvents: events, series: null })!;
  return { view, move: yourMove(view, "working", null) };
}

describe("model wait", () => {
  it("CurrentModelWait_EndsWithTheNextModelCall", () => {
    expect(currentModelWait([started, waiting])).toEqual({ since: SINCE, line: LINE });
    expect(currentModelWait([started, waiting, callStarted])).toBeNull();
  });

  it("TicketState_WaitingRun_CarriesTheWaitAndTheGateLine_NotTheStep", () => {
    const view = ticketState({ detail: workingDetail(), liveEvents: [started, waiting], series: null })!;
    expect(view.modelWait).toEqual({ since: SINCE, line: LINE });
    expect(view.detail).toBe(LINE);
    const after = ticketState({ detail: workingDetail(), liveEvents: [started, waiting, callStarted], series: null })!;
    expect(after.modelWait).toBeNull();
    expect(after.detail).toBe("Now: Analyze codebase");
  });

  it("AttemptStatus_WaitingRun_HeadSaysItWaitsForTheModel", () => {
    const head = modelWaitHead(en, currentModelWait([waiting]));
    expect(head).toBe(`Waiting for the model since ${waitClock(SINCE)}`);
    expect(attemptStatus(attempt({ status: "running", finishedAt: null }), null, null, head).head).toBe(head);
    expect(attemptStatus(attempt({ status: "running", finishedAt: null }), null).head).toBe("Working");
    expect(modelWaitHead(translator("de"), currentModelWait([waiting]))).toBe(`Wartet seit ${waitClock(SINCE)} auf das Modell`);
  });

  it("YourMoveBox_WaitingRun_SaysWaitingForTheModel", () => {
    current = read([started, waiting]);
    render(<YourMoveBox detail={workingDetail()} onReply={() => {}} onAnswer={() => {}} />);
    expect(screen.getByTestId("ticket-move-title")).toHaveTextContent("Waiting for the model. Nothing for you to do.");
    expect(screen.getByTestId("ticket-state-sentence"))
      .toHaveTextContent(`Attempt 1 has waited since ${waitClock(SINCE)} for a place on the model; other runs hold every place.`);
    expect(screen.getByTestId("ticket-state-detail")).toHaveTextContent(LINE);
  });
});
