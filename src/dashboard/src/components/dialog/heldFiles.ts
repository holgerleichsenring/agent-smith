import type { SpecDialogHeldContent } from "@/types/spec-dialog";
import type { SelectionEntry } from "./referenceSelection";

// 2026-10-09-86e1: which picked files the conversation already holds, by content — the same
// SHA-256 the server stores for every file of a set, so a file is found under any folder and any
// name. Checked in the browser so the operator sees it while choosing; the server refuses a pick
// it holds entirely either way, and is the only check a single .zip gets (it is unpacked there).
// Without crypto.subtle (plain http off localhost) nothing can be hashed here: the result is null.

export type HeldLookup = Map<File, SpecDialogHeldContent>;

/** The picked files a set of the conversation holds, or null when the page cannot hash. */
export async function heldIn(files: File[], held: SpecDialogHeldContent[]): Promise<HeldLookup | null> {
  const subtle = globalThis.crypto?.subtle;
  if (!subtle) return null;
  const byHash = new Map(held.map((h) => [h.sha256, h]));
  const found: HeldLookup = new Map();
  if (byHash.size === 0) return found;
  for (const file of files) {
    // A view made here: a buffer from another realm (jsdom's FileReader) is refused by Node's digest.
    const hex = hexOf(await subtle.digest("SHA-256", new Uint8Array(await bytesOf(file))));
    const holder = byHash.get(hex);
    if (holder) found.set(file, holder);
  }
  return found;
}

/** The entries with what is already held marked: a loose file — or a folder ALL of whose files
 *  are held — starts unticked and says where it is; the operator may still tick it. A folder says
 *  how many of its files are held, and one with any new file starts ticked and goes whole. */
export function markHeld(entries: SelectionEntry[], held: HeldLookup): SelectionEntry[] {
  return entries.map((entry) => {
    const all = [...entry.kept, ...entry.ignored];
    const hits = all.filter((file) => held.has(file));
    if (hits.length === 0) return entry;
    const where = [...new Set(hits.map((file) => `'${held.get(file)!.name}'`))].join(", ");
    const unticked = { ...entry, ticked: false, reason: `already uploaded in ${where}` };
    if (!entry.name.endsWith("/")) return unticked;
    const count = `${hits.length} of ${all.length} already uploaded in ${where}`;
    return hits.length === all.length ? { ...unticked, reason: count } : { ...entry, held: count };
  });
}

function hexOf(digest: ArrayBuffer): string {
  return [...new Uint8Array(digest)].map((b) => b.toString(16).padStart(2, "0")).join("");
}

/** A file's bytes; FileReader where Blob.arrayBuffer is missing (jsdom's File has none). */
function bytesOf(file: File): Promise<ArrayBuffer> {
  if (typeof file.arrayBuffer === "function") return file.arrayBuffer();
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(reader.result as ArrayBuffer);
    reader.onerror = () => reject(reader.error);
    reader.readAsArrayBuffer(file);
  });
}
