// Minimal markdown renderer for result.md.
// react-markdown + rehype-raw would be the spec'd path; deferred until the
// dashboard pulls a markdown deps tree across the board. For now, render
// fenced code blocks as <pre> and leave the rest as <pre>-wrapped text so
// the structure is visible without breaking the artefact display.

export function ResultMarkdown({ content }: { content: string }) {
  return (
    <article data-testid="result-markdown" className="prose prose-stone max-w-none text-sm">
      <pre className="whitespace-pre-wrap break-words rounded-md bg-stone-50 p-4">{content}</pre>
    </article>
  );
}
