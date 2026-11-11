const money = new Intl.NumberFormat('en-GB', { minimumFractionDigits: 2, maximumFractionDigits: 2 })

/** 1234.5 → "1,234.50 zł". Amounts are always PLN in the MVP. */
export function formatMoney(amount: number): string {
  return `${money.format(amount)} zł`
}

/** A balance with its sign spelled out: "+200.00 zł", "−150.00 zł". */
export function formatBalance(amount: number): string {
  if (amount === 0) return formatMoney(0)
  return `${amount > 0 ? '+' : '−'}${formatMoney(Math.abs(amount))}`
}

function parseDate(date: string): Date {
  const [y, m, d] = date.split('-').map(Number)
  return new Date(y, m - 1, d)
}

/** "2027-01-20" → "Wed 20 Jan". */
export function formatShortDate(date: string): string {
  return parseDate(date).toLocaleDateString('en-GB', { weekday: 'short', day: 'numeric', month: 'short' })
}

/** "2027-01-20" → "Wednesday, 20 January 2027". */
export function formatLongDate(date: string): string {
  return parseDate(date).toLocaleDateString('en-GB', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' })
}

/** "17:00:00" → "17:00". */
export function formatTime(time: string): string {
  return time.slice(0, 5)
}

export function endTime(startTime: string, durationMinutes: number): string {
  const [h, m] = startTime.split(':').map(Number)
  const total = h * 60 + m + durationMinutes
  return `${String(Math.floor(total / 60) % 24).padStart(2, '0')}:${String(total % 60).padStart(2, '0')}`
}

/** "61109010140000071219812874" → "61 1090 1014 0000 0712 1981 2874". */
export function formatAccount(account: string): string {
  if (account.length !== 26) return account
  return `${account.slice(0, 2)} ${account.slice(2).replace(/(.{4})/g, '$1 ').trim()}`
}

export function toIsoDate(date: Date): string {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`
}

export function currentMonth(): string {
  return toIsoDate(new Date()).slice(0, 7)
}

/** "2027-01" → "January 2027". */
export function formatMonth(month: string): string {
  return parseDate(`${month}-01`).toLocaleDateString('en-GB', { month: 'long', year: 'numeric' })
}
