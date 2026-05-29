import { describe, it, expect } from "vitest";
import {
  defaultDimensionState,
  parseDimensionsFromQuery,
  writeDimensionsToQuery,
} from "../dimensionFilterQuery";

describe("dimensionFilterQuery", () => {
  it("DimensionFilterQuery_RoundTripsThroughUrl", () => {
    const state = {
      agent: new Set(["UploadAuditor", "PathTrace"]),
      sandbox: new Set(["api-repo"]),
      pipeline: new Set(["fix-bug"]),
      activity: new Set(["scan headers"]),
    };

    const params = writeDimensionsToQuery(state, new URLSearchParams());
    const round = parseDimensionsFromQuery(params);

    expect([...round.agent].sort()).toEqual(["PathTrace", "UploadAuditor"]);
    expect([...round.sandbox]).toEqual(["api-repo"]);
    expect([...round.pipeline]).toEqual(["fix-bug"]);
    expect([...round.activity]).toEqual(["scan headers"]);
  });

  it("empty defaults serialise to no params", () => {
    const params = writeDimensionsToQuery(defaultDimensionState(), new URLSearchParams());
    expect(params.toString()).toBe("");
  });

  it("absent params parse to empty sets", () => {
    const round = parseDimensionsFromQuery(new URLSearchParams("foo=bar"));
    expect(round.agent.size).toBe(0);
    expect(round.sandbox.size).toBe(0);
    expect(round.pipeline.size).toBe(0);
    expect(round.activity.size).toBe(0);
  });
});
