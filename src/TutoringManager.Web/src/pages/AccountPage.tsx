import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { api, ApiError } from '../api'
import { useAuth } from '../auth'
import { Field } from '../components/Field'
import { FormError } from '../components/States'

export function AccountPage() {
  const { me } = useAuth()
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function deleteAccount(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const password = new FormData(event.currentTarget).get('password')
    if (!window.confirm('Delete your account and everything in it? This cannot be undone.')) return
    setBusy(true)
    setError(null)
    try {
      await api.post('/api/auth/delete-account', { password })
      window.location.assign('/')
    } catch (e) {
      setError((e as ApiError).message)
      setBusy(false)
    }
  }

  return (
    <div className="page page--narrow">
      <header className="page__header">
        <h1>Account</h1>
      </header>
      <dl className="facts">
        <div><dt>Name</dt><dd>{me?.displayName}</dd></div>
        <div><dt>Email</dt><dd>{me?.email}</dd></div>
        <div><dt>Time zone</dt><dd>{me?.timeZoneId}</dd></div>
      </dl>
      <p className="muted">
        What is stored and why: <Link to="/privacy" className="link">Privacy</Link>.
      </p>

      <section aria-labelledby="delete-title" className="danger">
        <h2 id="delete-title">Delete account</h2>
        <p className="muted">Erases your students, lessons, notes and imported statements, then your sign-in. This cannot be undone.</p>
        {me?.isDemo ? (
          <p className="muted">The demo account cannot be deleted; use Reset demo instead.</p>
        ) : (
          <form className="form form--narrow" onSubmit={deleteAccount}>
            <Field label="Your password, to confirm" name="password" type="password" autoComplete="current-password" required />
            <FormError error={error} />
            <button type="submit" className="btn btn--card btn--danger" aria-busy={busy} disabled={busy}>
              {busy ? 'Deleting…' : 'Delete my account'}
            </button>
          </form>
        )}
      </section>
    </div>
  )
}
