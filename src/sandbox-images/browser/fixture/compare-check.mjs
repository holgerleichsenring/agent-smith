// 2026-10-01-283di: the comparison fixture, run inside the browser image on each architecture:
//   docker run --rm -v <this dir>:/fixture:ro <image> node /fixture/compare-check.mjs
// A 900 px page and a 1400 px page compare in one invocation: the shorter is padded with magenta
// to the taller height and the pad counts as mismatch; each side's computed styles come back per
// selector pair, a selector that matches nothing is reported as such, and the diff is a JPEG
// under 700 KB.
import { execFileSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { PNG } from '/opt/agentsmith/node_modules/pngjs/lib/png.js';
import { pad } from '/opt/agentsmith/compare.mjs';

const work = fs.mkdtempSync('/tmp/compare-fixture-');
for (const name of ['reference', 'candidate']) {
  fs.mkdirSync(path.join(work, name));
  fs.copyFileSync(`/fixture/compare/${name}.html`, path.join(work, name, 'index.html'));
}
const request = { url: null, siteDir: null, page: '', selectors: [], properties: ['font-size', 'background-color'],
  outDir: path.join(work, 'out'), compare: {
    reference: { url: null, siteDir: path.join(work, 'reference'), page: '' },
    candidate: { url: null, siteDir: path.join(work, 'candidate'), page: '' },
    pairs: [{ reference: 'h1', candidate: 'h1' }, { reference: '.missing', candidate: 'main' }], viewports: ['desktop'] } };
fs.writeFileSync(path.join(work, 'request.json'), JSON.stringify(request));
try { execFileSync('node', ['/opt/agentsmith/render.mjs', path.join(work, 'request.json')], { stdio: 'inherit' }); }
catch { /* the result says why */ }
const result = JSON.parse(fs.readFileSync(path.join(work, 'out', 'result.json'), 'utf8'));
const failures = [];
const expect = (ok, what) => { if (!ok) failures.push(what); };
const view = result.compare?.viewports?.[0];

expect(!result.error, `compared without error (${result.error})`);
expect(view?.referenceHeight === 900 && view?.candidateHeight === 1400, `heights 900 and 1400 (${view?.referenceHeight}, ${view?.candidateHeight})`);
expect(view?.paddedHeight === 1400, `the shorter padded to 1400 (${view?.paddedHeight})`);
expect(view?.mismatchRatio >= 500 / 1400 && view?.mismatchRatio < 0.4, `the pad counts as mismatch (${view?.mismatchRatio})`);
expect(view?.reference?.[0]?.values?.['font-size'] === '16px', 'reference h1 is 16px');
expect(view?.candidate?.[0]?.values?.['font-size'] === '15px', 'candidate h1 is 15px');
expect(view?.reference?.[1]?.count === 0 && view?.candidate?.[1]?.count === 1, 'a selector matching nothing is reported, not skipped');
const shot = result.shots.find(s => s.file === 'diff-desktop.jpg.b64');
const jpeg = shot && Buffer.from(fs.readFileSync(path.join(work, 'out', shot.file), 'utf8'), 'base64');
expect(jpeg && jpeg.length < 700 * 1024 && jpeg[0] === 0xff && jpeg[1] === 0xd8, `diff is a JPEG under 700 KB (${jpeg?.length})`);

// pad() on its own: a 2x1 image padded to 2x3 keeps its row and fills the rest with opaque magenta.
const tiny = new PNG({ width: 2, height: 1 });
tiny.data.fill(0);
const padded = pad(tiny, 3);
expect(padded.height === 3 && padded.data[0] === 0, 'the original rows are kept');
expect([...padded.data.subarray(8, 12)].join() === '255,0,255,255', 'the pad is opaque magenta');

console.log(JSON.stringify({ compare: result.compare, shots: result.shots }, null, 2));
if (failures.length) { console.error('COMPARE FIXTURE FAILED:\n- ' + failures.join('\n- ')); process.exit(1); }
console.log('compare fixture passed');
