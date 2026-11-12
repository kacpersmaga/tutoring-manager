import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { api, ApiError, type LessonWithoutNote, type StudentNote, type StudentNotesPage } from '../api'
import { EmptyState, ErrorState, FormError, Skeleton } from '../components/States'
import { formatLongDate, formatShortDate, formatTime } from '../format'

/** A student's notes: add one for a recent lesson, edit or delete in place, older notes a page at a time. */
export function StudentNotes({ studentId }: { studentId: string }) {
  const [notes, setNotes] = useState<StudentNote[]>([])
  const [hasMore, setHasMore] = useState(false)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<ApiError>()
  const [adding, setAdding] = useState(false)
  const [undo, setUndo] = useState<StudentNote | null>(null)

  const load = useCallback(async (skip: number) => {
    setLoading(true)
    setError(undefined)
    try {
      const page = await api.get<StudentNotesPage>(`/api/students/${studentId}/notes?skip=${skip}`)
      setNotes((current) => (skip === 0 ? page.notes : [...current, ...page.notes]))
      setHasMore(page.hasMore)
    } catch (e) {
      setError(e as ApiError)
    } finally {
      setLoading(false)
    }
  }, [studentId])

  useEffect(() => { load(0) }, [load])

  // Deleting runs at once and can be undone for a few seconds, instead of asking "are you sure?".
  useEffect(() => {
    if (!undo) return
    const timer = setTimeout(() => setUndo(null), 8000)
    return () => clearTimeout(timer)
  }, [undo])

  async function remove(note: StudentNote) {
    await api.delete(`/api/lessons/${note.lessonId}/note`)
    setNotes((current) => current.filter((n) => n.lessonId !== note.lessonId))
    setUndo(note)
  }

  async function restore() {
    if (!undo) return
    await api.put(`/api/lessons/${undo.lessonId}/note`, { content: undo.content })
    setUndo(null)
    load(0)
  }

  return (
    <section aria-labelledby="notes-title" className="student-notes">
      <div className="section-head">
        <h2 id="notes-title">Lesson notes</h2>
        {!adding && (
          <button type="button" className="btn btn--card btn--small" onClick={() => setAdding(true)}>
            Add note
          </button>
        )}
      </div>

      {adding && (
        <AddNoteForm
          studentId={studentId}
          onCancel={() => setAdding(false)}
          onSaved={() => { setAdding(false); load(0) }}
        />
      )}

      <p className="notice" role="status" aria-live="polite" hidden={!undo}>
        Note from {undo && formatShortDate(undo.date)} deleted.{' '}
        <button type="button" className="link-button" onClick={restore}>Undo</button>
      </p>

      {error && <ErrorState error={error} onRetry={() => load(0)} />}
      {loading && notes.length === 0 && <Skeleton rows={3} height={88} />}
      {!loading && !error && notes.length === 0 && !adding && (
        <EmptyState
          title="No notes yet."
          text="Write what you covered and what is set for next time; notes stay here, newest first."
          action={<button type="button" className="btn btn--card" onClick={() => setAdding(true)}>Add the first note</button>}
        />
      )}

      <ol className="notes">
        {notes.map((note) => (
          <NoteCard key={note.lessonId} note={note} onDelete={() => remove(note)}
            onSaved={(content) => setNotes((current) => current.map((n) => (n.lessonId === note.lessonId ? { ...n, content } : n)))} />
        ))}
      </ol>

      {hasMore && (
        <button type="button" className="btn btn--card notes__more" onClick={() => load(notes.length)} aria-busy={loading} disabled={loading}>
          {loading ? 'Loading…' : 'Show older notes'}
        </button>
      )}
    </section>
  )
}

function AddNoteForm({ studentId, onCancel, onSaved }: { studentId: string; onCancel: () => void; onSaved: () => void }) {
  const [lessons, setLessons] = useState<LessonWithoutNote[]>()
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    api.get<LessonWithoutNote[]>(`/api/students/${studentId}/lessons-without-note`)
      .then(setLessons)
      .catch((e: ApiError) => setError(e.message))
  }, [studentId])

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = new FormData(event.currentTarget)
    setBusy(true)
    setError(null)
    try {
      await api.put(`/api/lessons/${form.get('lessonId')}/note`, { content: form.get('content') })
      onSaved()
    } catch (e) {
      setError((e as ApiError).message)
      setBusy(false)
    }
  }

  if (lessons && lessons.length === 0) {
    return (
      <div className="state">
        <p className="state__title">Every recent lesson has a note.</p>
        <p className="state__text">A lesson can get a note once it has started. Upcoming lessons are in the calendar.</p>
        <button type="button" className="btn btn--card" onClick={onCancel}>Close</button>
      </div>
    )
  }

  return (
    <form className="stock note-card note-card--form" onSubmit={submit}>
      <div className="field">
        <label className="field__label" htmlFor="note-lesson">Lesson</label>
        <select id="note-lesson" name="lessonId" className="input select" required disabled={!lessons}>
          {lessons?.map((l) => (
            <option key={l.lessonId} value={l.lessonId}>{formatLongDate(l.date)}, {formatTime(l.startTime)}</option>
          ))}
        </select>
      </div>
      <div className="field">
        <label className="field__label" htmlFor="note-content">What you covered, what is set for next time</label>
        <textarea id="note-content" name="content" className="input textarea" maxLength={4000} required autoFocus />
      </div>
      <FormError error={error} />
      <div className="note-card__actions">
        <button type="button" className="btn btn--text" onClick={onCancel}>Cancel</button>
        <button type="submit" className="btn btn--ink" aria-busy={busy} disabled={busy || !lessons}>
          {busy ? 'Saving…' : 'Save note'}
        </button>
      </div>
    </form>
  )
}

function NoteCard({ note, onDelete, onSaved }: { note: StudentNote; onDelete: () => void; onSaved: (content: string) => void }) {
  const [editing, setEditing] = useState(false)
  const [content, setContent] = useState(note.content)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function save(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    setError(null)
    try {
      await api.put(`/api/lessons/${note.lessonId}/note`, { content })
      onSaved(content.trim())
      setEditing(false)
    } catch (e) {
      setError((e as ApiError).message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <li className="stock note-card">
      <div className="note-card__top">
        <p className="note-card__date">{formatLongDate(note.date)}, {formatTime(note.startTime)}</p>
        {!editing && (
          <div className="note-card__tools">
            <button type="button" className="btn btn--text btn--small" onClick={() => setEditing(true)}>Edit</button>
            <button type="button" className="btn btn--text btn--small" onClick={onDelete}>Delete</button>
          </div>
        )}
      </div>
      {editing ? (
        <form className="form" onSubmit={save}>
          <label className="visually-hidden" htmlFor={`edit-${note.lessonId}`}>Note</label>
          <textarea id={`edit-${note.lessonId}`} className="input textarea" maxLength={4000} value={content}
            onChange={(e) => setContent(e.target.value)} autoFocus />
          <FormError error={error} />
          <div className="note-card__actions">
            <button type="button" className="btn btn--text" onClick={() => { setContent(note.content); setEditing(false) }}>Cancel</button>
            <button type="submit" className="btn btn--ink" aria-busy={busy} disabled={busy || !content.trim()}>
              {busy ? 'Saving…' : 'Save'}
            </button>
          </div>
        </form>
      ) : (
        <p className="note-card__text">{note.content}</p>
      )}
    </li>
  )
}
