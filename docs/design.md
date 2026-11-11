# Design direction: Tutoring Manager

Theme **Coral**. Every screen of the app, the public attendance page and the landing page follow this file.

## Idea

A tutor's desk: warm grey paper, cotton stationery lit from the left, one coral ink pressed in only where something is settled. Lessons, notes and statements are paperwork the product handles for the tutor, so they are drawn as paper objects (index cards, ledger sheets) rather than generic cards.

**Signature:** the week as a desk. Each lesson in the calendar is a cotton index card; its attendance answer is visible at a glance:

| Lesson state | Treatment |
| --- | --- |
| Scheduled, no answer | plain cotton card |
| Confirmed by the student | "Coming" pressed into the card in coral (the settled state) |
| Declined | ink outline, "Not coming" |
| Completed | quiet paper-3 card, "Done" |
| Cancelled | faded, name struck through |
| Email failed | ink outline tag "Email failed" |

## Tokens

All colours and fonts come from `src/TutoringManager.Web/src/styles/tokens.css` (OKLCH). Nothing outside the token block uses a literal colour or font family.

- **Paper:** `--color-paper` (page), `--color-paper-2/3` (wells, hover), `--color-cotton` (stationery), `--color-desk` (the public page and the landing hero).
- **Ink:** `--color-ink`, `--color-ink-2`, `--color-muted` (labels, helper text).
- **Accent:** coral, spent only on the settled state (`--color-paid`), focus (`--color-focus`) and link hovers. Never a band, a fill behind text or a heading.
- **Board:** `--color-board` for the one primary control per screen.

## Type

- General Sans 400/500/600 throughout; headings 600, tracking -0.03em, sentence case.
- Geist Mono only for figures in columns (amounts, times, account numbers).
- App scale fixed at 1.2 (body 16px). Labels: body face a step smaller, sentence case, `--color-muted`. Never tracked capitals.

## Surfaces and controls

- Shell: a top bar (Calendar, Students, Payments, account). Fewer than seven destinations, so no sidebar.
- Controls are letterpress cards: cotton with a lit top-left edge and a cast falling right. The primary is ink board. Hover lifts 1px, active presses 1px.
- Inputs: 40px in the app, 6px radius, 1px `--color-muted` border, coral focus ring (outline, never a border change).
- Tables before cards: hairline rows, no outer border, figures right-aligned in Geist Mono.
- Dialogs: native `<dialog>`, one filled primary per dialog.

## States

Every region that loads data has loading (a skeleton in the shape of the content), empty (what belongs here and the one action that fills it), error (what broke and what to do) and full.

## Motion

State only: `--dur-micro` / `--dur-short`, `transform` and `opacity`. No reveals in the app. Everything has a `prefers-reduced-motion` fallback.

## Voice

Plain, warm second person about lessons and money: who, when, how much. Never cute, no exclamation marks.
