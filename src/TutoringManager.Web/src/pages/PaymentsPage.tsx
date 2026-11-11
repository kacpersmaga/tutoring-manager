import { useState, type ChangeEvent } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { api, ApiError, type ImportResult, type MonthlyReport, type StudentSummary, type Transaction } from '../api'
import { Balance } from '../components/Balance'
import { EmptyState, ErrorState, Skeleton } from '../components/States'
import { currentMonth, formatAccount, formatMonth, formatMoney, formatShortDate } from '../format'
import { useLoad } from '../hooks'

export function PaymentsPage() {
  const [params, setParams] = useSearchParams()
  const month = params.get('month') ?? currentMonth()
  const report = useLoad<MonthlyReport>(`/api/payments/report?month=${month}`)
  const unmatched = useLoad<Transaction[]>('/api/payments/unmatched')
  const students = useLoad<StudentSummary[]>('/api/students')

  const refresh = () => {
    report.reload()
    unmatched.reload()
  }

  return (
    <div className="page">
      <header className="page__header">
        <h1>Payments</h1>
        <div className="page__actions">
          <label className="field__label month-picker">
            Month
            <input type="month" className="input" value={month} onChange={(e) => e.target.value && setParams({ month: e.target.value })} />
          </label>
          <ImportButton onImported={refresh} />
        </div>
      </header>

      <section aria-labelledby="report-title" className="ledger stock">
        <div className="ledger__head">
          <h2 id="report-title">{formatMonth(month)}</h2>
          {report.data && (
            <dl className="ledger__totals">
              <div><dt>Due this month</dt><dd className="fig">{formatMoney(report.data.dueInMonth)}</dd></div>
              <div><dt>Paid this month</dt><dd className="fig">{formatMoney(report.data.paidInMonth)}</dd></div>
              <div><dt>Outstanding</dt><dd className="fig">{formatMoney(report.data.totalOutstanding)}</dd></div>
            </dl>
          )}
        </div>
        {report.loading && !report.data && <Skeleton rows={5} />}
        {report.error && <ErrorState error={report.error} onRetry={report.reload} />}
        {report.data?.students.length === 0 && (
          <EmptyState title="Nothing to settle this month." text="Completed lessons and matched transfers show up here, per student." />
        )}
        {report.data && report.data.students.length > 0 && (
          <table className="table">
            <thead>
              <tr>
                <th scope="col">Student</th>
                <th scope="col" className="num hide-narrow">Lessons</th>
                <th scope="col" className="num hide-narrow">Brought forward</th>
                <th scope="col" className="num">Due</th>
                <th scope="col" className="num">Paid</th>
                <th scope="col" className="num">Balance</th>
              </tr>
            </thead>
            <tbody>
              {report.data.students.map((r) => (
                <tr key={r.studentId}>
                  <th scope="row"><Link to={`/app/students/${r.studentId}`} className="row-link">{r.fullName}</Link></th>
                  <td className="num fig hide-narrow">{r.lessonsCompleted}</td>
                  <td className="num hide-narrow"><Balance amount={r.openingBalance} /></td>
                  <td className="num fig">{formatMoney(r.dueInMonth)}</td>
                  <td className="num fig">{formatMoney(r.paidInMonth)}</td>
                  <td className="num"><Balance amount={r.closingBalance} /></td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
        <p className="ledger__foot muted">Balance = everything paid minus every completed lesson, up to the end of the month.</p>
      </section>

      <section aria-labelledby="unmatched-title" className="unmatched">
        <h2 id="unmatched-title">Transfers to assign</h2>
        {unmatched.loading && !unmatched.data && <Skeleton rows={2} height={64} />}
        {unmatched.error && <ErrorState error={unmatched.error} onRetry={unmatched.reload} />}
        {unmatched.data?.length === 0 && (
          <EmptyState title="Every transfer is assigned." text="Transfers from accounts you have not saved yet will wait here." />
        )}
        <ul className="plain-list">
          {unmatched.data?.map((t) => (
            <UnmatchedRow key={t.id} transaction={t} students={students.data ?? []} onAssigned={refresh} />
          ))}
        </ul>
      </section>
    </div>
  )
}

function ImportButton({ onImported }: { onImported: () => void }) {
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState<{ text: string; error?: boolean } | null>(null)

  async function upload(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0]
    event.target.value = ''
    if (!file) return
    const form = new FormData()
    form.append('file', file)
    setBusy(true)
    setMessage(null)
    try {
      const result = await api.upload<ImportResult>('/api/payments/imports', form)
      setMessage({ text: importSummary(result) })
      onImported()
    } catch (e) {
      setMessage({ text: (e as ApiError).message, error: true })
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="import">
      <label className={busy ? 'btn btn--ink is-busy' : 'btn btn--ink'} aria-busy={busy}>
        {busy ? 'Importing…' : 'Import bank statement'}
        <input type="file" accept=".csv,text/csv" className="visually-hidden" onChange={upload} disabled={busy} />
      </label>
      <p className={message?.error ? 'import__message form-error' : 'import__message muted'} role="status">
        {message?.text ?? 'mBank CSV, up to 1 MB.'}
      </p>
    </div>
  )
}

function importSummary(r: ImportResult): string {
  const parts = [`${r.imported} new transfers`, `${r.matched} matched`]
  if (r.unmatched) parts.push(`${r.unmatched} to assign`)
  if (r.skippedAsDuplicates) parts.push(`${r.skippedAsDuplicates} already imported`)
  return `${parts.join(', ')}.`
}

function UnmatchedRow({ transaction: t, students, onAssigned }: { transaction: Transaction; students: StudentSummary[]; onAssigned: () => void }) {
  const [studentId, setStudentId] = useState('')
  const [remember, setRemember] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function assign() {
    setBusy(true)
    setError(null)
    try {
      await api.post(`/api/payments/transactions/${t.id}/assign`, { studentId, rememberAccount: remember })
      onAssigned()
    } catch (e) {
      setError((e as ApiError).message)
      setBusy(false)
    }
  }

  return (
    <li className="transfer stock">
      <div className="transfer__what">
        <span className="transfer__amount fig">{formatMoney(t.amount)}</span>
        <span>{t.counterpartyName || 'Unknown sender'}</span>
        <span className="muted">{formatShortDate(t.bookingDate)}, “{t.title}”</span>
        {t.counterpartyAccount && <span className="muted fig">{formatAccount(t.counterpartyAccount)}</span>}
      </div>
      <div className="transfer__assign">
        <label className="visually-hidden" htmlFor={`student-${t.id}`}>Student</label>
        <select id={`student-${t.id}`} className="input select" value={studentId} onChange={(e) => setStudentId(e.target.value)}>
          <option value="">Choose a student</option>
          {students.map((s) => <option key={s.id} value={s.id}>{s.fullName}</option>)}
        </select>
        {t.counterpartyAccount && (
          <label className="check">
            <input type="checkbox" checked={remember} onChange={(e) => setRemember(e.target.checked)} />
            <span>Remember this account</span>
          </label>
        )}
        <button type="button" className="btn btn--card btn--small" disabled={!studentId || busy} aria-busy={busy} onClick={assign}>
          Assign
        </button>
      </div>
      {error && <p className="form-error" role="alert">{error}</p>}
    </li>
  )
}
