import type { DatesSetArg, EventClickArg, EventContentArg, EventDropArg } from '@fullcalendar/core'
import interactionPlugin, { type DateClickArg, type EventResizeDoneArg } from '@fullcalendar/interaction'
import FullCalendar from '@fullcalendar/react'
import timeGridPlugin from '@fullcalendar/timegrid'
import { useMemo, useState } from 'react'
import { api, ApiError, type Lesson, type StudentSummary } from '../api'
import { ErrorState } from '../components/States'
import { formatTime, toIsoDate } from '../format'
import { useLoad } from '../hooks'
import { LessonDialog } from './LessonDialog'
import { NewLessonDialog, type Slot } from './NewLessonDialog'
import { lessonTag } from './lessonTag'

function lessonClass(lesson: Lesson): string {
  if (lesson.status === 'Cancelled') return 'lesson--cancelled'
  if (lesson.status === 'Completed') return 'lesson--done'
  if (lesson.attendance === 'Confirmed') return 'lesson--confirmed'
  if (lesson.attendance === 'Declined') return 'lesson--declined'
  return 'lesson--open'
}

function renderLesson(arg: EventContentArg) {
  const lesson = arg.event.extendedProps.lesson as Lesson
  const tag = lessonTag(lesson)
  return (
    <div className="lesson-card__content" title={`${lesson.studentName}, ${formatTime(lesson.startTime)}`}>
      <span className="lesson-card__name">{lesson.studentName}</span>
      {tag && <span className={`tag tag--${tag.tone}`}>{tag.label}</span>}
    </div>
  )
}

export function CalendarPage() {
  const [range, setRange] = useState<{ from: string; to: string } | null>(null)
  const lessons = useLoad<Lesson[]>(range ? `/api/lessons?from=${range.from}&to=${range.to}` : null)
  const students = useLoad<StudentSummary[]>('/api/students')
  const [openLessonId, setOpenLessonId] = useState<string | null>(null)
  const [newSlot, setNewSlot] = useState<Slot | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const narrow = useMemo(() => window.matchMedia('(max-width: 40rem)').matches, [])

  const events = useMemo(
    () =>
      (lessons.data ?? []).map((lesson) => ({
        id: lesson.id,
        // Wall-clock strings without a zone: the calendar shows the tutor's local time whatever the browser's zone.
        start: `${lesson.date}T${lesson.startTime}`,
        end: `${lesson.date}T${addMinutes(lesson.startTime, lesson.durationMinutes)}`,
        classNames: ['lesson-card', lessonClass(lesson)],
        editable: lesson.status === 'Scheduled',
        extendedProps: { lesson },
      })),
    [lessons.data],
  )

  const openLesson = lessons.data?.find((l) => l.id === openLessonId) ?? null

  // Show the day from an hour before the week's first lesson (afternoons by default), so there is no empty morning to scroll past.
  const firstHour = Math.max(7, Math.min(13, ...(lessons.data ?? []).map((l) => Number(l.startTime.slice(0, 2)) - 1)))

  async function move(arg: EventDropArg | EventResizeDoneArg) {
    const start = arg.event.start!
    const end = arg.event.end!
    try {
      await api.put(`/api/lessons/${arg.event.id}`, {
        date: toIsoDate(start),
        startTime: `${String(start.getHours()).padStart(2, '0')}:${String(start.getMinutes()).padStart(2, '0')}`,
        durationMinutes: Math.round((end.getTime() - start.getTime()) / 60000),
      })
      setMessage(null)
      lessons.reload()
    } catch (e) {
      arg.revert()
      setMessage((e as ApiError).message)
    }
  }

  async function completePast() {
    setBusy(true)
    try {
      const result = await api.post<{ completed: number }>('/api/lessons/complete-past')
      setMessage(result.completed === 0 ? 'No past lessons were waiting.' : `Marked ${result.completed} past lessons as completed.`)
      lessons.reload()
    } catch (e) {
      setMessage((e as ApiError).message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="page page--calendar">
      <header className="page__header">
        <h1>Calendar</h1>
        <div className="page__actions">
          <button type="button" className="btn btn--card" onClick={completePast} aria-busy={busy} disabled={busy}>
            Mark past lessons completed
          </button>
          <button type="button" className="btn btn--ink" onClick={() => setNewSlot({ date: toIsoDate(new Date()), time: '16:00' })}>
            New lesson
          </button>
        </div>
      </header>

      <p className="notice" role="status" aria-live="polite" hidden={!message}>
        {message}
      </p>

      {lessons.error && <ErrorState error={lessons.error} onRetry={lessons.reload} />}

      <div className="calendar stock" aria-busy={lessons.loading}>
        <FullCalendar
          plugins={[timeGridPlugin, interactionPlugin]}
          initialView={narrow ? 'timeGridDay' : 'timeGridWeek'}
          firstDay={1}
          locale="en-GB"
          headerToolbar={{ left: 'title', center: '', right: 'today prev,next' }}
          buttonText={{ today: 'This week' }}
          allDaySlot={false}
          slotMinTime={`${String(firstHour).padStart(2, '0')}:00:00`}
          slotMaxTime="22:00:00"
          slotDuration="00:30:00"
          slotLabelFormat={{ hour: '2-digit', minute: '2-digit', hour12: false }}
          dayHeaderFormat={{ weekday: 'short', day: 'numeric' }}
          nowIndicator
          height="auto"
          events={events}
          eventContent={renderLesson}
          eventClick={(arg: EventClickArg) => setOpenLessonId(arg.event.id)}
          eventDrop={move}
          eventResize={move}
          dateClick={(arg: DateClickArg) =>
            setNewSlot({ date: toIsoDate(arg.date), time: `${String(arg.date.getHours()).padStart(2, '0')}:${String(arg.date.getMinutes()).padStart(2, '0')}` })
          }
          datesSet={(arg: DatesSetArg) => {
            const last = new Date(arg.end)
            last.setDate(last.getDate() - 1)
            setRange({ from: toIsoDate(arg.start), to: toIsoDate(last) })
          }}
        />
      </div>

      {!lessons.loading && lessons.data?.length === 0 && (
        <p className="calendar__empty">
          No lessons this week. Click a free slot to add one, or set up a weekly lesson with “New lesson”.
        </p>
      )}

      <LessonDialog
        key={openLesson?.id ?? 'closed'}
        lesson={openLesson}
        onClose={() => setOpenLessonId(null)}
        onChanged={lessons.reload}
      />
      <NewLessonDialog
        key={newSlot ? `${newSlot.date}-${newSlot.time}` : 'closed'}
        slot={newSlot}
        students={students.data ?? []}
        onClose={() => setNewSlot(null)}
        onCreated={() => {
          setNewSlot(null)
          lessons.reload()
        }}
      />
    </div>
  )
}

function addMinutes(time: string, minutes: number): string {
  const [h, m] = time.split(':').map(Number)
  const total = h * 60 + m + minutes
  return `${String(Math.floor(total / 60)).padStart(2, '0')}:${String(total % 60).padStart(2, '0')}:00`
}
