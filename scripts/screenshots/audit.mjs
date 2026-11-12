// UI audit helper: visits every screen at desktop and phone width and reports measurable problems
// (contrast, touch targets, unlabeled fields, missing alt text, horizontal overflow). Screenshots go to audit-out/.
import { mkdir } from 'node:fs/promises'
import { chromium } from 'playwright'

const appUrl = process.env.APP_URL ?? 'http://localhost:8080'
const mailpitUrl = process.env.MAILPIT_URL ?? 'http://localhost:8025'
const out = new URL('./audit-out/', import.meta.url)
await mkdir(out, { recursive: true })

function measure() {
  const canvas = document.createElement('canvas')
  canvas.width = canvas.height = 1
  const ctx = canvas.getContext('2d', { willReadFrequently: true })
  const rgb = (css) => { ctx.clearRect(0, 0, 1, 1); ctx.fillStyle = '#000'; ctx.fillStyle = css; ctx.fillRect(0, 0, 1, 1); const d = ctx.getImageData(0, 0, 1, 1).data; return [d[0], d[1], d[2], d[3] / 255] }
  const lum = ([r, g, b]) => { const f = (c) => { c /= 255; return c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4 }; return 0.2126 * f(r) + 0.7152 * f(g) + 0.0722 * f(b) }
  const background = (el) => { for (let e = el; e; e = e.parentElement) { const c = rgb(getComputedStyle(e).backgroundColor); if (c[3] > 0.5) return c } return [255, 255, 255, 1] }
  const issues = { contrast: [], targets: [], labels: [], alt: [] }

  for (const el of document.querySelectorAll('body *')) {
    const text = [...el.childNodes].filter((n) => n.nodeType === 3).map((n) => n.textContent.trim()).join(' ')
    if (!text || el.closest('[hidden], .visually-hidden, .fc-timegrid-slot-label')) continue
    const rect = el.getBoundingClientRect()
    if (!rect.width || !rect.height) continue
    const style = getComputedStyle(el)
    const fg = rgb(style.color); const bg = background(el)
    const l1 = lum(fg); const l2 = lum(bg)
    const ratio = (Math.max(l1, l2) + 0.05) / (Math.min(l1, l2) + 0.05)
    const size = parseFloat(style.fontSize); const bold = parseInt(style.fontWeight) >= 600
    const needed = size >= 24 || (size >= 18.66 && bold) ? 3 : 4.5
    if (ratio < needed && parseFloat(style.opacity) > 0.6) issues.contrast.push(`${ratio.toFixed(2)} < ${needed} "${text.slice(0, 40)}" (${size}px)`)
  }
  for (const el of document.querySelectorAll('a, button, input, select, textarea, [role=button]')) {
    const r = el.getBoundingClientRect()
    if (r.width && (r.width < 24 || r.height < 24) && !el.closest('.fc')) issues.targets.push(`${el.tagName.toLowerCase()} ${Math.round(r.width)}x${Math.round(r.height)} "${(el.textContent || el.getAttribute('aria-label') || '').trim().slice(0, 30)}"`)
  }
  for (const el of document.querySelectorAll('input:not([type=hidden]), select, textarea')) {
    const labelled = el.labels?.length || el.getAttribute('aria-label') || el.getAttribute('aria-labelledby')
    if (!labelled) issues.labels.push(el.outerHTML.slice(0, 80))
  }
  for (const img of document.querySelectorAll('img')) if (!img.hasAttribute('alt')) issues.alt.push(img.src)
  return { ...issues, overflowX: document.documentElement.scrollWidth > window.innerWidth || [...document.querySelectorAll('main *')].some((e) => !e.closest('.table-scroll, .fc, .l-hero__shot, .auth__showcase, .l-pinned__frame') && e.getBoundingClientRect().right > window.innerWidth + 1), fonts: [...new Set([...document.querySelectorAll('body *')].slice(0, 400).map((e) => getComputedStyle(e).fontFamily.split(',')[0]))] }
}

const browser = await chromium.launch()
const report = {}
try {
  const list = await (await fetch(`${mailpitUrl}/api/v1/messages?limit=1`)).json()
  const message = list.messages?.length ? await (await fetch(`${mailpitUrl}/api/v1/message/${list.messages[0].ID}`)).json() : null
  const attendance = message?.Text.match(/\/a\/[A-Za-z0-9_-]{43}/)?.[0]

  for (const [label, viewport] of [['desktop', { width: 1440, height: 900 }], ['phone', { width: 375, height: 812 }]]) {
    const context = await browser.newContext({ viewport, reducedMotion: 'reduce' })
    const page = await context.newPage()
    const visit = async (name, path) => {
      await page.goto(`${appUrl}${path}`)
      await page.waitForLoadState('networkidle')
      await page.evaluate(() => document.fonts.ready)
      report[`${label} ${name}`] = await page.evaluate(measure)
      await page.screenshot({ path: new URL(`${label}-${name}.png`, out).pathname.slice(1), fullPage: name !== 'calendar' })
    }
    await visit('landing', '/')
    await visit('privacy', '/privacy')
    await visit('register', '/app/register')
    await visit('login', '/app/login')
    if (attendance) await visit('attendance', attendance)
    await page.goto(`${appUrl}/app/login`)
    await page.getByRole('button', { name: 'Open the demo account' }).click()
    await page.waitForURL(`${appUrl}/app`)
    await visit('calendar', '/app')
    await visit('students', '/app/students')
    await page.locator('.row-link').first().click()
    await page.locator('.student-grid').waitFor()
    await visit('student', new URL(page.url()).pathname)
    await visit('payments', '/app/payments')
    await visit('account', '/app/account')
    await context.close()
  }
} finally {
  await browser.close()
}

for (const [page, r] of Object.entries(report)) {
  const lines = []
  if (r.overflowX) lines.push('  horizontal overflow')
  for (const k of ['contrast', 'targets', 'labels', 'alt']) for (const item of [...new Set(r[k])].slice(0, 6)) lines.push(`  ${k}: ${item}`)
  console.log(`${page}${lines.length ? '' : ': clean'}  [fonts: ${r.fonts.join(', ')}]`)
  for (const l of lines) console.log(l)
}
