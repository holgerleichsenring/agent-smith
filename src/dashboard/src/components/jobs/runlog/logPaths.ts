// 2026-11-18-e06ba: a path in the log is read for where it is, not spelled out — its first segment (the repository or
// root folder) and its last (the file), the middle elided. A full path stays one hover away as the element's title.

/** True for a single token that names a path: it holds a slash and no whitespace. A command is not a path. */
export function isPathLike(text: string): boolean {
  const t = text.trim();
  return t.length > 0 && !/\s/.test(t) && t.includes("/");
}

/** "repo/a/b/File.cs" -> "repo/…/File.cs"; a path of two segments or a non-path stays as it is. */
export function shortPath(path: string): string {
  const t = path.trim();
  if (!isPathLike(t)) return t;
  const parts = t.split("/").filter(Boolean);
  if (parts.length <= 2) return t;
  return `${parts[0]}/…/${parts[parts.length - 1]}`;
}

/** The items of a "; "-separated list, trimmed, empties dropped. */
export function splitList(text: string): string[] {
  return text.split(/;\s*/).map((s) => s.trim()).filter(Boolean);
}

/** A plan target as one short line: each "; "-separated item shortened, joined by ", ". */
export function shortTargets(target: string): string {
  return splitList(target).map(shortPath).join(", ");
}
