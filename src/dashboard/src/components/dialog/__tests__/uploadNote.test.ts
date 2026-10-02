import { describe, expect, it } from "vitest";
import { ApiRefusal, ApiResponseError } from "@/lib/apiResponse";
import { refusedNote, storedNote } from "../uploadNote";

// 2026-10-02-0d72: what an upload leaves at the composer.
describe("uploadNote", () => {
  const stored = { setId: "s", name: "site", files: 3, bytes: 9, at: "2026-10-02T10:00:00Z" };

  const none = { leftOut: [], leftOutCount: 0, credentialFiles: [] };

  it("StoredNote_EveryFileKeptNoCredentials_IsNull", () => {
    expect(storedNote({ ...stored, ...none })).toBeNull();
  });

  it("StoredNote_MoreLeftOutThanListed_SaysWhyAndHowManyMore", () => {
    const note = storedNote({
      ...stored, ...none, leftOut: [{ path: "site/.venv/", reason: "rebuildable" }], leftOutCount: 4,
    })!;

    expect(note.tone).toBe("stored");
    expect(note.text).toBe("Stored 3 files of 'site'. Left out: site/.venv/ (rebuildable) and 3 more.");
  });

  it("StoredNote_CredentialFiles_SaysTheModelReadsThem", () => {
    expect(storedNote({ ...stored, ...none, credentialFiles: ["site/.env"] })!.text)
      .toBe("Stored 3 files of 'site'. The model and any run it starts can read site/.env.");
  });

  // 2026-10-02-075db: what the selection card left out joins the server's answer.
  it("StoredNote_WhatThePickLeftOut_IsNamedBesideAKeptSet", () => {
    const note = storedNote({ ...stored, ...none }, { count: 3, summary: [".venv/ (rebuildable)", "2 files under src/ (.gitignore)"] });

    expect(note!.tone).toBe("stored");
    expect(note!.text).toContain("Not sent (3 files): .venv/ (rebuildable), 2 files under src/ (.gitignore).");
  });

  it("StoredNote_APickThatLeftNothingOut_StaysNull", () => {
    expect(storedNote({ ...stored, ...none }, { count: 0, summary: [] })).toBeNull();
  });

  it("RefusedNote_AStatedReason_IsTheNote", () => {
    expect(refusedNote(new ApiResponseError("/x", 400, "HTTP 400 — why", "why"))).toEqual({ tone: "refused", text: "why" });
  });

  it("RefusedNote_NoReason_SaysWhatFailed", () => {
    expect(refusedNote(new ApiResponseError("/x", 500, "HTTP 500")).text).toBe("The upload failed: /x: HTTP 500");
    expect(refusedNote(new ApiRefusal("/x", 403, "permission", [])).text).toContain("refused");
  });
});
