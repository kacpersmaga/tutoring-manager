import { useEffect, useRef, type ReactNode } from 'react'

interface DialogProps {
  title: string
  open: boolean
  onClose: () => void
  children: ReactNode
  wide?: boolean
}

/** A native <dialog> opened with showModal(): focus trap, Escape and backdrop for free. */
export function Dialog({ title, open, onClose, children, wide }: DialogProps) {
  const ref = useRef<HTMLDialogElement>(null)

  useEffect(() => {
    const dialog = ref.current
    if (!dialog) return
    if (open && !dialog.open) dialog.showModal()
    if (!open && dialog.open) dialog.close()
  }, [open])

  return (
    <dialog
      ref={ref}
      className={wide ? 'dialog dialog--wide' : 'dialog'}
      aria-labelledby="dialog-title"
      onClose={onClose}
      onClick={(event) => {
        if (event.target === ref.current) onClose()
      }}
    >
      {open && (
        <div className="dialog__body">
          <header className="dialog__header">
            <h2 id="dialog-title">{title}</h2>
            <button type="button" className="icon-btn" aria-label="Close" onClick={onClose}>
              <svg width="16" height="16" viewBox="0 0 16 16" aria-hidden="true">
                <path d="M3 3l10 10M13 3L3 13" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" />
              </svg>
            </button>
          </header>
          {children}
        </div>
      )}
    </dialog>
  )
}
