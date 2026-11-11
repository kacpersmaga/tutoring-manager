import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import '../styles/landing.css'

const atWork = [
  {
    title: 'The week at a glance',
    text: 'Every lesson is a card. “Coming” is pressed in the moment a student answers.',
    image: '/product/calendar.jpg',
    width: 2384,
    height: 1530,
    alt: 'The weekly calendar with lessons marked done, coming, not coming and asked',
  },
  {
    title: 'Notes that follow the student',
    text: 'One note per lesson. The student’s page lists them newest first, beside what they paid.',
    image: '/product/student.jpg',
    width: 2384,
    height: 1718,
    alt: 'A student page with lesson notes, weekly lesson, bank account and payments',
  },
  {
    title: 'A balance, not a guess',
    text: 'Import the bank statement. Transfers match by account; the month shows who owes what.',
    image: '/product/payments.jpg',
    width: 2384,
    height: 1264,
    alt: 'The monthly report with due, paid and running balance per student',
  },
]

const questions = [
  ['Where does my data live?', 'In your own PostgreSQL database. Nothing is shared, sold or used for ads.'],
  ['What does it cost?', 'Nothing while in beta.'],
  ['Which banks can I import from?', 'mBank CSV statements today. Each bank is one small parser, so more are on the way.'],
  ['Can I delete everything?', 'Deleting a student erases their lessons and notes. Deleting your account erases it all.'],
]

/** The "at work" run: the screenshot stays put while the three panels scroll past and swap it. */
function useActivePanel(count: number) {
  const [active, setActive] = useState(0)
  const panels = useRef<(HTMLElement | null)[]>([])

  useEffect(() => {
    const observer = new IntersectionObserver(
      (entries) => {
        for (const entry of entries) {
          if (entry.isIntersecting) setActive(Number((entry.target as HTMLElement).dataset.index))
        }
      },
      { rootMargin: '-45% 0px -45% 0px' },
    )
    panels.current.slice(0, count).forEach((panel) => panel && observer.observe(panel))
    return () => observer.disconnect()
  }, [count])

  return { active, panels }
}

