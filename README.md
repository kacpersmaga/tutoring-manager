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
| Mailpit (all outgoing email) | http://localhost:8025 |
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
- **Bank accounts are stored normalized** (26 digits, no `PL`, no spaces) and checked with the IBAN mod-97 checksum, so a typo is caught when the account is entered, not when a payment fails to match.

## Security

- **Passwords:** ASP.NET Core Identity (PBKDF2 hashing), minimum 12 characters, no composition rules (NIST SP 800-63B). Account locked for 15 minutes after 5 failed sign-ins. Unknown email and wrong password get the same answer.
- **Session cookie:** `HttpOnly`, `SameSite=Strict`, `Secure` when `Security__HttpsOnly=true` (production), 7-day sliding expiry.
- **CSRF:** every state-changing `/api` request must carry an `X-XSRF-TOKEN` header matching the antiforgery cookie (double submit). The token is bound to the signed-in user.
- **Isolation:** global query filter per tutor, covered by tests that tutor B gets 404 on tutor A's data.
- **Rate limiting** (built-in ASP.NET Core limiter, per IP): sign-in and registration 10/min.


## What's deliberately missing

Queues, Redis, microservices, Kubernetes, CQRS, student accounts, online payments, SMS, a mobile app. Each would add moving parts without solving a problem this product has today.

## Roadmap

Late-cancellation fee, SMS reminders, student accounts, online payments, more banks, PDF export, a queue once there are thousands of emails per minute or other systems reacting to events.
