import type { ReactNode } from 'react'
import type { ApiError } from '../api'

/** A skeleton in the shape of what is coming: a few rows of the given height. */
export function Skeleton({ rows = 4, height = 44 }: { rows?: number; height?: number }) {
  return (
    <div className="skeleton" aria-busy="true" aria-label="Loading">
      {Array.from({ length: rows }, (_, i) => (
        <div key={i} className="skeleton__row" style={{ height }} />
      ))}
    </div>
  )
}

export function PageLoading() {
  return (
    <div className="page-loading" aria-busy="true">
      <span className="spinner keep-motion" aria-hidden="true" />
      <span>Opening your desk…</span>
    </div>
  )
}

export function ErrorState({ error, onRetry }: { error: ApiError; onRetry?: () => void }) {
  return (
    <div className="state state--error" role="alert">
      <p className="state__title">This could not be loaded.</p>
      <p className="state__text">{error.message}</p>
      {onRetry && (
        <button type="button" className="btn btn--card" onClick={onRetry}>
          Try again
        </button>
      )}
    </div>
  )
}

export function EmptyState({ title, text, action }: { title: string; text: string; action?: ReactNode }) {
  return (
    <div className="state">
      <p className="state__title">{title}</p>
      <p className="state__text">{text}</p>
      {action}
    </div>
  )
}

/** A form-level error line; the message comes from the API's ProblemDetails. */
export function FormError({ error }: { error: string | null }) {
  return (
    <p className="form-error" role="alert" aria-live="polite">
      {error}
    </p>
  )
}
