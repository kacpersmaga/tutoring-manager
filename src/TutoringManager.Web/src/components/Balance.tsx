import { formatBalance } from '../format'

/** A running balance: positive is credit, negative is owed. Colour is never the only signal; the sign and the words carry it. */
export function Balance({ amount, withLabel }: { amount: number; withLabel?: boolean }) {
  const tone = amount < 0 ? 'owed' : amount > 0 ? 'credit' : 'settled'
  const label = amount < 0 ? 'owes' : amount > 0 ? 'in credit' : 'settled'
  return (
    <span className={`balance balance--${tone} fig`}>
      {formatBalance(amount)}
      {withLabel && <span className="balance__label">{label}</span>}
    </span>
  )
}
