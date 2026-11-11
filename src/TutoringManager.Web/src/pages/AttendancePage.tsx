import { useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'
import { api, ApiError, type AttendancePage as Page, type AttendanceResponse } from '../api'
import { endTime, formatLongDate, formatTime } from '../format'

/** The public page behind the email link, opened by students on their phones. No sign-in, no app chrome. */
export function AttendancePage() {
  const { token } = useParams()
  const [page, setPage] = useState<Page>()
  const [error, setError] = useState<ApiError>()
  const [sending, setSending] = useState<AttendanceResponse | null>(null)

  useEffect(() => {
    api.get<Page>(`/api/attendance/${token}`).then(setPage).catch(setError)
  }, [token])

  async function answer(response: AttendanceResponse) {
    setSending(response)
    try {
      setPage(await api.post<Page>(`/api/attendance/${token}`, { response }))
    } catch (e) {
      setError(e as ApiError)
    } finally {
      setSending(null)
    }
  }

  return (
    <main className="desk attend">
      <section className="stock attend__card" aria-live="polite">
        {!page && !error && <p className="muted">Opening your lesson…</p>}

        {error && (
          <>
            <h1>{error.status === 410 ? 'This lesson has started.' : 'This link does not work.'}</h1>
            <p className="attend__text">
              {error.status === 410
                ? 'Answers close when the lesson begins. If something changed, message your tutor directly.'
                : error.status === 429
                  ? 'Too many tries from this device. Wait a minute and open the link again.'
                  : 'It may be from an older email: lessons that are moved or cancelled get a new link. Check your newest email from your tutor.'}
            </p>
          </>
        )}

        {page && (
          <>
            <p className="attend__who">Lesson with {page.tutorName}</p>
            <h1 className="attend__when">
              {formatLongDate(page.date)}
              <span className="attend__time fig">{formatTime(page.startTime)} to {endTime(page.startTime, page.durationMinutes)}</span>
            </h1>

            {page.response === 'Unknown' ? (
              <p className="attend__text">Will you be there?</p>
            ) : (
              <p className="attend__stamp-wrap">
                <span className={page.response === 'Confirmed' ? 'stamp' : 'stamp stamp--ink'}>
                  {page.response === 'Confirmed' ? 'Coming' : 'Not coming'}
                </span>
                <span className="attend__text">{page.tutorName} can see your answer. You can change it until the lesson starts.</span>
              </p>
            )}

            <div className="attend__actions">
              <button type="button" className={page.response === 'Confirmed' ? 'btn btn--card btn--block' : 'btn btn--ink btn--block'}
                aria-pressed={page.response === 'Confirmed'} aria-busy={sending === 'Confirmed'} disabled={sending !== null}
                onClick={() => answer('Confirmed')}>
                {sending === 'Confirmed' ? 'Sending…' : 'I’ll be there'}
              </button>
              <button type="button" className="btn btn--card btn--block"
                aria-pressed={page.response === 'Declined'} aria-busy={sending === 'Declined'} disabled={sending !== null}
                onClick={() => answer('Declined')}>
                {sending === 'Declined' ? 'Sending…' : 'I can’t make it'}
              </button>
            </div>
          </>
        )}
      </section>
      <p className="attend__foot muted">
        Sent with Tutoring Manager. <a href="/privacy">Privacy</a>
      </p>
    </main>
  )
}
