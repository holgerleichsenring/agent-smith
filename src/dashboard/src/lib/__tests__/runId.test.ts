import { describe, expect, it } from "vitest";
import { shortRunId } from "../runId";

// p0441: the operator saw `#2026-08-` in the header — the one part of a run id that is the
// same for every run of the day.
describe("shortRunId", () => {
  it("keeps the hash the run is actually called by", () => {
    expect(shortRunId("2026-08-17T21-30-46-a98c")).toBe("a98c");
  });

  it("tells two runs of the same minute apart", () => {
    expect(shortRunId("2026-08-17T21-30-46-a98c")).not.toBe(
      shortRunId("2026-08-17T21-30-46-6632"),
    );
  });

  it("falls back to the END of an id that does not follow the convention", () => {
    expect(shortRunId("abcdefghijklmnopqrstuvwxyz")).toBe("stuvwxyz");
  });

  it("leaves an id alone when its tail would identify nothing", () => {
    // A one-character tail is not a name; the whole short id is more use than "1".
    expect(shortRunId("run-1")).toBe("run-1");
    expect(shortRunId("short")).toBe("short");
  });

  it("survives an empty id", () => {
    expect(shortRunId("")).toBe("");
  });
});
