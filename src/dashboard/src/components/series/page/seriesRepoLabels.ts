// 2026-11-27-ecf2b: a repo's chip label on the series page — the first letter of its name's last part (Server, Client,
// Worker → S, C, W), as many letters as it takes to tell it from the series' other repos; two repos whose last parts
// are the same keep the whole part and a number.

function lastPart(repo: string): string {
  const parts = repo.split(/[./\\\-_\s]+/).filter(Boolean);
  return parts[parts.length - 1] ?? repo;
}

const cap = (s: string) => s.charAt(0).toUpperCase() + s.slice(1).toLowerCase();

/** One short, unique label per repo, keyed by the repo's name. */
export function seriesRepoChipLabels(repos: readonly string[]): Record<string, string> {
  const unique = [...new Set(repos)];
  const parts = new Map(unique.map((repo) => [repo, lastPart(repo)]));
  const labels: Record<string, string> = {};
  for (const repo of unique) {
    const own = parts.get(repo)!;
    const others = unique.filter((r) => r !== repo).map((r) => parts.get(r)!.toLowerCase());
    let label: string | null = null;
    for (let n = 1; n <= own.length && label === null; n++) {
      const prefix = own.slice(0, n).toLowerCase();
      if (!others.some((o) => o.startsWith(prefix))) label = cap(own.slice(0, n));
    }
    // No prefix is its own: the whole part ("web" beside "webapp"), numbered only when another repo's part is the same.
    const same = unique.filter((r) => parts.get(r)!.toLowerCase() === own.toLowerCase());
    labels[repo] = label ?? (same.length > 1 ? `${cap(own)}${same.indexOf(repo) + 1}` : cap(own));
  }
  return labels;
}
