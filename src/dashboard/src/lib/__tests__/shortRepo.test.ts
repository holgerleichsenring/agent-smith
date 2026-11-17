import { describe, expect, it } from "vitest";
import { shortRepo } from "../shortRepo";

// 2026-11-17-d6fbe: restored from the deleted attemptChips suite (2026-10-16-7ed3b). A repository's short name for a
// tight place — after the last '/', then the last '.' segment — as YourMoveBox, ThreadMessages and attemptFlowLayout
// draw it.

describe("shortRepo", () => {
  it("shortRepo_DottedAndSlashedNames_KeepTheLastSegment", () => {
    expect(shortRepo("Sample.App.Server")).toBe("Server");
    expect(shortRepo("org/Sample.App.Client")).toBe("Client");
    expect(shortRepo("o/api")).toBe("api");
    expect(shortRepo("api")).toBe("api");
  });
});
