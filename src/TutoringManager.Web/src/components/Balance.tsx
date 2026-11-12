import { formatBalance } from '../format'

/** A running balance: positive is credit, negative is owed. Colour is never the only signal; the sign and the words carry it. */
export function Balance({ amount, withLabel, large }: { amount: number; withLabel?: boolean; large?: boolean }) {
  const tone = amount < 0 ? 'owed' : amount > 0 ? 'credit' : 'settled'
  const label = amount < 0 ? 'Owes' : amount > 0 ? 'In credit' : 'Settled'
  return (
    <span className={`balance balance--${tone}${large ? ' balance--large' : ''}`}>
      <span className="fig">{formatBalance(amount)}</span>
      {withLabel && <span className={`tag balance__tag balance__tag--${tone}`}>{label}</span>}
    </span>
  )
}
