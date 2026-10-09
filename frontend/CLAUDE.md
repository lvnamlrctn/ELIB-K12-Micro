# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
npm run dev        # Dev server on port 3000 with Gemini API key injected
npm run start      # ng serve (default port 4200)
npm run build      # Production build
npm test           # Unit tests via Vitest
npm run lint       # ESLint with angular-eslint
npm run serve:ssr:app  # Run SSR server after build
```

## Environment Setup

Copy `.env.example` to `.env` and set:
- `GEMINI_API_KEY` — required for AI features
- `APP_URL` — injected at runtime by AI Studio

The dev server proxies `/api/Auth` and `/api` to `http://103.97.134.58:8091` via `proxy.conf.json`. The `npm run dev` script injects `GEMINI_API_KEY` via `--define` flag.

## Architecture

**Angular 21 standalone admin dashboard** for a library management system with i18n (vi/en).

### Key Structural Decisions

- **All components are standalone** (`standalone: true`) — no NgModules. Import dependencies directly in each component's `imports` array.
- **State via Angular Signals** (`signal<T>()`) — not a global store. Components own their local state (modals, loading flags, selection).
- **RxJS for async** — services return Observables. Components use `async` pipe or subscribe with `takeUntil(destroy$)` for cleanup in `ngOnDestroy`.

### Data Flow

1. Components inject services via Angular DI
2. Services call `HttpClient` (configured in `app.config.ts` with fetch adapter)
3. `AuthInterceptor` automatically appends `Authorization: Bearer <token>` and `Accept-Language` header to every outgoing request
4. Auth token stored in `localStorage`, managed by `AuthService`

### Routing

- `/login` — public, no guard
- `/admin/**` — protected by `authGuard` (redirects to `/login` if unauthenticated)
- All admin sub-routes lazy-load standalone components

### Page Component Pattern

Most admin pages follow this consistent pattern:
- Load data into a table on init
- Add/Edit via Angular Material modal dialog (`MatDialog`)
- Bulk selection with checkboxes, bulk delete
- Confirmation dialog before destructive operations
- `MatPaginator` with Vietnamese translations (configured globally in `app.config.ts`)

### i18n

Default language is Vietnamese (`vi`). `@ngx-translate` handles runtime translation. Translation files are in `public/i18n/en.json` and `public/i18n/vi.json`. Use `TranslateService` and the `translate` pipe in templates.

### Styling

Tailwind CSS 4 + Angular Material 21. Material component overrides are in `styles.css`. Do not add raw CSS outside of `styles.css` or component-scoped styles.
