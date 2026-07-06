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
- [Contributors](#contributors)

---

## Live Development Status

| Module | Status |
|---|---|
| API Foundation | ✅ Complete |
| Authentication | ✅ Complete |
| Customer Management | ✅ Complete |
| Vehicle Management | ✅ Complete |
| GPS Device Management | ✅ Complete |
| Device Assignment | ✅ Complete |
| Current Location | ✅ Complete |
| Device Status | ✅ Complete |
| GPS Tracking History | 🚧 Next |
| Device Events | ⏳ Planned |
| Raw Packet Services | ⏳ Planned |

**Current Version:** `v0.4.0` — Active Development

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

- JWT Authentication
- Login / Logout
- Token validation and refresh
- Role-based authorization

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

## Current Endpoints

### Authentication
```
POST   /api/auth/login
POST   /api/auth/logout
POST   /api/auth/refresh-token
```

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
- Authentication (JWT, login, token refresh)
- Device Assignment Module
- Assignment history preservation
- Transaction support for multi-step operations
- Active assignment validation (one device per vehicle constraint)

### v0.4.0 — Telemetry Foundation
- Current Location API (live vehicle/device location lookup)
- Device Status API (online status, battery, signal, ignition, movement, power)
- Query Repository Pattern introduced for telemetry modules
- Expression-based projection mapping (`Expression<Func<TEntity, DTO>>`)
- Read/write architectural separation between business and telemetry modules
- Performance improvements: `AsNoTracking()`, projection queries, reduced entity materialization

---

## Roadmap

### v0.5.0 — Tracking & Events
- GPS Tracking History
- Device Events
- Raw Packets (Admin)
- Filtering and pagination

### v0.6.0 — Platform Hardening
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

## Contributors

**Suwen Jayathunga** — Lead Developer & System Architect
System Architecture · Backend Development · Database Engineering · Gateway Development · API Development

**Nuwan Aloka** — Founder & Product Owner
Product Vision · Business Operations · Strategic Direction

---

Built with ❤️ by the ShaloTrack Development Team.
