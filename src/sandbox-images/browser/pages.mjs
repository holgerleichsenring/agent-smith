// 2026-10-01-283di: what a render and a comparison both do with a page — the source to navigate
// to, a guarded browser context per viewport, and the computed styles of named selectors. Moved
// out of render.mjs so the two modes cannot drift apart.
import path from 'node:path';
import { locate, serve } from './site.mjs';

// A URL as it is, or a directory served from a local origin the guard lets through.
export async function targetOf(side, guard) {
  if (side.url) return side.url;
  const site = await locate(path.resolve(side.siteDir), side.page ?? '');
  const port = await serve(site.root);
  guard.allowLocal(port);
  return `http://127.0.0.1:${port}${site.path}`;
}

export async function newPage(browser, viewport, report) {
  const context = await browser.newContext({ viewport: { width: viewport.width, height: viewport.height },
    deviceScaleFactor: 1, isMobile: viewport.isMobile, hasTouch: viewport.isMobile,
    serviceWorkers: 'block', acceptDownloads: false });
  await context.route('**/*', route => /^https?:$/.test(new URL(route.request().url()).protocol)
    ? route.continue() : route.abort('blockedbyclient'));
  const page = await context.newPage();
  page.on('console', message => { if (message.type() === 'error') report.consoleErrors.push(message.text()); });
  page.on('pageerror', error => report.consoleErrors.push(String(error.message ?? error)));
  page.on('requestfailed', r => report.failedRequests.push({ url: r.url(), reason: r.failure()?.errorText ?? 'failed' }));
  page.on('response', r => { if (r.status() >= 400) report.failedRequests.push({ url: r.url(), reason: `HTTP ${r.status()}` }); });
  return page;
}

export async function open(browser, viewport, report, url) {
  const page = await newPage(browser, viewport, report);
  await page.goto(url, { waitUntil: 'load', timeout: 45_000 });
  await page.waitForLoadState('networkidle', { timeout: 10_000 }).catch(() => {});
  return page;
}

export const styleRows = (page, selectors, properties) => page.evaluate(({ selectors, properties }) => selectors.map(selector => {
  let nodes;
  try { nodes = document.querySelectorAll(selector); } catch { return { selector, count: 0, values: null }; }
  if (nodes.length === 0) return { selector, count: 0, values: null };
  const computed = getComputedStyle(nodes[0]);
  return { selector, count: nodes.length, values: Object.fromEntries(properties.map(p => [p, computed.getPropertyValue(p)])) };
}), { selectors, properties });
