import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { api, ApiError } from '../api'
import { useAuth } from '../auth'
import { Field } from '../components/Field'
import { FormError } from '../components/States'

export function AccountPage() {
  const { me, signOut } = useAuth()
  const navigate = useNavigate()
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
      <div className="page__actions">
        {me?.isDemo && (
          <button type="button" className="btn btn--card" onClick={async () => {
            if (!window.confirm('Put the demo account back to its starting state? Your changes in it are lost.')) return
            await api.post('/api/demo/reset')
            window.location.assign('/app')
          }}>
            Reset demo
          </button>
        )}
        <button type="button" className="btn btn--card" onClick={async () => { await signOut(); navigate('/app/login') }}>
          Sign out
        </button>
      </div>
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
