# Coon.Meeting Dashboard — handoff

Written 2026-09-15. Read this before touching anything if you're opening this in a fresh IDE
with no prior context.

---

## What this is

Coon.Meeting Dashboard is a real, self-serve product (signup, login, org/team membership) that
lets a human create and join meetings through a UI. It is built as **just another integrator**
of a separate, already-built, standalone product called Coon.Meeting (API keys for
server-to-server calls, short-lived participant JWTs for a browser, zero end-user identity of
its own). The Dashboard's own backend holds a Coon.Meeting API key per organization, server-side
only, and calls Coon.Meeting's REST API exactly like any third-party company integrating with it
would. The Dashboard's own frontend never sees that API key — it only ever gets short-lived
participant tokens.

This split is deliberate, not incidental: bolting user accounts onto Coon.Meeting itself would
pollute a product designed to be neutral for every future integrator. See the full architecture
discussion and rationale in the plan file (below) if you need to re-justify this to anyone.

**The plan**: `C:\Users\20100\.claude\plans\elegant-weaving-umbrella.md` — the full phased build
order, the exact Coon.Meeting REST/CORS contract this was built against, and the explicit v1
scope cuts. Read it before starting Phase 2 or later; this handoff is the "what's actually true
right now" summary, the plan is the "why and what's next" detail.

---

## The four repos

| Path | Remote | What it is | Status |
|---|---|---|---|
| `C:\Talks tent\Coon Meeting\backend` | `github.com/Ahmad-S-Nasser/Meeting-backend` | Coon.Meeting itself — .NET 8 + LiteDB, the standalone meeting/calling service | Phases (a)-(e) + CORS done, working |
| `C:\Talks tent\Coon Meeting\frontend` | `github.com/Ahmad-S-Nasser/Meeting-frontend` | `coon-meeting-sdk` npm package (calling UI) - install via `npm install github:Ahmad-S-Nasser/Meeting-frontend` | Done, published nowhere but git-installable (has a `prepare` script) |
| `C:\Talks tent\Coon Meeting\dashboard-backend` | `github.com/Ahmad-S-Nasser/Meeting-Admin-Backend` | This repo - the Dashboard's own API | **Phase 1 of 4 done** (auth + org + real tenant provisioning) |
| `C:\Talks tent\Coon Meeting\dashboard-frontend` | `github.com/Ahmad-S-Nasser/Meeting-Admin-Frontend` | The Dashboard's UI | **Not started** - repo exists (bootstrap commit only), no app code yet |

No solution file ties these together across repos. Each has its own `dotnet build`/`npm` cycle.

---

## Running it locally (order matters — Dashboard depends on Coon.Meeting)

**1. Coon.Meeting backend first** (`cd backend/src/Coon.Meeting.Api`):

```bash
ASPNETCORE_ENVIRONMENT=Development \
Jwt__ParticipantKey="<any long string>" \
Turn__SharedSecret="<any long string>" \
Smtp__Host="127.0.0.1" Smtp__Port="2525" Smtp__FromAddress="noreply@test" Smtp__Username="x" Smtp__Password="<any string>" \
Admin__ProvisioningKey="<pick one, e.g. dev-admin-key>" \
dotnet run
```

