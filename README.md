# ShaloTrack API

![ASP.NET Core](https://img.shields.io/badge/ASP.NET_Core-8.0-blue)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-17-blue)
![EF Core](https://img.shields.io/badge/Entity_Framework_Core-8-purple)
![Status](https://img.shields.io/badge/Status-Active_Development-green)
![Version](https://img.shields.io/badge/Version-v0.4.0-orange)

Central Business & Application Layer of the ShaloTrack GPS Tracking and Fleet Management Platform.

The ShaloTrack API is the central backend service responsible for authentication, customer management, vehicle management, GPS device management, tracking services, alerts, subscriptions, and administrative operations across the entire ShaloTrack ecosystem.

The API acts as the single source of truth for all client applications. Business rules, security policies, and data validation are enforced here — not in the client.

> **Note:** The API does not communicate directly with GPS devices. Device communication is handled by the [ShaloTrack Gateway](https://github.com/Shalotrack-Lanka/shalotrack-gateway). The Gateway writes tracking data directly to the shared PostgreSQL database; the API reads and exposes it.

---

## Table of Contents

- [Live Development Status](#live-development-status)
- [System Architecture](#system-architecture)
- [Technology Stack](#technology-stack)
- [Repository Structure](#repository-structure)
- [API Architecture](#api-architecture)
- [Implemented Modules](#implemented-modules)
- [Current Endpoints](#current-endpoints)
- [Business Rules](#business-rules)
- [Release History](#release-history)
- [Roadmap](#roadmap)
- [Getting Started](#getting-started)
- [Testing the API with Swagger (No Coding Needed)](#testing-the-api-with-swagger-no-coding-needed)
- [Contributors](#contributors)

---

## Live Development Status

| Module | Status |
|---|---|
| API Foundation | ✅ Complete |
| Authentication (Firebase JWT + ownership) | ✅ Complete |
| Customer Management | ✅ Complete |
| Vehicle Management | ✅ Complete |
| GPS Device Management | ✅ Complete |
| Device Assignment | ✅ Complete |
| Current Location | ✅ Complete |
| Device Status | ✅ Complete |
| GPS Tracking History (Trips, Stops, Distance, Speed) | ✅ Complete |
| Real-Time Location Push (Postgres NOTIFY + SignalR) | ✅ Complete |
| Alerts & Notifications | 🚧 Foundation only — persistence + API done, push/trigger detection not yet built |
| Device Events | ⏳ Planned |
| Raw Packet Services | ⏳ Planned |

**Current Version:** `v0.5.0` — Active Development

---

## System Architecture

```
                     GPS Devices
                          │
                          ▼
                Python TCP Gateway
                          │
                          ▼
                Packet Processing Layer
                          │
                          ▼
                  PostgreSQL (Supabase)
                          ▲
                          │
                  ASP.NET Core API
                          │
          ┌───────────────┼──────────────┐
          │               │              │
          ▼               ▼              ▼
    Mobile App      Admin Portal    Dealer Portal
```

The Gateway and API never communicate directly. The Gateway writes device data to the database; the API reads and serves it to client applications. This decoupling allows both services to scale and deploy independently.

---

## Technology Stack

### Backend
- ASP.NET Core 8
- C#
- REST API
- Entity Framework Core 8
- Repository Pattern
- Query Repository Pattern (projection-based reads)
- Unit of Work
- Dependency Injection
- Swagger / OpenAPI

### Database
- PostgreSQL 17
- Supabase (managed hosting)
- Npgsql (EF Core provider)

### Gateway (separate repository)
- Python 3.11
- TCP Socket Server
- GT06 Protocol
- V5 GPS Tracker

### Infrastructure
- AWS EC2
- Cloudflare (DNS, SSL/TLS, DDoS protection)
- Docker *(planned)*
- GitHub Actions *(planned)*

---

## Repository Structure

```
ShaloTrack_API/
│
├── Controllers/                  # HTTP endpoints — thin adapters only
│
├── DTOs/                         # Data Transfer Objects
│   └── Customer/
│       ├── CreateCustomerDto.cs
│       ├── UpdateCustomerDto.cs
│       └── CustomerResponseDto.cs
│
├── Enums/                        # Domain enumerations
│   ├── CustomerStatus.cs
│   ├── ActivationStatus.cs
│   ├── AssignmentStatus.cs
│   └── PowerStatus.cs
│
├── Extensions/                   # IServiceCollection extension methods
│
├── Models/                       # EF Core entities (database tables)
│
├── Repositories/
│   ├── Interfaces/               # Repository contracts
│   └── Implementations/          # EF Core implementations (write + read/query repos)
│
├── Responses/
│   ├── ApiResponse.cs            # Standard response envelope
│   └── PagedResponse.cs
│
├── Services/
│   ├── Interfaces/               # Service contracts
│   └── Implementations/          # Business logic
│
├── Data/
│   └── ShaloTrackDbContext.cs    # EF Core DbContext — 8 tables
│
├── Migrations/                   # EF Core migration history
├── Program.cs                    # Bootstrap, DI, middleware pipeline
└── Dockerfile
```

---

## API Architecture

Every request passes through a strict, layered pipeline:

```
HTTP Request
      │
      ▼
Controller
      │  delegates to
      ▼
Service Layer          ← business logic, validation, entity mapping
      │  persists via
      ▼
Repository             ← EF Core queries, no business logic
      │  coordinated by
      ▼
Unit of Work           ← SaveChanges, transaction management
      │
      ▼
Entity Framework Core
      │
      ▼
PostgreSQL (Supabase)
      │
      ▼
ApiResponse<T>         ← standard envelope on every response
      │
      ▼
HTTP Response
```

Controllers contain no logic — they receive a request, call a service, and return `StatusCode(response.StatusCode, response)`. All validation, business rules, and entity mapping are in the service layer.

### Read vs. Write Separation

As of v0.4.0, telemetry modules follow a dedicated read pipeline distinct from business modules:

```
Business Modules:    Repository → Unit of Work → Service
Telemetry Modules:   Repository → Projection (Expression<Func<TEntity, DTO>>) → Service
```

Telemetry repositories are read-only, use `AsNoTracking()`, and project directly to DTOs — avoiding full entity materialization for high-frequency location and status queries.

---

## Implemented Modules

### Authentication ✅

- Firebase JWT bearer authentication (validated against Google's own signing
  keys — no custom login/token-issuing endpoint; the client authenticates
  with Firebase directly and presents that token to the API)
- Deny-by-default authorization policy — every endpoint requires a valid
  token unless explicitly marked `[AllowAnonymous]`
- Per-resource ownership enforcement (`[OwnsCustomer]` attribute for
  `{customerId}` routes; service-layer checks for vehicle/telemetry routes
  keyed by a resource ID rather than a customer ID)
- Role-based authorization for staff-only endpoints (`Admin`, `Dealer`)
- 404 (not 403) returned for non-owned resources, to avoid confirming
  resource existence to an unauthorized caller

---

### Customer Management ✅

- Create customer (email + NIC uniqueness enforced)
- Retrieve all customers
- Retrieve customer by ID (includes vehicle count)
- Update customer details
- Soft deactivation (record preserved, status set to Inactive)

---

### Vehicle Management ✅

- Vehicle CRUD
- Customer vehicle listing
- Duplicate vehicle number, chassis number, and engine number prevention

---

### GPS Device Management ✅

- IMEI validation
- Device registration
- Device activation status management
- Installation details tracking

---

### Device Assignment ✅

- Assign GPS device to vehicle
- Unassign GPS device
- Assignment history (full historical record preserved on change)
- Active assignment validation (one active device per vehicle enforced)
- Transaction support (assignment and status changes are atomic)

---

### Current Location ✅

- Live vehicle location retrieval
- Lookup by vehicle
- Lookup by device
- Projection-based, read-only queries for low-latency responses

---

### Device Status ✅

- Online / offline status
- Battery level
- GPS signal strength
- Ignition status
- Movement status
- Power status

---

### GPS Tracking History ✅

- Raw tracking point retrieval, ownership-scoped, server-side capped at 500
  points per request
- Trip/stop report computation: given a vehicle and date range, returns
  individual trips (start/end point and time, duration, real route distance,
  max/avg speed) and individual stops (location, duration)
- A "stop" is 5+ continuous minutes stationary; a "trip" additionally
  requires 100m+ of real displacement, to filter GPS jitter (drift while
  parked) from being misread as a trip
- Distance is the true route distance (sum of point-to-point movement),
  not straight-line start-to-end displacement

---

### Real-Time Location Push ✅

- A Postgres trigger fires `pg_notify` on every `CurrentLocations`
  insert/update
- A dedicated background service (on a persistent, session-pooled DB
  connection — required, since `LISTEN`/`NOTIFY` is incompatible with
  transaction-mode pooling) listens for these notifications
- Pushes are relayed to connected clients via a SignalR hub, scoped to a
  per-vehicle group; joining a group enforces the same ownership rule as
  REST endpoints
- Typical end-to-end latency: under two seconds from database write to
  client receipt, replacing what was previously a fixed polling interval

---

### Alerts & Notifications 🚧 (Foundation only)

- Alert persistence (`Alerts` table) and retrieval, ownership-scoped to the
  caller's own vehicles
- Mark-as-read
- FCM device token registration endpoint (`CustomerFcmTokens` table)
- **Not yet built:** actual push delivery, and the trigger logic that
  detects alert-worthy conditions (ignition change, overspeed, power-cut,
  low battery, device offline, geofence). The data model supports these
  alert types (`AlertType` enum), but nothing currently creates an `Alert`
  row automatically — only manual/test inserts exist right now.

---

## Current Endpoints

### Authentication

No login/token-issuing endpoints exist on this API. The client authenticates
directly with Firebase (phone OTP + email verification) and presents the
resulting Firebase JWT as a Bearer token on every request. The API validates
it against Google's public signing keys — there is nothing to log in
"to" here.

### Customers
```
GET    /api/customers
GET    /api/customers/{id}
POST   /api/customers
PUT    /api/customers/{id}
DELETE /api/customers/{id}       ← soft deactivation, not a database delete
```

### Vehicles
```
GET    /api/vehicles
GET    /api/vehicles/{id}
POST   /api/vehicles
PUT    /api/vehicles/{id}
DELETE /api/vehicles/{id}
```

### GPS Devices
```
GET    /api/devices
GET    /api/devices/{id}
POST   /api/devices
PUT    /api/devices/{id}
DELETE /api/devices/{id}
```

### Device Assignments
```
GET    /api/assignments                    ← assignment history
POST   /api/assignments                    ← assign device to vehicle
PATCH  /api/assignments/{id}/unassign      ← unassign device
GET    /api/assignments/vehicle/{id}       ← history for a specific vehicle
```

### Current Location
```
GET    /api/currentlocations
GET    /api/currentlocations/vehicle/{vehicleId}
GET    /api/currentlocations/device/{deviceId}
```

### Device Status
```
GET    /api/devicestatus
GET    /api/devicestatus/device/{deviceId}
GET    /api/devicestatus/vehicle/{vehicleId}
```

### GPS Tracking
```
GET    /api/gpstracking?vehicleId={id}&from={iso}&to={iso}     ← raw points, capped at 500
GET    /api/gpstracking/trips?vehicleId={id}&from={iso}&to={iso}  ← trip/stop report
```

### Alerts
```
GET    /api/alerts?page={n}&pageSize={n}      ← caller's own alert history
PATCH  /api/alerts/{alertId}/read              ← mark as read
POST   /api/alerts/register-token              ← register/refresh FCM device token
```

All responses use the `ApiResponse<T>` envelope:

```json
{
  "success": true,
  "statusCode": 200,
  "message": "Customer retrieved successfully.",
  "data": { },
  "errors": null,
  "timestamp": "2025-01-10T14:23:05Z"
}
```

---

## Business Rules

The API enforces business rules at the service layer — the database is never accessed directly by client applications.

Current rules:

- One active GPS device per vehicle at any time
- One active vehicle per GPS device at any time
- Full assignment history preserved when a device is reassigned or removed
- Soft deletion for all business entities (records are deactivated, never dropped)
- Duplicate IMEI prevention across all GPS devices
- Duplicate vehicle number prevention
- Duplicate chassis number prevention
- Duplicate engine number prevention
- Email uniqueness enforced per customer
- NIC uniqueness enforced per customer
- Telemetry endpoints are read-only and never mutate device or vehicle data

---

## Release History

### v0.1.0
- Clean Architecture foundation (Repository Pattern, Unit of Work, Service Layer, DTOs)
- Standard `ApiResponse<T>` response envelope
- Swagger / OpenAPI documentation
- Customer Module (Create, Read, Update, Soft Delete)
- EF Core migrations: `InitialCoreTables`, `TrackingTables`

### v0.2.0
- Vehicle Module (CRUD, duplicate validation)
- GPS Device Module (IMEI validation, registration, activation)

### v0.3.0
- Device Assignment Module
- Assignment history preservation
- Transaction support for multi-step operations
- Active assignment validation (one device per vehicle constraint)
- *(Note: this version's original changelog entry claimed JWT
  authentication was complete. It was not — the API had no authentication
  at all until v0.4.1, below. Corrected here for accuracy.)*

### v0.4.0 — Telemetry Foundation
- Current Location API (live vehicle/device location lookup)
- Device Status API (online status, battery, signal, ignition, movement, power)
- Query Repository Pattern introduced for telemetry modules
- Expression-based projection mapping (`Expression<Func<TEntity, DTO>>`)
- Read/write architectural separation between business and telemetry modules
- Performance improvements: `AsNoTracking()`, projection queries, reduced entity materialization

### v0.4.1 — Security Hardening
- **Discovered the API had no authentication at all** — every endpoint was
  publicly readable/writable, and customer-scoped routes had no ownership
  check (IDOR on all of them)
- Firebase JWT bearer authentication added, with a deny-by-default
  fallback policy
- `Customer.FirebaseUid` added, linking Firebase accounts to customer
  records
- `[OwnsCustomer]` filter for `{customerId}` routes; service-layer
  ownership checks for vehicle/telemetry routes keyed by a resource ID
- Staff-only role restriction on list ("get all") endpoints
- Production exception handler fixed to stop leaking raw database error
  details

### v0.5.0 — Tracking, Real-Time, and Alerts Foundation
- GPS Tracking History: trip/stop report computation (distance, duration,
  max/avg speed per trip; individual stop records with location + duration)
- GPS-jitter filtering: a "trip" requires genuine displacement, not just a
  transient above-threshold speed reading
- Real-time location push: Postgres `NOTIFY` trigger + a persistent
  background listener + a SignalR hub, replacing fixed-interval polling
  with sub-2-second live updates
- Alerts & Notifications foundation: persistence, retrieval, mark-as-read,
  and FCM token registration (push delivery and trigger detection are not
  yet built — see Roadmap)

---

## Roadmap

### v0.6.0 — Alerts & Notifications (Full)
- FCM push delivery
- Trigger detection for Ignition, Overspeed, Power-cut, and Low-battery
  (state-change alerts — can reuse the existing NOTIFY/SignalR pipeline)
- Device offline detection (a periodic background check, not an event
  trigger, since silence isn't something a row-write can announce)
- Geofencing (new schema — no fence concept exists yet; boundary
  definition, management, and crossing detection)

### v0.7.0 — Events & Admin Tooling
- Device Events
- Raw Packets (Admin)
- Filtering and pagination

### v0.8.0 — Platform Hardening
- Centralized logging
- Global exception handling middleware
- Rate limiting

### v1.0.0 — Production Release
- Full test coverage
- CI/CD pipeline (GitHub Actions)
- Docker deployment to AWS EC2
- Production environment configuration

---

## Getting Started

### Requirements

- .NET 8.0 SDK
- PostgreSQL (Supabase or local)

### Setup

```bash
git clone https://github.com/Shalotrack-Lanka/shalotrack-api.git
cd ShaloTrack_API
dotnet restore
```

Add your connection string to `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=db.<project>.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=<password>"
  }
}
```

### Apply Migrations

```bash
dotnet ef database update
```

### Run

```bash
dotnet run
```

Swagger UI: `https://localhost:<port>/swagger`

---

## Testing the API with Swagger (No Coding Needed)

The live API also has a public Swagger page for anyone to try out — no local setup required:

```
https://api.shalotrack.com/swagger/index.html
```

The API is protected by Firebase Authentication, so before you can test any endpoint, you need to unlock the page with a temporary access key (called a **token**). This section explains how, in plain terms, for anyone — technical or not.

> **Why do I need to do this?**
> The API is protected, like a locked door. To open it, you need a special "key" (token). This guide shows you how to get that key and use it.

### What you'll need

- A Windows computer
- Internet connection
- 5 minutes

### Step 1: Get your key (token)

1. Click the **Windows Start icon** (bottom-left of your screen).
2. Type **CMD** and press **Enter**. A black window will open — this is called Command Prompt.
3. Copy the text below, paste it into that black window, and press **Enter**:

```bash
curl -X POST "https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key=AIzaSyBbFQSKpl8B4ld47LH4-BaGGWKa55ha9kk" -H "Content-Type: application/json" -d "{\"email\":\"swaggertest@test.com\",\"password\":\"Test1234!\",\"returnSecureToken\":true}"
```

> **Tip:** To paste into Command Prompt, right-click inside the window (Ctrl+V doesn't always work there).

4. After pressing Enter, you'll see a bunch of text appear. Somewhere in there is a line that looks like this:

```json
"idToken": "eyJhbGciOiJSUzI1NiIsImtpZCI6...(long text)...",
```

5. Copy **only the long text between the quotes** after `idToken`. This is your key. Don't include the quotation marks.

### Step 2: Go to the API testing page

Open your web browser and go to:

```
https://api.shalotrack.com/swagger/index.html
```

This page lists everything the API can do.

### Step 3: Unlock the page with your key

1. Near the top-right of the page, click the green **Authorize** button (it has a padlock icon).
2. A small box will pop up asking for a value. **Paste your key here** (the long text you copied in Step 1).

   > Just paste the key by itself — you don't need to type "Bearer" or anything else in front of it.

3. Click **Authorize**, then click **Close**.

The padlock icons on the page should now look "locked" and ready — this means you're successfully signed in.

### Step 4: Try out the API

1. Click on any section (like **Customers**) to expand it.
2. Click on any request (like `GET /api/Customers`).
3. Click the **Try it out** button.
4. Fill in any details it asks for (if any).
5. Click **Execute**.
6. Scroll down to see the result under **Server response**.

That's it — you're now testing the live API.

### Something not working?

**Getting an error that says "token expired"?**
Your key only works for **1 hour** after you created it. Just repeat **Step 1** to get a new key, and repeat **Step 3** to use it.

**Still stuck?**
Double-check that:
- You copied the *entire* key (it's very long, and it's easy to miss part of it)
- You didn't accidentally copy the quotation marks along with the key

### A quick note on safety

Your key acts like a temporary password. Don't share it with anyone or post it publicly (like in a chat, forum, or GitHub issue) — even though it stops working after an hour, it's good habit to keep it private.

---

## Contributors

**Suwen Jayathunga** — Lead Developer & System Architect
System Architecture · Backend Development · Database Engineering · Gateway Development · API Development

**Nuwan Aloka** — Founder & Product Owner
Product Vision · Business Operations · Strategic Direction

---

Built with ❤️ by the ShaloTrack Development Team.
