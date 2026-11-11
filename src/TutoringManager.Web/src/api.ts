// Thin wrapper around fetch for the Tutoring Manager API. Types mirror the C# contracts.

export type LessonStatus = 'Scheduled' | 'Completed' | 'Cancelled'
export type AttendanceResponse = 'Unknown' | 'Confirmed' | 'Declined'
export type EmailStatus = 'Pending' | 'Processing' | 'Sent' | 'Failed' | 'Cancelled'
export type DayOfWeek = 'Sunday' | 'Monday' | 'Tuesday' | 'Wednesday' | 'Thursday' | 'Friday' | 'Saturday'
export type MatchedBy = 'None' | 'Account' | 'Manual'

export interface Me { email: string; displayName: string; timeZoneId: string; isDemo: boolean }

export interface StudentSummary {
  id: string; fullName: string; email: string; ratePerLesson: number; isActive: boolean; balance: number
}

export interface BankAccount { id: string; accountNumber: string }

export interface StudentDetails {
  id: string; fullName: string; email: string; ratePerLesson: number; isActive: boolean
  createdAt: string; balance: number; bankAccounts: BankAccount[]
}

export interface Lesson {
  id: string; studentId: string; studentName: string; seriesId: string | null
  date: string; startTime: string; durationMinutes: number; startUtc: string; endUtc: string
  status: LessonStatus; attendance: AttendanceResponse; attendanceRespondedAt: string | null
  pricePerLesson: number; isModifiedFromSeries: boolean; note: string | null; emailStatus: EmailStatus | null
}

export interface StudentNote { lessonId: string; date: string; startTime: string; content: string; updatedAt: string }

export interface Series {
  id: string; studentId: string; dayOfWeek: DayOfWeek; startTime: string; durationMinutes: number
  startDate: string; endDate: string | null
}

export interface Transaction {
  id: string; bookingDate: string; amount: number; currency: string; counterpartyAccount: string
  counterpartyName: string; title: string; studentId: string | null; studentName: string | null; matchedBy: MatchedBy
}

export interface ImportResult {
  importId: string; bankFormat: string; incomingTransactions: number; imported: number
  skippedAsDuplicates: number; matched: number; unmatched: number
}

export interface StudentMonthReport {
  studentId: string; fullName: string; lessonsCompleted: number; openingBalance: number
  dueInMonth: number; paidInMonth: number; closingBalance: number
}

export interface MonthlyReport {
  month: string; dueInMonth: number; paidInMonth: number; totalOutstanding: number; students: StudentMonthReport[]
}

export interface AttendancePage {
  date: string; startTime: string; durationMinutes: number; tutorName: string; response: AttendanceResponse
}

/** An error answered by the API as ProblemDetails. */
export class ApiError extends Error {
  readonly status: number
  readonly errors: Record<string, string[]>

  constructor(status: number, message: string, errors: Record<string, string[]> = {}) {
    super(message)
    this.status = status
    this.errors = errors
  }
}

function readXsrfToken(): string | null {
  const match = document.cookie.split('; ').find((c) => c.startsWith('XSRF-TOKEN='))
  return match ? decodeURIComponent(match.slice('XSRF-TOKEN='.length)) : null
}

/** The antiforgery token is bound to the signed-in user, so refresh it after signing in or out. */
export async function refreshAntiforgery(): Promise<void> {
  await fetch('/api/auth/antiforgery', { credentials: 'same-origin' })
}

async function request<T>(method: string, url: string, body?: unknown): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json' }
  const isForm = body instanceof FormData
  if (body !== undefined && !isForm) headers['Content-Type'] = 'application/json'
  if (method !== 'GET') {
    const token = readXsrfToken()
    if (token) headers['X-XSRF-TOKEN'] = token
  }

  let response: Response
  try {
    response = await fetch(url, {
      method,
      headers,
      credentials: 'same-origin',
      body: body === undefined ? undefined : isForm ? body : JSON.stringify(body),
    })
  } catch {
    throw new ApiError(0, 'Could not reach the server. Check your connection and try again.')
  }

  if (response.status === 204) return undefined as T
  const data = response.headers.get('content-type')?.includes('json') ? await response.json() : null
  if (!response.ok) {
    const errors: Record<string, string[]> = data?.errors ?? {}
    const firstError = Object.values(errors)[0]?.[0]
    throw new ApiError(response.status, data?.detail ?? firstError ?? data?.title ?? `Request failed (${response.status}).`, errors)
  }
  return data as T
}

export const api = {
  get: <T>(url: string) => request<T>('GET', url),
  post: <T>(url: string, body?: unknown) => request<T>('POST', url, body ?? {}),
  put: <T>(url: string, body: unknown) => request<T>('PUT', url, body),
  delete: (url: string) => request<void>('DELETE', url),
  upload: <T>(url: string, form: FormData) => request<T>('POST', url, form),
}
