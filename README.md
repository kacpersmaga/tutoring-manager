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

## Security


## What's deliberately missing

Queues, Redis, microservices, Kubernetes, CQRS, student accounts, online payments, SMS, a mobile app. Each would add moving parts without solving a problem this product has today.

## Roadmap

Late-cancellation fee, SMS reminders, student accounts, online payments, more banks, PDF export, a queue once there are thousands of emails per minute or other systems reacting to events.
