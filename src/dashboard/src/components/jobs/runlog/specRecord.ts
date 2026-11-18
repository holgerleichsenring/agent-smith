import { parse } from "yaml";

// 2026-11-18-e06bc: the spec a run executed, read from its YAML into the parts a reader looks for — goal, scope, steps,
// tests, done criteria and the outcome — and every other key as plain label/value lines. The outcome's delivered text is
// the account SpecAccountRenderer writes: "**repo**" then "- [x] criterion — `a; b`" rows, read back into rows here.

export interface SpecStep {
  id: string;
  action: string;
}

export type DeliveredMark = "met" | "unmet" | "na";

export type DeliveredRow =
  | { kind: "criterion"; mark: DeliveredMark; text: string; citation: string | null; note: string | null }
  | { kind: "line"; text: string };

export interface DeliveredAccount {
  repo: string | null;
  rows: DeliveredRow[];
}

export interface SpecRecordModel {
  goal: string | null;
  scope: Array<{ label: string; lines: string[] }>;
  steps: SpecStep[];
  tests: string[];
  done: string[];
  run: string | null;
  delivered: DeliveredAccount[];
  other: Array<{ label: string; lines: string[] }>;
}

const KNOWN = new Set(["spec", "goal", "scope", "steps", "tests", "done", "outcome"]);

function text(value: unknown): string {
  if (value === null || value === undefined) return "";
  if (typeof value === "string") return value.trim();
  if (typeof value === "number" || typeof value === "boolean") return String(value);
  if (Array.isArray(value)) return value.map(text).filter(Boolean).join(", ");
  return Object.values(value as Record<string, unknown>).map(text).filter(Boolean).join(" — ");
}

/** A value as lines: a list item per line, an object's keys as "key: value", a scalar as itself. */
export function toLines(value: unknown): string[] {
  if (value === null || value === undefined) return [];
  if (Array.isArray(value)) return value.map(text).filter(Boolean);
  if (typeof value === "object") {
    return Object.entries(value as Record<string, unknown>).map(([k, v]) => `${k}: ${text(v)}`).filter((l) => !l.endsWith(": "));
  }
  const t = text(value);
  return t ? [t] : [];
}

function stepsOf(value: unknown): SpecStep[] {
  if (!Array.isArray(value)) return [];
  return value.map((s, i) => {
    if (s && typeof s === "object") {
      const o = s as Record<string, unknown>;
      return { id: text(o.id) || String(i + 1), action: text(o.action ?? o.what ?? o.description ?? Object.values(o).slice(1)) };
    }
    return { id: String(i + 1), action: text(s) };
  });
}

function scopeOf(value: unknown): Array<{ label: string; lines: string[] }> {
  if (value && typeof value === "object" && !Array.isArray(value)) {
    return Object.entries(value as Record<string, unknown>).map(([k, v]) => ({ label: `scope ${k}`, lines: toLines(v) }))
      .filter((b) => b.lines.length > 0);
  }
  const lines = toLines(value);
  return lines.length > 0 ? [{ label: "scope", lines }] : [];
}

const ROW = /^- \[(.)\]\s+(.*)$/;

function criterionRow(mark: string, body: string): DeliveredRow {
  const m: DeliveredMark = mark === "x" || mark === "X" ? "met" : mark === "~" ? "na" : "unmet";
  const cited = /^(.*?)\s+—\s+`([^`]*)`\s*$/.exec(body);
  if (cited) return { kind: "criterion", mark: m, text: cited[1], citation: cited[2], note: null };
  const notApplicable = /^(.*?)\s+—\s+(not applicable:.*?)(?:\s+\(`([^`]*)`\))?\s*$/.exec(body);
  if (notApplicable) return { kind: "criterion", mark: m, text: notApplicable[1], citation: notApplicable[3] ?? null, note: notApplicable[2] };
  const dash = body.lastIndexOf(" — ");
  if (dash > 0) return { kind: "criterion", mark: m, text: body.slice(0, dash), citation: null, note: body.slice(dash + 3).trim() };
  return { kind: "criterion", mark: m, text: body, citation: null, note: null };
}

/** The delivered account's markdown read back into accounts per repository. */
export function deliveredAccounts(markdown: string): DeliveredAccount[] {
  const accounts: DeliveredAccount[] = [];
  let current: DeliveredAccount | null = null;
  const into = () => {
    if (!current) {
      current = { repo: null, rows: [] };
      accounts.push(current);
    }
    return current;
  };
  for (const raw of markdown.split("\n")) {
    const line = raw.trim();
    if (!line || line.startsWith("#")) continue;
    const repo = /^\*\*(.+)\*\*$/.exec(line);
    if (repo) {
      current = { repo: repo[1], rows: [] };
      accounts.push(current);
      continue;
    }
    const row = ROW.exec(line);
    into().rows.push(row ? criterionRow(row[1], row[2]) : { kind: "line", text: line.replace(/^- /, "") });
  }
  return accounts;
}

/** The record as parts, or null when it is not a YAML mapping with a goal or a spec id — then it is shown as a document. */
export function parseSpecRecord(record: string): SpecRecordModel | null {
  let doc: unknown;
  try {
    doc = parse(record);
  } catch {
    return null;
  }
  if (!doc || typeof doc !== "object" || Array.isArray(doc)) return null;
  const o = doc as Record<string, unknown>;
  if (o.goal === undefined && o.spec === undefined) return null;
  const outcome = (o.outcome && typeof o.outcome === "object" ? o.outcome : {}) as Record<string, unknown>;
  const delivered = typeof outcome.delivered === "string" ? deliveredAccounts(outcome.delivered) : [];
  return {
    goal: text(o.goal) || null,
    scope: scopeOf(o.scope),
    steps: stepsOf(o.steps),
    tests: toLines(o.tests),
    done: toLines(o.done),
    run: text(outcome.run) || null,
    delivered,
    other: [
      ...Object.entries(o).filter(([k]) => !KNOWN.has(k)),
      ...Object.entries(outcome).filter(([k]) => k !== "run" && k !== "delivered").map(([k, v]) => [`outcome ${k}`, v] as const),
    ].map(([k, v]) => ({ label: k.replace(/[_-]/g, " "), lines: toLines(v) })).filter((b) => b.lines.length > 0),
  };
}
