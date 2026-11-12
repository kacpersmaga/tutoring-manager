// Renders the README as a sequence of product slides (headline on the desk + the real screen), like a landing page.
// Run `npm run screenshots` first. Usage: node readme-slides.mjs
import { readFile } from 'node:fs/promises'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { chromium } from 'playwright'

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..')
const images = resolve(root, 'docs/images')
const out = resolve(root, 'docs/readme')
const tokens = await readFile(resolve(root, 'src/TutoringManager.Web/src/styles/tokens.css'), 'utf8')
const image = async (name) => `data:image/jpeg;base64,${(await readFile(resolve(images, name))).toString('base64')}`

const width = 1600
const css = `
${tokens}
html, body { margin: 0; overflow: clip; }
body { width: ${width}px; font-family: var(--font-body); color: var(--color-ink); -webkit-font-smoothing: antialiased;
  background-color: var(--color-desk); background-image: radial-gradient(90% 120% at 0% 20%, var(--color-cotton), transparent 62%); }
.slide { box-sizing: border-box; padding: 96px 120px 110px; display: grid; gap: 56px; }
h1, h2 { margin: 0; font-family: var(--font-display); font-weight: 600; letter-spacing: -0.035em; line-height: 1.02; color: var(--color-ink); text-wrap: balance; }
h1 { font-size: 96px; max-width: 13ch; }
h2 { font-size: 72px; max-width: 16ch; }
p { margin: 0; font-size: 30px; line-height: 1.4; color: var(--color-ink-2); max-width: 36ch; }
.name { font-family: var(--font-display); font-size: 30px; font-weight: 600; letter-spacing: -0.02em; }
.head { display: grid; gap: 24px; }
.screen { width: 100%; border-radius: 16px; display: block; box-shadow: var(--edge-stock), 22px 20px 44px -18px oklch(var(--shade) / 0.38); }
.tilt { transform: rotate(-1deg); }
.split { display: grid; grid-template-columns: 1fr auto; gap: 80px; align-items: center; }
.phone { width: 380px; padding: 14px; border-radius: 56px; background: var(--color-board); box-shadow: 22px 20px 44px -18px oklch(var(--shade) / 0.45); }
.phone img { display: block; width: 100%; border-radius: 44px; }
.board { background: var(--color-board); color: var(--color-board-type); border-radius: 20px; padding: 44px 56px; font-family: var(--font-outlier); font-size: 34px; line-height: 1.7; }
.board .dim { color: oklch(72% 0.01 60); }
.facts { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 0; border-top: 2px solid var(--color-print); }
.facts div { padding: 32px 32px 8px 0; display: grid; align-content: start; gap: 10px; }
.facts strong { font-family: var(--font-display); font-size: 30px; letter-spacing: -0.02em; line-height: 1.15; }
.facts span { font-size: 22px; line-height: 1.45; color: var(--color-ink-2); }
`

const slides = {
  'hero': async () => `<div class="slide"><div class="head"><div class="name">Tutoring Manager</div>
    <h1>Every lesson confirmed. Every złoty matched.</h1>
    <p>Weekly lessons, notes, attendance links and bank-statement matching, for private tutors.</p></div>
    <img class="screen tilt" src="${await image('calendar.jpg')}"></div>`,
  'week': async () => `<div class="slide"><div class="head"><h2>Your week, with every answer on it</h2>
    <p>Weekly lessons planned twelve weeks ahead. Coming, not coming or asked, at a glance.</p></div>
    <img class="screen" src="${await image('lesson-dialog.jpg')}"></div>`,
  'student': async () => `<div class="slide split"><div class="head"><h2>One tap from the student</h2>
    <p>An email 24 hours before. No account, no app. The link closes when the lesson starts.</p></div>
    <div class="phone"><img src="${await image('attendance-phone.jpg')}"></div></div>`,
  'notes': async () => `<div class="slide"><div class="head"><h2>Notes that follow the student</h2>
    <p>One note per lesson, newest first, beside what they paid.</p></div>
    <img class="screen" src="${await image('student.jpg')}"></div>`,
  'payments': async () => `<div class="slide"><div class="head"><h2>Import the statement. The month settles itself.</h2>
    <p>Transfers find their students by account. A running balance, not a guess.</p></div>
    <img class="screen" src="${await image('payments.jpg')}"></div>`,
  'engineering': async () => `<div class="slide"><div class="head"><h2>Small, plain, and built to be trusted</h2>
    <p>.NET 10, PostgreSQL 16 and React. 90 integration tests on a real database.</p></div>
    <div class="facts">
      <div><strong>Emails sent once, on any number of servers</strong><span>A table as the queue, FOR UPDATE SKIP LOCKED, leases and retries.</span></div>
      <div><strong>No double booking</strong><span>An exclusion constraint in PostgreSQL, not just a check in code.</span></div>
      <div><strong>Links that leak nothing</strong><span>256 random bits in the URL, only a SHA-256 hash in the database.</span></div>
      <div><strong>A running balance</strong><span>Prepayments, late payments and one transfer for two months need no special rules.</span></div>
      <div><strong>17:00 stays 17:00</strong><span>Local time is the truth; UTC is derived, across both clock changes.</span></div>
      <div><strong>One tutor never sees another's data</strong><span>A global query filter, covered by tests on every resource.</span></div>
    </div></div>`,
  'run': async () => `<div class="slide"><div class="head"><h2>Running on your machine in 20 seconds</h2>
    <p>Docker is all it needs. A demo account with two months of data is ready on the first start.</p></div>
    <div class="board"><span class="dim">$</span> git clone https://github.com/kacpersmaga/tutoring-manager<br><span class="dim">$</span> cd tutoring-manager &amp;&amp; docker compose up<br><span class="dim">→ http://localhost:8080 · demo@tutoring-manager.local</span></div></div>`,
}

const browser = await chromium.launch()
try {
  const page = await browser.newPage({ viewport: { width, height: 900 }, deviceScaleFactor: 1 })
  for (const [name, build] of Object.entries(slides)) {
    await page.setContent(`<!doctype html><html><head>
      <link href="https://api.fontshare.com/v2/css?f[]=general-sans@600&display=swap" rel="stylesheet">
      <link href="https://fonts.googleapis.com/css2?family=Geist:wght@400;500&family=Geist+Mono&display=swap" rel="stylesheet">
      <style>${css}</style></head><body>${await build()}</body></html>`, { waitUntil: 'networkidle' })
    await page.evaluate(() => document.fonts.ready)
    await page.locator('.slide').screenshot({ path: resolve(out, `${name}.jpg`), type: 'jpeg', quality: 85 })
    console.log(`  docs/readme/${name}.jpg`)
  }
} finally {
  await browser.close()
}
