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

const localDate = (date) => date.toLocaleDateString('sv-SE') // yyyy-mm-dd in local time
const polishAmount = (amount) => amount.toFixed(2).replace('.', ',')

/** A smooth scroll the viewer can follow, instead of a jump cut. */
async function smoothScrollTo(page, top) {
  await page.evaluate((y) => window.scrollTo({ top: y, behavior: 'smooth' }), top)
  await pause(1400)
}

// Five students for the payments clip: a fresh tutor, so the clip starts with nothing paid.
const payers = [
  { fullName: 'Zofia Wiśniewska', rate: 150, day: 0, time: '16:00', account: accountFromSeed('1140200400003002013') },
  { fullName: 'Jakub Kowalczyk', rate: 140, day: 0, time: '17:30', account: accountFromSeed('1020102600000422702'), pays: 0.5 },
  { fullName: 'Maja Lewandowska', rate: 180, day: 1, time: '15:00', account: accountFromSeed('2490000500004000300') },
  { fullName: 'Hanna Dąbrowska', rate: 150, day: 3, time: '16:30', account: accountFromSeed('1870100045000222111'),
    parent: { name: 'KATARZYNA DĄBROWSKA', account: accountFromSeed('1600140200000654321') } },
  { fullName: 'Antoni Zieliński', rate: 130, day: 4, time: '17:00', account: accountFromSeed('1050001234000090301') },
]

/**
 * Registers a new tutor with four weeks of completed lessons and no transfers, through the public API.
 * Returns the signed-in session and a bank statement that pays most of it (one parent pays from her own account).
 */
async function prepareEmptyPayments(browser) {
  const context = await browser.newContext()
  const request = context.request
  const xsrf = async () => {
    await request.get(`${appUrl}/api/auth/antiforgery`)
    return decodeURIComponent((await context.cookies()).find((c) => c.name === 'XSRF-TOKEN').value)
  }
  const post = async (url, data) => {
    const response = await request.post(`${appUrl}${url}`, { data, headers: { 'X-XSRF-TOKEN': await xsrf() } })
    if (!response.ok()) throw new Error(`${url}: ${response.status()} ${await response.text()}`)
    return response.status() === 204 ? null : response.json()
  }

  await post('/api/auth/register', { email: `recording-${Date.now()}@example.com`, password: 'recording password 2027', displayName: 'Maria Kowalczyk' })
  const now = new Date()
  const monday = new Date(now)
  monday.setDate(now.getDate() - ((now.getDay() + 6) % 7) - 28)
  for (const payer of payers) {
    const student = await post('/api/students', {
      fullName: payer.fullName, email: 'student@example.com', ratePerLesson: payer.rate, bankAccounts: [payer.account],
    })
    const first = new Date(monday)
    first.setDate(monday.getDate() + payer.day)
    await post('/api/series', { studentId: student.id, startDate: localDate(first), startTime: payer.time, durationMinutes: 60 })
  }
  await post('/api/lessons/complete-past')

  const students = await (await request.get(`${appUrl}/api/students`)).json()
  const monthStart = new Date(now.getFullYear(), now.getMonth(), 1)
  const lines = students.map((student, i) => {
    const payer = payers.find((p) => p.fullName === student.fullName)
    const amount = Math.round(-student.balance * (payer.pays ?? 1))
    const date = new Date(Math.max(monthStart.getTime(), now.getTime() - (i + 1) * 86400000))
    const from = payer.parent ?? { name: student.fullName.toUpperCase(), account: payer.account }
    const day = localDate(date)
    return `${day};${day};PRZELEW ZEWNĘTRZNY PRZYCHODZĄCY;"Korepetycje";"${from.name}";'${from.account}';${polishAmount(amount)};0,00;`
  })
  const statement = ['#Data księgowania;#Data operacji;#Opis operacji;#Tytuł;#Nadawca/Odbiorca;#Numer konta;#Kwota;#Saldo po operacji;', ...lines, ''].join('\n')

  const session = await context.storageState()
  await context.close()
  return { session, statement }
}

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
    // Late in the week every lesson is already done; the next week shows the answers.
    if (await page.locator('.tag--asked').count() === 0) {
      await page.locator('.fc-next-button').click()
      await page.locator('.tag--asked').first().waitFor()
    }
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
    await page.locator('.lesson-card').first().waitFor()
    if (await page.locator('.tag--asked').count() === 0) await page.locator('.fc-next-button').click()
    await page.locator('.lesson--confirmed').first().waitFor()
    await page.evaluate(() => document.fonts.ready)
    mark('ready')
    await pause(3000)
  })

  const empty = await prepareEmptyPayments(browser)
  await writeFile(resolve(outDir, 'statement.csv'), empty.statement)

  await clip(browser, 'payments', desktop, empty.session, async (page, mark) => {
    await page.goto(`${appUrl}/app/payments`)
    await page.locator('.ledger table').waitFor()
    await page.evaluate(() => document.fonts.ready)
    mark('ready')
    await pause(2000)
    await page.locator('input[type=file]').setInputFiles(resolve(outDir, 'statement.csv'))
    await page.getByText(/new transfers/).waitFor()
    mark('imported')
    await pause(2500)
    const unmatched = await page.locator('.unmatched').evaluate((el) => el.getBoundingClientRect().top + window.scrollY - 120)
    await smoothScrollTo(page, unmatched)
    const row = page.locator('.transfer', { hasText: 'KATARZYNA' }).first()
    await row.locator('select').selectOption({ label: 'Hanna Dąbrowska' })
    await pause(700)
    await row.getByRole('button', { name: 'Assign' }).click()
    mark('assigned')
    await pause(1200)
    await smoothScrollTo(page, 0)
    await pause(2000)
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
