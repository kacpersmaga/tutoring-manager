// Takes repeatable screenshots of the key screens from the demo account.
// Needs the app running (`docker compose up`). Usage: npm run screenshots
import { mkdir } from 'node:fs/promises'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { chromium } from 'playwright'

const appUrl = process.env.APP_URL ?? 'http://localhost:8080'
const mailpitUrl = process.env.MAILPIT_URL ?? 'http://localhost:8025'
const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..')
const docsDir = resolve(root, 'docs/images')
const landingDir = resolve(root, 'src/TutoringManager.Web/public/product')

const desktop = { width: 1440, height: 900 }
const phone = { width: 390, height: 844 }

async function signInToDemo(page) {
  await page.goto(`${appUrl}/app/login`)
  await page.getByRole('button', { name: 'Open the demo account' }).click()
  await page.waitForURL(`${appUrl}/app`)
  await page.locator('.lesson-card').first().waitFor()
}

/** The newest attendance link in Mailpit: the demo leaves one email pending, which the worker sends on start. */
async function latestAttendanceLink() {
  for (let attempt = 0; attempt < 30; attempt++) {
    const list = await (await fetch(`${mailpitUrl}/api/v1/messages?limit=1`)).json()
    if (list.messages?.length) {
      const message = await (await fetch(`${mailpitUrl}/api/v1/message/${list.messages[0].ID}`)).json()
      const path = message.Text.match(/\/a\/[A-Za-z0-9_-]{43}/)?.[0]
      if (path) return { path, id: list.messages[0].ID }
    }
    await new Promise((r) => setTimeout(r, 2000))
  }
  throw new Error('No attendance email in Mailpit. Is the app running with the demo account?')
}

/** Docs get the whole screen; the landing page gets a crop (`landing` is a selector) so the text stays legible. */
async function shoot(page, name, { landing } = {}) {
  await page.waitForLoadState('networkidle')
  await page.evaluate(() => document.fonts.ready)
  await page.screenshot({ path: `${docsDir}/${name}.jpg`, type: 'jpeg', quality: 82 })
  if (landing === 'page') await page.screenshot({ path: `${landingDir}/${name}.jpg`, type: 'jpeg', quality: 86 })
  else if (landing) await page.locator(landing).first().screenshot({ path: `${landingDir}/${name}.jpg`, type: 'jpeg', quality: 86 })
  console.log(`  ${name}`)
}

await mkdir(docsDir, { recursive: true })
await mkdir(landingDir, { recursive: true })
const browser = await chromium.launch()

try {
  const context = await browser.newContext({ viewport: desktop, deviceScaleFactor: 2, reducedMotion: 'reduce' })
  const page = await context.newPage()
  await signInToDemo(page)
  // Late in the week every lesson is done; the next week shows the students' answers.
  if (await page.locator('.tag--asked').count() === 0) {
    await page.locator('.fc-next-button').click()
    await page.locator('.tag--asked').first().waitFor()
  }

  await shoot(page, 'calendar', { landing: '.calendar' })

  await page.locator('.lesson--confirmed').first().click()
  await page.locator('dialog[open]').waitFor()
  await shoot(page, 'lesson-dialog')
  await page.keyboard.press('Escape')

  await page.goto(`${appUrl}/app/students`)
  await page.locator('table').waitFor()
  await shoot(page, 'students')

  await page.getByRole('link', { name: 'Zofia Wiśniewska' }).click()
  await page.locator('.note-card').first().waitFor()
  await shoot(page, 'student', { landing: '.student-grid' })

  await page.goto(`${appUrl}/app/payments`)
  await page.locator('.ledger table').waitFor()
  await shoot(page, 'payments', { landing: '.ledger' })

  const link = await latestAttendanceLink()
  await page.goto(`${mailpitUrl}/view/${link.id}`)
  await shoot(page, 'email-in-mailpit')

  const mobile = await browser.newContext({ viewport: phone, deviceScaleFactor: 3, isMobile: true, hasTouch: true, reducedMotion: 'reduce' })
  const phonePage = await mobile.newPage()
  await phonePage.goto(`${appUrl}${link.path}`)
  await phonePage.getByRole('button', { name: 'I’ll be there' }).waitFor()
  await shoot(phonePage, 'attendance-phone', { landing: 'page' })

  await page.goto(`${appUrl}/`)
  await shoot(page, 'landing')

  await page.context().clearCookies()
  await page.goto(`${appUrl}/app/login`)
  await shoot(page, 'sign-in')
} finally {
  await browser.close()
}

console.log(`Saved to ${docsDir}`)
