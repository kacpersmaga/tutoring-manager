import { useEffect, useState, type FormEvent } from 'react'
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom'
import { api, ApiError } from '../api'
import { useAuth } from '../auth'
import { Field } from '../components/Field'
import { FormError } from '../components/States'

/** The form on paper on the left; the product itself, on the desk, on the right (hidden on phones). */
function AuthLayout({ title, lead, children }: { title: string; lead: string; children: React.ReactNode }) {
  return (
    <div className="auth">
      <main className="auth__panel">
        <Link to="/" className="wordmark auth__wordmark">Tutoring Manager</Link>
        <section className="auth__form" aria-labelledby="auth-title">
          <div className="auth__head">
            <h1 id="auth-title">{title}</h1>
            <p className="auth__lead">{lead}</p>
          </div>
          {children}
        </section>
        <Link to="/privacy" className="auth__privacy">Privacy</Link>
      </main>
      <aside className="auth__showcase desk" aria-hidden="true">
        <p className="auth__line">Every lesson confirmed. Every złoty matched.</p>
        <img className="auth__shot stock" src="/product/calendar.jpg" width="2384" height="1530" alt="" />
      </aside>
    </div>
  )
}

export function LoginPage() {
  const { me, signIn } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [demo, setDemo] = useState<{ email: string; password: string } | null>(null)

  useEffect(() => {
    api.get<{ email: string; password: string }>('/api/demo').then(setDemo).catch(() => setDemo(null))
  }, [])

  if (me) return <Navigate to="/app" replace />

  async function signInWith(email: string, password: string) {
    setBusy(true)
    setError(null)
    try {
      await signIn(email, password)
      navigate((location.state as { from?: string } | null)?.from ?? '/app', { replace: true })
    } catch (e) {
      setError((e as ApiError).message)
    } finally {
      setBusy(false)
    }
  }

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = new FormData(event.currentTarget)
    signInWith(String(form.get('email')), String(form.get('password')))
  }

  return (
    <AuthLayout title="Sign in" lead="Your week, your students and who has paid.">
      <form className="form" onSubmit={submit} noValidate>
        <Field label="Email" name="email" type="email" autoComplete="email" required />
        <Field label="Password" name="password" type="password" autoComplete="current-password" required />
        <FormError error={error} />
        <button type="submit" className="btn btn--ink btn--block" aria-busy={busy} disabled={busy}>
          {busy ? 'Signing in…' : 'Sign in'}
        </button>
      </form>
      <p className="auth__switch">
        New here? <Link to="/app/register">Create an account</Link>
      </p>
      {demo && (
        <div className="auth__demo">
          <p><strong>Just looking?</strong> The demo account has two months of lessons, notes and payments.</p>
          <button type="button" className="btn btn--card btn--block" disabled={busy} onClick={() => signInWith(demo.email, demo.password)}>
            Open the demo account
          </button>
        </div>
      )}
    </AuthLayout>
  )
}

export function RegisterPage() {
  const { me, register } = useAuth()
  const navigate = useNavigate()
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  if (me) return <Navigate to="/app" replace />

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = new FormData(event.currentTarget)
    const password = String(form.get('password'))
    if (password.length < 12) {
      setErrors({ password: 'Use at least 12 characters. A short sentence works well.' })
      return
    }

    setBusy(true)
    setErrors({})
    setError(null)
    try {
      await register(String(form.get('email')), password, String(form.get('displayName')))
      navigate('/app', { replace: true })
    } catch (e) {
      const apiError = e as ApiError
      setErrors(Object.fromEntries(Object.entries(apiError.errors).map(([k, v]) => [k.toLowerCase(), v[0]])))
      setError(Object.keys(apiError.errors).length ? null : apiError.message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <AuthLayout title="Create your account" lead="Free during beta. Your data stays in your own database.">
      <form className="form" onSubmit={submit} noValidate>
        <Field label="Your name" name="displayName" autoComplete="name" required hint="Students see it on their confirmation page." error={errors.displayname} />
        <Field label="Email" name="email" type="email" autoComplete="email" required error={errors.email} />
        <Field label="Password" name="password" type="password" autoComplete="new-password" required minLength={12} hint="At least 12 characters." error={errors.password} />
        <FormError error={error} />
        <button type="submit" className="btn btn--ink btn--block" aria-busy={busy} disabled={busy}>
          {busy ? 'Creating your account…' : 'Create account'}
        </button>
      </form>
      <p className="auth__switch">
        Already have an account? <Link to="/app/login">Sign in</Link>
      </p>
    </AuthLayout>
  )
}
