# ISP Management System 🚀

A production-ready **Multi-Tenant ISP Management RESTful API** built with ASP.NET Core 8 and Clean Architecture.
Designed for ISPs to manage subscribers, subscriptions, payments, and notifications — all isolated per tenant.

---

## 📑 Table of Contents

- [Overview](#-overview)
- [Architecture](#-architecture)
- [Features](#-features)
- [Tech Stack](#-tech-stack)
- [Project Structure](#-project-structure)
- [Getting Started](#-getting-started)
- [API Endpoints](#-api-endpoints)
- [Security](#-security)
- [Logging](#-logging)
- [Testing](#-testing)

---

## 🌐 Overview

ISP Management System is a SaaS platform where multiple ISP agents (tenants) share the same API instance while keeping their data fully isolated.

Each tenant has:

- Its own **subscribers**, **plans**, and **subscriptions**
- Its own **Telegram bot** for notifications
- A **subscription plan** (Free / Basic / Pro) that controls how many subscribers it can have
- A **TenantAdmin** user to manage everything

A **SuperAdmin** oversees all tenants, confirms payments, and manages the platform.

---

## 🏗 Architecture

```
ISP.API            ← Controllers, Middleware, Program.cs
ISP.Application    ← DTOs, Interfaces, Validators, AutoMapper
ISP.Domain         ← Entities, Enums, Domain Interfaces
ISP.Infrastructure ← EF Core, Repositories, Services, Hangfire
ISP.Tests          ← xUnit Unit Tests
```

### Design Patterns

- **Clean Architecture** — strict layer separation
- **Generic Repository + Unit of Work** — consistent data access
- **Multi-Tenancy** — every entity carries a `TenantId`
- **Soft Delete** — data is never permanently lost unless explicitly requested

---

## ✅ Features

### 👥 Tenant Management

- Create tenants with auto-generated Admin account
- Three subscription plans: Free (50 subs), Basic (500 subs), Pro (unlimited)
- Renewal request flow: TenantAdmin requests → SuperAdmin confirms payment
- Activate / Deactivate tenants

### 🔐 Authentication & Authorization

- Custom JWT authentication with Refresh Tokens
- Role-based access: `SuperAdmin`, `TenantAdmin`, `Employee`
- Account lockout after failed login attempts
- Password policy (minimum length, complexity, bcrypt with max 128 chars)
- Refresh token revocation on password change and account deletion

### 👤 Subscriber Management

- Full CRUD with phone number uniqueness validation
- Subscriber limit enforced per tenant plan
- Soft delete with manual cascade to subscriptions
- Telegram account linking
- Permanent delete (SuperAdmin only, after soft delete)

### 📦 Plan Management

- Create and manage internet plans (speed, price, duration)
- Soft delete blocked if active subscriptions exist
- Restore and permanent delete support

### 📋 Subscription Management

- Create, renew, and cancel subscriptions
- Auto status updates: `Active` → `Expiring` → `Expired`
- Cascade soft delete when subscriber is deleted

### 💳 Payment Management

- Cash payment processing with automatic subscription renewal
- Invoice generation (PDF thermal 80mm)
- Invoice numbering with Database-safe sequential counter per tenant per year
- Refund and partial refund support

### 🔔 Notifications

- Telegram notifications for expiry warnings and payment reminders
- Retry mechanism for failed notifications
- Multi-channel support structure (WhatsApp, Email — planned)

### 📊 Reports & Dashboard

- Dashboard summary metrics
- Revenue reports
- Subscription growth reports
- Plan popularity reports
- Expiring soon alerts

### 🕵️ Audit Logging

- Logs all mutating operations (POST, PUT, PATCH, DELETE)
- Sensitive data sanitized: passwords deleted, emails/phones masked
- IP address masked for privacy (e.g. `192.168.1.*`)
- User-agent parsed to readable format (e.g. `Chrome/Windows`)
- Statistics endpoint with full dataset accuracy

### 🔧 Background Jobs (Hangfire)

- Automatic subscription status updates
- Scheduled expiry notification sending
- Maintenance jobs via `MaintenanceController`

---

## 🛠 Tech Stack

| Category        | Technology                             |
| --------------- | -------------------------------------- |
| Framework       | ASP.NET Core 8                         |
| ORM             | Entity Framework Core                  |
| Database        | SQL Server                             |
| Authentication  | Custom JWT + Refresh Tokens            |
| Background Jobs | Hangfire                               |
| Logging         | Serilog (Console + File + Error sinks) |
| Validation      | FluentValidation                       |
| Mapping         | AutoMapper                             |
| PDF Generation  | QuestPDF (Thermal 80mm)                |
| QR Code         | QRCoder                                |
| Testing         | xUnit                                  |
| Architecture    | Clean Architecture                     |

---

## 📁 Project Structure

```
src/
├── ISP.API/
│   ├── Controllers/
│   │   ├── AuthController.cs
│   │   ├── UsersController.cs
│   │   ├── TenantsController.cs
│   │   ├── SubscribersController.cs
│   │   ├── PlansController.cs
│   │   ├── SubscriptionsController.cs
│   │   ├── PaymentsController.cs
│   │   ├── InvoicesController.cs
│   │   ├── ReportsController.cs
│   │   ├── AuditLogsController.cs
│   │   ├── MaintenanceController.cs
│   │   └── TelegramTestController.cs
│   ├── Middleware/
│   │   ├── AuditLoggingMiddleware.cs
│   │   └── ExceptionHandlingMiddleware.cs
│   └── Program.cs
│
├── ISP.Application/
│   ├── DTOs/
│   ├── Helpers/           ← EmailHelper, PhoneHelper, NationalIdHelper
│   ├── Interfaces/
│   ├── Mappings/
│   └── Validators/
│
├── ISP.Domain/
│   ├── Entities/
│   ├── Enums/
│   └── Interfaces/
│
├── ISP.Infrastructure/
│   ├── Data/              ← DbContext, Migrations
│   ├── Repositories/
│   └── Services/
│       ├── AuthService.cs
│       ├── UserService.cs
│       ├── TenantService.cs
│       ├── SubscriberService.cs
│       ├── PlanService.cs
│       ├── SubscriptionService.cs
│       ├── PaymentService.cs
│       ├── InvoiceService.cs
│       ├── NotificationService.cs
│       ├── ReportService.cs
│       └── AuditLogService.cs
│
└── ISP.Tests/
    ├── AuthServiceTests.cs
    └── UserServiceTests.cs
```

---

## 🚀 Getting Started

### Prerequisites

- .NET 8 SDK
- SQL Server
- Telegram Bot Token (optional, for notifications)

### 1. Clone the repository

```bash
git clone https://github.com/Mohammed-gittech/ISP-Management-System
cd ISPManagementSystem
```

### 2. Configure appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=ISPManagement;Trusted_Connection=True;"
  },
  "JwtSettings": {
    "SecretKey": "your-secret-key-here",
    "Issuer": "ISPManagementSystem",
    "Audience": "ISPManagementSystem",
    "ExpiryMinutes": 60,
    "RefreshTokenExpiryDays": 7
  },
  "PasswordPolicy": {
    "MinimumLength": 8,
    "RequireUppercase": true,
    "RequireLowercase": true,
    "RequireDigit": true,
    "RequireSpecialCharacter": true
  }
}
```

### 3. Apply Migrations

```bash
cd src/ISP.API
dotnet ef database update
```

### 4. Run the API

```bash
dotnet run
```

The API will be available at `https://localhost:5001`
Hangfire dashboard: `https://localhost:5001/hangfire`

---

## 📡 API Endpoints

### Auth

| Method | Endpoint            | Description          |
| ------ | ------------------- | -------------------- |
| POST   | `/api/auth/login`   | Login and get JWT    |
| POST   | `/api/auth/refresh` | Refresh access token |
| POST   | `/api/auth/revoke`  | Revoke refresh token |

### Users

| Method | Endpoint                         | Description      |
| ------ | -------------------------------- | ---------------- |
| GET    | `/api/users`                     | Get all users    |
| POST   | `/api/users`                     | Create user      |
| PUT    | `/api/users/{id}`                | Update user      |
| DELETE | `/api/users/{id}`                | Soft delete user |
| POST   | `/api/users/{id}/reset-password` | Reset password   |
| POST   | `/api/users/{id}/assign-role`    | Assign role      |

### Tenants _(SuperAdmin only)_

| Method | Endpoint                            | Description                  |
| ------ | ----------------------------------- | ---------------------------- |
| GET    | `/api/tenants`                      | Get all tenants              |
| POST   | `/api/tenants`                      | Create tenant                |
| PUT    | `/api/tenants/{id}`                 | Update tenant                |
| POST   | `/api/tenants/{id}/activate`        | Activate tenant              |
| POST   | `/api/tenants/{id}/deactivate`      | Deactivate tenant            |
| POST   | `/api/tenants/{id}/confirm-payment` | Confirm subscription payment |
| GET    | `/api/tenants/pending-renewals`     | Get pending renewal requests |

### Subscribers

| Method | Endpoint                          | Description            |
| ------ | --------------------------------- | ---------------------- |
| GET    | `/api/subscribers`                | Get all subscribers    |
| POST   | `/api/subscribers`                | Create subscriber      |
| PUT    | `/api/subscribers/{id}`           | Update subscriber      |
| DELETE | `/api/subscribers/{id}`           | Soft delete subscriber |
| POST   | `/api/subscribers/{id}/restore`   | Restore subscriber     |
| DELETE | `/api/subscribers/{id}/permanent` | Permanent delete       |

### Plans

| Method | Endpoint                  | Description      |
| ------ | ------------------------- | ---------------- |
| GET    | `/api/plans`              | Get all plans    |
| GET    | `/api/plans/active`       | Get active plans |
| POST   | `/api/plans`              | Create plan      |
| PUT    | `/api/plans/{id}`         | Update plan      |
| DELETE | `/api/plans/{id}`         | Soft delete plan |
| POST   | `/api/plans/{id}/restore` | Restore plan     |

### Subscriptions

| Method | Endpoint                        | Description                |
| ------ | ------------------------------- | -------------------------- |
| GET    | `/api/subscriptions`            | Get all subscriptions      |
| POST   | `/api/subscriptions`            | Create subscription        |
| POST   | `/api/subscriptions/{id}/renew` | Renew subscription         |
| DELETE | `/api/subscriptions/{id}`       | Cancel subscription        |
| GET    | `/api/subscriptions/expiring`   | Get expiring subscriptions |
| GET    | `/api/subscriptions/expired`    | Get expired subscriptions  |

### Payments

| Method | Endpoint                    | Description          |
| ------ | --------------------------- | -------------------- |
| GET    | `/api/payments`             | Get all payments     |
| POST   | `/api/payments/cash`        | Process cash payment |
| POST   | `/api/payments/{id}/refund` | Refund payment       |
| GET    | `/api/payments/stats`       | Payment statistics   |

### Invoices

| Method | Endpoint                    | Description                 |
| ------ | --------------------------- | --------------------------- |
| GET    | `/api/invoices/{id}`        | Get invoice                 |
| GET    | `/api/invoices/{id}/pdf`    | Download PDF (80mm thermal) |
| POST   | `/api/invoices/{id}/cancel` | Cancel invoice              |

### Reports

| Method | Endpoint                       | Description            |
| ------ | ------------------------------ | ---------------------- |
| GET    | `/api/reports/dashboard`       | Dashboard summary      |
| GET    | `/api/reports/revenue`         | Revenue report         |
| GET    | `/api/reports/growth`          | Growth report          |
| GET    | `/api/reports/expiring-soon`   | Expiring soon report   |
| GET    | `/api/reports/plan-popularity` | Plan popularity report |

### Audit Logs _(SuperAdmin / TenantAdmin)_

| Method | Endpoint                    | Description                 |
| ------ | --------------------------- | --------------------------- |
| GET    | `/api/auditlogs`            | Get audit logs (filterable) |
| GET    | `/api/auditlogs/statistics` | Audit statistics            |

---

## 🔒 Security

| Feature            | Details                                               |
| ------------------ | ----------------------------------------------------- |
| JWT Authentication | Access token + Refresh token rotation                 |
| Role Authorization | SuperAdmin, TenantAdmin, Employee                     |
| Account Lockout    | Configurable max attempts + lockout duration          |
| Password Policy    | Complexity rules enforced via FluentValidation        |
| Rate Limiting      | Sliding window on auth endpoints, fixed window global |
| CORS               | Configurable per environment                          |
| Audit Logging      | All mutations logged with masked sensitive data       |
| Token Revocation   | On password change and account deletion               |

---

## 📋 Logging

Serilog is configured with three outputs:

| Sink      | Path               | Level        | Retention |
| --------- | ------------------ | ------------ | --------- |
| Console   | —                  | Information+ | —         |
| App log   | `logs/app-.txt`    | Information+ | 30 days   |
| Error log | `logs/errors-.txt` | Error+       | 90 days   |

Sensitive data handling in logs:

- **Deleted**: passwords, tokens, API keys
- **Masked**: emails (`a***d@gmail.com`), phones (`079*****67`), national IDs
- **Stored as-is**: usernames, roles, IDs

---

## 🧪 Testing

```bash
cd src/ISP.Tests
dotnet test
```

| Test Suite  | Count   | Coverage                                 |
| ----------- | ------- | ---------------------------------------- |
| AuthService | 85      | Login, Refresh, Lockout, Password Policy |
| UserService | 48      | CRUD, Role, Password, Soft Delete        |
| **Total**   | **133** |                                          |
