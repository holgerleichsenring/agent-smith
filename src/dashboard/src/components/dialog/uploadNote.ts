import { ApiResponseError, refusalIn } from "@/lib/apiResponse";
import type { SpecDialogReferenceUpload } from "@/types/spec-dialog";
import type { PickLeftOut } from "./referenceSelection";

// 2026-10-02-0d72: what an upload leaves beside the composer that made it. A refused upload is a
// sentence about the files the operator picked, not a failure of the page — it used to land on
// the page-wide "could not be rendered" panel as a bare "HTTP 400". And an upload stored without
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

/**
 * 2026-10-02-075da: the note for a stored upload — what was left out and why, and which stored
 * files commonly hold credentials, because the model and a run will now read them. Null when
 * there is nothing to say.
 * 2026-10-02-075db: and what the selection card left out before sending, first — the pick's
 * left-out files are as much the operator's to know about as the server's.
 */
export function storedNote(answer: SpecDialogReferenceUpload, notSent?: PickLeftOut): UploadNote | null {
  const parts: string[] = [];
  if (notSent && notSent.count > 0) {
    const files = notSent.count === 1 ? "1 file" : `${notSent.count} files`;
    parts.push(`Not sent (${files}): ${notSent.summary.join(", ")}.`);
  }
  if (answer.leftOutCount > 0) {
    const listed = answer.leftOut.map((e) => `${e.path} (${e.reason})`).join(", ");
    const more = answer.leftOutCount > answer.leftOut.length
      ? ` and ${answer.leftOutCount - answer.leftOut.length} more`
      : "";
    parts.push(`Left out: ${listed}${more}.`);
  }
  if (answer.credentialFiles.length > 0)
    parts.push(`The model and any run it starts can read ${answer.credentialFiles.join(", ")}.`);
  if (parts.length === 0) return null;
  const files = answer.files === 1 ? "1 file" : `${answer.files} files`;
  return { tone: "stored", text: [`Stored ${files} of '${answer.name}'.`, ...parts].join(" ") };
}
