import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { TraceReader } from "../TraceReader";

// p0423b: a traced run's conversation is readable entry by entry, in call order. A run that
// was NOT traced leaves the reader absent, never broken — an empty reader promising content
// nobody recorded is worse than no reader at all.

const fetchMock = vi.fn();

const ENTRIES = [
  { sequence: 1, label: "prompt", chars: 151_040 },
  { sequence: 2, label: "answer", chars: 3_886 },
  { sequence: 3, label: "tool", chars: 100_048 },
];

beforeEach(() => {
  fetchMock.mockReset();
  vi.stubGlobal("fetch", fetchMock);
});

function serve(entries: typeof ENTRIES) {
  fetchMock.mockImplementation(async (url: string) => {
    if (/\/trace\/\d+\//.test(String(url))) {
      return { ok: true, json: async () => ({ sequence: 2, label: "answer", content: "the answer text" }) };
    }
    return { ok: true, json: async () => ({ entries }) };
  });
}

describe("TraceReader", () => {
  it("TraceReader_ShowsEntriesInCallOrder", async () => {
    serve(ENTRIES);
    render(<TraceReader runId="r1" />);

    const reader = await screen.findByTestId("trace-reader");
    expect(reader).toHaveAttribute("data-entries", "3");
    const rows = screen.getAllByTestId("trace-entry");
    expect(rows.map((r) => r.getAttribute("data-sequence"))).toEqual(["1", "2", "3"]);
    expect(rows[0]).toHaveTextContent("0001");
    expect(rows[0]).toHaveTextContent("Prompt");
    // The list carries SIZES, never content — a recorded prompt reaches megabytes.
    expect(rows[0]).toHaveTextContent("151k");
    expect(screen.getByTestId("trace-entry-none")).toBeInTheDocument();
  });

  it("TraceReader_ReadsOneEntryAtATime", async () => {
    serve(ENTRIES);
    render(<TraceReader runId="r1" />);

    const rows = await screen.findAllByTestId("trace-entry");
    fireEvent.click(rows[1]);

    await waitFor(() =>
      expect(screen.getByTestId("trace-entry-body")).toHaveTextContent("the answer text"));
    expect(screen.getByTestId("trace-entry-body")).toHaveAttribute("data-sequence", "2");
    const entryFetches = fetchMock.mock.calls
      .map((c) => String(c[0]))
      .filter((u) => /\/trace\/\d+\//.test(u));
    expect(entryFetches).toEqual(["/api/runs/r1/trace/2/answer"]);
  });

  it("TraceReader_AnUntracedRun_IsAbsent_NotBroken", async () => {
    serve([]);
    render(<TraceReader runId="r1" />);

    expect(await screen.findByTestId("trace-reader-absent")).toHaveTextContent(
      "This run was not traced",
    );
    expect(screen.queryByTestId("trace-reader")).not.toBeInTheDocument();
  });
});
