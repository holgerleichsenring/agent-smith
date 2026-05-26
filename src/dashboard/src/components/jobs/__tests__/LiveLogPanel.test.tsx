import { render, screen, act, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { LiveLogPanel } from "../LiveLogPanel";

type Listener = (e: MessageEvent) => void;

class FakeEventSource {
  static instances: FakeEventSource[] = [];
  onopen: (() => void) | null = null;
  onerror: (() => void) | null = null;
  private listeners = new Map<string, Listener[]>();

  constructor(url: string) {
    FakeEventSource.instances.push(this);
    void url;
  }

  addEventListener(name: string, listener: Listener) {
    const arr = this.listeners.get(name) ?? [];
    arr.push(listener);
    this.listeners.set(name, arr);
  }

  emit(name: string, data: object) {
    const e = { data: JSON.stringify(data) } as MessageEvent;
    for (const l of this.listeners.get(name) ?? []) l(e);
  }

  close() {}
}

beforeEach(() => {
  FakeEventSource.instances = [];
  vi.stubGlobal("EventSource", FakeEventSource);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("LiveLogPanel", () => {
  it("renders progress event into the progress bar", async () => {
    render(<LiveLogPanel jobId="job-1" />);
    const src = FakeEventSource.instances[0];
    act(() => src.emit("progress", { step: 2, total: 5, command_name: "Plan" }));
    await waitFor(() => expect(screen.getByTestId("progress-bar")).toBeInTheDocument());
    expect(screen.getByTestId("progress-bar")).toHaveAttribute("data-step", "2");
    expect(screen.getByTestId("progress-bar")).toHaveAttribute("data-total", "5");
  });

  it("renders observation event as severity-badged card", async () => {
    render(<LiveLogPanel jobId="job-1" />);
    const src = FakeEventSource.instances[0];
    act(() =>
      src.emit("skill_observation", {
        severity: "blocking",
        category: "security",
        body_preview: "SQL injection in /login",
        source_ref: "src/Auth.cs:42",
      }),
    );
    await waitFor(() => expect(screen.getByTestId("observation-card")).toBeInTheDocument());
    expect(screen.getByTestId("observation-card")).toHaveAttribute("data-severity", "blocking");
  });

  it("renders done event as terminal card", async () => {
    render(<LiveLogPanel jobId="job-1" />);
    const src = FakeEventSource.instances[0];
    act(() => src.emit("done", { run_id: "job-1", summary: "shipped", pr_url: "https://example/pr" }));
    await waitFor(() => expect(screen.getByTestId("done-card")).toBeInTheDocument());
  });
});
