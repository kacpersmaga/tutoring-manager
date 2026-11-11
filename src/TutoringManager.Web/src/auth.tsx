import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { api, refreshAntiforgery, type Me } from './api'
import { PageLoading } from './components/States'

interface AuthState {
  me: Me | null | undefined // undefined while checking the session
  signIn: (email: string, password: string) => Promise<void>
  register: (email: string, password: string, displayName: string) => Promise<void>
  signOut: () => Promise<void>
}

const AuthContext = createContext<AuthState | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [me, setMe] = useState<Me | null>()

  useEffect(() => {
    refreshAntiforgery()
      .then(() => api.get<Me>('/api/auth/me'))
      .then(setMe)
      .catch(() => setMe(null))
  }, [])

  const signIn = useCallback(async (email: string, password: string) => {
    const result = await api.post<Me>('/api/auth/login', { email, password })
    await refreshAntiforgery()
    setMe(result)
  }, [])

  const register = useCallback(async (email: string, password: string, displayName: string) => {
    const result = await api.post<Me>('/api/auth/register', { email, password, displayName })
    await refreshAntiforgery()
    setMe(result)
  }, [])

  const signOut = useCallback(async () => {
    await api.post('/api/auth/logout')
    await refreshAntiforgery()
    setMe(null)
  }, [])

  return <AuthContext.Provider value={{ me, signIn, register, signOut }}>{children}</AuthContext.Provider>
}

export function useAuth(): AuthState {
  const context = useContext(AuthContext)
  if (!context) throw new Error('useAuth must be used inside AuthProvider')
  return context
}

export function RequireAuth({ children }: { children: ReactNode }) {
  const { me } = useAuth()
  const location = useLocation()
  if (me === undefined) return <PageLoading />
  if (me === null) return <Navigate to="/app/login" replace state={{ from: location.pathname }} />
  return children
}
