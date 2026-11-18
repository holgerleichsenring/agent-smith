// 2026-11-18-e06bb: a decision as a title and a reason. An agent often logs both in one markdown string,
// "**Title**: reason"; a structured one carries them apart. A plain name without a reason is a title alone.

export interface DecisionParts {
  title: string;
  reason: string | null;
}

const BOLD_LEAD = /^\s*\*\*(.+?)\*\*\s*[:—–-]?\s*([\s\S]*)$/;

export function decisionParts(name: string, reason: string | null | undefined): DecisionParts {
  const m = BOLD_LEAD.exec(name);
  const title = m ? m[1].trim() : name.trim();
  const rest = [m ? m[2].trim() : "", (reason ?? "").trim()].filter(Boolean).join(" — ");
  return { title, reason: rest || null };
}