Prints two dev-seeded tenants' raw API keys on first run (useful for testing Coon.Meeting
directly, not needed by the Dashboard). Listens on whatever `Properties/launchSettings.json`
says (currently `http://localhost:5029`, but that's not guaranteed stable — check the console
output's "Now listening on" line every time).

**2. Dashboard backend second** (`cd dashboard-backend/src/CoonMeeting.Dashboard.Api`):

```bash
ASPNETCORE_ENVIRONMENT=Development \
Session__SigningKey="<any long string>" \
CoonMeeting__ApiBaseUrl="http://localhost:5029" \
CoonMeeting__AdminProvisioningKey="<must match Coon.Meeting's Admin__ProvisioningKey above>" \
Frontend__BaseUrl="http://localhost:5174" \
dotnet run
```

`CoonMeeting__AdminProvisioningKey` here and `Admin__ProvisioningKey` on Coon.Meeting **must be
the identical string** - they're the same secret, one on each side of the call. Also check its
own console output for the actual listening port (currently defaults to `5256`).

**Smoke test once both are up**:

```bash
curl -X POST http://localhost:5256/api/v1/auth/signup -H "Content-Type: application/json" \
  -d '{"email":"you@test.com","password":"whatever12345","name":"You","organizationName":"Test Org"}'
```

Should return `201` with a token. If it 500s, one of the two services' required secrets is
missing — check that service's own console output for `Startup Configuration Error:`.

---

## Standing checks

No test framework here either (matching Coon.Meeting's own pattern - `dotnet build` + curl is
the safety net). After any change:

```bash
# Both build clean, 0 errors
cd backend && dotnet build
cd dashboard-backend && dotnet build

# Full signup -> login -> /me flow (see above) still returns 201/200, not 500
```

If you touch `CoonMeetingClient.cs`, re-verify against a REAL running Coon.Meeting instance -
the tenant it provisions should show up via `GET /api/v1/tenants/me` on Coon.Meeting itself
(using the raw key logged from a temporary debug line - see the git history of `AuthController.cs`
for exactly how this was verified last time, then remove the debug line again before committing).

---

## Traps that already cost time building this

**LiteDB's LINQ-to-BsonExpression translator can't handle a method call inside a `Find`
predicate** - `now.Add(window)` inside a lambda throws `NotSupportedException` at runtime, not
compile time. Precompute the value as a plain local variable outside the predicate. (Bit
Coon.Meeting's reminder poller; watch for the same shape here if `PendingInvite` expiry queries
end up doing arithmetic inline.)

**LiteDB silently converts `DateTime` to local time on read** unless you register a custom
`BsonMapper.RegisterType<DateTime>` that calls `.ToUniversalTime()` on **both** serialize and
deserialize. Both `LiteDbContext` classes (Coon.Meeting's and this repo's) already do this - if
you ever add a third LiteDB-backed service, copy it, don't skip it. Getting only one direction
right still corrupts the value (this was caught and fixed once already, the wrong way, before
being fixed properly - see Coon.Meeting's git history on `LiteDbContext.cs`).

**A namespace segment that matches a class name breaks unqualified references.**
`Coon.Meeting.Api`'s own root namespace collided with its `Meeting` class (`Coon.Meeting` is
also a real namespace, so bare `Meeting` resolved to the namespace, not the type - `CS0118`).
This repo is deliberately named `CoonMeeting.Dashboard.Api` (glued, no dot after "Coon") to
avoid the same trap now that a `Dashboard`/`Organization`/etc. class exists. **Don't rename the
root namespace to `Coon.Meeting.Dashboard` "for consistency" without checking for this first.**

**`System.Text.Json`'s default deserialization is case-SENSITIVE**, unlike ASP.NET Core's MVC
JSON formatter (which is case-insensitive by default). `CoonMeetingClient.cs` calling
Coon.Meeting's camelCase API had to pass `JsonSerializerDefaults.Web` explicitly to
`ReadFromJsonAsync` - without it, `Id`/`ApiKey` silently deserialize to empty strings instead of
throwing. Any new outbound HTTP client added here needs the same explicit options.

**`SmtpClient`'s default timeout is 100 seconds.** If Phase 2's invite email sender is adapted
from Coon.Meeting's `SmtpEmailSender.cs`, keep its explicit `Timeout = 10_000` - email sends
synchronously right after a write, so an unreachable mail server must fail fast, not stall the
request for 100s.

**Kill `dotnet run` processes properly before rebuilding** - a running instance locks the DLL.
On Windows, `taskkill //F //IM dotnet.exe` kills *all* dotnet processes including unrelated
ones; if you have other .NET work open, find the specific PID via `netstat -ano | grep
":<port>.*LISTENING"` instead.

---

## Conventions to follow (matching both this repo and Coon.Meeting)

- **Every DI registration is `AddSingleton`.** LiteDB collections are cheap thread-safe handles;
  no per-request scoping needed.
- **Every required-at-startup secret goes through the same `RequireSecret` pattern**: bind in a
  `try`, throw with a clear message naming both the config key and the env var, catch and turn
  into a blanket 500 via middleware rather than crashing the process. Only add a secret to this
  list once something actually consumes it - Coon.Meeting's `Smtp:Password` wasn't required
  until the email-invite feature landed; this repo's follows the same discipline (no `Smtp:*`
  requirement yet, since nothing sends email until Phase 2).
