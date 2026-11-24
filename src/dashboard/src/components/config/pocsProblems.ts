// 2026-11-24-f965: a refused PoCs save, placed at its fields. The server's message is "Invalid 'pocs' settings: "
// followed by PocsConfig.Problems() joined by "; " (ConfigSettingsAccess.Normalize). Every sentence opens with the key it
// names, so the sentences are split where the next key starts and each is placed by that key — the rules stay on the
// server; this only reads where its sentences belong.

export interface PocsProblems {
  agent: string[];
  tracker: string[];
  targets: string[];
  /** Per target index: field (the wire name after pocs.targets[]., e.g. "source_repo") → its sentences. */
  target: Record<number, Record<string, string[]>>;
}

const PREFIX = /^Invalid 'pocs' settings: /;

export const NO_POCS_PROBLEMS: PocsProblems = { agent: [], tracker: [], targets: [], target: {} };

/** The sentences of a refused save, or none when the message is not a PoCs settings refusal. */
export function pocsProblemSentences(message: string | null | undefined): string[] {
  if (!message || !PREFIX.test(message)) return [];
  return message.replace(PREFIX, "").split(/; (?=pocs\.)/).map((s) => s.trim()).filter((s) => s !== "");
}

/** Place each sentence at the field its key names; a target is found by the name the sentence quotes. */
export function placePocsProblems(message: string | null | undefined, targetNames: string[]): PocsProblems {
  const placed: PocsProblems = { agent: [], tracker: [], targets: [], target: {} };
  for (const sentence of pocsProblemSentences(message)) {
    const key = sentence.split(" ", 1)[0];
    if (key === "pocs.project_agent") placed.agent.push(sentence);
    else if (key === "pocs.project_tracker") placed.tracker.push(sentence);
    else if (key === "pocs.targets") placed.targets.push(sentence);
    else if (key.startsWith("pocs.targets[].")) {
      const field = key.slice("pocs.targets[].".length).split(".")[0];
      const name = targetNameIn(sentence, field);
      if (name === null) continue;
      targetNames.forEach((candidate, i) => {
        if (candidate.toLowerCase() !== name.toLowerCase()) return;
        const fields = (placed.target[i] ??= {});
        (fields[field] ??= []).push(sentence);
      });
    }
  }
  return placed;
}

function targetNameIn(sentence: string, field: string): string | null {
  const named = /of target '([^']*)'/.exec(sentence) ?? /\(target '([^']*)'\)$/.exec(sentence);
  if (named) return named[1];
  // The name's own sentences quote the name itself: "pocs.targets[].name is 'X'" and "pocs.targets[].name 'x' is used".
  if (field === "name") return /^pocs\.targets\[\]\.name (?:is )?'([^']*)'/.exec(sentence)?.[1] ?? null;
  return null;
}
