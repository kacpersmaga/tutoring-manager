import { Link } from 'react-router-dom'

export function PrivacyPage() {
  return (
    <main className="prose-page">
      <Link to="/" className="wordmark">Tutoring Manager</Link>
      <article className="prose">
        <h1>Privacy</h1>
        <p>Tutoring Manager keeps only what a tutor needs to plan lessons and settle payments. Many students are under 18, so we ask for as little as possible about them.</p>

        <h2>What is stored</h2>
        <dl className="prose__list">
          <dt>About you, the tutor</dt>
          <dd>Your name, your email and a hash of your password. We never see the password itself.</dd>
          <dt>About your students</dt>
          <dd>Name, the email that receives lesson reminders (often a parent’s), the rate per lesson, the bank accounts they pay from, their lessons and your notes on them.</dd>
          <dt>From your bank statements</dt>
          <dd>Incoming transfers only: date, amount, sender’s name and account, and the title. Outgoing payments are skipped when the file is read.</dd>
        </dl>

        <h2>Why</h2>
        <p>To show your calendar, email students a link to confirm a lesson 24 hours before it, and match transfers to students. Nothing is used for advertising, shared or sold.</p>

        <h2>The confirmation link</h2>
        <p>The page behind it shows only the lesson’s date and time and your name. The link stops working when the lesson starts, or when you move or cancel the lesson.</p>

        <h2>Deleting data</h2>
        <p>Deleting a student erases their lessons, notes and saved accounts at once; their transfers stay in your records without a name. Deleting your account (Account, then Delete account) erases everything you stored.</p>

        <h2>Cookies</h2>
        <p>One cookie keeps you signed in and one protects your forms against forged requests. There are no tracking or advertising cookies.</p>
      </article>
    </main>
  )
}
