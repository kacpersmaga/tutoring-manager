import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { api, ApiError, type StudentSummary } from '../api'
import { Dialog } from '../components/Dialog'
import { Field } from '../components/Field'
import { FormError } from '../components/States'

export interface Slot {
  date: string
  time: string
  studentId?: string
}

interface Props {
  slot: Slot | null
  students: StudentSummary[]
  onClose: () => void
  onCreated: () => void
}

/** One form for a single lesson and for a weekly series: the "every week" switch decides which endpoint it posts to. */
export function NewLessonDialog({ slot, students, onClose, onCreated }: Props) {
  const [weekly, setWeekly] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = new FormData(event.currentTarget)
    setBusy(true)
    setError(null)
    try {
      const common = {
        studentId: form.get('studentId'),
        startTime: form.get('startTime'),
        durationMinutes: Number(form.get('durationMinutes')),
      }
      if (weekly) {
        await api.post('/api/series', { ...common, startDate: form.get('date'), endDate: form.get('endDate') || null })
      } else {
        await api.post('/api/lessons', { ...common, date: form.get('date') })
      }
      onCreated()
    } catch (e) {
      setError((e as ApiError).message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <Dialog title="New lesson" open={slot !== null} onClose={onClose}>
      {slot && students.length === 0 ? (
        <div className="state">
          <p className="state__title">Add a student first.</p>
          <p className="state__text">Every lesson belongs to a student, so their reminder goes to the right inbox.</p>
          <Link to="/app/students" className="btn btn--ink">Add a student</Link>
        </div>
      ) : slot && (
        <form className="form" onSubmit={submit}>
          <div className="field">
            <label className="field__label" htmlFor="studentId">Student</label>
            <select id="studentId" name="studentId" className="input select" defaultValue={slot.studentId ?? students[0]?.id} required>
              {students.map((s) => (
                <option key={s.id} value={s.id}>{s.fullName}</option>
              ))}
            </select>
            <p className="field__help" />
          </div>

          <div className="form__row">
            <Field label={weekly ? 'First lesson' : 'Date'} name="date" type="date" defaultValue={slot.date} required />
            <Field label="Start" name="startTime" type="time" step={300} defaultValue={slot.time} required />
            <Field label="Minutes" name="durationMinutes" type="number" min={15} max={480} step={15} defaultValue={60} required />
          </div>

          <label className="check">
            <input type="checkbox" checked={weekly} onChange={(e) => setWeekly(e.target.checked)} />
            <span>Every week at this time</span>
          </label>

          {weekly && (
            <Field label="Last lesson (optional)" name="endDate" type="date" hint="Leave empty to keep it going. Lessons are planned 12 weeks ahead." />
          )}

          <FormError error={error} />
          <footer className="dialog__footer">
            <span className="field__help">The student is asked to confirm 24 hours before.</span>
            <button type="submit" className="btn btn--ink" aria-busy={busy} disabled={busy}>
              {busy ? 'Adding…' : weekly ? 'Add weekly lessons' : 'Add lesson'}
            </button>
          </footer>
        </form>
      )}
    </Dialog>
  )
}
