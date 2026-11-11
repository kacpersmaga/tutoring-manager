import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter, Route, Routes } from 'react-router-dom'
import { AuthProvider, RequireAuth } from './auth'
import { Shell } from './components/Shell'
import { AccountPage } from './pages/AccountPage'
import { AttendancePage } from './pages/AttendancePage'
import { CalendarPage } from './pages/CalendarPage'
import { LandingPage } from './pages/LandingPage'
import { LoginPage, RegisterPage } from './pages/AuthPages'
import { NotFoundPage } from './pages/NotFoundPage'
import { PaymentsPage } from './pages/PaymentsPage'
import { PrivacyPage } from './pages/PrivacyPage'
import { StudentPage } from './pages/StudentPage'
import { StudentsPage } from './pages/StudentsPage'
import './styles/app.css'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<LandingPage />} />
        <Route path="/a/:token" element={<AttendancePage />} />
        <Route path="/privacy" element={<PrivacyPage />} />
        <Route
          path="/app/*"
          element={
            <AuthProvider>
              <Routes>
                <Route path="login" element={<LoginPage />} />
                <Route path="register" element={<RegisterPage />} />
                <Route
                  element={
                    <RequireAuth>
                      <Shell />
                    </RequireAuth>
                  }
                >
                  <Route index element={<CalendarPage />} />
                  <Route path="students" element={<StudentsPage />} />
                  <Route path="students/:id" element={<StudentPage />} />
                  <Route path="payments" element={<PaymentsPage />} />
                  <Route path="account" element={<AccountPage />} />
                  <Route path="*" element={<NotFoundPage />} />
                </Route>
              </Routes>
            </AuthProvider>
          }
        />
        <Route path="*" element={<NotFoundPage />} />
      </Routes>
    </BrowserRouter>
  </StrictMode>,
)
