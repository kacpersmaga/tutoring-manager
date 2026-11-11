import type { Lesson } from '../api'

/** The tag a lesson card carries: what the tutor needs to know at a glance. */
export function lessonTag(lesson: Lesson): { label: string; tone: string } | null {
  if (lesson.status === 'Cancelled') return { label: 'Cancelled', tone: 'cancelled' }
  if (lesson.status === 'Completed') return { label: 'Done', tone: 'done' }
  if (lesson.attendance === 'Confirmed') return { label: 'Coming', tone: 'confirmed' }
  if (lesson.attendance === 'Declined') return { label: 'Not coming', tone: 'declined' }
  if (lesson.emailStatus === 'Failed') return { label: 'Email failed', tone: 'failed' }
  if (lesson.emailStatus === 'Sent') return { label: 'Asked', tone: 'asked' }
  return null
}
