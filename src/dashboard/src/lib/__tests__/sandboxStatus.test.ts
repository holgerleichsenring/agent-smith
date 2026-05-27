import { describe, it, expect } from "vitest";
import { EventType, type RunEvent } from "@/types/hub-events";
import { paletteFor, sandboxStatusColor } from "../sandboxStatus";

const RUN_ID = "2026-05-27T20-00-00-eeee";
const TS = "2026-05-27T20:00:00.000Z";

function created(repo: string): RunEvent {
  return {
    runId: RUN_ID,
    type: EventType.SandboxCreated,
    timestamp: TS,
    repo,
    image: "img:latest",
    language: "csharp",
  };
}

function disposed(repo: string): RunEvent {
  return {
    runId: RUN_ID,
    type: EventType.SandboxDisposed,
    timestamp: TS,
    repo,
    exitCode: 0,
  };
}

function stepFinished(status: "success" | "failed", reason: string | null = null): RunEvent {
  return {
    runId: RUN_ID,
    type: EventType.StepFinished,
    timestamp: TS,
    stepIndex: 1,
    status,
    durationMs: 100,
    reason,
  };
}

function runFinished(status: "success" | "failed"): RunEvent {
  return {
    runId: RUN_ID,
    type: EventType.RunFinished,
    timestamp: TS,
    status,
    prUrl: null,
    summary: "",
    finishedAt: TS,
  };
}

describe("sandboxStatusColor", () => {
  it("before SandboxCreated returns waiting", () => {
    expect(sandboxStatusColor([], "server")).toBe("waiting");
  });

  it("after SandboxCreated returns running", () => {
    expect(sandboxStatusColor([created("server")], "server")).toBe("running");
  });

  it("disposed without run-finished returns running (still in-flight)", () => {
    expect(sandboxStatusColor([created("server"), disposed("server")], "server")).toBe("running");
  });

  it("disposed AND run succeeded returns success", () => {
    const events = [created("server"), disposed("server"), runFinished("success")];
    expect(sandboxStatusColor(events, "server")).toBe("success");
  });

  it("a failed step pins the status at failed regardless of dispose", () => {
    const events = [created("server"), stepFinished("failed", "boom"), disposed("server"), runFinished("failed")];
    expect(sandboxStatusColor(events, "server")).toBe("failed");
  });

  it("disposed with a non-success run (e.g. cancelled) without step-fail returns disposed", () => {
    const events = [created("server"), disposed("server"), runFinished("failed")];
    expect(sandboxStatusColor(events, "server")).toBe("disposed");
  });

  it("per-repo isolation — created server, not created client", () => {
    expect(sandboxStatusColor([created("server")], "client")).toBe("waiting");
  });
});

describe("paletteFor", () => {
  it("running is amber + pulses", () => {
    const p = paletteFor("running");
    expect(p.pulse).toBe(true);
    expect(p.fill).toContain("amber");
  });

  it("success uses emerald (green = done)", () => {
    const p = paletteFor("success");
    expect(p.fill).toContain("emerald");
    expect(p.pulse).toBe(false);
  });

  it("failed uses rose", () => {
    const p = paletteFor("failed");
    expect(p.fill).toContain("rose");
  });
});
