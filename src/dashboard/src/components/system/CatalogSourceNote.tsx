// 2026-09-18-84be: the ONE composition of "where a change to this text is made". The
// origin line and every expanded SKILL.md render this same component, so the page cannot
// say it two ways — and the overlay case is a different sentence, not a hedge on the
// source one: on an overlaid installation the body may be the operator's own file, which
// the Skills settings form does not carry and therefore cannot change.

export function CatalogSourceNote({
  overlayPath,
  testId,
}: {
  overlayPath: string | null;
  testId: string;
}) {
  if (overlayPath !== null) {
    return (
      <span className="msub" data-testid={testId}>
        An overlay from <span className="mono">{overlayPath}</span> is layered over the catalog
        source, so this text may be the operator&apos;s own file rather than the source&apos;s.
        That directory lives on the server and is not on the Skills settings form — no setting
        in this dashboard changes it, and nothing here edits it.
      </span>
    );
  }
  return (
    <span className="msub" data-testid={testId}>
      The catalog source is the Skills setting in this dashboard — Settings → Skills, which
      needs configuration access. A saved change shows here once the server resolves the
      catalog again: the next pipeline run does that, while the background refresh re-pulls
      only a changed release version. No catalog text is edited here.
    </span>
  );
}
