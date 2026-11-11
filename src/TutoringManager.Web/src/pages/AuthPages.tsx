import { useState, type FormEvent } from 'react'
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom'
import { ApiError } from '../api'
import { useAuth } from '../auth'
import { Field } from '../components/Field'
import { FormError } from '../components/States'

function AuthLayout({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <main className="desk auth">
      <Link to="/" className="wordmark auth__wordmark">
        Tutoring Manager
      </Link>
      <section className="stock auth__sheet" aria-labelledby="auth-title">
        <h1 id="auth-title">{title}</h1>
        {children}
      </section>
    </main>
  )
}

export function LoginPage() {
  const { me, signIn } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  if (me) return <Navigate to="/app" replace />

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = new FormData(event.currentTarget)
    setBusy(true)
    setError(null)
    try {
      await signIn(String(form.get('email')), String(form.get('password')))
      navigate((location.state as { from?: string } | null)?.from ?? '/app', { replace: true })
    } catch (e) {
      setError((e as ApiError).message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <AuthLayout title="Sign in to your desk">
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
    <AuthLayout title="Set up your desk">
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
