import type { SpecDialogHeldContent } from "@/types/spec-dialog";
import { heldIn } from "./heldFiles";

/** 2026-10-09-86e1: the upload that already holds `file`, or null — also when nothing can be checked. */
export async function heldWhere(
  file: File,
  loadHeld?: () => Promise<SpecDialogHeldContent[]>,
): Promise<string | null> {
  if (!loadHeld) return null;
  try {
    return (await heldIn([file], await loadHeld()))?.get(file)?.name ?? null;
  } catch {
    // A failed read is no reason to hold the file back: the server checks again on arrival.
    return null;
  }
}
