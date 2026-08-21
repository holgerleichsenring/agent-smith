// p0488: the glob semantics of a project's repo ref live HERE, in one place, so
// the discovered inventory and the connection repo picker can never disagree
// about what a rule covers.
//
// A repo ref is either a PICK ("conn/Sample.Api") naming exactly one repo
// forever, or a RULE ("conn/*", "conn/Sample.*") that keeps matching repos
// discovered next week. Plain catalog ids (no slash) are neither.

/** Does a glob pattern ("*", "Sample.*", "Sample.Api") cover this repo name? */
export function patternMatches(pattern: string, repoName: string): boolean {
  if (!pattern.includes("*")) return pattern === repoName;
  const rx = new RegExp(
    `^${pattern
      .split("*")
      .map((s) => s.replace(/[.*+?^${}()|[\]\\]/g, "\\$&"))
      .join(".*")}$`,
  );
  return rx.test(repoName);
}

/** Does a project repo ref ("conn/Name", "conn/*", "conn/Pre*") cover this
 *  discovered repo? Plain catalog refs (no slash) never match here. */
export function refMatches(ref: string, connectionId: string, repoName: string): boolean {
  const slash = ref.indexOf("/");
  if (slash <= 0 || ref.slice(0, slash) !== connectionId) return false;
  return patternMatches(ref.slice(slash + 1), repoName);
}

/** A ref carrying a wildcard is a RULE, not a pick. */
export function isRule(ref: string): boolean {
  return ref.includes("*");
}
