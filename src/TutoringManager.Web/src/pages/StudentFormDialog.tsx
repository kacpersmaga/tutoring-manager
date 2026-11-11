import { useState, type FormEvent } from 'react'
import { api, ApiError, type StudentDetails } from '../api'
import { Dialog } from '../components/Dialog'
import { Field } from '../components/Field'
import { FormError } from '../components/States'

interface Props {
  open: boolean
  student?: StudentDetails // editing when given, creating otherwise
  onClose: () => void
  onSaved: (student: StudentDetails) => void
}

export function StudentFormDialog({ open, student, onClose, onSaved }: Props) {
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = new FormData(event.currentTarget)
    const body = {
      fullName: form.get('fullName'),
      email: form.get('email'),
      ratePerLesson: Number(String(form.get('ratePerLesson')).replace(',', '.')),
      isActive: student ? form.get('isActive') === 'on' : true,
      bankAccounts: form.get('account') ? [form.get('account')] : [],
    }
    setBusy(true)
    setErrors({})
    setError(null)
    try {
      const saved = student
        ? await api.put<StudentDetails>(`/api/students/${student.id}`, body)
        : await api.post<StudentDetails>('/api/students', body)
      onSaved(saved)
    } catch (e) {
      const apiError = e as ApiError
      const fieldErrors = Object.fromEntries(Object.entries(apiError.errors).map(([k, v]) => [k.toLowerCase(), v[0]]))
      setErrors(fieldErrors)
      setError(Object.keys(fieldErrors).length ? null : apiError.message)
    } finally {
      setBusy(false)
    }
  }

  return (
    <Dialog title={student ? 'Edit student' : 'New student'} open={open} onClose={onClose}>
      <form className="form" onSubmit={submit} noValidate>
        <Field label="Full name" name="fullName" autoComplete="off" defaultValue={student?.fullName} required error={errors.fullname} />
        <Field label="Email for lesson reminders" name="email" type="email" autoComplete="off" defaultValue={student?.email} required
          hint="A parent’s address works too." error={errors.email} />
        <Field label="Rate per lesson (zł)" name="ratePerLesson" inputMode="decimal" defaultValue={student?.ratePerLesson ?? ''} required
          hint={student ? 'Applies to lessons you add from now on.' : undefined} error={errors.rateperlesson} />
        {!student && (
          <Field label="Bank account they pay from (optional)" name="account" inputMode="numeric" autoComplete="off"
            hint="26 digits. Transfers from it are matched automatically." error={errors.accountnumber} />
        )}
        {student && (
          <label className="check">
            <input type="checkbox" name="isActive" defaultChecked={student.isActive} />
            <span>Active student</span>
          </label>
        )}
        <FormError error={error} />
        <footer className="dialog__footer">
          <span />
          <button type="submit" className="btn btn--ink" aria-busy={busy} disabled={busy}>
            {busy ? 'Saving…' : student ? 'Save changes' : 'Add student'}
          </button>
        </footer>
      </form>
    </Dialog>
  )
}
