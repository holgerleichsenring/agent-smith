import { ApiResponseError, refusalIn } from "@/lib/apiResponse";
import type { SpecDialogReferenceUpload } from "@/types/spec-dialog";

// 2026-10-02-0d72: what an upload leaves beside the composer that made it. A refused upload is a
// sentence about the files the operator picked, not a failure of the page — it used to land on
// the page-wide "could not be rendered" panel as a bare "HTTP 400". And a website stored without
// some of its files says which, so nothing is left out silently.

export interface UploadNote {
  /** "refused" when nothing was stored; "stored" when the set was kept and some files were not. */
  tone: "refused" | "stored";
  text: string;
}

/** The note for an upload that threw: the server's own reason where it gave one. */
export function refusedNote(thrown: unknown): UploadNote {
  const refusal = refusalIn(thrown);
  if (refusal) return { tone: "refused", text: `The upload was refused: ${refusal.message}` };
  if (thrown instanceof ApiResponseError && thrown.reason) return { tone: "refused", text: thrown.reason };
  const message = thrown instanceof Error ? thrown.message : String(thrown);
  return { tone: "refused", text: `The upload failed: ${message}` };
}

/** The note for a stored website, or null when every file it carried was kept. */
export function skippedNote(answer: SpecDialogReferenceUpload): UploadNote | null {
  if (!answer.skippedCount) return null;
  const listed = answer.skipped.join(", ");
  const more = answer.skippedCount > answer.skipped.length
    ? ` and ${answer.skippedCount - answer.skipped.length} more`
    : "";
  const files = answer.files === 1 ? "1 file" : `${answer.files} files`;
  return {
    tone: "stored",
    text: `Stored ${files} of '${answer.name}'. Skipped ${answer.skippedCount} that are not part `
      + `of a website: ${listed}${more}.`,
  };
}