export function LandingPage() {
  const { active, panels } = useActivePanel(atWork.length)

  return (
    <div className="landing">
      <a className="skip-link" href="#content">Skip to content</a>
      <header className="l-nav">
        <nav className="l-nav__pill" aria-label="Main">
          <Link to="/" className="wordmark" aria-label="Tutoring Manager, home">Tutoring Manager</Link>
          <a href="#how" className="l-nav__link">How it works</a>
          <a href="#questions" className="l-nav__link">Questions</a>
          <Link to="/app/login" className="btn btn--card btn--small">Sign in</Link>
        </nav>
      </header>

      <main id="content">
        <section className="l-hero desk">
          <div className="container l-hero__grid">
            <div className="l-hero__text">
              <h1>Every lesson confirmed. Every złoty matched.</h1>
              <p className="lead">Weekly lessons, notes, attendance links and bank-statement matching, for private tutors.</p>
              <div className="l-actions">
                <Link to="/app/login" className="btn btn--ink btn--large">Try the demo account</Link>
                <Link to="/app/register" className="btn btn--card btn--large">Create an account</Link>
              </div>
              <p className="l-terms">Free during beta. Runs on your machine with one command.</p>
            </div>
            <figure className="l-hero__shot stock" data-slot="hero" data-kind="product">
              <img src="/product/calendar.jpg" width="2384" height="1530" alt="The tutor’s week: lesson cards marked done, coming, not coming and asked" />
            </figure>
          </div>
        </section>

        <section className="l-section" aria-labelledby="evening-title">
          <div className="container l-rows">
            <h2 id="evening-title">Three questions every evening</h2>
            <dl className="l-rows__list">
              <div>
                <dt>Is Ola coming tomorrow?</dt>
                <dd>Students get a link the day before and tap an answer. No texting round.</dd>
              </div>
              <div>
                <dt>What did we cover last time?</dt>
                <dd>Each lesson keeps one note, and the student’s page lists them newest first.</dd>
              </div>
              <div>
                <dt>Who paid for October?</dt>
                <dd>Your bank statement goes in; transfers find their students by account number.</dd>
              </div>
            </dl>
          </div>
        </section>

        <section className="l-section l-section--tight" aria-labelledby="work-title">
          <div className="container">
            <h2 id="work-title">The desk, at work</h2>
            <div className="l-pinned">
              <div className="l-pinned__visual">
                <figure className="l-pinned__frame stock" data-slot="at-work" data-kind="product">
                  {atWork.map((item, index) => (
                    <img key={item.image} src={item.image} width={item.width} height={item.height} alt={item.alt}
                      className={index === active ? 'is-active' : undefined} aria-hidden={index !== active} />
                  ))}
                </figure>
              </div>
              <ol className="l-pinned__panels">
                {atWork.map((item, index) => (
                  <li key={item.title} data-index={index} ref={(el) => { panels.current[index] = el }}
                    className={index === active ? 'is-active' : undefined}>
                    <h3>{item.title}</h3>
                    <p>{item.text}</p>
                  </li>
                ))}
              </ol>
            </div>
          </div>
        </section>

        <section className="l-section l-band" aria-labelledby="student-title">
          <div className="container l-split">
            <div className="l-split__text">
              <h2 id="student-title">What your student sees</h2>
              <p className="lead">One email, one tap. No account, no app.</p>
              <dl className="l-facts">
                <div><dt>Only the essentials</dt><dd>The date, the time and your name.</dd></div>
                <div><dt>Answers until the start</dt><dd>The link closes when the lesson begins.</dd></div>
                <div><dt>Moved lesson, new link</dt><dd>The old one stops working at once.</dd></div>
              </dl>
            </div>
            <figure className="l-phone stock" data-slot="student" data-kind="product">
              <img src="/product/attendance-phone.jpg" width="1170" height="2532" alt="The attendance page on a phone with “I’ll be there” and “I can’t make it”" />
            </figure>
          </div>
        </section>

        <section className="l-section" id="how" aria-labelledby="how-title">
          <div className="container">
            <h2 id="how-title">From timetable to settled month</h2>
            <ol className="l-steps">
              <li>
                <h3>Add students and their weekly slot</h3>
                <p>Lessons are planned twelve weeks ahead and keep their hour across clock changes.</p>
              </li>
              <li>
                <h3>Students answer the day before</h3>
                <p>The link arrives 24 hours ahead; your calendar shows who is coming.</p>
              </li>
              <li>
                <h3>Import the statement at month end</h3>
                <p>Payments match by account; a parent’s transfer is assigned once and remembered.</p>
              </li>
            </ol>
          </div>
        </section>

        <section className="l-section l-section--tight" id="questions" aria-labelledby="faq-title">
          <div className="container l-rows">
            <h2 id="faq-title">Questions</h2>
            <dl className="l-rows__list">
              {questions.map(([q, a]) => (
                <div key={q}><dt>{q}</dt><dd>{a}</dd></div>
              ))}
            </dl>
          </div>
        </section>

        <section className="l-close desk" aria-labelledby="close-title">
          <div className="container l-close__inner">
            <h2 id="close-title">See a tutor’s real week</h2>
            <div className="l-actions">
              <Link to="/app/login" className="btn btn--ink btn--large">Try the demo account</Link>
              <Link to="/app/register" className="btn btn--card btn--large">Create an account</Link>
            </div>
          </div>
        </section>
      </main>

      <footer className="l-footer">
        <div className="container l-footer__inner">
          <p className="l-footer__sign">Built with .NET, PostgreSQL and React. Free during beta.</p>
          <nav aria-label="Footer" className="l-footer__links">
            <Link to="/privacy">Privacy</Link>
            <Link to="/app/login">Sign in</Link>
            <Link to="/app/register">Create an account</Link>
          </nav>
        </div>
      </footer>
    </div>
  )
}
