import { Link } from 'react-router-dom'

export function NotFoundPage() {
  return (
    <div className="state state--page">
      <p className="state__title">This page does not exist.</p>
      <p className="state__text">The link may be old, or the item was deleted.</p>
      <Link to="/app" className="btn btn--card">
        Back to the calendar
      </Link>
    </div>
  )
}
