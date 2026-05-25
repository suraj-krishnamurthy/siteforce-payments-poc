# SiteForce Smart Payment Disbursement — POC

A proof-of-concept demonstrating the core payment disbursement workflow:
**Excel upload → Pay calculation (rules engine) → Dashboard + Batch approve → Full audit trail**

## Architecture Style

**Modular Monolith** with **Microkernel (Plugin) Architecture** for rule-based execution.

- **Modular Monolith**: The application is deployed as a single unit but internally organized into cohesive modules (Ingestion, Calculation, Disputes, Audit, Rules) with clear boundaries and separated concerns via service interfaces.
- **Microkernel / Plugin Pattern**: The payment calculation engine uses a plugin-based rule execution model (`IRulePlugin`). Each rule (BasePay, AdvanceDeduction, SiteAllowance, DisputeThreshold) is an independent plugin registered with the `RuleEngine`. New rules can be added without modifying existing calculation logic — simply implement `IRulePlugin` and register it.

This hybrid approach provides:
- Fast development velocity of a monolith
- Extensibility of a plugin system for business rules
- Clear path to decomposition if needed in production

## Architecture Decisions

| Decision | Rationale |
|----------|-----------|
| **Modular Monolith + Microkernel** | Monolith for simplicity and fast iteration; Microkernel (plugin) pattern for the rule engine enabling extensible, swappable calculation rules without modifying core logic |
| No authentication | POC uses hardcoded `X-User-Name` header; production inherits SiteForce JWT |
| Single-tenant | No tenant isolation in POC |
| Config-driven rules | Global defaults in appsettings; per-site overrides stored in DB |
| ClosedXML (MIT) | Avoids EPPlus license complexity |
| EF Core auto-migrate | Convenience for POC; production uses explicit migration deployment |
| SQL DENY + Trigger | Defense-in-depth: audit immutability enforced at DB layer |
| In-memory DB for tests | Fast, isolated test runs without external dependencies |

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend | C# / ASP.NET Core 8 Web API |
| Frontend | React 18 + TypeScript + Vite + TailwindCSS |
| Database | SQL Server (SQL Express or Docker) |
| Excel Parsing | ClosedXML |
| ORM | Entity Framework Core 8 |

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 18+](https://nodejs.org/)
- SQL Server (SQL Express locally, or [Docker Desktop](https://www.docker.com/products/docker-desktop/))

## Quick Start

### 1. Database Setup

The default connection string in `appsettings.json` uses SQL Express on `localhost\SQLEXPRESS`. Update the connection string if using a different SQL Server instance.

### 2. Run the Backend API

```bash
cd src/SiteForce.PaymentApi
dotnet run
```

API will be available at `http://localhost:5062`. Swagger UI at `http://localhost:5062/swagger`.

EF Core migrations run automatically on startup — the database and tables are created for you.

### 3. Run the Frontend

```bash
cd frontend
npm install
npm run dev
```

Frontend available at `http://localhost:5173`. API calls are proxied to the backend.

### 4. Apply Audit Immutability (Optional)

After the database is created, run the SQL script to enforce append-only on the audit table:

```bash
sqlcmd -S localhost\SQLEXPRESS -U sa -P "SQLExpress@123" -d SiteForcePayments -i src/SiteForce.PaymentApi/Data/Scripts/deny_audit_mutations.sql
```

## Running Tests

```bash
dotnet test
```

The integration tests upload sample Excel files, calculate payments, approve batches, raise/resolve disputes, and verify the audit trail end-to-end (uses in-memory database).

## POC Functional Coverage

### 1. Excel Ingestion (Month-End Upload)
- `POST /api/upload` — Upload `.xlsx` with columns: WorkerId, Site, DaysPresent, DayRate
- Validates schema, data types, and values
- Returns error report for rejected rows
- Audit event: `attendance_uploaded`

### 2. Pay Calculation (Rule Engine)
- `POST /api/payments/calculate` — Triggers calculation for an upload
- Rules executed in order:
  1. **BasePay**: `DaysPresent × DayRate`
  2. **AdvanceDeduction**: Subtract configurable amount (default: ₹0, configurable per-site)
  3. **SiteAllowance**: Add percentage of gross (default: 10%, configurable per-site)
  4. **DisputeThreshold**: Flag if net < threshold (default: ₹20,358, configurable per-site)
- Groups results by site into payment batches
- Stores full breakdown as JSON per worker
- Audit event: `payment_calculated`

### 3. Dashboard UI
- Per-worker payment grid with: WorkerId, Site, Gross, Deductions, Allowances, Net, Status badge
- Filters by site and status (Ready / Disputed / Pending)
- Per-site batch cards with total amount + **one-click "Approve Batch"**
- Contractors can raise concerns: select payment, choose reason (Attendance/Deduction/Rate), describe issue

### 4. Configurable Rules
- Per-site rule configuration via API and Rules UI page
- Override advance deduction, allowance percentage, and dispute threshold per site
- Falls back to global defaults when no site-specific rule exists

### 5. Audit Trail
- Every action (upload, calculate, approve, dispute raise/resolve) logged automatically
- Timeline view with expandable JSON payload
- DB-level enforcement: `DENY UPDATE/DELETE` + trigger on AuditEvents table
- `GET /api/audit` — Query with filters (entityType, entityId, date range)

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/upload` | Upload attendance Excel |
| POST | `/api/payments/calculate` | Trigger pay calculation |
| GET | `/api/payments` | List payment lines (paginated, filterable) |
| GET | `/api/batches` | List payment batches |
| POST | `/api/batches/{id}/approve` | One-click batch approval |
| POST | `/api/disputes` | Raise a concern |
| GET | `/api/disputes` | List disputes |
| POST | `/api/disputes/{id}/resolve` | Resolve a dispute |
| GET | `/api/rules` | List all site rule configurations |
| GET | `/api/rules/defaults` | Get global default rule settings |
| GET | `/api/rules/{siteName}` | Get rule config for a specific site |
| POST | `/api/rules` | Create or update site rule config |
| DELETE | `/api/rules/{siteName}` | Delete site rule config (revert to defaults) |
| GET | `/api/audit` | Query audit trail |

## Configuration

Rule thresholds are in `appsettings.json` → `RuleSettings`:

```json
{
  "RuleSettings": {
    "AdvanceDeductionAmount": 0,
    "DefaultSiteAllowancePercent": 10,
    "DisputeThresholdAmount": 20358
  }
}
```

These serve as global defaults. Per-site overrides can be configured via the Rules API.

## Sample Test Data

The integration tests use workers like:

| WorkerId | Site | DaysPresent | DayRate | Expected Gross | Allowance (10%) | Expected Net | Status |
|----------|------|:-----------:|:-------:|:--------------:|:---------------:|:------------:|--------|
| W010 | CalcSite | 22 | ₹1,000 | ₹22,000 | ₹2,200 | ₹24,200 | Ready |
| W011 | CalcSite | 5 | ₹500 | ₹2,500 | ₹250 | ₹2,750 | Disputed (below ₹20,358) |

## Project Structure

```
SiteForce-POC/
├── SiteForce.sln
├── src/SiteForce.PaymentApi/
│   ├── Controllers/         # API endpoints (Upload, Payments, Batches, Disputes, Rules, Audit)
│   ├── Data/
│   │   ├── Entities/        # EF Core entity classes
│   │   ├── Scripts/         # SQL scripts (audit immutability)
│   │   └── PaymentDbContext.cs
│   ├── DTOs/                # Request/Response models
│   ├── Rules/               # Rule engine (IRulePlugin, RuleEngine, DbRuleConfigProvider, 4 rules)
│   ├── Services/            # Business logic (Ingestion, Calculation, Dispute, Audit)
│   ├── Program.cs           # App entry + DI config
│   └── appsettings.json     # Configuration
├── tests/SiteForce.PaymentApi.Tests/
│   ├── PaymentFlowTests.cs          # Unit/integration tests
│   ├── EndToEndFlowTests.cs         # Full E2E workflow tests
│   └── TestWebApplicationFactory.cs # In-memory test server setup
└── frontend/
    ├── src/
    │   ├── pages/           # Upload, Dashboard, Disputes, Rules, Audit
    │   ├── layout/          # App shell with sidebar
    │   └── api/             # Axios client
    ├── package.json
    └── vite.config.ts
```


