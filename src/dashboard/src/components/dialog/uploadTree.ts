import type { SpecDialogReferenceFile } from "@/types/spec-dialog";

// 2026-10-09-86e1: an upload's files as folders of files, so a set of five hundred paths reads
// as a tree the operator opens where they look, not as one long list. A set whose files all sit
// under one folder starts INSIDE it — the folder is already the upload's name.

export interface UploadFolder {
  /** The folder's own name; "" for the root. */
  name: string;
  /** Its full path inside the set, with a trailing slash; "" for the root. */
  path: string;
  folders: UploadFolder[];
  files: (SpecDialogReferenceFile & { name: string })[];
  /** How many files it holds at every depth. */
  count: number;
}

/** A set holding at most this many files opens every folder; a larger one shows its first level closed. */
export const OPEN_ALL_UP_TO = 40;

export function treeOf(files: SpecDialogReferenceFile[]): UploadFolder {
  const root: UploadFolder = { name: "", path: "", folders: [], files: [], count: 0 };
  for (const file of [...files].sort((a, b) => a.path.localeCompare(b.path))) {
    const parts = file.path.split("/");
    let at = root;
    at.count++;
    for (const part of parts.slice(0, -1)) {
      at = folderIn(at, part);
      at.count++;
    }
    at.files.push({ ...file, name: parts.at(-1) ?? file.path });
  }
  return root.files.length === 0 && root.folders.length === 1 ? root.folders[0] : root;
}

function folderIn(parent: UploadFolder, name: string): UploadFolder {
  const found = parent.folders.find((folder) => folder.name === name);
  if (found) return found;
  const made: UploadFolder = { name, path: `${parent.path}${name}/`, folders: [], files: [], count: 0 };
  parent.folders.push(made);
  return made;
}

/** The folders that start open: every one in a small set, none in a large one. */
export function initiallyOpen(root: UploadFolder): Set<string> {
  const open = new Set<string>();
  if (root.count > OPEN_ALL_UP_TO) return open;
  const walk = (folder: UploadFolder) => folder.folders.forEach((child) => {
    open.add(child.path);
    walk(child);
  });
  walk(root);
  return open;
}
