import ignore from "ignore";

// 2026-10-02-075db: what a folder or file pick will send, worked out before anything is sent. The
// server reads at most a bounded body and refuses a larger one unread, so a folder carrying a
// 47 MB .venv was refused whatever the server would have kept — the choosing has to happen here.
// It works per FILE at every depth and is shown per top-level entry: a file is left out, with its
// reason, when it lies under a rebuildable folder, when it is over the per-file bound, or when a
// .gitignore of the pick ignores it — except a file that commonly holds credentials, which a
// .gitignore never leaves out: the operator ruled that a .env is information. The operator ticks
// what goes; ticking an entry that started unticked re-adds what a .gitignore left out under it.

/** The server's bounds on one set (ReferenceSetLimits.cs); the per-file bound is the set's. */
export const MAX_SET_BYTES = 25 * 1024 * 1024;
export const MAX_FILE_BYTES = MAX_SET_BYTES;
export const MAX_FILES = 500;

/** ReferenceRebuildableFolders.Names, held equal by a test that reads the C# file. */
export const REBUILDABLE_FOLDERS = [
  ".git", "node_modules", ".venv", "venv", "__pycache__", ".pytest_cache", ".mypy_cache",
  ".next", ".nuxt", ".gradle", ".terraform", "bower_components",
];

/** ReferenceCredentialFiles.Holds, mirrored: files that commonly hold credentials. */
export function holdsCredentials(path: string): boolean {
  const name = path.split("/").at(-1) ?? "";
  const lower = name.toLowerCase();
  return [".env", ".npmrc", "kubeconfig"].includes(lower)
    || lower.startsWith(".env.")
    || lower.endsWith(".env")
    || lower.endsWith(".pem")
    || lower.endsWith(".key")
    || name.startsWith("id_rsa")
    || (lower.startsWith("credentials") && lower.endsWith(".json"));
}

/** The rebuildable folder `path` lies under, as its path prefix, or null — FolderOf's mirror. */
export function rebuildableFolderOf(path: string): string | null {
  const segments = path.split("/");
  for (let i = 0; i < segments.length - 1; i++)
    if (REBUILDABLE_FOLDERS.includes(segments[i])) return `${segments.slice(0, i + 1).join("/")}/`;
  return null;
}

/** Why a file is not sent; null for a file that is. */
export type LeftOutReason = "rebuildable" | "over 25 MB" | ".gitignore";

export interface SelectionEntry {
  /** The top-level entry: a folder as "name/", a file as its name. */
  name: string;
  /** Sent when the entry is ticked as it starts. */
  kept: File[];
  /** Left out by a .gitignore; re-added when the operator ticks an entry that started unticked. */
  ignored: File[];
  /** Left out whatever is ticked — rebuildable or oversized — with their reasons. */
  excluded: { file: File; reason: LeftOutReason }[];
  /** Whether the entry starts ticked: it has a file that is kept. */
  ticked: boolean;
  /** Why an entry starts unticked: the reasons of its files. */
  reason: string | null;
}

/** What a pick left out on this side, joined to the server's answer once the set is stored. */
export interface PickLeftOut {
  count: number;
  /** One line per entry or reason, such as ".venv/ (rebuildable)" or "3 files under src/ (.gitignore)". */
  summary: string[];
}

/** The path a file goes under: the folder pick's relative path, or a plain pick's name. */
export function pathOf(file: File): string {
  return file.webkitRelativePath || file.name;
}

/** The pick's .gitignore files, read: their folder prefix ("" or "site/sub/") to their text. */
export async function gitignoresOf(files: File[]): Promise<Record<string, string>> {
  const found = files.filter((file) => pathOf(file).split("/").at(-1) === ".gitignore");
  const read = await Promise.all(found.map(async (file) => [folderOf(pathOf(file)), await textOf(file)] as const));
  return Object.fromEntries(read);
}

/** The pick's entries, each with what it sends and what it leaves out. */
export function entriesOf(files: File[], gitignores: Record<string, string>): SelectionEntry[] {
  const root = sharedRoot(files.map(pathOf));
  const ignores = Object.entries(gitignores)
    .sort(([a], [b]) => a.length - b.length)
    .map(([folder, text]) => ({ folder, rules: ignore().add(text) }));
  const entries = new Map<string, SelectionEntry>();
  for (const file of files) {
    const inside = pathOf(file).slice(root.length);
    const name = inside.includes("/") ? `${inside.split("/")[0]}/` : inside;
    const entry = entries.get(name) ?? { name, kept: [], ignored: [], excluded: [], ticked: false, reason: null };
    entries.set(name, entry);
    sortInto(entry, file, ignores);
  }
  return [...entries.values()].map(settle);
}

