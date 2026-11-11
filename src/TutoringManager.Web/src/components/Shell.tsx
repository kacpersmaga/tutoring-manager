import { useState } from 'react'
import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import { api } from '../api'
import { useAuth } from '../auth'

export function Shell() {
  const { me, signOut } = useAuth()
  const navigate = useNavigate()
  const [resetting, setResetting] = useState(false)

  async function resetDemo() {
    if (!window.confirm('Put the demo account back to its starting state? Your changes in it are lost.')) return
    setResetting(true)
    await api.post('/api/demo/reset')
    window.location.assign('/app')
  }

  return (
    <div className="shell">
      <a className="skip-link" href="#main">
        Skip to content
      </a>
      <header className="topbar">
        <div className="topbar__inner">
          <NavLink to="/app" end className="wordmark" aria-label="Tutoring Manager, calendar">
            Tutoring Manager
          </NavLink>
          <nav className="topnav" aria-label="Main">
            <NavLink to="/app" end>
              Calendar
            </NavLink>
            <NavLink to="/app/students">Students</NavLink>
            <NavLink to="/app/payments">Payments</NavLink>
          </nav>
          <div className="topbar__account">
            <NavLink to="/app/account" className="topbar__name">{me?.displayName}</NavLink>
            {me?.isDemo && (
              <button type="button" className="btn btn--text" onClick={resetDemo} aria-busy={resetting} disabled={resetting}>
                {resetting ? 'Resetting…' : 'Reset demo'}
              </button>
            )}
            <button
              type="button"
              className="btn btn--text"
              onClick={async () => {
                await signOut()
                navigate('/app/login')
              }}
            >
              Sign out
            </button>
          </div>
        </div>
      </header>
      <main id="main" className="main">
        <Outlet />
      </main>
    </div>
  )
}
