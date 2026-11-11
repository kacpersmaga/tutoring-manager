// Records the demo flow as short clips for the README GIF and the Remotion film.
// Needs a fresh demo (`docker compose down -v && docker compose up`). Usage: npm run record
import { mkdir, readdir, rename, rm, writeFile } from 'node:fs/promises'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { chromium } from 'playwright'

const appUrl = process.env.APP_URL ?? 'http://localhost:8080'
const mailpitUrl = process.env.MAILPIT_URL ?? 'http://localhost:8025'
const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..')
const outDir = resolve(root, 'video/public/clips')
const desktop = { width: 1440, height: 900 }
const phone = { width: 390, height: 844 }
const pause = (ms) => new Promise((r) => setTimeout(r, ms))

/** Same rule as DemoSeeder.AccountFromSeed: a valid Polish account number from a digit seed. */
function accountFromSeed(seed) {
  const bban = (seed + '0'.repeat(24)).slice(0, 24)
  const check = 98n - (BigInt(bban + '252100') % 97n)
  return String(check).padStart(2, '0') + bban
}

const today = new Date().toLocaleDateString('sv-SE') // local yyyy-mm-dd
const statement = [
  '#Data księgowania;#Data operacji;#Opis operacji;#Tytuł;#Nadawca/Odbiorca;#Numer konta;#Kwota;#Saldo po operacji;',
  `${today};${today};PRZELEW ZEWNĘTRZNY PRZYCHODZĄCY;"Korepetycje Jakub";"JAKUB KOWALCZYK";'${accountFromSeed('1020102600000422702')}';280,00;5 280,00;`,
  `${today};${today};PRZELEW ZEWNĘTRZNY PRZYCHODZĄCY;"Lekcje Hani";"KATARZYNA DĄBROWSKA";'${accountFromSeed('1600140200000654321')}';150,00;5 430,00;`,
  '',
].join('\n')

const marks = {}

/** Records one clip: `name.webm` plus the moments (ms from the clip's start) the film cuts on. */
async function clip(browser, name, viewport, storageState, run) {
  const context = await browser.newContext({
    viewport, deviceScaleFactor: 1, storageState, isMobile: viewport === phone, hasTouch: viewport === phone,
    recordVideo: { dir: outDir, size: viewport },
  })
  const page = await context.newPage()
  const start = Date.now()
  const mark = (label) => { marks[name] = { ...marks[name], [label]: Date.now() - start } }
  await run(page, mark)
  mark('end')
  const video = page.video()
  await context.close()
  await rename(await video.path(), resolve(outDir, `${name}.webm`))
  console.log(`  ${name}.webm`)
}

await rm(outDir, { recursive: true, force: true })
await mkdir(outDir, { recursive: true })
await writeFile(resolve(outDir, 'statement.csv'), statement)
const browser = await chromium.launch()

try {
  // Sign in once, outside the recordings.
  const login = await browser.newContext({ viewport: desktop })
  const loginPage = await login.newPage()
  await loginPage.goto(`${appUrl}/app/login`)
  await loginPage.getByRole('button', { name: 'Open the demo account' }).click()
  await loginPage.waitForURL(`${appUrl}/app`)
  const session = await login.storageState()
  await login.close()

  const list = await (await fetch(`${mailpitUrl}/api/v1/messages?limit=1`)).json()
  const message = await (await fetch(`${mailpitUrl}/api/v1/message/${list.messages[0].ID}`)).json()
  const link = message.Text.match(/\/a\/[A-Za-z0-9_-]{43}/)[0]

  await clip(browser, 'calendar', desktop, session, async (page, mark) => {
    await page.goto(`${appUrl}/app`)
    await page.locator('.lesson-card').first().waitFor()
    await page.evaluate(() => document.fonts.ready)
    mark('ready')
    await pause(2500)
    const asked = page.locator('.lesson-card', { has: page.locator('.tag--asked') }).first()
    await asked.hover()
    await pause(600)
    await asked.click()
    mark('dialog')
    await pause(3000)
    await page.keyboard.press('Escape')
    await pause(800)
  })

  await clip(browser, 'email', desktop, undefined, async (page, mark) => {
    await page.goto(`${mailpitUrl}/view/${list.messages[0].ID}`)
    await page.waitForLoadState('networkidle')
    mark('ready')
    await pause(3500)
  })

  await clip(browser, 'phone', phone, undefined, async (page, mark) => {
    await page.goto(`${appUrl}${link}`)
    await page.getByRole('button', { name: 'I’ll be there' }).waitFor()
    await page.evaluate(() => document.fonts.ready)
    mark('ready')
    await pause(1800)
    await page.getByRole('button', { name: 'I’ll be there' }).tap()
    mark('tapped')
    await pause(2500)
  })

  await clip(browser, 'coming', desktop, session, async (page, mark) => {
    await page.goto(`${appUrl}/app`)
    await page.locator('.lesson--confirmed').first().waitFor()
    await page.evaluate(() => document.fonts.ready)
    mark('ready')
    await pause(3000)
  })

  await clip(browser, 'payments', desktop, session, async (page, mark) => {
    await page.goto(`${appUrl}/app/payments`)
    await page.locator('.ledger table').waitFor()
    await page.evaluate(() => document.fonts.ready)
    mark('ready')
    await pause(1500)
    await page.locator('input[type=file]').setInputFiles(resolve(outDir, 'statement.csv'))
    await page.getByText(/new transfers/).waitFor()
    mark('imported')
    await pause(2200)
    await page.locator('.unmatched').scrollIntoViewIfNeeded()
    await pause(800)
    const row = page.locator('.transfer', { hasText: 'KATARZYNA' }).first()
    await row.locator('select').selectOption({ label: 'Hanna Dąbrowska' })
    await pause(700)
    await row.getByRole('button', { name: 'Assign' }).click()
    mark('assigned')
    await pause(1200)
    await page.evaluate(() => window.scrollTo({ top: 0, behavior: 'smooth' }))
    await pause(2500)
  })
} finally {
  await browser.close()
}

for (const file of await readdir(outDir)) {
  if (file.endsWith('.webm') && !['calendar', 'email', 'phone', 'coming', 'payments'].some((n) => file === `${n}.webm`)) {
    await rm(resolve(outDir, file))
  }
}
await writeFile(resolve(outDir, 'marks.json'), JSON.stringify(marks, null, 2))
console.log('marks:', JSON.stringify(marks))
