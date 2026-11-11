# Tutoring Manager

Students, lesson notes, a calendar with recurring lessons, attendance confirmation by email and bank-statement reconciliation for private tutors.


## The problem

A private tutor juggles a weekly schedule, notes for each student, "is she coming tomorrow?" messages and a bank account full of transfers that need to be matched to lessons. Spreadsheets and a phone calendar get out of sync quickly.

## Run locally

Requirements: Docker.

```bash
docker compose up --build
```

| What | Where |
| --- | --- |
| App | http://localhost:8080 |
| Mailpit (all outgoing email, including attendance links) | http://localhost:8025 |
| Health check | http://localhost:8080/health |

## Tests

Requirements: .NET 10 SDK and Docker (integration tests start a real PostgreSQL with Testcontainers).

```bash
dotnet test
```

## Design decisions

- **One API project, folders per feature** (`Auth/`, `Students/`, `Lessons/`, ...). Minimal APIs with plain services. No MediatR, CQRS, AutoMapper or repository wrappers around EF Core: at this size they add indirection without removing any complexity.
- **PostgreSQL with snake_case names** (`EFCore.NamingConventions`). Column names match PostgreSQL conventions, so the raw SQL used in a few places (exclusion constraint, email queue) reads naturally.
- **Migrations run on startup** (`Database.MigrateAsync`). Simplest option for a single instance run locally. With several instances you would run them as a separate deployment step.
- **Tests use a real PostgreSQL** (Testcontainers) instead of the in-memory provider, because key guarantees live in the database (constraints, `FOR UPDATE SKIP LOCKED`).
- **Cookie sign-in instead of JWT.** The SPA and the API share one origin, so an HttpOnly cookie is the simplest secure option: JavaScript never sees the session, and there is no refresh-token dance. The price is CSRF, handled with antiforgery tokens (see Security).
- **Tenant isolation in one place.** Every tutor-owned table has `TutorId` and a global query filter in `AppDbContext`, so a forgotten `Where` cannot leak another tutor's data. Another tutor's resources answer 404, not 403, so their existence is not revealed.
- **Expected errors as exceptions.** Services throw `ApiException` (404, 409, validation) and one `IExceptionHandler` turns it into ProblemDetails. Endpoints stay one line long and every error has the same shape.
- **Time: local time + zone is the source of truth.** A lesson stores `LocalDate`, `LocalStartTime` and `TimeZoneId`; `StartUtc`/`EndUtc` are derived on save and used for queries. "Every Tuesday at 17:00" stays at 17:00 after a daylight saving change. A time inside the spring-forward gap is moved forward, the way clocks move.
- **Recurring lessons are generated rows, not a rule evaluated on the fly.** A series creates real `Lesson` rows 12 weeks ahead and a daily worker tops it up. An exception (moved, cancelled or annotated lesson) is just an edit of that row. Editing the series replaces only future lessons that were not touched individually; each lesson remembers which series date it came from (`SeriesOccurrenceDate`), so a moved lesson never gets a duplicate. Queries stay simple and notes and attendance hang off a concrete row.
- **The database guarantees no overlapping lessons**, not just the code: an exclusion constraint `EXCLUDE USING gist (tutor_id WITH =, tstzrange(start_utc, end_utc) WITH &&) WHERE (status <> 'Cancelled')`. Two concurrent requests cannot both win. The service still checks first, so the user gets a readable 409 ("overlaps with Anna on 2027-01-20 at 17:00").
- **"Completed" is set by the tutor**, one lesson at a time or with "mark all past lessons as completed". An automatic rule would bill lessons that silently did not happen.
- **Attendance links carry a random token, the database only its hash.** 32 bytes from `RandomNumberGenerator`, Base64Url in the URL, SHA-256 in the database: a leaked database dump contains no working links. Moving or cancelling a lesson revokes its links; a link expires when the lesson starts. The page behind the link shows only the date, time and the tutor's name.
- **The email queue is a table, not a message broker.** `ScheduledEmails` rows are written in the same transaction as the lesson, so a lesson can never exist without its email (no dual-write problem). A `BackgroundService` polls every 30 s and claims a batch with `UPDATE … WHERE id IN (SELECT … FOR UPDATE SKIP LOCKED) RETURNING *`: several app instances never take the same row, and none waits for another. A claimed row is leased for 5 minutes; if the instance dies, the lease expires and another instance takes over. That makes delivery **at-least-once**: a crash after the SMTP server accepted the email but before the row was marked as sent means the student gets it twice. Exactly-once delivery over SMTP does not exist; a duplicate reminder is harmless.
- **Retries with backoff:** 1, 5, 25 and 60 minutes; after 5 attempts the email is `Failed` and the tutor sees it on the lesson.
- **When to send:** 24 hours before the lesson, or right away if the lesson is sooner. Moving a lesson cancels the pending email and queues a new one; cancelling a lesson cancels it. The attendance token is created when the email is sent, so the raw token never sits in the database.
- **Optimistic concurrency with PostgreSQL's `xmin`**: a lesson edited in two tabs answers 409 instead of silently overwriting, without an extra version column.
- **Lesson price is copied from the student's rate** when the lesson is created, so changing the rate does not rewrite past invoices.
- **Bank accounts are stored normalized** (26 digits, no `PL`, no spaces) and checked with the IBAN mod-97 checksum, so a typo is caught when the account is entered, not when a payment fails to match.

## Security

- **Passwords:** ASP.NET Core Identity (PBKDF2 hashing), minimum 12 characters, no composition rules (NIST SP 800-63B). Account locked for 15 minutes after 5 failed sign-ins. Unknown email and wrong password get the same answer.
- **Session cookie:** `HttpOnly`, `SameSite=Strict`, `Secure` when `Security__HttpsOnly=true` (production), 7-day sliding expiry.
- **CSRF:** every state-changing `/api` request must carry an `X-XSRF-TOKEN` header matching the antiforgery cookie (double submit). The token is bound to the signed-in user.
- **Isolation:** global query filter per tutor, covered by tests that tutor B gets 404 on tutor A's data.
- **Rate limiting** (built-in ASP.NET Core limiter, per IP): sign-in and registration 10/min, public attendance endpoints 30/min.
- **Attendance tokens:** 256 bits of randomness, only the SHA-256 hash stored, expire at lesson start, revoked when the lesson is moved or cancelled. Malformed, unknown and revoked tokens all get the same 404.


## What's deliberately missing

Queues, Redis, microservices, Kubernetes, CQRS, student accounts, online payments, SMS, a mobile app. Each would add moving parts without solving a problem this product has today.

## Roadmap

Late-cancellation fee, SMS reminders, student accounts, online payments, more banks, PDF export, a queue once there are thousands of emails per minute or other systems reacting to events.
