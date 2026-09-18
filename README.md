# Property Rental Management System

A full-stack ASP.NET Core MVC application for a property management company: property managers maintain properties and units, applicants apply for available units through a multi-step application, and managers review, approve, return, or deny those applications.

## Tech stack

- **.NET 10** / ASP.NET Core MVC (Razor views, partial views, view components)
- **Entity Framework Core** (code-first migrations) with **SQL Server / SQL Server Express (LocalDB)**
- **ASP.NET Core Identity** for authentication and role-based authorization (cookie-based)
- **Bogus** for database seeding
- **xUnit** + **FluentAssertions** + SQLite in-memory for unit tests

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server LocalDB - or point the connection string at any SQL Server instance you have

## Running the app

```bash
dotnet run --project src/RentalManagement.Web/RentalManagement.Web.csproj
```

Then open `http://localhost:5161`.

On startup, the app automatically:
1. Applies EF Core migrations, creating the database if it doesn't exist.
2. Seeds it idempotently (safe to restart — it only inserts what's missing) with:
   - The `Applicant` and `PropertyManager` roles
   - 2 property managers, 5 applicants
   - 5 unit types (`Studio`, `1/2/3 Bedroom`, and `Penthouse` seeded **inactive**, to exercise the active/inactive unit-type rule)
   - 3 properties with 4–6 units each, via Bogus
   - One rental application in **every** status (Draft, Submitted, Returned, Approved, Denied, Withdrawn), so each state is visible without manually walking one through the whole lifecycle

### Seeded accounts

All seeded accounts use the password **`Passw0rd!`**.

| Role | Emails |
|---|---|
| Property Manager | `manager1@rentalmanagement.test`, `manager2@rentalmanagement.test` |
| Applicant | `applicant1@rentalmanagement.test` … `applicant5@rentalmanagement.test` |

You can also register a new account from the app — sign-up lets you pick either role.

### Connection string

The default connection string in `src/RentalManagement.Web/appsettings.json` targets LocalDB:

```
Server=(localdb)\mssqllocaldb;Database=RentalManagementDb;Trusted_Connection=True;MultipleActiveResultSets=true
```

If you're using a different SQL Server instance (e.g. SQL Server Express), update `ConnectionStrings:DefaultConnection` in `appsettings.json` or `appsettings.Development.json` accordingly.

### Resetting the database

To wipe and re-seed from scratch:

```bash
dotnet ef database drop --force --project src/RentalManagement.Infrastructure --startup-project src/RentalManagement.Web
```

Then run the app again — it will re-migrate and re-seed automatically.

## Running tests

```bash
dotnet test
```

Covers the business logic in `ApplicationService` and `UnitService`: status transitions, the "unit already has an active lease" guard at both submit and approve, unit-type active/inactive selectability, ownership checks, and the optimistic-concurrency guards (including a genuine two-DbContext race simulation for the "second save rejected as stale" scenario).

## Solution structure

```
src/
├── RentalManagement.Domain          entities, enums, service interfaces, domain exceptions
├── RentalManagement.Infrastructure  EF Core DbContext, migrations, Identity, Bogus seeder, service implementations
└── RentalManagement.Web             controllers, Razor views, view components, view models
tests/
└── RentalManagement.Tests           xUnit tests for the service layer
```

## Key design decisions

- **No per-manager property ownership.** Any user in the `PropertyManager` role can manage any property and review any application.
- **The application wizard persists per section**, not all at once. Clicking Continue validates and saves *only* the current section; Back is a plain navigation link (no save). Progress is never lost on refresh or browser close, since each step is already committed by the time you move to the next one.
- **Two different concurrency mechanisms** are used on purpose:
  - `ApplicantInfo`/`RentalApplication` use SQL Server's native `rowversion` column (`RowVersion`) for optimistic concurrency, since each is a single row.
  - Residence History is a *collection*, not a single row, so it instead uses a manually incremented `int ResidenceHistoryVersion` on the parent `RentalApplication`, configured as an EF Core concurrency token. SQL Server only allows one native rowversion column per table, so the collection-level section needed a different mechanism to get the same "stale save is rejected" behavior.
- **The lease-conflict guard is checked twice** — once at Submit and once at Approve — since two applications can be open for the same unit simultaneously; approving one must not silently let the other slip through afterward.
- **Unit type Active/Inactive** is enforced server-side in `UnitService`: a unit already assigned an inactive type keeps it, but switching *to* an inactive type (on create or update) is rejected.