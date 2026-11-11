import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { api, ApiError, type Lesson } from '../api'
import { Dialog } from '../components/Dialog'
import { Field } from '../components/Field'
import { FormError } from '../components/States'
import { endTime, formatLongDate, formatMoney, formatTime } from '../format'
import { lessonTag } from './lessonTag'

interface Props {
  lesson: Lesson | null
  onClose: () => void
  onChanged: () => void
}

const attendanceText: Record<Lesson['attendance'], string> = {
  Unknown: 'No answer yet',
  Confirmed: 'Coming',
  Declined: 'Not coming',
}

const emailText: Record<NonNullable<Lesson['emailStatus']>, string> = {
  Pending: 'Will be sent 24 hours before',
  Processing: 'Sending now',
  Sent: 'Sent',
  Failed: 'Could not be sent after 5 attempts',
  Cancelled: 'Not sent',
}

export function LessonDialog({ lesson, onClose, onChanged }: Props) {
  const [mode, setMode] = useState<'view' | 'move'>('view')
  const [note, setNote] = useState(lesson?.note ?? '')
  const [noteState, setNoteState] = useState<'idle' | 'saving' | 'saved'>('idle')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  if (!lesson) return <Dialog title="" open={false} onClose={onClose}>{null}</Dialog>

  const started = new Date(lesson.startUtc) <= new Date()
  const scheduled = lesson.status === 'Scheduled'
  const tag = lessonTag(lesson)

  async function run(action: () => Promise<unknown>) {
    setBusy(true)
    setError(null)
    try {
      await action()
      onChanged()
    } catch (e) {
      setError((e as ApiError).message)
    } finally {
      setBusy(false)
    }
  }

  async function saveNote(event: FormEvent) {
    event.preventDefault()
    setNoteState('saving')
    setError(null)
    try {
      await api.put(`/api/lessons/${lesson!.id}/note`, { content: note })
      setNoteState('saved')
      onChanged()
    } catch (e) {
      setNoteState('idle')
      setError((e as ApiError).message)
    }
  }

  async function saveMove(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = new FormData(event.currentTarget)
    await run(async () => {
      await api.put(`/api/lessons/${lesson!.id}`, {
        date: form.get('date'),
        startTime: form.get('startTime'),
        durationMinutes: Number(form.get('durationMinutes')),
      })
      setMode('view')
    })
  }

  return (
    <Dialog title={lesson.studentName} open onClose={onClose}>
      {mode === 'view' ? (
        <>
          <p className="lesson-when">
            {formatLongDate(lesson.date)}, {formatTime(lesson.startTime)} to {endTime(lesson.startTime, lesson.durationMinutes)}
            {tag && <span className={`tag tag--${tag.tone}`}>{tag.label}</span>}
          </p>

          <dl className="facts">
            <div>
              <dt>Attendance</dt>
              <dd>{attendanceText[lesson.attendance]}</dd>
            </div>
            <div>
              <dt>Reminder email</dt>
              <dd>{lesson.emailStatus ? emailText[lesson.emailStatus] : 'None'}</dd>
            </div>
            <div>
              <dt>Price</dt>
              <dd className="fig">{formatMoney(lesson.pricePerLesson)}</dd>
            </div>
            <div>
              <dt>Repeats</dt>
              <dd>{lesson.seriesId ? (lesson.isModifiedFromSeries ? 'Weekly, changed this time' : 'Weekly') : 'One-off'}</dd>
            </div>
          </dl>

          <form className="note-form" onSubmit={saveNote}>
            <label className="field__label" htmlFor="note">
              Note: what you covered, what is set for next time
            </label>
            <textarea
              id="note"
              name="note"
              className="input textarea"
              maxLength={4000}
              value={note}
              onChange={(e) => {
                setNote(e.target.value)
                setNoteState('idle')
              }}
              placeholder="Quadratic equations, ex. 4 to 9 for Thursday"
            />
            <div className="note-form__footer">
              <span className="field__help">{noteState === 'saved' ? 'Note saved.' : `${note.length} / 4000`}</span>
              <button type="submit" className="btn btn--card btn--small" aria-busy={noteState === 'saving'} disabled={noteState === 'saving' || note === (lesson.note ?? '')}>
                {noteState === 'saving' ? 'Saving…' : 'Save note'}
              </button>
            </div>
          </form>

          <FormError error={error} />

          <footer className="dialog__footer">
            <Link to={`/app/students/${lesson.studentId}`} className="link">
              Open student
            </Link>
            <div className="dialog__actions">
              {scheduled && (
                <>
                  <button type="button" className="btn btn--text" disabled={busy}
                    onClick={() => { if (window.confirm('Cancel this lesson? The student’s link stops working.')) run(() => api.post(`/api/lessons/${lesson.id}/cancel`)) }}>
                    Cancel lesson
                  </button>
                  <button type="button" className="btn btn--card" disabled={busy} onClick={() => setMode('move')}>
                    Move
                  </button>
                  <button type="button" className="btn btn--ink" disabled={busy || !started} aria-busy={busy}
                    title={started ? undefined : 'You can mark a lesson completed once it has started.'}
                    onClick={() => run(() => api.post(`/api/lessons/${lesson.id}/complete`))}>
                    Mark completed
                  </button>
                </>
              )}
            </div>
          </footer>
        </>
      ) : (
        <form className="form" onSubmit={saveMove}>
          <p className="form__intro">The student gets a new email for the new time; the old link stops working.</p>
          <div className="form__row">
            <Field label="Date" name="date" type="date" defaultValue={lesson.date} required />
            <Field label="Start" name="startTime" type="time" step={300} defaultValue={formatTime(lesson.startTime)} required />
            <Field label="Minutes" name="durationMinutes" type="number" min={15} max={480} step={15} defaultValue={lesson.durationMinutes} required />
          </div>
          <FormError error={error} />
          <footer className="dialog__footer">
            <span />
            <div className="dialog__actions">
              <button type="button" className="btn btn--text" onClick={() => setMode('view')}>
                Back
              </button>
              <button type="submit" className="btn btn--ink" aria-busy={busy} disabled={busy}>
                {busy ? 'Moving…' : 'Move lesson'}
              </button>
            </div>
          </footer>
        </form>
      )}
    </Dialog>
  )
}
