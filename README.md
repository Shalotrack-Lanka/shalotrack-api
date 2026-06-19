# ShaloTrack API

Central Business & Application Layer of the ShaloTrack Ecosystem

The ShaloTrack API is the central backend service of the ShaloTrack GPS Tracking and Fleet Management Platform. It contains the core business logic responsible for managing customers, vehicles, GPS devices, tracking information, subscriptions, alerts, authentication, authorization, and administrative operations across the entire ShaloTrack ecosystem.

The API acts as the single source of truth for all applications operating under the ShaloTrack platform and ensures that all business rules, security policies, and data validation procedures are applied consistently.

> **Note:** The API does not communicate directly with GPS devices. Device communication is handled separately by the [ShaloTrack Gateway](https://github.com/shalotrack/shalotrack-gateway) service. The API consumes and exposes processed data stored within the platform database.

---

## Table of Contents

* [Executive Summary](#executive-summary)
* [Project Vision](#project-vision)
* [Live Development Status](#live-development-status)
* [Technology Stack](#technology-stack)
* [API as the Central Service Layer](#api-as-the-central-service-layer)
* [System Architecture](#system-architecture)
* [ShaloTrack Ecosystem](#shalotrack-ecosystem)
* [Application Integration Architecture](#application-integration-architecture)
* [Gateway vs API Responsibilities](#gateway-vs-api-responsibilities)
* [Repository Interaction Architecture](#repository-interaction-architecture)
* [API Modules](#api-modules)
* [Endpoint Categories](#endpoint-categories)
* [Request Lifecycle](#request-lifecycle)
* [GPS Data Lifecycle](#gps-data-lifecycle)
* [Authentication Architecture](#authentication-architecture)
* [Security Architecture](#security-architecture)
* [Database Architecture](#database-architecture)
* [API Hosting Architecture](#api-hosting-architecture)
* [Infrastructure Architecture](#infrastructure-architecture)
* [Scaling Architecture](#scaling-architecture)
* [CI/CD Roadmap](#cicd-roadmap)
* [Development History](#development-history)
* [Development Roadmap](#development-roadmap)
* [Repository Status](#repository-status)
* [Contributors](#contributors)
* [Long-Term Vision](#long-term-vision)

---

## Executive Summary

### Business Problem

Traditional GPS tracking platforms often suffer from:

* Direct database access by client applications
* Weak security controls
* Duplicate business logic across services
* Limited scalability
* Difficult maintenance
* Tight coupling between services

As the number of devices, customers, and applications grows, these architectural limitations become increasingly difficult to manage.

### Solution Overview

ShaloTrack solves these challenges through a centralized API architecture that separates tracking infrastructure from business services and client applications, organized into three specialized layers:

| Layer | Responsibility |
|---|---|
| **Tracking Infrastructure** | GPS device communication, TCP connections, protocol parsing, packet processing |
| **API Infrastructure** | Authentication, authorization, business logic, data validation, data access |
| **Client Applications** | User interaction, tracking visualization, reporting, administration |

This architecture enables scalability, maintainability, and long-term commercial deployment.

---

### Scope of the API

| Domain | Responsibilities |
|---|---|
| **Authentication** | User login, token management, role validation |
| **Customer Services** | Customer registration and management |
| **Vehicle Services** | Vehicle registration and assignment |
| **Device Services** | Device registration, IMEI validation, device assignment |
| **Tracking Services** | Current location, historical tracking, route playback, trip analysis |
| **Subscription Services** | Plan validation, renewals, expiry monitoring |
| **Alert Services** | Overspeed, ignition, device offline, geofence alerts |
| **Administrative Services** | User management, customer management, dealer management, platform configuration |

---

## Project Vision

### Academic Objective

ShaloTrack was initially developed as a university-level software engineering project intended to demonstrate practical implementation of modern enterprise technologies and distributed systems.

Key learning objectives include Software Architecture Design, Backend Engineering, Cloud Infrastructure, GPS Communication Systems, Database Engineering, Distributed Systems, Security Engineering, and API Development.

### Commercial Objective

Beyond academic requirements, ShaloTrack is being developed as a commercial GPS Tracking and Fleet Management Platform targeting Individual Vehicle Owners, Fleet Operators, Corporate Customers, Logistics Companies, Dealers & Resellers, and Government Organizations.

The architecture is designed to support tens of thousands of GPS devices while maintaining performance, security, and reliability.

---

## Live Development Status

| Item | Status |
|---|---|
| Architecture Design | ✓ Complete |
| Database Schema | ✓ Complete |
| Supabase Integration | ✓ Complete |
| API Foundation | In Progress (70%) |
| Authentication | In Progress (50%) |
| Tracking Services | In Progress (30%) |

**Current Version:** `v0.2.0`
**Development Phase:** Core Backend Development

---

## Technology Stack

| Layer | Technology |
|---|---|
| **Backend** | ASP.NET Core 8, C#, REST API |
| **Database** | Supabase PostgreSQL, Entity Framework Core 8, Npgsql |
| **GPS Infrastructure** | Python TCP Gateway, GT06 Protocol, V5 GPS Tracker Support |
| **Cloud** | AWS EC2, Cloudflare DNS |
| **Version Control** | Git, GitHub |

---

## API as the Central Service Layer

The ShaloTrack API serves as the central application service layer for the entire ShaloTrack ecosystem.

While the Gateway is responsible for communicating with GPS tracking devices and storing tracking information, the API is responsible for exposing business services to all user-facing applications. It acts as the bridge between the database infrastructure and the applications used by customers, administrators, dealers, and future platform integrations.

Without the API, every application would require direct access to the database, resulting in security risks, duplicated business logic, and significant maintenance challenges. By centralizing all business operations within the API, the platform ensures that validation, authorization, and business rules are applied consistently across every service.

### Why the API Exists

| Benefit | Description |
|---|---|
| **Security** | Applications never access the database directly |
| **Centralized Business Logic** | All validation and business rules are managed in one location |
| **Scalability** | Applications can scale independently from backend services |
| **Maintainability** | Changes to the database structure do not impact client applications |
| **Extensibility** | Future applications can be integrated without modifying existing systems |

### API Position Within the Ecosystem

```
                           shalotrack.com
                                  │
     ┌───────────────┬────────────┴────────────┬───────────────┐
     │               │                         │               │
     ▼               ▼                         ▼               ▼
gateway.         api.                     www.           Supabase
shalotrack.com   shalotrack.com          shalotrack.com PostgreSQL
TCP Gateway      Core Business API       Website         Database
                     │
      ┌──────────────┼──────────────┐
      │              │              │
      ▼              ▼              ▼
app.            admin.          dealer.
shalotrack.com  shalotrack.com  shalotrack.com
Android App     Admin Portal    Dealer Portal
```

---

## System Architecture

```
GPS Device
      │
      ▼
gateway.shalotrack.com
      │
      ▼
Packet Parser
      │
      ▼
Supabase PostgreSQL
      ▲
      │
api.shalotrack.com
      │
 ┌────┼────────────────┐
 ▼    ▼                ▼
app. admin.         dealer.
```

---

## ShaloTrack Ecosystem

The ShaloTrack platform operates across multiple subdomains under `shalotrack.com`, each with a specific responsibility.

### `gateway.shalotrack.com` — TCP Communication Layer

* Accepts GPS tracker connections
* Manages TCP sessions
* Processes GT06 and V5 packets
* Generates ACK responses
* Stores tracking information

Supported Devices: V5 GPS Tracker, GT06 Compatible Devices, Future 4G Devices

> The Gateway communicates only with the database layer and never directly with customers.

---

### `api.shalotrack.com` — Business Logic Layer

* Authentication & Authorization
* Customer, Vehicle & Device Management
* Tracking & Alert Services
* Subscription Services

> The API serves as the central communication layer for the entire platform.

---

### `app.shalotrack.com` — Customer Application

* Live Tracking
* Vehicle Monitoring
* Route History
* Notifications
* Account Management

---

### `admin.shalotrack.com` — Administrative Platform

* Customer Management
* Device Management
* Subscription Management
* Reporting & Platform Administration

---

### `dealer.shalotrack.com` — Dealer Management Platform

* Customer Onboarding
* Device Activation & Assignment
* Subscription Activation

---

### `www.shalotrack.com` — Corporate Website

* Marketing & Product Information
* Customer Acquisition
* Contact Services

---

## Application Integration Architecture

### Android Application

The Android application communicates exclusively through the API and never directly with the database or GPS devices.

Features supported by the API:

* Authentication
* Vehicle Management
* Live Tracking & Route History
* Vehicle Status & Notifications
* Profile Management

```
Android Application
         │
         ▼
api.shalotrack.com
         │
         ▼
Supabase PostgreSQL
```

---

### Administrative Portal

All platform management operations are validated through API services before being committed to the database.

Features:

* Customer & Vehicle Management
* Device & Subscription Management
* Reporting & Administrative Operations

```
Admin Portal
      │
      ▼
api.shalotrack.com
      │
      ▼
Business Services
      │
      ▼
Database
```

---

### Dealer Portal

Dealers operate under the same business rules and validation processes as administrators, all routed through the API.

Planned features:

* Device Registration & Activation
* Customer Onboarding
* Subscription Activation
* Device Assignment

```
Dealer Portal
      │
      ▼
api.shalotrack.com
      │
      ▼
Database
```

---

### Public Website

While most content is static, future dynamic services will communicate with the API for Contact Forms, Lead Management, Customer Registration, and Service Requests.

```
www.shalotrack.com
        │
        ▼
api.shalotrack.com
        │
        ▼
Database
```

---

## Gateway vs API Responsibilities

The Gateway and API perform distinct responsibilities and never communicate directly with each other. Communication between them occurs indirectly through the shared database layer.

| Concern | Gateway | API |
|---|---|---|
| TCP Communication | ✓ | — |
| Device Connectivity | ✓ | — |
| Packet Processing | ✓ | — |
| Protocol Handling | ✓ | — |
| GPS Data Collection | ✓ | — |
| Authentication | — | ✓ |
| Authorization | — | ✓ |
| Business Logic | — | ✓ |
| Data Access | — | ✓ |
| Application Services | — | ✓ |

```
GPS Device
      │
      ▼
Gateway
      │
      ▼
Database
      ▲
      │
API
      │
      ▼
Applications
```

This architecture provides clear separation of concerns and allows both services to scale independently.

---

## Repository Interaction Architecture

The ShaloTrack platform is divided into independent repositories with clear boundaries.

| Repository | Responsibilities | Communicates With |
|---|---|---|
| `shalotrack-gateway` | Device communication, TCP services, packet processing | Supabase PostgreSQL |
| `shalotrack-api` | Business logic, authentication, data services | Supabase PostgreSQL, all client applications |
| `shalotrack-mobile` | Customer interface, live tracking, notifications | ShaloTrack API |
| `shalotrack-admin` | Administrative operations | ShaloTrack API |
| `shalotrack-website` | Public website | ShaloTrack API |

---

## API Modules

### Authentication Module

* Login, Logout, Token Validation
* Refresh Tokens, Password Management

```http
POST /api/auth/login
POST /api/auth/logout
POST /api/auth/refresh-token
```

---

### Customer Module

* Customer Registration & Updates
* Account Status Management

**Database Tables:** `Customers`

---

### Vehicle Module

* Vehicle Registration & Assignment
* Vehicle Information Management

**Database Tables:** `Vehicles`

---

### Device Module

* IMEI Validation
* Device Registration & Assignment

**Database Tables:** `GpsDevices`, `DeviceAssignments`

---

### Tracking Module

* Current Location
* Historical Tracking & Route Playback
* Trip Reports

**Database Tables:** `GpsTrackings`, `CurrentLocations`, `RawPackets`

---

### Alert Module

* Overspeed Detection
* Ignition Events
* Power Disconnect Alerts
* Offline Detection

---

### Subscription Module

* Plan Validation
* Subscription Renewal
* Expiry Monitoring

---

## Endpoint Categories

```
/api/auth
/api/customers
/api/vehicles
/api/devices
/api/tracking
/api/trips
/api/alerts
/api/subscriptions
/api/admin
/api/dealers
```

Each category represents a separate business domain within the platform.

---

## Request Lifecycle

Every customer request follows a strict validation and authorization pipeline before data is returned.

```
Customer
    ↓
Mobile Application
    ↓
HTTPS Request
    ↓
Cloudflare
    ↓
api.shalotrack.com
    ↓
JWT Middleware
    ↓
Authorization Layer
    ↓
Tracking Service
    ↓
Repository Layer
    ↓
Entity Framework Core
    ↓
Supabase PostgreSQL
    ↓
Response
    ↓
Mobile Application
```

---

## GPS Data Lifecycle

This architecture ensures complete separation between tracking infrastructure and application services.

```
GPS Device
      ↓
Satellite Position Fix
      ↓
GT06 Packet
      ↓
TCP Connection
      ↓
gateway.shalotrack.com
      ↓
Packet Validation
      ↓
Packet Parsing
      ↓
RawPackets → GpsTrackings → CurrentLocations
      ↓
api.shalotrack.com
      ↓
Mobile Application
```

---

## Authentication Architecture

The platform uses JWT-based authentication.

```
User
   ↓
Login Request
   ↓
API
   ↓
Credential Validation
   ↓
JWT Token Issued
   ↓
Application Access
```

**Future Enhancements:** Multi-Factor Authentication, Single Sign-On, Enterprise Identity Providers

---

## Security Architecture

The platform implements a layered defense-in-depth security model.

| Layer | Protection |
|---|---|
| Layer 1 | Cloudflare Protection |
| Layer 2 | HTTPS Encryption |
| Layer 3 | JWT Authentication |
| Layer 4 | Role-Based Authorization |
| Layer 5 | Business Rule Validation |
| Layer 6 | Database Constraints |
| Layer 7 | Audit Logging |

---

## Database Architecture

### Current Core Tables

**Customer Management**

* `Customers`
* `Vehicles`
* `DeviceAssignments`

**Device Management**

* `GpsDevices`
* `DeviceStatuses`

**Tracking Infrastructure**

* `RawPackets`
* `GpsTrackings`
* `CurrentLocations`

### Database Relationships

```
Customer
    ↓
Vehicle
    ↓
DeviceAssignment
    ↓
GpsDevice
```

* One customer may own multiple vehicles
* One vehicle is assigned a GPS device
* Device assignments allow hardware replacement without losing vehicle history
* Tracking records remain linked to the assigned device

---

## API Hosting Architecture

### Current Development Environment

During development, the ShaloTrack API is hosted locally on the development machine and connected to the cloud database infrastructure.

```
Developer Machine
        │
        ▼
ASP.NET Core 8 API
        │
        ▼
Supabase PostgreSQL
```

Purpose: Feature Development, API Testing, Database Integration, Entity Framework Development, Local Debugging

---

### Current Cloud Infrastructure

The ShaloTrack Gateway is currently deployed on AWS EC2 and receives live GPS tracking data from V5 GPS devices.

```
GPS Device
      │
      ▼
AWS EC2 Gateway Server
      │
      ▼
Supabase PostgreSQL
```

Used for: Protocol Testing, Device Validation, Packet Processing, Live GPS Data Collection

---

### Planned Production API Hosting

The ShaloTrack API will be deployed on a cloud-based Linux server via the following architecture:

```
Cloudflare DNS
        │
        ▼
api.shalotrack.com
        │
        ▼
AWS EC2 Instance
        │
        ▼
ASP.NET Core 8 API
        │
        ▼
Supabase PostgreSQL
```

**Component Responsibilities:**

| Component | Responsibilities |
|---|---|
| **Cloudflare** | DNS management, SSL/TLS certificates, DDoS protection, traffic routing |
| **AWS EC2** | API hosting, background services, scheduled tasks, future container deployment |
| **Supabase PostgreSQL** | Data storage — tracking, customer, vehicle, and device data |

---

### Domain Routing Strategy

```
www.shalotrack.com        →  Public Website
api.shalotrack.com        →  ASP.NET Core API
gateway.shalotrack.com    →  Python TCP Gateway
admin.shalotrack.com      →  Admin Portal
dealer.shalotrack.com     →  Dealer Portal
app.shalotrack.com        →  Customer Application Services
```

---

### Future Production Deployment Architecture

As the platform grows, the API infrastructure will scale horizontally for high availability and fault tolerance.

```
Cloudflare
      │
      ▼
Load Balancer
      │
 ┌────┴────┐
 │         │
 ▼         ▼
API #1   API #2
      │
      ▼
Supabase PostgreSQL
```

Benefits: High Availability, Fault Tolerance, Better Performance, Support for Thousands of Devices and Users

---

## Infrastructure Architecture

### Current Infrastructure

```
Cloudflare
      ↓
AWS EC2
      ↓
Supabase PostgreSQL
```

### Future Production Infrastructure

```
Cloudflare
      ↓
Load Balancer
      ↓
API Cluster
      ↓
Gateway Cluster
      ↓
Supabase PostgreSQL
```

Future additions include multiple Gateway nodes, redundancy, automated backups, and failover services.

---

## Scaling Architecture

| Stage | Device Count | Configuration |
|---|---|---|
| Development | 1 Device | Single Gateway, Single API |
| Pilot | 100 Devices | — |
| Commercial Launch | 1,000 Devices | — |
| Growth | 10,000 Devices | Horizontal scaling |
| Enterprise | 25,000+ Devices | Full cluster deployment |

**Scaling Strategy:** Horizontal API Scaling, Multiple Gateway Nodes, Database Optimization, Load Balancing

---

## CI/CD Roadmap

### Current Deployment

```
GitHub → Manual Deployment → AWS EC2
```

### Future Deployment

```
GitHub
   ↓
GitHub Actions
   ↓
Build & Test
   ↓
Deploy
   ↓
AWS EC2
```

This will enable automated deployment pipelines and reduce downtime during updates.

Planned features: Automated Testing, Automated Deployments, Rollback Support, Version Management

---

## Development History

| Day | Achievements |
|---|---|
| Day 01 | Architecture planning, repository planning, infrastructure design |
| Day 02 | ASP.NET Core 8 setup, Entity Framework Core setup |
| Day 03 | Supabase integration, database schema creation, core table creation |
| Day 04 | Gateway architecture finalization, tracking flow design |
| Day 05 | Database persistence, raw packet storage, tracking storage integration |

---

## Development Roadmap

| Phase | Focus |
|---|---|
| Phase 1 | Core Platform Development |
| Phase 2 | Authentication & Authorization |
| Phase 3 | Tracking Services |
| Phase 4 | Customer Applications |
| Phase 5 | Dealer Platform |
| Phase 6 | Analytics & Reporting |
| Phase 7 | Commercial Launch |

---

## Repository Status

**Version:** `v0.2.0`
**Status:** Active Development
**Current Phase:** Core Backend Development

**Next Milestones:**

* Device Services
* Tracking Services
* Current Location Services

---

## Contributors

### Founder & Product Owner

**Nuwan Aloka**

* Product Vision
* Business Operations
* Strategic Direction

### Lead Developer & System Architect

**Swen Jayathunga**

* System Architecture
* Backend Development
* Database Engineering
* Gateway Development
* Infrastructure Design
* API Development

---

## Long-Term Vision

The ShaloTrack API is being designed as the central service layer for a complete fleet intelligence ecosystem.

Future platform capabilities include Real-Time Vehicle Tracking, Fleet Management, Route Optimization, Driver Behaviour Analysis, Fuel Monitoring, Geofencing, Dashcam Integration, IoT Monitoring, Predictive Maintenance, and AI-Powered Fleet Analytics.

The API will remain the core communication layer connecting every service, application, and future integration within the ShaloTrack ecosystem.

---

Built with ❤️ by the ShaloTrack Development Team.
