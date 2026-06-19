# Barberia Web

Fase 2 web scaffold for authenticated booking, customer accounts, barber panels and admin workflows.

## Stack

- Next.js App Router with TypeScript.
- Supabase Auth/PostgreSQL/RLS through `@supabase/ssr`.
- Login is the first screen at `/`; internal routes require a valid session through Next.js `proxy.ts`.
- Registration emails return to `/auth/confirm`; successful confirmation shows a user-confirmed notice on `/`.
- Password recovery starts at `/auth/reset-password`, returns through `/auth/confirm?next=/auth/update-password` and then saves the new password at `/auth/update-password`.
- Customer role redirects land on `/app/book` so `/app` can remain a role router without looping.
- Customer, barber and admin surfaces are separated by both proxy role checks and server-side guards.
- The shared UI baseline follows `design.md`: Inter/system typography, light tonal surfaces, royal-blue primary actions, crimson only for error/accent states, 8px standard radius and restrained borders/shadows.

## Local Setup

1. Copy `.env.example` to `.env.local`.
2. Fill `NEXT_PUBLIC_SUPABASE_URL` and `NEXT_PUBLIC_SUPABASE_PUBLISHABLE_KEY` from the Supabase project.
3. Fill `PUBLIC_SITE_URL` with the public HTTPS site origin used in appointment emails.
4. Run `npm install`.
5. Run `npm run dev`.

The initial scaffold intentionally uses placeholder role pages until Supabase Auth/RLS and seeded profiles are available.

## Appointment Email Assets

The public email logo is served from `/email/master-clips-logo.png`. The appointment email Edge Function uses
`${PUBLIC_SITE_URL}/email/master-clips-logo.png`, so `PUBLIC_SITE_URL` must be the real HTTPS production origin before
testing live emails.


## Hostinger Git Deployment

Hostinger keeps the repository root as `./`, while the Next.js app lives in `src/web/barberia-web`. The root `package.json` bridges that layout by running the web build through `npm --prefix`.

Use these settings in Hostinger:

- Framework preset: `Next.js`
- Node version: `22.x`
- Root directory: `./`
- Install command: `npm install`
- Build command: `npm run build`
- Output directory: `src/web/barberia-web/.next/standalone`

The Next.js config uses `output: "standalone"`, and the build copies `public` plus `.next/static` into the standalone output so Hostinger can run the generated `server.js` artifact from the nested app.

## Validation

- `npm run typecheck`
- `npm run lint`
- `npm run build`
- `npm run test`
