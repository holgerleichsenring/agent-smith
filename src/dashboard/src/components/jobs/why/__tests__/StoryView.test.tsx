import { render, screen, within } from "@testing-library/react";
import { describe, it, expect } from "vitest";
import { PhaseAccount } from "../PhaseAccount";
import { TicketStatistics } from "../TicketStatistics";
import type {
  RunCallPoint, RunCommandPoint, RunPhaseStatistics, RunStatistics,
} from "@/lib/runStoryApi";
import type { RunAcceptance } from "@/types/hub-events";

// p0423b: the story view answers "why did this run do that" — phase by phase, its criteria
// and how each was accounted for, its commands with exit codes, and its calls with their
// sizes and durations. Every number is a fold over the trail; none is a counter.

function stats(over: Partial<RunStatistics["totals"]> = {}): RunStatistics["totals"] {
  return {
    calls: 5, failedCalls: 1, totalDurationMs: 1_724_300,
    totalPromptChars: 1_342_690, largestPromptChars: 356_632,
    totalResponseChars: 11_491, smallestResponseChars: 0,
    toolCalls: 22, toolOutputChars: 4_136_125, toolCharsNeverDelivered: 4_035_565,
    retries: 2, ...over,
  };
}

const PHASE: RunPhaseStatistics = {
  phaseId: "p1", steps: 4, durationMs: 1_980_000,
  calls: stats(), commands: 3, failedCommands: 1,
};

function call(index: number, promptChars: number, answerChars: number): RunCallPoint {
  return {
    index, phaseId: "p1", stepIndex: 2, role: "agentic-executor", model: "sonnet",
    promptChars, answerChars, durationMs: 9_300, throttleWaitMs: 0, outcome: "Ok", attempt: 1,
  };
}

const COMMANDS: RunCommandPoint[] = [
  {
    index: 1, phaseId: "p1", stepIndex: 2, repo: "server", command: "dotnet build",
    exitCode: 0, durationMs: 41_000, outputChars: 12_400, deliveredChars: 12_400, attempt: 1,
  },
  {
    index: 2, phaseId: "p1", stepIndex: 3, repo: "server", command: "dotnet test",
    exitCode: 1, durationMs: 96_000, outputChars: 412_000, deliveredChars: 100_048, attempt: 1,
  },
];

const ACCEPTANCE: RunAcceptance = {
  criteria: [
    { text: "The login button authenticates", status: "met", reason: null },
    { text: "A failed login shows an error", status: "unmet", reason: "no test exercised it" },
  ],
  outcome: "edited",
  ratifiedBy: "operator",
};

describe("The story view", () => {
  it("StoryView_ShowsEachPhaseWithItsAccountingAndItsCalls", () => {
    render(
      <PhaseAccount
        phase={PHASE}
        calls={[call(1, 151_040, 3_886), call(2, 356_632, 0)]}
        commands={COMMANDS}
      />,
    );

    const account = screen.getByTestId("phase-account");
    expect(account).toHaveAttribute("data-phase", "p1");
    expect(account).toHaveTextContent("Phase p1");

    // The phase's own accounting — steps, wall clock, calls, the sizes that matter.
    const numbers = within(account).getByTestId("phase-numbers");
    expect(numbers).toHaveTextContent("Steps");
    expect(numbers).toHaveTextContent("357k");
    expect(numbers).toHaveTextContent("1 non-zero");

    // Its commands, WITH exit codes — the evidence behind "verification failed".
    const rows = within(account).getAllByTestId("command-row");
    expect(rows).toHaveLength(2);
    expect(rows[0]).toHaveAttribute("data-exit", "0");
    expect(rows[1]).toHaveAttribute("data-exit", "1");
    expect(rows[1]).toHaveTextContent("dotnet test");

    // Its calls, with their sizes and durations — plot and table alike.
    expect(within(account).getByTestId("call-size-plot")).toHaveAttribute("data-calls", "2");
    expect(within(account).getAllByTestId("call-row")).toHaveLength(2);
  });

  it("StoryView_APhaseWhereNothingFailed_SaysSo_RatherThanShowingGreen", () => {
    render(
      <PhaseAccount
        phase={{ ...PHASE, failedCommands: 0, calls: stats({ failedCalls: 0 }) }}
        calls={[call(1, 1000, 900)]}
        commands={[COMMANDS[0]]}
      />,
    );

    expect(screen.getByTestId("phase-verdict")).toHaveTextContent("nothing failed");
  });

  it("TicketStatistics_ComeFromTheTrail_NotFromCounters", () => {
    const statistics: RunStatistics = {
      totals: stats(), totalDurationMs: 2_040_000,
      phases: [PHASE, { ...PHASE, phaseId: "p2" }],
      calls: [], commands: [], truncated: false,
    };

    render(<TicketStatistics statistics={statistics} acceptance={ACCEPTANCE} />);

    const panel = screen.getByTestId("ticket-statistics");
    expect(panel).toHaveTextContent("derived from the trail");
    expect(panel).toHaveTextContent("Phases");
    expect(panel).toHaveTextContent("1/2");
    expect(panel).toHaveTextContent("357k");
    // What a bound cut is a first-class number, not a footnote.
    expect(panel).toHaveTextContent("tool output a bound cut");
  });

  it("TicketStatistics_ATruncatedSeries_SaysWhichNumbersStillCoverTheWholeRun", () => {
    render(
      <TicketStatistics
        statistics={{
          totals: stats(), totalDurationMs: 1, phases: [], calls: [], commands: [], truncated: true,
        }}
        acceptance={null}
      />,
    );

    expect(screen.getByTestId("ticket-statistics-truncated")).toHaveTextContent(
      "The totals cover the whole run",
    );
  });
});
