import { useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import type { StudentSummary } from '../api'
import { Balance } from '../components/Balance'
import { EmptyState, ErrorState, Skeleton } from '../components/States'
import { formatMoney } from '../format'
import { useLoad } from '../hooks'
import { StudentFormDialog } from './StudentFormDialog'

export function StudentsPage() {
  const [params, setParams] = useSearchParams()
  const showInactive = params.get('inactive') === 'true'
  const students = useLoad<StudentSummary[]>(`/api/students?includeInactive=${showInactive}`)
  const [creating, setCreating] = useState(false)
  const navigate = useNavigate()

  return (
    <div className="page">
      <header className="page__header">
        <h1>Students</h1>
        <div className="page__actions">
          <label className="check">
            <input
              type="checkbox"
              checked={showInactive}
              onChange={(e) => setParams(e.target.checked ? { inactive: 'true' } : {})}
            />
            <span>Show inactive</span>
          </label>
          <button type="button" className="btn btn--ink" onClick={() => setCreating(true)}>
            New student
          </button>
        </div>
      </header>

      {students.loading && !students.data && <Skeleton rows={5} />}
      {students.error && <ErrorState error={students.error} onRetry={students.reload} />}
      {students.data?.length === 0 && (
        <EmptyState
          title="No students yet."
          text="Add a student to plan lessons, keep notes and match their transfers."
          action={<button type="button" className="btn btn--card" onClick={() => setCreating(true)}>Add your first student</button>}
        />
      )}
      {students.data && students.data.length > 0 && (
        <table className="table">
          <thead>
            <tr>
              <th scope="col">Name</th>
              <th scope="col" className="hide-narrow">Reminder email</th>
              <th scope="col" className="num">Rate</th>
              <th scope="col" className="num">Balance</th>
            </tr>
          </thead>
          <tbody>
            {students.data.map((s) => (
              <tr key={s.id} className={s.isActive ? undefined : 'is-inactive'}>
                <th scope="row">
                  <Link to={`/app/students/${s.id}`} className="row-link">{s.fullName}</Link>
                  {!s.isActive && <span className="tag tag--cancelled">Inactive</span>}
                </th>
                <td className="hide-narrow muted">{s.email}</td>
                <td className="num fig">{formatMoney(s.ratePerLesson)}</td>
                <td className="num"><Balance amount={s.balance} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      <StudentFormDialog
        key={String(creating)}
        open={creating}
        onClose={() => setCreating(false)}
        onSaved={(student) => navigate(`/app/students/${student.id}`)}
      />
    </div>
  )
}
