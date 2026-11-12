import type { InputHTMLAttributes } from 'react'

interface FieldProps extends InputHTMLAttributes<HTMLInputElement> {
  label: string
  name: string
  error?: string
  hint?: string
}

/** A labelled input with a reserved helper line, so an error never shifts the layout. */
export function Field({ label, name, error, hint, ...input }: FieldProps) {
  const helpId = `${name}-help`
  return (
    <div className="field">
      <label className="field__label" htmlFor={name}>
        {label}
      </label>
      <input
        id={name}
        name={name}
        className="input"
        aria-invalid={error ? true : undefined}
        aria-describedby={error || hint ? helpId : undefined}
        {...input}
      />
      {(error || hint) && (
        <p id={helpId} className={error ? 'field__help field__help--error' : 'field__help'}>
          {error ?? hint}
        </p>
      )}
    </div>
  )
}
