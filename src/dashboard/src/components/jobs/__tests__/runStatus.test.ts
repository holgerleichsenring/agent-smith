import { describe, it, expect } from "vitest";
import { toNodeStatus } from "../runStatus";

describe("toNodeStatus", () => {
  it("toNodeStatus_KnownStatuses_MapToNodeStatus", () => {
    expect(toNodeStatus("success")).toBe("ok");
    expect(toNodeStatus("failed")).toBe("fail");
    expect(toNodeStatus("error")).toBe("fail");
    expect(toNodeStatus("running")).toBe("run");
    // p0259: cancelled is its own node status, not "fail" and not "wait".
    expect(toNodeStatus("cancelled")).toBe("cancel");
    expect(toNodeStatus("CANCELLED")).toBe("cancel");
  });

  it("toNodeStatus_UnknownStatus_MapsToWait", () => {
    expect(toNodeStatus("pending")).toBe("wait");
    expect(toNodeStatus("")).toBe("wait");
  });

  it("toNodeStatus_Queued_MapsToItsOwnTone", () => {
    // p0320d: queued is a first-class amber state, distinct from a stalled "wait".
    expect(toNodeStatus("queued")).toBe("queued");
  });
});
