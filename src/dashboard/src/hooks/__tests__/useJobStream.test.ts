import { renderHook, act, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { useJobStream } from "../useJobStream";

type Listener = (e: MessageEvent) => void;

class FakeEventSource {
  static instances: FakeEventSource[] = [];
  readonly url: string;
  onopen: (() => void) | null = null;
  onerror: (() => void) | null = null;
  private listeners = new Map<string, Listener[]>();
  closed = false;

  constructor(url: string) {
    this.url = url;
    FakeEventSource.instances.push(this);
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

  triggerOpen() { this.onopen?.(); }
  triggerError() { this.onerror?.(); }
  close() { this.closed = true; }
}

beforeEach(() => {
  FakeEventSource.instances = [];
  vi.stubGlobal("EventSource", FakeEventSource);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("useJobStream", () => {
  it("emits a parsed progress event", async () => {
    const { result } = renderHook(() => useJobStream("job-1"));
    const src = FakeEventSource.instances[0];
    expect(src).toBeTruthy();
    act(() => src.triggerOpen());
    act(() => src.emit("progress", { step: 1, total: 3, command_name: "Boot" }));
    await waitFor(() => expect(result.current.events).toHaveLength(1));
    const ev = result.current.events[0];
    expect(ev.type).toBe("progress");
    if (ev.type === "progress") {
      expect(ev.step).toBe(1);
      expect(ev.total).toBe(3);
    }
  });

  it("sets reconnecting flag on error", async () => {
    const { result } = renderHook(() => useJobStream("job-1"));
    const src = FakeEventSource.instances[0];
    act(() => src.triggerError());
    await waitFor(() => expect(result.current.reconnecting).toBe(true));
  });

  it("closes stream on done event", async () => {
    const { result } = renderHook(() => useJobStream("job-1"));
    const src = FakeEventSource.instances[0];
    act(() => src.triggerOpen());
    act(() => src.emit("done", { run_id: "job-1", summary: "ok", pr_url: null }));
    await waitFor(() => expect(result.current.status).toBe("closed"));
    expect(src.closed).toBe(true);
  });
});