- **Auth schemes never leak into each other.** Coon.Meeting has `ApiKey` and `ParticipantToken`;
  this repo has `DashboardSession`. Three completely separate JWT-bearer registrations, three
  separate signing keys, none of them accept each other's tokens.
- **External calls that can't be rolled back happen before local writes.**
  `AuthController.Signup` calls Coon.Meeting's tenant-provisioning endpoint *before* creating
  the local `User`/`Organization` - a failure there means no local write happened either.
- **Cross-document consistency uses an explicit LiteDB transaction**
  (`LiteDbContext.CreateUserAndOrganization`), not two independent inserts - a `User` with
  `OrganizationId` set but no matching `Organization` (or the reverse) would be broken.
- **A top-level LiteDB collection with a unique index beats an embedded list you need to search
  across parents.** `PendingInvite` (Phase 2, not built yet) is planned as its own collection
  keyed by token hash, not embedded in `Organization`, for exactly this reason - see the plan
  file for the full reasoning.

---

## What was done this cycle

**Phase 0 (in the Coon.Meeting repo, not this one)**: per-tenant CORS
(`DynamicCorsPolicyProvider`), since Coon.Meeting's original plan called for it but it never got
built across phases (a)-(e), and the Dashboard's SDK-driven browser calls need it. Verified: an
allowed origin gets `Access-Control-Allow-Origin` on both a plain request and an OPTIONS
preflight; a disallowed one gets neither.

**Phase 1 (this repo)**: `AuthController` (signup/login), `MeController`, `CoonMeetingClient`,
the `DashboardSession` JWT scheme, `User`/`Organization` LiteDB models. Verified end-to-end
against a live Coon.Meeting instance - signup provisions a real tenant (confirmed via
Coon.Meeting's own `/tenants/me`), login + `/me` work, duplicate email → 409, wrong password →
401, missing secret → clean 500.

---

## Open items / next steps

**Phase 2** — org membership + email invites. `PendingInvite` collection, `OrgController`
(`GET /org/members`, `POST /org/invites`), `InvitesController` (preview + two accept endpoints -
`accept-new` for someone without an account, `accept` for an already-logged-in session). Adapt
email sending from Coon.Meeting's `SmtpEmailSender.cs` mechanics (see the SmtpClient timeout
trap above).

**Phase 3** — the big one. Backend: `MeetingsController` (thin proxy to Coon.Meeting's CRUD) and
`CallController` (mints participant tokens). Frontend: **`dashboard-frontend` doesn't exist as
code yet** - this phase is where it actually gets built (React + Vite + react-router +
`coon-meeting-sdk`). Has a **pre-flight step**: before building the call UI, verify on two
genuinely different origins (not just different localhost ports) that the SDK's cross-origin
calls to Coon.Meeting actually succeed now that Phase 0's CORS is in place - this hasn't been
checked with a real browser yet, only reasoned about.

**Phase 4** — polish, a `DEPLOY.md` for this repo (mirroring Coon.Meeting's own
`docs/DEPLOY.md`), meeting edit/cancel UI, org settings page.

**Explicit v1 scope cuts** (see the plan file for the full list with reasoning): password reset,
removing/demoting org members, revoking/rotating a Coon.Meeting API key, multi-org-per-user,
live (`sk_live_`) keys, per-meeting attendee-only call access, session revocation.

---

## Environment

- Both backends need secrets set as env vars locally (see "Running it locally" above) - neither
  has real values in `appsettings.json`, by design.
- Coon.Meeting's `Admin__ProvisioningKey` and this repo's `CoonMeeting__AdminProvisioningKey`
  must match - it's one shared secret, not two.
- `Frontend__BaseUrl` (this repo) becomes the Coon.Meeting tenant's only `AllowedOrigins` entry
  at signup - if you run the eventual `dashboard-frontend` dev server on a different port than
  whatever this is set to, its calls to Coon.Meeting will be silently CORS-blocked. Keep them in
  sync once Phase 3 starts.
- No `gh` CLI on this machine as of this session - GitHub repos were created manually and their
  URLs supplied by hand. If that's changed, `gh repo create` would simplify future repo bootstrap.
