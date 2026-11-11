// Renders the Open Graph image and the GitHub social preview from the real calendar screenshot.
// Run `npm run screenshots` first. Usage: node social-images.mjs
import { readFile } from 'node:fs/promises'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { chromium } from 'playwright'

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..')
const calendar = await readFile(resolve(root, 'src/TutoringManager.Web/public/product/calendar.jpg'))
const tokens = await readFile(resolve(root, 'src/TutoringManager.Web/src/styles/tokens.css'), 'utf8')

const html = (width, height) => `<!doctype html>
<html><head>
<link href="https://api.fontshare.com/v2/css?f[]=general-sans@400,500,600&display=swap" rel="stylesheet">
<style>
${tokens}
html, body { margin: 0; overflow: clip; }
body { width: ${width}px; height: ${height}px; display: grid; grid-template-columns: 0.9fr 1.1fr; align-items: center; gap: 48px; padding: 0 0 0 72px; box-sizing: border-box;
  background: radial-gradient(90% 120% at 0% 30%, var(--color-cotton), var(--color-desk) 62%); color: var(--color-ink); font-family: var(--font-display); }
.name { font-size: 26px; font-weight: 600; letter-spacing: -0.02em; margin-bottom: 28px; }
h1 { margin: 0; font-size: 64px; line-height: 1.02; letter-spacing: -0.035em; font-weight: 600; }
p { margin: 24px 0 0; font-size: 22px; color: var(--color-ink-2); line-height: 1.4; }
img { width: 100%; border-radius: 10px; box-shadow: var(--edge-stock), var(--cast-stock-lift); transform: rotate(-1.5deg) translateX(24px); }
</style></head><body>
<div><div class="name">Tutoring Manager</div><h1>Every lesson confirmed. Every złoty matched.</h1>
<p>Lessons, notes, attendance links and bank-statement matching for private tutors.</p></div>
<img src="data:image/jpeg;base64,${calendar.toString('base64')}">
</body></html>`

const browser = await chromium.launch()
try {
  for (const [width, height, out] of [
    [1200, 630, 'src/TutoringManager.Web/public/og.jpg'],
    [1280, 640, 'docs/images/social-preview.jpg'],
  ]) {
    const page = await browser.newPage({ viewport: { width, height }, deviceScaleFactor: 1 })
    await page.setContent(html(width, height), { waitUntil: 'networkidle' })
    await page.evaluate(() => document.fonts.ready)
    await page.screenshot({ path: resolve(root, out), type: 'jpeg', quality: 90 })
    console.log(`  ${out}`)
  }
} finally {
  await browser.close()
}
