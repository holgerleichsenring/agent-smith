import { describe, expect, it } from "vitest";
import { ApiRefusal, ApiResponseError } from "@/lib/apiResponse";
import { refusedNote, skippedNote } from "../uploadNote";

// 2026-10-02-0d72: what an upload leaves at the composer.
describe("uploadNote", () => {
  const stored = { setId: "s", name: "site", files: 3, bytes: 9, at: "2026-10-02T10:00:00Z" };

  it("SkippedNote_EveryFileKept_IsNull", () => {
    expect(skippedNote({ ...stored, skipped: [], skippedCount: 0 })).toBeNull();
  });

  it("SkippedNote_MoreSkippedThanListed_SaysHowManyMore", () => {
    const note = skippedNote({ ...stored, skipped: ["site/a.xml"], skippedCount: 4 })!;

    expect(note.tone).toBe("stored");
    expect(note.text).toBe("Stored 3 files of 'site'. Skipped 4 that are not part of a website: site/a.xml and 3 more.");
  });

  it("RefusedNote_AStatedReason_IsTheNote", () => {
    expect(refusedNote(new ApiResponseError("/x", 400, "HTTP 400 — why", "why"))).toEqual({ tone: "refused", text: "why" });
  });

  it("RefusedNote_NoReason_SaysWhatFailed", () => {
    expect(refusedNote(new ApiResponseError("/x", 500, "HTTP 500")).text).toBe("The upload failed: /x: HTTP 500");
    expect(refusedNote(new ApiRefusal("/x", 403, "permission", [])).text).toContain("refused");
  });
});
