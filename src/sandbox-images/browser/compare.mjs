// 2026-10-01-283di: compare_reference's half of the script. Both sides load in ONE browser — two
// page loads per viewport — and come back as the computed styles of each selector pair (the exact
// level, judged by the server) and a screenshot similarity (the observed level). The two captures
// share the viewport width; the shorter is PADDED at the bottom with opaque magenta to the taller
// height, so a section one side lacks counts as mismatch instead of being cropped away. pixelmatch
// gives the ratio and a diff image, encoded like a screenshot. Nothing here decides pass or fail.
import fs from 'node:fs/promises';
import path from 'node:path';
import pixelmatch from 'pixelmatch';
import { PNG } from 'pngjs';
import { open, styleRows, targetOf } from './pages.mjs';
import { capture, encode, viewports } from './shots.mjs';

const magenta = [255, 0, 255, 255];

// The image padded at the bottom to `height` with opaque magenta.
export function pad(image, height) {
  if (image.height === height) return image;
  const padded = new PNG({ width: image.width, height });
  for (let i = 0; i < padded.data.length; i += 4) padded.data.set(magenta, i);
  PNG.bitblt(image, padded, 0, 0, image.width, image.height, 0, 0);
  return padded;
}

// Two PNG captures of one width: the mismatch over the padded area and the diff image.
export function diff(referencePng, candidatePng) {
  const reference = PNG.sync.read(referencePng);
  const candidate = PNG.sync.read(candidatePng);
  const width = Math.min(reference.width, candidate.width);
  const height = Math.max(reference.height, candidate.height);
  const [a, b] = [pad(reference, height), pad(candidate, height)];
  const out = new PNG({ width, height });
  const mismatched = pixelmatch(a.data, b.data, out.data, width, height, { threshold: 0.1 });
  return { mismatched, ratio: mismatched / (width * height), height, png: PNG.sync.write(out) };
}

async function side(browser, viewport, report, url, selectors, properties) {
  const page = await open(browser, viewport, report, url);
  const styles = await styleRows(page, selectors, properties);
  const shot = await capture(page, viewport);
  await page.context().close();
  return { styles, ...shot };
}

export async function compare(request, browser, guard, encoder, report) {
  const job = request.compare;
  const referenceUrl = await targetOf(job.reference, guard);
  const candidateUrl = await targetOf(job.candidate, guard);
  report.compare = { referenceUrl, candidateUrl, viewports: [] };
  report.url = candidateUrl;
  for (const viewport of viewports.filter(v => job.viewports.includes(v.name))) {
    const reference = await side(browser, viewport, report, referenceUrl, job.pairs.map(p => p.reference), request.properties);
    const candidate = await side(browser, viewport, report, candidateUrl, job.pairs.map(p => p.candidate), request.properties);
    const result = diff(reference.png, candidate.png);
    const jpeg = await encode(encoder, result.png);
    const file = `diff-${viewport.name}.jpg.b64`;
    await fs.writeFile(path.join(request.outDir, file), jpeg.base64);
    report.shots.push({ viewport: viewport.name, file, width: jpeg.width, height: jpeg.height, bytes: jpeg.bytes,
      pageHeight: result.height, capturedHeight: result.height });
    report.compare.viewports.push({ viewport: viewport.name, reference: reference.styles, candidate: candidate.styles,
      referenceHeight: reference.capturedHeight, candidateHeight: candidate.capturedHeight, paddedHeight: result.height,
      mismatchedPixels: result.mismatched, mismatchRatio: result.ratio });
  }
}
