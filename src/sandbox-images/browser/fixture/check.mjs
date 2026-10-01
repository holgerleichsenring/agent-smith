// 2026-10-01-283de: the browser image's fixture, run inside the image on each architecture:
//   docker run --rm --add-host internal.test:10.0.0.1 -v <this dir>:/fixture:ro <image> node /fixture/check.mjs
// A module script and a root-relative stylesheet render from the local origin; a subresource to
// 127.0.0.1:6379, one to a name resolving to 10.0.0.1, IPv6 / IPv4-mapped / 0.0.0.0 literals and a
// loopback websocket are refused; service workers are blocked; the 6000 px page yields a JPEG under
// 700 KB whose long edge is 1568 px.
import { execFileSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';

const work = fs.mkdtempSync('/tmp/render-fixture-');
fs.cpSync('/fixture/set', path.join(work, 'set'), { recursive: true });
const request = { url: null, siteDir: path.join(work, 'set'), page: '', selectors: ['button', 'h1', 'h2', 'nav'],
  properties: ['color', 'background-color', 'padding', 'border-radius'], outDir: path.join(work, 'out') };
fs.writeFileSync(path.join(work, 'request.json'), JSON.stringify(request));
try { execFileSync('node', ['/opt/agentsmith/render.mjs', path.join(work, 'request.json')], { stdio: 'inherit' }); }
catch { /* the result says why */ }
const result = JSON.parse(fs.readFileSync(path.join(work, 'out', 'result.json'), 'utf8'));
const failures = [];
const expect = (ok, what) => { if (!ok) failures.push(what); };
const style = selector => result.styles.find(s => s.selector === selector);

expect(!result.error, `rendered without error (${result.error})`);
expect(style('button')?.values?.['background-color'] === 'rgb(192, 255, 238)', 'root-relative stylesheet applied');
expect(style('h1')?.values?.color === 'rgb(1, 2, 3)', 'module script ran');
expect(style('nav')?.count === 0, 'an absent selector is reported as no match');
expect(result.refused.some(r => r.url.includes('127.0.0.1:6379')), 'loopback subresource refused');
expect(result.refused.some(r => r.url.includes('internal.test') && r.reason.includes('10.0.0.1')), 'name resolving to 10.0.0.1 refused');
expect(result.refused.some(r => r.url.includes(':6380')), 'IPv6 loopback literal refused');
expect(result.refused.some(r => r.url.includes(':6381')), 'IPv4-mapped IPv6 loopback refused');
expect(result.refused.some(r => r.url.includes(':6382')), '0.0.0.0 refused');
expect(result.refused.some(r => r.url.includes(':6383')), 'loopback websocket refused');
expect(style('h2')?.values?.color === 'rgb(4, 5, 6)', `no service worker became active (${style('h2')?.values?.color})`);

// The proxy's own range check, address by address — the same table PublicAddressRule holds.
const { refusalFor } = await import('/opt/agentsmith/egress.mjs');
for (const address of ['127.0.0.1', '::1', '::', '0.0.0.0', '10.0.0.1', '172.16.0.1', '192.168.0.1', '169.254.169.254',
  'fe80::1', 'fd00::1', '100.64.0.1', '224.0.0.1', '255.255.255.255', '::ffff:10.0.0.1', '::ffff:a00:1', '::ffff:127.0.0.1'])
  expect(refusalFor(address) !== null, `${address} refused by the proxy's rule`);
for (const address of ['93.184.215.14', '2606:4700:4700::1111', '::ffff:93.184.215.14'])
  expect(refusalFor(address) === null, `${address} allowed by the proxy's rule`);
for (const viewport of ['desktop', 'mobile']) {
  const shot = result.shots.find(s => s.viewport === viewport);
  const jpeg = shot && Buffer.from(fs.readFileSync(path.join(work, 'out', shot.file), 'utf8'), 'base64');
  expect(jpeg && jpeg.length < 700 * 1024, `${viewport} JPEG under 700 KB (${jpeg?.length})`);
  expect(jpeg && jpeg[0] === 0xff && jpeg[1] === 0xd8, `${viewport} screenshot is a JPEG`);
  expect(shot && Math.max(shot.width, shot.height) === 1568, `${viewport} long edge 1568 (${shot?.width}x${shot?.height})`);
}
console.log(JSON.stringify({ styles: result.styles, refused: result.refused, shots: result.shots }, null, 2));
if (failures.length) { console.error('FIXTURE FAILED:\n- ' + failures.join('\n- ')); process.exit(1); }
console.log('fixture passed');
