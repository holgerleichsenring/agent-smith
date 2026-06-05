// p0235: split a leading YAML frontmatter block (`---\n…\n---`) off a markdown
// document. result.md/plan.md lead with one (ticket/cost/repos); rendered as
// markdown it becomes an <hr> + a run-on paragraph, so the dashboard shows the
// frontmatter as a clean monospace header and the rest as styled markdown.

export interface SplitDocument {
  /** The frontmatter body (between the `---` fences), trimmed; null when absent. */
  frontmatter: string | null;
  /** The markdown after the frontmatter (or the whole doc when none). */
  body: string;
}

export function splitFrontmatter(content: string): SplitDocument {
  // Must start with a `---` fence on the first line.
  if (!/^---\r?\n/.test(content)) return { frontmatter: null, body: content };
  const rest = content.slice(content.indexOf("\n") + 1);
  const closeRe = /\r?\n---\r?\n/;
  const m = closeRe.exec(rest);
  if (!m) return { frontmatter: null, body: content };
  const frontmatter = rest.slice(0, m.index).trim();
  const body = rest.slice(m.index + m[0].length);
  return { frontmatter: frontmatter.length > 0 ? frontmatter : null, body };
}
