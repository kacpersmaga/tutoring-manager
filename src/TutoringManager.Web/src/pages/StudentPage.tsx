import { useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { api, ApiError, type Series, type StudentDetails, type StudentNote, type Transaction } from '../api'
import { Balance } from '../components/Balance'
import { Dialog } from '../components/Dialog'
import { EmptyState, ErrorState, FormError, Skeleton } from '../components/States'
import { endTime, formatAccount, formatLongDate, formatMoney, formatShortDate, formatTime } from '../format'
import { useLoad } from '../hooks'
import { StudentFormDialog } from './StudentFormDialog'

export function StudentPage() {
  const { id } = useParams()
  const student = useLoad<StudentDetails>(`/api/students/${id}`)
  const notes = useLoad<StudentNote[]>(`/api/students/${id}/notes`)
  const payments = useLoad<Transaction[]>(`/api/students/${id}/payments`)
  const series = useLoad<Series[]>(`/api/series?studentId=${id}`)
  const [editing, setEditing] = useState(false)
  const [deleting, setDeleting] = useState(false)

  if (student.loading && !student.data) return <div className="page"><Skeleton rows={6} height={56} /></div>
  if (student.error) return <div className="page"><ErrorState error={student.error} onRetry={student.reload} /></div>
  const s = student.data!

  return (
    <div className="page">
      <nav className="breadcrumb" aria-label="Breadcrumb">
        <Link to="/app/students">Students</Link>
      </nav>
      <header className="page__header student-head">
        <div>
          <h1>{s.fullName}</h1>
          <p className="muted">
            {s.email}, {formatMoney(s.ratePerLesson)} a lesson{!s.isActive && ', inactive'}
          </p>
        </div>
        <div className="student-head__balance">
          <span className="muted">Balance</span>
          <Balance amount={s.balance} withLabel />
        </div>
        <div className="page__actions">
          <button type="button" className="btn btn--card" onClick={() => setEditing(true)}>Edit</button>
        </div>
      </header>

      <div className="student-grid">
        <section aria-labelledby="notes-title" className="student-grid__main">
          <h2 id="notes-title">Lesson notes</h2>
          {notes.loading && !notes.data && <Skeleton rows={3} height={88} />}
          {notes.error && <ErrorState error={notes.error} onRetry={notes.reload} />}
          {notes.data?.length === 0 && (
            <EmptyState title="No notes yet." text="Open a lesson in the calendar and write what you covered; it lands here, newest first." />
          )}
          <ol className="notes">
            {notes.data?.map((n) => (
              <li key={n.lessonId} className="stock note-card">
                <p className="note-card__date">{formatLongDate(n.date)}, {formatTime(n.startTime)}</p>
                <p className="note-card__text">{n.content}</p>
              </li>
            ))}
          </ol>
        </section>

        <aside className="student-grid__side">
          <section aria-labelledby="weekly-title">
            <h2 id="weekly-title">Weekly lessons</h2>
            {series.data?.length === 0 && <p className="muted">No weekly lesson. Add one from the calendar.</p>}
            <ul className="plain-list">
              {series.data?.map((x) => (
                <SeriesRow key={x.id} series={x} onChanged={series.reload} />
              ))}
            </ul>
          </section>

          <BankAccounts student={s} onChanged={student.reload} />

          <section aria-labelledby="payments-title">
            <h2 id="payments-title">Payments</h2>
            {payments.data?.length === 0 && <p className="muted">No transfers matched yet. Import a bank statement in Payments.</p>}
            <table className="table table--compact">
              <tbody>
                {payments.data?.map((p) => (
                  <tr key={p.id}>
                    <td>{formatShortDate(p.bookingDate)}</td>
                    <td className="num fig">{formatMoney(p.amount)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </section>

          <section aria-labelledby="danger-title" className="danger">
            <h2 id="danger-title">Delete student</h2>
            <p className="muted">Removes their lessons, notes and saved accounts. Transfers stay, unassigned.</p>
            <button type="button" className="btn btn--text btn--danger" onClick={() => setDeleting(true)}>Delete {s.fullName}</button>
          </section>
        </aside>
      </div>

      <StudentFormDialog key={String(editing)} open={editing} student={s} onClose={() => setEditing(false)}
        onSaved={() => { setEditing(false); student.reload() }} />
      <DeleteStudentDialog open={deleting} student={s} onClose={() => setDeleting(false)} />
    </div>
  )
}

function SeriesRow({ series, onChanged }: { series: Series; onChanged: () => void }) {
  const [busy, setBusy] = useState(false)
  return (
    <li className="series-row">
      <span>
        {series.dayOfWeek}s, {formatTime(series.startTime)} to {endTime(series.startTime, series.durationMinutes)}
        <span className="muted"> {series.endDate ? `until ${formatShortDate(series.endDate)}` : 'ongoing'}</span>
      </span>
      <button type="button" className="btn btn--text btn--small" disabled={busy} aria-busy={busy}
        onClick={async () => {
          if (!window.confirm('End this weekly lesson? Future lessons go; past lessons and notes stay.')) return
          setBusy(true)
          await api.delete(`/api/series/${series.id}`).finally(() => setBusy(false))
          onChanged()
        }}>
        End
      </button>
    </li>
  )
}

function BankAccounts({ student, onChanged }: { student: StudentDetails; onChanged: () => void }) {
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function add(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const formElement = event.currentTarget
    const accountNumber = new FormData(formElement).get('accountNumber')
    setBusy(true)
    setError(null)
    try {
      await api.post(`/api/students/${student.id}/accounts`, { accountNumber })
      formElement.reset()
      onChanged()
    } catch (e) {
      setError((e as ApiError).message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <section aria-labelledby="accounts-title">
      <h2 id="accounts-title">Pays from</h2>
      <ul className="plain-list">
        {student.bankAccounts.map((a) => (
          <li key={a.id} className="series-row">
            <span className="fig">{formatAccount(a.accountNumber)}</span>
            <button type="button" className="btn btn--text btn--small" aria-label={`Remove account ${a.accountNumber}`}
              onClick={async () => { await api.delete(`/api/students/${student.id}/accounts/${a.id}`); onChanged() }}>
              Remove
            </button>
          </li>
        ))}
      </ul>
      <form className="inline-form" onSubmit={add}>
        <label className="visually-hidden" htmlFor="accountNumber">Account number</label>
        <input id="accountNumber" name="accountNumber" className="input" inputMode="numeric" autoComplete="off"
          placeholder="26 digits" required aria-describedby="account-error" />
        <button type="submit" className="btn btn--card btn--small" aria-busy={busy} disabled={busy}>Add</button>
      </form>
      <p id="account-error" className="form-error" role="alert">{error}</p>
    </section>
  )
}

function DeleteStudentDialog({ open, student, onClose }: { open: boolean; student: StudentDetails; onClose: () => void }) {
  const [typed, setTyped] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const navigate = useNavigate()

  return (
    <Dialog title={`Delete ${student.fullName}?`} open={open} onClose={onClose}>
      <form className="form" onSubmit={async (e) => {
        e.preventDefault()
        setBusy(true)
        try {
          await api.delete(`/api/students/${student.id}`)
          navigate('/app/students')
        } catch (err) {
          setError((err as ApiError).message)
          setBusy(false)
        }
      }}>
        <p>This cannot be undone. Their lessons, notes and saved bank accounts are erased; their transfers stay in your records, unassigned.</p>
        <div className="field">
          <label className="field__label" htmlFor="confirm-name">Type the student’s name to confirm</label>
          <input id="confirm-name" className="input" autoComplete="off" value={typed} onChange={(e) => setTyped(e.target.value)} />
        </div>
        <FormError error={error} />
        <footer className="dialog__footer">
          <button type="button" className="btn btn--text" onClick={onClose}>Keep student</button>
          <button type="submit" className="btn btn--ink" disabled={busy || typed.trim() !== student.fullName} aria-busy={busy}>
            Delete for good
          </button>
        </footer>
      </form>
    </Dialog>
  )
}
