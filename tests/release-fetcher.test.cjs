const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const { join } = require('node:path');
const { runInNewContext } = require('node:vm');
const source = readFileSync(join(__dirname, '../docs/js/release-fetcher.js'), 'utf8');

async function render(releases, ok = true) {
  const elements = new Map();
  for (const id of ['download-version', 'download-button', 'download-size', 'download-date']) {
    elements.set(id, { href: 'https://github.com/MOSHHHHHH/PDFtoIMG/releases',
      textContent: '', setAttribute() {}, removeAttribute() {} });
  }
  runInNewContext(source, {
    document: { getElementById: id => elements.get(id) },
    fetch: async url => {
      assert.match(url, /\/releases\?per_page=10$/);
      return { ok, status: ok ? 200 : 403, json: async () => releases };
    }
  });
  await new Promise(setImmediate);
  return elements;
}
const release = (tag, date, prerelease = false, assets = [{ name: 'PDFtoIMG.exe', browser_download_url: `https://example.test/${tag}.exe`, size: 74000000 }]) =>
  ({ tag_name: tag, published_at: date, prerelease, assets });

test('new beta takes precedence over older stable release; drafts and assetless releases excluded', async () => {
  const elements = await render([
    release('old', '2026-08-28'),
    { ...release('draft', '2026-10-26'), draft: true },
    release('no-exe', '2026-10-25', false, []),
    release('v1.0.0-beta.1', '2026-10-24', true)
  ]);
  assert.equal(elements.get('download-button').href, 'https://example.test/v1.0.0-beta.1.exe');
  assert.match(elements.get('download-version').textContent, /v1\.0\.0-beta\.1.*בטא/);
});

test('network failure preserves releases fallback', async () => {
  const elements = await render([], false);
  assert.equal(elements.get('download-button').href, 'https://github.com/MOSHHHHHH/PDFtoIMG/releases');
  assert.match(elements.get('download-version').textContent, /Releases/);
});

test('empty release list preserves releases fallback', async () => {
  const elements = await render([]);
  assert.equal(elements.get('download-button').href, 'https://github.com/MOSHHHHHH/PDFtoIMG/releases');
  assert.match(elements.get('download-version').textContent, /Releases/);
});