/** The files an entry sends with the operator's tick. */
export function sentBy(entry: SelectionEntry, ticked: boolean): File[] {
  if (!ticked) return [];
  return entry.ticked ? entry.kept : [...entry.kept, ...entry.ignored];
}

export interface SelectionTotals {
  files: number;
  bytes: number;
  /** What is over the bounds, as a sentence; null when the selection fits. */
  over: string | null;
}

/** The totals of what will actually be sent, against the set's bounds. */
export function totalsOf(sent: File[]): SelectionTotals {
  const bytes = sent.reduce((sum, file) => sum + file.size, 0);
  const over: string[] = [];
  if (bytes > MAX_SET_BYTES) over.push(`${megabytes(bytes - MAX_SET_BYTES)} over the 25 MB a set may hold`);
  if (sent.length > MAX_FILES) over.push(`${sent.length - MAX_FILES} files over the ${MAX_FILES} a set may hold`);
  return { files: sent.length, bytes, over: over.length > 0 ? over.join("; ") : null };
}

/** What the pick leaves out with these ticks: whole entries by name, the rest counted by reason. */
export function leftOutOf(entries: SelectionEntry[], ticks: boolean[]): PickLeftOut {
  let count = 0;
  const summary: string[] = [];
  entries.forEach((entry, i) => {
    const all = entry.kept.length + entry.ignored.length + entry.excluded.length;
    const sent = sentBy(entry, ticks[i]).length;
    if (sent === all) return;
    count += all - sent;
    if (sent === 0) summary.push(`${entry.name} (${entry.reason ?? "unticked"})`);
    else summary.push(...partsLeftOut(entry));
  });
  return { count, summary };
}

/** A size as the card states it. */
export function megabytes(bytes: number): string {
  return `${(bytes / (1024 * 1024)).toFixed(1).replace(/\.0$/, "")} MB`;
}

/** A ticked entry's left-out files by reason; what a .gitignore left out counts only where the
 *  entry started ticked, because ticking one that started unticked sent those files. */
function partsLeftOut(entry: SelectionEntry): string[] {
  const counts = new Map<string, number>();
  for (const { reason } of entry.excluded) counts.set(reason, (counts.get(reason) ?? 0) + 1);
  if (entry.ticked) counts.set(".gitignore", entry.ignored.length);
  return [...counts].filter(([, n]) => n > 0)
    .map(([reason, n]) => `${n === 1 ? "1 file" : `${n} files`} under ${entry.name} (${reason})`);
}

function sortInto(entry: SelectionEntry, file: File, ignores: { folder: string; rules: ReturnType<typeof ignore> }[]) {
  const path = pathOf(file);
  if (rebuildableFolderOf(path)) entry.excluded.push({ file, reason: "rebuildable" });
  else if (file.size > MAX_FILE_BYTES) entry.excluded.push({ file, reason: "over 25 MB" });
  else if (!holdsCredentials(path) && ignoredBy(path, ignores)) entry.ignored.push(file);
  else entry.kept.push(file);
}

function settle(entry: SelectionEntry): SelectionEntry {
  if (entry.kept.length > 0) return { ...entry, ticked: true };
  const reasons = new Set<string>(entry.excluded.map((e) => e.reason));
  if (entry.ignored.length > 0) reasons.add(".gitignore");
  return { ...entry, ticked: false, reason: [...reasons].join(", ") || null };
}

/** Whether the .gitignore files above `path` ignore it; a deeper file's negation wins. */
function ignoredBy(path: string, ignores: { folder: string; rules: ReturnType<typeof ignore> }[]): boolean {
  let ignored = false;
  for (const { folder, rules } of ignores) {
    if (!path.startsWith(folder)) continue;
    const verdict = rules.test(path.slice(folder.length));
    if (verdict.ignored) ignored = true;
    else if (verdict.unignored) ignored = false;
  }
  return ignored;
}

/** A folder pick's one root folder ("site/"), skipped so the entries are what is inside it. */
function sharedRoot(paths: string[]): string {
  const first = paths[0]?.split("/") ?? [];
  if (first.length < 2) return "";
  const root = `${first[0]}/`;
  return paths.every((path) => path.startsWith(root) && path.length > root.length) ? root : "";
}

function folderOf(path: string): string {
  const cut = path.lastIndexOf("/");
  return cut < 0 ? "" : path.slice(0, cut + 1);
}

/** A file's text; FileReader where Blob.text is missing (jsdom's File has no text()). */
function textOf(file: File): Promise<string> {
  if (typeof file.text === "function") return file.text();
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result ?? ""));
    reader.onerror = () => reject(reader.error);
    reader.readAsText(file);
  });
}
